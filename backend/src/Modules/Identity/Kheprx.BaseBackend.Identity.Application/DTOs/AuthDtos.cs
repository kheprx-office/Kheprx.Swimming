namespace Kheprx.BaseBackend.Identity.Application.DTOs;

/// <summary>Credentials for POST /api/auth/login.</summary>
/// <param name="Email">Account email address.</param>
/// <param name="Password">Account password.</param>
/// <param name="Role">Role code the user selected at login; must match the account's actual role.</param>
public sealed record LoginRequest(string Email, string Password, string Role);

/// <summary>Token exchange payload for POST /api/auth/refresh.</summary>
/// <param name="RefreshToken">The refresh token issued with the last session.</param>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Payload for POST /api/auth/change-password.</summary>
/// <param name="CurrentPassword">The password being replaced.</param>
/// <param name="NewPassword">The new password to set.</param>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>An authenticated session: token pair plus signed-in user essentials.</summary>
/// <param name="AccessToken">JWT for the Authorization: Bearer header.</param>
/// <param name="RefreshToken">Token used to obtain a new pair via POST /api/auth/refresh.</param>
/// <param name="Role">Role code of the signed-in user (e.g. admin).</param>
/// <param name="UserId">Identifier of the signed-in user.</param>
/// <param name="MustChangePassword">True when the user must change their password before continuing.</param>
public sealed record SessionDto(string AccessToken, string RefreshToken, string Role, Guid UserId, bool MustChangePassword);

/// <summary>Profile of the currently authenticated user (GET /api/auth/me).</summary>
public sealed record CurrentUserDto(
    Guid UserId, string Email, string NameEn, string? NameAr, string Role,
    string? Phone, string? Gender, int? Age, string? NationalId);

/// <summary>A selectable role.</summary>
public sealed record RoleDto(Guid Id, string Code, string NameEn, string? NameAr);

/// <summary>Outcome of a login attempt.</summary>
public enum LoginStatus
{
    /// <summary>Credentials verified and the selected role matched the account.</summary>
    Success,
    /// <summary>Unknown email, no password set, or wrong password.</summary>
    InvalidCredentials,
    /// <summary>Credentials were valid but the selected role does not match the account's role.</summary>
    RoleMismatch
}

/// <summary>Result of <see cref="Services.Interfaces.IAuthService.LoginAsync"/>: a status plus the session when successful.</summary>
/// <param name="Status">Which outcome occurred.</param>
/// <param name="Session">The issued session when <see cref="Status"/> is <see cref="LoginStatus.Success"/>; otherwise null.</param>
public sealed record LoginResult(LoginStatus Status, SessionDto? Session)
{
    public static LoginResult Success(SessionDto session) => new(LoginStatus.Success, session);
    public static LoginResult InvalidCredentials() => new(LoginStatus.InvalidCredentials, null);
    public static LoginResult RoleMismatch() => new(LoginStatus.RoleMismatch, null);
}
