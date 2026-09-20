using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Route("api/observations")]
[Authorize]
public sealed class ObservationsController : BaseApiController
{
    private readonly IObservationService _service;
    public ObservationsController(IObservationService service) => _service = service;

    /// <summary>Lists a swimmer's data fields (observations), newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ObservationDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ObservationDto>>>> List([FromQuery] Guid swimmerId, CancellationToken ct)
    {
        var list = await _service.ListBySwimmerAsync(swimmerId, ct);
        return Ok(ApiResponse<IReadOnlyList<ObservationDto>>.Success(ObservationMessages.Success.Listed(AppLanguage.Current), list));
    }

    /// <summary>Adds a swimmer data field (observation). Head Coach or Captain only.</summary>
    [HttpPost]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<ObservationDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<ObservationDto>>> Create(CreateObservationRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, CurrentUserId(), ct);
        var body = ApiResponse<ObservationDto>.Success(
            ObservationMessages.Success.Added(AppLanguage.Current), created);
        return StatusCode(StatusCodes.Status201Created, body);
    }

    /// <summary>Edits a swimmer data field. Head Coach or Captain only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<ObservationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ObservationDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ObservationDto>>> Update(Guid id, UpdateObservationRequest request, CancellationToken ct)
    {
        var updated = await _service.UpdateAsync(id, request, ct);
        if (updated is null)
        {
            var nf = ApiResponse<ObservationDto>.Failure(ObservationMessages.Errors.NotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<ObservationDto>.Success(ObservationMessages.Success.Updated(AppLanguage.Current), updated));
    }

    /// <summary>Deletes a swimmer data field. Head Coach or Captain only.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _service.DeleteAsync(id, ct);
        if (!deleted)
        {
            var nf = ApiResponse<object>.Failure(ObservationMessages.Errors.NotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<object>.Success(ObservationMessages.Success.Deleted(AppLanguage.Current), null));
    }
}
