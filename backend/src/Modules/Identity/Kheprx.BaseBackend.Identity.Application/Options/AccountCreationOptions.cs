namespace Kheprx.BaseBackend.Identity.Application.Options;

/// <summary>Bound from the "AccountCreation" config section — the generic password assigned to
/// every new swimmer/coach on registration (they must change it on first login).</summary>
public sealed class AccountCreationOptions
{
    public const string SectionName = "AccountCreation";
    public string GenericPassword { get; set; } = string.Empty;
}
