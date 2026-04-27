using IDADRS.Core.Entities;
namespace IDADRS.Core.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<bool>  ExistsByUsernameAsync(string username, CancellationToken ct = default);
    Task<bool>  ExistsByEmailAsync(string email, CancellationToken ct = default);
}
