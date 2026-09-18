namespace Kheprx.BaseBackend.Identity.Application.DTOs;

/// <summary>Payload to register a captain or head coach (POST /api/coaches).</summary>
public sealed record CreateCoachRequest(
    string Role, string NameEn, string Username, string Email, string NationalId,
    Guid GenderId, DateOnly Dob, string Phone, string? NameAr);

/// <summary>Result of a successful coach registration.</summary>
public sealed record CreatedCoachDto(Guid Id, string Username, string NameEn, string Role, string TemporaryPassword);
