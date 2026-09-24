using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Validators;

public sealed class OnboardingValidatorTests
{
    private static CompleteIdentityVitalsRequest Valid() => new(
        "Ahmed Ali", null, Guid.NewGuid(), new DateOnly(2010, 1, 1), Guid.NewGuid(),
        new DateOnly(2026, 1, 1), null, 14.5m, 175m, 68m,
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private static readonly CompleteIdentityVitalsRequestValidator V = new();

    [Fact] public void Accepts_a_valid_request() => Assert.True(V.Validate(Valid()).IsValid);

    [Fact] public void Rejects_blank_name() => Assert.False(V.Validate(Valid() with { NameEn = "" }).IsValid);

    [Fact] public void Rejects_empty_gender() => Assert.False(V.Validate(Valid() with { GenderId = Guid.Empty }).IsValid);

    [Fact] public void Rejects_empty_training_club() => Assert.False(V.Validate(Valid() with { TrainingClubId = Guid.Empty }).IsValid);

    [Fact] public void Rejects_future_dob() => Assert.False(V.Validate(Valid() with { Dob = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1) }).IsValid);

    [Fact] public void Rejects_future_exam_date() => Assert.False(V.Validate(Valid() with { ExamDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1) }).IsValid);

    [Fact] public void Rejects_empty_assessment() => Assert.False(V.Validate(Valid() with { InternalMedId = Guid.Empty }).IsValid);

    [Fact] public void Rejects_non_positive_height() => Assert.False(V.Validate(Valid() with { HeightCm = 0m }).IsValid);

    [Fact] public void Allows_null_blood_type() => Assert.True(V.Validate(Valid() with { BloodTypeId = null }).IsValid);
}
