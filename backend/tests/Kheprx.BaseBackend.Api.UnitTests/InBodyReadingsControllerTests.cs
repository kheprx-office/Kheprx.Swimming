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

public class InBodyReadingsControllerTests
{
    // Permissive guard: CanReadAsync always returns true — keeps all pre-existing tests passing.
    private static ISwimmerSelfAccessGuard PermissiveGuard()
    {
        var m = new Mock<ISwimmerSelfAccessGuard>();
        m.Setup(a => a.CanReadAsync(It.IsAny<bool>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(true);
        return m.Object;
    }

    private static InBodyReadingsController CreateInBody(IInBodyReadingService svc, ISwimmerSelfAccessGuard? access = null)
        => new(svc, access ?? PermissiveGuard()) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    private static InBodyReadingDto Dto(Guid id) => new(id, new DateOnly(2024, 10, 4), 180m, 74m, 12.8m, 42.1m, 55.3m, 1.35m, 1.07m, Guid.NewGuid());
    private static CreateInBodyReadingRequest Req() => new(new DateOnly(2024, 10, 4), 180m, 74m, 12.8m, 42.1m, 55.3m, 1.35m, 1.07m);

    [Fact]
    public async Task List_returns_200_with_readings()
    {
        var svc = new Mock<IInBodyReadingService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.ListAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Dto(Guid.NewGuid()) });

        var result = await CreateInBody(svc.Object).List(id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<InBodyReadingDto>>>(ok.Value);
        Assert.True(body.SuccessStatus);
        Assert.Single(body.Data!);
    }

    [Fact]
    public async Task Create_returns_201()
    {
        var svc = new Mock<IInBodyReadingService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.CreateAsync(id, It.IsAny<CreateInBodyReadingRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Dto(Guid.NewGuid()));

        var result = await CreateInBody(svc.Object).Create(id, Req(), CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
    }

    [Fact]
    public async Task Update_returns_200_when_found_404_when_null()
    {
        var svc = new Mock<IInBodyReadingService>();
        var id = Guid.NewGuid();
        var rid = Guid.NewGuid();
        svc.Setup(s => s.UpdateAsync(id, rid, It.IsAny<CreateInBodyReadingRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Dto(rid));
        svc.Setup(s => s.UpdateAsync(id, It.Is<Guid>(g => g != rid), It.IsAny<CreateInBodyReadingRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((InBodyReadingDto?)null);

        var ok = await CreateInBody(svc.Object).Update(id, rid, Req(), CancellationToken.None);
        Assert.IsType<OkObjectResult>(ok.Result);

        var nf = await CreateInBody(svc.Object).Update(id, Guid.NewGuid(), Req(), CancellationToken.None);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(nf.Result).StatusCode);
    }

    [Fact]
    public async Task Delete_returns_200_when_true_404_when_false()
    {
        var svc = new Mock<IInBodyReadingService>();
        var id = Guid.NewGuid();
        var rid = Guid.NewGuid();
        svc.Setup(s => s.DeleteAsync(id, rid, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        svc.Setup(s => s.DeleteAsync(id, It.Is<Guid>(g => g != rid), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Assert.IsType<OkObjectResult>((await CreateInBody(svc.Object).Delete(id, rid, CancellationToken.None)).Result);
        var nf = await CreateInBody(svc.Object).Delete(id, Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(nf.Result).StatusCode);
    }

    // ── Guard tests (Task 4) ──────────────────────────────────────────────────

    [Fact]
    public async Task List_returns_403_when_swimmer_requests_foreign_id()
    {
        var svc = new Mock<IInBodyReadingService>();
        var access = new Mock<ISwimmerSelfAccessGuard>();
        access.Setup(a => a.CanReadAsync(It.IsAny<bool>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);
        var controller = CreateInBody(svc.Object, access.Object);
        var result = await controller.List(Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task List_returns_200_when_guard_allows()
    {
        var svc = new Mock<IInBodyReadingService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.ListAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<InBodyReadingDto>());
        var access = new Mock<ISwimmerSelfAccessGuard>();
        access.Setup(a => a.CanReadAsync(It.IsAny<bool>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);
        var controller = CreateInBody(svc.Object, access.Object);
        var result = await controller.List(id, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result.Result);
    }
}
