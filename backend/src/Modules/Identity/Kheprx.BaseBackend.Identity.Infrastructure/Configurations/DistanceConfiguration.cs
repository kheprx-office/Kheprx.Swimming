using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class DistanceConfiguration : IEntityTypeConfiguration<Distance>
{
    public void Configure(EntityTypeBuilder<Distance> builder)
    {
        builder.ToTable("distance", "reference");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(d => d.Code).IsUnique();
        builder.Property(d => d.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(d => d.NameAr).HasMaxLength(100);
        builder.Property(d => d.Meters).IsRequired();
    }
}
