namespace Kheprx.BaseBackend.Health.Domain.Entities;

public sealed class MedicalTest
{
    public Guid Id { get; private set; }
    public string NameEn { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;
    public string Unit { get; private set; } = string.Empty;
    public decimal LowerBound { get; private set; }
    public decimal UpperBound { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private MedicalTest() { } // EF Core

    public MedicalTest(string nameEn, string nameAr, string unit,
        decimal lowerBound, decimal upperBound, Guid createdBy)
    {
        Id = Guid.NewGuid();
        NameEn = nameEn.Trim();
        NameAr = nameAr.Trim();
        Unit = unit.Trim();
        LowerBound = lowerBound;
        UpperBound = upperBound;
        CreatedBy = createdBy;
        CreatedAt = DateTime.UtcNow;
    }
}
