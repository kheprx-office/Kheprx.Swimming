using FluentValidation;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Identity.Application.Validators;

internal static class UserValidationRules
{
    public static readonly string[] Roles = { "admin", "manager", "moqawel", "worker" };
    public static readonly string[] Statuses = { "active", "disabled" };
    public static readonly string[] Genders = { "male", "female" };
    public const string NidPattern = @"^\d{14}$";
    public const string PhonePattern = @"^01[0125]\d{8}$";
}

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage(_ => CommonMessages.Errors.FullNameRequired(AppLanguage.Current))
            .MaximumLength(200).WithMessage(_ => CommonMessages.Errors.FullNameTooLong(AppLanguage.Current));
        RuleFor(x => x.Role)
            .NotEmpty().WithMessage(_ => UserMessages.Errors.RoleRequired(AppLanguage.Current))
            .Must(r => UserValidationRules.Roles.Contains(r)).WithMessage(_ => UserMessages.Errors.UnknownRole(AppLanguage.Current));
        RuleFor(x => x.Nid)
            .NotEmpty().WithMessage(_ => UserMessages.Errors.NidRequired(AppLanguage.Current))
            .Matches(UserValidationRules.NidPattern).WithMessage(_ => UserMessages.Errors.NidInvalid(AppLanguage.Current));

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(_ => CommonMessages.Errors.EmailRequired(AppLanguage.Current));
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage(_ => CommonMessages.Errors.EmailInvalid(AppLanguage.Current))
            .MaximumLength(256).WithMessage(_ => CommonMessages.Errors.EmailTooLong(AppLanguage.Current))
            .When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(_ => CommonMessages.Errors.PasswordRequired(AppLanguage.Current));
        RuleFor(x => x.Password)
            .MinimumLength(8).WithMessage(_ => CommonMessages.Errors.PasswordMinLength(AppLanguage.Current))
            .MaximumLength(256).WithMessage(_ => CommonMessages.Errors.PasswordTooLong(AppLanguage.Current))
            .When(x => !string.IsNullOrEmpty(x.Password));

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage(_ => UserMessages.Errors.PhoneRequired(AppLanguage.Current));
        RuleFor(x => x.Phone)
            .Matches(UserValidationRules.PhonePattern).WithMessage(_ => UserMessages.Errors.PhoneInvalid(AppLanguage.Current))
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.Gender)
            .NotEmpty().WithMessage(_ => UserMessages.Errors.GenderRequired(AppLanguage.Current));
        RuleFor(x => x.Gender)
            .Must(g => UserValidationRules.Genders.Contains(g)).WithMessage(_ => UserMessages.Errors.InvalidGender(AppLanguage.Current))
            .When(x => !string.IsNullOrEmpty(x.Gender));

        RuleFor(x => x.Age)
            .NotNull().WithMessage(_ => UserMessages.Errors.AgeRequired(AppLanguage.Current));
        RuleFor(x => x.Age)
            .InclusiveBetween(14, 90).WithMessage(_ => UserMessages.Errors.InvalidAge(AppLanguage.Current))
            .When(x => x.Age.HasValue);

        this.AddProfileFieldRules(x => x.Role, x => x.MonthlySalary, x => x.DailyWage, x => x.HireDate);
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage(_ => CommonMessages.Errors.FullNameRequired(AppLanguage.Current))
            .MaximumLength(200).WithMessage(_ => CommonMessages.Errors.FullNameTooLong(AppLanguage.Current));
        RuleFor(x => x.Role)
            .NotEmpty().WithMessage(_ => UserMessages.Errors.RoleRequired(AppLanguage.Current))
            .Must(r => UserValidationRules.Roles.Contains(r)).WithMessage(_ => UserMessages.Errors.UnknownRole(AppLanguage.Current));
        RuleFor(x => x.Nid)
            .NotEmpty().WithMessage(_ => UserMessages.Errors.NidRequired(AppLanguage.Current))
            .Matches(UserValidationRules.NidPattern).WithMessage(_ => UserMessages.Errors.NidInvalid(AppLanguage.Current));
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage(_ => UserMessages.Errors.StatusRequired(AppLanguage.Current))
            .Must(s => UserValidationRules.Statuses.Contains(s)).WithMessage(_ => UserMessages.Errors.InvalidStatus(AppLanguage.Current));
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage(_ => CommonMessages.Errors.EmailInvalid(AppLanguage.Current))
            .MaximumLength(256).WithMessage(_ => CommonMessages.Errors.EmailTooLong(AppLanguage.Current))
            .When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Password)
            .MinimumLength(8).WithMessage(_ => CommonMessages.Errors.PasswordMinLength(AppLanguage.Current))
            .MaximumLength(256).WithMessage(_ => CommonMessages.Errors.PasswordTooLong(AppLanguage.Current))
            .When(x => !string.IsNullOrEmpty(x.Password));
        RuleFor(x => x.Phone)
            .Matches(UserValidationRules.PhonePattern).WithMessage(_ => UserMessages.Errors.PhoneInvalid(AppLanguage.Current))
            .When(x => !string.IsNullOrEmpty(x.Phone));
        RuleFor(x => x.Gender)
            .Must(g => UserValidationRules.Genders.Contains(g)).WithMessage(_ => UserMessages.Errors.InvalidGender(AppLanguage.Current))
            .When(x => !string.IsNullOrEmpty(x.Gender));
        RuleFor(x => x.Age)
            .InclusiveBetween(14, 90).WithMessage(_ => UserMessages.Errors.InvalidAge(AppLanguage.Current))
            .When(x => x.Age.HasValue);

        this.AddProfileFieldRules(x => x.Role, x => x.MonthlySalary, x => x.DailyWage, x => x.HireDate);
    }
}

public sealed class SetUserStatusRequestValidator : AbstractValidator<SetUserStatusRequest>
{
    public SetUserStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage(_ => UserMessages.Errors.StatusRequired(AppLanguage.Current))
            .Must(s => UserValidationRules.Statuses.Contains(s)).WithMessage(_ => UserMessages.Errors.InvalidStatus(AppLanguage.Current));
    }
}

internal static class ProfileFieldRuleExtensions
{
    /// <summary>Role-conditional profile rules shared by create/update:
    /// manager → MonthlySalary required ≥0; moqawel/worker → DailyWage required (≥0);
    /// worker → HireDate optional. Fields belonging to a DIFFERENT role are rejected, not ignored.</summary>
    public static void AddProfileFieldRules<T>(
        this AbstractValidator<T> validator,
        Func<T, string> role,
        System.Linq.Expressions.Expression<Func<T, decimal?>> monthlySalary,
        System.Linq.Expressions.Expression<Func<T, decimal?>> dailyWage,
        System.Linq.Expressions.Expression<Func<T, DateOnly?>> hireDate) where T : class
    {
        validator.RuleFor(monthlySalary)
            .NotNull().WithMessage(_ => UserMessages.Errors.MonthlySalaryRequired(AppLanguage.Current))
            .GreaterThanOrEqualTo(0).WithMessage(_ => UserMessages.Errors.MonthlySalaryInvalid(AppLanguage.Current))
            .When(x => role(x) == "manager");
        validator.RuleFor(monthlySalary)
            .Null().WithMessage(_ => UserMessages.Errors.FieldNotAllowedForRole(AppLanguage.Current))
            .When(x => role(x) != "manager");

        validator.RuleFor(dailyWage)
            .NotNull().WithMessage(_ => UserMessages.Errors.DailyWageRequired(AppLanguage.Current))
            .GreaterThanOrEqualTo(0).WithMessage(_ => UserMessages.Errors.DailyWageInvalid(AppLanguage.Current))
            .When(x => role(x) is "moqawel" or "worker");
        validator.RuleFor(dailyWage)
            .Null().WithMessage(_ => UserMessages.Errors.FieldNotAllowedForRole(AppLanguage.Current))
            .When(x => role(x) is not ("moqawel" or "worker"));

        validator.RuleFor(hireDate)
            .Null().WithMessage(_ => UserMessages.Errors.FieldNotAllowedForRole(AppLanguage.Current))
            .When(x => role(x) != "worker");
    }
}
