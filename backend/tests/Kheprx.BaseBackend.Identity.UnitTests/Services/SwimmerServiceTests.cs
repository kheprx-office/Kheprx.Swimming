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
        users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new AppUser("z", "Zed", Guid.NewGuid(), email: "z@x.io", genderId: Guid.NewGuid(), dob: new DateOnly(2009, 1, 1)));
        users.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var roles = new Mock<IRoleRepository>();
        roles.Setup(r => r.GetByCodeAsync("swimmer", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Role("swimmer", "Swimmer"));

        var clubs = new Mock<IClubRepository>(); clubs.Setup(c => c.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(refsExist);
        var strokes = new Mock<IStrokeRepository>(); strokes.Setup(c => c.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(refsExist);
        var genders = new Mock<IGenderRepository>(); genders.Setup(c => c.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(refsExist);
        var bloodTypes = new Mock<IBloodTypeRepository>(); bloodTypes.Setup(b => b.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(refsExist);
        var fitness = new Mock<IFitnessAssessmentRepository>(); fitness.Setup(f => f.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(refsExist);

        var hasher = new Mock<IPasswordHasher>(); hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("HASH");
        var opts = Microsoft.Extensions.Options.Options.Create(new AccountCreationOptions { GenericPassword = "Oasis2026!" });

        var svc = new SwimmerService(swimmers.Object, users.Object, roles.Object,
            clubs.Object, strokes.Object, genders.Object, fitness.Object, bloodTypes.Object, hasher.Object, opts);
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

    [Fact]
    public async Task GetProfile_returns_null_when_not_found()
    {
        var (svc, swimmers, _) = Build();
        swimmers.Setup(r => r.GetProfileByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwimmerProfileRow?)null);
        Assert.Null(await svc.GetProfileAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetProfile_maps_identity_and_null_vitals_when_no_exam()
    {
        var (svc, swimmers, _) = Build();
        var id = Guid.NewGuid();
        swimmers.Setup(r => r.GetProfileByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SwimmerProfileRow(id, "SW-0001", "Alpha", "ألفا",
                    new DateOnly(2010, 1, 1), "male", "01000000001", "Oasis Main", "الواحة"));
        swimmers.Setup(r => r.GetLatestExamAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((MedicalExamRow?)null);

        var dto = await svc.GetProfileAsync(id);

        Assert.NotNull(dto);
        Assert.Equal("Alpha", dto!.Identity.NameEn);
        Assert.Equal("male", dto.Identity.GenderCode);
        Assert.NotNull(dto.Identity.Age);
        Assert.Null(dto.Vitals);
    }

    [Fact]
    public async Task GetProfile_maps_latest_vitals_when_present()
    {
        var (svc, swimmers, _) = Build();
        var id = Guid.NewGuid();
        var im = Guid.NewGuid();
        var examId = Guid.NewGuid();
        swimmers.Setup(r => r.GetProfileByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SwimmerProfileRow(id, "SW-0001", "Alpha", null, null, null, null, null, null));
        swimmers.Setup(r => r.GetLatestExamAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MedicalExamRow(examId, new DateOnly(2024, 10, 8), 14.8m, 182m, 74m,
                    null, null, null, null,
                    im, "fit", "Fit", "لائق",
                    im, "fit", "Fit", "لائق",
                    im, "fit", "Fit", "لائق"));

        var dto = await svc.GetProfileAsync(id);

        Assert.NotNull(dto!.Vitals);
        Assert.Equal(examId, dto!.Vitals!.Id);
        Assert.Equal(14.8m, dto.Vitals!.Hemoglobin);
        Assert.Null(dto.Vitals.BloodType);
        Assert.Equal("Fit", dto.Vitals.InternalMed.NameEn);
    }

    [Fact]
    public async Task UpdateIdentity_returns_false_when_profile_missing()
    {
        var (svc, swimmers, _) = Build();
        swimmers.Setup(r => r.GetByIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwimmerProfile?)null);

        var ok = await svc.UpdateIdentityAsync(Guid.NewGuid(),
            new UpdateSwimmerIdentityRequest("New Name", null, new DateOnly(2010, 1, 1), "01000000009"));
        Assert.False(ok);
    }

    [Fact]
    public async Task UpdateIdentity_updates_user_and_saves()
    {
        var (svc, swimmers, users) = Build();
        var profile = new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid());
        swimmers.Setup(r => r.GetByIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(profile);
        var user = new AppUser("a.user", "Alpha", Guid.NewGuid(), email: "a@x.io", genderId: Guid.NewGuid(), dob: new DateOnly(2009, 1, 1));
        users.Setup(u => u.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var ok = await svc.UpdateIdentityAsync(profile.Id,
            new UpdateSwimmerIdentityRequest("New Name", "اسم", new DateOnly(2011, 2, 3), "01000000009"));

        Assert.True(ok);
        Assert.Equal("New Name", user.NameEn);
        Assert.Equal("a@x.io", user.Email);              // preserved
        Assert.Equal(new DateOnly(2011, 2, 3), user.Dob);
        users.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateExam_returns_null_when_swimmer_missing()
    {
        var (svc, swimmers, _) = Build();
        swimmers.Setup(r => r.GetByIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwimmerProfile?)null);
        Assert.Null(await svc.CreateExamAsync(Guid.NewGuid(), ExamReq()));
    }

    [Fact]
    public async Task CreateExam_persists_and_returns_vitals()
    {
        var (svc, swimmers, _) = Build();
        var profile = new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid());
        swimmers.Setup(r => r.GetByIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        var im = Guid.NewGuid();
        swimmers.Setup(r => r.GetExamRowByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MedicalExamRow(Guid.NewGuid(), new DateOnly(2026, 9, 19), 15m, 183m, 75m,
                    null, null, null, null, im, "fit", "Fit", "لائق", im, "fit", "Fit", "لائق", im, "fit", "Fit", "لائق"));

        var vitals = await svc.CreateExamAsync(profile.Id, ExamReq());

        Assert.NotNull(vitals);
        Assert.Equal(15m, vitals!.Hemoglobin);
        swimmers.Verify(r => r.AddExamAsync(It.IsAny<MedicalExam>(), It.IsAny<CancellationToken>()), Times.Once);
        swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateExam_throws_when_fitness_ref_unknown()
    {
        var (svc, swimmers, _) = Build(refsExist: false);
        swimmers.Setup(r => r.GetByIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid()));
        await Assert.ThrowsAnyAsync<Exception>(() => svc.CreateExamAsync(Guid.NewGuid(), ExamReq()));
    }

    private static CreateMedicalExamRequest ExamReq() => new(
        new DateOnly(2026, 9, 19), null, 15m, 183m, 75m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task ListExams_returns_null_when_swimmer_missing()
    {
        var (svc, swimmers, _) = Build();
        swimmers.Setup(r => r.GetByIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwimmerProfile?)null);
        Assert.Null(await svc.ListExamsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ListExams_maps_rows_to_dtos()
    {
        var (svc, swimmers, _) = Build();
        var id = Guid.NewGuid(); var examId = Guid.NewGuid(); var im = Guid.NewGuid();
        swimmers.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid()));
        swimmers.Setup(r => r.ListExamsAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new MedicalExamRow(examId, new DateOnly(2024, 10, 8), 14.8m, 182m, 74m,
                    null, null, null, null, im, "fit", "Fit", "لائق", im, "fit", "Fit", "لائق", im, "fit", "Fit", "لائق") });

        var list = await svc.ListExamsAsync(id);

        Assert.NotNull(list);
        Assert.Single(list!);
        Assert.Equal(examId, list![0].Id);
    }

    [Fact]
    public async Task UpdateExam_returns_null_when_exam_missing_or_not_owned()
    {
        var (svc, swimmers, _) = Build();
        var swimmerId = Guid.NewGuid();
        // not found
        swimmers.Setup(r => r.GetExamTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MedicalExam?)null);
        Assert.Null(await svc.UpdateExamAsync(swimmerId, Guid.NewGuid(), ExamReq()));
        // owned by a different swimmer
        var other = new MedicalExam(Guid.NewGuid(), new DateOnly(2024, 1, 1), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 13m, 178m, 71m);
        swimmers.Setup(r => r.GetExamTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(other);
        Assert.Null(await svc.UpdateExamAsync(swimmerId, other.Id, ExamReq()));
    }

    [Fact]
    public async Task UpdateExam_updates_saves_and_returns_vitals()
    {
        var (svc, swimmers, _) = Build();
        var swimmerId = Guid.NewGuid(); var im = Guid.NewGuid();
        var exam = new MedicalExam(swimmerId, new DateOnly(2024, 1, 1), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 13m, 178m, 71m);
        swimmers.Setup(r => r.GetExamTrackedAsync(exam.Id, It.IsAny<CancellationToken>())).ReturnsAsync(exam);
        swimmers.Setup(r => r.GetExamRowByIdAsync(exam.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MedicalExamRow(exam.Id, new DateOnly(2026, 9, 19), 15m, 183m, 75m,
                    null, null, null, null, im, "fit", "Fit", "لائق", im, "fit", "Fit", "لائق", im, "fit", "Fit", "لائق"));

        var vitals = await svc.UpdateExamAsync(swimmerId, exam.Id, ExamReq());

        Assert.NotNull(vitals);
        Assert.Equal(exam.Id, vitals!.Id);
        Assert.Equal(new DateOnly(2026, 9, 19), exam.ExamDate);   // entity was mutated
        swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteExam_removes_and_returns_true()
    {
        var (svc, swimmers, _) = Build();
        var swimmerId = Guid.NewGuid();
        var exam = new MedicalExam(swimmerId, new DateOnly(2024, 1, 1), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 13m, 178m, 71m);
        swimmers.Setup(r => r.GetExamTrackedAsync(exam.Id, It.IsAny<CancellationToken>())).ReturnsAsync(exam);

        Assert.True(await svc.DeleteExamAsync(swimmerId, exam.Id));
        swimmers.Verify(r => r.RemoveExam(exam), Times.Once);
        swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteExam_returns_false_when_missing_or_not_owned()
    {
        var (svc, swimmers, _) = Build();
        swimmers.Setup(r => r.GetExamTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MedicalExam?)null);
        Assert.False(await svc.DeleteExamAsync(Guid.NewGuid(), Guid.NewGuid()));
    }
}
