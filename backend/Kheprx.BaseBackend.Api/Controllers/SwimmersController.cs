using Kheprx.BaseBackend.Api.Security;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
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
    private readonly IObservationService _observations;
    private readonly IInBodyReadingService _inbody;
    private readonly ISwimmerSelfAccessGuard _access;

    #endregion

    #region Constructor

    public SwimmersController(ISwimmerService service, IObservationService observations, IInBodyReadingService inbody, ISwimmerSelfAccessGuard access)
    {
        _service = service;
        _observations = observations;
        _inbody = inbody;
        _access = access;
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
        if (!await _access.CanReadAsync(User.IsInRole("swimmer"), CurrentUserId(), id, ct))
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<SwimmerProfileDto>.Failure(SwimmerMessages.Errors.Forbidden(AppLanguage.Current), "forbidden"));

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
        if (!await _access.CanReadAsync(User.IsInRole("swimmer"), CurrentUserId(), id, ct))
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<IReadOnlyList<SwimmerVitalsDto>>.Failure(SwimmerMessages.Errors.Forbidden(AppLanguage.Current), "forbidden"));

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

    #region Guardians — GET / PUT api/swimmers/{id}/guardians

    /// <summary>Returns a swimmer's guardians (father + mother; either may be null).</summary>
    /// <response code="200">The guardians.</response>
    /// <response code="404">No swimmer with that id.</response>
    [HttpGet("{id:guid}/guardians")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SwimmerGuardiansDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SwimmerGuardiansDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SwimmerGuardiansDto>>> GetGuardians(Guid id, CancellationToken ct)
    {
        if (!await _access.CanReadAsync(User.IsInRole("swimmer"), CurrentUserId(), id, ct))
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<SwimmerGuardiansDto>.Failure(SwimmerMessages.Errors.Forbidden(AppLanguage.Current), "forbidden"));

        var dto = await _service.GetGuardiansAsync(id, ct);
        if (dto is null)
        {
            var nf = ApiResponse<SwimmerGuardiansDto>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<SwimmerGuardiansDto>.Success(SwimmerMessages.Success.GuardiansRetrieved(AppLanguage.Current), dto));
    }

    /// <summary>Upserts both guardians (father + mother). Head Coach or Captain only.</summary>
    /// <response code="200">Guardians saved.</response>
    /// <response code="404">No swimmer with that id.</response>
    [HttpPut("{id:guid}/guardians")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> UpsertGuardians(Guid id, UpsertGuardiansRequest request, CancellationToken ct)
    {
        var saved = await _service.UpsertGuardiansAsync(id, request, ct);
        if (!saved)
        {
            var nf = ApiResponse<object>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<object>.Success(SwimmerMessages.Success.GuardiansSaved(AppLanguage.Current), null));
    }

    #endregion

    #region Body measurements — GET latest / POST api/swimmers/{id}/body-measurements

    /// <summary>Returns a swimmer's latest body measurement (null when none recorded yet).</summary>
    /// <response code="200">The latest body measurement (or empty).</response>
    /// <response code="404">No swimmer with that id.</response>
    [HttpGet("{id:guid}/body-measurements/latest")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SwimmerBodyMeasurementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SwimmerBodyMeasurementDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SwimmerBodyMeasurementDto>>> GetLatestBodyMeasurement(Guid id, CancellationToken ct)
    {
        if (!await _access.CanReadAsync(User.IsInRole("swimmer"), CurrentUserId(), id, ct))
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<SwimmerBodyMeasurementDto>.Failure(SwimmerMessages.Errors.Forbidden(AppLanguage.Current), "forbidden"));

        var dto = await _service.GetBodyMeasurementAsync(id, ct);
        if (dto is null)
        {
            var nf = ApiResponse<SwimmerBodyMeasurementDto>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<SwimmerBodyMeasurementDto>.Success(SwimmerMessages.Success.BodyMeasurementRetrieved(AppLanguage.Current), dto));
    }

    /// <summary>Records a new dated body measurement for a swimmer. Head Coach or Captain only.</summary>
    /// <response code="200">Body measurement saved.</response>
    /// <response code="404">No swimmer with that id.</response>
    [HttpPost("{id:guid}/body-measurements")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> CreateBodyMeasurement(Guid id, CreateBodyMeasurementRequest request, CancellationToken ct)
    {
        var saved = await _service.AddBodyMeasurementAsync(id, request, ct);
        if (!saved)
        {
            var nf = ApiResponse<object>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<object>.Success(SwimmerMessages.Success.BodyMeasurementSaved(AppLanguage.Current), null));
    }

    #endregion

    #region Self-reference — GET api/swimmers/me

    /// <summary>Returns the calling swimmer's own profile id. Swimmer self-service.</summary>
    /// <response code="200">The caller's swimmer id.</response>
    /// <response code="404">The caller has no swimmer profile.</response>
    [HttpGet("me")]
    [Authorize(Roles = "swimmer")]
    [ProducesResponseType(typeof(ApiResponse<MySwimmerRefDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MySwimmerRefDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<MySwimmerRefDto>>> GetMe(CancellationToken ct)
    {
        var id = await _service.GetSwimmerIdByUserAsync(CurrentUserId(), ct);
        if (id is null)
            return StatusCode(StatusCodes.Status404NotFound,
                ApiResponse<MySwimmerRefDto>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found"));
        return Ok(ApiResponse<MySwimmerRefDto>.Success(
            SwimmerMessages.Success.ProfileRetrieved(AppLanguage.Current), new MySwimmerRefDto(id.Value)));
    }

    #endregion

    #region Onboarding (self-service) — GET/POST api/swimmers/me/onboarding/identity-vitals

    /// <summary>Prefill for the swimmer's own first-login wizard, Step 1. Swimmer only; resolved from the JWT.</summary>
    /// <response code="200">The prefill.</response>
    /// <response code="404">The caller is not a swimmer / has no profile.</response>
    [HttpGet("me/onboarding/identity-vitals")]
    [Authorize(Roles = "swimmer")]
    [ProducesResponseType(typeof(ApiResponse<OnboardingPrefillDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OnboardingPrefillDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OnboardingPrefillDto>>> GetOnboardingPrefill(CancellationToken ct)
    {
        var dto = await _service.GetOnboardingPrefillAsync(CurrentUserId(), ct);
        if (dto is null)
        {
            var nf = ApiResponse<OnboardingPrefillDto>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<OnboardingPrefillDto>.Success(SwimmerMessages.Success.OnboardingPrefillRetrieved(AppLanguage.Current), dto));
    }

    /// <summary>Saves the swimmer's own first-login Step 1 (identity + medical exam), idempotently, and advances to Step 2. Does NOT clear first-login. Swimmer only.</summary>
    /// <response code="200">Saved; first-login stays true until the InBody step completes onboarding.</response>
    /// <response code="404">The caller is not a swimmer / has no profile.</response>
    [HttpPost("me/onboarding/identity-vitals")]
    [Authorize(Roles = "swimmer")]
    [ProducesResponseType(typeof(ApiResponse<OnboardingStepResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OnboardingStepResultDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OnboardingStepResultDto>>> CompleteOnboardingIdentityVitals(
        CompleteIdentityVitalsRequest request, CancellationToken ct)
    {
        var result = await _service.CompleteIdentityVitalsAsync(CurrentUserId(), request, ct);
        if (result is null)
        {
            var nf = ApiResponse<OnboardingStepResultDto>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<OnboardingStepResultDto>.Success(SwimmerMessages.Success.OnboardingCompleted(AppLanguage.Current), result));
    }

    /// <summary>Saves the swimmer's first-login Step 3 (physiological measurements), idempotently, and advances to Step 4. Does NOT clear first-login. Swimmer only.</summary>
    /// <response code="200">Saved; first-login stays true until the InBody step completes onboarding.</response>
    /// <response code="404">The caller is not a swimmer / has no profile.</response>
    [HttpPost("me/onboarding/physiological")]
    [Authorize(Roles = "swimmer")]
    [ProducesResponseType(typeof(ApiResponse<OnboardingStepResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OnboardingStepResultDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OnboardingStepResultDto>>> CompleteOnboardingPhysiological(
        CompletePhysiologicalRequest request, CancellationToken ct)
    {
        var ok = await _service.CompleteOnboardingPhysiologicalAsync(CurrentUserId(), request, ct);
        if (!ok)
        {
            var nf = ApiResponse<OnboardingStepResultDto>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<OnboardingStepResultDto>.Success(
            SwimmerMessages.Success.OnboardingCompleted(AppLanguage.Current), new OnboardingStepResultDto(false)));
    }

    /// <summary>Completes the swimmer's first-login Step 4 (InBody reading), clearing first-login. Swimmer only.</summary>
    /// <response code="200">Completed; returns the refreshed first-login flag (false).</response>
    /// <response code="404">The caller is not a swimmer / has no profile.</response>
    [HttpPost("me/onboarding/inbody")]
    [Authorize(Roles = "swimmer")]
    [ProducesResponseType(typeof(ApiResponse<OnboardingStepResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OnboardingStepResultDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OnboardingStepResultDto>>> CompleteOnboardingInBody(
        CompleteInBodyRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId();

        // 1) Resolve the swimmer (Identity); null → not a swimmer.
        var swimmerId = await _service.GetSwimmerIdByUserAsync(userId, ct);
        if (swimmerId is null)
        {
            var nf = ApiResponse<OnboardingStepResultDto>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }

        // 2) Save the InBody reading (Health) — update the latest reading if one exists (idempotent
        //    Back→edit→Next / resume re-run), else insert. Date server-set to today, recorded by the swimmer.
        var reading = new CreateInBodyReadingRequest(DateOnly.FromDateTime(DateTime.UtcNow),
            request.HeightCm, request.WeightKg, request.FatPct, request.MusclePct, request.WaterPct, request.BoneDensity, request.BodyDensity);
        var existing = await _inbody.ListAsync(swimmerId.Value, ct);
        if (existing.Count > 0)
            await _inbody.UpdateAsync(swimmerId.Value, existing[0].Id, reading, ct);
        else
            await _inbody.CreateAsync(swimmerId.Value, reading, userId, ct);

        // 3) Clear first-login LAST — the commit point for onboarding.
        await _service.CompleteOnboardingAsync(userId, ct);

        return Ok(ApiResponse<OnboardingStepResultDto>.Success(
            SwimmerMessages.Success.OnboardingCompleted(AppLanguage.Current), new OnboardingStepResultDto(false)));
    }

    /// <summary>Completes the swimmer's first-login Step 2 (guardians + medical history), advancing to Step 3. Swimmer only.</summary>
    /// <response code="200">Saved; returns the first-login flag (still true until Step 3 completes).</response>
    /// <response code="404">The caller is not a swimmer / has no profile.</response>
    [HttpPost("me/onboarding/guardian-medical")]
    [Authorize(Roles = "swimmer")]
    [ProducesResponseType(typeof(ApiResponse<OnboardingStepResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OnboardingStepResultDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OnboardingStepResultDto>>> CompleteOnboardingGuardianMedical(
        CompleteGuardianMedicalRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId();

        // 1) Guardians (Identity). Returns the swimmer id needed for observations; null → not a swimmer.
        var swimmerId = await _service.UpsertOnboardingGuardiansAsync(userId, request.Father, request.Mother, ct);
        if (swimmerId is null)
        {
            var nf = ApiResponse<OnboardingStepResultDto>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }

        // 2) Medical history (Health) — replace the swimmer's observations with the current "Yes" set (idempotent).
        var items = request.Medical
            .Select(m => new CreateObservationRequest(swimmerId.Value, m.CategoryId, m.FieldLabel, m.Value))
            .ToList();
        await _observations.ReplaceForSwimmerAsync(swimmerId.Value, items, userId, ct);

        // Onboarding is NOT completed here — Step 4 (InBody) is the finish line.
        return Ok(ApiResponse<OnboardingStepResultDto>.Success(
            SwimmerMessages.Success.OnboardingCompleted(AppLanguage.Current), new OnboardingStepResultDto(false)));
    }

    #endregion
}
