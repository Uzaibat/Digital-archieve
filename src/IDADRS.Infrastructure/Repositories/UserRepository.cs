using IDADRS.Core.Entities;
using IDADRS.Core.Interfaces;
using IDADRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IDADRS.Infrastructure.Repositories;

public sealed class UserRepository : GenericRepository<User>, IUserRepository
{
    public UserRepository(AppDbContext db) : base(db) { }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default) =>
        await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username, ct);

    public async Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default) =>
        await _db.Users.AnyAsync(u => u.Username == username, ct);

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) =>
        await _db.Users.AnyAsync(u => u.Email == email.ToLowerInvariant(), ct);
}
