using Kheprx.BaseBackend.Api.Security;
using Kheprx.BaseBackend.Championships.Application.DTOs;
using Kheprx.BaseBackend.Championships.Application.Resources;
using Kheprx.BaseBackend.Championships.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Route("api/championships")]
[Authorize]
public sealed class ChampionshipsController : BaseApiController
{
    private readonly IChampionshipService _service;
    private readonly IReferenceService _reference;
    private readonly ISwimmerSelfAccessGuard _access;

    public ChampionshipsController(IChampionshipService service, IReferenceService reference, ISwimmerSelfAccessGuard access)
    {
        _service = service;
        _reference = reference;
        _access = access;
    }

    /// <summary>Lists all championship events (newest start first), with status code + names resolved.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CompetitionEventDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CompetitionEventDto>>>> List(CancellationToken ct)
    {
        var rows = await _service.ListAsync(ct);
        var statuses = await _reference.GetCompetitionStatusesAsync(ct);
        var byId = statuses.ToDictionary(s => s.Id);

        var enriched = rows.Select(r => byId.TryGetValue(r.StatusId, out var s)
            ? r with { StatusCode = s.Code, StatusNameEn = s.NameEn, StatusNameAr = s.NameAr }
            : r).ToList();

        return Ok(ApiResponse<IReadOnlyList<CompetitionEventDto>>.Success(
            ChampionshipMessages.Success.Listed(AppLanguage.Current), enriched));
    }

    /// <summary>Creates a championship (status defaults to 'upcoming', created_by = current user).
    /// The single Name/Location map to the current-language column. Head Coach or Captain only.</summary>
    [HttpPost]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<CompetitionEventDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CompetitionEventDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CompetitionEventDto>>> Create(CreateCompetitionEventRequest request, CancellationToken ct)
    {
        var lang = AppLanguage.Current;
        var name = request.Name?.Trim() ?? string.Empty;
        var location = request.Location?.Trim() ?? string.Empty;

        if (name.Length == 0)
            return BadRequest(ApiResponse<CompetitionEventDto>.Failure(ChampionshipMessages.Errors.NameRequired(lang), "validation"));
        if (location.Length == 0)
            return BadRequest(ApiResponse<CompetitionEventDto>.Failure(ChampionshipMessages.Errors.LocationRequired(lang), "validation"));
        if (request.EndDate < request.StartDate)
            return BadRequest(ApiResponse<CompetitionEventDto>.Failure(ChampionshipMessages.Errors.EndBeforeStart(lang), "validation"));

        var statuses = await _reference.GetCompetitionStatusesAsync(ct);
        var upcoming = statuses.FirstOrDefault(s => s.Code == "upcoming");
        if (upcoming is null)
            return BadRequest(ApiResponse<CompetitionEventDto>.Failure(ChampionshipMessages.Errors.StatusUnavailable(lang), "validation"));

        // English is the required (NOT NULL) anchor column; the Arabic column is filled too when the
        // request culture is Arabic, so the author's language shows and the anchor is never null.
        var toAr = lang == "ar";
        var command = new CreateCompetitionEventCommand(
            name, toAr ? name : null,
            request.StartDate, request.EndDate,
            location, toAr ? location : null,
            upcoming.Id, CurrentUserId());

        var created = await _service.CreateAsync(command, ct);
        var enriched = created with { StatusCode = upcoming.Code, StatusNameEn = upcoming.NameEn, StatusNameAr = upcoming.NameAr };

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<CompetitionEventDto>.Success(ChampionshipMessages.Success.Created(lang), enriched));
    }

    /// <summary>Returns a single championship event with its status resolved. 404 when unknown.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CompetitionEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CompetitionEventDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CompetitionEventDto>>> GetById(Guid id, CancellationToken ct)
    {
        var lang = AppLanguage.Current;
        var dto = await _service.GetByIdAsync(id, ct);
        if (dto is null)
            return StatusCode(StatusCodes.Status404NotFound,
                ApiResponse<CompetitionEventDto>.Failure(ChampionshipMessages.NotFound.Event(lang), "not_found"));

        var statuses = await _reference.GetCompetitionStatusesAsync(ct);
        var s = statuses.FirstOrDefault(x => x.Id == dto.StatusId);
        var enriched = s is null ? dto
            : dto with { StatusCode = s.Code, StatusNameEn = s.NameEn, StatusNameAr = s.NameAr };

        return Ok(ApiResponse<CompetitionEventDto>.Success(ChampionshipMessages.Success.Listed(lang), enriched));
    }

    /// <summary>Lists the swimmer ids enrolled in an event. 404 when the event is unknown.</summary>
    [HttpGet("{eventId:guid}/enrollments")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<Guid>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<Guid>>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Guid>>>> GetEnrollments(Guid eventId, CancellationToken ct)
    {
        var lang = AppLanguage.Current;
        var ids = await _service.GetEnrolledSwimmerIdsAsync(eventId, ct);
        if (ids is null)
            return StatusCode(StatusCodes.Status404NotFound,
                ApiResponse<IReadOnlyList<Guid>>.Failure(ChampionshipMessages.NotFound.Event(lang), "not_found"));

        return Ok(ApiResponse<IReadOnlyList<Guid>>.Success(ChampionshipMessages.EnrollmentSuccess.Retrieved(lang), ids));
    }

    /// <summary>Replaces the whole enrolled-swimmer set for an event. Head Coach or Captain only.</summary>
    [HttpPut("{eventId:guid}/enrollments")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> SetEnrollments(Guid eventId, SetEnrollmentsRequest request, CancellationToken ct)
    {
        var lang = AppLanguage.Current;
        var swimmerIds = request.SwimmerIds ?? new List<Guid>();
        var ok = await _service.SetEnrollmentsAsync(eventId, swimmerIds, ct);
        if (!ok)
            return StatusCode(StatusCodes.Status404NotFound,
                ApiResponse<object>.Failure(ChampionshipMessages.NotFound.Event(lang), "not_found"));

        return Ok(ApiResponse<object>.Success(ChampionshipMessages.EnrollmentSuccess.Saved(lang), null));
    }

    /// <summary>Returns the full day → race → assigned-swimmer schedule for an event. 404 when unknown.</summary>
    [HttpGet("{eventId:guid}/schedule")]
    [ProducesResponseType(typeof(ApiResponse<ScheduleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ScheduleDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ScheduleDto>>> GetSchedule(Guid eventId, CancellationToken ct)
    {
        var lang = AppLanguage.Current;
        var dto = await _service.GetScheduleAsync(eventId, ct);
        if (dto is null)
            return StatusCode(StatusCodes.Status404NotFound,
                ApiResponse<ScheduleDto>.Failure(ChampionshipMessages.NotFound.Event(lang), "not_found"));

        return Ok(ApiResponse<ScheduleDto>.Success(ChampionshipMessages.ScheduleSuccess.Retrieved(lang), dto));
    }

    /// <summary>Replaces the whole schedule for an event (atomic). Head Coach or Captain only.
    /// Every assigned swimmer must be enrolled in the event.</summary>
    [HttpPut("{eventId:guid}/schedule")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<ScheduleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ScheduleDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ScheduleDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ScheduleDto>>> SetSchedule(Guid eventId, SetScheduleRequest request, CancellationToken ct)
    {
        var lang = AppLanguage.Current;
        var days = request.Days ?? new List<SetScheduleDay>();
        var result = await _service.SetScheduleAsync(eventId, days, ct);

        return result.Outcome switch
        {
            SetScheduleOutcome.NotFound => StatusCode(StatusCodes.Status404NotFound,
                ApiResponse<ScheduleDto>.Failure(ChampionshipMessages.NotFound.Event(lang), "not_found")),
            SetScheduleOutcome.Invalid => BadRequest(
                ApiResponse<ScheduleDto>.Failure(ChampionshipMessages.ScheduleErrors.Invalid(lang), "validation")),
            _ => Ok(ApiResponse<ScheduleDto>.Success(ChampionshipMessages.ScheduleSuccess.Saved(lang), result.Saved!)),
        };
    }

    /// <summary>Returns all recorded race results for an event. 404 when the event is unknown.</summary>
    [HttpGet("{eventId:guid}/results")]
    [ProducesResponseType(typeof(ApiResponse<ResultsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ResultsDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ResultsDto>>> GetResults(Guid eventId, CancellationToken ct)
    {
        var lang = AppLanguage.Current;
        var dto = await _service.GetResultsAsync(eventId, ct);
        if (dto is null)
            return StatusCode(StatusCodes.Status404NotFound,
                ApiResponse<ResultsDto>.Failure(ChampionshipMessages.NotFound.Event(lang), "not_found"));

        return Ok(ApiResponse<ResultsDto>.Success(ChampionshipMessages.ResultsSuccess.Retrieved(lang), dto));
    }

    /// <summary>Replaces the recorded times for one race (atomic). Head Coach or Captain only.
    /// Every entered swimmer must be assigned to the race; times are positive milliseconds.</summary>
    [HttpPut("{eventId:guid}/races/{raceSessionId:guid}/results")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<ResultsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ResultsDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ResultsDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ResultsDto>>> SetRaceResults(Guid eventId, Guid raceSessionId, SetRaceResultsRequest request, CancellationToken ct)
    {
        var lang = AppLanguage.Current;
        var entries = request.Entries ?? new List<SetRaceResultsEntry>();
        var result = await _service.SetRaceResultsAsync(eventId, raceSessionId, entries, CurrentUserId(), ct);

        return result.Outcome switch
        {
            SetRaceResultsOutcome.NotFound => StatusCode(StatusCodes.Status404NotFound,
                ApiResponse<ResultsDto>.Failure(ChampionshipMessages.NotFound.Event(lang), "not_found")),
            SetRaceResultsOutcome.Invalid => BadRequest(
                ApiResponse<ResultsDto>.Failure(ChampionshipMessages.ResultsErrors.Invalid(lang), "validation")),
            _ => Ok(ApiResponse<ResultsDto>.Success(ChampionshipMessages.ResultsSuccess.Saved(lang), result.Saved!)),
        };
    }

    /// <summary>Returns the swimmer's championship participation history — every enrolled event (newest first)
    /// with the swimmer's races and times. Always 200 (empty list when the swimmer joined nothing).</summary>
    [HttpGet("swimmer/{swimmerId:guid}/history")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ChampionshipSwimmerHistoryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ChampionshipSwimmerHistoryDto>>>> GetSwimmerHistory(Guid swimmerId, CancellationToken ct)
    {
        if (!await _access.CanReadAsync(User.IsInRole("swimmer"), CurrentUserId(), swimmerId, ct))
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<IReadOnlyList<ChampionshipSwimmerHistoryDto>>.Failure(SwimmerMessages.Errors.Forbidden(AppLanguage.Current), "forbidden"));

        var rows = await _service.GetSwimmerHistoryAsync(swimmerId, ct);
        return Ok(ApiResponse<IReadOnlyList<ChampionshipSwimmerHistoryDto>>.Success(
            ChampionshipMessages.SwimmerHistorySuccess.Retrieved(AppLanguage.Current), rows));
    }
}
