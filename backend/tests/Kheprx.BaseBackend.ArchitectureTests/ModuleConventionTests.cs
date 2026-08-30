using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Contracts;
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
        Assert.Contains(services, d => d.ServiceType == typeof(IIdentityModule));
    }
}
