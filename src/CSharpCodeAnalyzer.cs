// CSharpCodeAnalyzer.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public class CSharpCodeAnalyzer
{
    public RowClassInfo AnalyzeRowClass(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var connectionKey = MatchAttributeValue(content, "ConnectionKey");
        var tableName = MatchAttributeValue(content, "TableName");

        // 找主要的 Row 類別，排除 RowFields
        var mainRowClassRegex = new Regex(
            @"(?<attrs>(?:^\s*\[[^\]]+\]\s*\r?\n?)*)^(?<decl>\s*(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*Row)(?!\s*:\s*RowFieldsBase)\s*[^{]*\{)",
            RegexOptions.Multiline);

        var rowClassMatch = mainRowClassRegex.Match(content);

        // 如果找不到主要 Row 類別，就找任何非 RowFields 的類別
        if (!rowClassMatch.Success)
        {
            var anyClassRegex = new Regex(
                @"(?<attrs>(?:^\s*\[[^\]]+\]\s*\r?\n?)*)^(?<decl>\s*(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)(?!.*RowFields)(?!\s*:\s*RowFieldsBase)\s*[^{]*\{)",
                RegexOptions.Multiline);
            var anyClassMatch = anyClassRegex.Match(content);
            if (!anyClassMatch.Success) return null;
        }

        // 解析屬性與欄位對應
        var properties = ParseProperties(content);

        return new RowClassInfo
        {
            ConnectionKey = connectionKey,
            TableName = tableName,
            Properties = properties
        };
    }

    public string UpdateDisplayNames(string content, List<RowPropertyInfo> properties,
        Dictionary<string, string> columnComments)
    {
        if (string.IsNullOrWhiteSpace(content) || properties == null || properties.Count == 0 || columnComments == null)
            return content;

        var sb = new StringBuilder(content);
        // 為避免因索引偏移，倒序處理出現位置（我們用正則配合每次重建內容，這裡簡化為基於屬性名搜尋）
        foreach (var prop in properties)
        {
            var columnKey = columnComments.Keys.FirstOrDefault(k =>
                string.Equals(k, prop.ColumnName ?? prop.PropertyName, StringComparison.OrdinalIgnoreCase));

            if (columnKey == null) continue;
            var display = columnComments[columnKey];
            if (string.IsNullOrWhiteSpace(display)) continue;

            sb = new StringBuilder(UpdateSinglePropertyDisplayName(sb.ToString(), prop, display));
        }

        return sb.ToString();
    }

    private string UpdateSinglePropertyDisplayName(string content, RowPropertyInfo prop, string display)
    {
        // 目標：
        // 在 property 定義之前插入或替換 [DisplayName("...")]
        // 匹配 property 的起始行與前置屬性塊
        var propPattern =
            $@"(?<attrs>(?:^\s*\[[^\]]+\]\s*\r?\n?)*)^(?<decl>\s*(?:public|protected|internal)\s+[^\n\r{{}};]*\s+{Regex.Escape(prop.PropertyName)}\s*\{{)";
        var regex = new Regex(propPattern, RegexOptions.Multiline);
        var m = regex.Match(content);
        if (!m.Success) return content;

        var attrsText = m.Groups["attrs"].Value;
        var declIndex = m.Groups["decl"].Index;

        var attrLines = SplitAttributes(attrsText);
        var map = ParseAttributeMap(attrLines);
        map["DisplayName"] = $"\"{Escape(display)}\"";

        var rebuilt = BuildAttributesPreserveUnknown(map, preferredOrder: new[]
        {
            "DisplayName"
        });

        var sb = new StringBuilder();
        sb.Append(content, 0, m.Groups["attrs"].Index);
        sb.Append(rebuilt);
        sb.Append(content, m.Groups["attrs"].Index + m.Groups["attrs"].Length,
            content.Length - (m.Groups["attrs"].Index + m.Groups["attrs"].Length));
        return sb.ToString();
    }

    public string UpdateClassAttributes(string content, string tableDisplayName)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(tableDisplayName))
            return content;

        // 先確保有 using EnterpriseOne.Behaviors;
        content = EnsureUsingStatement(content, "using EnterpriseOne.Behaviors;");

        // 尋找主要的 Row 類別，排除 RowFields 類別
        var mainRowClassRegex = new Regex(
            @"(?<attrs>(?:^\s*\[[^\]]+\]\s*\r?\n?)*)^(?<decl>\s*(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*Row)(?!\s*:\s*RowFieldsBase)\s*)(?<inheritance>[^{]*)\{",
            RegexOptions.Multiline);

        var match = mainRowClassRegex.Match(content);

        // 如果還是找不到，試著找任何不是 RowFields 的類別
        if (!match.Success)
        {
            var anyClassRegex = new Regex(
                @"(?<attrs>(?:^\s*\[[^\]]+\]\s*\r?\n?)*)^(?<decl>\s*(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*?)(?!.*RowFields)(?!\s*:\s*RowFieldsBase)\s*)(?<inheritance>[^{]*)\{",
                RegexOptions.Multiline);
            match = anyClassRegex.Match(content);
            if (!match.Success)
                return content;
        }

        var attrsGroup = match.Groups["attrs"];
        var declGroup = match.Groups["decl"];
        var nameGroup = match.Groups["name"];
        var inheritanceGroup = match.Groups["inheritance"];

        var existingAttrsText = attrsGroup.Value;
        var attrLines = SplitAttributes(existingAttrsText);
        var attrMap = ParseAttributeMap(attrLines);

        // 更新屬性值
        SetOrUpdate(attrMap, "DisplayName", $"\"{Escape(tableDisplayName)}\"");
        SetOrUpdate(attrMap, "InstanceName", $"\"{Escape(tableDisplayName)}\"");
        SetOrUpdate(attrMap, "ReadPermission", $"\"{Escape(tableDisplayName)}:讀取\"");
        SetOrUpdate(attrMap, "ModifyPermission", $"\"{Escape(tableDisplayName)}:修改\"");
        SetOrUpdate(attrMap, "ServiceLookupPermission", $"\"{Escape(tableDisplayName)}:查表\"");
        SetOrUpdate(attrMap, "LookupScript", "");
        SetOrUpdate(attrMap, "DataAuditLog", "");

        var rebuiltAttrs = BuildAttributesPreserveUnknown(attrMap, preferredOrder: new[]
        {
            "DisplayName",
            "InstanceName",
            "ReadPermission",
            "ModifyPermission",
            "ServiceLookupPermission",
            "LookupScript",
            "DataAuditLog"
        });

        // 檢查並更新繼承關係，只有當類別有審計屬性時才實作 IAuditableRow
        var inheritanceText = inheritanceGroup.Value.Trim();
        var className = nameGroup.Value;
        var updatedInheritance = inheritanceText;

        if (className.EndsWith("Row") && HasAuditProperties(content))
        {
            updatedInheritance = EnsureImplementsIAuditableRow(inheritanceText);
        }

        var sb = new StringBuilder(content.Length + 512);
        // 加入屬性之前的內容
        sb.Append(content, 0, attrsGroup.Index);
        // 加入重建的屬性
        sb.Append(rebuiltAttrs);
        // 加入類別宣告
        sb.Append(declGroup.Value);
        // 加入更新的繼承關係
        sb.Append(updatedInheritance);
        // 加入開始大括號
        sb.Append('{');
        // 加入剩餘的內容
        sb.Append(content, match.Index + match.Length, content.Length - (match.Index + match.Length));

        return sb.ToString();
    }

    private bool HasAuditProperties(string content)
    {
        // 檢查是否包含所有必要的審計屬性
        var auditProperties = new[]
        {
            @"DateTime\?\s+CreatedAt\s*\{\s*get\s*;\s*set\s*;\s*\}",
            @"string\s+CreatedBy\s*\{\s*get\s*;\s*set\s*;\s*\}",
            @"DateTime\?\s+UpdatedAt\s*\{\s*get\s*;\s*set\s*;\s*\}",
            @"string\s+UpdatedBy\s*\{\s*get\s*;\s*set\s*;\s*\}"
        };

        return auditProperties.All(pattern =>
            Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline));
    }

    private string EnsureUsingStatement(string content, string usingStatement)
    {
        // 檢查是否已經存在
        if (content.Contains(usingStatement))
            return content;

        // 找到最後一個 using 語句的位置
        var usingRegex = new Regex(@"^using\s+[^;]+;", RegexOptions.Multiline);
        var matches = usingRegex.Matches(content);

        if (matches.Count > 0)
        {
            // 在最後一個 using 語句後插入
            var lastMatch = matches[matches.Count - 1];
            var insertPosition = lastMatch.Index + lastMatch.Length;
            return content.Insert(insertPosition, Environment.NewLine + usingStatement);
        }
        else
        {
            // 如果沒有 using 語句，在檔案開頭插入
            return usingStatement + Environment.NewLine + Environment.NewLine + content;
        }
    }

    private string EnsureImplementsIAuditableRow(string inheritanceText)
    {
        // 移除前後空白
        var cleaned = inheritanceText.Trim();

        // 檢查是否已經實作 IAuditableRow
        if (cleaned.Contains("IAuditableRow"))
            return cleaned;

        // 如果沒有繼承關係，加上 IAuditableRow
        if (string.IsNullOrWhiteSpace(cleaned))
            return " : IAuditableRow";

        // 如果已有繼承關係但不是以冒號開始，加上冒號
        if (!cleaned.StartsWith(":"))
            cleaned = ": " + cleaned;

        // 在現有繼承關係後加上 IAuditableRow
        return cleaned + ", IAuditableRow";
    }

    private static string MatchAttributeValue(string content, string attrName)
    {
        // 第一種模式：單獨的屬性 [AttributeName("value")]
        var singleAttrPattern = @"\[\s*" + Regex.Escape(attrName) +
                                @"\s*\(\s*(?<val>(""[^""]*""|nameof\s*\([^)]+\)))\s*\)\s*\]";
        var singleMatch = Regex.Match(content, singleAttrPattern);
        if (singleMatch.Success)
        {
            return ExtractAttributeValue(singleMatch.Groups["val"].Value);
        }

        // 第二種模式：組合屬性中的一個 [Attr1("val1"), AttributeName("value"), Attr3("val3")]
        var combinedAttrPattern = @"\[\s*[^\]]*?\b" + Regex.Escape(attrName) +
                                  @"\s*\(\s*(?<val>(""[^""]*""|nameof\s*\([^)]+\)))\s*\)[^\]]*?\]";
        var combinedMatch = Regex.Match(content, combinedAttrPattern);
        if (combinedMatch.Success)
        {
            return ExtractAttributeValue(combinedMatch.Groups["val"].Value);
        }

        // 第三種模式：跨行屬性組合
        var multilineAttrPattern = @"\[\s*[^\]]*?" + Regex.Escape(attrName) +
                                   @"\s*\(\s*(?<val>(""[^""]*""|nameof\s*\([^)]+\)))\s*\)[^\]]*?\]";
        var multilineMatch = Regex.Match(content, multilineAttrPattern, RegexOptions.Singleline);
        if (multilineMatch.Success)
        {
            return ExtractAttributeValue(multilineMatch.Groups["val"].Value);
        }

        return null;
    }

    private static string ExtractAttributeValue(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return null;

        var trimmed = rawValue.Trim();
        if (trimmed.StartsWith("nameof("))
            return trimmed; // 不進一步展開
        if (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
            return trimmed.Substring(1, trimmed.Length - 2);
        return trimmed;
    }

    private static List<RowPropertyInfo> ParseProperties(string content)
    {
        var list = new List<RowPropertyInfo>();

        // 抓取每個屬性區塊（包含前置屬性）
        var propRegex = new Regex(
            @"(?<attrs>(?:^\s*\[[^\]]+\]\s*\r?\n?)*)^(?<decl>\s*(?:public|protected|internal)\s+[^\n\r{{}};]*\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{\s*get\s*;\s*set\s*;\s*\})",
            RegexOptions.Multiline);

        foreach (Match m in propRegex.Matches(content))
        {
            var attrsText = m.Groups["attrs"].Value;
            var propName = m.Groups["name"].Value;

            string columnName = null;
            // 修正 Column 屬性解析，支援組合屬性
            columnName = MatchAttributeValue(attrsText, "Column");

            list.Add(new RowPropertyInfo
            {
                PropertyName = propName,
                ColumnName = columnName
            });
        }

        return list;
    }

    private static List<string> SplitAttributes(string existingAttrsText)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(existingAttrsText))
            return lines;

        using var sr = new System.IO.StringReader(existingAttrsText);
        string line;
        while ((line = sr.ReadLine()) != null)
        {
            if (!string.IsNullOrWhiteSpace(line))
                lines.Add(line);
        }

        return lines;
    }

    private static Dictionary<string, string> ParseAttributeMap(List<string> attrLines)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in attrLines)
        {
            // 處理組合屬性：[Attr1("val1"), Attr2("val2"), ...]
            var combinedAttrRegex = new Regex(@"\[\s*(?<attrs>[^\]]+)\s*\]");
            var combinedMatch = combinedAttrRegex.Match(line);
            if (combinedMatch.Success)
            {
                var attrsContent = combinedMatch.Groups["attrs"].Value;

                // 分割多個屬性，注意要處理引號內的逗號
                var attributes = SplitAttributesInLine(attrsContent);

                foreach (var attr in attributes)
                {
                    var attrRegex = new Regex(@"^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(\((?<args>.*)\))?\s*$");
                    var attrMatch = attrRegex.Match(attr.Trim());
                    if (attrMatch.Success)
                    {
                        var name = attrMatch.Groups["name"].Value;
                        var args = attrMatch.Groups["args"]?.Value?.Trim() ?? "";
                        if (!map.ContainsKey(name))
                            map[name] = args;
                    }
                }
            }
            else
            {
                // 處理單一屬性：[Attribute("value")]
                var singleAttrRegex =
                    new Regex(@"^\s*\[\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(\((?<args>.*)\))?\s*\]\s*$");
                var singleMatch = singleAttrRegex.Match(line);
                if (singleMatch.Success)
                {
                    var name = singleMatch.Groups["name"].Value;
                    var args = singleMatch.Groups["args"]?.Value?.Trim() ?? "";
                    if (!map.ContainsKey(name))
                        map[name] = args;
                }
            }
        }

        return map;
    }

    private static List<string> SplitAttributesInLine(string attrsContent)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var quoteChar = '\0';
        var parenLevel = 0;

        for (int i = 0; i < attrsContent.Length; i++)
        {
            char c = attrsContent[i];

            if (!inQuotes)
            {
                if (c == '"' || c == '\'')
                {
                    inQuotes = true;
                    quoteChar = c;
                    current.Append(c);
                }
                else if (c == '(')
                {
                    parenLevel++;
                    current.Append(c);
                }
                else if (c == ')')
                {
                    parenLevel--;
                    current.Append(c);
                }
                else if (c == ',' && parenLevel == 0)
                {
                    // 這是屬性分隔符
                    var attr = current.ToString().Trim();
                    if (!string.IsNullOrEmpty(attr))
                        result.Add(attr);
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                current.Append(c);
                if (c == quoteChar && (i == 0 || attrsContent[i - 1] != '\\'))
                {
                    inQuotes = false;
                    quoteChar = '\0';
                }
            }
        }

        // 加入最後一個屬性
        var lastAttr = current.ToString().Trim();
        if (!string.IsNullOrEmpty(lastAttr))
            result.Add(lastAttr);

        return result;
    }

    private static void SetOrUpdate(Dictionary<string, string> map, string name, string args)
    {
        map[name] = args;
    }

    private static string BuildAttributesPreserveUnknown(Dictionary<string, string> map,
        IEnumerable<string> preferredOrder)
    {
        var knownOrder = preferredOrder?.ToArray() ?? Array.Empty<string>();
        var unknown = map.Keys
            .Where(k => !knownOrder.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        var ordered = unknown.Concat(knownOrder).Where(k => map.ContainsKey(k));

        var sb = new StringBuilder();
        foreach (var name in ordered)
        {
            var args = map[name];
            if (string.IsNullOrWhiteSpace(args))
                sb.AppendLine($"[{name}]");
            else
                sb.AppendLine($"[{name}({args})]");
        }

        return sb.ToString();
    }

    private static string Escape(string s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
}

public class RowClassInfo
{
    public string ConnectionKey { get; set; }
    public string TableName { get; set; }
    public List<RowPropertyInfo> Properties { get; set; } = new();
}

public class RowPropertyInfo
{
    public string PropertyName { get; set; }
    public string ColumnName { get; set; }
}