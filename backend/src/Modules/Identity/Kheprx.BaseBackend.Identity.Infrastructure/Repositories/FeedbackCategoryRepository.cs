using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class FeedbackCategoryRepository : IFeedbackCategoryRepository
{
    private readonly IdentityDbContext _db;
    public FeedbackCategoryRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<FeedbackCategory>> GetAllAsync(CancellationToken ct = default)
        => await _db.FeedbackCategories.AsNoTracking().OrderBy(c => c.Code).ToListAsync(ct);
}
