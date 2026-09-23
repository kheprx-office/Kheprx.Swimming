namespace Kheprx.BaseBackend.Championships.Domain.Entities;

public sealed class CompetitionEvent
{
    public Guid Id { get; private set; }
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string LocationEn { get; private set; } = string.Empty;
    public string? LocationAr { get; private set; }
    public Guid StatusId { get; private set; }
    public Guid CreatedBy { get; private set; }

    private CompetitionEvent() { } // EF Core

    public CompetitionEvent(string nameEn, string? nameAr, DateOnly startDate, DateOnly endDate,
        string locationEn, string? locationAr, Guid statusId, Guid createdBy)
    {
        Id = Guid.NewGuid();
        NameEn = nameEn.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
        StartDate = startDate;
        EndDate = endDate;
        LocationEn = locationEn.Trim();
        LocationAr = string.IsNullOrWhiteSpace(locationAr) ? null : locationAr.Trim();
        StatusId = statusId;
        CreatedBy = createdBy;
    }
}
