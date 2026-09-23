using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Authorize]
public sealed class ReferenceController : BaseApiController
{
    private readonly IReferenceService _service;

    public ReferenceController(IReferenceService service)
    {
        _service = service;
    }

    /// <summary>Lists all clubs (training / championship club selectors).</summary>
    [HttpGet("clubs")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ClubDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ClubDto>>>> Clubs(CancellationToken ct)
    {
        var data = await _service.GetClubsAsync(ct);
        var body = ApiResponse<IReadOnlyList<ClubDto>>.Success(
            ReferenceMessages.Success.ClubsListed(AppLanguage.Current), data);
        return Ok(body);
    }

    /// <summary>Lists all blood types.</summary>
    [HttpGet("blood-types")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CodedLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CodedLookupDto>>>> BloodTypes(CancellationToken ct)
    {
        var data = await _service.GetBloodTypesAsync(ct);
        var body = ApiResponse<IReadOnlyList<CodedLookupDto>>.Success(
            ReferenceMessages.Success.BloodTypesListed(AppLanguage.Current), data);
        return Ok(body);
    }

    /// <summary>Lists all swim strokes (specialization chips; IM = medley).</summary>
    [HttpGet("strokes")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CodedLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CodedLookupDto>>>> Strokes(CancellationToken ct)
    {
        var data = await _service.GetStrokesAsync(ct);
        var body = ApiResponse<IReadOnlyList<CodedLookupDto>>.Success(
            ReferenceMessages.Success.StrokesListed(AppLanguage.Current), data);
        return Ok(body);
    }

    /// <summary>Lists all genders.</summary>
    [HttpGet("genders")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CodedLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CodedLookupDto>>>> Genders(CancellationToken ct)
    {
        var data = await _service.GetGendersAsync(ct);
        var body = ApiResponse<IReadOnlyList<CodedLookupDto>>.Success(
            ReferenceMessages.Success.GendersListed(AppLanguage.Current), data);
        return Ok(body);
    }

    /// <summary>Lists all observation categories (Swimmer Data Fields).</summary>
    [HttpGet("observation-categories")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CodedLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CodedLookupDto>>>> ObservationCategories(CancellationToken ct)
    {
        var data = await _service.GetObservationCategoriesAsync(ct);
        var body = ApiResponse<IReadOnlyList<CodedLookupDto>>.Success(
            ReferenceMessages.Success.ObservationCategoriesListed(AppLanguage.Current), data);
        return Ok(body);
    }

    /// <summary>Lists all fitness assessment results (Internal Medicine / Heart / Spine selectors).</summary>
    [HttpGet("fitness-assessments")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CodedLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CodedLookupDto>>>> FitnessAssessments(CancellationToken ct)
    {
        var data = await _service.GetFitnessAssessmentsAsync(ct);
        var body = ApiResponse<IReadOnlyList<CodedLookupDto>>.Success(
            ReferenceMessages.Success.FitnessAssessmentsListed(AppLanguage.Current), data);
        return Ok(body);
    }

    /// <summary>Lists all feedback categories (coach evaluation categories).</summary>
    [HttpGet("feedback-categories")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CodedLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CodedLookupDto>>>> FeedbackCategories(CancellationToken ct)
    {
        var data = await _service.GetFeedbackCategoriesAsync(ct);
        var body = ApiResponse<IReadOnlyList<CodedLookupDto>>.Success(
            ReferenceMessages.Success.FeedbackCategoriesListed(AppLanguage.Current), data);
        return Ok(body);
    }

    /// <summary>Lists all attendance statuses (present / late / absent / excused).</summary>
    [HttpGet("attendance-statuses")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CodedLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CodedLookupDto>>>> AttendanceStatuses(CancellationToken ct)
    {
        var data = await _service.GetAttendanceStatusesAsync(ct);
        var body = ApiResponse<IReadOnlyList<CodedLookupDto>>.Success(
            ReferenceMessages.Success.AttendanceStatusesListed(AppLanguage.Current), data);
        return Ok(body);
    }
}
