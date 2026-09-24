using Kheprx.BaseBackend.Identity.Domain.Entities;
namespace Kheprx.BaseBackend.Identity.Domain.Repositories;
public interface IDistanceRepository
{
    Task<IReadOnlyList<Distance>> GetAllAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
}
