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
[Authorize(Roles = "head_coach,captain")]
public sealed class ObservationsController : BaseApiController
{
    private readonly IObservationService _service;
    public ObservationsController(IObservationService service) => _service = service;

    /// <summary>Adds a swimmer data field (observation). Head Coach or Captain only.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ObservationDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<ObservationDto>>> Create(CreateObservationRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, CurrentUserId(), ct);
        var body = ApiResponse<ObservationDto>.Success(
            ObservationMessages.Success.Added(AppLanguage.Current), created);
        return StatusCode(StatusCodes.Status201Created, body);
    }
}
