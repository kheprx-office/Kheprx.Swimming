using Kheprx.BaseBackend.Identity.Application.Services;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Services;

public class ReferenceServiceTests
{
    private static ReferenceService NewService(
        IClubRepository? clubs = null, IBloodTypeRepository? blood = null,
        IStrokeRepository? strokes = null, IGenderRepository? genders = null)
        => new(clubs ?? Mock.Of<IClubRepository>(), blood ?? Mock.Of<IBloodTypeRepository>(),
               strokes ?? Mock.Of<IStrokeRepository>(), genders ?? Mock.Of<IGenderRepository>());

    [Fact]
    public async Task GetStrokes_maps_entities_to_coded_dtos()
    {
        var strokes = new Mock<IStrokeRepository>();
        strokes.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new[] { new Stroke("medley", "IM", "متنوع فردي") });

        var result = await NewService(strokes: strokes.Object).GetStrokesAsync();

        Assert.Single(result);
        Assert.Equal("medley", result[0].Code);
        Assert.Equal("IM", result[0].NameEn);
        Assert.Equal("متنوع فردي", result[0].NameAr);
    }

    [Fact]
    public async Task GetClubs_maps_entities_to_club_dtos()
    {
        var clubs = new Mock<IClubRepository>();
        clubs.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new[] { new Club("Al Ahly", "الأهلي") });

        var result = await NewService(clubs: clubs.Object).GetClubsAsync();

        Assert.Single(result);
        Assert.Equal("Al Ahly", result[0].NameEn);
        Assert.Equal("الأهلي", result[0].NameAr);
    }
}
