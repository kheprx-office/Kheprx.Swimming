using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;

namespace Kheprx.BaseBackend.Health.Application.Services;

internal sealed class FeedbackService : IFeedbackService
{
    private readonly IFeedbackEntryRepository _entries;
    public FeedbackService(IFeedbackEntryRepository entries) => _entries = entries;

    public async Task<IReadOnlyList<FeedbackEntryDto>> ListAsync(Guid swimmerId, CancellationToken ct = default)
    {
        var rows = await _entries.ListBySwimmerAsync(swimmerId, ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<FeedbackEntryDto> CreateAsync(Guid swimmerId, CreateFeedbackEntryRequest request, Guid authorId, CancellationToken ct = default)
    {
        var entry = new FeedbackEntry(swimmerId, request.Rating, request.CategoryId, request.Comment.Trim(), authorId);
        await _entries.AddAsync(entry, ct);
        await _entries.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task<FeedbackEntryDto?> UpdateAsync(Guid swimmerId, Guid entryId, CreateFeedbackEntryRequest request, CancellationToken ct = default)
    {
        var entry = await _entries.GetTrackedAsync(entryId, ct);
        if (entry is null || entry.SwimmerId != swimmerId) return null;

        entry.Update(request.Rating, request.CategoryId, request.Comment.Trim());
        await _entries.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task<bool> DeleteAsync(Guid swimmerId, Guid entryId, CancellationToken ct = default)
    {
        var entry = await _entries.GetTrackedAsync(entryId, ct);
        if (entry is null || entry.SwimmerId != swimmerId) return false;

        _entries.Remove(entry);
        await _entries.SaveChangesAsync(ct);
        return true;
    }

    private static FeedbackEntryDto ToDto(FeedbackEntry e) =>
        new(e.Id, e.SwimmerId, e.Rating, e.CategoryId, e.Comment, e.AuthorId, string.Empty, null, e.EntryDate);
}
