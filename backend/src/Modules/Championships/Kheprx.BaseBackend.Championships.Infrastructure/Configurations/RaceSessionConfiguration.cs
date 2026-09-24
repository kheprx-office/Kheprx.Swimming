using Kheprx.BaseBackend.Championships.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Configurations;

internal sealed class RaceSessionConfiguration : IEntityTypeConfiguration<RaceSession>
{
    public void Configure(EntityTypeBuilder<RaceSession> builder)
    {
        builder.ToTable("race_session", "championships");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.DayId).IsRequired();         // loose Guid → competition_day
        builder.Property(s => s.StrokeId).IsRequired();      // loose Guid → reference.stroke
        builder.Property(s => s.DistanceId).IsRequired();    // loose Guid → reference.distance
        builder.Property(s => s.ScheduledTime);              // TimeOnly? → time, nullable
        builder.HasIndex(s => s.DayId);
    }
}
