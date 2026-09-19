using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;

namespace Kheprx.BaseBackend.Health.Application.Services;

internal sealed class InBodyReadingService : IInBodyReadingService
{
    private readonly IInBodyReadingRepository _readings;
    public InBodyReadingService(IInBodyReadingRepository readings) => _readings = readings;

    public async Task<IReadOnlyList<InBodyReadingDto>> ListAsync(Guid swimmerId, CancellationToken ct = default)
    {
        var rows = await _readings.ListBySwimmerAsync(swimmerId, ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<InBodyReadingDto> CreateAsync(Guid swimmerId, CreateInBodyReadingRequest request, Guid recordedBy, CancellationToken ct = default)
    {
        var reading = new InBodyReading(swimmerId, request.ReadingDate, request.HeightCm, request.WeightKg,
            request.FatPct, request.MusclePct, request.BoneDensity, request.BodyDensity, recordedBy);
        await _readings.AddAsync(reading, ct);
        await _readings.SaveChangesAsync(ct);
        return ToDto(reading);
    }

    public async Task<InBodyReadingDto?> UpdateAsync(Guid swimmerId, Guid readingId, CreateInBodyReadingRequest request, CancellationToken ct = default)
    {
        var reading = await _readings.GetTrackedAsync(readingId, ct);
        if (reading is null || reading.SwimmerId != swimmerId) return null;

        reading.Update(request.ReadingDate, request.HeightCm, request.WeightKg,
            request.FatPct, request.MusclePct, request.BoneDensity, request.BodyDensity);
        await _readings.SaveChangesAsync(ct);
        return ToDto(reading);
    }

    public async Task<bool> DeleteAsync(Guid swimmerId, Guid readingId, CancellationToken ct = default)
    {
        var reading = await _readings.GetTrackedAsync(readingId, ct);
        if (reading is null || reading.SwimmerId != swimmerId) return false;

        _readings.Remove(reading);
        await _readings.SaveChangesAsync(ct);
        return true;
    }

    private static InBodyReadingDto ToDto(InBodyReading r) =>
        new(r.Id, r.ReadingDate, r.HeightCm, r.WeightKg, r.FatPct, r.MusclePct, r.BoneDensity, r.BodyDensity, r.RecordedBy);
}
