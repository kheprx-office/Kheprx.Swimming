using Kheprx.BaseBackend.Health.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Health.Infrastructure.Configurations;

internal sealed class MedicalTestConfiguration : IEntityTypeConfiguration<MedicalTest>
{
    public void Configure(EntityTypeBuilder<MedicalTest> builder)
    {
        builder.ToTable("medical_test", "health");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(t => t.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Unit).HasMaxLength(50).IsRequired();
        builder.Property(t => t.LowerBound).HasColumnType("numeric(8,2)").IsRequired();
        builder.Property(t => t.UpperBound).HasColumnType("numeric(8,2)").IsRequired();
        builder.Property(t => t.CreatedBy).IsRequired();   // loose Guid — no cross-module FK
        builder.Property(t => t.CreatedAt).IsRequired();
    }
}
