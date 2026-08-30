using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class ControllerMessageTests
{
    [Fact]
    public async Task Login_failure_returns_localized_invalid_credentials_message()
    {
        var service = new Mock<IAuthService>();
        service.Setup(s => s.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionDto?)null);
        var controller = new AuthController(service.Object);

        var result = await controller.Login(new LoginRequest("a@b.c", "x"), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<SessionDto>>(unauthorized.Value);
        Assert.Equal(AuthMessages.Errors.InvalidCredentials(AppLanguage.Current), body.Message);
        Assert.Equal("INVALID_CREDENTIALS", body.Error);
    }
}
