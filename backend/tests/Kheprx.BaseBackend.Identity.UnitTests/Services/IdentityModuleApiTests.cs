using Kheprx.BaseBackend.Identity.Domain.ReadModels;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Services;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Services;

public class IdentityModuleApiTests
{
    private static readonly Guid W = Guid.NewGuid();
    private static readonly Guid M = Guid.NewGuid();

    private static IdentityModuleApi Sut(
        IWorkerReadRepository repo,
        IManagerReadRepository? managers = null,
        IMoqawelReadRepository? moqaweleen = null)
        => new(null!, repo, managers ?? Mock.Of<IManagerReadRepository>(),
               moqaweleen ?? Mock.Of<IMoqawelReadRepository>());

    private static WorkerListRow Row() =>
        new(W, "محمد", "male", 30, "0100000000", "29001010100010", true);

    [Fact]
    public async Task ListWorkersAsync_forwards_filters_and_maps_person_fields()
    {
        var ids = new[] { W };
        var repo = new Mock<IWorkerReadRepository>();
        repo.Setup(r => r.ListAsync(ids, "مح", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Row() });

        var result = await Sut(repo.Object).ListWorkersAsync(ids, "مح", true, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(W, dto.Id);
        Assert.Equal("محمد", dto.FullName);
        Assert.Equal("29001010100010", dto.Nid);
        Assert.True(dto.IsActive);
        Assert.Equal("male", dto.Gender);
        Assert.Equal(30, dto.Age);
        Assert.Equal("0100000000", dto.Phone);
        repo.Verify(r => r.ListAsync(ids, "مح", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListWorkersAsync_maps_person_fields_no_engagement()
    {
        var repo = new Mock<IWorkerReadRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<IReadOnlyList<Guid>>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Row() });

        var result = await Sut(repo.Object).ListWorkersAsync(new[] { W }, null, null, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(W, dto.Id);
        Assert.Equal("محمد", dto.FullName);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task ListManagerNamesAsync_maps_rows_to_dtos()
    {
        var ids = new[] { M };
        var managers = new Mock<IManagerReadRepository>();
        managers.Setup(r => r.ListNamesAsync(ids, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ManagerNameRow(M, "أحمد سمير") });

        var result = await Sut(Mock.Of<IWorkerReadRepository>(), managers.Object)
            .ListManagerNamesAsync(ids, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(M, dto.ManagerId);
        Assert.Equal("أحمد سمير", dto.FullName);
    }

    [Fact]
    public async Task ListManagerNamesAsync_returns_empty_for_empty_input()
    {
        var managers = new Mock<IManagerReadRepository>();
        managers.Setup(r => r.ListNamesAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ManagerNameRow>());

        var result = await Sut(Mock.Of<IWorkerReadRepository>(), managers.Object)
            .ListManagerNamesAsync(Array.Empty<Guid>(), CancellationToken.None);

        Assert.Empty(result);
    }

    private static MoqawelListRow MoqawelRow() =>
        new(M, "مقاول أحمد", "male", 40, "0100000001", "29001010100011", true);

    [Fact]
    public async Task ListMoqaweleenAsync_forwards_filters_and_maps_person_fields()
    {
        var ids = new[] { M };
        var repo = new Mock<IMoqawelReadRepository>();
        repo.Setup(r => r.ListAsync(ids, "مق", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MoqawelRow() });

        var result = await Sut(Mock.Of<IWorkerReadRepository>(), moqaweleen: repo.Object)
            .ListMoqaweleenAsync(ids, "مق", true, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(M, dto.Id);
        Assert.Equal("مقاول أحمد", dto.FullName);
        Assert.Equal("29001010100011", dto.Nid);
        Assert.True(dto.IsActive);
        Assert.Equal("male", dto.Gender);
        Assert.Equal(40, dto.Age);
        Assert.Equal("0100000001", dto.Phone);
        repo.Verify(r => r.ListAsync(ids, "مق", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListMoqaweleenAsync_maps_person_fields_no_filters()
    {
        var repo = new Mock<IMoqawelReadRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<IReadOnlyList<Guid>>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MoqawelRow() });

        var result = await Sut(Mock.Of<IWorkerReadRepository>(), moqaweleen: repo.Object)
            .ListMoqaweleenAsync(new[] { M }, null, null, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(M, dto.Id);
        Assert.Equal("مقاول أحمد", dto.FullName);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task ListAvailableWorkersAsync_requests_active_only_and_maps_id_name()
    {
        var repo = new Mock<IWorkerReadRepository>();
        repo.Setup(r => r.ListAllAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Row() });

        var result = await Sut(repo.Object).ListAvailableWorkersAsync(CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(W, dto.Id);            // id flows through as the user id
        Assert.Equal("محمد", dto.FullName);
        repo.Verify(r => r.ListAllAsync(true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListAvailableMoqaweleenAsync_requests_active_only_and_maps_id_name()
    {
        var repo = new Mock<IMoqawelReadRepository>();
        repo.Setup(r => r.ListAllAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MoqawelRow() });

        var result = await Sut(Mock.Of<IWorkerReadRepository>(), moqaweleen: repo.Object)
            .ListAvailableMoqaweleenAsync(CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(M, dto.Id);            // id flows through as the user id
        Assert.Equal("مقاول أحمد", dto.FullName);
        repo.Verify(r => r.ListAllAsync(true, It.IsAny<CancellationToken>()), Times.Once);
    }
}
