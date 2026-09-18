using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Domain.Exceptions;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class UsersControllerTests
{
    [Fact]
    public async Task List_returns_200_with_users()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.ListAsync(null, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new[] { new UserDto(Guid.NewGuid(), "alice", "Alice", null, "a@b.com", null, null, null, "admin") });

        var result = await new UsersController(svc.Object).List(null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<UserDto>>>(ok.Value);
        Assert.Single(body.Data!);
    }

    [Fact]
    public async Task Create_returns_201_on_success()
    {
        var svc = new Mock<IUserService>();
        var dto = new UserDto(Guid.NewGuid(), "alice", "Alice", null, "a@b.com", null, null, null, "admin");
        svc.Setup(s => s.CreateAsync(It.IsAny<CreateUserRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var result = await new UsersController(svc.Object)
            .Create(new CreateUserRequest("alice", "Alice", "admin", null, "a@b.com", "password8", null, null, null), CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        var body = Assert.IsType<ApiResponse<UserDto>>(created.Value);
        Assert.Equal("admin", body.Data!.Role);
    }

    [Fact]
    public async Task Create_returns_409_when_service_returns_null()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.CreateAsync(It.IsAny<CreateUserRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((UserDto?)null);

        var result = await new UsersController(svc.Object)
            .Create(new CreateUserRequest("alice", "Alice", "admin", null, "a@b.com", "password8", null, null, null), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<UserDto>>(conflict.Value);
        Assert.Equal("EMAIL_IN_USE", body.Error);
    }

    [Fact]
    public async Task Update_returns_404_when_missing()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateUserRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync((UserDto?)null);

        var result = await new UsersController(svc.Object)
            .Update(Guid.NewGuid(), new UpdateUserRequest("A", "admin", null, "a@b.com", null, null, null, null), CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<UserDto>>(notFound.Value);
        Assert.Equal("USER_NOT_FOUND", body.Error);
    }

    [Fact]
    public async Task Update_returns_200_on_success()
    {
        var id = Guid.NewGuid();
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.UpdateAsync(id, It.IsAny<UpdateUserRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new UserDto(id, "alice", "Alice", null, "a@b.com", null, null, null, "admin"));

        var result = await new UsersController(svc.Object)
            .Update(id, new UpdateUserRequest("Alice", "admin", null, "a@b.com", null, null, null, null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<UserDto>>(ok.Value);
        Assert.Equal("Alice", body.Data!.NameEn);
    }

    [Fact]
    public async Task Update_returns_409_when_email_in_use()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateUserRequest>(), It.IsAny<CancellationToken>()))
           .ThrowsAsync(new EmailInUseException("Email is already in use."));

        var result = await new UsersController(svc.Object)
            .Update(Guid.NewGuid(), new UpdateUserRequest("A", "admin", null, "taken@b.com", null, null, null, null), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<UserDto>>(conflict.Value);
        Assert.Equal("EMAIL_IN_USE", body.Error);
    }

    [Fact]
    public async Task List_passes_search_to_service()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.ListAsync("alice", It.IsAny<CancellationToken>()))
           .ReturnsAsync(Array.Empty<UserDto>());

        await new UsersController(svc.Object).List("alice", CancellationToken.None);

        svc.Verify(s => s.ListAsync("alice", It.IsAny<CancellationToken>()), Times.Once);
    }

}
