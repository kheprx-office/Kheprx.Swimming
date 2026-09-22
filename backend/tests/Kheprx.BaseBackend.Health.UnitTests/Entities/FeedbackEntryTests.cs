using Kheprx.BaseBackend.Health.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Entities;

public class FeedbackEntryTests
{
    [Fact]
    public void Ctor_assigns_fields_author_and_todays_entry_date()
    {
        var swimmerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var e = new FeedbackEntry(swimmerId, 5, categoryId, "Great rhythm", authorId);

        Assert.NotEqual(Guid.Empty, e.Id);
        Assert.Equal(swimmerId, e.SwimmerId);
        Assert.Equal((short)5, e.Rating);
        Assert.Equal(categoryId, e.CategoryId);
        Assert.Equal("Great rhythm", e.Comment);
        Assert.Equal(authorId, e.AuthorId);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), e.EntryDate);
    }

    [Fact]
    public void Update_mutates_rating_category_comment_preserves_author_swimmer_and_date()
    {
        var swimmerId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var e = new FeedbackEntry(swimmerId, 3, Guid.NewGuid(), "ok", authorId);
        var date = e.EntryDate;
        var newCategory = Guid.NewGuid();

        e.Update(4, newCategory, "Improved a lot");

        Assert.Equal((short)4, e.Rating);
        Assert.Equal(newCategory, e.CategoryId);
        Assert.Equal("Improved a lot", e.Comment);
        Assert.Equal(authorId, e.AuthorId);      // preserved
        Assert.Equal(swimmerId, e.SwimmerId);    // preserved
        Assert.Equal(date, e.EntryDate);         // preserved
    }
}
