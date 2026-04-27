using IDADRS.Core.Entities;
namespace IDADRS.Core.Interfaces;

public interface IAccessLogRepository : IRepository<AccessLog>
{
    Task<(IEnumerable<AccessLog> Items, int Total)> GetPagedAsync(
        int page, int pageSize, CancellationToken ct = default);
}
