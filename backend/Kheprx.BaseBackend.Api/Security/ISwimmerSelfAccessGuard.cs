namespace Kheprx.BaseBackend.Api.Security;

/// <summary>Decides whether the caller may READ a given swimmer's data.
/// Coaches (head_coach/captain) may read anyone; a swimmer may read only their own record.</summary>
public interface ISwimmerSelfAccessGuard
{
    Task<bool> CanReadAsync(bool callerIsSwimmer, Guid callerUserId, Guid targetSwimmerId, CancellationToken ct);
}
