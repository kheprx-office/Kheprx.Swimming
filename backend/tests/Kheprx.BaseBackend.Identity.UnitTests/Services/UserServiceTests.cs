using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Exceptions;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Services;

public class UserServiceTests
{
    private static readonly Role AdminRole = new("admin", "Administrator");
    private static readonly Role CaptainRole = new("captain", "Captain");
    private static readonly Role HeadCoachRole = new("head_coach", "Head Coach");

    private static (UserService svc, Mock<IUserRepository> users, Mock<IRoleRepository> roles,
        Mock<IPasswordHasher> hasher) Build()
    {
        var users = new Mock<IUserRepository>();
        var roles = new Mock<IRoleRepository>();
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns((string p) => $"hashed:{p}");
        return (new UserService(users.Object, roles.Object, hasher.Object),
            users, roles, hasher);
    }

    private static CreateUserRequest CaptainCreate(string? email = "c@x.com", string? password = "password8") =>
        new("captain.dave", "Dave Smith", "captain", null, email, password, "01012345678", null, null);

    [Fact]
    public async Task Create_captain_persists_and_maps_dto()
    {
        var (svc, users, roles, _) = Build();
        roles.Setup(r => r.GetByCodeAsync("captain", It.IsAny<CancellationToken>())).ReturnsAsync(CaptainRole);

        var dto = await svc.CreateAsync(CaptainCreate());

        Assert.NotNull(dto);
        Assert.Equal("Dave Smith", dto!.NameEn);
        Assert.Equal("captain", dto.Role);
        Assert.Equal("c@x.com", dto.Email);
        users.Verify(u => u.AddAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()), Times.Once);
        users.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_login_less_user_has_no_email()
    {
        var (svc, users, roles, hasher) = Build();
        roles.Setup(r => r.GetByCodeAsync("captain", It.IsAny<CancellationToken>())).ReturnsAsync(CaptainRole);

        var dto = await svc.CreateAsync(CaptainCreate(email: null, password: null));

        Assert.NotNull(dto);
        Assert.Null(dto!.Email);
        hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        users.Verify(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_returns_null_when_email_in_use()
    {
        var (svc, users, roles, _) = Build();
        roles.Setup(r => r.GetByCodeAsync("captain", It.IsAny<CancellationToken>())).ReturnsAsync(CaptainRole);
        users.Setup(u => u.GetByEmailAsync("c@x.com", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new AppUser("other", "Other", CaptainRole.Id, email: "c@x.com"));

        var dto = await svc.CreateAsync(CaptainCreate());

        Assert.Null(dto);
    }

    [Fact]
    public async Task Create_throws_on_unknown_role()
    {
        var (svc, _, roles, _) = Build();
        roles.Setup(r => r.GetByCodeAsync("ghost", It.IsAny<CancellationToken>())).ReturnsAsync((Role?)null);

        await Assert.ThrowsAsync<InvalidUserException>(() =>
            svc.CreateAsync(new CreateUserRequest("u", "Name", "ghost", null, null, null, null, null, null)));
    }

    [Fact]
    public async Task Update_returns_null_when_user_missing()
    {
        var (svc, users, _, _) = Build();
        users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((AppUser?)null);

        var result = await svc.UpdateAsync(Guid.NewGuid(),
            new UpdateUserRequest("Name", "captain", null, null, null, null, null, null));

        Assert.Null(result);
        users.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_applies_profile_changes_and_saves()
    {
        var (svc, users, roles, _) = Build();
        var existing = new AppUser("cap.dave", "Dave", CaptainRole.Id, email: "c@x.com");
        users.Setup(u => u.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        roles.Setup(r => r.GetByIdAsync(CaptainRole.Id, It.IsAny<CancellationToken>())).ReturnsAsync(CaptainRole);

        var dto = await svc.UpdateAsync(existing.Id,
            new UpdateUserRequest("Dave Updated", "captain", "ديف", "c@x.com", null, "01012345678", null, null));

        Assert.NotNull(dto);
        Assert.Equal("Dave Updated", dto!.NameEn);
        Assert.Equal("ديف", dto.NameAr);
        users.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task List_passes_search_and_maps_role()
    {
        var (svc, users, roles, _) = Build();
        var user = new AppUser("cap.dave", "Dave", CaptainRole.Id);
        users.Setup(u => u.ListAsync("Dave", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { user });
        roles.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { CaptainRole });

        var result = await svc.ListAsync("Dave");

        Assert.Single(result);
        Assert.Equal("captain", result[0].Role);
        Assert.Equal("Dave", result[0].NameEn);
    }
}
