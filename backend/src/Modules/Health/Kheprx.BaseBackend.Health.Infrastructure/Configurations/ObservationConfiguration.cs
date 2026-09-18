using Kheprx.BaseBackend.Health.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Health.Infrastructure.Configurations;

internal sealed class ObservationConfiguration : IEntityTypeConfiguration<Observation>
{
    public void Configure(EntityTypeBuilder<Observation> builder)
    {
        builder.ToTable("observation", "health");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.SwimmerId).IsRequired();   // loose Guid — no cross-module FK
        builder.Property(o => o.CategoryId).IsRequired();  // loose Guid — reference.observation_category (other module)
        builder.Property(o => o.FieldLabel).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Value).HasMaxLength(500).IsRequired();
        builder.Property(o => o.ObservedDate).IsRequired(); // DateTime → timestamptz (npgsql default)
        builder.Property(o => o.RecordedBy).IsRequired();  // loose Guid — no cross-module FK
    }
}
