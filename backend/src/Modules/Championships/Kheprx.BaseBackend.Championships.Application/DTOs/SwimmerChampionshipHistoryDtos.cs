namespace Kheprx.BaseBackend.Championships.Application.DTOs;

/// <summary>One race the swimmer swam in a championship (name resolved client-side from the ids).</summary>
public sealed record ChampionshipSwimmerRaceDto(
    string DayLabelEn,
    string? DayLabelAr,
    Guid DistanceId,
    Guid StrokeId,
    int TimeMs,
    bool IsPersonalBest);

/// <summary>A championship the swimmer joined, with the swimmer's races (empty when none recorded yet).</summary>
public sealed record ChampionshipSwimmerHistoryDto(
    Guid EventId,
    string NameEn,
    string? NameAr,
    DateOnly StartDate,
    DateOnly EndDate,
    string LocationEn,
    string? LocationAr,
    IReadOnlyList<ChampionshipSwimmerRaceDto> Races);
