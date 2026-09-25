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

    [Fact]
    public async Task ReplaceForSwimmer_removes_existing_then_inserts_new_set()
    {
        var repo = new Mock<IObservationRepository>();
        var sw = Guid.NewGuid();
        var recordedBy = Guid.NewGuid();
        var stale = new List<Observation> { new(sw, Guid.NewGuid(), "Old", "Value", Guid.NewGuid()) };
        repo.Setup(r => r.ListBySwimmerTrackedAsync(sw, It.IsAny<CancellationToken>())).ReturnsAsync(stale);
        var added = new List<Observation>();
        repo.Setup(r => r.AddAsync(It.IsAny<Observation>(), It.IsAny<CancellationToken>()))
            .Callback<Observation, CancellationToken>((o, _) => added.Add(o)).Returns(Task.CompletedTask);
        var svc = new ObservationService(repo.Object);

        var cat = Guid.NewGuid();
        var items = new List<CreateObservationRequest> { new(sw, cat, "Allergies", "Peanuts") };
        await svc.ReplaceForSwimmerAsync(sw, items, recordedBy);

        repo.Verify(r => r.RemoveRange(stale), Times.Once);
        Assert.Single(added);
        Assert.Equal("Peanuts", added[0].Value);
        Assert.Equal(recordedBy, added[0].RecordedBy);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReplaceForSwimmer_with_empty_set_deletes_all_and_inserts_none()
    {
        var repo = new Mock<IObservationRepository>();
        var sw = Guid.NewGuid();
        var stale = new List<Observation> { new(sw, Guid.NewGuid(), "Old", "Value", Guid.NewGuid()) };
        repo.Setup(r => r.ListBySwimmerTrackedAsync(sw, It.IsAny<CancellationToken>())).ReturnsAsync(stale);
        var svc = new ObservationService(repo.Object);

        await svc.ReplaceForSwimmerAsync(sw, System.Array.Empty<CreateObservationRequest>(), Guid.NewGuid());

        repo.Verify(r => r.RemoveRange(stale), Times.Once);
        repo.Verify(r => r.AddAsync(It.IsAny<Observation>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
