using FluentValidation;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Health.Application.Validators;

public sealed class CompleteInBodyRequestValidator : AbstractValidator<CompleteInBodyRequest>
{
    public CompleteInBodyRequestValidator()
    {
        RuleFor(x => x.HeightCm).GreaterThan(0m).LessThanOrEqualTo(999.9m).WithMessage(_ => InBodyReadingMessages.Errors.ValueInvalid(AppLanguage.Current));
        RuleFor(x => x.WeightKg).GreaterThan(0m).LessThanOrEqualTo(999.9m).WithMessage(_ => InBodyReadingMessages.Errors.ValueInvalid(AppLanguage.Current));
        RuleFor(x => x.FatPct).InclusiveBetween(0m, 100m).WithMessage(_ => InBodyReadingMessages.Errors.ValueInvalid(AppLanguage.Current));
        RuleFor(x => x.MusclePct).InclusiveBetween(0m, 100m).WithMessage(_ => InBodyReadingMessages.Errors.ValueInvalid(AppLanguage.Current));
        RuleFor(x => x.WaterPct).InclusiveBetween(0m, 100m).WithMessage(_ => InBodyReadingMessages.Errors.ValueInvalid(AppLanguage.Current));
        RuleFor(x => x.BoneDensity).GreaterThan(0m).LessThanOrEqualTo(99.99m).WithMessage(_ => InBodyReadingMessages.Errors.ValueInvalid(AppLanguage.Current));
        RuleFor(x => x.BodyDensity).GreaterThan(0m).LessThanOrEqualTo(99.99m).WithMessage(_ => InBodyReadingMessages.Errors.ValueInvalid(AppLanguage.Current));
    }
}
