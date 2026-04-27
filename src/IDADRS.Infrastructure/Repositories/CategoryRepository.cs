using IDADRS.Core.Entities;
using IDADRS.Core.Interfaces;
using IDADRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IDADRS.Infrastructure.Repositories;

public sealed class CategoryRepository : GenericRepository<Category>, ICategoryRepository
{
    public CategoryRepository(AppDbContext db) : base(db) { }

    public override async Task<Category?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _db.Categories.Include(c => c.Documents)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public override async Task<IEnumerable<Category>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Categories.Include(c => c.Documents).AsNoTracking().ToListAsync(ct);
}
