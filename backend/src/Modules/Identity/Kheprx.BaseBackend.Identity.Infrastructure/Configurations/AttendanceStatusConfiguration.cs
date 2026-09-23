using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class AttendanceStatusConfiguration : IEntityTypeConfiguration<AttendanceStatus>
{
    public void Configure(EntityTypeBuilder<AttendanceStatus> builder)
    {
        builder.ToTable("attendance_status", "reference");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(s => s.Code).IsUnique();
        builder.Property(s => s.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(s => s.NameAr).HasMaxLength(100);
    }
}
