using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class SwimmerProfileConfiguration : IEntityTypeConfiguration<SwimmerProfile>
{
    public void Configure(EntityTypeBuilder<SwimmerProfile> builder)
    {
        builder.ToTable("swimmer_profile");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Uid).HasMaxLength(32).IsRequired();
        builder.HasIndex(s => s.Uid).IsUnique();
        builder.Property(s => s.UserId).IsRequired();
        builder.HasIndex(s => s.UserId).IsUnique();
        builder.Property(s => s.TrainingClubId).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Club>().WithMany().HasForeignKey(s => s.TrainingClubId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Club>().WithMany().HasForeignKey(s => s.RepresentChampionshipClubId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BloodType>().WithMany().HasForeignKey(s => s.BloodTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
