using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Validators;

public class CreateInBodyReadingRequestValidatorTests
{
    private readonly CreateInBodyReadingRequestValidator _validator = new();

    private static CreateInBodyReadingRequest Valid()
        => new(new DateOnly(2024, 10, 4), 180m, 74m, 12.8m, 42.1m, 1.35m, 1.07m);

    [Fact]
    public void Valid_request_passes() => Assert.True(_validator.Validate(Valid()).IsValid);

    [Fact]
    public void Missing_date_fails()
    {
        var result = _validator.Validate(Valid() with { ReadingDate = default });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ReadingDate");
    }

    [Fact]
    public void Non_positive_height_fails()
        => Assert.False(_validator.Validate(Valid() with { HeightCm = 0m }).IsValid);

    [Fact]
    public void Fat_over_100_fails()
    {
        var result = _validator.Validate(Valid() with { FatPct = 101m });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "FatPct");
    }

    [Fact]
    public void Bone_density_over_column_max_fails()
        => Assert.False(_validator.Validate(Valid() with { BoneDensity = 100m }).IsValid); // numeric(4,2) max 99.99
}
