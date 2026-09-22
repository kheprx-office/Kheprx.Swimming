using Kheprx.BaseBackend.Health.Application.DTOs;

namespace Kheprx.BaseBackend.Health.Application.Services.Interfaces;

public interface IHealthReadingService
{
    /// <summary>Logs a reading. Returns null when the referenced medical test does not exist.</summary>
    Task<HealthReadingDto?> CreateAsync(CreateHealthReadingRequest request, Guid recordedBy, CancellationToken ct = default);

    /// <summary>Lists a swimmer's readings (newest first), enriched with test details + derived status.</summary>
    Task<IReadOnlyList<HealthReadingListItemDto>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default);

    /// <summary>Edits a reading's value; re-derives status. Returns null when the reading does not exist.</summary>
    Task<HealthReadingListItemDto?> UpdateAsync(Guid id, UpdateHealthReadingRequest request, CancellationToken ct = default);

    /// <summary>Deletes a reading. Returns false when the reading does not exist.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
