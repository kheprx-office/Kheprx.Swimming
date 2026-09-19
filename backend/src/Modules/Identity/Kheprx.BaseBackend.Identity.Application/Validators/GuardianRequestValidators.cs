using FluentValidation;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Identity.Application.Validators;

public sealed class UpsertGuardiansRequestValidator : AbstractValidator<UpsertGuardiansRequest>
{
    public UpsertGuardiansRequestValidator()
    {
        RuleFor(x => x.Father).NotNull().SetValidator(new GuardianInputDtoValidator());
        RuleFor(x => x.Mother).NotNull().SetValidator(new GuardianInputDtoValidator());
    }
}

internal sealed class GuardianInputDtoValidator : AbstractValidator<GuardianInputDto>
{
    public GuardianInputDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(_ => CommonMessages.Errors.FullNameRequired(AppLanguage.Current))
            .MaximumLength(200).WithMessage(_ => CommonMessages.Errors.FullNameTooLong(AppLanguage.Current));
        RuleFor(x => x.NationalId)
            .NotEmpty().Matches(@"^\d{14}$").WithMessage(_ => CoachMessages.Errors.NationalIdInvalid(AppLanguage.Current));
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage(_ => CoachMessages.Errors.PhoneRequired(AppLanguage.Current));
    }
}
