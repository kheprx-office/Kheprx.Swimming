using Kheprx.BaseBackend.Health.Application.DTOs;

namespace Kheprx.BaseBackend.Health.Application.Services.Interfaces;

public interface IHealthReadingService
{
    /// <summary>Logs a reading. Returns null when the referenced medical test does not exist.</summary>
    Task<HealthReadingDto?> CreateAsync(CreateHealthReadingRequest request, Guid recordedBy, CancellationToken ct = default);
}
