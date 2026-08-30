namespace Kheprx.BaseBackend.Identity.Domain.ReadModels;

// Manager id + display name (from users.FullName) for cross-module name resolution.
public sealed record ManagerNameRow(Guid Id, string FullName);
