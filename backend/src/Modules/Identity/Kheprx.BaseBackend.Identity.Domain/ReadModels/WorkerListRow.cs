namespace Kheprx.BaseBackend.Identity.Domain.ReadModels;

// Flattened worker + person row for the workers-list read (wage + engagement are project-level now).
public sealed record WorkerListRow(
    Guid Id, string FullName, string? Gender, int? Age, string? Phone, string Nid, bool IsActive);
