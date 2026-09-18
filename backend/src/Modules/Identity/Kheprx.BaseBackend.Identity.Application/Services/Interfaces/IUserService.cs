using Kheprx.BaseBackend.Identity.Application.DTOs;

namespace Kheprx.BaseBackend.Identity.Application.Services.Interfaces;

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> ListAsync(string? search = null, CancellationToken ct = default);
    Task<UserDto?> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
}
