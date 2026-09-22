using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Kheprx.BaseBackend.Identity.Application.Services;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Services;

public class UserServiceDisplayNamesTests
{
    [Fact]
    public async Task GetDisplayNamesAsync_maps_resolved_users_and_skips_unknown_ids()
    {
        var users = new Mock<IUserRepository>();
        var roleId = Guid.NewGuid();
        var coach = new AppUser("coach.omar", "Coach Omar", roleId, nameAr: "الكابتن عمر");
        users.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new[] { coach });
        var svc = new UserService(users.Object, Mock.Of<IRoleRepository>(), Mock.Of<IPasswordHasher>());

        var map = await svc.GetDisplayNamesAsync(new[] { coach.Id, Guid.NewGuid() });

        Assert.Single(map);
        Assert.Equal("Coach Omar", map[coach.Id].NameEn);
        Assert.Equal("الكابتن عمر", map[coach.Id].NameAr);
    }

    [Fact]
    public async Task GetDisplayNamesAsync_returns_empty_for_no_ids()
    {
        var svc = new UserService(Mock.Of<IUserRepository>(), Mock.Of<IRoleRepository>(), Mock.Of<IPasswordHasher>());
        Assert.Empty(await svc.GetDisplayNamesAsync(Array.Empty<Guid>()));
    }
}
