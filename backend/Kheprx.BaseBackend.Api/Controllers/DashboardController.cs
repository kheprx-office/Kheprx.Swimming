using Kheprx.BaseBackend.Api.Models;
using Kheprx.BaseBackend.Api.Resources;
using Kheprx.BaseBackend.Attendance.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController : BaseApiController
{
    private readonly ISwimmerService _swimmers;
    private readonly IAttendanceService _attendance;
    private readonly IReferenceService _reference;

    public DashboardController(ISwimmerService swimmers, IAttendanceService attendance, IReferenceService reference)
    {
        _swimmers = swimmers;
        _attendance = attendance;
        _reference = reference;
    }

    /// <summary>Club-wide overview: swimmer count + growth, current-month attendance rate,
    /// last-7-recorded-days present/absent, and swimmers-per-stroke split.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<DashboardSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DashboardSummaryDto>>> Summary(CancellationToken ct)
    {
        var lang = AppLanguage.Current;
        var now = DateTime.UtcNow;

        var count = (await _swimmers.GetCountAsync(ct)).Count;
        var newThisMonth = await _swimmers.GetNewThisMonthCountAsync(ct);
        var strokeSplit = (await _swimmers.GetStrokeSplitAsync(ct))
            .Select(s => new StrokeSplitItemDto(s.StrokeId, s.Code, s.NameEn, s.NameAr, s.Count))
            .ToList();

        var statuses = await _reference.GetAttendanceStatusesAsync(ct);
        Guid IdOf(string code) => statuses.FirstOrDefault(s => s.Code == code)?.Id ?? Guid.Empty;
        var present = IdOf("present");
        var late = IdOf("late");
        var absent = IdOf("absent");
        int C(IReadOnlyDictionary<Guid, int> c, Guid id) => id != Guid.Empty && c.TryGetValue(id, out var n) ? n : 0;

        // Month rate — aggregate the per-swimmer month counts into club totals.
        var monthCounts = await _attendance.GetMonthStatusCountsAsync(now.Year, now.Month, ct);
        int mp = 0, ml = 0, ma = 0;
        foreach (var c in monthCounts.Values) { mp += C(c, present); ml += C(c, late); ma += C(c, absent); }
        var attended = mp + ml;
        var denom = attended + ma;
        int? rate = denom > 0 ? (int)Math.Round(attended * 100.0 / denom) : (int?)null;

        // Last 7 recorded days — Present = present+late, Absent = absent.
        var daily = await _attendance.GetRecentDailyStatusCountsAsync(7, ct);
        var last7 = daily
            .Select(d => new DailyAttendanceDto(d.Date, C(d.Counts, present) + C(d.Counts, late), C(d.Counts, absent)))
            .ToList();

        var dto = new DashboardSummaryDto(count, newThisMonth, rate, last7, strokeSplit);
        return Ok(ApiResponse<DashboardSummaryDto>.Success(DashboardMessages.Loaded(lang), dto));
    }
}
