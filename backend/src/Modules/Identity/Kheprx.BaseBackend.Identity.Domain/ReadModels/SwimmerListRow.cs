namespace Kheprx.BaseBackend.Identity.Domain.ReadModels;

/// <summary>Flattened swimmer row for the roster list — a join of profile + user + club + gender.</summary>
public sealed record SwimmerListRow(
    Guid Id,
    string Uid,
    string NameEn,
    string? NameAr,
    string? ClubNameEn,
    string? ClubNameAr,
    string? GenderCode,
    DateOnly? Dob);
