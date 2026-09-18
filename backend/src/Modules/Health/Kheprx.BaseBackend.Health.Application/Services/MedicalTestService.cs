using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;

namespace Kheprx.BaseBackend.Health.Application.Services;

internal sealed class MedicalTestService : IMedicalTestService
{
    private readonly IMedicalTestRepository _tests;
    public MedicalTestService(IMedicalTestRepository tests) => _tests = tests;

    public async Task<IReadOnlyList<MedicalTestDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await _tests.ListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<MedicalTestDto> CreateAsync(CreateMedicalTestRequest request, Guid createdBy, CancellationToken ct = default)
    {
        var test = new MedicalTest(request.NameEn, request.NameAr, request.Unit,
            request.LowerBound, request.UpperBound, createdBy);
        await _tests.AddAsync(test, ct);
        await _tests.SaveChangesAsync(ct);
        return ToDto(test);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var test = await _tests.GetByIdAsync(id, ct);
        if (test is null) return false;
        await _tests.RemoveAsync(test, ct);
        await _tests.SaveChangesAsync(ct);
        return true;
    }

    private static MedicalTestDto ToDto(MedicalTest t) =>
        new(t.Id, t.NameEn, t.NameAr, t.Unit, t.LowerBound, t.UpperBound, t.CreatedAt);
}
