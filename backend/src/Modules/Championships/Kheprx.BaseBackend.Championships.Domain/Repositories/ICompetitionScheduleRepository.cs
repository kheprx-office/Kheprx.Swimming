namespace Kheprx.BaseBackend.Championships.Domain.Repositories;

public interface ICompetitionScheduleRepository
{
    Task<IReadOnlyList<ScheduleDayRow>> GetAsync(Guid eventId, CancellationToken ct = default);
    Task ReplaceAsync(Guid eventId, IReadOnlyList<ScheduleDayInput> days, CancellationToken ct = default);
}
