using Kheprx.BaseBackend.Health.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Health.Infrastructure.Configurations;

internal sealed class FeedbackEntryConfiguration : IEntityTypeConfiguration<FeedbackEntry>
{
    public void Configure(EntityTypeBuilder<FeedbackEntry> builder)
    {
        builder.ToTable("feedback_entry", "health");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.SwimmerId).IsRequired();   // loose Guid — no cross-module FK
        builder.Property(e => e.Rating).IsRequired();       // smallint
        builder.Property(e => e.CategoryId).IsRequired();   // loose Guid → reference.feedback_category
        builder.Property(e => e.Comment).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.AuthorId).IsRequired();     // loose Guid — no cross-module FK
        builder.Property(e => e.EntryDate).IsRequired();    // DateOnly → date
        builder.HasIndex(e => new { e.SwimmerId, e.EntryDate });
    }
}
