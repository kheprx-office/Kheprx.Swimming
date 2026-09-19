namespace Kheprx.BaseBackend.Identity.Domain.ReadModels;

/// <summary>Flattened guardian row — guardian joined to its relation code.</summary>
public sealed record GuardianRow(
    Guid Id,
    string RelationCode,
    string Name,
    string NationalId,
    string Phone);
