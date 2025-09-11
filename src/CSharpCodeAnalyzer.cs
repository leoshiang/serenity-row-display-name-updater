using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SerenityRowDisplayNameUpdater;

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
                Properties = []
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

                switch (attributeName)
                {
                    case "ConnectionKey":
                        classInfo.ConnectionKey = GetAttributeStringArgument(attribute);
                        break;
                    case "TableName":
                        classInfo.TableName = GetAttributeStringArgument(attribute);
                        break;
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
            attributeList.GetLocation().GetLineSpan();

            foreach (var attribute in attributeList.Attributes)
            {
                var attributeName = attribute.Name.ToString();
                var attributeSpan = attribute.GetLocation().GetLineSpan();

                switch (attributeName)
                {
                    case "DisplayName":
                        propertyInfo.DisplayName = GetAttributeStringArgument(attribute);
                        propertyInfo.DisplayNameLine = attributeSpan.StartLinePosition.Line;
                        propertyInfo.DisplayNameAttribute = attribute;
                        break;
                    case "Column":
                        propertyInfo.ColumnName = GetAttributeStringArgument(attribute);
                        propertyInfo.ColumnLine = attributeSpan.StartLinePosition.Line;
                        propertyInfo.ColumnAttribute = attribute;
                        break;
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
            if (!columnComments.TryGetValue(property.ColumnName, out var newComment))
                continue;

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