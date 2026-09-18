using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Options;
using Kheprx.BaseBackend.Identity.Application.Services;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Kheprx.BaseBackend.Identity.Domain.ReadModels;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Services;

public class SwimmerServiceTests
{
    private static CreateSwimmerRequest Req() => new(
        "Mona Ali", "mona.ali", Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2010, 5, 1),
        new[] { Guid.NewGuid() }, null, null, null, null);

    private static (SwimmerService svc, Mock<ISwimmerProfileRepository> swimmers, Mock<IUserRepository> users)
        Build(bool usernameTaken = false, bool emailTaken = false, bool refsExist = true)
    {
        var swimmers = new Mock<ISwimmerProfileRepository>();
        swimmers.Setup(r => r.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
        swimmers.Setup(r => r.GetMaxUidNumberAsync(It.IsAny<CancellationToken>())).ReturnsAsync(6);
        swimmers.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(usernameTaken ? new AppUser("x", "X", Guid.NewGuid()) : null);
        users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(emailTaken ? new AppUser("y", "Y", Guid.NewGuid()) : null);

        var roles = new Mock<IRoleRepository>();
        roles.Setup(r => r.GetByCodeAsync("swimmer", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Role("swimmer", "Swimmer"));

        var clubs = new Mock<IClubRepository>(); clubs.Setup(c => c.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(refsExist);
        var strokes = new Mock<IStrokeRepository>(); strokes.Setup(c => c.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(refsExist);
        var genders = new Mock<IGenderRepository>(); genders.Setup(c => c.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(refsExist);

        var hasher = new Mock<IPasswordHasher>(); hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("HASH");
        var opts = Microsoft.Extensions.Options.Options.Create(new AccountCreationOptions { GenericPassword = "Oasis2026!" });

        var svc = new SwimmerService(swimmers.Object, users.Object, roles.Object,
            clubs.Object, strokes.Object, genders.Object, hasher.Object, opts);
        return (svc, swimmers, users);
    }

    [Fact]
    public async Task GetCount_returns_repository_count()
    {
        var (svc, swimmers, _) = Build();
        swimmers.Setup(r => r.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(24);

        var result = await svc.GetCountAsync();

        Assert.Equal(24, result.Count);
    }

    [Fact]
    public async Task Create_builds_user_profile_specializations_and_returns_dto()
    {
        var (svc, swimmers, users) = Build();
        var result = await svc.CreateAsync(Req());

        Assert.NotNull(result);
        Assert.Equal("SW-0007", result!.Uid);          // max 6 + 1
        Assert.Equal("mona.ali", result.Username);
        Assert.Equal("Oasis2026!", result.TemporaryPassword);
        users.Verify(u => u.AddAsync(It.Is<AppUser>(a => a.Username == "mona.ali" && a.IsFirstLogin), It.IsAny<CancellationToken>()), Times.Once);
        swimmers.Verify(s => s.AddAsync(It.IsAny<SwimmerProfile>(), It.IsAny<CancellationToken>()), Times.Once);
        swimmers.Verify(s => s.AddSpecializationsAsync(It.IsAny<IEnumerable<SwimmerSpecialization>>(), It.IsAny<CancellationToken>()), Times.Once);
        swimmers.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_returns_null_when_username_taken()
    {
        var (svc, _, _) = Build(usernameTaken: true);
        Assert.Null(await svc.CreateAsync(Req()));
    }

    [Fact]
    public async Task Create_throws_when_a_reference_id_is_unknown()
    {
        var (svc, _, _) = Build(refsExist: false);
        await Assert.ThrowsAnyAsync<Exception>(() => svc.CreateAsync(Req()));
    }

    [Fact]
    public async Task Create_returns_null_on_unique_index_conflict()
    {
        var (svc, swimmers, _) = Build();
        swimmers.Setup(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        Assert.Null(await svc.CreateAsync(Req()));
    }

    [Fact]
    public async Task List_maps_rows_computes_age_and_passes_search()
    {
        var (svc, swimmers, _) = Build();
        var dob = new DateOnly(2010, 3, 15);
        var rows = new[]
        {
            new SwimmerListRow(Guid.NewGuid(), "SW-0001", "Alpha", "ألفا", "Oasis Main", "الواحة", "male", dob),
            new SwimmerListRow(Guid.NewGuid(), "SW-0002", "Bravo", null, null, null, null, null),
        };
        swimmers.Setup(r => r.ListAsync("al", It.IsAny<CancellationToken>())).ReturnsAsync(rows);

        var result = await svc.ListAsync("al");

        swimmers.Verify(r => r.ListAsync("al", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, result.Count);
        Assert.Equal("SW-0001", result[0].Uid);
        Assert.Equal("Oasis Main", result[0].ClubNameEn);
        Assert.Equal("male", result[0].GenderCode);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expectedAge = today.Year - dob.Year;
        if (dob > today.AddYears(-expectedAge)) expectedAge--;
        Assert.Equal(expectedAge, result[0].Age);

        Assert.Null(result[1].Age);                        // null Dob → null age
        Assert.Equal(string.Empty, result[1].GenderCode);  // null gender code → ""
        Assert.Null(result[1].ClubNameEn);
    }
}
