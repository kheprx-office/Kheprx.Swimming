using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Validators;

public sealed class CompleteInBodyRequestValidatorTests
{
    private static CompleteInBodyRequest Valid() => new(175m, 68m, 15m, 40m, 55m, 3.2m, 1.05m);
    private static readonly CompleteInBodyRequestValidator V = new();

    [Fact] public void Accepts_a_valid_request() => Assert.True(V.Validate(Valid()).IsValid);
    [Fact] public void Rejects_zero_height() => Assert.False(V.Validate(Valid() with { HeightCm = 0m }).IsValid);
    [Fact] public void Rejects_over_max_weight() => Assert.False(V.Validate(Valid() with { WeightKg = 1000m }).IsValid);
    [Fact] public void Accepts_zero_water() => Assert.True(V.Validate(Valid() with { WaterPct = 0m }).IsValid);
    [Fact] public void Accepts_hundred_muscle() => Assert.True(V.Validate(Valid() with { MusclePct = 100m }).IsValid);
    [Fact] public void Rejects_fat_over_hundred() => Assert.False(V.Validate(Valid() with { FatPct = 101m }).IsValid);
    [Fact] public void Rejects_zero_bone_density() => Assert.False(V.Validate(Valid() with { BoneDensity = 0m }).IsValid);
    [Fact] public void Rejects_over_max_body_density() => Assert.False(V.Validate(Valid() with { BodyDensity = 100m }).IsValid);
}
