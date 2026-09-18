using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class CoachesControllerTests
{
    private static CreateCoachRequest Req() => new(
        "captain", "Dave", "dave.coach", "dave@oasis.com", "29001011234567",
        Guid.NewGuid(), new DateOnly(1990, 1, 1), "01000000000", null);

    [Fact]
    public async Task Create_returns_201_with_created_coach()
    {
        var svc = new Mock<ICoachService>();
        var created = new CreatedCoachDto(Guid.NewGuid(), "dave.coach", "Dave", "captain", "Oasis2026!");
        svc.Setup(s => s.CreateAsync(It.IsAny<CreateCoachRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(created);

        var result = await new CoachesController(svc.Object).Create(Req(), CancellationToken.None);

        var ok = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, ok.StatusCode);
        var body = Assert.IsType<ApiResponse<CreatedCoachDto>>(ok.Value);
        Assert.Equal("captain", body.Data!.Role);
    }

    [Fact]
    public async Task Create_returns_409_when_service_returns_null()
    {
        var svc = new Mock<ICoachService>();
        svc.Setup(s => s.CreateAsync(It.IsAny<CreateCoachRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((CreatedCoachDto?)null);

        var result = await new CoachesController(svc.Object).Create(Req(), CancellationToken.None);

        var conflict = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }
}
