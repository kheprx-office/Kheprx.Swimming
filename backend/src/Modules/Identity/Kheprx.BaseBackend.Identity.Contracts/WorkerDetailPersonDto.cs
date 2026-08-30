namespace Kheprx.BaseBackend.Identity.Contracts;

// Worker person row for the detail header (includes HireDate; wage/engagement are project-level).
public sealed record WorkerDetailPersonDto(
    Guid Id, string FullName, string? Gender, int? Age, string? Phone, string Nid, bool IsActive, DateOnly? HireDate);
