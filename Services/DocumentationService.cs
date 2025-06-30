using DbSchemaExplorer.DataAcess;
using DbSchemaExplorer.Models;
using Microsoft.EntityFrameworkCore;

namespace DbSchemaExplorer.Services
{
    public class DocumentationService
    {
        private readonly AppDbContext _context;

        public DocumentationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<string>> GetAllTablesAsync()
        {
            return await _context.ColumnDocumentations
                .Select(d => d.TableName)
                .Distinct()
                .ToListAsync();
        }

        public async Task<List<ColumnDocumentation>> GetByTableAsync(string tableName)
        {
            return await _context.ColumnDocumentations
                .Where(d => d.TableName == tableName)
                .ToListAsync();
        }

        public async Task SaveDocsAsync(string tableName, List<ColumnDocumentation> data)
        {
            var existing = _context.ColumnDocumentations.Where(d => d.TableName == tableName);
            _context.ColumnDocumentations.RemoveRange(existing);
            await _context.ColumnDocumentations.AddRangeAsync(data);
            await _context.SaveChangesAsync();
        }
    }
}

