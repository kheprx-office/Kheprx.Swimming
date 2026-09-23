using Kheprx.BaseBackend.Championships.Application.DTOs;
using Kheprx.BaseBackend.Championships.Application.Services.Interfaces;
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;

namespace Kheprx.BaseBackend.Championships.Application.Services;

internal sealed class ChampionshipService : IChampionshipService
{
    private readonly ICompetitionEventRepository _events;
    public ChampionshipService(ICompetitionEventRepository events) => _events = events;

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

    private static CompetitionEventDto ToDto(CompetitionEvent e) =>
        new(e.Id, e.NameEn, e.NameAr, e.StartDate, e.EndDate, e.LocationEn, e.LocationAr,
            e.StatusId, string.Empty, string.Empty, null);
}
