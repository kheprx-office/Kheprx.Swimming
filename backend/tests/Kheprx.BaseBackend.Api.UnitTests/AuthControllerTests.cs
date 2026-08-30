using System.Security.Claims;
using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class AuthControllerTests
{
    private static AuthController WithUser(Mock<IAuthService> svc, Guid? userId = null)
    {
        var identity = userId is null
            ? new ClaimsIdentity()
            : new ClaimsIdentity(new[] { new Claim("sub", userId.Value.ToString()) }, "jwt");
        return new AuthController(svc.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    [Fact]
    public async Task Login_returns_200_with_session_on_success()
    {
        var svc = new Mock<IAuthService>();
        svc.Setup(s => s.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new SessionDto("a", "r", "admin", Guid.NewGuid(), true));

        var result = await WithUser(svc).Login(new LoginRequest("a@b.com", "pw"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<SessionDto>>(ok.Value);
        Assert.True(body.SuccessStatus);
        Assert.Equal("admin", body.Data!.Role);
        Assert.True(body.Data.MustChangePassword);
    }

    [Fact]
    public async Task Login_returns_401_when_service_returns_null()
    {
        var svc = new Mock<IAuthService>();
        svc.Setup(s => s.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((SessionDto?)null);

        var result = await WithUser(svc).Login(new LoginRequest("a@b.com", "pw"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Logout_reads_sub_claim_and_returns_200()
    {
        var userId = Guid.NewGuid();
        var svc = new Mock<IAuthService>();

        var result = await WithUser(svc, userId).Logout(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        svc.Verify(s => s.LogoutAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Me_returns_200_with_current_user()
    {
        var userId = Guid.NewGuid();
        var svc = new Mock<IAuthService>();
        svc.Setup(s => s.GetCurrentUserAsync(userId, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new CurrentUserDto(userId, "a@b.com", "Alice", "admin", "01000000000", "male", 30));

        var result = await WithUser(svc, userId).Me(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<CurrentUserDto>>(ok.Value);
        Assert.Equal(userId, body.Data!.UserId);
        Assert.Equal("01000000000", body.Data.Phone);
        Assert.Equal("male", body.Data.Gender);
        Assert.Equal(30, body.Data.Age);
    }

    [Fact]
    public async Task ChangePassword_returns_401_when_current_password_wrong()
    {
        var userId = Guid.NewGuid();
        var svc = new Mock<IAuthService>();
        svc.Setup(s => s.ChangePasswordAsync(userId, It.IsAny<ChangePasswordRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync((SessionDto?)null);

        var result = await WithUser(svc, userId).ChangePassword(new ChangePasswordRequest("bad", "newpass8"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task ChangePassword_returns_200_with_new_session()
    {
        var userId = Guid.NewGuid();
        var svc = new Mock<IAuthService>();
        svc.Setup(s => s.ChangePasswordAsync(userId, It.IsAny<ChangePasswordRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new SessionDto("a", "r", "admin", userId, false));

        var result = await WithUser(svc, userId).ChangePassword(new ChangePasswordRequest("cur", "newpass8"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<SessionDto>>(ok.Value);
        Assert.False(body.Data!.MustChangePassword);
    }
}
