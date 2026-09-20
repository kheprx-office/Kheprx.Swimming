using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Services;

public class ObservationServiceTests
{
    [Fact]
    public async Task Create_persists_observation_stamps_recorder_and_returns_dto()
    {
        var repo = new Mock<IObservationRepository>();
        Observation? added = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Observation>(), It.IsAny<CancellationToken>()))
            .Callback<Observation, CancellationToken>((o, _) => added = o)
            .Returns(Task.CompletedTask);
        var svc = new ObservationService(repo.Object);
        var recordedBy = Guid.NewGuid();
        var req = new CreateObservationRequest(Guid.NewGuid(), Guid.NewGuid(), "Penicillin", "Severe");

        var dto = await svc.CreateAsync(req, recordedBy);

        Assert.NotNull(added);
        Assert.Equal(recordedBy, added!.RecordedBy);
        Assert.Equal("Penicillin", dto.FieldLabel);
        Assert.Equal("Severe", dto.Value);
        Assert.NotEqual(default, dto.ObservedDate);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListBySwimmer_maps_rows()
    {
        var repo = new Mock<IObservationRepository>();
        var sw = Guid.NewGuid();
        repo.Setup(r => r.ListBySwimmerAsync(sw, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Observation> { new(sw, Guid.NewGuid(), "Penicillin", "Severe", Guid.NewGuid()) });
        var svc = new ObservationService(repo.Object);

        var list = await svc.ListBySwimmerAsync(sw);

        Assert.Single(list);
        Assert.Equal("Penicillin", list[0].FieldLabel);
    }

    [Fact]
    public async Task Update_applies_when_found_and_returns_null_when_missing()
    {
        var repo = new Mock<IObservationRepository>();
        var existing = new Observation(Guid.NewGuid(), Guid.NewGuid(), "Penicillin", "Severe", Guid.NewGuid());
        repo.Setup(r => r.GetTrackedAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var svc = new ObservationService(repo.Object);
        var newCat = Guid.NewGuid();

        var ok = await svc.UpdateAsync(existing.Id, new UpdateObservationRequest(newCat, "Pollen", "Mild"));
        var missing = await svc.UpdateAsync(Guid.NewGuid(), new UpdateObservationRequest(newCat, "X", "Y"));

        Assert.NotNull(ok);
        Assert.Equal("Pollen", existing.FieldLabel);
        Assert.Equal("Mild", existing.Value);
        Assert.Equal(newCat, existing.CategoryId);
        Assert.Null(missing);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_removes_when_found_false_when_missing()
    {
        var repo = new Mock<IObservationRepository>();
        var existing = new Observation(Guid.NewGuid(), Guid.NewGuid(), "Penicillin", "Severe", Guid.NewGuid());
        repo.Setup(r => r.GetTrackedAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var svc = new ObservationService(repo.Object);

        Assert.True(await svc.DeleteAsync(existing.Id));
        repo.Verify(r => r.Remove(existing), Times.Once);
        Assert.False(await svc.DeleteAsync(Guid.NewGuid()));
    }
}
