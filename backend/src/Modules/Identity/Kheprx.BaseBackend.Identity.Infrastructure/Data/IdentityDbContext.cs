using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Data;

public sealed class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Gender> Genders => Set<Gender>();
    public DbSet<HeadCoachProfile> HeadCoachProfiles => Set<HeadCoachProfile>();
    public DbSet<CaptainProfile> CaptainProfiles => Set<CaptainProfile>();
    public DbSet<SwimmerProfile> SwimmerProfiles => Set<SwimmerProfile>();
    public DbSet<Stroke> Strokes => Set<Stroke>();
    public DbSet<BloodType> BloodTypes => Set<BloodType>();
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<SwimmerSpecialization> SwimmerSpecializations => Set<SwimmerSpecialization>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
