using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Route("api/health-readings")]
[Authorize(Roles = "head_coach,captain")]
public sealed class HealthReadingsController : BaseApiController
{
    private readonly IHealthReadingService _service;
    public HealthReadingsController(IHealthReadingService service) => _service = service;

    /// <summary>Logs a swimmer's test reading. Head Coach or Captain only.</summary>
    [HttpPost]
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
}
