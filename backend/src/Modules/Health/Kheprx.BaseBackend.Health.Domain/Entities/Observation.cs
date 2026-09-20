namespace Kheprx.BaseBackend.Health.Domain.Entities;

public sealed class Observation
{
    public Guid Id { get; private set; }
    public Guid SwimmerId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string FieldLabel { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public DateTime ObservedDate { get; private set; }
    public Guid RecordedBy { get; private set; }

    private Observation() { } // EF Core

    public Observation(Guid swimmerId, Guid categoryId, string fieldLabel, string value, Guid recordedBy)
    {
        Id = Guid.NewGuid();
        SwimmerId = swimmerId;
        CategoryId = categoryId;
        FieldLabel = fieldLabel.Trim();
        Value = value.Trim();
        ObservedDate = DateTime.UtcNow;
        RecordedBy = recordedBy;
    }

    public void Update(Guid categoryId, string fieldLabel, string value)
    {
        CategoryId = categoryId;
        FieldLabel = fieldLabel.Trim();
        Value = value.Trim();
    }
}
