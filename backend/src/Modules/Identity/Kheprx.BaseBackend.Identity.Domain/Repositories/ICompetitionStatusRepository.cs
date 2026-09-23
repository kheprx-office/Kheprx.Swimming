using Kheprx.BaseBackend.Identity.Domain.Entities;
namespace Kheprx.BaseBackend.Identity.Domain.Repositories;
public interface ICompetitionStatusRepository
{
    Task<IReadOnlyList<CompetitionStatus>> GetAllAsync(CancellationToken ct = default);
}
