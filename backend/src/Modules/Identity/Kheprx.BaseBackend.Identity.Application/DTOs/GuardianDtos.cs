namespace Kheprx.BaseBackend.Identity.Application.DTOs;

/// <summary>One guardian for the read shape (GET /api/swimmers/{id}/guardians).</summary>
public sealed record GuardianDto(Guid Id, string RelationCode, string Name, string NationalId, string Phone);

/// <summary>A swimmer's guardians — father/mother slots, either may be null.</summary>
public sealed record SwimmerGuardiansDto(GuardianDto? Father, GuardianDto? Mother);

/// <summary>One guardian slot in an upsert payload.</summary>
public sealed record GuardianInputDto(string Name, string NationalId, string Phone);

/// <summary>Payload to upsert both guardians (PUT /api/swimmers/{id}/guardians).</summary>
public sealed record UpsertGuardiansRequest(GuardianInputDto Father, GuardianInputDto Mother);
