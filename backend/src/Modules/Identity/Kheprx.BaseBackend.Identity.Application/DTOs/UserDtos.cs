namespace Kheprx.BaseBackend.Identity.Application.DTOs;

/// <summary>A managed user account.</summary>
/// <param name="Id">User identifier.</param>
/// <param name="Username">Login username.</param>
/// <param name="NameEn">English display name.</param>
/// <param name="NameAr">Arabic display name; null when not set.</param>
/// <param name="Email">Account email address; null for login-less accounts.</param>
/// <param name="Phone">Phone number; null when not set.</param>
/// <param name="GenderId">Gender lookup identifier; null when not set.</param>
/// <param name="Dob">Date of birth; null when not set.</param>
/// <param name="Role">Role code (e.g. captain).</param>
public sealed record UserDto(
    Guid Id, string Username, string NameEn, string? NameAr, string? Email,
    string? Phone, Guid? GenderId, DateOnly? Dob, string Role);

/// <summary>Payload for POST /api/users.</summary>
/// <param name="Username">Login username.</param>
/// <param name="NameEn">English display name.</param>
/// <param name="Role">Role code; determines which profile fields apply.</param>
/// <param name="NameAr">Arabic display name.</param>
/// <param name="Email">Email address; optional for login-less accounts.</param>
/// <param name="Password">Initial password; server may require a change at first sign-in.</param>
/// <param name="Phone">Phone number.</param>
/// <param name="GenderId">Gender lookup identifier.</param>
/// <param name="Dob">Date of birth.</param>
public sealed record CreateUserRequest(
    string Username, string NameEn, string Role,
    string? NameAr, string? Email, string? Password, string? Phone, Guid? GenderId, DateOnly? Dob);

/// <summary>Payload for PATCH /api/users/{id}.</summary>
/// <param name="NameEn">English display name.</param>
/// <param name="Role">Role code.</param>
/// <param name="NameAr">Arabic display name.</param>
/// <param name="Email">Email address.</param>
/// <param name="Password">New password; omit to keep the current one.</param>
/// <param name="Phone">Phone number.</param>
/// <param name="GenderId">Gender lookup identifier.</param>
/// <param name="Dob">Date of birth.</param>
public sealed record UpdateUserRequest(
    string NameEn, string Role, string? NameAr, string? Email,
    string? Password, string? Phone, Guid? GenderId, DateOnly? Dob);

/// <summary>A user's display names, for resolving author/actor ids to names.</summary>
public sealed record UserNameDto(Guid Id, string NameEn, string? NameAr);
