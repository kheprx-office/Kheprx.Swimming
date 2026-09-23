using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class ReferenceControllerCompetitionStatusesTests
{
    [Fact]
    public async Task CompetitionStatuses_returns_200_with_data()
    {
        var svc = new Mock<IReferenceService>();
        svc.Setup(s => s.GetCompetitionStatusesAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new[] { new CodedLookupDto(Guid.NewGuid(), "upcoming", "Upcoming", "قادمة") });
        var controller = new ReferenceController(svc.Object);

        var result = await controller.CompetitionStatuses(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<CodedLookupDto>>>(ok.Value);
        Assert.Single(body.Data!);
        Assert.Equal("upcoming", body.Data![0].Code);
    }
}
