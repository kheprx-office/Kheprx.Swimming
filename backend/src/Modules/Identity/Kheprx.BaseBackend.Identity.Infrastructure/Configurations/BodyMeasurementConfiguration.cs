using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class BodyMeasurementConfiguration : IEntityTypeConfiguration<BodyMeasurement>
{
    public void Configure(EntityTypeBuilder<BodyMeasurement> builder)
    {
        builder.ToTable("body_measurement", "athlete");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.SwimmerId).IsRequired();
        builder.Property(m => m.MeasuredAt).IsRequired();
        builder.Property(m => m.RightArmCm).HasPrecision(5, 1).IsRequired();
        builder.Property(m => m.LeftArmCm).HasPrecision(5, 1).IsRequired();
        builder.Property(m => m.RightLegCm).HasPrecision(5, 1).IsRequired();
        builder.Property(m => m.LeftLegCm).HasPrecision(5, 1).IsRequired();
        builder.Property(m => m.TorsoCm).HasPrecision(5, 1).IsRequired();
        builder.Property(m => m.BustDiameterCm).HasPrecision(5, 1).IsRequired();
        builder.Property(m => m.WaistDiameterCm).HasPrecision(5, 1).IsRequired();
        builder.HasIndex(m => new { m.SwimmerId, m.MeasuredAt });
        builder.HasOne<SwimmerProfile>().WithMany().HasForeignKey(m => m.SwimmerId).OnDelete(DeleteBehavior.Cascade);
    }
}
