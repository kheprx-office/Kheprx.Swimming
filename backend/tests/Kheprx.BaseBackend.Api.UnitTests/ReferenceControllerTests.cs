using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class ReferenceControllerTests
{
    [Fact]
    public async Task Clubs_returns_200_with_clubs()
    {
        var svc = new Mock<IReferenceService>();
        svc.Setup(s => s.GetClubsAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<ClubDto> { new(Guid.NewGuid(), "Al Ahly", "الأهلي") });

        var result = await new ReferenceController(svc.Object).Clubs(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<ClubDto>>>(ok.Value);
        Assert.Single(body.Data!);
        Assert.Equal("Al Ahly", body.Data![0].NameEn);
    }

    [Fact]
    public async Task Strokes_returns_200_with_coded_lookups()
    {
        var svc = new Mock<IReferenceService>();
        svc.Setup(s => s.GetStrokesAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<CodedLookupDto> { new(Guid.NewGuid(), "medley", "IM", "متنوع فردي") });

        var result = await new ReferenceController(svc.Object).Strokes(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<CodedLookupDto>>>(ok.Value);
        Assert.Equal("medley", body.Data![0].Code);
    }
}
