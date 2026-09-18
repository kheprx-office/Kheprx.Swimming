using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class SwimmerSpecializationConfiguration : IEntityTypeConfiguration<SwimmerSpecialization>
{
    public void Configure(EntityTypeBuilder<SwimmerSpecialization> builder)
    {
        builder.ToTable("swimmer_specialization", "athlete");
        builder.HasKey(x => new { x.SwimmerProfileId, x.StrokeId });
        builder.HasOne<SwimmerProfile>().WithMany().HasForeignKey(x => x.SwimmerProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Stroke>().WithMany().HasForeignKey(x => x.StrokeId).OnDelete(DeleteBehavior.Restrict);
    }
}
