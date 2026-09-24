using Kheprx.BaseBackend.Championships.Application.DTOs;
using Kheprx.BaseBackend.Championships.Application.Services;
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Services;

public class ChampionshipServiceTests
{
    [Fact]
    public async Task ListAsync_maps_entities_to_dtos_with_empty_status_fields()
    {
        var statusId = Guid.NewGuid();
        var ev = new CompetitionEvent("Nats", "الوطنية", new DateOnly(2023, 11, 15),
            new DateOnly(2023, 11, 16), "Cairo", null, statusId, Guid.NewGuid());
        var repo = new Mock<ICompetitionEventRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { ev });

        var svc = new ChampionshipService(repo.Object, new Mock<IChampionshipEnrollmentRepository>().Object, Mock.Of<ICompetitionScheduleRepository>(), Mock.Of<IRaceResultRepository>());
        var list = await svc.ListAsync();

        Assert.Single(list);
        Assert.Equal("Nats", list[0].NameEn);
        Assert.Equal(statusId, list[0].StatusId);
        Assert.Equal("", list[0].StatusCode);        // resolved later, in the controller
        Assert.Equal("", list[0].StatusNameEn);
        Assert.Null(list[0].StatusNameAr);
    }

    [Fact]
    public async Task CreateAsync_builds_entity_persists_and_returns_dto()
    {
        var statusId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        CompetitionEvent? saved = null;
        var repo = new Mock<ICompetitionEventRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<CompetitionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<CompetitionEvent, CancellationToken>((e, _) => saved = e)
            .Returns(Task.CompletedTask);

        var svc = new ChampionshipService(repo.Object, new Mock<IChampionshipEnrollmentRepository>().Object, Mock.Of<ICompetitionScheduleRepository>(), Mock.Of<IRaceResultRepository>());
        var cmd = new CreateCompetitionEventCommand("Spring Cup", "كأس الربيع",
            new DateOnly(2024, 3, 1), new DateOnly(2024, 3, 2), "Cairo", "القاهرة", statusId, createdBy);

        var dto = await svc.CreateAsync(cmd);

        repo.Verify(r => r.AddAsync(It.IsAny<CompetitionEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(saved);
        Assert.Equal("Spring Cup", saved!.NameEn);
        Assert.Equal("كأس الربيع", saved.NameAr);
        Assert.Equal("Cairo", saved.LocationEn);
        Assert.Equal("القاهرة", saved.LocationAr);
        Assert.Equal(statusId, saved.StatusId);
        Assert.Equal(createdBy, saved.CreatedBy);
        // returned dto mirrors the entity; status fields stay empty (resolved in the API layer)
        Assert.Equal("Spring Cup", dto.NameEn);
        Assert.Equal(statusId, dto.StatusId);
        Assert.Equal("", dto.StatusCode);
    }

    [Fact]
    public async Task GetByIdAsync_maps_the_event_with_empty_status_fields()
    {
        var statusId = Guid.NewGuid();
        var ev = new CompetitionEvent("Nats", null, new DateOnly(2023, 11, 15),
            new DateOnly(2023, 11, 16), "Cairo", null, statusId, Guid.NewGuid());
        var events = new Mock<ICompetitionEventRepository>();
        events.Setup(r => r.GetByIdAsync(ev.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ev);
        var enr = new Mock<IChampionshipEnrollmentRepository>();

        var svc = new ChampionshipService(events.Object, enr.Object, Mock.Of<ICompetitionScheduleRepository>(), Mock.Of<IRaceResultRepository>());
        var dto = await svc.GetByIdAsync(ev.Id);

        Assert.NotNull(dto);
        Assert.Equal("Nats", dto!.NameEn);
        Assert.Equal(statusId, dto.StatusId);
        Assert.Equal("", dto.StatusCode);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_missing()
    {
        var events = new Mock<ICompetitionEventRepository>();
        events.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((CompetitionEvent?)null);
        var svc = new ChampionshipService(events.Object, new Mock<IChampionshipEnrollmentRepository>().Object, Mock.Of<ICompetitionScheduleRepository>(), Mock.Of<IRaceResultRepository>());

        Assert.Null(await svc.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetEnrolledSwimmerIdsAsync_returns_null_when_event_missing()
    {
        var events = new Mock<ICompetitionEventRepository>();
        events.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((CompetitionEvent?)null);
        var enr = new Mock<IChampionshipEnrollmentRepository>();
        var svc = new ChampionshipService(events.Object, enr.Object, Mock.Of<ICompetitionScheduleRepository>(), Mock.Of<IRaceResultRepository>());

        Assert.Null(await svc.GetEnrolledSwimmerIdsAsync(Guid.NewGuid()));
        enr.Verify(r => r.ListSwimmerIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetEnrolledSwimmerIdsAsync_returns_ids_when_event_exists()
    {
        var ev = new CompetitionEvent("Nats", null, new DateOnly(2023, 11, 15),
            new DateOnly(2023, 11, 16), "Cairo", null, Guid.NewGuid(), Guid.NewGuid());
        var s1 = Guid.NewGuid();
        var events = new Mock<ICompetitionEventRepository>();
        events.Setup(r => r.GetByIdAsync(ev.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ev);
        var enr = new Mock<IChampionshipEnrollmentRepository>();
        enr.Setup(r => r.ListSwimmerIdsAsync(ev.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { s1 });
        var svc = new ChampionshipService(events.Object, enr.Object, Mock.Of<ICompetitionScheduleRepository>(), Mock.Of<IRaceResultRepository>());

        var ids = await svc.GetEnrolledSwimmerIdsAsync(ev.Id);
        Assert.NotNull(ids);
        Assert.Equal(new[] { s1 }, ids!);
    }

    [Fact]
    public async Task SetEnrollmentsAsync_returns_false_when_event_missing_and_does_not_replace()
    {
        var events = new Mock<ICompetitionEventRepository>();
        events.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((CompetitionEvent?)null);
        var enr = new Mock<IChampionshipEnrollmentRepository>();
        var svc = new ChampionshipService(events.Object, enr.Object, Mock.Of<ICompetitionScheduleRepository>(), Mock.Of<IRaceResultRepository>());

        Assert.False(await svc.SetEnrollmentsAsync(Guid.NewGuid(), new[] { Guid.NewGuid() }));
        enr.Verify(r => r.ReplaceAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetEnrollmentsAsync_replaces_and_returns_true_when_event_exists()
    {
        var ev = new CompetitionEvent("Nats", null, new DateOnly(2023, 11, 15),
            new DateOnly(2023, 11, 16), "Cairo", null, Guid.NewGuid(), Guid.NewGuid());
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var events = new Mock<ICompetitionEventRepository>();
        events.Setup(r => r.GetByIdAsync(ev.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ev);
        var enr = new Mock<IChampionshipEnrollmentRepository>();
        var svc = new ChampionshipService(events.Object, enr.Object, Mock.Of<ICompetitionScheduleRepository>(), Mock.Of<IRaceResultRepository>());

        Assert.True(await svc.SetEnrollmentsAsync(ev.Id, ids));
        enr.Verify(r => r.ReplaceAsync(ev.Id, ids, It.IsAny<CancellationToken>()), Times.Once);
    }
}
