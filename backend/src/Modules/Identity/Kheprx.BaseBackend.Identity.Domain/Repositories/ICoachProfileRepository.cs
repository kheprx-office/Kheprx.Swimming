using Kheprx.BaseBackend.Identity.Domain.Entities;

namespace Kheprx.BaseBackend.Identity.Domain.Repositories;

public interface ICoachProfileRepository
{
    // Returns the national_id of the user's profile subtype (head_coach_profile or
    // captain_profile) selected by roleCode, or null when no matching profile row exists.
    Task<string?> GetNationalIdAsync(Guid userId, string roleCode, CancellationToken ct = default);
    Task AddHeadCoachAsync(HeadCoachProfile profile, CancellationToken ct = default);
    Task AddCaptainAsync(CaptainProfile profile, CancellationToken ct = default);
    Task<bool> NationalIdExistsAsync(string nationalId, CancellationToken ct = default);
    Task<bool> SaveChangesAsync(CancellationToken ct = default);
}
