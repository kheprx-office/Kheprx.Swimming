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
        IStrokeRepository? strokes = null, IGenderRepository? genders = null,
        IObservationCategoryRepository? categories = null,
        IFitnessAssessmentRepository? fitness = null,
        IFeedbackCategoryRepository? feedbackCategories = null,
        IAttendanceStatusRepository? attendanceStatuses = null)
        => new(clubs ?? Mock.Of<IClubRepository>(), blood ?? Mock.Of<IBloodTypeRepository>(),
               strokes ?? Mock.Of<IStrokeRepository>(), genders ?? Mock.Of<IGenderRepository>(),
               categories ?? Mock.Of<IObservationCategoryRepository>(),
               fitness ?? Mock.Of<IFitnessAssessmentRepository>(),
               feedbackCategories ?? Mock.Of<IFeedbackCategoryRepository>(),
               attendanceStatuses ?? Mock.Of<IAttendanceStatusRepository>());

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

    [Fact]
    public async Task GetObservationCategories_maps_entities_to_coded_dtos()
    {
        var categories = new Mock<IObservationCategoryRepository>();
        categories.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new[] { new ObservationCategory("allergy", "Allergy", "حساسية") });

        var result = await NewService(categories: categories.Object).GetObservationCategoriesAsync();

        Assert.Single(result);
        Assert.Equal("allergy", result[0].Code);
        Assert.Equal("Allergy", result[0].NameEn);
    }

    [Fact]
    public async Task GetFitnessAssessments_maps_entities_to_coded_dtos()
    {
        var fitness = new Mock<IFitnessAssessmentRepository>();
        fitness.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new[] { new Kheprx.BaseBackend.Identity.Domain.Entities.FitnessAssessment("fit", "Fit", "لائق") });

        var result = await NewService(fitness: fitness.Object).GetFitnessAssessmentsAsync();

        Assert.Single(result);
        Assert.Equal("fit", result[0].Code);
        Assert.Equal("Fit", result[0].NameEn);
    }
}
