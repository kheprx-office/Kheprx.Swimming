using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Route("api/swimmers/{id:guid}/feedback-entries")]
public sealed class FeedbackEntriesController : BaseApiController
{
    private readonly IFeedbackService _service;
    private readonly IUserService _users;

    public FeedbackEntriesController(IFeedbackService service, IUserService users)
    {
        _service = service;
        _users = users;
    }

    /// <summary>Lists a swimmer's feedback entries, newest first, with author names resolved.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FeedbackEntryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FeedbackEntryDto>>>> List(Guid id, CancellationToken ct)
    {
        var rows = await _service.ListAsync(id, ct);
        var enriched = await EnrichAuthors(rows, ct);
        return Ok(ApiResponse<IReadOnlyList<FeedbackEntryDto>>.Success(FeedbackMessages.Success.Listed(AppLanguage.Current), enriched));
    }

    /// <summary>Records new feedback. Head Coach or Captain only.</summary>
    [HttpPost]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<FeedbackEntryDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<FeedbackEntryDto>>> Create(Guid id, CreateFeedbackEntryRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(id, request, CurrentUserId(), ct);
        var enriched = (await EnrichAuthors(new[] { created }, ct))[0];
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<FeedbackEntryDto>.Success(FeedbackMessages.Success.Created(AppLanguage.Current), enriched));
    }

    /// <summary>Updates a feedback entry. Head Coach or Captain only.</summary>
    [HttpPut("{entryId:guid}")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<FeedbackEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FeedbackEntryDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<FeedbackEntryDto>>> Update(Guid id, Guid entryId, CreateFeedbackEntryRequest request, CancellationToken ct)
    {
        var updated = await _service.UpdateAsync(id, entryId, request, ct);
        if (updated is null)
        {
            var nf = ApiResponse<FeedbackEntryDto>.Failure(FeedbackMessages.Errors.NotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        var enriched = (await EnrichAuthors(new[] { updated }, ct))[0];
        return Ok(ApiResponse<FeedbackEntryDto>.Success(FeedbackMessages.Success.Updated(AppLanguage.Current), enriched));
    }

    /// <summary>Deletes a feedback entry. Head Coach or Captain only.</summary>
    [HttpDelete("{entryId:guid}")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, Guid entryId, CancellationToken ct)
    {
        var deleted = await _service.DeleteAsync(id, entryId, ct);
        if (!deleted)
        {
            var nf = ApiResponse<object>.Failure(FeedbackMessages.Errors.NotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<object>.Success(FeedbackMessages.Success.Deleted(AppLanguage.Current), null));
    }

    // Resolves author_id -> display names via the Identity module (composition at the API layer).
    private async Task<IReadOnlyList<FeedbackEntryDto>> EnrichAuthors(IReadOnlyList<FeedbackEntryDto> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return rows;
        var ids = rows.Select(r => r.AuthorId).Distinct().ToList();
        var names = await _users.GetDisplayNamesAsync(ids, ct);
        return rows.Select(r => names.TryGetValue(r.AuthorId, out var n)
            ? r with { AuthorNameEn = n.NameEn, AuthorNameAr = n.NameAr }
            : r).ToList();
    }
}
