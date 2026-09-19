using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class GuardianRelationConfiguration : IEntityTypeConfiguration<GuardianRelation>
{
    public void Configure(EntityTypeBuilder<GuardianRelation> builder)
    {
        builder.ToTable("guardian_relation", "reference");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();
        builder.Property(r => r.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(r => r.NameAr).HasMaxLength(100);
    }
}
