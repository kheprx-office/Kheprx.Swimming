using FluentValidation.TestHelper;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Validators;

public class UserRequestValidatorsTests
{
    private readonly CreateUserRequestValidator _create = new();
    private readonly UpdateUserRequestValidator _update = new();

    private static CreateUserRequest ValidCreate(
        string nameEn = "Dave Smith", string role = "captain",
        string? email = "c@x.com", string? password = "password8") =>
        new("captain.dave", nameEn, role, null, email, password, "01012345678", null, null);

    [Fact]
    public void Create_accepts_valid_request()
    {
        var result = _create.TestValidate(ValidCreate());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Create_requires_name_en()
    {
        _create.TestValidate(ValidCreate(nameEn: "")).ShouldHaveValidationErrorFor(x => x.NameEn);
        _create.TestValidate(ValidCreate(nameEn: "  ")).ShouldHaveValidationErrorFor(x => x.NameEn);
    }

    [Fact]
    public void Create_requires_role()
    {
        _create.TestValidate(ValidCreate(role: "")).ShouldHaveValidationErrorFor(x => x.Role);
    }

    [Fact]
    public void Create_validates_email_format_when_present()
    {
        var bad = new CreateUserRequest("u", "Name", "captain", null, "not-an-email", "password8", null, null, null);
        _create.TestValidate(bad).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Create_validates_password_min_length_when_present()
    {
        var bad = ValidCreate(password: "short");
        _create.TestValidate(bad).ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Create_allows_no_email_and_no_password()
    {
        var result = _create.TestValidate(ValidCreate(email: null, password: null));
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Create_validates_phone_format_when_present()
    {
        var bad = new CreateUserRequest("u", "Name", "captain", null, null, null, "011", null, null);
        _create.TestValidate(bad).ShouldHaveValidationErrorFor(x => x.Phone);

        var good = new CreateUserRequest("u", "Name", "captain", null, null, null, "01012345678", null, null);
        _create.TestValidate(good).ShouldNotHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public void Update_requires_name_en_and_role()
    {
        var valid = new UpdateUserRequest("Dave", "captain", null, null, null, null, null, null);
        _update.TestValidate(valid).ShouldNotHaveAnyValidationErrors();

        _update.TestValidate(valid with { NameEn = "" }).ShouldHaveValidationErrorFor(x => x.NameEn);
        _update.TestValidate(valid with { Role = "" }).ShouldHaveValidationErrorFor(x => x.Role);
    }

    [Fact]
    public void Update_validates_email_and_phone_format_when_present()
    {
        var valid = new UpdateUserRequest("Dave", "captain", null, null, null, null, null, null);
        _update.TestValidate(valid with { Email = "bad" }).ShouldHaveValidationErrorFor(x => x.Email);
        _update.TestValidate(valid with { Phone = "011" }).ShouldHaveValidationErrorFor(x => x.Phone);
        _update.TestValidate(valid with { Phone = "01012345678" }).ShouldNotHaveValidationErrorFor(x => x.Phone);
    }
}
