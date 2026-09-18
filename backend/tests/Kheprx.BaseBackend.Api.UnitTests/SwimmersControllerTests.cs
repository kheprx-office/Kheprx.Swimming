using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class SwimmersControllerTests
{
    [Fact]
    public async Task Count_returns_200_with_swimmer_count()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.GetCountAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new SwimmerCountDto(24));

        var result = await new SwimmersController(svc.Object).Count(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<SwimmerCountDto>>(ok.Value);
        Assert.True(body.SuccessStatus);
        Assert.Equal(24, body.Data!.Count);
    }

    [Fact]
    public async Task Create_returns_201_with_created_swimmer()
    {
        var svc = new Mock<ISwimmerService>();
        var created = new CreatedSwimmerDto(Guid.NewGuid(), "SW-0007", "mona.ali", "Mona Ali", "Oasis2026!");
        svc.Setup(s => s.CreateAsync(It.IsAny<CreateSwimmerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(created);

        var req = new CreateSwimmerRequest("Mona Ali", "mona.ali", Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2010, 5, 1), new[] { Guid.NewGuid() }, null, null, null, null);

        var result = await new SwimmersController(svc.Object).Create(req, CancellationToken.None);

        var ok = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, ok.StatusCode);
        var body = Assert.IsType<ApiResponse<CreatedSwimmerDto>>(ok.Value);
        Assert.Equal("SW-0007", body.Data!.Uid);
    }

    [Fact]
    public async Task Create_returns_409_when_service_returns_null()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.CreateAsync(It.IsAny<CreateSwimmerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((CreatedSwimmerDto?)null);
        var req = new CreateSwimmerRequest("Mona Ali", "mona.ali", Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2010, 5, 1), new[] { Guid.NewGuid() }, null, null, null, null);

        var result = await new SwimmersController(svc.Object).Create(req, CancellationToken.None);

        var conflict = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task List_returns_200_with_swimmers()
    {
        var svc = new Mock<ISwimmerService>();
        var rows = new List<SwimmerListItemDto>
        {
            new(Guid.NewGuid(), "SW-0001", "Alpha", null, "Oasis Main", null, "male", 15),
        };
        svc.Setup(s => s.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(rows);

        var result = await new SwimmersController(svc.Object).List("al", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<SwimmerListItemDto>>>(ok.Value);
        Assert.True(body.SuccessStatus);
        Assert.Single(body.Data!);
        Assert.Equal("SW-0001", body.Data![0].Uid);
    }
}
