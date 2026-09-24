using Kheprx.BaseBackend.Attendance.Domain.Entities;
using Kheprx.BaseBackend.Attendance.Domain.Repositories;
using Kheprx.BaseBackend.Attendance.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Attendance.Infrastructure.Repositories;

internal sealed class AttendanceRecordRepository : IAttendanceRecordRepository
{
    private readonly AttendanceDbContext _db;
    public AttendanceRecordRepository(AttendanceDbContext db) => _db = db;

    public async Task<IReadOnlyList<AttendanceRecord>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default)
        => await _db.AttendanceRecords.AsNoTracking()
              .Where(r => r.SwimmerId == swimmerId)
              .OrderByDescending(r => r.SessionDate).ThenByDescending(r => r.Id)
              .ToListAsync(ct);

    public async Task<IReadOnlyList<AttendanceRecord>> ListByDateAsync(DateOnly date, CancellationToken ct = default)
        => await _db.AttendanceRecords.AsNoTracking()
              .Where(r => r.SessionDate == date)
              .ToListAsync(ct);

    public async Task<IReadOnlyList<AttendanceRecord>> ListByMonthAsync(int year, int month, CancellationToken ct = default)
        => await _db.AttendanceRecords.AsNoTracking()
              .Where(r => r.SessionDate.Year == year && r.SessionDate.Month == month)
              .ToListAsync(ct);

    public async Task<IReadOnlyList<AttendanceRecord>> ListRecentDaysAsync(int dayCount, CancellationToken ct = default)
    {
        var dates = await _db.AttendanceRecords.AsNoTracking()
            .Select(r => r.SessionDate).Distinct()
            .OrderByDescending(d => d).Take(dayCount)
            .ToListAsync(ct);
        if (dates.Count == 0) return Array.Empty<AttendanceRecord>();
        return await _db.AttendanceRecords.AsNoTracking()
            .Where(r => dates.Contains(r.SessionDate))
            .ToListAsync(ct);
    }

    public async Task UpsertSessionAsync(DateOnly date, IReadOnlyList<AttendanceRecord> incoming, CancellationToken ct = default)
    {
        // Tracked load (no AsNoTracking) so Update() mutations are persisted.
        var existing = await _db.AttendanceRecords.Where(r => r.SessionDate == date).ToListAsync(ct);
        var bySwimmer = existing.ToDictionary(r => r.SwimmerId);
        foreach (var rec in incoming)
        {
            if (bySwimmer.TryGetValue(rec.SwimmerId, out var current))
                current.Update(rec.StatusId, rec.RecordedBy, rec.CoachNoteEn, rec.CoachNoteAr);
            else
                _db.AttendanceRecords.Add(rec);
        }
        await _db.SaveChangesAsync(ct);
    }
}
