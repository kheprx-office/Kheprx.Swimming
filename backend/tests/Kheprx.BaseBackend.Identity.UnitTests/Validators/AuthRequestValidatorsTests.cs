using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Validators;

public class AuthRequestValidatorsTests
{
    [Fact]
    public void Login_valid_passes()
        => Assert.True(new LoginRequestValidator().Validate(new LoginRequest("a@b.com", "x")).IsValid);

    [Fact]
    public void Login_empty_email_fails()
        => Assert.False(new LoginRequestValidator().Validate(new LoginRequest("", "x")).IsValid);

    [Fact]
    public void Login_bad_email_fails()
        => Assert.False(new LoginRequestValidator().Validate(new LoginRequest("nope", "x")).IsValid);

    [Fact]
    public void Login_short_password_still_passes_no_length_rule() // AD-011
        => Assert.True(new LoginRequestValidator().Validate(new LoginRequest("a@b.com", "x")).IsValid);

    [Fact]
    public void Refresh_empty_fails()
        => Assert.False(new RefreshRequestValidator().Validate(new RefreshRequest("")).IsValid);

    [Fact]
    public void ChangePassword_short_new_password_fails() // AD-011 min 8
        => Assert.False(new ChangePasswordRequestValidator()
            .Validate(new ChangePasswordRequest("current", "short7!")).IsValid);

    [Fact]
    public void ChangePassword_valid_passes()
        => Assert.True(new ChangePasswordRequestValidator()
            .Validate(new ChangePasswordRequest("current", "newpass8")).IsValid);

    [Fact]
    public void ChangePassword_empty_current_fails()
        => Assert.False(new ChangePasswordRequestValidator()
            .Validate(new ChangePasswordRequest("", "newpass8")).IsValid);
}
