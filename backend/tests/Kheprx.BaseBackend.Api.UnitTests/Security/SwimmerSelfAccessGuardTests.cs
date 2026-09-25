using Kheprx.BaseBackend.Api.Security;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Moq;
using Xunit;

public class SwimmerSelfAccessGuardTests
{
    private static (ISwimmerSelfAccessGuard guard, Mock<ISwimmerService> svc) Build()
    {
        var svc = new Mock<ISwimmerService>();
        return (new SwimmerSelfAccessGuard(svc.Object), svc);
    }

    [Fact]
    public async Task Coach_can_read_any_swimmer()
    {
        var (guard, _) = Build();
        Assert.True(await guard.CanReadAsync(callerIsSwimmer: false, Guid.NewGuid(), Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Swimmer_can_read_own_record()
    {
        var (guard, svc) = Build();
        var userId = Guid.NewGuid();
        var mine = Guid.NewGuid();
        svc.Setup(s => s.GetSwimmerIdByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(mine);
        Assert.True(await guard.CanReadAsync(callerIsSwimmer: true, userId, mine, default));
    }

    [Fact]
    public async Task Swimmer_cannot_read_foreign_record()
    {
        var (guard, svc) = Build();
        var userId = Guid.NewGuid();
        svc.Setup(s => s.GetSwimmerIdByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        Assert.False(await guard.CanReadAsync(callerIsSwimmer: true, userId, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Swimmer_without_profile_is_denied()
    {
        var (guard, svc) = Build();
        var userId = Guid.NewGuid();
        svc.Setup(s => s.GetSwimmerIdByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        Assert.False(await guard.CanReadAsync(callerIsSwimmer: true, userId, Guid.NewGuid(), default));
    }
}
