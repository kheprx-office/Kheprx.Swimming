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
    private static readonly Role AdminRole = new("admin", labelEn: "Administrator", sortOrder: 1);
    private static readonly Role ManagerRole = new("manager", labelEn: "Manager", sortOrder: 2);
    private static readonly Role WorkerRole = new("worker", labelEn: "Worker", sortOrder: 4);

    private static (UserService svc, Mock<IUserRepository> users, Mock<IRoleRepository> roles,
        Mock<IUserProfileRepository> profiles,
        Mock<IPasswordHasher> hasher) Build()
    {
        var users = new Mock<IUserRepository>();
        var roles = new Mock<IRoleRepository>();
        var profiles = new Mock<IUserProfileRepository>();
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns((string p) => $"hashed:{p}");
        users.Setup(u => u.ListCodesByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(Array.Empty<string>());
        profiles.Setup(p => p.ListManagersAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, Manager>());
        profiles.Setup(p => p.ListMoqaweleenAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, Moqawel>());
        profiles.Setup(p => p.ListWorkersAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, Worker>());
        return (new UserService(users.Object, roles.Object, profiles.Object, hasher.Object),
            users, roles, profiles, hasher);
    }

    private static CreateUserRequest WorkerCreate(string nid = "29801014501234") =>
        new("Worker One", "worker", nid, "w@x.com", "password8", "0100000001", "male", 24,
            null, 350m, new DateOnly(2026, 1, 15));

    [Fact]
    public async Task Create_worker_generates_code_and_persists_profile()
    {
        var (svc, users, roles, profiles, _) = Build();
        roles.Setup(r => r.GetByCodeAsync("worker", It.IsAny<CancellationToken>())).ReturnsAsync(WorkerRole);
        users.Setup(u => u.ListCodesByPrefixAsync("W-", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new[] { "W-1", "W-7", "W-3" });
        Worker? addedWorker = null;
        profiles.Setup(p => p.AddWorkerAsync(It.IsAny<Worker>(), It.IsAny<CancellationToken>()))
                .Callback<Worker, CancellationToken>((w, _) => addedWorker = w)
                .Returns(Task.CompletedTask);

        var dto = await svc.CreateAsync(WorkerCreate());

        Assert.NotNull(dto);
        Assert.Equal("W-8", dto!.Code);                       // max suffix 7 + 1
        Assert.Equal("29801014501234", dto.Nid);
        Assert.Equal(350m, dto.Profile!.DailyWage);
        Assert.Equal(new DateOnly(2026, 1, 15), dto.Profile.HireDate);
        Assert.NotNull(addedWorker);
        Assert.Equal(350m, addedWorker!.DailyWage);
        users.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_manager_persists_salary_profile()
    {
        var (svc, users, roles, profiles, _) = Build();
        roles.Setup(r => r.GetByCodeAsync("manager", It.IsAny<CancellationToken>())).ReturnsAsync(ManagerRole);
        Manager? added = null;
        profiles.Setup(p => p.AddManagerAsync(It.IsAny<Manager>(), It.IsAny<CancellationToken>()))
                .Callback<Manager, CancellationToken>((m, _) => added = m)
                .Returns(Task.CompletedTask);

        var dto = await svc.CreateAsync(new CreateUserRequest(
            "Mgr", "manager", "29801014505678", "m@x.com", "password8",
            null, null, 45, 15000m, null, null));

        Assert.NotNull(dto);
        Assert.Equal("PM-1", dto!.Code);
        Assert.Equal(15000m, dto.Profile!.MonthlySalary);
        Assert.Equal(15000m, added!.MonthlySalary);
    }

    [Fact]
    public async Task Create_admin_has_null_profile_and_owner_code()
    {
        var (svc, users, roles, profiles, _) = Build();
        roles.Setup(r => r.GetByCodeAsync("admin", It.IsAny<CancellationToken>())).ReturnsAsync(AdminRole);

        var dto = await svc.CreateAsync(new CreateUserRequest(
            "Admin", "admin", "29801014509999", "a@x.com", "password8",
            null, null, null, null, null, null));

        Assert.NotNull(dto);
        Assert.Equal("OWNER-1", dto!.Code);
        Assert.Null(dto.Profile);
        profiles.Verify(p => p.AddManagerAsync(It.IsAny<Manager>(), It.IsAny<CancellationToken>()), Times.Never);
        users.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_login_less_worker_has_no_email_and_no_must_change_password()
    {
        var (svc, users, roles, _, hasher) = Build();
        roles.Setup(r => r.GetByCodeAsync("worker", It.IsAny<CancellationToken>())).ReturnsAsync(WorkerRole);

        var dto = await svc.CreateAsync(WorkerCreate() with { Email = null, Password = null });

        Assert.NotNull(dto);
        Assert.Null(dto!.Email);
        Assert.False(dto.MustChangePassword);
        hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        users.Verify(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_throws_when_nid_in_use()
    {
        var (svc, users, roles, _, _) = Build();
        roles.Setup(r => r.GetByCodeAsync("worker", It.IsAny<CancellationToken>())).ReturnsAsync(WorkerRole);
        users.Setup(u => u.GetByNidAsync("29801014501234", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new User("Other", WorkerRole.Id, "29801014501234"));

        await Assert.ThrowsAsync<NidInUseException>(() => svc.CreateAsync(WorkerCreate()));
        users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_returns_null_when_email_in_use()
    {
        var (svc, users, roles, _, _) = Build();
        roles.Setup(r => r.GetByCodeAsync("worker", It.IsAny<CancellationToken>())).ReturnsAsync(WorkerRole);
        users.Setup(u => u.GetByEmailAsync("w@x.com", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new User("Other", WorkerRole.Id, "29801014505555", email: "w@x.com"));

        var dto = await svc.CreateAsync(WorkerCreate());

        Assert.Null(dto);
    }

    [Fact]
    public async Task Update_rejects_role_change()
    {
        var (svc, users, roles, _, _) = Build();
        var existing = new User("W", WorkerRole.Id, "29801014501234", email: "w@x.com");
        users.Setup(u => u.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        roles.Setup(r => r.GetByIdAsync(WorkerRole.Id, It.IsAny<CancellationToken>())).ReturnsAsync(WorkerRole);

        await Assert.ThrowsAsync<InvalidUserException>(() => svc.UpdateAsync(existing.Id,
            new UpdateUserRequest("W", "manager", "29801014501234", "active",
                "w@x.com", null, null, null, null, 15000m, null, null)));
    }

    [Fact]
    public async Task Update_worker_upserts_profile_and_corrects_pending_nid()
    {
        var (svc, users, roles, profiles, _) = Build();
        var existing = new User("W", WorkerRole.Id, "PENDING-W-1", email: "w@x.com");
        users.Setup(u => u.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        roles.Setup(r => r.GetByIdAsync(WorkerRole.Id, It.IsAny<CancellationToken>())).ReturnsAsync(WorkerRole);
        users.Setup(u => u.GetByNidAsync("29801014501234", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var worker = new Worker(existing.Id, 300m);
        profiles.Setup(p => p.GetWorkerAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(worker);

        var dto = await svc.UpdateAsync(existing.Id, new UpdateUserRequest(
            "W", "worker", "29801014501234", "active",
            "w@x.com", null, "0100000001", "male", 25, null, 400m, null));

        Assert.NotNull(dto);
        Assert.Equal("29801014501234", dto!.Nid);
        Assert.Equal(400m, worker.DailyWage);
        Assert.Equal(400m, dto.Profile!.DailyWage);
        users.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_creates_missing_profile_row()
    {
        var (svc, users, roles, profiles, _) = Build();
        var existing = new User("M", ManagerRole.Id, "29801014501234", email: "m@x.com");
        users.Setup(u => u.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        roles.Setup(r => r.GetByIdAsync(ManagerRole.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ManagerRole);
        profiles.Setup(p => p.GetManagerAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Manager?)null);
        Manager? added = null;
        profiles.Setup(p => p.AddManagerAsync(It.IsAny<Manager>(), It.IsAny<CancellationToken>()))
                .Callback<Manager, CancellationToken>((m, _) => added = m)
                .Returns(Task.CompletedTask);

        var dto = await svc.UpdateAsync(existing.Id, new UpdateUserRequest(
            "M", "manager", "29801014501234", "active",
            "m@x.com", null, null, null, null, 18000m, null, null));

        Assert.NotNull(dto);
        Assert.Equal(18000m, added!.MonthlySalary);
        Assert.Equal(18000m, dto!.Profile!.MonthlySalary);
    }

    [Fact]
    public async Task Update_throws_when_nid_taken_by_another_user()
    {
        var (svc, users, roles, _, _) = Build();
        var existing = new User("A", AdminRole.Id, "29801014501111", email: "a@x.com");
        users.Setup(u => u.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        roles.Setup(r => r.GetByIdAsync(AdminRole.Id, It.IsAny<CancellationToken>())).ReturnsAsync(AdminRole);
        users.Setup(u => u.GetByNidAsync("29801014502222", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new User("Other", AdminRole.Id, "29801014502222"));

        await Assert.ThrowsAsync<NidInUseException>(() => svc.UpdateAsync(existing.Id,
            new UpdateUserRequest("A", "admin", "29801014502222", "active",
                "a@x.com", null, null, null, null, null, null, null)));
        users.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_returns_null_when_user_missing()
    {
        var (svc, users, _, _, _) = Build();
        users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await svc.UpdateAsync(Guid.NewGuid(), new UpdateUserRequest(
            "A", "admin", "29801014501234", "active",
            "a@b.com", null, null, null, null, null, null, null));

        Assert.Null(result);
        users.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetStatus_returns_null_when_user_missing()
    {
        var (svc, users, _, _, _) = Build();
        users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await svc.SetStatusAsync(Guid.NewGuid(), new SetUserStatusRequest("disabled"));

        Assert.Null(result);
        users.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task List_passes_search_and_maps_profiles()
    {
        var (svc, users, roles, profiles, _) = Build();
        var worker = new User("W", WorkerRole.Id, "29801014501234");
        users.Setup(u => u.ListAsync("29801", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { worker });
        roles.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { WorkerRole });
        profiles.Setup(p => p.ListWorkersAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, Worker> { [worker.Id] = new(worker.Id, 350m) });

        var result = await svc.ListAsync("29801");

        Assert.Single(result);
        Assert.Equal("worker", result[0].Role);
        Assert.Equal(350m, result[0].Profile!.DailyWage);
        users.Verify(u => u.ListAsync("29801", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetStatus_returns_profile_for_worker()
    {
        var (svc, users, roles, profiles, _) = Build();
        var existing = new User("W", WorkerRole.Id, "29801014501234", email: "w@x.com");
        users.Setup(u => u.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        roles.Setup(r => r.GetByIdAsync(WorkerRole.Id, It.IsAny<CancellationToken>())).ReturnsAsync(WorkerRole);
        profiles.Setup(p => p.GetWorkerAsync(existing.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Worker(existing.Id, 350m));

        var dto = await svc.SetStatusAsync(existing.Id, new SetUserStatusRequest("disabled"));

        Assert.NotNull(dto);
        Assert.Equal("disabled", dto!.Status);
        Assert.Equal(350m, dto.Profile!.DailyWage);
    }

    [Fact]
    public async Task SetStatus_still_works()
    {
        var (svc, users, roles, _, _) = Build();
        var existing = new User("A", AdminRole.Id, "29801014501234", email: "a@x.com");
        users.Setup(u => u.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        roles.Setup(r => r.GetByIdAsync(AdminRole.Id, It.IsAny<CancellationToken>())).ReturnsAsync(AdminRole);

        var dto = await svc.SetStatusAsync(existing.Id, new SetUserStatusRequest("disabled"));

        Assert.NotNull(dto);
        Assert.Equal("disabled", dto!.Status);
        Assert.False(existing.IsActive);
    }
}
