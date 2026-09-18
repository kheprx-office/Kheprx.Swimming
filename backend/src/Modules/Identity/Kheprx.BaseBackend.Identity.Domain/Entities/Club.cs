namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class Club
{
    public Guid Id { get; private set; }
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Club() { } // EF Core

    public Club(string nameEn, string? nameAr = null)
    {
        Id = Guid.NewGuid();
        NameEn = nameEn.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
        CreatedAt = DateTime.UtcNow;
    }
}
