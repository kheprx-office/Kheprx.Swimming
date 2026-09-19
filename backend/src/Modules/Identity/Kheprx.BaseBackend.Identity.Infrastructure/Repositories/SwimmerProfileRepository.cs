using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.ReadModels;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class SwimmerProfileRepository : ISwimmerProfileRepository
{
    private readonly IdentityDbContext _db;
    public SwimmerProfileRepository(IdentityDbContext db) => _db = db;

    public Task<int> CountAsync(CancellationToken ct = default)
        => _db.SwimmerProfiles.AsNoTracking().CountAsync(ct);

    public async Task<int> GetMaxUidNumberAsync(CancellationToken ct = default)
    {
        var uids = await _db.SwimmerProfiles.AsNoTracking().Select(s => s.Uid).ToListAsync(ct);
        var max = 0;
        foreach (var uid in uids)
            if (uid.StartsWith("SW-") && int.TryParse(uid.AsSpan(3), out var n) && n > max) max = n;
        return max;
    }

    public async Task<IReadOnlyList<SwimmerListRow>> ListAsync(string? search = null, CancellationToken ct = default)
    {
        var query =
            from p in _db.SwimmerProfiles.AsNoTracking()
            join u in _db.Users.AsNoTracking() on p.UserId equals u.Id
            join c in _db.Clubs.AsNoTracking() on p.TrainingClubId equals c.Id into clubs
            from c in clubs.DefaultIfEmpty()
            join g in _db.Genders.AsNoTracking() on u.GenderId equals g.Id into genders
            from g in genders.DefaultIfEmpty()
            select new { p, u, c, g };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim()
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
            var pattern = "%" + term + "%";
            query = query.Where(x =>
                EF.Functions.ILike(x.u.NameEn, pattern) ||
                (x.u.NameAr != null && EF.Functions.ILike(x.u.NameAr, pattern)) ||
                EF.Functions.ILike(x.p.Uid, pattern));
        }

        return await query
            .OrderBy(x => x.u.NameEn)
            .Select(x => new SwimmerListRow(
                x.p.Id,
                x.p.Uid,
                x.u.NameEn,
                x.u.NameAr,
                x.c == null ? null : x.c.NameEn,
                x.c == null ? null : x.c.NameAr,
                x.g == null ? null : x.g.Code,
                x.u.Dob))
            .ToListAsync(ct);
    }

    public async Task AddAsync(SwimmerProfile profile, CancellationToken ct = default)
        => await _db.SwimmerProfiles.AddAsync(profile, ct);

    public async Task AddSpecializationsAsync(IEnumerable<SwimmerSpecialization> specializations, CancellationToken ct = default)
        => await _db.SwimmerSpecializations.AddRangeAsync(specializations, ct);

    public async Task<bool> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Unique-index race (username/email/uid/user_id). Other DbUpdateExceptions propagate (→ 500).
            return false;
        }
    }

    public Task AddExamAsync(MedicalExam exam, CancellationToken ct = default)
        => _db.MedicalExams.AddAsync(exam, ct).AsTask();

    public Task<MedicalExamRow?> GetLatestExamAsync(Guid swimmerId, CancellationToken ct = default)
        => ExamRows()
            .Where(x => x.SwimmerId == swimmerId)
            .OrderByDescending(x => x.ExamDate).ThenByDescending(x => x.CreatedAt)
            .Select(Project())
            .FirstOrDefaultAsync(ct);

    public Task<MedicalExamRow?> GetExamRowByIdAsync(Guid examId, CancellationToken ct = default)
        => ExamRows()
            .Where(x => x.ExamId == examId)
            .Select(Project())
            .FirstOrDefaultAsync(ct);

    public Task<SwimmerProfileRow?> GetProfileByIdAsync(Guid id, CancellationToken ct = default)
        => (from p in _db.SwimmerProfiles.AsNoTracking()
            where p.Id == id
            join u in _db.Users.AsNoTracking() on p.UserId equals u.Id
            join c in _db.Clubs.AsNoTracking() on p.TrainingClubId equals c.Id into clubs
            from c in clubs.DefaultIfEmpty()
            join g in _db.Genders.AsNoTracking() on u.GenderId equals g.Id into genders
            from g in genders.DefaultIfEmpty()
            select new SwimmerProfileRow(
                p.Id, p.Uid, u.NameEn, u.NameAr, u.Dob,
                g == null ? null : g.Code, u.Phone,
                c == null ? null : c.NameEn, c == null ? null : c.NameAr))
           .FirstOrDefaultAsync(ct);

    public Task<SwimmerProfile?> GetByIdTrackedAsync(Guid id, CancellationToken ct = default)
        => _db.SwimmerProfiles.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<MedicalExamRow>> ListExamsAsync(Guid swimmerId, CancellationToken ct = default)
        => await ExamRows()
            .Where(x => x.SwimmerId == swimmerId)
            .OrderByDescending(x => x.ExamDate).ThenByDescending(x => x.CreatedAt)
            .Select(Project())
            .ToListAsync(ct);

    public Task<MedicalExam?> GetExamTrackedAsync(Guid examId, CancellationToken ct = default)
        => _db.MedicalExams.FirstOrDefaultAsync(e => e.Id == examId, ct);

    public void RemoveExam(MedicalExam exam) => _db.MedicalExams.Remove(exam);

    // exam LEFT-joined to blood_type, INNER-joined to fitness_assessment ×3
    private IQueryable<ExamJoin> ExamRows() =>
        from e in _db.MedicalExams.AsNoTracking()
        join im in _db.FitnessAssessments.AsNoTracking() on e.InternalMedId equals im.Id
        join ha in _db.FitnessAssessments.AsNoTracking() on e.HeartAssessId equals ha.Id
        join sa in _db.FitnessAssessments.AsNoTracking() on e.SpineAssessId equals sa.Id
        join bt in _db.BloodTypes.AsNoTracking() on e.BloodTypeId equals bt.Id into btj
        from bt in btj.DefaultIfEmpty()
        select new ExamJoin
        {
            ExamId = e.Id,
            SwimmerId = e.SwimmerId,
            ExamDate = e.ExamDate,
            CreatedAt = e.CreatedAt,
            Hemoglobin = e.Hemoglobin,
            HeightCm = e.HeightCm,
            WeightKg = e.WeightKg,
            BloodTypeId = (Guid?)bt.Id,
            BloodTypeCode = bt.Code,
            BloodTypeNameEn = bt.NameEn,
            BloodTypeNameAr = bt.NameAr,
            InternalMedId = im.Id,
            InternalMedCode = im.Code,
            InternalMedNameEn = im.NameEn,
            InternalMedNameAr = im.NameAr,
            HeartAssessId = ha.Id,
            HeartAssessCode = ha.Code,
            HeartAssessNameEn = ha.NameEn,
            HeartAssessNameAr = ha.NameAr,
            SpineAssessId = sa.Id,
            SpineAssessCode = sa.Code,
            SpineAssessNameEn = sa.NameEn,
            SpineAssessNameAr = sa.NameAr,
        };

    private static System.Linq.Expressions.Expression<Func<ExamJoin, MedicalExamRow>> Project() =>
        x => new MedicalExamRow(
            x.ExamId,
            x.ExamDate, x.Hemoglobin, x.HeightCm, x.WeightKg,
            x.BloodTypeId,
            x.BloodTypeCode,
            x.BloodTypeNameEn,
            x.BloodTypeNameAr,
            x.InternalMedId, x.InternalMedCode, x.InternalMedNameEn, x.InternalMedNameAr,
            x.HeartAssessId, x.HeartAssessCode, x.HeartAssessNameEn, x.HeartAssessNameAr,
            x.SpineAssessId, x.SpineAssessCode, x.SpineAssessNameEn, x.SpineAssessNameAr);

    private sealed class ExamJoin
    {
        public Guid ExamId { get; init; }
        public Guid SwimmerId { get; init; }
        public DateOnly ExamDate { get; init; }
        public DateTime CreatedAt { get; init; }
        public decimal Hemoglobin { get; init; }
        public decimal HeightCm { get; init; }
        public decimal WeightKg { get; init; }
        public Guid? BloodTypeId { get; init; }
        public string? BloodTypeCode { get; init; }
        public string? BloodTypeNameEn { get; init; }
        public string? BloodTypeNameAr { get; init; }
        public Guid InternalMedId { get; init; }
        public string InternalMedCode { get; init; } = string.Empty;
        public string InternalMedNameEn { get; init; } = string.Empty;
        public string? InternalMedNameAr { get; init; }
        public Guid HeartAssessId { get; init; }
        public string HeartAssessCode { get; init; } = string.Empty;
        public string HeartAssessNameEn { get; init; } = string.Empty;
        public string? HeartAssessNameAr { get; init; }
        public Guid SpineAssessId { get; init; }
        public string SpineAssessCode { get; init; } = string.Empty;
        public string SpineAssessNameEn { get; init; } = string.Empty;
        public string? SpineAssessNameAr { get; init; }
    }
}
