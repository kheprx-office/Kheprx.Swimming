using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;

namespace Kheprx.BaseBackend.Api.Security;

public sealed class SwimmerSelfAccessGuard : ISwimmerSelfAccessGuard
{
    private readonly ISwimmerService _swimmers;
    public SwimmerSelfAccessGuard(ISwimmerService swimmers) => _swimmers = swimmers;

    public async Task<bool> CanReadAsync(bool callerIsSwimmer, Guid callerUserId, Guid targetSwimmerId, CancellationToken ct)
    {
        if (!callerIsSwimmer) return true;                       // coaches unrestricted
        var mine = await _swimmers.GetSwimmerIdByUserAsync(callerUserId, ct);
        return mine is { } id && id == targetSwimmerId;         // swimmer: own record only (fail-closed)
    }
}
