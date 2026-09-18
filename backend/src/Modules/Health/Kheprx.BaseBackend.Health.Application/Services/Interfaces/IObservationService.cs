using Kheprx.BaseBackend.Health.Application.DTOs;

namespace Kheprx.BaseBackend.Health.Application.Services.Interfaces;

public interface IObservationService
{
    Task<ObservationDto> CreateAsync(CreateObservationRequest request, Guid recordedBy, CancellationToken ct = default);
}
