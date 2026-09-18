using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Route("api/medical-tests")]
[Authorize(Roles = "head_coach")]
public sealed class MedicalTestsController : BaseApiController
{
    private readonly IMedicalTestService _service;
    public MedicalTestsController(IMedicalTestService service) => _service = service;

    /// <summary>Lists the medical-test catalog.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MedicalTestDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MedicalTestDto>>>> List(CancellationToken ct)
    {
        var tests = await _service.ListAsync(ct);
        var body = ApiResponse<IReadOnlyList<MedicalTestDto>>.Success(
            MedicalTestMessages.Success.Listed(AppLanguage.Current), tests);
        return Ok(body);
    }

    /// <summary>Adds a medical test. Head Coach only.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<MedicalTestDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<MedicalTestDto>>> Create(CreateMedicalTestRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, CurrentUserId(), ct);
        var body = ApiResponse<MedicalTestDto>.Success(
            MedicalTestMessages.Success.Created(AppLanguage.Current), created);
        return StatusCode(StatusCodes.Status201Created, body);
    }

    /// <summary>Deletes a medical test by id. Head Coach only.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<Guid>>> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _service.DeleteAsync(id, ct);
        if (!deleted)
        {
            var notFound = ApiResponse<Guid>.Failure(MedicalTestMessages.Errors.NotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, notFound);
        }
        var body = ApiResponse<Guid>.Success(MedicalTestMessages.Success.Deleted(AppLanguage.Current), id);
        return Ok(body);
    }
}
