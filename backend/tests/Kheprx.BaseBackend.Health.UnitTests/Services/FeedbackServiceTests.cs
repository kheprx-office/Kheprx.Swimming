using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Services;

public class FeedbackServiceTests
{
    private static (FeedbackService svc, Mock<IFeedbackEntryRepository> repo) Build()
    {
        var repo = new Mock<IFeedbackEntryRepository>();
        return (new FeedbackService(repo.Object), repo);
    }

    private static CreateFeedbackEntryRequest Req() => new(5, Guid.NewGuid(), "Great rhythm");

    [Fact]
    public async Task Create_stamps_author_saves_and_returns_dto()
    {
        var (svc, repo) = Build();
        FeedbackEntry? added = null;
        repo.Setup(r => r.AddAsync(It.IsAny<FeedbackEntry>(), It.IsAny<CancellationToken>()))
            .Callback<FeedbackEntry, CancellationToken>((e, _) => added = e).Returns(Task.CompletedTask);
        var swimmerId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var dto = await svc.CreateAsync(swimmerId, Req(), authorId);

        Assert.NotNull(added);
        Assert.Equal(swimmerId, added!.SwimmerId);
        Assert.Equal(authorId, added.AuthorId);
        Assert.Equal(swimmerId, dto.SwimmerId);
        Assert.Equal((short)5, dto.Rating);
        Assert.Equal(string.Empty, dto.AuthorNameEn); // enriched later by the controller
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_applies_when_owned_and_null_when_foreign_or_missing()
    {
        var (svc, repo) = Build();
        var sw = Guid.NewGuid();
        var owned = new FeedbackEntry(sw, 3, Guid.NewGuid(), "ok", Guid.NewGuid());
        repo.Setup(r => r.GetTrackedAsync(owned.Id, It.IsAny<CancellationToken>())).ReturnsAsync(owned);
        var foreign = new FeedbackEntry(Guid.NewGuid(), 3, Guid.NewGuid(), "x", Guid.NewGuid());
        repo.Setup(r => r.GetTrackedAsync(foreign.Id, It.IsAny<CancellationToken>())).ReturnsAsync(foreign);

        var ok = await svc.UpdateAsync(sw, owned.Id, Req());
        var foreignResult = await svc.UpdateAsync(sw, foreign.Id, Req());
        var missing = await svc.UpdateAsync(sw, Guid.NewGuid(), Req());

        Assert.NotNull(ok);
        Assert.Equal((short)5, owned.Rating);   // updated in place
        Assert.Null(foreignResult);              // ownership guard
        Assert.Null(missing);
    }

    [Fact]
    public async Task Delete_true_when_owned_false_when_foreign_or_missing()
    {
        var (svc, repo) = Build();
        var sw = Guid.NewGuid();
        var owned = new FeedbackEntry(sw, 3, Guid.NewGuid(), "ok", Guid.NewGuid());
        repo.Setup(r => r.GetTrackedAsync(owned.Id, It.IsAny<CancellationToken>())).ReturnsAsync(owned);

        Assert.True(await svc.DeleteAsync(sw, owned.Id));
        repo.Verify(r => r.Remove(owned), Times.Once);
        Assert.False(await svc.DeleteAsync(sw, Guid.NewGuid()));
    }
}
