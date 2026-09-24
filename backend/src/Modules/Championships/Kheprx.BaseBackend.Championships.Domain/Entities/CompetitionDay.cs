namespace Kheprx.BaseBackend.Championships.Domain.Entities;

public sealed class CompetitionDay
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string LabelEn { get; private set; } = string.Empty;
    public string? LabelAr { get; private set; }
    public DateOnly DayDate { get; private set; }

    private CompetitionDay() { } // EF Core

    public CompetitionDay(Guid eventId, string labelEn, string? labelAr, DateOnly dayDate)
    {
        Id = Guid.NewGuid();
        EventId = eventId;
        LabelEn = labelEn.Trim();
        LabelAr = string.IsNullOrWhiteSpace(labelAr) ? null : labelAr.Trim();
        DayDate = dayDate;
    }
}
