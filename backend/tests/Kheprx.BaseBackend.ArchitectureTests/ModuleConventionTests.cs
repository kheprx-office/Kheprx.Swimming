using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Kheprx.BaseBackend.Health.Infrastructure.Extensions;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kheprx.BaseBackend.ArchitectureTests;

public class ModuleConventionTests
{
    [Fact]
    public void AddIdentityModule_registers_identity_services()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=u;Password=p"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddIdentityModule(configuration);

        Assert.Contains(services, d => d.ServiceType == typeof(IAuthService));
        Assert.Contains(services, d => d.ServiceType == typeof(IRoleService));
        Assert.Contains(services, d => d.ServiceType == typeof(IUserService));
    }

    [Fact]
    public void AddHealthModule_registers_health_services()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=u;Password=p"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddHealthModule(configuration);

        Assert.Contains(services, d => d.ServiceType == typeof(IMedicalTestService));
        Assert.Contains(services, d => d.ServiceType == typeof(IMedicalTestRepository));
    }
}
