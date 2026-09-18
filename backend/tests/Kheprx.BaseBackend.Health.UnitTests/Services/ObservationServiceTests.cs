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
}
