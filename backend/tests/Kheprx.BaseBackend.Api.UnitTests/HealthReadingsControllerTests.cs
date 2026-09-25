using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Api.Security;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class HealthReadingsControllerTests
{
    // Permissive guard: CanReadAsync always returns true — keeps all pre-existing tests passing.
    private static ISwimmerSelfAccessGuard PermissiveGuard()
    {
        var m = new Mock<ISwimmerSelfAccessGuard>();
        m.Setup(a => a.CanReadAsync(It.IsAny<bool>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(true);
        return m.Object;
    }

    private static HealthReadingsController Controller(IHealthReadingService svc, ISwimmerSelfAccessGuard? access = null)
        => new(svc, access ?? PermissiveGuard()) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    private static HealthReadingListItemDto Row(Guid id) =>
        new(id, Guid.NewGuid(), "Glucose", "الجلوكوز", "mg/dL", 90m, 70m, 110m, DateTime.UtcNow, "normal");

    [Fact]
    public async Task List_returns_200_with_rows()
    {
        var svc = new Mock<IHealthReadingService>();
        var swimmerId = Guid.NewGuid();
        svc.Setup(s => s.ListBySwimmerAsync(swimmerId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Row(Guid.NewGuid()) });

        var result = await Controller(svc.Object).List(swimmerId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<HealthReadingListItemDto>>>(ok.Value);
        Assert.True(body.SuccessStatus);
        Assert.Single(body.Data!);
    }

    [Fact]
    public async Task Update_returns_200_when_found_404_when_null()
    {
        var svc = new Mock<IHealthReadingService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.UpdateAsync(id, It.IsAny<UpdateHealthReadingRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Row(id));
        svc.Setup(s => s.UpdateAsync(It.Is<Guid>(g => g != id), It.IsAny<UpdateHealthReadingRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((HealthReadingListItemDto?)null);

        var ok = await Controller(svc.Object).Update(id, new UpdateHealthReadingRequest(90m), CancellationToken.None);
        Assert.IsType<OkObjectResult>(ok.Result);

        var nf = await Controller(svc.Object).Update(Guid.NewGuid(), new UpdateHealthReadingRequest(90m), CancellationToken.None);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(nf.Result).StatusCode);
    }

    [Fact]
    public async Task Delete_returns_200_when_true_404_when_false()
    {
        var svc = new Mock<IHealthReadingService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.DeleteAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        svc.Setup(s => s.DeleteAsync(It.Is<Guid>(g => g != id), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Assert.IsType<OkObjectResult>((await Controller(svc.Object).Delete(id, CancellationToken.None)).Result);
        var nf = await Controller(svc.Object).Delete(Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(nf.Result).StatusCode);
    }
}
