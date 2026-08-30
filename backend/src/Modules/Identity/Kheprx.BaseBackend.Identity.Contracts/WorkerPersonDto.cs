namespace Kheprx.BaseBackend.Identity.Contracts;

// Worker person row exposed to other modules for the labour list (wage is project-level now).
public sealed record WorkerPersonDto(
    Guid Id, string FullName, string? Gender, int? Age, string? Phone, string Nid, bool IsActive);
