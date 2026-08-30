using Kheprx.BaseBackend.Identity.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class RefreshTokenTests
{
    [Fact]
    public void New_token_is_active()
        => Assert.True(new RefreshToken(Guid.NewGuid(), "hash", DateTime.UtcNow.AddDays(7)).IsActive);

    [Fact]
    public void Expired_token_is_not_active()
        => Assert.False(new RefreshToken(Guid.NewGuid(), "hash", DateTime.UtcNow.AddSeconds(-1)).IsActive);

    [Fact]
    public void Revoke_sets_revoked_and_replacement_and_deactivates()
    {
        var t = new RefreshToken(Guid.NewGuid(), "hash", DateTime.UtcNow.AddDays(7));
        t.Revoke("newhash");
        Assert.NotNull(t.RevokedAt);
        Assert.Equal("newhash", t.ReplacedByTokenHash);
        Assert.False(t.IsActive);
    }

    [Fact]
    public void Revoke_is_idempotent()
    {
        var t = new RefreshToken(Guid.NewGuid(), "hash", DateTime.UtcNow.AddDays(7));
        t.Revoke("first");
        t.Revoke("second");
        Assert.Equal("first", t.ReplacedByTokenHash);
    }
}
