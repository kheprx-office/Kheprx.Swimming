using Kheprx.BaseBackend.Health.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Health.Infrastructure.Data;

public sealed class HealthDbContext : DbContext
{
    public HealthDbContext(DbContextOptions<HealthDbContext> options) : base(options) { }

    public DbSet<MedicalTest> MedicalTests => Set<MedicalTest>();
    public DbSet<HealthReading> HealthReadings => Set<HealthReading>();
    public DbSet<Observation> Observations => Set<Observation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("health");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HealthDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
