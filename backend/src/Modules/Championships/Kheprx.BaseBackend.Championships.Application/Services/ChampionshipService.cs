using Kheprx.BaseBackend.Championships.Application.DTOs;
using Kheprx.BaseBackend.Championships.Application.Services.Interfaces;
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;

namespace Kheprx.BaseBackend.Championships.Application.Services;

internal sealed class ChampionshipService : IChampionshipService
{
    private readonly ICompetitionEventRepository _events;
    private readonly IChampionshipEnrollmentRepository _enrollments;

    public ChampionshipService(ICompetitionEventRepository events, IChampionshipEnrollmentRepository enrollments)
    {
        _events = events;
        _enrollments = enrollments;
    }

    public async Task<IReadOnlyList<CompetitionEventDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await _events.ListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<CompetitionEventDto> CreateAsync(CreateCompetitionEventCommand c, CancellationToken ct = default)
    {
        var e = new CompetitionEvent(c.NameEn, c.NameAr, c.StartDate, c.EndDate, c.LocationEn, c.LocationAr, c.StatusId, c.CreatedBy);
        await _events.AddAsync(e, ct);
        return ToDto(e);
    }

    public async Task<CompetitionEventDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _events.GetByIdAsync(id, ct);
        return e is null ? null : ToDto(e);
    }

    public async Task<IReadOnlyList<Guid>?> GetEnrolledSwimmerIdsAsync(Guid eventId, CancellationToken ct = default)
    {
        var e = await _events.GetByIdAsync(eventId, ct);
        if (e is null) return null;
        return await _enrollments.ListSwimmerIdsAsync(eventId, ct);
    }

    public async Task<bool> SetEnrollmentsAsync(Guid eventId, IReadOnlyList<Guid> swimmerIds, CancellationToken ct = default)
    {
        var e = await _events.GetByIdAsync(eventId, ct);
        if (e is null) return false;
        await _enrollments.ReplaceAsync(eventId, swimmerIds, ct);
        return true;
    }

    private static CompetitionEventDto ToDto(CompetitionEvent e) =>
        new(e.Id, e.NameEn, e.NameAr, e.StartDate, e.EndDate, e.LocationEn, e.LocationAr,
            e.StatusId, string.Empty, string.Empty, null);
}
