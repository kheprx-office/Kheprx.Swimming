using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

public sealed class SwimmersController : BaseApiController
{
    #region Fields

    private readonly ISwimmerService _service;

    #endregion

    #region Constructor

    public SwimmersController(ISwimmerService service)
    {
        _service = service;
    }

    #endregion

    #region Count — GET api/swimmers/count — total tracked swimmers

    // مستخدم في:
    // 1. صفحة تسجيل الدخول (/login) — إحصائية عدد السبّاحين
    /// <summary>Returns the total number of tracked swimmers.</summary>
    /// <remarks>Anonymous: the login screen shows this stat before authentication.</remarks>
    /// <response code="200">The swimmer count.</response>
    [HttpGet("count")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<SwimmerCountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SwimmerCountDto>>> Count(CancellationToken ct)
    {
        var dto = await _service.GetCountAsync(ct);
        var successMessage = SwimmerMessages.Success.CountRetrieved(AppLanguage.Current);
        var body = ApiResponse<SwimmerCountDto>.Success(successMessage, dto);
        return Ok(body);
    }

    #endregion

    #region List — GET api/swimmers — roster list, optional search

    // مستخدم في:
    // 1. صفحة السبّاحين (/swimmers) — القائمة + البحث
    /// <summary>Lists registered swimmers, optionally filtered by a search term (name or UID).</summary>
    /// <remarks>Any authenticated user may view the roster.</remarks>
    /// <response code="200">The matching swimmers.</response>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SwimmerListItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SwimmerListItemDto>>>> List(
        [FromQuery] string? search, CancellationToken ct)
    {
        var swimmers = await _service.ListAsync(search, ct);
        var successMessage = SwimmerMessages.Success.SwimmersListed(AppLanguage.Current);
        var body = ApiResponse<IReadOnlyList<SwimmerListItemDto>>.Success(successMessage, swimmers);
        return Ok(body);
    }

    #endregion

    #region Create — POST api/swimmers — register a swimmer

    /// <summary>Registers a swimmer (app_user + profile + specializations). Head Coach or Captain only.</summary>
    /// <response code="201">Swimmer created.</response>
    /// <response code="409">Username or email already in use.</response>
    [HttpPost]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<CreatedSwimmerDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CreatedSwimmerDto>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CreatedSwimmerDto>>> Create(CreateSwimmerRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, ct);
        if (created is null)
        {
            var conflict = ApiResponse<CreatedSwimmerDto>.Failure(
                SwimmerMessages.Errors.UsernameTaken(AppLanguage.Current), "conflict");
            return StatusCode(StatusCodes.Status409Conflict, conflict);
        }

        var body = ApiResponse<CreatedSwimmerDto>.Success(SwimmerMessages.Success.SwimmerCreated(AppLanguage.Current), created);
        return StatusCode(StatusCodes.Status201Created, body);
    }

    #endregion
}
