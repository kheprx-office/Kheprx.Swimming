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
        var opts = Microsoft.Extensions.Options.Options.Create(new AccountCreationOptions { GenericPassword = "12345679" });

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
        Assert.Equal("12345679", result.TemporaryPassword);
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

    private static CompleteIdentityVitalsRequest CompleteReq() => new(
        "Ahmed Ali", null, Guid.NewGuid(), new DateOnly(2010, 1, 1), Guid.NewGuid(),
        new DateOnly(2026, 1, 1), null, 14.5m, 175m, 68m,
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Phone: "01012345678");

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

    // ── Guardian tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetGuardiansAsync_returns_null_when_swimmer_missing()
    {
        var (svc, swimmers, _) = Build();
        var id = Guid.NewGuid();
        swimmers.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((SwimmerProfile?)null);

        Assert.Null(await svc.GetGuardiansAsync(id));
    }

    [Fact]
    public async Task GetGuardiansAsync_maps_father_and_mother_slots()
    {
        var (svc, swimmers, _) = Build();
        var id = Guid.NewGuid();
        swimmers.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid()));
        swimmers.Setup(r => r.ListGuardiansAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<GuardianRow>
        {
            new(Guid.NewGuid(), "father", "Hassan Ali", "27001010123456", "+201009876543"),
            new(Guid.NewGuid(), "mother", "Fatima Ibrahim", "27505050123456", "+201005554444"),
        });

        var dto = await svc.GetGuardiansAsync(id);

        Assert.NotNull(dto);
        Assert.Equal("Hassan Ali", dto!.Father!.Name);
        Assert.Equal("Fatima Ibrahim", dto.Mother!.Name);
    }

    [Fact]
    public async Task UpsertGuardiansAsync_returns_false_when_swimmer_missing()
    {
        var (svc, swimmers, _) = Build();
        var id = Guid.NewGuid();
        swimmers.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((SwimmerProfile?)null);

        var req = new UpsertGuardiansRequest(
            new GuardianInputDto("Hassan Ali", "27001010123456", "+201009876543"),
            new GuardianInputDto("Fatima Ibrahim", "27505050123456", "+201005554444"));

        Assert.False(await svc.UpsertGuardiansAsync(id, req));
    }

    [Fact]
    public async Task UpsertGuardiansAsync_inserts_when_slot_absent_and_updates_when_present()
    {
        var (svc, swimmers, _) = Build();
        var id = Guid.NewGuid();
        var fatherRel = Guid.NewGuid();
        var motherRel = Guid.NewGuid();
        swimmers.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid()));
        swimmers.Setup(r => r.GetGuardianRelationIdByCodeAsync("father", It.IsAny<CancellationToken>())).ReturnsAsync(fatherRel);
        swimmers.Setup(r => r.GetGuardianRelationIdByCodeAsync("mother", It.IsAny<CancellationToken>())).ReturnsAsync(motherRel);
        // father already exists → updated; mother absent → inserted
        var existingFather = new Guardian(id, fatherRel, "Old Name", "27001010123456", "+201000000000");
        swimmers.Setup(r => r.GetGuardianTrackedAsync(id, fatherRel, It.IsAny<CancellationToken>())).ReturnsAsync(existingFather);
        swimmers.Setup(r => r.GetGuardianTrackedAsync(id, motherRel, It.IsAny<CancellationToken>())).ReturnsAsync((Guardian?)null);

        var req = new UpsertGuardiansRequest(
            new GuardianInputDto("Hassan Ali", "27001010123456", "+201009876543"),
            new GuardianInputDto("Fatima Ibrahim", "27505050123456", "+201005554444"));

        var ok = await svc.UpsertGuardiansAsync(id, req);

        Assert.True(ok);
        Assert.Equal("Hassan Ali", existingFather.Name); // updated in place
        swimmers.Verify(r => r.AddGuardianAsync(It.Is<Guardian>(g => g.RelationId == motherRel && g.Name == "Fatima Ibrahim"), It.IsAny<CancellationToken>()), Times.Once);
        swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Body measurement tests ───────────────────────────────────────────────

    [Fact]
    public async Task GetBodyMeasurementAsync_returns_null_when_swimmer_missing()
    {
        var (svc, swimmers, _) = Build();
        var id = Guid.NewGuid();
        swimmers.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((SwimmerProfile?)null);

        Assert.Null(await svc.GetBodyMeasurementAsync(id));
    }

    [Fact]
    public async Task GetBodyMeasurementAsync_returns_wrapper_with_null_latest_when_no_rows()
    {
        var (svc, swimmers, _) = Build();
        var id = Guid.NewGuid();
        swimmers.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid()));
        swimmers.Setup(r => r.GetLatestBodyMeasurementAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((BodyMeasurementRow?)null);

        var dto = await svc.GetBodyMeasurementAsync(id);

        Assert.NotNull(dto);
        Assert.Null(dto!.Latest);
    }

    [Fact]
    public async Task GetBodyMeasurementAsync_maps_latest_row()
    {
        var (svc, swimmers, _) = Build();
        var id = Guid.NewGuid();
        swimmers.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid()));
        swimmers.Setup(r => r.GetLatestBodyMeasurementAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BodyMeasurementRow(Guid.NewGuid(), new DateOnly(2026, 9, 19), 78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m));

        var dto = await svc.GetBodyMeasurementAsync(id);

        Assert.NotNull(dto!.Latest);
        Assert.Equal(78.5m, dto.Latest!.RightArmCm);
        Assert.Equal(76.5m, dto.Latest.WaistDiameterCm);
    }

    [Fact]
    public async Task AddBodyMeasurementAsync_returns_false_when_swimmer_missing()
    {
        var (svc, swimmers, _) = Build();
        var id = Guid.NewGuid();
        swimmers.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((SwimmerProfile?)null);

        var req = new CreateBodyMeasurementRequest(78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m);
        Assert.False(await svc.AddBodyMeasurementAsync(id, req));
    }

    [Fact]
    public async Task AddBodyMeasurementAsync_inserts_and_saves_when_swimmer_exists()
    {
        var (svc, swimmers, _) = Build();
        var id = Guid.NewGuid();
        swimmers.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid()));

        var req = new CreateBodyMeasurementRequest(78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m);
        var ok = await svc.AddBodyMeasurementAsync(id, req);

        Assert.True(ok);
        swimmers.Verify(r => r.AddBodyMeasurementAsync(
            It.Is<BodyMeasurement>(m => m.SwimmerId == id && m.RightArmCm == 78.5m && m.WaistDiameterCm == 76.5m),
            It.IsAny<CancellationToken>()), Times.Once);
        swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetNewThisMonthCountAsync_uses_first_of_current_month_utc_as_cutoff()
    {
        var now = DateTime.UtcNow;
        var expectedCutoff = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var (svc, swimmers, _) = Build();
        swimmers.Setup(r => r.CountCreatedSinceAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(4);

        var result = await svc.GetNewThisMonthCountAsync();

        Assert.Equal(4, result);
        swimmers.Verify(r => r.CountCreatedSinceAsync(expectedCutoff, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetStrokeSplitAsync_orders_by_count_descending()
    {
        var (svc, swimmers, _) = Build();
        swimmers.Setup(r => r.GetStrokeCountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new StrokeCountRow(Guid.NewGuid(), "back", "Backstroke", "ظهر", 3),
            new StrokeCountRow(Guid.NewGuid(), "free", "Freestyle", "حرة", 9),
        });

        var result = await svc.GetStrokeSplitAsync();

        Assert.Equal("free", result[0].Code);
        Assert.Equal("back", result[1].Code);
    }

    // ── Onboarding prefill tests ─────────────────────────────────────────────

    [Fact]
    public async Task GetOnboardingPrefill_returns_null_when_not_a_swimmer()
    {
        var (svc, swimmers, _) = Build();
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwimmerProfile?)null);
        Assert.Null(await svc.GetOnboardingPrefillAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetOnboardingPrefill_maps_identity_ids()
    {
        var (svc, swimmers, users) = Build();
        var userId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var profile = new SwimmerProfile(userId, "SW-0009", clubId);
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        var genderId = Guid.NewGuid();
        users.Setup(u => u.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new AppUser("s.swimmer", "Sam", Guid.NewGuid(), nameAr: "سام", genderId: genderId, dob: new DateOnly(2011, 3, 4), phone: "01012345678"));

        var dto = await svc.GetOnboardingPrefillAsync(userId);

        Assert.NotNull(dto);
        Assert.Equal("SW-0009", dto!.Uid);
        Assert.Equal("Sam", dto.NameEn);
        Assert.Equal("سام", dto.NameAr);
        Assert.Equal(genderId, dto.GenderId);
        Assert.Equal(clubId, dto.TrainingClubId);
        Assert.Equal("01012345678", dto.Phone);
    }

    // ── CompleteIdentityVitals tests ─────────────────────────────────────────

    [Fact]
    public async Task CompleteIdentityVitals_returns_null_when_not_a_swimmer()
    {
        var (svc, swimmers, _) = Build();
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwimmerProfile?)null);
        Assert.Null(await svc.CompleteIdentityVitalsAsync(Guid.NewGuid(), CompleteReq()));
    }

    [Fact]
    public async Task CompleteIdentityVitals_updates_identity_inserts_exam_and_keeps_first_login()
    {
        var (svc, swimmers, users) = Build();
        var userId = Guid.NewGuid();
        var profile = new SwimmerProfile(userId, "SW-0001", Guid.NewGuid());
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        var user = new AppUser("s.swimmer", "Old Name", Guid.NewGuid(), email: "s@x.io", genderId: Guid.NewGuid(), dob: new DateOnly(2009, 1, 1), isFirstLogin: true);
        users.Setup(u => u.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var newClub = Guid.NewGuid();

        var result = await svc.CompleteIdentityVitalsAsync(userId, CompleteReq() with { NameEn = "New Name", TrainingClubId = newClub });

        Assert.NotNull(result);
        Assert.Equal("New Name", user.NameEn);
        Assert.Equal("01012345678", user.Phone);     // persisted from the request
        Assert.Equal("s@x.io", user.Email);          // preserved
        Assert.True(user.IsFirstLogin);              // NOT cleared — Step 2 completes onboarding
        Assert.Equal(newClub, profile.TrainingClubId);
        swimmers.Verify(r => r.AddExamAsync(It.IsAny<MedicalExam>(), It.IsAny<CancellationToken>()), Times.Once);
        swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteIdentityVitals_accepts_null_blood_type()
    {
        var (svc, swimmers, users) = Build();
        var userId = Guid.NewGuid();
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SwimmerProfile(userId, "SW-0001", Guid.NewGuid()));
        users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new AppUser("s", "S", Guid.NewGuid(), email: "s@x.io"));
        var result = await svc.CompleteIdentityVitalsAsync(userId, CompleteReq() with { BloodTypeId = null });
        Assert.NotNull(result);
    }

    [Fact]
    public async Task CompleteIdentityVitals_throws_when_reference_unknown()
    {
        var (svc, swimmers, users) = Build(refsExist: false);
        var userId = Guid.NewGuid();
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SwimmerProfile(userId, "SW-0001", Guid.NewGuid()));
        users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new AppUser("s", "S", Guid.NewGuid(), email: "s@x.io"));
        await Assert.ThrowsAnyAsync<Exception>(() => svc.CompleteIdentityVitalsAsync(userId, CompleteReq()));
    }

    [Fact]
    public async Task CompleteIdentityVitals_updates_latest_exam_when_one_exists()
    {
        var (svc, swimmers, users) = Build();
        var userId = Guid.NewGuid();
        var profile = new SwimmerProfile(userId, "SW-0001", Guid.NewGuid());
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        var user = new AppUser("s", "S", Guid.NewGuid(), email: "s@x.io", isFirstLogin: true);
        users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var examId = Guid.NewGuid();
        var im = Guid.NewGuid();
        swimmers.Setup(r => r.GetLatestExamAsync(profile.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MedicalExamRow(examId, new DateOnly(2020, 1, 1), 10m, 100m, 40m,
                    null, null, null, null, im, "fit", "Fit", "لائق", im, "fit", "Fit", "لائق", im, "fit", "Fit", "لائق"));
        var trackedExam = new MedicalExam(profile.Id, new DateOnly(2020, 1, 1), im, im, im, null, 10m, 100m, 40m);
        swimmers.Setup(r => r.GetExamTrackedAsync(examId, It.IsAny<CancellationToken>())).ReturnsAsync(trackedExam);

        var result = await svc.CompleteIdentityVitalsAsync(userId, CompleteReq());

        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2026, 1, 1), trackedExam.ExamDate);   // updated in place // CompleteReq().ExamDate
        Assert.Equal(14.5m, trackedExam.Hemoglobin);
        Assert.True(user.IsFirstLogin);   // Step 1 must NOT clear first-login on the update path
        swimmers.Verify(r => r.AddExamAsync(It.IsAny<MedicalExam>(), It.IsAny<CancellationToken>()), Times.Never);
        swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Onboarding Step 2 (guardian + complete) tests ────────────────────────

    [Fact]
    public async Task UpsertOnboardingGuardians_returns_null_when_not_a_swimmer()
    {
        var (svc, swimmers, _) = Build();
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwimmerProfile?)null);
        var father = new GuardianInputDto("Ahmed", "12345678901234", "010");
        var mother = new GuardianInputDto("Sara", "43210987654321", "011");
        Assert.Null(await svc.UpsertOnboardingGuardiansAsync(Guid.NewGuid(), father, mother));
    }

    [Fact]
    public async Task UpsertOnboardingGuardians_inserts_both_and_returns_swimmer_id()
    {
        var (svc, swimmers, _) = Build();
        var userId = Guid.NewGuid();
        var profile = new SwimmerProfile(userId, "SW-0001", Guid.NewGuid());
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        var fatherRel = Guid.NewGuid(); var motherRel = Guid.NewGuid();
        swimmers.Setup(r => r.GetGuardianRelationIdByCodeAsync("father", It.IsAny<CancellationToken>())).ReturnsAsync(fatherRel);
        swimmers.Setup(r => r.GetGuardianRelationIdByCodeAsync("mother", It.IsAny<CancellationToken>())).ReturnsAsync(motherRel);
        swimmers.Setup(r => r.GetGuardianTrackedAsync(profile.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guardian?)null);

        var result = await svc.UpsertOnboardingGuardiansAsync(userId,
            new GuardianInputDto("Ahmed", "12345678901234", "010"),
            new GuardianInputDto("Sara", "43210987654321", "011"));

        Assert.Equal(profile.Id, result);
        swimmers.Verify(r => r.AddGuardianAsync(It.Is<Guardian>(g => g.RelationId == fatherRel && g.Name == "Ahmed"), It.IsAny<CancellationToken>()), Times.Once);
        swimmers.Verify(r => r.AddGuardianAsync(It.Is<Guardian>(g => g.RelationId == motherRel && g.Name == "Sara"), It.IsAny<CancellationToken>()), Times.Once);
        swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteOnboarding_clears_first_login()
    {
        var (svc, swimmers, users) = Build();
        var userId = Guid.NewGuid();
        var profile = new SwimmerProfile(userId, "SW-0001", Guid.NewGuid());
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        var user = new AppUser("s", "S", Guid.NewGuid(), email: "s@x.io", isFirstLogin: true);
        users.Setup(u => u.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        Assert.True(await svc.CompleteOnboardingAsync(userId));
        Assert.False(user.IsFirstLogin);
        users.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteOnboarding_returns_false_when_not_a_swimmer()
    {
        var (svc, swimmers, _) = Build();
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwimmerProfile?)null);
        Assert.False(await svc.CompleteOnboardingAsync(Guid.NewGuid()));
    }

    // ── Onboarding Step 3 (physiological) tests ──────────────────────────────

    [Fact]
    public async Task CompleteOnboardingPhysiological_returns_false_when_not_a_swimmer()
    {
        var (svc, swimmers, _) = Build();
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwimmerProfile?)null);
        Assert.False(await svc.CompleteOnboardingPhysiologicalAsync(Guid.NewGuid(), new CompletePhysiologicalRequest(32.5m, 31.0m, 95m, 94m, 60m, 90m, 75m)));
    }

    [Fact]
    public async Task CompleteOnboardingPhysiological_stores_all_seven_limbs_and_keeps_first_login()
    {
        var (svc, swimmers, users) = Build();
        var userId = Guid.NewGuid();
        var profile = new SwimmerProfile(userId, "SW-0001", Guid.NewGuid());
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        swimmers.Setup(r => r.GetLatestBodyMeasurementTrackedAsync(profile.Id, It.IsAny<CancellationToken>())).ReturnsAsync((BodyMeasurement?)null);

        var ok = await svc.CompleteOnboardingPhysiologicalAsync(userId, new CompletePhysiologicalRequest(32.5m, 31.0m, 95m, 94m, 60m, 90m, 75m));

        Assert.True(ok);
        swimmers.Verify(r => r.AddBodyMeasurementAsync(It.Is<BodyMeasurement>(m =>
            m.SwimmerId == profile.Id &&
            m.RightArmCm == 32.5m && m.LeftArmCm == 31.0m &&
            m.RightLegCm == 95m && m.LeftLegCm == 94m &&
            m.TorsoCm == 60m && m.BustDiameterCm == 90m && m.WaistDiameterCm == 75m),
            It.IsAny<CancellationToken>()), Times.Once);
        swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        users.Verify(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never); // no longer clears first-login
    }

    [Fact]
    public async Task CompleteOnboardingPhysiological_updates_latest_measurement_when_one_exists()
    {
        var (svc, swimmers, _) = Build();
        var userId = Guid.NewGuid();
        var profile = new SwimmerProfile(userId, "SW-0001", Guid.NewGuid());
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        var existing = new BodyMeasurement(profile.Id, 1m, 2m, 3m, 4m, 5m, 6m, 7m);
        swimmers.Setup(r => r.GetLatestBodyMeasurementTrackedAsync(profile.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var ok = await svc.CompleteOnboardingPhysiologicalAsync(userId, new CompletePhysiologicalRequest(32.5m, 31.0m, 95m, 94m, 60m, 90m, 75m));

        Assert.True(ok);
        Assert.Equal(32.5m, existing.RightArmCm);            // updated in place
        Assert.Equal(75m, existing.WaistDiameterCm);
        swimmers.Verify(r => r.AddBodyMeasurementAsync(It.IsAny<BodyMeasurement>(), It.IsAny<CancellationToken>()), Times.Never);
        swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Onboarding Step 4 (InBody) — GetSwimmerIdByUser tests ────────────────

    [Fact]
    public async Task GetSwimmerIdByUser_returns_profile_id()
    {
        var (svc, swimmers, _) = Build();
        var userId = Guid.NewGuid();
        var profile = new SwimmerProfile(userId, "SW-0001", Guid.NewGuid());
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        Assert.Equal(profile.Id, await svc.GetSwimmerIdByUserAsync(userId));
    }

    [Fact]
    public async Task GetSwimmerIdByUser_returns_null_when_not_a_swimmer()
    {
        var (svc, swimmers, _) = Build();
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwimmerProfile?)null);
        Assert.Null(await svc.GetSwimmerIdByUserAsync(Guid.NewGuid()));
    }
}
