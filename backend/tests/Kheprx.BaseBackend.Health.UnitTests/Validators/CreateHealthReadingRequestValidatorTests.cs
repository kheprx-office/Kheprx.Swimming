using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Validators;

public class CreateHealthReadingRequestValidatorTests
{
    private static CreateHealthReadingRequest Valid() =>
        new(SwimmerId: Guid.NewGuid(), MedicalTestId: Guid.NewGuid(), Value: 95m);

    private readonly CreateHealthReadingRequestValidator _v = new();

    [Fact] public void Valid_request_passes() => Assert.True(_v.Validate(Valid()).IsValid);

    [Fact]
    public void Empty_ids_fail()
    {
        Assert.False(_v.Validate(Valid() with { SwimmerId = Guid.Empty }).IsValid);
        Assert.False(_v.Validate(Valid() with { MedicalTestId = Guid.Empty }).IsValid);
    }
}
