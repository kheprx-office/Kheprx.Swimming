namespace Kheprx.BaseBackend.Identity.Application.DTOs;

/// <summary>Role-dependent employment profile attached to a user.</summary>
/// <param name="MonthlySalary">Monthly salary, for salaried roles.</param>
/// <param name="DailyWage">Daily wage, for waged roles.</param>
/// <param name="HireDate">Date the user was hired.</param>
public sealed record UserProfileDto(decimal? MonthlySalary, decimal? DailyWage, DateOnly? HireDate);

/// <summary>A managed user account.</summary>
/// <param name="Id">User identifier.</param>
/// <param name="Code">Human-readable user code.</param>
/// <param name="FullName">Display name.</param>
/// <param name="Email">Account email address; null when the user signs in by NID only.</param>
/// <param name="Phone">Phone number.</param>
/// <param name="Gender">Gender code.</param>
/// <param name="Age">Age in years.</param>
/// <param name="Nid">National ID (unique).</param>
/// <param name="Role">Role code (e.g. admin).</param>
/// <param name="Status">Account status (active/inactive).</param>
/// <param name="MustChangePassword">True when the user must change their password at next sign-in.</param>
/// <param name="Profile">Role-dependent employment profile; null when the role has none.</param>
public sealed record UserDto(
    Guid Id, string? Code, string FullName, string? Email, string? Phone,
    string? Gender, int? Age, string Nid, string Role, string Status,
    bool MustChangePassword, UserProfileDto? Profile);

/// <summary>Payload for POST /api/users.</summary>
/// <param name="FullName">Display name.</param>
/// <param name="Role">Role code; determines which profile fields apply.</param>
/// <param name="Nid">National ID (must be unique).</param>
/// <param name="Email">Email address; optional for NID-only accounts.</param>
/// <param name="Password">Initial password; server may require a change at first sign-in.</param>
/// <param name="Phone">Phone number.</param>
/// <param name="Gender">Gender code.</param>
/// <param name="Age">Age in years.</param>
/// <param name="MonthlySalary">Monthly salary, for salaried roles.</param>
/// <param name="DailyWage">Daily wage, for waged roles.</param>
/// <param name="HireDate">Date the user was hired.</param>
public sealed record CreateUserRequest(
    string FullName, string Role, string Nid,
    string? Email, string? Password, string? Phone, string? Gender, int? Age,
    decimal? MonthlySalary, decimal? DailyWage, DateOnly? HireDate);

/// <summary>Payload for PATCH /api/users/{id}.</summary>
/// <param name="FullName">Display name.</param>
/// <param name="Role">Role code; determines which profile fields apply.</param>
/// <param name="Nid">National ID (must be unique).</param>
/// <param name="Status">Account status (active/inactive).</param>
/// <param name="Email">Email address; optional for NID-only accounts.</param>
/// <param name="Password">New password; omit to keep the current one.</param>
/// <param name="Phone">Phone number.</param>
/// <param name="Gender">Gender code.</param>
/// <param name="Age">Age in years.</param>
/// <param name="MonthlySalary">Monthly salary, for salaried roles.</param>
/// <param name="DailyWage">Daily wage, for waged roles.</param>
/// <param name="HireDate">Date the user was hired.</param>
public sealed record UpdateUserRequest(
    string FullName, string Role, string Nid, string Status,
    string? Email, string? Password, string? Phone, string? Gender, int? Age,
    decimal? MonthlySalary, decimal? DailyWage, DateOnly? HireDate);

/// <summary>Payload for PATCH /api/users/{id}/status.</summary>
/// <param name="Status">Target account status (active/inactive).</param>
public sealed record SetUserStatusRequest(string Status);
