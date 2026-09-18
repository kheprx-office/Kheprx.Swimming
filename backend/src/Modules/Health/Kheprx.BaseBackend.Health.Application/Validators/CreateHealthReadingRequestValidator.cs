using FluentValidation;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Health.Application.Validators;

public sealed class CreateHealthReadingRequestValidator : AbstractValidator<CreateHealthReadingRequest>
{
    public CreateHealthReadingRequestValidator()
    {
        RuleFor(x => x.SwimmerId)
            .NotEmpty().WithMessage(_ => HealthReadingMessages.Errors.SwimmerRequired(AppLanguage.Current));
        RuleFor(x => x.MedicalTestId)
            .NotEmpty().WithMessage(_ => HealthReadingMessages.Errors.TestRequired(AppLanguage.Current));
    }
}
