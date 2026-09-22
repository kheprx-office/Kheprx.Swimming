using FluentValidation;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Health.Application.Validators;

public sealed class CreateFeedbackEntryRequestValidator : AbstractValidator<CreateFeedbackEntryRequest>
{
    public CreateFeedbackEntryRequestValidator()
    {
        RuleFor(x => x.Rating)
            .InclusiveBetween((short)1, (short)5)
            .WithMessage(_ => FeedbackMessages.Errors.RatingInvalid(AppLanguage.Current));
        RuleFor(x => x.Comment)
            .NotEmpty()
            .WithMessage(_ => FeedbackMessages.Errors.CommentRequired(AppLanguage.Current))
            .MaximumLength(1000)
            .WithMessage(_ => FeedbackMessages.Errors.CommentRequired(AppLanguage.Current));
        RuleFor(x => x.CategoryId)
            .NotEqual(Guid.Empty)
            .WithMessage(_ => FeedbackMessages.Errors.CategoryRequired(AppLanguage.Current));
    }
}
