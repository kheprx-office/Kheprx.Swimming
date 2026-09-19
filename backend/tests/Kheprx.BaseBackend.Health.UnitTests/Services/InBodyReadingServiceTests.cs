using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Services;

public class InBodyReadingServiceTests
{
    private static (InBodyReadingService svc, Mock<IInBodyReadingRepository> repo) Build()
    {
        var repo = new Mock<IInBodyReadingRepository>();
        return (new InBodyReadingService(repo.Object), repo);
    }

    private static CreateInBodyReadingRequest Req() => new(new DateOnly(2024, 10, 4), 180m, 74m, 12.8m, 42.1m, 1.35m, 1.07m);

    [Fact]
    public async Task Create_stamps_recorder_saves_and_returns_dto()
    {
        var (svc, repo) = Build();
        InBodyReading? added = null;
        repo.Setup(r => r.AddAsync(It.IsAny<InBodyReading>(), It.IsAny<CancellationToken>()))
            .Callback<InBodyReading, CancellationToken>((r, _) => added = r).Returns(Task.CompletedTask);
        var swimmerId = Guid.NewGuid();
        var recordedBy = Guid.NewGuid();

        var dto = await svc.CreateAsync(swimmerId, Req(), recordedBy);

        Assert.NotNull(added);
        Assert.Equal(swimmerId, added!.SwimmerId);
        Assert.Equal(recordedBy, added.RecordedBy);
        Assert.Equal(180m, dto.HeightCm);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task List_maps_rows()
    {
        var (svc, repo) = Build();
        var sw = Guid.NewGuid();
        repo.Setup(r => r.ListBySwimmerAsync(sw, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InBodyReading> { new(sw, new DateOnly(2024, 10, 4), 180m, 74m, 12.8m, 42.1m, 1.35m, 1.07m, Guid.NewGuid()) });

        var list = await svc.ListAsync(sw);

        Assert.Single(list);
        Assert.Equal(74m, list[0].WeightKg);
    }

    [Fact]
    public async Task Update_applies_when_owned_and_returns_null_when_foreign_or_missing()
    {
        var (svc, repo) = Build();
        var sw = Guid.NewGuid();
        var owned = new InBodyReading(sw, new DateOnly(2024, 1, 1), 1m, 1m, 1m, 1m, 1m, 1m, Guid.NewGuid());
        repo.Setup(r => r.GetTrackedAsync(owned.Id, It.IsAny<CancellationToken>())).ReturnsAsync(owned);
        var foreign = new InBodyReading(Guid.NewGuid(), new DateOnly(2024, 1, 1), 1m, 1m, 1m, 1m, 1m, 1m, Guid.NewGuid());
        repo.Setup(r => r.GetTrackedAsync(foreign.Id, It.IsAny<CancellationToken>())).ReturnsAsync(foreign);

        var ok = await svc.UpdateAsync(sw, owned.Id, Req());
        var foreignResult = await svc.UpdateAsync(sw, foreign.Id, Req());
        var missing = await svc.UpdateAsync(sw, Guid.NewGuid(), Req());

        Assert.NotNull(ok);
        Assert.Equal(180m, owned.HeightCm);       // updated in place
        Assert.Null(foreignResult);                // ownership guard
        Assert.Null(missing);
    }

    [Fact]
    public async Task Delete_removes_when_owned_false_when_foreign_or_missing()
    {
        var (svc, repo) = Build();
        var sw = Guid.NewGuid();
        var owned = new InBodyReading(sw, new DateOnly(2024, 1, 1), 1m, 1m, 1m, 1m, 1m, 1m, Guid.NewGuid());
        repo.Setup(r => r.GetTrackedAsync(owned.Id, It.IsAny<CancellationToken>())).ReturnsAsync(owned);

        Assert.True(await svc.DeleteAsync(sw, owned.Id));
        repo.Verify(r => r.Remove(owned), Times.Once);
        Assert.False(await svc.DeleteAsync(sw, Guid.NewGuid()));
    }
}
