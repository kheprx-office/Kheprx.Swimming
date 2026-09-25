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

public class ObservationsControllerTests
{
    // Permissive guard: CanReadAsync always returns true — keeps all pre-existing tests passing.
    private static ISwimmerSelfAccessGuard PermissiveGuard()
    {
        var m = new Mock<ISwimmerSelfAccessGuard>();
        m.Setup(a => a.CanReadAsync(It.IsAny<bool>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(true);
        return m.Object;
    }

    private static ObservationsController CreateObs(IObservationService svc, ISwimmerSelfAccessGuard? access = null)
        => new(svc, access ?? PermissiveGuard()) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    private static ObservationDto Dto(Guid id) => new(id, Guid.NewGuid(), Guid.NewGuid(), "Penicillin", "Severe", DateTime.UtcNow, Guid.NewGuid());
    private static UpdateObservationRequest Req() => new(Guid.NewGuid(), "Penicillin", "Moderate");

    [Fact]
    public async Task List_returns_200_with_records()
    {
        var svc = new Mock<IObservationService>();
        var sw = Guid.NewGuid();
        svc.Setup(s => s.ListBySwimmerAsync(sw, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Dto(Guid.NewGuid()) });

        var result = await CreateObs(svc.Object).List(sw, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<ObservationDto>>>(ok.Value);
        Assert.True(body.SuccessStatus);
        Assert.Single(body.Data!);
    }

    [Fact]
    public async Task Update_returns_200_when_found_404_when_null()
    {
        var svc = new Mock<IObservationService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.UpdateAsync(id, It.IsAny<UpdateObservationRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Dto(id));
        svc.Setup(s => s.UpdateAsync(It.Is<Guid>(g => g != id), It.IsAny<UpdateObservationRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((ObservationDto?)null);

        Assert.IsType<OkObjectResult>((await CreateObs(svc.Object).Update(id, Req(), CancellationToken.None)).Result);
        var nf = await CreateObs(svc.Object).Update(Guid.NewGuid(), Req(), CancellationToken.None);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(nf.Result).StatusCode);
    }

    [Fact]
    public async Task Delete_returns_200_when_true_404_when_false()
    {
        var svc = new Mock<IObservationService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.DeleteAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        svc.Setup(s => s.DeleteAsync(It.Is<Guid>(g => g != id), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Assert.IsType<OkObjectResult>((await CreateObs(svc.Object).Delete(id, CancellationToken.None)).Result);
        var nf = await CreateObs(svc.Object).Delete(Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(nf.Result).StatusCode);
    }

    // ── Guard tests (Task 4) ──────────────────────────────────────────────────

    [Fact]
    public async Task List_returns_403_when_swimmer_requests_foreign_id()
    {
        var svc = new Mock<IObservationService>();
        var access = new Mock<ISwimmerSelfAccessGuard>();
        access.Setup(a => a.CanReadAsync(It.IsAny<bool>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);
        var controller = CreateObs(svc.Object, access.Object);
        var result = await controller.List(Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task List_returns_200_when_guard_allows()
    {
        var svc = new Mock<IObservationService>();
        var sw = Guid.NewGuid();
        svc.Setup(s => s.ListBySwimmerAsync(sw, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ObservationDto>());
        var access = new Mock<ISwimmerSelfAccessGuard>();
        access.Setup(a => a.CanReadAsync(It.IsAny<bool>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);
        var controller = CreateObs(svc.Object, access.Object);
        var result = await controller.List(sw, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result.Result);
    }
}
