using Kheprx.BaseBackend.Health.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Health.Infrastructure.Configurations;

internal sealed class HealthReadingConfiguration : IEntityTypeConfiguration<HealthReading>
{
    public void Configure(EntityTypeBuilder<HealthReading> builder)
    {
        builder.ToTable("health_reading", "health");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.SwimmerId).IsRequired();     // loose Guid — no cross-module FK
        builder.Property(r => r.MedicalTestId).IsRequired();
        builder.Property(r => r.Value).HasColumnType("numeric(8,2)").IsRequired();
        builder.Property(r => r.ReadingDate).IsRequired();   // DateTime → timestamptz (npgsql default)
        builder.Property(r => r.RecordedBy).IsRequired();    // loose Guid — no cross-module FK

        builder.HasOne<MedicalTest>()
            .WithMany()
            .HasForeignKey(r => r.MedicalTestId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
