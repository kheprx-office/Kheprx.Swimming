using Kheprx.BaseBackend.Identity.Application.DTOs;

namespace Kheprx.BaseBackend.Identity.Application.Services.Interfaces;

public interface ICoachService
{
    Task<CreatedCoachDto?> CreateAsync(CreateCoachRequest request, CancellationToken ct = default);
}
