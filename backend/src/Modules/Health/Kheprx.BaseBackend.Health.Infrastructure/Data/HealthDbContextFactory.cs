using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kheprx.BaseBackend.Health.Infrastructure.Data;

// Design-time factory used by `dotnet ef`. Uses a local-dev connection string;
// at runtime the application uses ConnectionStrings:Postgres from configuration instead.
public sealed class HealthDbContextFactory : IDesignTimeDbContextFactory<HealthDbContext>
{
    public HealthDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HealthDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=basebackend;Username=postgres;Password=postgres")
            .Options;
        return new HealthDbContext(options);
    }
}
