using FluentValidation.TestHelper;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Validators;

public class UpdateHealthReadingRequestValidatorTests
{
    private readonly UpdateHealthReadingRequestValidator _validator = new();

    [Fact]
    public void Passes_for_a_positive_value()
        => _validator.TestValidate(new UpdateHealthReadingRequest(95m)).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Fails_for_zero_or_negative_value(decimal value)
        => _validator.TestValidate(new UpdateHealthReadingRequest(value)).ShouldHaveValidationErrorFor(x => x.Value);
}
