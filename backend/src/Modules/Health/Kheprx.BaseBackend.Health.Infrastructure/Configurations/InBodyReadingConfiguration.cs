using Kheprx.BaseBackend.Health.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Health.Infrastructure.Configurations;

internal sealed class InBodyReadingConfiguration : IEntityTypeConfiguration<InBodyReading>
{
    public void Configure(EntityTypeBuilder<InBodyReading> builder)
    {
        builder.ToTable("inbody_reading", "health");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.SwimmerId).IsRequired();     // loose Guid — no cross-module FK
        builder.Property(r => r.ReadingDate).IsRequired();   // DateOnly → date
        builder.Property(r => r.HeightCm).HasPrecision(5, 1).IsRequired();
        builder.Property(r => r.WeightKg).HasPrecision(5, 1).IsRequired();
        builder.Property(r => r.FatPct).HasPrecision(4, 1).IsRequired();
        builder.Property(r => r.MusclePct).HasPrecision(4, 1).IsRequired();
        builder.Property(r => r.BoneDensity).HasPrecision(4, 2).IsRequired();
        builder.Property(r => r.BodyDensity).HasPrecision(4, 2).IsRequired();
        builder.Property(r => r.RecordedBy).IsRequired();    // loose Guid — no cross-module FK
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.HasIndex(r => new { r.SwimmerId, r.ReadingDate });
    }
}
