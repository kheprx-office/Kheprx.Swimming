using Kheprx.BaseBackend.Health.Application.DTOs;

namespace Kheprx.BaseBackend.Health.Application.Services.Interfaces;

public interface IMedicalTestService
{
    Task<IReadOnlyList<MedicalTestDto>> ListAsync(CancellationToken ct = default);
    Task<MedicalTestDto> CreateAsync(CreateMedicalTestRequest request, Guid createdBy, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
