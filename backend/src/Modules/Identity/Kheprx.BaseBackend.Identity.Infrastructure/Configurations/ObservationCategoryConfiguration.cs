using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class ObservationCategoryConfiguration : IEntityTypeConfiguration<ObservationCategory>
{
    public void Configure(EntityTypeBuilder<ObservationCategory> builder)
    {
        builder.ToTable("observation_category", "reference");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(c => c.Code).IsUnique();
        builder.Property(c => c.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(c => c.NameAr).HasMaxLength(100);
    }
}
