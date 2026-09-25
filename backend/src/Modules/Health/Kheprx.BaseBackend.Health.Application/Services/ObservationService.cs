using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;

namespace Kheprx.BaseBackend.Health.Application.Services;

internal sealed class ObservationService : IObservationService
{
    private readonly IObservationRepository _observations;
    public ObservationService(IObservationRepository observations) => _observations = observations;

    public async Task<ObservationDto> CreateAsync(CreateObservationRequest request, Guid recordedBy, CancellationToken ct = default)
    {
        var observation = new Observation(request.SwimmerId, request.CategoryId, request.FieldLabel, request.Value, recordedBy);
        await _observations.AddAsync(observation, ct);
        await _observations.SaveChangesAsync(ct);
        return ToDto(observation);
    }

    public async Task<IReadOnlyList<ObservationDto>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default)
    {
        var rows = await _observations.ListBySwimmerAsync(swimmerId, ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ObservationDto?> UpdateAsync(Guid id, UpdateObservationRequest request, CancellationToken ct = default)
    {
        var o = await _observations.GetTrackedAsync(id, ct);
        if (o is null) return null;

        o.Update(request.CategoryId, request.FieldLabel, request.Value);
        await _observations.SaveChangesAsync(ct);
        return ToDto(o);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var o = await _observations.GetTrackedAsync(id, ct);
        if (o is null) return false;

        _observations.Remove(o);
        await _observations.SaveChangesAsync(ct);
        return true;
    }

    public async Task ReplaceForSwimmerAsync(Guid swimmerId, IReadOnlyList<CreateObservationRequest> items, Guid recordedBy, CancellationToken ct = default)
    {
        // Idempotent per-step onboarding save: remove the swimmer's observations, then insert the current set.
        var existing = await _observations.ListBySwimmerTrackedAsync(swimmerId, ct);
        _observations.RemoveRange(existing);
        foreach (var item in items)
            await _observations.AddAsync(new Observation(item.SwimmerId, item.CategoryId, item.FieldLabel, item.Value, recordedBy), ct);
        await _observations.SaveChangesAsync(ct);
    }

    private static ObservationDto ToDto(Observation o) =>
        new(o.Id, o.SwimmerId, o.CategoryId, o.FieldLabel, o.Value, o.ObservedDate, o.RecordedBy);
}
