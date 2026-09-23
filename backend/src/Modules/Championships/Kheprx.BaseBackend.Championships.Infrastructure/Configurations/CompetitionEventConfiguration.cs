using Kheprx.BaseBackend.Championships.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Configurations;

internal sealed class CompetitionEventConfiguration : IEntityTypeConfiguration<CompetitionEvent>
{
    public void Configure(EntityTypeBuilder<CompetitionEvent> builder)
    {
        builder.ToTable("competition_event", "championships");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.NameEn).IsRequired();
        builder.Property(e => e.NameAr);
        builder.Property(e => e.StartDate).IsRequired();     // DateOnly → date
        builder.Property(e => e.EndDate).IsRequired();
        builder.Property(e => e.LocationEn).IsRequired();
        builder.Property(e => e.LocationAr);
        builder.Property(e => e.StatusId).IsRequired();      // loose Guid → reference.competition_status
        builder.Property(e => e.CreatedBy).IsRequired();     // loose Guid → identity.app_user
    }
}
