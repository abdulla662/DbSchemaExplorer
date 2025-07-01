using DbSchemaExplorer.DataAcess;
using DbSchemaExplorer.Models;
using Microsoft.AspNetCore.Components;
using MySql.Data.MySqlClient;
using System.Data;

namespace DbSchemaExplorer.Pages
{
    public class TableEditBase : ComponentBase
    {
        [Inject] protected AppDbContext DbContext { get; set; }

        protected string ServerName = "localhost";
        protected string DatabaseName = "";
        protected string Password = "";
        protected bool IsConnected = false;
        protected List<string> Tables = new();
        protected string SelectedTable = "";
        protected TableDocumentation CurrentDocumentation = new();
        protected string StatusMessage = "";
        protected List<ColumnInfo> Columns = new();

        protected async Task ConnectToMySQL()
        {
            try
            {
                StatusMessage = "🚀 Trying to connect...";
                var connStr = $"Server={ServerName};Port=3306;Database={DatabaseName};Uid=blazoruser;Pwd={Password};";

                using var conn = new MySqlConnection(connStr);
                await conn.OpenAsync();

                using var cmd = new MySqlCommand("SHOW TABLES;", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                Tables.Clear();

                while (await reader.ReadAsync())
                    Tables.Add(reader.GetString(0));

                IsConnected = true;
                StatusMessage = $"✅ Connected! {Tables.Count} tables found.";
            }
            catch (Exception ex)
            {
                IsConnected = false;
                StatusMessage = $"❌ Connection failed: {ex.Message}";
            }
        }

        protected async Task HandleTableChange()
        {
            await LoadTableDocumentation();
            await LoadTableColumns();
        }

        protected async Task LoadTableDocumentation()
        {
            CurrentDocumentation = DbContext.TableDocumentations
                .FirstOrDefault(d => d.TableName == SelectedTable && d.DatabaseName == DatabaseName)
                ?? new TableDocumentation { TableName = SelectedTable, DatabaseName = DatabaseName };
        }

        protected async Task SaveDocumentation()
        {
            var existing = DbContext.TableDocumentations
                .FirstOrDefault(d => d.TableName == SelectedTable && d.DatabaseName == DatabaseName);

            if (existing != null)
            {
                existing.Notes = CurrentDocumentation.Notes;
            }
            else
            {
                await DbContext.TableDocumentations.AddAsync(CurrentDocumentation);
            }

            await DbContext.SaveChangesAsync();
            StatusMessage = "✅ Table notes saved successfully!";
        }

        protected async Task LoadTableColumns()
        {
            Columns.Clear();

            try
            {
                var connStr = $"Server={ServerName};Port=3306;Database={DatabaseName};Uid=blazoruser;Pwd={Password};";
                using var conn = new MySqlConnection(connStr);
                await conn.OpenAsync();

                var columnList = new List<ColumnInfo>();

                using (var cmd = new MySqlCommand($"DESCRIBE `{SelectedTable}`", conn))
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
                using (var fkCmd = new MySqlCommand($@"
                    SELECT COLUMN_NAME
                    FROM information_schema.KEY_COLUMN_USAGE
                    WHERE TABLE_SCHEMA = '{DatabaseName}' AND TABLE_NAME = '{SelectedTable}' AND REFERENCED_COLUMN_NAME IS NOT NULL", conn))
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
                        col.IsForeignKey = true;

                    Columns.Add(col);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Failed to load columns: {ex.Message}";
            }
        }

        public class ColumnInfo
        {
            public string Name { get; set; } = "";
            public string DataType { get; set; } = "";
            public bool IsPrimaryKey { get; set; } = false;
            public bool IsForeignKey { get; set; } = false;
        }
    }
}

