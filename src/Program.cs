using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace SerenityRowDisplayNameUpdater;

public class Program
{
    private static readonly Dictionary<string, (string ConnectionString, string ProviderName)> _connectionConfigs =
        new();

    public static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("使用方法: serupd.exe <目錄路徑> [appsettings.json路徑]");
            Console.WriteLine("範例: serupd.exe C:\\MyProject\\Modules");
            Console.WriteLine("範例: serupd.exe C:\\MyProject\\Modules C:\\MyProject\\appsettings.json");
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

                if (string.IsNullOrEmpty(connectionString)) continue;
                _connectionConfigs[connection.Name] = (connectionString, providerName);
                Console.WriteLine($"載入連接字串: {connection.Name} (Provider: {providerName ?? "自動偵測"})");
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

        if (!_connectionConfigs.TryGetValue(classInfo.ConnectionKey, out var value))
        {
            Console.WriteLine($"  跳過: 找不到連接字串 '{classInfo.ConnectionKey}'");
            return;
        }

        try
        {
            var (connectionString, providerName) = value;

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

    private static (string Schema, string Table) ParseSqlserverTableName(string tableName)
    {
        // 移除引號
        var cleanName = tableName.Replace("\"", "").Replace("'", "").Replace("`", "").Replace("[", "")
            .Replace("]", "");
        ;

        // 檢查是否包含 schema
        if (cleanName.Contains('.'))
        {
            var parts = cleanName.Split('.');
            if (parts.Length == 2)
            {
                return (parts[0], parts[1]);
            }
        }

        // 如果沒有指定 schema，預設使用 public
        return ("public", cleanName);
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
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var (schema, table) = ParsePostgreSqlTableName(tableName);

        const string sql = """
                               SELECT 
                                   c.COLUMN_NAME,
                                   ep.value AS COLUMN_COMMENT
                               FROM INFORMATION_SCHEMA.COLUMNS c
                               LEFT JOIN sys.extended_properties ep ON 
                                   ep.major_id = OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME) AND
                                   ep.minor_id = c.ORDINAL_POSITION AND
                                   ep.name = 'MS_Description'
                               WHERE c.TABLE_SCHEMA = @SchemaName AND c.TABLE_NAME = @TableName
                               AND ep.value IS NOT NULL
                           """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@TableName", table);
        command.Parameters.AddWithValue("@SchemaName", schema);

        await using var reader = await command.ExecuteReaderAsync();
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
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        // 解析 PostgreSQL 的 schema 和 table name
        var (schema, table) = ParsePostgreSqlTableName(tableName);

        const string sql = """
                               SELECT column_name,
                                      col_description(pgc.oid, ordinal_position) as column_comment
                               FROM information_schema.columns isc
                                        JOIN pg_class pgc ON pgc.relname = isc.table_name
                               WHERE isc.table_name = @table_name
                                 and table_schema = @table_schema
                                 AND col_description(pgc.oid, ordinal_position) IS NOT NULL
                           """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("table_name", table);
        command.Parameters.AddWithValue("table_schema", schema);

        await using var reader = await command.ExecuteReaderAsync();
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