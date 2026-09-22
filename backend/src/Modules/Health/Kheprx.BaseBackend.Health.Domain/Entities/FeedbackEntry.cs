namespace Kheprx.BaseBackend.Health.Domain.Entities;

public sealed class FeedbackEntry
{
    public Guid Id { get; private set; }
    public Guid SwimmerId { get; private set; }
    public short Rating { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Comment { get; private set; } = string.Empty;
    public Guid AuthorId { get; private set; }
    public DateOnly EntryDate { get; private set; }

    private FeedbackEntry() { } // EF Core

    public FeedbackEntry(Guid swimmerId, short rating, Guid categoryId, string comment, Guid authorId)
    {
        Id = Guid.NewGuid();
        SwimmerId = swimmerId;
        Rating = rating;
        CategoryId = categoryId;
        Comment = comment;
        AuthorId = authorId;
        EntryDate = DateOnly.FromDateTime(DateTime.UtcNow);
    }

    public void Update(short rating, Guid categoryId, string comment)
    {
        Rating = rating;
        CategoryId = categoryId;
        Comment = comment;
    }
}
