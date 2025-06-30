namespace DbSchemaExplorer.Models
{
    public class TableDocumentation
    {
        public int Id { get; set; }
        public string DatabaseName { get; set; } = "";
        public string TableName { get; set; } = "";
        public string Notes { get; set; } = "";
    }

}
