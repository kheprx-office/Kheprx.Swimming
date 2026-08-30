using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Domain.Exceptions;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Authorize(Roles = "admin")]
public sealed class UsersController : BaseApiController
{
    #region Fields

    private readonly IUserService _service;

    #endregion

    #region Constructor

    public UsersController(IUserService service)
    {
        _service = service;
    }

    #endregion

    #region APIs

    #region List — GET api/users — list users, optional search

    // مستخدم في:
    // 1. صفحة إدارة المستخدمين (/user-management) — القائمة + البحث
    /// <summary>Lists users, optionally filtered by a search term (name, email, code, or NID).</summary>
    /// <response code="200">The matching users.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserDto>>>> List(
        [FromQuery] string? search, CancellationToken ct)
    {
        var users = await _service.ListAsync(search, ct);

        var successMessage = UserMessages.Success.UsersListed(AppLanguage.Current);
        var body = ApiResponse<IReadOnlyList<UserDto>>.Success(successMessage, users);
        return Ok(body);
    }

    #endregion

    #region Create — POST api/users — create user with role profile

    // مستخدم في:
    // 1. صفحة إدارة المستخدمين (/user-management) — مودال إضافة مستخدم
    /// <summary>Creates a user with a role-dependent profile.</summary>
    /// <response code="201">User created.</response>
    /// <response code="400">Validation failed; joined messages in the envelope's error field.</response>
    /// <response code="409">Email or NID already taken — error code EMAIL_IN_USE or NID_IN_USE.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<UserDto>>> Create(CreateUserRequest request, CancellationToken ct)
    {
        try
        {
            var user = await _service.CreateAsync(request, ct);

            if (user is null)
            {
                var message = UserMessages.Errors.EmailInUse(AppLanguage.Current);
                var envelope = ApiResponse<UserDto>.Failure(message, "EMAIL_IN_USE");
                return Conflict(envelope);
            }

            var successMessage = UserMessages.Success.UserCreated(AppLanguage.Current);
            var body = ApiResponse<UserDto>.Success(successMessage, user);
            return StatusCode(StatusCodes.Status201Created, body);
        }
        catch (NidInUseException)
        {
            return Conflict(ApiResponse<UserDto>.Failure(UserMessages.Errors.NidInUse(AppLanguage.Current), "NID_IN_USE"));
        }
    }

    #endregion

    #region Update — PATCH api/users/{id} — update details, status, profile

    // مستخدم في:
    // 1. صفحة إدارة المستخدمين (/user-management) — مودال تعديل المستخدم
    /// <summary>Updates a user's details, role, status, and profile.</summary>
    /// <response code="200">User updated.</response>
    /// <response code="400">Validation failed; joined messages in the envelope's error field.</response>
    /// <response code="404">No user with this id — error code USER_NOT_FOUND.</response>
    /// <response code="409">Email or NID already taken — error code EMAIL_IN_USE or NID_IN_USE.</response>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<UserDto>>> Update(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        try
        {
            var user = await _service.UpdateAsync(id, request, ct);

            if (user is null)
            {
                var message = UserMessages.Errors.UserNotFound(AppLanguage.Current);
                var envelope = ApiResponse<UserDto>.Failure(message, "USER_NOT_FOUND");
                return NotFound(envelope);
            }

            var successMessage = UserMessages.Success.UserUpdated(AppLanguage.Current);
            var body = ApiResponse<UserDto>.Success(successMessage, user);
            return Ok(body);
        }
        catch (EmailInUseException)
        {
            return Conflict(ApiResponse<UserDto>.Failure(UserMessages.Errors.EmailInUse(AppLanguage.Current), "EMAIL_IN_USE"));
        }
        catch (NidInUseException)
        {
            return Conflict(ApiResponse<UserDto>.Failure(UserMessages.Errors.NidInUse(AppLanguage.Current), "NID_IN_USE"));
        }
    }

    #endregion

    #region SetStatus — PATCH api/users/{id}/status — activate or deactivate

    // مستخدم في:
    // 1. صفحة إدارة المستخدمين (/user-management) — زر تفعيل/تعطيل بالصف
    /// <summary>Activates or deactivates a user.</summary>
    /// <response code="200">Status updated.</response>
    /// <response code="400">Validation failed; joined messages in the envelope's error field.</response>
    /// <response code="404">No user with this id — error code USER_NOT_FOUND.</response>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserDto>>> SetStatus(Guid id, SetUserStatusRequest request, CancellationToken ct)
    {
        var user = await _service.SetStatusAsync(id, request, ct);

        if (user is null)
        {
            var message = UserMessages.Errors.UserNotFound(AppLanguage.Current);
            var envelope = ApiResponse<UserDto>.Failure(message, "USER_NOT_FOUND");
            return NotFound(envelope);
        }

        var successMessage = UserMessages.Success.StatusUpdated(AppLanguage.Current);
        var body = ApiResponse<UserDto>.Success(successMessage, user);
        return Ok(body);
    }

    #endregion

    #endregion
}
