using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Validators;

public class CreateMedicalTestRequestValidatorTests
{
    private static CreateMedicalTestRequest Valid() =>
        new(NameEn: "Hemoglobin", NameAr: "الهيموغلوبين", Unit: "g/dL", LowerBound: 11m, UpperBound: 17.5m);

    private readonly CreateMedicalTestRequestValidator _v = new();

    [Fact] public void Valid_request_passes() => Assert.True(_v.Validate(Valid()).IsValid);

    [Fact]
    public void Missing_required_text_fields_fail()
    {
        Assert.False(_v.Validate(Valid() with { NameEn = "" }).IsValid);
        Assert.False(_v.Validate(Valid() with { NameAr = "" }).IsValid);
        Assert.False(_v.Validate(Valid() with { Unit = "" }).IsValid);
    }

    [Fact]
    public void Upper_bound_must_exceed_lower_bound()
    {
        Assert.False(_v.Validate(Valid() with { LowerBound = 10m, UpperBound = 10m }).IsValid); // equal
        Assert.False(_v.Validate(Valid() with { LowerBound = 20m, UpperBound = 5m }).IsValid);  // inverted
    }
}
