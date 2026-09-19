using Kheprx.BaseBackend.Health.Application.DTOs;

namespace Kheprx.BaseBackend.Health.Application.Services.Interfaces;

public interface IInBodyReadingService
{
    Task<IReadOnlyList<InBodyReadingDto>> ListAsync(Guid swimmerId, CancellationToken ct = default);
    Task<InBodyReadingDto> CreateAsync(Guid swimmerId, CreateInBodyReadingRequest request, Guid recordedBy, CancellationToken ct = default);
    Task<InBodyReadingDto?> UpdateAsync(Guid swimmerId, Guid readingId, CreateInBodyReadingRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid swimmerId, Guid readingId, CancellationToken ct = default);
}
