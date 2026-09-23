using Kheprx.BaseBackend.Championships.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Configurations;

internal sealed class ChampionshipEnrollmentConfiguration : IEntityTypeConfiguration<ChampionshipEnrollment>
{
    public void Configure(EntityTypeBuilder<ChampionshipEnrollment> builder)
    {
        builder.ToTable("championship_enrollment", "championships");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EventId).IsRequired();     // loose Guid → championships.competition_event
        builder.Property(e => e.SwimmerId).IsRequired();   // loose Guid → identity.swimmer_profile
        builder.HasIndex(e => new { e.EventId, e.SwimmerId }).IsUnique();
    }
}
