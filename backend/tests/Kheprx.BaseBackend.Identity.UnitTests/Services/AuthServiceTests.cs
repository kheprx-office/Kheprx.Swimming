using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRoleRepository> _roles = new();
    private readonly Mock<IRefreshTokenRepository> _tokens = new();
    private readonly Mock<ICoachProfileRepository> _profiles = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IJwtTokenService> _jwt = new();
    private readonly Guid _roleId = Guid.NewGuid();

    private AuthService NewService() => new(
        _users.Object, _roles.Object, _tokens.Object, _profiles.Object,
        _hasher.Object, _jwt.Object, Options.Create(new JwtOptions { RefreshTokenDays = 7 }));

    private AppUser User(string pwHash = "stored", bool firstLogin = false)
        => new("captain.dave", "Dave", _roleId, email: "a@b.com", passwordHash: pwHash, isFirstLogin: firstLogin);

    [Fact]
    public async Task Login_unknown_email_returns_invalid_credentials()
    {
        _users.Setup(r => r.GetByEmailAsync("a@b.com", It.IsAny<CancellationToken>())).ReturnsAsync((AppUser?)null);
        var result = await NewService().LoginAsync(new LoginRequest("a@b.com", "pw", "captain"));
        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        Assert.Null(result.Session);
    }

    [Fact]
    public async Task Login_wrong_password_returns_invalid_credentials()
    {
        _users.Setup(r => r.GetByEmailAsync("a@b.com", It.IsAny<CancellationToken>())).ReturnsAsync(User());
        _hasher.Setup(h => h.Verify("pw", "stored")).Returns(false);
        var result = await NewService().LoginAsync(new LoginRequest("a@b.com", "pw", "captain"));
        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        Assert.Null(result.Session);
    }

    [Fact]
    public async Task Login_without_password_returns_invalid_credentials()
    {
        var u = new AppUser("u", "n", _roleId, email: "a@b.com"); // PasswordHash null
        _users.Setup(r => r.GetByEmailAsync("a@b.com", It.IsAny<CancellationToken>())).ReturnsAsync(u);
        var result = await NewService().LoginAsync(new LoginRequest("a@b.com", "pw", "captain"));
        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        Assert.Null(result.Session);
    }

    [Fact]
    public async Task Login_role_mismatch_returns_role_mismatch_and_issues_no_session()
    {
        var u = User(); // real role resolves to "captain" below
        _users.Setup(r => r.GetByEmailAsync("a@b.com", It.IsAny<CancellationToken>())).ReturnsAsync(u);
        _hasher.Setup(h => h.Verify("pw", "stored")).Returns(true);
        _roles.Setup(r => r.GetByIdAsync(_roleId, It.IsAny<CancellationToken>())).ReturnsAsync(new Role("captain", "Captain"));

        var result = await NewService().LoginAsync(new LoginRequest("a@b.com", "pw", "head_coach"));

        Assert.Equal(LoginStatus.RoleMismatch, result.Status);
        Assert.Null(result.Session);
        _tokens.Verify(t => t.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Login_success_issues_session_with_first_login_flag()
    {
        var u = User(firstLogin: true);
        _users.Setup(r => r.GetByEmailAsync("a@b.com", It.IsAny<CancellationToken>())).ReturnsAsync(u);
        _hasher.Setup(h => h.Verify("pw", "stored")).Returns(true);
        _roles.Setup(r => r.GetByIdAsync(_roleId, It.IsAny<CancellationToken>())).ReturnsAsync(new Role("captain", "Captain"));
        _jwt.Setup(j => j.CreateAccessToken(u, "captain")).Returns("access");
        _jwt.Setup(j => j.CreateRefreshToken()).Returns(("raw", "hash"));

        var result = await NewService().LoginAsync(new LoginRequest("a@b.com", "pw", "captain"));

        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.NotNull(result.Session);
        Assert.Equal("captain", result.Session!.Role);
        Assert.True(result.Session.MustChangePassword);
        _tokens.Verify(t => t.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Me_returns_profile_with_national_id()
    {
        var u = User();
        _users.Setup(r => r.GetByIdAsync(u.Id, It.IsAny<CancellationToken>())).ReturnsAsync(u);
        _roles.Setup(r => r.GetByIdAsync(_roleId, It.IsAny<CancellationToken>())).ReturnsAsync(new Role("head_coach", "Head Coach"));
        _profiles.Setup(p => p.GetNationalIdAsync(u.Id, "head_coach", It.IsAny<CancellationToken>())).ReturnsAsync("29001011234567");
        _users.Setup(r => r.GetGenderCodeAsync(u.GenderId, It.IsAny<CancellationToken>())).ReturnsAsync("male");

        var me = await NewService().GetCurrentUserAsync(u.Id);

        Assert.Equal("29001011234567", me!.NationalId);
        Assert.Equal("head_coach", me.Role);
        Assert.Equal("Dave", me.NameEn);
        Assert.Equal("male", me.Gender);
    }

    [Fact]
    public async Task ChangePassword_wrong_current_returns_null()
    {
        var u = User();
        _users.Setup(r => r.GetByIdAsync(u.Id, It.IsAny<CancellationToken>())).ReturnsAsync(u);
        _hasher.Setup(h => h.Verify("bad", "stored")).Returns(false);
        Assert.Null(await NewService().ChangePasswordAsync(u.Id, new ChangePasswordRequest("bad", "new")));
    }

    // ── Restored security-invariant tests ──────────────────────────────────

    [Fact]
    public async Task Refresh_rotates_old_token_and_issues_new_pair()
    {
        var u = User();
        var stored = new RefreshToken(u.Id, "old.hash", DateTime.UtcNow.AddDays(1));
        _jwt.Setup(j => j.HashRefreshToken("old.raw")).Returns("old.hash");
        _tokens.Setup(t => t.GetByHashAsync("old.hash", It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        _users.Setup(r => r.GetByIdAsync(u.Id, It.IsAny<CancellationToken>())).ReturnsAsync(u);
        _roles.Setup(r => r.GetByIdAsync(_roleId, It.IsAny<CancellationToken>())).ReturnsAsync(new Role("captain", "Captain"));
        _jwt.Setup(j => j.CreateAccessToken(u, "captain")).Returns("new.access");
        _jwt.Setup(j => j.CreateRefreshToken()).Returns(("new.raw", "new.hash"));

        var session = await NewService().RefreshAsync(new RefreshRequest("old.raw"));

        Assert.NotNull(session);
        Assert.Equal("new.raw", session!.RefreshToken);
        _tokens.Verify(t => t.AddAsync(
            It.Is<RefreshToken>(x => x.TokenHash == "new.hash" && x.ExpiresAt == stored.ExpiresAt),
            It.IsAny<CancellationToken>()), Times.Once);
        _tokens.Verify(t => t.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_reuse_of_revoked_token_revokes_all_and_returns_null() // AD-002
    {
        var userId = Guid.NewGuid();
        var stored = new RefreshToken(userId, "old.hash", DateTime.UtcNow.AddDays(1));
        stored.Revoke("rotated.hash");
        _jwt.Setup(j => j.HashRefreshToken("old.raw")).Returns("old.hash");
        _tokens.Setup(t => t.GetByHashAsync("old.hash", It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        var result = await NewService().RefreshAsync(new RefreshRequest("old.raw"));

        Assert.Null(result);
        _tokens.Verify(t => t.RevokeAllForUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _tokens.Verify(t => t.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_expired_token_returns_null()
    {
        var stored = new RefreshToken(Guid.NewGuid(), "old.hash", DateTime.UtcNow.AddSeconds(-1));
        _jwt.Setup(j => j.HashRefreshToken("old.raw")).Returns("old.hash");
        _tokens.Setup(t => t.GetByHashAsync("old.hash", It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        Assert.Null(await NewService().RefreshAsync(new RefreshRequest("old.raw")));
    }

    [Fact]
    public async Task Logout_revokes_all_user_tokens() // AD-009
    {
        var userId = Guid.NewGuid();

        await NewService().LogoutAsync(userId);

        _tokens.Verify(t => t.RevokeAllForUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _tokens.Verify(t => t.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePassword_success_sets_hash_revokes_all_reissues_and_clears_flag() // AD-003/AD-007
    {
        var u = User(firstLogin: true);
        _users.Setup(r => r.GetByIdAsync(u.Id, It.IsAny<CancellationToken>())).ReturnsAsync(u);
        _hasher.Setup(h => h.Verify("current", "stored")).Returns(true);
        _hasher.Setup(h => h.Hash("newpass8")).Returns("new.hash");
        _roles.Setup(r => r.GetByIdAsync(_roleId, It.IsAny<CancellationToken>())).ReturnsAsync(new Role("captain", "Captain"));
        _jwt.Setup(j => j.CreateAccessToken(u, "captain")).Returns("access");
        _jwt.Setup(j => j.CreateRefreshToken()).Returns(("raw", "hash"));

        var session = await NewService().ChangePasswordAsync(u.Id, new ChangePasswordRequest("current", "newpass8"));

        Assert.NotNull(session);
        Assert.False(session!.MustChangePassword);
        _tokens.Verify(t => t.RevokeAllForUserAsync(u.Id, It.IsAny<CancellationToken>()), Times.Once);
        _tokens.Verify(t => t.AddAsync(
            It.Is<RefreshToken>(x => x.TokenHash == "hash"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
