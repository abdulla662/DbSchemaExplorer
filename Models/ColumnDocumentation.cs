namespace DbSchemaExplorer.Models
{
    public class ColumnDocumentation
    {
        public int Id { get; set; }
        public string TableName { get; set; } = "";
        public string ColumnName { get; set; } = "";
        public string Purpose { get; set; } = "";
        public string Notes { get; set; } = "";
    }
}
