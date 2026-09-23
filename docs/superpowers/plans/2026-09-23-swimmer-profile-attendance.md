# Swimmer Profile — Attendance tab (read-only) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a read-only **Attendance** tab to the swimmer profile page — a monthly calendar colored by attendance status with an attendance-rate figure and a per-day coach-note detail — backed by a new `Attendance` backend module, plus Aiven seed data for every swimmer.

**Architecture:** New backend `Attendance` module (own bounded context + `attendance` schema) exposes a flat read endpoint `GET /api/attendance-records?swimmerId={id}`; the `reference.attendance_status` lookup lives in the Identity module and is served via `GET /api/reference/attendance-statuses`. The frontend enables the already-stubbed `attendance` tab in the `swimmer-profile` slice, resolves status codes client-side, and computes the calendar in the viewmodel. Recorder display names are enriched at the API layer by reusing Identity's `IUserService.GetDisplayNamesAsync`.

**Tech Stack:** .NET 10 (ASP.NET Core, EF Core + Npgsql, xUnit + Moq + EF InMemory), Angular 20 (standalone components, signals, Jasmine/Karma), PostgreSQL (Aiven), Tailwind.

**Spec:** `docs/superpowers/specs/2026-09-23-swimmer-profile-attendance-design.md`

## Global Constraints

- **No commits until the user explicitly says so.** Do all work in the working tree; do not `git commit`/`git push`.
- **`dotnet ef` / `dotnet build` require the running API stopped** (it locks the built DLLs). Stop any running backend before EF/build steps.
- **Loose Guids, no cross-module FKs** — `attendance_record.swimmer_id`, `.status_id`, `.recorded_by` are plain `Guid` columns with no EF navigation/FK (matches `feedback_entry`, `observation`).
- **EF column naming is PascalCase, quoted in raw SQL** — tables are snake_case (`attendance_record`), columns are `"Id"`, `"SwimmerId"`, `"SessionDate"`, `"StatusId"`, `"RecordedBy"`, `"CoachNoteEn"`, `"CoachNoteAr"`. Raw SQL must double-quote them.
- **i18n is bilingual** — every user-facing string added to `en.json` must have an `ar.json` counterpart at the same key path.
- **`ApiResponse<T>` envelope** — all controllers return `ApiResponse<T>.Success(message, data)` with a localized message via `AppLanguage.Current`.
- **Spec refinement (statuses seeding):** the spec said "seed statuses via migration `HasData`". The codebase convention is to seed reference rows in `IdentitySeeder` (migrations only create tables). This plan follows the convention: statuses are seeded in `IdentitySeeder` for local dev **and** inserted by the Aiven SQL script (self-contained), because the runtime app seeds local, not Aiven.

---

## File map

**Backend — Identity module (`reference.attendance_status`)**
- Create: `src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/AttendanceStatus.cs`
- Create: `src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/IAttendanceStatusRepository.cs`
- Create: `src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/AttendanceStatusConfiguration.cs`
- Create: `src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/AttendanceStatusRepository.cs`
- Modify: `…Identity.Infrastructure/Data/IdentityDbContext.cs` (add `DbSet`)
- Modify: `…Identity.Infrastructure/Data/IdentitySeeder.cs` (seed 4 statuses)
- Modify: `…Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs` (register repo)
- Modify: `…Identity.Application/Services/Interfaces/IReferenceService.cs` (+ method)
- Modify: `…Identity.Application/Services/ReferenceService.cs` (+ impl + ctor dep)
- Modify: `…Identity.Application/Resources/ReferenceMessages.cs` (+ message)
- Modify: `Kheprx.BaseBackend.Api/Controllers/ReferenceController.cs` (+ action)
- Migration: `…Identity.Infrastructure/Migrations` (new `CreateAttendanceStatusTable`)

**Backend — new `Attendance` module (`attendance.attendance_record`)**
- Create projects under `src/Modules/Attendance/`: `.Domain`, `.Application`, `.Contracts`, `.Infrastructure` (+ `tests/Kheprx.BaseBackend.Attendance.UnitTests`)
- Domain: `Entities/AttendanceRecord.cs`, `Repositories/IAttendanceRecordRepository.cs`
- Application: `DTOs/AttendanceRecordDtos.cs`, `Services/Interfaces/IAttendanceService.cs`, `Services/AttendanceService.cs`, `Resources/AttendanceMessages.cs`
- Infrastructure: `Data/AttendanceDbContext.cs`, `Data/AttendanceDbContextFactory.cs`, `Configurations/AttendanceRecordConfiguration.cs`, `Repositories/AttendanceRecordRepository.cs`, `Extensions/AttendanceModuleExtensions.cs`, `Migrations` (new `CreateAttendanceRecordTable`)
- Modify: `Kheprx.BaseBackend.sln`, `Kheprx.BaseBackend.Api/Kheprx.BaseBackend.Api.csproj`, `Kheprx.BaseBackend.Api/Program.cs`, `Kheprx.BaseBackend.Api/Extensions/Data/MigrationExtensions.cs`
- Create: `Kheprx.BaseBackend.Api/Controllers/AttendanceRecordsController.cs`

**Frontend (`frontend/src/app`)**
- Reference feature: modify `features/reference/domain/repositories/reference.repository.ts`, `features/reference/data/repositories/reference.repository.impl.ts`, `features/reference/index.ts`; create `features/reference/domain/usecases/load-attendance-statuses.use-case.ts`
- swimmer-profile data: create `features/swimmer-profile/data/dto/attendance-record.dto.ts`, `…/data/dto/attendance-record.mapper.ts`, `…/domain/model/attendance-record.ts`, `…/domain/usecases/list-attendance-records.use-case.ts`; modify `…/domain/repositories/swimmer-profile.repository.ts`, `…/data/repositories/swimmer-profile.repository.impl.ts`
- swimmer-profile presentation: modify `…/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.ts`, `…/swimmer-profile.page.ts`, `…/swimmer-profile.page.html`
- i18n: modify `features/... core/i18n/en.json` + `ar.json`
- Tests under `features/**/testing/**`

**Seeding**
- Create: `scripts/seed-attendance-aiven.sql`

---

## Task 1: Identity — `reference.attendance_status` lookup + endpoint

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/AttendanceStatus.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/IAttendanceStatusRepository.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/AttendanceStatusConfiguration.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/AttendanceStatusRepository.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentityDbContext.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentitySeeder.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/IReferenceService.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/ReferenceService.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Resources/ReferenceMessages.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/ReferenceController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/AttendanceStatusTests.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/ReferenceServiceAttendanceStatusTests.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/ReferenceControllerAttendanceStatusesTests.cs`

**Interfaces:**
- Produces: `AttendanceStatus(string code, string nameEn, string? nameAr)`; `IAttendanceStatusRepository.GetAllAsync(CancellationToken) → IReadOnlyList<AttendanceStatus>`; `IReferenceService.GetAttendanceStatusesAsync(CancellationToken) → IReadOnlyList<CodedLookupDto>`; `GET /api/reference/attendance-statuses`.
- Consumes: existing `CodedLookupDto(Guid Id, string Code, string NameEn, string? NameAr)`.

- [ ] **Step 1: Write the failing entity test**

Create `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/AttendanceStatusTests.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class AttendanceStatusTests
{
    [Fact]
    public void Constructor_trims_and_assigns_fields()
    {
        var s = new AttendanceStatus(" present ", " Present ", " حاضر ");
        Assert.NotEqual(Guid.Empty, s.Id);
        Assert.Equal("present", s.Code);
        Assert.Equal("Present", s.NameEn);
        Assert.Equal("حاضر", s.NameAr);
    }

    [Fact]
    public void Constructor_nulls_blank_arabic()
    {
        var s = new AttendanceStatus("absent", "Absent", "   ");
        Assert.Null(s.NameAr);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd backend && dotnet test tests/Kheprx.BaseBackend.Identity.UnitTests --filter AttendanceStatusTests`
Expected: FAIL — `AttendanceStatus` does not exist (compile error).

- [ ] **Step 3: Create the entity**

Create `…Identity.Domain/Entities/AttendanceStatus.cs` (clone of `FeedbackCategory.cs`):

```csharp
namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class AttendanceStatus
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }

    private AttendanceStatus() { } // EF Core

    public AttendanceStatus(string code, string nameEn, string? nameAr = null)
    {
        Id = Guid.NewGuid();
        Code = code.Trim();
        NameEn = nameEn.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
    }
}
```

- [ ] **Step 4: Run the entity test — expect PASS**

Run: `cd backend && dotnet test tests/Kheprx.BaseBackend.Identity.UnitTests --filter AttendanceStatusTests`
Expected: PASS.

- [ ] **Step 5: Add repository interface, config, impl, DbSet, DI**

Create `…Identity.Domain/Repositories/IAttendanceStatusRepository.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
namespace Kheprx.BaseBackend.Identity.Domain.Repositories;
public interface IAttendanceStatusRepository
{
    Task<IReadOnlyList<AttendanceStatus>> GetAllAsync(CancellationToken ct = default);
}
```

Create `…Identity.Infrastructure/Configurations/AttendanceStatusConfiguration.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class AttendanceStatusConfiguration : IEntityTypeConfiguration<AttendanceStatus>
{
    public void Configure(EntityTypeBuilder<AttendanceStatus> builder)
    {
        builder.ToTable("attendance_status", "reference");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(s => s.Code).IsUnique();
        builder.Property(s => s.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(s => s.NameAr).HasMaxLength(100);
    }
}
```

Create `…Identity.Infrastructure/Repositories/AttendanceStatusRepository.cs` (clone of `FeedbackCategoryRepository`):

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class AttendanceStatusRepository : IAttendanceStatusRepository
{
    private readonly IdentityDbContext _db;
    public AttendanceStatusRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<AttendanceStatus>> GetAllAsync(CancellationToken ct = default)
        => await _db.AttendanceStatuses.AsNoTracking().OrderBy(s => s.Code).ToListAsync(ct);
}
```

In `IdentityDbContext.cs`, add after the `FeedbackCategories` DbSet (line ~24):

```csharp
    public DbSet<AttendanceStatus> AttendanceStatuses => Set<AttendanceStatus>();
```

In `IdentityModuleExtensions.cs`, add after the `IFeedbackCategoryRepository` registration (line ~41):

```csharp
        services.AddScoped<IAttendanceStatusRepository, AttendanceStatusRepository>();
```

- [ ] **Step 6: Seed the 4 statuses in IdentitySeeder**

In `IdentitySeeder.cs`, add a call inside `SeedAsync` right after `await EnsureFeedbackCategories(db, ct);` (line ~26):

```csharp
        await EnsureAttendanceStatuses(db, ct);
```

And add this method next to `EnsureFeedbackCategories`:

```csharp
    private static async Task EnsureAttendanceStatuses(IdentityDbContext db, CancellationToken ct)
    {
        var rows = new (string Code, string En, string Ar)[]
        {
            ("present", "Present", "حاضر"),
            ("late", "Late", "متأخر"),
            ("absent", "Absent", "غائب"),
            ("excused", "Excused", "بعذر"),
        };
        foreach (var (code, en, ar) in rows)
            if (!await db.AttendanceStatuses.AnyAsync(s => s.Code == code, ct))
                await db.AttendanceStatuses.AddAsync(new AttendanceStatus(code, en, ar), ct);
    }
```

- [ ] **Step 7: Write the failing reference-service test**

Create `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/ReferenceServiceAttendanceStatusTests.cs`. Note `ReferenceService`'s constructor takes 7 repos; mock all, and stub only the attendance one:

```csharp
using Kheprx.BaseBackend.Identity.Application.Services;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Services;

public class ReferenceServiceAttendanceStatusTests
{
    [Fact]
    public async Task GetAttendanceStatusesAsync_maps_rows_to_coded_lookups()
    {
        var attendance = new Mock<IAttendanceStatusRepository>();
        attendance.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new AttendanceStatus("present", "Present", "حاضر") });

        var svc = new ReferenceService(
            Mock.Of<IClubRepository>(), Mock.Of<IBloodTypeRepository>(), Mock.Of<IStrokeRepository>(),
            Mock.Of<IGenderRepository>(), Mock.Of<IObservationCategoryRepository>(),
            Mock.Of<IFitnessAssessmentRepository>(), Mock.Of<IFeedbackCategoryRepository>(),
            attendance.Object);

        var result = await svc.GetAttendanceStatusesAsync();

        Assert.Single(result);
        Assert.Equal("present", result[0].Code);
        Assert.Equal("Present", result[0].NameEn);
    }
}
```

- [ ] **Step 8: Run it to verify it fails**

Run: `cd backend && dotnet test tests/Kheprx.BaseBackend.Identity.UnitTests --filter ReferenceServiceAttendanceStatusTests`
Expected: FAIL — `GetAttendanceStatusesAsync` missing and the constructor has 7 params, not 8.

- [ ] **Step 9: Extend `IReferenceService` + `ReferenceService` + messages**

In `IReferenceService.cs` add:

```csharp
    Task<IReadOnlyList<CodedLookupDto>> GetAttendanceStatusesAsync(CancellationToken ct = default);
```

In `ReferenceService.cs`: add field `private readonly IAttendanceStatusRepository _attendanceStatuses;`, add the constructor parameter `IAttendanceStatusRepository attendanceStatuses` (append last) and assign `_attendanceStatuses = attendanceStatuses;`, then add:

```csharp
    public async Task<IReadOnlyList<CodedLookupDto>> GetAttendanceStatusesAsync(CancellationToken ct = default)
        => (await _attendanceStatuses.GetAllAsync(ct)).Select(s => new CodedLookupDto(s.Id, s.Code, s.NameEn, s.NameAr)).ToList();
```

In `ReferenceMessages.cs` add to `Success`:

```csharp
        public static string AttendanceStatusesListed(string lang) => lang switch { "ar" => "حالات الحضور", _ => "Attendance statuses" };
```

- [ ] **Step 10: Run the service test — expect PASS**

Run: `cd backend && dotnet test tests/Kheprx.BaseBackend.Identity.UnitTests --filter ReferenceServiceAttendanceStatusTests`
Expected: PASS.

- [ ] **Step 11: Write the failing controller test**

Create `backend/tests/Kheprx.BaseBackend.Api.UnitTests/ReferenceControllerAttendanceStatusesTests.cs`:

```csharp
using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class ReferenceControllerAttendanceStatusesTests
{
    [Fact]
    public async Task AttendanceStatuses_returns_200_with_data()
    {
        var svc = new Mock<IReferenceService>();
        svc.Setup(s => s.GetAttendanceStatusesAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new[] { new CodedLookupDto(Guid.NewGuid(), "present", "Present", "حاضر") });
        var controller = new ReferenceController(svc.Object);

        var result = await controller.AttendanceStatuses(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<CodedLookupDto>>>(ok.Value);
        Assert.Single(body.Data!);
        Assert.Equal("present", body.Data![0].Code);
    }
}
```

- [ ] **Step 12: Run it to verify it fails**

Run: `cd backend && dotnet test tests/Kheprx.BaseBackend.Api.UnitTests --filter ReferenceControllerAttendanceStatusesTests`
Expected: FAIL — `AttendanceStatuses` action missing.

- [ ] **Step 13: Add the controller action**

In `ReferenceController.cs`, add after the `FeedbackCategories` action (clone it):

```csharp
    /// <summary>Lists all attendance statuses (present / late / absent / excused).</summary>
    [HttpGet("attendance-statuses")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CodedLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CodedLookupDto>>>> AttendanceStatuses(CancellationToken ct)
    {
        var data = await _service.GetAttendanceStatusesAsync(ct);
        var body = ApiResponse<IReadOnlyList<CodedLookupDto>>.Success(
            ReferenceMessages.Success.AttendanceStatusesListed(AppLanguage.Current), data);
        return Ok(body);
    }
```

- [ ] **Step 14: Run controller test — expect PASS**

Run: `cd backend && dotnet test tests/Kheprx.BaseBackend.Api.UnitTests --filter ReferenceControllerAttendanceStatusesTests`
Expected: PASS.

- [ ] **Step 15: Create the Identity migration (stop the API first)**

Run:
```bash
cd backend
dotnet ef migrations add CreateAttendanceStatusTable \
  --context IdentityDbContext \
  --project src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --startup-project Kheprx.BaseBackend.Api
```
Expected: a new migration under `…Identity.Infrastructure/Migrations` that creates `reference.attendance_status` (`"Id"`, `"Code"`, `"NameEn"`, `"NameAr"`) with a unique index on `"Code"`. Open it and confirm it only creates that table (no unrelated model drift). Then build:
`dotnet build Kheprx.BaseBackend.sln` → Expected: Build succeeded.

- [ ] **Step 16: Run the whole Identity + Api unit-test suites, then commit-gate**

Run: `cd backend && dotnet test tests/Kheprx.BaseBackend.Identity.UnitTests && dotnet test tests/Kheprx.BaseBackend.Api.UnitTests`
Expected: PASS. **Do not commit** (see Global Constraints).

---

## Task 2: Scaffold the `Attendance` module + `AttendanceRecord` persistence

**Files:**
- Create projects (`.csproj` + code) under `backend/src/Modules/Attendance/`:
  - `Kheprx.BaseBackend.Attendance.Domain/` → `Kheprx.BaseBackend.Attendance.Domain.csproj`, `Entities/AttendanceRecord.cs`, `Repositories/IAttendanceRecordRepository.cs`
  - `Kheprx.BaseBackend.Attendance.Contracts/` → `Kheprx.BaseBackend.Attendance.Contracts.csproj` (empty, matches Health.Contracts)
  - `Kheprx.BaseBackend.Attendance.Application/` → `.csproj` (created in Task 3)
  - `Kheprx.BaseBackend.Attendance.Infrastructure/` → `.csproj`, `Data/AttendanceDbContext.cs`, `Data/AttendanceDbContextFactory.cs`, `Configurations/AttendanceRecordConfiguration.cs`, `Repositories/AttendanceRecordRepository.cs`, `Extensions/AttendanceModuleExtensions.cs`
- Create: `backend/tests/Kheprx.BaseBackend.Attendance.UnitTests/` → `.csproj`, `Entities/AttendanceRecordTests.cs`, `Repositories/AttendanceRecordRepositoryTests.cs`
- Modify: `backend/Kheprx.BaseBackend.sln`, `backend/Kheprx.BaseBackend.Api/Kheprx.BaseBackend.Api.csproj`, `backend/Kheprx.BaseBackend.Api/Program.cs`, `backend/Kheprx.BaseBackend.Api/Extensions/Data/MigrationExtensions.cs`
- Migration: `…Attendance.Infrastructure/Migrations` (new `CreateAttendanceRecordTable`)

**Interfaces:**
- Produces: `AttendanceRecord(Guid swimmerId, DateOnly sessionDate, Guid statusId, Guid recordedBy, string? coachNoteEn = null, string? coachNoteAr = null)` with read-only properties `Id, SwimmerId, SessionDate, StatusId, RecordedBy, CoachNoteEn, CoachNoteAr`; `IAttendanceRecordRepository.ListBySwimmerAsync(Guid swimmerId, CancellationToken) → IReadOnlyList<AttendanceRecord>`; `AttendanceDbContext` with `DbSet<AttendanceRecord> AttendanceRecords`; `AddAttendanceModule(IServiceCollection, IConfiguration)`.

- [ ] **Step 1: Scaffold the four `Attendance` module projects**

Create the project files (mirror the Health module `.csproj`s; namespaces swap `Health`→`Attendance`).

`src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Domain/Kheprx.BaseBackend.Attendance.Domain.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\..\SharedKernel\Kheprx.BaseBackend.SharedKernel\Kheprx.BaseBackend.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

`src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Contracts/Kheprx.BaseBackend.Attendance.Contracts.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

</Project>
```

`src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Application/Kheprx.BaseBackend.Attendance.Application.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);1591</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Kheprx.BaseBackend.Attendance.Domain\Kheprx.BaseBackend.Attendance.Domain.csproj" />
    <ProjectReference Include="..\..\..\SharedKernel\Kheprx.BaseBackend.SharedKernel\Kheprx.BaseBackend.SharedKernel.csproj" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Kheprx.BaseBackend.Attendance.Infrastructure" />
    <InternalsVisibleTo Include="Kheprx.BaseBackend.Attendance.UnitTests" />
  </ItemGroup>
</Project>
```

`src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Infrastructure/Kheprx.BaseBackend.Attendance.Infrastructure.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Kheprx.BaseBackend.Attendance.Domain\Kheprx.BaseBackend.Attendance.Domain.csproj" />
    <ProjectReference Include="..\Kheprx.BaseBackend.Attendance.Application\Kheprx.BaseBackend.Attendance.Application.csproj" />
    <ProjectReference Include="..\Kheprx.BaseBackend.Attendance.Contracts\Kheprx.BaseBackend.Attendance.Contracts.csproj" />
    <ProjectReference Include="..\..\..\SharedKernel\Kheprx.BaseBackend.SharedKernel\Kheprx.BaseBackend.SharedKernel.csproj" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Kheprx.BaseBackend.Attendance.UnitTests" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the entity, repository interface, config, DbContext, factory, repository, module extension**

`…Attendance.Domain/Entities/AttendanceRecord.cs`:
```csharp
namespace Kheprx.BaseBackend.Attendance.Domain.Entities;

public sealed class AttendanceRecord
{
    public Guid Id { get; private set; }
    public Guid SwimmerId { get; private set; }
    public DateOnly SessionDate { get; private set; }
    public Guid StatusId { get; private set; }
    public Guid RecordedBy { get; private set; }
    public string? CoachNoteEn { get; private set; }
    public string? CoachNoteAr { get; private set; }

    private AttendanceRecord() { } // EF Core

    public AttendanceRecord(Guid swimmerId, DateOnly sessionDate, Guid statusId, Guid recordedBy,
        string? coachNoteEn = null, string? coachNoteAr = null)
    {
        Id = Guid.NewGuid();
        SwimmerId = swimmerId;
        SessionDate = sessionDate;
        StatusId = statusId;
        RecordedBy = recordedBy;
        CoachNoteEn = string.IsNullOrWhiteSpace(coachNoteEn) ? null : coachNoteEn.Trim();
        CoachNoteAr = string.IsNullOrWhiteSpace(coachNoteAr) ? null : coachNoteAr.Trim();
    }
}
```

`…Attendance.Domain/Repositories/IAttendanceRecordRepository.cs`:
```csharp
using Kheprx.BaseBackend.Attendance.Domain.Entities;
namespace Kheprx.BaseBackend.Attendance.Domain.Repositories;
public interface IAttendanceRecordRepository
{
    Task<IReadOnlyList<AttendanceRecord>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default);
}
```

`…Attendance.Infrastructure/Data/AttendanceDbContext.cs`:
```csharp
using Kheprx.BaseBackend.Attendance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Attendance.Infrastructure.Data;

public sealed class AttendanceDbContext : DbContext
{
    public AttendanceDbContext(DbContextOptions<AttendanceDbContext> options) : base(options) { }

    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("attendance");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AttendanceDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

`…Attendance.Infrastructure/Data/AttendanceDbContextFactory.cs` (clone of `HealthDbContextFactory`):
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kheprx.BaseBackend.Attendance.Infrastructure.Data;

// Design-time factory used by `dotnet ef`. Uses a local-dev connection string;
// at runtime the application uses ConnectionStrings:Postgres from configuration instead.
public sealed class AttendanceDbContextFactory : IDesignTimeDbContextFactory<AttendanceDbContext>
{
    public AttendanceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AttendanceDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=basebackend;Username=postgres;Password=postgres")
            .Options;
        return new AttendanceDbContext(options);
    }
}
```

`…Attendance.Infrastructure/Configurations/AttendanceRecordConfiguration.cs`:
```csharp
using Kheprx.BaseBackend.Attendance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Attendance.Infrastructure.Configurations;

internal sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("attendance_record", "attendance");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.SwimmerId).IsRequired();     // loose Guid — no cross-module FK
        builder.Property(e => e.SessionDate).IsRequired();   // DateOnly → date
        builder.Property(e => e.StatusId).IsRequired();      // loose Guid → reference.attendance_status
        builder.Property(e => e.RecordedBy).IsRequired();    // loose Guid → identity.app_user
        builder.Property(e => e.CoachNoteEn);
        builder.Property(e => e.CoachNoteAr);
        builder.HasIndex(e => new { e.SwimmerId, e.SessionDate }).IsUnique();
    }
}
```

`…Attendance.Infrastructure/Repositories/AttendanceRecordRepository.cs`:
```csharp
using Kheprx.BaseBackend.Attendance.Domain.Entities;
using Kheprx.BaseBackend.Attendance.Domain.Repositories;
using Kheprx.BaseBackend.Attendance.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Attendance.Infrastructure.Repositories;

internal sealed class AttendanceRecordRepository : IAttendanceRecordRepository
{
    private readonly AttendanceDbContext _db;
    public AttendanceRecordRepository(AttendanceDbContext db) => _db = db;

    public async Task<IReadOnlyList<AttendanceRecord>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default)
        => await _db.AttendanceRecords.AsNoTracking()
              .Where(r => r.SwimmerId == swimmerId)
              .OrderByDescending(r => r.SessionDate).ThenByDescending(r => r.Id)
              .ToListAsync(ct);
}
```

`…Attendance.Infrastructure/Extensions/AttendanceModuleExtensions.cs` (service registrations for `IAttendanceService`/repo — the service type is added in Task 3; include the repo now and add the service line in Task 3):
```csharp
using Kheprx.BaseBackend.Attendance.Domain.Repositories;
using Kheprx.BaseBackend.Attendance.Infrastructure.Data;
using Kheprx.BaseBackend.Attendance.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kheprx.BaseBackend.Attendance.Infrastructure.Extensions;

public static class AttendanceModuleExtensions
{
    public static IServiceCollection AddAttendanceModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AttendanceDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddScoped<IAttendanceRecordRepository, AttendanceRecordRepository>();
        return services;
    }
}
```

- [ ] **Step 3: Add the four projects to the solution + reference them from the API**

Run (from `backend/`), adding each project under the `Modules` solution folder:
```bash
cd backend
dotnet sln Kheprx.BaseBackend.sln add \
  src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Domain/Kheprx.BaseBackend.Attendance.Domain.csproj \
  src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Application/Kheprx.BaseBackend.Attendance.Application.csproj \
  src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Contracts/Kheprx.BaseBackend.Attendance.Contracts.csproj \
  src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Infrastructure/Kheprx.BaseBackend.Attendance.Infrastructure.csproj \
  --solution-folder Modules
dotnet add Kheprx.BaseBackend.Api/Kheprx.BaseBackend.Api.csproj reference \
  src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Application/Kheprx.BaseBackend.Attendance.Application.csproj \
  src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Infrastructure/Kheprx.BaseBackend.Attendance.Infrastructure.csproj
```
(If your `dotnet` build doesn't support `--solution-folder`, drop that flag — placement in the solution tree is cosmetic.)

- [ ] **Step 4: Register the module + migrations at startup**

In `Program.cs`: add `using Kheprx.BaseBackend.Attendance.Infrastructure.Extensions;` and, after `builder.Services.AddHealthModule(builder.Configuration);` (line ~21):
```csharp
builder.Services.AddAttendanceModule(builder.Configuration);
```
And after `await app.ApplyHealthMigrationsAsync();` (line ~33):
```csharp
await app.ApplyAttendanceMigrationsAsync();
```

In `MigrationExtensions.cs`: add `using Kheprx.BaseBackend.Attendance.Infrastructure.Data;` and this method (no seeder — records are seeded via SQL/local seeder elsewhere):
```csharp
    public static async Task ApplyAttendanceMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
        await db.Database.MigrateAsync();
    }
```

- [ ] **Step 5: Create the test project + failing entity test**

`backend/tests/Kheprx.BaseBackend.Attendance.UnitTests/Kheprx.BaseBackend.Attendance.UnitTests.csproj` (clone of Health.UnitTests.csproj, refs swapped):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Moq" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Modules\Attendance\Kheprx.BaseBackend.Attendance.Domain\Kheprx.BaseBackend.Attendance.Domain.csproj" />
    <ProjectReference Include="..\..\src\Modules\Attendance\Kheprx.BaseBackend.Attendance.Application\Kheprx.BaseBackend.Attendance.Application.csproj" />
    <ProjectReference Include="..\..\src\Modules\Attendance\Kheprx.BaseBackend.Attendance.Infrastructure\Kheprx.BaseBackend.Attendance.Infrastructure.csproj" />
    <ProjectReference Include="..\..\src\SharedKernel\Kheprx.BaseBackend.SharedKernel\Kheprx.BaseBackend.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```
Add it to the solution: `dotnet sln Kheprx.BaseBackend.sln add tests/Kheprx.BaseBackend.Attendance.UnitTests/Kheprx.BaseBackend.Attendance.UnitTests.csproj --solution-folder tests`

`…Attendance.UnitTests/Entities/AttendanceRecordTests.cs`:
```csharp
using Kheprx.BaseBackend.Attendance.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Attendance.UnitTests.Entities;

public class AttendanceRecordTests
{
    [Fact]
    public void Constructor_assigns_fields_and_generates_id()
    {
        var swimmer = Guid.NewGuid(); var status = Guid.NewGuid(); var coach = Guid.NewGuid();
        var r = new AttendanceRecord(swimmer, new DateOnly(2026, 9, 9), status, coach, "note", "ملاحظة");
        Assert.NotEqual(Guid.Empty, r.Id);
        Assert.Equal(swimmer, r.SwimmerId);
        Assert.Equal(new DateOnly(2026, 9, 9), r.SessionDate);
        Assert.Equal(status, r.StatusId);
        Assert.Equal(coach, r.RecordedBy);
        Assert.Equal("note", r.CoachNoteEn);
        Assert.Equal("ملاحظة", r.CoachNoteAr);
    }

    [Fact]
    public void Constructor_nulls_blank_notes()
    {
        var r = new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 9), Guid.NewGuid(), Guid.NewGuid(), "  ", null);
        Assert.Null(r.CoachNoteEn);
        Assert.Null(r.CoachNoteAr);
    }
}
```

- [ ] **Step 6: Run entity test to verify it fails, then build**

Run: `cd backend && dotnet test tests/Kheprx.BaseBackend.Attendance.UnitTests --filter AttendanceRecordTests`
Expected: FAIL first (project/refs not yet resolving) → after `dotnet build Kheprx.BaseBackend.sln` succeeds, re-run: Expected PASS.

- [ ] **Step 7: Add the repository InMemory test**

`…Attendance.UnitTests/Repositories/AttendanceRecordRepositoryTests.cs` (clone of `FeedbackEntryRepositoryTests`):
```csharp
using Kheprx.BaseBackend.Attendance.Domain.Entities;
using Kheprx.BaseBackend.Attendance.Infrastructure.Data;
using Kheprx.BaseBackend.Attendance.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Attendance.UnitTests.Repositories;

public class AttendanceRecordRepositoryTests
{
    private static AttendanceDbContext NewDb()
        => new(new DbContextOptionsBuilder<AttendanceDbContext>().UseInMemoryDatabase($"att-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task ListBySwimmerAsync_returns_only_that_swimmer_newest_first()
    {
        await using var db = NewDb();
        var sw = Guid.NewGuid();
        db.AttendanceRecords.Add(new AttendanceRecord(sw, new DateOnly(2026, 9, 1), Guid.NewGuid(), Guid.NewGuid()));
        db.AttendanceRecords.Add(new AttendanceRecord(sw, new DateOnly(2026, 9, 5), Guid.NewGuid(), Guid.NewGuid()));
        db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 3), Guid.NewGuid(), Guid.NewGuid()));
        await db.SaveChangesAsync();

        var repo = new AttendanceRecordRepository(db);
        var list = await repo.ListBySwimmerAsync(sw);

        Assert.Equal(2, list.Count);
        Assert.All(list, r => Assert.Equal(sw, r.SwimmerId));
        Assert.Equal(new DateOnly(2026, 9, 5), list[0].SessionDate); // newest first
    }
}
```
Run: `cd backend && dotnet test tests/Kheprx.BaseBackend.Attendance.UnitTests` → Expected: PASS.

- [ ] **Step 8: Create the Attendance migration + build**

Run (API stopped):
```bash
cd backend
dotnet ef migrations add CreateAttendanceRecordTable \
  --context AttendanceDbContext \
  --project src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Infrastructure \
  --startup-project Kheprx.BaseBackend.Api
dotnet build Kheprx.BaseBackend.sln
```
Expected: migration creates the `attendance` schema + `attendance_record` table with a unique `("SwimmerId","SessionDate")` index; build succeeds. **Do not commit.**

---

## Task 3: `AttendanceService` + DTO + messages

**Files:**
- Create: `…Attendance.Application/DTOs/AttendanceRecordDtos.cs`
- Create: `…Attendance.Application/Services/Interfaces/IAttendanceService.cs`
- Create: `…Attendance.Application/Services/AttendanceService.cs`
- Create: `…Attendance.Application/Resources/AttendanceMessages.cs`
- Modify: `…Attendance.Infrastructure/Extensions/AttendanceModuleExtensions.cs`
- Test: `…Attendance.UnitTests/Services/AttendanceServiceTests.cs`

**Interfaces:**
- Consumes: `IAttendanceRecordRepository.ListBySwimmerAsync` (Task 2).
- Produces: `AttendanceRecordDto(Guid Id, Guid SwimmerId, DateOnly SessionDate, Guid StatusId, string? CoachNoteEn, string? CoachNoteAr, Guid RecordedBy, string RecordedByNameEn, string? RecordedByNameAr)`; `IAttendanceService.ListBySwimmerAsync(Guid swimmerId, CancellationToken) → IReadOnlyList<AttendanceRecordDto>` (name fields empty from the service).

- [ ] **Step 1: Write the failing service test**

`…Attendance.UnitTests/Services/AttendanceServiceTests.cs`:
```csharp
using Kheprx.BaseBackend.Attendance.Application.Services;
using Kheprx.BaseBackend.Attendance.Domain.Entities;
using Kheprx.BaseBackend.Attendance.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Attendance.UnitTests.Services;

public class AttendanceServiceTests
{
    [Fact]
    public async Task ListBySwimmerAsync_maps_entities_to_dtos_with_empty_recorder_names()
    {
        var sw = Guid.NewGuid();
        var recorder = Guid.NewGuid();
        var repo = new Mock<IAttendanceRecordRepository>();
        repo.Setup(r => r.ListBySwimmerAsync(sw, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new AttendanceRecord(sw, new DateOnly(2026, 9, 9), Guid.NewGuid(), recorder, "note", null) });

        var svc = new AttendanceService(repo.Object);
        var list = await svc.ListBySwimmerAsync(sw);

        Assert.Single(list);
        Assert.Equal(sw, list[0].SwimmerId);
        Assert.Equal(recorder, list[0].RecordedBy);
        Assert.Equal("note", list[0].CoachNoteEn);
        Assert.Equal(string.Empty, list[0].RecordedByNameEn); // enriched later by the controller
        Assert.Null(list[0].RecordedByNameAr);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd backend && dotnet test tests/Kheprx.BaseBackend.Attendance.UnitTests --filter AttendanceServiceTests`
Expected: FAIL — `AttendanceService`/`AttendanceRecordDto` do not exist.

- [ ] **Step 3: Create DTO, interface, service, messages**

`…Attendance.Application/DTOs/AttendanceRecordDtos.cs`:
```csharp
namespace Kheprx.BaseBackend.Attendance.Application.DTOs;

/// <summary>A swimmer's attendance record. RecordedByName* are resolved in the API layer (empty from the service).</summary>
public sealed record AttendanceRecordDto(
    Guid Id,
    Guid SwimmerId,
    DateOnly SessionDate,
    Guid StatusId,
    string? CoachNoteEn,
    string? CoachNoteAr,
    Guid RecordedBy,
    string RecordedByNameEn,
    string? RecordedByNameAr);
```

`…Attendance.Application/Services/Interfaces/IAttendanceService.cs`:
```csharp
using Kheprx.BaseBackend.Attendance.Application.DTOs;
namespace Kheprx.BaseBackend.Attendance.Application.Services.Interfaces;
public interface IAttendanceService
{
    Task<IReadOnlyList<AttendanceRecordDto>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default);
}
```

`…Attendance.Application/Services/AttendanceService.cs`:
```csharp
using Kheprx.BaseBackend.Attendance.Application.DTOs;
using Kheprx.BaseBackend.Attendance.Application.Services.Interfaces;
using Kheprx.BaseBackend.Attendance.Domain.Entities;
using Kheprx.BaseBackend.Attendance.Domain.Repositories;

namespace Kheprx.BaseBackend.Attendance.Application.Services;

internal sealed class AttendanceService : IAttendanceService
{
    private readonly IAttendanceRecordRepository _records;
    public AttendanceService(IAttendanceRecordRepository records) => _records = records;

    public async Task<IReadOnlyList<AttendanceRecordDto>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default)
    {
        var rows = await _records.ListBySwimmerAsync(swimmerId, ct);
        return rows.Select(ToDto).ToList();
    }

    private static AttendanceRecordDto ToDto(AttendanceRecord r) =>
        new(r.Id, r.SwimmerId, r.SessionDate, r.StatusId, r.CoachNoteEn, r.CoachNoteAr, r.RecordedBy, string.Empty, null);
}
```

`…Attendance.Application/Resources/AttendanceMessages.cs`:
```csharp
namespace Kheprx.BaseBackend.Attendance.Application.Resources;

/// <summary>Localized messages for attendance queries.</summary>
public static class AttendanceMessages
{
    public static class Success
    {
        public static string Listed(string lang) => lang switch { "ar" => "سجل الحضور", _ => "Attendance" };
    }
}
```

- [ ] **Step 4: Register the service in the module extension**

In `AttendanceModuleExtensions.cs` add `using Kheprx.BaseBackend.Attendance.Application.Services;` + `using Kheprx.BaseBackend.Attendance.Application.Services.Interfaces;` and, after the repository registration:
```csharp
        services.AddScoped<IAttendanceService, AttendanceService>();
```

- [ ] **Step 5: Run service test — expect PASS**

Run: `cd backend && dotnet test tests/Kheprx.BaseBackend.Attendance.UnitTests --filter AttendanceServiceTests`
Expected: PASS. Then `dotnet build Kheprx.BaseBackend.sln` → Build succeeded. **Do not commit.**

---

## Task 4: `AttendanceRecordsController` (flat read + recorder-name enrichment)

**Files:**
- Create: `backend/Kheprx.BaseBackend.Api/Controllers/AttendanceRecordsController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/AttendanceRecordsControllerTests.cs`

**Interfaces:**
- Consumes: `IAttendanceService.ListBySwimmerAsync` (Task 3), `IUserService.GetDisplayNamesAsync(IReadOnlyCollection<Guid>, CancellationToken) → IReadOnlyDictionary<Guid, UserNameDto>` (existing), `UserNameDto(Guid Id, string NameEn, string? NameAr)`.
- Produces: `GET /api/attendance-records?swimmerId={id}` → `ApiResponse<IReadOnlyList<AttendanceRecordDto>>` with `RecordedByName*` populated.

- [ ] **Step 1: Write the failing controller test**

`backend/tests/Kheprx.BaseBackend.Api.UnitTests/AttendanceRecordsControllerTests.cs`:
```csharp
using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Attendance.Application.DTOs;
using Kheprx.BaseBackend.Attendance.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class AttendanceRecordsControllerTests
{
    private static AttendanceRecordDto Dto(Guid recorder) =>
        new(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 9), Guid.NewGuid(), "note", null, recorder, string.Empty, null);

    [Fact]
    public async Task List_returns_200_and_resolves_recorder_names()
    {
        var swimmerId = Guid.NewGuid();
        var recorder = Guid.NewGuid();
        var svc = new Mock<IAttendanceService>();
        var users = new Mock<IUserService>();
        svc.Setup(s => s.ListBySwimmerAsync(swimmerId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Dto(recorder) });
        users.Setup(u => u.GetDisplayNamesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Dictionary<Guid, UserNameDto> { [recorder] = new(recorder, "Coach Layla", "الكابتن ليلى") });

        var controller = new AttendanceRecordsController(svc.Object, users.Object);
        var result = await controller.List(swimmerId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<AttendanceRecordDto>>>(ok.Value);
        Assert.Equal("Coach Layla", body.Data![0].RecordedByNameEn);
        Assert.Equal("الكابتن ليلى", body.Data![0].RecordedByNameAr);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd backend && dotnet test tests/Kheprx.BaseBackend.Api.UnitTests --filter AttendanceRecordsControllerTests`
Expected: FAIL — controller doesn't exist.

- [ ] **Step 3: Create the controller (model on `HealthReadingsController` GET + `FeedbackEntriesController` enrichment)**

`backend/Kheprx.BaseBackend.Api/Controllers/AttendanceRecordsController.cs`:
```csharp
using Kheprx.BaseBackend.Attendance.Application.DTOs;
using Kheprx.BaseBackend.Attendance.Application.Resources;
using Kheprx.BaseBackend.Attendance.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Route("api/attendance-records")]
[Authorize]
public sealed class AttendanceRecordsController : BaseApiController
{
    private readonly IAttendanceService _service;
    private readonly IUserService _users;

    public AttendanceRecordsController(IAttendanceService service, IUserService users)
    {
        _service = service;
        _users = users;
    }

    /// <summary>Lists a swimmer's attendance records (newest first), with recorder names resolved.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AttendanceRecordDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AttendanceRecordDto>>>> List([FromQuery] Guid swimmerId, CancellationToken ct)
    {
        var rows = await _service.ListBySwimmerAsync(swimmerId, ct);
        var enriched = await EnrichRecorders(rows, ct);
        return Ok(ApiResponse<IReadOnlyList<AttendanceRecordDto>>.Success(
            AttendanceMessages.Success.Listed(AppLanguage.Current), enriched));
    }

    // Resolves recorded_by -> display names via the Identity module (composition at the API layer).
    private async Task<IReadOnlyList<AttendanceRecordDto>> EnrichRecorders(IReadOnlyList<AttendanceRecordDto> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return rows;
        var ids = rows.Select(r => r.RecordedBy).Distinct().ToList();
        var names = await _users.GetDisplayNamesAsync(ids, ct);
        return rows.Select(r => names.TryGetValue(r.RecordedBy, out var n)
            ? r with { RecordedByNameEn = n.NameEn, RecordedByNameAr = n.NameAr }
            : r).ToList();
    }
}
```

- [ ] **Step 4: Run controller test — expect PASS**

Run: `cd backend && dotnet test tests/Kheprx.BaseBackend.Api.UnitTests --filter AttendanceRecordsControllerTests`
Expected: PASS.

- [ ] **Step 5: Full backend build + test sweep**

Run: `cd backend && dotnet build Kheprx.BaseBackend.sln && dotnet test Kheprx.BaseBackend.sln`
Expected: Build succeeded; all tests pass. **Do not commit.**

- [ ] **Step 6: Smoke-test the endpoints locally (optional but recommended)**

Start the API, then (statuses migration + seeder having run locally):
`GET http://localhost:<port>/api/reference/attendance-statuses` → 4 statuses.
`GET http://localhost:<port>/api/attendance-records?swimmerId=<any-local-swimmer>` → `[]` (no local records yet) with 200. Stop the API afterward.

---

## Task 5: Frontend — reference `attendance-statuses` loader

**Files:**
- Modify: `frontend/src/app/features/reference/domain/repositories/reference.repository.ts`
- Modify: `frontend/src/app/features/reference/data/repositories/reference.repository.impl.ts`
- Create: `frontend/src/app/features/reference/domain/usecases/load-attendance-statuses.use-case.ts`
- Modify: `frontend/src/app/features/reference/index.ts`
- Test: `frontend/src/app/features/reference/testing/domain/usecases/load-attendance-statuses.use-case.spec.ts` (create; if `features/reference/testing` doesn't exist, place under the repo's established reference test folder — mirror an existing reference use-case spec location)

**Interfaces:**
- Consumes: existing `CodedLookupListDtoRs`, `isCodedLookupListValid`, `LookupItem`, `REFERENCE_REPOSITORY`.
- Produces: `IReferenceRepository.getAttendanceStatuses(): Promise<CodedLookupListDtoRs>`; `LoadAttendanceStatusesUseCase.run(): Promise<Result<LookupItem[]>>`.

- [ ] **Step 1: Write the failing use-case spec**

Create the spec (clone the existing feedback/observation categories use-case spec; find one under `features/reference/testing` or the repo's reference test location and mirror it):
```ts
import { LoadAttendanceStatusesUseCase } from '@features/reference/domain/usecases/load-attendance-statuses.use-case';
import { REFERENCE_REPOSITORY } from '@features/reference/domain/repositories/reference.repository';
import { TestBed } from '@angular/core/testing';

describe('LoadAttendanceStatusesUseCase', () => {
  function setup(repo: Partial<{ getAttendanceStatuses: () => Promise<unknown> }>) {
    TestBed.configureTestingModule({
      providers: [
        LoadAttendanceStatusesUseCase,
        { provide: REFERENCE_REPOSITORY, useValue: repo },
      ],
    });
    return TestBed.inject(LoadAttendanceStatusesUseCase);
  }

  it('maps coded lookups to LookupItem[]', async () => {
    const uc = setup({
      getAttendanceStatuses: async () => ({
        data: [{ id: 's1', code: 'present', nameEn: 'Present', nameAr: 'حاضر' }],
      }),
    });
    const res = await uc.run();
    expect(res.ok).toBe(true);
    if (res.ok) {
      expect(res.data[0].code).toBe('present');
      expect(res.data[0].nameEn).toBe('Present');
    }
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd frontend && npx ng test --watch=false --include='**/load-attendance-statuses.use-case.spec.ts'`
Expected: FAIL — use-case not found.

- [ ] **Step 3: Add the repository method (interface + impl)**

In `reference.repository.ts` add to `IReferenceRepository`:
```ts
  getAttendanceStatuses(): Promise<CodedLookupListDtoRs>;
```
In `reference.repository.impl.ts` add:
```ts
  getAttendanceStatuses(): Promise<CodedLookupListDtoRs> {
    return this.http.get<CodedLookupListDtoRs>('/api/reference/attendance-statuses');
  }
```

- [ ] **Step 4: Create the use-case (clone of `load-feedback-categories.use-case.ts`)**

`features/reference/domain/usecases/load-attendance-statuses.use-case.ts`:
```ts
// load-attendance-statuses.use-case.ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { REFERENCE_REPOSITORY } from '@features/reference/domain/repositories/reference.repository';
import { isCodedLookupListValid } from '@features/reference/data/dto/reference.dto';
import { LookupItem } from '@features/reference/domain/model/reference';

@Injectable({ providedIn: 'root' })
export class LoadAttendanceStatusesUseCase extends UseCase<void, LookupItem[]> {
  private readonly repo = inject(REFERENCE_REPOSITORY);
  constructor() { super('LoadAttendanceStatuses'); }
  protected async execute(): Promise<LookupItem[]> {
    const res = await this.repo.getAttendanceStatuses();
    if (!isCodedLookupListValid(res.data)) throw new AppError('Invalid attendance statuses received', 'validation');
    return res.data.map((s) => ({ id: s.id, code: s.code, nameEn: s.nameEn, nameAr: s.nameAr }));
  }
}
```

In `features/reference/index.ts` add:
```ts
export * from '@features/reference/domain/usecases/load-attendance-statuses.use-case';
```

- [ ] **Step 5: Run the spec — expect PASS**

Run: `cd frontend && npx ng test --watch=false --include='**/load-attendance-statuses.use-case.spec.ts'`
Expected: PASS. **Do not commit.**

---

## Task 6: Frontend — swimmer-profile attendance data layer

**Files:**
- Create: `frontend/src/app/features/swimmer-profile/data/dto/attendance-record.dto.ts`
- Create: `frontend/src/app/features/swimmer-profile/data/dto/attendance-record.mapper.ts`
- Create: `frontend/src/app/features/swimmer-profile/domain/model/attendance-record.ts`
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/list-attendance-records.use-case.ts`
- Modify: `frontend/src/app/features/swimmer-profile/domain/repositories/swimmer-profile.repository.ts`
- Modify: `frontend/src/app/features/swimmer-profile/data/repositories/swimmer-profile.repository.impl.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/data/dto/attendance-record.mapper.spec.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/domain/usecases/list-attendance-records.use-case.spec.ts`

**Interfaces:**
- Produces: `AttendanceRecord { id; sessionDate; statusId; coachNoteEn; coachNoteAr; recordedByNameEn; recordedByNameAr }`; `ISwimmerProfileRepository.getAttendanceRecords(id): Promise<AttendanceRecordListDtoRs>`; `ListAttendanceRecordsUseCase.run(id: string): Promise<Result<AttendanceRecord[]>>`.

- [ ] **Step 1: Write the failing mapper spec**

`…/testing/data/dto/attendance-record.mapper.spec.ts` (clone of `feedback-entry.mapper.spec.ts`):
```ts
import { toAttendanceRecord, toAttendanceRecordList } from '@features/swimmer-profile/data/dto/attendance-record.mapper';
import { AttendanceRecordDtoRs } from '@features/swimmer-profile/data/dto/attendance-record.dto';

const DTO: AttendanceRecordDtoRs = {
  id: 'a1', swimmerId: 's1', sessionDate: '2026-09-09', statusId: 'st1',
  coachNoteEn: 'Excused', coachNoteAr: 'بعذر', recordedBy: 'u1',
  recordedByNameEn: 'Coach Layla', recordedByNameAr: 'الكابتن ليلى',
};

describe('attendance-record.mapper', () => {
  it('maps a record (keeps status + notes + recorder names, drops ids we do not render)', () => {
    const m = toAttendanceRecord(DTO);
    expect(m.id).toBe('a1');
    expect(m.sessionDate).toBe('2026-09-09');
    expect(m.statusId).toBe('st1');
    expect(m.coachNoteEn).toBe('Excused');
    expect(m.recordedByNameEn).toBe('Coach Layla');
    expect((m as unknown as Record<string, unknown>).swimmerId).toBeUndefined();
    expect((m as unknown as Record<string, unknown>).recordedBy).toBeUndefined();
  });

  it('maps a list', () => {
    expect(toAttendanceRecordList([DTO])).toHaveLength(1);
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd frontend && npx ng test --watch=false --include='**/attendance-record.mapper.spec.ts'`
Expected: FAIL — mapper/dto not found.

- [ ] **Step 3: Create model, dto, mapper**

`domain/model/attendance-record.ts`:
```ts
export interface AttendanceRecord {
  id: string;
  sessionDate: string;      // 'YYYY-MM-DD'
  statusId: string;
  coachNoteEn: string | null;
  coachNoteAr: string | null;
  recordedByNameEn: string;
  recordedByNameAr: string | null;
}
```

`data/dto/attendance-record.dto.ts` (clone of `record.dto.ts` shape):
```ts
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface AttendanceRecordDtoRs {
  id: string;
  swimmerId: string;
  sessionDate: string;
  statusId: string;
  coachNoteEn: string | null;
  coachNoteAr: string | null;
  recordedBy: string;
  recordedByNameEn: string;
  recordedByNameAr: string | null;
}
export interface AttendanceRecordListDtoRs extends BaseResponseRs<AttendanceRecordDtoRs[]> {}

export function isAttendanceRecordDtoRsValid(x: unknown): x is AttendanceRecordDtoRs {
  const d = x as AttendanceRecordDtoRs;
  if (!d || typeof d !== 'object') return false;
  return typeof d.id === 'string'
    && typeof d.swimmerId === 'string'
    && typeof d.sessionDate === 'string'
    && typeof d.statusId === 'string';
}

export function isAttendanceRecordListValid(data: unknown): data is AttendanceRecordDtoRs[] {
  return Array.isArray(data) && data.every(isAttendanceRecordDtoRsValid);
}
```

`data/dto/attendance-record.mapper.ts`:
```ts
import { AttendanceRecordDtoRs } from '@features/swimmer-profile/data/dto/attendance-record.dto';
import { AttendanceRecord } from '@features/swimmer-profile/domain/model/attendance-record';

export function toAttendanceRecord(d: AttendanceRecordDtoRs): AttendanceRecord {
  return {
    id: d.id,
    sessionDate: d.sessionDate,
    statusId: d.statusId,
    coachNoteEn: d.coachNoteEn,
    coachNoteAr: d.coachNoteAr,
    recordedByNameEn: d.recordedByNameEn,
    recordedByNameAr: d.recordedByNameAr,
  };
}

export function toAttendanceRecordList(list: AttendanceRecordDtoRs[]): AttendanceRecord[] {
  return list.map(toAttendanceRecord);
}
```

- [ ] **Step 4: Run mapper spec — expect PASS**

Run: `cd frontend && npx ng test --watch=false --include='**/attendance-record.mapper.spec.ts'`
Expected: PASS.

- [ ] **Step 5: Add repo method (interface + impl) and the use-case**

In `swimmer-profile.repository.ts`: import `AttendanceRecordListDtoRs` and add to the interface:
```ts
  getAttendanceRecords(id: string): Promise<AttendanceRecordListDtoRs>;
```
In `swimmer-profile.repository.impl.ts`: import it and add (flat route, mirrors `listRecords`):
```ts
  getAttendanceRecords(id: string): Promise<AttendanceRecordListDtoRs> {
    return this.http.get<AttendanceRecordListDtoRs>(`/api/attendance-records?swimmerId=${id}`);
  }
```

`domain/usecases/list-attendance-records.use-case.ts` (clone of `list-records.use-case.ts`):
```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { isAttendanceRecordListValid } from '@features/swimmer-profile/data/dto/attendance-record.dto';
import { toAttendanceRecordList } from '@features/swimmer-profile/data/dto/attendance-record.mapper';
import { AttendanceRecord } from '@features/swimmer-profile/domain/model/attendance-record';

@Injectable({ providedIn: 'root' })
export class ListAttendanceRecordsUseCase extends UseCase<string, AttendanceRecord[]> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('ListAttendanceRecords'); }
  protected async execute(id: string): Promise<AttendanceRecord[]> {
    const res = await this.repo.getAttendanceRecords(id);
    if (!isAttendanceRecordListValid(res.data)) throw new AppError('Invalid attendance records received', 'validation');
    return toAttendanceRecordList(res.data);
  }
}
```

- [ ] **Step 6: Write + run the use-case spec**

`…/testing/domain/usecases/list-attendance-records.use-case.spec.ts` (mirror `records.use-cases.spec.ts` idiom — provide a fake `SWIMMER_PROFILE_REPOSITORY.getAttendanceRecords` returning `{ data: [DTO] }`, assert `res.ok` and mapped length/fields). Then run:
Run: `cd frontend && npx ng test --watch=false --include='**/list-attendance-records.use-case.spec.ts'`
Expected: PASS. **Do not commit.**

---

## Task 7: Frontend — viewmodel attendance state + calendar computeds

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/attendance.viewmodel.spec.ts` (create; mirror the existing `swimmer-profile.viewmodel.spec.ts` setup)

**Interfaces:**
- Consumes: `ListAttendanceRecordsUseCase` (Task 6), `LoadAttendanceStatusesUseCase` (Task 5), `AttendanceRecord` model, `LookupItem`.
- Produces (on `SwimmerProfileViewModel`): signals `attendanceRecords`, `attendanceStatuses`, `loadingAttendance`, `selectedMonth`, `selectedAttendanceDay`; computeds `attendanceMonths`, `attendanceRate`, `attendanceWeeks`, `selectedAttendanceDetail`; methods `selectAttendanceMonth(m)`, `selectAttendanceDay(iso)`; `activeTab` now includes `'attendance'`.

- [ ] **Step 1: Write the failing viewmodel spec**

`…/testing/presentation/pages/swimmer-profile/attendance.viewmodel.spec.ts` — set up the `SwimmerProfileViewModel` with fakes (mirror the existing viewmodel spec's TestBed providers), stub `ListAttendanceRecordsUseCase.run` to return two records in the same month + one in a prior month, and `LoadAttendanceStatusesUseCase.run` to return the 4 statuses. Assert:
```ts
// after switching to the attendance tab and awaiting load:
// - vm.attendanceMonths()[0] is the latest month present
// - vm.selectedMonth() defaults to that latest month
// - vm.attendanceRate() equals round((present+late)/total*100) for that month
// - vm.attendanceWeeks() is a non-empty array of 7-length rows; the day cell for a
//   record's date exposes the right status code and hasNote flag
// - after vm.selectAttendanceDay('<absent-day-iso>'), vm.selectedAttendanceDetail().status.code === 'absent'
```
Write concrete assertions with fixed dates (e.g. records on `2026-09-07` excused, `2026-09-09` absent present-day math). Keep the month deterministic by using explicit ISO date strings in the stub (the computeds derive the calendar purely from `sessionDate` strings — no reliance on "today").

- [ ] **Step 2: Run it to verify it fails**

Run: `cd frontend && npx ng test --watch=false --include='**/attendance.viewmodel.spec.ts'`
Expected: FAIL — new members don't exist.

- [ ] **Step 3: Wire imports + inject the two use-cases**

At the top of `swimmer-profile.viewmodel.ts` add imports:
```ts
import { ListAttendanceRecordsUseCase } from '@features/swimmer-profile/domain/usecases/list-attendance-records.use-case';
import { LoadAttendanceStatusesUseCase } from '@features/reference/domain/usecases/load-attendance-statuses.use-case';
import { AttendanceRecord } from '@features/swimmer-profile/domain/model/attendance-record';
```
Add injected fields near the other `inject(...)` lines:
```ts
  private readonly listAttendanceUc = inject(ListAttendanceRecordsUseCase);
  private readonly loadAttendanceStatuses = inject(LoadAttendanceStatusesUseCase);
```

- [ ] **Step 4: Extend the `activeTab` union + `setTab` signature (both occurrences) and add attendance state/computeds**

Update the `activeTab` signal type (line ~128) and the `setTab` parameter type (line ~475) to include `'attendance'`:
```ts
  readonly activeTab = signal<'identityVitals' | 'guardian' | 'physiological' | 'inbody' | 'records' | 'healthMonitoring' | 'attendance' | 'feedback'>('identityVitals');
```
```ts
  setTab(key: 'identityVitals' | 'guardian' | 'physiological' | 'inbody' | 'records' | 'healthMonitoring' | 'attendance' | 'feedback'): void {
```
Add `private attendanceLoaded = false;` beside the other `*Loaded` flags (line ~134). Add the attendance state + computeds (place near the Feedback state block):
```ts
  // Attendance state (read-only calendar)
  readonly attendanceRecords = signal<AttendanceRecord[]>([]);
  readonly attendanceStatuses = signal<LookupItem[]>([]);
  readonly loadingAttendance = signal(false);
  readonly selectedMonth = signal('');                       // 'YYYY-MM'
  readonly selectedAttendanceDay = signal<string | null>(null); // 'YYYY-MM-DD'

  private readonly statusById = computed(() => new Map(this.attendanceStatuses().map((s) => [s.id, s])));

  readonly attendanceMonths = computed(() => {
    const set = new Set(this.attendanceRecords().map((r) => r.sessionDate.slice(0, 7)));
    return [...set].sort((a, b) => b.localeCompare(a)); // newest first
  });

  readonly attendanceRate = computed(() => {
    const month = this.selectedMonth();
    const inMonth = this.attendanceRecords().filter((r) => r.sessionDate.slice(0, 7) === month);
    if (inMonth.length === 0) return 0;
    const attended = inMonth.filter((r) => {
      const code = this.statusById().get(r.statusId)?.code;
      return code === 'present' || code === 'late';
    }).length;
    return Math.round((attended / inMonth.length) * 100);
  });

  readonly attendanceWeeks = computed(() => {
    const month = this.selectedMonth();
    type Cell = { day: number; dateIso: string; code: string | null; hasNote: boolean } | null;
    if (!month) return [] as Cell[][];
    const [y, m] = month.split('-').map(Number);
    const byDate = new Map(
      this.attendanceRecords().filter((r) => r.sessionDate.slice(0, 7) === month).map((r) => [r.sessionDate, r]),
    );
    const firstWeekday = new Date(y, m - 1, 1).getDay(); // 0=Sun
    const daysInMonth = new Date(y, m, 0).getDate();
    const cells: Cell[] = [];
    for (let i = 0; i < firstWeekday; i++) cells.push(null);
    for (let day = 1; day <= daysInMonth; day++) {
      const dateIso = `${month}-${String(day).padStart(2, '0')}`;
      const rec = byDate.get(dateIso) ?? null;
      cells.push({
        day,
        dateIso,
        code: rec ? (this.statusById().get(rec.statusId)?.code ?? null) : null,
        hasNote: !!(rec && (rec.coachNoteEn || rec.coachNoteAr)),
      });
    }
    while (cells.length % 7 !== 0) cells.push(null);
    const weeks: Cell[][] = [];
    for (let i = 0; i < cells.length; i += 7) weeks.push(cells.slice(i, i + 7));
    return weeks;
  });

  readonly selectedAttendanceDetail = computed(() => {
    const iso = this.selectedAttendanceDay();
    if (!iso) return null;
    const rec = this.attendanceRecords().find((r) => r.sessionDate === iso);
    if (!rec) return null;
    return { dateIso: iso, status: this.statusById().get(rec.statusId) ?? null, record: rec };
  });

  selectAttendanceMonth(month: string): void { this.selectedMonth.set(month); this.selectedAttendanceDay.set(null); }
  selectAttendanceDay(iso: string): void { this.selectedAttendanceDay.set(this.selectedAttendanceDay() === iso ? null : iso); }
```

- [ ] **Step 5: Load on tab activation + reset in `load()`**

In `setTab`, add a branch alongside the others:
```ts
    if (key === 'attendance' && !this.attendanceLoaded) void this.loadAttendance();
```
Add the loader (place near `loadFeedback`):
```ts
  private async loadAttendance(): Promise<void> {
    this.attendanceLoaded = true;
    this.loadingAttendance.set(true);
    const [listRes, statusRes] = await Promise.all([
      this.listAttendanceUc.run(this.swimmerId),
      this.loadAttendanceStatuses.run(),
    ]);
    this.loadingAttendance.set(false);
    if (statusRes.ok) this.attendanceStatuses.set(statusRes.data);
    if (listRes.ok) {
      this.attendanceRecords.set(listRes.data);
      this.selectedMonth.set(this.attendanceMonths()[0] ?? '');
      this.selectedAttendanceDay.set(null);
    } else {
      this.attendanceLoaded = false;
      this.attendanceRecords.set([]);
    }
  }
```
In `load(id)`, add resets alongside the other per-swimmer resets (near the feedback resets, line ~334):
```ts
    this.attendanceLoaded = false;
    this.attendanceRecords.set([]);
    this.attendanceStatuses.set([]);
    this.selectedMonth.set('');
    this.selectedAttendanceDay.set(null);
```

- [ ] **Step 6: Run the viewmodel spec — expect PASS**

Run: `cd frontend && npx ng test --watch=false --include='**/attendance.viewmodel.spec.ts'`
Expected: PASS. **Do not commit.**

---

## Task 8: Frontend — enable the tab + calendar template + i18n

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.ts`
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.html`
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`

**Interfaces:**
- Consumes: the viewmodel members from Task 7.
- Produces: the visible Attendance tab.

- [ ] **Step 1: Enable the tab + add page helpers**

In `swimmer-profile.page.ts`, add `'attendance'` to `enabledTabs` (line ~23):
```ts
  protected readonly enabledTabs = new Set(['identityVitals', 'guardian', 'physiological', 'inbody', 'records', 'healthMonitoring', 'attendance', 'feedback']);
```
Add helper methods on the class (status label by language, cell color classes, month label, localized note/recorder):
```ts
  monthLabel(ym: string): string {
    if (!ym) return '';
    const [y, m] = ym.split('-').map(Number);
    const d = new Date(y, m - 1, 1);
    return d.toLocaleDateString(this.language.lang() === 'ar' ? 'ar-EG' : 'en-US', { month: 'long', year: 'numeric' });
  }

  attStatusLabel(statusId: string | null | undefined): string {
    if (!statusId) return '';
    const s = this.vm.attendanceStatuses().find((x) => x.id === statusId);
    return this.refLabel(s ?? null);
  }

  // Calendar cell tint by status code (present=blue, late=amber, absent=red, excused=blue/info).
  attCellClass(code: string | null): string {
    switch (code) {
      case 'present': return 'bg-blue-500/10 text-blue-600';
      case 'late': return 'bg-amber-500/10 text-amber-600';
      case 'absent': return 'bg-red-500/10 text-red-600';
      case 'excused': return 'bg-blue-500/10 text-blue-600';
      default: return 'text-text-secondary';
    }
  }

  attDotClass(code: string): string {
    switch (code) {
      case 'present': return 'bg-blue-500';
      case 'late': return 'bg-amber-500';
      case 'absent': return 'bg-red-500';
      case 'excused': return 'bg-blue-500';
      default: return 'bg-border';
    }
  }
```
(Legend uses codes `present`, `absent`, `excused` per the mock; `late` is supported too. `this.language` and `refLabel` already exist on the page component.)

- [ ] **Step 2: Add the attendance section to the template**

In `swimmer-profile.page.html`, add this block immediately after the `healthMonitoring` section's closing `}` and before the `feedback` section (so tab order matches the strip):
```html
    @if (vm.activeTab() === 'attendance') {
      <section class="rounded-2xl border border-border bg-surface p-6 shadow-sm">
        <div class="mb-4 border-b border-border pb-3">
          <h2 class="font-heading text-lg text-ink">{{ 'swimmerProfile.tabs.attendance' | translate }}</h2>
          <p class="mt-0.5 text-sm text-text-secondary">{{ 'swimmerProfile.attendance.subtitle' | translate }}</p>
        </div>

        @if (vm.loadingAttendance()) {
          <p class="text-sm text-text-secondary">{{ 'swimmerProfile.states.loading' | translate }}</p>
        } @else if (vm.attendanceRecords().length === 0) {
          <p class="text-sm text-text-secondary">{{ 'swimmerProfile.attendance.noRecords' | translate }}</p>
        } @else {
          <!-- Rate + legend -->
          <div class="mb-5 flex flex-wrap items-end justify-between gap-4">
            <div>
              <p class="font-heading text-3xl text-success">{{ vm.attendanceRate() }}%</p>
              <p class="text-sm text-text-secondary">{{ 'swimmerProfile.attendance.rate' | translate }}</p>
            </div>
            <div class="flex flex-wrap gap-3 text-xs text-text-secondary">
              @for (code of ['present', 'absent', 'excused']; track code) {
                <span class="flex items-center gap-1.5">
                  <span class="h-2.5 w-2.5 rounded-full" [class]="attDotClass(code)"></span>
                  {{ ('swimmerProfile.attendance.legend.' + code) | translate }}
                </span>
              }
            </div>
          </div>

          <!-- Month picker -->
          <div class="mb-4 flex items-center gap-3">
            <span class="text-sm font-semibold text-ink">{{ 'swimmerProfile.attendance.monthly' | translate }}</span>
            <select class="h-9 rounded-md border border-border bg-surface px-2 text-sm text-ink"
                    [value]="vm.selectedMonth()"
                    (change)="vm.selectAttendanceMonth($any($event.target).value)">
              @for (m of vm.attendanceMonths(); track m) { <option [value]="m">{{ monthLabel(m) }}</option> }
            </select>
          </div>

          <!-- Calendar grid -->
          <div class="grid grid-cols-7 gap-1.5">
            @for (d of ['S','M','T','W','T','F','S']; track $index) {
              <div class="py-1 text-center text-xs font-medium text-text-secondary">{{ d }}</div>
            }
            @for (week of vm.attendanceWeeks(); track $index) {
              @for (cell of week; track $index) {
                @if (cell === null) {
                  <div class="aspect-square"></div>
                } @else {
                  <button type="button"
                          class="relative flex aspect-square items-center justify-center rounded-md text-xs font-medium"
                          [class]="attCellClass(cell.code)"
                          [class.ring-2]="vm.selectedAttendanceDay() === cell.dateIso"
                          [class.ring-primary]="vm.selectedAttendanceDay() === cell.dateIso"
                          [disabled]="cell.code === null"
                          (click)="vm.selectAttendanceDay(cell.dateIso)">
                    {{ cell.day }}
                    @if (cell.hasNote) { <span class="absolute bottom-1 h-1 w-1 rounded-full bg-current opacity-70"></span> }
                  </button>
                }
              }
            }
          </div>

          <!-- Selected-day detail -->
          @if (vm.selectedAttendanceDetail(); as detail) {
            <div class="mt-5 rounded-xl border border-border bg-card p-4">
              <div class="flex items-center gap-3">
                <span class="font-heading text-lg text-ink">{{ fmtDate(detail.dateIso) }}</span>
                @if (detail.status) {
                  <span class="rounded-full px-2.5 py-0.5 text-xs font-semibold" [class]="attCellClass(detail.status.code ?? null)">
                    {{ refLabel(detail.status) }}
                  </span>
                }
              </div>
              @if ((language.lang() === 'ar' ? detail.record.coachNoteAr : detail.record.coachNoteEn); as note) {
                <p class="mt-2 text-sm text-ink">{{ note }}</p>
                <p class="mt-1 text-xs text-text-secondary">{{ language.lang() === 'ar' ? (detail.record.recordedByNameAr || detail.record.recordedByNameEn) : detail.record.recordedByNameEn }}</p>
              } @else {
                <p class="mt-2 text-sm text-text-secondary">{{ 'swimmerProfile.attendance.noNote' | translate }}</p>
              }
            </div>
          }
        }
      </section>
    }
```
Note: `language` must be accessible from the template — it's a `private` field on the page today. Change its declaration in `swimmer-profile.page.ts` from `private readonly language` to `protected readonly language` so the template can read `language.lang()`.

- [ ] **Step 3: Add i18n keys (en + ar)**

In `en.json`, add under `swimmerProfile` (sibling of `records`/`healthMonitoring`):
```json
    "attendance": {
      "subtitle": "Monthly training attendance.",
      "rate": "Attendance Rate",
      "monthly": "Monthly Attendance",
      "noRecords": "No attendance recorded yet.",
      "noNote": "No note recorded for this day.",
      "legend": { "present": "Present", "late": "Late", "absent": "Absent", "excused": "Excused" }
    },
```
In `ar.json`, add the matching block:
```json
    "attendance": {
      "subtitle": "سجل الحضور الشهري للتدريب.",
      "rate": "معدل الحضور",
      "monthly": "الحضور الشهري",
      "noRecords": "لا يوجد حضور مسجل بعد.",
      "noNote": "لا توجد ملاحظة لهذا اليوم.",
      "legend": { "present": "حاضر", "late": "متأخر", "absent": "غائب", "excused": "بعذر" }
    },
```
(Validate JSON after editing: `cd frontend && node -e "require('./src/app/core/i18n/en.json'); require('./src/app/core/i18n/ar.json'); console.log('json ok')"`.)

- [ ] **Step 4: Build the frontend + run the swimmer-profile spec suite**

Run: `cd frontend && npx ng build --configuration development`
Expected: build succeeds (no template/type errors).
Run: `cd frontend && npx ng test --watch=false --include='**/swimmer-profile/**/*.spec.ts'`
Expected: PASS.

- [ ] **Step 5: Manual visual check against the mock**

Start the frontend + backend (local), open a swimmer profile (`/swimmers/<id>`), click **Attendance**. With local data empty you'll see the empty state; after Task 9 seeds Aiven (and if the app points at that data) the calendar renders colored days + a 92%-style rate + a per-day note card, matching `Desktop/mcp/2.png` and `3.png`. **Do not commit.**

---

## Task 9: Aiven seed — statuses + attendance for all swimmers

**Files:**
- Create: `scripts/seed-attendance-aiven.sql`

**Interfaces:**
- Consumes: existing `identity.swimmer_profile`, `identity.app_user`, `reference.role`; the `reference.attendance_status` + `attendance.attendance_record` tables created by the migrations from Tasks 1 & 2.

- [ ] **Step 1: Write the seed script**

Create `scripts/seed-attendance-aiven.sql`:
```sql
-- seed-attendance-aiven.sql
-- Seeds reference.attendance_status (4 statuses, fixed ids) and ~6 weeks of weekday
-- attendance.attendance_record rows for EVERY swimmer. Idempotent (safe to re-run).
-- NOTE: EF column names are PascalCase -> must be double-quoted in Postgres.

BEGIN;

-- pgcrypto provides gen_random_uuid() (built-in on PG13+, this is a safety net).
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- 1) Attendance statuses (fixed ids so records reference them deterministically).
INSERT INTO reference.attendance_status ("Id","Code","NameEn","NameAr") VALUES
  ('11111111-1111-1111-1111-111111111101','present','Present','حاضر'),
  ('11111111-1111-1111-1111-111111111102','late','Late','متأخر'),
  ('11111111-1111-1111-1111-111111111103','absent','Absent','غائب'),
  ('11111111-1111-1111-1111-111111111104','excused','Excused','بعذر')
ON CONFLICT ("Code") DO NOTHING;

-- 2) ~6 weeks of weekday records for every swimmer.
WITH days AS (
  SELECT d::date AS session_date
  FROM generate_series(CURRENT_DATE - INTERVAL '42 days', CURRENT_DATE, INTERVAL '1 day') AS d
  WHERE EXTRACT(ISODOW FROM d) < 6            -- Mon..Fri only
),
coach AS (
  SELECT "Id" AS id FROM identity.app_user
  WHERE "RoleId" = (SELECT "Id" FROM reference.role WHERE "Code" = 'head_coach')
  ORDER BY "Id" LIMIT 1
),
sids AS (
  SELECT
    (SELECT "Id" FROM reference.attendance_status WHERE "Code"='present') AS present,
    (SELECT "Id" FROM reference.attendance_status WHERE "Code"='late')    AS late,
    (SELECT "Id" FROM reference.attendance_status WHERE "Code"='absent')  AS absent,
    (SELECT "Id" FROM reference.attendance_status WHERE "Code"='excused') AS excused
)
INSERT INTO attendance.attendance_record
  ("Id","SwimmerId","SessionDate","StatusId","RecordedBy","CoachNoteEn","CoachNoteAr")
SELECT
  gen_random_uuid(),
  sp."Id",
  d.session_date,
  CASE abs(hashtextextended(sp."Id"::text || d.session_date::text, 0)) % 10
    WHEN 0 THEN sids.absent
    WHEN 1 THEN sids.excused
    WHEN 2 THEN sids.late
    ELSE sids.present
  END,
  (SELECT id FROM coach),
  CASE WHEN abs(hashtextextended(sp."Id"::text || d.session_date::text, 0)) % 10 = 1
       THEN 'Excused — family notified, travel.' END,
  CASE WHEN abs(hashtextextended(sp."Id"::text || d.session_date::text, 0)) % 10 = 1
       THEN 'بعذر — تم إبلاغ العائلة، سفر.' END
FROM identity.swimmer_profile sp
CROSS JOIN days d
CROSS JOIN sids
WHERE (SELECT id FROM coach) IS NOT NULL
ON CONFLICT ("SwimmerId","SessionDate") DO NOTHING;

COMMIT;

-- Verify:
--   SELECT count(*) FROM attendance.attendance_record;
--   SELECT s."Code", count(*) FROM attendance.attendance_record r
--     JOIN reference.attendance_status s ON s."Id" = r."StatusId" GROUP BY s."Code" ORDER BY 1;
```

- [ ] **Step 2: Apply the migrations to Aiven (needs the Aiven password)**

The Aiven password is not stored. Ask the user for it (or have them run these via `!`). With the API stopped, from `backend/`:
```bash
# Replace <PW> with the Aiven password.
AIVEN='Host=kheprx-service-kheprx.b.aivencloud.com;Port=14647;Database=defaultdb;Username=avnadmin;Password=<PW>;SSL Mode=Require;Trust Server Certificate=true'
dotnet ef database update --context IdentityDbContext \
  --project src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --startup-project Kheprx.BaseBackend.Api --connection "$AIVEN"
dotnet ef database update --context AttendanceDbContext \
  --project src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Infrastructure \
  --startup-project Kheprx.BaseBackend.Api --connection "$AIVEN"
```
Expected: `reference.attendance_status` and `attendance.attendance_record` now exist on Aiven. Verify with `dotnet ef migrations list --context AttendanceDbContext … --connection "$AIVEN"` → 0 pending.

- [ ] **Step 3: Precondition check — Aiven must have swimmers + a head coach**

The seed cross-joins `identity.swimmer_profile` and picks a `head_coach` from `app_user`. Migrations create empty tables; the identity **rows** are seeded by the app's `IdentitySeeder` at startup (against whatever DB the app runs on), so a freshly-migrated Aiven may have **no** swimmer/coach rows. Verify first:
```bash
PGPASSWORD='<PW>' psql \
  "host=kheprx-service-kheprx.b.aivencloud.com port=14647 dbname=defaultdb user=avnadmin sslmode=require" \
  -c 'SELECT count(*) AS swimmers FROM identity.swimmer_profile;' \
  -c $'SELECT count(*) AS head_coaches FROM identity.app_user WHERE "RoleId" = (SELECT "Id" FROM reference.role WHERE "Code"=\'head_coach\');'
```
Expected: `swimmers > 0` and `head_coaches >= 1`. **If either is 0**, the identity data isn't on Aiven yet — run the API once pointed at Aiven (set `ConnectionStrings:Postgres` to the Aiven connection, start the app so `IdentitySeeder` runs, then stop it), or seed identity by another agreed means, before proceeding. Do not "fix" this inside the attendance seed script.

- [ ] **Step 4: Run the seed script against Aiven**

```bash
# Requires psql. <PW> = Aiven password.
PGPASSWORD='<PW>' psql \
  "host=kheprx-service-kheprx.b.aivencloud.com port=14647 dbname=defaultdb user=avnadmin sslmode=require" \
  -v ON_ERROR_STOP=1 -f scripts/seed-attendance-aiven.sql
```
Expected: `INSERT 0 N` lines (N > 0) and a final verify count > 0. Re-running inserts 0 additional rows (idempotent).

- [ ] **Step 5: Verify end-to-end**

Point the app at Aiven (or query directly) and open a swimmer's **Attendance** tab: the calendar shows colored weekdays for the last ~6 weeks, a computed rate, and clicking an excused/absent day with a note shows the coach note + recorder name. Confirm the status counts query returns all four (or at least present/absent/excused/late) codes. **Do not commit anything until the user says so.**

---

## Notes for the executor

- **Order matters:** Tasks 1→4 are backend (each independently testable); 5→8 are frontend; 9 is seeding (gated on the Aiven password). Frontend Tasks 5–8 can be built and unit-tested without Aiven; the live calendar needs Task 9's data.
- **DLL lock:** stop the running API before any `dotnet ef`, `dotnet build`, or `dotnet test` step.
- **Migration drift:** when creating each migration, open the generated `Up`/`Down` and confirm it only touches the intended table (no stray model changes). If it includes unrelated drift, investigate before proceeding.
- **No commits** at any step. The user will say when to commit.
