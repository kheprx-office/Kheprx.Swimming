# Competition Days Tab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the 2nd tab — **Competition Days** — to the championship detail page (`/championships/:id`), letting head_coach/captain build a day → race → assigned-swimmer schedule for an event and save it in one atomic replace.

**Architecture:** Extend the existing `Championships` module (Clean Architecture per-module) with a `reference.distance` lookup (Identity module) and three `championships` tables (`competition_day`, `race_session`, `race_assignment`). The whole schedule is one aggregate: `GET /schedule` returns the tree, `PUT /schedule` replaces it atomically (rows regenerated each save). The Angular tab is a dedicated `CompetitionDaysViewModel` (isolated from enrollment) that batch-loads and single-Saves, mirroring the Enrollment tab.

**Tech Stack:** .NET (EF Core, Npgsql, xUnit + Moq + EF InMemory), Angular (standalone components, signals, Jest), PostgreSQL (Aiven `Swimming_Production`).

**Spec:** `docs/superpowers/specs/2026-09-23-competition-days-design.md` — read it alongside this plan.

## Global Constraints

- **NO COMMITS UNTIL THE USER SAYS SO.** The user explicitly said "don't commit anything until I tell u." Every task ends with a **Commit** step for when authorization arrives, but **do not run any `git commit` until the user authorizes it.** When authorized, commit per task in order.
- **Branch:** `feat/championships` (already checked out).
- **Stop the running API before `dotnet ef` or `dotnet test`** — a running backend locks the build DLLs and both will fail otherwise.
- **EF naming convention (verbatim):** tables + schema are snake_case via `ToTable("name","schema")`; **columns keep PascalCase property names** (so SQL quotes them `"Id"`, `"LabelEn"`, …). Cross-module ids are **loose Guids** — no EF navigations, no DB-level FK across contexts.
- **Loose-Guid rule:** the only DB-enforced invariant added is the `race_assignment` unique index `(RaceSessionId, SwimmerId)`.
- **Migrations target Aiven `Swimming_Production`** and require `--connection "<aiven conn>"` (design-time factories hardcode local). Applying needs the DB password **at run time** — pause and ask the user.
- **Out of scope:** `race_result`, results/time entry, Finished races & Results tabs. The per-race status pill is **client-derived only**, never persisted.
- **Frontend tests use Jest** (not Karma); backend tests use **xUnit + Moq + EF InMemory**.
- **Distance seed list (verbatim codes/meters):** `50m/50, 100m/100, 200m/200, 400m/400, 800m/800, 1000m/1000, 1500m/1500, 5000m/5000, 7000m/7000, 7500m/7500, 10000m/10000`.

---

## Task overview

1. Backend: `reference.distance` lookup (entity → endpoint) — Identity module.
2. Backend: schedule tables + `CompetitionScheduleRepository` — Championships module.
3. Backend: schedule service methods + DTOs + enrolled-subset validation.
4. Backend: `GET`/`PUT /schedule` controller endpoints + messages + build.
5. Frontend: `reference.distance` — repo method + `LoadDistancesUseCase`.
6. Frontend: schedule DTOs + mapper + repo methods.
7. Frontend: `load-schedule` + `save-schedule` use-cases.
8. Frontend: `CompetitionDaysViewModel` + editor model.
9. Frontend: enable the `days` tab — page wiring + template + i18n.
10. Seed scripts (`distances`, `competition-schedule`).
11. Apply migrations + run seeds on Aiven + verify.

---

### Task 1: Backend — `reference.distance` lookup (Identity module)

Mirrors `reference.stroke` exactly (entity → config → repo → DbSet → DI → service → controller → migration).

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/Distance.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/IDistanceRepository.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/DistanceConfiguration.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/DistanceRepository.cs`
- Modify: `.../Infrastructure/Data/IdentityDbContext.cs` (add `DbSet<Distance> Distances`)
- Modify: `.../Application/Services/Interfaces/IReferenceService.cs` (add `GetDistancesAsync`)
- Modify: `.../Application/Services/ReferenceService.cs` (ctor dep + method)
- Modify: `.../Infrastructure/Extensions/IdentityModuleExtensions.cs` (register `IDistanceRepository`)
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/ReferenceController.cs` (add `distances` action)
- Modify: `.../Application/Resources/ReferenceMessages.cs` (add `DistancesListed`)
- Create migration: `.../Infrastructure/Migrations/<timestamp>_AddDistanceReference.cs` (scaffolded)
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/DistanceRepositoryTests.cs`
- Test (modify): `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/ReferenceServiceTests.cs`

**Interfaces:**
- Produces: `IDistanceRepository { Task<IReadOnlyList<Distance>> GetAllAsync(ct); Task<bool> ExistsAsync(Guid id, ct); }`; `IReferenceService.GetDistancesAsync(ct) → IReadOnlyList<CodedLookupDto>`; `GET /api/reference/distances → ApiResponse<IReadOnlyList<CodedLookupDto>>`.
- Consumes: `CodedLookupDto(Guid Id, string Code, string NameEn, string? NameAr)` (existing).

- [ ] **Step 1: Write the failing repository test**

Create `DistanceRepositoryTests.cs`:
```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Kheprx.BaseBackend.Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Repositories;

public class DistanceRepositoryTests
{
    private static IdentityDbContext NewDb()
        => new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"dist-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task GetAllAsync_returns_all_distances_ordered_by_meters()
    {
        await using var db = NewDb();
        db.Distances.Add(new Distance("100m", "100m", "100 متر", 100));
        db.Distances.Add(new Distance("50m", "50m", "50 متر", 50));
        await db.SaveChangesAsync();

        var all = await new DistanceRepository(db).GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Equal(50, all[0].Meters);   // ordered by meters
        Assert.Equal(100, all[1].Meters);
    }

    [Fact]
    public async Task ExistsAsync_is_true_only_for_a_known_id()
    {
        await using var db = NewDb();
        var d = new Distance("50m", "50m", null, 50);
        db.Distances.Add(d);
        await db.SaveChangesAsync();

        var repo = new DistanceRepository(db);
        Assert.True(await repo.ExistsAsync(d.Id));
        Assert.False(await repo.ExistsAsync(Guid.NewGuid()));
    }
}
```

- [ ] **Step 2: Run the test — verify it fails to compile**

Run (API stopped): `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter DistanceRepositoryTests`
Expected: FAIL — `Distance` / `DistanceRepository` / `db.Distances` do not exist.

- [ ] **Step 3: Create the `Distance` entity**

`Distance.cs`:
```csharp
namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class Distance
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public int Meters { get; private set; }

    private Distance() { } // EF Core

    public Distance(string code, string nameEn, string? nameAr, int meters)
    {
        Id = Guid.NewGuid();
        Code = code.Trim();
        NameEn = nameEn.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
        Meters = meters;
    }
}
```

- [ ] **Step 4: Create the repository interface**

`IDistanceRepository.cs`:
```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
namespace Kheprx.BaseBackend.Identity.Domain.Repositories;
public interface IDistanceRepository
{
    Task<IReadOnlyList<Distance>> GetAllAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
}
```

- [ ] **Step 5: Create the EF configuration**

`DistanceConfiguration.cs`:
```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class DistanceConfiguration : IEntityTypeConfiguration<Distance>
{
    public void Configure(EntityTypeBuilder<Distance> builder)
    {
        builder.ToTable("distance", "reference");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(d => d.Code).IsUnique();
        builder.Property(d => d.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(d => d.NameAr).HasMaxLength(100);
        builder.Property(d => d.Meters).IsRequired();
    }
}
```

- [ ] **Step 6: Create the repository**

`DistanceRepository.cs`:
```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class DistanceRepository : IDistanceRepository
{
    private readonly IdentityDbContext _db;
    public DistanceRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<Distance>> GetAllAsync(CancellationToken ct = default)
        => await _db.Distances.AsNoTracking().OrderBy(d => d.Meters).ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _db.Distances.AsNoTracking().AnyAsync(d => d.Id == id, ct);
}
```

- [ ] **Step 7: Register the `DbSet`**

In `IdentityDbContext.cs`, add next to `public DbSet<Stroke> Strokes => Set<Stroke>();`:
```csharp
    public DbSet<Distance> Distances => Set<Distance>();
```

- [ ] **Step 8: Run the repository test — verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter DistanceRepositoryTests`
Expected: PASS.

- [ ] **Step 9: Add the service test (failing)**

In `ReferenceServiceTests.cs`, update the `NewService` helper to accept distances and add a test. Change the helper signature to add `IDistanceRepository? distances = null` and pass `distances ?? Mock.Of<IDistanceRepository>()` in the correct constructor position (see Step 11 for the ctor order — distances goes **last**). Then add:
```csharp
    [Fact]
    public async Task GetDistances_maps_entities_to_coded_dtos()
    {
        var distances = new Mock<IDistanceRepository>();
        distances.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new[] { new Distance("50m", "50m", "٥٠ متر", 50) });

        var result = await NewService(distances: distances.Object).GetDistancesAsync();

        Assert.Single(result);
        Assert.Equal("50m", result[0].Code);
        Assert.Equal("50m", result[0].NameEn);
    }
```
Add `using Kheprx.BaseBackend.Identity.Domain.Repositories;` if not present.

- [ ] **Step 10: Extend `IReferenceService`**

In `IReferenceService.cs`, add after `GetStrokesAsync`:
```csharp
    Task<IReadOnlyList<CodedLookupDto>> GetDistancesAsync(CancellationToken ct = default);
```

- [ ] **Step 11: Implement in `ReferenceService`**

In `ReferenceService.cs`: add a field `private readonly IDistanceRepository _distances;`, add `IDistanceRepository distances` as the **last** constructor parameter, assign `_distances = distances;`, and add the method after `GetStrokesAsync`:
```csharp
    public async Task<IReadOnlyList<CodedLookupDto>> GetDistancesAsync(CancellationToken ct = default)
        => (await _distances.GetAllAsync(ct)).Select(d => new CodedLookupDto(d.Id, d.Code, d.NameEn, d.NameAr)).ToList();
```

- [ ] **Step 12: Register the repository in DI**

In `IdentityModuleExtensions.cs`, add next to `services.AddScoped<IStrokeRepository, StrokeRepository>();`:
```csharp
        services.AddScoped<IDistanceRepository, DistanceRepository>();
```

- [ ] **Step 13: Add the localized message**

In `ReferenceMessages.cs` `Success` class, add:
```csharp
        public static string DistancesListed(string lang) => lang switch { "ar" => "المسافات", _ => "Distances" };
```

- [ ] **Step 14: Add the controller action**

In `ReferenceController.cs`, add after the `Strokes` action:
```csharp
    /// <summary>Lists all race distances (Competition Days race builder).</summary>
    [HttpGet("distances")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CodedLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CodedLookupDto>>>> Distances(CancellationToken ct)
    {
        var data = await _service.GetDistancesAsync(ct);
        var body = ApiResponse<IReadOnlyList<CodedLookupDto>>.Success(
            ReferenceMessages.Success.DistancesListed(AppLanguage.Current), data);
        return Ok(body);
    }
```

- [ ] **Step 15: Run the service test — verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter ReferenceServiceTests`
Expected: PASS (all reference tests, including the new distances test).

- [ ] **Step 16: Scaffold the migration**

Ensure the API is stopped. Run (adjust project paths to match the repo layout):
```bash
dotnet ef migrations add AddDistanceReference \
  --context IdentityDbContext \
  --project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --startup-project backend/Kheprx.BaseBackend.Api
```
Expected: a new `Migrations/<timestamp>_AddDistanceReference.cs` whose `Up` creates `reference.distance` with columns `Id/Code/NameEn/NameAr/Meters` and a unique index on `Code`. Verify it matches this shape (it should — the config drives it):
```csharp
migrationBuilder.CreateTable(
    name: "distance", schema: "reference",
    columns: table => new
    {
        Id = table.Column<Guid>(type: "uuid", nullable: false),
        Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
        NameEn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
        NameAr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
        Meters = table.Column<int>(type: "integer", nullable: false)
    },
    constraints: table => table.PrimaryKey("PK_distance", x => x.Id));
migrationBuilder.CreateIndex(
    name: "IX_distance_Code", schema: "reference", table: "distance", column: "Code", unique: true);
```
(Do **not** apply it to any DB here — Aiven apply happens in Task 11.)

- [ ] **Step 17: Build the API to confirm wiring**

Run: `dotnet build backend/Kheprx.BaseBackend.Api`
Expected: build succeeds.

- [ ] **Step 18: Commit** (only once the user has authorized commits)

```bash
git add backend/src/Modules/Identity backend/tests/Kheprx.BaseBackend.Identity.UnitTests
git commit -m "feat(reference): add reference.distance lookup + /api/reference/distances"
```

---

### Task 2: Backend — schedule tables + `CompetitionScheduleRepository` (Championships module)

Three entities + configs + migration + one aggregate repository (atomic delete-and-reinsert), tested via EF InMemory like `ChampionshipEnrollmentRepositoryTests`.

**Files:**
- Create: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Domain/Entities/CompetitionDay.cs`
- Create: `.../Domain/Entities/RaceSession.cs`
- Create: `.../Domain/Entities/RaceAssignment.cs`
- Create: `.../Domain/Repositories/CompetitionScheduleData.cs` (transfer records)
- Create: `.../Domain/Repositories/ICompetitionScheduleRepository.cs`
- Create: `.../Infrastructure/Configurations/CompetitionDayConfiguration.cs`
- Create: `.../Infrastructure/Configurations/RaceSessionConfiguration.cs`
- Create: `.../Infrastructure/Configurations/RaceAssignmentConfiguration.cs`
- Create: `.../Infrastructure/Repositories/CompetitionScheduleRepository.cs`
- Modify: `.../Infrastructure/Data/ChampionshipsDbContext.cs` (3 `DbSet`s)
- Modify: `.../Infrastructure/Extensions/ChampionshipsModuleExtensions.cs` (register repo)
- Create migration: `.../Infrastructure/Migrations/<timestamp>_CreateCompetitionScheduleTables.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Repositories/CompetitionScheduleRepositoryTests.cs`

**Interfaces:**
- Produces (Domain transfer records, in `CompetitionScheduleData.cs`):
  - `ScheduleRaceRow(Guid Id, Guid StrokeId, Guid DistanceId, TimeOnly? ScheduledTime, IReadOnlyList<Guid> SwimmerIds)`
  - `ScheduleDayRow(Guid Id, string LabelEn, string? LabelAr, DateOnly DayDate, IReadOnlyList<ScheduleRaceRow> Races)`
  - `ScheduleRaceInput(Guid StrokeId, Guid DistanceId, TimeOnly? ScheduledTime, IReadOnlyList<Guid> SwimmerIds)`
  - `ScheduleDayInput(string LabelEn, string? LabelAr, DateOnly DayDate, IReadOnlyList<ScheduleRaceInput> Races)`
- Produces (repo): `ICompetitionScheduleRepository { Task<IReadOnlyList<ScheduleDayRow>> GetAsync(Guid eventId, ct); Task ReplaceAsync(Guid eventId, IReadOnlyList<ScheduleDayInput> days, ct); }`
- Consumes: entity ctors `new CompetitionDay(eventId, labelEn, labelAr, dayDate)`, `new RaceSession(dayId, strokeId, distanceId, scheduledTime)`, `new RaceAssignment(raceSessionId, swimmerId)`.

- [ ] **Step 1: Write the failing repository test**

Create `CompetitionScheduleRepositoryTests.cs`:
```csharp
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Kheprx.BaseBackend.Championships.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Repositories;

public class CompetitionScheduleRepositoryTests
{
    private static ChampionshipsDbContext NewDb()
        => new(new DbContextOptionsBuilder<ChampionshipsDbContext>()
            .UseInMemoryDatabase($"sched-{Guid.NewGuid()}").Options);

    private static ScheduleDayInput Day(string label, DateOnly date, params ScheduleRaceInput[] races)
        => new(label, null, date, races);

    private static ScheduleRaceInput Race(Guid stroke, Guid distance, TimeOnly? time, params Guid[] swimmers)
        => new(stroke, distance, time, swimmers);

    [Fact]
    public async Task ReplaceAsync_then_GetAsync_round_trips_the_tree()
    {
        await using var db = NewDb();
        var eventId = Guid.NewGuid();
        var stroke = Guid.NewGuid();
        var distance = Guid.NewGuid();
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();

        await new CompetitionScheduleRepository(db).ReplaceAsync(eventId, new[]
        {
            Day("Day 1", new DateOnly(2023, 11, 15),
                Race(stroke, distance, new TimeOnly(9, 0), s1, s2)),
        });

        var rows = await new CompetitionScheduleRepository(db).GetAsync(eventId);

        Assert.Single(rows);
        Assert.Equal("Day 1", rows[0].LabelEn);
        Assert.Single(rows[0].Races);
        Assert.Equal(new TimeOnly(9, 0), rows[0].Races[0].ScheduledTime);
        Assert.Equal(2, rows[0].Races[0].SwimmerIds.Count);
        Assert.Contains(s1, rows[0].Races[0].SwimmerIds);
    }

    [Fact]
    public async Task ReplaceAsync_deletes_the_prior_tree_for_the_event()
    {
        await using var db = NewDb();
        var eventId = Guid.NewGuid();
        var repo = new CompetitionScheduleRepository(db);
        await repo.ReplaceAsync(eventId, new[]
        {
            Day("Old", new DateOnly(2023, 1, 1), Race(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid())),
        });

        await repo.ReplaceAsync(eventId, new[] { Day("New", new DateOnly(2023, 2, 2)) });

        var rows = await repo.GetAsync(eventId);
        Assert.Single(rows);
        Assert.Equal("New", rows[0].LabelEn);
        Assert.Empty(rows[0].Races);
        Assert.Empty(await db.RaceSessions.ToListAsync());     // old sessions gone
        Assert.Empty(await db.RaceAssignments.ToListAsync());  // old assignments gone
    }

    [Fact]
    public async Task GetAsync_returns_only_the_target_event()
    {
        await using var db = NewDb();
        var repo = new CompetitionScheduleRepository(db);
        var eventA = Guid.NewGuid();
        var eventB = Guid.NewGuid();
        await repo.ReplaceAsync(eventA, new[] { Day("A", new DateOnly(2023, 1, 1)) });
        await repo.ReplaceAsync(eventB, new[] { Day("B", new DateOnly(2023, 1, 1)) });

        var rows = await repo.GetAsync(eventA);
        Assert.Single(rows);
        Assert.Equal("A", rows[0].LabelEn);
    }

    [Fact]
    public async Task ReplaceAsync_with_empty_list_clears_the_event()
    {
        await using var db = NewDb();
        var eventId = Guid.NewGuid();
        var repo = new CompetitionScheduleRepository(db);
        await repo.ReplaceAsync(eventId, new[] { Day("D", new DateOnly(2023, 1, 1), Race(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid())) });

        await repo.ReplaceAsync(eventId, Array.Empty<ScheduleDayInput>());

        Assert.Empty(await repo.GetAsync(eventId));
    }
}
```

- [ ] **Step 2: Run the test — verify it fails to compile**

Run (API stopped): `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests --filter CompetitionScheduleRepositoryTests`
Expected: FAIL — entities, records, `DbSet`s, and repo do not exist.

- [ ] **Step 3: Create the three entities**

`CompetitionDay.cs`:
```csharp
namespace Kheprx.BaseBackend.Championships.Domain.Entities;

public sealed class CompetitionDay
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string LabelEn { get; private set; } = string.Empty;
    public string? LabelAr { get; private set; }
    public DateOnly DayDate { get; private set; }

    private CompetitionDay() { } // EF Core

    public CompetitionDay(Guid eventId, string labelEn, string? labelAr, DateOnly dayDate)
    {
        Id = Guid.NewGuid();
        EventId = eventId;
        LabelEn = labelEn.Trim();
        LabelAr = string.IsNullOrWhiteSpace(labelAr) ? null : labelAr.Trim();
        DayDate = dayDate;
    }
}
```
`RaceSession.cs`:
```csharp
namespace Kheprx.BaseBackend.Championships.Domain.Entities;

public sealed class RaceSession
{
    public Guid Id { get; private set; }
    public Guid DayId { get; private set; }
    public Guid StrokeId { get; private set; }
    public Guid DistanceId { get; private set; }
    public TimeOnly? ScheduledTime { get; private set; }

    private RaceSession() { } // EF Core

    public RaceSession(Guid dayId, Guid strokeId, Guid distanceId, TimeOnly? scheduledTime)
    {
        Id = Guid.NewGuid();
        DayId = dayId;
        StrokeId = strokeId;
        DistanceId = distanceId;
        ScheduledTime = scheduledTime;
    }
}
```
`RaceAssignment.cs`:
```csharp
namespace Kheprx.BaseBackend.Championships.Domain.Entities;

public sealed class RaceAssignment
{
    public Guid Id { get; private set; }
    public Guid RaceSessionId { get; private set; }
    public Guid SwimmerId { get; private set; }

    private RaceAssignment() { } // EF Core

    public RaceAssignment(Guid raceSessionId, Guid swimmerId)
    {
        Id = Guid.NewGuid();
        RaceSessionId = raceSessionId;
        SwimmerId = swimmerId;
    }
}
```

- [ ] **Step 4: Create the transfer records + repo interface**

`CompetitionScheduleData.cs`:
```csharp
namespace Kheprx.BaseBackend.Championships.Domain.Repositories;

// Read shape returned by GetAsync (carries persisted ids).
public sealed record ScheduleRaceRow(Guid Id, Guid StrokeId, Guid DistanceId, TimeOnly? ScheduledTime, IReadOnlyList<Guid> SwimmerIds);
public sealed record ScheduleDayRow(Guid Id, string LabelEn, string? LabelAr, DateOnly DayDate, IReadOnlyList<ScheduleRaceRow> Races);

// Write shape accepted by ReplaceAsync (ids are generated on insert).
public sealed record ScheduleRaceInput(Guid StrokeId, Guid DistanceId, TimeOnly? ScheduledTime, IReadOnlyList<Guid> SwimmerIds);
public sealed record ScheduleDayInput(string LabelEn, string? LabelAr, DateOnly DayDate, IReadOnlyList<ScheduleRaceInput> Races);
```
`ICompetitionScheduleRepository.cs`:
```csharp
namespace Kheprx.BaseBackend.Championships.Domain.Repositories;

public interface ICompetitionScheduleRepository
{
    Task<IReadOnlyList<ScheduleDayRow>> GetAsync(Guid eventId, CancellationToken ct = default);
    Task ReplaceAsync(Guid eventId, IReadOnlyList<ScheduleDayInput> days, CancellationToken ct = default);
}
```

- [ ] **Step 5: Create the three EF configurations**

`CompetitionDayConfiguration.cs`:
```csharp
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Configurations;

internal sealed class CompetitionDayConfiguration : IEntityTypeConfiguration<CompetitionDay>
{
    public void Configure(EntityTypeBuilder<CompetitionDay> builder)
    {
        builder.ToTable("competition_day", "championships");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.EventId).IsRequired();       // loose Guid → competition_event
        builder.Property(d => d.LabelEn).IsRequired();
        builder.Property(d => d.LabelAr);
        builder.Property(d => d.DayDate).IsRequired();       // DateOnly → date
        builder.HasIndex(d => d.EventId);
    }
}
```
`RaceSessionConfiguration.cs`:
```csharp
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Configurations;

internal sealed class RaceSessionConfiguration : IEntityTypeConfiguration<RaceSession>
{
    public void Configure(EntityTypeBuilder<RaceSession> builder)
    {
        builder.ToTable("race_session", "championships");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.DayId).IsRequired();         // loose Guid → competition_day
        builder.Property(s => s.StrokeId).IsRequired();      // loose Guid → reference.stroke
        builder.Property(s => s.DistanceId).IsRequired();    // loose Guid → reference.distance
        builder.Property(s => s.ScheduledTime);              // TimeOnly? → time, nullable
        builder.HasIndex(s => s.DayId);
    }
}
```
`RaceAssignmentConfiguration.cs`:
```csharp
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Configurations;

internal sealed class RaceAssignmentConfiguration : IEntityTypeConfiguration<RaceAssignment>
{
    public void Configure(EntityTypeBuilder<RaceAssignment> builder)
    {
        builder.ToTable("race_assignment", "championships");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.RaceSessionId).IsRequired(); // loose Guid → race_session
        builder.Property(a => a.SwimmerId).IsRequired();     // loose Guid → identity.swimmer_profile
        builder.HasIndex(a => new { a.RaceSessionId, a.SwimmerId }).IsUnique();
    }
}
```

- [ ] **Step 6: Register the `DbSet`s**

In `ChampionshipsDbContext.cs`, add after `public DbSet<ChampionshipEnrollment> Enrollments => Set<ChampionshipEnrollment>();`:
```csharp
    public DbSet<CompetitionDay> CompetitionDays => Set<CompetitionDay>();
    public DbSet<RaceSession> RaceSessions => Set<RaceSession>();
    public DbSet<RaceAssignment> RaceAssignments => Set<RaceAssignment>();
```

- [ ] **Step 7: Create the repository**

`CompetitionScheduleRepository.cs`:
```csharp
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Repositories;

internal sealed class CompetitionScheduleRepository : ICompetitionScheduleRepository
{
    private readonly ChampionshipsDbContext _db;
    public CompetitionScheduleRepository(ChampionshipsDbContext db) => _db = db;

    public async Task<IReadOnlyList<ScheduleDayRow>> GetAsync(Guid eventId, CancellationToken ct = default)
    {
        var days = await _db.CompetitionDays.AsNoTracking()
            .Where(d => d.EventId == eventId)
            .OrderBy(d => d.DayDate).ThenBy(d => d.Id).ToListAsync(ct);
        var dayIds = days.Select(d => d.Id).ToList();

        var sessions = await _db.RaceSessions.AsNoTracking()
            .Where(s => dayIds.Contains(s.DayId)).ToListAsync(ct);
        var sessionIds = sessions.Select(s => s.Id).ToList();

        var assignments = await _db.RaceAssignments.AsNoTracking()
            .Where(a => sessionIds.Contains(a.RaceSessionId)).ToListAsync(ct);

        var swimmersBySession = assignments
            .GroupBy(a => a.RaceSessionId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(a => a.SwimmerId).ToList());
        var sessionsByDay = sessions
            .GroupBy(s => s.DayId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return days.Select(d => new ScheduleDayRow(
            d.Id, d.LabelEn, d.LabelAr, d.DayDate,
            (sessionsByDay.TryGetValue(d.Id, out var ss) ? ss : new List<RaceSession>())
                .OrderBy(s => s.ScheduledTime ?? TimeOnly.MinValue).ThenBy(s => s.Id)
                .Select(s => new ScheduleRaceRow(
                    s.Id, s.StrokeId, s.DistanceId, s.ScheduledTime,
                    swimmersBySession.TryGetValue(s.Id, out var sw) ? sw : new List<Guid>()))
                .ToList()))
            .ToList();
    }

    public async Task ReplaceAsync(Guid eventId, IReadOnlyList<ScheduleDayInput> days, CancellationToken ct = default)
    {
        var oldDays = await _db.CompetitionDays.Where(d => d.EventId == eventId).ToListAsync(ct);
        var oldDayIds = oldDays.Select(d => d.Id).ToList();
        var oldSessions = await _db.RaceSessions.Where(s => oldDayIds.Contains(s.DayId)).ToListAsync(ct);
        var oldSessionIds = oldSessions.Select(s => s.Id).ToList();
        var oldAssignments = await _db.RaceAssignments.Where(a => oldSessionIds.Contains(a.RaceSessionId)).ToListAsync(ct);

        _db.RaceAssignments.RemoveRange(oldAssignments);
        _db.RaceSessions.RemoveRange(oldSessions);
        _db.CompetitionDays.RemoveRange(oldDays);

        foreach (var d in days)
        {
            var day = new CompetitionDay(eventId, d.LabelEn, d.LabelAr, d.DayDate);
            _db.CompetitionDays.Add(day);
            foreach (var r in d.Races)
            {
                var session = new RaceSession(day.Id, r.StrokeId, r.DistanceId, r.ScheduledTime);
                _db.RaceSessions.Add(session);
                foreach (var swimmerId in r.SwimmerIds.Distinct())
                    _db.RaceAssignments.Add(new RaceAssignment(session.Id, swimmerId));
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 8: Register the repository in DI**

In `ChampionshipsModuleExtensions.cs`, add after the enrollment registration:
```csharp
        services.AddScoped<ICompetitionScheduleRepository, CompetitionScheduleRepository>();
```

- [ ] **Step 9: Run the repository test — verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests --filter CompetitionScheduleRepositoryTests`
Expected: PASS (all four cases).

- [ ] **Step 10: Scaffold the migration**

API stopped. Run:
```bash
dotnet ef migrations add CreateCompetitionScheduleTables \
  --context ChampionshipsDbContext \
  --project backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure \
  --startup-project backend/Kheprx.BaseBackend.Api
```
Expected: a migration creating `championships.competition_day`, `championships.race_session`, `championships.race_assignment`, with the unique index `IX_race_assignment_RaceSessionId_SwimmerId` and the non-unique indexes on `EventId` / `DayId`. Do not apply it here.

- [ ] **Step 11: Build to confirm**

Run: `dotnet build backend/Kheprx.BaseBackend.Api`
Expected: succeeds.

- [ ] **Step 12: Commit** (only once authorized)

```bash
git add backend/src/Modules/Championships backend/tests/Kheprx.BaseBackend.Championships.UnitTests
git commit -m "feat(championships): competition schedule tables + repository"
```

---

### Task 3: Backend — schedule service methods + DTOs + validation

Adds `GetScheduleAsync` / `SetScheduleAsync` to `IChampionshipService`. `SetScheduleAsync` enforces the **enrolled-subset** rule (every assigned swimmer must be enrolled) and returns a small result type. Tested with Moq like `ReferenceServiceTests`.

**Files:**
- Create: `.../Application/DTOs/CompetitionScheduleDtos.cs`
- Modify: `.../Application/Services/Interfaces/IChampionshipService.cs`
- Modify: `.../Application/Services/ChampionshipService.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Services/ChampionshipScheduleServiceTests.cs`

**Interfaces:**
- Produces (Application DTOs):
  - `ScheduleRaceDto(Guid Id, Guid StrokeId, Guid DistanceId, TimeOnly? ScheduledTime, IReadOnlyList<Guid> SwimmerIds)`
  - `ScheduleDayDto(Guid Id, string LabelEn, string? LabelAr, DateOnly DayDate, IReadOnlyList<ScheduleRaceDto> Races)`
  - `ScheduleDto(IReadOnlyList<ScheduleDayDto> Days)`
  - `SetScheduleRace(Guid StrokeId, Guid DistanceId, TimeOnly? ScheduledTime, IReadOnlyList<Guid> SwimmerIds)`
  - `SetScheduleDay(string LabelEn, string? LabelAr, DateOnly DayDate, IReadOnlyList<SetScheduleRace> Races)`
  - `SetScheduleRequest(IReadOnlyList<SetScheduleDay> Days)`
  - `enum SetScheduleOutcome { Ok, NotFound, Invalid }`
  - `SetScheduleResult(SetScheduleOutcome Outcome, ScheduleDto? Saved, string? Error)`
- Produces (service): `Task<ScheduleDto?> GetScheduleAsync(Guid eventId, ct)` (null ⇒ event not found); `Task<SetScheduleResult> SetScheduleAsync(Guid eventId, IReadOnlyList<SetScheduleDay> days, ct)`.
- Consumes: `ICompetitionEventRepository.GetByIdAsync`, `IChampionshipEnrollmentRepository.ListSwimmerIdsAsync`, `ICompetitionScheduleRepository` (Task 2).

- [ ] **Step 1: Write the failing service tests**

Create `ChampionshipScheduleServiceTests.cs`:
```csharp
using Kheprx.BaseBackend.Championships.Application.DTOs;
using Kheprx.BaseBackend.Championships.Application.Services;
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Services;

public class ChampionshipScheduleServiceTests
{
    private static CompetitionEvent NewEvent()
        => new("National Junior", null, new DateOnly(2023, 11, 15), new DateOnly(2023, 11, 16),
               "Cairo", null, Guid.NewGuid(), Guid.NewGuid());

    private static (ChampionshipService svc, Mock<ICompetitionEventRepository> events,
                    Mock<IChampionshipEnrollmentRepository> enr, Mock<ICompetitionScheduleRepository> sched) Build()
    {
        var events = new Mock<ICompetitionEventRepository>();
        var enr = new Mock<IChampionshipEnrollmentRepository>();
        var sched = new Mock<ICompetitionScheduleRepository>();
        return (new ChampionshipService(events.Object, enr.Object, sched.Object), events, enr, sched);
    }

    [Fact]
    public async Task GetScheduleAsync_returns_null_when_event_missing()
    {
        var (svc, events, _, _) = Build();
        events.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((CompetitionEvent?)null);

        Assert.Null(await svc.GetScheduleAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetScheduleAsync_maps_rows_to_dto()
    {
        var (svc, events, _, sched) = Build();
        var eventId = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        var dayId = Guid.NewGuid();
        var raceId = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        sched.Setup(r => r.GetAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new ScheduleDayRow(dayId, "Day 1", null, new DateOnly(2023, 11, 15), new[]
            {
                new ScheduleRaceRow(raceId, Guid.NewGuid(), Guid.NewGuid(), new TimeOnly(9, 0), new[] { swimmer }),
            }),
        });

        var dto = await svc.GetScheduleAsync(eventId);

        Assert.NotNull(dto);
        Assert.Single(dto!.Days);
        Assert.Equal("Day 1", dto.Days[0].LabelEn);
        Assert.Equal(swimmer, dto.Days[0].Races[0].SwimmerIds[0]);
    }

    [Fact]
    public async Task SetScheduleAsync_returns_NotFound_when_event_missing()
    {
        var (svc, events, _, _) = Build();
        events.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((CompetitionEvent?)null);

        var result = await svc.SetScheduleAsync(Guid.NewGuid(), Array.Empty<SetScheduleDay>());

        Assert.Equal(SetScheduleOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task SetScheduleAsync_rejects_a_non_enrolled_swimmer()
    {
        var (svc, events, enr, sched) = Build();
        var eventId = Guid.NewGuid();
        var enrolled = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        enr.Setup(r => r.ListSwimmerIdsAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { enrolled });

        var days = new[]
        {
            new SetScheduleDay("Day 1", null, new DateOnly(2023, 11, 15), new[]
            {
                new SetScheduleRace(Guid.NewGuid(), Guid.NewGuid(), null, new[] { stranger }),
            }),
        };

        var result = await svc.SetScheduleAsync(eventId, days);

        Assert.Equal(SetScheduleOutcome.Invalid, result.Outcome);
        sched.Verify(r => r.ReplaceAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ScheduleDayInput>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetScheduleAsync_rejects_a_blank_day_label()
    {
        var (svc, events, enr, sched) = Build();
        var eventId = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        enr.Setup(r => r.ListSwimmerIdsAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Guid>());

        var days = new[] { new SetScheduleDay("  ", null, new DateOnly(2023, 11, 15), Array.Empty<SetScheduleRace>()) };

        var result = await svc.SetScheduleAsync(eventId, days);

        Assert.Equal(SetScheduleOutcome.Invalid, result.Outcome);
        sched.Verify(r => r.ReplaceAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ScheduleDayInput>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetScheduleAsync_replaces_and_returns_the_saved_tree()
    {
        var (svc, events, enr, sched) = Build();
        var eventId = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        enr.Setup(r => r.ListSwimmerIdsAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { swimmer });
        var savedDayId = Guid.NewGuid();
        sched.Setup(r => r.GetAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new ScheduleDayRow(savedDayId, "Day 1", null, new DateOnly(2023, 11, 15), Array.Empty<ScheduleRaceRow>()),
        });

        var days = new[]
        {
            new SetScheduleDay("Day 1", null, new DateOnly(2023, 11, 15), new[]
            {
                new SetScheduleRace(Guid.NewGuid(), Guid.NewGuid(), new TimeOnly(9, 0), new[] { swimmer }),
            }),
        };

        var result = await svc.SetScheduleAsync(eventId, days);

        Assert.Equal(SetScheduleOutcome.Ok, result.Outcome);
        Assert.Equal(savedDayId, result.Saved!.Days[0].Id);
        sched.Verify(r => r.ReplaceAsync(eventId, It.IsAny<IReadOnlyList<ScheduleDayInput>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Run — verify it fails to compile**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests --filter ChampionshipScheduleServiceTests`
Expected: FAIL — the DTOs, the service overload, and the 3-arg `ChampionshipService` ctor don't exist.

- [ ] **Step 3: Create the DTOs**

`CompetitionScheduleDtos.cs`:
```csharp
namespace Kheprx.BaseBackend.Championships.Application.DTOs;

// ---- Read (GET /schedule) ----
public sealed record ScheduleRaceDto(Guid Id, Guid StrokeId, Guid DistanceId, TimeOnly? ScheduledTime, IReadOnlyList<Guid> SwimmerIds);
public sealed record ScheduleDayDto(Guid Id, string LabelEn, string? LabelAr, DateOnly DayDate, IReadOnlyList<ScheduleRaceDto> Races);
public sealed record ScheduleDto(IReadOnlyList<ScheduleDayDto> Days);

// ---- Write (PUT /schedule) ----
public sealed record SetScheduleRace(Guid StrokeId, Guid DistanceId, TimeOnly? ScheduledTime, IReadOnlyList<Guid> SwimmerIds);
public sealed record SetScheduleDay(string LabelEn, string? LabelAr, DateOnly DayDate, IReadOnlyList<SetScheduleRace> Races);
public sealed record SetScheduleRequest(IReadOnlyList<SetScheduleDay> Days);

public enum SetScheduleOutcome { Ok, NotFound, Invalid }

/// <summary>Result of a schedule replace: Ok carries the re-read tree; Invalid carries a short reason code.</summary>
public sealed record SetScheduleResult(SetScheduleOutcome Outcome, ScheduleDto? Saved, string? Error);
```

- [ ] **Step 4: Extend `IChampionshipService`**

In `IChampionshipService.cs`, add after `SetEnrollmentsAsync`:
```csharp
    Task<ScheduleDto?> GetScheduleAsync(Guid eventId, CancellationToken ct = default);
    Task<SetScheduleResult> SetScheduleAsync(Guid eventId, IReadOnlyList<SetScheduleDay> days, CancellationToken ct = default);
```

- [ ] **Step 5: Implement in `ChampionshipService`**

In `ChampionshipService.cs`: add `using Kheprx.BaseBackend.Championships.Domain.Repositories;` (already present via repos). Add a field and extend the constructor to a **3rd** parameter:
```csharp
    private readonly ICompetitionScheduleRepository _schedule;

    public ChampionshipService(ICompetitionEventRepository events, IChampionshipEnrollmentRepository enrollments, ICompetitionScheduleRepository schedule)
    {
        _events = events;
        _enrollments = enrollments;
        _schedule = schedule;
    }
```
Add the two methods and a private mapper:
```csharp
    public async Task<ScheduleDto?> GetScheduleAsync(Guid eventId, CancellationToken ct = default)
    {
        var e = await _events.GetByIdAsync(eventId, ct);
        if (e is null) return null;
        var rows = await _schedule.GetAsync(eventId, ct);
        return ToDto(rows);
    }

    public async Task<SetScheduleResult> SetScheduleAsync(Guid eventId, IReadOnlyList<SetScheduleDay> days, CancellationToken ct = default)
    {
        var e = await _events.GetByIdAsync(eventId, ct);
        if (e is null) return new SetScheduleResult(SetScheduleOutcome.NotFound, null, null);

        var enrolled = (await _enrollments.ListSwimmerIdsAsync(eventId, ct)).ToHashSet();

        foreach (var d in days)
        {
            if (string.IsNullOrWhiteSpace(d.LabelEn))
                return new SetScheduleResult(SetScheduleOutcome.Invalid, null, "label_required");
            foreach (var r in d.Races)
            {
                if (r.StrokeId == Guid.Empty || r.DistanceId == Guid.Empty)
                    return new SetScheduleResult(SetScheduleOutcome.Invalid, null, "race_incomplete");
                foreach (var swimmerId in r.SwimmerIds)
                    if (!enrolled.Contains(swimmerId))
                        return new SetScheduleResult(SetScheduleOutcome.Invalid, null, "not_enrolled");
            }
        }

        var input = days.Select(d => new ScheduleDayInput(
            d.LabelEn.Trim(),
            string.IsNullOrWhiteSpace(d.LabelAr) ? null : d.LabelAr!.Trim(),
            d.DayDate,
            d.Races.Select(r => new ScheduleRaceInput(
                r.StrokeId, r.DistanceId, r.ScheduledTime, r.SwimmerIds.Distinct().ToList())).ToList())).ToList();

        await _schedule.ReplaceAsync(eventId, input, ct);
        var rows = await _schedule.GetAsync(eventId, ct);
        return new SetScheduleResult(SetScheduleOutcome.Ok, ToDto(rows), null);
    }

    private static ScheduleDto ToDto(IReadOnlyList<ScheduleDayRow> rows) =>
        new(rows.Select(d => new ScheduleDayDto(
            d.Id, d.LabelEn, d.LabelAr, d.DayDate,
            d.Races.Select(r => new ScheduleRaceDto(r.Id, r.StrokeId, r.DistanceId, r.ScheduledTime, r.SwimmerIds)).ToList()))
            .ToList());
```

- [ ] **Step 6: Run the tests — verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests --filter ChampionshipScheduleServiceTests`
Expected: PASS (all six). Also re-run the full Championships suite to confirm the ctor change didn't break the existing `ChampionshipService` tests: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests`. If an existing service test constructs `new ChampionshipService(...)` with 2 args, update it to pass a `Mock.Of<ICompetitionScheduleRepository>()` third arg.

- [ ] **Step 7: Commit** (only once authorized)

```bash
git add backend/src/Modules/Championships backend/tests/Kheprx.BaseBackend.Championships.UnitTests
git commit -m "feat(championships): schedule service (get + validated replace)"
```

---

### Task 4: Backend — `GET`/`PUT /schedule` controller endpoints + messages

Thin controller wiring (no new DI — the service already resolves). Matches the codebase convention of no controller unit tests; verified by build here and by the running app in Task 11.

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/ChampionshipsController.cs`
- Modify: `.../Application/Resources/ChampionshipMessages.cs`

**Interfaces:**
- Produces:
  - `GET /api/championships/{eventId}/schedule` → `ApiResponse<ScheduleDto>` (200) / 404
  - `PUT /api/championships/{eventId}/schedule` (`head_coach,captain`) body `SetScheduleRequest` → `ApiResponse<ScheduleDto>` (200) / 404 / 400 (validation) / 403
- Consumes: `IChampionshipService.GetScheduleAsync`, `.SetScheduleAsync` (Task 3).

- [ ] **Step 1: Add the schedule messages**

In `ChampionshipMessages.cs`, add two nested classes after `EnrollmentSuccess`:
```csharp
    public static class ScheduleSuccess
    {
        public static string Retrieved(string lang) => lang switch { "ar" => "الجدول", _ => "Schedule" };
        public static string Saved(string lang) => lang switch { "ar" => "تم حفظ الجدول", _ => "Schedule saved" };
    }

    public static class ScheduleErrors
    {
        public static string Invalid(string lang) => lang switch { "ar" => "بيانات الجدول غير صالحة (تأكد من العناوين والسباقات والسباحين المسجلين)", _ => "Invalid schedule (check day labels, races, and that all swimmers are enrolled)" };
    }
```

- [ ] **Step 2: Add the two controller actions**

In `ChampionshipsController.cs`, add after `SetEnrollments`:
```csharp
    /// <summary>Returns the full day → race → assigned-swimmer schedule for an event. 404 when unknown.</summary>
    [HttpGet("{eventId:guid}/schedule")]
    [ProducesResponseType(typeof(ApiResponse<ScheduleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ScheduleDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ScheduleDto>>> GetSchedule(Guid eventId, CancellationToken ct)
    {
        var lang = AppLanguage.Current;
        var dto = await _service.GetScheduleAsync(eventId, ct);
        if (dto is null)
            return StatusCode(StatusCodes.Status404NotFound,
                ApiResponse<ScheduleDto>.Failure(ChampionshipMessages.NotFound.Event(lang), "not_found"));

        return Ok(ApiResponse<ScheduleDto>.Success(ChampionshipMessages.ScheduleSuccess.Retrieved(lang), dto));
    }

    /// <summary>Replaces the whole schedule for an event (atomic). Head Coach or Captain only.
    /// Every assigned swimmer must be enrolled in the event.</summary>
    [HttpPut("{eventId:guid}/schedule")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<ScheduleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ScheduleDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ScheduleDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ScheduleDto>>> SetSchedule(Guid eventId, SetScheduleRequest request, CancellationToken ct)
    {
        var lang = AppLanguage.Current;
        var days = request.Days ?? new List<SetScheduleDay>();
        var result = await _service.SetScheduleAsync(eventId, days, ct);

        return result.Outcome switch
        {
            SetScheduleOutcome.NotFound => StatusCode(StatusCodes.Status404NotFound,
                ApiResponse<ScheduleDto>.Failure(ChampionshipMessages.NotFound.Event(lang), "not_found")),
            SetScheduleOutcome.Invalid => BadRequest(
                ApiResponse<ScheduleDto>.Failure(ChampionshipMessages.ScheduleErrors.Invalid(lang), "validation")),
            _ => Ok(ApiResponse<ScheduleDto>.Success(ChampionshipMessages.ScheduleSuccess.Saved(lang), result.Saved!)),
        };
    }
```

- [ ] **Step 3: Build the API — verify it compiles and routes resolve**

API stopped. Run: `dotnet build backend/Kheprx.BaseBackend.Api`
Expected: build succeeds (the `ScheduleDto`/`SetScheduleRequest` types resolve from the Application project already referenced by the controller).

- [ ] **Step 4: Commit** (only once authorized)

```bash
git add backend/Kheprx.BaseBackend.Api backend/src/Modules/Championships
git commit -m "feat(championships): GET/PUT /schedule endpoints"
```

---

### Task 5: Frontend — `reference.distance` (repo method + `LoadDistancesUseCase`)

Mirrors `getStrokes` / `LoadStrokesUseCase` exactly.

**Files:**
- Modify: `frontend/src/app/features/reference/domain/repositories/reference.repository.ts`
- Modify: `frontend/src/app/features/reference/data/repositories/reference.repository.impl.ts`
- Create: `frontend/src/app/features/reference/domain/usecases/load-distances.use-case.ts`
- Modify: `frontend/src/app/features/reference/index.ts`
- Test: `frontend/src/app/features/reference/testing/domain/usecases/load-distances.use-case.spec.ts`

> **Jest test path note:** confirm the reference feature's existing spec location by finding any `*.spec.ts` under `features/reference/`. If specs live somewhere other than `features/reference/testing/…`, place the new spec beside the existing ones instead.

**Interfaces:**
- Produces: `IReferenceRepository.getDistances(): Promise<CodedLookupListDtoRs>`; `LoadDistancesUseCase extends UseCase<void, LookupItem[]>`.
- Consumes: `CodedLookupListDtoRs`, `isCodedLookupListValid`, `LookupItem` (existing).

- [ ] **Step 1: Write the failing use-case test**

Create `load-distances.use-case.spec.ts`:
```typescript
import { TestBed } from '@angular/core/testing';
import { REFERENCE_REPOSITORY } from '@features/reference/domain/repositories/reference.repository';
import { LoadDistancesUseCase } from '@features/reference/domain/usecases/load-distances.use-case';

function setup(getDistances: jest.Mock) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      LoadDistancesUseCase,
      { provide: REFERENCE_REPOSITORY, useValue: { getDistances } },
    ],
  });
  return TestBed.inject(LoadDistancesUseCase);
}

describe('LoadDistancesUseCase', () => {
  it('maps a valid response to LookupItem[]', async () => {
    const getDistances = jest.fn().mockResolvedValue({
      data: [{ id: 'd1', code: '50m', nameEn: '50m', nameAr: '٥٠ متر' }],
    });
    const uc = setup(getDistances);

    const res = await uc.run();

    expect(res.ok).toBe(true);
    expect(res.ok && res.data).toEqual([{ id: 'd1', code: '50m', nameEn: '50m', nameAr: '٥٠ متر' }]);
  });

  it('fails with a validation error on a malformed payload', async () => {
    const getDistances = jest.fn().mockResolvedValue({ data: [{ id: 'd1' }] });
    const uc = setup(getDistances);

    const res = await uc.run();

    expect(res.ok).toBe(false);
  });
});
```

- [ ] **Step 2: Run — verify it fails**

Run: `cd frontend && npx jest load-distances`
Expected: FAIL — `LoadDistancesUseCase` and `getDistances` don't exist.

- [ ] **Step 3: Add `getDistances` to the repository port**

In `reference.repository.ts`, add to the interface after `getStrokes`:
```typescript
  getDistances(): Promise<CodedLookupListDtoRs>;
```

- [ ] **Step 4: Implement `getDistances`**

In `reference.repository.impl.ts`, add after `getStrokes`:
```typescript
  getDistances(): Promise<CodedLookupListDtoRs> {
    return this.http.get<CodedLookupListDtoRs>('/api/reference/distances');
  }
```

- [ ] **Step 5: Create the use-case**

`load-distances.use-case.ts`:
```typescript
// load-distances.use-case.ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { REFERENCE_REPOSITORY } from '@features/reference/domain/repositories/reference.repository';
import { isCodedLookupListValid } from '@features/reference/data/dto/reference.dto';
import { LookupItem } from '@features/reference/domain/model/reference';

@Injectable({ providedIn: 'root' })
export class LoadDistancesUseCase extends UseCase<void, LookupItem[]> {
  private readonly repo = inject(REFERENCE_REPOSITORY);
  constructor() { super('LoadDistances'); }
  protected async execute(): Promise<LookupItem[]> {
    const res = await this.repo.getDistances();
    if (!isCodedLookupListValid(res.data)) throw new AppError('Invalid distances received', 'validation');
    return res.data.map((d) => ({ id: d.id, code: d.code, nameEn: d.nameEn, nameAr: d.nameAr }));
  }
}
```

- [ ] **Step 6: Export it from the barrel**

In `reference/index.ts`, add:
```typescript
export * from '@features/reference/domain/usecases/load-distances.use-case';
```

- [ ] **Step 7: Run — verify it passes**

Run: `cd frontend && npx jest load-distances`
Expected: PASS.

- [ ] **Step 8: Commit** (only once authorized)

```bash
git add frontend/src/app/features/reference
git commit -m "feat(reference): frontend getDistances + LoadDistancesUseCase"
```

---

### Task 6: Frontend — schedule DTOs, editor model, mapper + repo methods

Defines the wire DTOs, the editor model (with client `key`s + a status helper), the mapper, and the two repository methods.

**Files:**
- Create: `frontend/src/app/features/championships/data/dto/schedule.dto.ts`
- Create: `frontend/src/app/features/championships/domain/model/competition-schedule.ts`
- Create: `frontend/src/app/features/championships/data/dto/schedule.mapper.ts`
- Modify: `frontend/src/app/features/championships/domain/repositories/championships.repository.ts`
- Modify: `frontend/src/app/features/championships/data/repositories/championships.repository.impl.ts`
- Test: `frontend/src/app/features/championships/testing/data/dto/schedule.mapper.spec.ts`

**Interfaces:**
- Produces (DTOs): `ScheduleDtoRs extends BaseResponseRs<ScheduleDtoData>`, `ScheduleDtoData { days: ScheduleDayDtoRs[] }`, `SetScheduleRq`, `isScheduleDtoValid`.
- Produces (model): `ScheduleRaceData`, `ScheduleDayData` (key-less), `ScheduleRace`, `ScheduleDay` (with `key`), `toScheduleData(days: ScheduleDay[]): ScheduleDayData[]`, `serializeSchedule(days): string`, `raceStatus(dayDate, time): 'scheduled' | 'awaitingResults'`.
- Produces (mapper): `toScheduleDataList(d: ScheduleDtoData): ScheduleDayData[]`, `toSetScheduleRq(days: ScheduleDayData[]): SetScheduleRq`.
- Produces (repo): `getSchedule(eventId): Promise<ScheduleDtoRs>`, `setSchedule(eventId, rq): Promise<ScheduleDtoRs>`.

- [ ] **Step 1: Write the failing mapper test**

Create `schedule.mapper.spec.ts`:
```typescript
import { ScheduleDtoData } from '@features/championships/data/dto/schedule.dto';
import { toScheduleDataList, toSetScheduleRq } from '@features/championships/data/dto/schedule.mapper';
import { ScheduleDayData } from '@features/championships/domain/model/competition-schedule';

describe('schedule mapper', () => {
  it('reads a DTO tree and normalizes the time to HH:mm', () => {
    const dto: ScheduleDtoData = {
      days: [
        {
          id: 'day1', labelEn: 'Day 1', labelAr: null, dayDate: '2023-11-15',
          races: [
            { id: 'r1', strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00:00', swimmerIds: ['s1', 's2'] },
          ],
        },
      ],
    };

    const data = toScheduleDataList(dto);

    expect(data).toHaveLength(1);
    expect(data[0].labelEn).toBe('Day 1');
    expect(data[0].races[0].scheduledTime).toBe('09:00'); // sliced from HH:mm:ss
    expect(data[0].races[0].swimmerIds).toEqual(['s1', 's2']);
  });

  it('builds a SetScheduleRq that drops client keys', () => {
    const data: ScheduleDayData[] = [
      { labelEn: 'Day 1', labelAr: 'يوم', dayDate: '2023-11-15',
        races: [{ strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00', swimmerIds: ['s1'] }] },
    ];

    const rq = toSetScheduleRq(data);

    expect(rq).toEqual({
      days: [
        { labelEn: 'Day 1', labelAr: 'يوم', dayDate: '2023-11-15',
          races: [{ strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00', swimmerIds: ['s1'] }] },
      ],
    });
  });
});
```

- [ ] **Step 2: Run — verify it fails**

Run: `cd frontend && npx jest schedule.mapper`
Expected: FAIL — modules don't exist.

- [ ] **Step 3: Create the DTOs**

`schedule.dto.ts`:
```typescript
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface ScheduleRaceDtoRs {
  id: string;
  strokeId: string;
  distanceId: string;
  scheduledTime: string | null; // 'HH:mm:ss' | 'HH:mm' | null
  swimmerIds: string[];
}
export interface ScheduleDayDtoRs {
  id: string;
  labelEn: string;
  labelAr: string | null;
  dayDate: string;              // 'YYYY-MM-DD'
  races: ScheduleRaceDtoRs[];
}
export interface ScheduleDtoData {
  days: ScheduleDayDtoRs[];
}
export interface ScheduleDtoRs extends BaseResponseRs<ScheduleDtoData> {}

/** PUT body — the whole desired schedule (server replaces atomically). */
export interface SetScheduleRq {
  days: {
    labelEn: string;
    labelAr: string | null;
    dayDate: string;
    races: {
      strokeId: string;
      distanceId: string;
      scheduledTime: string | null;
      swimmerIds: string[];
    }[];
  }[];
}

function isStr(v: unknown): v is string { return typeof v === 'string'; }

export function isScheduleDtoValid(data: unknown): data is ScheduleDtoData {
  const d = data as ScheduleDtoData;
  if (!d || typeof d !== 'object' || !Array.isArray(d.days)) return false;
  return d.days.every(
    (day) =>
      day != null && typeof day === 'object' &&
      isStr(day.id) && isStr(day.labelEn) && isStr(day.dayDate) &&
      Array.isArray(day.races) &&
      day.races.every(
        (r) =>
          r != null && typeof r === 'object' &&
          isStr(r.id) && isStr(r.strokeId) && isStr(r.distanceId) &&
          Array.isArray(r.swimmerIds) && r.swimmerIds.every(isStr),
      ),
  );
}
```

- [ ] **Step 4: Create the editor model + helpers**

`competition-schedule.ts`:
```typescript
// Editor + wire-data model for the Competition Days schedule.

/** Key-less shape used on the wire (load result / save request). */
export interface ScheduleRaceData {
  strokeId: string;
  distanceId: string;
  scheduledTime: string | null; // 'HH:mm' | null
  swimmerIds: string[];
}
export interface ScheduleDayData {
  labelEn: string;
  labelAr: string | null;
  dayDate: string;              // 'YYYY-MM-DD'
  races: ScheduleRaceData[];
}

/** Editor shape carried in the ViewModel — adds a stable client `key` for @for tracking. */
export interface ScheduleRace extends ScheduleRaceData {
  key: string;
}
export interface ScheduleDay extends Omit<ScheduleDayData, 'races'> {
  key: string;
  races: ScheduleRace[];
}

/** Strip client keys + sort swimmerIds so dirty-comparison is order-stable for selections. */
export function toScheduleData(days: ScheduleDay[]): ScheduleDayData[] {
  return days.map((d) => ({
    labelEn: d.labelEn,
    labelAr: d.labelAr,
    dayDate: d.dayDate,
    races: d.races.map((r) => ({
      strokeId: r.strokeId,
      distanceId: r.distanceId,
      scheduledTime: r.scheduledTime,
      swimmerIds: [...r.swimmerIds].sort(),
    })),
  }));
}

/** Canonical string for dirty tracking. */
export function serializeSchedule(days: ScheduleDay[]): string {
  return JSON.stringify(toScheduleData(days));
}

/**
 * Client-derived status pill. A race is 'awaitingResults' once its scheduled start has passed,
 * otherwise 'scheduled'. No results are persisted here — that is a later tab.
 */
export function raceStatus(dayDate: string, time: string | null): 'scheduled' | 'awaitingResults' {
  if (!dayDate) return 'scheduled';
  const stamp = new Date(`${dayDate}T${time || '00:00'}`);
  if (Number.isNaN(stamp.getTime())) return 'scheduled';
  return stamp.getTime() <= Date.now() ? 'awaitingResults' : 'scheduled';
}
```

- [ ] **Step 5: Create the mapper**

`schedule.mapper.ts`:
```typescript
import { ScheduleDtoData, SetScheduleRq } from '@features/championships/data/dto/schedule.dto';
import { ScheduleDayData } from '@features/championships/domain/model/competition-schedule';

/** DTO tree → key-less data the ViewModel hydrates with client keys. Time normalized to 'HH:mm'. */
export function toScheduleDataList(dto: ScheduleDtoData): ScheduleDayData[] {
  return dto.days.map((day) => ({
    labelEn: day.labelEn,
    labelAr: day.labelAr,
    dayDate: day.dayDate,
    races: day.races.map((r) => ({
      strokeId: r.strokeId,
      distanceId: r.distanceId,
      scheduledTime: r.scheduledTime ? r.scheduledTime.slice(0, 5) : null,
      swimmerIds: [...r.swimmerIds],
    })),
  }));
}

/** Key-less data → PUT body. */
export function toSetScheduleRq(days: ScheduleDayData[]): SetScheduleRq {
  return {
    days: days.map((d) => ({
      labelEn: d.labelEn,
      labelAr: d.labelAr,
      dayDate: d.dayDate,
      races: d.races.map((r) => ({
        strokeId: r.strokeId,
        distanceId: r.distanceId,
        scheduledTime: r.scheduledTime,
        swimmerIds: r.swimmerIds,
      })),
    })),
  };
}
```

- [ ] **Step 6: Run the mapper test — verify it passes**

Run: `cd frontend && npx jest schedule.mapper`
Expected: PASS.

- [ ] **Step 7: Add the repository methods (port + impl)**

In `championships.repository.ts`: add the import and two methods:
```typescript
import { ScheduleDtoRs, SetScheduleRq } from '@features/championships/data/dto/schedule.dto';
```
```typescript
  getSchedule(eventId: string): Promise<ScheduleDtoRs>;
  setSchedule(eventId: string, rq: SetScheduleRq): Promise<ScheduleDtoRs>;
```
In `championships.repository.impl.ts`: add the same import and:
```typescript
  getSchedule(eventId: string): Promise<ScheduleDtoRs> {
    return this.http.get<ScheduleDtoRs>(`/api/championships/${eventId}/schedule`);
  }

  setSchedule(eventId: string, rq: SetScheduleRq): Promise<ScheduleDtoRs> {
    return this.http.put<ScheduleDtoRs>(`/api/championships/${eventId}/schedule`, { body: rq });
  }
```

- [ ] **Step 8: Type-check the frontend**

Run: `cd frontend && npx tsc --noEmit`
Expected: no errors.

- [ ] **Step 9: Commit** (only once authorized)

```bash
git add frontend/src/app/features/championships
git commit -m "feat(championships): schedule DTOs, editor model, mapper + repo methods"
```

---

### Task 7: Frontend — `load-schedule` + `save-schedule` use-cases

Mirrors `load-enrollments` / `save-enrollments`. `load` validates the DTO then maps to key-less data; `save` maps working data → request, PUTs, and returns the re-read tree (key-less) so the VM can rebuild its baseline.

**Files:**
- Create: `frontend/src/app/features/championships/domain/usecases/load-schedule.use-case.ts`
- Create: `frontend/src/app/features/championships/domain/usecases/save-schedule.use-case.ts`
- Test: `frontend/src/app/features/championships/testing/domain/usecases/load-schedule.use-case.spec.ts`
- Test: `frontend/src/app/features/championships/testing/domain/usecases/save-schedule.use-case.spec.ts`

**Interfaces:**
- Produces: `LoadScheduleUseCase extends UseCase<string, ScheduleDayData[]>`; `SaveScheduleUseCase extends UseCase<SaveScheduleInput, ScheduleDayData[]>` where `SaveScheduleInput { eventId: string; days: ScheduleDayData[] }`.
- Consumes: `CHAMPIONSHIPS_REPOSITORY`, `isScheduleDtoValid`, `toScheduleDataList`, `toSetScheduleRq`, `ScheduleDayData`.

- [ ] **Step 1: Write the failing use-case tests**

Create `load-schedule.use-case.spec.ts`:
```typescript
import { TestBed } from '@angular/core/testing';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { LoadScheduleUseCase } from '@features/championships/domain/usecases/load-schedule.use-case';

function setup(getSchedule: jest.Mock) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [LoadScheduleUseCase, { provide: CHAMPIONSHIPS_REPOSITORY, useValue: { getSchedule } }],
  });
  return TestBed.inject(LoadScheduleUseCase);
}

describe('LoadScheduleUseCase', () => {
  it('maps a valid schedule tree to key-less data', async () => {
    const getSchedule = jest.fn().mockResolvedValue({
      data: { days: [{ id: 'd1', labelEn: 'Day 1', labelAr: null, dayDate: '2023-11-15',
        races: [{ id: 'r1', strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00:00', swimmerIds: ['s1'] }] }] },
    });
    const res = await setup(getSchedule).run('e1');
    expect(res.ok).toBe(true);
    expect(res.ok && res.data[0].races[0].scheduledTime).toBe('09:00');
  });

  it('fails on a malformed payload', async () => {
    const getSchedule = jest.fn().mockResolvedValue({ data: { days: [{ id: 'd1' }] } });
    const res = await setup(getSchedule).run('e1');
    expect(res.ok).toBe(false);
  });
});
```
Create `save-schedule.use-case.spec.ts`:
```typescript
import { TestBed } from '@angular/core/testing';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { SaveScheduleUseCase } from '@features/championships/domain/usecases/save-schedule.use-case';
import { ScheduleDayData } from '@features/championships/domain/model/competition-schedule';

const days: ScheduleDayData[] = [
  { labelEn: 'Day 1', labelAr: null, dayDate: '2023-11-15',
    races: [{ strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00', swimmerIds: ['s1'] }] },
];

function setup(setSchedule: jest.Mock) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [SaveScheduleUseCase, { provide: CHAMPIONSHIPS_REPOSITORY, useValue: { setSchedule } }],
  });
  return TestBed.inject(SaveScheduleUseCase);
}

describe('SaveScheduleUseCase', () => {
  it('PUTs the mapped request and returns the re-read tree', async () => {
    const setSchedule = jest.fn().mockResolvedValue({
      data: { days: [{ id: 'D1', labelEn: 'Day 1', labelAr: null, dayDate: '2023-11-15',
        races: [{ id: 'R1', strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00:00', swimmerIds: ['s1'] }] }] },
    });
    const res = await setup(setSchedule).run({ eventId: 'e1', days });

    expect(setSchedule).toHaveBeenCalledWith('e1', {
      days: [{ labelEn: 'Day 1', labelAr: null, dayDate: '2023-11-15',
        races: [{ strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00', swimmerIds: ['s1'] }] }],
    });
    expect(res.ok).toBe(true);
    expect(res.ok && res.data[0].races[0].scheduledTime).toBe('09:00');
  });

  it('fails when the server returns a malformed tree', async () => {
    const setSchedule = jest.fn().mockResolvedValue({ data: { days: [{ id: 'D1' }] } });
    const res = await setup(setSchedule).run({ eventId: 'e1', days });
    expect(res.ok).toBe(false);
  });
});
```

- [ ] **Step 2: Run — verify both fail**

Run: `cd frontend && npx jest "schedule.use-case"`
Expected: FAIL — use-cases don't exist.

- [ ] **Step 3: Create `LoadScheduleUseCase`**

`load-schedule.use-case.ts`:
```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { isScheduleDtoValid } from '@features/championships/data/dto/schedule.dto';
import { toScheduleDataList } from '@features/championships/data/dto/schedule.mapper';
import { ScheduleDayData } from '@features/championships/domain/model/competition-schedule';

@Injectable({ providedIn: 'root' })
export class LoadScheduleUseCase extends UseCase<string, ScheduleDayData[]> {
  private readonly repo = inject(CHAMPIONSHIPS_REPOSITORY);
  constructor() { super('LoadSchedule'); }

  protected async execute(eventId: string): Promise<ScheduleDayData[]> {
    const res = await this.repo.getSchedule(eventId);
    if (!isScheduleDtoValid(res.data)) throw new AppError('Invalid schedule received', 'validation');
    return toScheduleDataList(res.data);
  }
}
```

- [ ] **Step 4: Create `SaveScheduleUseCase`**

`save-schedule.use-case.ts`:
```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { isScheduleDtoValid } from '@features/championships/data/dto/schedule.dto';
import { toScheduleDataList, toSetScheduleRq } from '@features/championships/data/dto/schedule.mapper';
import { ScheduleDayData } from '@features/championships/domain/model/competition-schedule';

export interface SaveScheduleInput {
  eventId: string;
  days: ScheduleDayData[];
}

@Injectable({ providedIn: 'root' })
export class SaveScheduleUseCase extends UseCase<SaveScheduleInput, ScheduleDayData[]> {
  private readonly repo = inject(CHAMPIONSHIPS_REPOSITORY);
  constructor() { super('SaveSchedule'); }

  // The PUT replaces the whole schedule and echoes the persisted tree (fresh ids). We re-map it
  // to key-less data so the ViewModel can rebuild its baseline. A 4xx (e.g. 400 validation) throws
  // → run() converts to Result.fail and the caller shows the error toast.
  protected async execute(input: SaveScheduleInput): Promise<ScheduleDayData[]> {
    const res = await this.repo.setSchedule(input.eventId, toSetScheduleRq(input.days));
    if (!isScheduleDtoValid(res.data)) throw new AppError('Invalid schedule received', 'validation');
    return toScheduleDataList(res.data);
  }
}
```

- [ ] **Step 5: Run — verify both pass**

Run: `cd frontend && npx jest "schedule.use-case"`
Expected: PASS.

- [ ] **Step 6: Commit** (only once authorized)

```bash
git add frontend/src/app/features/championships
git commit -m "feat(championships): load-schedule + save-schedule use-cases"
```

---

### Task 8: Frontend — `CompetitionDaysViewModel`

The isolated ViewModel for the Days tab: lazy-loads (schedule + strokes + distances + roster + enrollments), holds the editor tree in a signal, tracks dirty, edits days/races/assignments, and saves once. Provided at the `/championships/:id` route.

**Files:**
- Create: `frontend/src/app/features/championships/presentation/pages/championship-detail/competition-days.viewmodel.ts`
- Test: `frontend/src/app/features/championships/testing/presentation/pages/championship-detail/competition-days.viewmodel.spec.ts`

**Interfaces:**
- Produces: `CompetitionDaysViewModel` with signals `loading/loaded/error/saving/strokes/distances/days`, computeds `canManage/enrolledSwimmers/dayCount/raceCount/entryCount/dirty`, and methods `ensureLoaded(eventId)`, `load(eventId)`, `addDay()`, `removeDay(key)`, `updateDay(key, patch)`, `addRace(dayKey)`, `removeRace(dayKey, raceKey)`, `updateRace(dayKey, raceKey, patch)`, `toggleSwimmer(dayKey, raceKey, swimmerId)`, `isAssigned(dayKey, raceKey, swimmerId)`, `save()`, plus display helpers `strokeLabel/distanceLabel/swimmerName/statusOf`.
- Consumes: `LoadScheduleUseCase`, `SaveScheduleUseCase`, `LoadStrokesUseCase`, `LoadDistancesUseCase`, `LoadEnrollmentsUseCase`, `ListSwimmersUseCase`, `LanguageStore`, `AuthSessionStore`, `NotificationService`, `TranslateService`, the editor model (Task 6).

- [ ] **Step 1: Write the failing ViewModel spec**

Create `competition-days.viewmodel.spec.ts`:
```typescript
import { TestBed } from '@angular/core/testing';
import { ok } from '@core/domain/result/result';
import { LanguageStore } from '@core/i18n/language.store';
import { TranslateService } from '@core/i18n';
import { NotificationService } from '@core/ui/notification.service';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { CompetitionDaysViewModel } from '@features/championships/presentation/pages/championship-detail/competition-days.viewmodel';
import { LoadScheduleUseCase } from '@features/championships/domain/usecases/load-schedule.use-case';
import { SaveScheduleUseCase } from '@features/championships/domain/usecases/save-schedule.use-case';
import { LoadStrokesUseCase, LoadDistancesUseCase } from '@features/reference';
import { LoadEnrollmentsUseCase } from '@features/championships/domain/usecases/load-enrollments.use-case';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { ScheduleDayData } from '@features/championships/domain/model/competition-schedule';
import { AppError } from '@core/domain/errors/app-error';

const strokes = [{ id: 'st1', code: 'freestyle', nameEn: 'Freestyle', nameAr: null }];
const distances = [{ id: 'ds1', code: '50m', nameEn: '50m', nameAr: null }];
const roster = [
  { id: 's1', uid: 'U1', nameEn: 'Ahmed', nameAr: 'أحمد', clubNameEn: 'Oasis', clubNameAr: null, gender: 'male', age: 18 },
  { id: 's2', uid: 'U2', nameEn: 'Sara', nameAr: 'سارة', clubNameEn: 'Oasis', clubNameAr: null, gender: 'female', age: 16 },
];
const seededDay: ScheduleDayData = {
  labelEn: 'Day 1', labelAr: null, dayDate: '2023-11-15',
  races: [{ strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00', swimmerIds: ['s1'] }],
};

interface Opts { role?: string; schedRun?: jest.Mock; saveRun?: jest.Mock; enrRun?: jest.Mock; }

function setup(opts: Opts = {}) {
  const schedRun = opts.schedRun ?? jest.fn().mockResolvedValue(ok([seededDay]));
  const saveRun = opts.saveRun ?? jest.fn().mockResolvedValue(ok([seededDay]));
  const enrRun = opts.enrRun ?? jest.fn().mockResolvedValue(ok(['s1', 's2']));
  const notify = { success: jest.fn(), error: jest.fn() };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      CompetitionDaysViewModel,
      { provide: LoadScheduleUseCase, useValue: { run: schedRun } },
      { provide: SaveScheduleUseCase, useValue: { run: saveRun } },
      { provide: LoadStrokesUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(strokes)) } },
      { provide: LoadDistancesUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(distances)) } },
      { provide: LoadEnrollmentsUseCase, useValue: { run: enrRun } },
      { provide: ListSwimmersUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(roster)) } },
      { provide: LanguageStore, useValue: { lang: () => 'en' } },
      { provide: AuthSessionStore, useValue: { role: () => opts.role ?? 'head_coach' } },
      { provide: NotificationService, useValue: notify },
      { provide: TranslateService, useValue: { t: (k: string) => k } },
    ],
  });
  return { vm: TestBed.inject(CompetitionDaysViewModel), schedRun, saveRun, enrRun, notify };
}

describe('CompetitionDaysViewModel', () => {
  it('loads the schedule, lookups, roster and enrolled set', async () => {
    const { vm } = setup();
    await vm.load('e1');
    expect(vm.loaded()).toBe(true);
    expect(vm.dayCount()).toBe(1);
    expect(vm.raceCount()).toBe(1);
    expect(vm.entryCount()).toBe(1);
    expect(vm.enrolledSwimmers().map((s) => s.id)).toEqual(['s1', 's2']);
    expect(vm.dirty()).toBe(false);
  });

  it('ensureLoaded only loads once per event', async () => {
    const { vm, schedRun } = setup();
    await vm.ensureLoaded('e1');
    await vm.ensureLoaded('e1');
    expect(schedRun).toHaveBeenCalledTimes(1);
  });

  it('sets error when the schedule fails to load', async () => {
    const schedRun = jest.fn().mockResolvedValue({ ok: false, error: new AppError('x', 'http', 404) });
    const { vm } = setup({ schedRun });
    await vm.load('e1');
    expect(vm.error()).toBe(true);
    expect(vm.loaded()).toBe(false);
  });

  it('addDay / addRace / toggleSwimmer mutate the tree and drive totals + dirty', async () => {
    const { vm } = setup();
    await vm.load('e1');
    vm.addDay();
    expect(vm.dayCount()).toBe(2);
    expect(vm.dirty()).toBe(true);
    const newDay = vm.days()[1];
    vm.addRace(newDay.key);
    expect(vm.raceCount()).toBe(2);
    const race = vm.days()[1].races[0];
    vm.toggleSwimmer(newDay.key, race.key, 's2');
    expect(vm.isAssigned(newDay.key, race.key, 's2')).toBe(true);
    expect(vm.entryCount()).toBe(2);
  });

  it('removeDay / removeRace shrink the tree', async () => {
    const { vm } = setup();
    await vm.load('e1');
    const day = vm.days()[0];
    vm.removeRace(day.key, day.races[0].key);
    expect(vm.raceCount()).toBe(0);
    vm.removeDay(day.key);
    expect(vm.dayCount()).toBe(0);
  });

  it('updateRace changes distance/stroke/time', async () => {
    const { vm } = setup();
    await vm.load('e1');
    const day = vm.days()[0];
    vm.updateRace(day.key, day.races[0].key, { scheduledTime: '10:30' });
    expect(vm.days()[0].races[0].scheduledTime).toBe('10:30');
  });

  it('edits are no-ops for a non-manager', async () => {
    const { vm } = setup({ role: 'swimmer' });
    await vm.load('e1');
    vm.addDay();
    expect(vm.dayCount()).toBe(1);
    expect(vm.dirty()).toBe(false);
  });

  it('save persists, toasts success, rebuilds baseline and clears dirty', async () => {
    const { vm, saveRun, notify } = setup();
    await vm.load('e1');
    vm.addDay();
    expect(vm.dirty()).toBe(true);
    saveRun.mockResolvedValue(ok(vm.days().map((d) => ({ labelEn: d.labelEn, labelAr: d.labelAr, dayDate: d.dayDate, races: d.races.map((r) => ({ strokeId: r.strokeId, distanceId: r.distanceId, scheduledTime: r.scheduledTime, swimmerIds: r.swimmerIds })) }))));
    await vm.save();
    expect(saveRun).toHaveBeenCalled();
    expect(notify.success).toHaveBeenCalled();
    expect(vm.dirty()).toBe(false);
  });

  it('save surfaces an error toast and keeps the working tree on failure', async () => {
    const saveRun = jest.fn().mockResolvedValue({ ok: false, error: new AppError('bad', 'http', 400) });
    const { vm, notify } = setup({ saveRun });
    await vm.load('e1');
    vm.addDay();
    await vm.save();
    expect(notify.error).toHaveBeenCalled();
    expect(vm.dayCount()).toBe(2);
    expect(vm.dirty()).toBe(true);
  });

  it('save is a no-op when not dirty or not a manager', async () => {
    const saveRun = jest.fn();
    const { vm } = setup({ role: 'captain', saveRun });
    await vm.load('e1');            // clean
    await vm.save();
    expect(saveRun).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run — verify it fails**

Run: `cd frontend && npx jest competition-days.viewmodel`
Expected: FAIL — the ViewModel doesn't exist.

- [ ] **Step 3: Create the ViewModel**

`competition-days.viewmodel.ts`:
```typescript
import { Injectable, computed, inject, signal } from '@angular/core';
import { LanguageStore } from '@core/i18n/language.store';
import { TranslateService } from '@core/i18n';
import { NotificationService } from '@core/ui/notification.service';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { LoadScheduleUseCase } from '@features/championships/domain/usecases/load-schedule.use-case';
import { SaveScheduleUseCase } from '@features/championships/domain/usecases/save-schedule.use-case';
import { LoadEnrollmentsUseCase } from '@features/championships/domain/usecases/load-enrollments.use-case';
import { LoadStrokesUseCase, LoadDistancesUseCase, LookupItem } from '@features/reference';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';
import {
  ScheduleDay, ScheduleRace, ScheduleDayData,
  serializeSchedule, toScheduleData, raceStatus,
} from '@features/championships/domain/model/competition-schedule';

@Injectable()
export class CompetitionDaysViewModel {
  private readonly loadScheduleUc = inject(LoadScheduleUseCase);
  private readonly saveScheduleUc = inject(SaveScheduleUseCase);
  private readonly loadEnrollmentsUc = inject(LoadEnrollmentsUseCase);
  private readonly loadStrokesUc = inject(LoadStrokesUseCase);
  private readonly loadDistancesUc = inject(LoadDistancesUseCase);
  private readonly listSwimmersUc = inject(ListSwimmersUseCase);
  private readonly language = inject(LanguageStore);
  private readonly session = inject(AuthSessionStore);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  private eventId = '';
  private keySeq = 0;

  readonly loading = signal(false);
  readonly loaded = signal(false);
  readonly error = signal(false);
  readonly saving = signal(false);

  readonly strokes = signal<LookupItem[]>([]);
  readonly distances = signal<LookupItem[]>([]);
  readonly roster = signal<SwimmerListItem[]>([]);
  private readonly enrolledIds = signal<ReadonlySet<string>>(new Set());
  readonly days = signal<ScheduleDay[]>([]);
  private readonly baseline = signal<string>('[]');

  readonly canManage = computed(() => {
    const r = this.session.role();
    return r === 'head_coach' || r === 'captain';
  });

  readonly enrolledSwimmers = computed<SwimmerListItem[]>(() => {
    const ids = this.enrolledIds();
    return this.roster().filter((s) => ids.has(s.id));
  });

  readonly dayCount = computed(() => this.days().length);
  readonly raceCount = computed(() => this.days().reduce((a, d) => a + d.races.length, 0));
  readonly entryCount = computed(() =>
    this.days().reduce((a, d) => a + d.races.reduce((b, r) => b + r.swimmerIds.length, 0), 0));
  readonly dirty = computed(() => serializeSchedule(this.days()) !== this.baseline());

  async ensureLoaded(eventId: string): Promise<void> {
    if (this.loaded() && this.eventId === eventId) return;
    await this.load(eventId);
  }

  async load(eventId: string): Promise<void> {
    this.eventId = eventId;
    this.loading.set(true);
    this.error.set(false);
    this.loaded.set(false);

    const [schedRes, strokesRes, distRes, rosterRes, enrRes] = await Promise.all([
      this.loadScheduleUc.run(eventId),
      this.loadStrokesUc.run(),
      this.loadDistancesUc.run(),
      this.listSwimmersUc.run(undefined),
      this.loadEnrollmentsUc.run(eventId),
    ]);

    if (!schedRes.ok) {
      this.loading.set(false);
      this.error.set(true);
      return;
    }
    this.strokes.set(strokesRes.ok ? strokesRes.data : []);
    this.distances.set(distRes.ok ? distRes.data : []);
    this.roster.set(rosterRes.ok ? rosterRes.data : []);
    this.enrolledIds.set(new Set(enrRes.ok ? enrRes.data : []));

    const days = schedRes.data.map((d) => this.hydrateDay(d));
    this.days.set(days);
    this.baseline.set(serializeSchedule(days));
    this.loaded.set(true);
    this.loading.set(false);
  }

  // ---- day ops ----
  addDay(): void {
    if (!this.canManage()) return;
    const n = this.days().length + 1;
    const day: ScheduleDay = {
      key: this.nextKey(),
      labelEn: `${this.i18n.t('championships.days.dayName')} ${n}`,
      labelAr: null,
      dayDate: '',
      races: [],
    };
    this.days.set([...this.days(), day]);
  }

  removeDay(dayKey: string): void {
    if (!this.canManage()) return;
    this.days.set(this.days().filter((d) => d.key !== dayKey));
  }

  updateDay(dayKey: string, patch: Partial<Pick<ScheduleDay, 'labelEn' | 'labelAr' | 'dayDate'>>): void {
    if (!this.canManage()) return;
    this.days.set(this.days().map((d) => (d.key === dayKey ? { ...d, ...patch } : d)));
  }

  // ---- race ops ----
  addRace(dayKey: string): void {
    if (!this.canManage()) return;
    const race: ScheduleRace = {
      key: this.nextKey(),
      strokeId: this.strokes()[0]?.id ?? '',
      distanceId: this.distances()[0]?.id ?? '',
      scheduledTime: null,
      swimmerIds: [],
    };
    this.days.set(this.days().map((d) => (d.key === dayKey ? { ...d, races: [...d.races, race] } : d)));
  }

  removeRace(dayKey: string, raceKey: string): void {
    if (!this.canManage()) return;
    this.days.set(this.days().map((d) =>
      d.key === dayKey ? { ...d, races: d.races.filter((r) => r.key !== raceKey) } : d));
  }

  updateRace(dayKey: string, raceKey: string, patch: Partial<Pick<ScheduleRace, 'strokeId' | 'distanceId' | 'scheduledTime'>>): void {
    if (!this.canManage()) return;
    this.days.set(this.days().map((d) =>
      d.key !== dayKey ? d : { ...d, races: d.races.map((r) => (r.key === raceKey ? { ...r, ...patch } : r)) }));
  }

  toggleSwimmer(dayKey: string, raceKey: string, swimmerId: string): void {
    if (!this.canManage()) return;
    this.days.set(this.days().map((d) => {
      if (d.key !== dayKey) return d;
      return {
        ...d,
        races: d.races.map((r) => {
          if (r.key !== raceKey) return r;
          const has = r.swimmerIds.includes(swimmerId);
          return { ...r, swimmerIds: has ? r.swimmerIds.filter((id) => id !== swimmerId) : [...r.swimmerIds, swimmerId] };
        }),
      };
    }));
  }

  isAssigned(dayKey: string, raceKey: string, swimmerId: string): boolean {
    const day = this.days().find((d) => d.key === dayKey);
    const race = day?.races.find((r) => r.key === raceKey);
    return race?.swimmerIds.includes(swimmerId) ?? false;
  }

  async save(): Promise<void> {
    if (!this.canManage() || this.saving() || !this.dirty()) return;
    this.saving.set(true);
    try {
      const data: ScheduleDayData[] = toScheduleData(this.days());
      const res = await this.saveScheduleUc.run({ eventId: this.eventId, days: data });
      if (res.ok) {
        this.notify.success(this.i18n.t('championships.days.saved'));
        const days = res.data.map((d) => this.hydrateDay(d));
        this.days.set(days);
        this.baseline.set(serializeSchedule(days));
      } else {
        this.notify.error(this.i18n.t('championships.days.saveFailed'));
      }
    } finally {
      this.saving.set(false);
    }
  }

  // ---- display helpers ----
  strokeLabel(id: string): string { return this.lookupLabel(this.strokes(), id); }
  distanceLabel(id: string): string { return this.lookupLabel(this.distances(), id); }
  swimmerName(s: SwimmerListItem): string {
    return this.language.lang() === 'ar' ? (s.nameAr ?? s.nameEn) : s.nameEn;
  }
  statusOf(day: ScheduleDay, race: ScheduleRace): 'scheduled' | 'awaitingResults' {
    return raceStatus(day.dayDate, race.scheduledTime);
  }

  private lookupLabel(items: LookupItem[], id: string): string {
    const item = items.find((i) => i.id === id);
    if (!item) return '';
    return this.language.lang() === 'ar' ? (item.nameAr ?? item.nameEn) : item.nameEn;
  }

  private hydrateDay(d: ScheduleDayData): ScheduleDay {
    return {
      key: this.nextKey(),
      labelEn: d.labelEn,
      labelAr: d.labelAr,
      dayDate: d.dayDate,
      races: d.races.map((r) => ({ key: this.nextKey(), ...r, swimmerIds: [...r.swimmerIds] })),
    };
  }

  private nextKey(): string { return `k${this.keySeq++}`; }
}
```

- [ ] **Step 4: Run — verify it passes**

Run: `cd frontend && npx jest competition-days.viewmodel`
Expected: PASS (all cases).

- [ ] **Step 5: Commit** (only once authorized)

```bash
git add frontend/src/app/features/championships
git commit -m "feat(championships): CompetitionDaysViewModel"
```

---

### Task 9: Frontend — enable the `days` tab (page wiring + template + i18n + routing)

Wires the Days tab into the detail page: enables it, lazy-loads on first activation, renders the schedule editor, adds i18n and the route provider.

**Files:**
- Modify: `frontend/src/app/features/championships/presentation/pages/championship-detail/championship-detail.page.ts`
- Modify: `frontend/src/app/features/championships/presentation/pages/championship-detail/championship-detail.page.html`
- Modify: `frontend/src/app/features/championships/index.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`
- Test: `frontend/src/app/features/championships/testing/presentation/pages/championship-detail/championship-detail.page.spec.ts`

**Interfaces:**
- Consumes: `CompetitionDaysViewModel` (Task 8), `ChampionshipDetailViewModel` (existing).
- Produces: `ChampionshipDetailPage.onTab(key)` + `enabledTabs` now including `'days'`; exported `CompetitionDaysViewModel` from the feature barrel.

- [ ] **Step 1: Write the failing page spec**

Create `championship-detail.page.spec.ts`:
```typescript
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { ChampionshipDetailPage } from '@features/championships/presentation/pages/championship-detail/championship-detail.page';
import { ChampionshipDetailViewModel } from '@features/championships/presentation/pages/championship-detail/championship-detail.viewmodel';
import { CompetitionDaysViewModel } from '@features/championships/presentation/pages/championship-detail/competition-days.viewmodel';

function setup() {
  const detailVm = {
    load: jest.fn(),
    setTab: jest.fn(),
    activeTab: () => 'enrollment',
    loading: () => true, error: () => false, notFound: () => false, championship: () => null,
  };
  const daysVm = { ensureLoaded: jest.fn() };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [ChampionshipDetailPage],
    providers: [
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'e1' } } } },
    ],
  })
    .overrideComponent(ChampionshipDetailPage, {
      set: { providers: [
        { provide: ChampionshipDetailViewModel, useValue: detailVm },
        { provide: CompetitionDaysViewModel, useValue: daysVm },
      ] },
    });
  const fixture = TestBed.createComponent(ChampionshipDetailPage);
  return { c: fixture.componentInstance, detailVm, daysVm };
}

describe('ChampionshipDetailPage', () => {
  it('enables the enrollment and days tabs, disables finished and results', () => {
    const { c } = setup();
    expect(c.isEnabled('enrollment')).toBe(true);
    expect(c.isEnabled('days')).toBe(true);
    expect(c.isEnabled('finished')).toBe(false);
    expect(c.isEnabled('results')).toBe(false);
  });

  it('lazy-loads the days schedule on first activation of the days tab', () => {
    const { c, detailVm, daysVm } = setup();
    c.ngOnInit();
    c.onTab('days');
    expect(detailVm.setTab).toHaveBeenCalledWith('days');
    expect(daysVm.ensureLoaded).toHaveBeenCalledWith('e1');
  });

  it('ignores clicks on a disabled tab', () => {
    const { c, detailVm } = setup();
    c.onTab('finished');
    expect(detailVm.setTab).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run — verify it fails**

Run: `cd frontend && npx jest championship-detail.page`
Expected: FAIL — `onTab` / `CompetitionDaysViewModel` provider / `days` enabled don't exist yet.

- [ ] **Step 3: Update the page component**

Replace `championship-detail.page.ts` with:
```typescript
import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@core/i18n';
import { ChampionshipDetailViewModel, DetailTab } from './championship-detail.viewmodel';
import { CompetitionDaysViewModel } from './competition-days.viewmodel';

interface DetailTabDef { key: DetailTab; labelKey: string; }

@Component({
  selector: 'app-championship-detail-page',
  standalone: true,
  imports: [FormsModule, TranslatePipe, RouterLink],
  templateUrl: './championship-detail.page.html',
  providers: [CompetitionDaysViewModel],
})
export class ChampionshipDetailPage implements OnInit {
  readonly vm = inject(ChampionshipDetailViewModel);
  readonly daysVm = inject(CompetitionDaysViewModel);
  private readonly route = inject(ActivatedRoute);

  private eventId = '';

  // Enrollment + Competition Days are live; Finished / Results are still placeholders.
  protected readonly enabledTabs = new Set<DetailTab>(['enrollment', 'days']);
  isEnabled(key: DetailTab): boolean { return this.enabledTabs.has(key); }

  protected readonly tabs: DetailTabDef[] = [
    { key: 'enrollment', labelKey: 'championships.detail.tabs.enrollment' },
    { key: 'days', labelKey: 'championships.detail.tabs.days' },
    { key: 'finished', labelKey: 'championships.detail.tabs.finished' },
    { key: 'results', labelKey: 'championships.detail.tabs.results' },
  ];

  ngOnInit(): void {
    this.eventId = this.route.snapshot.paramMap.get('id') ?? '';
    void this.vm.load(this.eventId);
  }

  onTab(key: DetailTab): void {
    if (!this.isEnabled(key)) return;
    this.vm.setTab(key);
    if (key === 'days') void this.daysVm.ensureLoaded(this.eventId);
  }
}
```

> Note: providing `CompetitionDaysViewModel` on the component (not only the route) keeps the page self-contained; the route provider in Step 6 is belt-and-suspenders for lazy-load ordering. Either alone works; keep the component provider.

- [ ] **Step 4: Update the tab click handler in the template**

In `championship-detail.page.html`, change the tab button's click binding from:
```html
                (click)="isEnabled(t.key) && vm.setTab(t.key)">
```
to:
```html
                (click)="onTab(t.key)">
```

- [ ] **Step 5: Add the Competition Days section to the template**

In `championship-detail.page.html`, immediately **after** the closing `}` of the `@if (vm.activeTab() === 'enrollment') { … }` block (and still inside the `@else if (vm.championship(); as c) { … }` branch), add:
```html
    @if (vm.activeTab() === 'days') {
      <section class="rounded-2xl border border-border bg-surface shadow-sm">
        @if (daysVm.loading()) {
          <p class="p-6 text-center text-sm text-text-secondary">{{ 'championships.detail.loading' | translate }}</p>
        } @else if (daysVm.error()) {
          <p class="p-6 text-center text-sm text-danger">{{ 'championships.detail.error' | translate }}</p>
        } @else if (daysVm.loaded()) {
          <!-- Header: totals + Save -->
          <div class="flex flex-col gap-3 border-b border-border p-5 sm:flex-row sm:items-center sm:justify-between">
            <div class="flex gap-6 text-sm">
              <span><span class="font-bold text-primary">{{ daysVm.dayCount() }}</span> {{ 'championships.days.totals.days' | translate }}</span>
              <span><span class="font-bold text-primary">{{ daysVm.raceCount() }}</span> {{ 'championships.days.totals.races' | translate }}</span>
              <span><span class="font-bold text-primary">{{ daysVm.entryCount() }}</span> {{ 'championships.days.totals.entries' | translate }}</span>
            </div>
            @if (daysVm.canManage()) {
              <div class="flex items-center gap-2">
                <button type="button" (click)="daysVm.addDay()"
                        class="inline-flex h-9 items-center rounded-lg border border-border px-3 text-sm font-medium text-text-secondary hover:text-primary">
                  + {{ 'championships.days.addDay' | translate }}
                </button>
                <button type="button" (click)="daysVm.save()" [disabled]="!daysVm.dirty() || daysVm.saving()"
                        class="inline-flex h-9 items-center rounded-lg bg-primary px-4 text-sm font-medium text-white hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-50">
                  {{ 'championships.days.save' | translate }}
                </button>
              </div>
            }
          </div>

          @if (daysVm.days().length === 0) {
            <p class="p-8 text-center text-sm text-text-secondary">{{ 'championships.days.empty' | translate }}</p>
          } @else {
            <div class="space-y-4 p-5">
              @for (day of daysVm.days(); track day.key) {
                <div class="rounded-xl border border-border">
                  <!-- Day header -->
                  <div class="flex flex-wrap items-center justify-between gap-3 border-b border-border bg-muted/30 p-4">
                    <div class="flex flex-wrap items-center gap-2">
                      <input type="text" [ngModel]="day.labelEn" (ngModelChange)="daysVm.updateDay(day.key, { labelEn: $event })"
                             [disabled]="!daysVm.canManage()" [attr.aria-label]="'championships.days.dayLabel' | translate"
                             class="h-9 rounded-lg border border-border bg-surface px-3 text-sm font-medium text-ink" />
                      <input type="date" [ngModel]="day.dayDate" (ngModelChange)="daysVm.updateDay(day.key, { dayDate: $event })"
                             [disabled]="!daysVm.canManage()" [attr.aria-label]="'championships.days.dayDate' | translate"
                             class="h-9 rounded-lg border border-border bg-surface px-3 text-sm text-ink" />
                    </div>
                    @if (daysVm.canManage()) {
                      <div class="flex items-center gap-2">
                        <button type="button" (click)="daysVm.addRace(day.key)"
                                class="inline-flex h-8 items-center rounded-md bg-primary/10 px-3 text-xs font-bold text-primary hover:bg-primary/20">
                          + {{ 'championships.days.addRace' | translate }}
                        </button>
                        <button type="button" (click)="daysVm.removeDay(day.key)" [attr.aria-label]="'championships.days.removeDay' | translate"
                                class="rounded-md p-2 text-danger hover:bg-danger/10">✕</button>
                      </div>
                    }
                  </div>

                  <!-- Races -->
                  @if (day.races.length === 0) {
                    <p class="p-4 text-center text-sm text-text-secondary">{{ 'championships.days.noRaces' | translate }}</p>
                  } @else {
                    <div class="divide-y divide-border">
                      @for (race of day.races; track race.key) {
                        <div class="space-y-3 p-4">
                          <div class="flex flex-wrap items-center justify-between gap-3">
                            <div class="flex flex-wrap items-center gap-2">
                              <select [ngModel]="race.distanceId" (ngModelChange)="daysVm.updateRace(day.key, race.key, { distanceId: $event })"
                                      [disabled]="!daysVm.canManage()" [attr.aria-label]="'championships.days.distance' | translate"
                                      class="h-8 rounded-md border border-border bg-surface px-2 text-sm text-ink">
                                @for (d of daysVm.distances(); track d.id) { <option [value]="d.id">{{ d.nameEn }}</option> }
                              </select>
                              <select [ngModel]="race.strokeId" (ngModelChange)="daysVm.updateRace(day.key, race.key, { strokeId: $event })"
                                      [disabled]="!daysVm.canManage()" [attr.aria-label]="'championships.days.stroke' | translate"
                                      class="h-8 rounded-md border border-border bg-surface px-2 text-sm text-ink">
                                @for (st of daysVm.strokes(); track st.id) { <option [value]="st.id">{{ st.nameEn }}</option> }
                              </select>
                              <input type="time" [ngModel]="race.scheduledTime" (ngModelChange)="daysVm.updateRace(day.key, race.key, { scheduledTime: $event || null })"
                                     [disabled]="!daysVm.canManage()" [attr.aria-label]="'championships.days.time' | translate"
                                     class="h-8 rounded-md border border-border bg-surface px-2 text-sm text-ink" />
                              <span class="rounded-full bg-primary/10 px-2 py-0.5 text-xs font-bold text-primary">
                                {{ race.swimmerIds.length }} {{ 'championships.days.swimmersCount' | translate }}
                              </span>
                              <span class="rounded-full bg-muted px-2 py-0.5 text-xs text-text-secondary">
                                {{ ('championships.days.status.' + daysVm.statusOf(day, race)) | translate }}
                              </span>
                            </div>
                            @if (daysVm.canManage()) {
                              <button type="button" (click)="daysVm.removeRace(day.key, race.key)" [attr.aria-label]="'championships.days.removeRace' | translate"
                                      class="rounded-md p-2 text-danger hover:bg-danger/10">✕</button>
                            }
                          </div>

                          <!-- Participant chips (enrolled swimmers only) -->
                          @if (daysVm.canManage()) {
                            <div class="rounded-lg border border-border bg-muted/20 p-3">
                              <p class="mb-2 text-xs font-bold uppercase text-text-secondary">{{ 'championships.days.chooseParticipants' | translate }}</p>
                              @if (daysVm.enrolledSwimmers().length === 0) {
                                <p class="text-sm text-text-secondary">{{ 'championships.days.enrollFirst' | translate }}</p>
                              } @else {
                                <div class="flex flex-wrap gap-2">
                                  @for (s of daysVm.enrolledSwimmers(); track s.id) {
                                    <button type="button" (click)="daysVm.toggleSwimmer(day.key, race.key, s.id)"
                                            [attr.aria-pressed]="daysVm.isAssigned(day.key, race.key, s.id)"
                                            class="inline-flex h-8 items-center rounded-md border px-3 text-xs font-medium"
                                            [class.bg-primary]="daysVm.isAssigned(day.key, race.key, s.id)"
                                            [class.text-white]="daysVm.isAssigned(day.key, race.key, s.id)"
                                            [class.border-primary]="daysVm.isAssigned(day.key, race.key, s.id)"
                                            [class.border-border]="!daysVm.isAssigned(day.key, race.key, s.id)"
                                            [class.text-text-secondary]="!daysVm.isAssigned(day.key, race.key, s.id)">
                                      {{ daysVm.swimmerName(s) }}
                                    </button>
                                  }
                                </div>
                              }
                            </div>
                          }
                        </div>
                      }
                    </div>
                  }
                </div>
              }
            </div>
          }
        }
      </section>
    }
```

- [ ] **Step 6: Export the VM + add the route provider**

In `championships/index.ts`, add:
```typescript
export { CompetitionDaysViewModel } from './presentation/pages/championship-detail/competition-days.viewmodel';
```
In `app.routes.ts`: import it and add to the `championships/:id` route providers array. Add to the imports at the top:
```typescript
import { ChampionshipDetailViewModel, CompetitionDaysViewModel } from '@features/championships';
```
(If `ChampionshipDetailViewModel` is already imported, just add `CompetitionDaysViewModel` to that import.) Then change the detail route's providers:
```typescript
        providers: [ChampionshipDetailViewModel, CompetitionDaysViewModel],
```

- [ ] **Step 7: Add the i18n block (English)**

In `en.json`, inside the `"championships"` object, add a `"days"` block after `"enrollment"`:
```json
    "days": {
      "dayName": "Day",
      "totals": { "days": "Days", "races": "Races", "entries": "Entries" },
      "empty": "Add competition days to structure this event.",
      "noRaces": "No races yet — add a race to this day.",
      "addDay": "Add Day",
      "addRace": "Race",
      "removeDay": "Remove day",
      "removeRace": "Remove race",
      "dayLabel": "Day label",
      "dayDate": "Day date",
      "distance": "Distance",
      "stroke": "Stroke",
      "time": "Time",
      "swimmersCount": "swimmers",
      "chooseParticipants": "Choose participants",
      "enrollFirst": "No enrolled swimmers yet — enroll swimmers in the Enrollment tab first.",
      "status": { "scheduled": "Scheduled", "awaitingResults": "Awaiting results" },
      "save": "Save",
      "saved": "Schedule saved",
      "saveFailed": "Couldn't save the schedule."
    }
```
(Add a comma after the preceding `"enrollment"` block's closing brace.)

- [ ] **Step 8: Add the i18n block (Arabic)**

In `ar.json`, inside `"championships"`, add after `"enrollment"`:
```json
    "days": {
      "dayName": "اليوم",
      "totals": { "days": "أيام", "races": "سباقات", "entries": "مشاركات" },
      "empty": "أضف أيام البطولة لهيكلة هذا الحدث.",
      "noRaces": "لا سباقات بعد — أضف سباقاً لهذا اليوم.",
      "addDay": "إضافة يوم",
      "addRace": "سباق",
      "removeDay": "حذف اليوم",
      "removeRace": "حذف السباق",
      "dayLabel": "اسم اليوم",
      "dayDate": "تاريخ اليوم",
      "distance": "المسافة",
      "stroke": "النوع",
      "time": "الوقت",
      "swimmersCount": "سباح",
      "chooseParticipants": "اختر المشاركين",
      "enrollFirst": "لا يوجد سباحون مسجلون بعد — سجّل سباحين من تبويب التسجيل أولاً.",
      "status": { "scheduled": "مجدول", "awaitingResults": "بانتظار النتائج" },
      "save": "حفظ",
      "saved": "تم حفظ الجدول",
      "saveFailed": "تعذّر حفظ الجدول."
    }
```

- [ ] **Step 9: Run the page spec + type-check + full frontend suite**

Run:
```bash
cd frontend
npx jest championship-detail.page
npx tsc --noEmit
npx jest features/championships features/reference
```
Expected: the page spec passes, no type errors, and all championships + reference specs pass. Also validate the JSON edits parse: `node -e "require('./src/app/core/i18n/en.json'); require('./src/app/core/i18n/ar.json'); console.log('json ok')"`.

- [ ] **Step 10: Commit** (only once authorized)

```bash
git add frontend/src/app
git commit -m "feat(championships): enable Competition Days tab (page + template + i18n)"
```

---

### Task 10: Seed scripts (`distances`, `competition-schedule`)

Idempotent SQL against Aiven `Swimming_Production`, matching the sibling seeds (fixed UUIDs, PascalCase quoted columns, `ON CONFLICT`). No tests — verified in Task 11.

**Files:**
- Create: `scripts/seed-distances-aiven.sql`
- Create: `scripts/seed-competition-schedule-aiven.sql`

- [ ] **Step 1: Create the distances seed**

`scripts/seed-distances-aiven.sql`:
```sql
-- seed-distances-aiven.sql
-- Seeds reference.distance with the canonical race distances (fixed ids). Idempotent.
-- NOTE: EF column names are PascalCase -> must be double-quoted in Postgres.

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

INSERT INTO reference.distance ("Id","Code","NameEn","NameAr","Meters") VALUES
  ('44444444-4444-4444-4444-444444000001', '50m',    '50m',    '٥٠ متر',     50),
  ('44444444-4444-4444-4444-444444000002', '100m',   '100m',   '١٠٠ متر',    100),
  ('44444444-4444-4444-4444-444444000003', '200m',   '200m',   '٢٠٠ متر',    200),
  ('44444444-4444-4444-4444-444444000004', '400m',   '400m',   '٤٠٠ متر',    400),
  ('44444444-4444-4444-4444-444444000005', '800m',   '800m',   '٨٠٠ متر',    800),
  ('44444444-4444-4444-4444-444444000006', '1000m',  '1000m',  '١٠٠٠ متر',   1000),
  ('44444444-4444-4444-4444-444444000007', '1500m',  '1500m',  '١٥٠٠ متر',   1500),
  ('44444444-4444-4444-4444-444444000008', '5000m',  '5000m',  '٥٠٠٠ متر',   5000),
  ('44444444-4444-4444-4444-444444000009', '7000m',  '7000m',  '٧٠٠٠ متر',   7000),
  ('44444444-4444-4444-4444-444444000010', '7500m',  '7500m',  '٧٥٠٠ متر',   7500),
  ('44444444-4444-4444-4444-444444000011', '10000m', '10000m', '١٠٠٠٠ متر', 10000)
ON CONFLICT ("Code") DO NOTHING;

COMMIT;

-- Verify:
--   SELECT "Code","Meters" FROM reference.distance ORDER BY "Meters";
```
> Ids run `444444000001`–`444444000011` (one per distance, ordered by meters). They only need to be stable/unique; `Code` is the conflict key.

- [ ] **Step 2: Create the schedule seed**

`scripts/seed-competition-schedule-aiven.sql` — builds a 2-day schedule for the National Junior event (`…3301`), resolving stroke/distance by `Code` and assigning that event's enrolled swimmers:
```sql
-- seed-competition-schedule-aiven.sql
-- Builds a demo Competition Days schedule for the National Junior event (…3301):
--   Day 1 — Heats (2023-11-15): 50m Freestyle, 100m Backstroke
--   Day 2 — Finals (2023-11-16): 100m Freestyle
-- Races resolve StrokeId/DistanceId by Code; assignments come from …3301's enrolled swimmers.
-- Idempotent. Inserts nothing if the event, strokes, distances, or enrollments are missing.
-- NOTE: EF column names are PascalCase -> must be double-quoted in Postgres.

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- 1) Days (fixed ids so races/assignments reference them deterministically).
INSERT INTO championships.competition_day ("Id","EventId","LabelEn","LabelAr","DayDate")
SELECT v."Id", v."EventId", v."LabelEn", v."LabelAr", v."DayDate"
FROM (VALUES
  ('55555555-5555-5555-5555-555555550001'::uuid, '33333333-3333-3333-3333-333333333301'::uuid, 'Day 1 — Heats',  'اليوم الأول — التصفيات', DATE '2023-11-15'),
  ('55555555-5555-5555-5555-555555550002'::uuid, '33333333-3333-3333-3333-333333333301'::uuid, 'Day 2 — Finals', 'اليوم الثاني — النهائيات', DATE '2023-11-16')
) AS v("Id","EventId","LabelEn","LabelAr","DayDate")
WHERE EXISTS (SELECT 1 FROM championships.competition_event e WHERE e."Id" = v."EventId")
ON CONFLICT ("Id") DO NOTHING;

-- 2) Races (fixed ids). StrokeId/DistanceId resolved by Code; skipped if a lookup is absent.
INSERT INTO championships.race_session ("Id","DayId","StrokeId","DistanceId","ScheduledTime")
SELECT v."Id", v."DayId",
       (SELECT "Id" FROM reference.stroke   WHERE "Code" = v.stroke_code),
       (SELECT "Id" FROM reference.distance WHERE "Code" = v.distance_code),
       v."ScheduledTime"
FROM (VALUES
  ('66666666-6666-6666-6666-666666660001'::uuid, '55555555-5555-5555-5555-555555550001'::uuid, 'freestyle',  '50m',  TIME '09:00'),
  ('66666666-6666-6666-6666-666666660002'::uuid, '55555555-5555-5555-5555-555555550001'::uuid, 'backstroke', '100m', TIME '09:30'),
  ('66666666-6666-6666-6666-666666660003'::uuid, '55555555-5555-5555-5555-555555550002'::uuid, 'freestyle',  '100m', TIME '10:00')
) AS v("Id","DayId",stroke_code,distance_code,"ScheduledTime")
WHERE EXISTS (SELECT 1 FROM championships.competition_day d WHERE d."Id" = v."DayId")
  AND (SELECT "Id" FROM reference.stroke   WHERE "Code" = v.stroke_code)   IS NOT NULL
  AND (SELECT "Id" FROM reference.distance WHERE "Code" = v.distance_code) IS NOT NULL
ON CONFLICT ("Id") DO NOTHING;

-- 3) Assignments: put the first two enrolled swimmers of …3301 into the two Day-1 races.
WITH enrolled AS (
  SELECT en."SwimmerId" AS swimmer_id, row_number() OVER (ORDER BY en."SwimmerId") AS rn
  FROM championships.championship_enrollment en
  WHERE en."EventId" = '33333333-3333-3333-3333-333333333301'
)
INSERT INTO championships.race_assignment ("Id","RaceSessionId","SwimmerId")
SELECT gen_random_uuid(), r.race_id, enrolled.swimmer_id
FROM (VALUES
  ('66666666-6666-6666-6666-666666660001'::uuid),   -- 50m Freestyle heats
  ('66666666-6666-6666-6666-666666660002'::uuid)    -- 100m Backstroke heats
) AS r(race_id)
JOIN enrolled ON enrolled.rn <= 2
WHERE EXISTS (SELECT 1 FROM championships.race_session s WHERE s."Id" = r.race_id)
ON CONFLICT ("RaceSessionId","SwimmerId") DO NOTHING;

COMMIT;

-- Verify:
--   SELECT d."LabelEn", count(rs.*) AS races
--   FROM championships.competition_day d
--   LEFT JOIN championships.race_session rs ON rs."DayId" = d."Id"
--   WHERE d."EventId" = '33333333-3333-3333-3333-333333333301'
--   GROUP BY d."LabelEn" ORDER BY d."LabelEn";
```
> Note: `reference.stroke` codes are assumed to be `freestyle` / `backstroke` (per the diagram's `stroke.code` note). In Task 11 you will confirm the actual codes on Aiven and adjust these `stroke_code` values if they differ.

- [ ] **Step 3: Commit** (only once authorized)

```bash
git add scripts/seed-distances-aiven.sql scripts/seed-competition-schedule-aiven.sql
git commit -m "chore(seed): distances + competition schedule seed scripts"
```

---

### Task 11: Apply migrations + seed on Aiven + verify (ops — needs the DB password)

Not TDD — an operational checklist. **Pause and ask the user for the Aiven `Swimming_Production` password** before running anything here; it is not stored. The user must also approve the classifier for writing to the live DB.

**Interfaces:** none — this validates the whole feature end-to-end against real data.

- [ ] **Step 1: Stop the running API** (release the build DLL lock) and confirm no `dotnet run` / API process is active.

- [ ] **Step 2: Confirm the Aiven connection string with the user**

Ask for the password and assemble the connection string (host `kheprx-service-kheprx.b.aivencloud.com:14647`, db **`Swimming_Production`**, user `avnadmin`, `SSL Mode=Require`). Do not echo the password into committed files.

- [ ] **Step 3: Apply the Identity migration (distance) to Aiven**

```bash
dotnet ef database update \
  --context IdentityDbContext \
  --project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --startup-project backend/Kheprx.BaseBackend.Api \
  --connection "Host=kheprx-service-kheprx.b.aivencloud.com;Port=14647;Database=Swimming_Production;Username=avnadmin;Password=<PASSWORD>;SSL Mode=Require;Trust Server Certificate=true"
```
Expected: `AddDistanceReference` applied. Verify: `dotnet ef migrations list --context IdentityDbContext --connection "…" ` shows 0 pending.

- [ ] **Step 4: Apply the Championships migration (schedule) to Aiven**

```bash
dotnet ef database update \
  --context ChampionshipsDbContext \
  --project backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure \
  --startup-project backend/Kheprx.BaseBackend.Api \
  --connection "Host=…;Database=Swimming_Production;Username=avnadmin;Password=<PASSWORD>;SSL Mode=Require;Trust Server Certificate=true"
```
Expected: `CreateCompetitionScheduleTables` applied; `migrations list` shows 0 pending.

- [ ] **Step 5: Confirm `reference.stroke` codes on Aiven**

Run (psql or any client): `SELECT "Code" FROM reference.stroke ORDER BY "Code";`
- If empty → strokes aren't seeded on Aiven; seed them first (locate/author a stroke seed) or the schedule seed's races will be skipped.
- If the codes differ from `freestyle` / `backstroke`, edit `seed-competition-schedule-aiven.sql`'s `stroke_code` values to match, then re-save.

- [ ] **Step 6: Run the distances seed**

Apply `scripts/seed-distances-aiven.sql` to Aiven `Swimming_Production`. Verify: `SELECT count(*) FROM reference.distance;` → 11.

- [ ] **Step 7: Run the schedule seed**

Apply `scripts/seed-competition-schedule-aiven.sql`. Verify with the script's trailing query — expect `Day 1 — Heats` = 2 races, `Day 2 — Finals` = 1 race; and:
```sql
SELECT count(*) FROM championships.race_assignment ra
JOIN championships.race_session rs ON rs."Id" = ra."RaceSessionId"
JOIN championships.competition_day d ON d."Id" = rs."DayId"
WHERE d."EventId" = '33333333-3333-3333-3333-333333333301';
```
→ expect 4 (2 swimmers × 2 Day-1 races), assuming `…3301` has ≥2 enrolled swimmers.

- [ ] **Step 8: End-to-end smoke in the running app**

Start the API + frontend against Aiven. Log in as head_coach/captain, open **Championships → National Junior Championship → Competition Days**. Confirm: totals show **2 days / 3 races**, the seeded races render with the right distance/stroke/time, participant chips show enrolled swimmers with the two Day-1 races pre-selected. Add a day + race, assign a swimmer, **Save**, reload the tab → the change persisted. Try assigning is impossible for non-enrolled swimmers (chips only show enrolled). Open **Winter Open (…3303)** → empty schedule, `Add competition days…` empty state.

- [ ] **Step 9: Update project memory**

Append a memory note (per the repo's memory workflow) recording that the `AddDistanceReference` + `CreateCompetitionScheduleTables` migrations were applied to Aiven `Swimming_Production`, and that `reference.distance`, `championships.competition_day/race_session/race_assignment` now exist there with the demo schedule on `…3301`.

- [ ] **Step 10: Commit** (only once authorized) — nothing new to commit here unless the stroke seed or `stroke_code` values were edited in Step 5:

```bash
git add scripts/seed-competition-schedule-aiven.sql
git commit -m "chore(seed): align schedule seed stroke codes with Aiven"
```

---

## Done criteria

- Backend: `reference.distance` + 3 schedule tables exist; `GET`/`PUT /api/championships/{id}/schedule` and `GET /api/reference/distances` work; enrolled-subset validation enforced; all xUnit suites green.
- Frontend: the **Competition Days** tab is enabled, lazy-loads, edits days/races/assignments, single-Saves with a toast, and all Jest suites green.
- Aiven `Swimming_Production` migrated + seeded; the tab shows the demo schedule on `…3301` and empty states elsewhere.
- No results/Finished/Results work leaked into scope.
