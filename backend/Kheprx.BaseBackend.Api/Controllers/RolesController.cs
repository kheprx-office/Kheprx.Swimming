using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

public sealed class RolesController : BaseApiController
{
    #region Fields

    private readonly IRoleService _service;

    #endregion

    #region Constructor

    public RolesController(IRoleService service)
    {
        _service = service;
    }

    #endregion

    #region APIs

    #region Get — GET api/roles — list all selectable roles

    // غير مستخدم في أي صفحة/تبويب
    /// <summary>Lists all selectable roles, ordered for display.</summary>
    /// <response code="200">All roles.</response>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RoleDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RoleDto>>>> Get(CancellationToken ct)
    {
        var roles = await _service.GetAllAsync(ct);

        var successMessage = RoleMessages.Success.RolesListed(AppLanguage.Current);
        var body = ApiResponse<IReadOnlyList<RoleDto>>.Success(successMessage, roles);
        return Ok(body);
    }

    #endregion

    #endregion
}
