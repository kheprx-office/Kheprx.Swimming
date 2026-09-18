using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Services;

public class MedicalTestServiceTests
{
    private static CreateMedicalTestRequest Req() =>
        new("Hemoglobin", "الهيموغلوبين", "g/dL", 11m, 17.5m);

    [Fact]
    public async Task Create_persists_entity_with_creator_and_returns_dto()
    {
        var repo = new Mock<IMedicalTestRepository>();
        MedicalTest? added = null;
        repo.Setup(r => r.AddAsync(It.IsAny<MedicalTest>(), It.IsAny<CancellationToken>()))
            .Callback<MedicalTest, CancellationToken>((t, _) => added = t)
            .Returns(Task.CompletedTask);
        var svc = new MedicalTestService(repo.Object);
        var createdBy = Guid.NewGuid();

        var dto = await svc.CreateAsync(Req(), createdBy);

        Assert.NotNull(added);
        Assert.Equal(createdBy, added!.CreatedBy);
        Assert.Equal("Hemoglobin", dto.NameEn);
        Assert.Equal("الهيموغلوبين", dto.NameAr);
        Assert.Equal(11m, dto.LowerBound);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task List_maps_rows_to_dtos()
    {
        var repo = new Mock<IMedicalTestRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new MedicalTest("Glucose", "الجلوكوز", "mg/dL", 70m, 110m, Guid.NewGuid()) });
        var svc = new MedicalTestService(repo.Object);

        var list = await svc.ListAsync();

        Assert.Single(list);
        Assert.Equal("Glucose", list[0].NameEn);
        Assert.Equal(110m, list[0].UpperBound);
    }

    [Fact]
    public async Task Delete_returns_false_when_missing_and_true_when_present()
    {
        var repo = new Mock<IMedicalTestRepository>();
        var svc = new MedicalTestService(repo.Object);
        var id = Guid.NewGuid();

        repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((MedicalTest?)null);
        Assert.False(await svc.DeleteAsync(id));

        var existing = new MedicalTest("Uric Acid", "حمض اليوريك", "mg/dL", 1m, 7m, Guid.NewGuid());
        repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        Assert.True(await svc.DeleteAsync(existing.Id));
        repo.Verify(r => r.RemoveAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
