using Kheprx.BaseBackend.Health.Domain.Entities;

namespace Kheprx.BaseBackend.Health.Domain.Repositories;

public interface IMedicalTestRepository
{
    Task<IReadOnlyList<MedicalTest>> ListAsync(CancellationToken ct = default);
    Task AddAsync(MedicalTest test, CancellationToken ct = default);
    Task<MedicalTest?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task RemoveAsync(MedicalTest test, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
