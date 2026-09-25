using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Api.Security;
using Kheprx.BaseBackend.Attendance.Application.DTOs;
using Kheprx.BaseBackend.Attendance.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class AttendanceRecordsControllerSessionTests
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

    // Permissive guard: CanReadAsync always returns true — keeps all pre-existing tests passing.
    private static ISwimmerSelfAccessGuard PermissiveGuard()
    {
        var m = new Mock<ISwimmerSelfAccessGuard>();
        m.Setup(a => a.CanReadAsync(It.IsAny<bool>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(true);
        return m.Object;
    }

    private static AttendanceRecordsController Build(
        Mock<IAttendanceService> svc, Mock<ISwimmerService> swimmers, Mock<IReferenceService> reference,
        Guid? userId = null)
    {
        var users = new Mock<IUserService>();
        var controller = new AttendanceRecordsController(svc.Object, users.Object, swimmers.Object, reference.Object, PermissiveGuard());
        var claims = userId is { } id
            ? new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(JwtRegisteredClaimNames.Sub, id.ToString()) }))
            : new ClaimsPrincipal(new ClaimsIdentity());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = claims } };
        return controller;
    }

    [Fact]
    public async Task Session_composes_roster_with_records_and_rates()
    {
        var date = new DateOnly(2026, 9, 23);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var swimmers = new Mock<ISwimmerService>();
        swimmers.Setup(s => s.ListAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new SwimmerListItemDto(a, "SW-A", "Alice", null, "Oasis", null, "female", 15),
            new SwimmerListItemDto(b, "SW-B", "Bob", null, "Oasis", null, "male", 16),
        });
        var svc = new Mock<IAttendanceService>();
        // A has a record for the date; B has none.
        svc.Setup(s => s.ListByDateAsync(date, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new AttendanceRecordDto(Guid.NewGuid(), a, date, Present, "great", null, Guid.NewGuid(), "", null),
        });
        // A month: present+present+absent → rate = round(2/3*100)=67 (excused excluded, none here).
        svc.Setup(s => s.GetMonthStatusCountsAsync(2026, 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyDictionary<Guid, int>>
            {
                [a] = new Dictionary<Guid, int> { [Present] = 2, [Absent] = 1 },
            });
        var reference = new Mock<IReferenceService>();
        reference.Setup(r => r.GetAttendanceStatusesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Statuses());

        var controller = Build(svc, swimmers, reference);
        var result = await controller.Session(date, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<AttendanceSessionDto>>(ok.Value);
        var rows = body.Data!.Rows;
        Assert.Equal(2, rows.Count);
        var rowA = rows.Single(r => r.SwimmerId == a);
        Assert.True(rowA.HasRecord);
        Assert.Equal(Present, rowA.StatusId);
        Assert.Equal("great", rowA.CoachNote);
        Assert.Equal(67, rowA.MonthRatePct);
        var rowB = rows.Single(r => r.SwimmerId == b);
        Assert.False(rowB.HasRecord);
        Assert.Null(rowB.StatusId);
        Assert.Null(rowB.MonthRatePct);
    }

    [Fact]
    public async Task SaveSession_rejects_duplicate_swimmer()
    {
        var dup = Guid.NewGuid();
        var reference = new Mock<IReferenceService>();
        reference.Setup(r => r.GetAttendanceStatusesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Statuses());
        var controller = Build(new Mock<IAttendanceService>(), new Mock<ISwimmerService>(), reference, Guid.NewGuid());

        var req = new SaveSessionRequest(new DateOnly(2026, 9, 23), new[]
        {
            new SaveSessionEntryRequest(dup, Present, null),
            new SaveSessionEntryRequest(dup, Absent, null),
        });
        var result = await controller.SaveSession(req, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task SaveSession_rejects_unknown_status()
    {
        var reference = new Mock<IReferenceService>();
        reference.Setup(r => r.GetAttendanceStatusesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Statuses());
        var controller = Build(new Mock<IAttendanceService>(), new Mock<ISwimmerService>(), reference, Guid.NewGuid());

        var req = new SaveSessionRequest(new DateOnly(2026, 9, 23), new[]
        {
            new SaveSessionEntryRequest(Guid.NewGuid(), Guid.NewGuid() /* not a real status */, null),
        });
        var result = await controller.SaveSession(req, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task SaveSession_stamps_current_user_and_reloads_session()
    {
        var date = new DateOnly(2026, 9, 23);
        var me = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        var svc = new Mock<IAttendanceService>();
        svc.Setup(s => s.SaveSessionAsync(date, It.IsAny<IReadOnlyList<SaveSessionEntry>>(), me, It.IsAny<CancellationToken>()))
           .Returns(Task.CompletedTask);
        svc.Setup(s => s.ListByDateAsync(date, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<AttendanceRecordDto>());
        svc.Setup(s => s.GetMonthStatusCountsAsync(2026, 9, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new Dictionary<Guid, IReadOnlyDictionary<Guid, int>>());
        var swimmers = new Mock<ISwimmerService>();
        swimmers.Setup(s => s.ListAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<SwimmerListItemDto>());
        var reference = new Mock<IReferenceService>();
        reference.Setup(r => r.GetAttendanceStatusesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Statuses());

        var controller = Build(svc, swimmers, reference, me);
        var req = new SaveSessionRequest(date, new[] { new SaveSessionEntryRequest(swimmer, Present, "hi") });
        var result = await controller.SaveSession(req, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        svc.Verify(s => s.SaveSessionAsync(date, It.IsAny<IReadOnlyList<SaveSessionEntry>>(), me, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void SaveSession_requires_coach_roles()
    {
        var method = typeof(AttendanceRecordsController).GetMethod(nameof(AttendanceRecordsController.SaveSession))!;
        var attr = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute?)Attribute
            .GetCustomAttribute(method, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute));
        Assert.NotNull(attr);
        Assert.Equal("head_coach,captain", attr!.Roles);
    }
}
