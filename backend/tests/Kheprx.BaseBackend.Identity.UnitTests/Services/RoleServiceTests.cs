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
        var roles = new Role[] { new("admin", labelEn: "Administrator", sortOrder: 1), new("worker", labelEn: "Worker", sortOrder: 4) };
        var repo = new Mock<IRoleRepository>();
        repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(roles);

        var result = await new RoleService(repo.Object).GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("admin", result[0].Code);
        Assert.Equal("Administrator", result[0].LabelEn);
        Assert.Equal(1, result[0].SortOrder);
    }
}
