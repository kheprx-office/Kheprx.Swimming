using Kheprx.BaseBackend.Identity.Domain.Entities;
namespace Kheprx.BaseBackend.Identity.Domain.Repositories;
public interface IFeedbackCategoryRepository
{
    Task<IReadOnlyList<FeedbackCategory>> GetAllAsync(CancellationToken ct = default);
}
