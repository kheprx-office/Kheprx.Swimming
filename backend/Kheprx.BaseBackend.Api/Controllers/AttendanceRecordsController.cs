using Kheprx.BaseBackend.Api.Security;
using Kheprx.BaseBackend.Attendance.Application.DTOs;
using Kheprx.BaseBackend.Attendance.Application.Resources;
using Kheprx.BaseBackend.Attendance.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Route("api/attendance-records")]
[Authorize]
public sealed class AttendanceRecordsController : BaseApiController
{
    private readonly IAttendanceService _service;
    private readonly IUserService _users;
    private readonly ISwimmerService _swimmers;
    private readonly IReferenceService _reference;
    private readonly ISwimmerSelfAccessGuard _access;

    public AttendanceRecordsController(IAttendanceService service, IUserService users,
        ISwimmerService swimmers, IReferenceService reference, ISwimmerSelfAccessGuard access)
    {
        _service = service;
        _users = users;
        _swimmers = swimmers;
        _reference = reference;
        _access = access;
    }

    /// <summary>Lists a swimmer's attendance records (newest first), with recorder names resolved.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AttendanceRecordDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AttendanceRecordDto>>>> List([FromQuery] Guid swimmerId, CancellationToken ct)
    {
        if (!await _access.CanReadAsync(User.IsInRole("swimmer"), CurrentUserId(), swimmerId, ct))
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<IReadOnlyList<AttendanceRecordDto>>.Failure(SwimmerMessages.Errors.Forbidden(AppLanguage.Current), "forbidden"));

        var rows = await _service.ListBySwimmerAsync(swimmerId, ct);
        var enriched = await EnrichRecorders(rows, ct);
        return Ok(ApiResponse<IReadOnlyList<AttendanceRecordDto>>.Success(
            AttendanceMessages.Success.Listed(AppLanguage.Current), enriched));
    }

    /// <summary>Loads the whole roster's attendance for a date: status + note (if any) + this month's rate.</summary>
    [HttpGet("session")]
    [ProducesResponseType(typeof(ApiResponse<AttendanceSessionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AttendanceSessionDto>>> Session([FromQuery] DateOnly date, CancellationToken ct)
    {
        var lang = AppLanguage.Current;
        var dto = await BuildSessionAsync(date, lang, ct);
        return Ok(ApiResponse<AttendanceSessionDto>.Success(AttendanceMessages.Success.SessionLoaded(lang), dto));
    }

    /// <summary>Upserts the roster's attendance for a date (create or re-mark). Head Coach or Captain only.</summary>
    [HttpPut("session")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<AttendanceSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AttendanceSessionDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AttendanceSessionDto>>> SaveSession([FromBody] SaveSessionRequest request, CancellationToken ct)
    {
        var lang = AppLanguage.Current;

        if (request.Entries.Select(e => e.SwimmerId).Distinct().Count() != request.Entries.Count)
            return BadRequest(ApiResponse<AttendanceSessionDto>.Failure(AttendanceMessages.Errors.DuplicateSwimmer(lang), "validation"));

        var statuses = await _reference.GetAttendanceStatusesAsync(ct);
        var validIds = statuses.Select(s => s.Id).ToHashSet();
        if (request.Entries.Any(e => !validIds.Contains(e.StatusId)))
            return BadRequest(ApiResponse<AttendanceSessionDto>.Failure(AttendanceMessages.Errors.InvalidStatus(lang), "validation"));

        var toAr = lang == "ar";
        var entries = request.Entries
            .Select(e => new SaveSessionEntry(e.SwimmerId, e.StatusId, toAr ? null : e.CoachNote, toAr ? e.CoachNote : null))
            .ToList();

        await _service.SaveSessionAsync(request.Date, entries, CurrentUserId(), ct);

        var dto = await BuildSessionAsync(request.Date, lang, ct);
        return Ok(ApiResponse<AttendanceSessionDto>.Success(AttendanceMessages.Success.SessionSaved(lang), dto));
    }

    // Resolves recorded_by -> display names via the Identity module (composition at the API layer).
    private async Task<IReadOnlyList<AttendanceRecordDto>> EnrichRecorders(IReadOnlyList<AttendanceRecordDto> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return rows;
        var ids = rows.Select(r => r.RecordedBy).Distinct().ToList();
        var names = await _users.GetDisplayNamesAsync(ids, ct);
        return rows.Select(r => names.TryGetValue(r.RecordedBy, out var n)
            ? r with { RecordedByNameEn = n.NameEn, RecordedByNameAr = n.NameAr }
            : r).ToList();
    }

    // Composes roster (Identity) + records-for-date + month status-counts (Attendance) into session rows.
    private async Task<AttendanceSessionDto> BuildSessionAsync(DateOnly date, string lang, CancellationToken ct)
    {
        var roster = await _swimmers.ListAsync(null, ct);
        var records = await _service.ListByDateAsync(date, ct);
        var counts = await _service.GetMonthStatusCountsAsync(date.Year, date.Month, ct);
        var statuses = await _reference.GetAttendanceStatusesAsync(ct);

        Guid IdOf(string code) => statuses.FirstOrDefault(s => s.Code == code)?.Id ?? Guid.Empty;
        var present = IdOf("present");
        var late = IdOf("late");
        var absent = IdOf("absent");

        var recBySwimmer = records
            .GroupBy(r => r.SwimmerId)
            .ToDictionary(g => g.Key, g => g.First());

        var rows = roster.Select(s =>
        {
            recBySwimmer.TryGetValue(s.Id, out var rec);

            int? rate = null;
            if (counts.TryGetValue(s.Id, out var c))
            {
                int Count(Guid id) => id != Guid.Empty && c.TryGetValue(id, out var n) ? n : 0;
                var attended = Count(present) + Count(late);
                var denom = attended + Count(absent);
                rate = denom > 0 ? (int)Math.Round(attended * 100.0 / denom) : (int?)null;
            }

            var note = rec is null ? null
                : lang == "ar" ? (rec.CoachNoteAr ?? rec.CoachNoteEn) : (rec.CoachNoteEn ?? rec.CoachNoteAr);

            return new SwimmerSessionRowDto(
                s.Id, s.Uid, s.NameEn, s.NameAr, s.ClubNameEn, s.ClubNameAr, s.GenderCode,
                rec?.StatusId, note, rate, rec is not null);
        }).ToList();

        return new AttendanceSessionDto(date, rows);
    }
}
