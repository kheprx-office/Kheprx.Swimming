using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class GenderConfiguration : IEntityTypeConfiguration<Gender>
{
    public void Configure(EntityTypeBuilder<Gender> builder)
    {
        builder.ToTable("gender", "reference");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(g => g.Code).IsUnique();
        builder.Property(g => g.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(g => g.NameAr).HasMaxLength(100);
    }
}
