# Swimmer Data Fields — Add an Observation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the Captain Panel "Swimmer Records" card to a "Swimmer Data Fields" form that adds a `health.observation` row (server-stamped `observed_date`), with categories sourced from a new seeded `reference.observation_category` lookup.

**Architecture:** Two backend halves. (1) A reference lookup `observation_category` in the **Identity** module, mirroring `blood_type` (entity/repo/config/seed/read-endpoint). (2) An `observation` write-path in the **Health** module, mirroring `health_reading` (entity/repo/config/migration/service/validator/controller). Frontend adds a `features/observations` slice + a `LoadObservationCategoriesUseCase` on `features/reference`, and a Captain Panel page reusing the swimmer + category dropdowns.

**Tech Stack:** .NET 10 / EF Core / Npgsql / FluentValidation / xUnit + Moq (backend); Angular 20 standalone + signals / Jest (frontend).

**Spec:** `docs/superpowers/specs/2026-09-18-swimmer-data-fields-design.md`

## Global Constraints

- **Scope is write-path only.** No GET/list of observations, no swimmer-detail page. (Spec §10.)
- **Roles:** `POST /api/observations` → `head_coach,captain`. `GET /api/reference/observation-categories` → `[Authorize]` (any authenticated user), matching the other lookups.
- **`observed_date` = created moment:** set in the `Observation` ctor to `DateTime.UtcNow`, stored as `timestamptz`. No date input on the form.
- **All Guids on `observation` are loose** — `swimmer_id`, `category_id`, `recorded_by` have **no** EF FK (`observation_category` is in a different module; same convention as `swimmer_id`). The service does **not** validate category existence.
- **Category lookup lives in the Identity module** `reference` schema, seeded with 7 rows (`allergy, surgery, chronic, autoimmune, composition, flag, other`).
- **DLL lock:** stop the backend dev server before `dotnet ef` / `dotnet test` (the `dotnet-test-dev-server-lock` note).
- **Solution file** is `backend/Kheprx.BaseBackend.sln` (NOT `backend/backend.sln`).
- **Frontend** has no `lint` npm script — verify with `npx jest <path>` + `npm run build`.
- **Commits:** each task ends with a commit step per plan format; defer/skip if the user is keeping work uncommitted until they ask.
- **No `ObservationsController` unit test** (matches `HealthReadingsController`/`MedicalTestsController` — controllers using `CurrentUserId()` need HttpContext plumbing these unit tests lack; the service test covers it). The `ReferenceController` endpoint IS tested (that controller has a harness and no `CurrentUserId()`).

**Paths:**
- Identity module: `backend/src/Modules/Identity/`
- Health module: `backend/src/Modules/Health/`
- Api controllers: `backend/Kheprx.BaseBackend.Api/Controllers/`
- Identity tests: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/`
- Health tests: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/`
- Api tests: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/`
- Frontend app: `frontend/src/app/`

---

## Task 1: `observation_category` reference lookup — persistence + seed

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/ObservationCategory.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/IObservationCategoryRepository.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/ObservationCategoryConfiguration.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/ObservationCategoryRepository.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentityDbContext.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentitySeeder.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs`
- Generated: `.../Identity.Infrastructure/Migrations/<timestamp>_CreateObservationCategoryTable.cs` (+ snapshot)
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/ReferenceEntityTests.cs` (append)
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/ReferenceRepositoryTests.cs` (append)

**Interfaces:**
- Produces: `ObservationCategory(string code, string nameEn, string? nameAr = null)` with props `Id, Code, NameEn, NameAr?`; `IObservationCategoryRepository.GetAllAsync(CancellationToken) -> Task<IReadOnlyList<ObservationCategory>>`; `IdentityDbContext.ObservationCategories` DbSet.

- [ ] **Step 1: Write the failing repository test**

Append to `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/ReferenceRepositoryTests.cs` (inside the class):

```csharp
    [Fact]
    public async Task ObservationCategoryRepository_returns_all_ordered_by_code()
    {
        await using var db = NewDb();
        db.ObservationCategories.AddRange(
            new ObservationCategory("surgery", "Surgery", "جراحة"),
            new ObservationCategory("allergy", "Allergy", "حساسية"));
        await db.SaveChangesAsync();

        var result = await new ObservationCategoryRepository(db).GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("allergy", result[0].Code);
    }
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~ReferenceRepositoryTests`
Expected: FAIL — `ObservationCategory` / `ObservationCategoryRepository` / `db.ObservationCategories` do not exist.

- [ ] **Step 3: Create the entity**

`.../Identity.Domain/Entities/ObservationCategory.cs`:

```csharp
namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class ObservationCategory
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }

    private ObservationCategory() { } // EF Core

    public ObservationCategory(string code, string nameEn, string? nameAr = null)
    {
        Id = Guid.NewGuid();
        Code = code.Trim();
        NameEn = nameEn.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
    }
}
```

- [ ] **Step 4: Create the repository interface**

`.../Identity.Domain/Repositories/IObservationCategoryRepository.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
namespace Kheprx.BaseBackend.Identity.Domain.Repositories;
public interface IObservationCategoryRepository
{
    Task<IReadOnlyList<ObservationCategory>> GetAllAsync(CancellationToken ct = default);
}
```

- [ ] **Step 5: Create the EF configuration**

`.../Identity.Infrastructure/Configurations/ObservationCategoryConfiguration.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class ObservationCategoryConfiguration : IEntityTypeConfiguration<ObservationCategory>
{
    public void Configure(EntityTypeBuilder<ObservationCategory> builder)
    {
        builder.ToTable("observation_category", "reference");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(c => c.Code).IsUnique();
        builder.Property(c => c.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(c => c.NameAr).HasMaxLength(100);
    }
}
```

- [ ] **Step 6: Create the repository implementation**

`.../Identity.Infrastructure/Repositories/ObservationCategoryRepository.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class ObservationCategoryRepository : IObservationCategoryRepository
{
    private readonly IdentityDbContext _db;
    public ObservationCategoryRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<ObservationCategory>> GetAllAsync(CancellationToken ct = default)
        => await _db.ObservationCategories.AsNoTracking().OrderBy(c => c.Code).ToListAsync(ct);
}
```

- [ ] **Step 7: Add the DbSet**

In `.../Identity.Infrastructure/Data/IdentityDbContext.cs`, add next to `BloodTypes`:

```csharp
    public DbSet<ObservationCategory> ObservationCategories => Set<ObservationCategory>();
```

- [ ] **Step 8: Register the repository**

In `.../Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs`, add next to the `IBloodTypeRepository` line:

```csharp
        services.AddScoped<IObservationCategoryRepository, ObservationCategoryRepository>();
```

- [ ] **Step 9: Seed the 7 categories**

In `.../Identity.Infrastructure/Data/IdentitySeeder.cs`: add the call inside `SeedAsync` right after `await EnsureBloodTypes(db, ct);`:

```csharp
        await EnsureObservationCategories(db, ct);
```

and add the method (next to `EnsureBloodTypes`):

```csharp
    private static async Task EnsureObservationCategories(IdentityDbContext db, CancellationToken ct)
    {
        var rows = new (string Code, string En, string Ar)[]
        {
            ("allergy", "Allergy", "حساسية"),
            ("surgery", "Surgery", "جراحة"),
            ("chronic", "Chronic", "مرض مزمن"),
            ("autoimmune", "Autoimmune", "مناعي ذاتي"),
            ("composition", "Composition", "تركيب الجسم"),
            ("flag", "Flag", "ملاحظة"),
            ("other", "Other", "أخرى"),
        };
        foreach (var (code, en, ar) in rows)
            if (!await db.ObservationCategories.AnyAsync(c => c.Code == code, ct))
                await db.ObservationCategories.AddAsync(new ObservationCategory(code, en, ar), ct);
    }
```

- [ ] **Step 10: Run the repository test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~ReferenceRepositoryTests`
Expected: PASS.

- [ ] **Step 11: Add an entity test**

Append to `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/ReferenceEntityTests.cs` (inside the class):

```csharp
    [Fact]
    public void ObservationCategory_ctor_trims_and_assigns()
    {
        var c = new ObservationCategory(" allergy ", " Allergy ", " حساسية ");
        Assert.NotEqual(Guid.Empty, c.Id);
        Assert.Equal("allergy", c.Code);
        Assert.Equal("Allergy", c.NameEn);
        Assert.Equal("حساسية", c.NameAr);
    }
```

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~ReferenceEntityTests`
Expected: PASS. (If `ReferenceEntityTests` lacks a `using Kheprx.BaseBackend.Identity.Domain.Entities;`, it already has it — the other reference entities live in the same namespace.)

- [ ] **Step 12: Generate the migration**

Stop the dev server first. From the repo root:

```bash
dotnet ef migrations add CreateObservationCategoryTable \
  --project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --startup-project backend/Kheprx.BaseBackend.Api \
  --context IdentityDbContext
```

Verify the generated `Up()` creates `reference.observation_category` with a unique index on `Code`. Confirm the Identity snapshot updated.

- [ ] **Step 13: Build**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: build succeeds.

- [ ] **Step 14: Commit**

```bash
git add backend/src/Modules/Identity/ backend/tests/Kheprx.BaseBackend.Identity.UnitTests/
git commit -m "feat(reference): add observation_category lookup + seed"
```

---

## Task 2: `observation_category` read endpoint

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/IReferenceService.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/ReferenceService.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Resources/ReferenceMessages.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/ReferenceController.cs`
- Modify: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/ReferenceServiceTests.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/ReferenceControllerTests.cs` (append)

**Interfaces:**
- Consumes: `IObservationCategoryRepository.GetAllAsync` (Task 1); existing `CodedLookupDto(Guid, string, string, string?)`.
- Produces: `IReferenceService.GetObservationCategoriesAsync(CancellationToken) -> Task<IReadOnlyList<CodedLookupDto>>`; `GET /api/reference/observation-categories`.

- [ ] **Step 1: Write the failing service test**

Append to `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/ReferenceServiceTests.cs` (inside the class). Also update the `NewService` helper to accept the new repo (replace the existing helper):

```csharp
    private static ReferenceService NewService(
        IClubRepository? clubs = null, IBloodTypeRepository? blood = null,
        IStrokeRepository? strokes = null, IGenderRepository? genders = null,
        IObservationCategoryRepository? categories = null)
        => new(clubs ?? Mock.Of<IClubRepository>(), blood ?? Mock.Of<IBloodTypeRepository>(),
               strokes ?? Mock.Of<IStrokeRepository>(), genders ?? Mock.Of<IGenderRepository>(),
               categories ?? Mock.Of<IObservationCategoryRepository>());

    [Fact]
    public async Task GetObservationCategories_maps_entities_to_coded_dtos()
    {
        var categories = new Mock<IObservationCategoryRepository>();
        categories.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new[] { new ObservationCategory("allergy", "Allergy", "حساسية") });

        var result = await NewService(categories: categories.Object).GetObservationCategoriesAsync();

        Assert.Single(result);
        Assert.Equal("allergy", result[0].Code);
        Assert.Equal("Allergy", result[0].NameEn);
    }
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~ReferenceServiceTests`
Expected: FAIL — the 5-arg constructor and `GetObservationCategoriesAsync` do not exist.

- [ ] **Step 3: Add the interface method**

In `IReferenceService.cs`, add:

```csharp
    Task<IReadOnlyList<CodedLookupDto>> GetObservationCategoriesAsync(CancellationToken ct = default);
```

- [ ] **Step 4: Implement it in the service**

In `ReferenceService.cs`, add the field + constructor param + method. The class becomes:

```csharp
    private readonly IClubRepository _clubs;
    private readonly IBloodTypeRepository _bloodTypes;
    private readonly IStrokeRepository _strokes;
    private readonly IGenderRepository _genders;
    private readonly IObservationCategoryRepository _categories;

    public ReferenceService(
        IClubRepository clubs, IBloodTypeRepository bloodTypes,
        IStrokeRepository strokes, IGenderRepository genders,
        IObservationCategoryRepository categories)
    {
        _clubs = clubs;
        _bloodTypes = bloodTypes;
        _strokes = strokes;
        _genders = genders;
        _categories = categories;
    }
```

and add the method next to `GetGendersAsync`:

```csharp
    public async Task<IReadOnlyList<CodedLookupDto>> GetObservationCategoriesAsync(CancellationToken ct = default)
        => (await _categories.GetAllAsync(ct)).Select(c => new CodedLookupDto(c.Id, c.Code, c.NameEn, c.NameAr)).ToList();
```

Add the using if missing: `using Kheprx.BaseBackend.Identity.Domain.Repositories;` is already present.

- [ ] **Step 5: Add the message**

In `ReferenceMessages.cs`, inside `Success`:

```csharp
        public static string ObservationCategoriesListed(string lang) => lang switch { "ar" => "فئات البيانات", _ => "Observation categories" };
```

- [ ] **Step 6: Run the service test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~ReferenceServiceTests`
Expected: PASS.

- [ ] **Step 7: Write the failing controller test**

Append to `backend/tests/Kheprx.BaseBackend.Api.UnitTests/ReferenceControllerTests.cs` (inside the class):

```csharp
    [Fact]
    public async Task ObservationCategories_returns_200_with_coded_lookups()
    {
        var svc = new Mock<IReferenceService>();
        svc.Setup(s => s.GetObservationCategoriesAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<CodedLookupDto> { new(Guid.NewGuid(), "allergy", "Allergy", "حساسية") });

        var result = await new ReferenceController(svc.Object).ObservationCategories(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<CodedLookupDto>>>(ok.Value);
        Assert.Equal("allergy", body.Data![0].Code);
    }
```

- [ ] **Step 8: Run it to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter FullyQualifiedName~ReferenceControllerTests`
Expected: FAIL — `ReferenceController.ObservationCategories` does not exist.

- [ ] **Step 9: Add the endpoint**

In `ReferenceController.cs`, add after the `Genders` action:

```csharp
    /// <summary>Lists all observation categories (Swimmer Data Fields).</summary>
    [HttpGet("observation-categories")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CodedLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CodedLookupDto>>>> ObservationCategories(CancellationToken ct)
    {
        var data = await _service.GetObservationCategoriesAsync(ct);
        var body = ApiResponse<IReadOnlyList<CodedLookupDto>>.Success(
            ReferenceMessages.Success.ObservationCategoriesListed(AppLanguage.Current), data);
        return Ok(body);
    }
```

- [ ] **Step 10: Run the controller test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter FullyQualifiedName~ReferenceControllerTests`
Expected: PASS.

- [ ] **Step 11: Commit**

```bash
git add backend/src/Modules/Identity/ backend/Kheprx.BaseBackend.Api/Controllers/ReferenceController.cs backend/tests/
git commit -m "feat(reference): expose GET /api/reference/observation-categories"
```

---

## Task 3: `observation` domain + persistence + schema docs

**Files:**
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Entities/Observation.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Repositories/IObservationRepository.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Configurations/ObservationConfiguration.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Repositories/ObservationRepository.cs`
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Data/HealthDbContext.cs`
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Extensions/HealthModuleExtensions.cs`
- Generated: `.../Health.Infrastructure/Migrations/<timestamp>_CreateObservationTable.cs` (+ snapshot)
- Modify (docs): `docs/references/swimming-database-diagram.html`
- Modify (docs): `docs/superpowers/specs/2026-08-30-swimming-database-design.md`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Entities/ObservationTests.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Repositories/ObservationRepositoryTests.cs`

**Interfaces:**
- Produces: `Observation(Guid swimmerId, Guid categoryId, string fieldLabel, string value, Guid recordedBy)` with props `Id, SwimmerId, CategoryId, FieldLabel, Value, ObservedDate, RecordedBy`; `IObservationRepository.AddAsync(Observation, CancellationToken)` + `SaveChangesAsync(CancellationToken)`; `HealthDbContext.Observations` DbSet.

- [ ] **Step 1: Write the failing entity test**

`backend/tests/Kheprx.BaseBackend.Health.UnitTests/Entities/ObservationTests.cs`:

```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Entities;

public class ObservationTests
{
    [Fact]
    public void Ctor_assigns_id_trims_strings_stamps_observed_date_and_recorder()
    {
        var swimmerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var recordedBy = Guid.NewGuid();

        var o = new Observation(swimmerId, categoryId, " Penicillin ", " Severe ", recordedBy);

        Assert.NotEqual(Guid.Empty, o.Id);
        Assert.Equal(swimmerId, o.SwimmerId);
        Assert.Equal(categoryId, o.CategoryId);
        Assert.Equal("Penicillin", o.FieldLabel);
        Assert.Equal("Severe", o.Value);
        Assert.Equal(recordedBy, o.RecordedBy);
        Assert.NotEqual(default, o.ObservedDate);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~ObservationTests`
Expected: FAIL — `Observation` does not exist.

- [ ] **Step 3: Create the entity**

`.../Health.Domain/Entities/Observation.cs`:

```csharp
namespace Kheprx.BaseBackend.Health.Domain.Entities;

public sealed class Observation
{
    public Guid Id { get; private set; }
    public Guid SwimmerId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string FieldLabel { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public DateTime ObservedDate { get; private set; }
    public Guid RecordedBy { get; private set; }

    private Observation() { } // EF Core

    public Observation(Guid swimmerId, Guid categoryId, string fieldLabel, string value, Guid recordedBy)
    {
        Id = Guid.NewGuid();
        SwimmerId = swimmerId;
        CategoryId = categoryId;
        FieldLabel = fieldLabel.Trim();
        Value = value.Trim();
        ObservedDate = DateTime.UtcNow;
        RecordedBy = recordedBy;
    }
}
```

- [ ] **Step 4: Run the entity test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~ObservationTests`
Expected: PASS.

- [ ] **Step 5: Write the failing repository test**

`backend/tests/Kheprx.BaseBackend.Health.UnitTests/Repositories/ObservationRepositoryTests.cs`:

```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Kheprx.BaseBackend.Health.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Repositories;

public class ObservationRepositoryTests
{
    private static HealthDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<HealthDbContext>()
            .UseInMemoryDatabase($"health-{Guid.NewGuid()}")
            .Options;
        return new HealthDbContext(options);
    }

    [Fact]
    public async Task Add_then_Save_persists_observation()
    {
        await using var db = NewDb();
        var repo = new ObservationRepository(db);
        var o = new Observation(Guid.NewGuid(), Guid.NewGuid(), "Penicillin", "Severe", Guid.NewGuid());

        await repo.AddAsync(o);
        await repo.SaveChangesAsync();

        var saved = await db.Observations.AsNoTracking().SingleAsync();
        Assert.Equal("Penicillin", saved.FieldLabel);
        Assert.NotEqual(default, saved.ObservedDate);
    }
}
```

- [ ] **Step 6: Run it to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~ObservationRepositoryTests`
Expected: FAIL — `ObservationRepository` / `db.Observations` do not exist.

- [ ] **Step 7: Create the repository interface**

`.../Health.Domain/Repositories/IObservationRepository.cs`:

```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;

namespace Kheprx.BaseBackend.Health.Domain.Repositories;

public interface IObservationRepository
{
    Task AddAsync(Observation observation, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

- [ ] **Step 8: Create the EF configuration**

`.../Health.Infrastructure/Configurations/ObservationConfiguration.cs`:

```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Health.Infrastructure.Configurations;

internal sealed class ObservationConfiguration : IEntityTypeConfiguration<Observation>
{
    public void Configure(EntityTypeBuilder<Observation> builder)
    {
        builder.ToTable("observation", "health");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.SwimmerId).IsRequired();   // loose Guid — no cross-module FK
        builder.Property(o => o.CategoryId).IsRequired();  // loose Guid — reference.observation_category (other module)
        builder.Property(o => o.FieldLabel).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Value).HasMaxLength(500).IsRequired();
        builder.Property(o => o.ObservedDate).IsRequired(); // DateTime → timestamptz (npgsql default)
        builder.Property(o => o.RecordedBy).IsRequired();  // loose Guid — no cross-module FK
    }
}
```

- [ ] **Step 9: Create the repository implementation**

`.../Health.Infrastructure/Repositories/ObservationRepository.cs`:

```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Kheprx.BaseBackend.Health.Infrastructure.Data;

namespace Kheprx.BaseBackend.Health.Infrastructure.Repositories;

internal sealed class ObservationRepository : IObservationRepository
{
    private readonly HealthDbContext _db;
    public ObservationRepository(HealthDbContext db) => _db = db;

    public async Task AddAsync(Observation observation, CancellationToken ct = default)
        => await _db.Observations.AddAsync(observation, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
```

- [ ] **Step 10: Add the DbSet**

In `.../Health.Infrastructure/Data/HealthDbContext.cs`, add next to `HealthReadings`:

```csharp
    public DbSet<Observation> Observations => Set<Observation>();
```

- [ ] **Step 11: Register the repository**

In `.../Health.Infrastructure/Extensions/HealthModuleExtensions.cs`, add next to the `IHealthReadingRepository` registration:

```csharp
        services.AddScoped<IObservationRepository, ObservationRepository>();
```

- [ ] **Step 12: Run the repository test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~ObservationRepositoryTests`
Expected: PASS.

- [ ] **Step 13: Generate the migration**

Stop the dev server first. From the repo root:

```bash
dotnet ef migrations add CreateObservationTable \
  --project backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure \
  --startup-project backend/Kheprx.BaseBackend.Api \
  --context HealthDbContext
```

Verify the `Up()` creates `health.observation` with `field_label`/`value` as `character varying`, `observed_date` as `timestamp with time zone`, and no foreign keys. Confirm the Health snapshot updated.

- [ ] **Step 14: Update the schema docs**

In `docs/references/swimming-database-diagram.html`:
- Change `observation.observed_date` from `{c:"observed_date",t:"date",...}` to `t:"timestamptz"`.
- In the `IMPLEMENTED` set, add `"reference.observation_category"` and `"health.observation"` (so both render green).

In `docs/superpowers/specs/2026-08-30-swimming-database-design.md`, change the `observation` table's `observed_date` row type from `date` to `timestamptz`.

- [ ] **Step 15: Build**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: build succeeds.

- [ ] **Step 16: Commit**

```bash
git add backend/src/Modules/Health/ backend/tests/Kheprx.BaseBackend.Health.UnitTests/ docs/references/swimming-database-diagram.html docs/superpowers/specs/2026-08-30-swimming-database-design.md
git commit -m "feat(health): add observation table + persistence; schema doc updates"
```

---

## Task 4: `observation` application + API

**Files:**
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/DTOs/ObservationDtos.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Resources/ObservationMessages.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/Interfaces/IObservationService.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/ObservationService.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Validators/CreateObservationRequestValidator.cs`
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Extensions/HealthModuleExtensions.cs`
- Create: `backend/Kheprx.BaseBackend.Api/Controllers/ObservationsController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Services/ObservationServiceTests.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Validators/CreateObservationRequestValidatorTests.cs`

**Interfaces:**
- Consumes: `IObservationRepository` (Task 3); `BaseApiController.CurrentUserId()`, `ApiResponse<T>`.
- Produces: `CreateObservationRequest(Guid SwimmerId, Guid CategoryId, string FieldLabel, string Value)`; `ObservationDto(Guid Id, Guid SwimmerId, Guid CategoryId, string FieldLabel, string Value, DateTime ObservedDate, Guid RecordedBy)`; `IObservationService.CreateAsync(CreateObservationRequest, Guid recordedBy, CancellationToken) -> Task<ObservationDto>`; `POST /api/observations`.

- [ ] **Step 1: Write the failing service test**

`backend/tests/Kheprx.BaseBackend.Health.UnitTests/Services/ObservationServiceTests.cs`:

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Services;

public class ObservationServiceTests
{
    [Fact]
    public async Task Create_persists_observation_stamps_recorder_and_returns_dto()
    {
        var repo = new Mock<IObservationRepository>();
        Observation? added = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Observation>(), It.IsAny<CancellationToken>()))
            .Callback<Observation, CancellationToken>((o, _) => added = o)
            .Returns(Task.CompletedTask);
        var svc = new ObservationService(repo.Object);
        var recordedBy = Guid.NewGuid();
        var req = new CreateObservationRequest(Guid.NewGuid(), Guid.NewGuid(), "Penicillin", "Severe");

        var dto = await svc.CreateAsync(req, recordedBy);

        Assert.NotNull(added);
        Assert.Equal(recordedBy, added!.RecordedBy);
        Assert.Equal("Penicillin", dto.FieldLabel);
        Assert.Equal("Severe", dto.Value);
        Assert.NotEqual(default, dto.ObservedDate);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~ObservationServiceTests`
Expected: FAIL — service/DTOs do not exist.

- [ ] **Step 3: Create the DTOs**

`.../Health.Application/DTOs/ObservationDtos.cs`:

```csharp
namespace Kheprx.BaseBackend.Health.Application.DTOs;

/// <summary>Payload to add a swimmer data field (POST /api/observations).</summary>
public sealed record CreateObservationRequest(
    Guid SwimmerId,
    Guid CategoryId,
    string FieldLabel,
    string Value);

/// <summary>A stored observation (swimmer data field).</summary>
public sealed record ObservationDto(
    Guid Id,
    Guid SwimmerId,
    Guid CategoryId,
    string FieldLabel,
    string Value,
    DateTime ObservedDate,
    Guid RecordedBy);
```

- [ ] **Step 4: Create the messages**

`.../Health.Application/Resources/ObservationMessages.cs`:

```csharp
namespace Kheprx.BaseBackend.Health.Application.Resources;

/// <summary>Localized messages for swimmer data fields (observations).</summary>
public static class ObservationMessages
{
    public static class Success
    {
        public static string Added(string lang) => lang switch { "ar" => "تم إضافة البيان", _ => "Data field added" };
    }

    public static class Errors
    {
        public static string SwimmerRequired(string lang) => lang switch { "ar" => "السبّاح مطلوب", _ => "Swimmer is required" };
        public static string CategoryRequired(string lang) => lang switch { "ar" => "الفئة مطلوبة", _ => "Category is required" };
        public static string FieldLabelRequired(string lang) => lang switch { "ar" => "اسم الحقل مطلوب", _ => "Field name is required" };
        public static string ValueRequired(string lang) => lang switch { "ar" => "القيمة مطلوبة", _ => "Value is required" };
    }
}
```

- [ ] **Step 5: Create the service interface**

`.../Health.Application/Services/Interfaces/IObservationService.cs`:

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;

namespace Kheprx.BaseBackend.Health.Application.Services.Interfaces;

public interface IObservationService
{
    Task<ObservationDto> CreateAsync(CreateObservationRequest request, Guid recordedBy, CancellationToken ct = default);
}
```

- [ ] **Step 6: Create the service**

`.../Health.Application/Services/ObservationService.cs`:

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;

namespace Kheprx.BaseBackend.Health.Application.Services;

internal sealed class ObservationService : IObservationService
{
    private readonly IObservationRepository _observations;
    public ObservationService(IObservationRepository observations) => _observations = observations;

    public async Task<ObservationDto> CreateAsync(CreateObservationRequest request, Guid recordedBy, CancellationToken ct = default)
    {
        var observation = new Observation(request.SwimmerId, request.CategoryId, request.FieldLabel, request.Value, recordedBy);
        await _observations.AddAsync(observation, ct);
        await _observations.SaveChangesAsync(ct);
        return ToDto(observation);
    }

    private static ObservationDto ToDto(Observation o) =>
        new(o.Id, o.SwimmerId, o.CategoryId, o.FieldLabel, o.Value, o.ObservedDate, o.RecordedBy);
}
```

- [ ] **Step 7: Register the service**

In `.../Health.Infrastructure/Extensions/HealthModuleExtensions.cs`, add next to the `IObservationRepository` registration (from Task 3):

```csharp
        services.AddScoped<IObservationService, ObservationService>();
```

Ensure the usings `Kheprx.BaseBackend.Health.Application.Services` and `...Services.Interfaces` are present (they already are for the existing services).

- [ ] **Step 8: Run the service test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~ObservationServiceTests`
Expected: PASS.

- [ ] **Step 9: Write the failing validator test**

`backend/tests/Kheprx.BaseBackend.Health.UnitTests/Validators/CreateObservationRequestValidatorTests.cs`:

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Validators;

public class CreateObservationRequestValidatorTests
{
    private static CreateObservationRequest Valid() =>
        new(SwimmerId: Guid.NewGuid(), CategoryId: Guid.NewGuid(), FieldLabel: "Penicillin", Value: "Severe");

    private readonly CreateObservationRequestValidator _v = new();

    [Fact] public void Valid_request_passes() => Assert.True(_v.Validate(Valid()).IsValid);

    [Fact]
    public void Empty_fields_fail()
    {
        Assert.False(_v.Validate(Valid() with { SwimmerId = Guid.Empty }).IsValid);
        Assert.False(_v.Validate(Valid() with { CategoryId = Guid.Empty }).IsValid);
        Assert.False(_v.Validate(Valid() with { FieldLabel = "" }).IsValid);
        Assert.False(_v.Validate(Valid() with { Value = "" }).IsValid);
    }
}
```

- [ ] **Step 10: Run it to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~CreateObservationRequestValidatorTests`
Expected: FAIL — validator does not exist.

- [ ] **Step 11: Create the validator**

`.../Health.Application/Validators/CreateObservationRequestValidator.cs`:

```csharp
using FluentValidation;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Health.Application.Validators;

public sealed class CreateObservationRequestValidator : AbstractValidator<CreateObservationRequest>
{
    public CreateObservationRequestValidator()
    {
        RuleFor(x => x.SwimmerId)
            .NotEmpty().WithMessage(_ => ObservationMessages.Errors.SwimmerRequired(AppLanguage.Current));
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage(_ => ObservationMessages.Errors.CategoryRequired(AppLanguage.Current));
        RuleFor(x => x.FieldLabel)
            .NotEmpty().WithMessage(_ => ObservationMessages.Errors.FieldLabelRequired(AppLanguage.Current))
            .MaximumLength(200);
        RuleFor(x => x.Value)
            .NotEmpty().WithMessage(_ => ObservationMessages.Errors.ValueRequired(AppLanguage.Current))
            .MaximumLength(500);
    }
}
```

- [ ] **Step 12: Run the validator test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~CreateObservationRequestValidatorTests`
Expected: PASS.

- [ ] **Step 13: Create the controller**

`backend/Kheprx.BaseBackend.Api/Controllers/ObservationsController.cs`:

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Route("api/observations")]
[Authorize(Roles = "head_coach,captain")]
public sealed class ObservationsController : BaseApiController
{
    private readonly IObservationService _service;
    public ObservationsController(IObservationService service) => _service = service;

    /// <summary>Adds a swimmer data field (observation). Head Coach or Captain only.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ObservationDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<ObservationDto>>> Create(CreateObservationRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, CurrentUserId(), ct);
        var body = ApiResponse<ObservationDto>.Success(
            ObservationMessages.Success.Added(AppLanguage.Current), created);
        return StatusCode(StatusCodes.Status201Created, body);
    }
}
```

- [ ] **Step 14: Build and run backend suites**

Stop the dev server. Run:
```bash
dotnet build backend/Kheprx.BaseBackend.sln
dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests
dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests
dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests
dotnet test backend/tests/Kheprx.BaseBackend.ArchitectureTests
```
Expected: build succeeds; all suites green.

- [ ] **Step 15: Commit**

```bash
git add backend/src/Modules/Health/ backend/Kheprx.BaseBackend.Api/Controllers/ObservationsController.cs backend/tests/Kheprx.BaseBackend.Health.UnitTests/
git commit -m "feat(health): add observations application service + POST endpoint"
```

---

## Task 5: Frontend — load observation categories

**Files:**
- Modify: `frontend/src/app/features/reference/domain/repositories/reference.repository.ts`
- Modify: `frontend/src/app/features/reference/data/repositories/reference.repository.impl.ts`
- Create: `frontend/src/app/features/reference/domain/usecases/load-observation-categories.use-case.ts`
- Test: `frontend/src/app/features/reference/testing/domain/usecases/load-observation-categories.use-case.spec.ts`

**Interfaces:**
- Consumes: `GET /api/reference/observation-categories` (Task 2); existing `CodedLookupListDtoRs`, `isCodedLookupListValid`, `LookupItem`.
- Produces: `IReferenceRepository.getObservationCategories() -> Promise<CodedLookupListDtoRs>`; `LoadObservationCategoriesUseCase extends UseCase<void, LookupItem[]>`.

- [ ] **Step 1: Write the failing use-case test**

`frontend/src/app/features/reference/testing/domain/usecases/load-observation-categories.use-case.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { LoadObservationCategoriesUseCase } from '@features/reference/domain/usecases/load-observation-categories.use-case';
import { REFERENCE_REPOSITORY, IReferenceRepository } from '@features/reference/domain/repositories/reference.repository';

function build(repo: IReferenceRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: REFERENCE_REPOSITORY, useValue: repo }] });
  return TestBed.inject(LoadObservationCategoriesUseCase);
}

describe('LoadObservationCategoriesUseCase', () => {
  it('maps coded lookups to LookupItems', async () => {
    const repo = { getObservationCategories: async () => ({ data: [{ id: 'c1', code: 'allergy', nameEn: 'Allergy', nameAr: 'حساسية' }] }) } as unknown as IReferenceRepository;
    const uc = build(repo);
    const r = await uc.run();
    expect(r.ok).toBe(true);
    if (r.ok) { expect(r.data[0].id).toBe('c1'); expect(r.data[0].code).toBe('allergy'); }
  });

  it('fails validation on malformed data', async () => {
    const repo = { getObservationCategories: async () => ({ data: [{ id: 'c1' }] }) } as unknown as IReferenceRepository;
    const uc = build(repo);
    const r = await uc.run();
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run (from `frontend/`): `npx jest src/app/features/reference/testing/domain/usecases/load-observation-categories.use-case.spec.ts`
Expected: FAIL — `LoadObservationCategoriesUseCase` / `getObservationCategories` do not exist.

- [ ] **Step 3: Add the repository port method**

In `reference.repository.ts`, add to the `IReferenceRepository` interface:

```typescript
  getObservationCategories(): Promise<CodedLookupListDtoRs>;
```

- [ ] **Step 4: Implement it**

In `reference.repository.impl.ts`, add the method:

```typescript
  getObservationCategories(): Promise<CodedLookupListDtoRs> {
    return this.http.get<CodedLookupListDtoRs>('/api/reference/observation-categories');
  }
```

- [ ] **Step 5: Create the use-case**

`frontend/src/app/features/reference/domain/usecases/load-observation-categories.use-case.ts`:

```typescript
// load-observation-categories.use-case.ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { REFERENCE_REPOSITORY } from '@features/reference/domain/repositories/reference.repository';
import { isCodedLookupListValid } from '@features/reference/data/dto/reference.dto';
import { LookupItem } from '@features/reference/domain/model/reference';

@Injectable({ providedIn: 'root' })
export class LoadObservationCategoriesUseCase extends UseCase<void, LookupItem[]> {
  private readonly repo = inject(REFERENCE_REPOSITORY);
  constructor() { super('LoadObservationCategories'); }
  protected async execute(): Promise<LookupItem[]> {
    const res = await this.repo.getObservationCategories();
    if (!isCodedLookupListValid(res.data)) throw new AppError('Invalid observation categories received', 'validation');
    return res.data.map((c) => ({ id: c.id, code: c.code, nameEn: c.nameEn, nameAr: c.nameAr }));
  }
}
```

- [ ] **Step 6: Run the use-case test to verify it passes**

Run (from `frontend/`): `npx jest src/app/features/reference/testing/domain/usecases/load-observation-categories.use-case.spec.ts`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/app/features/reference/
git commit -m "feat(reference): add LoadObservationCategoriesUseCase"
```

---

## Task 6: Frontend — `observations` feature slice

**Files:**
- Create: `frontend/src/app/features/observations/domain/model/observation.ts`
- Create: `frontend/src/app/features/observations/data/dto/observation.dto.ts`
- Create: `frontend/src/app/features/observations/domain/repositories/observation.repository.ts`
- Create: `frontend/src/app/features/observations/data/repositories/observation.repository.impl.ts`
- Create: `frontend/src/app/features/observations/domain/usecases/create-observation.use-case.ts`
- Create: `frontend/src/app/features/observations/data/observation.providers.ts`
- Modify: `frontend/src/app/app.config.ts`
- Test: `frontend/src/app/features/observations/testing/data/repositories/observation.repository.impl.spec.ts`
- Test: `frontend/src/app/features/observations/testing/domain/usecases/create-observation.use-case.spec.ts`

**Interfaces:**
- Produces: `CreateObservationDtoRq { swimmerId, categoryId, fieldLabel, value }`; `OBSERVATION_REPOSITORY` token + `IObservationRepository.create(rq)`; `CreateObservationUseCase extends UseCase<CreateObservationDtoRq, Observation>`; `OBSERVATION_PROVIDERS`.

- [ ] **Step 1: Create the domain model**

`frontend/src/app/features/observations/domain/model/observation.ts`:

```typescript
export interface Observation {
  id: string;
  swimmerId: string;
  categoryId: string;
  fieldLabel: string;
  value: string;
  observedDate: string;
  recordedBy: string;
}
```

- [ ] **Step 2: Create the DTOs + guard**

`frontend/src/app/features/observations/data/dto/observation.dto.ts`:

```typescript
// observation.dto.ts — observation request/response DTOs (API_FLOW convention).
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface ObservationDtoRs {
  id: string;
  swimmerId: string;
  categoryId: string;
  fieldLabel: string;
  value: string;
  observedDate: string;
  recordedBy: string;
}

export interface ObservationItemDtoRs extends BaseResponseRs<ObservationDtoRs> {}

export interface CreateObservationDtoRq {
  swimmerId: string;
  categoryId: string;
  fieldLabel: string;
  value: string;
}

export function isObservationDtoRsValid(dto: unknown): dto is ObservationDtoRs {
  const d = dto as ObservationDtoRs;
  return !!d
    && typeof d.id === 'string'
    && typeof d.swimmerId === 'string'
    && typeof d.categoryId === 'string'
    && typeof d.fieldLabel === 'string'
    && typeof d.value === 'string'
    && typeof d.observedDate === 'string'
    && typeof d.recordedBy === 'string';
}
```

- [ ] **Step 3: Create the repository interface + token**

`frontend/src/app/features/observations/domain/repositories/observation.repository.ts`:

```typescript
import { InjectionToken } from '@angular/core';
import { ObservationItemDtoRs, CreateObservationDtoRq } from '@features/observations/data/dto/observation.dto';

export interface IObservationRepository {
  create(rq: CreateObservationDtoRq): Promise<ObservationItemDtoRs>;
}

export const OBSERVATION_REPOSITORY = new InjectionToken<IObservationRepository>('OBSERVATION_REPOSITORY');
```

- [ ] **Step 4: Write the failing repository-impl test**

`frontend/src/app/features/observations/testing/data/repositories/observation.repository.impl.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { ObservationRepositoryImpl } from '@features/observations/data/repositories/observation.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('ObservationRepositoryImpl', () => {
  const http = { post: jest.fn() } as unknown as HttpClientService;
  let repo: ObservationRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [ObservationRepositoryImpl, { provide: HttpClientService, useValue: http }],
    });
    repo = TestBed.inject(ObservationRepositoryImpl);
  });

  it('create POSTs /api/observations with the body', async () => {
    (http.post as jest.Mock).mockResolvedValue({ data: {} });
    const rq = { swimmerId: 's1', categoryId: 'c1', fieldLabel: 'Penicillin', value: 'Severe' };
    await repo.create(rq);
    expect(http.post).toHaveBeenCalledWith('/api/observations', { body: rq });
  });
});
```

- [ ] **Step 5: Run it to verify it fails**

Run (from `frontend/`): `npx jest src/app/features/observations/testing/data/repositories/observation.repository.impl.spec.ts`
Expected: FAIL — `ObservationRepositoryImpl` does not exist.

- [ ] **Step 6: Create the repository implementation**

`frontend/src/app/features/observations/data/repositories/observation.repository.impl.ts`:

```typescript
// observation.repository.impl.ts — observations repository. Returns the API DTO envelope.
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { IObservationRepository } from '@features/observations/domain/repositories/observation.repository';
import { ObservationItemDtoRs, CreateObservationDtoRq } from '@features/observations/data/dto/observation.dto';

@Injectable({ providedIn: 'root' })
export class ObservationRepositoryImpl implements IObservationRepository {
  private readonly http = inject(HttpClientService);

  create(rq: CreateObservationDtoRq): Promise<ObservationItemDtoRs> {
    return this.http.post<ObservationItemDtoRs>('/api/observations', { body: rq });
  }
}
```

- [ ] **Step 7: Run the repository test to verify it passes**

Run (from `frontend/`): `npx jest src/app/features/observations/testing/data/repositories/observation.repository.impl.spec.ts`
Expected: PASS.

- [ ] **Step 8: Write the failing use-case test**

`frontend/src/app/features/observations/testing/domain/usecases/create-observation.use-case.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { CreateObservationUseCase } from '@features/observations/domain/usecases/create-observation.use-case';
import { OBSERVATION_REPOSITORY, IObservationRepository } from '@features/observations/domain/repositories/observation.repository';

const RQ = { swimmerId: 's1', categoryId: 'c1', fieldLabel: 'Penicillin', value: 'Severe' };
const DTO = { id: 'o1', swimmerId: 's1', categoryId: 'c1', fieldLabel: 'Penicillin', value: 'Severe', observedDate: '2026-09-18T00:00:00Z', recordedBy: 'u1' };

function build(repo: IObservationRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: OBSERVATION_REPOSITORY, useValue: repo }] });
  return TestBed.inject(CreateObservationUseCase);
}

describe('CreateObservationUseCase', () => {
  it('maps the created DTO to a model', async () => {
    const repo = { create: async () => ({ data: DTO }) } as unknown as IObservationRepository;
    const uc = build(repo);
    const r = await uc.run(RQ);
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.data.id).toBe('o1');
  });

  it('fails validation when the response is malformed', async () => {
    const repo = { create: async () => ({ data: { id: 'o1' } }) } as unknown as IObservationRepository;
    const uc = build(repo);
    const r = await uc.run(RQ);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
```

- [ ] **Step 9: Run it to verify it fails**

Run (from `frontend/`): `npx jest src/app/features/observations/testing/domain/usecases/create-observation.use-case.spec.ts`
Expected: FAIL — `CreateObservationUseCase` does not exist.

- [ ] **Step 10: Create the use-case**

`frontend/src/app/features/observations/domain/usecases/create-observation.use-case.ts`:

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { OBSERVATION_REPOSITORY } from '@features/observations/domain/repositories/observation.repository';
import { CreateObservationDtoRq, isObservationDtoRsValid } from '@features/observations/data/dto/observation.dto';
import { Observation } from '@features/observations/domain/model/observation';

@Injectable({ providedIn: 'root' })
export class CreateObservationUseCase extends UseCase<CreateObservationDtoRq, Observation> {
  private readonly repo = inject(OBSERVATION_REPOSITORY);
  constructor() { super('CreateObservation'); }

  protected async execute(input: CreateObservationDtoRq): Promise<Observation> {
    const res = await this.repo.create(input);
    if (!isObservationDtoRsValid(res.data)) throw new AppError('Invalid created observation received', 'validation');
    const d = res.data;
    return {
      id: d.id, swimmerId: d.swimmerId, categoryId: d.categoryId,
      fieldLabel: d.fieldLabel, value: d.value, observedDate: d.observedDate, recordedBy: d.recordedBy,
    };
  }
}
```

- [ ] **Step 11: Run the use-case test to verify it passes**

Run (from `frontend/`): `npx jest src/app/features/observations/testing/domain/usecases/create-observation.use-case.spec.ts`
Expected: PASS.

- [ ] **Step 12: Create the providers and register them**

`frontend/src/app/features/observations/data/observation.providers.ts`:

```typescript
import { Provider } from '@angular/core';
import { OBSERVATION_REPOSITORY } from '@features/observations/domain/repositories/observation.repository';
import { ObservationRepositoryImpl } from '@features/observations/data/repositories/observation.repository.impl';

// Live wiring: bind the observations repository port to the HTTP impl (/api/observations).
export const OBSERVATION_PROVIDERS: Provider[] = [
  { provide: OBSERVATION_REPOSITORY, useClass: ObservationRepositoryImpl },
];
```

In `frontend/src/app/app.config.ts`, add the import next to the other feature providers:
```typescript
import { OBSERVATION_PROVIDERS } from '@features/observations/data/observation.providers';
```
and spread it into the providers array next to `...HEALTH_READING_PROVIDERS`:
```typescript
    ...HEALTH_READING_PROVIDERS,
    ...OBSERVATION_PROVIDERS,
```

- [ ] **Step 13: Commit**

```bash
git add frontend/src/app/features/observations/ frontend/src/app/app.config.ts
git commit -m "feat(observations): add observations frontend feature slice"
```

---

## Task 7: Frontend — Swimmer Data Fields page + wiring

**Files:**
- Create: `frontend/src/app/features/captain-panel/presentation/pages/swimmer-data/swimmer-data.viewmodel.ts`
- Create: `frontend/src/app/features/captain-panel/presentation/pages/swimmer-data/swimmer-data.page.ts`
- Create: `frontend/src/app/features/captain-panel/presentation/pages/swimmer-data/swimmer-data.page.html`
- Modify: `frontend/src/app/features/captain-panel/index.ts`
- Modify: `frontend/src/app/features/captain-panel/presentation/pages/captain-panel/captain-panel.page.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`
- Test: `frontend/src/app/features/captain-panel/testing/presentation/pages/swimmer-data/swimmer-data.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `ListSwimmersUseCase` (`@features/swimmers/domain/usecases/list-swimmers.use-case`), `LoadObservationCategoriesUseCase` (Task 5), `CreateObservationUseCase` (Task 6), `NotificationService`, `TranslateService`, `SelectFieldComponent`, `TextFieldComponent`, `LookupItem`.
- Produces: `SwimmerDataViewModel`, `SwimmerDataPage`, route `captain-panel/swimmer-data`.

- [ ] **Step 1: Write the failing viewmodel test**

`frontend/src/app/features/captain-panel/testing/presentation/pages/swimmer-data/swimmer-data.viewmodel.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { SwimmerDataViewModel } from '@features/captain-panel/presentation/pages/swimmer-data/swimmer-data.viewmodel';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { LoadObservationCategoriesUseCase } from '@features/reference/domain/usecases/load-observation-categories.use-case';
import { CreateObservationUseCase } from '@features/observations/domain/usecases/create-observation.use-case';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';

const swimmer = { id: 's1', uid: 'SW-1', nameEn: 'Ahmed', nameAr: null, clubNameEn: null, clubNameAr: null, gender: 'male', age: 15 };
const category = { id: 'c1', code: 'allergy', nameEn: 'Allergy', nameAr: 'حساسية' };

function build(over: { create?: unknown } = {}) {
  const swimmersUc = { run: jest.fn().mockResolvedValue({ ok: true, data: [swimmer] }) };
  const catsUc = { run: jest.fn().mockResolvedValue({ ok: true, data: [category] }) };
  const createUc = { run: jest.fn().mockResolvedValue(over.create ?? { ok: true, data: { id: 'o1' } }) };
  const notify = { success: jest.fn(), error: jest.fn() };
  const i18n = { t: (k: string) => k };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [
    SwimmerDataViewModel,
    { provide: ListSwimmersUseCase, useValue: swimmersUc },
    { provide: LoadObservationCategoriesUseCase, useValue: catsUc },
    { provide: CreateObservationUseCase, useValue: createUc },
    { provide: NotificationService, useValue: notify },
    { provide: TranslateService, useValue: i18n },
  ] });
  return { vm: TestBed.inject(SwimmerDataViewModel), createUc, notify };
}

describe('SwimmerDataViewModel', () => {
  it('loads swimmers and categories on construction', async () => {
    const { vm } = build();
    await Promise.resolve(); await Promise.resolve();
    expect(vm.swimmerOptions()).toHaveLength(1);
    expect(vm.categoryOptions()).toHaveLength(1);
  });

  it('canSubmit requires swimmer, category, field name and value', async () => {
    const { vm } = build();
    await Promise.resolve(); await Promise.resolve();
    expect(vm.canSubmit()).toBe(false);
    vm.swimmerId.set('s1'); vm.categoryId.set('c1'); vm.fieldLabel.set('Penicillin');
    expect(vm.canSubmit()).toBe(false);
    vm.value.set('Severe');
    expect(vm.canSubmit()).toBe(true);
  });

  it('submit adds the data, toasts success and clears field + value (keeps swimmer + category)', async () => {
    const { vm, notify } = build();
    await Promise.resolve(); await Promise.resolve();
    vm.swimmerId.set('s1'); vm.categoryId.set('c1'); vm.fieldLabel.set('Penicillin'); vm.value.set('Severe');
    await vm.submit();
    expect(notify.success).toHaveBeenCalledWith('swimmerData.toasts.added');
    expect(vm.fieldLabel()).toBe('');
    expect(vm.value()).toBe('');
    expect(vm.swimmerId()).toBe('s1');
    expect(vm.categoryId()).toBe('c1');
  });

  it('submit shows an error toast on failure', async () => {
    const { vm, notify } = build({ create: { ok: false, error: { status: 500 } } });
    await Promise.resolve(); await Promise.resolve();
    vm.swimmerId.set('s1'); vm.categoryId.set('c1'); vm.fieldLabel.set('Penicillin'); vm.value.set('Severe');
    await vm.submit();
    expect(notify.error).toHaveBeenCalledWith('swimmerData.toasts.addFailed');
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run (from `frontend/`): `npx jest src/app/features/captain-panel/testing/presentation/pages/swimmer-data/swimmer-data.viewmodel.spec.ts`
Expected: FAIL — `SwimmerDataViewModel` does not exist.

- [ ] **Step 3: Create the viewmodel**

`frontend/src/app/features/captain-panel/presentation/pages/swimmer-data/swimmer-data.viewmodel.ts`:

```typescript
import { Injectable, computed, inject, signal } from '@angular/core';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { LoadObservationCategoriesUseCase } from '@features/reference/domain/usecases/load-observation-categories.use-case';
import { CreateObservationUseCase } from '@features/observations/domain/usecases/create-observation.use-case';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';
import { LookupItem } from '@features/reference/domain/model/reference';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';

@Injectable()
export class SwimmerDataViewModel {
  private readonly listSwimmers = inject(ListSwimmersUseCase);
  private readonly loadCategories = inject(LoadObservationCategoriesUseCase);
  private readonly createObservation = inject(CreateObservationUseCase);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  readonly swimmers = signal<SwimmerListItem[]>([]);
  readonly categories = signal<LookupItem[]>([]);
  readonly loading = signal(false);
  readonly error = signal(false);

  readonly swimmerId = signal('');
  readonly categoryId = signal('');
  readonly fieldLabel = signal('');
  readonly value = signal('');
  readonly submitting = signal(false);

  readonly swimmerOptions = computed<LookupItem[]>(() =>
    this.swimmers().map((s) => ({ id: s.id, nameEn: s.nameEn, nameAr: s.nameAr })));

  readonly categoryOptions = computed<LookupItem[]>(() => this.categories());

  readonly canSubmit = computed(() =>
    this.swimmerId().length > 0
    && this.categoryId().length > 0
    && this.fieldLabel().trim().length > 0
    && this.value().trim().length > 0);

  constructor() { void this.load(); }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(false);
    const [s, c] = await Promise.all([this.listSwimmers.run(undefined), this.loadCategories.run()]);
    this.loading.set(false);
    if (s.ok) this.swimmers.set(s.data); else this.error.set(true);
    if (c.ok) this.categories.set(c.data); else this.error.set(true);
  }

  async submit(): Promise<void> {
    if (!this.canSubmit() || this.submitting()) return;
    this.submitting.set(true);
    const r = await this.createObservation.run({
      swimmerId: this.swimmerId(),
      categoryId: this.categoryId(),
      fieldLabel: this.fieldLabel().trim(),
      value: this.value().trim(),
    });
    this.submitting.set(false);
    if (r.ok) {
      this.notify.success(this.i18n.t('swimmerData.toasts.added'));
      this.fieldLabel.set('');
      this.value.set('');
    } else {
      this.notify.error(this.i18n.t('swimmerData.toasts.addFailed'));
    }
  }
}
```

- [ ] **Step 4: Run the viewmodel test to verify it passes**

Run (from `frontend/`): `npx jest src/app/features/captain-panel/testing/presentation/pages/swimmer-data/swimmer-data.viewmodel.spec.ts`
Expected: PASS.

- [ ] **Step 5: Create the page component**

`frontend/src/app/features/captain-panel/presentation/pages/swimmer-data/swimmer-data.page.ts`:

```typescript
import { Component, inject } from '@angular/core';
import { TranslatePipe } from '@core/i18n';
import { SelectFieldComponent } from '@core/ui/components/select-field.component';
import { TextFieldComponent } from '@core/ui/components/text-field.component';
import { SwimmerDataViewModel } from './swimmer-data.viewmodel';

@Component({
  selector: 'app-swimmer-data-page',
  standalone: true,
  imports: [TranslatePipe, SelectFieldComponent, TextFieldComponent],
  templateUrl: './swimmer-data.page.html',
})
export class SwimmerDataPage {
  protected readonly vm = inject(SwimmerDataViewModel);
}
```

- [ ] **Step 6: Create the page template**

`frontend/src/app/features/captain-panel/presentation/pages/swimmer-data/swimmer-data.page.html`:

```html
<div>
  <header class="mb-8">
    <h1 class="font-heading text-3xl text-ink">{{ 'swimmerData.title' | translate }}</h1>
    <p class="mt-2 text-muted">{{ 'swimmerData.description' | translate }}</p>
  </header>

  @if (vm.loading()) {
    <p class="text-muted">{{ 'swimmerData.states.loading' | translate }}</p>
  } @else if (vm.error()) {
    <p class="text-danger">{{ 'swimmerData.states.error' | translate }}</p>
  } @else {
    <section class="rounded-2xl border border-border bg-card p-6 shadow-sm">
      <h2 class="font-heading text-xl text-ink">{{ 'swimmerData.add.title' | translate }}</h2>
      <form class="mt-4 grid grid-cols-1 gap-5 md:grid-cols-2 lg:grid-cols-4"
            (submit)="$event.preventDefault(); vm.submit()">
        <app-select-field
          [label]="'swimmerData.fields.swimmer' | translate"
          [placeholder]="'swimmerData.placeholders.swimmer' | translate"
          [options]="vm.swimmerOptions()"
          [value]="vm.swimmerId()" (valueChange)="vm.swimmerId.set($event)"></app-select-field>

        <app-select-field
          [label]="'swimmerData.fields.category' | translate"
          [placeholder]="'swimmerData.placeholders.category' | translate"
          [options]="vm.categoryOptions()"
          [value]="vm.categoryId()" (valueChange)="vm.categoryId.set($event)"></app-select-field>

        <app-text-field
          [label]="'swimmerData.fields.fieldName' | translate"
          [placeholder]="'swimmerData.placeholders.fieldName' | translate"
          [value]="vm.fieldLabel()" (valueChange)="vm.fieldLabel.set($event)"></app-text-field>

        <app-text-field
          [label]="'swimmerData.fields.value' | translate"
          [placeholder]="'swimmerData.placeholders.value' | translate"
          [value]="vm.value()" (valueChange)="vm.value.set($event)"></app-text-field>

        <div class="flex items-end">
          <button type="submit" class="rounded-md bg-primary px-5 py-2.5 text-sm font-semibold text-white disabled:opacity-50"
            [disabled]="!vm.canSubmit() || vm.submitting()">
            {{ 'swimmerData.add.submit' | translate }}
          </button>
        </div>

        <p class="md:col-span-2 lg:col-span-4 text-xs text-muted">{{ 'swimmerData.add.helper' | translate }}</p>
      </form>
    </section>
  }
</div>
```

- [ ] **Step 7: Export page + viewmodel from the captain-panel barrel**

In `frontend/src/app/features/captain-panel/index.ts`, append:

```typescript
export { SwimmerDataPage } from './presentation/pages/swimmer-data/swimmer-data.page';
export { SwimmerDataViewModel } from './presentation/pages/swimmer-data/swimmer-data.viewmodel';
```

- [ ] **Step 8: Wire the route**

In `frontend/src/app/app.routes.ts`:

Add `SwimmerDataViewModel` to the captain-panel import:
```typescript
import { RegisterSwimmerViewModel, RegisterCoachViewModel, MedicalTestsViewModel, HealthMonitoringViewModel, SwimmerDataViewModel } from '@features/captain-panel';
```

Add the route object after the `captain-panel/health-monitoring` route:
```typescript
      {
        path: 'captain-panel/swimmer-data',
        canActivate: [firstLoginGuard, roleGuard('head_coach', 'captain')],
        loadComponent: () => import('@features/captain-panel').then((m) => m.SwimmerDataPage),
        providers: [SwimmerDataViewModel],
      },
```

- [ ] **Step 9: Point the Swimmer Records card at the route**

In `frontend/src/app/features/captain-panel/presentation/pages/captain-panel/captain-panel.page.ts`, give the `swimmerRecords` card a route:
```typescript
      { key: 'swimmerRecords', icon: LucideDatabase, route: '/captain-panel/swimmer-data' },
```

- [ ] **Step 10: Add English i18n**

In `frontend/src/app/core/i18n/en.json`, add a top-level `swimmerData` block:

```json
  "swimmerData": {
    "title": "Swimmer Data Fields",
    "description": "Record any extra swimmer data as a name and a value — allergies, surgeries, measurements. Medical test results live in their own section.",
    "add": {
      "title": "Add a data field",
      "helper": "Records are stamped with the current date and time and appear on the swimmer's detail page.",
      "submit": "Add Data"
    },
    "fields": {
      "swimmer": "Select Swimmer",
      "category": "Select Category",
      "fieldName": "Field Name (Key)",
      "value": "Value"
    },
    "placeholders": {
      "swimmer": "Choose a swimmer",
      "category": "Choose a category",
      "fieldName": "e.g. Penicillin",
      "value": "e.g. Severe"
    },
    "states": {
      "loading": "Loading…",
      "error": "Couldn't load swimmers or categories. Please try again."
    },
    "toasts": {
      "added": "Data field added",
      "addFailed": "Couldn't add the data field"
    }
  }
```

- [ ] **Step 11: Add Arabic i18n**

In `frontend/src/app/core/i18n/ar.json`, add the matching `swimmerData` block:

```json
  "swimmerData": {
    "title": "بيانات السباح الإضافية",
    "description": "سجّل أي بيان إضافي للسباح كاسم وقيمة — الحساسية، الجراحات، القياسات. نتائج الاختبارات الطبية لها قسم خاص.",
    "add": {
      "title": "إضافة بيان",
      "helper": "تُختم السجلات بالتاريخ والوقت الحاليين وتظهر في صفحة تفاصيل السباح.",
      "submit": "إضافة بيان"
    },
    "fields": {
      "swimmer": "اختر السبّاح",
      "category": "اختر الفئة",
      "fieldName": "اسم الحقل",
      "value": "القيمة"
    },
    "placeholders": {
      "swimmer": "اختر سبّاحًا",
      "category": "اختر فئة",
      "fieldName": "مثال: بنسلين",
      "value": "مثال: شديد"
    },
    "states": {
      "loading": "جارٍ التحميل…",
      "error": "تعذّر تحميل السبّاحين أو الفئات. حاول مرة أخرى."
    },
    "toasts": {
      "added": "تم إضافة البيان",
      "addFailed": "تعذّر إضافة البيان"
    }
  }
```

- [ ] **Step 12: Run the frontend suites + build**

Run (from `frontend/`):
```bash
npx jest src/app/features/observations src/app/features/reference src/app/features/captain-panel
npm run build
```
Expected: all specs pass; `ng build` succeeds (compiles the template + validates en/ar JSON).

- [ ] **Step 13: Manually verify (optional)**

Start backend + frontend, log in as Head Coach or Captain, open Captain Panel → Swimmer Records, pick a swimmer + category, enter a field name + value, and confirm the success toast + a new row in `health.observation`.

- [ ] **Step 14: Commit**

```bash
git add frontend/src/app/features/captain-panel/ frontend/src/app/app.routes.ts frontend/src/app/core/i18n/en.json frontend/src/app/core/i18n/ar.json
git commit -m "feat(observations): add Swimmer Data Fields page + route + i18n"
```

---

## Self-Review Notes (author)

- **Spec coverage:** §3.1 lookup → Task 1; §3.2 observation table + doc updates → Task 3 (Step 14); §4 reference backend → Tasks 1–2; §5 observation backend → Tasks 3–4; §6 frontend reference → Task 5, observations slice → Task 6, page/route/card/i18n → Task 7; §7 data flow → Task 7 viewmodel; §8 error handling → Task 4 (validation 400) + Task 7 (error toast); §9 testing → entity/repo/service (Identity + Health), validator, ReferenceController, frontend use-case/repo/viewmodel specs. §10 out-of-scope respected (no list endpoint, no detail tab). The `ObservationsController` unit test is intentionally omitted (Global Constraints, matches the health/medical controller precedent).
- **Type consistency:** frontend `CreateObservationDtoRq { swimmerId, categoryId, fieldLabel, value }` ↔ backend `CreateObservationRequest(SwimmerId, CategoryId, FieldLabel, Value)` (camelCase JSON). `ObservationDto`/`ObservationDtoRs` fields align. `IObservationService.CreateAsync` returns non-nullable `ObservationDto` (no cross-module existence check). Backend `IObservationRepository` (AddAsync/SaveChangesAsync) is distinct from the frontend `IObservationRepository` (`create`). `ReferenceService` constructor gains a 5th param — the existing `ReferenceServiceTests.NewService` helper is updated in Task 2 Step 1 to match, preventing a compile break.
- **Cross-module convention:** `observation.category_id` is a loose Guid (no EF FK), consistent with `swimmer_id`/`recorded_by`; the service performs no category-existence check (Task 4 service has no null path), matching the spec.
