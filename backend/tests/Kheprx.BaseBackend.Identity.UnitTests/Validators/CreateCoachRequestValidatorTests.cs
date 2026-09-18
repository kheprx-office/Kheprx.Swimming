using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Validators;

public class CreateCoachRequestValidatorTests
{
    private static CreateCoachRequest Valid() => new(
        Role: "captain", NameEn: "Dave Coach", Username: "dave.coach", Email: "dave@oasis.com",
        NationalId: "29001011234567", GenderId: Guid.NewGuid(), Dob: new DateOnly(1990, 1, 1),
        Phone: "01000000000", NameAr: null);

    private readonly CreateCoachRequestValidator _v = new();

    [Fact] public void Valid_request_passes() => Assert.True(_v.Validate(Valid()).IsValid);

    [Fact]
    public void Role_must_be_captain_or_head_coach()
    {
        Assert.True(_v.Validate(Valid() with { Role = "head_coach" }).IsValid);
        Assert.False(_v.Validate(Valid() with { Role = "swimmer" }).IsValid);
        Assert.False(_v.Validate(Valid() with { Role = "" }).IsValid);
    }

    [Fact]
    public void National_id_must_be_14_digits()
    {
        Assert.False(_v.Validate(Valid() with { NationalId = "123" }).IsValid);
        Assert.False(_v.Validate(Valid() with { NationalId = "2900101123456A" }).IsValid);
    }

    [Fact]
    public void Required_fields_and_formats_are_enforced()
    {
        Assert.False(_v.Validate(Valid() with { NameEn = "" }).IsValid);
        Assert.False(_v.Validate(Valid() with { Username = "" }).IsValid);
        Assert.False(_v.Validate(Valid() with { Email = "nope" }).IsValid);
        Assert.False(_v.Validate(Valid() with { GenderId = Guid.Empty }).IsValid);
        Assert.False(_v.Validate(Valid() with { Phone = "123" }).IsValid);
        Assert.False(_v.Validate(Valid() with { Dob = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) }).IsValid);
    }
}
