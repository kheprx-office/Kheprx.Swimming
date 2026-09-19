namespace Kheprx.BaseBackend.Identity.Domain.ReadModels;

/// <summary>Flattened identity row for the profile page — profile + user + club + gender.</summary>
public sealed record SwimmerProfileRow(
    Guid Id,
    string Uid,
    string NameEn,
    string? NameAr,
    DateOnly? Dob,
    string? GenderCode,
    string? Phone,
    string? TrainingClubNameEn,
    string? TrainingClubNameAr);
