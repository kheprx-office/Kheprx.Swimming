# Championship Finished Races + Results Tabs — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the Finished Races tab (enter each assigned swimmer's finish time for a race whose start has passed) and the Results tab (view recorded times, ranked, with a personal-best badge) to the championship detail page, backed by a new `championships.race_result` table.

**Architecture:** One new table + repository + two service methods + two controller endpoints on the existing Championships module (no new module). The frontend adds one route-provided `RaceResultsViewModel` that serves both tabs from a single load (schedule read *with ids* + results + roster + reference lookups), plus a per-race save. Both tabs are client-composed: Finished = schedule races whose start passed and have no results; Results = races that have results.

**Tech Stack:** .NET (C#, EF Core, xUnit, Moq), Angular (standalone components, signals, Jest), PostgreSQL (Aiven `Swimming_Production`).

**Spec:** `docs/superpowers/specs/2026-09-24-championship-finished-races-design.md`

## Global Constraints

- **No commits until the user explicitly authorizes.** This branch (`feat/championships`) already carries uncommitted Competition Days work; do not stage or commit anything. Each task ends at a **green checkpoint** (run the named tests, confirm they pass) instead of a `git commit`.
- **EF mapping convention:** table + schema are snake_case via `ToTable("name","championships")`; columns keep PascalCase property names (SQL quotes them `"Id"`, `"TimeMs"`, …). Cross-module ids are **loose Guids** — no EF navigation, no DB-level FK. The only DB invariant is the unique index `(RaceSessionId, SwimmerId)`.
- **Points is always `0`** in stored rows this pass (no scoring table). **`RecordedBy` comes from the authenticated user** (`CurrentUserId()`), never the request body.
- **`IsPersonalBest` is computed server-side:** true iff the new `TimeMs` is strictly less than the swimmer's best prior `TimeMs` for the **same distance + stroke** across all race sessions (excluding the session being saved), or the swimmer has no prior time. A tie is **not** a PB.
- **Finished rule (client-derived):** a race is in Finished iff `raceStatus(day.dayDate, race.scheduledTime) === 'awaitingResults'` (existing helper — venue local wall-clock) **and** it has zero results. A race with ≥1 result is in Results only.
- **Dev-server DLL lock:** stop any running backend before `dotnet ef` or `dotnet test` (running API locks the DLLs).
- **Frontend tests are Jest**, not Karma. Run a single file with `npx jest <path>` from `frontend/`.
- **Backend `dotnet` commands** run from `backend/`.

## Review Focus

- **Blank / malformed time input** (e.g. `""`, `"abc"`, `"1:99"`): `parseTimeToMs` must return `null` and `saveResults` must skip it — never send `0`/`NaN`. (Test in Task 5 + Task 10.)
- **Finished race with zero assigned swimmers** (the seed's 100m Free Finals has none): the card renders an empty state and `saveResults` sends nothing / no-ops. (Test in Task 10.)
- **Partial results** (only some assigned swimmers timed): the race still leaves Finished and appears in Results with only the entered swimmers. (Test in Task 10.)
- **Future-scheduled race** must never appear in Finished (raceStatus boundary). (Test in Task 10.)
- **Career-wide PB spans events:** `GetBestTimesAsync` must include results from *other events'* sessions with the same distance+stroke. (Test in Task 2.)

---

### Task 1: `race_result` table — entity, EF config, DbSet, migration

**Files:**
- Create: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Domain/Entities/RaceResult.cs`
- Create: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure/Configurations/RaceResultConfiguration.cs`
- Modify: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure/Data/ChampionshipsDbContext.cs`
- Generated: a new migration `CreateRaceResultTable` + the updated `ChampionshipsDbContextModelSnapshot.cs`

**Interfaces:**
- Produces: `RaceResult` entity (`Id, RaceSessionId, SwimmerId, TimeMs, Points, IsPersonalBest, RecordedBy`; private EF ctor + public ctor `RaceResult(Guid raceSessionId, Guid swimmerId, int timeMs, int points, bool isPersonalBest, Guid recordedBy)`); `ChampionshipsDbContext.RaceResults` DbSet.

- [ ] **Step 1: Write `RaceResult.cs`** (mirrors `RaceAssignment.cs`)

```csharp
namespace Kheprx.BaseBackend.Championships.Domain.Entities;

public sealed class RaceResult
{
    public Guid Id { get; private set; }
    public Guid RaceSessionId { get; private set; }
    public Guid SwimmerId { get; private set; }
    public int TimeMs { get; private set; }
    public int Points { get; private set; }
    public bool IsPersonalBest { get; private set; }
    public Guid RecordedBy { get; private set; }

    private RaceResult() { } // EF Core

    public RaceResult(Guid raceSessionId, Guid swimmerId, int timeMs, int points, bool isPersonalBest, Guid recordedBy)
    {
        Id = Guid.NewGuid();
        RaceSessionId = raceSessionId;
        SwimmerId = swimmerId;
        TimeMs = timeMs;
        Points = points;
        IsPersonalBest = isPersonalBest;
        RecordedBy = recordedBy;
    }
}
```

- [ ] **Step 2: Write `RaceResultConfiguration.cs`** (mirrors `RaceAssignmentConfiguration.cs`)

```csharp
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Configurations;

internal sealed class RaceResultConfiguration : IEntityTypeConfiguration<RaceResult>
{
    public void Configure(EntityTypeBuilder<RaceResult> builder)
    {
        builder.ToTable("race_result", "championships");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.RaceSessionId).IsRequired(); // loose Guid -> race_session
        builder.Property(r => r.SwimmerId).IsRequired();     // loose Guid -> identity.swimmer_profile
        builder.Property(r => r.TimeMs).IsRequired();
        builder.Property(r => r.Points).IsRequired();
        builder.Property(r => r.IsPersonalBest).IsRequired();
        builder.Property(r => r.RecordedBy).IsRequired();     // loose Guid -> identity.app_user
        builder.HasIndex(r => new { r.RaceSessionId, r.SwimmerId }).IsUnique();
    }
}
```

- [ ] **Step 3: Add the DbSet** to `ChampionshipsDbContext.cs` after the `RaceAssignments` line:

```csharp
    public DbSet<RaceResult> RaceResults => Set<RaceResult>();
```

- [ ] **Step 4: Generate the migration.** Ensure the backend API is stopped, then from `backend/`:

```bash
dotnet ef migrations add CreateRaceResultTable \
  --project src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure \
  --startup-project Kheprx.BaseBackend.Api \
  --context ChampionshipsDbContext
```

- [ ] **Step 5: Verify the generated `Up()`** contains the table + unique index (open the new `*_CreateRaceResultTable.cs`). Expected:

```csharp
migrationBuilder.CreateTable(
    name: "race_result",
    schema: "championships",
    columns: table => new
    {
        Id = table.Column<Guid>(type: "uuid", nullable: false),
        RaceSessionId = table.Column<Guid>(type: "uuid", nullable: false),
        SwimmerId = table.Column<Guid>(type: "uuid", nullable: false),
        TimeMs = table.Column<int>(type: "integer", nullable: false),
        Points = table.Column<int>(type: "integer", nullable: false),
        IsPersonalBest = table.Column<bool>(type: "boolean", nullable: false),
        RecordedBy = table.Column<Guid>(type: "uuid", nullable: false)
    },
    constraints: table => { table.PrimaryKey("PK_race_result", x => x.Id); });

migrationBuilder.CreateIndex(
    name: "IX_race_result_RaceSessionId_SwimmerId",
    schema: "championships",
    table: "race_result",
    columns: new[] { "RaceSessionId", "SwimmerId" },
    unique: true);
```

- [ ] **Step 6: Checkpoint** — build succeeds and the migration is registered. Do NOT commit.

```bash
dotnet build backend/Kheprx.BaseBackend.sln
dotnet ef migrations list --project backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure --startup-project backend/Kheprx.BaseBackend.Api --context ChampionshipsDbContext
```
Expected: build OK; `CreateRaceResultTable` appears in the list.

---

### Task 2: `RaceResultRepository` — read, atomic per-session replace, best-times

**Files:**
- Create: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Domain/Repositories/RaceResultData.cs`
- Create: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Domain/Repositories/IRaceResultRepository.cs`
- Create: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure/Repositories/RaceResultRepository.cs`
- Modify: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure/Extensions/ChampionshipsModuleExtensions.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Repositories/RaceResultRepositoryTests.cs`

**Interfaces:**
- Consumes: `ChampionshipsDbContext.RaceResults`, `.RaceSessions`, `.CompetitionDays` (Task 1).
- Produces:
  - `RaceResultRow(Guid Id, Guid RaceSessionId, Guid SwimmerId, int TimeMs, int Points, bool IsPersonalBest, Guid RecordedBy)`
  - `RaceResultInput(Guid SwimmerId, int TimeMs, int Points, bool IsPersonalBest, Guid RecordedBy)`
  - `IRaceResultRepository.GetByEventAsync(Guid, CancellationToken) : Task<IReadOnlyList<RaceResultRow>>`
  - `IRaceResultRepository.ReplaceForSessionAsync(Guid raceSessionId, IReadOnlyList<RaceResultInput>, CancellationToken) : Task`
  - `IRaceResultRepository.GetBestTimesAsync(Guid distanceId, Guid strokeId, Guid excludeSessionId, IReadOnlyList<Guid> swimmerIds, CancellationToken) : Task<IReadOnlyDictionary<Guid,int>>`

- [ ] **Step 1: Write the domain records** `RaceResultData.cs` (mirrors `CompetitionScheduleData.cs`)

```csharp
namespace Kheprx.BaseBackend.Championships.Domain.Repositories;

// Read shape returned by GetByEventAsync (carries persisted ids).
public sealed record RaceResultRow(Guid Id, Guid RaceSessionId, Guid SwimmerId, int TimeMs, int Points, bool IsPersonalBest, Guid RecordedBy);

// Write shape accepted by ReplaceForSessionAsync (ids generated on insert).
public sealed record RaceResultInput(Guid SwimmerId, int TimeMs, int Points, bool IsPersonalBest, Guid RecordedBy);
```

- [ ] **Step 2: Write the port** `IRaceResultRepository.cs`

```csharp
namespace Kheprx.BaseBackend.Championships.Domain.Repositories;

public interface IRaceResultRepository
{
    Task<IReadOnlyList<RaceResultRow>> GetByEventAsync(Guid eventId, CancellationToken ct = default);
    Task ReplaceForSessionAsync(Guid raceSessionId, IReadOnlyList<RaceResultInput> rows, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, int>> GetBestTimesAsync(Guid distanceId, Guid strokeId, Guid excludeSessionId, IReadOnlyList<Guid> swimmerIds, CancellationToken ct = default);
}
```

- [ ] **Step 3: Write the failing tests** `RaceResultRepositoryTests.cs` (mirrors `CompetitionScheduleRepositoryTests.cs`)

```csharp
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Kheprx.BaseBackend.Championships.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Repositories;

public class RaceResultRepositoryTests
{
    private static ChampionshipsDbContext NewDb()
        => new(new DbContextOptionsBuilder<ChampionshipsDbContext>()
            .UseInMemoryDatabase($"results-{Guid.NewGuid()}").Options);

    // Seeds a day+session under an event and returns the session id.
    private static async Task<Guid> SeedSessionAsync(ChampionshipsDbContext db, Guid eventId, Guid strokeId, Guid distanceId)
    {
        var day = new CompetitionDay(eventId, "Day 1", null, new DateOnly(2023, 11, 15));
        db.CompetitionDays.Add(day);
        var session = new RaceSession(day.Id, strokeId, distanceId, new TimeOnly(9, 0));
        db.RaceSessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    [Fact]
    public async Task ReplaceForSessionAsync_then_GetByEventAsync_round_trips()
    {
        await using var db = NewDb();
        var eventId = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        var sessionId = await SeedSessionAsync(db, eventId, Guid.NewGuid(), Guid.NewGuid());
        var repo = new RaceResultRepository(db);

        await repo.ReplaceForSessionAsync(sessionId, new[] { new RaceResultInput(swimmer, 24560, 0, true, Guid.NewGuid()) });

        var rows = await repo.GetByEventAsync(eventId);
        Assert.Single(rows);
        Assert.Equal(sessionId, rows[0].RaceSessionId);
        Assert.Equal(24560, rows[0].TimeMs);
        Assert.True(rows[0].IsPersonalBest);
    }

    [Fact]
    public async Task ReplaceForSessionAsync_replaces_the_sessions_prior_rows()
    {
        await using var db = NewDb();
        var eventId = Guid.NewGuid();
        var sessionId = await SeedSessionAsync(db, eventId, Guid.NewGuid(), Guid.NewGuid());
        var repo = new RaceResultRepository(db);
        await repo.ReplaceForSessionAsync(sessionId, new[] { new RaceResultInput(Guid.NewGuid(), 100, 0, false, Guid.NewGuid()) });

        var keep = Guid.NewGuid();
        await repo.ReplaceForSessionAsync(sessionId, new[] { new RaceResultInput(keep, 200, 0, true, Guid.NewGuid()) });

        var rows = await repo.GetByEventAsync(eventId);
        Assert.Single(rows);
        Assert.Equal(keep, rows[0].SwimmerId);
        Assert.Equal(200, rows[0].TimeMs);
    }

    [Fact]
    public async Task ReplaceForSessionAsync_with_empty_list_clears_the_session()
    {
        await using var db = NewDb();
        var eventId = Guid.NewGuid();
        var sessionId = await SeedSessionAsync(db, eventId, Guid.NewGuid(), Guid.NewGuid());
        var repo = new RaceResultRepository(db);
        await repo.ReplaceForSessionAsync(sessionId, new[] { new RaceResultInput(Guid.NewGuid(), 100, 0, false, Guid.NewGuid()) });

        await repo.ReplaceForSessionAsync(sessionId, Array.Empty<RaceResultInput>());

        Assert.Empty(await repo.GetByEventAsync(eventId));
    }

    [Fact]
    public async Task GetByEventAsync_returns_only_the_target_event()
    {
        await using var db = NewDb();
        var eventA = Guid.NewGuid();
        var eventB = Guid.NewGuid();
        var sessionA = await SeedSessionAsync(db, eventA, Guid.NewGuid(), Guid.NewGuid());
        var sessionB = await SeedSessionAsync(db, eventB, Guid.NewGuid(), Guid.NewGuid());
        var repo = new RaceResultRepository(db);
        await repo.ReplaceForSessionAsync(sessionA, new[] { new RaceResultInput(Guid.NewGuid(), 100, 0, false, Guid.NewGuid()) });
        await repo.ReplaceForSessionAsync(sessionB, new[] { new RaceResultInput(Guid.NewGuid(), 200, 0, false, Guid.NewGuid()) });

        var rows = await repo.GetByEventAsync(eventA);
        Assert.Single(rows);
        Assert.Equal(sessionA, rows[0].RaceSessionId);
    }

    [Fact]
    public async Task GetBestTimesAsync_returns_min_prior_time_for_same_distance_stroke_across_events_excluding_current_session()
    {
        await using var db = NewDb();
        var stroke = Guid.NewGuid();
        var distance = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        // Two sessions in DIFFERENT events, same distance+stroke -> PB is career-wide.
        var priorSession = await SeedSessionAsync(db, Guid.NewGuid(), stroke, distance);
        var currentSession = await SeedSessionAsync(db, Guid.NewGuid(), stroke, distance);
        var repo = new RaceResultRepository(db);
        await repo.ReplaceForSessionAsync(priorSession, new[] { new RaceResultInput(swimmer, 26000, 0, true, Guid.NewGuid()) });
        await repo.ReplaceForSessionAsync(currentSession, new[] { new RaceResultInput(swimmer, 25000, 0, true, Guid.NewGuid()) });

        var best = await repo.GetBestTimesAsync(distance, stroke, currentSession, new[] { swimmer });

        Assert.Equal(26000, best[swimmer]); // the prior session's time, NOT the current one
    }

    [Fact]
    public async Task GetBestTimesAsync_omits_swimmers_with_no_prior_time()
    {
        await using var db = NewDb();
        var repo = new RaceResultRepository(db);
        var best = await repo.GetBestTimesAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new[] { Guid.NewGuid() });
        Assert.Empty(best);
    }
}
```

- [ ] **Step 4: Run the tests — verify they fail** (no `RaceResultRepository` yet)

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests --filter RaceResultRepositoryTests`
Expected: FAIL (compile error / type not found).

- [ ] **Step 5: Write `RaceResultRepository.cs`** (mirrors `CompetitionScheduleRepository.cs`)

```csharp
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Repositories;

internal sealed class RaceResultRepository : IRaceResultRepository
{
    private readonly ChampionshipsDbContext _db;
    public RaceResultRepository(ChampionshipsDbContext db) => _db = db;

    public async Task<IReadOnlyList<RaceResultRow>> GetByEventAsync(Guid eventId, CancellationToken ct = default)
    {
        var dayIds = await _db.CompetitionDays.AsNoTracking()
            .Where(d => d.EventId == eventId).Select(d => d.Id).ToListAsync(ct);
        var sessionIds = await _db.RaceSessions.AsNoTracking()
            .Where(s => dayIds.Contains(s.DayId)).Select(s => s.Id).ToListAsync(ct);
        var rows = await _db.RaceResults.AsNoTracking()
            .Where(r => sessionIds.Contains(r.RaceSessionId)).ToListAsync(ct);

        return rows.Select(r => new RaceResultRow(
            r.Id, r.RaceSessionId, r.SwimmerId, r.TimeMs, r.Points, r.IsPersonalBest, r.RecordedBy)).ToList();
    }

    public async Task ReplaceForSessionAsync(Guid raceSessionId, IReadOnlyList<RaceResultInput> rows, CancellationToken ct = default)
    {
        var old = await _db.RaceResults.Where(r => r.RaceSessionId == raceSessionId).ToListAsync(ct);
        _db.RaceResults.RemoveRange(old);
        foreach (var r in rows)
            _db.RaceResults.Add(new RaceResult(raceSessionId, r.SwimmerId, r.TimeMs, r.Points, r.IsPersonalBest, r.RecordedBy));
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetBestTimesAsync(Guid distanceId, Guid strokeId, Guid excludeSessionId, IReadOnlyList<Guid> swimmerIds, CancellationToken ct = default)
    {
        if (swimmerIds.Count == 0) return new Dictionary<Guid, int>();

        var sessionIds = await _db.RaceSessions.AsNoTracking()
            .Where(s => s.DistanceId == distanceId && s.StrokeId == strokeId && s.Id != excludeSessionId)
            .Select(s => s.Id).ToListAsync(ct);
        if (sessionIds.Count == 0) return new Dictionary<Guid, int>();

        var rows = await _db.RaceResults.AsNoTracking()
            .Where(r => sessionIds.Contains(r.RaceSessionId) && swimmerIds.Contains(r.SwimmerId))
            .ToListAsync(ct);

        return rows.GroupBy(r => r.SwimmerId).ToDictionary(g => g.Key, g => g.Min(r => r.TimeMs));
    }
}
```

- [ ] **Step 6: Register the repository** in `ChampionshipsModuleExtensions.cs`, after the `ICompetitionScheduleRepository` line:

```csharp
        services.AddScoped<IRaceResultRepository, RaceResultRepository>();
```

- [ ] **Step 7: Run the tests — verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests --filter RaceResultRepositoryTests`
Expected: PASS (6 tests).

- [ ] **Step 8: Checkpoint** — `dotnet build backend/Kheprx.BaseBackend.sln` OK. Do NOT commit.

---

### Task 3: Service — `GetResultsAsync` + `SetRaceResultsAsync` (with PB), DTOs, messages

**Files:**
- Create: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Application/DTOs/RaceResultDtos.cs`
- Modify: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Application/Services/Interfaces/IChampionshipService.cs`
- Modify: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Application/Services/ChampionshipService.cs`
- Modify: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Application/Resources/ChampionshipMessages.cs`
- Modify (fix ctor): `backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Services/ChampionshipServiceTests.cs`
- Modify (fix ctor): `backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Services/ChampionshipScheduleServiceTests.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Services/RaceResultServiceTests.cs`

**Interfaces:**
- Consumes: `IRaceResultRepository` (Task 2), `ICompetitionScheduleRepository.GetAsync` (existing), `ICompetitionEventRepository.GetByIdAsync` (existing).
- Produces:
  - DTOs: `RaceResultDto(Guid RaceSessionId, Guid SwimmerId, int TimeMs, int Points, bool IsPersonalBest)`, `ResultsDto(IReadOnlyList<RaceResultDto> Results)`, `SetRaceResultsEntry(Guid SwimmerId, int TimeMs)`, `SetRaceResultsRequest(IReadOnlyList<SetRaceResultsEntry> Entries)`, `enum SetRaceResultsOutcome { Ok, NotFound, Invalid }`, `SetRaceResultsResult(SetRaceResultsOutcome Outcome, ResultsDto? Saved, string? Error)`.
  - Service: `GetResultsAsync(Guid, CancellationToken) : Task<ResultsDto?>`; `SetRaceResultsAsync(Guid eventId, Guid raceSessionId, IReadOnlyList<SetRaceResultsEntry> entries, Guid recordedBy, CancellationToken) : Task<SetRaceResultsResult>`.

- [ ] **Step 1: Write the DTOs** `RaceResultDtos.cs`

```csharp
namespace Kheprx.BaseBackend.Championships.Application.DTOs;

// ---- Read (GET /results) ----
public sealed record RaceResultDto(Guid RaceSessionId, Guid SwimmerId, int TimeMs, int Points, bool IsPersonalBest);
public sealed record ResultsDto(IReadOnlyList<RaceResultDto> Results);

// ---- Write (PUT /races/{id}/results) ----
public sealed record SetRaceResultsEntry(Guid SwimmerId, int TimeMs);
public sealed record SetRaceResultsRequest(IReadOnlyList<SetRaceResultsEntry> Entries);

public enum SetRaceResultsOutcome { Ok, NotFound, Invalid }

/// <summary>Result of a per-race results replace: Ok carries the event's re-read results; Invalid carries a reason code.</summary>
public sealed record SetRaceResultsResult(SetRaceResultsOutcome Outcome, ResultsDto? Saved, string? Error);
```

- [ ] **Step 2: Extend the service interface** — add to `IChampionshipService.cs`, before the closing brace:

```csharp
    Task<ResultsDto?> GetResultsAsync(Guid eventId, CancellationToken ct = default);
    Task<SetRaceResultsResult> SetRaceResultsAsync(Guid eventId, Guid raceSessionId, IReadOnlyList<SetRaceResultsEntry> entries, Guid recordedBy, CancellationToken ct = default);
```

- [ ] **Step 3: Add messages** to `ChampionshipMessages.cs`, before the final closing brace:

```csharp
    public static class ResultsSuccess
    {
        public static string Retrieved(string lang) => lang switch { "ar" => "النتائج", _ => "Results" };
        public static string Saved(string lang) => lang switch { "ar" => "تم حفظ النتائج", _ => "Results saved" };
    }

    public static class ResultsErrors
    {
        public static string Invalid(string lang) => lang switch { "ar" => "بيانات النتائج غير صالحة (تأكد من الأوقات وأن كل سباح مُسند إلى هذا السباق)", _ => "Invalid results (check the times and that every swimmer is assigned to this race)" };
    }
```

- [ ] **Step 4: Write the failing service tests** `RaceResultServiceTests.cs` (mirrors `ChampionshipScheduleServiceTests.cs`; note the 4-arg constructor)

```csharp
using Kheprx.BaseBackend.Championships.Application.DTOs;
using Kheprx.BaseBackend.Championships.Application.Services;
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Services;

public class RaceResultServiceTests
{
    private static CompetitionEvent NewEvent()
        => new("National Junior", null, new DateOnly(2023, 11, 15), new DateOnly(2023, 11, 16),
               "Cairo", null, Guid.NewGuid(), Guid.NewGuid());

    private static (ChampionshipService svc, Mock<ICompetitionEventRepository> events,
                    Mock<IChampionshipEnrollmentRepository> enr, Mock<ICompetitionScheduleRepository> sched,
                    Mock<IRaceResultRepository> results) Build()
    {
        var events = new Mock<ICompetitionEventRepository>();
        var enr = new Mock<IChampionshipEnrollmentRepository>();
        var sched = new Mock<ICompetitionScheduleRepository>();
        var results = new Mock<IRaceResultRepository>();
        return (new ChampionshipService(events.Object, enr.Object, sched.Object, results.Object), events, enr, sched, results);
    }

    // One event with one day holding one race (raceId) that has the given assigned swimmers.
    private static void SetupSchedule(Mock<ICompetitionScheduleRepository> sched, Guid eventId, Guid raceId,
        Guid strokeId, Guid distanceId, params Guid[] assigned)
        => sched.Setup(r => r.GetAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new ScheduleDayRow(Guid.NewGuid(), "Day 1", null, new DateOnly(2023, 11, 15), new[]
            {
                new ScheduleRaceRow(raceId, strokeId, distanceId, new TimeOnly(9, 0), assigned),
            }),
        });

    [Fact]
    public async Task GetResultsAsync_returns_null_when_event_missing()
    {
        var (svc, events, _, _, _) = Build();
        events.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((CompetitionEvent?)null);
        Assert.Null(await svc.GetResultsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetResultsAsync_maps_rows_to_dto()
    {
        var (svc, events, _, _, results) = Build();
        var eventId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        results.Setup(r => r.GetByEventAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new RaceResultRow(Guid.NewGuid(), sessionId, swimmer, 24560, 0, true, Guid.NewGuid()),
        });

        var dto = await svc.GetResultsAsync(eventId);

        Assert.NotNull(dto);
        Assert.Single(dto!.Results);
        Assert.Equal(24560, dto.Results[0].TimeMs);
        Assert.True(dto.Results[0].IsPersonalBest);
    }

    [Fact]
    public async Task SetRaceResultsAsync_returns_NotFound_when_event_missing()
    {
        var (svc, events, _, _, _) = Build();
        events.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((CompetitionEvent?)null);
        var result = await svc.SetRaceResultsAsync(Guid.NewGuid(), Guid.NewGuid(), Array.Empty<SetRaceResultsEntry>(), Guid.NewGuid());
        Assert.Equal(SetRaceResultsOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task SetRaceResultsAsync_returns_NotFound_when_session_not_in_event()
    {
        var (svc, events, _, sched, _) = Build();
        var eventId = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        SetupSchedule(sched, eventId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()); // some other race
        var result = await svc.SetRaceResultsAsync(eventId, Guid.NewGuid(), Array.Empty<SetRaceResultsEntry>(), Guid.NewGuid());
        Assert.Equal(SetRaceResultsOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task SetRaceResultsAsync_rejects_a_non_assigned_swimmer()
    {
        var (svc, events, _, sched, results) = Build();
        var eventId = Guid.NewGuid();
        var raceId = Guid.NewGuid();
        var assigned = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        SetupSchedule(sched, eventId, raceId, Guid.NewGuid(), Guid.NewGuid(), assigned);

        var result = await svc.SetRaceResultsAsync(eventId, raceId, new[] { new SetRaceResultsEntry(stranger, 25000) }, Guid.NewGuid());

        Assert.Equal(SetRaceResultsOutcome.Invalid, result.Outcome);
        results.Verify(r => r.ReplaceForSessionAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<RaceResultInput>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetRaceResultsAsync_rejects_a_non_positive_time()
    {
        var (svc, events, _, sched, _) = Build();
        var eventId = Guid.NewGuid();
        var raceId = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        SetupSchedule(sched, eventId, raceId, Guid.NewGuid(), Guid.NewGuid(), swimmer);

        var result = await svc.SetRaceResultsAsync(eventId, raceId, new[] { new SetRaceResultsEntry(swimmer, 0) }, Guid.NewGuid());

        Assert.Equal(SetRaceResultsOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public async Task SetRaceResultsAsync_rejects_a_duplicate_swimmer_in_the_batch()
    {
        var (svc, events, _, sched, _) = Build();
        var eventId = Guid.NewGuid();
        var raceId = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        SetupSchedule(sched, eventId, raceId, Guid.NewGuid(), Guid.NewGuid(), swimmer);

        var result = await svc.SetRaceResultsAsync(eventId, raceId,
            new[] { new SetRaceResultsEntry(swimmer, 25000), new SetRaceResultsEntry(swimmer, 26000) }, Guid.NewGuid());

        Assert.Equal(SetRaceResultsOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public async Task SetRaceResultsAsync_marks_personal_best_only_when_it_beats_the_prior_best()
    {
        var (svc, events, _, sched, results) = Build();
        var eventId = Guid.NewGuid();
        var raceId = Guid.NewGuid();
        var stroke = Guid.NewGuid();
        var distance = Guid.NewGuid();
        var faster = Guid.NewGuid();   // has a slower prior time -> new time is a PB
        var slower = Guid.NewGuid();   // has a faster prior time -> new time is NOT a PB
        var fresh = Guid.NewGuid();    // no prior time -> PB
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        SetupSchedule(sched, eventId, raceId, stroke, distance, faster, slower, fresh);
        results.Setup(r => r.GetByEventAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<RaceResultRow>());
        results.Setup(r => r.GetBestTimesAsync(distance, stroke, raceId, It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Dictionary<Guid, int> { [faster] = 30000, [slower] = 20000 });

        IReadOnlyList<RaceResultInput>? captured = null;
        results.Setup(r => r.ReplaceForSessionAsync(raceId, It.IsAny<IReadOnlyList<RaceResultInput>>(), It.IsAny<CancellationToken>()))
               .Callback<Guid, IReadOnlyList<RaceResultInput>, CancellationToken>((_, rows, _) => captured = rows)
               .Returns(Task.CompletedTask);

        var result = await svc.SetRaceResultsAsync(eventId, raceId, new[]
        {
            new SetRaceResultsEntry(faster, 25000),
            new SetRaceResultsEntry(slower, 25000),
            new SetRaceResultsEntry(fresh, 25000),
        }, Guid.NewGuid());

        Assert.Equal(SetRaceResultsOutcome.Ok, result.Outcome);
        Assert.NotNull(captured);
        Assert.True(captured!.Single(r => r.SwimmerId == faster).IsPersonalBest);
        Assert.False(captured.Single(r => r.SwimmerId == slower).IsPersonalBest);
        Assert.True(captured.Single(r => r.SwimmerId == fresh).IsPersonalBest);
        Assert.All(captured, r => Assert.Equal(0, r.Points));   // points always 0 this pass
    }

    [Fact]
    public async Task SetRaceResultsAsync_ties_are_not_personal_bests()
    {
        var (svc, events, _, sched, results) = Build();
        var eventId = Guid.NewGuid();
        var raceId = Guid.NewGuid();
        var stroke = Guid.NewGuid();
        var distance = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        SetupSchedule(sched, eventId, raceId, stroke, distance, swimmer);
        results.Setup(r => r.GetByEventAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<RaceResultRow>());
        results.Setup(r => r.GetBestTimesAsync(distance, stroke, raceId, It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Dictionary<Guid, int> { [swimmer] = 25000 });
        IReadOnlyList<RaceResultInput>? captured = null;
        results.Setup(r => r.ReplaceForSessionAsync(raceId, It.IsAny<IReadOnlyList<RaceResultInput>>(), It.IsAny<CancellationToken>()))
               .Callback<Guid, IReadOnlyList<RaceResultInput>, CancellationToken>((_, rows, _) => captured = rows)
               .Returns(Task.CompletedTask);

        await svc.SetRaceResultsAsync(eventId, raceId, new[] { new SetRaceResultsEntry(swimmer, 25000) }, Guid.NewGuid());

        Assert.False(captured!.Single().IsPersonalBest); // equal time is not a PB
    }

    [Fact]
    public async Task SetRaceResultsAsync_replaces_and_returns_the_events_results()
    {
        var (svc, events, _, sched, results) = Build();
        var eventId = Guid.NewGuid();
        var raceId = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        var recorder = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        SetupSchedule(sched, eventId, raceId, Guid.NewGuid(), Guid.NewGuid(), swimmer);
        results.Setup(r => r.GetBestTimesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), raceId, It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Dictionary<Guid, int>());
        results.Setup(r => r.GetByEventAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new RaceResultRow(Guid.NewGuid(), raceId, swimmer, 25000, 0, true, recorder),
        });

        var result = await svc.SetRaceResultsAsync(eventId, raceId, new[] { new SetRaceResultsEntry(swimmer, 25000) }, recorder);

        Assert.Equal(SetRaceResultsOutcome.Ok, result.Outcome);
        Assert.Single(result.Saved!.Results);
        results.Verify(r => r.ReplaceForSessionAsync(raceId, It.IsAny<IReadOnlyList<RaceResultInput>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 5: Fix the existing service-test constructors** (the ctor gains a 4th parameter — Task 4 changes the DI too):
  - In `ChampionshipScheduleServiceTests.cs`, update `Build()` to add `var results = new Mock<IRaceResultRepository>();`, change the return tuple/type to include it, and construct `new ChampionshipService(events.Object, enr.Object, sched.Object, results.Object)`.
  - In `ChampionshipServiceTests.cs`, every `new ChampionshipService(...)` currently ends with `Mock.Of<ICompetitionScheduleRepository>()`. Append `, Mock.Of<IRaceResultRepository>()` to each such call. (Search the file for `new ChampionshipService(` and fix all occurrences.)

- [ ] **Step 6: Run the tests — verify they fail** (service methods not implemented)

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests --filter RaceResultServiceTests`
Expected: FAIL (compile error — `SetRaceResultsAsync` not defined / ctor arity).

- [ ] **Step 7: Implement the service.** In `ChampionshipService.cs`: add the field + constructor param, then the two methods + the mapper.

Change the field block + constructor to:

```csharp
    private readonly ICompetitionEventRepository _events;
    private readonly IChampionshipEnrollmentRepository _enrollments;
    private readonly ICompetitionScheduleRepository _schedule;
    private readonly IRaceResultRepository _results;

    public ChampionshipService(ICompetitionEventRepository events, IChampionshipEnrollmentRepository enrollments, ICompetitionScheduleRepository schedule, IRaceResultRepository results)
    {
        _events = events;
        _enrollments = enrollments;
        _schedule = schedule;
        _results = results;
    }
```

Add these methods (before the private `ToDto` helpers):

```csharp
    public async Task<ResultsDto?> GetResultsAsync(Guid eventId, CancellationToken ct = default)
    {
        var e = await _events.GetByIdAsync(eventId, ct);
        if (e is null) return null;
        var rows = await _results.GetByEventAsync(eventId, ct);
        return ToDto(rows);
    }

    public async Task<SetRaceResultsResult> SetRaceResultsAsync(Guid eventId, Guid raceSessionId, IReadOnlyList<SetRaceResultsEntry> entries, Guid recordedBy, CancellationToken ct = default)
    {
        var e = await _events.GetByIdAsync(eventId, ct);
        if (e is null) return new SetRaceResultsResult(SetRaceResultsOutcome.NotFound, null, null);

        // Locate the race within THIS event's schedule (also gives us its distance/stroke + assigned swimmers).
        var days = await _schedule.GetAsync(eventId, ct);
        var race = days.SelectMany(d => d.Races).FirstOrDefault(r => r.Id == raceSessionId);
        if (race is null) return new SetRaceResultsResult(SetRaceResultsOutcome.NotFound, null, null);

        var assigned = race.SwimmerIds.ToHashSet();
        var seen = new HashSet<Guid>();
        foreach (var entry in entries)
        {
            if (entry.TimeMs <= 0)
                return new SetRaceResultsResult(SetRaceResultsOutcome.Invalid, null, "invalid_time");
            if (!assigned.Contains(entry.SwimmerId))
                return new SetRaceResultsResult(SetRaceResultsOutcome.Invalid, null, "not_assigned");
            if (!seen.Add(entry.SwimmerId))
                return new SetRaceResultsResult(SetRaceResultsOutcome.Invalid, null, "duplicate_swimmer");
        }

        var swimmerIds = entries.Select(x => x.SwimmerId).ToList();
        var bestTimes = await _results.GetBestTimesAsync(race.DistanceId, race.StrokeId, raceSessionId, swimmerIds, ct);

        var rows = entries.Select(entry =>
        {
            var isPb = !bestTimes.TryGetValue(entry.SwimmerId, out var best) || entry.TimeMs < best;
            return new RaceResultInput(entry.SwimmerId, entry.TimeMs, 0, isPb, recordedBy);
        }).ToList();

        await _results.ReplaceForSessionAsync(raceSessionId, rows, ct);
        var saved = await _results.GetByEventAsync(eventId, ct);
        return new SetRaceResultsResult(SetRaceResultsOutcome.Ok, ToDto(saved), null);
    }

    private static ResultsDto ToDto(IReadOnlyList<RaceResultRow> rows) =>
        new(rows.Select(r => new RaceResultDto(r.RaceSessionId, r.SwimmerId, r.TimeMs, r.Points, r.IsPersonalBest)).ToList());
```

Add `using Kheprx.BaseBackend.Championships.Domain.Repositories;` if not already present (it is, for the schedule repo).

- [ ] **Step 8: Run all Championships unit tests — verify green**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests`
Expected: PASS (including the fixed schedule/service tests + new `RaceResultServiceTests`).

- [ ] **Step 9: Checkpoint** — `dotnet build backend/Kheprx.BaseBackend.sln` OK. Do NOT commit.

---

### Task 4: Controller — `GET /results` + `PUT /races/{id}/results`

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/ChampionshipsController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/ChampionshipsControllerTests.cs`

**Interfaces:**
- Consumes: `IChampionshipService.GetResultsAsync`, `IChampionshipService.SetRaceResultsAsync` (Task 3); `CurrentUserId()` (BaseApiController).
- Produces: `GET /api/championships/{eventId}/results`; `PUT /api/championships/{eventId}/races/{raceSessionId}/results` (Roles head_coach,captain).

- [ ] **Step 1: Write the failing controller tests** — append to `ChampionshipsControllerTests.cs` (before the final `}`)

```csharp
    [Fact]
    public async Task GetResults_returns_200_with_rows()
    {
        var eventId = Guid.NewGuid();
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.GetResultsAsync(eventId, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ResultsDto(new[] { new RaceResultDto(Guid.NewGuid(), Guid.NewGuid(), 24560, 0, true) }));

        var result = await NewController(svc.Object, new Mock<IReferenceService>().Object).GetResults(eventId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<ResultsDto>>(ok.Value);
        Assert.Single(body.Data!.Results);
    }

    [Fact]
    public async Task GetResults_returns_404_when_event_missing()
    {
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.GetResultsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ResultsDto?)null);

        var result = await NewController(svc.Object, new Mock<IReferenceService>().Object).GetResults(Guid.NewGuid(), CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task SetRaceResults_returns_200_when_saved()
    {
        var eventId = Guid.NewGuid();
        var raceId = Guid.NewGuid();
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.SetRaceResultsAsync(eventId, raceId, It.IsAny<IReadOnlyList<SetRaceResultsEntry>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new SetRaceResultsResult(SetRaceResultsOutcome.Ok, new ResultsDto(Array.Empty<RaceResultDto>()), null));

        var request = new SetRaceResultsRequest(new[] { new SetRaceResultsEntry(Guid.NewGuid(), 25000) });
        var result = await NewController(svc.Object, new Mock<IReferenceService>().Object).SetRaceResults(eventId, raceId, request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task SetRaceResults_returns_404_when_not_found()
    {
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.SetRaceResultsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<SetRaceResultsEntry>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new SetRaceResultsResult(SetRaceResultsOutcome.NotFound, null, null));

        var request = new SetRaceResultsRequest(Array.Empty<SetRaceResultsEntry>());
        var result = await NewController(svc.Object, new Mock<IReferenceService>().Object).SetRaceResults(Guid.NewGuid(), Guid.NewGuid(), request, CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task SetRaceResults_returns_400_when_invalid()
    {
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.SetRaceResultsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<SetRaceResultsEntry>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new SetRaceResultsResult(SetRaceResultsOutcome.Invalid, null, "not_assigned"));

        var request = new SetRaceResultsRequest(new[] { new SetRaceResultsEntry(Guid.NewGuid(), 25000) });
        var result = await NewController(svc.Object, new Mock<IReferenceService>().Object).SetRaceResults(Guid.NewGuid(), Guid.NewGuid(), request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void SetRaceResults_is_restricted_to_head_coach_and_captain()
    {
        var attr = typeof(ChampionshipsController).GetMethod(nameof(ChampionshipsController.SetRaceResults))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single();
        Assert.Equal("head_coach,captain", attr.Roles);
    }
```

Add `using Kheprx.BaseBackend.Championships.Application.DTOs;` — already present in the file.

- [ ] **Step 2: Run — verify fail** (methods not defined)

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter ChampionshipsControllerTests`
Expected: FAIL (compile error).

- [ ] **Step 3: Add the two actions** to `ChampionshipsController.cs`, before the final `}`

```csharp
    /// <summary>Returns all recorded race results for an event. 404 when the event is unknown.</summary>
    [HttpGet("{eventId:guid}/results")]
    [ProducesResponseType(typeof(ApiResponse<ResultsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ResultsDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ResultsDto>>> GetResults(Guid eventId, CancellationToken ct)
    {
        var lang = AppLanguage.Current;
        var dto = await _service.GetResultsAsync(eventId, ct);
        if (dto is null)
            return StatusCode(StatusCodes.Status404NotFound,
                ApiResponse<ResultsDto>.Failure(ChampionshipMessages.NotFound.Event(lang), "not_found"));

        return Ok(ApiResponse<ResultsDto>.Success(ChampionshipMessages.ResultsSuccess.Retrieved(lang), dto));
    }

    /// <summary>Replaces the recorded times for one race (atomic). Head Coach or Captain only.
    /// Every entered swimmer must be assigned to the race; times are positive milliseconds.</summary>
    [HttpPut("{eventId:guid}/races/{raceSessionId:guid}/results")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<ResultsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ResultsDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ResultsDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ResultsDto>>> SetRaceResults(Guid eventId, Guid raceSessionId, SetRaceResultsRequest request, CancellationToken ct)
    {
        var lang = AppLanguage.Current;
        var entries = request.Entries ?? new List<SetRaceResultsEntry>();
        var result = await _service.SetRaceResultsAsync(eventId, raceSessionId, entries, CurrentUserId(), ct);

        return result.Outcome switch
        {
            SetRaceResultsOutcome.NotFound => StatusCode(StatusCodes.Status404NotFound,
                ApiResponse<ResultsDto>.Failure(ChampionshipMessages.NotFound.Event(lang), "not_found")),
            SetRaceResultsOutcome.Invalid => BadRequest(
                ApiResponse<ResultsDto>.Failure(ChampionshipMessages.ResultsErrors.Invalid(lang), "validation")),
            _ => Ok(ApiResponse<ResultsDto>.Success(ChampionshipMessages.ResultsSuccess.Saved(lang), result.Saved!)),
        };
    }
```

- [ ] **Step 4: Run — verify pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter ChampionshipsControllerTests`
Expected: PASS.

- [ ] **Step 5: Checkpoint** — full backend build + test suites green. Do NOT commit.

```bash
dotnet build backend/Kheprx.BaseBackend.sln
dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests
dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests
```

---

### Task 5: Frontend — `race-time.ts` conversions

**Files:**
- Create: `frontend/src/app/features/championships/domain/model/race-time.ts`
- Test: `frontend/src/app/features/championships/testing/domain/model/race-time.spec.ts`

**Interfaces:**
- Produces: `parseTimeToMs(text: string): number | null`, `formatMsToTime(ms: number): string`.

- [ ] **Step 1: Write the failing spec** `race-time.spec.ts`

```ts
import { parseTimeToMs, formatMsToTime } from '@features/championships/domain/model/race-time';

describe('race-time', () => {
  it('parses mm:ss.SS to milliseconds', () => {
    expect(parseTimeToMs('0:24.56')).toBe(24560);
    expect(parseTimeToMs('1:05.30')).toBe(65300);
    expect(parseTimeToMs('2:08.45')).toBe(128450);
  });

  it('parses ss.SS without a minutes part', () => {
    expect(parseTimeToMs('24.56')).toBe(24560);
    expect(parseTimeToMs('9.1')).toBe(9100); // one decimal → tenths
  });

  it('rejects blank and malformed input', () => {
    expect(parseTimeToMs('')).toBeNull();
    expect(parseTimeToMs('   ')).toBeNull();
    expect(parseTimeToMs('abc')).toBeNull();
    expect(parseTimeToMs('1:99')).toBeNull(); // seconds out of range
    expect(parseTimeToMs('0')).toBeNull();    // zero is not a valid finish time
  });

  it('formats milliseconds back to m:ss.SS', () => {
    expect(formatMsToTime(24560)).toBe('0:24.56');
    expect(formatMsToTime(65300)).toBe('1:05.30');
  });

  it('round-trips a parsed value', () => {
    const ms = parseTimeToMs('1:05.30')!;
    expect(formatMsToTime(ms)).toBe('1:05.30');
  });

  it('formats invalid input as empty string', () => {
    expect(formatMsToTime(-1)).toBe('');
    expect(formatMsToTime(NaN)).toBe('');
  });
});
```

- [ ] **Step 2: Run — verify fail**

Run: `cd frontend && npx jest src/app/features/championships/testing/domain/model/race-time.spec.ts`
Expected: FAIL (module not found).

- [ ] **Step 3: Write `race-time.ts`**

```ts
// Swim finish-time conversions between the UI text form and integer milliseconds.
// Accepts 'm:ss.SS', 'mm:ss.SS' or 'ss.SS' (fractional seconds up to 2 digits = centiseconds).

const PATTERN = /^(?:(\d{1,2}):)?([0-5]?\d)(?:\.(\d{1,2}))?$/;

export function parseTimeToMs(text: string): number | null {
  const t = (text ?? '').trim();
  if (!t) return null;
  const m = PATTERN.exec(t);
  if (!m) return null;
  const minutes = m[1] ? parseInt(m[1], 10) : 0;
  const seconds = parseInt(m[2], 10);
  const centis = m[3] ? parseInt(m[3].padEnd(2, '0'), 10) : 0;
  const ms = (minutes * 60 + seconds) * 1000 + centis * 10;
  return ms > 0 ? ms : null;
}

export function formatMsToTime(ms: number): string {
  if (!Number.isFinite(ms) || ms < 0) return '';
  const totalCentis = Math.round(ms / 10);
  const centis = totalCentis % 100;
  const totalSeconds = Math.floor(totalCentis / 100);
  const seconds = totalSeconds % 60;
  const minutes = Math.floor(totalSeconds / 60);
  return `${minutes}:${seconds.toString().padStart(2, '0')}.${centis.toString().padStart(2, '0')}`;
}
```

- [ ] **Step 4: Run — verify pass.** Do NOT commit.

Run: `cd frontend && npx jest src/app/features/championships/testing/domain/model/race-time.spec.ts`
Expected: PASS.

---

### Task 6: Frontend — results DTO, domain model, mapper

**Files:**
- Create: `frontend/src/app/features/championships/data/dto/race-result.dto.ts`
- Create: `frontend/src/app/features/championships/data/dto/race-result.mapper.ts`
- Create: `frontend/src/app/features/championships/domain/model/race-result.ts`
- Test: `frontend/src/app/features/championships/testing/data/dto/race-result.dto.spec.ts`

**Interfaces:**
- Produces:
  - `race-result.dto.ts`: `RaceResultData`, `ResultsData`, `ResultsDtoRs`, `SetRaceResultsRq`, `isResultsDtoValid(data): data is ResultsData`.
  - `race-result.ts`: `RaceResultEntry`, `RaceScheduleDay`, `RaceScheduleRace`, `FinishedRaceCard`, `ResultsRaceCard`, `ResultsRaceEntry`.
  - `race-result.mapper.ts`: `toRaceResultList(dto: ResultsData): RaceResultEntry[]`.

- [ ] **Step 1: Write `race-result.dto.ts`**

```ts
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface RaceResultData {
  raceSessionId: string;
  swimmerId: string;
  timeMs: number;
  points: number;
  isPersonalBest: boolean;
}
export interface ResultsData { results: RaceResultData[]; }
export interface ResultsDtoRs extends BaseResponseRs<ResultsData> {}

/** PUT body — the finish times for one race (server replaces that race's results atomically). */
export interface SetRaceResultsRq {
  entries: { swimmerId: string; timeMs: number }[];
}

function isStr(v: unknown): v is string { return typeof v === 'string'; }
function isNum(v: unknown): v is number { return typeof v === 'number' && Number.isFinite(v); }

export function isResultsDtoValid(data: unknown): data is ResultsData {
  const d = data as ResultsData;
  if (!d || typeof d !== 'object' || !Array.isArray(d.results)) return false;
  return d.results.every(
    (r) =>
      r != null && typeof r === 'object' &&
      isStr(r.raceSessionId) && isStr(r.swimmerId) &&
      isNum(r.timeMs) && isNum(r.points) && typeof r.isPersonalBest === 'boolean',
  );
}
```

- [ ] **Step 2: Write `race-result.ts`** (domain model)

```ts
// Read + view models for the Finished races and Results tabs.

export interface RaceResultEntry {
  raceSessionId: string;
  swimmerId: string;
  timeMs: number;
  points: number;
  isPersonalBest: boolean;
}

// Id-carrying schedule read model. (The Days editor model in competition-schedule.ts intentionally
// drops server ids; Finished/Results need them to match results and target the results PUT.)
export interface RaceScheduleRace {
  id: string;
  strokeId: string;
  distanceId: string;
  scheduledTime: string | null; // 'HH:mm' | null
  swimmerIds: string[];
}
export interface RaceScheduleDay {
  id: string;
  labelEn: string;
  labelAr: string | null;
  dayDate: string;              // 'YYYY-MM-DD'
  races: RaceScheduleRace[];
}

// Card view-models the template renders.
export interface FinishedRaceCard {
  raceSessionId: string;
  raceName: string;
  dayLabel: string;
  scheduledTime: string | null;
  swimmers: { id: string; name: string }[];
}
export interface ResultsRaceEntry {
  swimmerName: string;
  timeMs: number;
  rank: number;
  isPersonalBest: boolean;
}
export interface ResultsRaceCard {
  raceSessionId: string;
  raceName: string;
  dayLabel: string;
  entries: ResultsRaceEntry[];
}
```

- [ ] **Step 3: Write `race-result.mapper.ts`**

```ts
import { ResultsData } from '@features/championships/data/dto/race-result.dto';
import { RaceResultEntry } from '@features/championships/domain/model/race-result';

export function toRaceResultList(dto: ResultsData): RaceResultEntry[] {
  return dto.results.map((r) => ({
    raceSessionId: r.raceSessionId,
    swimmerId: r.swimmerId,
    timeMs: r.timeMs,
    points: r.points,
    isPersonalBest: r.isPersonalBest,
  }));
}
```

- [ ] **Step 4: Write the spec** `race-result.dto.spec.ts`

```ts
import { isResultsDtoValid } from '@features/championships/data/dto/race-result.dto';
import { toRaceResultList } from '@features/championships/data/dto/race-result.mapper';

describe('race-result dto', () => {
  const valid = { results: [{ raceSessionId: 'r1', swimmerId: 's1', timeMs: 24560, points: 0, isPersonalBest: true }] };

  it('accepts a well-formed payload', () => {
    expect(isResultsDtoValid(valid)).toBe(true);
    expect(isResultsDtoValid({ results: [] })).toBe(true);
  });

  it('rejects malformed payloads', () => {
    expect(isResultsDtoValid(null)).toBe(false);
    expect(isResultsDtoValid({})).toBe(false);
    expect(isResultsDtoValid({ results: [{ raceSessionId: 'r1' }] })).toBe(false);
    expect(isResultsDtoValid({ results: [{ raceSessionId: 'r1', swimmerId: 's1', timeMs: 'x', points: 0, isPersonalBest: true }] })).toBe(false);
  });

  it('maps DTO rows to domain entries', () => {
    const rows = toRaceResultList(valid);
    expect(rows).toEqual([{ raceSessionId: 'r1', swimmerId: 's1', timeMs: 24560, points: 0, isPersonalBest: true }]);
  });
});
```

- [ ] **Step 5: Run — verify pass.** Do NOT commit.

Run: `cd frontend && npx jest src/app/features/championships/testing/data/dto/race-result.dto.spec.ts`
Expected: PASS.

---

### Task 7: Frontend — repository `getResults` + `setRaceResults`

**Files:**
- Modify: `frontend/src/app/features/championships/domain/repositories/championships.repository.ts`
- Modify: `frontend/src/app/features/championships/data/repositories/championships.repository.impl.ts`
- Test: `frontend/src/app/features/championships/testing/data/repositories/championships.repository.impl.spec.ts`

**Interfaces:**
- Consumes: `ResultsDtoRs`, `SetRaceResultsRq` (Task 6).
- Produces: `IChampionshipsRepository.getResults(eventId): Promise<ResultsDtoRs>`, `.setRaceResults(eventId, raceSessionId, rq): Promise<ResultsDtoRs>`.

- [ ] **Step 1: Add the failing tests** — append inside the `describe` in `championships.repository.impl.spec.ts`

```ts
  it('getResults GETs the results endpoint', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: { results: [] } });
    await repo.getResults('e1');
    expect(http.get).toHaveBeenCalledWith('/api/championships/e1/results');
  });

  it('setRaceResults PUTs to the per-race results endpoint with the body', async () => {
    (http.put as jest.Mock).mockResolvedValue({ data: { results: [] } });
    const rq = { entries: [{ swimmerId: 's1', timeMs: 24560 }] };
    await repo.setRaceResults('e1', 'race1', rq);
    expect(http.put).toHaveBeenCalledWith('/api/championships/e1/races/race1/results', { body: rq });
  });
```

- [ ] **Step 2: Run — verify fail**

Run: `cd frontend && npx jest src/app/features/championships/testing/data/repositories/championships.repository.impl.spec.ts`
Expected: FAIL (methods not on repo).

- [ ] **Step 3: Add to the port** `championships.repository.ts` — add the import and two methods:

```ts
import { ResultsDtoRs, SetRaceResultsRq } from '@features/championships/data/dto/race-result.dto';
```
```ts
  getResults(eventId: string): Promise<ResultsDtoRs>;
  setRaceResults(eventId: string, raceSessionId: string, rq: SetRaceResultsRq): Promise<ResultsDtoRs>;
```

- [ ] **Step 4: Implement in** `championships.repository.impl.ts` — add the import and two methods:

```ts
import { ResultsDtoRs, SetRaceResultsRq } from '@features/championships/data/dto/race-result.dto';
```
```ts
  getResults(eventId: string): Promise<ResultsDtoRs> {
    return this.http.get<ResultsDtoRs>(`/api/championships/${eventId}/results`);
  }

  setRaceResults(eventId: string, raceSessionId: string, rq: SetRaceResultsRq): Promise<ResultsDtoRs> {
    return this.http.put<ResultsDtoRs>(`/api/championships/${eventId}/races/${raceSessionId}/results`, { body: rq });
  }
```

- [ ] **Step 5: Run — verify pass.** Do NOT commit.

Run: `cd frontend && npx jest src/app/features/championships/testing/data/repositories/championships.repository.impl.spec.ts`
Expected: PASS.

---

### Task 8: Frontend — id-preserving schedule read + results use-cases

**Files:**
- Modify: `frontend/src/app/features/championships/data/dto/schedule.mapper.ts`
- Create: `frontend/src/app/features/championships/domain/usecases/load-race-schedule.use-case.ts`
- Create: `frontend/src/app/features/championships/domain/usecases/load-results.use-case.ts`
- Create: `frontend/src/app/features/championships/domain/usecases/save-race-results.use-case.ts`
- Test: `frontend/src/app/features/championships/testing/domain/usecases/race-result.use-cases.spec.ts`

**Interfaces:**
- Consumes: `repo.getSchedule` (existing), `repo.getResults`/`repo.setRaceResults` (Task 7), `isScheduleDtoValid` (existing), `isResultsDtoValid`/`toRaceResultList` (Task 6), `RaceScheduleDay`/`RaceResultEntry` (Task 6).
- Produces:
  - `toRaceScheduleList(dto: ScheduleDtoData): RaceScheduleDay[]` (schedule.mapper.ts)
  - `LoadRaceScheduleUseCase.run(eventId): Result<RaceScheduleDay[]>`
  - `LoadResultsUseCase.run(eventId): Result<RaceResultEntry[]>`
  - `SaveRaceResultsUseCase.run({ eventId, raceSessionId, entries }): Result<RaceResultEntry[]>` with `SaveRaceResultsInput { eventId; raceSessionId; entries: { swimmerId; timeMs }[] }`.

- [ ] **Step 1: Add `toRaceScheduleList`** to `schedule.mapper.ts` (append; keep `toScheduleDataList`/`toSetScheduleRq`)

```ts
import { RaceScheduleDay } from '@features/championships/domain/model/race-result';

/** DTO tree → id-preserving read model for Finished/Results. Time normalized to 'HH:mm'. */
export function toRaceScheduleList(dto: ScheduleDtoData): RaceScheduleDay[] {
  return dto.days.map((day) => ({
    id: day.id,
    labelEn: day.labelEn,
    labelAr: day.labelAr,
    dayDate: day.dayDate,
    races: day.races.map((r) => ({
      id: r.id,
      strokeId: r.strokeId,
      distanceId: r.distanceId,
      scheduledTime: r.scheduledTime ? r.scheduledTime.slice(0, 5) : null,
      swimmerIds: [...r.swimmerIds],
    })),
  }));
}
```

(`ScheduleDtoData` is already imported at the top of `schedule.mapper.ts`.)

- [ ] **Step 2: Write `load-race-schedule.use-case.ts`**

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { isScheduleDtoValid } from '@features/championships/data/dto/schedule.dto';
import { toRaceScheduleList } from '@features/championships/data/dto/schedule.mapper';
import { RaceScheduleDay } from '@features/championships/domain/model/race-result';

@Injectable({ providedIn: 'root' })
export class LoadRaceScheduleUseCase extends UseCase<string, RaceScheduleDay[]> {
  private readonly repo = inject(CHAMPIONSHIPS_REPOSITORY);
  constructor() { super('LoadRaceSchedule'); }

  protected async execute(eventId: string): Promise<RaceScheduleDay[]> {
    const res = await this.repo.getSchedule(eventId);
    if (!isScheduleDtoValid(res.data)) throw new AppError('Invalid schedule received', 'validation');
    return toRaceScheduleList(res.data);
  }
}
```

- [ ] **Step 3: Write `load-results.use-case.ts`**

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { isResultsDtoValid } from '@features/championships/data/dto/race-result.dto';
import { toRaceResultList } from '@features/championships/data/dto/race-result.mapper';
import { RaceResultEntry } from '@features/championships/domain/model/race-result';

@Injectable({ providedIn: 'root' })
export class LoadResultsUseCase extends UseCase<string, RaceResultEntry[]> {
  private readonly repo = inject(CHAMPIONSHIPS_REPOSITORY);
  constructor() { super('LoadResults'); }

  protected async execute(eventId: string): Promise<RaceResultEntry[]> {
    const res = await this.repo.getResults(eventId);
    if (!isResultsDtoValid(res.data)) throw new AppError('Invalid results received', 'validation');
    return toRaceResultList(res.data);
  }
}
```

- [ ] **Step 4: Write `save-race-results.use-case.ts`**

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { isResultsDtoValid, SetRaceResultsRq } from '@features/championships/data/dto/race-result.dto';
import { toRaceResultList } from '@features/championships/data/dto/race-result.mapper';
import { RaceResultEntry } from '@features/championships/domain/model/race-result';

export interface SaveRaceResultsInput {
  eventId: string;
  raceSessionId: string;
  entries: { swimmerId: string; timeMs: number }[];
}

@Injectable({ providedIn: 'root' })
export class SaveRaceResultsUseCase extends UseCase<SaveRaceResultsInput, RaceResultEntry[]> {
  private readonly repo = inject(CHAMPIONSHIPS_REPOSITORY);
  constructor() { super('SaveRaceResults'); }

  protected async execute(input: SaveRaceResultsInput): Promise<RaceResultEntry[]> {
    const rq: SetRaceResultsRq = { entries: input.entries };
    const res = await this.repo.setRaceResults(input.eventId, input.raceSessionId, rq);
    if (!isResultsDtoValid(res.data)) throw new AppError('Invalid results received', 'validation');
    return toRaceResultList(res.data);
  }
}
```

- [ ] **Step 5: Write the spec** `race-result.use-cases.spec.ts` (mirrors `load-schedule.use-case.spec.ts` / `save-schedule.use-case.spec.ts`)

```ts
import { TestBed } from '@angular/core/testing';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { LoadRaceScheduleUseCase } from '@features/championships/domain/usecases/load-race-schedule.use-case';
import { LoadResultsUseCase } from '@features/championships/domain/usecases/load-results.use-case';
import { SaveRaceResultsUseCase } from '@features/championships/domain/usecases/save-race-results.use-case';

const scheduleDto = {
  data: { days: [{ id: 'd1', labelEn: 'Day 1', labelAr: null, dayDate: '2023-11-15',
    races: [{ id: 'r1', strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00:00', swimmerIds: ['s1'] }] }] },
};
const resultsDto = { data: { results: [{ raceSessionId: 'r1', swimmerId: 's1', timeMs: 24560, points: 0, isPersonalBest: true }] } };

function make<T>(token: any, repo: any): T {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: CHAMPIONSHIPS_REPOSITORY, useValue: repo }] });
  return TestBed.inject(token);
}

describe('race-result use-cases', () => {
  it('LoadRaceSchedule preserves day/race ids and normalizes time to HH:mm', async () => {
    const uc = make<LoadRaceScheduleUseCase>(LoadRaceScheduleUseCase, { getSchedule: jest.fn().mockResolvedValue(scheduleDto) });
    const res = await uc.run('e1');
    expect(res.ok).toBe(true);
    if (res.ok) {
      expect(res.data[0].id).toBe('d1');
      expect(res.data[0].races[0].id).toBe('r1');           // id preserved (unlike LoadScheduleUseCase)
      expect(res.data[0].races[0].scheduledTime).toBe('09:00');
    }
  });

  it('LoadRaceSchedule fails on an invalid payload', async () => {
    const uc = make<LoadRaceScheduleUseCase>(LoadRaceScheduleUseCase, { getSchedule: jest.fn().mockResolvedValue({ data: { days: 'x' } }) });
    const res = await uc.run('e1');
    expect(res.ok).toBe(false);
  });

  it('LoadResults maps rows', async () => {
    const uc = make<LoadResultsUseCase>(LoadResultsUseCase, { getResults: jest.fn().mockResolvedValue(resultsDto) });
    const res = await uc.run('e1');
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data[0].timeMs).toBe(24560);
  });

  it('LoadResults fails on an invalid payload', async () => {
    const uc = make<LoadResultsUseCase>(LoadResultsUseCase, { getResults: jest.fn().mockResolvedValue({ data: {} }) });
    const res = await uc.run('e1');
    expect(res.ok).toBe(false);
  });

  it('SaveRaceResults sends entries and adopts the returned results', async () => {
    const setRaceResults = jest.fn().mockResolvedValue(resultsDto);
    const uc = make<SaveRaceResultsUseCase>(SaveRaceResultsUseCase, { setRaceResults });
    const res = await uc.run({ eventId: 'e1', raceSessionId: 'r1', entries: [{ swimmerId: 's1', timeMs: 24560 }] });
    expect(setRaceResults).toHaveBeenCalledWith('e1', 'r1', { entries: [{ swimmerId: 's1', timeMs: 24560 }] });
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data[0].swimmerId).toBe('s1');
  });
});
```

- [ ] **Step 6: Run — verify pass.** Do NOT commit.

Run: `cd frontend && npx jest src/app/features/championships/testing/domain/usecases/race-result.use-cases.spec.ts`
Expected: PASS.

---

### Task 9: Frontend — `RaceResultsViewModel`

**Files:**
- Create: `frontend/src/app/features/championships/presentation/pages/championship-detail/race-results.viewmodel.ts`
- Modify: `frontend/src/app/features/championships/index.ts` (barrel export)
- Test: `frontend/src/app/features/championships/testing/presentation/pages/championship-detail/race-results.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `LoadRaceScheduleUseCase`, `LoadResultsUseCase`, `SaveRaceResultsUseCase` (Task 8); `LoadStrokesUseCase`, `LoadDistancesUseCase` (`@features/reference`); `ListSwimmersUseCase`; `raceStatus` (competition-schedule.ts); `parseTimeToMs`/`formatMsToTime` (Task 5); stores.
- Produces: `RaceResultsViewModel` with `ensureLoaded(eventId)`, `load(eventId)`, signals `loading/loaded/error/saving/openRaceId`, computeds `canManage/finishedRaces/finishedCount/resultRaces`, methods `openResults/isOpen/timeInput/setTime/saveResults`, helpers `formatTime/swimmerName`.

- [ ] **Step 1: Write `race-results.viewmodel.ts`**

```ts
import { Injectable, computed, inject, signal } from '@angular/core';
import { LanguageStore } from '@core/i18n/language.store';
import { TranslateService } from '@core/i18n';
import { NotificationService } from '@core/ui/notification.service';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { LoadRaceScheduleUseCase } from '@features/championships/domain/usecases/load-race-schedule.use-case';
import { LoadResultsUseCase } from '@features/championships/domain/usecases/load-results.use-case';
import { SaveRaceResultsUseCase } from '@features/championships/domain/usecases/save-race-results.use-case';
import { LoadStrokesUseCase, LoadDistancesUseCase, LookupItem } from '@features/reference';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';
import {
  RaceScheduleDay, RaceResultEntry, FinishedRaceCard, ResultsRaceCard,
} from '@features/championships/domain/model/race-result';
import { raceStatus } from '@features/championships/domain/model/competition-schedule';
import { formatMsToTime, parseTimeToMs } from '@features/championships/domain/model/race-time';

@Injectable()
export class RaceResultsViewModel {
  private readonly loadScheduleUc = inject(LoadRaceScheduleUseCase);
  private readonly loadResultsUc = inject(LoadResultsUseCase);
  private readonly saveResultsUc = inject(SaveRaceResultsUseCase);
  private readonly loadStrokesUc = inject(LoadStrokesUseCase);
  private readonly loadDistancesUc = inject(LoadDistancesUseCase);
  private readonly listSwimmersUc = inject(ListSwimmersUseCase);
  private readonly language = inject(LanguageStore);
  private readonly session = inject(AuthSessionStore);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  private eventId = '';

  readonly loading = signal(false);
  readonly loaded = signal(false);
  readonly error = signal(false);
  readonly saving = signal(false);

  private readonly strokes = signal<LookupItem[]>([]);
  private readonly distances = signal<LookupItem[]>([]);
  private readonly roster = signal<SwimmerListItem[]>([]);
  private readonly schedule = signal<RaceScheduleDay[]>([]);
  private readonly results = signal<RaceResultEntry[]>([]);

  readonly openRaceId = signal<string | null>(null);
  // sessionId -> swimmerId -> raw text input
  private readonly inputs = signal<Record<string, Record<string, string>>>({});

  readonly canManage = computed(() => {
    const r = this.session.role();
    return r === 'head_coach' || r === 'captain';
  });

  private readonly resultsBySession = computed(() => {
    const map = new Map<string, RaceResultEntry[]>();
    for (const r of this.results()) {
      const list = map.get(r.raceSessionId) ?? [];
      list.push(r);
      map.set(r.raceSessionId, list);
    }
    return map;
  });

  readonly finishedRaces = computed<FinishedRaceCard[]>(() => {
    const done = this.resultsBySession();
    const cards: FinishedRaceCard[] = [];
    for (const day of this.schedule()) {
      for (const race of day.races) {
        if (raceStatus(day.dayDate, race.scheduledTime) !== 'awaitingResults') continue;
        if (done.has(race.id)) continue;
        cards.push({
          raceSessionId: race.id,
          raceName: this.raceName(race.distanceId, race.strokeId),
          dayLabel: this.dayLabel(day),
          scheduledTime: race.scheduledTime,
          swimmers: race.swimmerIds
            .map((id) => this.roster().find((s) => s.id === id))
            .filter((s): s is SwimmerListItem => !!s)
            .map((s) => ({ id: s.id, name: this.swimmerName(s) })),
        });
      }
    }
    return cards;
  });

  readonly finishedCount = computed(() => this.finishedRaces().length);

  readonly resultRaces = computed<ResultsRaceCard[]>(() => {
    const done = this.resultsBySession();
    const cards: ResultsRaceCard[] = [];
    for (const day of this.schedule()) {
      for (const race of day.races) {
        const rows = done.get(race.id);
        if (!rows || rows.length === 0) continue;
        const entries = [...rows]
          .sort((a, b) => a.timeMs - b.timeMs)
          .map((r, i) => ({
            swimmerName: this.swimmerNameById(r.swimmerId),
            timeMs: r.timeMs,
            rank: i + 1,
            isPersonalBest: r.isPersonalBest,
          }));
        cards.push({ raceSessionId: race.id, raceName: this.raceName(race.distanceId, race.strokeId), dayLabel: this.dayLabel(day), entries });
      }
    }
    return cards;
  });

  async ensureLoaded(eventId: string): Promise<void> {
    if (this.loaded() && this.eventId === eventId) return;
    await this.load(eventId);
  }

  async load(eventId: string): Promise<void> {
    this.eventId = eventId;
    this.loading.set(true);
    this.error.set(false);
    this.loaded.set(false);
    this.openRaceId.set(null);
    this.inputs.set({});

    const [schedRes, resultsRes, strokesRes, distRes, rosterRes] = await Promise.all([
      this.loadScheduleUc.run(eventId),
      this.loadResultsUc.run(eventId),
      this.loadStrokesUc.run(),
      this.loadDistancesUc.run(),
      this.listSwimmersUc.run(undefined),
    ]);

    if (!schedRes.ok || !resultsRes.ok) {
      this.loading.set(false);
      this.error.set(true);
      return;
    }
    this.schedule.set(schedRes.data);
    this.results.set(resultsRes.data);
    this.strokes.set(strokesRes.ok ? strokesRes.data : []);
    this.distances.set(distRes.ok ? distRes.data : []);
    this.roster.set(rosterRes.ok ? rosterRes.data : []);
    this.loaded.set(true);
    this.loading.set(false);
  }

  openResults(sessionId: string): void {
    if (!this.canManage()) return;
    this.openRaceId.set(this.openRaceId() === sessionId ? null : sessionId);
  }
  isOpen(sessionId: string): boolean { return this.openRaceId() === sessionId; }

  timeInput(sessionId: string, swimmerId: string): string {
    return this.inputs()[sessionId]?.[swimmerId] ?? '';
  }
  setTime(sessionId: string, swimmerId: string, value: string): void {
    if (!this.canManage()) return;
    const all = { ...this.inputs() };
    all[sessionId] = { ...(all[sessionId] ?? {}), [swimmerId]: value };
    this.inputs.set(all);
  }

  async saveResults(sessionId: string): Promise<void> {
    if (!this.canManage() || this.saving()) return;
    const forSession = this.inputs()[sessionId] ?? {};
    const entries: { swimmerId: string; timeMs: number }[] = [];
    for (const [swimmerId, text] of Object.entries(forSession)) {
      const ms = parseTimeToMs(text);
      if (ms !== null) entries.push({ swimmerId, timeMs: ms });
    }
    if (entries.length === 0) return; // nothing valid to save
    this.saving.set(true);
    try {
      const res = await this.saveResultsUc.run({ eventId: this.eventId, raceSessionId: sessionId, entries });
      if (res.ok) {
        this.notify.success(this.i18n.t('championships.finished.saved'));
        this.results.set(res.data);
        this.openRaceId.set(null);
        const all = { ...this.inputs() };
        delete all[sessionId];
        this.inputs.set(all);
      } else {
        this.notify.error(this.i18n.t('championships.finished.saveFailed'));
      }
    } finally {
      this.saving.set(false);
    }
  }

  // ---- display helpers ----
  formatTime(ms: number): string { return formatMsToTime(ms); }
  swimmerName(s: SwimmerListItem): string {
    return this.language.lang() === 'ar' ? (s.nameAr ?? s.nameEn) : s.nameEn;
  }
  private swimmerNameById(id: string): string {
    const s = this.roster().find((x) => x.id === id);
    return s ? this.swimmerName(s) : id;
  }
  private raceName(distanceId: string, strokeId: string): string {
    return [this.lookupLabel(this.distances(), distanceId), this.lookupLabel(this.strokes(), strokeId)]
      .filter(Boolean).join(' ');
  }
  private dayLabel(day: RaceScheduleDay): string {
    return this.language.lang() === 'ar' ? (day.labelAr ?? day.labelEn) : day.labelEn;
  }
  private lookupLabel(items: LookupItem[], id: string): string {
    const item = items.find((i) => i.id === id);
    if (!item) return '';
    return this.language.lang() === 'ar' ? (item.nameAr ?? item.nameEn) : item.nameEn;
  }
}
```

- [ ] **Step 2: Export from the barrel** — add to `index.ts`:

```ts
export { RaceResultsViewModel } from './presentation/pages/championship-detail/race-results.viewmodel';
```

- [ ] **Step 3: Write the spec** `race-results.viewmodel.spec.ts` (mirrors `competition-days.viewmodel.spec.ts`). Note the schedule dates are **2023** (past) so races are `awaitingResults`.

```ts
import { TestBed } from '@angular/core/testing';
import { ok } from '@core/domain/result/result';
import { LanguageStore } from '@core/i18n/language.store';
import { TranslateService } from '@core/i18n';
import { NotificationService } from '@core/ui/notification.service';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { RaceResultsViewModel } from '@features/championships/presentation/pages/championship-detail/race-results.viewmodel';
import { LoadRaceScheduleUseCase } from '@features/championships/domain/usecases/load-race-schedule.use-case';
import { LoadResultsUseCase } from '@features/championships/domain/usecases/load-results.use-case';
import { SaveRaceResultsUseCase } from '@features/championships/domain/usecases/save-race-results.use-case';
import { LoadStrokesUseCase, LoadDistancesUseCase } from '@features/reference';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { RaceScheduleDay, RaceResultEntry } from '@features/championships/domain/model/race-result';
import { AppError } from '@core/domain/errors/app-error';

const strokes = [{ id: 'st1', code: 'freestyle', nameEn: 'Freestyle', nameAr: null }];
const distances = [{ id: 'ds1', code: '50m', nameEn: '50m', nameAr: null }, { id: 'ds2', code: '100m', nameEn: '100m', nameAr: null }];
const roster = [
  { id: 's1', uid: 'U1', nameEn: 'Ahmed', nameAr: null, clubNameEn: 'Oasis', clubNameAr: null, gender: 'male', age: 18 },
  { id: 's2', uid: 'U2', nameEn: 'Sara', nameAr: null, clubNameEn: 'Oasis', clubNameAr: null, gender: 'female', age: 16 },
];
// Two past races: r1 (50m Free) has 2 swimmers, r2 (100m Free) has 0 swimmers.
const schedule: RaceScheduleDay[] = [{
  id: 'd1', labelEn: 'Day 1', labelAr: null, dayDate: '2023-11-15',
  races: [
    { id: 'r1', strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00', swimmerIds: ['s1', 's2'] },
    { id: 'r2', strokeId: 'st1', distanceId: 'ds2', scheduledTime: '10:00', swimmerIds: [] },
  ],
}];
// A far-future race must never show in Finished.
const futureDay: RaceScheduleDay = {
  id: 'd2', labelEn: 'Day 2', labelAr: null, dayDate: '2999-01-01',
  races: [{ id: 'r3', strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00', swimmerIds: ['s1'] }],
};

interface Opts { role?: string; schedule?: RaceScheduleDay[]; results?: RaceResultEntry[]; saveRun?: jest.Mock; }

function setup(opts: Opts = {}) {
  const schedRun = jest.fn().mockResolvedValue(ok(opts.schedule ?? schedule));
  const resultsRun = jest.fn().mockResolvedValue(ok(opts.results ?? []));
  const saveRun = opts.saveRun ?? jest.fn();
  const notify = { success: jest.fn(), error: jest.fn() };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      RaceResultsViewModel,
      { provide: LoadRaceScheduleUseCase, useValue: { run: schedRun } },
      { provide: LoadResultsUseCase, useValue: { run: resultsRun } },
      { provide: SaveRaceResultsUseCase, useValue: { run: saveRun } },
      { provide: LoadStrokesUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(strokes)) } },
      { provide: LoadDistancesUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(distances)) } },
      { provide: ListSwimmersUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(roster)) } },
      { provide: LanguageStore, useValue: { lang: () => 'en' } },
      { provide: AuthSessionStore, useValue: { role: () => opts.role ?? 'head_coach' } },
      { provide: NotificationService, useValue: notify },
      { provide: TranslateService, useValue: { t: (k: string) => k } },
    ],
  });
  return { vm: TestBed.inject(RaceResultsViewModel), schedRun, resultsRun, saveRun, notify };
}

describe('RaceResultsViewModel', () => {
  it('lists past races with no results under Finished, resolving names', async () => {
    const { vm } = setup();
    await vm.load('e1');
    expect(vm.loaded()).toBe(true);
    const finished = vm.finishedRaces();
    expect(finished.map((f) => f.raceSessionId)).toEqual(['r1', 'r2']);
    expect(finished[0].raceName).toBe('50m Freestyle');
    expect(finished[0].swimmers.map((s) => s.name)).toEqual(['Ahmed', 'Sara']);
    expect(finished[1].swimmers).toEqual([]); // r2 has no assigned swimmers
    expect(vm.finishedCount()).toBe(2);
    expect(vm.resultRaces()).toEqual([]);
  });

  it('a future-scheduled race never appears in Finished', async () => {
    const { vm } = setup({ schedule: [futureDay] });
    await vm.load('e1');
    expect(vm.finishedRaces()).toEqual([]);
  });

  it('a race with results appears in Results (ranked by time, PB kept) and leaves Finished', async () => {
    const results: RaceResultEntry[] = [
      { raceSessionId: 'r1', swimmerId: 's2', timeMs: 25890, points: 0, isPersonalBest: false },
      { raceSessionId: 'r1', swimmerId: 's1', timeMs: 24560, points: 0, isPersonalBest: true },
    ];
    const { vm } = setup({ results });
    await vm.load('e1');
    expect(vm.finishedRaces().map((f) => f.raceSessionId)).toEqual(['r2']); // r1 moved out
    const race = vm.resultRaces().find((r) => r.raceSessionId === 'r1')!;
    expect(race.entries.map((e) => [e.rank, e.swimmerName, e.isPersonalBest]))
      .toEqual([[1, 'Ahmed', true], [2, 'Sara', false]]); // sorted by time
  });

  it('partial results (one of two swimmers) still moves the race to Results', async () => {
    const results: RaceResultEntry[] = [{ raceSessionId: 'r1', swimmerId: 's1', timeMs: 24560, points: 0, isPersonalBest: true }];
    const { vm } = setup({ results });
    await vm.load('e1');
    expect(vm.finishedRaces().map((f) => f.raceSessionId)).toEqual(['r2']);
    expect(vm.resultRaces().find((r) => r.raceSessionId === 'r1')!.entries).toHaveLength(1);
  });

  it('saveResults skips blank/invalid inputs and posts only valid times', async () => {
    const saveRun = jest.fn().mockResolvedValue(ok([{ raceSessionId: 'r1', swimmerId: 's1', timeMs: 24560, points: 0, isPersonalBest: true }]));
    const { vm, notify } = setup({ saveRun });
    await vm.load('e1');
    vm.openResults('r1');
    vm.setTime('r1', 's1', '0:24.56');
    vm.setTime('r1', 's2', '   '); // blank → skipped
    await vm.saveResults('r1');
    expect(saveRun).toHaveBeenCalledWith({ eventId: 'e1', raceSessionId: 'r1', entries: [{ swimmerId: 's1', timeMs: 24560 }] });
    expect(notify.success).toHaveBeenCalled();
    expect(vm.isOpen('r1')).toBe(false);
  });

  it('saveResults is a no-op when there is nothing valid to save', async () => {
    const saveRun = jest.fn();
    const { vm } = setup({ saveRun });
    await vm.load('e1');
    vm.openResults('r2'); // race with no swimmers → no inputs
    await vm.saveResults('r2');
    expect(saveRun).not.toHaveBeenCalled();
  });

  it('saveResults surfaces an error toast on failure and keeps the card open', async () => {
    const saveRun = jest.fn().mockResolvedValue({ ok: false, error: new AppError('bad', 'http', 400) });
    const { vm, notify } = setup({ saveRun });
    await vm.load('e1');
    vm.openResults('r1');
    vm.setTime('r1', 's1', '0:24.56');
    await vm.saveResults('r1');
    expect(notify.error).toHaveBeenCalled();
    expect(vm.isOpen('r1')).toBe(true);
  });

  it('a non-manager cannot open or edit results', async () => {
    const saveRun = jest.fn();
    const { vm } = setup({ role: 'swimmer', saveRun });
    await vm.load('e1');
    vm.openResults('r1');
    expect(vm.isOpen('r1')).toBe(false);
    vm.setTime('r1', 's1', '0:24.56');
    expect(vm.timeInput('r1', 's1')).toBe('');
    await vm.saveResults('r1');
    expect(saveRun).not.toHaveBeenCalled();
  });

  it('sets error when a source fails to load', async () => {
    const { vm } = setup();
    // override results run to fail
    TestBed.resetTestingModule();
    const failing = jest.fn().mockResolvedValue({ ok: false, error: new AppError('x', 'http', 404) });
    TestBed.configureTestingModule({
      providers: [
        RaceResultsViewModel,
        { provide: LoadRaceScheduleUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(schedule)) } },
        { provide: LoadResultsUseCase, useValue: { run: failing } },
        { provide: LoadStrokesUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(strokes)) } },
        { provide: LoadDistancesUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(distances)) } },
        { provide: ListSwimmersUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(roster)) } },
        { provide: LanguageStore, useValue: { lang: () => 'en' } },
        { provide: AuthSessionStore, useValue: { role: () => 'head_coach' } },
        { provide: NotificationService, useValue: { success: jest.fn(), error: jest.fn() } },
        { provide: TranslateService, useValue: { t: (k: string) => k } },
      ],
    });
    const vm2 = TestBed.inject(RaceResultsViewModel);
    await vm2.load('e1');
    expect(vm2.error()).toBe(true);
    expect(vm2.loaded()).toBe(false);
  });

  it('ensureLoaded only loads once per event', async () => {
    const { vm, schedRun } = setup();
    await vm.ensureLoaded('e1');
    await vm.ensureLoaded('e1');
    expect(schedRun).toHaveBeenCalledTimes(1);
  });
});
```

- [ ] **Step 4: Run — verify pass.** Do NOT commit.

Run: `cd frontend && npx jest src/app/features/championships/testing/presentation/pages/championship-detail/race-results.viewmodel.spec.ts`
Expected: PASS.

---

### Task 10: Frontend — page wiring, template sections, i18n

**Files:**
- Modify: `frontend/src/app/features/championships/presentation/pages/championship-detail/championship-detail.page.ts`
- Modify: `frontend/src/app/features/championships/presentation/pages/championship-detail/championship-detail.page.html`
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`
- Modify (fix): `frontend/src/app/features/championships/testing/presentation/pages/championship-detail/championship-detail.page.spec.ts`

**Interfaces:**
- Consumes: `RaceResultsViewModel` (Task 9).
- Produces: Finished + Results tabs enabled and rendered; `resultsVm` on the page component.

- [ ] **Step 1: Update the page spec first (failing).** In `championship-detail.page.spec.ts`:
  - Add the import: `import { RaceResultsViewModel } from '@features/championships/presentation/pages/championship-detail/race-results.viewmodel';`
  - In `setup()`: add `const resultsVm = { ensureLoaded: jest.fn() };`, add `{ provide: RaceResultsViewModel, useValue: resultsVm }` to the `.overrideComponent` providers set, and return `resultsVm`.
  - In the big render test's `.overrideComponent`, add `{ provide: RaceResultsViewModel, useValue: { loading: () => false, loaded: () => false, error: () => false, finishedRaces: () => [], resultRaces: () => [], finishedCount: () => 0, canManage: () => true, ensureLoaded: jest.fn(), isOpen: () => false, timeInput: () => '', openResults: jest.fn(), setTime: jest.fn(), saveResults: jest.fn(), formatTime: (n: number) => String(n) } }` to the providers set.
  - Replace the first test to assert all four tabs are enabled:

```ts
  it('enables all four detail tabs', () => {
    const { c } = setup();
    expect(c.isEnabled('enrollment')).toBe(true);
    expect(c.isEnabled('days')).toBe(true);
    expect(c.isEnabled('finished')).toBe(true);
    expect(c.isEnabled('results')).toBe(true);
  });
```

  - Replace the "ignores clicks on a disabled tab" test (no tab is disabled now) with a lazy-load assertion:

```ts
  it('lazy-loads results on first activation of the finished tab', () => {
    const { c, detailVm, resultsVm } = setup();
    c.ngOnInit();
    c.onTab('finished');
    expect(detailVm.setTab).toHaveBeenCalledWith('finished');
    expect(resultsVm.ensureLoaded).toHaveBeenCalledWith('e1');
  });
```

  - In the big render test, delete the assertion block that expects `finished`/`results` buttons to be `disabled` (lines asserting `disabled` arrayContaining the finished/results tab labels) — those tabs are now enabled.

- [ ] **Step 2: Run the page spec — verify fail**

Run: `cd frontend && npx jest src/app/features/championships/testing/presentation/pages/championship-detail/championship-detail.page.spec.ts`
Expected: FAIL (tabs still disabled / `resultsVm` not on component).

- [ ] **Step 3: Update `championship-detail.page.ts`**
  - Add the import: `import { RaceResultsViewModel } from './race-results.viewmodel';`
  - Add `RaceResultsViewModel` to the component `providers` array (`providers: [CompetitionDaysViewModel, RaceResultsViewModel]`).
  - Add the field: `readonly resultsVm = inject(RaceResultsViewModel);`
  - Change `enabledTabs` and the stale comment:

```ts
  // All four tabs are live.
  protected readonly enabledTabs = new Set<DetailTab>(['enrollment', 'days', 'finished', 'results']);
```

  - Extend `onTab`:

```ts
  onTab(key: DetailTab): void {
    if (!this.isEnabled(key)) return;
    this.vm.setTab(key);
    if (key === 'days') {
      const c = this.vm.championship();
      void this.daysVm.ensureLoaded(this.eventId, c?.startDate ?? '', c?.endDate ?? '');
    } else if (key === 'finished' || key === 'results') {
      void this.resultsVm.ensureLoaded(this.eventId);
    }
  }
```

- [ ] **Step 4: Add the two template sections** to `championship-detail.page.html`. Insert them **after** the `}` that closes the `@if (vm.activeTab() === 'days')` block and **before** the `}` that closes the enclosing `@else if (vm.championship(); as c) {` block (i.e. between the last two lines `}` and `}` that precede the final `</div>`). Both new sections are siblings of the enrollment/days `@if` blocks.

```html
    @if (vm.activeTab() === 'finished') {
      <section class="rounded-2xl border border-border bg-surface shadow-sm">
        @if (resultsVm.loading()) {
          <p class="p-6 text-center text-sm text-text-secondary">{{ 'championships.detail.loading' | translate }}</p>
        } @else if (resultsVm.error()) {
          <p class="p-6 text-center text-sm text-danger">{{ 'championships.detail.error' | translate }}</p>
        } @else if (resultsVm.loaded()) {
          <p class="border-b border-border p-4 text-xs text-text-secondary">{{ 'championships.finished.helper' | translate }}</p>
          @if (resultsVm.finishedRaces().length === 0) {
            <p class="p-8 text-center text-sm text-text-secondary">{{ 'championships.finished.empty' | translate }}</p>
          } @else {
            <div class="space-y-4 p-5">
              @for (race of resultsVm.finishedRaces(); track race.raceSessionId) {
                <div class="rounded-xl border border-border">
                  <div class="flex flex-wrap items-center justify-between gap-3 border-b border-border bg-muted/30 p-4">
                    <div>
                      <p class="font-medium text-ink">{{ race.raceName }}</p>
                      <p class="text-xs text-text-secondary">
                        {{ race.dayLabel }} · {{ race.scheduledTime || '—' }} · {{ race.swimmers.length }} {{ 'championships.finished.swimmers' | translate }}
                      </p>
                    </div>
                    @if (resultsVm.canManage()) {
                      <button type="button" (click)="resultsVm.openResults(race.raceSessionId)"
                              [attr.aria-expanded]="resultsVm.isOpen(race.raceSessionId)"
                              class="inline-flex h-9 items-center rounded-lg border border-border px-3 text-sm font-medium text-text-secondary hover:text-primary">
                        {{ (resultsVm.isOpen(race.raceSessionId) ? 'championships.finished.cancel' : 'championships.finished.enter') | translate }}
                      </button>
                    }
                  </div>
                  @if (resultsVm.canManage() && resultsVm.isOpen(race.raceSessionId)) {
                    <div class="space-y-3 p-4">
                      @if (race.swimmers.length === 0) {
                        <p class="text-sm text-text-secondary">{{ 'championships.finished.noSwimmers' | translate }}</p>
                      } @else {
                        @for (s of race.swimmers; track s.id) {
                          <div class="flex items-center justify-between gap-3">
                            <span class="text-sm text-ink">{{ s.name }}</span>
                            <input type="text" inputmode="decimal"
                                   [ngModel]="resultsVm.timeInput(race.raceSessionId, s.id)"
                                   (ngModelChange)="resultsVm.setTime(race.raceSessionId, s.id, $event)"
                                   placeholder="0:00.00" [attr.aria-label]="s.name"
                                   class="h-9 w-28 rounded-md border border-border bg-surface px-2 text-sm text-ink" />
                          </div>
                        }
                        <div class="flex justify-end">
                          <button type="button" (click)="resultsVm.saveResults(race.raceSessionId)" [disabled]="resultsVm.saving()"
                                  class="inline-flex h-9 items-center rounded-lg bg-primary px-4 text-sm font-medium text-white hover:bg-primary/90 disabled:opacity-50">
                            {{ 'championships.finished.save' | translate }}
                          </button>
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
    @if (vm.activeTab() === 'results') {
      <section class="rounded-2xl border border-border bg-surface shadow-sm">
        @if (resultsVm.loading()) {
          <p class="p-6 text-center text-sm text-text-secondary">{{ 'championships.detail.loading' | translate }}</p>
        } @else if (resultsVm.error()) {
          <p class="p-6 text-center text-sm text-danger">{{ 'championships.detail.error' | translate }}</p>
        } @else if (resultsVm.loaded()) {
          @if (resultsVm.resultRaces().length === 0) {
            <p class="p-8 text-center text-sm text-text-secondary">{{ 'championships.results.empty' | translate }}</p>
          } @else {
            <div class="space-y-4 p-5">
              @for (race of resultsVm.resultRaces(); track race.raceSessionId) {
                <div class="rounded-xl border border-border">
                  <div class="border-b border-border bg-muted/30 p-4">
                    <p class="font-medium text-ink">{{ race.raceName }}</p>
                    <p class="text-xs text-text-secondary">{{ race.dayLabel }}</p>
                  </div>
                  <table class="w-full text-sm">
                    <thead class="border-b border-border text-xs text-text-secondary">
                      <tr>
                        <th class="p-2 text-start">{{ 'championships.results.rank' | translate }}</th>
                        <th class="p-2 text-start">{{ 'championships.results.swimmer' | translate }}</th>
                        <th class="p-2 text-start">{{ 'championships.results.time' | translate }}</th>
                        <th class="p-2 text-start">{{ 'championships.results.pb' | translate }}</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-border">
                      @for (e of race.entries; track e.rank) {
                        <tr>
                          <td class="p-2 font-bold text-primary">{{ e.rank }}</td>
                          <td class="p-2 text-ink">{{ e.swimmerName }}</td>
                          <td class="p-2 tabular-nums text-ink">{{ resultsVm.formatTime(e.timeMs) }}</td>
                          <td class="p-2">
                            @if (e.isPersonalBest) {
                              <span class="rounded-full bg-primary/10 px-2 py-0.5 text-xs font-bold text-primary">{{ 'championships.results.pbBadge' | translate }}</span>
                            }
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              }
            </div>
          }
        }
      </section>
    }
```

- [ ] **Step 5: Add i18n `finished` + `results` blocks** to the `championships` object in `en.json` (place after the `days` block; mind the trailing comma after `days`):

```json
    "finished": {
      "helper": "Finished races: races whose start time has passed and are waiting on results.",
      "empty": "No finished races. Races appear here once their start time has passed.",
      "swimmers": "swimmers",
      "enter": "Enter results",
      "cancel": "Cancel",
      "save": "Save results",
      "noSwimmers": "No swimmers assigned to this race.",
      "saved": "Results saved",
      "saveFailed": "Couldn't save the results."
    },
    "results": {
      "empty": "No results recorded yet.",
      "rank": "Rank",
      "swimmer": "Swimmer",
      "time": "Time",
      "pb": "PB",
      "pbBadge": "PB"
    }
```

- [ ] **Step 6: Add the same keys to `ar.json`** (Arabic):

```json
    "finished": {
      "helper": "السباقات المنتهية: سباقات انقضى وقت بدايتها وتنتظر إدخال النتائج.",
      "empty": "لا سباقات منتهية. ستظهر السباقات هنا تلقائياً بعد انقضاء وقت بدايتها.",
      "swimmers": "سباح",
      "enter": "إدخال النتائج",
      "cancel": "إلغاء",
      "save": "حفظ النتائج",
      "noSwimmers": "لم يُسند أي سباح لهذا السباق.",
      "saved": "تم حفظ النتائج",
      "saveFailed": "تعذّر حفظ النتائج."
    },
    "results": {
      "empty": "لا نتائج مسجلة بعد.",
      "rank": "الترتيب",
      "swimmer": "السباح",
      "time": "الوقت",
      "pb": "أفضل رقم",
      "pbBadge": "أفضل رقم"
    }
```

- [ ] **Step 7: Run the page spec — verify pass**

Run: `cd frontend && npx jest src/app/features/championships/testing/presentation/pages/championship-detail/championship-detail.page.spec.ts`
Expected: PASS.

- [ ] **Step 8: Checkpoint** — full frontend suite + typecheck green. Do NOT commit.

```bash
cd frontend && npx jest src/app/features/championships && npx tsc -p tsconfig.app.json --noEmit
```
Expected: PASS / no type errors. (Confirm the `en.json`/`ar.json` edits are valid JSON — a syntax error breaks the whole suite.)

---

### Task 11: Migration to Aiven + seed data

**Files:**
- Create: `scripts/seed-race-results-aiven.sql`

**Interfaces:**
- Consumes: the already-applied `seed-competition-schedule-aiven.sql` (event `…3301`, race `…660001` = 50m Freestyle with 2 assigned swimmers, race `…660002` = 100m Backstroke with 2 assigned swimmers).

- [ ] **Step 1: Apply the migration to Aiven `Swimming_Production`.** Stop the backend API (DLL lock), then run (substituting the Aiven password — ask the user; do not store it):

```bash
cd backend
dotnet ef database update \
  --project src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure \
  --startup-project Kheprx.BaseBackend.Api \
  --context ChampionshipsDbContext \
  --connection "Host=kheprx-service-kheprx.b.aivencloud.com;Port=14647;Database=Swimming_Production;Username=avnadmin;Password=<AIVEN_PASSWORD>;SslMode=Require;Trust Server Certificate=true"
```

- [ ] **Step 2: Verify the migration applied** (0 pending):

```bash
dotnet ef migrations list --project src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure --startup-project Kheprx.BaseBackend.Api --context ChampionshipsDbContext --connection "Host=…;Database=Swimming_Production;…"
```
Expected: `CreateRaceResultTable` present, none marked pending.

- [ ] **Step 3: Write `scripts/seed-race-results-aiven.sql`**

```sql
-- seed-race-results-aiven.sql
-- Records finish times for one seeded race on the National Junior event (…3301), leaving
-- another seeded race empty, so both tabs are populated after implementation:
--   50m Freestyle heats (…660001): its 2 assigned swimmers get times  -> Results tab
--   100m Backstroke heats (…660002): left with no results             -> Finished races tab
-- Depends on seed-competition-schedule-aiven.sql having run (races + assignments exist).
-- Idempotent. Inserts nothing if the race, its assignments, or an app_user are missing.
-- NOTE: EF column names are PascalCase -> must be double-quoted in Postgres.

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- recorded_by is not surfaced in the UI; any existing app_user is fine.
WITH recorder AS (
  SELECT "Id" AS id FROM identity.app_user ORDER BY "Id" LIMIT 1
),
-- The 50m Freestyle race's assigned swimmers, ordered deterministically.
entrants AS (
  SELECT a."SwimmerId" AS swimmer_id,
         row_number() OVER (ORDER BY a."SwimmerId") AS rn
  FROM championships.race_assignment a
  WHERE a."RaceSessionId" = '66666666-6666-6666-6666-666666660001'
)
INSERT INTO championships.race_result
  ("Id","RaceSessionId","SwimmerId","TimeMs","Points","IsPersonalBest","RecordedBy")
SELECT
  v."Id",
  '66666666-6666-6666-6666-666666660001'::uuid,
  entrants.swimmer_id,
  v."TimeMs",
  0,
  true,
  (SELECT id FROM recorder)
FROM (VALUES
  ('77777777-7777-7777-7777-777777770001'::uuid, 1, 24560),   -- 0:24.56
  ('77777777-7777-7777-7777-777777770002'::uuid, 2, 25890)    -- 0:25.89
) AS v("Id", rn, "TimeMs")
JOIN entrants ON entrants.rn = v.rn
WHERE EXISTS (SELECT 1 FROM recorder)
ON CONFLICT ("RaceSessionId","SwimmerId") DO NOTHING;

COMMIT;

-- Verify (expect r=…660001 -> 2 results, r=…660002 -> 0):
--   SELECT rs."Id" AS session, count(rr.*) AS results
--   FROM championships.race_session rs
--   JOIN championships.competition_day d ON d."Id" = rs."DayId"
--   LEFT JOIN championships.race_result rr ON rr."RaceSessionId" = rs."Id"
--   WHERE d."EventId" = '33333333-3333-3333-3333-333333333301'
--   GROUP BY rs."Id" ORDER BY rs."Id";
```

- [ ] **Step 4: Run the seed against Aiven** (psql or the project's usual runner), then run the verify query. Expected: race `…660001` → 2 results, race `…660002` → 0 results.

- [ ] **Step 5: Manual end-to-end check.** Start the API + frontend, open `/championships/…3301`:
  - **Finished races** tab shows the 100m Backstroke race (2 swimmers, awaiting). Enter a time for each swimmer and Save → the race moves to Results.
  - **Results** tab shows the 50m Freestyle with two ranked rows (0:24.56 rank 1 with a PB badge, 0:25.89 rank 2).

- [ ] **Step 6: Checkpoint** — feature complete and verified against Aiven. Do NOT commit (await the user's go-ahead).

---

## Notes for the executor

- **Known limitation (documented in the spec, do not fix here):** re-saving Competition Days regenerates `race_session` ids and would orphan existing `race_result` rows. The seed/verify flow never re-saves, so it is unaffected.
- If `dotnet ef` fails with a file-lock error, a backend API instance is still running — stop it and retry.
- The `en.json`/`ar.json` files already contain a `championships.days` block; add `finished`/`results` as **siblings** of `days` inside `championships` — do not nest them under `days`.
- **Intentional simplification vs the spec:** the spec mentions a `finishedCount()` badge on the Finished tab label. Because the tab strip is a generic loop and `RaceResultsViewModel` loads lazily (the count is unknown until the tab is first opened), the badge is **not** wired into the strip. `finishedCount()` still exists on the VM (used in tests and available for a future eager-count enhancement). If the user wants the badge, it needs an eager count fetch on page load — out of scope here.
