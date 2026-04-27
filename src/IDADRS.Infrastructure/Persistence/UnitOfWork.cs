using IDADRS.Core.Interfaces;
using IDADRS.Infrastructure.Repositories;

namespace IDADRS.Infrastructure.Persistence;

/// <summary>
/// Concrete UnitOfWork — all repositories share a single AppDbContext instance
/// so SaveAsync() flushes all changes atomically.
/// SOLID note — SRP: this class only manages transaction boundary.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    // Lazy-initialise repositories to avoid creating them when unused.
    private IDocumentRepository?     _documents;
    private IUserRepository?         _users;
    private ICategoryRepository?     _categories;
    private IAccessLogRepository?    _accessLogs;
    private IRefreshTokenRepository? _refreshTokens;

    public UnitOfWork(AppDbContext db) => _db = db;

    public IDocumentRepository     Documents     => _documents     ??= new DocumentRepository(_db);
    public IUserRepository         Users         => _users         ??= new UserRepository(_db);
    public ICategoryRepository     Categories    => _categories    ??= new CategoryRepository(_db);
    public IAccessLogRepository    AccessLogs    => _accessLogs    ??= new AccessLogRepository(_db);
    public IRefreshTokenRepository RefreshTokens => _refreshTokens ??= new RefreshTokenRepository(_db);

    public async Task<int> SaveAsync(CancellationToken ct = default) =>
        await _db.SaveChangesAsync(ct);

    public async ValueTask DisposeAsync() => await _db.DisposeAsync();
}
