using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.Health.Application.Validators;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Validators;

public class CreateFeedbackEntryRequestValidatorTests
{
    private readonly CreateFeedbackEntryRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
        => Assert.True(_validator.Validate(new CreateFeedbackEntryRequest(5, Guid.NewGuid(), "Great rhythm")).IsValid);

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)6)]
    public void Fails_for_out_of_range_rating(short rating)
    {
        var result = _validator.Validate(new CreateFeedbackEntryRequest(rating, Guid.NewGuid(), "ok"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Rating");
    }

    [Fact]
    public void Fails_for_blank_comment()
    {
        var result = _validator.Validate(new CreateFeedbackEntryRequest(3, Guid.NewGuid(), "   "));
        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors, e => e.PropertyName == "Comment");
        Assert.Equal(FeedbackMessages.Errors.CommentRequired(AppLanguage.Current), error.ErrorMessage);
    }

    [Fact]
    public void Fails_for_comment_exceeding_max_length()
    {
        var result = _validator.Validate(new CreateFeedbackEntryRequest(3, Guid.NewGuid(), new string('x', 1001)));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Comment");
    }

    [Fact]
    public void Fails_for_empty_category()
    {
        var result = _validator.Validate(new CreateFeedbackEntryRequest(3, Guid.Empty, "ok"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "CategoryId");
    }
}
