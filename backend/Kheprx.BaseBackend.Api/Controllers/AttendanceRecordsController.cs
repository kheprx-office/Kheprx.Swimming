using Kheprx.BaseBackend.Attendance.Application.DTOs;
using Kheprx.BaseBackend.Attendance.Application.Resources;
using Kheprx.BaseBackend.Attendance.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Route("api/attendance-records")]
[Authorize]
public sealed class AttendanceRecordsController : BaseApiController
{
    private readonly IAttendanceService _service;
    private readonly IUserService _users;

    public AttendanceRecordsController(IAttendanceService service, IUserService users)
    {
        _service = service;
        _users = users;
    }

    /// <summary>Lists a swimmer's attendance records (newest first), with recorder names resolved.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AttendanceRecordDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AttendanceRecordDto>>>> List([FromQuery] Guid swimmerId, CancellationToken ct)
    {
        var rows = await _service.ListBySwimmerAsync(swimmerId, ct);
        var enriched = await EnrichRecorders(rows, ct);
        return Ok(ApiResponse<IReadOnlyList<AttendanceRecordDto>>.Success(
            AttendanceMessages.Success.Listed(AppLanguage.Current), enriched));
    }

    // Resolves recorded_by -> display names via the Identity module (composition at the API layer).
    private async Task<IReadOnlyList<AttendanceRecordDto>> EnrichRecorders(IReadOnlyList<AttendanceRecordDto> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return rows;
        var ids = rows.Select(r => r.RecordedBy).Distinct().ToList();
        var names = await _users.GetDisplayNamesAsync(ids, ct);
        return rows.Select(r => names.TryGetValue(r.RecordedBy, out var n)
            ? r with { RecordedByNameEn = n.NameEn, RecordedByNameAr = n.NameAr }
            : r).ToList();
    }
}
