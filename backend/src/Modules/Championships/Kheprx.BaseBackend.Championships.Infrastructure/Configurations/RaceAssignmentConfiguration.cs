using Kheprx.BaseBackend.Championships.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Configurations;

internal sealed class RaceAssignmentConfiguration : IEntityTypeConfiguration<RaceAssignment>
{
    public void Configure(EntityTypeBuilder<RaceAssignment> builder)
    {
        builder.ToTable("race_assignment", "championships");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.RaceSessionId).IsRequired(); // loose Guid → race_session
        builder.Property(a => a.SwimmerId).IsRequired();     // loose Guid → identity.swimmer_profile
        builder.HasIndex(a => new { a.RaceSessionId, a.SwimmerId }).IsUnique();
    }
}
