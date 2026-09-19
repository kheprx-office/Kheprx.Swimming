using FluentValidation;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Identity.Application.Validators;

public sealed class CreateBodyMeasurementRequestValidator : AbstractValidator<CreateBodyMeasurementRequest>
{
    public CreateBodyMeasurementRequestValidator()
    {
        Rule(x => x.RightArmCm);
        Rule(x => x.LeftArmCm);
        Rule(x => x.RightLegCm);
        Rule(x => x.LeftLegCm);
        Rule(x => x.TorsoCm);
        Rule(x => x.BustDiameterCm);
        Rule(x => x.WaistDiameterCm);
    }

    private void Rule(System.Linq.Expressions.Expression<System.Func<CreateBodyMeasurementRequest, decimal>> selector)
        => RuleFor(selector)
            .GreaterThan(0m)
            .LessThanOrEqualTo(999.9m)
            .WithMessage(_ => SwimmerMessages.Errors.MeasurementInvalid(AppLanguage.Current));
}
