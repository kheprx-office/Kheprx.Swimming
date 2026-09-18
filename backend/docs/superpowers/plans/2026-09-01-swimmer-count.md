# Swimmer Count Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the login page's hardcoded "452 Swimmers tracked" with a real, database-backed count.

**Architecture:** Add a minimal `identity.swimmer_profile` table to the existing Identity module (entity + EF config + DbSet + repository), seed 24 demo swimmers, expose an anonymous `GET /api/swimmers/count` endpoint through a service + controller, and consume it from a small `features/swimmers` frontend slice that feeds a signal on the login viewmodel.

**Tech Stack:** .NET 10, EF Core (Npgsql/PostgreSQL), ASP.NET Core MVC controllers; Angular (standalone components, signals), Jest; xUnit + Moq for backend tests.

**Spec:** `backend/docs/superpowers/specs/2026-09-01-swimmer-count-design.md`

## Global Constraints

- **Minimal table:** `swimmer_profile` columns are `Id`, `Uid`, `NameEn`, `NameAr?`, `CreatedAt`. **No FKs** to `app_user`, `reference.club`, or `reference.blood_type`.
- **Seed:** exactly **24** swimmers, `Uid` values `SW-0001`…`SW-0024`, idempotent (only when the table is empty).
- **API envelope:** every response wraps in `ApiResponse<T>` — `ApiResponse<T>.Success(string message, T? data)`.
- **Localized messages:** static class with `Method(string lang) => lang switch { "ar" => "…", _ => "…" }`; controllers pass `AppLanguage.Current` (namespace `Kheprx.BaseBackend.SharedKernel.Resources`).
- **Anonymous endpoint:** the count endpoint is consumed by the pre-auth login page, so it MUST carry `[AllowAnonymous]` (same rule as `/api/roles`).
- **Frontend conventions:** repository port + `InjectionToken`, `UseCase<I, O>` base (`execute` throws `AppError` on failure; `run` wraps in `Result`), and a DTO validity guard before mapping.
- **Dirty working tree:** the repo has an unrelated in-progress schema refactor staged. Every commit step uses explicit `git add <paths>` — **never `git add -A`**. Confirm with the user before the first commit.
- **Build lock:** stop the running dev API server (`Kheprx.BaseBackend.Api`) before any `dotnet build` / `dotnet ef` / `dotnet test` that rebuilds the API project — it holds a lock on its `bin` output.
- **Solution / test project paths:**
  - Solution: `backend/Kheprx.BaseBackend.sln`
  - Identity unit tests: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj`
  - API unit tests: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/Kheprx.BaseBackend.Api.UnitTests.csproj`
  - Frontend root: `frontend/` (run `npx jest <path>` from there)

---

### Task 1: Preflight — clean baseline

Confirms the EF model is in sync (so the migration we add later contains **only** `swimmer_profile`), the dev server is stopped, and all suites are green before we start.

**Files:** none (verification only)

- [ ] **Step 1: Stop the dev API server**

```bash
# PowerShell: find and stop the running API so builds/migrations aren't blocked
powershell -Command "Get-Process -Name 'Kheprx.BaseBackend.Api' -ErrorAction SilentlyContinue | Stop-Process -Force"
```

- [ ] **Step 2: Verify the EF model has no pending changes**

Run (a class-library startup project works because `IdentityDbContextFactory` implements `IDesignTimeDbContextFactory`):

```bash
dotnet ef migrations has-pending-model-changes \
  --project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --startup-project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure
```

Expected: **"No changes have been made to the model since the last migration."**
If it reports pending changes, **STOP** — the working tree's schema is already out of sync with the snapshot; resolve that with the user before continuing (otherwise our migration will sweep in unrelated edits). If `dotnet ef` is missing: `dotnet tool install --global dotnet-ef`.

- [ ] **Step 3: Baseline the test suites (all green)**

```bash
dotnet test backend/Kheprx.BaseBackend.sln -v minimal --nologo
cd frontend && npx jest --silent && cd ..
```

Expected: all backend and frontend tests pass. Record the counts; later tasks must not regress them.

---

### Task 2: `SwimmerProfile` domain entity

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/SwimmerProfile.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/SwimmerProfileTests.cs`

**Interfaces:**
- Produces: `SwimmerProfile` with public ctor `SwimmerProfile(string uid, string nameEn, string? nameAr = null)` and read-only properties `Guid Id`, `string Uid`, `string NameEn`, `string? NameAr`, `DateTime CreatedAt`.

- [ ] **Step 1: Write the failing test**

`backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/SwimmerProfileTests.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class SwimmerProfileTests
{
    [Fact]
    public void Ctor_assigns_id_trims_fields_and_stamps_created_at()
    {
        var before = DateTime.UtcNow;
        var s = new SwimmerProfile("  SW-0001 ", "  Ali ", "  علي ");

        Assert.NotEqual(Guid.Empty, s.Id);
        Assert.Equal("SW-0001", s.Uid);
        Assert.Equal("Ali", s.NameEn);
        Assert.Equal("علي", s.NameAr);
        Assert.True(s.CreatedAt >= before);
    }

    [Fact]
    public void Ctor_normalizes_blank_arabic_name_to_null()
    {
        var s = new SwimmerProfile("SW-0002", "Sara", "   ");
        Assert.Null(s.NameAr);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~SwimmerProfileTests" -v minimal --nologo
```

Expected: FAIL — `SwimmerProfile` does not exist (compile error).

- [ ] **Step 3: Write the entity**

`backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/SwimmerProfile.cs`:

```csharp
namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class SwimmerProfile
{
    public Guid Id { get; private set; }
    public string Uid { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private SwimmerProfile() { } // EF Core

    public SwimmerProfile(string uid, string nameEn, string? nameAr = null)
    {
        Id = Guid.NewGuid();
        Uid = uid.Trim();
        NameEn = nameEn.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
        CreatedAt = DateTime.UtcNow;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~SwimmerProfileTests" -v minimal --nologo
```

Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/SwimmerProfile.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/SwimmerProfileTests.cs
git commit -m "feat(identity): add SwimmerProfile domain entity"
```

---

### Task 3: Persistence — EF config, DbSet, repository, DI

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/SwimmerProfileConfiguration.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/ISwimmerProfileRepository.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/SwimmerProfileRepository.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentityDbContext.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs`

**Interfaces:**
- Consumes: `SwimmerProfile` (Task 2); `IdentityDbContext`.
- Produces: `ISwimmerProfileRepository` with `Task<int> CountAsync(CancellationToken ct = default)`; `IdentityDbContext.SwimmerProfiles` DbSet.

This task is verified by a clean build + the existing suites (repositories are thin and untested by codebase convention, mirroring `RoleRepository`).

- [ ] **Step 1: Add the DbSet to `IdentityDbContext`**

In `IdentityDbContext.cs`, add after the `CaptainProfiles` line:

```csharp
    public DbSet<SwimmerProfile> SwimmerProfiles => Set<SwimmerProfile>();
```

- [ ] **Step 2: Create the EF configuration**

`Configurations/SwimmerProfileConfiguration.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class SwimmerProfileConfiguration : IEntityTypeConfiguration<SwimmerProfile>
{
    public void Configure(EntityTypeBuilder<SwimmerProfile> builder)
    {
        builder.ToTable("swimmer_profile");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Uid).HasMaxLength(32).IsRequired();
        builder.HasIndex(p => p.Uid).IsUnique();
        builder.Property(p => p.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(p => p.NameAr).HasMaxLength(200);
        builder.Property(p => p.CreatedAt).IsRequired();
    }
}
```

- [ ] **Step 3: Create the repository port**

`Domain/Repositories/ISwimmerProfileRepository.cs`:

```csharp
namespace Kheprx.BaseBackend.Identity.Domain.Repositories;

public interface ISwimmerProfileRepository
{
    Task<int> CountAsync(CancellationToken ct = default);
}
```

- [ ] **Step 4: Create the repository implementation**

`Repositories/SwimmerProfileRepository.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class SwimmerProfileRepository : ISwimmerProfileRepository
{
    private readonly IdentityDbContext _db;

    public SwimmerProfileRepository(IdentityDbContext db)
    {
        _db = db;
    }

    public Task<int> CountAsync(CancellationToken ct = default)
        => _db.SwimmerProfiles.AsNoTracking().CountAsync(ct);
}
```

- [ ] **Step 5: Register the repository (and the service, used in Task 4) in DI**

In `IdentityModuleExtensions.cs`, add inside `AddIdentityModule` next to the other `AddScoped` calls:

```csharp
        services.AddScoped<ISwimmerProfileRepository, SwimmerProfileRepository>();
        services.AddScoped<ISwimmerService, SwimmerService>();
```

(`ISwimmerService`/`SwimmerService` are created in Task 4; the file's existing `using` directives already cover `...Services`, `...Services.Interfaces`, `...Domain.Repositories`, and `...Infrastructure.Repositories`.)

- [ ] **Step 6: Build to verify it compiles**

> If Task 4 is not yet done, the two lines referencing `ISwimmerService`/`SwimmerService` will not compile. Either do Step 5 as the first step of Task 4, or implement Task 4 before building. Recommended: **defer Step 5 to Task 4, Step 5** and build here without it.

```bash
dotnet build backend/Kheprx.BaseBackend.sln -v minimal --nologo
```

Expected: build succeeds.

- [ ] **Step 7: Commit**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/SwimmerProfileConfiguration.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/ISwimmerProfileRepository.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/SwimmerProfileRepository.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentityDbContext.cs
git commit -m "feat(identity): map swimmer_profile and add count repository"
```

---

### Task 4: Application — DTO, service, messages

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/SwimmerDtos.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/ISwimmerService.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Resources/SwimmerMessages.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs` (if not done in Task 3)
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs`

**Interfaces:**
- Consumes: `ISwimmerProfileRepository.CountAsync` (Task 3).
- Produces: `SwimmerCountDto(int Count)`; `ISwimmerService.GetCountAsync(CancellationToken) : Task<SwimmerCountDto>`; `SwimmerMessages.Success.CountRetrieved(string lang)`.

- [ ] **Step 1: Write the failing test**

`backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Application.Services;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Services;

public class SwimmerServiceTests
{
    [Fact]
    public async Task GetCount_returns_repository_count()
    {
        var repo = new Mock<ISwimmerProfileRepository>();
        repo.Setup(r => r.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(24);

        var result = await new SwimmerService(repo.Object).GetCountAsync();

        Assert.Equal(24, result.Count);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~SwimmerServiceTests" -v minimal --nologo
```

Expected: FAIL — `SwimmerService` / `SwimmerCountDto` do not exist.

- [ ] **Step 3: Create the DTO**

`Application/DTOs/SwimmerDtos.cs`:

```csharp
namespace Kheprx.BaseBackend.Identity.Application.DTOs;

/// <summary>Total number of tracked swimmers (GET /api/swimmers/count).</summary>
/// <param name="Count">How many swimmer profiles exist.</param>
public sealed record SwimmerCountDto(int Count);
```

- [ ] **Step 4: Create the service interface**

`Application/Services/Interfaces/ISwimmerService.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Application.DTOs;

namespace Kheprx.BaseBackend.Identity.Application.Services.Interfaces;

public interface ISwimmerService
{
    Task<SwimmerCountDto> GetCountAsync(CancellationToken ct = default);
}
```

- [ ] **Step 5: Create the service implementation**

`Application/Services/SwimmerService.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Domain.Repositories;

namespace Kheprx.BaseBackend.Identity.Application.Services;

internal sealed class SwimmerService : ISwimmerService
{
    private readonly ISwimmerProfileRepository _swimmers;

    public SwimmerService(ISwimmerProfileRepository swimmers)
    {
        _swimmers = swimmers;
    }

    public async Task<SwimmerCountDto> GetCountAsync(CancellationToken ct = default)
    {
        var count = await _swimmers.CountAsync(ct);
        return new SwimmerCountDto(count);
    }
}
```

- [ ] **Step 6: Create the localized messages**

`Application/Resources/SwimmerMessages.cs`:

```csharp
namespace Kheprx.BaseBackend.Identity.Application.Resources;

/// <summary>Localized messages for swimmer queries.</summary>
public static class SwimmerMessages
{
    public static class Success
    {
        public static string CountRetrieved(string lang) => lang switch
        {
            "ar" => "عدد السبّاحين",
            _ => "Swimmer count"
        };
    }
}
```

- [ ] **Step 7: Ensure DI registration is present**

Confirm `IdentityModuleExtensions.AddIdentityModule` contains (add if you deferred it from Task 3):

```csharp
        services.AddScoped<ISwimmerProfileRepository, SwimmerProfileRepository>();
        services.AddScoped<ISwimmerService, SwimmerService>();
```

- [ ] **Step 8: Run the test to verify it passes**

```bash
dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~SwimmerServiceTests" -v minimal --nologo
```

Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/SwimmerDtos.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/ISwimmerService.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Resources/SwimmerMessages.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs
git commit -m "feat(identity): add swimmer count service, DTO, and messages"
```

---

### Task 5: API — `SwimmersController`

**Files:**
- Create: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs`

**Interfaces:**
- Consumes: `ISwimmerService.GetCountAsync` (Task 4); `ApiResponse<T>.Success`; `AppLanguage.Current`.
- Produces: `GET /api/swimmers/count` → `ApiResponse<SwimmerCountDto>` (200, anonymous).

- [ ] **Step 1: Write the failing test**

`backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs`:

```csharp
using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class SwimmersControllerTests
{
    [Fact]
    public async Task Count_returns_200_with_swimmer_count()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.GetCountAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new SwimmerCountDto(24));

        var result = await new SwimmersController(svc.Object).Count(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<SwimmerCountDto>>(ok.Value);
        Assert.True(body.SuccessStatus);
        Assert.Equal(24, body.Data!.Count);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests/Kheprx.BaseBackend.Api.UnitTests.csproj --filter "FullyQualifiedName~SwimmersControllerTests" -v minimal --nologo
```

Expected: FAIL — `SwimmersController` does not exist.

- [ ] **Step 3: Create the controller**

`backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

public sealed class SwimmersController : BaseApiController
{
    #region Fields

    private readonly ISwimmerService _service;

    #endregion

    #region Constructor

    public SwimmersController(ISwimmerService service)
    {
        _service = service;
    }

    #endregion

    #region Count — GET api/swimmers/count — total tracked swimmers

    // مستخدم في:
    // 1. صفحة تسجيل الدخول (/login) — إحصائية عدد السبّاحين
    /// <summary>Returns the total number of tracked swimmers.</summary>
    /// <remarks>Anonymous: the login screen shows this stat before authentication.</remarks>
    /// <response code="200">The swimmer count.</response>
    [HttpGet("count")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<SwimmerCountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SwimmerCountDto>>> Count(CancellationToken ct)
    {
        var dto = await _service.GetCountAsync(ct);
        var successMessage = SwimmerMessages.Success.CountRetrieved(AppLanguage.Current);
        var body = ApiResponse<SwimmerCountDto>.Success(successMessage, dto);
        return Ok(body);
    }

    #endregion
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests/Kheprx.BaseBackend.Api.UnitTests.csproj --filter "FullyQualifiedName~SwimmersControllerTests" -v minimal --nologo
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs \
        backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs
git commit -m "feat(api): add anonymous GET /api/swimmers/count endpoint"
```

---

### Task 6: Seeder — 24 demo swimmers

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentitySeeder.cs`

**Interfaces:**
- Consumes: `SwimmerProfile` ctor (Task 2), `IdentityDbContext.SwimmerProfiles` (Task 3).

Verified by build + existing suites (the seeder is not unit-tested by codebase convention; its effect is confirmed at runtime in Task 7).

- [ ] **Step 1: Call the swimmer seeding from `SeedAsync`**

In `IdentitySeeder.SeedAsync`, after the final `await db.SaveChangesAsync(ct);` (following `EnsureCaptain`), add:

```csharp
        await EnsureSwimmers(db, ct);
        await db.SaveChangesAsync(ct);
```

- [ ] **Step 2: Add the `EnsureSwimmers` helper**

Add this private method to the `IdentitySeeder` class (next to `EnsureCaptain`):

```csharp
    private static async Task EnsureSwimmers(IdentityDbContext db, CancellationToken ct)
    {
        if (await db.SwimmerProfiles.AnyAsync(ct)) return; // idempotent — seed only when empty
        for (var i = 1; i <= 24; i++)
        {
            var uid = $"SW-{i:D4}";
            await db.SwimmerProfiles.AddAsync(new SwimmerProfile(uid, $"Swimmer {i:D2}", $"سبّاح {i:D2}"), ct);
        }
    }
```

(`Microsoft.EntityFrameworkCore` for `AnyAsync` and `Kheprx.BaseBackend.Identity.Domain.Entities` for `SwimmerProfile` are already imported in this file.)

- [ ] **Step 3: Build to verify it compiles**

```bash
dotnet build backend/Kheprx.BaseBackend.sln -v minimal --nologo
```

Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentitySeeder.cs
git commit -m "feat(identity): seed 24 demo swimmers (idempotent)"
```

---

### Task 7: EF migration + runtime bring-up

**Files:**
- Create (generated): `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Migrations/<timestamp>_AddSwimmerProfile.cs` (+ `.Designer.cs`)
- Modify (generated): `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Migrations/IdentityDbContextModelSnapshot.cs`

**Interfaces:** consumes the mapped model from Tasks 2–3.

- [ ] **Step 1: Ensure the dev server is stopped** (repeat Task 1 Step 1 if needed).

- [ ] **Step 2: Generate the migration**

```bash
dotnet ef migrations add AddSwimmerProfile \
  --project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --startup-project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure
```

- [ ] **Step 3: Verify the migration is clean (safety gate)**

Open the generated `<timestamp>_AddSwimmerProfile.cs`. Its `Up()` must contain **only**:
- one `migrationBuilder.CreateTable(name: "swimmer_profile", schema: "identity", …)` with columns `id`, `uid`, `name_en`, `name_ar`, `created_at`, and
- one `migrationBuilder.CreateIndex(...)` unique index on `uid`.

If `Up()` contains **any other operation** (alters/creates/drops on other tables), the model was out of sync — run the rollback and **STOP / consult the user**:

```bash
dotnet ef migrations remove \
  --project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --startup-project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure
```

- [ ] **Step 4: Build**

```bash
dotnet build backend/Kheprx.BaseBackend.sln -v minimal --nologo
```

Expected: build succeeds.

- [ ] **Step 5: Runtime bring-up — apply + seed + verify the endpoint**

Start the API (it applies migrations at startup via `MigrationExtensions.ApplyIdentityMigrationsAsync`, then runs the seeder):

```bash
dotnet run --project backend/Kheprx.BaseBackend.Api
```

In a second shell, hit the endpoint (dev HTTPS port is 7080; `-k` skips the dev cert check):

```bash
curl.exe -k https://localhost:7080/api/swimmers/count
```

Expected body: `{"successStatus":true,"message":"Swimmer count","error":null,"data":{"count":24}}` (field order may vary). Also confirm `/api/swimmers/count` appears in Swagger at `https://localhost:7080/swagger`. Stop the server when done (Ctrl+C) or leave it for the frontend step.

- [ ] **Step 6: Commit**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Migrations/
git commit -m "feat(identity): add AddSwimmerProfile migration"
```

---

### Task 8: Frontend — swimmers data slice

**Files:**
- Create: `frontend/src/app/features/swimmers/data/dto/swimmer-count.dto.ts`
- Create: `frontend/src/app/features/swimmers/domain/repositories/swimmer.repository.ts`
- Create: `frontend/src/app/features/swimmers/data/repositories/swimmer.repository.impl.ts`
- Create: `frontend/src/app/features/swimmers/data/swimmer.providers.ts`
- Modify: `frontend/src/app/app.config.ts`
- Test: `frontend/src/app/features/swimmers/testing/data/repositories/swimmer.repository.impl.spec.ts`

**Interfaces:**
- Produces: `SwimmerCountDtoRs { count: number }`, `SwimmerCountItemDtoRs`, `isSwimmerCountDtoRsValid`; `ISwimmerRepository.getCount(): Promise<SwimmerCountItemDtoRs>`; `SWIMMER_REPOSITORY` token; `SWIMMER_PROVIDERS`.

- [ ] **Step 1: Write the failing repository test**

`frontend/src/app/features/swimmers/testing/data/repositories/swimmer.repository.impl.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { SwimmerRepositoryImpl } from '@features/swimmers/data/repositories/swimmer.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('SwimmerRepositoryImpl', () => {
  const http = { get: jest.fn() } as unknown as HttpClientService;
  let repo: SwimmerRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [SwimmerRepositoryImpl, { provide: HttpClientService, useValue: http }],
    });
    repo = TestBed.inject(SwimmerRepositoryImpl);
  });

  it('getCount GETs the swimmer count envelope', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: { count: 24 } });
    await expect(repo.getCount()).resolves.toEqual({ data: { count: 24 } });
    expect(http.get).toHaveBeenCalledWith('/api/swimmers/count');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
cd frontend && npx jest src/app/features/swimmers --silent ; cd ..
```

Expected: FAIL — modules don't exist yet.

- [ ] **Step 3: Create the DTO**

`frontend/src/app/features/swimmers/data/dto/swimmer-count.dto.ts`:

```ts
// swimmer-count.dto.ts — swimmer count response DTO (API_FLOW convention).
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface SwimmerCountDtoRs {
  count: number;
}

export interface SwimmerCountItemDtoRs extends BaseResponseRs<SwimmerCountDtoRs> {}

export function isSwimmerCountDtoRsValid(dto: SwimmerCountDtoRs): boolean {
  return typeof dto.count === 'number' && Number.isFinite(dto.count) && dto.count >= 0;
}
```

- [ ] **Step 4: Create the repository port + token**

`frontend/src/app/features/swimmers/domain/repositories/swimmer.repository.ts`:

```ts
import { InjectionToken } from '@angular/core';
import { SwimmerCountItemDtoRs } from '@features/swimmers/data/dto/swimmer-count.dto';

export interface ISwimmerRepository {
  getCount(): Promise<SwimmerCountItemDtoRs>;
}

export const SWIMMER_REPOSITORY = new InjectionToken<ISwimmerRepository>('SWIMMER_REPOSITORY');
```

- [ ] **Step 5: Create the repository implementation**

`frontend/src/app/features/swimmers/data/repositories/swimmer.repository.impl.ts`:

```ts
// swimmer.repository.impl.ts — fetch-only swimmers repository. Returns the API DTO envelope.
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { ISwimmerRepository } from '@features/swimmers/domain/repositories/swimmer.repository';
import { SwimmerCountItemDtoRs } from '@features/swimmers/data/dto/swimmer-count.dto';

@Injectable({ providedIn: 'root' })
export class SwimmerRepositoryImpl implements ISwimmerRepository {
  private readonly http = inject(HttpClientService);

  getCount(): Promise<SwimmerCountItemDtoRs> {
    return this.http.get<SwimmerCountItemDtoRs>('/api/swimmers/count');
  }
}
```

- [ ] **Step 6: Create the DI providers and wire them into the app**

`frontend/src/app/features/swimmers/data/swimmer.providers.ts`:

```ts
import { Provider } from '@angular/core';
import { SWIMMER_REPOSITORY } from '@features/swimmers/domain/repositories/swimmer.repository';
import { SwimmerRepositoryImpl } from '@features/swimmers/data/repositories/swimmer.repository.impl';

// Live wiring: bind the swimmers repository port to the fetch-only impl (/api/swimmers/*).
export const SWIMMER_PROVIDERS: Provider[] = [
  { provide: SWIMMER_REPOSITORY, useClass: SwimmerRepositoryImpl },
];
```

In `frontend/src/app/app.config.ts`, add the import and spread it into `providers` next to `...AUTH_PROVIDERS`:

```ts
import { SWIMMER_PROVIDERS } from '@features/swimmers/data/swimmer.providers';
```
```ts
    ...AUTH_PROVIDERS,
    ...SWIMMER_PROVIDERS,
```

- [ ] **Step 7: Run the test to verify it passes**

```bash
cd frontend && npx jest src/app/features/swimmers --silent ; cd ..
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add frontend/src/app/features/swimmers/data/dto/swimmer-count.dto.ts \
        frontend/src/app/features/swimmers/domain/repositories/swimmer.repository.ts \
        frontend/src/app/features/swimmers/data/repositories/swimmer.repository.impl.ts \
        frontend/src/app/features/swimmers/data/swimmer.providers.ts \
        frontend/src/app/features/swimmers/testing/data/repositories/swimmer.repository.impl.spec.ts \
        frontend/src/app/app.config.ts
git commit -m "feat(swimmers): add frontend swimmers data slice and DI wiring"
```

---

### Task 9: Frontend — `LoadSwimmerCountUseCase`

**Files:**
- Create: `frontend/src/app/features/swimmers/domain/usecases/load-swimmer-count.use-case.ts`
- Test: `frontend/src/app/features/swimmers/testing/domain/usecases/load-swimmer-count.use-case.spec.ts`

**Interfaces:**
- Consumes: `SWIMMER_REPOSITORY`, `isSwimmerCountDtoRsValid` (Task 8).
- Produces: `LoadSwimmerCountUseCase extends UseCase<void, number>` with `run(): Promise<Result<number>>`.

- [ ] **Step 1: Write the failing test**

`frontend/src/app/features/swimmers/testing/domain/usecases/load-swimmer-count.use-case.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { LoadSwimmerCountUseCase } from '@features/swimmers/domain/usecases/load-swimmer-count.use-case';
import { SWIMMER_REPOSITORY, ISwimmerRepository } from '@features/swimmers/domain/repositories/swimmer.repository';

function makeRepo(overrides: Partial<ISwimmerRepository> = {}): ISwimmerRepository {
  return {
    getCount: async () => ({ data: { count: 24 } }),
    ...overrides,
  } as unknown as ISwimmerRepository;
}

function build(repo: ISwimmerRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_REPOSITORY, useValue: repo }] });
  return TestBed.inject(LoadSwimmerCountUseCase);
}

describe('LoadSwimmerCountUseCase', () => {
  it('validates + returns the count', async () => {
    const uc = build(makeRepo());
    const r = await uc.run();
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.data).toBe(24);
  });

  it('returns fail(validation) on a malformed count', async () => {
    const uc = build(makeRepo({ getCount: async () => ({ data: { count: -1 } }) }));
    const r = await uc.run();
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
cd frontend && npx jest src/app/features/swimmers/testing/domain --silent ; cd ..
```

Expected: FAIL — use case doesn't exist.

- [ ] **Step 3: Create the use case**

`frontend/src/app/features/swimmers/domain/usecases/load-swimmer-count.use-case.ts`:

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_REPOSITORY } from '@features/swimmers/domain/repositories/swimmer.repository';
import { isSwimmerCountDtoRsValid } from '@features/swimmers/data/dto/swimmer-count.dto';

// LoadSwimmerCountUseCase: fetches the total tracked-swimmer count (GET /api/swimmers/count)
// for the login hero stat. Anonymous endpoint, safe to call before sign-in.
@Injectable({ providedIn: 'root' })
export class LoadSwimmerCountUseCase extends UseCase<void, number> {
  private readonly repo = inject(SWIMMER_REPOSITORY);
  constructor() { super('LoadSwimmerCount'); }
  protected async execute(): Promise<number> {
    const res = await this.repo.getCount();
    if (!isSwimmerCountDtoRsValid(res.data)) throw new AppError('Invalid swimmer count received', 'validation');
    return res.data.count;
  }
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
cd frontend && npx jest src/app/features/swimmers/testing/domain --silent ; cd ..
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/features/swimmers/domain/usecases/load-swimmer-count.use-case.ts \
        frontend/src/app/features/swimmers/testing/domain/usecases/load-swimmer-count.use-case.spec.ts
git commit -m "feat(swimmers): add LoadSwimmerCountUseCase"
```

---

### Task 10: Frontend — wire the login hero stat

**Files:**
- Modify: `frontend/src/app/features/auth/presentation/pages/login/login.viewmodel.ts`
- Modify: `frontend/src/app/features/auth/presentation/pages/login/login.page.html`
- Modify: `frontend/src/app/features/auth/testing/presentation/pages/login/login.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `LoadSwimmerCountUseCase` (Task 9).
- Produces: `LoginViewModel.swimmerCount: Signal<number | null>` and `loadSwimmerCount(): Promise<void>`.

> Note: `LoginViewModel`'s constructor already calls `loadRoles()`. We add a parallel `loadSwimmerCount()`. Because the constructor now injects `LoadSwimmerCountUseCase`, the existing viewmodel spec MUST provide a mock for it (Step 1) or every existing test fails to construct the VM.

- [ ] **Step 1: Update the failing test (add provider + count assertion)**

In `login.viewmodel.spec.ts`:

Add the import:
```ts
import { LoadSwimmerCountUseCase } from '@features/swimmers/domain/usecases/load-swimmer-count.use-case';
```

Add a module-level mock next to the existing `loadRoles` mock:
```ts
  const loadSwimmerCount = { run: jest.fn().mockResolvedValue(ok(24)) };
```

In `beforeEach`, after the existing `(loadRoles.run as jest.Mock).mockResolvedValue(ok([]));` line, reset it and register the provider:
```ts
    (loadSwimmerCount.run as jest.Mock).mockResolvedValue(ok(24));
```
```ts
        { provide: LoadSwimmerCountUseCase, useValue: loadSwimmerCount },
```
(add that provider inside the `providers: [...]` array alongside `{ provide: LoadRolesUseCase, useValue: loadRoles }`.)

Add a new test:
```ts
  it('loads the swimmer count into the signal', async () => {
    (loadSwimmerCount.run as jest.Mock).mockResolvedValue(ok(24));
    await vm.loadSwimmerCount();
    expect(vm.swimmerCount()).toBe(24);
  });
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
cd frontend && npx jest src/app/features/auth/testing/presentation/pages/login --silent ; cd ..
```

Expected: FAIL — `LoadSwimmerCountUseCase` not injectable in the VM / `loadSwimmerCount`/`swimmerCount` don't exist.

- [ ] **Step 3: Update the viewmodel**

In `login.viewmodel.ts`:

Add the imports:
```ts
import { LoadSwimmerCountUseCase } from '@features/swimmers/domain/usecases/load-swimmer-count.use-case';
```

Add the injected dependency (next to `loadRolesUseCase`):
```ts
  private readonly loadSwimmerCountUseCase = inject(LoadSwimmerCountUseCase);
```

Add the signal (next to `roles`/`selectedRole`):
```ts
  readonly swimmerCount = signal<number | null>(null);
```

In the constructor, add the load call next to `void this.loadRoles();`:
```ts
    void this.loadSwimmerCount();
```

Add the method (next to `loadRoles`):
```ts
  async loadSwimmerCount(): Promise<void> {
    const r = await this.loadSwimmerCountUseCase.run();
    if (r.ok) this.swimmerCount.set(r.data);
  }
```

- [ ] **Step 4: Replace the hardcoded number in the template**

In `login.page.html`, replace:
```html
        <p class="tabular text-2xl font-semibold">452</p>
```
with:
```html
        <p class="tabular text-2xl font-semibold">{{ vm.swimmerCount() ?? 452 }}</p>
```

- [ ] **Step 5: Run the login tests to verify they pass**

```bash
cd frontend && npx jest src/app/features/auth/testing/presentation/pages/login --silent ; cd ..
```

Expected: PASS (existing tests + the new count test).

- [ ] **Step 6: Full frontend suite + AOT build**

```bash
cd frontend && npx jest --silent && npx ng build --configuration development ; cd ..
```

Expected: all tests pass; the build succeeds (template type-checks).

- [ ] **Step 7: Commit**

```bash
git add frontend/src/app/features/auth/presentation/pages/login/login.viewmodel.ts \
        frontend/src/app/features/auth/presentation/pages/login/login.page.html \
        frontend/src/app/features/auth/testing/presentation/pages/login/login.viewmodel.spec.ts
git commit -m "feat(login): show live swimmer count with 452 fallback"
```

---

### Task 11: Final end-to-end verification

**Files:** none (verification only)

- [ ] **Step 1: Full backend suite**

```bash
dotnet test backend/Kheprx.BaseBackend.sln -v minimal --nologo
```

Expected: all pass, including `SwimmerProfileTests`, `SwimmerServiceTests`, `SwimmersControllerTests`, and no regressions in the baseline from Task 1.

- [ ] **Step 2: Run the app and confirm the login page shows the live number**

```bash
dotnet run --project backend/Kheprx.BaseBackend.Api
# separate shell:
cd frontend && npx ng serve
```

Open `http://localhost:4200/login` and confirm the hero stat reads **24** (not 452). Stop both servers when done (or leave the API for the user to restart, per their preference).

- [ ] **Step 3: Restart note**

Remind the user the dev API server was stopped/started during this work and confirm the final desired state (running or stopped).

---

## Self-Review

**Spec coverage:**
- Minimal `swimmer_profile` (Id/Uid/NameEn/NameAr/CreatedAt, no external FKs) → Tasks 2–3, 7. ✔
- Anonymous `GET /api/swimmers/count` returning `{ count }` → Tasks 4–5. ✔
- Seed 24 idempotent swimmers → Task 6. ✔
- Migration with clean-diff safety gate → Tasks 1 (preflight) + 7 (inspect `Up()`). ✔
- Frontend slice mirroring roles + login hero wired with 452 fallback → Tasks 8–10. ✔
- Testing (backend service + controller; frontend use case + repo + viewmodel) → Tasks 2,4,5,8,9,10. ✔
- Placement in Identity module / `identity` schema → Tasks 2–7 (default schema). ✔

**Placeholder scan:** No TBD/TODO; every code step has literal content; no "similar to Task N" without code. ✔

**Type consistency:** `SwimmerCountDto(int Count)` ↔ frontend `SwimmerCountDtoRs { count }`; `ISwimmerProfileRepository.CountAsync` used identically in Task 3 (impl) and Task 4 (service test mock); `ISwimmerService.GetCountAsync` used in Task 4 (impl) and Task 5 (controller); `SWIMMER_REPOSITORY`/`ISwimmerRepository.getCount` consistent across Tasks 8–9; `LoadSwimmerCountUseCase` consistent across Tasks 9–10. ✔

**Known deferrals (from spec non-goals):** no club/blood_type tables, no swimmer app_user/role, no swimmer CRUD — intentionally out of scope.
