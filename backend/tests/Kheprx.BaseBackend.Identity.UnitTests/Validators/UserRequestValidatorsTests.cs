using FluentValidation.TestHelper;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Validators;

public class UserRequestValidatorsTests
{
    private readonly CreateUserRequestValidator _create = new();
    private readonly UpdateUserRequestValidator _update = new();
    private readonly SetUserStatusRequestValidator _status = new();

    private static CreateUserRequest ValidWorker(
        string role = "worker", string nid = "29801014501234",
        string? email = "w@x.com", string? password = "password8",
        decimal? monthlySalary = null,
        decimal? dailyWage = 350m, DateOnly? hireDate = null) =>
        new("Worker", role, nid, email, password, "01012345678", "male", 24,
            monthlySalary,
            role is "worker" or "moqawel" ? dailyWage : null, hireDate);

    [Fact]
    public void Create_accepts_valid_worker()
    {
        var result = _create.TestValidate(ValidWorker());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Create_rejects_bad_nid_formats()
    {
        _create.TestValidate(ValidWorker(nid: "")).ShouldHaveValidationErrorFor(x => x.Nid);
        _create.TestValidate(ValidWorker(nid: "123")).ShouldHaveValidationErrorFor(x => x.Nid);
        _create.TestValidate(ValidWorker(nid: "2980101450123X")).ShouldHaveValidationErrorFor(x => x.Nid);
    }

    [Fact]
    public void Create_requires_email_and_password()
    {
        var both = _create.TestValidate(ValidWorker(email: null, password: null));
        both.ShouldHaveValidationErrorFor(x => x.Email);
        both.ShouldHaveValidationErrorFor(x => x.Password);
        _create.TestValidate(ValidWorker(email: "w@x.com", password: null)).ShouldHaveValidationErrorFor(x => x.Password);
        _create.TestValidate(ValidWorker(email: null, password: "password8")).ShouldHaveValidationErrorFor(x => x.Email);

        var valid = _create.TestValidate(ValidWorker());
        valid.ShouldNotHaveValidationErrorFor(x => x.Email);
        valid.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Create_rejects_invalid_gender_and_age()
    {
        var badGender = ValidWorker() with { Gender = "other" };
        _create.TestValidate(badGender).ShouldHaveValidationErrorFor(x => x.Gender);
        var badAge = ValidWorker() with { Age = 9 };
        _create.TestValidate(badAge).ShouldHaveValidationErrorFor(x => x.Age);
    }

    [Fact]
    public void Create_manager_requires_salary_and_rejects_wage_fields()
    {
        var manager = new CreateUserRequest("Mgr", "manager", "29801014501234",
            "m@x.com", "password8", "01012345678", "male", 24, null, null, null);
        _create.TestValidate(manager).ShouldHaveValidationErrorFor(x => x.MonthlySalary);

        var managerWithWage = manager with { MonthlySalary = 15000m, DailyWage = 100m };
        _create.TestValidate(managerWithWage).ShouldHaveValidationErrorFor(x => x.DailyWage);

        var valid = manager with { MonthlySalary = 15000m };
        _create.TestValidate(valid).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Create_worker_requires_wage()
    {
        var result = _create.TestValidate(ValidWorker() with { DailyWage = null });
        result.ShouldHaveValidationErrorFor(x => x.DailyWage);
    }

    [Fact]
    public void Create_admin_rejects_all_profile_fields()
    {
        var admin = new CreateUserRequest("Admin", "admin", "29801014501234",
            "a@x.com", "password8", null, null, null, 1m, 1m, new DateOnly(2026, 1, 1));
        var result = _create.TestValidate(admin);
        result.ShouldHaveValidationErrorFor(x => x.MonthlySalary);
        result.ShouldHaveValidationErrorFor(x => x.DailyWage);
        result.ShouldHaveValidationErrorFor(x => x.HireDate);
    }

    [Fact]
    public void Update_mirrors_create_rules_and_keeps_status()
    {
        var valid = new UpdateUserRequest("Worker", "worker", "29801014501234", "active",
            "w@x.com", null, "01012345678", "male", 25, null, 400m, null);
        _update.TestValidate(valid).ShouldNotHaveAnyValidationErrors();

        _update.TestValidate(valid with { Status = "paused" }).ShouldHaveValidationErrorFor(x => x.Status);
        _update.TestValidate(valid with { Nid = "123" }).ShouldHaveValidationErrorFor(x => x.Nid);
        _update.TestValidate(valid with { Role = "ghost" }).ShouldHaveValidationErrorFor(x => x.Role);
    }

    [Fact]
    public void Status_rejects_unknown_value()
    {
        var result = _status.TestValidate(new SetUserStatusRequest("paused"));
        result.ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void Create_requires_phone_gender_age()
    {
        _create.TestValidate(ValidWorker() with { Phone = null }).ShouldHaveValidationErrorFor(x => x.Phone);
        _create.TestValidate(ValidWorker() with { Gender = null }).ShouldHaveValidationErrorFor(x => x.Gender);
        _create.TestValidate(ValidWorker() with { Age = null }).ShouldHaveValidationErrorFor(x => x.Age);
    }

    [Fact]
    public void Create_rejects_malformed_phone()
    {
        _create.TestValidate(ValidWorker() with { Phone = "011" }).ShouldHaveValidationErrorFor(x => x.Phone);
        _create.TestValidate(ValidWorker() with { Phone = "01312345678" }).ShouldHaveValidationErrorFor(x => x.Phone);
        _create.TestValidate(ValidWorker() with { Phone = "01012345678" }).ShouldNotHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public void Update_keeps_phone_gender_age_optional_but_validates_phone_format()
    {
        var valid = new UpdateUserRequest("Worker", "worker", "29801014501234", "active",
            "w@x.com", null, null, null, null, null, 400m, null);
        _update.TestValidate(valid).ShouldNotHaveValidationErrorFor(x => x.Phone);
        _update.TestValidate(valid).ShouldNotHaveValidationErrorFor(x => x.Gender);
        _update.TestValidate(valid).ShouldNotHaveValidationErrorFor(x => x.Age);
        _update.TestValidate(valid with { Phone = "011" }).ShouldHaveValidationErrorFor(x => x.Phone);
    }
}
