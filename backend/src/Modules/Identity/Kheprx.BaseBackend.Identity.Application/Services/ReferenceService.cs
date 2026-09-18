using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Domain.Repositories;

namespace Kheprx.BaseBackend.Identity.Application.Services;

internal sealed class ReferenceService : IReferenceService
{
    private readonly IClubRepository _clubs;
    private readonly IBloodTypeRepository _bloodTypes;
    private readonly IStrokeRepository _strokes;
    private readonly IGenderRepository _genders;
    private readonly IObservationCategoryRepository _categories;

    public ReferenceService(
        IClubRepository clubs, IBloodTypeRepository bloodTypes,
        IStrokeRepository strokes, IGenderRepository genders,
        IObservationCategoryRepository categories)
    {
        _clubs = clubs;
        _bloodTypes = bloodTypes;
        _strokes = strokes;
        _genders = genders;
        _categories = categories;
    }

    public async Task<IReadOnlyList<ClubDto>> GetClubsAsync(CancellationToken ct = default)
        => (await _clubs.GetAllAsync(ct)).Select(c => new ClubDto(c.Id, c.NameEn, c.NameAr)).ToList();

    public async Task<IReadOnlyList<CodedLookupDto>> GetBloodTypesAsync(CancellationToken ct = default)
        => (await _bloodTypes.GetAllAsync(ct)).Select(b => new CodedLookupDto(b.Id, b.Code, b.NameEn, b.NameAr)).ToList();

    public async Task<IReadOnlyList<CodedLookupDto>> GetStrokesAsync(CancellationToken ct = default)
        => (await _strokes.GetAllAsync(ct)).Select(s => new CodedLookupDto(s.Id, s.Code, s.NameEn, s.NameAr)).ToList();

    public async Task<IReadOnlyList<CodedLookupDto>> GetGendersAsync(CancellationToken ct = default)
        => (await _genders.GetAllAsync(ct)).Select(g => new CodedLookupDto(g.Id, g.Code, g.NameEn, g.NameAr)).ToList();

    public async Task<IReadOnlyList<CodedLookupDto>> GetObservationCategoriesAsync(CancellationToken ct = default)
        => (await _categories.GetAllAsync(ct)).Select(c => new CodedLookupDto(c.Id, c.Code, c.NameEn, c.NameAr)).ToList();
}
