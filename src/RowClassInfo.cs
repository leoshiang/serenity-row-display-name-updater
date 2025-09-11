namespace SerenityRowDisplayNameUpdater;

public class RowClassInfo
{
    public string ClassName { get; set; }
    public string ConnectionKey { get; set; }
    public string TableName { get; set; }
    public List<PropertyInfo> Properties { get; set; }
}