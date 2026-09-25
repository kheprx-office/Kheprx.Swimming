using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Validators;

public sealed class OnboardingValidatorTests
{
    private static CompleteIdentityVitalsRequest Valid() => new(
        "Ahmed Ali", null, Guid.NewGuid(), new DateOnly(2010, 1, 1), Guid.NewGuid(),
        new DateOnly(2026, 1, 1), null, 14.5m, 175m, 68m,
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "01012345678");

    private static readonly CompleteIdentityVitalsRequestValidator V = new();

    [Fact] public void Accepts_a_valid_request() => Assert.True(V.Validate(Valid()).IsValid);

    [Fact] public void Iv_rejects_invalid_phone() => Assert.False(V.Validate(Valid() with { Phone = "12345" }).IsValid);

    [Fact] public void Iv_accepts_null_phone() => Assert.True(V.Validate(Valid() with { Phone = null }).IsValid);

    [Fact] public void Rejects_blank_name() => Assert.False(V.Validate(Valid() with { NameEn = "" }).IsValid);

    [Fact] public void Rejects_empty_gender() => Assert.False(V.Validate(Valid() with { GenderId = Guid.Empty }).IsValid);

    [Fact] public void Rejects_empty_training_club() => Assert.False(V.Validate(Valid() with { TrainingClubId = Guid.Empty }).IsValid);

    [Fact] public void Rejects_future_dob() => Assert.False(V.Validate(Valid() with { Dob = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1) }).IsValid);

    [Fact] public void Rejects_future_exam_date() => Assert.False(V.Validate(Valid() with { ExamDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1) }).IsValid);

    [Fact] public void Rejects_empty_assessment() => Assert.False(V.Validate(Valid() with { InternalMedId = Guid.Empty }).IsValid);

    [Fact] public void Rejects_non_positive_height() => Assert.False(V.Validate(Valid() with { HeightCm = 0m }).IsValid);

    [Fact] public void Allows_null_blood_type() => Assert.True(V.Validate(Valid() with { BloodTypeId = null }).IsValid);

    private static CompleteGuardianMedicalRequest ValidGm() => new(
        new GuardianInputDto("Ahmed Ali", "12345678901234", "0100000000"),
        new GuardianInputDto("Sara Omar", "43210987654321", "0111111111"),
        new[] { new OnboardingMedicalItemDto(Guid.NewGuid(), "Allergies", "Peanuts") });

    private static readonly CompleteGuardianMedicalRequestValidator GV = new();

    [Fact] public void Gm_accepts_a_valid_request() => Assert.True(GV.Validate(ValidGm()).IsValid);

    [Fact] public void Gm_accepts_empty_medical_list()
        => Assert.True(GV.Validate(ValidGm() with { Medical = System.Array.Empty<OnboardingMedicalItemDto>() }).IsValid);

    [Fact] public void Gm_rejects_father_national_id_not_14_digits()
        => Assert.False(GV.Validate(ValidGm() with { Father = new GuardianInputDto("Ahmed Ali", "1234567890123", "0100000000") }).IsValid);

    [Fact] public void Gm_rejects_blank_mother_name()
        => Assert.False(GV.Validate(ValidGm() with { Mother = new GuardianInputDto("", "43210987654321", "0111111111") }).IsValid);

    [Fact] public void Gm_rejects_medical_item_with_blank_value()
        => Assert.False(GV.Validate(ValidGm() with { Medical = new[] { new OnboardingMedicalItemDto(Guid.NewGuid(), "Allergies", "") } }).IsValid);

    [Fact] public void Gm_rejects_medical_item_with_empty_category()
        => Assert.False(GV.Validate(ValidGm() with { Medical = new[] { new OnboardingMedicalItemDto(Guid.Empty, "Allergies", "Peanuts") } }).IsValid);

    private static CompletePhysiologicalRequest ValidPhys() => new(32.5m, 31.0m, 95m, 94m, 60m, 90m, 75m);

    private static readonly CompletePhysiologicalRequestValidator PV = new();

    [Fact] public void Phys_accepts_a_valid_request() => Assert.True(PV.Validate(ValidPhys()).IsValid);

    [Fact] public void Phys_rejects_zero_right_arm() => Assert.False(PV.Validate(ValidPhys() with { RightArmCm = 0m }).IsValid);

    [Fact] public void Phys_rejects_zero_left_arm() => Assert.False(PV.Validate(ValidPhys() with { LeftArmCm = 0m }).IsValid);

    [Fact] public void Phys_rejects_negative_right_leg() => Assert.False(PV.Validate(ValidPhys() with { RightLegCm = -1m }).IsValid);

    [Fact] public void Phys_rejects_negative_left_leg() => Assert.False(PV.Validate(ValidPhys() with { LeftLegCm = -1m }).IsValid);

    [Fact] public void Phys_rejects_over_max_torso() => Assert.False(PV.Validate(ValidPhys() with { TorsoCm = 1000m }).IsValid);

    [Fact] public void Phys_accepts_boundary_max() => Assert.True(PV.Validate(ValidPhys() with { WaistDiameterCm = 999.9m }).IsValid);
}
