using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Validators;

public class UpsertGuardiansRequestValidatorTests
{
    private readonly UpsertGuardiansRequestValidator _validator = new();

    private static UpsertGuardiansRequest Valid() => new(
        new GuardianInputDto("Hassan Ali", "27001010123456", "+201009876543"),
        new GuardianInputDto("Fatima Ibrahim", "27505050123456", "+201005554444"));

    [Fact]
    public void Valid_request_passes()
    {
        var result = _validator.Validate(Valid());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Father_national_id_not_14_digits_fails()
    {
        var req = Valid() with { Father = new GuardianInputDto("Hassan Ali", "123", "+201009876543") };
        var result = _validator.Validate(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Father.NationalId");
    }

    [Fact]
    public void Mother_empty_name_fails()
    {
        var req = Valid() with { Mother = new GuardianInputDto("", "27505050123456", "+201005554444") };
        var result = _validator.Validate(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Mother.Name");
    }

    [Fact]
    public void Empty_phone_fails()
    {
        var req = Valid() with { Father = new GuardianInputDto("Hassan Ali", "27001010123456", "") };
        var result = _validator.Validate(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Father.Phone");
    }
}
