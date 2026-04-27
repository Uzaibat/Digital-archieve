namespace IDADRS.Core.Interfaces;

/// <summary>
/// Unit-of-Work — coordinates multiple repositories under one DbContext transaction.
/// SOLID note — DIP: callers depend on this interface, not EF Core.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IDocumentRepository     Documents    { get; }
    IUserRepository         Users        { get; }
    ICategoryRepository     Categories   { get; }
    IAccessLogRepository    AccessLogs   { get; }
    IRefreshTokenRepository RefreshTokens{ get; }
    Task<int> SaveAsync(CancellationToken ct = default);
}
