using Kheprx.BaseBackend.Api.Controllers;
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
    private static ObservationsController Controller(IObservationService svc)
        => new(svc) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    private static ObservationDto Dto(Guid id) => new(id, Guid.NewGuid(), Guid.NewGuid(), "Penicillin", "Severe", DateTime.UtcNow, Guid.NewGuid());
    private static UpdateObservationRequest Req() => new(Guid.NewGuid(), "Penicillin", "Moderate");

    [Fact]
    public async Task List_returns_200_with_records()
    {
        var svc = new Mock<IObservationService>();
        var sw = Guid.NewGuid();
        svc.Setup(s => s.ListBySwimmerAsync(sw, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Dto(Guid.NewGuid()) });

        var result = await Controller(svc.Object).List(sw, CancellationToken.None);

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

        Assert.IsType<OkObjectResult>((await Controller(svc.Object).Update(id, Req(), CancellationToken.None)).Result);
        var nf = await Controller(svc.Object).Update(Guid.NewGuid(), Req(), CancellationToken.None);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(nf.Result).StatusCode);
    }

    [Fact]
    public async Task Delete_returns_200_when_true_404_when_false()
    {
        var svc = new Mock<IObservationService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.DeleteAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        svc.Setup(s => s.DeleteAsync(It.Is<Guid>(g => g != id), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Assert.IsType<OkObjectResult>((await Controller(svc.Object).Delete(id, CancellationToken.None)).Result);
        var nf = await Controller(svc.Object).Delete(Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(nf.Result).StatusCode);
    }
}
