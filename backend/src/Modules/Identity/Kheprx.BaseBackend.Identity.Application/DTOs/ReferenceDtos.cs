namespace Kheprx.BaseBackend.Identity.Application.DTOs;

/// <summary>A reference lookup row that carries a stable code (stroke, blood type, gender).</summary>
public sealed record CodedLookupDto(Guid Id, string Code, string NameEn, string? NameAr);

/// <summary>A club lookup row (no code — clubs are identified by id).</summary>
public sealed record ClubDto(Guid Id, string NameEn, string? NameAr);
