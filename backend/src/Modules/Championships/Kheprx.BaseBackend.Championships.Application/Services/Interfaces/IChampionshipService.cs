using Kheprx.BaseBackend.Championships.Application.DTOs;
namespace Kheprx.BaseBackend.Championships.Application.Services.Interfaces;
public interface IChampionshipService
{
    Task<IReadOnlyList<CompetitionEventDto>> ListAsync(CancellationToken ct = default);
    Task<CompetitionEventDto> CreateAsync(CreateCompetitionEventCommand command, CancellationToken ct = default);
    Task<CompetitionEventDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>?> GetEnrolledSwimmerIdsAsync(Guid eventId, CancellationToken ct = default);
    Task<bool> SetEnrollmentsAsync(Guid eventId, IReadOnlyList<Guid> swimmerIds, CancellationToken ct = default);
    Task<ScheduleDto?> GetScheduleAsync(Guid eventId, CancellationToken ct = default);
    Task<SetScheduleResult> SetScheduleAsync(Guid eventId, IReadOnlyList<SetScheduleDay> days, CancellationToken ct = default);
    Task<ResultsDto?> GetResultsAsync(Guid eventId, CancellationToken ct = default);
    Task<SetRaceResultsResult> SetRaceResultsAsync(Guid eventId, Guid raceSessionId, IReadOnlyList<SetRaceResultsEntry> entries, Guid recordedBy, CancellationToken ct = default);
    Task<IReadOnlyList<ChampionshipSwimmerHistoryDto>> GetSwimmerHistoryAsync(Guid swimmerId, CancellationToken ct = default);
}
