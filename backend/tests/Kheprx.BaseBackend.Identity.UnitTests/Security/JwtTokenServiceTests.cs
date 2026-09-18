using System.IdentityModel.Tokens.Jwt;
using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Security;

public class JwtTokenServiceTests
{
    private static JwtTokenService NewService() => new(Options.Create(new JwtOptions
    {
        Issuer = "kheprx",
        Audience = "kheprx-clients",
        SigningKey = "test-signing-key-at-least-32-bytes-long!!",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 7
    }));

    [Fact]
    public void Access_token_carries_sub_email_role_and_future_expiry()
    {
        var user = new AppUser("alice", "Alice", Guid.NewGuid(), email: "a@b.com", passwordHash: "h");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(NewService().CreateAccessToken(user, "admin"));

        Assert.Equal(user.Id.ToString(), jwt.Claims.First(c => c.Type == "sub").Value);
        Assert.Equal("a@b.com", jwt.Claims.First(c => c.Type == "email").Value);
        Assert.Equal("admin", jwt.Claims.First(c => c.Type == "role").Value);
        Assert.True(jwt.ValidTo > DateTime.UtcNow);
    }

    [Fact]
    public void Refresh_token_hash_matches_independent_hashing()
    {
        var svc = NewService();
        var (token, hash) = svc.CreateRefreshToken();
        Assert.Equal(hash, svc.HashRefreshToken(token));
        Assert.NotEqual(token, hash);
    }
}
