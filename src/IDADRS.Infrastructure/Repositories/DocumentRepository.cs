using IDADRS.Core.Entities;
using IDADRS.Core.Interfaces;
using IDADRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IDADRS.Infrastructure.Repositories;

public sealed class DocumentRepository : GenericRepository<Document>, IDocumentRepository
{
    public DocumentRepository(AppDbContext db) : base(db) { }

    public async Task<Document?> GetDetailedByIdAsync(int id, CancellationToken ct = default) =>
        await _db.Documents
            .Include(d => d.Uploader)
            .Include(d => d.Category)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IEnumerable<Document>> GetAllDetailedAsync(CancellationToken ct = default) =>
        await _db.Documents
            .Include(d => d.Uploader)
            .Include(d => d.Category)
            .AsNoTracking()
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync(ct);

    /// <summary>
    /// Uses PostgreSQL tsvector GIN index for full-text search.
    /// Native C BMH / TF-IDF re-ranking is applied by DocumentService after this call.
    /// </summary>
    public async Task<IEnumerable<Document>> FullTextSearchAsync(
        string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetAllDetailedAsync(ct);

        return await _db.Documents
            .Include(d => d.Uploader)
            .Include(d => d.Category)
            .Where(d => EF.Functions
                .ToTsVector("english", d.Title + " " + (d.Description ?? string.Empty))
                .Matches(EF.Functions.PlainToTsQuery("english", query)))
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Document>> GetRecentAsync(int count, CancellationToken ct = default) =>
        await _db.Documents
            .Include(d => d.Category)
            .OrderByDescending(d => d.UploadDate)
            .Take(count)
            .AsNoTracking()
            .ToListAsync(ct);
}
