using IDADRS.Core.Entities;
using IDADRS.Core.Interfaces;
using IDADRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IDADRS.Infrastructure.Repositories;

public sealed class AccessLogRepository : GenericRepository<AccessLog>, IAccessLogRepository
{
    public AccessLogRepository(AppDbContext db) : base(db) { }

    public async Task<(IEnumerable<AccessLog> Items, int Total)> GetPagedAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.AccessLogs
            .Include(a => a.User)
            .Include(a => a.Document)
            .OrderByDescending(a => a.AccessDate)
            .AsNoTracking();

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
}
