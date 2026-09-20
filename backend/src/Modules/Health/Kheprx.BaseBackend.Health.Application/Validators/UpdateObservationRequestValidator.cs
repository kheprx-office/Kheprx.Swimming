using FluentValidation;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Health.Application.Validators;

public sealed class UpdateObservationRequestValidator : AbstractValidator<UpdateObservationRequest>
{
    public UpdateObservationRequestValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage(_ => ObservationMessages.Errors.CategoryRequired(AppLanguage.Current));
        RuleFor(x => x.FieldLabel)
            .NotEmpty().WithMessage(_ => ObservationMessages.Errors.FieldLabelRequired(AppLanguage.Current))
            .MaximumLength(200);
        RuleFor(x => x.Value)
            .NotEmpty().WithMessage(_ => ObservationMessages.Errors.ValueRequired(AppLanguage.Current))
            .MaximumLength(500);
    }
}
