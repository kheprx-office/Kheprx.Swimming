using System;
using System.Threading;
using System.Threading.Tasks;
using Kheprx.BaseBackend.Identity.Domain.ReadModels;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Services;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests;

public class IdentityModuleWorkerDetailTests
{
    [Fact]
    public async Task GetWorkerDetailAsync_maps_row_including_hiredate()
    {
        var id = Guid.NewGuid();
        var repo = new Mock<IWorkerReadRepository>();
        repo.Setup(r => r.GetDetailAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkerDetailRow(id, "محمد أحمد", "male", 24, "0100000001", "29001010100011", true, new DateOnly(2024, 3, 12)));
        // _db is unused by GetWorkerDetailAsync, so null! is safe here.
        var api = new IdentityModuleApi(null!, repo.Object, Mock.Of<IManagerReadRepository>(), Mock.Of<IMoqawelReadRepository>());

        var dto = await api.GetWorkerDetailAsync(id);

        Assert.NotNull(dto);
        Assert.Equal("محمد أحمد", dto!.FullName);
        Assert.Equal(new DateOnly(2024, 3, 12), dto.HireDate);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task GetWorkerDetailAsync_returns_null_when_not_a_worker()
    {
        var repo = new Mock<IWorkerReadRepository>();
        repo.Setup(r => r.GetDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkerDetailRow?)null);
        var api = new IdentityModuleApi(null!, repo.Object, Mock.Of<IManagerReadRepository>(), Mock.Of<IMoqawelReadRepository>());

        Assert.Null(await api.GetWorkerDetailAsync(Guid.NewGuid()));
    }
}
