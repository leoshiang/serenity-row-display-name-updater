using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Npgsql;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SerenityRowDisplayNameUpdater
{
    public class Program
    {
        private static readonly Dictionary<string, (string ConnectionString, string ProviderName)> _connectionConfigs =
            new();

        public static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("使用方法: SerenityRowDisplayNameUpdater.exe <目錄路徑> [appsettings.json路徑]");
                Console.WriteLine("範例: SerenityRowDisplayNameUpdater.exe C:\\MyProject\\Modules");
                Console.WriteLine(
                    "範例: RowDisplayNameUpdater.exe C:\\MyProject\\Modules C:\\MyProject\\appsettings.json");
                return;
            }

            var targetDirectory = args[0];
            var appSettingsPath = args.Length > 1
                ? args[1]
                : Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");

            if (!Directory.Exists(targetDirectory))
            {
                Console.WriteLine($"錯誤: 目錄 '{targetDirectory}' 不存在");
                return;
            }

            if (!File.Exists(appSettingsPath))
            {
                Console.WriteLine($"錯誤: appsettings.json 檔案 '{appSettingsPath}' 不存在");
                return;
            }

            try
            {
                // 載入連接字串
                await LoadConnectionStrings(appSettingsPath);

                // 找出所有 *Row.cs 檔案
                var rowFiles = Directory.GetFiles(targetDirectory, "*Row.cs", SearchOption.AllDirectories)
                    .Where(f => !Path.GetFileName(f).StartsWith("LoggingRow")) // 排除 LoggingRow
                    .ToList();

                Console.WriteLine($"找到 {rowFiles.Count} 個 Row 檔案");

                foreach (var filePath in rowFiles)
                {
                    await ProcessRowFile(filePath);
                }

                Console.WriteLine("處理完成!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"錯誤: {ex.Message}");
                Console.WriteLine($"詳細錯誤: {ex}");
            }
        }

        private static async Task LoadConnectionStrings(string appSettingsPath)
        {
            var json = await File.ReadAllTextAsync(appSettingsPath);
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.TryGetProperty("Data", out var dataElement))
            {
                foreach (var connection in dataElement.EnumerateObject())
                {
                    string connectionString = null;
                    string providerName = null;

                    if (connection.Value.TryGetProperty("ConnectionString", out var connectionStringElement))
                    {
                        connectionString = connectionStringElement.GetString();
                    }

                    if (connection.Value.TryGetProperty("ProviderName", out var providerNameElement))
                    {
                        providerName = providerNameElement.GetString();
                    }

                    if (!string.IsNullOrEmpty(connectionString))
                    {
                        _connectionConfigs[connection.Name] = (connectionString, providerName);
                        Console.WriteLine($"載入連接字串: {connection.Name} (Provider: {providerName ?? "自動偵測"})");
                    }
                }
            }
        }

        private static async Task ProcessRowFile(string filePath)
        {
            Console.WriteLine($"處理檔案: {Path.GetFileName(filePath)}");

            var content = await File.ReadAllTextAsync(filePath);

            // 使用 Roslyn 解析程式碼
            var analyzer = new CSharpCodeAnalyzer();
            var classInfo = analyzer.AnalyzeRowClass(content);

            if (classInfo == null)
            {
                Console.WriteLine($"  跳過: 無法解析類別");
                return;
            }

            if (string.IsNullOrEmpty(classInfo.ConnectionKey) || string.IsNullOrEmpty(classInfo.TableName))
            {
                Console.WriteLine($"  跳過: 無法找到 ConnectionKey 或 TableName");
                return;
            }

            if (!_connectionConfigs.ContainsKey(classInfo.ConnectionKey))
            {
                Console.WriteLine($"  跳過: 找不到連接字串 '{classInfo.ConnectionKey}'");
                return;
            }

            try
            {
                var (connectionString, providerName) = _connectionConfigs[classInfo.ConnectionKey];

                // 取得資料庫欄位註解
                var columnComments = await GetColumnComments(connectionString, providerName, classInfo.TableName);

                if (columnComments.Count == 0)
                {
                    Console.WriteLine($"  跳過: 資料表 '{classInfo.TableName}' 沒有欄位註解");
                    return;
                }

                // 更新 DisplayName
                var updatedContent = analyzer.UpdateDisplayNames(content, classInfo.Properties, columnComments);

                if (content != updatedContent)
                {
                    await File.WriteAllTextAsync(filePath, updatedContent);
                    Console.WriteLine($"  已更新: {columnComments.Count} 個 DisplayName");
                }
                else
                {
                    Console.WriteLine($"  無需更新");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  錯誤: {ex.Message}");
            }
        }

        private static async Task<Dictionary<string, string>> GetColumnComments(string connectionString,
            string providerName, string tableName)
        {
            var comments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 優先使用 ProviderName，如果沒有則從連接字串推斷
            var provider = !string.IsNullOrEmpty(providerName)
                ? MapProviderName(providerName)
                : DetectDatabaseProvider(connectionString);

            switch (provider.ToLower())
            {
                case "sqlserver":
                    await GetSqlServerColumnComments(connectionString, tableName, comments);
                    break;
                case "postgresql":
                    await GetPostgreSqlColumnComments(connectionString, tableName, comments);
                    break;
                case "sqlite":
                    // SQLite 不支援欄位註解
                    Console.WriteLine("  SQLite 不支援欄位註解");
                    break;
                default:
                    Console.WriteLine($"  不支援的資料庫類型: {provider}");
                    break;
            }

            return comments;
        }

        private static string MapProviderName(string providerName)
        {
            return providerName?.ToLower() switch
            {
                "npgsql" => "postgresql",
                "system.data.sqlclient" or "microsoft.data.sqlclient" => "sqlserver",
                "microsoft.data.sqlite" or "system.data.sqlite" => "sqlite",
                "mysql.data.mysqlclient" or "mysqlconnector" => "mysql",
                "oracle.manageddataaccess.client" => "oracle",
                _ => providerName?.ToLower() ?? "unknown"
            };
        }

        private static string DetectDatabaseProvider(string connectionString)
        {
            var lower = connectionString.ToLower();

            if (lower.Contains("server=") || lower.Contains("data source=") &&
                (lower.Contains("database=") || lower.Contains("initial catalog=")))
                return "sqlserver";

            if (lower.Contains("host=") || lower.Contains("server=") && lower.Contains("database="))
                return "postgresql";

            if (lower.Contains(".db") || lower.Contains(".sqlite") || lower.Contains("data source=") &&
                !lower.Contains("server="))
                return "sqlite";

            return "unknown";
        }

        private static (string Schema, string Table) ParsePostgreSqlTableName(string tableName)
        {
            // 移除引號
            var cleanName = tableName.Replace("\"", "").Replace("'", "").Replace("`", "").Replace("[", "")
                .Replace("]", "");

            // 檢查是否包含 schema
            if (cleanName.Contains('.'))
            {
                var parts = cleanName.Split('.');
                if (parts.Length == 2)
                {
                    return (parts[0].ToLower(), parts[1].ToLower());
                }
            }

            // 如果沒有指定 schema，預設使用 public
            return ("public", cleanName.ToLower());
        }

        private static async Task GetSqlServerColumnComments(string connectionString, string tableName,
            Dictionary<string, string> comments)
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // SQL Server 使用方括號，但在查詢 INFORMATION_SCHEMA 時不需要引號
            var cleanTableName = tableName.Replace("\"", "").Replace("'", "").Replace("`", "").Replace("[", "")
                .Replace("]", "");

            var sql = @"
                SELECT 
                    c.COLUMN_NAME,
                    ep.value AS COLUMN_COMMENT
                FROM INFORMATION_SCHEMA.COLUMNS c
                LEFT JOIN sys.extended_properties ep ON 
                    ep.major_id = OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME) AND
                    ep.minor_id = c.ORDINAL_POSITION AND
                    ep.name = 'MS_Description'
                WHERE c.TABLE_NAME = @TableName
                AND ep.value IS NOT NULL";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@TableName", cleanTableName);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString("COLUMN_NAME");
                var comment = reader.GetString("COLUMN_COMMENT");
                if (!string.IsNullOrWhiteSpace(comment))
                {
                    comments[columnName] = comment.Trim();
                }
            }
        }

        private static async Task GetPostgreSqlColumnComments(string connectionString, string tableName,
            Dictionary<string, string> comments)
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // 解析 PostgreSQL 的 schema 和 table name
            var (schema, table) = ParsePostgreSqlTableName(tableName);

            var sql = @"
                SELECT column_name,
                       col_description(pgc.oid, ordinal_position) as column_comment
                FROM information_schema.columns isc
                         JOIN pg_class pgc ON pgc.relname = isc.table_name
                WHERE isc.table_name = @table_name
                  and table_schema = @table_schema
                  AND col_description(pgc.oid, ordinal_position) IS NOT NULL";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("table_name", table);
            command.Parameters.AddWithValue("table_schema", schema);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString("column_name");
                var comment = reader.GetString("column_comment");
                if (!string.IsNullOrWhiteSpace(comment))
                {
                    comments[columnName] = comment.Trim();
                }
            }
        }
    }

    public class CSharpCodeAnalyzer
    {
        public RowClassInfo AnalyzeRowClass(string sourceCode)
        {
            try
            {
                var tree = CSharpSyntaxTree.ParseText(sourceCode);
                var root = tree.GetCompilationUnitRoot();

                var classDeclaration = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text.EndsWith("Row"));

                if (classDeclaration == null)
                    return null;

                var classInfo = new RowClassInfo
                {
                    ClassName = classDeclaration.Identifier.Text,
                    Properties = new List<PropertyInfo>()
                };

                // 解析類別屬性以取得 ConnectionKey 和 TableName
                ParseClassAttributes(classDeclaration, classInfo);

                // 解析屬性
                var properties = classDeclaration.DescendantNodes()
                    .OfType<PropertyDeclarationSyntax>();

                foreach (var property in properties)
                {
                    var propertyInfo = ParseProperty(property, tree);
                    if (propertyInfo != null)
                    {
                        classInfo.Properties.Add(propertyInfo);
                    }
                }

                return classInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解析程式碼時發生錯誤: {ex.Message}");
                return null;
            }
        }

        private void ParseClassAttributes(ClassDeclarationSyntax classDeclaration, RowClassInfo classInfo)
        {
            foreach (var attributeList in classDeclaration.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var attributeName = attribute.Name.ToString();

                    if (attributeName == "ConnectionKey")
                    {
                        classInfo.ConnectionKey = GetAttributeStringArgument(attribute);
                    }
                    else if (attributeName == "TableName")
                    {
                        classInfo.TableName = GetAttributeStringArgument(attribute);
                    }
                }
            }
        }

        private PropertyInfo ParseProperty(PropertyDeclarationSyntax property, SyntaxTree tree)
        {
            var propertyInfo = new PropertyInfo
            {
                PropertyName = property.Identifier.Text,
                StartLine = property.GetLocation().GetLineSpan().StartLinePosition.Line,
                EndLine = property.GetLocation().GetLineSpan().EndLinePosition.Line
            };

            // 解析屬性的 Attributes
            foreach (var attributeList in property.AttributeLists)
            {
                var listSpan = attributeList.GetLocation().GetLineSpan();

                foreach (var attribute in attributeList.Attributes)
                {
                    var attributeName = attribute.Name.ToString();
                    var attributeSpan = attribute.GetLocation().GetLineSpan();

                    if (attributeName == "DisplayName")
                    {
                        propertyInfo.DisplayName = GetAttributeStringArgument(attribute);
                        propertyInfo.DisplayNameLine = attributeSpan.StartLinePosition.Line;
                        propertyInfo.DisplayNameAttribute = attribute;
                    }
                    else if (attributeName == "Column")
                    {
                        propertyInfo.ColumnName = GetAttributeStringArgument(attribute);
                        propertyInfo.ColumnLine = attributeSpan.StartLinePosition.Line;
                        propertyInfo.ColumnAttribute = attribute;
                    }
                }
            }

            return propertyInfo;
        }

        private string GetAttributeStringArgument(AttributeSyntax attribute)
        {
            var argument = attribute.ArgumentList?.Arguments.FirstOrDefault();
            if (argument?.Expression is LiteralExpressionSyntax literal)
            {
                return literal.Token.ValueText;
            }

            return null;
        }

        public string UpdateDisplayNames(string sourceCode, List<PropertyInfo> properties,
            Dictionary<string, string> columnComments)
        {
            var lines = sourceCode.Split('\n').ToList();
            var insertedLines = 0; // 追蹤插入的行數以調整後續行號

            foreach (var property in properties.Where(p => !string.IsNullOrEmpty(p.ColumnName)))
            {
                if (!columnComments.ContainsKey(property.ColumnName))
                    continue;

                var newComment = columnComments[property.ColumnName];

                if (property.DisplayNameLine >= 0)
                {
                    // 更新現有的 DisplayName
                    var actualLine = property.DisplayNameLine + insertedLines;
                    var currentLine = lines[actualLine];

                    // 使用正規表達式替換 DisplayName 值
                    var displayNameRegex = new Regex(@"DisplayName\s*\(\s*""[^""]*""\s*\)");
                    if (displayNameRegex.IsMatch(currentLine))
                    {
                        lines[actualLine] = displayNameRegex.Replace(currentLine, $"DisplayName(\"{newComment}\")");
                    }
                }
                else if (property.ColumnLine >= 0)
                {
                    // 在 Column 屬性前面插入新的 DisplayName
                    var actualLine = property.ColumnLine + insertedLines;
                    var columnLine = lines[actualLine];

                    // 取得縮排
                    var indent = GetLineIndentation(columnLine);
                    var newDisplayNameLine = $"{indent}[DisplayName(\"{newComment}\")]";

                    lines.Insert(actualLine, newDisplayNameLine);
                    insertedLines++;
                }
            }

            return string.Join('\n', lines);
        }

        private string GetLineIndentation(string line)
        {
            var indentCount = 0;
            foreach (var c in line)
            {
                if (c == ' ' || c == '\t')
                    indentCount++;
                else
                    break;
            }

            return line.Substring(0, indentCount);
        }
    }

    public class RowClassInfo
    {
        public string ClassName { get; set; }
        public string ConnectionKey { get; set; }
        public string TableName { get; set; }
        public List<PropertyInfo> Properties { get; set; }
    }

    public class PropertyInfo
    {
        public string PropertyName { get; set; }
        public string ColumnName { get; set; }
        public string DisplayName { get; set; }
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public int DisplayNameLine { get; set; } = -1;
        public int ColumnLine { get; set; } = -1;
        public AttributeSyntax DisplayNameAttribute { get; set; }
        public AttributeSyntax ColumnAttribute { get; set; }
    }
}