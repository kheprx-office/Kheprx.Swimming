using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Validators;

public class CreateObservationRequestValidatorTests
{
    private static CreateObservationRequest Valid() =>
        new(SwimmerId: Guid.NewGuid(), CategoryId: Guid.NewGuid(), FieldLabel: "Penicillin", Value: "Severe");

    private readonly CreateObservationRequestValidator _v = new();

    [Fact] public void Valid_request_passes() => Assert.True(_v.Validate(Valid()).IsValid);

    [Fact]
    public void Empty_fields_fail()
    {
        Assert.False(_v.Validate(Valid() with { SwimmerId = Guid.Empty }).IsValid);
        Assert.False(_v.Validate(Valid() with { CategoryId = Guid.Empty }).IsValid);
        Assert.False(_v.Validate(Valid() with { FieldLabel = "" }).IsValid);
        Assert.False(_v.Validate(Valid() with { Value = "" }).IsValid);
    }
}
