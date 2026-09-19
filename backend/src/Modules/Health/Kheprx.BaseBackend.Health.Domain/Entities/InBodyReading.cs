namespace Kheprx.BaseBackend.Health.Domain.Entities;

public sealed class InBodyReading
{
    public Guid Id { get; private set; }
    public Guid SwimmerId { get; private set; }
    public DateOnly ReadingDate { get; private set; }
    public decimal HeightCm { get; private set; }
    public decimal WeightKg { get; private set; }
    public decimal FatPct { get; private set; }
    public decimal MusclePct { get; private set; }
    public decimal WaterPct { get; private set; }
    public decimal BoneDensity { get; private set; }
    public decimal BodyDensity { get; private set; }
    public Guid RecordedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private InBodyReading() { } // EF Core

    public InBodyReading(Guid swimmerId, DateOnly readingDate, decimal heightCm, decimal weightKg,
        decimal fatPct, decimal musclePct, decimal waterPct, decimal boneDensity, decimal bodyDensity, Guid recordedBy)
    {
        Id = Guid.NewGuid();
        SwimmerId = swimmerId;
        ReadingDate = readingDate;
        HeightCm = heightCm;
        WeightKg = weightKg;
        FatPct = fatPct;
        MusclePct = musclePct;
        WaterPct = waterPct;
        BoneDensity = boneDensity;
        BodyDensity = bodyDensity;
        RecordedBy = recordedBy;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(DateOnly readingDate, decimal heightCm, decimal weightKg,
        decimal fatPct, decimal musclePct, decimal waterPct, decimal boneDensity, decimal bodyDensity)
    {
        ReadingDate = readingDate;
        HeightCm = heightCm;
        WeightKg = weightKg;
        FatPct = fatPct;
        MusclePct = musclePct;
        WaterPct = waterPct;
        BoneDensity = boneDensity;
        BodyDensity = bodyDensity;
    }
}
