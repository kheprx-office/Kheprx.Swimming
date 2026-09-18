using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("role", "reference");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();
        builder.Property(r => r.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(r => r.NameAr).HasMaxLength(100);
    }
}
