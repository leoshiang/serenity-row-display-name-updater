using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SerenityRowDisplayNameUpdater;

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