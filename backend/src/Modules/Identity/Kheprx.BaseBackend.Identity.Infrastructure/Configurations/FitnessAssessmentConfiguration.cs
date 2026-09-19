using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class FitnessAssessmentConfiguration : IEntityTypeConfiguration<FitnessAssessment>
{
    public void Configure(EntityTypeBuilder<FitnessAssessment> builder)
    {
        builder.ToTable("fitness_assessment", "reference");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(f => f.Code).IsUnique();
        builder.Property(f => f.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(f => f.NameAr).HasMaxLength(100);
    }
}
