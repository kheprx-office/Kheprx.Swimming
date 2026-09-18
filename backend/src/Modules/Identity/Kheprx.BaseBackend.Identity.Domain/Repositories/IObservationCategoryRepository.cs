using Kheprx.BaseBackend.Identity.Domain.Entities;
namespace Kheprx.BaseBackend.Identity.Domain.Repositories;
public interface IObservationCategoryRepository
{
    Task<IReadOnlyList<ObservationCategory>> GetAllAsync(CancellationToken ct = default);
}
