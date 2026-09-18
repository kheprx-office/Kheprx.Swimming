using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class BloodTypeConfiguration : IEntityTypeConfiguration<BloodType>
{
    public void Configure(EntityTypeBuilder<BloodType> builder)
    {
        builder.ToTable("blood_type", "reference");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Code).HasMaxLength(10).IsRequired();
        builder.HasIndex(b => b.Code).IsUnique();
        builder.Property(b => b.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(b => b.NameAr).HasMaxLength(100);
    }
}
