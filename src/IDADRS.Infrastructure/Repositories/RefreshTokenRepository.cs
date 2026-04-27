using IDADRS.Core.Entities;
using IDADRS.Core.Interfaces;
using IDADRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IDADRS.Infrastructure.Repositories;

public sealed class RefreshTokenRepository : GenericRepository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(AppDbContext db) : base(db) { }

    public async Task<RefreshToken?> GetActiveAsync(string token, CancellationToken ct = default) =>
        await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == token && !t.Revoked && t.ExpiresAt > DateTime.UtcNow, ct);

    public async Task RevokeAllForUserAsync(int userId, CancellationToken ct = default)
    {
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && !t.Revoked).ToListAsync(ct);
        foreach (var t in tokens) t.Revoked = true;
    }
}
