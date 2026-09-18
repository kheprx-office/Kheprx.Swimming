using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Domain.Repositories;

namespace Kheprx.BaseBackend.Identity.Application.Services;

internal sealed class RoleService : IRoleService
{
    #region Fields

    private readonly IRoleRepository _roles;

    #endregion

    #region Constructor

    public RoleService(IRoleRepository roles)
    {
        _roles = roles;
    }

    #endregion

    #region APIs

    #region GetAllAsync — list all roles for display

    public async Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken ct = default)
    {
        var roles = await _roles.GetAllAsync(ct);

        var dtos = roles
            .Select(r => new RoleDto(r.Id, r.Code, r.NameEn, r.NameAr))
            .ToList();

        return dtos;
    }

    #endregion

    #endregion
}
