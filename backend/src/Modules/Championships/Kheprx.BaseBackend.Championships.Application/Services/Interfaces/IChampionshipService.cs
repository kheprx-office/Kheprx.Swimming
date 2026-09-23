using Kheprx.BaseBackend.Championships.Application.DTOs;
namespace Kheprx.BaseBackend.Championships.Application.Services.Interfaces;
public interface IChampionshipService
{
    Task<IReadOnlyList<CompetitionEventDto>> ListAsync(CancellationToken ct = default);
    Task<CompetitionEventDto> CreateAsync(CreateCompetitionEventCommand command, CancellationToken ct = default);
    Task<CompetitionEventDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>?> GetEnrolledSwimmerIdsAsync(Guid eventId, CancellationToken ct = default);
    Task<bool> SetEnrollmentsAsync(Guid eventId, IReadOnlyList<Guid> swimmerIds, CancellationToken ct = default);
}
