using Kheprx.BaseBackend.Championships.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Data;

public sealed class ChampionshipsDbContext : DbContext
{
    public ChampionshipsDbContext(DbContextOptions<ChampionshipsDbContext> options) : base(options) { }

    public DbSet<CompetitionEvent> CompetitionEvents => Set<CompetitionEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("championships");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChampionshipsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
