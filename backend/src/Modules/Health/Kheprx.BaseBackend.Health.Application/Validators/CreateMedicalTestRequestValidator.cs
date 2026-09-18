using FluentValidation;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Health.Application.Validators;

public sealed class CreateMedicalTestRequestValidator : AbstractValidator<CreateMedicalTestRequest>
{
    public CreateMedicalTestRequestValidator()
    {
        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage(_ => MedicalTestMessages.Errors.NameEnRequired(AppLanguage.Current))
            .MaximumLength(200);
        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage(_ => MedicalTestMessages.Errors.NameArRequired(AppLanguage.Current))
            .MaximumLength(200);
        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage(_ => MedicalTestMessages.Errors.UnitRequired(AppLanguage.Current))
            .MaximumLength(50);
        RuleFor(x => x.UpperBound)
            .GreaterThan(x => x.LowerBound)
            .WithMessage(_ => MedicalTestMessages.Errors.UpperMustExceedLower(AppLanguage.Current));
    }
}
