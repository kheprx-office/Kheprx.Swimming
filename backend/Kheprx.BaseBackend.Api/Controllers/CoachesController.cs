using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Authorize(Roles = "head_coach")]
public sealed class CoachesController : BaseApiController
{
    private readonly ICoachService _service;

    public CoachesController(ICoachService service)
    {
        _service = service;
    }

    /// <summary>Registers a captain or head coach (app_user + profile). Head Coach only.</summary>
    /// <response code="201">Account created.</response>
    /// <response code="409">Username, email, or national ID already in use.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreatedCoachDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CreatedCoachDto>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CreatedCoachDto>>> Create(CreateCoachRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, ct);
        if (created is null)
        {
            var conflict = ApiResponse<CreatedCoachDto>.Failure(
                CoachMessages.Errors.NationalIdTaken(AppLanguage.Current), "conflict");
            return StatusCode(StatusCodes.Status409Conflict, conflict);
        }

        var body = ApiResponse<CreatedCoachDto>.Success(CoachMessages.Success.CoachCreated(AppLanguage.Current), created);
        return StatusCode(StatusCodes.Status201Created, body);
    }
}
