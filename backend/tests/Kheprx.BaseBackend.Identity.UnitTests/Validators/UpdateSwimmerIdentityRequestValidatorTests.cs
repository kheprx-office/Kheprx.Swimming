using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Validators;

public class UpdateSwimmerIdentityRequestValidatorTests
{
    private readonly UpdateSwimmerIdentityRequestValidator _v = new();

    [Fact]
    public void Valid_request_passes()
        => Assert.True(_v.Validate(new UpdateSwimmerIdentityRequest("Alpha", "ألفا", new DateOnly(2010, 1, 1), "01000000001")).IsValid);

    [Fact]
    public void Empty_name_fails()
        => Assert.False(_v.Validate(new UpdateSwimmerIdentityRequest("", null, new DateOnly(2010, 1, 1), null)).IsValid);

    [Fact]
    public void Future_dob_fails()
        => Assert.False(_v.Validate(new UpdateSwimmerIdentityRequest("Alpha", null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), null)).IsValid);

    [Fact]
    public void Bad_phone_fails()
        => Assert.False(_v.Validate(new UpdateSwimmerIdentityRequest("Alpha", null, new DateOnly(2010, 1, 1), "12345")).IsValid);
}
