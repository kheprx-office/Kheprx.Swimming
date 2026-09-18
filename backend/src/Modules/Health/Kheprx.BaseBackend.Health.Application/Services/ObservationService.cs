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

    private static ObservationDto ToDto(Observation o) =>
        new(o.Id, o.SwimmerId, o.CategoryId, o.FieldLabel, o.Value, o.ObservedDate, o.RecordedBy);
}
