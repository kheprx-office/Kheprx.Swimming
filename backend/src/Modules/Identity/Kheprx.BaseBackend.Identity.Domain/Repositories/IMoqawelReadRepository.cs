using Kheprx.BaseBackend.Identity.Domain.ReadModels;

namespace Kheprx.BaseBackend.Identity.Domain.Repositories;

public interface IMoqawelReadRepository
{
    // Returns moqawel rows for the given moqawel ids (= users.Id), filtered by an optional
    // name/NID search and active flag. Empty input → empty result (no query).
    Task<IReadOnlyList<MoqawelListRow>> ListAsync(
        IReadOnlyList<Guid> moqawelIds, string? search, bool? isActive, CancellationToken ct = default);

    // All moqawel-users (moqaweleen ⨝ users) → rows whose Id is users.Id, ordered by name.
    // Optional active filter. Feeds the create-project contractors picker.
    Task<IReadOnlyList<MoqawelListRow>> ListAllAsync(bool? isActive, CancellationToken ct = default);
}
