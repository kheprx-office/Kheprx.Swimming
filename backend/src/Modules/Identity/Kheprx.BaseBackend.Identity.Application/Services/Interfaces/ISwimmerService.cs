using Kheprx.BaseBackend.Identity.Application.DTOs;

namespace Kheprx.BaseBackend.Identity.Application.Services.Interfaces;

public interface ISwimmerService
{
    Task<SwimmerCountDto> GetCountAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SwimmerListItemDto>> ListAsync(string? search = null, CancellationToken ct = default);
    Task<CreatedSwimmerDto?> CreateAsync(CreateSwimmerRequest request, CancellationToken ct = default);
    Task<SwimmerProfileDto?> GetProfileAsync(Guid id, CancellationToken ct = default);
    Task<bool> UpdateIdentityAsync(Guid id, UpdateSwimmerIdentityRequest request, CancellationToken ct = default);
    Task<SwimmerVitalsDto?> CreateExamAsync(Guid id, CreateMedicalExamRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<SwimmerVitalsDto>?> ListExamsAsync(Guid swimmerId, CancellationToken ct = default);
    Task<SwimmerVitalsDto?> UpdateExamAsync(Guid swimmerId, Guid examId, CreateMedicalExamRequest request, CancellationToken ct = default);
    Task<bool> DeleteExamAsync(Guid swimmerId, Guid examId, CancellationToken ct = default);
    Task<SwimmerGuardiansDto?> GetGuardiansAsync(Guid id, CancellationToken ct = default);
    Task<bool> UpsertGuardiansAsync(Guid id, UpsertGuardiansRequest request, CancellationToken ct = default);
    Task<SwimmerBodyMeasurementDto?> GetBodyMeasurementAsync(Guid id, CancellationToken ct = default);
    Task<bool> AddBodyMeasurementAsync(Guid id, CreateBodyMeasurementRequest request, CancellationToken ct = default);
}
