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

    #region Profile — GET api/swimmers/{id} — identity + latest vitals

    /// <summary>Returns a swimmer's profile: identity plus the latest medical exam (vitals).</summary>
    /// <response code="200">The swimmer profile.</response>
    /// <response code="404">No swimmer with that id.</response>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SwimmerProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SwimmerProfileDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SwimmerProfileDto>>> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _service.GetProfileAsync(id, ct);
        if (dto is null)
        {
            var nf = ApiResponse<SwimmerProfileDto>.Failure(
                SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        var body = ApiResponse<SwimmerProfileDto>.Success(SwimmerMessages.Success.ProfileRetrieved(AppLanguage.Current), dto);
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

    #region Record exam — POST api/swimmers/{id}/medical-exams

    /// <summary>Records a new dated medical exam for a swimmer (updates the shown vitals). Head Coach or Captain only.</summary>
    /// <response code="201">Exam recorded; returns the new vitals.</response>
    /// <response code="404">No swimmer with that id.</response>
    [HttpPost("{id:guid}/medical-exams")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<SwimmerVitalsDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<SwimmerVitalsDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SwimmerVitalsDto>>> CreateExam(Guid id, CreateMedicalExamRequest request, CancellationToken ct)
    {
        var vitals = await _service.CreateExamAsync(id, request, ct);
        if (vitals is null)
        {
            var nf = ApiResponse<SwimmerVitalsDto>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        var body = ApiResponse<SwimmerVitalsDto>.Success(SwimmerMessages.Success.ExamRecorded(AppLanguage.Current), vitals);
        return StatusCode(StatusCodes.Status201Created, body);
    }

    #endregion

    #region Update identity — PUT api/swimmers/{id}/identity

    /// <summary>Updates a swimmer's identity (name EN/AR, DOB, phone). Head Coach or Captain only.</summary>
    /// <response code="200">Identity updated.</response>
    /// <response code="404">No swimmer with that id.</response>
    [HttpPut("{id:guid}/identity")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateIdentity(Guid id, UpdateSwimmerIdentityRequest request, CancellationToken ct)
    {
        var updated = await _service.UpdateIdentityAsync(id, request, ct);
        if (!updated)
        {
            var nf = ApiResponse<object>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<object>.Success(SwimmerMessages.Success.IdentityUpdated(AppLanguage.Current), null));
    }

    #endregion

    #region Exams — GET list / PUT update / DELETE

    /// <summary>Lists all medical exams for a swimmer, newest first.</summary>
    /// <response code="200">The exam history.</response>
    /// <response code="404">No swimmer with that id.</response>
    [HttpGet("{id:guid}/medical-exams")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SwimmerVitalsDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SwimmerVitalsDto>>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SwimmerVitalsDto>>>> ListExams(Guid id, CancellationToken ct)
    {
        var exams = await _service.ListExamsAsync(id, ct);
        if (exams is null)
        {
            var nf = ApiResponse<IReadOnlyList<SwimmerVitalsDto>>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<IReadOnlyList<SwimmerVitalsDto>>.Success(SwimmerMessages.Success.ExamsListed(AppLanguage.Current), exams));
    }

    /// <summary>Updates a specific medical exam (all fields). Head Coach or Captain only.</summary>
    /// <response code="200">Exam updated; returns the new vitals.</response>
    /// <response code="404">No such exam for this swimmer.</response>
    [HttpPut("{id:guid}/medical-exams/{examId:guid}")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<SwimmerVitalsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SwimmerVitalsDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SwimmerVitalsDto>>> UpdateExam(Guid id, Guid examId, CreateMedicalExamRequest request, CancellationToken ct)
    {
        var vitals = await _service.UpdateExamAsync(id, examId, request, ct);
        if (vitals is null)
        {
            var nf = ApiResponse<SwimmerVitalsDto>.Failure(SwimmerMessages.Errors.ExamNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<SwimmerVitalsDto>.Success(SwimmerMessages.Success.ExamUpdated(AppLanguage.Current), vitals));
    }

    /// <summary>Deletes a specific medical exam. Head Coach or Captain only.</summary>
    /// <response code="200">Exam deleted.</response>
    /// <response code="404">No such exam for this swimmer.</response>
    [HttpDelete("{id:guid}/medical-exams/{examId:guid}")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteExam(Guid id, Guid examId, CancellationToken ct)
    {
        var deleted = await _service.DeleteExamAsync(id, examId, ct);
        if (!deleted)
        {
            var nf = ApiResponse<object>.Failure(SwimmerMessages.Errors.ExamNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<object>.Success(SwimmerMessages.Success.ExamDeleted(AppLanguage.Current), null));
    }

    #endregion
}
