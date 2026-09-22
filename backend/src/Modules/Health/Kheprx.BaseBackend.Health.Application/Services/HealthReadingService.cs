using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;

namespace Kheprx.BaseBackend.Health.Application.Services;

internal sealed class HealthReadingService : IHealthReadingService
{
    private readonly IHealthReadingRepository _readings;
    private readonly IMedicalTestRepository _tests;

    public HealthReadingService(IHealthReadingRepository readings, IMedicalTestRepository tests)
    {
        _readings = readings;
        _tests = tests;
    }

    public async Task<HealthReadingDto?> CreateAsync(CreateHealthReadingRequest request, Guid recordedBy, CancellationToken ct = default)
    {
        var test = await _tests.GetByIdAsync(request.MedicalTestId, ct);
        if (test is null) return null;

        var reading = new HealthReading(request.SwimmerId, request.MedicalTestId, request.Value, recordedBy);
        await _readings.AddAsync(reading, ct);
        await _readings.SaveChangesAsync(ct);

        return ToDto(reading, DeriveStatus(reading.Value, test));
    }

    public async Task<IReadOnlyList<HealthReadingListItemDto>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default)
    {
        var readings = await _readings.ListBySwimmerAsync(swimmerId, ct);
        var tests = (await _tests.ListAsync(ct)).ToDictionary(t => t.Id);
        return readings.Select(r =>
        {
            tests.TryGetValue(r.MedicalTestId, out var test);
            return ToListItem(r, test);
        }).ToList();
    }

    public async Task<HealthReadingListItemDto?> UpdateAsync(Guid id, UpdateHealthReadingRequest request, CancellationToken ct = default)
    {
        var reading = await _readings.GetTrackedAsync(id, ct);
        if (reading is null) return null;

        reading.Update(request.Value);
        await _readings.SaveChangesAsync(ct);

        var test = await _tests.GetByIdAsync(reading.MedicalTestId, ct);
        return ToListItem(reading, test);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var reading = await _readings.GetTrackedAsync(id, ct);
        if (reading is null) return false;

        _readings.Remove(reading);
        await _readings.SaveChangesAsync(ct);
        return true;
    }

    private static string DeriveStatus(decimal value, MedicalTest test) =>
        value >= test.LowerBound && value <= test.UpperBound ? "normal" : "out";

    private static HealthReadingDto ToDto(HealthReading r, string status) =>
        new(r.Id, r.SwimmerId, r.MedicalTestId, r.Value, r.ReadingDate, r.RecordedBy, status);

    private static HealthReadingListItemDto ToListItem(HealthReading r, MedicalTest? test) => new(
        r.Id,
        r.MedicalTestId,
        test?.NameEn ?? string.Empty,
        test?.NameAr ?? string.Empty,
        test?.Unit ?? string.Empty,
        r.Value,
        test?.LowerBound ?? 0m,
        test?.UpperBound ?? 0m,
        r.ReadingDate,
        test is null ? "unknown" : DeriveStatus(r.Value, test));
}
