namespace Kheprx.BaseBackend.Api.Models;

/// <summary>Everything the dashboard renders, in one payload (GET /api/dashboard/summary).</summary>
public sealed record DashboardSummaryDto(
    int SwimmerCount,
    int NewThisMonth,
    int? MonthAttendanceRatePct,
    IReadOnlyList<DailyAttendanceDto> Last7Days,
    IReadOnlyList<StrokeSplitItemDto> StrokeSplit);

public sealed record DailyAttendanceDto(DateOnly Date, int Present, int Absent);

public sealed record StrokeSplitItemDto(Guid StrokeId, string Code, string NameEn, string? NameAr, int Count);
