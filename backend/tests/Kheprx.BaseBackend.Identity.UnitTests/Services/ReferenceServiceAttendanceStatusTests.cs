using Kheprx.BaseBackend.Identity.Application.Services;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Services;

public class ReferenceServiceAttendanceStatusTests
{
    [Fact]
    public async Task GetAttendanceStatusesAsync_maps_rows_to_coded_lookups()
    {
        var attendance = new Mock<IAttendanceStatusRepository>();
        attendance.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new AttendanceStatus("present", "Present", "حاضر") });

        var svc = new ReferenceService(
            Mock.Of<IClubRepository>(), Mock.Of<IBloodTypeRepository>(), Mock.Of<IStrokeRepository>(),
            Mock.Of<IGenderRepository>(), Mock.Of<IObservationCategoryRepository>(),
            Mock.Of<IFitnessAssessmentRepository>(), Mock.Of<IFeedbackCategoryRepository>(),
            attendance.Object, Mock.Of<ICompetitionStatusRepository>());

        var result = await svc.GetAttendanceStatusesAsync();

        Assert.Single(result);
        Assert.Equal("present", result[0].Code);
        Assert.Equal("Present", result[0].NameEn);
    }
}
