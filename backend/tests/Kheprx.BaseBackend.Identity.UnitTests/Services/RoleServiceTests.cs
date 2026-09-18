using Kheprx.BaseBackend.Identity.Application.Services;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Services;

public class RoleServiceTests
{
    [Fact]
    public async Task GetAll_maps_roles_to_dtos()
    {
        var roles = new Role[] { new("admin", "Administrator"), new("captain", "Captain") };
        var repo = new Mock<IRoleRepository>();
        repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(roles);

        var result = await new RoleService(repo.Object).GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("admin", result[0].Code);
        Assert.Equal("Administrator", result[0].NameEn);
        Assert.Null(result[0].NameAr);
    }
}
