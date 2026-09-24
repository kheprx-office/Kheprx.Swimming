using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.ReadModels;

namespace Kheprx.BaseBackend.Identity.Domain.Repositories;

public interface ISwimmerProfileRepository
{
    Task<int> CountAsync(CancellationToken ct = default);
    Task<int> CountCreatedSinceAsync(DateTime sinceUtc, CancellationToken ct = default);
    Task<int> GetMaxUidNumberAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SwimmerListRow>> ListAsync(string? search = null, CancellationToken ct = default);
    Task AddAsync(SwimmerProfile profile, CancellationToken ct = default);
    Task AddSpecializationsAsync(IEnumerable<SwimmerSpecialization> specializations, CancellationToken ct = default);
    Task<bool> SaveChangesAsync(CancellationToken ct = default);  // true = saved; false = unique-index conflict
    Task AddExamAsync(MedicalExam exam, CancellationToken ct = default);
    Task<MedicalExamRow?> GetLatestExamAsync(Guid swimmerId, CancellationToken ct = default);
    Task<MedicalExamRow?> GetExamRowByIdAsync(Guid examId, CancellationToken ct = default);
    Task<SwimmerProfileRow?> GetProfileByIdAsync(Guid id, CancellationToken ct = default);
    Task<SwimmerProfile?> GetByIdTrackedAsync(Guid id, CancellationToken ct = default);
    Task<SwimmerProfile?> GetByUserIdTrackedAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<MedicalExamRow>> ListExamsAsync(Guid swimmerId, CancellationToken ct = default);
    Task<MedicalExam?> GetExamTrackedAsync(Guid examId, CancellationToken ct = default);
    void RemoveExam(MedicalExam exam);
    Task<IReadOnlyList<GuardianRow>> ListGuardiansAsync(Guid swimmerId, CancellationToken ct = default);
    Task<Guid?> GetGuardianRelationIdByCodeAsync(string code, CancellationToken ct = default);
    Task<Guardian?> GetGuardianTrackedAsync(Guid swimmerId, Guid relationId, CancellationToken ct = default);
    Task AddGuardianAsync(Guardian guardian, CancellationToken ct = default);
    Task<BodyMeasurementRow?> GetLatestBodyMeasurementAsync(Guid swimmerId, CancellationToken ct = default);
    Task AddBodyMeasurementAsync(BodyMeasurement measurement, CancellationToken ct = default);
    Task<IReadOnlyList<StrokeCountRow>> GetStrokeCountsAsync(CancellationToken ct = default);
}
