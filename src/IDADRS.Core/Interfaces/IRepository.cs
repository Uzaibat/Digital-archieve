namespace IDADRS.Core.Interfaces;

/// <summary>
/// Generic repository — SOLID note: ISP satisfied by keeping this minimal.
/// Specialised repositories add domain-specific methods without polluting this contract.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?>                  GetByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<T>>      GetAllAsync(CancellationToken ct = default);
    Task                      AddAsync(T entity, CancellationToken ct = default);
    Task                      UpdateAsync(T entity, CancellationToken ct = default);
    Task                      DeleteAsync(T entity, CancellationToken ct = default);
    Task<int>                 CountAsync(CancellationToken ct = default);
}
