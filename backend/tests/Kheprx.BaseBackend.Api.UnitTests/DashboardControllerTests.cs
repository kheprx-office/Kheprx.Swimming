using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Api.Models;
using Kheprx.BaseBackend.Attendance.Application.DTOs;
using Kheprx.BaseBackend.Attendance.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class DashboardControllerTests
{
    private static readonly Guid Present = Guid.NewGuid();
    private static readonly Guid Late = Guid.NewGuid();
    private static readonly Guid Absent = Guid.NewGuid();
    private static readonly Guid Excused = Guid.NewGuid();

    private static IReadOnlyList<CodedLookupDto> Statuses() => new[]
    {
        new CodedLookupDto(Present, "present", "Present", "حاضر"),
        new CodedLookupDto(Late, "late", "Late", "متأخر"),
        new CodedLookupDto(Absent, "absent", "Absent", "غائب"),
        new CodedLookupDto(Excused, "excused", "Excused", "معذور"),
    };

    private static DashboardController Build(
        Mock<ISwimmerService> swimmers, Mock<IAttendanceService> attendance, Mock<IReferenceService> reference)
        => new(swimmers.Object, attendance.Object, reference.Object);

    private static (Mock<ISwimmerService>, Mock<IAttendanceService>, Mock<IReferenceService>) Mocks()
    {
        var swimmers = new Mock<ISwimmerService>();
        swimmers.Setup(s => s.GetCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new SwimmerCountDto(452));
        swimmers.Setup(s => s.GetNewThisMonthCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(12);
        swimmers.Setup(s => s.GetStrokeSplitAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new StrokeSplitDto(Guid.NewGuid(), "free", "Freestyle", "حرة", 9) });
        var attendance = new Mock<IAttendanceService>();
        attendance.Setup(a => a.GetMonthStatusCountsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new Dictionary<Guid, IReadOnlyDictionary<Guid, int>>());
        attendance.Setup(a => a.GetRecentDailyStatusCountsAsync(7, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Array.Empty<DailyStatusCountsDto>());
        var reference = new Mock<IReferenceService>();
        reference.Setup(r => r.GetAttendanceStatusesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Statuses());
        return (swimmers, attendance, reference);
    }

    private static DashboardSummaryDto Data(ActionResult<ApiResponse<DashboardSummaryDto>> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<ApiResponse<DashboardSummaryDto>>(ok.Value).Data!;
    }

    [Fact]
    public async Task Summary_composes_counts_and_stroke_split()
    {
        var (sw, at, rf) = Mocks();
        var data = Data(await Build(sw, at, rf).Summary(CancellationToken.None));
        Assert.Equal(452, data.SwimmerCount);
        Assert.Equal(12, data.NewThisMonth);
        Assert.Single(data.StrokeSplit);
        Assert.Equal("free", data.StrokeSplit[0].Code);
    }

    [Fact]
    public async Task Summary_month_rate_uses_present_plus_late_over_present_late_absent_excused_excluded()
    {
        var (sw, at, rf) = Mocks();
        // Club month totals: present 6, late 2, absent 2, excused 5 -> attended 8 / denom 10 = 80.
        at.Setup(a => a.GetMonthStatusCountsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new Dictionary<Guid, IReadOnlyDictionary<Guid, int>>
          {
              [Guid.NewGuid()] = new Dictionary<Guid, int> { [Present] = 6, [Late] = 2, [Absent] = 2, [Excused] = 5 },
          });
        var data = Data(await Build(sw, at, rf).Summary(CancellationToken.None));
        Assert.Equal(80, data.MonthAttendanceRatePct);
    }

    [Fact]
    public async Task Summary_month_rate_is_null_when_no_records()
    {
        var (sw, at, rf) = Mocks(); // GetMonthStatusCountsAsync returns empty
        var data = Data(await Build(sw, at, rf).Summary(CancellationToken.None));
        Assert.Null(data.MonthAttendanceRatePct);
    }

    [Fact]
    public async Task Summary_maps_recent_days_present_is_present_plus_late_and_keeps_fewer_than_seven()
    {
        var (sw, at, rf) = Mocks();
        at.Setup(a => a.GetRecentDailyStatusCountsAsync(7, It.IsAny<CancellationToken>()))
          .ReturnsAsync(new[]
          {
              new DailyStatusCountsDto(new DateOnly(2026, 9, 27),
                  new Dictionary<Guid, int> { [Present] = 4, [Late] = 1, [Absent] = 2 }),
              new DailyStatusCountsDto(new DateOnly(2026, 9, 28),
                  new Dictionary<Guid, int> { [Present] = 5 }),
          });
        var data = Data(await Build(sw, at, rf).Summary(CancellationToken.None));
        Assert.Equal(2, data.Last7Days.Count); // fewer than 7 kept as-is
        Assert.Equal(5, data.Last7Days[0].Present); // 4 present + 1 late
        Assert.Equal(2, data.Last7Days[0].Absent);
        Assert.Equal(5, data.Last7Days[1].Present);
        Assert.Equal(0, data.Last7Days[1].Absent);
    }

    [Fact]
    public async Task Summary_handles_zero_swimmers()
    {
        var (sw, at, rf) = Mocks();
        sw.Setup(s => s.GetCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new SwimmerCountDto(0));
        sw.Setup(s => s.GetStrokeSplitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<StrokeSplitDto>());
        var data = Data(await Build(sw, at, rf).Summary(CancellationToken.None));
        Assert.Equal(0, data.SwimmerCount);
        Assert.Empty(data.StrokeSplit);
        Assert.Null(data.MonthAttendanceRatePct);
    }

    [Fact]
    public void Summary_requires_authorization()
    {
        var attr = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute?)Attribute
            .GetCustomAttribute(typeof(DashboardController), typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute));
        Assert.NotNull(attr);
    }
}
