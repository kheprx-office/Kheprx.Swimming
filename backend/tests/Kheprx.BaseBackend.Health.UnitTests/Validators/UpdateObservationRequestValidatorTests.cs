using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Validators;

public class UpdateObservationRequestValidatorTests
{
    private static UpdateObservationRequest Valid() =>
        new(CategoryId: Guid.NewGuid(), FieldLabel: "Penicillin", Value: "Severe");

    private readonly UpdateObservationRequestValidator _v = new();

    [Fact] public void Valid_request_passes() => Assert.True(_v.Validate(Valid()).IsValid);

    [Fact]
    public void Empty_fields_fail()
    {
        Assert.False(_v.Validate(Valid() with { CategoryId = Guid.Empty }).IsValid);
        Assert.False(_v.Validate(Valid() with { FieldLabel = "" }).IsValid);
        Assert.False(_v.Validate(Valid() with { Value = "" }).IsValid);
    }
}
