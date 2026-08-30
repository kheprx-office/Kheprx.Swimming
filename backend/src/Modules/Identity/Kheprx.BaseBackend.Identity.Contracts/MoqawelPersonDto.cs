namespace Kheprx.BaseBackend.Identity.Contracts;

// Moqawel (contractor) person row exposed to other modules for the مقاولين list.
public sealed record MoqawelPersonDto(
    Guid Id, string FullName, string? Gender, int? Age, string? Phone, string Nid, bool IsActive);
