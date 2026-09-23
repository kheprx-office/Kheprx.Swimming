# Championships List page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a read-only Championships list page — a filterable table of championship events — backed by a new `Championships` backend module and a `reference.competition_status` lookup, with demo seed data on Aiven.

**Architecture:** A new read-only bounded-context module (`Championships`) cloned from the existing `Attendance` module (Domain / Application / Infrastructure / Contracts + its own `DbContext` + migration). A `competition_status` reference lookup is added to the `Identity` module. `GET /api/championships` returns events with their status code/names resolved at the API layer (same seam `AttendanceRecordsController` uses to resolve recorder names). The frontend fills in the existing `features/championships` placeholder using the same clean-architecture layering as `features/attendance`.

**Tech Stack:** .NET 10 (ASP.NET Core, EF Core + Npgsql), Angular (standalone components, signals), Tailwind. Backend tests: xUnit + Moq + EF Core InMemory. Frontend tests: Jest.

**Spec:** `docs/superpowers/specs/2026-09-23-championships-list-design.md`

## Global Constraints

- **HOLD ALL COMMITS.** The user said "don't commit anything until I tell you." Complete every step in each task **except** the `git commit` step — leave changes uncommitted and report progress. Run the commits only once the user explicitly authorizes. When authorized: we are on the default branch `main`, so **create `feat/championships-list` first**, then commit task-by-task.
- **`dotnet` build / `dotnet ef` require the running API to be stopped** (it locks the output DLLs). Stop any `dotnet run` / dev server before building or running EF commands.
- **EF naming convention (verbatim):** schema + table are snake_case via `builder.ToTable("<table>", "<schema>")`; **columns default to PascalCase** property names. Seeds/SQL must double-quote PascalCase columns (`"Id"`, `"NameEn"`, …).
- **Cross-module references are loose `Guid`s** — no EF navigation/FK across module boundaries (`StatusId` → `reference.competition_status`, `CreatedBy` → `identity.app_user`).
- **Read-only feature:** no create/update/delete of events in this pass. Entities get a public constructor only so tests and seeds can build them.
- **Migrations** are generated with the design-time factory via `--project`/`--startup-project`; applied to the **live Aiven DB `Swimming_Production`** with `--connection` and the Aiven password, which the executor must **request from the user at run time** (per-session secret, needs the user's approval).
- Frontend uses **standalone components + signals**; tests are **Jest, not Karma**.

---

## File Structure

**Backend — new module `backend/src/Modules/Championships/`**
- `Kheprx.BaseBackend.Championships.Domain/` — `Entities/CompetitionEvent.cs`, `Repositories/ICompetitionEventRepository.cs`, csproj
- `Kheprx.BaseBackend.Championships.Application/` — `DTOs/CompetitionEventDtos.cs`, `Services/Interfaces/IChampionshipService.cs`, `Services/ChampionshipService.cs`, `Resources/ChampionshipMessages.cs`, csproj
- `Kheprx.BaseBackend.Championships.Infrastructure/` — `Data/ChampionshipsDbContext.cs`, `Data/ChampionshipsDbContextFactory.cs`, `Configurations/CompetitionEventConfiguration.cs`, `Repositories/CompetitionEventRepository.cs`, `Extensions/ChampionshipsModuleExtensions.cs`, `Migrations/…`, csproj
- `Kheprx.BaseBackend.Championships.Contracts/` — empty shell csproj (module-shape parity)

**Backend — Identity additions**
- `Entities/CompetitionStatus.cs`, `Repositories/ICompetitionStatusRepository.cs`, `Infrastructure/Configurations/CompetitionStatusConfiguration.cs`, `Infrastructure/Repositories/CompetitionStatusRepository.cs`, DbSet + DI + seeder + reference-service method + `/api/reference/competition-statuses`

**Backend — API host**
- `Controllers/ChampionshipsController.cs`, `Program.cs` (+2 lines), `Extensions/Data/MigrationExtensions.cs` (+1 method)

**Backend — tests**
- `backend/tests/Kheprx.BaseBackend.Championships.UnitTests/` (Entity, Repository, Service)
- `backend/tests/Kheprx.BaseBackend.Api.UnitTests/ChampionshipsControllerTests.cs`, `ReferenceControllerCompetitionStatusesTests.cs`

**Frontend — `frontend/src/app/features/championships/`**
- `domain/model/championship.ts`, `domain/repositories/championships.repository.ts`, `domain/usecases/load-championships.use-case.ts`
- `data/dto/competition-event.dto.ts`, `data/dto/competition-event.mapper.ts`, `data/repositories/championships.repository.impl.ts`, `data/championships.providers.ts`
- `presentation/pages/championships/championships.viewmodel.ts`, `format-date-range.ts`, `championships.page.ts` (rewrite), `championships.page.html` (rewrite)
- `index.ts` (add VM export), `testing/**` specs
- `app.routes.ts`, `app.config.ts`, `core/i18n/en.json`, `core/i18n/ar.json`

**Seed**
- `scripts/seed-championships-aiven.sql`

---

## Task 1: Identity — `competition_status` reference lookup (entity, config, repo, DbSet, DI, seeder, migration)

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/CompetitionStatus.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/ICompetitionStatusRepository.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/CompetitionStatusConfiguration.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/CompetitionStatusRepository.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentityDbContext.cs:25` (add DbSet)
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs:42` (register repo)
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentitySeeder.cs:27,190` (Ensure method + call)

**Interfaces:**
- Produces: `CompetitionStatus` (`Id, Code, NameEn, NameAr`, public ctor `(string code, string nameEn, string? nameAr = null)`); `ICompetitionStatusRepository.GetAllAsync(CancellationToken)`; `IdentityDbContext.CompetitionStatuses`.

> No unit test in this task — the Identity module has no unit-test project, mirroring how `attendance_status` is covered (its endpoint is tested in Task 2). Verification is `dotnet build` + successful migration generation.

- [ ] **Step 1: Create the entity** (clone of `AttendanceStatus.cs`)

```csharp
namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class CompetitionStatus
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }

    private CompetitionStatus() { } // EF Core

    public CompetitionStatus(string code, string nameEn, string? nameAr = null)
    {
        Id = Guid.NewGuid();
        Code = code.Trim();
        NameEn = nameEn.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
    }
}
```

- [ ] **Step 2: Create the repository interface**

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
namespace Kheprx.BaseBackend.Identity.Domain.Repositories;
public interface ICompetitionStatusRepository
{
    Task<IReadOnlyList<CompetitionStatus>> GetAllAsync(CancellationToken ct = default);
}
```

- [ ] **Step 3: Create the EF configuration**

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class CompetitionStatusConfiguration : IEntityTypeConfiguration<CompetitionStatus>
{
    public void Configure(EntityTypeBuilder<CompetitionStatus> builder)
    {
        builder.ToTable("competition_status", "reference");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(s => s.Code).IsUnique();
        builder.Property(s => s.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(s => s.NameAr).HasMaxLength(100);
    }
}
```

- [ ] **Step 4: Create the repository**

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class CompetitionStatusRepository : ICompetitionStatusRepository
{
    private readonly IdentityDbContext _db;
    public CompetitionStatusRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<CompetitionStatus>> GetAllAsync(CancellationToken ct = default)
        => await _db.CompetitionStatuses.AsNoTracking().OrderBy(s => s.Code).ToListAsync(ct);
}
```

- [ ] **Step 5: Add the DbSet** to `IdentityDbContext.cs` (right after the `AttendanceStatuses` line at :25)

```csharp
    public DbSet<CompetitionStatus> CompetitionStatuses => Set<CompetitionStatus>();
```

- [ ] **Step 6: Register the repository** in `IdentityModuleExtensions.cs` (after the `IAttendanceStatusRepository` line at :42)

```csharp
        services.AddScoped<ICompetitionStatusRepository, CompetitionStatusRepository>();
```

- [ ] **Step 7: Seed the two statuses** in `IdentitySeeder.cs` — add the call after `EnsureAttendanceStatuses` (:27):

```csharp
        await EnsureCompetitionStatuses(db, ct);
```

and add the method next to `EnsureAttendanceStatuses` (after :190):

```csharp
    private static async Task EnsureCompetitionStatuses(IdentityDbContext db, CancellationToken ct)
    {
        var rows = new (string Code, string En, string Ar)[]
        {
            ("upcoming", "Upcoming", "قادمة"),
            ("completed", "Completed", "مكتملة"),
        };
        foreach (var (code, en, ar) in rows)
            if (!await db.CompetitionStatuses.AnyAsync(s => s.Code == code, ct))
                await db.CompetitionStatuses.AddAsync(new CompetitionStatus(code, en, ar), ct);
    }
```

- [ ] **Step 8: Build** (API must be stopped)

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: build succeeds.

- [ ] **Step 9: Generate the migration**

Run:
```bash
dotnet ef migrations add CreateCompetitionStatusTable \
  --project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --startup-project backend/Kheprx.BaseBackend.Api \
  --context IdentityDbContext -o Migrations
```
Expected: a `…_CreateCompetitionStatusTable.cs` migration is created that `EnsureSchema("reference")` + `CreateTable("competition_status", …)` with a unique index on `Code`. Open it and confirm.

- [ ] **Step 10: Commit** (HOLD until user authorizes — see Global Constraints)

```bash
git add backend/src/Modules/Identity
git commit -m "feat(championships): add reference.competition_status lookup (entity, repo, seed, migration)"
```

---

## Task 2: Identity — reference service method + `/api/reference/competition-statuses` endpoint

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/IReferenceService.cs:14`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/ReferenceService.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Resources/ReferenceMessages.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/ReferenceController.cs:107`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/ReferenceControllerCompetitionStatusesTests.cs`

**Interfaces:**
- Consumes: `ICompetitionStatusRepository` (Task 1).
- Produces: `IReferenceService.GetCompetitionStatusesAsync(CancellationToken)` → `IReadOnlyList<CodedLookupDto>`; `ReferenceController.CompetitionStatuses(CancellationToken)`; `ReferenceMessages.Success.CompetitionStatusesListed(string lang)`.

- [ ] **Step 1: Write the failing controller test** (clone of `ReferenceControllerAttendanceStatusesTests.cs`)

```csharp
using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class ReferenceControllerCompetitionStatusesTests
{
    [Fact]
    public async Task CompetitionStatuses_returns_200_with_data()
    {
        var svc = new Mock<IReferenceService>();
        svc.Setup(s => s.GetCompetitionStatusesAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new[] { new CodedLookupDto(Guid.NewGuid(), "upcoming", "Upcoming", "قادمة") });
        var controller = new ReferenceController(svc.Object);

        var result = await controller.CompetitionStatuses(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<CodedLookupDto>>>(ok.Value);
        Assert.Single(body.Data!);
        Assert.Equal("upcoming", body.Data![0].Code);
    }
}
```

- [ ] **Step 2: Run it — expect a compile failure** (`GetCompetitionStatusesAsync`/`CompetitionStatuses` don't exist)

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter CompetitionStatuses`
Expected: FAIL to compile.

- [ ] **Step 3: Add the interface method** in `IReferenceService.cs` (after :14)

```csharp
    Task<IReadOnlyList<CodedLookupDto>> GetCompetitionStatusesAsync(CancellationToken ct = default);
```

- [ ] **Step 4: Implement it** in `ReferenceService.cs` — add the field, ctor param, and method:

Add field (after `_attendanceStatuses` at :16):
```csharp
    private readonly ICompetitionStatusRepository _competitionStatuses;
```
Add ctor param (extend the constructor signature at :18–24 and assign):
```csharp
        IAttendanceStatusRepository attendanceStatuses,
        ICompetitionStatusRepository competitionStatuses)
    {
        // …existing assignments…
        _attendanceStatuses = attendanceStatuses;
        _competitionStatuses = competitionStatuses;
    }
```
Add method (after `GetAttendanceStatusesAsync` at :58):
```csharp
    public async Task<IReadOnlyList<CodedLookupDto>> GetCompetitionStatusesAsync(CancellationToken ct = default)
        => (await _competitionStatuses.GetAllAsync(ct)).Select(s => new CodedLookupDto(s.Id, s.Code, s.NameEn, s.NameAr)).ToList();
```

- [ ] **Step 5: Add the localized message** in `ReferenceMessages.cs` (next to `AttendanceStatusesListed`)

```csharp
        public static string CompetitionStatusesListed(string lang) => lang switch { "ar" => "حالات البطولات", _ => "Competition statuses" };
```
> If `ReferenceMessages` nests messages under a `Success` class (as in the sibling modules), place it there to match; otherwise match the surrounding style in that file.

- [ ] **Step 6: Add the controller endpoint** in `ReferenceController.cs` (after the `AttendanceStatuses` action at :107)

```csharp
    /// <summary>Lists all competition statuses (upcoming / completed).</summary>
    [HttpGet("competition-statuses")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CodedLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CodedLookupDto>>>> CompetitionStatuses(CancellationToken ct)
    {
        var data = await _service.GetCompetitionStatusesAsync(ct);
        var body = ApiResponse<IReadOnlyList<CodedLookupDto>>.Success(
            ReferenceMessages.Success.CompetitionStatusesListed(AppLanguage.Current), data);
        return Ok(body);
    }
```

- [ ] **Step 7: Run the test — expect PASS**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter CompetitionStatuses`
Expected: PASS.

- [ ] **Step 8: Commit** (HOLD until authorized)

```bash
git add backend/src/Modules/Identity backend/Kheprx.BaseBackend.Api backend/tests/Kheprx.BaseBackend.Api.UnitTests
git commit -m "feat(championships): expose GET /api/reference/competition-statuses"
```

---

## Task 3: Championships module scaffold + Domain (entity + repository interface + unit-test project)

**Files:**
- Create csproj: `…/Championships.Domain`, `…/Championships.Application`, `…/Championships.Infrastructure`, `…/Championships.Contracts`, and `backend/tests/Kheprx.BaseBackend.Championships.UnitTests`
- Create: `…/Championships.Domain/Entities/CompetitionEvent.cs`, `…/Championships.Domain/Repositories/ICompetitionEventRepository.cs`
- Create: `backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Entities/CompetitionEventTests.cs`
- Modify: `backend/Kheprx.BaseBackend.sln` (add all 5 projects via `dotnet sln add`)

**Interfaces:**
- Produces: `CompetitionEvent` entity with public ctor `(string nameEn, string? nameAr, DateOnly startDate, DateOnly endDate, string locationEn, string? locationAr, Guid statusId, Guid createdBy)` and read-only props (`Id, NameEn, NameAr, StartDate, EndDate, LocationEn, LocationAr, StatusId, CreatedBy`); `ICompetitionEventRepository.ListAsync(CancellationToken)` → newest `StartDate` first.

- [ ] **Step 1: Create the four module csproj files** (hand-authored to match the Attendance module exactly — do NOT use `dotnet new`, which would duplicate the centrally-managed `TargetFramework`).

`…/Championships.Domain/Kheprx.BaseBackend.Championships.Domain.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\..\SharedKernel\Kheprx.BaseBackend.SharedKernel\Kheprx.BaseBackend.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

`…/Championships.Contracts/Kheprx.BaseBackend.Championships.Contracts.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

</Project>
```

`…/Championships.Application/Kheprx.BaseBackend.Championships.Application.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);1591</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Kheprx.BaseBackend.Championships.Domain\Kheprx.BaseBackend.Championships.Domain.csproj" />
    <ProjectReference Include="..\..\..\SharedKernel\Kheprx.BaseBackend.SharedKernel\Kheprx.BaseBackend.SharedKernel.csproj" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Kheprx.BaseBackend.Championships.Infrastructure" />
    <InternalsVisibleTo Include="Kheprx.BaseBackend.Championships.UnitTests" />
  </ItemGroup>
</Project>
```

`…/Championships.Infrastructure/Kheprx.BaseBackend.Championships.Infrastructure.csproj`:
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
    <ProjectReference Include="..\Kheprx.BaseBackend.Championships.Domain\Kheprx.BaseBackend.Championships.Domain.csproj" />
    <ProjectReference Include="..\Kheprx.BaseBackend.Championships.Application\Kheprx.BaseBackend.Championships.Application.csproj" />
    <ProjectReference Include="..\Kheprx.BaseBackend.Championships.Contracts\Kheprx.BaseBackend.Championships.Contracts.csproj" />
    <ProjectReference Include="..\..\..\SharedKernel\Kheprx.BaseBackend.SharedKernel\Kheprx.BaseBackend.SharedKernel.csproj" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Kheprx.BaseBackend.Championships.UnitTests" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the unit-test csproj** `backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Kheprx.BaseBackend.Championships.UnitTests.csproj` (clone of the Attendance one)

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
    <ProjectReference Include="..\..\src\Modules\Championships\Kheprx.BaseBackend.Championships.Domain\Kheprx.BaseBackend.Championships.Domain.csproj" />
    <ProjectReference Include="..\..\src\Modules\Championships\Kheprx.BaseBackend.Championships.Application\Kheprx.BaseBackend.Championships.Application.csproj" />
    <ProjectReference Include="..\..\src\Modules\Championships\Kheprx.BaseBackend.Championships.Infrastructure\Kheprx.BaseBackend.Championships.Infrastructure.csproj" />
    <ProjectReference Include="..\..\src\SharedKernel\Kheprx.BaseBackend.SharedKernel\Kheprx.BaseBackend.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Add all five projects to the solution**

Run:
```bash
dotnet sln backend/Kheprx.BaseBackend.sln add \
  backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Domain \
  backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Application \
  backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Contracts \
  backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure \
  backend/tests/Kheprx.BaseBackend.Championships.UnitTests
```

- [ ] **Step 4: Write the entity**

`…/Championships.Domain/Entities/CompetitionEvent.cs`:
```csharp
namespace Kheprx.BaseBackend.Championships.Domain.Entities;

public sealed class CompetitionEvent
{
    public Guid Id { get; private set; }
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string LocationEn { get; private set; } = string.Empty;
    public string? LocationAr { get; private set; }
    public Guid StatusId { get; private set; }
    public Guid CreatedBy { get; private set; }

    private CompetitionEvent() { } // EF Core

    public CompetitionEvent(string nameEn, string? nameAr, DateOnly startDate, DateOnly endDate,
        string locationEn, string? locationAr, Guid statusId, Guid createdBy)
    {
        Id = Guid.NewGuid();
        NameEn = nameEn.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
        StartDate = startDate;
        EndDate = endDate;
        LocationEn = locationEn.Trim();
        LocationAr = string.IsNullOrWhiteSpace(locationAr) ? null : locationAr.Trim();
        StatusId = statusId;
        CreatedBy = createdBy;
    }
}
```

- [ ] **Step 5: Write the repository interface**

`…/Championships.Domain/Repositories/ICompetitionEventRepository.cs`:
```csharp
using Kheprx.BaseBackend.Championships.Domain.Entities;
namespace Kheprx.BaseBackend.Championships.Domain.Repositories;
public interface ICompetitionEventRepository
{
    Task<IReadOnlyList<CompetitionEvent>> ListAsync(CancellationToken ct = default);
}
```

- [ ] **Step 6: Write the failing entity test**

`backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Entities/CompetitionEventTests.cs`:
```csharp
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Entities;

public class CompetitionEventTests
{
    [Fact]
    public void Ctor_assigns_id_trims_names_and_collapses_blank_arabic()
    {
        var statusId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var ev = new CompetitionEvent("  Nats  ", "   ", new DateOnly(2023, 11, 15),
            new DateOnly(2023, 11, 16), "  Cairo  ", null, statusId, createdBy);

        Assert.NotEqual(Guid.Empty, ev.Id);
        Assert.Equal("Nats", ev.NameEn);
        Assert.Null(ev.NameAr);              // whitespace-only → null
        Assert.Equal("Cairo", ev.LocationEn);
        Assert.Equal(statusId, ev.StatusId);
        Assert.Equal(createdBy, ev.CreatedBy);
        Assert.Equal(new DateOnly(2023, 11, 16), ev.EndDate);
    }
}
```

- [ ] **Step 7: Run tests — expect PASS** (entity already implemented)

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests`
Expected: PASS (1 test). Confirms the whole module graph compiles + the sln is wired.

- [ ] **Step 8: Commit** (HOLD until authorized)

```bash
git add backend/src/Modules/Championships backend/tests/Kheprx.BaseBackend.Championships.UnitTests backend/Kheprx.BaseBackend.sln
git commit -m "feat(championships): scaffold Championships module + CompetitionEvent domain"
```

---

## Task 4: Championships Infrastructure — DbContext, factory, config, repository, module extension, migration

**Files:**
- Create: `…/Championships.Infrastructure/Data/ChampionshipsDbContext.cs`, `Data/ChampionshipsDbContextFactory.cs`, `Configurations/CompetitionEventConfiguration.cs`, `Repositories/CompetitionEventRepository.cs`, `Extensions/ChampionshipsModuleExtensions.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Repositories/CompetitionEventRepositoryTests.cs`

**Interfaces:**
- Consumes: `CompetitionEvent`, `ICompetitionEventRepository` (Task 3).
- Produces: `ChampionshipsDbContext.CompetitionEvents`; `CompetitionEventRepository`; `AddChampionshipsModule(IServiceCollection, IConfiguration)`.

- [ ] **Step 1: Write the DbContext**

```csharp
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Data;

public sealed class ChampionshipsDbContext : DbContext
{
    public ChampionshipsDbContext(DbContextOptions<ChampionshipsDbContext> options) : base(options) { }

    public DbSet<CompetitionEvent> CompetitionEvents => Set<CompetitionEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("championships");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChampionshipsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

- [ ] **Step 2: Write the design-time factory** (clone of `AttendanceDbContextFactory`)

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Data;

// Design-time factory used by `dotnet ef`. Uses a local-dev connection string;
// at runtime the application uses ConnectionStrings:Postgres from configuration instead.
public sealed class ChampionshipsDbContextFactory : IDesignTimeDbContextFactory<ChampionshipsDbContext>
{
    public ChampionshipsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ChampionshipsDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=basebackend;Username=postgres;Password=postgres")
            .Options;
        return new ChampionshipsDbContext(options);
    }
}
```

- [ ] **Step 3: Write the EF configuration**

```csharp
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Configurations;

internal sealed class CompetitionEventConfiguration : IEntityTypeConfiguration<CompetitionEvent>
{
    public void Configure(EntityTypeBuilder<CompetitionEvent> builder)
    {
        builder.ToTable("competition_event", "championships");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.NameEn).IsRequired();
        builder.Property(e => e.NameAr);
        builder.Property(e => e.StartDate).IsRequired();     // DateOnly → date
        builder.Property(e => e.EndDate).IsRequired();
        builder.Property(e => e.LocationEn).IsRequired();
        builder.Property(e => e.LocationAr);
        builder.Property(e => e.StatusId).IsRequired();      // loose Guid → reference.competition_status
        builder.Property(e => e.CreatedBy).IsRequired();     // loose Guid → identity.app_user
    }
}
```

- [ ] **Step 4: Write the repository**

```csharp
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Repositories;

internal sealed class CompetitionEventRepository : ICompetitionEventRepository
{
    private readonly ChampionshipsDbContext _db;
    public CompetitionEventRepository(ChampionshipsDbContext db) => _db = db;

    public async Task<IReadOnlyList<CompetitionEvent>> ListAsync(CancellationToken ct = default)
        => await _db.CompetitionEvents.AsNoTracking()
              .OrderByDescending(e => e.StartDate).ThenByDescending(e => e.Id)
              .ToListAsync(ct);
}
```

- [ ] **Step 5: Write the module extension** (note: `IChampionshipService`/`ChampionshipService` come in Task 5 — register only the repo now, add the service line in Task 5)

```csharp
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Kheprx.BaseBackend.Championships.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Extensions;

public static class ChampionshipsModuleExtensions
{
    public static IServiceCollection AddChampionshipsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ChampionshipsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddScoped<ICompetitionEventRepository, CompetitionEventRepository>();
        return services;
    }
}
```

- [ ] **Step 6: Write the failing repository test**

`backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Repositories/CompetitionEventRepositoryTests.cs`:
```csharp
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Kheprx.BaseBackend.Championships.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Repositories;

public class CompetitionEventRepositoryTests
{
    private static ChampionshipsDbContext NewDb()
        => new(new DbContextOptionsBuilder<ChampionshipsDbContext>().UseInMemoryDatabase($"champ-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task ListAsync_returns_all_events_newest_start_first()
    {
        await using var db = NewDb();
        var sid = Guid.NewGuid();
        var cid = Guid.NewGuid();
        db.CompetitionEvents.Add(new CompetitionEvent("Oct", null, new DateOnly(2023, 10, 20), new DateOnly(2023, 10, 20), "Alex", null, sid, cid));
        db.CompetitionEvents.Add(new CompetitionEvent("Jan", null, new DateOnly(2024, 1, 20), new DateOnly(2024, 1, 21), "Giza", null, sid, cid));
        db.CompetitionEvents.Add(new CompetitionEvent("Nov", null, new DateOnly(2023, 11, 15), new DateOnly(2023, 11, 16), "Cairo", null, sid, cid));
        await db.SaveChangesAsync();

        var list = await new CompetitionEventRepository(db).ListAsync();

        Assert.Equal(3, list.Count);
        Assert.Equal("Jan", list[0].NameEn);   // 2024-01-20 newest
        Assert.Equal("Nov", list[1].NameEn);
        Assert.Equal("Oct", list[2].NameEn);
    }
}
```

- [ ] **Step 7: Run it — expect PASS**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests --filter CompetitionEventRepositoryTests`
Expected: PASS.

- [ ] **Step 8: Generate the migration**

Run:
```bash
dotnet ef migrations add CreateCompetitionEventTable \
  --project backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure \
  --startup-project backend/Kheprx.BaseBackend.Api \
  --context ChampionshipsDbContext -o Migrations
```
Expected: a `…_CreateCompetitionEventTable.cs` with `EnsureSchema("championships")` + `CreateTable("competition_event", …)`. Open it and confirm the columns are PascalCase (`NameEn`, `StartDate`, …).

> The startup project (`Kheprx.BaseBackend.Api`) must already reference the Championships infrastructure for `--startup-project` to resolve the context. If EF can't find the context, do Task 6 Step 3 (Api.csproj references) first, then return here.

- [ ] **Step 9: Commit** (HOLD until authorized)

```bash
git add backend/src/Modules/Championships backend/tests/Kheprx.BaseBackend.Championships.UnitTests
git commit -m "feat(championships): infrastructure (DbContext, repository, competition_event migration)"
```

---

## Task 5: Championships Application — DTO, service, messages

**Files:**
- Create: `…/Championships.Application/DTOs/CompetitionEventDtos.cs`, `Services/Interfaces/IChampionshipService.cs`, `Services/ChampionshipService.cs`, `Resources/ChampionshipMessages.cs`
- Modify: `…/Championships.Infrastructure/Extensions/ChampionshipsModuleExtensions.cs` (register the service)
- Test: `backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Services/ChampionshipServiceTests.cs`

**Interfaces:**
- Consumes: `ICompetitionEventRepository`, `CompetitionEvent`.
- Produces: `CompetitionEventDto(Guid Id, string NameEn, string? NameAr, DateOnly StartDate, DateOnly EndDate, string LocationEn, string? LocationAr, Guid StatusId, string StatusCode, string StatusNameEn, string? StatusNameAr)`; `IChampionshipService.ListAsync(CancellationToken)` → `IReadOnlyList<CompetitionEventDto>` (status fields empty from the service — enriched in the controller, Task 6); `ChampionshipMessages.Success.Listed(string lang)`.

- [ ] **Step 1: Write the DTO**

```csharp
namespace Kheprx.BaseBackend.Championships.Application.DTOs;

/// <summary>A championship event. Status* are resolved in the API layer (empty from the service).</summary>
public sealed record CompetitionEventDto(
    Guid Id,
    string NameEn,
    string? NameAr,
    DateOnly StartDate,
    DateOnly EndDate,
    string LocationEn,
    string? LocationAr,
    Guid StatusId,
    string StatusCode,
    string StatusNameEn,
    string? StatusNameAr);
```

- [ ] **Step 2: Write the service interface**

```csharp
using Kheprx.BaseBackend.Championships.Application.DTOs;
namespace Kheprx.BaseBackend.Championships.Application.Services.Interfaces;
public interface IChampionshipService
{
    Task<IReadOnlyList<CompetitionEventDto>> ListAsync(CancellationToken ct = default);
}
```

- [ ] **Step 3: Write the messages**

```csharp
namespace Kheprx.BaseBackend.Championships.Application.Resources;

/// <summary>Localized messages for championship queries.</summary>
public static class ChampionshipMessages
{
    public static class Success
    {
        public static string Listed(string lang) => lang switch { "ar" => "البطولات", _ => "Championships" };
    }
}
```

- [ ] **Step 4: Write the failing service test**

`backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Services/ChampionshipServiceTests.cs`:
```csharp
using Kheprx.BaseBackend.Championships.Application.Services;
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Services;

public class ChampionshipServiceTests
{
    [Fact]
    public async Task ListAsync_maps_entities_to_dtos_with_empty_status_fields()
    {
        var statusId = Guid.NewGuid();
        var ev = new CompetitionEvent("Nats", "الوطنية", new DateOnly(2023, 11, 15),
            new DateOnly(2023, 11, 16), "Cairo", null, statusId, Guid.NewGuid());
        var repo = new Mock<ICompetitionEventRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { ev });

        var svc = new ChampionshipService(repo.Object);
        var list = await svc.ListAsync();

        Assert.Single(list);
        Assert.Equal("Nats", list[0].NameEn);
        Assert.Equal(statusId, list[0].StatusId);
        Assert.Equal("", list[0].StatusCode);        // resolved later, in the controller
        Assert.Equal("", list[0].StatusNameEn);
        Assert.Null(list[0].StatusNameAr);
    }
}
```

- [ ] **Step 5: Run it — expect a compile failure** (`ChampionshipService` doesn't exist)

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests --filter ChampionshipServiceTests`
Expected: FAIL to compile.

- [ ] **Step 6: Write the service**

```csharp
using Kheprx.BaseBackend.Championships.Application.DTOs;
using Kheprx.BaseBackend.Championships.Application.Services.Interfaces;
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;

namespace Kheprx.BaseBackend.Championships.Application.Services;

internal sealed class ChampionshipService : IChampionshipService
{
    private readonly ICompetitionEventRepository _events;
    public ChampionshipService(ICompetitionEventRepository events) => _events = events;

    public async Task<IReadOnlyList<CompetitionEventDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await _events.ListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    private static CompetitionEventDto ToDto(CompetitionEvent e) =>
        new(e.Id, e.NameEn, e.NameAr, e.StartDate, e.EndDate, e.LocationEn, e.LocationAr,
            e.StatusId, string.Empty, string.Empty, null);
}
```

- [ ] **Step 7: Register the service** in `ChampionshipsModuleExtensions.cs` — add the using and the registration:

Add `using Kheprx.BaseBackend.Championships.Application.Services;` and `using Kheprx.BaseBackend.Championships.Application.Services.Interfaces;`, then after the repository registration:
```csharp
        services.AddScoped<IChampionshipService, ChampionshipService>();
```

- [ ] **Step 8: Run it — expect PASS**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests --filter ChampionshipServiceTests`
Expected: PASS.

- [ ] **Step 9: Commit** (HOLD until authorized)

```bash
git add backend/src/Modules/Championships backend/tests/Kheprx.BaseBackend.Championships.UnitTests
git commit -m "feat(championships): application service + DTO"
```

---

## Task 6: API — ChampionshipsController + Program.cs + MigrationExtensions wiring

**Files:**
- Create: `backend/Kheprx.BaseBackend.Api/Controllers/ChampionshipsController.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Kheprx.BaseBackend.Api.csproj:21` (add 2 project refs)
- Modify: `backend/Kheprx.BaseBackend.Api/Program.cs:23,36`
- Modify: `backend/Kheprx.BaseBackend.Api/Extensions/Data/MigrationExtensions.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/ChampionshipsControllerTests.cs`

**Interfaces:**
- Consumes: `IChampionshipService.ListAsync` (Task 5), `IReferenceService.GetCompetitionStatusesAsync` (Task 2), `CompetitionEventDto`, `ChampionshipMessages`, `AddChampionshipsModule` (Task 4).
- Produces: `ChampionshipsController.List(CancellationToken)` → `ActionResult<ApiResponse<IReadOnlyList<CompetitionEventDto>>>` at `GET /api/championships`.

- [ ] **Step 1: Add the project references** in `Kheprx.BaseBackend.Api.csproj` (after the Attendance refs at :20–21)

```xml
    <ProjectReference Include="..\src\Modules\Championships\Kheprx.BaseBackend.Championships.Application\Kheprx.BaseBackend.Championships.Application.csproj" />
    <ProjectReference Include="..\src\Modules\Championships\Kheprx.BaseBackend.Championships.Infrastructure\Kheprx.BaseBackend.Championships.Infrastructure.csproj" />
```

- [ ] **Step 2: Write the failing controller test**

`backend/tests/Kheprx.BaseBackend.Api.UnitTests/ChampionshipsControllerTests.cs`:
```csharp
using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Championships.Application.DTOs;
using Kheprx.BaseBackend.Championships.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class ChampionshipsControllerTests
{
    [Fact]
    public async Task List_returns_200_and_resolves_status_code_and_names()
    {
        var statusId = Guid.NewGuid();
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new[]
           {
               new CompetitionEventDto(Guid.NewGuid(), "Nats", null,
                   new DateOnly(2023, 11, 15), new DateOnly(2023, 11, 16),
                   "Cairo", null, statusId, "", "", null),
           });
        var reference = new Mock<IReferenceService>();
        reference.Setup(r => r.GetCompetitionStatusesAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new[] { new CodedLookupDto(statusId, "upcoming", "Upcoming", "قادمة") });

        var controller = new ChampionshipsController(svc.Object, reference.Object);

        var result = await controller.List(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<CompetitionEventDto>>>(ok.Value);
        Assert.Single(body.Data!);
        Assert.Equal("upcoming", body.Data![0].StatusCode);
        Assert.Equal("Upcoming", body.Data![0].StatusNameEn);
        Assert.Equal("قادمة", body.Data![0].StatusNameAr);
    }

    [Fact]
    public async Task List_leaves_status_fields_empty_when_status_id_is_unknown()
    {
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new[]
           {
               new CompetitionEventDto(Guid.NewGuid(), "Orphan", null,
                   new DateOnly(2024, 1, 20), new DateOnly(2024, 1, 21),
                   "Giza", null, Guid.NewGuid(), "", "", null),
           });
        var reference = new Mock<IReferenceService>();
        reference.Setup(r => r.GetCompetitionStatusesAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Array.Empty<CodedLookupDto>());

        var controller = new ChampionshipsController(svc.Object, reference.Object);
        var result = await controller.List(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<CompetitionEventDto>>>(ok.Value);
        Assert.Equal("", body.Data![0].StatusCode);   // no throw, graceful
    }
}
```

- [ ] **Step 3: Run it — expect a compile failure** (`ChampionshipsController` doesn't exist)

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter ChampionshipsControllerTests`
Expected: FAIL to compile.

- [ ] **Step 4: Write the controller**

`backend/Kheprx.BaseBackend.Api/Controllers/ChampionshipsController.cs`:
```csharp
using Kheprx.BaseBackend.Championships.Application.DTOs;
using Kheprx.BaseBackend.Championships.Application.Resources;
using Kheprx.BaseBackend.Championships.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Route("api/championships")]
[Authorize]
public sealed class ChampionshipsController : BaseApiController
{
    private readonly IChampionshipService _service;
    private readonly IReferenceService _reference;

    public ChampionshipsController(IChampionshipService service, IReferenceService reference)
    {
        _service = service;
        _reference = reference;
    }

    /// <summary>Lists all championship events (newest start first), with status code + names resolved.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CompetitionEventDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CompetitionEventDto>>>> List(CancellationToken ct)
    {
        var rows = await _service.ListAsync(ct);
        var statuses = await _reference.GetCompetitionStatusesAsync(ct);
        var byId = statuses.ToDictionary(s => s.Id);

        var enriched = rows.Select(r => byId.TryGetValue(r.StatusId, out var s)
            ? r with { StatusCode = s.Code, StatusNameEn = s.NameEn, StatusNameAr = s.NameAr }
            : r).ToList();

        return Ok(ApiResponse<IReadOnlyList<CompetitionEventDto>>.Success(
            ChampionshipMessages.Success.Listed(AppLanguage.Current), enriched));
    }
}
```

- [ ] **Step 5: Wire the module into `Program.cs`** — add the using at the top (with the other module usings, :7–9):
```csharp
using Kheprx.BaseBackend.Championships.Infrastructure.Extensions;
```
Add the registration after `AddAttendanceModule` (:23):
```csharp
builder.Services.AddChampionshipsModule(builder.Configuration);
```
Add the migration apply after `ApplyAttendanceMigrationsAsync` (:36):
```csharp
await app.ApplyChampionshipsMigrationsAsync();
```

- [ ] **Step 6: Add the migration-apply method** in `MigrationExtensions.cs` — add the using:
```csharp
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
```
and the method after `ApplyAttendanceMigrationsAsync` (:33):
```csharp
    public static async Task ApplyChampionshipsMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChampionshipsDbContext>();
        await db.Database.MigrateAsync();
    }
```

- [ ] **Step 7: Run the controller test — expect PASS**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter ChampionshipsControllerTests`
Expected: PASS (2 tests).

- [ ] **Step 8: Full backend build + test sweep**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: all tests pass (including the ArchitectureTests — the new module follows the same layering as its siblings).

- [ ] **Step 9: Commit** (HOLD until authorized)

```bash
git add backend/Kheprx.BaseBackend.Api backend/tests/Kheprx.BaseBackend.Api.UnitTests
git commit -m "feat(championships): GET /api/championships + module wiring"
```

---

## Task 7: Frontend data layer — model, DTO + validator, mapper

**Files:**
- Create: `frontend/src/app/features/championships/domain/model/championship.ts`
- Create: `frontend/src/app/features/championships/data/dto/competition-event.dto.ts`
- Create: `frontend/src/app/features/championships/data/dto/competition-event.mapper.ts`
- Test: `frontend/src/app/features/championships/testing/data/dto/competition-event.mapper.spec.ts`

**Interfaces:**
- Produces: `Championship` model; `CompetitionEventDtoRs`, `CompetitionEventListDtoRs`, `isCompetitionEventListValid(data): data is CompetitionEventDtoRs[]`; `toChampionship(d)`, `toChampionshipList(list)`.

- [ ] **Step 1: Write the model**

```ts
export interface Championship {
  id: string;
  nameEn: string;
  nameAr: string | null;
  startDate: string;   // 'YYYY-MM-DD'
  endDate: string;     // 'YYYY-MM-DD'
  locationEn: string;
  locationAr: string | null;
  statusId: string;
  statusCode: string;      // 'upcoming' | 'completed'
  statusNameEn: string;
  statusNameAr: string | null;
}
```

- [ ] **Step 2: Write the DTO + validator**

```ts
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface CompetitionEventDtoRs {
  id: string;
  nameEn: string;
  nameAr: string | null;
  startDate: string;
  endDate: string;
  locationEn: string;
  locationAr: string | null;
  statusId: string;
  statusCode: string;
  statusNameEn: string;
  statusNameAr: string | null;
}
export interface CompetitionEventListDtoRs extends BaseResponseRs<CompetitionEventDtoRs[]> {}

export function isCompetitionEventDtoRsValid(x: unknown): x is CompetitionEventDtoRs {
  const d = x as CompetitionEventDtoRs;
  if (!d || typeof d !== 'object') return false;
  return typeof d.id === 'string'
    && typeof d.nameEn === 'string'
    && typeof d.startDate === 'string'
    && typeof d.endDate === 'string'
    && typeof d.locationEn === 'string'
    && typeof d.statusId === 'string'
    && typeof d.statusCode === 'string';
}

export function isCompetitionEventListValid(data: unknown): data is CompetitionEventDtoRs[] {
  return Array.isArray(data) && data.every(isCompetitionEventDtoRsValid);
}
```

- [ ] **Step 3: Write the mapper**

```ts
import { CompetitionEventDtoRs } from '@features/championships/data/dto/competition-event.dto';
import { Championship } from '@features/championships/domain/model/championship';

export function toChampionship(d: CompetitionEventDtoRs): Championship {
  return {
    id: d.id,
    nameEn: d.nameEn,
    nameAr: d.nameAr,
    startDate: d.startDate,
    endDate: d.endDate,
    locationEn: d.locationEn,
    locationAr: d.locationAr,
    statusId: d.statusId,
    statusCode: d.statusCode,
    statusNameEn: d.statusNameEn,
    statusNameAr: d.statusNameAr,
  };
}

export function toChampionshipList(list: CompetitionEventDtoRs[]): Championship[] {
  return list.map(toChampionship);
}
```

- [ ] **Step 4: Write the failing mapper spec** (mirrors `attendance-session.mapper.spec.ts`)

```ts
import { toChampionshipList } from '@features/championships/data/dto/competition-event.mapper';
import { isCompetitionEventListValid } from '@features/championships/data/dto/competition-event.dto';

describe('competition-event mapper', () => {
  const dto = [{
    id: 'e1', nameEn: 'National Junior Championship', nameAr: 'بطولة الناشئين',
    startDate: '2023-11-15', endDate: '2023-11-16',
    locationEn: 'Cairo Olympic Pool', locationAr: null,
    statusId: 'st1', statusCode: 'upcoming', statusNameEn: 'Upcoming', statusNameAr: 'قادمة',
  }];

  it('accepts a valid dto list and maps every field', () => {
    expect(isCompetitionEventListValid(dto)).toBe(true);
    const list = toChampionshipList(dto);
    expect(list).toHaveLength(1);
    expect(list[0].nameEn).toBe('National Junior Championship');
    expect(list[0].endDate).toBe('2023-11-16');
    expect(list[0].statusCode).toBe('upcoming');
    expect(list[0].locationAr).toBeNull();
  });

  it('rejects a malformed dto list', () => {
    expect(isCompetitionEventListValid([{ id: 5 }])).toBe(false);
    expect(isCompetitionEventListValid('nope')).toBe(false);
  });
});
```

- [ ] **Step 5: Run it — expect PASS**

Run: `cd frontend && npx jest competition-event.mapper`
Expected: PASS.

- [ ] **Step 6: Commit** (HOLD until authorized)

```bash
git add frontend/src/app/features/championships
git commit -m "feat(championships): frontend data layer (model, dto, mapper)"
```

---

## Task 8: Frontend repository + use case + DI registration

**Files:**
- Create: `frontend/src/app/features/championships/domain/repositories/championships.repository.ts`
- Create: `frontend/src/app/features/championships/data/repositories/championships.repository.impl.ts`
- Create: `frontend/src/app/features/championships/data/championships.providers.ts`
- Create: `frontend/src/app/features/championships/domain/usecases/load-championships.use-case.ts`
- Modify: `frontend/src/app/app.config.ts` (register providers)
- Test: `frontend/src/app/features/championships/testing/domain/usecases/load-championships.use-case.spec.ts`

**Interfaces:**
- Consumes: `CompetitionEventListDtoRs`, `isCompetitionEventListValid`, `toChampionshipList`, `Championship` (Task 7).
- Produces: `IChampionshipsRepository.getChampionships()`, `CHAMPIONSHIPS_REPOSITORY`; `ChampionshipsRepositoryImpl`; `CHAMPIONSHIPS_PROVIDERS`; `LoadChampionshipsUseCase` (`run(): Promise<Result<Championship[]>>`).

- [ ] **Step 1: Write the repository port + token**

```ts
import { InjectionToken } from '@angular/core';
import { CompetitionEventListDtoRs } from '@features/championships/data/dto/competition-event.dto';

export interface IChampionshipsRepository {
  getChampionships(): Promise<CompetitionEventListDtoRs>;
}

export const CHAMPIONSHIPS_REPOSITORY = new InjectionToken<IChampionshipsRepository>('CHAMPIONSHIPS_REPOSITORY');
```

- [ ] **Step 2: Write the repository impl**

```ts
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { IChampionshipsRepository } from '@features/championships/domain/repositories/championships.repository';
import { CompetitionEventListDtoRs } from '@features/championships/data/dto/competition-event.dto';

@Injectable({ providedIn: 'root' })
export class ChampionshipsRepositoryImpl implements IChampionshipsRepository {
  private readonly http = inject(HttpClientService);

  getChampionships(): Promise<CompetitionEventListDtoRs> {
    return this.http.get<CompetitionEventListDtoRs>('/api/championships');
  }
}
```

- [ ] **Step 3: Write the providers**

```ts
import { Provider } from '@angular/core';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { ChampionshipsRepositoryImpl } from '@features/championships/data/repositories/championships.repository.impl';

export const CHAMPIONSHIPS_PROVIDERS: Provider[] = [
  { provide: CHAMPIONSHIPS_REPOSITORY, useClass: ChampionshipsRepositoryImpl },
];
```

- [ ] **Step 4: Register the providers** in `app.config.ts` — add the import (near :24) and spread it into `providers` (after `...ATTENDANCE_PROVIDERS`, :42):

```ts
import { CHAMPIONSHIPS_PROVIDERS } from '@features/championships/data/championships.providers';
```
```ts
    ...CHAMPIONSHIPS_PROVIDERS,
```

- [ ] **Step 5: Write the use case**

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { isCompetitionEventListValid } from '@features/championships/data/dto/competition-event.dto';
import { toChampionshipList } from '@features/championships/data/dto/competition-event.mapper';
import { Championship } from '@features/championships/domain/model/championship';

@Injectable({ providedIn: 'root' })
export class LoadChampionshipsUseCase extends UseCase<void, Championship[]> {
  private readonly repo = inject(CHAMPIONSHIPS_REPOSITORY);
  constructor() { super('LoadChampionships'); }
  protected async execute(): Promise<Championship[]> {
    const res = await this.repo.getChampionships();
    if (!isCompetitionEventListValid(res.data)) throw new AppError('Invalid championships received', 'validation');
    return toChampionshipList(res.data);
  }
}
```

- [ ] **Step 6: Write the failing use-case spec** (mirrors the attendance use-case specs)

```ts
import { TestBed } from '@angular/core/testing';
import { LoadChampionshipsUseCase } from '@features/championships/domain/usecases/load-championships.use-case';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { CompetitionEventListDtoRs } from '@features/championships/data/dto/competition-event.dto';

const validDto = {
  id: 'e1', nameEn: 'Nats', nameAr: null,
  startDate: '2023-11-15', endDate: '2023-11-16',
  locationEn: 'Cairo', locationAr: null,
  statusId: 'st1', statusCode: 'upcoming', statusNameEn: 'Upcoming', statusNameAr: 'قادمة',
};

function make(getChampionships: () => Promise<CompetitionEventListDtoRs>) {
  TestBed.configureTestingModule({
    providers: [
      LoadChampionshipsUseCase,
      { provide: CHAMPIONSHIPS_REPOSITORY, useValue: { getChampionships } },
    ],
  });
  return TestBed.inject(LoadChampionshipsUseCase);
}

describe('LoadChampionshipsUseCase', () => {
  it('maps a valid list', async () => {
    const uc = make(async () => ({ data: [validDto] } as unknown as CompetitionEventListDtoRs));
    const res = await uc.run();
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data[0].nameEn).toBe('Nats');
  });

  it('fails on an invalid payload', async () => {
    const uc = make(async () => ({ data: [{ id: 5 }] } as unknown as CompetitionEventListDtoRs));
    const res = await uc.run();
    expect(res.ok).toBe(false);
  });
});
```

- [ ] **Step 7: Run it — expect PASS**

Run: `cd frontend && npx jest load-championships`
Expected: PASS.

- [ ] **Step 8: Commit** (HOLD until authorized)

```bash
git add frontend/src/app/features/championships frontend/src/app/app.config.ts
git commit -m "feat(championships): frontend repository, use case, DI wiring"
```

---

## Task 9: Frontend ViewModel + date-range helper

**Files:**
- Create: `frontend/src/app/features/championships/presentation/pages/championships/format-date-range.ts`
- Create: `frontend/src/app/features/championships/presentation/pages/championships/championships.viewmodel.ts`
- Test: `frontend/src/app/features/championships/testing/presentation/pages/championships/format-date-range.spec.ts`
- Test: `frontend/src/app/features/championships/testing/presentation/pages/championships/championships.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `LoadChampionshipsUseCase` (Task 8), `Championship` (Task 7), `LanguageStore` (`lang(): 'en' | 'ar'`).
- Produces: `formatDateRange(startIso, endIso, lang): string`; `ChampionshipsViewModel` with signals `loading`, `error`, `filterFrom`, `filterTo`, computeds `filtered`, `count`, `hasFilter`, method `load()`, setters `setFrom/setTo/clearFilter`, and display helpers `name/location/statusLabel/dateRange/statusBadgeClass`.

- [ ] **Step 1: Write the date-range helper** (ported from the React `formatRange`; parses date parts locally to avoid timezone drift)

```ts
function parseLocal(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d);
}

// Formats a start–end range: single day → one date; same month → "15–16 Nov 2023";
// otherwise → "20 Oct – 5 Nov 2023". Locale-aware (en-GB / ar).
export function formatDateRange(startIso: string, endIso: string, lang: 'en' | 'ar'): string {
  const locale = lang === 'ar' ? 'ar' : 'en-GB';
  const start = parseLocal(startIso);
  const end = parseLocal(endIso || startIso);
  const full: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short', year: 'numeric' };

  if (!endIso || startIso === endIso) return start.toLocaleDateString(locale, full);

  const sameMonth = start.getMonth() === end.getMonth() && start.getFullYear() === end.getFullYear();
  if (sameMonth) {
    return `${start.toLocaleDateString(locale, { day: 'numeric' })}–${end.toLocaleDateString(locale, full)}`;
  }
  return `${start.toLocaleDateString(locale, { day: 'numeric', month: 'short' })} – ${end.toLocaleDateString(locale, full)}`;
}
```

- [ ] **Step 2: Write the failing helper spec**

```ts
import { formatDateRange } from '@features/championships/presentation/pages/championships/format-date-range';

describe('formatDateRange (en)', () => {
  it('collapses a single day', () => {
    expect(formatDateRange('2023-11-15', '2023-11-15', 'en')).toBe('15 Nov 2023');
  });
  it('collapses a same-month range', () => {
    expect(formatDateRange('2023-11-15', '2023-11-16', 'en')).toBe('15–16 Nov 2023');
  });
  it('spans a cross-month range', () => {
    expect(formatDateRange('2023-10-20', '2023-11-05', 'en')).toBe('20 Oct – 5 Nov 2023');
  });
});
```

- [ ] **Step 3: Run it — expect PASS**

Run: `cd frontend && npx jest format-date-range`
Expected: PASS.

- [ ] **Step 4: Write the view model**

```ts
import { Injectable, computed, inject, signal } from '@angular/core';
import { LanguageStore } from '@core/i18n/language.store';
import { LoadChampionshipsUseCase } from '@features/championships/domain/usecases/load-championships.use-case';
import { Championship } from '@features/championships/domain/model/championship';
import { formatDateRange } from '@features/championships/presentation/pages/championships/format-date-range';

@Injectable()
export class ChampionshipsViewModel {
  private readonly loadChampionships = inject(LoadChampionshipsUseCase);
  private readonly language = inject(LanguageStore);

  readonly loading = signal(true);
  readonly error = signal(false);
  private readonly all = signal<Championship[]>([]);
  readonly filterFrom = signal('');
  readonly filterTo = signal('');

  // Championships whose run overlaps the chosen period. Empty dates = show all.
  readonly filtered = computed<Championship[]>(() => {
    const from = this.filterFrom();
    const to = this.filterTo();
    return this.all().filter((e) => {
      if (from && e.endDate < from) return false;
      if (to && e.startDate > to) return false;
      return true;
    });
  });
  readonly count = computed(() => this.filtered().length);
  readonly hasFilter = computed(() => !!this.filterFrom() || !!this.filterTo());

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(false);
    const res = await this.loadChampionships.run();
    if (res.ok) this.all.set(res.data);
    else { this.error.set(true); this.all.set([]); }
    this.loading.set(false);
  }

  setFrom(v: string): void { this.filterFrom.set(v); }
  setTo(v: string): void { this.filterTo.set(v); }
  clearFilter(): void { this.filterFrom.set(''); this.filterTo.set(''); }

  name(e: Championship): string {
    return this.language.lang() === 'ar' ? (e.nameAr ?? e.nameEn) : e.nameEn;
  }
  location(e: Championship): string {
    const ar = this.language.lang() === 'ar';
    return (ar ? e.locationAr ?? e.locationEn : e.locationEn) ?? '';
  }
  statusLabel(e: Championship): string {
    const ar = this.language.lang() === 'ar';
    return (ar ? e.statusNameAr ?? e.statusNameEn : e.statusNameEn) ?? '';
  }
  dateRange(e: Championship): string {
    return formatDateRange(e.startDate, e.endDate, this.language.lang() === 'ar' ? 'ar' : 'en');
  }
  statusBadgeClass(e: Championship): string {
    return e.statusCode === 'completed' ? 'bg-emerald-100 text-emerald-700' : 'bg-sky-100 text-sky-700';
  }
}
```

- [ ] **Step 5: Write the failing view-model spec**

```ts
import { TestBed } from '@angular/core/testing';
import { ok } from '@core/domain/result/result';
import { LanguageStore } from '@core/i18n/language.store';
import { ChampionshipsViewModel } from '@features/championships/presentation/pages/championships/championships.viewmodel';
import { LoadChampionshipsUseCase } from '@features/championships/domain/usecases/load-championships.use-case';
import { Championship } from '@features/championships/domain/model/championship';

const sample: Championship[] = [
  { id: '1', nameEn: 'Nov Meet', nameAr: null, startDate: '2023-11-15', endDate: '2023-11-16',
    locationEn: 'Cairo', locationAr: null, statusId: 's1', statusCode: 'upcoming', statusNameEn: 'Upcoming', statusNameAr: null },
  { id: '2', nameEn: 'Oct Meet', nameAr: null, startDate: '2023-10-20', endDate: '2023-10-20',
    locationEn: 'Alex', locationAr: null, statusId: 's2', statusCode: 'completed', statusNameEn: 'Completed', statusNameAr: null },
];

function setup(items: Championship[]) {
  const run = jest.fn().mockResolvedValue(ok(items));
  TestBed.configureTestingModule({
    providers: [
      ChampionshipsViewModel,
      { provide: LoadChampionshipsUseCase, useValue: { run } },
      { provide: LanguageStore, useValue: { lang: () => 'en' } },
    ],
  });
  return TestBed.inject(ChampionshipsViewModel);
}

describe('ChampionshipsViewModel', () => {
  it('loads all events and counts them', async () => {
    const vm = setup(sample);
    await vm.load();
    expect(vm.loading()).toBe(false);
    expect(vm.count()).toBe(2);
  });

  it('overlap-filters by the From/To period', async () => {
    const vm = setup(sample);
    await vm.load();
    vm.setFrom('2023-11-01');            // excludes the Oct meet
    expect(vm.count()).toBe(1);
    expect(vm.filtered()[0].nameEn).toBe('Nov Meet');
    vm.setFrom(''); vm.setTo('2023-10-31'); // now only the Oct meet
    expect(vm.count()).toBe(1);
    expect(vm.filtered()[0].nameEn).toBe('Oct Meet');
  });

  it('clearFilter resets the period', async () => {
    const vm = setup(sample);
    await vm.load();
    vm.setFrom('2024-01-01');
    expect(vm.count()).toBe(0);
    vm.clearFilter();
    expect(vm.hasFilter()).toBe(false);
    expect(vm.count()).toBe(2);
  });

  it('picks a badge class by status code', async () => {
    const vm = setup(sample);
    await vm.load();
    expect(vm.statusBadgeClass(sample[0])).toContain('sky');       // upcoming
    expect(vm.statusBadgeClass(sample[1])).toContain('emerald');   // completed
  });

  it('sets error on failure', async () => {
    const run = jest.fn().mockResolvedValue({ ok: false, error: new Error('x') });
    TestBed.configureTestingModule({
      providers: [
        ChampionshipsViewModel,
        { provide: LoadChampionshipsUseCase, useValue: { run } },
        { provide: LanguageStore, useValue: { lang: () => 'en' } },
      ],
    });
    const vm = TestBed.inject(ChampionshipsViewModel);
    await vm.load();
    expect(vm.error()).toBe(true);
    expect(vm.count()).toBe(0);
  });
});
```

- [ ] **Step 6: Run it — expect PASS**

Run: `cd frontend && npx jest championships.viewmodel`
Expected: PASS.

- [ ] **Step 7: Commit** (HOLD until authorized)

```bash
git add frontend/src/app/features/championships
git commit -m "feat(championships): view model + date-range helper"
```

---

## Task 10: Frontend page (rewrite placeholder) + i18n + route wiring

**Files:**
- Modify (rewrite): `frontend/src/app/features/championships/presentation/pages/championships/championships.page.ts`
- Modify (rewrite): `frontend/src/app/features/championships/presentation/pages/championships/championships.page.html`
- Modify: `frontend/src/app/features/championships/index.ts` (export the VM)
- Modify: `frontend/src/app/app.routes.ts:7,36` (import + provide the VM)
- Modify: `frontend/src/app/core/i18n/en.json`, `frontend/src/app/core/i18n/ar.json` (add `championships` block)
- Test: `frontend/src/app/features/championships/testing/presentation/pages/championships/championships.page.spec.ts`

**Interfaces:**
- Consumes: `ChampionshipsViewModel` (Task 9).

- [ ] **Step 1: Rewrite the page component**

```ts
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@core/i18n';
import { ChampionshipsViewModel } from './championships.viewmodel';

@Component({
  selector: 'app-championships-page',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './championships.page.html',
})
export class ChampionshipsPage implements OnInit {
  readonly vm = inject(ChampionshipsViewModel);
  ngOnInit(): void { void this.vm.load(); }
}
```

- [ ] **Step 2: Rewrite the page template** (card + From/To filter + count + states + table, using the app's Tailwind tokens as in `swimmers.page.html`)

```html
<div class="mx-auto max-w-5xl px-4 py-8 sm:px-6">
  <header class="mb-6">
    <h1 class="font-heading text-3xl text-ink">{{ 'championships.title' | translate }}</h1>
    <p class="mt-1 text-text-secondary">{{ 'championships.subtitle' | translate }}</p>
  </header>

  <div class="rounded-2xl border border-border bg-surface shadow-sm">
    <!-- Period filter -->
    <div class="flex flex-col gap-3 border-b border-border px-4 py-3 sm:flex-row sm:items-end">
      <div>
        <label for="champ-from" class="mb-1 block text-xs text-text-secondary">{{ 'championships.fromDate' | translate }}</label>
        <input id="champ-from" type="date" [ngModel]="vm.filterFrom()" (ngModelChange)="vm.setFrom($event)"
               class="h-9 rounded-lg border border-border bg-surface px-3 text-sm text-ink" />
      </div>
      <div>
        <label for="champ-to" class="mb-1 block text-xs text-text-secondary">{{ 'championships.toDate' | translate }}</label>
        <input id="champ-to" type="date" [ngModel]="vm.filterTo()" (ngModelChange)="vm.setTo($event)"
               class="h-9 rounded-lg border border-border bg-surface px-3 text-sm text-ink" />
      </div>
      @if (vm.hasFilter()) {
        <button type="button" (click)="vm.clearFilter()"
                class="h-9 rounded-lg border border-border bg-surface px-3 text-sm text-text-secondary hover:text-ink">
          {{ 'championships.showAll' | translate }}
        </button>
      }
      @if (!vm.loading()) {
        <span class="text-xs text-text-secondary sm:ms-auto">
          {{ vm.count() }} {{ (vm.count() === 1 ? 'championships.countOne' : 'championships.count') | translate }}
        </span>
      }
    </div>

    <!-- States -->
    @if (vm.loading()) {
      <div class="px-5 py-10 text-center text-sm text-text-secondary">{{ 'championships.loading' | translate }}</div>
    } @else if (vm.error()) {
      <div class="px-5 py-10 text-center text-sm text-danger">{{ 'championships.error' | translate }}</div>
    } @else if (vm.count() === 0) {
      <div class="px-5 py-12 text-center">
        <p class="text-sm font-medium text-ink">{{ 'championships.empty' | translate }}</p>
      </div>
    } @else {
      <div class="overflow-x-auto">
        <table class="w-full text-start">
          <thead class="border-b border-border">
            <tr>
              <th class="px-5 py-3 text-start text-xs font-medium text-text-secondary">{{ 'championships.col.championship' | translate }}</th>
              <th class="py-3 text-start text-xs font-medium text-text-secondary">{{ 'championships.col.date' | translate }}</th>
              <th class="py-3 text-start text-xs font-medium text-text-secondary">{{ 'championships.col.location' | translate }}</th>
              <th class="px-5 py-3 text-start text-xs font-medium text-text-secondary">{{ 'championships.col.status' | translate }}</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border">
            @for (e of vm.filtered(); track e.id) {
              <tr class="hover:bg-muted/40">
                <td class="px-5 py-3.5">
                  <span class="text-sm font-medium text-ink">{{ vm.name(e) }}</span>
                </td>
                <td class="py-3.5 text-sm text-text-secondary whitespace-nowrap">{{ vm.dateRange(e) }}</td>
                <td class="py-3.5 text-sm text-text-secondary">{{ vm.location(e) }}</td>
                <td class="px-5 py-3.5">
                  <span class="inline-flex rounded-full px-2 py-0.5 text-xs font-semibold" [class]="vm.statusBadgeClass(e)">
                    {{ vm.statusLabel(e) }}
                  </span>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  </div>
</div>
```

- [ ] **Step 3: Export the view model** — replace `index.ts` contents:

```ts
export { ChampionshipsPage } from './presentation/pages/championships/championships.page';
export { ChampionshipsViewModel } from './presentation/pages/championships/championships.viewmodel';
```

- [ ] **Step 4: Provide the VM on the route** in `app.routes.ts` — add the import (with the other feature VM imports, near :7):
```ts
import { ChampionshipsViewModel } from '@features/championships';
```
and replace the championships route (:36) with:
```ts
      {
        path: 'championships',
        canActivate: [firstLoginGuard],
        loadComponent: () => import('@features/championships').then((m) => m.ChampionshipsPage),
        providers: [ChampionshipsViewModel],
      },
```

- [ ] **Step 5: Add the i18n block** — add a new top-level `"championships"` key to `en.json` (place it as a sibling of the `"swimmers"` block; mind the trailing comma):

```json
  "championships": {
    "title": "Championships",
    "subtitle": "Explore races, events, and results",
    "fromDate": "From date",
    "toDate": "To date",
    "showAll": "Show all",
    "count": "championships",
    "countOne": "championship",
    "col": { "championship": "Championship", "date": "Date", "location": "Location", "status": "Status" },
    "loading": "Loading championships…",
    "error": "Couldn't load championships.",
    "empty": "No championships in this period"
  },
```

and the mirror in `ar.json`:
```json
  "championships": {
    "title": "البطولات",
    "subtitle": "استكشف السباقات والبطولات والنتائج",
    "fromDate": "من تاريخ",
    "toDate": "إلى تاريخ",
    "showAll": "إظهار الكل",
    "count": "بطولة",
    "countOne": "بطولة",
    "col": { "championship": "البطولة", "date": "التاريخ", "location": "الموقع", "status": "الحالة" },
    "loading": "جارٍ تحميل البطولات…",
    "error": "تعذّر تحميل البطولات.",
    "empty": "لا بطولات في هذه الفترة"
  },
```

- [ ] **Step 6: Write the failing page smoke spec** (mirrors `swimmers.page.spec.ts`; asserts on data, not translated chrome)

```ts
import { TestBed } from '@angular/core/testing';
import { ok } from '@core/domain/result/result';
import { LanguageStore } from '@core/i18n/language.store';
import { ChampionshipsPage } from '@features/championships/presentation/pages/championships/championships.page';
import { ChampionshipsViewModel } from '@features/championships/presentation/pages/championships/championships.viewmodel';
import { LoadChampionshipsUseCase } from '@features/championships/domain/usecases/load-championships.use-case';
import { Championship } from '@features/championships/domain/model/championship';

const sample: Championship[] = [
  { id: '1', nameEn: 'National Junior Championship', nameAr: null, startDate: '2023-11-15', endDate: '2023-11-16',
    locationEn: 'Cairo Olympic Pool', locationAr: null, statusId: 's1', statusCode: 'upcoming', statusNameEn: 'Upcoming', statusNameAr: null },
];

describe('ChampionshipsPage', () => {
  it('renders championship rows after load', async () => {
    const run = jest.fn().mockResolvedValue(ok(sample));
    TestBed.configureTestingModule({
      imports: [ChampionshipsPage],
      providers: [
        ChampionshipsViewModel,
        { provide: LoadChampionshipsUseCase, useValue: { run } },
        { provide: LanguageStore, useValue: { lang: () => 'en' } },
      ],
    });
    const fixture = TestBed.createComponent(ChampionshipsPage);
    fixture.detectChanges();          // ngOnInit -> load()
    await fixture.whenStable();
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('National Junior Championship');
    expect(text).toContain('Cairo Olympic Pool');
  });
});
```

- [ ] **Step 7: Run the frontend suite + build**

Run: `cd frontend && npx jest championships && npm run build`
Expected: all championships specs pass; production build succeeds (verifies i18n JSON is valid and imports resolve).

- [ ] **Step 8: Commit** (HOLD until authorized)

```bash
git add frontend/src/app
git commit -m "feat(championships): list page UI + i18n + route wiring"
```

---

## Task 11: Seed data + apply migrations to Aiven `Swimming_Production` (manual verification)

**Files:**
- Create: `scripts/seed-championships-aiven.sql`

> This task touches the **live Aiven DB**. The executor must **ask the user for the Aiven password** and confirmation before running the `--connection` commands (per-session secret; requires the user's approval). Substitute it for `<AIVEN_PW>` below. Stop the running API first (DLL lock).

- [ ] **Step 1: Write the seed script** (idempotent; PascalCase quoted columns, like `scripts/seed-attendance-aiven.sql`)

```sql
-- seed-championships-aiven.sql
-- Seeds reference.competition_status (2 statuses, fixed ids) and the 3 demo
-- championships.competition_event rows from the design mock. Idempotent (safe to re-run).
-- NOTE: EF column names are PascalCase -> must be double-quoted in Postgres.

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- 1) Competition statuses (fixed ids so events reference them deterministically).
INSERT INTO reference.competition_status ("Id","Code","NameEn","NameAr") VALUES
  ('22222222-2222-2222-2222-222222222201','upcoming','Upcoming','قادمة'),
  ('22222222-2222-2222-2222-222222222202','completed','Completed','مكتملة')
ON CONFLICT ("Code") DO NOTHING;

-- 2) Three demo events (fixed ids), created by a head coach.
WITH coach AS (
  SELECT "Id" AS id FROM identity.app_user
  WHERE "RoleId" = (SELECT "Id" FROM reference.role WHERE "Code" = 'head_coach')
  ORDER BY "Id" LIMIT 1
),
st AS (
  SELECT
    (SELECT "Id" FROM reference.competition_status WHERE "Code"='upcoming')  AS upcoming,
    (SELECT "Id" FROM reference.competition_status WHERE "Code"='completed') AS completed
)
INSERT INTO championships.competition_event
  ("Id","NameEn","NameAr","StartDate","EndDate","LocationEn","LocationAr","StatusId","CreatedBy")
SELECT v."Id", v."NameEn", v."NameAr", v."StartDate", v."EndDate", v."LocationEn", v."LocationAr", v."StatusId", (SELECT id FROM coach)
FROM (
  VALUES
    ('33333333-3333-3333-3333-333333333301'::uuid, 'National Junior Championship', 'بطولة الناشئين الوطنية',
     DATE '2023-11-15', DATE '2023-11-16', 'Cairo Olympic Pool', 'حمام السباحة الأولمبي بالقاهرة', (SELECT upcoming FROM st)),
    ('33333333-3333-3333-3333-333333333302'::uuid, 'Regional Sprint Meet', 'لقاء السرعة الإقليمي',
     DATE '2023-10-20', DATE '2023-10-20', 'Alexandria Sports Center', 'مركز الإسكندرية الرياضي', (SELECT completed FROM st)),
    ('33333333-3333-3333-3333-333333333303'::uuid, 'Winter Open Championship', 'بطولة الشتاء المفتوحة',
     DATE '2024-01-20', DATE '2024-01-21', 'Giza Aquatic Center', 'مركز الجيزة المائي', (SELECT upcoming FROM st))
) AS v("Id","NameEn","NameAr","StartDate","EndDate","LocationEn","LocationAr","StatusId")
WHERE (SELECT id FROM coach) IS NOT NULL
ON CONFLICT ("Id") DO NOTHING;

COMMIT;

-- Verify:
--   SELECT count(*) FROM championships.competition_event;
--   SELECT e."NameEn", s."Code" FROM championships.competition_event e
--     JOIN reference.competition_status s ON s."Id" = e."StatusId" ORDER BY e."StartDate" DESC;
```

- [ ] **Step 2: Ask the user for the Aiven password**, then apply the two migrations to `Swimming_Production` (API stopped)

Run (substitute `<AIVEN_PW>`):
```bash
dotnet ef database update \
  --project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --startup-project backend/Kheprx.BaseBackend.Api \
  --context IdentityDbContext \
  --connection "Host=kheprx-service-kheprx.b.aivencloud.com;Port=14647;Database=Swimming_Production;Username=avnadmin;Password=<AIVEN_PW>;SSL Mode=Require;Trust Server Certificate=true"

dotnet ef database update \
  --project backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure \
  --startup-project backend/Kheprx.BaseBackend.Api \
  --context ChampionshipsDbContext \
  --connection "Host=kheprx-service-kheprx.b.aivencloud.com;Port=14647;Database=Swimming_Production;Username=avnadmin;Password=<AIVEN_PW>;SSL Mode=Require;Trust Server Certificate=true"
```
Expected: both report the new migration applied. `reference.competition_status` and `championships.competition_event` now exist on `Swimming_Production`.

- [ ] **Step 3: Run the seed script against `Swimming_Production`**

Run (substitute `<AIVEN_PW>`):
```bash
PGPASSWORD='<AIVEN_PW>' psql \
  "host=kheprx-service-kheprx.b.aivencloud.com port=14647 dbname=Swimming_Production user=avnadmin sslmode=require" \
  -f scripts/seed-championships-aiven.sql
```
Expected: `BEGIN … INSERT … COMMIT`. Then run the two verify queries at the bottom of the script — expect 3 events, statuses `upcoming`/`completed` resolved.

> If `psql` is unavailable, run the SQL through any Postgres client pointed at `Swimming_Production`, or paste it into an `dotnet ef`-adjacent tool. The script is idempotent.

- [ ] **Step 4: Manual end-to-end check**

Start the API + frontend, log in, open **Championships** from the sidebar. Expect the 3 seeded events, newest first (Winter Open Jan 2024 → National Junior Nov 2023 → Regional Sprint Oct 2023), each with its date range, location, and an Upcoming/Completed badge. Set a From/To period and confirm the list + count filter correctly; "Show all" resets.

- [ ] **Step 5: Commit** (HOLD until authorized)

```bash
git add scripts/seed-championships-aiven.sql
git commit -m "chore(championships): Aiven seed script for competition statuses + demo events"
```

---

## Self-Review

**1. Spec coverage**
- `reference.competition_status` (entity, seed, endpoint) → Tasks 1, 2. ✓
- `championships.competition_event` (module, entity, config, migration, repo) → Tasks 3, 4. ✓
- Application service + DTO with status enrichment shape → Task 5; enrichment at API layer → Task 6. ✓
- `GET /api/championships` + `/api/reference/competition-statuses` → Tasks 6, 2. ✓
- `Program.cs` + `MigrationExtensions` wiring → Task 6. ✓
- Frontend clean-arch (model/dto/mapper/repo/usecase/providers/vm/page) → Tasks 7–10. ✓
- Date-overlap filter, count, `formatRange`, status badge by code, EN/AR display → Tasks 9, 10. ✓
- i18n `championships.*`, route VM provider → Task 10. ✓
- Seed + apply to Aiven `Swimming_Production` → Task 11. ✓
- Out-of-scope items (Enrolled, Create, detail/days/results, role filter) → correctly absent. ✓

**2. Placeholder scan** — no TBD/TODO. The only intentional runtime substitution is `<AIVEN_PW>` (a per-session secret the executor must obtain from the user), flagged explicitly in Task 11.

**3. Type consistency** — `CompetitionEventDto` field order `(Id, NameEn, NameAr, StartDate, EndDate, LocationEn, LocationAr, StatusId, StatusCode, StatusNameEn, StatusNameAr)` is identical in Task 5 (definition + service `ToDto`), Task 6 (controller `with` + tests). Entity ctor `(nameEn, nameAr, startDate, endDate, locationEn, locationAr, statusId, createdBy)` is identical in Tasks 3, 4, 5. `Championship` (frontend) fields match the DTO/mapper in Tasks 7, 9, 10. `ICompetitionEventRepository.ListAsync`, `IChampionshipService.ListAsync`, `getChampionships`, `LoadChampionshipsUseCase`, `CHAMPIONSHIPS_REPOSITORY` names are consistent across producer/consumer tasks.
