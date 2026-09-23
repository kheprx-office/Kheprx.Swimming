using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class ReferenceControllerAttendanceStatusesTests
{
    [Fact]
    public async Task AttendanceStatuses_returns_200_with_data()
    {
        var svc = new Mock<IReferenceService>();
        svc.Setup(s => s.GetAttendanceStatusesAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new[] { new CodedLookupDto(Guid.NewGuid(), "present", "Present", "حاضر") });
        var controller = new ReferenceController(svc.Object);

        var result = await controller.AttendanceStatuses(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<CodedLookupDto>>>(ok.Value);
        Assert.Single(body.Data!);
        Assert.Equal("present", body.Data![0].Code);
    }
}
