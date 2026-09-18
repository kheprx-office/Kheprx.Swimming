using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class CaptainProfileConfiguration : IEntityTypeConfiguration<CaptainProfile>
{
    public void Configure(EntityTypeBuilder<CaptainProfile> builder)
    {
        builder.ToTable("captain_profile");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.UserId).IsRequired();
        builder.HasIndex(p => p.UserId).IsUnique();
        builder.Property(p => p.NationalId).HasMaxLength(14).IsRequired();
        builder.HasIndex(p => p.NationalId).IsUnique();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
