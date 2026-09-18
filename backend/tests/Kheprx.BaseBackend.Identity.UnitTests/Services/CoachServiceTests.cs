using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Options;
using Kheprx.BaseBackend.Identity.Application.Services;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Services;

public class CoachServiceTests
{
    private static CreateCoachRequest Req(string role = "captain") => new(
        role, "Dave Coach", "dave.coach", "dave@oasis.com", "29001011234567",
        Guid.NewGuid(), new DateOnly(1990, 1, 1), "01000000000", null);

    private static (CoachService svc, Mock<IUserRepository> users, Mock<ICoachProfileRepository> coaches) Build(
        bool usernameTaken = false, bool emailTaken = false, bool nidTaken = false,
        bool genderExists = true, bool saveOk = true)
    {
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(usernameTaken ? new AppUser("x", "X", Guid.NewGuid()) : null);
        users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(emailTaken ? new AppUser("y", "Y", Guid.NewGuid()) : null);

        var roles = new Mock<IRoleRepository>();
        roles.Setup(r => r.GetByCodeAsync("captain", It.IsAny<CancellationToken>())).ReturnsAsync(new Role("captain", "Captain"));
        roles.Setup(r => r.GetByCodeAsync("head_coach", It.IsAny<CancellationToken>())).ReturnsAsync(new Role("head_coach", "Head Coach"));

        var coaches = new Mock<ICoachProfileRepository>();
        coaches.Setup(c => c.NationalIdExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(nidTaken);
        coaches.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(saveOk);

        var genders = new Mock<IGenderRepository>();
        genders.Setup(g => g.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(genderExists);

        var hasher = new Mock<IPasswordHasher>(); hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("HASH");
        var opts = Options.Create(new AccountCreationOptions { GenericPassword = "Oasis2026!" });

        var svc = new CoachService(users.Object, roles.Object, coaches.Object, genders.Object, hasher.Object, opts);
        return (svc, users, coaches);
    }

    [Fact]
    public async Task Create_captain_adds_captain_profile_and_returns_dto()
    {
        var (svc, users, coaches) = Build();
        var result = await svc.CreateAsync(Req("captain"));

        Assert.NotNull(result);
        Assert.Equal("captain", result!.Role);
        Assert.Equal("Oasis2026!", result.TemporaryPassword);
        users.Verify(u => u.AddAsync(It.Is<AppUser>(a => a.Username == "dave.coach" && a.IsFirstLogin), It.IsAny<CancellationToken>()), Times.Once);
        coaches.Verify(c => c.AddCaptainAsync(It.IsAny<CaptainProfile>(), It.IsAny<CancellationToken>()), Times.Once);
        coaches.Verify(c => c.AddHeadCoachAsync(It.IsAny<HeadCoachProfile>(), It.IsAny<CancellationToken>()), Times.Never);
        coaches.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_head_coach_adds_head_coach_profile()
    {
        var (svc, _, coaches) = Build();
        var result = await svc.CreateAsync(Req("head_coach"));
        Assert.Equal("head_coach", result!.Role);
        coaches.Verify(c => c.AddHeadCoachAsync(It.IsAny<HeadCoachProfile>(), It.IsAny<CancellationToken>()), Times.Once);
        coaches.Verify(c => c.AddCaptainAsync(It.IsAny<CaptainProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact] public async Task Returns_null_when_username_taken() { var (s,_,_) = Build(usernameTaken: true); Assert.Null(await s.CreateAsync(Req())); }
    [Fact] public async Task Returns_null_when_email_taken() { var (s,_,_) = Build(emailTaken: true); Assert.Null(await s.CreateAsync(Req())); }
    [Fact] public async Task Returns_null_when_national_id_taken() { var (s,_,_) = Build(nidTaken: true); Assert.Null(await s.CreateAsync(Req())); }
    [Fact] public async Task Returns_null_on_save_conflict() { var (s,_,_) = Build(saveOk: false); Assert.Null(await s.CreateAsync(Req())); }
    [Fact] public async Task Throws_when_gender_unknown() { var (s,_,_) = Build(genderExists: false); await Assert.ThrowsAnyAsync<Exception>(() => s.CreateAsync(Req())); }
}
