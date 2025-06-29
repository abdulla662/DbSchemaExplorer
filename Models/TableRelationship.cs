namespace DbSchemaExplorer.Models
{
    public class TableRelationship
    {
        public string FromTable { get; set; }
        public string FromColumn { get; set; }
        public string ToTable { get; set; }
        public string ToColumn { get; set; }
    }
}
