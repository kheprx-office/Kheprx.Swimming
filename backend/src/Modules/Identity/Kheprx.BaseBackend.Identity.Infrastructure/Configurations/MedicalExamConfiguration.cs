using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class MedicalExamConfiguration : IEntityTypeConfiguration<MedicalExam>
{
    public void Configure(EntityTypeBuilder<MedicalExam> builder)
    {
        builder.ToTable("medical_exam", "athlete");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.SwimmerId).IsRequired();
        builder.Property(e => e.ExamDate).IsRequired();
        builder.Property(e => e.Hemoglobin).HasPrecision(4, 1).IsRequired();
        builder.Property(e => e.HeightCm).HasPrecision(5, 1).IsRequired();
        builder.Property(e => e.WeightKg).HasPrecision(5, 1).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.HasIndex(e => e.SwimmerId);
        builder.HasOne<SwimmerProfile>().WithMany().HasForeignKey(e => e.SwimmerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FitnessAssessment>().WithMany().HasForeignKey(e => e.InternalMedId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FitnessAssessment>().WithMany().HasForeignKey(e => e.HeartAssessId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FitnessAssessment>().WithMany().HasForeignKey(e => e.SpineAssessId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BloodType>().WithMany().HasForeignKey(e => e.BloodTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
