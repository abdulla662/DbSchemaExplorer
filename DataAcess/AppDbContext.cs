using DbSchemaExplorer.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace DbSchemaExplorer.DataAcess
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<ColumnDocumentation> ColumnDocumentations { get; set; }
        public DbSet<TableDocumentation> TableDocumentations { get; set; }

    }
}
