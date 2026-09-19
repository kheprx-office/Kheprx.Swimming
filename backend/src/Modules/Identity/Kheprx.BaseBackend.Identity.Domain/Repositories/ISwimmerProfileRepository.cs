using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.ReadModels;

namespace Kheprx.BaseBackend.Identity.Domain.Repositories;

public interface ISwimmerProfileRepository
{
    Task<int> CountAsync(CancellationToken ct = default);
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
    Task<IReadOnlyList<MedicalExamRow>> ListExamsAsync(Guid swimmerId, CancellationToken ct = default);
    Task<MedicalExam?> GetExamTrackedAsync(Guid examId, CancellationToken ct = default);
    void RemoveExam(MedicalExam exam);
}
