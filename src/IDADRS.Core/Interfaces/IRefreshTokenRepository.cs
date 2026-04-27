using IDADRS.Core.Entities;
namespace IDADRS.Core.Interfaces;

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    Task<RefreshToken?> GetActiveAsync(string token, CancellationToken ct = default);
    Task               RevokeAllForUserAsync(int userId, CancellationToken ct = default);
}
