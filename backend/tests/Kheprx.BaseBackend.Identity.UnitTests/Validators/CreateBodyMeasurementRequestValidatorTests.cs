using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Validators;

public class CreateBodyMeasurementRequestValidatorTests
{
    private readonly CreateBodyMeasurementRequestValidator _validator = new();

    private static CreateBodyMeasurementRequest Valid()
        => new(78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m);

    [Fact]
    public void Valid_request_passes()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Fact]
    public void Zero_value_fails()
    {
        var req = Valid() with { RightArmCm = 0m };
        var result = _validator.Validate(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "RightArmCm");
    }

    [Fact]
    public void Negative_value_fails()
    {
        var req = Valid() with { TorsoCm = -1m };
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Out_of_range_value_fails()
    {
        var req = Valid() with { WaistDiameterCm = 1000m };
        var result = _validator.Validate(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "WaistDiameterCm");
    }
}
