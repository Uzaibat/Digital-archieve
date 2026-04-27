using IDADRS.Core.Entities;
namespace IDADRS.Core.Interfaces;

public interface IDocumentRepository : IRepository<Document>
{
    Task<Document?>              GetDetailedByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<Document>>  GetAllDetailedAsync(CancellationToken ct = default);
    Task<IEnumerable<Document>>  FullTextSearchAsync(string query, CancellationToken ct = default);
    Task<IEnumerable<Document>>  GetRecentAsync(int count, CancellationToken ct = default);
}
