namespace Kheprx.BaseBackend.Championships.Domain.Repositories;

public interface IChampionshipEnrollmentRepository
{
    Task<IReadOnlyList<Guid>> ListSwimmerIdsAsync(Guid eventId, CancellationToken ct = default);
    Task ReplaceAsync(Guid eventId, IReadOnlyList<Guid> swimmerIds, CancellationToken ct = default);
}
