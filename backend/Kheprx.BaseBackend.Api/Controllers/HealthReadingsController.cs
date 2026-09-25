using Kheprx.BaseBackend.Api.Security;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Route("api/health-readings")]
[Authorize]
public sealed class HealthReadingsController : BaseApiController
{
    private readonly IHealthReadingService _service;
    private readonly ISwimmerSelfAccessGuard _access;

    public HealthReadingsController(IHealthReadingService service, ISwimmerSelfAccessGuard access)
    {
        _service = service;
        _access = access;
    }

    /// <summary>Lists a swimmer's readings (newest first), enriched with test details + status.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<HealthReadingListItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<HealthReadingListItemDto>>>> List([FromQuery] Guid swimmerId, CancellationToken ct)
    {
        if (!await _access.CanReadAsync(User.IsInRole("swimmer"), CurrentUserId(), swimmerId, ct))
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<IReadOnlyList<HealthReadingListItemDto>>.Failure(SwimmerMessages.Errors.Forbidden(AppLanguage.Current), "forbidden"));

        var list = await _service.ListBySwimmerAsync(swimmerId, ct);
        return Ok(ApiResponse<IReadOnlyList<HealthReadingListItemDto>>.Success(
            HealthReadingMessages.Success.Listed(AppLanguage.Current), list));
    }

    /// <summary>Logs a swimmer's test reading. Head Coach or Captain only.</summary>
    [HttpPost]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<HealthReadingDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<HealthReadingDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<HealthReadingDto>>> Create(CreateHealthReadingRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, CurrentUserId(), ct);
        if (created is null)
        {
            var notFound = ApiResponse<HealthReadingDto>.Failure(
                HealthReadingMessages.Errors.TestNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, notFound);
        }

        var body = ApiResponse<HealthReadingDto>.Success(
            HealthReadingMessages.Success.Logged(AppLanguage.Current), created);
        return StatusCode(StatusCodes.Status201Created, body);
    }

    /// <summary>Edits a reading's value. Head Coach or Captain only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<HealthReadingListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HealthReadingListItemDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<HealthReadingListItemDto>>> Update(Guid id, UpdateHealthReadingRequest request, CancellationToken ct)
    {
        var updated = await _service.UpdateAsync(id, request, ct);
        if (updated is null)
        {
            var nf = ApiResponse<HealthReadingListItemDto>.Failure(
                HealthReadingMessages.Errors.NotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<HealthReadingListItemDto>.Success(
            HealthReadingMessages.Success.Updated(AppLanguage.Current), updated));
    }

    /// <summary>Deletes a reading. Head Coach or Captain only.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _service.DeleteAsync(id, ct);
        if (!deleted)
        {
            var nf = ApiResponse<object>.Failure(
                HealthReadingMessages.Errors.NotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<object>.Success(HealthReadingMessages.Success.Deleted(AppLanguage.Current), null));
    }
}
