# Swimmer Profile — Feedback Tab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the seventh Swimmer Profile tab — **Feedback** — a full-CRUD view over coach performance evaluations (`health.feedback_entry`), categorized by a new `reference.feedback_category` lookup, with author names resolved for display.

**Architecture:** Two new tables in their owning modules — `reference.feedback_category` (Identity, mirrors `ObservationCategory`) and `health.feedback_entry` (Health, mirrors `InBodyReading`). Nested REST routes `api/swimmers/{id}/feedback-entries` on the Health module (matching InBody). Categories are resolved client-side via `GET /api/reference/feedback-categories`. Author display names are resolved at read time in the API layer via a new Identity batch lookup — the Health module never joins Identity.

**Tech Stack:** .NET (ASP.NET Core, EF Core/Npgsql, FluentValidation, xUnit, Moq) backend; Angular (standalone components, signals, clean-architecture use-cases, Vitest) frontend.

**Spec:** `docs/superpowers/specs/2026-09-22-swimmer-profile-feedback-design.md`

## Global Constraints

- **HOLD ALL COMMITS.** The user has said nothing is committed until they explicitly authorize it. Every task ends with a **"Stage (do NOT commit)"** step: run `git add` for the listed files and stop. Do **not** run `git commit` until the user says so; then commit per-task, in order, using the messages given.
- **Backend build lock:** the running dev API locks build DLLs. Stop any running backend before `dotnet build` / `dotnet test` / `dotnet ef`. (See memory: "dotnet test dev-server lock".)
- **Loose Guids, no cross-module FKs:** `swimmer_id`, `author_id`, `category_id` are stored as plain `Guid`s with no database foreign keys (matches every existing tab).
- **Localization:** every user-facing message has `en` + `ar`. Backend messages use the `lang switch { "ar" => …, _ => … }` shape; frontend copy lives in `en.json` + `ar.json`.
- **Category codes (seed, verbatim):** `technique`, `endurance`, `attitude`, `punctuality`, `other`.
- **Auth:** reads `[Authorize]` (any authenticated); writes `[Authorize(Roles = "head_coach,captain")]`. `author_id = CurrentUserId()` at create; `entry_date` server-set to `DateTime.UtcNow` date; both immutable on edit.
- **Migrations are created but NOT applied here.** Generating the migration files is in scope; running `database update` against any DB is a separate, user-driven step (see memory on Aiven/local DBs).

---

## File Structure

**Backend — Identity module (`reference.feedback_category` + author names)**
- Create `…/Identity.Domain/Entities/FeedbackCategory.cs`
- Create `…/Identity.Domain/Repositories/IFeedbackCategoryRepository.cs`
- Create `…/Identity.Infrastructure/Configurations/FeedbackCategoryConfiguration.cs`
- Create `…/Identity.Infrastructure/Repositories/FeedbackCategoryRepository.cs`
- Modify `…/Identity.Infrastructure/Data/IdentityDbContext.cs` (add `DbSet`)
- Modify `…/Identity.Infrastructure/Data/IdentitySeeder.cs` (seed 5 rows)
- Create `…/Identity.Infrastructure/Migrations/<stamp>_CreateFeedbackCategoryTable.cs` (via `dotnet ef`)
- Modify `…/Identity.Application/Services/Interfaces/IReferenceService.cs`, `…/Services/ReferenceService.cs`, `…/Resources/ReferenceMessages.cs`
- Modify `…/Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs` (register repo)
- Modify `backend/Kheprx.BaseBackend.Api/Controllers/ReferenceController.cs` (new endpoint)
- Modify `…/Identity.Application/DTOs/UserDtos.cs` (add `UserNameDto`)
- Modify `…/Identity.Domain/Repositories/IUserRepository.cs`, `…/Infrastructure/Repositories/UserRepository.cs` (add `GetByIdsAsync`)
- Modify `…/Identity.Application/Services/Interfaces/IUserService.cs`, `…/Services/UserService.cs` (add `GetDisplayNamesAsync`)

**Backend — Health module (`health.feedback_entry`)**
- Create `…/Health.Domain/Entities/FeedbackEntry.cs`
- Create `…/Health.Domain/Repositories/IFeedbackEntryRepository.cs`
- Create `…/Health.Infrastructure/Configurations/FeedbackEntryConfiguration.cs`
- Create `…/Health.Infrastructure/Repositories/FeedbackEntryRepository.cs`
- Modify `…/Health.Infrastructure/Data/HealthDbContext.cs` (add `DbSet`)
- Create `…/Health.Infrastructure/Migrations/<stamp>_CreateFeedbackEntryTable.cs` (via `dotnet ef`)
- Create `…/Health.Application/DTOs/FeedbackEntryDtos.cs`
- Create `…/Health.Application/Resources/FeedbackMessages.cs`
- Create `…/Health.Application/Services/Interfaces/IFeedbackService.cs`, `…/Services/FeedbackService.cs`
- Create `…/Health.Application/Validators/CreateFeedbackEntryRequestValidator.cs`
- Modify `…/Health.Infrastructure/Extensions/HealthModuleExtensions.cs` (register repo + service)
- Create `backend/Kheprx.BaseBackend.Api/Controllers/FeedbackEntriesController.cs`

**Frontend**
- Modify `…/features/reference/**` — repo interface + impl + `LoadFeedbackCategoriesUseCase`
- Create `…/features/swimmer-profile/data/dto/feedback-entry.dto.ts`, `…/dto/feedback-entry.mapper.ts`
- Create `…/features/swimmer-profile/domain/model/feedback-entry.ts`
- Create `…/features/swimmer-profile/domain/usecases/{list,create,update,delete}-feedback-entry.use-case.ts`
- Modify `…/domain/repositories/swimmer-profile.repository.ts`, `…/data/repositories/swimmer-profile.repository.impl.ts`
- Modify `…/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.ts`, `…/swimmer-profile.page.ts`, `…/swimmer-profile.page.html`
- Modify `frontend/src/app/core/i18n/en.json`, `…/ar.json`

---

## Backend — Identity: `reference.feedback_category`

### Task 1: `FeedbackCategory` domain entity

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/FeedbackCategory.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/FeedbackCategoryTests.cs`

**Interfaces:**
- Produces: `FeedbackCategory(string code, string nameEn, string? nameAr = null)` with `Id`, `Code`, `NameEn`, `NameAr` (private setters). Mirrors `ObservationCategory`.

- [ ] **Step 1: Write the failing test**

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class FeedbackCategoryTests
{
    [Fact]
    public void Ctor_trims_fields_and_assigns_id()
    {
        var c = new FeedbackCategory(" technique ", " Technique ", " تكنيك ");
        Assert.NotEqual(Guid.Empty, c.Id);
        Assert.Equal("technique", c.Code);
        Assert.Equal("Technique", c.NameEn);
        Assert.Equal("تكنيك", c.NameAr);
    }

    [Fact]
    public void Ctor_normalizes_blank_arabic_to_null()
    {
        var c = new FeedbackCategory("other", "Other", "   ");
        Assert.Null(c.NameAr);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FeedbackCategoryTests`
Expected: FAIL — `FeedbackCategory` does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class FeedbackCategory
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }

    private FeedbackCategory() { } // EF Core

    public FeedbackCategory(string code, string nameEn, string? nameAr = null)
    {
        Id = Guid.NewGuid();
        Code = code.Trim();
        NameEn = nameEn.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FeedbackCategoryTests`
Expected: PASS.

- [ ] **Step 5: Stage (do NOT commit)**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/FeedbackCategory.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/FeedbackCategoryTests.cs
```
Held commit message: `feat(identity): add FeedbackCategory reference entity`

---

### Task 2: FeedbackCategory configuration, repository, DbSet, DI

**Files:**
- Create: `…/Identity.Domain/Repositories/IFeedbackCategoryRepository.cs`
- Create: `…/Identity.Infrastructure/Configurations/FeedbackCategoryConfiguration.cs`
- Create: `…/Identity.Infrastructure/Repositories/FeedbackCategoryRepository.cs`
- Modify: `…/Identity.Infrastructure/Data/IdentityDbContext.cs`
- Modify: `…/Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/FeedbackCategoryRepositoryTests.cs`

**Interfaces:**
- Consumes: `FeedbackCategory` (Task 1).
- Produces: `IFeedbackCategoryRepository.GetAllAsync(CancellationToken) → IReadOnlyList<FeedbackCategory>`; `IdentityDbContext.FeedbackCategories`.

- [ ] **Step 1: Write the failing test**

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Kheprx.BaseBackend.Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Repositories;

public class FeedbackCategoryRepositoryTests
{
    private static IdentityDbContext NewDb()
        => new(new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase($"id-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task GetAllAsync_returns_rows_ordered_by_code()
    {
        await using var db = NewDb();
        db.FeedbackCategories.Add(new FeedbackCategory("technique", "Technique", "تكنيك"));
        db.FeedbackCategories.Add(new FeedbackCategory("attitude", "Attitude", "سلوك"));
        await db.SaveChangesAsync();

        var repo = new FeedbackCategoryRepository(db);
        var all = await repo.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Equal("attitude", all[0].Code); // alphabetical
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FeedbackCategoryRepositoryTests`
Expected: FAIL — repository/DbSet do not exist.

- [ ] **Step 3: Write minimal implementation**

`IFeedbackCategoryRepository.cs`:
```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
namespace Kheprx.BaseBackend.Identity.Domain.Repositories;
public interface IFeedbackCategoryRepository
{
    Task<IReadOnlyList<FeedbackCategory>> GetAllAsync(CancellationToken ct = default);
}
```

`FeedbackCategoryConfiguration.cs`:
```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class FeedbackCategoryConfiguration : IEntityTypeConfiguration<FeedbackCategory>
{
    public void Configure(EntityTypeBuilder<FeedbackCategory> builder)
    {
        builder.ToTable("feedback_category", "reference");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(c => c.Code).IsUnique();
        builder.Property(c => c.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(c => c.NameAr).HasMaxLength(100);
    }
}
```

`FeedbackCategoryRepository.cs`:
```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class FeedbackCategoryRepository : IFeedbackCategoryRepository
{
    private readonly IdentityDbContext _db;
    public FeedbackCategoryRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<FeedbackCategory>> GetAllAsync(CancellationToken ct = default)
        => await _db.FeedbackCategories.AsNoTracking().OrderBy(c => c.Code).ToListAsync(ct);
}
```

In `IdentityDbContext.cs`, add next to `ObservationCategories`:
```csharp
    public DbSet<FeedbackCategory> FeedbackCategories => Set<FeedbackCategory>();
```

In `IdentityModuleExtensions.cs`, add next to the `IObservationCategoryRepository` registration:
```csharp
        services.AddScoped<IFeedbackCategoryRepository, FeedbackCategoryRepository>();
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FeedbackCategoryRepositoryTests`
Expected: PASS.

- [ ] **Step 5: Stage (do NOT commit)**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/IFeedbackCategoryRepository.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/FeedbackCategoryConfiguration.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/FeedbackCategoryRepository.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentityDbContext.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/FeedbackCategoryRepositoryTests.cs
```
Held commit message: `feat(identity): feedback_category config, repository, DI`

---

### Task 3: Migration + seed for `reference.feedback_category`

**Files:**
- Create: `…/Identity.Infrastructure/Migrations/<stamp>_CreateFeedbackCategoryTable.cs` (+ `.Designer.cs`, snapshot update — all generated by `dotnet ef`)
- Modify: `…/Identity.Infrastructure/Data/IdentitySeeder.cs`

**Interfaces:**
- Consumes: Task 2 entity/config.
- Produces: physical table `reference.feedback_category`; seeded rows for the five category codes.

- [ ] **Step 1: Add the seed method + call**

In `IdentitySeeder.cs`, add the call inside `SeedAsync` next to `await EnsureObservationCategories(db, ct);`:
```csharp
        await EnsureFeedbackCategories(db, ct);
```

Add the method (place beside `EnsureObservationCategories`):
```csharp
    private static async Task EnsureFeedbackCategories(IdentityDbContext db, CancellationToken ct)
    {
        var rows = new (string Code, string En, string Ar)[]
        {
            ("technique", "Technique", "الأداء الفني"),
            ("endurance", "Endurance", "التحمل"),
            ("attitude", "Attitude", "السلوك"),
            ("punctuality", "Punctuality", "الالتزام بالمواعيد"),
            ("other", "Other", "أخرى"),
        };
        foreach (var (code, en, ar) in rows)
            if (!await db.FeedbackCategories.AnyAsync(c => c.Code == code, ct))
                await db.FeedbackCategories.AddAsync(new FeedbackCategory(code, en, ar), ct);
    }
```

- [ ] **Step 2: Generate the migration**

Stop the running backend first (DLL lock). Then run (design-time factory targets local `basebackend` — that's expected; we only generate the file here, we do not apply it):
```bash
dotnet ef migrations add CreateFeedbackCategoryTable \
  --project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --context IdentityDbContext
```
Expected: a new `…_CreateFeedbackCategoryTable.cs` whose `Up` calls `CreateTable("feedback_category", schema: "reference", …)` with a unique index on `Code` — structurally identical to `CreateObservationCategoryTable`. If the generated `Up` differs materially from that shape, stop and investigate the model before proceeding.

- [ ] **Step 3: Build to verify the migration compiles**

Run: `dotnet build backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure`
Expected: build succeeds.

- [ ] **Step 4: Stage (do NOT commit)**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Migrations/ \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentitySeeder.cs
```
Held commit message: `feat(identity): migration + seed for feedback_category`

---

### Task 4: Reference service + `GET /api/reference/feedback-categories`

**Files:**
- Modify: `…/Identity.Application/Services/Interfaces/IReferenceService.cs`
- Modify: `…/Identity.Application/Services/ReferenceService.cs`
- Modify: `…/Identity.Application/Resources/ReferenceMessages.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/ReferenceController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/ReferenceControllerTests.cs` (add a case)

**Interfaces:**
- Consumes: `IFeedbackCategoryRepository` (Task 2), `CodedLookupDto` (existing).
- Produces: `IReferenceService.GetFeedbackCategoriesAsync(CancellationToken) → IReadOnlyList<CodedLookupDto>`; `GET /api/reference/feedback-categories`.

- [ ] **Step 1: Write the failing test** (append to `ReferenceControllerTests`)

```csharp
    [Fact]
    public async Task FeedbackCategories_returns_200_with_coded_lookups()
    {
        var svc = new Mock<IReferenceService>();
        svc.Setup(s => s.GetFeedbackCategoriesAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<CodedLookupDto> { new(Guid.NewGuid(), "technique", "Technique", "الأداء الفني") });

        var result = await new ReferenceController(svc.Object).FeedbackCategories(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<CodedLookupDto>>>(ok.Value);
        Assert.Equal("technique", body.Data![0].Code);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter ReferenceControllerTests`
Expected: FAIL — `GetFeedbackCategoriesAsync` / `FeedbackCategories` do not exist.

- [ ] **Step 3: Write minimal implementation**

`IReferenceService.cs` — add:
```csharp
    Task<IReadOnlyList<CodedLookupDto>> GetFeedbackCategoriesAsync(CancellationToken ct = default);
```

`ReferenceService.cs` — add the field, constructor param, and method (mirror observation categories):
```csharp
    private readonly IFeedbackCategoryRepository _feedbackCategories;
```
Add `IFeedbackCategoryRepository feedbackCategories` to the constructor signature and `_feedbackCategories = feedbackCategories;` in the body, then:
```csharp
    public async Task<IReadOnlyList<CodedLookupDto>> GetFeedbackCategoriesAsync(CancellationToken ct = default)
        => (await _feedbackCategories.GetAllAsync(ct)).Select(c => new CodedLookupDto(c.Id, c.Code, c.NameEn, c.NameAr)).ToList();
```

`ReferenceMessages.cs` — add to `Success`:
```csharp
        public static string FeedbackCategoriesListed(string lang) => lang switch { "ar" => "فئات التقييم", _ => "Feedback categories" };
```

`ReferenceController.cs` — add the action (after `ObservationCategories`):
```csharp
    /// <summary>Lists all feedback categories (coach evaluation categories).</summary>
    [HttpGet("feedback-categories")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CodedLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CodedLookupDto>>>> FeedbackCategories(CancellationToken ct)
    {
        var data = await _service.GetFeedbackCategoriesAsync(ct);
        var body = ApiResponse<IReadOnlyList<CodedLookupDto>>.Success(
            ReferenceMessages.Success.FeedbackCategoriesListed(AppLanguage.Current), data);
        return Ok(body);
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter ReferenceControllerTests`
Expected: PASS.

- [ ] **Step 5: Stage (do NOT commit)**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/IReferenceService.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/ReferenceService.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Resources/ReferenceMessages.cs \
        backend/Kheprx.BaseBackend.Api/Controllers/ReferenceController.cs \
        backend/tests/Kheprx.BaseBackend.Api.UnitTests/ReferenceControllerTests.cs
```
Held commit message: `feat(identity): expose GET /api/reference/feedback-categories`

---

### Task 5: Identity author-name batch lookup

**Files:**
- Modify: `…/Identity.Application/DTOs/UserDtos.cs` (add `UserNameDto`)
- Modify: `…/Identity.Domain/Repositories/IUserRepository.cs`, `…/Infrastructure/Repositories/UserRepository.cs`
- Modify: `…/Identity.Application/Services/Interfaces/IUserService.cs`, `…/Services/UserService.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/UserServiceDisplayNamesTests.cs`

**Interfaces:**
- Produces:
  - `UserNameDto(Guid Id, string NameEn, string? NameAr)`
  - `IUserRepository.GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken) → IReadOnlyList<AppUser>`
  - `IUserService.GetDisplayNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken) → IReadOnlyDictionary<Guid, UserNameDto>`
- Consumed by: `FeedbackEntriesController` (Task 11).

- [ ] **Step 1: Write the failing test**

```csharp
using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Kheprx.BaseBackend.Identity.Application.Services;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Services;

public class UserServiceDisplayNamesTests
{
    [Fact]
    public async Task GetDisplayNamesAsync_maps_resolved_users_and_skips_unknown_ids()
    {
        var users = new Mock<IUserRepository>();
        var roleId = Guid.NewGuid();
        var coach = new AppUser("coach.omar", "Coach Omar", roleId, nameAr: "الكابتن عمر");
        users.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new[] { coach });
        var svc = new UserService(users.Object, Mock.Of<IRoleRepository>(), Mock.Of<IPasswordHasher>());

        var map = await svc.GetDisplayNamesAsync(new[] { coach.Id, Guid.NewGuid() });

        Assert.Single(map);
        Assert.Equal("Coach Omar", map[coach.Id].NameEn);
        Assert.Equal("الكابتن عمر", map[coach.Id].NameAr);
    }

    [Fact]
    public async Task GetDisplayNamesAsync_returns_empty_for_no_ids()
    {
        var svc = new UserService(Mock.Of<IUserRepository>(), Mock.Of<IRoleRepository>(), Mock.Of<IPasswordHasher>());
        Assert.Empty(await svc.GetDisplayNamesAsync(Array.Empty<Guid>()));
    }
}
```
(Confirm the `AppUser` constructor argument order against `IdentitySeeder.EnsureHeadCoach`; adjust the `new AppUser(...)` call if the real signature needs `email`/`passwordHash` — pass named args matching the seeder.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter UserServiceDisplayNamesTests`
Expected: FAIL — members do not exist.

- [ ] **Step 3: Write minimal implementation**

`UserDtos.cs` — add:
```csharp
/// <summary>A user's display names, for resolving author/actor ids to names.</summary>
public sealed record UserNameDto(Guid Id, string NameEn, string? NameAr);
```

`IUserRepository.cs` — add:
```csharp
    Task<IReadOnlyList<AppUser>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
```

`UserRepository.cs` — add:
```csharp
    // Read-only users whose id is in the given set (author/actor name resolution).
    public async Task<IReadOnlyList<AppUser>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids is null || ids.Count == 0) return Array.Empty<AppUser>();
        var idSet = ids.Distinct().ToList();
        return await _db.Users.AsNoTracking().Where(u => idSet.Contains(u.Id)).ToListAsync(ct);
    }
```

`IUserService.cs` — add:
```csharp
    Task<IReadOnlyDictionary<Guid, UserNameDto>> GetDisplayNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
```

`UserService.cs` — add (inside the class):
```csharp
    public async Task<IReadOnlyDictionary<Guid, UserNameDto>> GetDisplayNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids is null || ids.Count == 0) return new Dictionary<Guid, UserNameDto>();
        var users = await _users.GetByIdsAsync(ids, ct);
        return users.ToDictionary(u => u.Id, u => new UserNameDto(u.Id, u.NameEn, u.NameAr));
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter UserServiceDisplayNamesTests`
Expected: PASS.

- [ ] **Step 5: Stage (do NOT commit)**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/UserDtos.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/IUserRepository.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/UserRepository.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/IUserService.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/UserService.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/UserServiceDisplayNamesTests.cs
```
Held commit message: `feat(identity): batch user display-name lookup for author resolution`

---

## Backend — Health: `health.feedback_entry`

### Task 6: `FeedbackEntry` domain entity

**Files:**
- Create: `…/Health.Domain/Entities/FeedbackEntry.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Entities/FeedbackEntryTests.cs`

**Interfaces:**
- Produces:
  - `FeedbackEntry(Guid swimmerId, short rating, Guid categoryId, string comment, Guid authorId)` — sets `Id`, `EntryDate = DateOnly.FromDateTime(DateTime.UtcNow)`, others from args.
  - `Update(short rating, Guid categoryId, string comment)` — mutates only those three; `AuthorId`, `EntryDate`, `SwimmerId` preserved.
  - Public getters: `Id, SwimmerId, Rating, CategoryId, Comment, AuthorId, EntryDate`.

- [ ] **Step 1: Write the failing test**

```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Entities;

public class FeedbackEntryTests
{
    [Fact]
    public void Ctor_assigns_fields_author_and_todays_entry_date()
    {
        var swimmerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var e = new FeedbackEntry(swimmerId, 5, categoryId, "Great rhythm", authorId);

        Assert.NotEqual(Guid.Empty, e.Id);
        Assert.Equal(swimmerId, e.SwimmerId);
        Assert.Equal((short)5, e.Rating);
        Assert.Equal(categoryId, e.CategoryId);
        Assert.Equal("Great rhythm", e.Comment);
        Assert.Equal(authorId, e.AuthorId);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), e.EntryDate);
    }

    [Fact]
    public void Update_mutates_rating_category_comment_preserves_author_swimmer_and_date()
    {
        var swimmerId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var e = new FeedbackEntry(swimmerId, 3, Guid.NewGuid(), "ok", authorId);
        var date = e.EntryDate;
        var newCategory = Guid.NewGuid();

        e.Update(4, newCategory, "Improved a lot");

        Assert.Equal((short)4, e.Rating);
        Assert.Equal(newCategory, e.CategoryId);
        Assert.Equal("Improved a lot", e.Comment);
        Assert.Equal(authorId, e.AuthorId);      // preserved
        Assert.Equal(swimmerId, e.SwimmerId);    // preserved
        Assert.Equal(date, e.EntryDate);         // preserved
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FeedbackEntryTests`
Expected: FAIL — `FeedbackEntry` does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace Kheprx.BaseBackend.Health.Domain.Entities;

public sealed class FeedbackEntry
{
    public Guid Id { get; private set; }
    public Guid SwimmerId { get; private set; }
    public short Rating { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Comment { get; private set; } = string.Empty;
    public Guid AuthorId { get; private set; }
    public DateOnly EntryDate { get; private set; }

    private FeedbackEntry() { } // EF Core

    public FeedbackEntry(Guid swimmerId, short rating, Guid categoryId, string comment, Guid authorId)
    {
        Id = Guid.NewGuid();
        SwimmerId = swimmerId;
        Rating = rating;
        CategoryId = categoryId;
        Comment = comment;
        AuthorId = authorId;
        EntryDate = DateOnly.FromDateTime(DateTime.UtcNow);
    }

    public void Update(short rating, Guid categoryId, string comment)
    {
        Rating = rating;
        CategoryId = categoryId;
        Comment = comment;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FeedbackEntryTests`
Expected: PASS.

- [ ] **Step 5: Stage (do NOT commit)**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Entities/FeedbackEntry.cs \
        backend/tests/Kheprx.BaseBackend.Health.UnitTests/Entities/FeedbackEntryTests.cs
```
Held commit message: `feat(health): add FeedbackEntry entity`

---

### Task 7: FeedbackEntry configuration, repository, DbSet, DI

**Files:**
- Create: `…/Health.Domain/Repositories/IFeedbackEntryRepository.cs`
- Create: `…/Health.Infrastructure/Configurations/FeedbackEntryConfiguration.cs`
- Create: `…/Health.Infrastructure/Repositories/FeedbackEntryRepository.cs`
- Modify: `…/Health.Infrastructure/Data/HealthDbContext.cs`
- Modify: `…/Health.Infrastructure/Extensions/HealthModuleExtensions.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Repositories/FeedbackEntryRepositoryTests.cs`

**Interfaces:**
- Consumes: `FeedbackEntry` (Task 6).
- Produces:
  - `IFeedbackEntryRepository`: `ListBySwimmerAsync(Guid, CancellationToken) → IReadOnlyList<FeedbackEntry>`; `AddAsync(FeedbackEntry, CancellationToken)`; `GetTrackedAsync(Guid, CancellationToken) → FeedbackEntry?`; `void Remove(FeedbackEntry)`; `SaveChangesAsync(CancellationToken)`.
  - `HealthDbContext.FeedbackEntries`.

- [ ] **Step 1: Write the failing test**

```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Kheprx.BaseBackend.Health.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Repositories;

public class FeedbackEntryRepositoryTests
{
    private static HealthDbContext NewDb()
        => new(new DbContextOptionsBuilder<HealthDbContext>().UseInMemoryDatabase($"health-{Guid.NewGuid()}").Options);

    private static FeedbackEntry Entry(Guid swimmerId)
        => new(swimmerId, 4, Guid.NewGuid(), "note", Guid.NewGuid());

    [Fact]
    public async Task ListBySwimmerAsync_returns_only_that_swimmer()
    {
        await using var db = NewDb();
        var repo = new FeedbackEntryRepository(db);
        var sw = Guid.NewGuid();
        await repo.AddAsync(Entry(sw));
        await repo.AddAsync(Entry(sw));
        await repo.AddAsync(Entry(Guid.NewGuid())); // other swimmer
        await repo.SaveChangesAsync();

        var list = await repo.ListBySwimmerAsync(sw);

        Assert.Equal(2, list.Count);
        Assert.All(list, e => Assert.Equal(sw, e.SwimmerId));
    }

    [Fact]
    public async Task GetTrackedAsync_then_Remove_deletes()
    {
        await using var db = NewDb();
        var repo = new FeedbackEntryRepository(db);
        var e = Entry(Guid.NewGuid());
        await repo.AddAsync(e);
        await repo.SaveChangesAsync();

        var tracked = await repo.GetTrackedAsync(e.Id);
        Assert.NotNull(tracked);
        repo.Remove(tracked!);
        await repo.SaveChangesAsync();

        Assert.Null(await repo.GetTrackedAsync(e.Id));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FeedbackEntryRepositoryTests`
Expected: FAIL — repository/DbSet do not exist.

- [ ] **Step 3: Write minimal implementation**

`IFeedbackEntryRepository.cs`:
```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;

namespace Kheprx.BaseBackend.Health.Domain.Repositories;

public interface IFeedbackEntryRepository
{
    Task<IReadOnlyList<FeedbackEntry>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default);
    Task AddAsync(FeedbackEntry entry, CancellationToken ct = default);
    Task<FeedbackEntry?> GetTrackedAsync(Guid entryId, CancellationToken ct = default);
    void Remove(FeedbackEntry entry);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

`FeedbackEntryConfiguration.cs`:
```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Health.Infrastructure.Configurations;

internal sealed class FeedbackEntryConfiguration : IEntityTypeConfiguration<FeedbackEntry>
{
    public void Configure(EntityTypeBuilder<FeedbackEntry> builder)
    {
        builder.ToTable("feedback_entry", "health");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.SwimmerId).IsRequired();   // loose Guid — no cross-module FK
        builder.Property(e => e.Rating).IsRequired();       // smallint
        builder.Property(e => e.CategoryId).IsRequired();   // loose Guid → reference.feedback_category
        builder.Property(e => e.Comment).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.AuthorId).IsRequired();     // loose Guid — no cross-module FK
        builder.Property(e => e.EntryDate).IsRequired();    // DateOnly → date
        builder.HasIndex(e => new { e.SwimmerId, e.EntryDate });
    }
}
```

`FeedbackEntryRepository.cs` (list newest-first, tiebreak by Id for a stable order):
```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Health.Infrastructure.Repositories;

internal sealed class FeedbackEntryRepository : IFeedbackEntryRepository
{
    private readonly HealthDbContext _db;
    public FeedbackEntryRepository(HealthDbContext db) => _db = db;

    public async Task<IReadOnlyList<FeedbackEntry>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default)
        => await _db.FeedbackEntries.AsNoTracking()
              .Where(e => e.SwimmerId == swimmerId)
              .OrderByDescending(e => e.EntryDate).ThenByDescending(e => e.Id)
              .ToListAsync(ct);

    public async Task AddAsync(FeedbackEntry entry, CancellationToken ct = default)
        => await _db.FeedbackEntries.AddAsync(entry, ct);

    public Task<FeedbackEntry?> GetTrackedAsync(Guid entryId, CancellationToken ct = default)
        => _db.FeedbackEntries.FirstOrDefaultAsync(e => e.Id == entryId, ct);

    public void Remove(FeedbackEntry entry) => _db.FeedbackEntries.Remove(entry);

    public async Task SaveChangesAsync(CancellationToken ct = default) => await _db.SaveChangesAsync(ct);
}
```

In `HealthDbContext.cs`, add next to `InBodyReadings`:
```csharp
    public DbSet<FeedbackEntry> FeedbackEntries => Set<FeedbackEntry>();
```

In `HealthModuleExtensions.cs`, add next to the InBody registrations:
```csharp
        services.AddScoped<IFeedbackEntryRepository, FeedbackEntryRepository>();
        services.AddScoped<IFeedbackService, FeedbackService>();
```
(`IFeedbackService`/`FeedbackService` land in Task 9; add both registration lines now — the file won't compile until Task 9, which is fine because Tasks 7→9 build together. If you run a build between tasks, add only the repository line here and the service line in Task 9.)

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FeedbackEntryRepositoryTests`
Expected: PASS. (If you added the `IFeedbackService` registration early and the assembly won't compile, temporarily comment that one line, run the test, then restore it in Task 9.)

- [ ] **Step 5: Stage (do NOT commit)**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Repositories/IFeedbackEntryRepository.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Configurations/FeedbackEntryConfiguration.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Repositories/FeedbackEntryRepository.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Data/HealthDbContext.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Extensions/HealthModuleExtensions.cs \
        backend/tests/Kheprx.BaseBackend.Health.UnitTests/Repositories/FeedbackEntryRepositoryTests.cs
```
Held commit message: `feat(health): feedback_entry config, repository, DI`

---

### Task 8: Migration for `health.feedback_entry`

**Files:**
- Create: `…/Health.Infrastructure/Migrations/<stamp>_CreateFeedbackEntryTable.cs` (+ Designer + snapshot — generated by `dotnet ef`)

**Interfaces:**
- Consumes: Task 7 entity/config.
- Produces: physical table `health.feedback_entry`.

- [ ] **Step 1: Generate the migration**

Stop the running backend (DLL lock). Then:
```bash
dotnet ef migrations add CreateFeedbackEntryTable \
  --project backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure \
  --context HealthDbContext
```
Expected: `Up` calls `CreateTable("feedback_entry", schema: "health", …)` with columns `Id (uuid)`, `SwimmerId (uuid)`, `Rating (smallint)`, `CategoryId (uuid)`, `Comment (varchar(1000))`, `AuthorId (uuid)`, `EntryDate (date)`, PK on `Id`, and index `IX_feedback_entry_SwimmerId_EntryDate`. If it differs materially, stop and check the configuration.

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure`
Expected: build succeeds.

- [ ] **Step 3: Stage (do NOT commit)**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Migrations/
```
Held commit message: `feat(health): migration for feedback_entry`

---

### Task 9: Feedback DTOs, messages, service

**Files:**
- Create: `…/Health.Application/DTOs/FeedbackEntryDtos.cs`
- Create: `…/Health.Application/Resources/FeedbackMessages.cs`
- Create: `…/Health.Application/Services/Interfaces/IFeedbackService.cs`
- Create: `…/Health.Application/Services/FeedbackService.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Services/FeedbackServiceTests.cs`

**Interfaces:**
- Consumes: `IFeedbackEntryRepository` (Task 7).
- Produces:
  - `CreateFeedbackEntryRequest(short Rating, Guid CategoryId, string Comment)`
  - `FeedbackEntryDto(Guid Id, Guid SwimmerId, short Rating, Guid CategoryId, string Comment, Guid AuthorId, string AuthorNameEn, string? AuthorNameAr, DateOnly EntryDate)` — `AuthorName*` empty from the service; filled by the controller (Task 11).
  - `IFeedbackService`: `ListAsync(Guid swimmerId, …) → IReadOnlyList<FeedbackEntryDto>`; `CreateAsync(Guid swimmerId, CreateFeedbackEntryRequest, Guid authorId, …) → FeedbackEntryDto`; `UpdateAsync(Guid swimmerId, Guid entryId, CreateFeedbackEntryRequest, …) → FeedbackEntryDto?`; `DeleteAsync(Guid swimmerId, Guid entryId, …) → bool`.

- [ ] **Step 1: Write the failing test**

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Services;

public class FeedbackServiceTests
{
    private static (FeedbackService svc, Mock<IFeedbackEntryRepository> repo) Build()
    {
        var repo = new Mock<IFeedbackEntryRepository>();
        return (new FeedbackService(repo.Object), repo);
    }

    private static CreateFeedbackEntryRequest Req() => new(5, Guid.NewGuid(), "Great rhythm");

    [Fact]
    public async Task Create_stamps_author_saves_and_returns_dto()
    {
        var (svc, repo) = Build();
        FeedbackEntry? added = null;
        repo.Setup(r => r.AddAsync(It.IsAny<FeedbackEntry>(), It.IsAny<CancellationToken>()))
            .Callback<FeedbackEntry, CancellationToken>((e, _) => added = e).Returns(Task.CompletedTask);
        var swimmerId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var dto = await svc.CreateAsync(swimmerId, Req(), authorId);

        Assert.NotNull(added);
        Assert.Equal(swimmerId, added!.SwimmerId);
        Assert.Equal(authorId, added.AuthorId);
        Assert.Equal(swimmerId, dto.SwimmerId);
        Assert.Equal((short)5, dto.Rating);
        Assert.Equal(string.Empty, dto.AuthorNameEn); // enriched later by the controller
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_applies_when_owned_and_null_when_foreign_or_missing()
    {
        var (svc, repo) = Build();
        var sw = Guid.NewGuid();
        var owned = new FeedbackEntry(sw, 3, Guid.NewGuid(), "ok", Guid.NewGuid());
        repo.Setup(r => r.GetTrackedAsync(owned.Id, It.IsAny<CancellationToken>())).ReturnsAsync(owned);
        var foreign = new FeedbackEntry(Guid.NewGuid(), 3, Guid.NewGuid(), "x", Guid.NewGuid());
        repo.Setup(r => r.GetTrackedAsync(foreign.Id, It.IsAny<CancellationToken>())).ReturnsAsync(foreign);

        var ok = await svc.UpdateAsync(sw, owned.Id, Req());
        var foreignResult = await svc.UpdateAsync(sw, foreign.Id, Req());
        var missing = await svc.UpdateAsync(sw, Guid.NewGuid(), Req());

        Assert.NotNull(ok);
        Assert.Equal((short)5, owned.Rating);   // updated in place
        Assert.Null(foreignResult);              // ownership guard
        Assert.Null(missing);
    }

    [Fact]
    public async Task Delete_true_when_owned_false_when_foreign_or_missing()
    {
        var (svc, repo) = Build();
        var sw = Guid.NewGuid();
        var owned = new FeedbackEntry(sw, 3, Guid.NewGuid(), "ok", Guid.NewGuid());
        repo.Setup(r => r.GetTrackedAsync(owned.Id, It.IsAny<CancellationToken>())).ReturnsAsync(owned);

        Assert.True(await svc.DeleteAsync(sw, owned.Id));
        repo.Verify(r => r.Remove(owned), Times.Once);
        Assert.False(await svc.DeleteAsync(sw, Guid.NewGuid()));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FeedbackServiceTests`
Expected: FAIL — DTOs/service do not exist.

- [ ] **Step 3: Write minimal implementation**

`FeedbackEntryDtos.cs`:
```csharp
namespace Kheprx.BaseBackend.Health.Application.DTOs;

/// <summary>Payload to create/update a feedback entry (author + date set server-side).</summary>
public sealed record CreateFeedbackEntryRequest(short Rating, Guid CategoryId, string Comment);

/// <summary>A coach feedback entry. AuthorName* are resolved in the API layer (empty from the service).</summary>
public sealed record FeedbackEntryDto(
    Guid Id,
    Guid SwimmerId,
    short Rating,
    Guid CategoryId,
    string Comment,
    Guid AuthorId,
    string AuthorNameEn,
    string? AuthorNameAr,
    DateOnly EntryDate);
```

`FeedbackMessages.cs`:
```csharp
namespace Kheprx.BaseBackend.Health.Application.Resources;

/// <summary>Localized messages for coach feedback entries.</summary>
public static class FeedbackMessages
{
    public static class Success
    {
        public static string Listed(string lang) => lang switch { "ar" => "التقييمات", _ => "Feedback" };
        public static string Created(string lang) => lang switch { "ar" => "تم حفظ التقييم", _ => "Feedback saved" };
        public static string Updated(string lang) => lang switch { "ar" => "تم تحديث التقييم", _ => "Feedback updated" };
        public static string Deleted(string lang) => lang switch { "ar" => "تم حذف التقييم", _ => "Feedback deleted" };
    }

    public static class Errors
    {
        public static string NotFound(string lang) => lang switch { "ar" => "التقييم غير موجود", _ => "Feedback not found" };
        public static string RatingInvalid(string lang) => lang switch { "ar" => "التقييم يجب أن يكون بين 1 و5", _ => "Rating must be between 1 and 5" };
        public static string CommentRequired(string lang) => lang switch { "ar" => "التعليق مطلوب", _ => "Comment is required" };
        public static string CategoryRequired(string lang) => lang switch { "ar" => "الفئة مطلوبة", _ => "Category is required" };
    }
}
```

`IFeedbackService.cs`:
```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;

namespace Kheprx.BaseBackend.Health.Application.Services.Interfaces;

public interface IFeedbackService
{
    Task<IReadOnlyList<FeedbackEntryDto>> ListAsync(Guid swimmerId, CancellationToken ct = default);
    Task<FeedbackEntryDto> CreateAsync(Guid swimmerId, CreateFeedbackEntryRequest request, Guid authorId, CancellationToken ct = default);
    Task<FeedbackEntryDto?> UpdateAsync(Guid swimmerId, Guid entryId, CreateFeedbackEntryRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid swimmerId, Guid entryId, CancellationToken ct = default);
}
```

`FeedbackService.cs`:
```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;

namespace Kheprx.BaseBackend.Health.Application.Services;

internal sealed class FeedbackService : IFeedbackService
{
    private readonly IFeedbackEntryRepository _entries;
    public FeedbackService(IFeedbackEntryRepository entries) => _entries = entries;

    public async Task<IReadOnlyList<FeedbackEntryDto>> ListAsync(Guid swimmerId, CancellationToken ct = default)
    {
        var rows = await _entries.ListBySwimmerAsync(swimmerId, ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<FeedbackEntryDto> CreateAsync(Guid swimmerId, CreateFeedbackEntryRequest request, Guid authorId, CancellationToken ct = default)
    {
        var entry = new FeedbackEntry(swimmerId, request.Rating, request.CategoryId, request.Comment.Trim(), authorId);
        await _entries.AddAsync(entry, ct);
        await _entries.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task<FeedbackEntryDto?> UpdateAsync(Guid swimmerId, Guid entryId, CreateFeedbackEntryRequest request, CancellationToken ct = default)
    {
        var entry = await _entries.GetTrackedAsync(entryId, ct);
        if (entry is null || entry.SwimmerId != swimmerId) return null;

        entry.Update(request.Rating, request.CategoryId, request.Comment.Trim());
        await _entries.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task<bool> DeleteAsync(Guid swimmerId, Guid entryId, CancellationToken ct = default)
    {
        var entry = await _entries.GetTrackedAsync(entryId, ct);
        if (entry is null || entry.SwimmerId != swimmerId) return false;

        _entries.Remove(entry);
        await _entries.SaveChangesAsync(ct);
        return true;
    }

    private static FeedbackEntryDto ToDto(FeedbackEntry e) =>
        new(e.Id, e.SwimmerId, e.Rating, e.CategoryId, e.Comment, e.AuthorId, string.Empty, null, e.EntryDate);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FeedbackServiceTests`
Expected: PASS. (Ensure the `IFeedbackService`/`FeedbackService` DI lines from Task 7 are both present now.)

- [ ] **Step 5: Stage (do NOT commit)**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/DTOs/FeedbackEntryDtos.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Resources/FeedbackMessages.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/Interfaces/IFeedbackService.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/FeedbackService.cs \
        backend/tests/Kheprx.BaseBackend.Health.UnitTests/Services/FeedbackServiceTests.cs
```
Held commit message: `feat(health): feedback DTOs, messages, service`

---

### Task 10: Request validator

**Files:**
- Create: `…/Health.Application/Validators/CreateFeedbackEntryRequestValidator.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Validators/CreateFeedbackEntryRequestValidatorTests.cs`

**Interfaces:**
- Consumes: `CreateFeedbackEntryRequest` (Task 9).
- Produces: `CreateFeedbackEntryRequestValidator` (FluentValidation) — `Rating` 1..5; `Comment` non-empty, ≤1000; `CategoryId` not empty.

- [ ] **Step 1: Write the failing test**

```csharp
using FluentValidation.TestHelper;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Validators;

public class CreateFeedbackEntryRequestValidatorTests
{
    private readonly CreateFeedbackEntryRequestValidator _validator = new();

    [Fact]
    public void Passes_for_valid_request()
    {
        var r = new CreateFeedbackEntryRequest(5, Guid.NewGuid(), "Great rhythm");
        _validator.TestValidate(r).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Fails_for_out_of_range_rating(short rating)
    {
        var r = new CreateFeedbackEntryRequest(rating, Guid.NewGuid(), "ok");
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.Rating);
    }

    [Fact]
    public void Fails_for_blank_comment()
    {
        var r = new CreateFeedbackEntryRequest(3, Guid.NewGuid(), "   ");
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.Comment);
    }

    [Fact]
    public void Fails_for_empty_category()
    {
        var r = new CreateFeedbackEntryRequest(3, Guid.Empty, "ok");
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.CategoryId);
    }
}
```
(If the project references a different FluentValidation test helper package, match the assertion style already used in `CreateInBodyReadingRequestValidatorTests`; the rules under test are the same.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter CreateFeedbackEntryRequestValidatorTests`
Expected: FAIL — validator does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
using FluentValidation;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Health.Application.Validators;

public sealed class CreateFeedbackEntryRequestValidator : AbstractValidator<CreateFeedbackEntryRequest>
{
    public CreateFeedbackEntryRequestValidator()
    {
        RuleFor(x => x.Rating)
            .InclusiveBetween((short)1, (short)5)
            .WithMessage(_ => FeedbackMessages.Errors.RatingInvalid(AppLanguage.Current));
        RuleFor(x => x.Comment)
            .NotEmpty().MaximumLength(1000)
            .WithMessage(_ => FeedbackMessages.Errors.CommentRequired(AppLanguage.Current));
        RuleFor(x => x.CategoryId)
            .NotEqual(Guid.Empty)
            .WithMessage(_ => FeedbackMessages.Errors.CategoryRequired(AppLanguage.Current));
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter CreateFeedbackEntryRequestValidatorTests`
Expected: PASS.

- [ ] **Step 5: Stage (do NOT commit)**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Validators/CreateFeedbackEntryRequestValidator.cs \
        backend/tests/Kheprx.BaseBackend.Health.UnitTests/Validators/CreateFeedbackEntryRequestValidatorTests.cs
```
Held commit message: `feat(health): validate feedback create/update request`

---

### Task 11: `FeedbackEntriesController` (nested CRUD + author enrichment)

**Files:**
- Create: `backend/Kheprx.BaseBackend.Api/Controllers/FeedbackEntriesController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/FeedbackEntriesControllerTests.cs`

**Interfaces:**
- Consumes: `IFeedbackService` (Task 9), `IUserService.GetDisplayNamesAsync` (Task 5), `FeedbackMessages` (Task 9), `CurrentUserId()` (BaseApiController).
- Produces: routes `GET/POST/PUT{entryId}/DELETE{entryId} api/swimmers/{id}/feedback-entries`, list/create responses carry resolved `AuthorNameEn`/`AuthorNameAr`.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Security.Claims;
using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class FeedbackEntriesControllerTests
{
    private static FeedbackEntriesController Controller(IFeedbackService svc, IUserService users, Guid? userId = null)
    {
        var identity = userId is null
            ? new ClaimsIdentity()
            : new ClaimsIdentity(new[] { new Claim("sub", userId.Value.ToString()) }, "jwt");
        return new FeedbackEntriesController(svc, users)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static FeedbackEntryDto Dto(Guid id, Guid authorId) =>
        new(id, Guid.NewGuid(), (short)4, Guid.NewGuid(), "note", authorId, string.Empty, null, new DateOnly(2024, 10, 4));

    private static CreateFeedbackEntryRequest Req() => new(4, Guid.NewGuid(), "note");

    [Fact]
    public async Task List_returns_200_and_resolves_author_names()
    {
        var svc = new Mock<IFeedbackService>();
        var users = new Mock<IUserService>();
        var swimmerId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        svc.Setup(s => s.ListAsync(swimmerId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Dto(Guid.NewGuid(), authorId) });
        users.Setup(u => u.GetDisplayNamesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Dictionary<Guid, UserNameDto> { [authorId] = new(authorId, "Coach Omar", "الكابتن عمر") });

        var result = await Controller(svc.Object, users.Object).List(swimmerId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<FeedbackEntryDto>>>(ok.Value);
        Assert.Equal("Coach Omar", body.Data![0].AuthorNameEn);
    }

    [Fact]
    public async Task Create_uses_current_user_as_author_and_returns_201()
    {
        var svc = new Mock<IFeedbackService>();
        var users = new Mock<IUserService>();
        var swimmerId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        svc.Setup(s => s.CreateAsync(swimmerId, It.IsAny<CreateFeedbackEntryRequest>(), authorId, It.IsAny<CancellationToken>()))
           .ReturnsAsync(Dto(Guid.NewGuid(), authorId));
        users.Setup(u => u.GetDisplayNamesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Dictionary<Guid, UserNameDto> { [authorId] = new(authorId, "Coach Omar", null) });

        var result = await Controller(svc.Object, users.Object, authorId).Create(swimmerId, Req(), CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        svc.Verify(s => s.CreateAsync(swimmerId, It.IsAny<CreateFeedbackEntryRequest>(), authorId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_returns_404_when_null()
    {
        var svc = new Mock<IFeedbackService>();
        var users = new Mock<IUserService>();
        var swimmerId = Guid.NewGuid();
        svc.Setup(s => s.UpdateAsync(swimmerId, It.IsAny<Guid>(), It.IsAny<CreateFeedbackEntryRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync((FeedbackEntryDto?)null);

        var nf = await Controller(svc.Object, users.Object).Update(swimmerId, Guid.NewGuid(), Req(), CancellationToken.None);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(nf.Result).StatusCode);
    }

    [Fact]
    public async Task Delete_returns_200_when_true_404_when_false()
    {
        var svc = new Mock<IFeedbackService>();
        var users = new Mock<IUserService>();
        var swimmerId = Guid.NewGuid();
        var eid = Guid.NewGuid();
        svc.Setup(s => s.DeleteAsync(swimmerId, eid, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        svc.Setup(s => s.DeleteAsync(swimmerId, It.Is<Guid>(g => g != eid), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Assert.IsType<OkObjectResult>((await Controller(svc.Object, users.Object).Delete(swimmerId, eid, CancellationToken.None)).Result);
        var nf = await Controller(svc.Object, users.Object).Delete(swimmerId, Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(nf.Result).StatusCode);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter FeedbackEntriesControllerTests`
Expected: FAIL — controller does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Route("api/swimmers/{id:guid}/feedback-entries")]
public sealed class FeedbackEntriesController : BaseApiController
{
    private readonly IFeedbackService _service;
    private readonly IUserService _users;

    public FeedbackEntriesController(IFeedbackService service, IUserService users)
    {
        _service = service;
        _users = users;
    }

    /// <summary>Lists a swimmer's feedback entries, newest first, with author names resolved.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FeedbackEntryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FeedbackEntryDto>>>> List(Guid id, CancellationToken ct)
    {
        var rows = await _service.ListAsync(id, ct);
        var enriched = await EnrichAuthors(rows, ct);
        return Ok(ApiResponse<IReadOnlyList<FeedbackEntryDto>>.Success(FeedbackMessages.Success.Listed(AppLanguage.Current), enriched));
    }

    /// <summary>Records new feedback. Head Coach or Captain only.</summary>
    [HttpPost]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<FeedbackEntryDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<FeedbackEntryDto>>> Create(Guid id, CreateFeedbackEntryRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(id, request, CurrentUserId(), ct);
        var enriched = (await EnrichAuthors(new[] { created }, ct))[0];
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<FeedbackEntryDto>.Success(FeedbackMessages.Success.Created(AppLanguage.Current), enriched));
    }

    /// <summary>Updates a feedback entry. Head Coach or Captain only.</summary>
    [HttpPut("{entryId:guid}")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<FeedbackEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FeedbackEntryDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<FeedbackEntryDto>>> Update(Guid id, Guid entryId, CreateFeedbackEntryRequest request, CancellationToken ct)
    {
        var updated = await _service.UpdateAsync(id, entryId, request, ct);
        if (updated is null)
        {
            var nf = ApiResponse<FeedbackEntryDto>.Failure(FeedbackMessages.Errors.NotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        var enriched = (await EnrichAuthors(new[] { updated }, ct))[0];
        return Ok(ApiResponse<FeedbackEntryDto>.Success(FeedbackMessages.Success.Updated(AppLanguage.Current), enriched));
    }

    /// <summary>Deletes a feedback entry. Head Coach or Captain only.</summary>
    [HttpDelete("{entryId:guid}")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, Guid entryId, CancellationToken ct)
    {
        var deleted = await _service.DeleteAsync(id, entryId, ct);
        if (!deleted)
        {
            var nf = ApiResponse<object>.Failure(FeedbackMessages.Errors.NotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<object>.Success(FeedbackMessages.Success.Deleted(AppLanguage.Current), null));
    }

    // Resolves author_id -> display names via the Identity module (composition at the API layer).
    private async Task<IReadOnlyList<FeedbackEntryDto>> EnrichAuthors(IReadOnlyList<FeedbackEntryDto> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return rows;
        var ids = rows.Select(r => r.AuthorId).Distinct().ToList();
        var names = await _users.GetDisplayNamesAsync(ids, ct);
        return rows.Select(r => names.TryGetValue(r.AuthorId, out var n)
            ? r with { AuthorNameEn = n.NameEn, AuthorNameAr = n.NameAr }
            : r).ToList();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter FeedbackEntriesControllerTests`
Expected: PASS.

- [ ] **Step 5: Run the whole backend suite**

Run: `dotnet test backend/Kheprx.BaseBackend.sln` (stop the dev API first). Expected: green.

- [ ] **Step 6: Stage (do NOT commit)**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/FeedbackEntriesController.cs \
        backend/tests/Kheprx.BaseBackend.Api.UnitTests/FeedbackEntriesControllerTests.cs
```
Held commit message: `feat(api): FeedbackEntriesController with author-name enrichment`

---

## Frontend

### Task 12: Reference — load feedback categories

**Files:**
- Modify: `frontend/src/app/features/reference/domain/repositories/reference.repository.ts`
- Modify: `frontend/src/app/features/reference/data/repositories/reference.repository.impl.ts`
- Create: `frontend/src/app/features/reference/domain/usecases/load-feedback-categories.use-case.ts`
- Test: `frontend/src/app/features/reference/testing/domain/usecases/load-feedback-categories.use-case.spec.ts`

**Interfaces:**
- Consumes: `IReferenceRepository`, `CodedLookupListDtoRs`, `isCodedLookupListValid`, `LookupItem` (existing).
- Produces: `IReferenceRepository.getFeedbackCategories()`; `LoadFeedbackCategoriesUseCase.run(): Promise<Result<LookupItem[]>>`.

- [ ] **Step 1: Write the failing test**

```typescript
import { LoadFeedbackCategoriesUseCase } from '@features/reference/domain/usecases/load-feedback-categories.use-case';
import { REFERENCE_REPOSITORY } from '@features/reference/domain/repositories/reference.repository';
import { TestBed } from '@angular/core/testing';

describe('LoadFeedbackCategoriesUseCase', () => {
  it('maps coded lookups to LookupItem[]', async () => {
    const repo = {
      getFeedbackCategories: () =>
        Promise.resolve({ data: [{ id: 'c1', code: 'technique', nameEn: 'Technique', nameAr: 'الأداء الفني' }] }),
    };
    TestBed.configureTestingModule({
      providers: [{ provide: REFERENCE_REPOSITORY, useValue: repo }, LoadFeedbackCategoriesUseCase],
    });
    const uc = TestBed.inject(LoadFeedbackCategoriesUseCase);

    const res = await uc.run();

    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data[0].code).toBe('technique');
  });
});
```
(Mirror the exact provider/assertion shape used in `load-observation-categories.use-case.spec.ts` if it differs — match the sibling.)

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx vitest run src/app/features/reference/testing/domain/usecases/load-feedback-categories.use-case.spec.ts`
Expected: FAIL — use-case/repo method missing.

- [ ] **Step 3: Write minimal implementation**

`reference.repository.ts` — add to the interface:
```typescript
  getFeedbackCategories(): Promise<CodedLookupListDtoRs>;
```

`reference.repository.impl.ts` — add:
```typescript
  getFeedbackCategories(): Promise<CodedLookupListDtoRs> {
    return this.http.get<CodedLookupListDtoRs>('/api/reference/feedback-categories');
  }
```

`load-feedback-categories.use-case.ts`:
```typescript
// load-feedback-categories.use-case.ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { REFERENCE_REPOSITORY } from '@features/reference/domain/repositories/reference.repository';
import { isCodedLookupListValid } from '@features/reference/data/dto/reference.dto';
import { LookupItem } from '@features/reference/domain/model/reference';

@Injectable({ providedIn: 'root' })
export class LoadFeedbackCategoriesUseCase extends UseCase<void, LookupItem[]> {
  private readonly repo = inject(REFERENCE_REPOSITORY);
  constructor() { super('LoadFeedbackCategories'); }
  protected async execute(): Promise<LookupItem[]> {
    const res = await this.repo.getFeedbackCategories();
    if (!isCodedLookupListValid(res.data)) throw new AppError('Invalid feedback categories received', 'validation');
    return res.data.map((c) => ({ id: c.id, code: c.code, nameEn: c.nameEn, nameAr: c.nameAr }));
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx vitest run src/app/features/reference/testing/domain/usecases/load-feedback-categories.use-case.spec.ts`
Expected: PASS.

- [ ] **Step 5: Stage (do NOT commit)**

```bash
git add frontend/src/app/features/reference/domain/repositories/reference.repository.ts \
        frontend/src/app/features/reference/data/repositories/reference.repository.impl.ts \
        frontend/src/app/features/reference/domain/usecases/load-feedback-categories.use-case.ts \
        frontend/src/app/features/reference/testing/domain/usecases/load-feedback-categories.use-case.spec.ts
```
Held commit message: `feat(reference): load feedback categories`

---

### Task 13: Feedback DTO, model, mapper

**Files:**
- Create: `frontend/src/app/features/swimmer-profile/data/dto/feedback-entry.dto.ts`
- Create: `frontend/src/app/features/swimmer-profile/domain/model/feedback-entry.ts`
- Create: `frontend/src/app/features/swimmer-profile/data/dto/feedback-entry.mapper.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/data/dto/feedback-entry.mapper.spec.ts`

**Interfaces:**
- Produces:
  - `FeedbackEntryDtoRs { id; swimmerId; rating; categoryId; comment; authorId; authorNameEn; authorNameAr; entryDate }` + list/item/delete response aliases + `CreateFeedbackEntryDtoRq { rating; categoryId; comment }` + `isFeedbackEntryDtoRsValid` / `isFeedbackEntryListValid`.
  - `FeedbackEntry` model `{ id; rating; categoryId; comment; authorNameEn; authorNameAr; entryDate }`.
  - `toFeedbackEntry` / `toFeedbackEntryList`.

- [ ] **Step 1: Write the failing test**

```typescript
import { toFeedbackEntry, toFeedbackEntryList } from '@features/swimmer-profile/data/dto/feedback-entry.mapper';
import { FeedbackEntryDtoRs } from '@features/swimmer-profile/data/dto/feedback-entry.dto';

const DTO: FeedbackEntryDtoRs = {
  id: 'f1', swimmerId: 's1', rating: 5, categoryId: 'c1', comment: 'Great rhythm',
  authorId: 'u1', authorNameEn: 'Coach Omar', authorNameAr: 'الكابتن عمر', entryDate: '2024-10-22',
};

describe('feedback-entry.mapper', () => {
  it('maps an entry (keeps author names, drops ids we do not render)', () => {
    const m = toFeedbackEntry(DTO);
    expect(m.id).toBe('f1');
    expect(m.rating).toBe(5);
    expect(m.categoryId).toBe('c1');
    expect(m.comment).toBe('Great rhythm');
    expect(m.authorNameEn).toBe('Coach Omar');
    expect(m.entryDate).toBe('2024-10-22');
  });

  it('maps a list', () => {
    expect(toFeedbackEntryList([DTO])).toHaveLength(1);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx vitest run src/app/features/swimmer-profile/testing/data/dto/feedback-entry.mapper.spec.ts`
Expected: FAIL — modules do not exist.

- [ ] **Step 3: Write minimal implementation**

`feedback-entry.dto.ts`:
```typescript
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface FeedbackEntryDtoRs {
  id: string;
  swimmerId: string;
  rating: number;
  categoryId: string;
  comment: string;
  authorId: string;
  authorNameEn: string;
  authorNameAr: string | null;
  entryDate: string;
}
export interface FeedbackEntryListDtoRs extends BaseResponseRs<FeedbackEntryDtoRs[]> {}
export interface FeedbackEntryItemDtoRs extends BaseResponseRs<FeedbackEntryDtoRs> {}
export interface DeleteFeedbackEntryItemDtoRs extends BaseResponseRs<unknown> {}

export interface CreateFeedbackEntryDtoRq {
  rating: number;
  categoryId: string;
  comment: string;
}

export function isFeedbackEntryDtoRsValid(x: unknown): x is FeedbackEntryDtoRs {
  const d = x as FeedbackEntryDtoRs;
  if (!d || typeof d !== 'object') return false;
  return typeof d.id === 'string'
    && typeof d.rating === 'number'
    && typeof d.categoryId === 'string'
    && typeof d.comment === 'string'
    && typeof d.entryDate === 'string';
}

export function isFeedbackEntryListValid(data: unknown): data is FeedbackEntryDtoRs[] {
  return Array.isArray(data) && data.every(isFeedbackEntryDtoRsValid);
}
```

`feedback-entry.ts`:
```typescript
export interface FeedbackEntry {
  id: string;
  rating: number;
  categoryId: string;
  comment: string;
  authorNameEn: string;
  authorNameAr: string | null;
  entryDate: string;
}
```

`feedback-entry.mapper.ts`:
```typescript
import { FeedbackEntryDtoRs } from '@features/swimmer-profile/data/dto/feedback-entry.dto';
import { FeedbackEntry } from '@features/swimmer-profile/domain/model/feedback-entry';

export function toFeedbackEntry(d: FeedbackEntryDtoRs): FeedbackEntry {
  return {
    id: d.id,
    rating: d.rating,
    categoryId: d.categoryId,
    comment: d.comment,
    authorNameEn: d.authorNameEn,
    authorNameAr: d.authorNameAr,
    entryDate: d.entryDate,
  };
}

export function toFeedbackEntryList(list: FeedbackEntryDtoRs[]): FeedbackEntry[] {
  return list.map(toFeedbackEntry);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx vitest run src/app/features/swimmer-profile/testing/data/dto/feedback-entry.mapper.spec.ts`
Expected: PASS.

- [ ] **Step 5: Stage (do NOT commit)**

```bash
git add frontend/src/app/features/swimmer-profile/data/dto/feedback-entry.dto.ts \
        frontend/src/app/features/swimmer-profile/domain/model/feedback-entry.ts \
        frontend/src/app/features/swimmer-profile/data/dto/feedback-entry.mapper.ts \
        frontend/src/app/features/swimmer-profile/testing/data/dto/feedback-entry.mapper.spec.ts
```
Held commit message: `feat(swimmer-profile): feedback DTO, model, mapper`

---

### Task 14: Repository methods (list/create/update/delete feedback)

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/domain/repositories/swimmer-profile.repository.ts`
- Modify: `frontend/src/app/features/swimmer-profile/data/repositories/swimmer-profile.repository.impl.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/data/repositories/swimmer-profile.repository.impl.spec.ts` (add cases)

**Interfaces:**
- Consumes: feedback DTO types (Task 13).
- Produces on `ISwimmerProfileRepository`:
  - `getFeedbackEntries(id) → FeedbackEntryListDtoRs`
  - `createFeedbackEntry(id, rq) → FeedbackEntryItemDtoRs`
  - `updateFeedbackEntry(id, entryId, rq) → FeedbackEntryItemDtoRs`
  - `deleteFeedbackEntry(id, entryId) → DeleteFeedbackEntryItemDtoRs`

- [ ] **Step 1: Write the failing test** (add to the existing repo impl spec; mirror the InBody cases already there)

```typescript
  it('getFeedbackEntries GETs the nested route', async () => {
    const http = { get: vi.fn().mockResolvedValue({ data: [] }), post: vi.fn(), put: vi.fn(), delete: vi.fn() };
    const repo = makeRepo(http); // use the spec's existing factory/TestBed setup
    await repo.getFeedbackEntries('s1');
    expect(http.get).toHaveBeenCalledWith('/api/swimmers/s1/feedback-entries');
  });

  it('createFeedbackEntry POSTs the payload', async () => {
    const http = { get: vi.fn(), post: vi.fn().mockResolvedValue({ data: {} }), put: vi.fn(), delete: vi.fn() };
    const repo = makeRepo(http);
    const rq = { rating: 5, categoryId: 'c1', comment: 'x' };
    await repo.createFeedbackEntry('s1', rq);
    expect(http.post).toHaveBeenCalledWith('/api/swimmers/s1/feedback-entries', { body: rq });
  });

  it('updateFeedbackEntry PUTs and deleteFeedbackEntry DELETEs the entry route', async () => {
    const http = { get: vi.fn(), post: vi.fn(), put: vi.fn().mockResolvedValue({ data: {} }), delete: vi.fn().mockResolvedValue({ data: null }) };
    const repo = makeRepo(http);
    await repo.updateFeedbackEntry('s1', 'f1', { rating: 4, categoryId: 'c1', comment: 'y' });
    await repo.deleteFeedbackEntry('s1', 'f1');
    expect(http.put).toHaveBeenCalledWith('/api/swimmers/s1/feedback-entries/f1', { body: { rating: 4, categoryId: 'c1', comment: 'y' } });
    expect(http.delete).toHaveBeenCalledWith('/api/swimmers/s1/feedback-entries/f1');
  });
```
(Match the existing spec's harness — it already tests `getInBodyReadings`/`createInBodyReading` the same way; copy that factory and the `vi`/`vitest` import style verbatim.)

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx vitest run src/app/features/swimmer-profile/testing/data/repositories/swimmer-profile.repository.impl.spec.ts`
Expected: FAIL — methods missing.

- [ ] **Step 3: Write minimal implementation**

`swimmer-profile.repository.ts` — add the import and four interface methods:
```typescript
import { FeedbackEntryListDtoRs, FeedbackEntryItemDtoRs, DeleteFeedbackEntryItemDtoRs, CreateFeedbackEntryDtoRq } from '@features/swimmer-profile/data/dto/feedback-entry.dto';
```
```typescript
  getFeedbackEntries(id: string): Promise<FeedbackEntryListDtoRs>;
  createFeedbackEntry(id: string, rq: CreateFeedbackEntryDtoRq): Promise<FeedbackEntryItemDtoRs>;
  updateFeedbackEntry(id: string, entryId: string, rq: CreateFeedbackEntryDtoRq): Promise<FeedbackEntryItemDtoRs>;
  deleteFeedbackEntry(id: string, entryId: string): Promise<DeleteFeedbackEntryItemDtoRs>;
```

`swimmer-profile.repository.impl.ts` — add the same import and:
```typescript
  getFeedbackEntries(id: string): Promise<FeedbackEntryListDtoRs> {
    return this.http.get<FeedbackEntryListDtoRs>(`/api/swimmers/${id}/feedback-entries`);
  }
  createFeedbackEntry(id: string, rq: CreateFeedbackEntryDtoRq): Promise<FeedbackEntryItemDtoRs> {
    return this.http.post<FeedbackEntryItemDtoRs>(`/api/swimmers/${id}/feedback-entries`, { body: rq });
  }
  updateFeedbackEntry(id: string, entryId: string, rq: CreateFeedbackEntryDtoRq): Promise<FeedbackEntryItemDtoRs> {
    return this.http.put<FeedbackEntryItemDtoRs>(`/api/swimmers/${id}/feedback-entries/${entryId}`, { body: rq });
  }
  deleteFeedbackEntry(id: string, entryId: string): Promise<DeleteFeedbackEntryItemDtoRs> {
    return this.http.delete<DeleteFeedbackEntryItemDtoRs>(`/api/swimmers/${id}/feedback-entries/${entryId}`);
  }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx vitest run src/app/features/swimmer-profile/testing/data/repositories/swimmer-profile.repository.impl.spec.ts`
Expected: PASS.

- [ ] **Step 5: Stage (do NOT commit)**

```bash
git add frontend/src/app/features/swimmer-profile/domain/repositories/swimmer-profile.repository.ts \
        frontend/src/app/features/swimmer-profile/data/repositories/swimmer-profile.repository.impl.ts \
        frontend/src/app/features/swimmer-profile/testing/data/repositories/swimmer-profile.repository.impl.spec.ts
```
Held commit message: `feat(swimmer-profile): feedback repository methods`

---

### Task 15: Feedback use-cases (list/create/update/delete)

**Files:**
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/list-feedback-entries.use-case.ts`
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/create-feedback-entry.use-case.ts`
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/update-feedback-entry.use-case.ts`
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/delete-feedback-entry.use-case.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/domain/usecases/feedback-entries.use-cases.spec.ts`

**Interfaces:**
- Consumes: `SWIMMER_PROFILE_REPOSITORY`, feedback DTO validators/mappers (Tasks 13–14).
- Produces:
  - `ListFeedbackEntriesUseCase.run(id: string) → FeedbackEntry[]`
  - `CreateFeedbackEntryUseCase.run({ id, rq }) → FeedbackEntry`
  - `UpdateFeedbackEntryUseCase.run({ id, entryId, rq }) → FeedbackEntry`
  - `DeleteFeedbackEntryUseCase.run({ id, entryId }) → void`

- [ ] **Step 1: Write the failing test**

```typescript
import { TestBed } from '@angular/core/testing';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { ListFeedbackEntriesUseCase } from '@features/swimmer-profile/domain/usecases/list-feedback-entries.use-case';
import { CreateFeedbackEntryUseCase } from '@features/swimmer-profile/domain/usecases/create-feedback-entry.use-case';
import { DeleteFeedbackEntryUseCase } from '@features/swimmer-profile/domain/usecases/delete-feedback-entry.use-case';

const ENTRY = {
  id: 'f1', swimmerId: 's1', rating: 5, categoryId: 'c1', comment: 'x',
  authorId: 'u1', authorNameEn: 'Coach Omar', authorNameAr: null, entryDate: '2024-10-22',
};

function configure(repo: Partial<Record<string, unknown>>) {
  TestBed.configureTestingModule({
    providers: [
      { provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo },
      ListFeedbackEntriesUseCase, CreateFeedbackEntryUseCase, DeleteFeedbackEntryUseCase,
    ],
  });
}

describe('feedback use-cases', () => {
  it('list maps entries', async () => {
    configure({ getFeedbackEntries: () => Promise.resolve({ data: [ENTRY] }) });
    const res = await TestBed.inject(ListFeedbackEntriesUseCase).run('s1');
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data[0].comment).toBe('x');
  });

  it('create maps the returned entry', async () => {
    configure({ createFeedbackEntry: () => Promise.resolve({ data: ENTRY }) });
    const res = await TestBed.inject(CreateFeedbackEntryUseCase).run({ id: 's1', rq: { rating: 5, categoryId: 'c1', comment: 'x' } });
    expect(res.ok).toBe(true);
  });

  it('delete resolves', async () => {
    configure({ deleteFeedbackEntry: () => Promise.resolve({ data: null }) });
    const res = await TestBed.inject(DeleteFeedbackEntryUseCase).run({ id: 's1', entryId: 'f1' });
    expect(res.ok).toBe(true);
  });
});
```
(Follow the harness in `inbody-readings.use-cases.spec.ts` — same `UseCase.run` result shape `{ ok, data | error }`.)

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx vitest run src/app/features/swimmer-profile/testing/domain/usecases/feedback-entries.use-cases.spec.ts`
Expected: FAIL — use-cases do not exist.

- [ ] **Step 3: Write minimal implementation**

`list-feedback-entries.use-case.ts`:
```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { isFeedbackEntryListValid } from '@features/swimmer-profile/data/dto/feedback-entry.dto';
import { toFeedbackEntryList } from '@features/swimmer-profile/data/dto/feedback-entry.mapper';
import { FeedbackEntry } from '@features/swimmer-profile/domain/model/feedback-entry';

@Injectable({ providedIn: 'root' })
export class ListFeedbackEntriesUseCase extends UseCase<string, FeedbackEntry[]> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('ListFeedbackEntries'); }
  protected async execute(id: string): Promise<FeedbackEntry[]> {
    const res = await this.repo.getFeedbackEntries(id);
    if (!isFeedbackEntryListValid(res.data)) throw new AppError('Invalid feedback entries received', 'validation');
    return toFeedbackEntryList(res.data);
  }
}
```

`create-feedback-entry.use-case.ts`:
```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { CreateFeedbackEntryDtoRq, isFeedbackEntryDtoRsValid } from '@features/swimmer-profile/data/dto/feedback-entry.dto';
import { toFeedbackEntry } from '@features/swimmer-profile/data/dto/feedback-entry.mapper';
import { FeedbackEntry } from '@features/swimmer-profile/domain/model/feedback-entry';

export interface CreateFeedbackEntryInput { id: string; rq: CreateFeedbackEntryDtoRq; }

@Injectable({ providedIn: 'root' })
export class CreateFeedbackEntryUseCase extends UseCase<CreateFeedbackEntryInput, FeedbackEntry> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('CreateFeedbackEntry'); }
  protected async execute(input: CreateFeedbackEntryInput): Promise<FeedbackEntry> {
    const res = await this.repo.createFeedbackEntry(input.id, input.rq);
    if (!isFeedbackEntryDtoRsValid(res.data)) throw new AppError('Invalid feedback entry received', 'validation');
    return toFeedbackEntry(res.data);
  }
}
```

`update-feedback-entry.use-case.ts`:
```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { CreateFeedbackEntryDtoRq, isFeedbackEntryDtoRsValid } from '@features/swimmer-profile/data/dto/feedback-entry.dto';
import { toFeedbackEntry } from '@features/swimmer-profile/data/dto/feedback-entry.mapper';
import { FeedbackEntry } from '@features/swimmer-profile/domain/model/feedback-entry';

export interface UpdateFeedbackEntryInput { id: string; entryId: string; rq: CreateFeedbackEntryDtoRq; }

@Injectable({ providedIn: 'root' })
export class UpdateFeedbackEntryUseCase extends UseCase<UpdateFeedbackEntryInput, FeedbackEntry> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('UpdateFeedbackEntry'); }
  protected async execute(input: UpdateFeedbackEntryInput): Promise<FeedbackEntry> {
    const res = await this.repo.updateFeedbackEntry(input.id, input.entryId, input.rq);
    if (!isFeedbackEntryDtoRsValid(res.data)) throw new AppError('Invalid feedback entry received', 'validation');
    return toFeedbackEntry(res.data);
  }
}
```

`delete-feedback-entry.use-case.ts`:
```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

export interface DeleteFeedbackEntryInput { id: string; entryId: string; }

@Injectable({ providedIn: 'root' })
export class DeleteFeedbackEntryUseCase extends UseCase<DeleteFeedbackEntryInput, void> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('DeleteFeedbackEntry'); }
  protected async execute(input: DeleteFeedbackEntryInput): Promise<void> {
    await this.repo.deleteFeedbackEntry(input.id, input.entryId);
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx vitest run src/app/features/swimmer-profile/testing/domain/usecases/feedback-entries.use-cases.spec.ts`
Expected: PASS.

- [ ] **Step 5: Stage (do NOT commit)**

```bash
git add frontend/src/app/features/swimmer-profile/domain/usecases/list-feedback-entries.use-case.ts \
        frontend/src/app/features/swimmer-profile/domain/usecases/create-feedback-entry.use-case.ts \
        frontend/src/app/features/swimmer-profile/domain/usecases/update-feedback-entry.use-case.ts \
        frontend/src/app/features/swimmer-profile/domain/usecases/delete-feedback-entry.use-case.ts \
        frontend/src/app/features/swimmer-profile/testing/domain/usecases/feedback-entries.use-cases.spec.ts
```
Held commit message: `feat(swimmer-profile): feedback use-cases`

---

### Task 16: ViewModel wiring

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts` (add feedback cases)

**Interfaces:**
- Consumes: the four feedback use-cases (Task 15), `LoadFeedbackCategoriesUseCase` (Task 12).
- Produces on `SwimmerProfileViewModel`: `feedbackEntries`, `feedbackCategories`, add/edit form signals, `feedbackRows` (From/To filtered), `canSaveFeedback`, and methods `startAddFeedback/startEditFeedback/cancelEditFeedback/saveFeedback/askDeleteFeedback/cancelDeleteFeedback/confirmDeleteFeedback`. `'feedback'` added to the `activeTab` union and `setTab`.

Implementation notes — apply these edits (mirroring the InBody + Health-Monitoring blocks):

1. **Imports & injected use-cases** — add:
```typescript
import { ListFeedbackEntriesUseCase } from '@features/swimmer-profile/domain/usecases/list-feedback-entries.use-case';
import { CreateFeedbackEntryUseCase } from '@features/swimmer-profile/domain/usecases/create-feedback-entry.use-case';
import { UpdateFeedbackEntryUseCase } from '@features/swimmer-profile/domain/usecases/update-feedback-entry.use-case';
import { DeleteFeedbackEntryUseCase } from '@features/swimmer-profile/domain/usecases/delete-feedback-entry.use-case';
import { LoadFeedbackCategoriesUseCase } from '@features/reference/domain/usecases/load-feedback-categories.use-case';
import { FeedbackEntry } from '@features/swimmer-profile/domain/model/feedback-entry';
```
```typescript
  private readonly listFeedbackUc = inject(ListFeedbackEntriesUseCase);
  private readonly createFeedbackUc = inject(CreateFeedbackEntryUseCase);
  private readonly updateFeedbackUc = inject(UpdateFeedbackEntryUseCase);
  private readonly deleteFeedbackUc = inject(DeleteFeedbackEntryUseCase);
  private readonly loadFeedbackCategories = inject(LoadFeedbackCategoriesUseCase);
```

2. **Tab union** — extend both the `activeTab` signal type and the `setTab` parameter type to include `'feedback'`, and add a `private feedbackLoaded = false;` flag next to `healthReadingsLoaded`.

3. **State signals** (place near the Health-Monitoring block):
```typescript
  // Feedback state
  readonly feedbackEntries = signal<FeedbackEntry[]>([]);
  readonly feedbackCategories = signal<LookupItem[]>([]);
  readonly loadingFeedback = signal(false);
  readonly fbFrom = signal('');
  readonly fbTo = signal('');
  readonly editingFeedbackId = signal<string | null>(null); // null = not editing; 'new' = add form
  readonly fbRating = signal(0);
  readonly fbCategoryId = signal('');
  readonly fbComment = signal('');
  readonly savingFeedback = signal(false);
  readonly confirmingFeedbackDeleteId = signal<string | null>(null);
  readonly deletingFeedback = signal(false);

  readonly canSaveFeedback = computed(() =>
    this.fbRating() >= 1 && this.fbRating() <= 5
    && this.fbCategoryId().length > 0
    && this.fbComment().trim().length > 0 && this.fbComment().trim().length <= 1000);

  readonly feedbackRows = computed(() => {
    const from = this.fbFrom(); const to = this.fbTo();
    const inRange = (iso: string) => {
      const d = iso.slice(0, 10);
      if (from && d < from) return false;
      if (to && d > to) return false;
      return true;
    };
    return this.feedbackEntries().filter((e) => inRange(e.entryDate)); // server returns newest-first
  });
```

4. **Reset in `load()`** — alongside the other per-swimmer resets:
```typescript
    this.feedbackLoaded = false;
    this.feedbackEntries.set([]);
    this.feedbackCategories.set([]);
    this.editingFeedbackId.set(null);
    this.confirmingFeedbackDeleteId.set(null);
    this.fbFrom.set(''); this.fbTo.set('');
```

5. **`setTab`** — add:
```typescript
    if (key === 'feedback' && !this.feedbackLoaded) void this.loadFeedback();
```

6. **Load + CRUD methods** (mirror `loadRecords` for the parallel category fetch and `saveInBody`/record delete for add-vs-edit + confirm-delete):
```typescript
  private async loadFeedback(): Promise<void> {
    this.feedbackLoaded = true;
    this.loadingFeedback.set(true);
    const [listRes, catRes] = await Promise.all([
      this.listFeedbackUc.run(this.swimmerId),
      this.loadFeedbackCategories.run(),
    ]);
    this.loadingFeedback.set(false);
    if (catRes.ok) this.feedbackCategories.set(catRes.data);
    if (listRes.ok) {
      this.feedbackEntries.set(listRes.data);
    } else {
      this.feedbackLoaded = false;
      this.feedbackEntries.set([]);
    }
  }

  startAddFeedback(): void {
    this.confirmingFeedbackDeleteId.set(null);
    this.editingFeedbackId.set('new');
    this.fbRating.set(0);
    this.fbCategoryId.set(this.feedbackCategories()[0]?.id ?? '');
    this.fbComment.set('');
  }

  startEditFeedback(entry: FeedbackEntry): void {
    this.confirmingFeedbackDeleteId.set(null);
    this.editingFeedbackId.set(entry.id);
    this.fbRating.set(entry.rating);
    this.fbCategoryId.set(entry.categoryId);
    this.fbComment.set(entry.comment);
  }

  cancelEditFeedback(): void { this.editingFeedbackId.set(null); }

  async saveFeedback(): Promise<void> {
    const editing = this.editingFeedbackId();
    if (!editing || !this.canSaveFeedback() || this.savingFeedback()) return;
    this.savingFeedback.set(true);
    const rq = { rating: this.fbRating(), categoryId: this.fbCategoryId(), comment: this.fbComment().trim() };
    const r = editing === 'new'
      ? await this.createFeedbackUc.run({ id: this.swimmerId, rq })
      : await this.updateFeedbackUc.run({ id: this.swimmerId, entryId: editing, rq });
    this.savingFeedback.set(false);
    if (r.ok) {
      this.notify.success(this.i18n.t(editing === 'new' ? 'swimmerProfile.toasts.feedbackSaved' : 'swimmerProfile.toasts.feedbackUpdated'));
      this.editingFeedbackId.set(null);
      this.feedbackLoaded = false;
      await this.loadFeedback();
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.saveFailed'));
    }
  }

  askDeleteFeedback(entryId: string): void { this.editingFeedbackId.set(null); this.confirmingFeedbackDeleteId.set(entryId); }
  cancelDeleteFeedback(): void { this.confirmingFeedbackDeleteId.set(null); }

  async confirmDeleteFeedback(): Promise<void> {
    const entryId = this.confirmingFeedbackDeleteId();
    if (!entryId || this.deletingFeedback()) return;
    this.deletingFeedback.set(true);
    const res = await this.deleteFeedbackUc.run({ id: this.swimmerId, entryId });
    this.deletingFeedback.set(false);
    if (res.ok) {
      this.notify.success(this.i18n.t('swimmerProfile.toasts.feedbackRemoved'));
      this.confirmingFeedbackDeleteId.set(null);
      this.feedbackLoaded = false;
      await this.loadFeedback();
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.deleteFailed'));
    }
  }
```

- [ ] **Step 1: Write the failing test** (add to the viewmodel spec; follow its existing use-case-stub pattern)

```typescript
  it('loads feedback + categories on setTab("feedback") and filters by date range', async () => {
    const vm = makeVm({
      listFeedback: [
        { id: 'f1', rating: 5, categoryId: 'c1', comment: 'a', authorNameEn: 'Coach', authorNameAr: null, entryDate: '2024-10-22' },
        { id: 'f2', rating: 3, categoryId: 'c1', comment: 'b', authorNameEn: 'Coach', authorNameAr: null, entryDate: '2024-09-01' },
      ],
      feedbackCategories: [{ id: 'c1', code: 'technique', nameEn: 'Technique', nameAr: null }],
    });
    await vm.load('s1');
    vm.setTab('feedback');
    await Promise.resolve(); await Promise.resolve();

    expect(vm.feedbackEntries().length).toBe(2);
    vm.fbFrom.set('2024-10-01');
    expect(vm.feedbackRows().length).toBe(1); // only f1 is in range
  });
```
(Wire the two new use-cases + `LoadFeedbackCategoriesUseCase` into the spec's `makeVm` provider stubs exactly as the InBody/records use-cases are already stubbed there.)

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx vitest run src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts`
Expected: FAIL — feedback members missing.

- [ ] **Step 3: Apply the viewmodel edits above.**

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx vitest run src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts`
Expected: PASS.

- [ ] **Step 5: Stage (do NOT commit)**

```bash
git add frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.ts \
        frontend/src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts
```
Held commit message: `feat(swimmer-profile): wire feedback tab into the view-model`

---

### Task 17: Page — enable tab, render feedback section, i18n

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.ts`
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.html`
- Modify: `frontend/src/app/core/i18n/en.json`, `frontend/src/app/core/i18n/ar.json`

**Interfaces:**
- Consumes: viewmodel feedback API (Task 16); `swimmerProfile.tabs.feedback` label already exists.
- Produces: the rendered Feedback tab (add/edit form + history list + From/To filter + delete).

- [ ] **Step 1: Enable the tab and add page helpers** — in `swimmer-profile.page.ts`:
  - Add `'feedback'` to `enabledTabs`:
```typescript
  protected readonly enabledTabs = new Set(['identityVitals', 'guardian', 'physiological', 'inbody', 'records', 'healthMonitoring', 'feedback']);
```
  - Add a star scale + category-name helper:
```typescript
  protected readonly starScale = [1, 2, 3, 4, 5];

  feedbackCategoryName(categoryId: string): string {
    const c = this.vm.feedbackCategories().find((x) => x.id === categoryId);
    return this.refLabel(c ?? null);
  }
```

- [ ] **Step 2: Add i18n keys** — in `en.json` under `swimmerProfile`, add a `feedback` block and extend `toasts`:
```json
    "feedback": {
      "title": "Feedback",
      "subtitle": "Coach performance evaluations for this swimmer.",
      "add": "Add Feedback",
      "rating": "Performance Rating",
      "category": "Category",
      "comment": "Comment",
      "commentPlaceholder": "Write your comment...",
      "save": "Save Feedback",
      "history": "Feedback History",
      "from": "From",
      "to": "To",
      "clear": "Clear",
      "noneYet": "No feedback yet",
      "noneInRange": "No feedback in this date range",
      "edit": "Edit",
      "remove": "Remove",
      "confirmDelete": "Delete this feedback?"
    }
```
And add to `swimmerProfile.toasts`:
```json
      "feedbackSaved": "Feedback saved",
      "feedbackUpdated": "Feedback updated",
      "feedbackRemoved": "Feedback removed"
```
Mirror the same keys in `ar.json` with Arabic copy (e.g. `"title": "التقييمات"`, `"add": "إضافة تقييم"`, `"rating": "تقييم الأداء"`, `"category": "الفئة"`, `"comment": "التعليق"`, `"commentPlaceholder": "اكتب تعليقك..."`, `"save": "حفظ التقييم"`, `"history": "سجل التقييمات"`, `"from": "من"`, `"to": "إلى"`, `"clear": "مسح"`, `"noneYet": "لا توجد تقييمات بعد"`, `"noneInRange": "لا تقييمات في هذه الفترة"`, `"edit": "تعديل"`, `"remove": "حذف"`, `"confirmDelete": "حذف هذا التقييم؟"`; toasts `"feedbackSaved": "تم حفظ التقييم"`, `"feedbackUpdated": "تم تحديث التقييم"`, `"feedbackRemoved": "تم حذف التقييم"`).

- [ ] **Step 3: Render the feedback section** — in `swimmer-profile.page.html`, after the `@if (vm.activeTab() === 'healthMonitoring') { … }` block (still inside the `@else if (vm.profile(); as p)` body), add:
```html
    @if (vm.activeTab() === 'feedback') {
      <section class="rounded-2xl border border-border bg-surface p-6 shadow-sm">
        <div class="mb-4 flex flex-wrap items-end justify-between gap-3 border-b border-border pb-3">
          <div>
            <h2 class="font-heading text-lg text-ink">{{ 'swimmerProfile.feedback.title' | translate }}</h2>
            <p class="mt-0.5 text-sm text-text-secondary">{{ 'swimmerProfile.feedback.subtitle' | translate }}</p>
          </div>
          <div class="flex items-end gap-3">
            <label class="flex flex-col gap-1">
              <span class="text-xs font-bold text-text-secondary">{{ 'swimmerProfile.feedback.from' | translate }}</span>
              <input type="date" class="h-9 rounded-md border border-border bg-surface px-2 text-sm text-ink" [value]="vm.fbFrom()" (change)="vm.fbFrom.set($any($event.target).value)" />
            </label>
            <label class="flex flex-col gap-1">
              <span class="text-xs font-bold text-text-secondary">{{ 'swimmerProfile.feedback.to' | translate }}</span>
              <input type="date" class="h-9 rounded-md border border-border bg-surface px-2 text-sm text-ink" [value]="vm.fbTo()" (change)="vm.fbTo.set($any($event.target).value)" />
            </label>
          </div>
        </div>

        <!-- Coach add form -->
        @if (vm.canEdit() && vm.editingFeedbackId() === 'new') {
          <form class="mb-6 space-y-4 rounded-xl border border-border p-4" (submit)="$event.preventDefault(); vm.saveFeedback()">
            <div>
              <span class="mb-1.5 block text-xs font-bold text-text-secondary">{{ 'swimmerProfile.feedback.rating' | translate }}</span>
              <div class="flex gap-1">
                @for (n of starScale; track n) {
                  <button type="button" class="text-2xl leading-none" [class.text-amber-500]="n <= vm.fbRating()" [class.text-border]="n > vm.fbRating()" (click)="vm.fbRating.set(n)">★</button>
                }
              </div>
            </div>
            <div>
              <span class="mb-1.5 block text-xs font-bold text-text-secondary">{{ 'swimmerProfile.feedback.category' | translate }}</span>
              <div class="flex flex-wrap gap-2">
                @for (c of vm.feedbackCategories(); track c.id) {
                  <button type="button" class="rounded-full border px-3 py-1 text-sm"
                          [class.bg-primary]="vm.fbCategoryId() === c.id" [class.text-white]="vm.fbCategoryId() === c.id"
                          [class.text-text-secondary]="vm.fbCategoryId() !== c.id"
                          (click)="vm.fbCategoryId.set(c.id)">{{ refLabel(c) }}</button>
                }
              </div>
            </div>
            <label class="block">
              <span class="mb-1.5 block text-xs font-bold text-text-secondary">{{ 'swimmerProfile.feedback.comment' | translate }}</span>
              <textarea rows="3" class="w-full rounded-md border border-border bg-surface px-3 py-2 text-sm text-ink"
                        [value]="vm.fbComment()" (input)="vm.fbComment.set($any($event.target).value)"
                        [attr.placeholder]="'swimmerProfile.feedback.commentPlaceholder' | translate"></textarea>
            </label>
            <div class="flex gap-3">
              <button type="submit" class="rounded-md bg-primary px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" [disabled]="!vm.canSaveFeedback() || vm.savingFeedback()">{{ 'swimmerProfile.feedback.save' | translate }}</button>
              <button type="button" class="rounded-md px-4 py-2 text-sm text-text-secondary" (click)="vm.cancelEditFeedback()">{{ 'common.cancel' | translate }}</button>
            </div>
          </form>
        } @else if (vm.canEdit()) {
          <button type="button" class="mb-6 rounded-md border border-primary px-4 py-2 text-sm font-semibold text-primary" (click)="vm.startAddFeedback()">{{ 'swimmerProfile.feedback.add' | translate }}</button>
        }

        <!-- History -->
        <h3 class="mb-3 font-heading text-base text-ink">{{ 'swimmerProfile.feedback.history' | translate }} <span class="text-sm text-text-secondary">({{ vm.feedbackRows().length }})</span></h3>
        @if (vm.loadingFeedback()) {
          <p class="text-sm text-text-secondary">{{ 'swimmerProfile.states.loading' | translate }}</p>
        } @else if (vm.feedbackEntries().length === 0) {
          <p class="text-sm text-text-secondary">{{ 'swimmerProfile.feedback.noneYet' | translate }}</p>
        } @else if (vm.feedbackRows().length === 0) {
          <p class="text-sm text-text-secondary">{{ 'swimmerProfile.feedback.noneInRange' | translate }}</p>
        } @else {
          <div class="space-y-3">
            @for (fb of vm.feedbackRows(); track fb.id) {
              <div class="rounded-xl border border-border p-4">
                @if (vm.editingFeedbackId() === fb.id) {
                  <form class="space-y-3" (submit)="$event.preventDefault(); vm.saveFeedback()">
                    <div class="flex gap-1">
                      @for (n of starScale; track n) {
                        <button type="button" class="text-2xl leading-none" [class.text-amber-500]="n <= vm.fbRating()" [class.text-border]="n > vm.fbRating()" (click)="vm.fbRating.set(n)">★</button>
                      }
                    </div>
                    <div class="flex flex-wrap gap-2">
                      @for (c of vm.feedbackCategories(); track c.id) {
                        <button type="button" class="rounded-full border px-3 py-1 text-sm"
                                [class.bg-primary]="vm.fbCategoryId() === c.id" [class.text-white]="vm.fbCategoryId() === c.id"
                                [class.text-text-secondary]="vm.fbCategoryId() !== c.id"
                                (click)="vm.fbCategoryId.set(c.id)">{{ refLabel(c) }}</button>
                      }
                    </div>
                    <textarea rows="3" class="w-full rounded-md border border-border bg-surface px-3 py-2 text-sm text-ink" [value]="vm.fbComment()" (input)="vm.fbComment.set($any($event.target).value)"></textarea>
                    <div class="flex gap-3">
                      <button type="submit" class="rounded-md bg-primary px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" [disabled]="!vm.canSaveFeedback() || vm.savingFeedback()">{{ 'common.save' | translate }}</button>
                      <button type="button" class="rounded-md px-4 py-2 text-sm text-text-secondary" (click)="vm.cancelEditFeedback()">{{ 'common.cancel' | translate }}</button>
                    </div>
                  </form>
                } @else {
                  <div class="mb-2 flex items-start justify-between gap-3">
                    <div class="flex items-center gap-3">
                      <div class="flex gap-0.5">
                        @for (n of starScale; track n) {
                          <span class="text-sm" [class.text-amber-500]="n <= fb.rating" [class.text-border]="n > fb.rating">★</span>
                        }
                      </div>
                      <span class="rounded-full bg-primary/10 px-2.5 py-0.5 text-xs font-semibold text-primary">{{ feedbackCategoryName(fb.categoryId) }}</span>
                    </div>
                    <span class="shrink-0 text-xs text-text-secondary">{{ fmtDate(fb.entryDate) }}</span>
                  </div>
                  <p class="mb-2 text-sm text-ink">{{ fb.comment }}</p>
                  <div class="flex items-center justify-between">
                    <span class="text-xs font-medium text-text-secondary">{{ refLabel({ nameEn: fb.authorNameEn, nameAr: fb.authorNameAr }) }}</span>
                    @if (vm.canEdit()) {
                      @if (vm.confirmingFeedbackDeleteId() === fb.id) {
                        <span class="flex items-center gap-2 text-sm">
                          <span class="text-danger">{{ 'swimmerProfile.feedback.confirmDelete' | translate }}</span>
                          <button type="button" class="font-medium text-danger disabled:opacity-50" [disabled]="vm.deletingFeedback()" (click)="vm.confirmDeleteFeedback()">{{ 'swimmerProfile.confirmYes' | translate }}</button>
                          <button type="button" class="text-text-secondary" (click)="vm.cancelDeleteFeedback()">{{ 'swimmerProfile.confirmNo' | translate }}</button>
                        </span>
                      } @else {
                        <span class="flex items-center gap-3 text-sm">
                          <button type="button" class="font-medium text-primary" (click)="vm.startEditFeedback(fb)">{{ 'swimmerProfile.feedback.edit' | translate }}</button>
                          <button type="button" class="font-medium text-danger" (click)="vm.askDeleteFeedback(fb.id)">{{ 'swimmerProfile.feedback.remove' | translate }}</button>
                        </span>
                      }
                    }
                  </div>
                }
              </div>
            }
          </div>
        }
      </section>
    }
```

- [ ] **Step 4: Verify build + full frontend suite**

Run: `cd frontend && npx vitest run && npm run build`
Expected: all specs green; production build succeeds (no template/i18n key errors).

- [ ] **Step 5: Manual smoke (optional but recommended)**

Start backend + frontend, open a swimmer profile, click **Feedback**: the history loads (empty state on a fresh DB), a coach can Add (star rating, category chip, comment) → Save → the card appears with the coach's name and today's date; Edit and Remove work; From/To filters the list.

- [ ] **Step 6: Stage (do NOT commit)**

```bash
git add frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.ts \
        frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.html \
        frontend/src/app/core/i18n/en.json \
        frontend/src/app/core/i18n/ar.json
```
Held commit message: `feat(swimmer-profile): render Feedback tab (full CRUD)`

---

## Post-implementation (user-gated)

- [ ] **Update the DB diagram implemented-set** — once tables exist, add `reference.feedback_category` and `health.feedback_entry` to the `IMPLEMENTED` set in `docs/references/swimming-database-diagram.html` (separate small change; the feedback_category/feedback_entry FK edits from 2026-09-22 are already staged there).
- [ ] **Apply migrations** — when the user authorizes, run `dotnet ef database update` for `IdentityDbContext` and `HealthDbContext` against the target DB(s), passing `--connection` for Aiven (see memory). This is explicitly out of the automated plan.
- [ ] **Commit** — only when the user says so; commit each task in order using the held messages above.

---

## Self-Review

**Spec coverage:**
- `reference.feedback_category` lookup (entity/config/repo/migration/seed/endpoint) → Tasks 1–4. ✓
- `health.feedback_entry` full CRUD (entity/config/repo/migration/DTOs/service/validator/controller) → Tasks 6–11. ✓
- Nested routes matching InBody; reads any-auth, writes head_coach/captain; `author_id = CurrentUserId()`; 404 ownership guard → Task 11 (controller) + Task 9 (service guard). ✓
- `entry_date` server-set + immutable; edit limited to rating/category/comment → Task 6 (entity) + Task 9 (service). ✓
- Author name enriched at read in the API layer via a new Identity batch lookup → Tasks 5 + 11. ✓
- Categories resolved client-side via `GET /api/reference/feedback-categories` → Task 4 (backend) + Tasks 12/16/17 (frontend). ✓
- Frontend tab: add form + history list + From/To filter + edit/delete → Tasks 13–17. ✓
- i18n en/ar → Task 17. ✓
- Out of scope (Performance Insights panel; separate page) → not implemented. ✓
- No commits until user says so → Global Constraint + per-task "Stage (do NOT commit)". ✓

**Placeholder scan:** No TBD/TODO; every code step carries real code. Test steps that must match an existing harness (frontend repo-impl spec factory, viewmodel spec `makeVm`, FluentValidation helper) are flagged to mirror the named sibling rather than invent an interface.

**Type consistency:** `FeedbackEntryDto` shape (incl. `AuthorNameEn`/`AuthorNameAr`) is identical across Task 9 (definition), Task 11 (enrichment via `with`), and the frontend DTO in Task 13. `IFeedbackService`/`IFeedbackEntryRepository`/`IFeedbackCategoryRepository`/`IUserService.GetDisplayNamesAsync`/`UserNameDto` signatures match between their defining task and their consumers. Frontend `CreateFeedbackEntryDtoRq { rating, categoryId, comment }` matches the backend `CreateFeedbackEntryRequest(short Rating, Guid CategoryId, string Comment)` and the repository/use-case/viewmodel call sites. `activeTab`/`setTab` union extended to include `'feedback'` in Task 16 and gated in Task 17.
