// CSharpCodeAnalyzer.cs

using System;
using System.Collections.Generic;
using System.Linq;
// Roslyn
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class CSharpCodeAnalyzer
{
    public RowClassInfo AnalyzeRowClass(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var tree = CSharpSyntaxTree.ParseText(content);
        var root = tree.GetCompilationUnitRoot();

        // 找主要 Row 類別：名稱以 Row 結尾，且 base 不為 RowFieldsBase
        var allClasses = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();

        bool IsRowFieldsBase(BaseListSyntax baseList)
            => baseList?.Types.Any(t => t.Type.ToString().EndsWith("RowFieldsBase", StringComparison.Ordinal)) == true;

        var targetClass = allClasses
                              .FirstOrDefault(c =>
                                  c.Identifier.Text.EndsWith("Row", StringComparison.Ordinal) &&
                                  !IsRowFieldsBase(c.BaseList))
                          ?? allClasses.FirstOrDefault(c =>
                              !c.Identifier.Text.Contains("RowFields", StringComparison.Ordinal) &&
                              !IsRowFieldsBase(c.BaseList));

        if (targetClass == null) return null;

        // 抓取 class 上的 ConnectionKey / TableName
        var connectionKey = GetAttributeFirstStringArg(targetClass.AttributeLists, "ConnectionKey");
        var tableName = GetAttributeFirstStringArg(targetClass.AttributeLists, "TableName");

        // 抓取屬性與 Column 對應
        var properties = new List<RowPropertyInfo>();
        foreach (var prop in targetClass.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (!HasGetSetAccessors(prop)) continue;

            var columnName = GetAttributeFirstStringArg(prop.AttributeLists, "Column");
            properties.Add(new RowPropertyInfo
            {
                PropertyName = prop.Identifier.Text,
                ColumnName = columnName
            });
        }

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

        var tree = CSharpSyntaxTree.ParseText(content);
        var root = tree.GetCompilationUnitRoot();

        var propNameToDisplay = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in properties)
        {
            var key = columnComments.Keys.FirstOrDefault(k =>
                string.Equals(k, p.ColumnName ?? p.PropertyName, StringComparison.OrdinalIgnoreCase));
            if (key == null) continue;

            var display = columnComments[key];
            if (string.IsNullOrWhiteSpace(display)) continue;

            propNameToDisplay[p.PropertyName] = display;
        }

        if (propNameToDisplay.Count == 0) return content;

        var rewriter = new PropertyDisplayNameRewriter(propNameToDisplay);
        var newRoot = (CompilationUnitSyntax)rewriter.Visit(root);

        return newRoot.NormalizeWhitespace().ToFullString();
    }

    public string UpdateClassAttributes(string content, string tableDisplayName)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(tableDisplayName))
            return content;

        var tree = CSharpSyntaxTree.ParseText(content);
        var root = tree.GetCompilationUnitRoot();

        // 確保 using EnterpriseOne.Behaviors;
        if (!root.Usings.Any(u => u.Name.ToString() == "EnterpriseOne.Behaviors"))
        {
            var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("EnterpriseOne.Behaviors"))
                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
            root = root.AddUsings(usingDirective);
        }

        // 找主要 Row 類別
        var allClasses = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();

        bool IsRowFieldsBase(BaseListSyntax baseList)
            => baseList?.Types.Any(t => t.Type.ToString().EndsWith("RowFieldsBase", StringComparison.Ordinal)) == true;

        var targetClass = allClasses
                              .FirstOrDefault(c =>
                                  c.Identifier.Text.EndsWith("Row", StringComparison.Ordinal) &&
                                  !IsRowFieldsBase(c.BaseList))
                          ?? allClasses.FirstOrDefault(c =>
                              !c.Identifier.Text.Contains("RowFields", StringComparison.Ordinal) &&
                              !IsRowFieldsBase(c.BaseList));

        if (targetClass == null) return content;

        // 更新/設定類別層級的 Attributes
        var classAttrMap = new Dictionary<string, string>
        {
            ["DisplayName"] = Quote(Escape(tableDisplayName)),
            ["InstanceName"] = Quote(Escape(tableDisplayName)),
            ["ReadPermission"] = Quote($"{Escape(tableDisplayName)}:讀取"),
            ["ModifyPermission"] = Quote($"{Escape(tableDisplayName)}:修改"),
            ["ServiceLookupPermission"] = Quote($"{Escape(tableDisplayName)}:查表"),
            ["LookupScript"] = "", // 無參數
            ["DataAuditLog"] = "" // 無參數
        };

        var updatedClass = UpsertAttributes(targetClass, classAttrMap, preferredOrder:
        [
            "DisplayName",
            "InstanceName",
            "ReadPermission",
            "ModifyPermission",
            "ServiceLookupPermission",
            "LookupScript",
            "DataAuditLog"
        ]);

        // 若有審計屬性，加入 IAuditableRow
        if (updatedClass.Identifier.Text.EndsWith("Row", StringComparison.Ordinal) &&
            HasAuditProperties(updatedClass))
        {
            updatedClass = EnsureImplementsIAuditableRow(updatedClass);
        }

        var newRoot = root.ReplaceNode(targetClass, updatedClass);
        return newRoot.NormalizeWhitespace().ToFullString();
    }

    // 判斷 property 是否同時具有 get 與 set（不限制為 auto-property）
    private static bool HasGetSetAccessors(PropertyDeclarationSyntax prop)
    {
        if (prop.AccessorList == null) return false;
        var accessors = prop.AccessorList.Accessors;
        if (accessors.Count < 2) return false;
        bool HasKind(SyntaxKind kind) => accessors.Any(a => a.Kind() == kind);
        return HasKind(SyntaxKind.GetAccessorDeclaration) && HasKind(SyntaxKind.SetAccessorDeclaration);
    }

    private static string GetAttributeFirstStringArg(SyntaxList<AttributeListSyntax> lists, string shortAttrName)
    {
        foreach (var list in lists)
        {
            foreach (var attr in list.Attributes)
            {
                var name = attr.Name.ToString();
                // Roslyn 對 Attribute 名稱允許省略 "Attribute" 後綴
                if (!IsAttrNameMatch(name, shortAttrName)) continue;

                if (attr.ArgumentList?.Arguments.Count > 0)
                {
                    var arg = attr.ArgumentList.Arguments[0].Expression;
                    switch (arg)
                    {
                        case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                            return literal.Token.ValueText;
                        case InvocationExpressionSyntax inv
                            when inv.Expression.ToString().StartsWith("nameof", StringComparison.Ordinal):
                            return inv.ToString(); // 保留 nameof(...) 原樣
                        default:
                            return arg.ToString();
                    }
                }
            }
        }

        return null;
    }

    private static bool IsAttrNameMatch(string name, string shortName)
    {
        if (string.Equals(name, shortName, StringComparison.Ordinal)) return true;
        if (string.Equals(name, shortName + "Attribute", StringComparison.Ordinal)) return true;
        // 處理具名空間的型別
        if (name.EndsWith("." + shortName, StringComparison.Ordinal)) return true;
        if (name.EndsWith("." + shortName + "Attribute", StringComparison.Ordinal)) return true;
        return false;
    }

    private static string Quote(string s) => $"\"{s}\"";
    private static string Escape(string s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";

    private static ClassDeclarationSyntax UpsertAttributes(
        ClassDeclarationSyntax cls,
        IDictionary<string, string> attrMap,
        IEnumerable<string> preferredOrder)
    {
        // 先蒐集既有 attributes
        var allAttrs = new Dictionary<string, AttributeSyntax>(StringComparer.Ordinal);
        var lists = new List<AttributeListSyntax>();

        foreach (var list in cls.AttributeLists)
        {
            lists.Add(list);
            foreach (var attr in list.Attributes)
            {
                var key = NormalizeAttrKey(attr.Name.ToString());
                allAttrs.TryAdd(key, attr);
            }
        }

        // 更新或新增
        foreach (var kv in attrMap)
        {
            var key = NormalizeAttrKey(kv.Key);
            var args = kv.Value ?? "";

            if (allAttrs.TryGetValue(key, out var existing))
            {
                // 替換參數
                var newAttr = BuildAttribute(kv.Key, args);
                lists = ReplaceAttributeInLists(lists, existing, newAttr);
                allAttrs[key] = newAttr;
            }
            else
            {
                var newAttr = BuildAttribute(kv.Key, args);
                if (lists.Count == 0)
                {
                    lists.Add(SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList([newAttr])));
                }
                else
                {
                    // 將新的屬性加到第一個 list 後方
                    var first = lists[0];
                    var newList = first.WithAttributes(first.Attributes.Add(newAttr));
                    lists[0] = newList;
                }

                allAttrs[key] = newAttr;
            }
        }

        // 依 preferredOrder 排序（其餘按名稱排序）
        var pref = preferredOrder?.ToArray() ?? [];
        var known = allAttrs.Values.ToList();
        var ordered = known
            .OrderBy(a =>
            {
                var n = NormalizeAttrKey(a.Name.ToString());
                var idx = Array.IndexOf(pref, n);
                return idx >= 0 ? idx : int.MaxValue;
            })
            .ThenBy(a => NormalizeAttrKey(a.Name.ToString()), StringComparer.Ordinal)
            .ToList();

        // 重建為單一 AttributeList（簡化）
        var newAttrList = SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList(ordered))
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

        return cls.WithAttributeLists(SyntaxFactory.List([newAttrList]));
    }

    private static string NormalizeAttrKey(string name)
    {
        var simple = name.Contains('.') ? name.Split('.').Last() : name;
        return simple.EndsWith("Attribute", StringComparison.Ordinal)
            ? simple[..^"Attribute".Length]
            : simple;
    }

    private static AttributeSyntax BuildAttribute(string shortName, string args)
    {
        var name = SyntaxFactory.IdentifierName(shortName);
        if (args == "")
        {
            // 無參數 attribute
            return SyntaxFactory.Attribute(name);
        }

        var expr = SyntaxFactory.ParseExpression(args);
        var arg = SyntaxFactory.AttributeArgument(expr);
        var argList = SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList([arg]));
        return SyntaxFactory.Attribute(name, argList);
    }

    private static List<AttributeListSyntax> ReplaceAttributeInLists(
        List<AttributeListSyntax> lists,
        AttributeSyntax oldAttr,
        AttributeSyntax newAttr)
    {
        for (var i = 0; i < lists.Count; i++)
        {
            var list = lists[i];
            var idx = list.Attributes.IndexOf(oldAttr);
            if (idx >= 0)
            {
                lists[i] = list.WithAttributes(list.Attributes.Replace(oldAttr, newAttr));
                break;
            }
        }

        return lists;
    }

    private static bool HasAuditProperties(ClassDeclarationSyntax cls)
    {
        // 檢查是否有下列屬性：
        // DateTime? CreatedAt { get; set; }
        // string   CreatedBy { get; set; }
        // DateTime? UpdatedAt { get; set; }
        // string   UpdatedBy { get; set; }
        bool Has(string name, Func<TypeSyntax, bool> typePredicate)
        {
            var p = cls.Members.OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault(x => x.Identifier.Text == name && HasGetSetAccessors(x));
            return p != null && typePredicate(p.Type);
        }

        bool IsString(TypeSyntax t) => t.ToString() == "string" ||
                                       t is PredefinedTypeSyntax pts && pts.Keyword.IsKind(SyntaxKind.StringKeyword);

        bool IsNullableDateTime(TypeSyntax t)
        {
            // 支援 DateTime? 或 Nullable<DateTime>
            var text = t.ToString();
            if (text == "DateTime?") return true;
            switch (t)
            {
                case NullableTypeSyntax nts when nts.ElementType.ToString() == "DateTime":
                case GenericNameSyntax { Identifier.Text: "Nullable" } g when
                    g.TypeArgumentList.Arguments.FirstOrDefault()?.ToString() == "DateTime":
                    return true;
                default:
                    return false;
            }
        }

        return Has("CreatedAt", IsNullableDateTime)
               && Has("CreatedBy", IsString)
               && Has("UpdatedAt", IsNullableDateTime)
               && Has("UpdatedBy", IsString);
    }

    private static ClassDeclarationSyntax EnsureImplementsIAuditableRow(ClassDeclarationSyntax cls)
    {
        bool AlreadyHas()
            => cls.BaseList?.Types.Any(t =>
                t.Type.ToString() == "IAuditableRow" ||
                t.Type.ToString().EndsWith(".IAuditableRow", StringComparison.Ordinal)) == true;

        if (AlreadyHas()) return cls;

        var newType = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("IAuditableRow"));
        if (cls.BaseList == null)
        {
            var baseList = SyntaxFactory.BaseList(SyntaxFactory.SeparatedList<BaseTypeSyntax>([newType]));
            return cls.WithBaseList(baseList);
        }
        else
        {
            var types = cls.BaseList.Types.Add(newType);
            return cls.WithBaseList(cls.BaseList.WithTypes(types));
        }
    }

    // 以 Roslyn 修改屬性 DisplayName
    private sealed class PropertyDisplayNameRewriter(Dictionary<string, string> propDisplay) : CSharpSyntaxRewriter
    {
        public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            var name = node.Identifier.Text;
            if (!propDisplay.TryGetValue(name, out var display))
                return base.VisitPropertyDeclaration(node);

            if (!HasGetSetAccessors(node))
                return base.VisitPropertyDeclaration(node);

            var attrName = "DisplayName";
            var lists = node.AttributeLists;

            // 找現有 DisplayName
            AttributeSyntax existing = null;
            foreach (var l in lists)
            {
                foreach (var a in l.Attributes)
                {
                    if (IsAttrNameMatch(a.Name.ToString(), attrName))
                    {
                        existing = a;
                        break;
                    }
                }

                if (existing != null) break;
            }

            var newAttr = BuildAttribute(attrName, Quote(Escape(display)));
            if (existing != null)
            {
                // 以新 attr 取代舊的
                for (var i = 0; i < lists.Count; i++)
                {
                    var list = lists[i];
                    var idx = list.Attributes.IndexOf(existing);
                    if (idx >= 0)
                    {
                        lists = lists.Replace(list, list.WithAttributes(list.Attributes.Replace(existing, newAttr)));
                        break;
                    }
                }
            }
            else
            {
                // 附加到第一個 attribute list，或新建一個
                if (lists.Count > 0)
                {
                    var first = lists[0];
                    lists = lists.Replace(first, first.WithAttributes(first.Attributes.Add(newAttr)));
                }
                else
                {
                    lists = lists.Add(SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList([newAttr])));
                }
            }

            return node.WithAttributeLists(lists);
        }
    }
}

public class RowClassInfo
{
    public string ConnectionKey { get; init; }
    public string TableName { get; init; }
    public List<RowPropertyInfo> Properties { get; init; } = [];
}

public class RowPropertyInfo
{
    public string PropertyName { get; init; }
    public string ColumnName { get; init; }
}