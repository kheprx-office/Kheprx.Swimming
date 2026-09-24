using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.ReadModels;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Kheprx.BaseBackend.Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Repositories;

public class SwimmerProfileRepositoryTests
{
    private static IdentityDbContext NewDb() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task GetMaxUidNumber_is_zero_when_empty_and_parses_suffix()
    {
        await using var db = NewDb();
        var repo = new SwimmerProfileRepository(db);
        Assert.Equal(0, await repo.GetMaxUidNumberAsync());

        await repo.AddAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0003", Guid.NewGuid()));
        await repo.AddAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0011", Guid.NewGuid()));
        await repo.SaveChangesAsync();

        Assert.Equal(11, await repo.GetMaxUidNumberAsync());
    }

    [Fact]
    public async Task AddSpecializations_persists_rows()
    {
        await using var db = NewDb();
        var repo = new SwimmerProfileRepository(db);
        var swimmerId = Guid.NewGuid();
        await repo.AddSpecializationsAsync(new[]
        {
            new SwimmerSpecialization(swimmerId, Guid.NewGuid()),
            new SwimmerSpecialization(swimmerId, Guid.NewGuid()),
        });
        await repo.SaveChangesAsync();

        Assert.Equal(2, await db.SwimmerSpecializations.CountAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_returns_true_on_successful_save()
    {
        await using var db = NewDb();
        var repo = new SwimmerProfileRepository(db);
        await repo.AddAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid()));

        var result = await repo.SaveChangesAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task ListAsync_joins_user_club_gender_and_orders_by_name()
    {
        await using var db = NewDb();

        var gender = new Gender("male", "Male", "ذكر");
        var club = new Club("Oasis Main", "الواحة");
        db.Genders.Add(gender);
        db.Clubs.Add(club);

        var userB = new AppUser("b.user", "Bravo", Guid.NewGuid(), genderId: gender.Id, dob: new DateOnly(2010, 1, 1));
        var userA = new AppUser("a.user", "Alpha", Guid.NewGuid(), genderId: gender.Id, dob: new DateOnly(2012, 6, 1));
        db.Users.AddRange(userA, userB);

        db.SwimmerProfiles.Add(new SwimmerProfile(userB.Id, "SW-0002", club.Id));
        db.SwimmerProfiles.Add(new SwimmerProfile(userA.Id, "SW-0001", club.Id));
        await db.SaveChangesAsync();

        var repo = new SwimmerProfileRepository(db);
        var rows = await repo.ListAsync(null);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Alpha", rows[0].NameEn);            // ordered by NameEn
        Assert.Equal("Bravo", rows[1].NameEn);
        Assert.Equal("SW-0001", rows[0].Uid);
        Assert.Equal("Oasis Main", rows[0].ClubNameEn);
        Assert.Equal("male", rows[0].GenderCode);
        Assert.Equal(new DateOnly(2012, 6, 1), rows[0].Dob);
    }

    [Fact]
    public async Task GetLatestExam_returns_null_when_none()
    {
        await using var db = NewDb();
        var repo = new SwimmerProfileRepository(db);
        Assert.Null(await repo.GetLatestExamAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetLatestExam_picks_newest_by_exam_date_and_resolves_refs()
    {
        await using var db = NewDb();
        var swimmerId = Guid.NewGuid();
        var fit = new FitnessAssessment("fit", "Fit", "لائق");
        var blood = new BloodType("O+", "O+", "O+");
        db.FitnessAssessments.Add(fit);
        db.BloodTypes.Add(blood);
        db.MedicalExams.Add(new MedicalExam(swimmerId, new DateOnly(2024, 1, 1), fit.Id, fit.Id, fit.Id, blood.Id, 13.0m, 178m, 71m));
        db.MedicalExams.Add(new MedicalExam(swimmerId, new DateOnly(2024, 10, 8), fit.Id, fit.Id, fit.Id, null, 14.8m, 182m, 74m));
        await db.SaveChangesAsync();

        var row = await new SwimmerProfileRepository(db).GetLatestExamAsync(swimmerId);

        Assert.NotNull(row);
        Assert.Equal(new DateOnly(2024, 10, 8), row!.ExamDate); // newest
        Assert.Equal(14.8m, row.Hemoglobin);
        Assert.Null(row.BloodTypeId);                          // newest had no blood type
        Assert.Equal("Fit", row.InternalMedNameEn);
    }

    [Fact]
    public async Task AddExam_persists_and_GetExamRowById_resolves()
    {
        await using var db = NewDb();
        var swimmerId = Guid.NewGuid();
        var fit = new FitnessAssessment("fit", "Fit", "لائق");
        db.FitnessAssessments.Add(fit);
        await db.SaveChangesAsync();
        var repo = new SwimmerProfileRepository(db);

        var exam = new MedicalExam(swimmerId, new DateOnly(2026, 9, 19), fit.Id, fit.Id, fit.Id, null, 15.0m, 183m, 75m);
        await repo.AddExamAsync(exam);
        await repo.SaveChangesAsync();

        var row = await repo.GetExamRowByIdAsync(exam.Id);
        Assert.NotNull(row);
        Assert.Equal(15.0m, row!.Hemoglobin);
        Assert.Equal("Fit", row.SpineAssessNameEn);
    }

    [Fact]
    public async Task GetProfileById_returns_null_when_missing()
    {
        await using var db = NewDb();
        Assert.Null(await new SwimmerProfileRepository(db).GetProfileByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetProfileById_joins_user_club_gender_and_phone()
    {
        await using var db = NewDb();
        var gender = new Gender("male", "Male", "ذكر");
        var club = new Club("Oasis Main", "الواحة");
        db.Genders.Add(gender); db.Clubs.Add(club);
        var user = new AppUser("a.user", "Alpha", Guid.NewGuid(), nameAr: "ألفا",
            genderId: gender.Id, dob: new DateOnly(2010, 1, 1), phone: "01000000001");
        db.Users.Add(user);
        var profile = new SwimmerProfile(user.Id, "SW-0001", club.Id);
        db.SwimmerProfiles.Add(profile);
        await db.SaveChangesAsync();

        var row = await new SwimmerProfileRepository(db).GetProfileByIdAsync(profile.Id);

        Assert.NotNull(row);
        Assert.Equal("Alpha", row!.NameEn);
        Assert.Equal("ألفا", row.NameAr);
        Assert.Equal("male", row.GenderCode);
        Assert.Equal("Oasis Main", row.TrainingClubNameEn);
        Assert.Equal("01000000001", row.Phone);
        Assert.Equal(new DateOnly(2010, 1, 1), row.Dob);
    }

    [Fact]
    public async Task GetByIdTracked_returns_tracked_profile()
    {
        await using var db = NewDb();
        var profile = new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid());
        db.SwimmerProfiles.Add(profile);
        await db.SaveChangesAsync();

        var tracked = await new SwimmerProfileRepository(db).GetByIdTrackedAsync(profile.Id);
        Assert.NotNull(tracked);
        Assert.Equal(profile.Id, tracked!.Id);
    }

    [Fact]
    public async Task ListExams_returns_all_newest_first_with_ids()
    {
        await using var db = NewDb();
        var swimmerId = Guid.NewGuid();
        var fit = new FitnessAssessment("fit", "Fit", "لائق");
        db.FitnessAssessments.Add(fit);
        var older = new MedicalExam(swimmerId, new DateOnly(2024, 1, 1), fit.Id, fit.Id, fit.Id, null, 13m, 178m, 71m);
        var newer = new MedicalExam(swimmerId, new DateOnly(2024, 10, 8), fit.Id, fit.Id, fit.Id, null, 14.8m, 182m, 74m);
        db.MedicalExams.AddRange(older, newer);
        await db.SaveChangesAsync();

        var rows = await new SwimmerProfileRepository(db).ListExamsAsync(swimmerId);

        Assert.Equal(2, rows.Count);
        Assert.Equal(newer.Id, rows[0].Id);       // newest first
        Assert.Equal(older.Id, rows[1].Id);
        Assert.Equal("Fit", rows[0].InternalMedNameEn);
    }

    [Fact]
    public async Task GetExamTracked_returns_tracked_or_null()
    {
        await using var db = NewDb();
        var fit = new FitnessAssessment("fit", "Fit", "لائق");
        db.FitnessAssessments.Add(fit);
        var exam = new MedicalExam(Guid.NewGuid(), new DateOnly(2024, 1, 1), fit.Id, fit.Id, fit.Id, null, 13m, 178m, 71m);
        db.MedicalExams.Add(exam);
        await db.SaveChangesAsync();
        var repo = new SwimmerProfileRepository(db);

        Assert.NotNull(await repo.GetExamTrackedAsync(exam.Id));
        Assert.Null(await repo.GetExamTrackedAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RemoveExam_deletes_on_save()
    {
        await using var db = NewDb();
        var fit = new FitnessAssessment("fit", "Fit", "لائق");
        db.FitnessAssessments.Add(fit);
        var exam = new MedicalExam(Guid.NewGuid(), new DateOnly(2024, 1, 1), fit.Id, fit.Id, fit.Id, null, 13m, 178m, 71m);
        db.MedicalExams.Add(exam);
        await db.SaveChangesAsync();
        var repo = new SwimmerProfileRepository(db);

        var tracked = await repo.GetExamTrackedAsync(exam.Id);
        repo.RemoveExam(tracked!);
        await repo.SaveChangesAsync();

        Assert.Equal(0, await db.MedicalExams.CountAsync());
    }

    [Fact]
    public async Task ListGuardiansAsync_returns_rows_joined_to_relation_code()
    {
        await using var db = NewDb();
        var repo = new SwimmerProfileRepository(db);
        var swimmerId = Guid.NewGuid();
        var father = new GuardianRelation("father", "Father", "الأب");
        var mother = new GuardianRelation("mother", "Mother", "الأم");
        db.GuardianRelations.AddRange(father, mother);
        db.Guardians.Add(new Guardian(swimmerId, father.Id, "Hassan Ali", "27001010123456", "+201009876543"));
        db.Guardians.Add(new Guardian(swimmerId, mother.Id, "Fatima Ibrahim", "27505050123456", "+201005554444"));
        await db.SaveChangesAsync();

        var rows = await repo.ListGuardiansAsync(swimmerId);

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.RelationCode == "father" && r.Name == "Hassan Ali" && r.NationalId == "27001010123456");
        Assert.Contains(rows, r => r.RelationCode == "mother" && r.Name == "Fatima Ibrahim");
    }

    [Fact]
    public async Task GetGuardianRelationIdByCodeAsync_resolves_seeded_code()
    {
        await using var db = NewDb();
        var repo = new SwimmerProfileRepository(db);
        var father = new GuardianRelation("father", "Father", "الأب");
        db.GuardianRelations.Add(father);
        await db.SaveChangesAsync();

        Assert.Equal(father.Id, await repo.GetGuardianRelationIdByCodeAsync("father"));
        Assert.Null(await repo.GetGuardianRelationIdByCodeAsync("nonexistent"));
    }

    [Fact]
    public async Task GetGuardianTrackedAsync_returns_existing_row_for_swimmer_and_relation()
    {
        await using var db = NewDb();
        var repo = new SwimmerProfileRepository(db);
        var swimmerId = Guid.NewGuid();
        var relationId = Guid.NewGuid();
        db.Guardians.Add(new Guardian(swimmerId, relationId, "Hassan Ali", "27001010123456", "+201009876543"));
        await db.SaveChangesAsync();

        var found = await repo.GetGuardianTrackedAsync(swimmerId, relationId);
        Assert.NotNull(found);
        Assert.Equal("Hassan Ali", found!.Name);
        Assert.Null(await repo.GetGuardianTrackedAsync(swimmerId, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetLatestBodyMeasurementAsync_returns_null_when_none()
    {
        await using var db = NewDb();
        var repo = new SwimmerProfileRepository(db);

        Assert.Null(await repo.GetLatestBodyMeasurementAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetLatestBodyMeasurementAsync_returns_a_deterministic_row_when_several_exist()
    {
        await using var db = NewDb();
        var swimmerId = Guid.NewGuid();
        // The entity stamps MeasuredAt = today, so both rows share a date; the OrderBy(MeasuredAt desc).ThenBy(Id desc)
        // tiebreak makes the lookup deterministic and non-null. (Cross-day ordering is exercised via the DB, not in-memory.)
        db.BodyMeasurements.Add(new BodyMeasurement(swimmerId, 70m, 70m, 90m, 90m, 50m, 90m, 70m));
        db.BodyMeasurements.Add(new BodyMeasurement(swimmerId, 78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m));
        await db.SaveChangesAsync();
        var repo = new SwimmerProfileRepository(db);

        var row = await repo.GetLatestBodyMeasurementAsync(swimmerId);

        Assert.NotNull(row);
        Assert.Contains(row!.RightArmCm, new[] { 70m, 78.5m }); // one of the two same-day rows (the Id-max)
    }

    [Fact]
    public async Task AddBodyMeasurementAsync_inserts_and_is_readable()
    {
        await using var db = NewDb();
        var swimmerId = Guid.NewGuid();
        var repo = new SwimmerProfileRepository(db);

        await repo.AddBodyMeasurementAsync(new BodyMeasurement(swimmerId, 78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m));
        await db.SaveChangesAsync();

        var row = await repo.GetLatestBodyMeasurementAsync(swimmerId);
        Assert.NotNull(row);
        Assert.Equal(76.5m, row!.WaistDiameterCm);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), row.MeasuredAt);
    }

    [Fact]
    public async Task CountCreatedSinceAsync_counts_only_profiles_at_or_after_the_cutoff()
    {
        await using var db = NewDb();
        var repo = new SwimmerProfileRepository(db);
        await repo.AddAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid()));
        await repo.AddAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0002", Guid.NewGuid()));
        await repo.SaveChangesAsync();

        // Both were created "now" (ctor stamps DateTime.UtcNow).
        Assert.Equal(2, await repo.CountCreatedSinceAsync(DateTime.UtcNow.AddDays(-1)));
        Assert.Equal(0, await repo.CountCreatedSinceAsync(DateTime.UtcNow.AddDays(1)));
    }

    [Fact]
    public async Task GetStrokeCountsAsync_counts_swimmers_per_stroke_including_multi_stroke_overlap()
    {
        await using var db = NewDb();
        var free = new Stroke("free", "Freestyle", "حرة");
        var back = new Stroke("back", "Backstroke", "ظهر");
        db.Strokes.AddRange(free, back);

        var swimmerA = Guid.NewGuid();
        var swimmerB = Guid.NewGuid();
        // A specializes in both strokes; B only in freestyle.
        db.SwimmerSpecializations.AddRange(
            new SwimmerSpecialization(swimmerA, free.Id),
            new SwimmerSpecialization(swimmerA, back.Id),
            new SwimmerSpecialization(swimmerB, free.Id));
        await db.SaveChangesAsync();

        var repo = new SwimmerProfileRepository(db);
        var rows = await repo.GetStrokeCountsAsync();

        Assert.Equal(2, rows.Single(r => r.Code == "free").Count); // A + B
        Assert.Equal(1, rows.Single(r => r.Code == "back").Count); // A only (overlap)
        Assert.Equal("حرة", rows.Single(r => r.Code == "free").NameAr);
    }
}
