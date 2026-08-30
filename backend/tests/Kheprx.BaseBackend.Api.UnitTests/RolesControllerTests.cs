using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class RolesControllerTests
{
    [Fact]
    public async Task Get_returns_200_with_roles()
    {
        var svc = new Mock<IRoleService>();
        svc.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<RoleDto> { new(Guid.NewGuid(), "admin", null, "Administrator", 1) });

        var result = await new RolesController(svc.Object).Get(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<RoleDto>>>(ok.Value);
        Assert.Single(body.Data!);
        Assert.Equal("admin", body.Data![0].Code);
    }
}
