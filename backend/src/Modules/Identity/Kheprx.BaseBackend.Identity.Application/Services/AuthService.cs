using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Kheprx.BaseBackend.Identity.Application.Services;

internal sealed class AuthService : IAuthService
{
    #region Fields

    // Defensive fallback only; RoleId is a required FK to an existing role, so this never triggers in practice.
    private const string FallbackRole = "worker";

    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IRefreshTokenRepository _tokens;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly JwtOptions _options;

    #endregion

    #region Constructor

    public AuthService(
        IUserRepository users, IRoleRepository roles, IRefreshTokenRepository tokens,
        IPasswordHasher hasher, IJwtTokenService jwt, IOptions<JwtOptions> options)
    {
        _users = users;
        _roles = roles;
        _tokens = tokens;
        _hasher = hasher;
        _jwt = jwt;
        _options = options.Value;
    }

    #endregion

    #region APIs

    #region LoginAsync — verify credentials, issue session

    // About "CancellationToken ct = default" (used on every method here):
    // ct is a stop signal. ASP.NET creates one per HTTP request and the controller
    // passes it down (controller -> service -> repository -> EF Core), so if the
    // client disconnects, the database work stops early instead of running for
    // nothing. "= default" makes it optional: callers that don't pass one (e.g.
    // unit tests) get a token that never cancels.
    public async Task<SessionDto?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        // Find the account by the email typed in the login form.
        var user = await _users.GetByEmailAsync(request.Email, ct);

        // Say "no" for three different reasons with the SAME null (-> one identical 401):
        // unknown email, deactivated account, or an account with no password at all
        // (AD-005 NID-only accounts). Callers can't tell which check failed, so
        // registered emails can't be probed.
        if (user is null || !user.IsActive || user.PasswordHash is null) return null;

        // Never compare raw passwords: re-hash the typed password (PBKDF2) and compare
        // against the stored hash in fixed time. Wrong password -> the same null/401.
        if (!_hasher.Verify(request.Password, user.PasswordHash)) return null;

        // Credentials proven: stamp LastLoginAt for history and save it.
        // RecordLogin() is a method ON the User entity - the object updates its own
        // properties, so nothing needs to be passed in. SaveChangesAsync needs no
        // argument either: EF Core has been tracking "user" since GetByEmailAsync
        // loaded it, and on save it compares every tracked object against its
        // original values and writes only what changed (UPDATE ... WHERE id = ...).
        // Why is "user" tracked? GetByEmailAsync queries _db.Users WITHOUT
        // .AsNoTracking() - see the comment on UserRepository.GetByEmailAsync.
        user.RecordLogin();
        await _users.SaveChangesAsync(ct);

        // Issue the pair: 15-min access JWT + 7-day refresh token (only its SHA-256
        // hash is stored) and return them with mustChangePassword for the client.
        return await IssueSessionAsync(user, ct);
    }

    #endregion

    #region RefreshAsync — rotate refresh token, reissue pair

    public async Task<SessionDto?> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        // We never store raw refresh tokens - hash the incoming one and look up the hash.
        var hash = _jwt.HashRefreshToken(request.RefreshToken);
        var stored = await _tokens.GetByHashAsync(hash, ct);

        // Unknown token -> plain "no" (401 INVALID_REFRESH_TOKEN).
        if (stored is null) return null;

        // The token exists but was already used/revoked: someone is replaying an old
        // token (a theft signal). Kill EVERY session for that user, not just this one.
        if (stored.RevokedAt is not null) // AD-002: replay of a revoked/rotated token => revoke everything
        {
            await _tokens.RevokeAllForUserAsync(stored.UserId, ct);
            await _tokens.SaveChangesAsync(ct);
            return null;
        }

        // Past its 7-day life -> plain "no"; the user must log in again.
        if (stored.ExpiresAt <= DateTime.UtcNow) return null;

        // The token's owner must still exist and still be active.
        var user = await _users.GetByIdAsync(stored.UserId, ct);
        if (user is null || !user.IsActive) return null;

        // Rotation: mint a fresh access JWT and a brand-new refresh token, then mark
        // the old token as replaced by the new one (that is what Revoke(newHash) does).
        var (roleCode, access) = await IssueAccessAsync(user, ct);
        var (rawRefresh, refreshHash) = _jwt.CreateRefreshToken();
        stored.Revoke(refreshHash);

        // Key detail (AD-010): the new token KEEPS the old expiry date - refreshing
        // never extends the 7-day window, so a stolen token cannot live forever.
        await _tokens.AddAsync(
            new RefreshToken(user.Id, refreshHash, stored.ExpiresAt), ct);
        await _tokens.SaveChangesAsync(ct);

        return new SessionDto(access, rawRefresh, roleCode, user.Id, user.MustChangePassword);
    }

    #endregion

    #region LogoutAsync — revoke all refresh tokens

    // Logout = revoke-all by design: mark EVERY active refresh token of this user as
    // revoked, so signing out on one device signs out all devices. The access token
    // cannot be recalled - it simply dies on its own within 15 minutes. Note that
    // userId comes from the JWT's "sub" claim (the controller reads it); we never
    // trust a userId sent in the request body.
    public async Task LogoutAsync(Guid userId, CancellationToken ct = default) // AD-009
    {
        await _tokens.RevokeAllForUserAsync(userId, ct);
        await _tokens.SaveChangesAsync(ct);
    }

    #endregion

    #region ChangePasswordAsync — verify, set new password, reissue session

    public async Task<SessionDto?> ChangePasswordAsync(
        Guid userId, ChangePasswordRequest request, CancellationToken ct = default) // AD-003/AD-007
    {
        // Load the caller's own account (userId comes from the JWT, not the body).
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive || user.PasswordHash is null) return null;

        // Prove they know the CURRENT password before allowing a change - a stolen
        // access token alone must not be enough. Wrong -> 401 INVALID_CREDENTIALS.
        if (!_hasher.Verify(request.CurrentPassword, user.PasswordHash)) return null;

        // Store the new password as a PBKDF2 hash; SetPassword also clears the
        // MustChangePassword flag (the forced first-login change, AD-007).
        user.SetPassword(_hasher.Hash(request.NewPassword)); // clears MustChangePassword
        await _users.SaveChangesAsync(ct);

        // Kill every existing session (AD-003): if the password was changed because
        // of a suspected leak, any stolen refresh token dies right here...
        await _tokens.RevokeAllForUserAsync(user.Id, ct); // revoke all sessions...
        // ...then hand a brand-new pair to the device that made the change, so the
        // user stays signed in where they are (and only there).
        return await IssueSessionAsync(user, ct);          // ...then reissue a pair for the caller
    }

    #endregion

    #region GetCurrentUserAsync — load current user with role

    public async Task<CurrentUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null) return null;
        var role = await _roles.GetByIdAsync(user.RoleId, ct);
        return new CurrentUserDto(
            user.Id, user.Email ?? string.Empty, user.FullName, role?.Code ?? FallbackRole,
            user.Phone, user.Gender, user.Age);
    }

    #endregion

    #endregion

    #region Helpers

    private async Task<SessionDto> IssueSessionAsync(User user, CancellationToken ct)
    {
        var (roleCode, access) = await IssueAccessAsync(user, ct);
        var (rawRefresh, refreshHash) = _jwt.CreateRefreshToken();
        await _tokens.AddAsync(
            new RefreshToken(user.Id, refreshHash, DateTime.UtcNow.AddDays(_options.RefreshTokenDays)), ct);
        await _tokens.SaveChangesAsync(ct);
        return new SessionDto(access, rawRefresh, roleCode, user.Id, user.MustChangePassword);
    }

    private async Task<(string roleCode, string accessToken)> IssueAccessAsync(User user, CancellationToken ct)
    {
        var role = await _roles.GetByIdAsync(user.RoleId, ct);
        var roleCode = role?.Code ?? FallbackRole;
        return (roleCode, _jwt.CreateAccessToken(user, roleCode));
    }

    #endregion
}
