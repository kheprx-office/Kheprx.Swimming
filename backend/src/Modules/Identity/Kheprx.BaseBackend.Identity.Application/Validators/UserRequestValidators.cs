using FluentValidation;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Identity.Application.Validators;

internal static class UserValidationRules
{
    public const string PhonePattern = @"^01[0125]\d{8}$";
}

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage(_ => CommonMessages.Errors.FullNameRequired(AppLanguage.Current))
            .MaximumLength(200).WithMessage(_ => CommonMessages.Errors.FullNameTooLong(AppLanguage.Current));
        RuleFor(x => x.Role)
            .NotEmpty().WithMessage(_ => UserMessages.Errors.RoleRequired(AppLanguage.Current));

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
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage(_ => CommonMessages.Errors.FullNameRequired(AppLanguage.Current))
            .MaximumLength(200).WithMessage(_ => CommonMessages.Errors.FullNameTooLong(AppLanguage.Current));
        RuleFor(x => x.Role)
            .NotEmpty().WithMessage(_ => UserMessages.Errors.RoleRequired(AppLanguage.Current));

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
    }
}

public sealed class CreateSwimmerRequestValidator : AbstractValidator<CreateSwimmerRequest>
{
    public CreateSwimmerRequestValidator()
    {
        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage(_ => CommonMessages.Errors.FullNameRequired(AppLanguage.Current))
            .MaximumLength(200).WithMessage(_ => CommonMessages.Errors.FullNameTooLong(AppLanguage.Current));
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage(_ => SwimmerMessages.Errors.UsernameRequired(AppLanguage.Current))
            .MaximumLength(100)
            .Matches(@"^[A-Za-z0-9._-]+$").WithMessage(_ => SwimmerMessages.Errors.UsernameRequired(AppLanguage.Current));
        RuleFor(x => x.TrainingClubId)
            .NotEqual(Guid.Empty).WithMessage(_ => SwimmerMessages.Errors.TrainingClubRequired(AppLanguage.Current));
        RuleFor(x => x.GenderId)
            .NotEqual(Guid.Empty).WithMessage(_ => SwimmerMessages.Errors.GenderRequired(AppLanguage.Current));
        RuleFor(x => x.Dob)
            .NotEqual(default(DateOnly)).WithMessage(_ => SwimmerMessages.Errors.DobRequired(AppLanguage.Current))
            .LessThan(_ => DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage(_ => SwimmerMessages.Errors.DobInPast(AppLanguage.Current));
        RuleFor(x => x.StrokeIds)
            .NotEmpty().WithMessage(_ => SwimmerMessages.Errors.SpecializationRequired(AppLanguage.Current))
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage(_ => SwimmerMessages.Errors.UnknownReference(AppLanguage.Current));
        RuleFor(x => x.NameAr).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.NameAr));
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage(_ => CommonMessages.Errors.EmailInvalid(AppLanguage.Current))
            .MaximumLength(256).WithMessage(_ => CommonMessages.Errors.EmailTooLong(AppLanguage.Current))
            .When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Phone)
            .Matches(UserValidationRules.PhonePattern).WithMessage(_ => UserMessages.Errors.PhoneInvalid(AppLanguage.Current))
            .When(x => !string.IsNullOrEmpty(x.Phone));
    }
}

public sealed class CreateCoachRequestValidator : AbstractValidator<CreateCoachRequest>
{
    public CreateCoachRequestValidator()
    {
        RuleFor(x => x.Role)
            .NotEmpty().Must(r => r is "captain" or "head_coach")
            .WithMessage(_ => CoachMessages.Errors.InvalidRole(AppLanguage.Current));
        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage(_ => CommonMessages.Errors.FullNameRequired(AppLanguage.Current))
            .MaximumLength(200).WithMessage(_ => CommonMessages.Errors.FullNameTooLong(AppLanguage.Current));
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage(_ => CoachMessages.Errors.UsernameRequired(AppLanguage.Current))
            .MaximumLength(100).Matches(@"^[A-Za-z0-9._-]+$").WithMessage(_ => CoachMessages.Errors.UsernameRequired(AppLanguage.Current));
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(_ => CommonMessages.Errors.EmailInvalid(AppLanguage.Current))
            .EmailAddress().WithMessage(_ => CommonMessages.Errors.EmailInvalid(AppLanguage.Current))
            .MaximumLength(256).WithMessage(_ => CommonMessages.Errors.EmailTooLong(AppLanguage.Current));
        RuleFor(x => x.NationalId)
            .NotEmpty().Matches(@"^\d{14}$").WithMessage(_ => CoachMessages.Errors.NationalIdInvalid(AppLanguage.Current));
        RuleFor(x => x.GenderId)
            .NotEqual(Guid.Empty).WithMessage(_ => CoachMessages.Errors.GenderRequired(AppLanguage.Current));
        RuleFor(x => x.Dob)
            .NotEqual(default(DateOnly)).WithMessage(_ => CoachMessages.Errors.DobRequired(AppLanguage.Current))
            .LessThan(_ => DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage(_ => CoachMessages.Errors.DobInPast(AppLanguage.Current));
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage(_ => CoachMessages.Errors.PhoneRequired(AppLanguage.Current))
            .Matches(UserValidationRules.PhonePattern).WithMessage(_ => UserMessages.Errors.PhoneInvalid(AppLanguage.Current));
        RuleFor(x => x.NameAr).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.NameAr));
    }
}
