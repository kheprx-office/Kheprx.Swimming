using Kheprx.BaseBackend.Championships.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Configurations;

internal sealed class RaceResultConfiguration : IEntityTypeConfiguration<RaceResult>
{
    public void Configure(EntityTypeBuilder<RaceResult> builder)
    {
        builder.ToTable("race_result", "championships");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.RaceSessionId).IsRequired(); // loose Guid -> race_session
        builder.Property(r => r.SwimmerId).IsRequired();     // loose Guid -> identity.swimmer_profile
        builder.Property(r => r.TimeMs).IsRequired();
        builder.Property(r => r.Points).IsRequired();
        builder.Property(r => r.IsPersonalBest).IsRequired();
        builder.Property(r => r.RecordedBy).IsRequired();     // loose Guid -> identity.app_user
        builder.HasIndex(r => new { r.RaceSessionId, r.SwimmerId }).IsUnique();
    }
}
