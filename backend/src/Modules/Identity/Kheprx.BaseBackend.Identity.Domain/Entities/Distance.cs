namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class Distance
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public int Meters { get; private set; }

    private Distance() { } // EF Core

    public Distance(string code, string nameEn, string? nameAr, int meters)
    {
        Id = Guid.NewGuid();
        Code = code.Trim();
        NameEn = nameEn.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
        Meters = meters;
    }
}
