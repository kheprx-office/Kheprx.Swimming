using Kheprx.BaseBackend.Identity.Application.DTOs;

namespace Kheprx.BaseBackend.Identity.Application.Services.Interfaces;

public interface IReferenceService
{
    Task<IReadOnlyList<ClubDto>> GetClubsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CodedLookupDto>> GetBloodTypesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CodedLookupDto>> GetStrokesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CodedLookupDto>> GetGendersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CodedLookupDto>> GetObservationCategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CodedLookupDto>> GetFitnessAssessmentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CodedLookupDto>> GetFeedbackCategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CodedLookupDto>> GetAttendanceStatusesAsync(CancellationToken ct = default);
}
