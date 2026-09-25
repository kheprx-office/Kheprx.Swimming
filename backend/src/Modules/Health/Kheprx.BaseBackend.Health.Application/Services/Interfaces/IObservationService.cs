using Kheprx.BaseBackend.Health.Application.DTOs;

namespace Kheprx.BaseBackend.Health.Application.Services.Interfaces;

public interface IObservationService
{
    Task<ObservationDto> CreateAsync(CreateObservationRequest request, Guid recordedBy, CancellationToken ct = default);
    Task<IReadOnlyList<ObservationDto>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default);
    Task<ObservationDto?> UpdateAsync(Guid id, UpdateObservationRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task ReplaceForSwimmerAsync(Guid swimmerId, IReadOnlyList<CreateObservationRequest> items, Guid recordedBy, CancellationToken ct = default);
}
