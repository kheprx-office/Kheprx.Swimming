namespace Kheprx.BaseBackend.Identity.Domain.ReadModels;

// Flattened moqawel + person row for the مقاولين-list read (wage is project-level).
public sealed record MoqawelListRow(
    Guid Id, string FullName, string? Gender, int? Age, string? Phone, string Nid, bool IsActive);
