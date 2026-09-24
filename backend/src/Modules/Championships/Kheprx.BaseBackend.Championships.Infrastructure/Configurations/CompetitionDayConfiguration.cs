using Kheprx.BaseBackend.Championships.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Configurations;

internal sealed class CompetitionDayConfiguration : IEntityTypeConfiguration<CompetitionDay>
{
    public void Configure(EntityTypeBuilder<CompetitionDay> builder)
    {
        builder.ToTable("competition_day", "championships");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.EventId).IsRequired();       // loose Guid → competition_event
        builder.Property(d => d.LabelEn).IsRequired();
        builder.Property(d => d.LabelAr);
        builder.Property(d => d.DayDate).IsRequired();       // DateOnly → date
        builder.HasIndex(d => d.EventId);
    }
}
