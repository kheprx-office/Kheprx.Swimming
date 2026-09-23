using Kheprx.BaseBackend.Championships.Domain.Entities;
namespace Kheprx.BaseBackend.Championships.Domain.Repositories;
public interface ICompetitionEventRepository
{
    Task<IReadOnlyList<CompetitionEvent>> ListAsync(CancellationToken ct = default);
    Task AddAsync(CompetitionEvent competitionEvent, CancellationToken ct = default);
    Task<CompetitionEvent?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
