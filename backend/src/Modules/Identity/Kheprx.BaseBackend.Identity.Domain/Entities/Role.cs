namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class Role
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string? LabelAr { get; private set; }
    public string? LabelEn { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Role() { } // EF Core

    public Role(string code, string? labelAr = null, string? labelEn = null, int sortOrder = 0)
    {
        Id = Guid.NewGuid();
        Code = code.Trim();
        LabelAr = labelAr;
        LabelEn = labelEn;
        SortOrder = sortOrder;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }
}
