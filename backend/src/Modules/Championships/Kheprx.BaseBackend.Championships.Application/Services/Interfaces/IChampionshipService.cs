using Kheprx.BaseBackend.Championships.Application.DTOs;
namespace Kheprx.BaseBackend.Championships.Application.Services.Interfaces;
public interface IChampionshipService
{
    Task<IReadOnlyList<CompetitionEventDto>> ListAsync(CancellationToken ct = default);
    Task<CompetitionEventDto> CreateAsync(CreateCompetitionEventCommand command, CancellationToken ct = default);
}
