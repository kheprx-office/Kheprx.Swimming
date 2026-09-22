using FluentValidation;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Health.Application.Validators;

public sealed class UpdateHealthReadingRequestValidator : AbstractValidator<UpdateHealthReadingRequest>
{
    public UpdateHealthReadingRequestValidator()
    {
        RuleFor(x => x.Value)
            .GreaterThan(0m).WithMessage(_ => HealthReadingMessages.Errors.ValuePositive(AppLanguage.Current));
    }
}
