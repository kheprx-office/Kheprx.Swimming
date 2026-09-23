using Kheprx.BaseBackend.Championships.Application.DTOs;
using Kheprx.BaseBackend.Championships.Application.Resources;
using Kheprx.BaseBackend.Championships.Application.Services.Interfaces;
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

    public ChampionshipsController(IChampionshipService service, IReferenceService reference)
    {
        _service = service;
        _reference = reference;
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
}
