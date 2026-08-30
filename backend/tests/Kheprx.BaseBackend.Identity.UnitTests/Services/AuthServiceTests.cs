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
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IJwtTokenService> _jwt = new();
    private readonly Guid _roleId = Guid.NewGuid();

    private AuthService NewService() => new(
        _users.Object, _roles.Object, _tokens.Object, _hasher.Object, _jwt.Object,
        Options.Create(new JwtOptions { RefreshTokenDays = 7 }));

    private User ActiveUser(string pwHash = "stored", bool mustChange = false)
        => new("Alice", _roleId, "29801014501234", email: "a@b.com", passwordHash: pwHash, mustChangePassword: mustChange);

    [Fact]
    public async Task Login_unknown_email_returns_null()
    {
        _users.Setup(r => r.GetByEmailAsync("a@b.com", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        Assert.Null(await NewService().LoginAsync(new LoginRequest("a@b.com", "pw")));
    }

    [Fact]
    public async Task Login_wrong_password_returns_null()
    {
        _users.Setup(r => r.GetByEmailAsync("a@b.com", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveUser());
        _hasher.Setup(h => h.Verify("pw", "stored")).Returns(false);
        Assert.Null(await NewService().LoginAsync(new LoginRequest("a@b.com", "pw")));
    }

    [Fact]
    public async Task Login_inactive_user_returns_null()
    {
        var user = ActiveUser();
        user.Deactivate();
        _users.Setup(r => r.GetByEmailAsync("a@b.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        Assert.Null(await NewService().LoginAsync(new LoginRequest("a@b.com", "pw")));
    }

    [Fact]
    public async Task Login_user_without_password_returns_null() // AD-005 login-less
    {
        var user = new User("Bob", _roleId, "29801014501235", email: "a@b.com"); // PasswordHash null
        _users.Setup(r => r.GetByEmailAsync("a@b.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        Assert.Null(await NewService().LoginAsync(new LoginRequest("a@b.com", "pw")));
    }

    [Fact]
    public async Task Login_success_issues_tokens_records_login_stores_refresh_and_surfaces_flag()
    {
        var user = ActiveUser(mustChange: true);
        _users.Setup(r => r.GetByEmailAsync("a@b.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("pw", "stored")).Returns(true);
        _roles.Setup(r => r.GetByIdAsync(_roleId, It.IsAny<CancellationToken>())).ReturnsAsync(new Role("admin"));
        _jwt.Setup(j => j.CreateAccessToken(user, "admin")).Returns("access.jwt");
        _jwt.Setup(j => j.CreateRefreshToken()).Returns(("raw.refresh", "hash.refresh"));

        var session = await NewService().LoginAsync(new LoginRequest("a@b.com", "pw"));

        Assert.NotNull(session);
        Assert.Equal("access.jwt", session!.AccessToken);
        Assert.Equal("raw.refresh", session.RefreshToken);
        Assert.Equal("admin", session.Role);
        Assert.Equal(user.Id, session.UserId);
        Assert.True(session.MustChangePassword);
        Assert.NotNull(user.LastLoginAt);
        _tokens.Verify(t => t.AddAsync(It.Is<RefreshToken>(x => x.TokenHash == "hash.refresh"), It.IsAny<CancellationToken>()), Times.Once);
        _tokens.Verify(t => t.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_rotates_old_token_and_issues_new_pair()
    {
        var user = ActiveUser();
        var stored = new RefreshToken(user.Id, "old.hash", DateTime.UtcNow.AddDays(1));
        _jwt.Setup(j => j.HashRefreshToken("old.raw")).Returns("old.hash");
        _tokens.Setup(t => t.GetByHashAsync("old.hash", It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _roles.Setup(r => r.GetByIdAsync(_roleId, It.IsAny<CancellationToken>())).ReturnsAsync(new Role("admin"));
        _jwt.Setup(j => j.CreateAccessToken(user, "admin")).Returns("new.access");
        _jwt.Setup(j => j.CreateRefreshToken()).Returns(("new.raw", "new.hash"));

        var session = await NewService().RefreshAsync(new RefreshRequest("old.raw"));

        Assert.NotNull(session);
        Assert.Equal("new.raw", session!.RefreshToken);
        Assert.NotNull(stored.RevokedAt);
        Assert.Equal("new.hash", stored.ReplacedByTokenHash);
        _tokens.Verify(t => t.AddAsync(It.Is<RefreshToken>(x => x.TokenHash == "new.hash" && x.ExpiresAt == stored.ExpiresAt), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_unknown_token_returns_null()
    {
        _jwt.Setup(j => j.HashRefreshToken("x")).Returns("xh");
        _tokens.Setup(t => t.GetByHashAsync("xh", It.IsAny<CancellationToken>())).ReturnsAsync((RefreshToken?)null);
        Assert.Null(await NewService().RefreshAsync(new RefreshRequest("x")));
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
    public async Task Refresh_reuse_of_revoked_token_revokes_all_and_returns_null() // AD-002
    {
        var userId = Guid.NewGuid();
        var stored = new RefreshToken(userId, "old.hash", DateTime.UtcNow.AddDays(1));
        stored.Revoke("rotated.hash");
        _jwt.Setup(j => j.HashRefreshToken("old.raw")).Returns("old.hash");
        _tokens.Setup(t => t.GetByHashAsync("old.hash", It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        Assert.Null(await NewService().RefreshAsync(new RefreshRequest("old.raw")));

        _tokens.Verify(t => t.RevokeAllForUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _tokens.Verify(t => t.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task ChangePassword_wrong_current_returns_null()
    {
        var user = ActiveUser();
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("wrong", "stored")).Returns(false);
        Assert.Null(await NewService().ChangePasswordAsync(user.Id, new ChangePasswordRequest("wrong", "newpass8")));
    }

    [Fact]
    public async Task ChangePassword_success_sets_hash_revokes_all_reissues_and_clears_flag() // AD-003/AD-007
    {
        var user = ActiveUser(mustChange: true);
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("current", "stored")).Returns(true);
        _hasher.Setup(h => h.Hash("newpass8")).Returns("new.hash");
        _roles.Setup(r => r.GetByIdAsync(_roleId, It.IsAny<CancellationToken>())).ReturnsAsync(new Role("admin"));
        _jwt.Setup(j => j.CreateAccessToken(user, "admin")).Returns("access");
        _jwt.Setup(j => j.CreateRefreshToken()).Returns(("raw", "hash"));

        var session = await NewService().ChangePasswordAsync(user.Id, new ChangePasswordRequest("current", "newpass8"));

        Assert.NotNull(session);
        Assert.False(session!.MustChangePassword);
        Assert.Equal("new.hash", user.PasswordHash);
        Assert.False(user.MustChangePassword);
        _tokens.Verify(t => t.RevokeAllForUserAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
        _tokens.Verify(t => t.AddAsync(It.Is<RefreshToken>(x => x.TokenHash == "hash"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCurrentUser_returns_dto_with_role_code()
    {
        var user = ActiveUser();
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _roles.Setup(r => r.GetByIdAsync(_roleId, It.IsAny<CancellationToken>())).ReturnsAsync(new Role("admin"));

        var dto = await NewService().GetCurrentUserAsync(user.Id);

        Assert.NotNull(dto);
        Assert.Equal("a@b.com", dto!.Email);
        Assert.Equal("admin", dto.Role);
        Assert.Null(dto.Phone);
        Assert.Null(dto.Gender);
        Assert.Null(dto.Age);
    }

    [Fact]
    public async Task GetCurrentUser_returns_person_fields_when_present()
    {
        var user = new User("Alice", _roleId, "29801014501234", email: "a@b.com", passwordHash: "stored",
            phone: "01000000000", gender: "male", age: 30);
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _roles.Setup(r => r.GetByIdAsync(_roleId, It.IsAny<CancellationToken>())).ReturnsAsync(new Role("admin"));

        var dto = await NewService().GetCurrentUserAsync(user.Id);

        Assert.NotNull(dto);
        Assert.Equal("01000000000", dto!.Phone);
        Assert.Equal("male", dto.Gender);
        Assert.Equal(30, dto.Age);
    }

    [Fact]
    public async Task GetCurrentUser_unknown_returns_null()
    {
        _users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        Assert.Null(await NewService().GetCurrentUserAsync(Guid.NewGuid()));
    }
}
