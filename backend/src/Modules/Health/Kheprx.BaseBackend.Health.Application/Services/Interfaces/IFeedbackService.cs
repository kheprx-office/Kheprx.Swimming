using Kheprx.BaseBackend.Health.Application.DTOs;

namespace Kheprx.BaseBackend.Health.Application.Services.Interfaces;

public interface IFeedbackService
{
    Task<IReadOnlyList<FeedbackEntryDto>> ListAsync(Guid swimmerId, CancellationToken ct = default);
    Task<FeedbackEntryDto> CreateAsync(Guid swimmerId, CreateFeedbackEntryRequest request, Guid authorId, CancellationToken ct = default);
    Task<FeedbackEntryDto?> UpdateAsync(Guid swimmerId, Guid entryId, CreateFeedbackEntryRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid swimmerId, Guid entryId, CancellationToken ct = default);
}
