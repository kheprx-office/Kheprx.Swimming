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

    private static string DeriveStatus(decimal value, MedicalTest test) =>
        value >= test.LowerBound && value <= test.UpperBound ? "normal" : "out";

    private static HealthReadingDto ToDto(HealthReading r, string status) =>
        new(r.Id, r.SwimmerId, r.MedicalTestId, r.Value, r.ReadingDate, r.RecordedBy, status);
}
