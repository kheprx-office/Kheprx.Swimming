using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class GuardianConfiguration : IEntityTypeConfiguration<Guardian>
{
    public void Configure(EntityTypeBuilder<Guardian> builder)
    {
        builder.ToTable("guardian", "athlete");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.SwimmerId).IsRequired();
        builder.Property(g => g.RelationId).IsRequired();
        builder.Property(g => g.Name).HasMaxLength(200).IsRequired();
        builder.Property(g => g.NationalId).HasMaxLength(14).IsRequired();
        builder.Property(g => g.Phone).HasMaxLength(30).IsRequired();
        builder.HasIndex(g => new { g.SwimmerId, g.RelationId }).IsUnique();
        builder.HasOne<SwimmerProfile>().WithMany().HasForeignKey(g => g.SwimmerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<GuardianRelation>().WithMany().HasForeignKey(g => g.RelationId).OnDelete(DeleteBehavior.Restrict);
    }
}
