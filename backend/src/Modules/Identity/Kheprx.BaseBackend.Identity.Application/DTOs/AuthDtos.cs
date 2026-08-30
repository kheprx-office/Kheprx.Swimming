namespace Kheprx.BaseBackend.Identity.Application.DTOs;

/// <summary>Credentials for POST /api/auth/login.</summary>
/// <param name="Email">Account email address.</param>
/// <param name="Password">Account password.</param>
public sealed record LoginRequest(string Email, string Password);

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
/// <param name="UserId">User identifier.</param>
/// <param name="Email">Account email address.</param>
/// <param name="FullName">Display name.</param>
/// <param name="Role">Role code (e.g. admin).</param>
/// <param name="Phone">Phone number; null when not set.</param>
/// <param name="Gender">Gender code (male/female); null when not set.</param>
/// <param name="Age">Age in years; null when not set.</param>
public sealed record CurrentUserDto(
    Guid UserId, string Email, string FullName, string Role,
    string? Phone, string? Gender, int? Age);

/// <summary>A selectable role.</summary>
/// <param name="Id">Role identifier.</param>
/// <param name="Code">Stable role code (e.g. admin).</param>
/// <param name="LabelAr">Arabic display label.</param>
/// <param name="LabelEn">English display label.</param>
/// <param name="SortOrder">Display ordering, ascending.</param>
public sealed record RoleDto(Guid Id, string Code, string? LabelAr, string? LabelEn, int SortOrder);
