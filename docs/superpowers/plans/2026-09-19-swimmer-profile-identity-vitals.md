# Swimmer Profile — Identity & Vitals Tab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a swimmer profile page (reached from the roster at `swimmers/:id`) whose first tab, Identity & Vitals, reads real data and supports functional edit — identity → update `app_user`; vitals → record a new dated `medical_exam`.

**Architecture:** Backend adds two Identity-module tables (`reference.fitness_assessment`, `athlete.medical_exam`), a composite read endpoint (`GET /api/swimmers/{id}`), an identity update (`PUT …/identity`), a vitals write (`POST …/medical-exams`), and a `fitness-assessments` lookup — all mirroring the existing `BloodType`/`SwimmerService` slices. Frontend adds a dedicated `swimmer-profile` feature slice (clean-arch: model/repository/usecases/data/presentation) with a signal-based viewmodel, plus roster row navigation.

**Tech Stack:** .NET (EF Core + Postgres, FluentValidation, xUnit + Moq), Angular 20 (standalone components, signals, Jest), Tailwind.

**Spec:** `docs/superpowers/specs/2026-09-19-swimmer-profile-identity-vitals-design.md`

## Global Constraints

- **Module placement:** all new backend entities live in the **Identity module** (single `IdentityDbContext`); their Postgres schemas follow the diagram — `athlete.medical_exam`, `reference.fitness_assessment`.
- **National ID:** never shown or sent anywhere on this page (it is not part of the swimmer schema).
- **Vitals edit = POST a new dated `medical_exam`** (append-only history; section shows the latest). Identity edit = update `app_user` via the existing `AppUser.UpdateProfile(...)` preserving email + gender.
- **Auth:** `GET /api/swimmers/{id}` and `GET /api/reference/fitness-assessments` = `[Authorize]`; `PUT …/identity` and `POST …/medical-exams` = `[Authorize(Roles="head_coach,captain")]`. `UserRole` union is only `'head_coach' | 'captain'`.
- **Seed values:** `fitness_assessment` = fit / unfit / under_review (EN: Fit / Unfit / Under Review; AR: لائق / غير لائق / قيد المراجعة).
- **Bilingual:** every reference row and label carries `nameEn` + `nameAr?`; every new i18n key added to BOTH `en.json` and `ar.json`.
- **Tests before impl (TDD).** Backend tests: `dotnet test <project>`. Frontend tests: `npx jest <path>`. Compile checks: backend `dotnet build`; frontend `npm run build`.
- **DLL lock:** stop any running backend before `dotnet test`/`dotnet build` (running backend locks DLLs).
- **Commits:** the user asked to defer committing — the executing agent must confirm with the user before the first `git commit`. Never push. Branch already checked out: `feat/swimmer-profile-identity-vitals`.
- **Validators are auto-discovered** by the module's FluentValidation assembly scan — no manual registration needed (same as `CreateObservationRequestValidator`).

---

# Phase 1 — Backend

## Task 1: `FitnessAssessment` reference entity

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/FitnessAssessment.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/FitnessAssessmentTests.cs`

**Interfaces:**
- Produces: `FitnessAssessment` — `sealed`, props `Guid Id`, `string Code`, `string NameEn`, `string? NameAr`; ctor `FitnessAssessment(string code, string nameEn, string? nameAr = null)`.

- [ ] **Step 1: Write the failing test**

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class FitnessAssessmentTests
{
    [Fact]
    public void Ctor_trims_and_generates_id()
    {
        var fa = new FitnessAssessment("  fit ", " Fit ", " لائق ");
        Assert.NotEqual(Guid.Empty, fa.Id);
        Assert.Equal("fit", fa.Code);
        Assert.Equal("Fit", fa.NameEn);
        Assert.Equal("لائق", fa.NameAr);
    }

    [Fact]
    public void Ctor_nulls_blank_arabic_name()
    {
        var fa = new FitnessAssessment("unfit", "Unfit", "   ");
        Assert.Null(fa.NameAr);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FitnessAssessmentTests`
Expected: FAIL — `FitnessAssessment` does not exist (compile error).

- [ ] **Step 3: Write minimal implementation** (copy of `BloodType.cs`)

```csharp
namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class FitnessAssessment
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }

    private FitnessAssessment() { } // EF Core

    public FitnessAssessment(string code, string nameEn, string? nameAr = null)
    {
        Id = Guid.NewGuid();
        Code = code.Trim();
        NameEn = nameEn.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FitnessAssessmentTests`
Expected: PASS.

- [ ] **Step 5: Commit** (after user authorizes commits)

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/FitnessAssessment.cs backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/FitnessAssessmentTests.cs
git commit -m "feat(identity): add FitnessAssessment reference entity"
```

---

## Task 2: `FitnessAssessment` repository, EF config, DbSet, DI, seed, migration

**Files:**
- Create: `.../Identity.Domain/Repositories/IFitnessAssessmentRepository.cs`
- Create: `.../Identity.Infrastructure/Repositories/FitnessAssessmentRepository.cs`
- Create: `.../Identity.Infrastructure/Configurations/FitnessAssessmentConfiguration.cs`
- Modify: `.../Identity.Infrastructure/Data/IdentityDbContext.cs` (add DbSet)
- Modify: `.../Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs` (DI)
- Modify: `.../Identity.Infrastructure/Data/IdentitySeeder.cs` (seed 3 rows)
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/FitnessAssessmentRepositoryTests.cs`

**Interfaces:**
- Consumes: `FitnessAssessment` (Task 1).
- Produces: `IFitnessAssessmentRepository` — `Task<IReadOnlyList<FitnessAssessment>> GetAllAsync(CancellationToken)`, `Task<bool> ExistsAsync(Guid id, CancellationToken)`. `IdentityDbContext.FitnessAssessments` DbSet.

- [ ] **Step 1: Write the failing test**

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Kheprx.BaseBackend.Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Repositories;

public class FitnessAssessmentRepositoryTests
{
    private static IdentityDbContext NewDb() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task GetAllAsync_returns_rows_ordered_by_name()
    {
        await using var db = NewDb();
        db.FitnessAssessments.Add(new FitnessAssessment("unfit", "Unfit", "غير لائق"));
        db.FitnessAssessments.Add(new FitnessAssessment("fit", "Fit", "لائق"));
        await db.SaveChangesAsync();

        var rows = await new FitnessAssessmentRepository(db).GetAllAsync();

        Assert.Equal(2, rows.Count);
        Assert.Equal("Fit", rows[0].NameEn); // ordered by NameEn
    }

    [Fact]
    public async Task ExistsAsync_is_true_only_for_known_ids()
    {
        await using var db = NewDb();
        var fa = new FitnessAssessment("fit", "Fit", "لائق");
        db.FitnessAssessments.Add(fa);
        await db.SaveChangesAsync();
        var repo = new FitnessAssessmentRepository(db);

        Assert.True(await repo.ExistsAsync(fa.Id));
        Assert.False(await repo.ExistsAsync(Guid.NewGuid()));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FitnessAssessmentRepositoryTests`
Expected: FAIL — `FitnessAssessmentRepository` / `db.FitnessAssessments` do not exist.

- [ ] **Step 3: Write minimal implementation**

`IFitnessAssessmentRepository.cs` (mirror `IBloodTypeRepository`):

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
namespace Kheprx.BaseBackend.Identity.Domain.Repositories;
public interface IFitnessAssessmentRepository
{
    Task<IReadOnlyList<FitnessAssessment>> GetAllAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
}
```

`FitnessAssessmentRepository.cs` (mirror `BloodTypeRepository`, order by NameEn):

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class FitnessAssessmentRepository : IFitnessAssessmentRepository
{
    private readonly IdentityDbContext _db;
    public FitnessAssessmentRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<FitnessAssessment>> GetAllAsync(CancellationToken ct = default)
        => await _db.FitnessAssessments.AsNoTracking().OrderBy(f => f.NameEn).ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _db.FitnessAssessments.AsNoTracking().AnyAsync(f => f.Id == id, ct);
}
```

`FitnessAssessmentConfiguration.cs` (mirror `BloodTypeConfiguration`, `reference` schema):

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class FitnessAssessmentConfiguration : IEntityTypeConfiguration<FitnessAssessment>
{
    public void Configure(EntityTypeBuilder<FitnessAssessment> builder)
    {
        builder.ToTable("fitness_assessment", "reference");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(f => f.Code).IsUnique();
        builder.Property(f => f.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(f => f.NameAr).HasMaxLength(100);
    }
}
```

In `IdentityDbContext.cs`, add next to `BloodTypes`:

```csharp
    public DbSet<FitnessAssessment> FitnessAssessments => Set<FitnessAssessment>();
```

In `IdentityModuleExtensions.cs`, add next to the `IBloodTypeRepository` registration:

```csharp
        services.AddScoped<IFitnessAssessmentRepository, FitnessAssessmentRepository>();
```

In `IdentitySeeder.cs`, add the call inside `SeedAsync` right after `await EnsureBloodTypes(db, ct);`:

```csharp
        await EnsureFitnessAssessments(db, ct);
```

…and add the method (mirror `EnsureObservationCategories`):

```csharp
    private static async Task EnsureFitnessAssessments(IdentityDbContext db, CancellationToken ct)
    {
        var rows = new (string Code, string En, string Ar)[]
        {
            ("fit", "Fit", "لائق"),
            ("unfit", "Unfit", "غير لائق"),
            ("under_review", "Under Review", "قيد المراجعة"),
        };
        foreach (var (code, en, ar) in rows)
            if (!await db.FitnessAssessments.AnyAsync(f => f.Code == code, ct))
                await db.FitnessAssessments.AddAsync(new FitnessAssessment(code, en, ar), ct);
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FitnessAssessmentRepositoryTests`
Expected: PASS.

- [ ] **Step 5: Generate the migration and build**

Stop any running backend first. Run from `backend/`:

```bash
dotnet ef migrations add AddFitnessAssessmentAndMedicalExam \
  --project src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --startup-project Kheprx.BaseBackend.Api \
  --context IdentityDbContext
dotnet build
```

> This migration is authored once here and will also capture the `medical_exam` table added in Task 5. If Task 5 runs later, instead add a second migration `AddMedicalExam` at that point. Simplest path: **defer generating the migration until after Task 5** so a single migration covers both tables. If you defer, skip the `dotnet ef` command here and run it at Task 5 Step 5.

Expected: build succeeds; migration file created under `Identity.Infrastructure/Migrations/`.

- [ ] **Step 6: Commit** (after user authorizes)

```bash
git add backend/src/Modules/Identity/ backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/FitnessAssessmentRepositoryTests.cs
git commit -m "feat(identity): fitness_assessment lookup — repo, config, seed"
```

---

## Task 3: `GET /api/reference/fitness-assessments`

**Files:**
- Modify: `.../Identity.Application/Services/Interfaces/IReferenceService.cs`
- Modify: `.../Identity.Application/Services/ReferenceService.cs` (ctor dep + method)
- Modify: `.../Identity.Application/Resources/ReferenceMessages.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/ReferenceController.cs`
- Modify: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/ReferenceServiceTests.cs` (helper + test)
- Modify: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/ReferenceControllerTests.cs` (test)

**Interfaces:**
- Consumes: `IFitnessAssessmentRepository` (Task 2), `CodedLookupDto`.
- Produces: `IReferenceService.GetFitnessAssessmentsAsync(CancellationToken) → Task<IReadOnlyList<CodedLookupDto>>`; `ReferenceController.FitnessAssessments(...)`.

- [ ] **Step 1: Write the failing tests**

Add to `ReferenceServiceTests` — first extend the `NewService` helper to take a fitness repo, then a mapping test:

```csharp
    // update the helper signature + body:
    private static ReferenceService NewService(
        IClubRepository? clubs = null, IBloodTypeRepository? blood = null,
        IStrokeRepository? strokes = null, IGenderRepository? genders = null,
        IObservationCategoryRepository? categories = null,
        IFitnessAssessmentRepository? fitness = null)
        => new(clubs ?? Mock.Of<IClubRepository>(), blood ?? Mock.Of<IBloodTypeRepository>(),
               strokes ?? Mock.Of<IStrokeRepository>(), genders ?? Mock.Of<IGenderRepository>(),
               categories ?? Mock.Of<IObservationCategoryRepository>(),
               fitness ?? Mock.Of<IFitnessAssessmentRepository>());

    [Fact]
    public async Task GetFitnessAssessments_maps_entities_to_coded_dtos()
    {
        var fitness = new Mock<IFitnessAssessmentRepository>();
        fitness.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new[] { new Kheprx.BaseBackend.Identity.Domain.Entities.FitnessAssessment("fit", "Fit", "لائق") });

        var result = await NewService(fitness: fitness.Object).GetFitnessAssessmentsAsync();

        Assert.Single(result);
        Assert.Equal("fit", result[0].Code);
        Assert.Equal("Fit", result[0].NameEn);
    }
```

Add to `ReferenceControllerTests`:

```csharp
    [Fact]
    public async Task FitnessAssessments_returns_200_with_coded_lookups()
    {
        var svc = new Mock<IReferenceService>();
        svc.Setup(s => s.GetFitnessAssessmentsAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<CodedLookupDto> { new(Guid.NewGuid(), "fit", "Fit", "لائق") });

        var result = await new ReferenceController(svc.Object).FitnessAssessments(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<CodedLookupDto>>>(ok.Value);
        Assert.Equal("fit", body.Data![0].Code);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter ReferenceServiceTests` and `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter ReferenceControllerTests`
Expected: FAIL — methods/ctor arg don't exist.

- [ ] **Step 3: Write minimal implementation**

`IReferenceService.cs` — add:

```csharp
    Task<IReadOnlyList<CodedLookupDto>> GetFitnessAssessmentsAsync(CancellationToken ct = default);
```

`ReferenceService.cs` — add the field + ctor param + method:

```csharp
    private readonly IFitnessAssessmentRepository _fitness;
```
Add `IFitnessAssessmentRepository fitness` as the last ctor parameter and `_fitness = fitness;` in the body. Then:

```csharp
    public async Task<IReadOnlyList<CodedLookupDto>> GetFitnessAssessmentsAsync(CancellationToken ct = default)
        => (await _fitness.GetAllAsync(ct)).Select(f => new CodedLookupDto(f.Id, f.Code, f.NameEn, f.NameAr)).ToList();
```

`ReferenceMessages.cs` — add to `Success`:

```csharp
        public static string FitnessAssessmentsListed(string lang) => lang switch { "ar" => "تقييمات اللياقة", _ => "Fitness assessments" };
```

`ReferenceController.cs` — add the action (mirror `BloodTypes`):

```csharp
    /// <summary>Lists all fitness assessment results (Internal Medicine / Heart / Spine selectors).</summary>
    [HttpGet("fitness-assessments")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CodedLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CodedLookupDto>>>> FitnessAssessments(CancellationToken ct)
    {
        var data = await _service.GetFitnessAssessmentsAsync(ct);
        var body = ApiResponse<IReadOnlyList<CodedLookupDto>>.Success(
            ReferenceMessages.Success.FitnessAssessmentsListed(AppLanguage.Current), data);
        return Ok(body);
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run both filters from Step 2. Expected: PASS.

- [ ] **Step 5: Commit** (after user authorizes)

```bash
git add backend/
git commit -m "feat(reference): GET /api/reference/fitness-assessments"
```

---

## Task 4: `MedicalExam` entity

**Files:**
- Create: `.../Identity.Domain/Entities/MedicalExam.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/MedicalExamTests.cs`

**Interfaces:**
- Produces: `MedicalExam` — `sealed`; props `Guid Id`, `Guid SwimmerId`, `DateOnly ExamDate`, `Guid InternalMedId`, `Guid HeartAssessId`, `Guid SpineAssessId`, `Guid? BloodTypeId`, `decimal Hemoglobin`, `decimal HeightCm`, `decimal WeightKg`, `DateTime CreatedAt`. Ctor: `MedicalExam(Guid swimmerId, DateOnly examDate, Guid internalMedId, Guid heartAssessId, Guid spineAssessId, Guid? bloodTypeId, decimal hemoglobin, decimal heightCm, decimal weightKg)`.

- [ ] **Step 1: Write the failing test**

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class MedicalExamTests
{
    [Fact]
    public void Ctor_assigns_fields_and_stamps_id_and_createdAt()
    {
        var swimmerId = Guid.NewGuid();
        var im = Guid.NewGuid(); var ha = Guid.NewGuid(); var sa = Guid.NewGuid();
        var bt = Guid.NewGuid();

        var exam = new MedicalExam(swimmerId, new DateOnly(2026, 9, 19), im, ha, sa, bt, 14.8m, 182.0m, 74.0m);

        Assert.NotEqual(Guid.Empty, exam.Id);
        Assert.Equal(swimmerId, exam.SwimmerId);
        Assert.Equal(new DateOnly(2026, 9, 19), exam.ExamDate);
        Assert.Equal(im, exam.InternalMedId);
        Assert.Equal(bt, exam.BloodTypeId);
        Assert.Equal(14.8m, exam.Hemoglobin);
        Assert.True(exam.CreatedAt <= DateTime.UtcNow && exam.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void Ctor_allows_null_blood_type()
    {
        var exam = new MedicalExam(Guid.NewGuid(), new DateOnly(2026, 9, 19),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 14.0m, 180m, 70m);
        Assert.Null(exam.BloodTypeId);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter MedicalExamTests`
Expected: FAIL — `MedicalExam` does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class MedicalExam
{
    public Guid Id { get; private set; }
    public Guid SwimmerId { get; private set; }
    public DateOnly ExamDate { get; private set; }
    public Guid InternalMedId { get; private set; }
    public Guid HeartAssessId { get; private set; }
    public Guid SpineAssessId { get; private set; }
    public Guid? BloodTypeId { get; private set; }
    public decimal Hemoglobin { get; private set; }
    public decimal HeightCm { get; private set; }
    public decimal WeightKg { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private MedicalExam() { } // EF Core

    public MedicalExam(Guid swimmerId, DateOnly examDate, Guid internalMedId, Guid heartAssessId,
        Guid spineAssessId, Guid? bloodTypeId, decimal hemoglobin, decimal heightCm, decimal weightKg)
    {
        Id = Guid.NewGuid();
        SwimmerId = swimmerId;
        ExamDate = examDate;
        InternalMedId = internalMedId;
        HeartAssessId = heartAssessId;
        SpineAssessId = spineAssessId;
        BloodTypeId = bloodTypeId;
        Hemoglobin = hemoglobin;
        HeightCm = heightCm;
        WeightKg = weightKg;
        CreatedAt = DateTime.UtcNow;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter MedicalExamTests`
Expected: PASS.

- [ ] **Step 5: Commit** (after user authorizes)

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/MedicalExam.cs backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/MedicalExamTests.cs
git commit -m "feat(identity): add MedicalExam entity"
```

---

## Task 5: `MedicalExam` persistence + vitals read models + exam repo methods

**Files:**
- Create: `.../Identity.Infrastructure/Configurations/MedicalExamConfiguration.cs`
- Create: `.../Identity.Domain/ReadModels/MedicalExamRow.cs`
- Modify: `.../Identity.Infrastructure/Data/IdentityDbContext.cs` (DbSet)
- Modify: `.../Identity.Domain/Repositories/ISwimmerProfileRepository.cs` (add methods)
- Modify: `.../Identity.Infrastructure/Repositories/SwimmerProfileRepository.cs` (impl)
- Migration: `Identity.Infrastructure/Migrations/…AddFitnessAssessmentAndMedicalExam`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/SwimmerProfileRepositoryTests.cs` (add cases)

**Interfaces:**
- Consumes: `MedicalExam` (Task 4), `FitnessAssessment` (Task 2), `BloodType`.
- Produces:
  - `MedicalExamRow` record (resolved names — see impl).
  - `ISwimmerProfileRepository.AddExamAsync(MedicalExam, CancellationToken) → Task`
  - `ISwimmerProfileRepository.GetLatestExamAsync(Guid swimmerId, CancellationToken) → Task<MedicalExamRow?>`
  - `ISwimmerProfileRepository.GetExamRowByIdAsync(Guid examId, CancellationToken) → Task<MedicalExamRow?>`
  - `IdentityDbContext.MedicalExams` DbSet.

- [ ] **Step 1: Write the failing tests** (append to `SwimmerProfileRepositoryTests`)

```csharp
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
```

Add the required `using` lines to the test file: `using Kheprx.BaseBackend.Identity.Domain.Entities;` is already present.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter SwimmerProfileRepositoryTests`
Expected: FAIL — `db.MedicalExams`, `GetLatestExamAsync`, `MedicalExamRow` don't exist.

- [ ] **Step 3: Write minimal implementation**

`MedicalExamRow.cs`:

```csharp
namespace Kheprx.BaseBackend.Identity.Domain.ReadModels;

/// <summary>Flattened latest-exam row — vitals joined to blood_type + fitness_assessment (×3).</summary>
public sealed record MedicalExamRow(
    DateOnly ExamDate,
    decimal Hemoglobin,
    decimal HeightCm,
    decimal WeightKg,
    Guid? BloodTypeId,
    string? BloodTypeCode,
    string? BloodTypeNameEn,
    string? BloodTypeNameAr,
    Guid InternalMedId,
    string InternalMedCode,
    string InternalMedNameEn,
    string? InternalMedNameAr,
    Guid HeartAssessId,
    string HeartAssessCode,
    string HeartAssessNameEn,
    string? HeartAssessNameAr,
    Guid SpineAssessId,
    string SpineAssessCode,
    string SpineAssessNameEn,
    string? SpineAssessNameAr);
```

`MedicalExamConfiguration.cs` (`athlete` schema, real FKs):

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class MedicalExamConfiguration : IEntityTypeConfiguration<MedicalExam>
{
    public void Configure(EntityTypeBuilder<MedicalExam> builder)
    {
        builder.ToTable("medical_exam", "athlete");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.SwimmerId).IsRequired();
        builder.Property(e => e.ExamDate).IsRequired();
        builder.Property(e => e.Hemoglobin).HasPrecision(4, 1).IsRequired();
        builder.Property(e => e.HeightCm).HasPrecision(5, 1).IsRequired();
        builder.Property(e => e.WeightKg).HasPrecision(5, 1).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.HasIndex(e => e.SwimmerId);
        builder.HasOne<SwimmerProfile>().WithMany().HasForeignKey(e => e.SwimmerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FitnessAssessment>().WithMany().HasForeignKey(e => e.InternalMedId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FitnessAssessment>().WithMany().HasForeignKey(e => e.HeartAssessId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FitnessAssessment>().WithMany().HasForeignKey(e => e.SpineAssessId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BloodType>().WithMany().HasForeignKey(e => e.BloodTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

In `IdentityDbContext.cs`, add:

```csharp
    public DbSet<MedicalExam> MedicalExams => Set<MedicalExam>();
```

In `ISwimmerProfileRepository.cs`, add:

```csharp
    Task AddExamAsync(MedicalExam exam, CancellationToken ct = default);
    Task<MedicalExamRow?> GetLatestExamAsync(Guid swimmerId, CancellationToken ct = default);
    Task<MedicalExamRow?> GetExamRowByIdAsync(Guid examId, CancellationToken ct = default);
```

Ensure `ISwimmerProfileRepository.cs` has `using Kheprx.BaseBackend.Identity.Domain.Entities;` and `using Kheprx.BaseBackend.Identity.Domain.ReadModels;`.

In `SwimmerProfileRepository.cs`, add a shared projection helper + the three methods:

```csharp
    public Task AddExamAsync(MedicalExam exam, CancellationToken ct = default)
        => _db.MedicalExams.AddAsync(exam, ct).AsTask();

    public Task<MedicalExamRow?> GetLatestExamAsync(Guid swimmerId, CancellationToken ct = default)
        => ExamRows()
            .Where(x => x.e.SwimmerId == swimmerId)
            .OrderByDescending(x => x.e.ExamDate).ThenByDescending(x => x.e.CreatedAt)
            .Select(Project())
            .FirstOrDefaultAsync(ct);

    public Task<MedicalExamRow?> GetExamRowByIdAsync(Guid examId, CancellationToken ct = default)
        => ExamRows()
            .Where(x => x.e.Id == examId)
            .Select(Project())
            .FirstOrDefaultAsync(ct);

    // exam LEFT-joined to blood_type, INNER-joined to fitness_assessment ×3
    private IQueryable<ExamJoin> ExamRows() =>
        from e in _db.MedicalExams.AsNoTracking()
        join im in _db.FitnessAssessments.AsNoTracking() on e.InternalMedId equals im.Id
        join ha in _db.FitnessAssessments.AsNoTracking() on e.HeartAssessId equals ha.Id
        join sa in _db.FitnessAssessments.AsNoTracking() on e.SpineAssessId equals sa.Id
        join bt in _db.BloodTypes.AsNoTracking() on e.BloodTypeId equals bt.Id into btj
        from bt in btj.DefaultIfEmpty()
        select new ExamJoin(e, im, ha, sa, bt);

    private static System.Linq.Expressions.Expression<Func<ExamJoin, MedicalExamRow>> Project() =>
        x => new MedicalExamRow(
            x.e.ExamDate, x.e.Hemoglobin, x.e.HeightCm, x.e.WeightKg,
            x.bt == null ? (Guid?)null : x.bt.Id,
            x.bt == null ? null : x.bt.Code,
            x.bt == null ? null : x.bt.NameEn,
            x.bt == null ? null : x.bt.NameAr,
            x.im.Id, x.im.Code, x.im.NameEn, x.im.NameAr,
            x.ha.Id, x.ha.Code, x.ha.NameEn, x.ha.NameAr,
            x.sa.Id, x.sa.Code, x.sa.NameEn, x.sa.NameAr);

    private sealed record ExamJoin(MedicalExam e, FitnessAssessment im, FitnessAssessment ha, FitnessAssessment sa, BloodType? bt);
```

Add `using Kheprx.BaseBackend.Identity.Domain.ReadModels;` (already present) — and the file already imports Entities via the join types (add `using Kheprx.BaseBackend.Identity.Domain.Entities;` if missing).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter SwimmerProfileRepositoryTests`
Expected: PASS.

- [ ] **Step 5: Generate the combined migration and build**

Stop the backend. From `backend/` (if not already generated in Task 2):

```bash
dotnet ef migrations add AddFitnessAssessmentAndMedicalExam \
  --project src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --startup-project Kheprx.BaseBackend.Api \
  --context IdentityDbContext
dotnet build
```

Open the generated migration and confirm it creates `reference.fitness_assessment` and `athlete.medical_exam` with the FK columns. Expected: build succeeds.

- [ ] **Step 6: Commit** (after user authorizes)

```bash
git add backend/
git commit -m "feat(identity): medical_exam table + latest-exam read models & queries"
```

---

## Task 6: Swimmer profile identity read model + repository methods

**Files:**
- Create: `.../Identity.Domain/ReadModels/SwimmerProfileRow.cs`
- Modify: `.../Identity.Domain/Repositories/ISwimmerProfileRepository.cs`
- Modify: `.../Identity.Infrastructure/Repositories/SwimmerProfileRepository.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/SwimmerProfileRepositoryTests.cs` (add cases)

**Interfaces:**
- Produces:
  - `SwimmerProfileRow(Guid Id, string Uid, string NameEn, string? NameAr, DateOnly? Dob, string? GenderCode, string? Phone, string? TrainingClubNameEn, string? TrainingClubNameAr)`
  - `ISwimmerProfileRepository.GetProfileByIdAsync(Guid id, CancellationToken) → Task<SwimmerProfileRow?>`
  - `ISwimmerProfileRepository.GetByIdTrackedAsync(Guid id, CancellationToken) → Task<SwimmerProfile?>`

- [ ] **Step 1: Write the failing tests** (append to `SwimmerProfileRepositoryTests`)

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter SwimmerProfileRepositoryTests`
Expected: FAIL — methods/read model don't exist.

- [ ] **Step 3: Write minimal implementation**

`SwimmerProfileRow.cs`:

```csharp
namespace Kheprx.BaseBackend.Identity.Domain.ReadModels;

/// <summary>Flattened identity row for the profile page — profile + user + club + gender.</summary>
public sealed record SwimmerProfileRow(
    Guid Id,
    string Uid,
    string NameEn,
    string? NameAr,
    DateOnly? Dob,
    string? GenderCode,
    string? Phone,
    string? TrainingClubNameEn,
    string? TrainingClubNameAr);
```

`ISwimmerProfileRepository.cs` — add:

```csharp
    Task<SwimmerProfileRow?> GetProfileByIdAsync(Guid id, CancellationToken ct = default);
    Task<SwimmerProfile?> GetByIdTrackedAsync(Guid id, CancellationToken ct = default);
```

`SwimmerProfileRepository.cs` — add (mirror `ListAsync`'s join, single id, include phone):

```csharp
    public Task<SwimmerProfileRow?> GetProfileByIdAsync(Guid id, CancellationToken ct = default)
        => (from p in _db.SwimmerProfiles.AsNoTracking()
            where p.Id == id
            join u in _db.Users.AsNoTracking() on p.UserId equals u.Id
            join c in _db.Clubs.AsNoTracking() on p.TrainingClubId equals c.Id into clubs
            from c in clubs.DefaultIfEmpty()
            join g in _db.Genders.AsNoTracking() on u.GenderId equals g.Id into genders
            from g in genders.DefaultIfEmpty()
            select new SwimmerProfileRow(
                p.Id, p.Uid, u.NameEn, u.NameAr, u.Dob,
                g == null ? null : g.Code, u.Phone,
                c == null ? null : c.NameEn, c == null ? null : c.NameAr))
           .FirstOrDefaultAsync(ct);

    public Task<SwimmerProfile?> GetByIdTrackedAsync(Guid id, CancellationToken ct = default)
        => _db.SwimmerProfiles.FirstOrDefaultAsync(p => p.Id == id, ct);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter SwimmerProfileRepositoryTests`
Expected: PASS.

- [ ] **Step 5: Commit** (after user authorizes)

```bash
git add backend/
git commit -m "feat(identity): swimmer profile identity read model + queries"
```

---

## Task 7: Profile DTOs + `SwimmerService.GetProfileAsync` + `GET /api/swimmers/{id}`

**Files:**
- Modify: `.../Identity.Application/DTOs/SwimmerDtos.cs`
- Modify: `.../Identity.Application/Services/Interfaces/ISwimmerService.cs`
- Modify: `.../Identity.Application/Services/SwimmerService.cs`
- Modify: `.../Identity.Application/Resources/SwimmerMessages.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs`

**Interfaces:**
- Consumes: `GetProfileByIdAsync`, `GetLatestExamAsync` (Tasks 5–6), `CodedLookupDto`, `ComputeAge` (existing private).
- Produces:
  - `SwimmerProfileDto(SwimmerIdentityDto Identity, SwimmerVitalsDto? Vitals)`
  - `SwimmerIdentityDto(Guid Id, string Uid, string NameEn, string? NameAr, DateOnly? Dob, int? Age, string GenderCode, string? Phone, string? TrainingClubNameEn, string? TrainingClubNameAr)`
  - `SwimmerVitalsDto(DateOnly ExamDate, CodedLookupDto? BloodType, decimal Hemoglobin, decimal HeightCm, decimal WeightKg, CodedLookupDto InternalMed, CodedLookupDto HeartAssess, CodedLookupDto SpineAssess)`
  - `ISwimmerService.GetProfileAsync(Guid id, CancellationToken) → Task<SwimmerProfileDto?>`

- [ ] **Step 1: Write the failing tests**

Add to `SwimmerServiceTests` (the `Build()` helper already exposes the `swimmers` mock):

```csharp
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
        swimmers.Setup(r => r.GetProfileByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SwimmerProfileRow(id, "SW-0001", "Alpha", null, null, null, null, null, null));
        swimmers.Setup(r => r.GetLatestExamAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MedicalExamRow(new DateOnly(2024, 10, 8), 14.8m, 182m, 74m,
                    null, null, null, null,
                    im, "fit", "Fit", "لائق",
                    im, "fit", "Fit", "لائق",
                    im, "fit", "Fit", "لائق"));

        var dto = await svc.GetProfileAsync(id);

        Assert.NotNull(dto!.Vitals);
        Assert.Equal(14.8m, dto.Vitals!.Hemoglobin);
        Assert.Null(dto.Vitals.BloodType);
        Assert.Equal("Fit", dto.Vitals.InternalMed.NameEn);
    }
```

Add the `using`s to the test file if missing: `using Kheprx.BaseBackend.Identity.Domain.ReadModels;` (already imported).

Add to `SwimmersControllerTests`:

```csharp
    [Fact]
    public async Task GetById_returns_200_with_profile()
    {
        var svc = new Mock<ISwimmerService>();
        var id = Guid.NewGuid();
        var identity = new SwimmerIdentityDto(id, "SW-0001", "Alpha", null, null, 15, "male", null, "Oasis Main", null);
        svc.Setup(s => s.GetProfileAsync(id, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new SwimmerProfileDto(identity, null));

        var result = await new SwimmersController(svc.Object).GetById(id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<SwimmerProfileDto>>(ok.Value);
        Assert.Equal("SW-0001", body.Data!.Identity.Uid);
    }

    [Fact]
    public async Task GetById_returns_404_when_service_returns_null()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.GetProfileAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync((SwimmerProfileDto?)null);

        var result = await new SwimmersController(svc.Object).GetById(Guid.NewGuid(), CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter SwimmerServiceTests` and `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter SwimmersControllerTests`
Expected: FAIL — DTOs / `GetProfileAsync` / `GetById` don't exist.

- [ ] **Step 3: Write minimal implementation**

`SwimmerDtos.cs` — append:

```csharp
/// <summary>Composite swimmer profile (GET /api/swimmers/{id}) — identity + latest vitals (null when no exam).</summary>
public sealed record SwimmerProfileDto(SwimmerIdentityDto Identity, SwimmerVitalsDto? Vitals);

public sealed record SwimmerIdentityDto(
    Guid Id, string Uid, string NameEn, string? NameAr, DateOnly? Dob, int? Age,
    string GenderCode, string? Phone, string? TrainingClubNameEn, string? TrainingClubNameAr);

public sealed record SwimmerVitalsDto(
    DateOnly ExamDate, CodedLookupDto? BloodType, decimal Hemoglobin, decimal HeightCm, decimal WeightKg,
    CodedLookupDto InternalMed, CodedLookupDto HeartAssess, CodedLookupDto SpineAssess);
```

`ISwimmerService.cs` — add:

```csharp
    Task<SwimmerProfileDto?> GetProfileAsync(Guid id, CancellationToken ct = default);
```

`SwimmerService.cs` — add the method (uses existing `_swimmers` + `ComputeAge`):

```csharp
    public async Task<SwimmerProfileDto?> GetProfileAsync(Guid id, CancellationToken ct = default)
    {
        var row = await _swimmers.GetProfileByIdAsync(id, ct);
        if (row is null) return null;

        var identity = new SwimmerIdentityDto(
            row.Id, row.Uid, row.NameEn, row.NameAr, row.Dob, ComputeAge(row.Dob),
            row.GenderCode ?? string.Empty, row.Phone, row.TrainingClubNameEn, row.TrainingClubNameAr);

        var exam = await _swimmers.GetLatestExamAsync(id, ct);
        return new SwimmerProfileDto(identity, MapVitals(exam));
    }

    private static SwimmerVitalsDto? MapVitals(Kheprx.BaseBackend.Identity.Domain.ReadModels.MedicalExamRow? e)
    {
        if (e is null) return null;
        var blood = e.BloodTypeId is null
            ? null
            : new CodedLookupDto(e.BloodTypeId.Value, e.BloodTypeCode!, e.BloodTypeNameEn!, e.BloodTypeNameAr);
        return new SwimmerVitalsDto(
            e.ExamDate, blood, e.Hemoglobin, e.HeightCm, e.WeightKg,
            new CodedLookupDto(e.InternalMedId, e.InternalMedCode, e.InternalMedNameEn, e.InternalMedNameAr),
            new CodedLookupDto(e.HeartAssessId, e.HeartAssessCode, e.HeartAssessNameEn, e.HeartAssessNameAr),
            new CodedLookupDto(e.SpineAssessId, e.SpineAssessCode, e.SpineAssessNameEn, e.SpineAssessNameAr));
    }
```

`SwimmerMessages.cs` — add to `Success`:

```csharp
        public static string ProfileRetrieved(string lang) => lang switch { "ar" => "بيانات السبّاح", _ => "Swimmer profile" };
```
…and to `Errors`:

```csharp
        public static string ProfileNotFound(string lang) => lang switch { "ar" => "السبّاح غير موجود", _ => "Swimmer not found" };
```

`SwimmersController.cs` — add the action inside a new region (mirror `List`):

```csharp
    #region Profile — GET api/swimmers/{id} — identity + latest vitals

    /// <summary>Returns a swimmer's profile: identity plus the latest medical exam (vitals).</summary>
    /// <response code="200">The swimmer profile.</response>
    /// <response code="404">No swimmer with that id.</response>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SwimmerProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SwimmerProfileDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SwimmerProfileDto>>> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _service.GetProfileAsync(id, ct);
        if (dto is null)
        {
            var nf = ApiResponse<SwimmerProfileDto>.Failure(
                SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        var body = ApiResponse<SwimmerProfileDto>.Success(SwimmerMessages.Success.ProfileRetrieved(AppLanguage.Current), dto);
        return Ok(body);
    }

    #endregion
```

- [ ] **Step 4: Run tests to verify they pass**

Run both filters from Step 2. Expected: PASS.

- [ ] **Step 5: Commit** (after user authorizes)

```bash
git add backend/
git commit -m "feat(swimmers): GET /api/swimmers/{id} profile (identity + latest vitals)"
```

---

## Task 8: Identity update — `PUT /api/swimmers/{id}/identity`

**Files:**
- Modify: `.../Identity.Application/DTOs/SwimmerDtos.cs`
- Modify: `.../Identity.Application/Validators/UserRequestValidators.cs`
- Modify: `.../Identity.Application/Services/Interfaces/ISwimmerService.cs`
- Modify: `.../Identity.Application/Services/SwimmerService.cs`
- Modify: `.../Identity.Application/Resources/SwimmerMessages.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs`
- Test: `backend/tests/…/Services/SwimmerServiceTests.cs`, `…/Api.UnitTests/SwimmersControllerTests.cs`, `…/Validators/` (new file)

**Interfaces:**
- Consumes: `GetByIdTrackedAsync` (Task 6), `IUserRepository.GetByIdAsync`/`SaveChangesAsync`, `AppUser.UpdateProfile`.
- Produces: `UpdateSwimmerIdentityRequest(string NameEn, string? NameAr, DateOnly Dob, string? Phone)`; `ISwimmerService.UpdateIdentityAsync(Guid id, UpdateSwimmerIdentityRequest, CancellationToken) → Task<bool>`.

- [ ] **Step 1: Write the failing tests**

Service tests — the `Build()` helper needs `users.GetByIdAsync` + `users.SaveChangesAsync`. Add these two setups inside `Build()` (right after the existing `users` setups):

```csharp
        users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new AppUser("z", "Zed", Guid.NewGuid(), email: "z@x.io", genderId: Guid.NewGuid(), dob: new DateOnly(2009, 1, 1)));
        users.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
```

Then the tests:

```csharp
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
```

Controller tests:

```csharp
    [Fact]
    public async Task UpdateIdentity_returns_200_when_updated()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.UpdateIdentityAsync(It.IsAny<Guid>(), It.IsAny<UpdateSwimmerIdentityRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(true);
        var req = new UpdateSwimmerIdentityRequest("New Name", null, new DateOnly(2010, 1, 1), "01000000009");

        var result = await new SwimmersController(svc.Object).UpdateIdentity(Guid.NewGuid(), req, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateIdentity_returns_404_when_not_found()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.UpdateIdentityAsync(It.IsAny<Guid>(), It.IsAny<UpdateSwimmerIdentityRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(false);
        var req = new UpdateSwimmerIdentityRequest("New Name", null, new DateOnly(2010, 1, 1), null);

        var result = await new SwimmersController(svc.Object).UpdateIdentity(Guid.NewGuid(), req, CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }
```

Validator test — create `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Validators/UpdateSwimmerIdentityRequestValidatorTests.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Validators;

public class UpdateSwimmerIdentityRequestValidatorTests
{
    private readonly UpdateSwimmerIdentityRequestValidator _v = new();

    [Fact]
    public void Valid_request_passes()
        => Assert.True(_v.Validate(new UpdateSwimmerIdentityRequest("Alpha", "ألفا", new DateOnly(2010, 1, 1), "01000000001")).IsValid);

    [Fact]
    public void Empty_name_fails()
        => Assert.False(_v.Validate(new UpdateSwimmerIdentityRequest("", null, new DateOnly(2010, 1, 1), null)).IsValid);

    [Fact]
    public void Future_dob_fails()
        => Assert.False(_v.Validate(new UpdateSwimmerIdentityRequest("Alpha", null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), null)).IsValid);

    [Fact]
    public void Bad_phone_fails()
        => Assert.False(_v.Validate(new UpdateSwimmerIdentityRequest("Alpha", null, new DateOnly(2010, 1, 1), "12345")).IsValid);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the three affected test projects with filters `SwimmerServiceTests`, `SwimmersControllerTests`, `UpdateSwimmerIdentityRequestValidatorTests`.
Expected: FAIL — request/validator/method/action don't exist.

- [ ] **Step 3: Write minimal implementation**

`SwimmerDtos.cs` — append:

```csharp
/// <summary>Payload to update a swimmer's identity (PUT /api/swimmers/{id}/identity).</summary>
public sealed record UpdateSwimmerIdentityRequest(string NameEn, string? NameAr, DateOnly Dob, string? Phone);
```

`UserRequestValidators.cs` — add (reuse existing message resources + `UserValidationRules.PhonePattern`):

```csharp
public sealed class UpdateSwimmerIdentityRequestValidator : AbstractValidator<UpdateSwimmerIdentityRequest>
{
    public UpdateSwimmerIdentityRequestValidator()
    {
        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage(_ => CommonMessages.Errors.FullNameRequired(AppLanguage.Current))
            .MaximumLength(200).WithMessage(_ => CommonMessages.Errors.FullNameTooLong(AppLanguage.Current));
        RuleFor(x => x.NameAr).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.NameAr));
        RuleFor(x => x.Dob)
            .NotEqual(default(DateOnly)).WithMessage(_ => SwimmerMessages.Errors.DobRequired(AppLanguage.Current))
            .LessThan(_ => DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage(_ => SwimmerMessages.Errors.DobInPast(AppLanguage.Current));
        RuleFor(x => x.Phone)
            .Matches(UserValidationRules.PhonePattern).WithMessage(_ => UserMessages.Errors.PhoneInvalid(AppLanguage.Current))
            .When(x => !string.IsNullOrEmpty(x.Phone));
    }
}
```

`ISwimmerService.cs` — add:

```csharp
    Task<bool> UpdateIdentityAsync(Guid id, UpdateSwimmerIdentityRequest request, CancellationToken ct = default);
```

`SwimmerService.cs` — add:

```csharp
    public async Task<bool> UpdateIdentityAsync(Guid id, UpdateSwimmerIdentityRequest request, CancellationToken ct = default)
    {
        var profile = await _swimmers.GetByIdTrackedAsync(id, ct);
        if (profile is null) return false;

        var user = await _users.GetByIdAsync(profile.UserId, ct);
        if (user is null) return false;

        user.UpdateProfile(request.NameEn, request.NameAr, user.Email, user.GenderId, request.Dob, request.Phone);
        await _users.SaveChangesAsync(ct);
        return true;
    }
```

`SwimmerMessages.cs` — add to `Success`:

```csharp
        public static string IdentityUpdated(string lang) => lang switch { "ar" => "تم تحديث بيانات الهوية", _ => "Identity updated" };
```

`SwimmersController.cs` — add:

```csharp
    #region Update identity — PUT api/swimmers/{id}/identity

    /// <summary>Updates a swimmer's identity (name EN/AR, DOB, phone). Head Coach or Captain only.</summary>
    /// <response code="200">Identity updated.</response>
    /// <response code="404">No swimmer with that id.</response>
    [HttpPut("{id:guid}/identity")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateIdentity(Guid id, UpdateSwimmerIdentityRequest request, CancellationToken ct)
    {
        var updated = await _service.UpdateIdentityAsync(id, request, ct);
        if (!updated)
        {
            var nf = ApiResponse<object>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<object>.Success(SwimmerMessages.Success.IdentityUpdated(AppLanguage.Current), null));
    }

    #endregion
```

- [ ] **Step 4: Run tests to verify they pass**

Run the three filters from Step 2. Expected: PASS.

- [ ] **Step 5: Commit** (after user authorizes)

```bash
git add backend/
git commit -m "feat(swimmers): PUT /api/swimmers/{id}/identity"
```

---

## Task 9: Vitals write — `POST /api/swimmers/{id}/medical-exams`

**Files:**
- Modify: `.../Identity.Application/DTOs/SwimmerDtos.cs`
- Modify: `.../Identity.Application/Validators/UserRequestValidators.cs`
- Modify: `.../Identity.Application/Services/Interfaces/ISwimmerService.cs`
- Modify: `.../Identity.Application/Services/SwimmerService.cs` (ctor +2 deps, method)
- Modify: `.../Identity.Application/Resources/SwimmerMessages.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs`
- Test: `…/Services/SwimmerServiceTests.cs` (update `Build()` ctor), `…/Api.UnitTests/SwimmersControllerTests.cs`, `…/Validators/` (new file)

**Interfaces:**
- Consumes: `GetByIdTrackedAsync`, `AddExamAsync`, `GetExamRowByIdAsync`, `SaveChangesAsync` (Tasks 5–6), `IFitnessAssessmentRepository.ExistsAsync` (Task 2), `IBloodTypeRepository.ExistsAsync` (existing), `MedicalExam` ctor.
- Produces: `CreateMedicalExamRequest(DateOnly ExamDate, Guid? BloodTypeId, decimal Hemoglobin, decimal HeightCm, decimal WeightKg, Guid InternalMedId, Guid HeartAssessId, Guid SpineAssessId)`; `ISwimmerService.CreateExamAsync(Guid id, CreateMedicalExamRequest, CancellationToken) → Task<SwimmerVitalsDto?>`.

- [ ] **Step 1: Write the failing tests**

First, **update `SwimmerServiceTests.Build()`** to construct the service with the two new deps. Add mocks and pass them:

```csharp
        var bloodTypes = new Mock<IBloodTypeRepository>(); bloodTypes.Setup(b => b.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(refsExist);
        var fitness = new Mock<IFitnessAssessmentRepository>(); fitness.Setup(f => f.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(refsExist);
```
…and change the `new SwimmerService(...)` call to pass `fitness.Object, bloodTypes.Object` as the two new trailing args **before** `opts` per the new ctor order (see Step 3), then also expose `swimmers` as before. The updated construction:

```csharp
        var svc = new SwimmerService(swimmers.Object, users.Object, roles.Object,
            clubs.Object, strokes.Object, genders.Object, fitness.Object, bloodTypes.Object, hasher.Object, opts);
```

Then the tests:

```csharp
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
                .ReturnsAsync(new MedicalExamRow(new DateOnly(2026, 9, 19), 15m, 183m, 75m,
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
```

Controller tests:

```csharp
    [Fact]
    public async Task CreateExam_returns_201_with_vitals()
    {
        var svc = new Mock<ISwimmerService>();
        var im = new CodedLookupDto(Guid.NewGuid(), "fit", "Fit", "لائق");
        svc.Setup(s => s.CreateExamAsync(It.IsAny<Guid>(), It.IsAny<CreateMedicalExamRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new SwimmerVitalsDto(new DateOnly(2026, 9, 19), null, 15m, 183m, 75m, im, im, im));
        var req = new CreateMedicalExamRequest(new DateOnly(2026, 9, 19), null, 15m, 183m, 75m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var result = await new SwimmersController(svc.Object).CreateExam(Guid.NewGuid(), req, CancellationToken.None);

        var ok = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, ok.StatusCode);
    }

    [Fact]
    public async Task CreateExam_returns_404_when_swimmer_missing()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.CreateExamAsync(It.IsAny<Guid>(), It.IsAny<CreateMedicalExamRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync((SwimmerVitalsDto?)null);
        var req = new CreateMedicalExamRequest(new DateOnly(2026, 9, 19), null, 15m, 183m, 75m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var result = await new SwimmersController(svc.Object).CreateExam(Guid.NewGuid(), req, CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }
```

Validator test — `…/Validators/CreateMedicalExamRequestValidatorTests.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Validators;

public class CreateMedicalExamRequestValidatorTests
{
    private readonly CreateMedicalExamRequestValidator _v = new();
    private static CreateMedicalExamRequest Ok() => new(
        new DateOnly(2026, 9, 19), null, 15m, 183m, 75m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact] public void Valid_passes() => Assert.True(_v.Validate(Ok()).IsValid);
    [Fact] public void Empty_assessment_fails() => Assert.False(_v.Validate(Ok() with { InternalMedId = Guid.Empty }).IsValid);
    [Fact] public void Nonpositive_measure_fails() => Assert.False(_v.Validate(Ok() with { HeightCm = 0m }).IsValid);
    [Fact] public void Future_exam_date_fails() => Assert.False(_v.Validate(Ok() with { ExamDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) }).IsValid);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run `SwimmerServiceTests`, `SwimmersControllerTests`, `CreateMedicalExamRequestValidatorTests`.
Expected: FAIL — ctor/request/validator/method/action don't exist.

- [ ] **Step 3: Write minimal implementation**

`SwimmerDtos.cs` — append:

```csharp
/// <summary>Payload to record a new dated medical exam (POST /api/swimmers/{id}/medical-exams).</summary>
public sealed record CreateMedicalExamRequest(
    DateOnly ExamDate, Guid? BloodTypeId, decimal Hemoglobin, decimal HeightCm, decimal WeightKg,
    Guid InternalMedId, Guid HeartAssessId, Guid SpineAssessId);
```

`SwimmerMessages.cs` — add to `Success` + `Errors`:

```csharp
        public static string ExamRecorded(string lang) => lang switch { "ar" => "تم تسجيل الفحص", _ => "Exam recorded" };
```
```csharp
        public static string MeasurementInvalid(string lang) => lang switch { "ar" => "قيمة قياس غير صالحة", _ => "A measurement value is invalid" };
        public static string AssessmentRequired(string lang) => lang switch { "ar" => "نتيجة التقييم مطلوبة", _ => "An assessment result is required" };
        public static string ExamDateInvalid(string lang) => lang switch { "ar" => "تاريخ الفحص غير صالح", _ => "The exam date is invalid" };
```

`UserRequestValidators.cs` — add:

```csharp
public sealed class CreateMedicalExamRequestValidator : AbstractValidator<CreateMedicalExamRequest>
{
    public CreateMedicalExamRequestValidator()
    {
        RuleFor(x => x.ExamDate)
            .NotEqual(default(DateOnly)).WithMessage(_ => SwimmerMessages.Errors.ExamDateInvalid(AppLanguage.Current))
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage(_ => SwimmerMessages.Errors.ExamDateInvalid(AppLanguage.Current));
        RuleFor(x => x.InternalMedId).NotEqual(Guid.Empty).WithMessage(_ => SwimmerMessages.Errors.AssessmentRequired(AppLanguage.Current));
        RuleFor(x => x.HeartAssessId).NotEqual(Guid.Empty).WithMessage(_ => SwimmerMessages.Errors.AssessmentRequired(AppLanguage.Current));
        RuleFor(x => x.SpineAssessId).NotEqual(Guid.Empty).WithMessage(_ => SwimmerMessages.Errors.AssessmentRequired(AppLanguage.Current));
        RuleFor(x => x.Hemoglobin).GreaterThan(0m).LessThan(30m).WithMessage(_ => SwimmerMessages.Errors.MeasurementInvalid(AppLanguage.Current));
        RuleFor(x => x.HeightCm).GreaterThan(0m).LessThan(300m).WithMessage(_ => SwimmerMessages.Errors.MeasurementInvalid(AppLanguage.Current));
        RuleFor(x => x.WeightKg).GreaterThan(0m).LessThan(500m).WithMessage(_ => SwimmerMessages.Errors.MeasurementInvalid(AppLanguage.Current));
    }
}
```

`ISwimmerService.cs` — add:

```csharp
    Task<SwimmerVitalsDto?> CreateExamAsync(Guid id, CreateMedicalExamRequest request, CancellationToken ct = default);
```

`SwimmerService.cs` — add two fields, extend the ctor, add the method + a private reference guard:

Add fields:
```csharp
    private readonly IFitnessAssessmentRepository _fitness;
    private readonly IBloodTypeRepository _bloodTypes;
```
Change the ctor signature to insert the two deps **before** `IPasswordHasher hasher`:
```csharp
    public SwimmerService(
        ISwimmerProfileRepository swimmers, IUserRepository users, IRoleRepository roles,
        IClubRepository clubs, IStrokeRepository strokes, IGenderRepository genders,
        IFitnessAssessmentRepository fitness, IBloodTypeRepository bloodTypes,
        IPasswordHasher hasher, IOptions<AccountCreationOptions> options)
```
Assign `_fitness = fitness;` and `_bloodTypes = bloodTypes;` in the body. Then:

```csharp
    public async Task<SwimmerVitalsDto?> CreateExamAsync(Guid id, CreateMedicalExamRequest request, CancellationToken ct = default)
    {
        var profile = await _swimmers.GetByIdTrackedAsync(id, ct);
        if (profile is null) return null;

        await EnsureExamReferencesExist(request, ct);

        var exam = new MedicalExam(id, request.ExamDate, request.InternalMedId, request.HeartAssessId,
            request.SpineAssessId, request.BloodTypeId, request.Hemoglobin, request.HeightCm, request.WeightKg);
        await _swimmers.AddExamAsync(exam, ct);
        await _swimmers.SaveChangesAsync(ct);

        return MapVitals(await _swimmers.GetExamRowByIdAsync(exam.Id, ct));
    }

    private async Task EnsureExamReferencesExist(CreateMedicalExamRequest r, CancellationToken ct)
    {
        if (!await _fitness.ExistsAsync(r.InternalMedId, ct)) throw new InvalidUserException("Unknown internal medicine assessment.");
        if (!await _fitness.ExistsAsync(r.HeartAssessId, ct)) throw new InvalidUserException("Unknown heart assessment.");
        if (!await _fitness.ExistsAsync(r.SpineAssessId, ct)) throw new InvalidUserException("Unknown spine assessment.");
        if (r.BloodTypeId is { } bt && !await _bloodTypes.ExistsAsync(bt, ct)) throw new InvalidUserException("Unknown blood type.");
    }
```

`IdentityModuleExtensions.cs` needs no change — `SwimmerService` deps are resolved by DI; `IFitnessAssessmentRepository` is registered (Task 2) and `IBloodTypeRepository` already is.

`SwimmersController.cs` — add:

```csharp
    #region Record exam — POST api/swimmers/{id}/medical-exams

    /// <summary>Records a new dated medical exam for a swimmer (updates the shown vitals). Head Coach or Captain only.</summary>
    /// <response code="201">Exam recorded; returns the new vitals.</response>
    /// <response code="404">No swimmer with that id.</response>
    [HttpPost("{id:guid}/medical-exams")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<SwimmerVitalsDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<SwimmerVitalsDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SwimmerVitalsDto>>> CreateExam(Guid id, CreateMedicalExamRequest request, CancellationToken ct)
    {
        var vitals = await _service.CreateExamAsync(id, request, ct);
        if (vitals is null)
        {
            var nf = ApiResponse<SwimmerVitalsDto>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        var body = ApiResponse<SwimmerVitalsDto>.Success(SwimmerMessages.Success.ExamRecorded(AppLanguage.Current), vitals);
        return StatusCode(StatusCodes.Status201Created, body);
    }

    #endregion
```

- [ ] **Step 4: Run tests to verify they pass**

Run the three filters from Step 2, then the whole backend suite:
`dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests` and `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests`.
Expected: PASS (confirms the `SwimmerService` ctor change didn't break other tests).

- [ ] **Step 5: Commit** (after user authorizes)

```bash
git add backend/
git commit -m "feat(swimmers): POST /api/swimmers/{id}/medical-exams (record vitals)"
```

---

# Phase 2 — Frontend

Run frontend tests with `npx jest <path>` and compile with `npm run build` from `frontend/`.

## Task 10: `reference` feature — fitness-assessments lookup

**Files:**
- Modify: `frontend/src/app/features/reference/domain/repositories/reference.repository.ts`
- Modify: `frontend/src/app/features/reference/data/repositories/reference.repository.impl.ts`
- Create: `frontend/src/app/features/reference/domain/usecases/load-fitness-assessments.use-case.ts`
- Test: `frontend/src/app/features/reference/testing/data/repositories/reference.repository.impl.spec.ts` (add case)
- Test: `frontend/src/app/features/reference/testing/domain/usecases/load-fitness-assessments.use-case.spec.ts`

**Interfaces:**
- Produces: `IReferenceRepository.getFitnessAssessments(): Promise<CodedLookupListDtoRs>`; `LoadFitnessAssessmentsUseCase extends UseCase<void, LookupItem[]>`.

- [ ] **Step 1: Write the failing tests**

Add to `reference.repository.impl.spec.ts` (mirror the blood-types case already there):

```typescript
  it('getFitnessAssessments GETs the reference endpoint', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.getFitnessAssessments();
    expect(http.get).toHaveBeenCalledWith('/api/reference/fitness-assessments');
  });
```

Create `load-fitness-assessments.use-case.spec.ts` (mirror the blood-types use-case spec pattern):

```typescript
import { TestBed } from '@angular/core/testing';
import { LoadFitnessAssessmentsUseCase } from '@features/reference/domain/usecases/load-fitness-assessments.use-case';
import { REFERENCE_REPOSITORY, IReferenceRepository } from '@features/reference/domain/repositories/reference.repository';

function build(repo: Partial<IReferenceRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: REFERENCE_REPOSITORY, useValue: repo }] });
  return TestBed.inject(LoadFitnessAssessmentsUseCase);
}

describe('LoadFitnessAssessmentsUseCase', () => {
  it('maps the coded lookup list to LookupItems', async () => {
    const repo = { getFitnessAssessments: async () => ({ data: [{ id: 'f1', code: 'fit', nameEn: 'Fit', nameAr: 'لائق' }] }) };
    const r = await build(repo as IReferenceRepository).run();
    expect(r.ok).toBe(true);
    if (r.ok) { expect(r.data).toHaveLength(1); expect(r.data[0].code).toBe('fit'); }
  });

  it('fails validation when the payload is malformed', async () => {
    const repo = { getFitnessAssessments: async () => ({ data: [{ id: '' }] }) };
    const r = await build(repo as unknown as IReferenceRepository).run();
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `npx jest reference` (from `frontend/`)
Expected: FAIL — `getFitnessAssessments` / `LoadFitnessAssessmentsUseCase` don't exist.

- [ ] **Step 3: Write minimal implementation**

`reference.repository.ts` — add to the interface:

```typescript
  getFitnessAssessments(): Promise<CodedLookupListDtoRs>;
```

`reference.repository.impl.ts` — add the method:

```typescript
  getFitnessAssessments(): Promise<CodedLookupListDtoRs> {
    return this.http.get<CodedLookupListDtoRs>('/api/reference/fitness-assessments');
  }
```

`load-fitness-assessments.use-case.ts` (copy of `load-blood-types.use-case.ts`):

```typescript
// load-fitness-assessments.use-case.ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { REFERENCE_REPOSITORY } from '@features/reference/domain/repositories/reference.repository';
import { isCodedLookupListValid } from '@features/reference/data/dto/reference.dto';
import { LookupItem } from '@features/reference/domain/model/reference';

@Injectable({ providedIn: 'root' })
export class LoadFitnessAssessmentsUseCase extends UseCase<void, LookupItem[]> {
  private readonly repo = inject(REFERENCE_REPOSITORY);
  constructor() { super('LoadFitnessAssessments'); }
  protected async execute(): Promise<LookupItem[]> {
    const res = await this.repo.getFitnessAssessments();
    if (!isCodedLookupListValid(res.data)) throw new AppError('Invalid fitness assessments received', 'validation');
    return res.data.map((f) => ({ id: f.id, code: f.code, nameEn: f.nameEn, nameAr: f.nameAr }));
  }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `npx jest reference`. Expected: PASS.

- [ ] **Step 5: Commit** (after user authorizes)

```bash
git add frontend/src/app/features/reference/
git commit -m "feat(reference): fitness-assessments lookup use-case + repo method"
```

---

## Task 11: `swimmer-profile` domain models, repository port, DTOs

**Files:**
- Create: `frontend/src/app/features/swimmer-profile/domain/model/swimmer-profile.ts`
- Create: `frontend/src/app/features/swimmer-profile/data/dto/swimmer-profile.dto.ts`
- Create: `frontend/src/app/features/swimmer-profile/data/dto/update-identity.dto.ts`
- Create: `frontend/src/app/features/swimmer-profile/data/dto/create-medical-exam.dto.ts`
- Create: `frontend/src/app/features/swimmer-profile/domain/repositories/swimmer-profile.repository.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/data/dto/swimmer-profile.dto.spec.ts`

**Interfaces:**
- Produces: `SwimmerProfile`, `SwimmerIdentity`, `SwimmerVitals` models; DTOs `SwimmerProfileItemDtoRs`, `UpdateIdentityDtoRq`, `UpdateIdentityItemDtoRs`, `CreateMedicalExamDtoRq`, `CreatedVitalsItemDtoRs`; guard `isSwimmerProfileDtoRsValid`; `ISwimmerProfileRepository` + `SWIMMER_PROFILE_REPOSITORY` token.

- [ ] **Step 1: Write the failing test**

`swimmer-profile.dto.spec.ts`:

```typescript
import { isSwimmerProfileDtoRsValid } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';

describe('isSwimmerProfileDtoRsValid', () => {
  it('accepts a well-formed identity payload (vitals may be null)', () => {
    const dto = { identity: { id: 's1', uid: 'SW-1', nameEn: 'Ahmed', nameAr: null, dob: '2010-01-01', age: 16, genderCode: 'male', phone: null, trainingClubNameEn: 'Oasis', trainingClubNameAr: null }, vitals: null };
    expect(isSwimmerProfileDtoRsValid(dto)).toBe(true);
  });

  it('rejects a payload missing identity fields', () => {
    expect(isSwimmerProfileDtoRsValid({ identity: { id: 's1' }, vitals: null })).toBe(false);
    expect(isSwimmerProfileDtoRsValid(null)).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx jest swimmer-profile.dto`
Expected: FAIL — module doesn't exist.

- [ ] **Step 3: Write minimal implementation**

`domain/model/swimmer-profile.ts`:

```typescript
import { LookupItem } from '@features/reference/domain/model/reference';

export interface SwimmerIdentity {
  id: string;
  uid: string;
  nameEn: string;
  nameAr: string | null;
  dob: string | null;       // ISO yyyy-mm-dd
  age: number | null;
  genderCode: string;
  phone: string | null;
  trainingClubNameEn: string | null;
  trainingClubNameAr: string | null;
}

export interface SwimmerVitals {
  examDate: string;         // ISO yyyy-mm-dd
  bloodType: LookupItem | null;
  hemoglobin: number;
  heightCm: number;
  weightKg: number;
  internalMed: LookupItem;
  heartAssess: LookupItem;
  spineAssess: LookupItem;
}

export interface SwimmerProfile {
  identity: SwimmerIdentity;
  vitals: SwimmerVitals | null;
}
```

`data/dto/swimmer-profile.dto.ts`:

```typescript
// swimmer-profile.dto.ts — swimmer profile response DTOs (API_FLOW convention).
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface CodedRefDtoRs { id: string; code: string; nameEn: string; nameAr: string | null; }

export interface SwimmerIdentityDtoRs {
  id: string; uid: string; nameEn: string; nameAr: string | null;
  dob: string | null; age: number | null; genderCode: string; phone: string | null;
  trainingClubNameEn: string | null; trainingClubNameAr: string | null;
}

export interface SwimmerVitalsDtoRs {
  examDate: string; bloodType: CodedRefDtoRs | null;
  hemoglobin: number; heightCm: number; weightKg: number;
  internalMed: CodedRefDtoRs; heartAssess: CodedRefDtoRs; spineAssess: CodedRefDtoRs;
}

export interface SwimmerProfileDtoRs { identity: SwimmerIdentityDtoRs; vitals: SwimmerVitalsDtoRs | null; }
export interface SwimmerProfileItemDtoRs extends BaseResponseRs<SwimmerProfileDtoRs> {}

export function isSwimmerProfileDtoRsValid(dto: unknown): dto is SwimmerProfileDtoRs {
  const d = dto as SwimmerProfileDtoRs;
  return !!d && !!d.identity
    && typeof d.identity.id === 'string' && typeof d.identity.uid === 'string'
    && typeof d.identity.nameEn === 'string' && typeof d.identity.genderCode === 'string';
}
```

`data/dto/update-identity.dto.ts`:

```typescript
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface UpdateIdentityDtoRq { nameEn: string; nameAr?: string | null; dob: string; phone?: string | null; }
export interface UpdateIdentityItemDtoRs extends BaseResponseRs<unknown> {}
```

`data/dto/create-medical-exam.dto.ts`:

```typescript
import { BaseResponseRs } from '@core/network/api/base-response-rs';
import { SwimmerVitalsDtoRs } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';

export interface CreateMedicalExamDtoRq {
  examDate: string;
  bloodTypeId?: string | null;
  hemoglobin: number;
  heightCm: number;
  weightKg: number;
  internalMedId: string;
  heartAssessId: string;
  spineAssessId: string;
}
export interface CreatedVitalsItemDtoRs extends BaseResponseRs<SwimmerVitalsDtoRs> {}
```

`domain/repositories/swimmer-profile.repository.ts`:

```typescript
import { InjectionToken } from '@angular/core';
import { SwimmerProfileItemDtoRs } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';
import { UpdateIdentityDtoRq, UpdateIdentityItemDtoRs } from '@features/swimmer-profile/data/dto/update-identity.dto';
import { CreateMedicalExamDtoRq, CreatedVitalsItemDtoRs } from '@features/swimmer-profile/data/dto/create-medical-exam.dto';

export interface ISwimmerProfileRepository {
  getProfile(id: string): Promise<SwimmerProfileItemDtoRs>;
  updateIdentity(id: string, rq: UpdateIdentityDtoRq): Promise<UpdateIdentityItemDtoRs>;
  createExam(id: string, rq: CreateMedicalExamDtoRq): Promise<CreatedVitalsItemDtoRs>;
}

export const SWIMMER_PROFILE_REPOSITORY = new InjectionToken<ISwimmerProfileRepository>('SWIMMER_PROFILE_REPOSITORY');
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npx jest swimmer-profile.dto`. Expected: PASS.

- [ ] **Step 5: Commit** (after user authorizes)

```bash
git add frontend/src/app/features/swimmer-profile/
git commit -m "feat(swimmer-profile): domain models, DTOs, repository port"
```

---

## Task 12: `swimmer-profile` repository impl + providers + app wiring

**Files:**
- Create: `frontend/src/app/features/swimmer-profile/data/repositories/swimmer-profile.repository.impl.ts`
- Create: `frontend/src/app/features/swimmer-profile/data/swimmer-profile.providers.ts`
- Create: `frontend/src/app/features/swimmer-profile/index.ts`
- Modify: `frontend/src/app/app.config.ts` (register providers)
- Test: `frontend/src/app/features/swimmer-profile/testing/data/repositories/swimmer-profile.repository.impl.spec.ts`

**Interfaces:**
- Produces: `SwimmerProfileRepositoryImpl` bound to `SWIMMER_PROFILE_REPOSITORY` via `SWIMMER_PROFILE_PROVIDERS`.

- [ ] **Step 1: Write the failing test** (mirror `swimmer.repository.impl.spec.ts`)

```typescript
import { TestBed } from '@angular/core/testing';
import { SwimmerProfileRepositoryImpl } from '@features/swimmer-profile/data/repositories/swimmer-profile.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('SwimmerProfileRepositoryImpl', () => {
  const http = { get: jest.fn(), put: jest.fn(), post: jest.fn() } as unknown as HttpClientService;
  let repo: SwimmerProfileRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({ providers: [SwimmerProfileRepositoryImpl, { provide: HttpClientService, useValue: http }] });
    repo = TestBed.inject(SwimmerProfileRepositoryImpl);
  });

  it('getProfile GETs /api/swimmers/{id}', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: {} });
    await repo.getProfile('s1');
    expect(http.get).toHaveBeenCalledWith('/api/swimmers/s1');
  });

  it('updateIdentity PUTs /api/swimmers/{id}/identity with the body', async () => {
    (http.put as jest.Mock).mockResolvedValue({ data: null });
    const rq = { nameEn: 'A', dob: '2010-01-01' };
    await repo.updateIdentity('s1', rq as never);
    expect(http.put).toHaveBeenCalledWith('/api/swimmers/s1/identity', { body: rq });
  });

  it('createExam POSTs /api/swimmers/{id}/medical-exams with the body', async () => {
    (http.post as jest.Mock).mockResolvedValue({ data: {} });
    const rq = { examDate: '2026-09-19', hemoglobin: 15, heightCm: 183, weightKg: 75, internalMedId: 'f1', heartAssessId: 'f1', spineAssessId: 'f1' };
    await repo.createExam('s1', rq as never);
    expect(http.post).toHaveBeenCalledWith('/api/swimmers/s1/medical-exams', { body: rq });
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx jest swimmer-profile.repository.impl`
Expected: FAIL — impl doesn't exist.

- [ ] **Step 3: Write minimal implementation**

`data/repositories/swimmer-profile.repository.impl.ts`:

```typescript
// swimmer-profile.repository.impl.ts — profile read + identity/vitals writes.
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { ISwimmerProfileRepository } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { SwimmerProfileItemDtoRs } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';
import { UpdateIdentityDtoRq, UpdateIdentityItemDtoRs } from '@features/swimmer-profile/data/dto/update-identity.dto';
import { CreateMedicalExamDtoRq, CreatedVitalsItemDtoRs } from '@features/swimmer-profile/data/dto/create-medical-exam.dto';

@Injectable({ providedIn: 'root' })
export class SwimmerProfileRepositoryImpl implements ISwimmerProfileRepository {
  private readonly http = inject(HttpClientService);

  getProfile(id: string): Promise<SwimmerProfileItemDtoRs> {
    return this.http.get<SwimmerProfileItemDtoRs>(`/api/swimmers/${id}`);
  }
  updateIdentity(id: string, rq: UpdateIdentityDtoRq): Promise<UpdateIdentityItemDtoRs> {
    return this.http.put<UpdateIdentityItemDtoRs>(`/api/swimmers/${id}/identity`, { body: rq });
  }
  createExam(id: string, rq: CreateMedicalExamDtoRq): Promise<CreatedVitalsItemDtoRs> {
    return this.http.post<CreatedVitalsItemDtoRs>(`/api/swimmers/${id}/medical-exams`, { body: rq });
  }
}
```

`data/swimmer-profile.providers.ts`:

```typescript
import { Provider } from '@angular/core';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { SwimmerProfileRepositoryImpl } from '@features/swimmer-profile/data/repositories/swimmer-profile.repository.impl';

export const SWIMMER_PROFILE_PROVIDERS: Provider[] = [
  { provide: SWIMMER_PROFILE_REPOSITORY, useClass: SwimmerProfileRepositoryImpl },
];
```

`index.ts` (barrel — extended in later tasks):

```typescript
export { SWIMMER_PROFILE_PROVIDERS } from './data/swimmer-profile.providers';
export { SwimmerProfilePage } from './presentation/pages/swimmer-profile/swimmer-profile.page';
export { SwimmerProfileViewModel } from './presentation/pages/swimmer-profile/swimmer-profile.viewmodel';
```

> The `SwimmerProfilePage`/`SwimmerProfileViewModel` exports resolve in Tasks 14–15. If the barrel must compile before then, add those two exports when their files exist (Task 14 for the viewmodel, Task 15 for the page). Keep only the providers export until then.

`app.config.ts` — add the import and spread it into `providers` (next to `...SWIMMER_PROVIDERS`):

```typescript
import { SWIMMER_PROFILE_PROVIDERS } from '@features/swimmer-profile/data/swimmer-profile.providers';
```
```typescript
    ...SWIMMER_PROFILE_PROVIDERS,
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npx jest swimmer-profile.repository.impl`. Expected: PASS.

- [ ] **Step 5: Commit** (after user authorizes)

```bash
git add frontend/src/app/features/swimmer-profile/ frontend/src/app/app.config.ts
git commit -m "feat(swimmer-profile): repository impl + DI wiring"
```

---

## Task 13: `swimmer-profile` use-cases (get profile, update identity, create exam)

**Files:**
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/get-swimmer-profile.use-case.ts`
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/update-swimmer-identity.use-case.ts`
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/create-medical-exam.use-case.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/domain/usecases/*.spec.ts` (3 files)

**Interfaces:**
- Produces:
  - `GetSwimmerProfileUseCase extends UseCase<string, SwimmerProfile>`
  - `UpdateSwimmerIdentityUseCase extends UseCase<{ id: string; rq: UpdateIdentityDtoRq }, void>`
  - `CreateMedicalExamUseCase extends UseCase<{ id: string; rq: CreateMedicalExamDtoRq }, SwimmerVitals>`

- [ ] **Step 1: Write the failing tests**

`get-swimmer-profile.use-case.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { GetSwimmerProfileUseCase } from '@features/swimmer-profile/domain/usecases/get-swimmer-profile.use-case';
import { SWIMMER_PROFILE_REPOSITORY, ISwimmerProfileRepository } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

const IDENTITY = { id: 's1', uid: 'SW-1', nameEn: 'Ahmed', nameAr: 'أحمد', dob: '2010-01-01', age: 16, genderCode: 'male', phone: '01000000001', trainingClubNameEn: 'Oasis', trainingClubNameAr: null };
const REF = { id: 'f1', code: 'fit', nameEn: 'Fit', nameAr: 'لائق' };
const VITALS = { examDate: '2026-09-19', bloodType: null, hemoglobin: 14.8, heightCm: 182, weightKg: 74, internalMed: REF, heartAssess: REF, spineAssess: REF };

function build(repo: Partial<ISwimmerProfileRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }] });
  return TestBed.inject(GetSwimmerProfileUseCase);
}

describe('GetSwimmerProfileUseCase', () => {
  it('maps identity and vitals (with null blood type)', async () => {
    const repo = { getProfile: async () => ({ data: { identity: IDENTITY, vitals: VITALS } }) };
    const r = await build(repo as ISwimmerProfileRepository).run('s1');
    expect(r.ok).toBe(true);
    if (r.ok) {
      expect(r.data.identity.nameEn).toBe('Ahmed');
      expect(r.data.vitals?.bloodType).toBeNull();
      expect(r.data.vitals?.internalMed.nameEn).toBe('Fit');
    }
  });

  it('maps a profile with null vitals', async () => {
    const repo = { getProfile: async () => ({ data: { identity: IDENTITY, vitals: null } }) };
    const r = await build(repo as ISwimmerProfileRepository).run('s1');
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.data.vitals).toBeNull();
  });

  it('fails validation when identity is malformed', async () => {
    const repo = { getProfile: async () => ({ data: { identity: { id: 's1' }, vitals: null } }) };
    const r = await build(repo as unknown as ISwimmerProfileRepository).run('s1');
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
```

`update-swimmer-identity.use-case.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { UpdateSwimmerIdentityUseCase } from '@features/swimmer-profile/domain/usecases/update-swimmer-identity.use-case';
import { SWIMMER_PROFILE_REPOSITORY, ISwimmerProfileRepository } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

function build(repo: Partial<ISwimmerProfileRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }] });
  return TestBed.inject(UpdateSwimmerIdentityUseCase);
}

describe('UpdateSwimmerIdentityUseCase', () => {
  it('calls the repository with id and body', async () => {
    const updateIdentity = jest.fn().mockResolvedValue({ data: null });
    const uc = build({ updateIdentity } as unknown as ISwimmerProfileRepository);
    const rq = { nameEn: 'New', nameAr: null, dob: '2010-01-01', phone: '01000000009' };
    const r = await uc.run({ id: 's1', rq });
    expect(r.ok).toBe(true);
    expect(updateIdentity).toHaveBeenCalledWith('s1', rq);
  });

  it('fails when the repository throws', async () => {
    const updateIdentity = jest.fn().mockRejectedValue(new Error('boom'));
    const uc = build({ updateIdentity } as unknown as ISwimmerProfileRepository);
    const r = await uc.run({ id: 's1', rq: { nameEn: 'New', nameAr: null, dob: '2010-01-01', phone: null } });
    expect(r.ok).toBe(false);
  });
});
```

`create-medical-exam.use-case.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { CreateMedicalExamUseCase } from '@features/swimmer-profile/domain/usecases/create-medical-exam.use-case';
import { SWIMMER_PROFILE_REPOSITORY, ISwimmerProfileRepository } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

const REF = { id: 'f1', code: 'fit', nameEn: 'Fit', nameAr: 'لائق' };
const VITALS = { examDate: '2026-09-19', bloodType: null, hemoglobin: 15, heightCm: 183, weightKg: 75, internalMed: REF, heartAssess: REF, spineAssess: REF };

function build(repo: Partial<ISwimmerProfileRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }] });
  return TestBed.inject(CreateMedicalExamUseCase);
}

describe('CreateMedicalExamUseCase', () => {
  it('maps the created vitals', async () => {
    const uc = build({ createExam: async () => ({ data: VITALS }) } as unknown as ISwimmerProfileRepository);
    const rq = { examDate: '2026-09-19', hemoglobin: 15, heightCm: 183, weightKg: 75, internalMedId: 'f1', heartAssessId: 'f1', spineAssessId: 'f1' };
    const r = await uc.run({ id: 's1', rq });
    expect(r.ok).toBe(true);
    if (r.ok) { expect(r.data.hemoglobin).toBe(15); expect(r.data.internalMed.code).toBe('fit'); }
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `npx jest swimmer-profile/testing/domain/usecases`
Expected: FAIL — use-cases don't exist.

- [ ] **Step 3: Write minimal implementation**

`get-swimmer-profile.use-case.ts`:

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { CodedRefDtoRs, SwimmerProfileDtoRs, isSwimmerProfileDtoRsValid } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';
import { SwimmerProfile } from '@features/swimmer-profile/domain/model/swimmer-profile';
import { LookupItem } from '@features/reference/domain/model/reference';

@Injectable({ providedIn: 'root' })
export class GetSwimmerProfileUseCase extends UseCase<string, SwimmerProfile> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('GetSwimmerProfile'); }

  protected async execute(id: string): Promise<SwimmerProfile> {
    const res = await this.repo.getProfile(id);
    if (!isSwimmerProfileDtoRsValid(res.data)) throw new AppError('Invalid swimmer profile received', 'validation');
    return toProfile(res.data);
  }
}

function toRef(r: CodedRefDtoRs): LookupItem {
  return { id: r.id, code: r.code, nameEn: r.nameEn, nameAr: r.nameAr };
}

function toProfile(d: SwimmerProfileDtoRs): SwimmerProfile {
  return {
    identity: {
      id: d.identity.id, uid: d.identity.uid, nameEn: d.identity.nameEn, nameAr: d.identity.nameAr ?? null,
      dob: d.identity.dob ?? null, age: typeof d.identity.age === 'number' ? d.identity.age : null,
      genderCode: d.identity.genderCode, phone: d.identity.phone ?? null,
      trainingClubNameEn: d.identity.trainingClubNameEn ?? null, trainingClubNameAr: d.identity.trainingClubNameAr ?? null,
    },
    vitals: d.vitals ? {
      examDate: d.vitals.examDate,
      bloodType: d.vitals.bloodType ? toRef(d.vitals.bloodType) : null,
      hemoglobin: d.vitals.hemoglobin, heightCm: d.vitals.heightCm, weightKg: d.vitals.weightKg,
      internalMed: toRef(d.vitals.internalMed), heartAssess: toRef(d.vitals.heartAssess), spineAssess: toRef(d.vitals.spineAssess),
    } : null,
  };
}
```

`update-swimmer-identity.use-case.ts`:

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { UpdateIdentityDtoRq } from '@features/swimmer-profile/data/dto/update-identity.dto';

export interface UpdateSwimmerIdentityInput { id: string; rq: UpdateIdentityDtoRq; }

@Injectable({ providedIn: 'root' })
export class UpdateSwimmerIdentityUseCase extends UseCase<UpdateSwimmerIdentityInput, void> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('UpdateSwimmerIdentity'); }

  protected async execute(input: UpdateSwimmerIdentityInput): Promise<void> {
    await this.repo.updateIdentity(input.id, input.rq);
  }
}
```

`create-medical-exam.use-case.ts`:

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { CreateMedicalExamDtoRq } from '@features/swimmer-profile/data/dto/create-medical-exam.dto';
import { CodedRefDtoRs } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';
import { SwimmerVitals } from '@features/swimmer-profile/domain/model/swimmer-profile';
import { LookupItem } from '@features/reference/domain/model/reference';

export interface CreateMedicalExamInput { id: string; rq: CreateMedicalExamDtoRq; }

@Injectable({ providedIn: 'root' })
export class CreateMedicalExamUseCase extends UseCase<CreateMedicalExamInput, SwimmerVitals> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('CreateMedicalExam'); }

  protected async execute(input: CreateMedicalExamInput): Promise<SwimmerVitals> {
    const res = await this.repo.createExam(input.id, input.rq);
    const d = res.data;
    if (!d || typeof d.hemoglobin !== 'number') throw new AppError('Invalid created exam received', 'validation');
    const toRef = (r: CodedRefDtoRs): LookupItem => ({ id: r.id, code: r.code, nameEn: r.nameEn, nameAr: r.nameAr });
    return {
      examDate: d.examDate, bloodType: d.bloodType ? toRef(d.bloodType) : null,
      hemoglobin: d.hemoglobin, heightCm: d.heightCm, weightKg: d.weightKg,
      internalMed: toRef(d.internalMed), heartAssess: toRef(d.heartAssess), spineAssess: toRef(d.spineAssess),
    };
  }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `npx jest swimmer-profile/testing/domain/usecases`. Expected: PASS.

- [ ] **Step 5: Commit** (after user authorizes)

```bash
git add frontend/src/app/features/swimmer-profile/
git commit -m "feat(swimmer-profile): get-profile, update-identity, create-exam use-cases"
```

---

## Task 14: `SwimmerProfileViewModel`

**Files:**
- Create: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.ts`
- Modify: `frontend/src/app/features/swimmer-profile/index.ts` (add viewmodel export)
- Test: `frontend/src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `GetSwimmerProfileUseCase`, `UpdateSwimmerIdentityUseCase`, `CreateMedicalExamUseCase` (Task 13), `LoadBloodTypesUseCase` (existing), `LoadFitnessAssessmentsUseCase` (Task 10), `NotificationService`, `TranslateService`, `AuthSessionStore`.
- Produces: `SwimmerProfileViewModel` with signals `profile/loading/error/notFound/bloodTypes/fitnessAssessments`, computed `canEdit/canSaveIdentity/canSaveVitals`, identity+vitals edit state, and `load(id)/startEditIdentity()/cancelEditIdentity()/saveIdentity()/startEditVitals()/cancelEditVitals()/saveVitals()`.

- [ ] **Step 1: Write the failing test**

```typescript
import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { SwimmerProfileViewModel } from '@features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.viewmodel';
import { GetSwimmerProfileUseCase } from '@features/swimmer-profile/domain/usecases/get-swimmer-profile.use-case';
import { UpdateSwimmerIdentityUseCase } from '@features/swimmer-profile/domain/usecases/update-swimmer-identity.use-case';
import { CreateMedicalExamUseCase } from '@features/swimmer-profile/domain/usecases/create-medical-exam.use-case';
import { LoadBloodTypesUseCase } from '@features/reference/domain/usecases/load-blood-types.use-case';
import { LoadFitnessAssessmentsUseCase } from '@features/reference/domain/usecases/load-fitness-assessments.use-case';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';

const REF = { id: 'f1', code: 'fit', nameEn: 'Fit', nameAr: 'لائق' };
const IDENTITY = { id: 's1', uid: 'SW-1', nameEn: 'Ahmed', nameAr: 'أحمد', dob: '2010-01-01', age: 16, genderCode: 'male', phone: '01000000001', trainingClubNameEn: 'Oasis', trainingClubNameAr: null };
const VITALS = { examDate: '2026-09-19', bloodType: null, hemoglobin: 14.8, heightCm: 182, weightKg: 74, internalMed: REF, heartAssess: REF, spineAssess: REF };

function build(over: { profile?: unknown; update?: unknown; create?: unknown; role?: 'head_coach' | 'captain' | null } = {}) {
  const getUc = { run: jest.fn().mockResolvedValue(over.profile ?? { ok: true, data: { identity: IDENTITY, vitals: VITALS } }) };
  const updateUc = { run: jest.fn().mockResolvedValue(over.update ?? { ok: true, data: undefined }) };
  const createUc = { run: jest.fn().mockResolvedValue(over.create ?? { ok: true, data: VITALS }) };
  const bloodUc = { run: jest.fn().mockResolvedValue({ ok: true, data: [] }) };
  const fitnessUc = { run: jest.fn().mockResolvedValue({ ok: true, data: [REF] }) };
  const notify = { success: jest.fn(), error: jest.fn() };
  const i18n = { t: (k: string) => k };
  const session = { role: signal(over.role === undefined ? 'head_coach' : over.role) };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [
    SwimmerProfileViewModel,
    { provide: GetSwimmerProfileUseCase, useValue: getUc },
    { provide: UpdateSwimmerIdentityUseCase, useValue: updateUc },
    { provide: CreateMedicalExamUseCase, useValue: createUc },
    { provide: LoadBloodTypesUseCase, useValue: bloodUc },
    { provide: LoadFitnessAssessmentsUseCase, useValue: fitnessUc },
    { provide: NotificationService, useValue: notify },
    { provide: TranslateService, useValue: i18n },
    { provide: AuthSessionStore, useValue: session },
  ] });
  return { vm: TestBed.inject(SwimmerProfileViewModel), getUc, updateUc, createUc, notify };
}

describe('SwimmerProfileViewModel', () => {
  it('load populates identity and vitals', async () => {
    const { vm } = build();
    await vm.load('s1');
    expect(vm.profile()?.identity.nameEn).toBe('Ahmed');
    expect(vm.profile()?.vitals?.hemoglobin).toBe(14.8);
    expect(vm.notFound()).toBe(false);
  });

  it('load sets notFound on a not_found error', async () => {
    const { vm } = build({ profile: { ok: false, error: { status: 404, kind: 'not_found' } } });
    await vm.load('sX');
    expect(vm.notFound()).toBe(true);
    expect(vm.profile()).toBeNull();
  });

  it('canEdit is false for a null role', () => {
    const { vm } = build({ role: null });
    expect(vm.canEdit()).toBe(false);
  });

  it('saveIdentity toasts success and reloads', async () => {
    const { vm, updateUc, getUc, notify } = build();
    await vm.load('s1');
    vm.startEditIdentity();
    vm.idNameEn.set('New Name');
    await vm.saveIdentity();
    expect(updateUc.run).toHaveBeenCalled();
    expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.identityUpdated');
    expect(getUc.run).toHaveBeenCalledTimes(2); // initial + reload
    expect(vm.editingIdentity()).toBe(false);
  });

  it('saveIdentity toasts error on failure', async () => {
    const { vm, notify } = build({ update: { ok: false, error: { status: 500 } } });
    await vm.load('s1');
    vm.startEditIdentity();
    await vm.saveIdentity();
    expect(notify.error).toHaveBeenCalledWith('swimmerProfile.toasts.saveFailed');
  });

  it('saveVitals records a new exam, toasts success and reloads', async () => {
    const { vm, createUc, notify } = build();
    await vm.load('s1');
    await vm.startEditVitals();
    vm.vExamDate.set('2026-09-19');
    vm.vHemoglobin.set('15'); vm.vHeightCm.set('183'); vm.vWeightKg.set('75');
    vm.vInternalMedId.set('f1'); vm.vHeartAssessId.set('f1'); vm.vSpineAssessId.set('f1');
    expect(vm.canSaveVitals()).toBe(true);
    await vm.saveVitals();
    expect(createUc.run).toHaveBeenCalled();
    expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.examRecorded');
    expect(vm.editingVitals()).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx jest swimmer-profile.viewmodel`
Expected: FAIL — viewmodel doesn't exist.

- [ ] **Step 3: Write minimal implementation**

`swimmer-profile.viewmodel.ts`:

```typescript
import { Injectable, computed, inject, signal } from '@angular/core';
import { GetSwimmerProfileUseCase } from '@features/swimmer-profile/domain/usecases/get-swimmer-profile.use-case';
import { UpdateSwimmerIdentityUseCase } from '@features/swimmer-profile/domain/usecases/update-swimmer-identity.use-case';
import { CreateMedicalExamUseCase } from '@features/swimmer-profile/domain/usecases/create-medical-exam.use-case';
import { LoadBloodTypesUseCase } from '@features/reference/domain/usecases/load-blood-types.use-case';
import { LoadFitnessAssessmentsUseCase } from '@features/reference/domain/usecases/load-fitness-assessments.use-case';
import { LookupItem } from '@features/reference/domain/model/reference';
import { SwimmerProfile } from '@features/swimmer-profile/domain/model/swimmer-profile';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';

@Injectable()
export class SwimmerProfileViewModel {
  private readonly getProfileUc = inject(GetSwimmerProfileUseCase);
  private readonly updateIdentityUc = inject(UpdateSwimmerIdentityUseCase);
  private readonly createExamUc = inject(CreateMedicalExamUseCase);
  private readonly loadBloodTypes = inject(LoadBloodTypesUseCase);
  private readonly loadFitness = inject(LoadFitnessAssessmentsUseCase);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);
  private readonly session = inject(AuthSessionStore);

  readonly profile = signal<SwimmerProfile | null>(null);
  readonly loading = signal(false);
  readonly error = signal(false);
  readonly notFound = signal(false);

  readonly bloodTypes = signal<LookupItem[]>([]);
  readonly fitnessAssessments = signal<LookupItem[]>([]);

  readonly canEdit = computed(() => {
    const r = this.session.role();
    return r === 'head_coach' || r === 'captain';
  });

  // Identity edit state
  readonly editingIdentity = signal(false);
  readonly idNameEn = signal('');
  readonly idNameAr = signal('');
  readonly idDob = signal('');
  readonly idPhone = signal('');
  readonly savingIdentity = signal(false);
  readonly canSaveIdentity = computed(() => this.idNameEn().trim().length > 0 && this.idDob().length > 0);

  // Vitals edit state (numerics held as strings from inputs)
  readonly editingVitals = signal(false);
  readonly vExamDate = signal('');
  readonly vBloodTypeId = signal('');
  readonly vHemoglobin = signal('');
  readonly vHeightCm = signal('');
  readonly vWeightKg = signal('');
  readonly vInternalMedId = signal('');
  readonly vHeartAssessId = signal('');
  readonly vSpineAssessId = signal('');
  readonly savingVitals = signal(false);
  readonly canSaveVitals = computed(() => {
    const nums = [this.vHemoglobin(), this.vHeightCm(), this.vWeightKg()];
    const numericOk = nums.every((v) => v.trim().length > 0 && Number.isFinite(Number(v)) && Number(v) > 0);
    return this.vExamDate().length > 0
      && this.vInternalMedId().length > 0 && this.vHeartAssessId().length > 0 && this.vSpineAssessId().length > 0
      && numericOk;
  });

  private swimmerId = '';

  async load(id: string): Promise<void> {
    this.swimmerId = id;
    this.loading.set(true);
    this.error.set(false);
    this.notFound.set(false);
    const r = await this.getProfileUc.run(id);
    this.loading.set(false);
    if (r.ok) {
      this.profile.set(r.data);
    } else {
      this.profile.set(null);
      if (r.error.kind === 'not_found' || (r.error as { status?: number }).status === 404) this.notFound.set(true);
      else this.error.set(true);
    }
  }

  private async ensureLookups(): Promise<void> {
    if (this.bloodTypes().length === 0) {
      const b = await this.loadBloodTypes.run();
      if (b.ok) this.bloodTypes.set(b.data);
    }
    if (this.fitnessAssessments().length === 0) {
      const f = await this.loadFitness.run();
      if (f.ok) this.fitnessAssessments.set(f.data);
    }
  }

  startEditIdentity(): void {
    const i = this.profile()?.identity;
    if (!i) return;
    this.idNameEn.set(i.nameEn);
    this.idNameAr.set(i.nameAr ?? '');
    this.idDob.set(i.dob ?? '');
    this.idPhone.set(i.phone ?? '');
    this.editingIdentity.set(true);
  }

  cancelEditIdentity(): void { this.editingIdentity.set(false); }

  async saveIdentity(): Promise<void> {
    if (!this.canSaveIdentity() || this.savingIdentity()) return;
    this.savingIdentity.set(true);
    const r = await this.updateIdentityUc.run({
      id: this.swimmerId,
      rq: { nameEn: this.idNameEn().trim(), nameAr: this.idNameAr().trim() || null, dob: this.idDob(), phone: this.idPhone().trim() || null },
    });
    this.savingIdentity.set(false);
    if (r.ok) {
      this.notify.success(this.i18n.t('swimmerProfile.toasts.identityUpdated'));
      this.editingIdentity.set(false);
      await this.load(this.swimmerId);
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.saveFailed'));
    }
  }

  async startEditVitals(): Promise<void> {
    await this.ensureLookups();
    const v = this.profile()?.vitals;
    this.vExamDate.set(v?.examDate ?? '');
    this.vBloodTypeId.set(v?.bloodType?.id ?? '');
    this.vHemoglobin.set(v ? String(v.hemoglobin) : '');
    this.vHeightCm.set(v ? String(v.heightCm) : '');
    this.vWeightKg.set(v ? String(v.weightKg) : '');
    this.vInternalMedId.set(v?.internalMed.id ?? '');
    this.vHeartAssessId.set(v?.heartAssess.id ?? '');
    this.vSpineAssessId.set(v?.spineAssess.id ?? '');
    this.editingVitals.set(true);
  }

  cancelEditVitals(): void { this.editingVitals.set(false); }

  async saveVitals(): Promise<void> {
    if (!this.canSaveVitals() || this.savingVitals()) return;
    this.savingVitals.set(true);
    const r = await this.createExamUc.run({
      id: this.swimmerId,
      rq: {
        examDate: this.vExamDate(),
        bloodTypeId: this.vBloodTypeId() || null,
        hemoglobin: Number(this.vHemoglobin()), heightCm: Number(this.vHeightCm()), weightKg: Number(this.vWeightKg()),
        internalMedId: this.vInternalMedId(), heartAssessId: this.vHeartAssessId(), spineAssessId: this.vSpineAssessId(),
      },
    });
    this.savingVitals.set(false);
    if (r.ok) {
      this.notify.success(this.i18n.t('swimmerProfile.toasts.examRecorded'));
      this.editingVitals.set(false);
      await this.load(this.swimmerId);
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.saveFailed'));
    }
  }
}
```

Add to `index.ts`:

```typescript
export { SwimmerProfileViewModel } from './presentation/pages/swimmer-profile/swimmer-profile.viewmodel';
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npx jest swimmer-profile.viewmodel`. Expected: PASS.

- [ ] **Step 5: Commit** (after user authorizes)

```bash
git add frontend/src/app/features/swimmer-profile/
git commit -m "feat(swimmer-profile): signal-based viewmodel (load + identity/vitals edit)"
```

---

## Task 15: `SwimmerProfilePage` component + template

**Files:**
- Create: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.ts`
- Create: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.html`
- Modify: `frontend/src/app/features/swimmer-profile/index.ts` (add page export)

**Interfaces:**
- Consumes: `SwimmerProfileViewModel` (Task 14), `ActivatedRoute`, `SelectFieldComponent`, `TextFieldComponent`, `TranslatePipe`, `LanguageStore`.
- Produces: `SwimmerProfilePage` (standalone).

> No unit test for the page HTML (repo convention — page logic lives in the viewmodel, which Task 14 covers). Verification is a clean `npm run build`.

- [ ] **Step 1: Write the component**

`swimmer-profile.page.ts`:

```typescript
import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TranslatePipe } from '@core/i18n';
import { LanguageStore } from '@core/i18n/language.store';
import { SelectFieldComponent } from '@core/ui/components/select-field.component';
import { TextFieldComponent } from '@core/ui/components/text-field.component';
import { SwimmerProfileViewModel } from './swimmer-profile.viewmodel';

interface ProfileTab { key: string; labelKey: string; }

@Component({
  selector: 'app-swimmer-profile-page',
  standalone: true,
  imports: [TranslatePipe, SelectFieldComponent, TextFieldComponent],
  templateUrl: './swimmer-profile.page.html',
})
export class SwimmerProfilePage implements OnInit {
  protected readonly vm = inject(SwimmerProfileViewModel);
  private readonly route = inject(ActivatedRoute);
  private readonly language = inject(LanguageStore);

  // Full tab strip for visual fidelity; only 'identityVitals' is enabled this pass.
  protected readonly tabs: ProfileTab[] = [
    { key: 'identityVitals', labelKey: 'swimmerProfile.tabs.identityVitals' },
    { key: 'guardian', labelKey: 'swimmerProfile.tabs.guardian' },
    { key: 'physiological', labelKey: 'swimmerProfile.tabs.physiological' },
    { key: 'inbody', labelKey: 'swimmerProfile.tabs.inbody' },
    { key: 'records', labelKey: 'swimmerProfile.tabs.records' },
    { key: 'healthMonitoring', labelKey: 'swimmerProfile.tabs.healthMonitoring' },
    { key: 'attendance', labelKey: 'swimmerProfile.tabs.attendance' },
    { key: 'championships', labelKey: 'swimmerProfile.tabs.championships' },
    { key: 'feedback', labelKey: 'swimmerProfile.tabs.feedback' },
  ];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    void this.vm.load(id);
  }

  displayName(): string {
    const i = this.vm.profile()?.identity;
    if (!i) return '';
    return this.language.lang() === 'ar' ? (i.nameAr ?? i.nameEn) : i.nameEn;
  }

  refLabel(ref: { nameEn: string; nameAr: string | null } | null | undefined): string {
    if (!ref) return '—';
    return this.language.lang() === 'ar' ? (ref.nameAr ?? ref.nameEn) : ref.nameEn;
  }

  initials(): string {
    const en = this.vm.profile()?.identity.nameEn ?? '';
    const parts = en.trim().split(/\s+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase();
  }
}
```

- [ ] **Step 2: Write the template**

`swimmer-profile.page.html` (identity strip + tab strip + Identity & Vitals sections with role-gated edit forms):

```html
<div class="mx-auto max-w-5xl px-4 py-8 sm:px-6">
  @if (vm.loading()) {
    <p class="text-text-secondary">{{ 'swimmerProfile.states.loading' | translate }}</p>
  } @else if (vm.notFound()) {
    <p class="text-danger">{{ 'swimmerProfile.states.notFound' | translate }}</p>
  } @else if (vm.error()) {
    <p class="text-danger">{{ 'swimmerProfile.states.error' | translate }}</p>
  } @else if (vm.profile(); as p) {
    <!-- Identity strip -->
    <div class="mb-6 rounded-2xl border border-border bg-surface p-6 shadow-sm">
      <div class="flex items-center gap-4">
        <div class="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-primary/10 text-lg font-semibold text-primary">{{ initials() }}</div>
        <div class="min-w-0">
          <h1 class="font-heading text-2xl text-ink">{{ displayName() }}</h1>
          <p class="mt-0.5 text-sm text-text-secondary">
            <span>{{ p.identity.uid }}</span>
            @if (p.identity.genderCode) { <span> · {{ ('gender.' + p.identity.genderCode) | translate }}</span> }
            @if (p.identity.age !== null) { <span> · {{ p.identity.age }} {{ 'swimmerProfile.years' | translate }}</span> }
          </p>
          @if (p.identity.trainingClubNameEn) {
            <p class="mt-1 text-xs text-text-secondary">{{ 'swimmerProfile.registeredClub' | translate }}: {{ p.identity.trainingClubNameEn }}</p>
          }
        </div>
      </div>
    </div>

    <!-- Tab strip: only Identity & Vitals is enabled -->
    <div class="mb-6 flex flex-wrap gap-2 border-b border-border pb-2">
      @for (t of tabs; track t.key) {
        <span class="rounded-md px-3 py-1.5 text-sm"
              [class.bg-primary]="t.key === 'identityVitals'"
              [class.text-white]="t.key === 'identityVitals'"
              [class.text-text-secondary]="t.key !== 'identityVitals'"
              [class.opacity-50]="t.key !== 'identityVitals'"
              [attr.aria-disabled]="t.key !== 'identityVitals'">
          {{ t.labelKey | translate }}
        </span>
      }
    </div>

    <!-- Identity section -->
    <section class="mb-6 rounded-2xl border border-border bg-surface p-6 shadow-sm">
      <div class="mb-4 flex items-center justify-between border-b border-border pb-2">
        <h2 class="font-heading text-lg text-ink">{{ 'swimmerProfile.sections.identity' | translate }}</h2>
        @if (vm.canEdit() && !vm.editingIdentity()) {
          <button type="button" class="text-sm font-medium text-primary" (click)="vm.startEditIdentity()">{{ 'swimmerProfile.edit' | translate }}</button>
        }
      </div>

      @if (!vm.editingIdentity()) {
        <dl class="grid grid-cols-1 gap-y-5 gap-x-10 md:grid-cols-3">
          <div>
            <dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.fields.fullName' | translate }}</dt>
            <dd class="text-ink">{{ displayName() }}</dd>
          </div>
          <div>
            <dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.fields.dob' | translate }}</dt>
            <dd class="text-ink">{{ p.identity.dob ?? '—' }}</dd>
          </div>
          <div>
            <dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.fields.phone' | translate }}</dt>
            <dd class="text-ink">{{ p.identity.phone ?? '—' }}</dd>
          </div>
        </dl>
      } @else {
        <form class="grid grid-cols-1 gap-5 md:grid-cols-2" (submit)="$event.preventDefault(); vm.saveIdentity()">
          <app-text-field [label]="'swimmerProfile.fields.nameEn' | translate" [value]="vm.idNameEn()" (valueChange)="vm.idNameEn.set($event)"></app-text-field>
          <app-text-field [label]="'swimmerProfile.fields.nameAr' | translate" [value]="vm.idNameAr()" (valueChange)="vm.idNameAr.set($event)"></app-text-field>
          <label class="flex flex-col gap-1">
            <span class="text-sm font-bold text-ink">{{ 'swimmerProfile.fields.dob' | translate }}</span>
            <input type="date" class="h-10 rounded-md border border-input bg-card px-3 text-sm text-ink" [value]="vm.idDob()" (change)="vm.idDob.set($any($event.target).value)" />
          </label>
          <app-text-field [label]="'swimmerProfile.fields.phone' | translate" [value]="vm.idPhone()" (valueChange)="vm.idPhone.set($event)"></app-text-field>
          <div class="flex items-end gap-2 md:col-span-2">
            <button type="submit" class="rounded-md bg-primary px-5 py-2.5 text-sm font-semibold text-white disabled:opacity-50" [disabled]="!vm.canSaveIdentity() || vm.savingIdentity()">{{ 'common.save' | translate }}</button>
            <button type="button" class="rounded-md px-5 py-2.5 text-sm text-text-secondary" (click)="vm.cancelEditIdentity()">{{ 'common.cancel' | translate }}</button>
          </div>
        </form>
      }
    </section>

    <!-- Vitals section -->
    <section class="rounded-2xl border border-border bg-surface p-6 shadow-sm">
      <div class="mb-4 flex items-center justify-between border-b border-border pb-2">
        <h2 class="font-heading text-lg text-ink">{{ 'swimmerProfile.sections.vitals' | translate }}</h2>
        @if (vm.canEdit() && !vm.editingVitals()) {
          <button type="button" class="text-sm font-medium text-primary" (click)="vm.startEditVitals()">{{ 'swimmerProfile.recordExam' | translate }}</button>
        }
      </div>

      @if (vm.editingVitals()) {
        <form class="grid grid-cols-1 gap-5 md:grid-cols-3" (submit)="$event.preventDefault(); vm.saveVitals()">
          <label class="flex flex-col gap-1">
            <span class="text-sm font-bold text-ink">{{ 'swimmerProfile.fields.examDate' | translate }}</span>
            <input type="date" class="h-10 rounded-md border border-input bg-card px-3 text-sm text-ink" [value]="vm.vExamDate()" (change)="vm.vExamDate.set($any($event.target).value)" />
          </label>
          <app-select-field [label]="'swimmerProfile.fields.bloodType' | translate" [placeholder]="'swimmerProfile.placeholders.optional' | translate" [options]="vm.bloodTypes()" [value]="vm.vBloodTypeId()" (valueChange)="vm.vBloodTypeId.set($event)"></app-select-field>
          <app-text-field [label]="'swimmerProfile.fields.hemoglobin' | translate" [value]="vm.vHemoglobin()" (valueChange)="vm.vHemoglobin.set($event)"></app-text-field>
          <app-text-field [label]="'swimmerProfile.fields.height' | translate" [value]="vm.vHeightCm()" (valueChange)="vm.vHeightCm.set($event)"></app-text-field>
          <app-text-field [label]="'swimmerProfile.fields.weight' | translate" [value]="vm.vWeightKg()" (valueChange)="vm.vWeightKg.set($event)"></app-text-field>
          <app-select-field [label]="'swimmerProfile.fields.internalMed' | translate" [placeholder]="'swimmerProfile.placeholders.assessment' | translate" [options]="vm.fitnessAssessments()" [value]="vm.vInternalMedId()" (valueChange)="vm.vInternalMedId.set($event)"></app-select-field>
          <app-select-field [label]="'swimmerProfile.fields.heart' | translate" [placeholder]="'swimmerProfile.placeholders.assessment' | translate" [options]="vm.fitnessAssessments()" [value]="vm.vHeartAssessId()" (valueChange)="vm.vHeartAssessId.set($event)"></app-select-field>
          <app-select-field [label]="'swimmerProfile.fields.spine' | translate" [placeholder]="'swimmerProfile.placeholders.assessment' | translate" [options]="vm.fitnessAssessments()" [value]="vm.vSpineAssessId()" (valueChange)="vm.vSpineAssessId.set($event)"></app-select-field>
          <div class="flex items-end gap-2 md:col-span-3">
            <button type="submit" class="rounded-md bg-primary px-5 py-2.5 text-sm font-semibold text-white disabled:opacity-50" [disabled]="!vm.canSaveVitals() || vm.savingVitals()">{{ 'swimmerProfile.recordExam' | translate }}</button>
            <button type="button" class="rounded-md px-5 py-2.5 text-sm text-text-secondary" (click)="vm.cancelEditVitals()">{{ 'common.cancel' | translate }}</button>
          </div>
        </form>
      } @else if (p.vitals; as v) {
        <dl class="grid grid-cols-1 gap-y-5 gap-x-10 md:grid-cols-3">
          <div><dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.fields.bloodType' | translate }}</dt><dd class="text-ink">{{ refLabel(v.bloodType) }}</dd></div>
          <div><dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.fields.height' | translate }}</dt><dd class="text-ink">{{ v.heightCm }} cm</dd></div>
          <div><dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.fields.weight' | translate }}</dt><dd class="text-ink">{{ v.weightKg }} kg</dd></div>
          <div><dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.fields.hemoglobin' | translate }}</dt><dd class="text-ink">{{ v.hemoglobin }} g/dL</dd></div>
          <div><dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.fields.examDate' | translate }}</dt><dd class="text-ink">{{ v.examDate }}</dd></div>
          <div><dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.fields.internalMed' | translate }}</dt><dd class="text-ink">{{ refLabel(v.internalMed) }}</dd></div>
          <div><dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.fields.heart' | translate }}</dt><dd class="text-ink">{{ refLabel(v.heartAssess) }}</dd></div>
          <div><dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.fields.spine' | translate }}</dt><dd class="text-ink">{{ refLabel(v.spineAssess) }}</dd></div>
        </dl>
      } @else {
        <p class="text-sm text-text-secondary">{{ 'swimmerProfile.states.noExam' | translate }}</p>
      }
    </section>
  }
</div>
```

Add to `index.ts`:

```typescript
export { SwimmerProfilePage } from './presentation/pages/swimmer-profile/swimmer-profile.page';
```

- [ ] **Step 3: Verify the build compiles**

Run: `npm run build` (from `frontend/`). Expected: build succeeds (the route in Task 16 is what actually renders it; this step confirms the component + template type-check). If the barrel `index.ts` now exports page + viewmodel + providers, all three resolve.

- [ ] **Step 4: Commit** (after user authorizes)

```bash
git add frontend/src/app/features/swimmer-profile/
git commit -m "feat(swimmer-profile): profile page component + Identity & Vitals template"
```

---

## Task 16: Routing, roster row navigation, i18n

**Files:**
- Modify: `frontend/src/app/app.routes.ts` (+ `swimmers/:id` route)
- Modify: `frontend/src/app/app.routes.spec.ts` (assert route exists)
- Modify: `frontend/src/app/features/swimmers/presentation/pages/swimmers/swimmers.page.ts` (+ `RouterLink`)
- Modify: `frontend/src/app/features/swimmers/presentation/pages/swimmers/swimmers.page.html` (row → link)
- Modify: `frontend/src/app/core/i18n/en.json` (+ `swimmerProfile` block)
- Modify: `frontend/src/app/core/i18n/ar.json` (+ `swimmerProfile` block)

**Interfaces:**
- Consumes: `SwimmerProfilePage`, `SwimmerProfileViewModel` (`@features/swimmer-profile`).

- [ ] **Step 1: Write the failing test** (append to `app.routes.spec.ts`)

```typescript
import { Route } from '@angular/router';
import { routes } from './app.routes';

describe('swimmer profile route', () => {
  it('registers swimmers/:id under the shell layout', () => {
    const shell = routes.find((r: Route) => r.path === '' && Array.isArray(r.children));
    const child = shell?.children?.find((r: Route) => r.path === 'swimmers/:id');
    expect(child).toBeTruthy();
    expect(child?.loadComponent).toBeTruthy();
  });
});
```

> If `app.routes.spec.ts` already imports `routes`/`Route`, don't duplicate the imports — add only the `describe` block.

- [ ] **Step 2: Run test to verify it fails**

Run: `npx jest app.routes`
Expected: FAIL — no `swimmers/:id` route.

- [ ] **Step 3: Write minimal implementation**

`app.routes.ts` — add the import at the top (next to the other `@features/captain-panel` import):

```typescript
import { SwimmerProfileViewModel } from '@features/swimmer-profile';
```

…and add the route inside `children`, immediately **after** the `swimmers` route:

```typescript
      {
        path: 'swimmers/:id',
        canActivate: [firstLoginGuard],
        loadComponent: () => import('@features/swimmer-profile').then((m) => m.SwimmerProfilePage),
        providers: [SwimmerProfileViewModel],
      },
```

`swimmers.page.ts` — add `RouterLink` to imports:

```typescript
import { RouterLink } from '@angular/router';
```
…and add `RouterLink` to the component's `imports: [...]` array.

`swimmers.page.html` — make each row a link. Replace the `<li class="flex items-center gap-4 px-5 py-4">` … `</li>` block's contents with an anchor wrapper (keep the inner markup unchanged):

```html
        @for (s of visible(); track s.id) {
          <li>
            <a [routerLink]="['/swimmers', s.id]"
               class="flex items-center gap-4 px-5 py-4 transition-colors hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30">
              <div class="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-primary/10 text-sm font-semibold text-primary">
                {{ initials(s) }}
              </div>
              <div class="min-w-0 flex-1">
                <p class="truncate text-sm font-medium text-ink">{{ displayName(s) }}</p>
                <p class="mt-0.5 truncate text-xs text-text-secondary">{{ s.uid }}</p>
              </div>
              <div class="hidden w-32 shrink-0 truncate text-sm text-text-secondary md:block">{{ clubName(s) }}</div>
              <div class="hidden w-24 shrink-0 text-xs text-text-secondary sm:block">
                @if (s.gender) { <span>{{ genderLabelKey(s) | translate }}</span> }
                @if (s.gender && s.age !== null) { <span> · </span> }
                @if (s.age !== null) { <span>{{ s.age }}</span> }
              </div>
            </a>
          </li>
        }
```

`en.json` — add this top-level block (e.g., after the `"swimmers": { … },` block):

```json
  "swimmerProfile": {
    "years": "years",
    "registeredClub": "Registered Club",
    "edit": "Edit",
    "recordExam": "Record exam",
    "tabs": {
      "identityVitals": "Identity & Vitals",
      "guardian": "Guardian",
      "physiological": "Physiological",
      "inbody": "InBody",
      "records": "Records",
      "healthMonitoring": "Health Monitoring",
      "attendance": "Attendance",
      "championships": "Championships",
      "feedback": "Feedback"
    },
    "sections": { "identity": "Identity", "vitals": "Vitals" },
    "fields": {
      "fullName": "Full Name", "nameEn": "Full Name (EN)", "nameAr": "Full Name (AR)",
      "dob": "Date of Birth", "phone": "Phone Number",
      "bloodType": "Blood Type", "height": "Height (cm)", "weight": "Weight (kg)",
      "hemoglobin": "Hemoglobin (g/dL)", "examDate": "Last Examination Date",
      "internalMed": "Internal Medicine Result", "heart": "Heart Assessment", "spine": "Spine Assessment"
    },
    "placeholders": { "optional": "— none —", "assessment": "Select result" },
    "states": {
      "loading": "Loading profile…",
      "notFound": "Swimmer not found.",
      "error": "Couldn't load the profile. Please try again.",
      "noExam": "No exam on record yet."
    },
    "toasts": {
      "identityUpdated": "Identity updated",
      "examRecorded": "Exam recorded",
      "saveFailed": "Couldn't save. Please try again."
    }
  },
```

`ar.json` — add the matching block:

```json
  "swimmerProfile": {
    "years": "سنة",
    "registeredClub": "النادي المسجل",
    "edit": "تحرير",
    "recordExam": "تسجيل فحص",
    "tabs": {
      "identityVitals": "الهوية والمؤشرات",
      "guardian": "ولي الأمر",
      "physiological": "القياسات الفسيولوجية",
      "inbody": "InBody",
      "records": "السجلات",
      "healthMonitoring": "المتابعة الصحية",
      "attendance": "الحضور",
      "championships": "البطولات",
      "feedback": "التقييمات"
    },
    "sections": { "identity": "الهوية", "vitals": "المؤشرات الحيوية" },
    "fields": {
      "fullName": "الاسم الكامل", "nameEn": "الاسم (بالإنجليزية)", "nameAr": "الاسم (بالعربية)",
      "dob": "تاريخ الميلاد", "phone": "رقم الهاتف",
      "bloodType": "فصيلة الدم", "height": "الطول (سم)", "weight": "الوزن (كجم)",
      "hemoglobin": "الهيموجلوبين (جم/دل)", "examDate": "تاريخ آخر فحص",
      "internalMed": "نتيجة الباطنة", "heart": "تقييم القلب", "spine": "تقييم العمود الفقري"
    },
    "placeholders": { "optional": "— بدون —", "assessment": "اختر النتيجة" },
    "states": {
      "loading": "جارٍ تحميل الملف…",
      "notFound": "السبّاح غير موجود.",
      "error": "تعذّر تحميل الملف. حاول مرة أخرى.",
      "noExam": "لا يوجد فحص مسجّل بعد."
    },
    "toasts": {
      "identityUpdated": "تم تحديث بيانات الهوية",
      "examRecorded": "تم تسجيل الفحص",
      "saveFailed": "تعذّر الحفظ. حاول مرة أخرى."
    }
  },
```

- [ ] **Step 4: Run the test + build**

Run: `npx jest app.routes` (PASS), then `npm run build` (succeeds).

- [ ] **Step 5: Commit** (after user authorizes)

```bash
git add frontend/src/app/app.routes.ts frontend/src/app/app.routes.spec.ts frontend/src/app/features/swimmers/ frontend/src/app/core/i18n/en.json frontend/src/app/core/i18n/ar.json
git commit -m "feat(swimmer-profile): route swimmers/:id, roster row navigation, i18n"
```

---

## Task 17: Docs update + full verification

**Files:**
- Modify: `docs/references/swimming-database-diagram.html` (mark tables implemented)

- [ ] **Step 1: Mark the new tables implemented**

In `docs/references/swimming-database-diagram.html`, find the `IMPLEMENTED` set (a JS array/set of `"schema.table"` strings — search for `IMPLEMENTED` and for existing entries like `"reference.blood_type"`). Add two entries so the diagram shows green dots:

```
"reference.fitness_assessment",
"athlete.medical_exam",
```

Match the existing quoting/comma style exactly.

- [ ] **Step 2: Run the full test suites**

Stop any running backend, then:

Backend:
```bash
dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests
dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests
dotnet test backend/tests/Kheprx.BaseBackend.ArchitectureTests
```
Frontend (from `frontend/`):
```bash
npx jest
npm run build
```
Expected: all green; build succeeds.

- [ ] **Step 3: Manual smoke (optional, requires DB)**

Apply the migration and run the app:
```bash
# from backend/ — updates the Postgres DB
dotnet ef database update --project src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure --startup-project Kheprx.BaseBackend.Api --context IdentityDbContext
```
Start backend + frontend, log in as Head Coach, open **Swimmers**, click a swimmer → the profile opens on **Identity & Vitals**. Confirm: National ID absent; Edit Identity saves; Record exam adds vitals; a swimmer with no exam shows the empty state.

- [ ] **Step 4: Commit** (after user authorizes)

```bash
git add docs/references/swimming-database-diagram.html
git commit -m "docs: mark fitness_assessment + medical_exam implemented in the schema diagram"
```

---

## Self-Review (completed during planning)

**1. Spec coverage** — every spec section maps to a task:
- §3.1 fitness_assessment table → Tasks 1–2. §3.2 medical_exam table → Tasks 4–5. §3 doc updates → Task 17.
- §4 fitness-assessments endpoint → Task 3. §5 read endpoint → Tasks 6–7. §6 identity PUT → Task 8. §7 vitals POST → Task 9.
- §8.1 slice/models/DTOs/repo → Tasks 11–13. §8.2 reference lookup → Task 10. §8.3 page+viewmodel → Tasks 14–15. §8.4 routing+roster → Task 16. §8.5 i18n → Task 16.
- §10 auth → enforced in Tasks 3/7/8/9 (`[Authorize]` / role gate) and Task 14 (`canEdit`). §11 edge cases → Tasks 7 (404/null vitals), 14 (notFound), 15 (empty state). §12 testing → each task's tests. §13 out-of-scope → not built (tab strip disabled in Task 15).

**2. Placeholder scan** — no TBD/TODO; every code + test step carries real content; migration/CLI steps give exact commands.

**3. Type consistency** — verified across tasks: `SwimmerProfileDto`/`SwimmerIdentityDto`/`SwimmerVitalsDto`, `MedicalExamRow` (20 fields) and its projection, `CodedLookupDto` reuse, `MedicalExam` ctor arg order, repo method names (`GetProfileByIdAsync`, `GetLatestExamAsync`, `GetExamRowByIdAsync`, `GetByIdTrackedAsync`, `AddExamAsync`), and FE model/DTO shapes (`SwimmerProfileDtoRs`, `CodedRefDtoRs`) match between producer and consumer tasks. Two constructor ripples are handled explicitly: `SwimmerService` (+`IFitnessAssessmentRepository`, +`IBloodTypeRepository` → `Build()` updated in Task 9) and `ReferenceService` (+`IFitnessAssessmentRepository` → `NewService` updated in Task 3).

---

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-09-19-swimmer-profile-identity-vitals.md`.** Two execution options:

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration.

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints.

**Note:** you asked me not to commit anything yet — I have **not** committed the plan or the spec-branch work beyond the already-committed spec. When we start executing, I'll confirm with you before the first `git commit`.

**Which approach?**
