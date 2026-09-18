namespace Kheprx.BaseBackend.Health.Domain.Entities;

public sealed class HealthReading
{
    public Guid Id { get; private set; }
    public Guid SwimmerId { get; private set; }
    public Guid MedicalTestId { get; private set; }
    public decimal Value { get; private set; }
    public DateTime ReadingDate { get; private set; }
    public Guid RecordedBy { get; private set; }

    private HealthReading() { } // EF Core

    public HealthReading(Guid swimmerId, Guid medicalTestId, decimal value, Guid recordedBy)
    {
        Id = Guid.NewGuid();
        SwimmerId = swimmerId;
        MedicalTestId = medicalTestId;
        Value = value;
        ReadingDate = DateTime.UtcNow;
        RecordedBy = recordedBy;
    }
}
