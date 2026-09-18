using FluentValidation;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Identity.Application.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(_ => CommonMessages.Errors.EmailRequired(AppLanguage.Current))
            .EmailAddress().WithMessage(_ => CommonMessages.Errors.EmailInvalid(AppLanguage.Current))
            .MaximumLength(256).WithMessage(_ => CommonMessages.Errors.EmailTooLong(AppLanguage.Current));
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(_ => CommonMessages.Errors.PasswordRequired(AppLanguage.Current));
        RuleFor(x => x.Role)
            .NotEmpty().WithMessage(_ => AuthMessages.Errors.RoleRequired(AppLanguage.Current));
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage(_ => AuthMessages.Errors.RefreshTokenRequired(AppLanguage.Current));
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage(_ => AuthMessages.Errors.CurrentPasswordRequired(AppLanguage.Current));
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage(_ => AuthMessages.Errors.NewPasswordRequired(AppLanguage.Current))
            .MinimumLength(8).WithMessage(_ => CommonMessages.Errors.PasswordMinLength(AppLanguage.Current))
            .MaximumLength(256).WithMessage(_ => CommonMessages.Errors.PasswordTooLong(AppLanguage.Current));
    }
}
