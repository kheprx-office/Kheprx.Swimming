namespace Kheprx.BaseBackend.Identity.Domain.ReadModels;

// Single worker + person row for the worker-detail header. Id is the person's users.Id.
public sealed record WorkerDetailRow(
    Guid Id, string FullName, string? Gender, int? Age, string? Phone, string Nid, bool IsActive, DateOnly? HireDate);
