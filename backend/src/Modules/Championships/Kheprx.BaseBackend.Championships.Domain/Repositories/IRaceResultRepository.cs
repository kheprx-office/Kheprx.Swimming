namespace Kheprx.BaseBackend.Championships.Domain.Repositories;

public interface IRaceResultRepository
{
    Task<IReadOnlyList<RaceResultRow>> GetByEventAsync(Guid eventId, CancellationToken ct = default);
    Task ReplaceForSessionAsync(Guid raceSessionId, IReadOnlyList<RaceResultInput> rows, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, int>> GetBestTimesAsync(Guid distanceId, Guid strokeId, Guid excludeSessionId, IReadOnlyList<Guid> swimmerIds, CancellationToken ct = default);
    Task<IReadOnlyList<SwimmerRaceLineRow>> GetSwimmerRaceLinesAsync(Guid swimmerId, CancellationToken ct = default);
}
