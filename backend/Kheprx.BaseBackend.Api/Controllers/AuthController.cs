using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

public sealed class AuthController : BaseApiController
{
    #region Fields

    private readonly IAuthService _service;

    #endregion

    #region Constructor

    public AuthController(IAuthService service)
    {
        _service = service;
    }

    #endregion

    #region APIs

    #region Login — POST api/auth/login — sign in with email and password

    // مستخدم في:
    // 1. صفحة تسجيل الدخول (/login)
    /// <summary>Authenticates a user with email and password and issues a token pair.</summary>
    /// <response code="200">Signed in; returns the session.</response>
    /// <response code="400">Validation failed; joined messages in the envelope's error field.</response>
    /// <response code="401">Wrong email or password — error code INVALID_CREDENTIALS.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<SessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SessionDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<SessionDto>>> Login(LoginRequest request, CancellationToken ct)
    {
        var session = await _service.LoginAsync(request, ct);

        if (session is null)
        {
            var message = AuthMessages.Errors.InvalidCredentials(AppLanguage.Current);
            var envelope = ApiResponse<SessionDto>.Failure(message, "INVALID_CREDENTIALS");
            return Unauthorized(envelope);
        }

        var successMessage = AuthMessages.Success.SignedIn(AppLanguage.Current);
        var body = ApiResponse<SessionDto>.Success(successMessage, session);
        return Ok(body);
    }

    #endregion

    #region Refresh — POST api/auth/refresh — exchange refresh token for a new pair

    // مستخدم في:
    // 1. (على مستوى التطبيق) — معترض HTTP (تجديد الرمز عند 401)
    /// <summary>Exchanges a refresh token for a new access/refresh token pair.</summary>
    /// <response code="200">New session issued; the old refresh token is invalidated.</response>
    /// <response code="400">Validation failed; joined messages in the envelope's error field.</response>
    /// <response code="401">Refresh token unknown, expired, or revoked — error code INVALID_REFRESH_TOKEN.</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<SessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SessionDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<SessionDto>>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var session = await _service.RefreshAsync(request, ct);

        if (session is null)
        {
            var message = AuthMessages.Errors.InvalidRefreshToken(AppLanguage.Current);
            var envelope = ApiResponse<SessionDto>.Failure(message, "INVALID_REFRESH_TOKEN");
            return Unauthorized(envelope);
        }

        var successMessage = AuthMessages.Success.TokenRefreshed(AppLanguage.Current);
        var body = ApiResponse<SessionDto>.Success(successMessage, session);
        return Ok(body);
    }

    #endregion

    #region Logout — POST api/auth/logout — sign out, revoke refresh tokens

    // مستخدم في:
    // 1. (على مستوى التطبيق) — زر تسجيل الخروج في الشريط الجانبي
    /// <summary>Signs the current user out and revokes their refresh token.</summary>
    /// <response code="200">Signed out; data is null.</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Logout(CancellationToken ct) // AD-009: no body
    {
        await _service.LogoutAsync(CurrentUserId(), ct);

        var successMessage = AuthMessages.Success.SignedOut(AppLanguage.Current);
        var body = ApiResponse<object>.Success(successMessage, null);
        return Ok(body);
    }

    #endregion

    #region Me — GET api/auth/me — current user profile

    // مستخدم في:
    // 1. (على مستوى التطبيق) — تهيئة الجلسة واسم المستخدم في الترويسة (AuthSessionStore)
    // 2. صفحة حسابي (/account)
    /// <summary>Returns the profile of the currently authenticated user.</summary>
    /// <response code="200">The current user.</response>
    /// <response code="401">Token valid but user no longer resolvable — error code NOT_AUTHENTICATED.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CurrentUserDto>>> Me(CancellationToken ct)
    {
        var user = await _service.GetCurrentUserAsync(CurrentUserId(), ct);

        if (user is null)
        {
            var message = AuthMessages.Errors.NotAuthenticated(AppLanguage.Current);
            var envelope = ApiResponse<CurrentUserDto>.Failure(message, "NOT_AUTHENTICATED");
            return Unauthorized(envelope);
        }

        var successMessage = AuthMessages.Success.CurrentUser(AppLanguage.Current);
        var body = ApiResponse<CurrentUserDto>.Success(successMessage, user);
        return Ok(body);
    }

    #endregion

    #region ChangePassword — POST api/auth/change-password — change password, issue fresh session

    // مستخدم في:
    // 1. صفحة تغيير كلمة المرور (/change-password)
    /// <summary>Changes the current user's password and issues a fresh session.</summary>
    /// <response code="200">Password changed; returns a new session.</response>
    /// <response code="400">Validation failed; joined messages in the envelope's error field.</response>
    /// <response code="401">Current password incorrect — error code INVALID_CREDENTIALS.</response>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SessionDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<SessionDto>>> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var session = await _service.ChangePasswordAsync(CurrentUserId(), request, ct);

        if (session is null)
        {
            var message = AuthMessages.Errors.CurrentPasswordIncorrect(AppLanguage.Current);
            var envelope = ApiResponse<SessionDto>.Failure(message, "INVALID_CREDENTIALS");
            return Unauthorized(envelope);
        }

        var successMessage = AuthMessages.Success.PasswordChanged(AppLanguage.Current);
        var body = ApiResponse<SessionDto>.Success(successMessage, session);
        return Ok(body);
    }

    #endregion

    #endregion

}
