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

public class AttendanceRecordsControllerTests
{
    // Permissive guard: CanReadAsync always returns true — keeps all pre-existing tests passing.
    private static ISwimmerSelfAccessGuard PermissiveGuard()
    {
        var m = new Mock<ISwimmerSelfAccessGuard>();
        m.Setup(a => a.CanReadAsync(It.IsAny<bool>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(true);
        return m.Object;
    }

    private static AttendanceRecordsController CreateAttendance(
        IAttendanceService svc, IUserService users, ISwimmerSelfAccessGuard? access = null)
        => new(svc, users, new Mock<ISwimmerService>().Object, new Mock<IReferenceService>().Object, access ?? PermissiveGuard())
           { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    private static AttendanceRecordDto Dto(Guid recorder) =>
        new(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 9), Guid.NewGuid(), "note", null, recorder, string.Empty, null);

    [Fact]
    public async Task List_returns_200_and_resolves_recorder_names()
    {
        var swimmerId = Guid.NewGuid();
        var recorder = Guid.NewGuid();
        var svc = new Mock<IAttendanceService>();
        var users = new Mock<IUserService>();
        svc.Setup(s => s.ListBySwimmerAsync(swimmerId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Dto(recorder) });
        users.Setup(u => u.GetDisplayNamesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Dictionary<Guid, UserNameDto> { [recorder] = new(recorder, "Coach Layla", "الكابتن ليلى") });

        var controller = CreateAttendance(svc.Object, users.Object);
        var result = await controller.List(swimmerId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<AttendanceRecordDto>>>(ok.Value);
        Assert.Equal("Coach Layla", body.Data![0].RecordedByNameEn);
        Assert.Equal("الكابتن ليلى", body.Data![0].RecordedByNameAr);
    }
}
