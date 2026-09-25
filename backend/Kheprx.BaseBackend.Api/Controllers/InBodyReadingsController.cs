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

[Route("api/swimmers/{id:guid}/inbody-readings")]
public sealed class InBodyReadingsController : BaseApiController
{
    private readonly IInBodyReadingService _service;
    private readonly ISwimmerSelfAccessGuard _access;

    public InBodyReadingsController(IInBodyReadingService service, ISwimmerSelfAccessGuard access)
    {
        _service = service;
        _access = access;
    }

    /// <summary>Lists a swimmer's InBody readings, newest first.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<InBodyReadingDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<InBodyReadingDto>>>> List(Guid id, CancellationToken ct)
    {
        if (!await _access.CanReadAsync(User.IsInRole("swimmer"), CurrentUserId(), id, ct))
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<IReadOnlyList<InBodyReadingDto>>.Failure(SwimmerMessages.Errors.Forbidden(AppLanguage.Current), "forbidden"));

        var list = await _service.ListAsync(id, ct);
        return Ok(ApiResponse<IReadOnlyList<InBodyReadingDto>>.Success(InBodyReadingMessages.Success.Listed(AppLanguage.Current), list));
    }

    /// <summary>Records a new InBody reading. Head Coach or Captain only.</summary>
    [HttpPost]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<InBodyReadingDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<InBodyReadingDto>>> Create(Guid id, CreateInBodyReadingRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(id, request, CurrentUserId(), ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<InBodyReadingDto>.Success(InBodyReadingMessages.Success.Created(AppLanguage.Current), created));
    }

    /// <summary>Updates an InBody reading. Head Coach or Captain only.</summary>
    [HttpPut("{readingId:guid}")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<InBodyReadingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InBodyReadingDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<InBodyReadingDto>>> Update(Guid id, Guid readingId, CreateInBodyReadingRequest request, CancellationToken ct)
    {
        var updated = await _service.UpdateAsync(id, readingId, request, ct);
        if (updated is null)
        {
            var nf = ApiResponse<InBodyReadingDto>.Failure(InBodyReadingMessages.Errors.NotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<InBodyReadingDto>.Success(InBodyReadingMessages.Success.Updated(AppLanguage.Current), updated));
    }

    /// <summary>Deletes an InBody reading. Head Coach or Captain only.</summary>
    [HttpDelete("{readingId:guid}")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, Guid readingId, CancellationToken ct)
    {
        var deleted = await _service.DeleteAsync(id, readingId, ct);
        if (!deleted)
        {
            var nf = ApiResponse<object>.Failure(InBodyReadingMessages.Errors.NotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<object>.Success(InBodyReadingMessages.Success.Deleted(AppLanguage.Current), null));
    }
}
