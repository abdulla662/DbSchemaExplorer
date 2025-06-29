using DbSchemaExplorer.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Data.SqlClient;
using System.Net;
using System.Text;

namespace DbSchemaExplorer.Pages
{
    public partial class Connect : ComponentBase
    {
        [Inject] private IJSRuntime JSRuntime { get; set; }

        protected string ServerName = "";
        protected string DatabaseName = "";
        protected string ResultMessage = "";
        protected bool IsSuccess = false;

        protected List<string> Tables = new();
        protected string? SelectedTable = null;
        protected List<ColumnInfo> Columns = new();
        protected List<TableRelationship> Relationships = new();
        protected bool ShowRelationships = false;
        protected MarkupString MermaidCode = default!;

        protected async Task TestConnection()
        {
            Tables.Clear();
            Columns.Clear();
            Relationships.Clear();
            ShowRelationships = false;

            try
            {
                var connectionString = $"Server={ServerName};Database={DatabaseName};Integrated Security=True;TrustServerCertificate=True;";
                using SqlConnection connection = new(connectionString);
                await connection.OpenAsync();

                ResultMessage = "✅ Connection successful!";
                IsSuccess = true;

                var cmd = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'", connection);
                var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    Tables.Add(reader.GetString(0));
                }

                reader.Close();
            }
            catch (Exception ex)
            {
                ResultMessage = $"❌ Connection failed: {ex.Message}";
                IsSuccess = false;
            }
        }

        protected async Task LoadTableDetails(string tableName)
        {
            SelectedTable = tableName;
            Columns.Clear();

            try
            {
                var connectionString = $"Server={ServerName};Database={DatabaseName};Integrated Security=True;TrustServerCertificate=True;";
                using SqlConnection connection = new(connectionString);
                await connection.OpenAsync();

                var cmd = new SqlCommand(@"
                    SELECT c.COLUMN_NAME, c.DATA_TYPE,
                           CASE WHEN k.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey
                    FROM INFORMATION_SCHEMA.COLUMNS c
                    LEFT JOIN (
                        SELECT COLUMN_NAME
                        FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
                        WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + CONSTRAINT_NAME), 'IsPrimaryKey') = 1
                              AND TABLE_NAME = @TableName
                    ) k ON c.COLUMN_NAME = k.COLUMN_NAME
                    WHERE c.TABLE_NAME = @TableName", connection);

                cmd.Parameters.AddWithValue("@TableName", tableName);

                var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    Columns.Add(new ColumnInfo
                    {
                        Name = reader.GetString(0),
                        DataType = reader.GetString(1),
                        IsPrimaryKey = reader.GetInt32(2) == 1,
                        IsForeignKey = false
                    });
                }
                reader.Close();

                var fkCmd = new SqlCommand(@"
                    SELECT
                        parent_col.name AS ColumnName
                    FROM sys.foreign_key_columns fk
                    INNER JOIN sys.tables parent_table ON fk.parent_object_id = parent_table.object_id
                    INNER JOIN sys.columns parent_col ON fk.parent_object_id = parent_col.object_id AND fk.parent_column_id = parent_col.column_id
                    WHERE parent_table.name = @TableName", connection);

                fkCmd.Parameters.AddWithValue("@TableName", tableName);

                var fkReader = await fkCmd.ExecuteReaderAsync();
                var foreignKeyColumns = new HashSet<string>();

                while (await fkReader.ReadAsync())
                {
                    foreignKeyColumns.Add(fkReader.GetString(0));
                }
                fkReader.Close();

                foreach (var col in Columns)
                {
                    if (foreignKeyColumns.Contains(col.Name))
                    {
                        col.IsForeignKey = true;
                    }
                }
            }
            catch (Exception ex)
            {
                ResultMessage = $"❌ Failed to load table: {ex.Message}";
                IsSuccess = false;
            }
        }

        protected async Task LoadRelationships()
        {
            Relationships.Clear();
            ShowRelationships = false;

            try
            {
                var connectionString = $"Server={ServerName};Database={DatabaseName};Integrated Security=True;TrustServerCertificate=True;";
                using SqlConnection connection = new(connectionString);
                await connection.OpenAsync();

                var cmd = new SqlCommand(@"
                    SELECT
                        parent_table.name AS FromTable,
                        parent_col.name AS FromColumn,
                        referenced_table.name AS ToTable,
                        referenced_col.name AS ToColumn
                    FROM sys.foreign_key_columns fk
                    INNER JOIN sys.tables parent_table ON fk.parent_object_id = parent_table.object_id
                    INNER JOIN sys.columns parent_col ON fk.parent_object_id = parent_col.object_id AND fk.parent_column_id = parent_col.column_id
                    INNER JOIN sys.tables referenced_table ON fk.referenced_object_id = referenced_table.object_id
                    INNER JOIN sys.columns referenced_col ON fk.referenced_object_id = referenced_col.object_id AND fk.referenced_column_id = referenced_col.column_id
                ", connection);

                var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    Relationships.Add(new TableRelationship
                    {
                        FromTable = reader.GetString(0),
                        FromColumn = reader.GetString(1),
                        ToTable = reader.GetString(2),
                        ToColumn = reader.GetString(3)
                    });
                }
                reader.Close();

                GenerateMermaidCode();
                ShowRelationships = true;

                await InvokeAsync(StateHasChanged);
                await JSRuntime.InvokeVoidAsync("renderMermaid");
            }
            catch (Exception ex)
            {
                ResultMessage = $"❌ Failed to load relationships: {ex.Message}";
                IsSuccess = false;
            }
        }

        private void GenerateMermaidCode()
        {
            var sb = new StringBuilder();
            sb.AppendLine("erDiagram");

            var tables = Relationships.Select(r => r.FromTable)
                .Union(Relationships.Select(r => r.ToTable))
                .Distinct()
                .OrderBy(t => t);

            foreach (var table in tables)
            {
                sb.AppendLine($"    {table} {{}}");
            }

            var printedRelations = new HashSet<string>();

            foreach (var rel in Relationships)
            {
                string fromCol = rel.FromColumn ?? "Unknown";
                string toCol = rel.ToColumn ?? "Unknown";
                string key = $"{rel.FromTable}-{fromCol}-{rel.ToTable}-{toCol}";

                if (!printedRelations.Contains(key))
                {
                    sb.AppendLine($"    {rel.FromTable} ||--o{{ {rel.ToTable} : \"{fromCol} to {toCol}\"");
                    printedRelations.Add(key);
                }
            }

            var encoded = WebUtility.HtmlEncode(sb.ToString());
            MermaidCode = (MarkupString)$"<div class='mermaid'>{encoded}</div>";
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await JSRuntime.InvokeVoidAsync("eval", @"
                const script = document.createElement('script');
                script.src = 'https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.min.js';
                script.onload = () => {
                    console.log('✅ Mermaid.js loaded');
                    mermaid.initialize({ startOnLoad: false });
                    window.renderMermaid = () => {
                        mermaid.run({ querySelector: '.mermaid' });
                    };
                    window.applyZoom = (scale) => {
                        const wrapper = document.getElementById('mermaid-wrapper');
                        if (wrapper) {
                            wrapper.style.transform = `scale(${scale})`;
                        }
                    };
                };
                document.head.appendChild(script);
            ");
            }
        }

        double zoomLevel = 1.0;

        protected async Task ZoomIn()
        {
            zoomLevel += 0.1;
            await JSRuntime.InvokeVoidAsync("applyZoom", zoomLevel);
        }

        protected async Task ZoomOut()
        {
            zoomLevel = Math.Max(0.1, zoomLevel - 0.1);
            await JSRuntime.InvokeVoidAsync("applyZoom", zoomLevel);
        }
    }
}
