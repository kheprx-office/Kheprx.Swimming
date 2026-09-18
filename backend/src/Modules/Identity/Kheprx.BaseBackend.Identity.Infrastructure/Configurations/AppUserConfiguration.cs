using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_user");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Username).HasMaxLength(100).IsRequired();
        builder.HasIndex(u => u.Username).IsUnique();
        builder.Property(u => u.Email).HasMaxLength(256);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(512);
        builder.Property(u => u.NameAr).HasMaxLength(200);
        builder.Property(u => u.Phone).HasMaxLength(40);
        builder.Property(u => u.RoleId).IsRequired();
        builder.Property(u => u.IsFirstLogin).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.HasOne<Role>().WithMany().HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Gender>().WithMany().HasForeignKey(u => u.GenderId).OnDelete(DeleteBehavior.Restrict);
    }
}
