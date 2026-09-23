using Kheprx.BaseBackend.Attendance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Attendance.Infrastructure.Configurations;

internal sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("attendance_record", "attendance");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.SwimmerId).IsRequired();     // loose Guid — no cross-module FK
        builder.Property(e => e.SessionDate).IsRequired();   // DateOnly → date
        builder.Property(e => e.StatusId).IsRequired();      // loose Guid → reference.attendance_status
        builder.Property(e => e.RecordedBy).IsRequired();    // loose Guid → identity.app_user
        builder.Property(e => e.CoachNoteEn);
        builder.Property(e => e.CoachNoteAr);
        builder.HasIndex(e => new { e.SwimmerId, e.SessionDate }).IsUnique();
    }
}
