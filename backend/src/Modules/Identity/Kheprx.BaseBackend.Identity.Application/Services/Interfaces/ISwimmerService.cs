using Kheprx.BaseBackend.Identity.Application.DTOs;

namespace Kheprx.BaseBackend.Identity.Application.Services.Interfaces;

public interface ISwimmerService
{
    Task<SwimmerCountDto> GetCountAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SwimmerListItemDto>> ListAsync(string? search = null, CancellationToken ct = default);
    Task<CreatedSwimmerDto?> CreateAsync(CreateSwimmerRequest request, CancellationToken ct = default);
}
