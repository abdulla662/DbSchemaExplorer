using DbSchemaExplorer.DataAcess;
using DbSchemaExplorer.Models;
            using Microsoft.AspNetCore.Components;
            using Microsoft.Data.SqlClient;
            using Microsoft.JSInterop;
using MySql.Data.MySqlClient;
using System.Data;
            using System.Net;
            using System.Text;
using Microsoft.EntityFrameworkCore;


namespace DbSchemaExplorer.Pages
            {
                public partial class Connect : ComponentBase
                {
                    [Inject] private IJSRuntime JSRuntime { get; set; }
                [Inject] private AppDbContext DbContext { get; set; }


        protected string SelectedDbType = "sql";
                    protected string ServerName = "";
                    protected string DatabaseName = "";
                    protected string ResultMessage = "";
                    protected bool IsSuccess = false;
                    protected string MySqlPassword = "";
                    protected string SearchText = "";
                    private bool ShouldRenderMermaid = false;

                    protected List<string> Tables = new();
                    protected string? SelectedTable = null;
                    protected List<ColumnInfo> Columns = new();
                    protected List<TableRelationship> Relationships = new();
                    protected bool ShowRelationships = false;
                    protected MarkupString MermaidCode = default!;
        protected string TableDescription = "";
        protected bool ShowDescription = false;


        protected List<string> FilteredTables => Tables
                        .Where(t => string.IsNullOrWhiteSpace(SearchText) || t.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                        .ToList();

        protected async Task TestConnection()
        {
            Tables.Clear();
            Columns.Clear();
            Relationships.Clear();
            ShowRelationships = false;

            try
            {
                if (SelectedDbType == "sql")
                {
                    var connectionString = $"Server={ServerName};Database={DatabaseName};Integrated Security=True;TrustServerCertificate=True;";
                    using SqlConnection connection = new(connectionString);
                    await connection.OpenAsync();

                    ResultMessage = "✅ SQL Server Connection successful!";
                    IsSuccess = true;

                    var cmd = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'", connection);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        Tables.Add(reader.GetString(0));
                    }
                }
                else if (SelectedDbType == "mysql")
                {
                    var connectionString = $"Server={ServerName};Port=3306;Database={DatabaseName};Uid=root;Pwd={MySqlPassword};";
                    using var connection = new MySqlConnection(connectionString);
                    await connection.OpenAsync();

                    ResultMessage = "✅ MySQL Connection successful!";
                    IsSuccess = true;

                    var cmd = new MySqlCommand("SHOW TABLES", connection);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        Tables.Add(reader.GetString(0));
                    }
                }

                if (IsSuccess && Tables.Any())
                {
                    foreach (var table in Tables)
                    {
                        var exists = DbContext.TableDocumentations.Any(t =>
                            t.DatabaseName == DatabaseName &&
                            t.TableName == table);

                        if (!exists)
                        {
                            DbContext.TableDocumentations.Add(new TableDocumentation
                            {
                                DatabaseName = DatabaseName,
                                TableName = table,
                                Notes = "" 
                            });
                        }
                    }

                    await DbContext.SaveChangesAsync();
                }
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
            TableDescription = "";
            ShowDescription = false;

            try
            {
                if (SelectedDbType == "sql")
                {
                    var connectionString = $"Server={ServerName};Database={DatabaseName};Integrated Security=True;TrustServerCertificate=True;";
                    using SqlConnection connection = new(connectionString);
                    await connection.OpenAsync();

                    var columnList = new List<ColumnInfo>();

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

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            columnList.Add(new ColumnInfo
                            {
                                Name = reader.GetString(0),
                                DataType = reader.GetString(1),
                                IsPrimaryKey = reader.GetInt32(2) == 1,
                                IsForeignKey = false
                            });
                        }
                    }

                    var foreignKeyColumns = new HashSet<string>();

                    var fkCmd = new SqlCommand(@"
                SELECT parent_col.name AS ColumnName
                FROM sys.foreign_key_columns fk
                INNER JOIN sys.tables parent_table ON fk.parent_object_id = parent_table.object_id
                INNER JOIN sys.columns parent_col ON fk.parent_object_id = parent_col.object_id AND fk.parent_column_id = parent_col.column_id
                WHERE parent_table.name = @TableName", connection);

                    fkCmd.Parameters.AddWithValue("@TableName", tableName);

                    using (var fkReader = await fkCmd.ExecuteReaderAsync())
                    {
                        while (await fkReader.ReadAsync())
                        {
                            foreignKeyColumns.Add(fkReader.GetString(0));
                        }
                    }

                    foreach (var col in columnList)
                    {
                        if (foreignKeyColumns.Contains(col.Name))
                        {
                            col.IsForeignKey = true;
                        }
                        Columns.Add(col);
                    }
                }
                else if (SelectedDbType == "mysql")
                {
                    var connectionString = $"Server={ServerName};Port=3306;Database={DatabaseName};Uid=root;Pwd={MySqlPassword};";
                    using var connection = new MySqlConnection(connectionString);
                    await connection.OpenAsync();

                    var columnList = new List<ColumnInfo>();

                    var cmd = new MySqlCommand($"DESCRIBE `{tableName}`", connection);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            columnList.Add(new ColumnInfo
                            {
                                Name = reader.GetString("Field"),
                                DataType = reader.GetString("Type"),
                                IsPrimaryKey = reader.GetString("Key") == "PRI",
                                IsForeignKey = false
                            });
                        }
                    }

                    var foreignKeyColumns = new HashSet<string>();

                    var fkCmd = new MySqlCommand($@"
                SELECT COLUMN_NAME 
                FROM information_schema.KEY_COLUMN_USAGE 
                WHERE TABLE_SCHEMA = '{DatabaseName}' AND TABLE_NAME = '{tableName}' AND REFERENCED_COLUMN_NAME IS NOT NULL", connection);

                    using (var fkReader = await fkCmd.ExecuteReaderAsync())
                    {
                        while (await fkReader.ReadAsync())
                        {
                            foreignKeyColumns.Add(fkReader.GetString("COLUMN_NAME"));
                        }
                    }

                    foreach (var col in columnList)
                    {
                        if (foreignKeyColumns.Contains(col.Name))
                        {
                            col.IsForeignKey = true;
                        }
                        Columns.Add(col);
                    }
                }

                // ✅ Load table description if available from local documentation table
                var doc = await DbContext.TableDocumentations
                    .FirstOrDefaultAsync(t => t.DatabaseName == DatabaseName && t.TableName == SelectedTable);
                if (doc != null && !string.IsNullOrWhiteSpace(doc.Notes))
                {
                    TableDescription = doc.Notes;
                }
            }
            catch (Exception ex)
            {
                ResultMessage = $"❌ Failed to load table: {ex.Message}";
                IsSuccess = false;
            }
        }

        protected async Task LoadAndShowRelationships()
                    {
                        await LoadRelationships();
                        ShowRelationships = true;
                        StateHasChanged();
                    }

                    protected async Task LoadRelationships()
                    {
                        Relationships.Clear();
                        ShowRelationships = false;

                        try
                        {
                            if (SelectedDbType == "sql")
                            {
                                var connectionString = $"Server={ServerName};Database={DatabaseName};Integrated Security=True;TrustServerCertificate=True;";
                                using SqlConnection connection = new(connectionString);
                                await connection.OpenAsync();

                                var cmd = new SqlCommand(@"
                                    SELECT parent_table.name AS FromTable,
                                           parent_col.name AS FromColumn,
                                           referenced_table.name AS ToTable,
                                           referenced_col.name AS ToColumn
                                    FROM sys.foreign_key_columns fk
                                    INNER JOIN sys.tables parent_table ON fk.parent_object_id = parent_table.object_id
                                    INNER JOIN sys.columns parent_col ON fk.parent_object_id = parent_col.object_id AND fk.parent_column_id = parent_col.column_id
                                    INNER JOIN sys.tables referenced_table ON fk.referenced_object_id = referenced_table.object_id
                                    INNER JOIN sys.columns referenced_col ON fk.referenced_object_id = referenced_col.object_id AND fk.referenced_column_id = referenced_col.column_id", connection);

                                using var reader = await cmd.ExecuteReaderAsync();
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
                            }
                            else if (SelectedDbType == "mysql")
                            {
                                var connectionString = $"Server={ServerName};Port=3306;Database={DatabaseName};Uid=root;Pwd={MySqlPassword};";
                                using var connection = new MySqlConnection(connectionString);
                                await connection.OpenAsync();

                                var cmd = new MySqlCommand($@"
                                    SELECT TABLE_NAME AS FromTable, COLUMN_NAME AS FromColumn, REFERENCED_TABLE_NAME AS ToTable, REFERENCED_COLUMN_NAME AS ToColumn
                                    FROM information_schema.KEY_COLUMN_USAGE
                                    WHERE TABLE_SCHEMA = '{DatabaseName}' AND REFERENCED_TABLE_NAME IS NOT NULL", connection);

                                using var reader = await cmd.ExecuteReaderAsync();
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
                            }

                            await GenerateMermaidCodeAsync();
                            await Task.Delay(1000);
                            await JSRuntime.InvokeVoidAsync("renderMermaid");


                        }
                        catch (Exception ex)
                        {
                            ResultMessage = $"❌ Failed to load relationships: {ex.Message}";
                            IsSuccess = false;
                        }
                    }


                    private Task GenerateMermaidCodeAsync()
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

                        MermaidCode = (MarkupString)$"<div class='mermaid'>{sb.ToString()}</div>";
                        return Task.CompletedTask;
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

                        if (ShowRelationships)
                        {
                            await JSRuntime.InvokeVoidAsync("renderMermaid");
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