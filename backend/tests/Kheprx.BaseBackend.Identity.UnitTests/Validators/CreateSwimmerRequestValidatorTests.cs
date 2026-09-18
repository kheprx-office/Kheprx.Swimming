using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Validators;

public class CreateSwimmerRequestValidatorTests
{
    private static CreateSwimmerRequest Valid() => new(
        NameEn: "Mona Ali", Username: "mona.ali", TrainingClubId: Guid.NewGuid(),
        GenderId: Guid.NewGuid(), Dob: new DateOnly(2010, 5, 1),
        StrokeIds: new[] { Guid.NewGuid() }, NameAr: null, Email: null, Phone: null,
        RepresentChampionshipClubId: null);

    private readonly CreateSwimmerRequestValidator _v = new();

    [Fact] public void Valid_request_passes() => Assert.True(_v.Validate(Valid()).IsValid);

    [Fact]
    public void Missing_required_fields_fail()
    {
        Assert.False(_v.Validate(Valid() with { NameEn = "" }).IsValid);
        Assert.False(_v.Validate(Valid() with { Username = "" }).IsValid);
        Assert.False(_v.Validate(Valid() with { TrainingClubId = Guid.Empty }).IsValid);
        Assert.False(_v.Validate(Valid() with { GenderId = Guid.Empty }).IsValid);
        Assert.False(_v.Validate(Valid() with { StrokeIds = Array.Empty<Guid>() }).IsValid);
    }

    [Fact]
    public void Dob_must_be_in_the_past()
        => Assert.False(_v.Validate(Valid() with { Dob = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) }).IsValid);

    [Fact]
    public void Invalid_optional_email_and_phone_fail_when_present()
    {
        Assert.False(_v.Validate(Valid() with { Email = "nope" }).IsValid);
        Assert.False(_v.Validate(Valid() with { Phone = "123" }).IsValid);
    }
}
