# Swimmer profile — Championships tab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable the already-present-but-disabled **Championships** tab on the swimmer profile page — a read-only dropdown of every championship the swimmer joined, and a Race · Day · Time (+PB) table of that swimmer's results in the selected one.

**Architecture:** One new read endpoint on the existing `ChampionshipsController` (`GET /api/championships/swimmer/{swimmerId}/history`) backed by a new `IChampionshipService.GetSwimmerHistoryAsync` and three new repository query methods over the existing `championships` tables (no new tables, no migration). The frontend adds a championships read slice (DTO + validator + mapper + model + use case + repository method) and wires it into `SwimmerProfileViewModel` following the existing lazy per-tab load pattern; the page resolves stroke/distance names client-side via the reference lookups.

**Tech Stack:** Backend — .NET 10, EF Core (Npgsql), xUnit + Moq, EF InMemory for repo tests. Frontend — Angular (standalone, signals), Jest, Tailwind.

**Spec:** `docs/superpowers/specs/2026-09-24-swimmer-profile-championships-tab-design.md`

## Global Constraints

- **Branch `feat/championships`. Do NOT commit or push anything, at any step — the user commits manually.** Where a task would normally end with a commit, instead end by running that task's tests green and leaving the working tree staged/unstaged as-is. (Overrides the commit steps implied by the execution sub-skill.)
- **No migration, no seed.** All `championships` tables already exist and are populated on Aiven `Swimming_Production`.
- **Loose Guids across modules** — no EF navigation / DB FK; the new query joins tables inside `ChampionshipsDbContext` only.
- **EF column convention:** table/schema snake_case via configuration; columns are PascalCase property names.
- **Read-only feature** — no add/edit/delete, no auth-role change; the tab is visible to anyone who can open the profile.
- **Backend running locks build DLLs** — stop the API/dev server before `dotnet test`/`dotnet build` (see project memory).
- **Client-side name resolution** — the endpoint returns `distanceId`/`strokeId`; the client resolves `"50m Freestyle"` via `LoadDistancesUseCase` + `LoadStrokesUseCase`.

## Review Focus

- **Swimmer with zero enrollments** → the endpoint returns `[]`; the tab shows a "no championships joined" empty state (no dropdown, no card), not a blank/broken panel. *(Pinned in Task 1 service test + Task 6 viewmodel test + Task 7 markup.)*
- **Enrolled championship with zero recorded results** → it still appears in the dropdown and its card renders with an empty "No results recorded yet" table (`races: []`). *(Pinned in Task 1 service test + Task 6 viewmodel test.)*
- **A race whose `distanceId`/`strokeId` is absent from the loaded lookups** → the race name degrades to the known part (or empty) with no crash. *(Pinned in Task 4 `resolveRaceName` test.)*
- **Ordering** — events newest-first by start date; within an event, races by day date then scheduled time (null times last). *(Pinned in Task 1 service test.)*
- **Invalid / malformed history payload from the server** (missing `races` array, non-numeric `timeMs`) → the use case rejects it as a validation error instead of surfacing a half-built model. *(Pinned in Task 4 validator test + Task 5 use-case test.)*

---

## Task 1: Backend — history DTOs, service method, repo interfaces (service unit-tested)

**Files:**
- Create: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Application/DTOs/SwimmerChampionshipHistoryDtos.cs`
- Modify: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Domain/Repositories/RaceResultData.cs` (add `SwimmerRaceLineRow`)
- Modify: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Domain/Repositories/IChampionshipEnrollmentRepository.cs`
- Modify: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Domain/Repositories/ICompetitionEventRepository.cs`
- Modify: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Domain/Repositories/IRaceResultRepository.cs`
- Modify: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Application/Services/Interfaces/IChampionshipService.cs`
- Modify: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Application/Services/ChampionshipService.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Services/SwimmerHistoryServiceTests.cs`

**Interfaces:**
- Produces:
  - `record ChampionshipSwimmerRaceDto(string DayLabelEn, string? DayLabelAr, Guid DistanceId, Guid StrokeId, int TimeMs, bool IsPersonalBest)`
  - `record ChampionshipSwimmerHistoryDto(Guid EventId, string NameEn, string? NameAr, DateOnly StartDate, DateOnly EndDate, string LocationEn, string? LocationAr, IReadOnlyList<ChampionshipSwimmerRaceDto> Races)`
  - `record SwimmerRaceLineRow(Guid EventId, string DayLabelEn, string? DayLabelAr, DateOnly DayDate, TimeOnly? ScheduledTime, Guid DistanceId, Guid StrokeId, int TimeMs, bool IsPersonalBest)`
  - `IChampionshipEnrollmentRepository.ListEventIdsBySwimmerAsync(Guid swimmerId, CancellationToken ct = default) : Task<IReadOnlyList<Guid>>`
  - `ICompetitionEventRepository.ListByIdsAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct = default) : Task<IReadOnlyList<CompetitionEvent>>`
  - `IRaceResultRepository.GetSwimmerRaceLinesAsync(Guid swimmerId, CancellationToken ct = default) : Task<IReadOnlyList<SwimmerRaceLineRow>>`
  - `IChampionshipService.GetSwimmerHistoryAsync(Guid swimmerId, CancellationToken ct = default) : Task<IReadOnlyList<ChampionshipSwimmerHistoryDto>>`
- Consumes: existing `CompetitionEvent` entity; existing repo mocks (`RaceResultServiceTests` shows the `Build()` mock harness — reuse its shape).

- [ ] **Step 1: Add the DTOs**

Create `SwimmerChampionshipHistoryDtos.cs`:

```csharp
namespace Kheprx.BaseBackend.Championships.Application.DTOs;

/// <summary>One race the swimmer swam in a championship (name resolved client-side from the ids).</summary>
public sealed record ChampionshipSwimmerRaceDto(
    string DayLabelEn,
    string? DayLabelAr,
    Guid DistanceId,
    Guid StrokeId,
    int TimeMs,
    bool IsPersonalBest);

/// <summary>A championship the swimmer joined, with the swimmer's races (empty when none recorded yet).</summary>
public sealed record ChampionshipSwimmerHistoryDto(
    Guid EventId,
    string NameEn,
    string? NameAr,
    DateOnly StartDate,
    DateOnly EndDate,
    string LocationEn,
    string? LocationAr,
    IReadOnlyList<ChampionshipSwimmerRaceDto> Races);
```

- [ ] **Step 2: Add the domain row record**

In `RaceResultData.cs`, append:

```csharp
// Flat join row for a swimmer's results across events (race_result -> race_session -> competition_day).
public sealed record SwimmerRaceLineRow(
    Guid EventId, string DayLabelEn, string? DayLabelAr, DateOnly DayDate,
    TimeOnly? ScheduledTime, Guid DistanceId, Guid StrokeId, int TimeMs, bool IsPersonalBest);
```

- [ ] **Step 3: Add the repository interface methods**

In `IChampionshipEnrollmentRepository.cs`, add inside the interface:

```csharp
Task<IReadOnlyList<Guid>> ListEventIdsBySwimmerAsync(Guid swimmerId, CancellationToken ct = default);
```

In `ICompetitionEventRepository.cs`, add:

```csharp
Task<IReadOnlyList<CompetitionEvent>> ListByIdsAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct = default);
```

In `IRaceResultRepository.cs`, add:

```csharp
Task<IReadOnlyList<SwimmerRaceLineRow>> GetSwimmerRaceLinesAsync(Guid swimmerId, CancellationToken ct = default);
```

- [ ] **Step 4: Add the service method to the interface**

In `IChampionshipService.cs`, add:

```csharp
Task<IReadOnlyList<ChampionshipSwimmerHistoryDto>> GetSwimmerHistoryAsync(Guid swimmerId, CancellationToken ct = default);
```

- [ ] **Step 5: Write the failing service tests**

Create `SwimmerHistoryServiceTests.cs`:

```csharp
using Kheprx.BaseBackend.Championships.Application.Services;
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Services;

public class SwimmerHistoryServiceTests
{
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

    private static CompetitionEvent Ev(string name, DateOnly start, DateOnly end)
        => new(name, null, start, end, "Cairo", null, Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task Returns_empty_when_swimmer_has_no_enrollments()
    {
        var (svc, _, enr, _, _) = Build();
        enr.Setup(r => r.ListEventIdsBySwimmerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Array.Empty<Guid>());

        var history = await svc.GetSwimmerHistoryAsync(Guid.NewGuid());

        Assert.Empty(history);
    }

    [Fact]
    public async Task Enrolled_event_with_no_results_appears_with_empty_races()
    {
        var (svc, events, enr, _, results) = Build();
        var swimmer = Guid.NewGuid();
        var ev = Ev("Spring Invitational", new DateOnly(2023, 4, 8), new DateOnly(2023, 4, 9));
        enr.Setup(r => r.ListEventIdsBySwimmerAsync(swimmer, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { ev.Id });
        events.Setup(r => r.ListByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { ev });
        results.Setup(r => r.GetSwimmerRaceLinesAsync(swimmer, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<SwimmerRaceLineRow>());

        var history = await svc.GetSwimmerHistoryAsync(swimmer);

        Assert.Single(history);
        Assert.Equal(ev.Id, history[0].EventId);
        Assert.Empty(history[0].Races);
    }

    [Fact]
    public async Task Orders_events_newest_first_and_races_by_day_then_time()
    {
        var (svc, events, enr, _, results) = Build();
        var swimmer = Guid.NewGuid();
        var older = Ev("Regional", new DateOnly(2023, 10, 20), new DateOnly(2023, 10, 20));
        var newer = Ev("National", new DateOnly(2023, 11, 15), new DateOnly(2023, 11, 16));
        enr.Setup(r => r.ListEventIdsBySwimmerAsync(swimmer, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { older.Id, newer.Id });
        events.Setup(r => r.ListByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { older, newer });
        var dist = Guid.NewGuid(); var stroke = Guid.NewGuid();
        results.Setup(r => r.GetSwimmerRaceLinesAsync(swimmer, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            // newer event, day 2 then day 1 out of order; day 1 has two races out of time order
            new SwimmerRaceLineRow(newer.Id, "Day 2", null, new DateOnly(2023, 11, 16), new TimeOnly(10, 0), dist, stroke, 61770, false),
            new SwimmerRaceLineRow(newer.Id, "Day 1", null, new DateOnly(2023, 11, 15), new TimeOnly(9, 30), dist, stroke, 118120, false),
            new SwimmerRaceLineRow(newer.Id, "Day 1", null, new DateOnly(2023, 11, 15), new TimeOnly(9, 0), dist, stroke, 52340, true),
        });

        var history = await svc.GetSwimmerHistoryAsync(swimmer);

        Assert.Equal(new[] { "National", "Regional" }, history.Select(h => h.NameEn));   // newest first
        Assert.Equal(new[] { 52340, 118120, 61770 }, history[0].Races.Select(r => r.TimeMs)); // day1@9:00, day1@9:30, day2@10:00
        Assert.True(history[0].Races[0].IsPersonalBest);
    }
}
```

- [ ] **Step 6: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests --filter FullyQualifiedName~SwimmerHistoryServiceTests`
Expected: FAIL to compile / `GetSwimmerHistoryAsync` not implemented.

- [ ] **Step 7: Implement the service method**

In `ChampionshipService.cs`, add the method (uses the existing injected `_enrollments`, `_events`, `_results`):

```csharp
public async Task<IReadOnlyList<ChampionshipSwimmerHistoryDto>> GetSwimmerHistoryAsync(Guid swimmerId, CancellationToken ct = default)
{
    var eventIds = await _enrollments.ListEventIdsBySwimmerAsync(swimmerId, ct);
    if (eventIds.Count == 0) return Array.Empty<ChampionshipSwimmerHistoryDto>();

    var events = await _events.ListByIdsAsync(eventIds, ct);
    var lines = await _results.GetSwimmerRaceLinesAsync(swimmerId, ct);
    var linesByEvent = lines.GroupBy(l => l.EventId).ToDictionary(g => g.Key, g => g.ToList());

    return events
        .OrderByDescending(e => e.StartDate).ThenByDescending(e => e.EndDate).ThenBy(e => e.NameEn)
        .Select(e =>
        {
            var races = (linesByEvent.TryGetValue(e.Id, out var ls) ? ls : new List<SwimmerRaceLineRow>())
                .OrderBy(l => l.DayDate).ThenBy(l => l.ScheduledTime ?? TimeOnly.MinValue)
                .Select(l => new ChampionshipSwimmerRaceDto(l.DayLabelEn, l.DayLabelAr, l.DistanceId, l.StrokeId, l.TimeMs, l.IsPersonalBest))
                .ToList();
            return new ChampionshipSwimmerHistoryDto(e.Id, e.NameEn, e.NameAr, e.StartDate, e.EndDate, e.LocationEn, e.LocationAr, races);
        })
        .ToList();
}
```

Add `using Kheprx.BaseBackend.Championships.Domain.Repositories;` if not already present (it is).

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests --filter FullyQualifiedName~SwimmerHistoryServiceTests`
Expected: PASS (3 tests). Do NOT commit.

---

## Task 2: Backend — repository implementations (EF InMemory tested)

**Files:**
- Modify: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure/Repositories/ChampionshipEnrollmentRepository.cs`
- Modify: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure/Repositories/CompetitionEventRepository.cs`
- Modify: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure/Repositories/RaceResultRepository.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Repositories/SwimmerHistoryRepositoryTests.cs`

**Interfaces:**
- Consumes: the three interface methods from Task 1.
- Produces: their concrete implementations (same signatures).

- [ ] **Step 1: Write the failing repository tests**

Create `SwimmerHistoryRepositoryTests.cs`:

```csharp
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Kheprx.BaseBackend.Championships.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Repositories;

public class SwimmerHistoryRepositoryTests
{
    private static ChampionshipsDbContext NewDb()
        => new(new DbContextOptionsBuilder<ChampionshipsDbContext>()
            .UseInMemoryDatabase($"history-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task ListEventIdsBySwimmerAsync_returns_distinct_events_for_the_swimmer()
    {
        await using var db = NewDb();
        var swimmer = Guid.NewGuid();
        var other = Guid.NewGuid();
        var eventA = Guid.NewGuid();
        var eventB = Guid.NewGuid();
        db.Enrollments.Add(new ChampionshipEnrollment(eventA, swimmer));
        db.Enrollments.Add(new ChampionshipEnrollment(eventB, swimmer));
        db.Enrollments.Add(new ChampionshipEnrollment(eventA, other));
        await db.SaveChangesAsync();
        var repo = new ChampionshipEnrollmentRepository(db);

        var ids = await repo.ListEventIdsBySwimmerAsync(swimmer);

        Assert.Equal(2, ids.Count);
        Assert.Contains(eventA, ids);
        Assert.Contains(eventB, ids);
    }

    [Fact]
    public async Task ListByIdsAsync_returns_only_the_requested_events()
    {
        await using var db = NewDb();
        var e1 = new CompetitionEvent("A", null, new DateOnly(2023, 1, 1), new DateOnly(2023, 1, 2), "X", null, Guid.NewGuid(), Guid.NewGuid());
        var e2 = new CompetitionEvent("B", null, new DateOnly(2023, 2, 1), new DateOnly(2023, 2, 2), "Y", null, Guid.NewGuid(), Guid.NewGuid());
        db.CompetitionEvents.AddRange(e1, e2);
        await db.SaveChangesAsync();
        var repo = new CompetitionEventRepository(db);

        var got = await repo.ListByIdsAsync(new[] { e1.Id });

        Assert.Single(got);
        Assert.Equal(e1.Id, got[0].Id);
    }

    [Fact]
    public async Task ListByIdsAsync_returns_empty_for_empty_input()
    {
        await using var db = NewDb();
        var repo = new CompetitionEventRepository(db);
        Assert.Empty(await repo.ListByIdsAsync(Array.Empty<Guid>()));
    }

    [Fact]
    public async Task GetSwimmerRaceLinesAsync_joins_result_to_session_and_day_for_the_swimmer()
    {
        await using var db = NewDb();
        var swimmer = Guid.NewGuid();
        var other = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var dist = Guid.NewGuid();
        var stroke = Guid.NewGuid();
        var day = new CompetitionDay(eventId, "Day 1", "اليوم 1", new DateOnly(2023, 11, 15));
        db.CompetitionDays.Add(day);
        var session = new RaceSession(day.Id, stroke, dist, new TimeOnly(9, 0));
        db.RaceSessions.Add(session);
        db.RaceResults.Add(new RaceResult(session.Id, swimmer, 52340, 0, true, Guid.NewGuid()));
        db.RaceResults.Add(new RaceResult(session.Id, other, 60000, 0, false, Guid.NewGuid()));
        await db.SaveChangesAsync();
        var repo = new RaceResultRepository(db);

        var lines = await repo.GetSwimmerRaceLinesAsync(swimmer);

        Assert.Single(lines);
        var l = lines[0];
        Assert.Equal(eventId, l.EventId);
        Assert.Equal("Day 1", l.DayLabelEn);
        Assert.Equal("اليوم 1", l.DayLabelAr);
        Assert.Equal(new DateOnly(2023, 11, 15), l.DayDate);
        Assert.Equal(new TimeOnly(9, 0), l.ScheduledTime);
        Assert.Equal(dist, l.DistanceId);
        Assert.Equal(stroke, l.StrokeId);
        Assert.Equal(52340, l.TimeMs);
        Assert.True(l.IsPersonalBest);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests --filter FullyQualifiedName~SwimmerHistoryRepositoryTests`
Expected: FAIL to compile — methods not implemented.

- [ ] **Step 3: Implement `ListEventIdsBySwimmerAsync`**

In `ChampionshipEnrollmentRepository.cs`, add:

```csharp
public async Task<IReadOnlyList<Guid>> ListEventIdsBySwimmerAsync(Guid swimmerId, CancellationToken ct = default)
    => await _db.Enrollments.AsNoTracking()
          .Where(e => e.SwimmerId == swimmerId)
          .Select(e => e.EventId)
          .Distinct()
          .ToListAsync(ct);
```

- [ ] **Step 4: Implement `ListByIdsAsync`**

In `CompetitionEventRepository.cs`, add:

```csharp
public async Task<IReadOnlyList<CompetitionEvent>> ListByIdsAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct = default)
{
    if (eventIds.Count == 0) return Array.Empty<CompetitionEvent>();
    return await _db.CompetitionEvents.AsNoTracking()
        .Where(e => eventIds.Contains(e.Id))
        .ToListAsync(ct);
}
```

- [ ] **Step 5: Implement `GetSwimmerRaceLinesAsync`**

In `RaceResultRepository.cs`, add:

```csharp
public async Task<IReadOnlyList<SwimmerRaceLineRow>> GetSwimmerRaceLinesAsync(Guid swimmerId, CancellationToken ct = default)
{
    var query =
        from r in _db.RaceResults.AsNoTracking().Where(x => x.SwimmerId == swimmerId)
        join s in _db.RaceSessions.AsNoTracking() on r.RaceSessionId equals s.Id
        join d in _db.CompetitionDays.AsNoTracking() on s.DayId equals d.Id
        select new SwimmerRaceLineRow(
            d.EventId, d.LabelEn, d.LabelAr, d.DayDate,
            s.ScheduledTime, s.DistanceId, s.StrokeId, r.TimeMs, r.IsPersonalBest);

    return await query.ToListAsync(ct);
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests --filter FullyQualifiedName~SwimmerHistoryRepositoryTests`
Expected: PASS (4 tests). Do NOT commit.

---

## Task 3: Backend — controller endpoint + message (controller unit-tested)

**Files:**
- Modify: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Application/Resources/ChampionshipMessages.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/ChampionshipsController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/ChampionshipsControllerTests.cs` (add a test)

**Interfaces:**
- Consumes: `IChampionshipService.GetSwimmerHistoryAsync` (Task 1).
- Produces: `GET /api/championships/swimmer/{swimmerId:guid}/history` → `ApiResponse<IReadOnlyList<ChampionshipSwimmerHistoryDto>>`, always 200.

- [ ] **Step 1: Add the localized message**

In `ChampionshipMessages.cs`, add a class alongside the others:

```csharp
public static class SwimmerHistorySuccess
{
    public static string Retrieved(string lang) => lang switch { "ar" => "سجل البطولات", _ => "Championship history" };
}
```

- [ ] **Step 2: Write the failing controller test**

In `ChampionshipsControllerTests.cs`, add (the reference mock can return an empty status list — this endpoint does not resolve statuses):

```csharp
[Fact]
public async Task GetSwimmerHistory_returns_200_with_the_service_rows()
{
    var swimmerId = Guid.NewGuid();
    var eventId = Guid.NewGuid();
    var svc = new Mock<IChampionshipService>();
    svc.Setup(s => s.GetSwimmerHistoryAsync(swimmerId, It.IsAny<CancellationToken>()))
       .ReturnsAsync(new[]
       {
           new ChampionshipSwimmerHistoryDto(eventId, "National", null,
               new DateOnly(2023, 11, 15), new DateOnly(2023, 11, 16), "Cairo", null,
               new[] { new ChampionshipSwimmerRaceDto("Day 1", null, Guid.NewGuid(), Guid.NewGuid(), 52340, true) }),
       });
    var reference = new Mock<IReferenceService>();

    var controller = NewController(svc.Object, reference.Object);
    var result = await controller.GetSwimmerHistory(swimmerId, CancellationToken.None);

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var body = Assert.IsType<ApiResponse<IReadOnlyList<ChampionshipSwimmerHistoryDto>>>(ok.Value);
    Assert.Single(body.Data!);
    Assert.Equal(eventId, body.Data![0].EventId);
    Assert.Single(body.Data![0].Races);
    Assert.True(body.Data![0].Races[0].IsPersonalBest);
}

[Fact]
public async Task GetSwimmerHistory_returns_200_and_empty_list_for_a_swimmer_with_no_history()
{
    var svc = new Mock<IChampionshipService>();
    svc.Setup(s => s.GetSwimmerHistoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
       .ReturnsAsync(Array.Empty<ChampionshipSwimmerHistoryDto>());
    var controller = NewController(svc.Object, new Mock<IReferenceService>().Object);

    var result = await controller.GetSwimmerHistory(Guid.NewGuid(), CancellationToken.None);

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var body = Assert.IsType<ApiResponse<IReadOnlyList<ChampionshipSwimmerHistoryDto>>>(ok.Value);
    Assert.Empty(body.Data!);
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter FullyQualifiedName~GetSwimmerHistory`
Expected: FAIL to compile — `GetSwimmerHistory` action not defined.

- [ ] **Step 4: Add the controller action**

In `ChampionshipsController.cs`, add before the closing brace:

```csharp
/// <summary>Returns the swimmer's championship participation history — every enrolled event (newest first)
/// with the swimmer's races and times. Always 200 (empty list when the swimmer joined nothing).</summary>
[HttpGet("swimmer/{swimmerId:guid}/history")]
[ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ChampionshipSwimmerHistoryDto>>), StatusCodes.Status200OK)]
public async Task<ActionResult<ApiResponse<IReadOnlyList<ChampionshipSwimmerHistoryDto>>>> GetSwimmerHistory(Guid swimmerId, CancellationToken ct)
{
    var rows = await _service.GetSwimmerHistoryAsync(swimmerId, ct);
    return Ok(ApiResponse<IReadOnlyList<ChampionshipSwimmerHistoryDto>>.Success(
        ChampionshipMessages.SwimmerHistorySuccess.Retrieved(AppLanguage.Current), rows));
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter FullyQualifiedName~GetSwimmerHistory`
Expected: PASS (2 tests). Do NOT commit.

- [ ] **Step 6: Run the whole backend Championships + Api suites**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests` then `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests`
Expected: PASS (all green, including the pre-existing tests). Do NOT commit.

---

## Task 4: Frontend — data contract (DTO, validator, model, mapper, race-name resolver, repository method)

**Files:**
- Create: `frontend/src/app/features/championships/data/dto/swimmer-championship-history.dto.ts`
- Create: `frontend/src/app/features/championships/data/dto/swimmer-championship-history.mapper.ts`
- Create: `frontend/src/app/features/championships/domain/model/swimmer-championship-history.ts`
- Modify: `frontend/src/app/features/championships/domain/repositories/championships.repository.ts`
- Modify: `frontend/src/app/features/championships/data/repositories/championships.repository.impl.ts`
- Test: `frontend/src/app/features/championships/testing/data/dto/swimmer-championship-history.spec.ts`
- Test: `frontend/src/app/features/championships/testing/domain/model/swimmer-championship-history.spec.ts`
- Test (modify): `frontend/src/app/features/championships/testing/data/repositories/championships.repository.impl.spec.ts`

**Interfaces:**
- Produces:
  - `interface SwimmerChampionshipRace { dayLabelEn: string; dayLabelAr: string | null; distanceId: string; strokeId: string; timeMs: number; isPersonalBest: boolean }`
  - `interface SwimmerChampionshipHistory { eventId: string; nameEn: string; nameAr: string | null; startDate: string; endDate: string; locationEn: string; locationAr: string | null; races: SwimmerChampionshipRace[] }`
  - `resolveRaceName(distances: NameLookup[], strokes: NameLookup[], distanceId: string, strokeId: string, lang: string): string` where `NameLookup = { id: string; nameEn: string; nameAr: string | null }`
  - `isSwimmerChampionshipHistoryValid(data: unknown): data is SwimmerChampionshipHistoryData[]`
  - `toSwimmerChampionshipHistory(data: SwimmerChampionshipHistoryData[]): SwimmerChampionshipHistory[]`
  - `IChampionshipsRepository.getSwimmerChampionshipHistory(swimmerId: string): Promise<SwimmerChampionshipHistoryDtoRs>`

- [ ] **Step 1: Write the failing DTO/validator + mapper tests**

Create `testing/data/dto/swimmer-championship-history.spec.ts`:

```typescript
import { isSwimmerChampionshipHistoryValid } from '@features/championships/data/dto/swimmer-championship-history.dto';
import { toSwimmerChampionshipHistory } from '@features/championships/data/dto/swimmer-championship-history.mapper';

const valid = [{
  eventId: 'e1', nameEn: 'National', nameAr: null,
  startDate: '2023-11-15', endDate: '2023-11-16', locationEn: 'Cairo', locationAr: null,
  races: [{ dayLabelEn: 'Day 1', dayLabelAr: null, distanceId: 'd1', strokeId: 's1', timeMs: 52340, isPersonalBest: true }],
}];

describe('swimmer-championship-history dto', () => {
  it('accepts a well-formed payload', () => {
    expect(isSwimmerChampionshipHistoryValid(valid)).toBe(true);
    expect(isSwimmerChampionshipHistoryValid([])).toBe(true); // empty history is valid
  });

  it('rejects a non-array, a missing races array, and a non-numeric time', () => {
    expect(isSwimmerChampionshipHistoryValid(null)).toBe(false);
    expect(isSwimmerChampionshipHistoryValid([{ ...valid[0], races: undefined }])).toBe(false);
    expect(isSwimmerChampionshipHistoryValid([{ ...valid[0], races: [{ ...valid[0].races[0], timeMs: '52' }] }])).toBe(false);
  });

  it('maps a payload to the domain model preserving fields', () => {
    const model = toSwimmerChampionshipHistory(valid as never);
    expect(model[0].eventId).toBe('e1');
    expect(model[0].races[0].timeMs).toBe(52340);
    expect(model[0].races[0].isPersonalBest).toBe(true);
  });
});
```

Create `testing/domain/model/swimmer-championship-history.spec.ts`:

```typescript
import { resolveRaceName } from '@features/championships/domain/model/swimmer-championship-history';

const distances = [{ id: 'd1', nameEn: '50m', nameAr: '٥٠م' }];
const strokes = [{ id: 's1', nameEn: 'Freestyle', nameAr: 'حرة' }];

describe('resolveRaceName', () => {
  it('joins distance and stroke in the active language', () => {
    expect(resolveRaceName(distances, strokes, 'd1', 's1', 'en')).toBe('50m Freestyle');
    expect(resolveRaceName(distances, strokes, 'd1', 's1', 'ar')).toBe('٥٠م حرة');
  });

  it('degrades gracefully when a lookup id is missing (no crash)', () => {
    expect(resolveRaceName(distances, strokes, 'd1', 'unknown', 'en')).toBe('50m');
    expect(resolveRaceName([], [], 'd1', 's1', 'en')).toBe('');
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd frontend && npx jest src/app/features/championships/testing/data/dto/swimmer-championship-history.spec.ts src/app/features/championships/testing/domain/model/swimmer-championship-history.spec.ts`
Expected: FAIL — modules not found.

- [ ] **Step 3: Create the domain model + resolver**

Create `domain/model/swimmer-championship-history.ts`:

```typescript
export interface SwimmerChampionshipRace {
  dayLabelEn: string;
  dayLabelAr: string | null;
  distanceId: string;
  strokeId: string;
  timeMs: number;
  isPersonalBest: boolean;
}

export interface SwimmerChampionshipHistory {
  eventId: string;
  nameEn: string;
  nameAr: string | null;
  startDate: string; // 'YYYY-MM-DD'
  endDate: string;   // 'YYYY-MM-DD'
  locationEn: string;
  locationAr: string | null;
  races: SwimmerChampionshipRace[];
}

interface NameLookup { id: string; nameEn: string; nameAr: string | null; }

/** "<distance> <stroke>" in the active language; a missing lookup contributes nothing (no crash). */
export function resolveRaceName(
  distances: NameLookup[], strokes: NameLookup[], distanceId: string, strokeId: string, lang: string): string {
  const label = (items: NameLookup[], id: string): string => {
    const item = items.find((i) => i.id === id);
    if (!item) return '';
    return lang === 'ar' ? (item.nameAr ?? item.nameEn) : item.nameEn;
  };
  return [label(distances, distanceId), label(strokes, strokeId)].filter(Boolean).join(' ');
}
```

- [ ] **Step 4: Create the DTO + validator**

Create `data/dto/swimmer-championship-history.dto.ts`:

```typescript
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface SwimmerChampionshipRaceData {
  dayLabelEn: string;
  dayLabelAr: string | null;
  distanceId: string;
  strokeId: string;
  timeMs: number;
  isPersonalBest: boolean;
}
export interface SwimmerChampionshipHistoryData {
  eventId: string;
  nameEn: string;
  nameAr: string | null;
  startDate: string;
  endDate: string;
  locationEn: string;
  locationAr: string | null;
  races: SwimmerChampionshipRaceData[];
}
export interface SwimmerChampionshipHistoryDtoRs extends BaseResponseRs<SwimmerChampionshipHistoryData[]> {}

function isStr(v: unknown): v is string { return typeof v === 'string'; }
function isNum(v: unknown): v is number { return typeof v === 'number' && Number.isFinite(v); }

function isRace(r: unknown): r is SwimmerChampionshipRaceData {
  const x = r as SwimmerChampionshipRaceData;
  return !!x && typeof x === 'object'
    && isStr(x.dayLabelEn) && isStr(x.distanceId) && isStr(x.strokeId)
    && isNum(x.timeMs) && typeof x.isPersonalBest === 'boolean';
}

export function isSwimmerChampionshipHistoryValid(data: unknown): data is SwimmerChampionshipHistoryData[] {
  if (!Array.isArray(data)) return false;
  return data.every((c) => {
    const x = c as SwimmerChampionshipHistoryData;
    return !!x && typeof x === 'object'
      && isStr(x.eventId) && isStr(x.nameEn)
      && isStr(x.startDate) && isStr(x.endDate) && isStr(x.locationEn)
      && Array.isArray(x.races) && x.races.every(isRace);
  });
}
```

- [ ] **Step 5: Create the mapper**

Create `data/dto/swimmer-championship-history.mapper.ts`:

```typescript
import { SwimmerChampionshipHistoryData } from '@features/championships/data/dto/swimmer-championship-history.dto';
import { SwimmerChampionshipHistory } from '@features/championships/domain/model/swimmer-championship-history';

export function toSwimmerChampionshipHistory(data: SwimmerChampionshipHistoryData[]): SwimmerChampionshipHistory[] {
  return data.map((c) => ({
    eventId: c.eventId,
    nameEn: c.nameEn,
    nameAr: c.nameAr,
    startDate: c.startDate,
    endDate: c.endDate,
    locationEn: c.locationEn,
    locationAr: c.locationAr,
    races: c.races.map((r) => ({
      dayLabelEn: r.dayLabelEn,
      dayLabelAr: r.dayLabelAr,
      distanceId: r.distanceId,
      strokeId: r.strokeId,
      timeMs: r.timeMs,
      isPersonalBest: r.isPersonalBest,
    })),
  }));
}
```

- [ ] **Step 6: Add the repository method (interface + impl)**

In `domain/repositories/championships.repository.ts`, add the import and the method to the interface:

```typescript
import { SwimmerChampionshipHistoryDtoRs } from '@features/championships/data/dto/swimmer-championship-history.dto';
```
```typescript
  getSwimmerChampionshipHistory(swimmerId: string): Promise<SwimmerChampionshipHistoryDtoRs>;
```

In `data/repositories/championships.repository.impl.ts`, add the same import and the method:

```typescript
  getSwimmerChampionshipHistory(swimmerId: string): Promise<SwimmerChampionshipHistoryDtoRs> {
    return this.http.get<SwimmerChampionshipHistoryDtoRs>(`/api/championships/swimmer/${swimmerId}/history`);
  }
```

- [ ] **Step 7: Add the repository-impl test case**

In `testing/data/repositories/championships.repository.impl.spec.ts`, add inside the `describe`:

```typescript
  it('getSwimmerChampionshipHistory GETs the swimmer history endpoint', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.getSwimmerChampionshipHistory('sw1');
    expect(http.get).toHaveBeenCalledWith('/api/championships/swimmer/sw1/history');
  });
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `cd frontend && npx jest src/app/features/championships/testing/data/dto/swimmer-championship-history.spec.ts src/app/features/championships/testing/domain/model/swimmer-championship-history.spec.ts src/app/features/championships/testing/data/repositories/championships.repository.impl.spec.ts`
Expected: PASS. Do NOT commit.

---

## Task 5: Frontend — LoadSwimmerChampionshipHistoryUseCase

**Files:**
- Create: `frontend/src/app/features/championships/domain/usecases/load-swimmer-championship-history.use-case.ts`
- Test: `frontend/src/app/features/championships/testing/domain/usecases/load-swimmer-championship-history.use-case.spec.ts`

**Interfaces:**
- Consumes: `CHAMPIONSHIPS_REPOSITORY`, `isSwimmerChampionshipHistoryValid`, `toSwimmerChampionshipHistory` (Task 4).
- Produces: `LoadSwimmerChampionshipHistoryUseCase.run(swimmerId: string): Promise<Result<SwimmerChampionshipHistory[]>>`.

- [ ] **Step 1: Write the failing use-case test**

Create `testing/domain/usecases/load-swimmer-championship-history.use-case.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { LoadSwimmerChampionshipHistoryUseCase } from '@features/championships/domain/usecases/load-swimmer-championship-history.use-case';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';

const history = [{
  eventId: 'e1', nameEn: 'National', nameAr: null,
  startDate: '2023-11-15', endDate: '2023-11-16', locationEn: 'Cairo', locationAr: null,
  races: [{ dayLabelEn: 'Day 1', dayLabelAr: null, distanceId: 'd1', strokeId: 's1', timeMs: 52340, isPersonalBest: true }],
}];

function setup(getResult: unknown) {
  const repo = { getSwimmerChampionshipHistory: jest.fn().mockResolvedValue(getResult) };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: CHAMPIONSHIPS_REPOSITORY, useValue: repo }] });
  return { uc: TestBed.inject(LoadSwimmerChampionshipHistoryUseCase), repo };
}

describe('LoadSwimmerChampionshipHistoryUseCase', () => {
  it('loads and maps a valid response', async () => {
    const { uc, repo } = setup({ data: history });
    const res = await uc.run('sw1');
    expect(repo.getSwimmerChampionshipHistory).toHaveBeenCalledWith('sw1');
    expect(res.ok).toBe(true);
    if (res.ok) { expect(res.data[0].races[0].timeMs).toBe(52340); }
  });

  it('fails on an invalid payload', async () => {
    const { uc } = setup({ data: [{ eventId: 'e1' }] }); // missing required fields
    const res = await uc.run('sw1');
    expect(res.ok).toBe(false);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd frontend && npx jest src/app/features/championships/testing/domain/usecases/load-swimmer-championship-history.use-case.spec.ts`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement the use case**

Create `domain/usecases/load-swimmer-championship-history.use-case.ts`:

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { isSwimmerChampionshipHistoryValid } from '@features/championships/data/dto/swimmer-championship-history.dto';
import { toSwimmerChampionshipHistory } from '@features/championships/data/dto/swimmer-championship-history.mapper';
import { SwimmerChampionshipHistory } from '@features/championships/domain/model/swimmer-championship-history';

@Injectable({ providedIn: 'root' })
export class LoadSwimmerChampionshipHistoryUseCase extends UseCase<string, SwimmerChampionshipHistory[]> {
  private readonly repo = inject(CHAMPIONSHIPS_REPOSITORY);
  constructor() { super('LoadSwimmerChampionshipHistory'); }

  protected async execute(swimmerId: string): Promise<SwimmerChampionshipHistory[]> {
    const res = await this.repo.getSwimmerChampionshipHistory(swimmerId);
    if (!isSwimmerChampionshipHistoryValid(res.data)) throw new AppError('Invalid championship history received', 'validation');
    return toSwimmerChampionshipHistory(res.data);
  }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd frontend && npx jest src/app/features/championships/testing/domain/usecases/load-swimmer-championship-history.use-case.spec.ts`
Expected: PASS (2 tests). Do NOT commit.

---

## Task 6: Frontend — wire the tab into SwimmerProfileViewModel + enable it

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.ts`
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.ts`
- Test (modify): `frontend/src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `LoadSwimmerChampionshipHistoryUseCase` (Task 5), `LoadDistancesUseCase`, `LoadStrokesUseCase`, `LookupItem` from `@features/reference`, `SwimmerChampionshipHistory` (Task 4).
- Produces on the viewmodel: signals `championshipHistory`, `champDistances`, `champStrokes`, `selectedChampId`, `loadingChampionships`; computed `selectedChampionship`; `setTab('championships')` loads once. `activeTab`/`setTab` union gains `'championships'`.

- [ ] **Step 1: Write the failing viewmodel tests**

In `swimmer-profile.viewmodel.spec.ts`:

Add these imports at the top (with the other imports):

```typescript
import { LoadSwimmerChampionshipHistoryUseCase } from '@features/championships/domain/usecases/load-swimmer-championship-history.use-case';
import { LoadDistancesUseCase } from '@features/reference/domain/usecases/load-distances.use-case';
import { LoadStrokesUseCase } from '@features/reference/domain/usecases/load-strokes.use-case';
```

Inside `build(...)`, add these mocks before `TestBed.configureTestingModule` (after the attendance mocks):

```typescript
  const CH = [{
    eventId: 'ev1', nameEn: 'National', nameAr: null, startDate: '2023-11-15', endDate: '2023-11-16',
    locationEn: 'Cairo', locationAr: null,
    races: [{ dayLabelEn: 'Day 1', dayLabelAr: null, distanceId: 'd1', strokeId: 's1', timeMs: 52340, isPersonalBest: true }],
  }];
  const loadChampHistoryUc = { run: jest.fn().mockResolvedValue((over as any).champHistory ?? { ok: true, data: CH }) };
  const loadDistancesUc = { run: jest.fn().mockResolvedValue({ ok: true, data: [{ id: 'd1', code: '50m', nameEn: '50m', nameAr: null }] }) };
  const loadStrokesUc = { run: jest.fn().mockResolvedValue({ ok: true, data: [{ id: 's1', code: 'freestyle', nameEn: 'Freestyle', nameAr: null }] }) };
```

Add these three providers to the `providers` array:

```typescript
    { provide: LoadSwimmerChampionshipHistoryUseCase, useValue: loadChampHistoryUc },
    { provide: LoadDistancesUseCase, useValue: loadDistancesUc },
    { provide: LoadStrokesUseCase, useValue: loadStrokesUc },
```

Add these test cases (inside the top-level `describe`, after an existing tab test):

```typescript
  it('loads championship history + lookups once when the championships tab opens', async () => {
    const { vm } = buildAndLoad();
    vm.setTab('championships');
    await Promise.resolve(); await Promise.resolve();
    expect(vm.championshipHistory().length).toBe(1);
    expect(vm.selectedChampId()).toBe('ev1');
    expect(vm.selectedChampionship()?.races[0].timeMs).toBe(52340);
    vm.setTab('identityVitals');
    vm.setTab('championships');
    await Promise.resolve();
    // guard prevents a second load
    expect((vm as unknown as { championshipHistory: () => unknown[] }).championshipHistory().length).toBe(1);
  });

  it('a joined championship with no races selects but shows no races', async () => {
    const { vm } = buildAndLoad({ champHistory: { ok: true, data: [{ eventId: 'ev2', nameEn: 'Spring', nameAr: null, startDate: '2023-04-08', endDate: '2023-04-09', locationEn: 'Oasis', locationAr: null, races: [] }] } });
    vm.setTab('championships');
    await Promise.resolve(); await Promise.resolve();
    expect(vm.selectedChampionship()?.races.length).toBe(0);
  });

  it('a swimmer with no championships leaves the selection null', async () => {
    const { vm } = buildAndLoad({ champHistory: { ok: true, data: [] } });
    vm.setTab('championships');
    await Promise.resolve(); await Promise.resolve();
    expect(vm.championshipHistory().length).toBe(0);
    expect(vm.selectedChampionship()).toBeNull();
  });
```

> Note: `buildAndLoad` is the existing helper in this spec that builds the VM and awaits `load('s1')`. If the spec instead calls `build()` then `await vm.load('s1')`, follow that local pattern — mirror whatever the neighbouring tab tests use.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd frontend && npx jest src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts -t championship`
Expected: FAIL — provider missing / `championshipHistory` not a function.

- [ ] **Step 3: Wire the viewmodel — imports + injected use cases**

In `swimmer-profile.viewmodel.ts`, add imports:

```typescript
import { LoadSwimmerChampionshipHistoryUseCase } from '@features/championships/domain/usecases/load-swimmer-championship-history.use-case';
import { LoadDistancesUseCase } from '@features/reference/domain/usecases/load-distances.use-case';
import { LoadStrokesUseCase } from '@features/reference/domain/usecases/load-strokes.use-case';
import { SwimmerChampionshipHistory } from '@features/championships/domain/model/swimmer-championship-history';
```

Add injected use cases with the other `private readonly ... = inject(...)` lines:

```typescript
  private readonly loadChampHistoryUc = inject(LoadSwimmerChampionshipHistoryUseCase);
  private readonly loadDistancesUc = inject(LoadDistancesUseCase);
  private readonly loadStrokesUc = inject(LoadStrokesUseCase);
```

- [ ] **Step 4: Wire the viewmodel — union types, signals, guard, computed**

Extend the `activeTab` signal union and the `setTab` parameter union to include `'championships'` (both occurrences):

```typescript
  readonly activeTab = signal<'identityVitals' | 'guardian' | 'physiological' | 'inbody' | 'records' | 'healthMonitoring' | 'attendance' | 'championships' | 'feedback'>('identityVitals');
```
```typescript
  setTab(key: 'identityVitals' | 'guardian' | 'physiological' | 'inbody' | 'records' | 'healthMonitoring' | 'attendance' | 'championships' | 'feedback'): void {
```

Add the guard field with the other `private ...Loaded = false;` lines:

```typescript
  private championshipsLoaded = false;
```

Add the state signals + computed (place near the other tab state, e.g. after the attendance block):

```typescript
  // Championships state (read-only history)
  readonly championshipHistory = signal<SwimmerChampionshipHistory[]>([]);
  readonly champDistances = signal<LookupItem[]>([]);
  readonly champStrokes = signal<LookupItem[]>([]);
  readonly selectedChampId = signal('');
  readonly loadingChampionships = signal(false);

  readonly selectedChampionship = computed(() =>
    this.championshipHistory().find((c) => c.eventId === this.selectedChampId()) ?? this.championshipHistory()[0] ?? null);
```

(`LookupItem` and `computed` are already imported in this file.)

- [ ] **Step 5: Wire the viewmodel — reset, setTab branch, loader**

In `load(id)`, add to the per-swimmer reset block (next to the other resets):

```typescript
    this.championshipsLoaded = false;
    this.championshipHistory.set([]);
    this.champDistances.set([]);
    this.champStrokes.set([]);
    this.selectedChampId.set('');
```

In `setTab`, add the branch (with the other `if (key === ...)` lines):

```typescript
    if (key === 'championships' && !this.championshipsLoaded) void this.loadChampionships();
```

Add the loader method (next to the other `private async loadX()` methods):

```typescript
  private async loadChampionships(): Promise<void> {
    this.championshipsLoaded = true;
    this.loadingChampionships.set(true);
    const [histRes, distRes, strokeRes] = await Promise.all([
      this.loadChampHistoryUc.run(this.swimmerId),
      this.loadDistancesUc.run(),
      this.loadStrokesUc.run(),
    ]);
    this.loadingChampionships.set(false);
    if (distRes.ok) this.champDistances.set(distRes.data);
    if (strokeRes.ok) this.champStrokes.set(strokeRes.data);
    if (histRes.ok) {
      this.championshipHistory.set(histRes.data);
      this.selectedChampId.set(histRes.data[0]?.eventId ?? '');
    } else {
      this.championshipsLoaded = false;
      this.championshipHistory.set([]);
      this.selectedChampId.set('');
    }
  }
```

- [ ] **Step 6: Enable the tab in the page component**

In `swimmer-profile.page.ts`, add `'championships'` to `enabledTabs`:

```typescript
  protected readonly enabledTabs = new Set(['identityVitals', 'guardian', 'physiological', 'inbody', 'records', 'healthMonitoring', 'attendance', 'championships', 'feedback']);
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `cd frontend && npx jest src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts`
Expected: PASS (existing + 3 new). Do NOT commit.

---

## Task 7: Frontend — the tab markup, page helpers, and i18n

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.ts` (display helpers + `formatMsToTime` import)
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.html` (the `championships` section)
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`

**Interfaces:**
- Consumes: viewmodel signals/computed from Task 6; `resolveRaceName` from Task 4; `formatMsToTime` from `@features/championships/domain/model/race-time`.

- [ ] **Step 1: Add the i18n block (en)**

In `frontend/src/app/core/i18n/en.json`, inside the `swimmerProfile` object (as a sibling of `tabs`, e.g. right after the `feedback` sub-block), add:

```json
    "championships": {
      "title": "Championship History",
      "subtitle": "Every championship entered, with its races and results",
      "select": "Championship",
      "raceCol": "Race",
      "dayCol": "Day",
      "timeCol": "Time",
      "races": "races",
      "empty": "No results recorded yet.",
      "noHistory": "No championships joined yet.",
      "pb": "PB"
    },
```

(The `swimmerProfile.tabs.championships` label already exists — do not re-add it.)

- [ ] **Step 2: Add the i18n block (ar)**

In `frontend/src/app/core/i18n/ar.json`, in the matching place inside `swimmerProfile`, add:

```json
    "championships": {
      "title": "سجل البطولات",
      "subtitle": "كل بطولة شارك فيها، وسباقاتها ونتائجها",
      "select": "البطولة",
      "raceCol": "السباق",
      "dayCol": "اليوم",
      "timeCol": "الوقت",
      "races": "سباق",
      "empty": "لا نتائج مسجلة بعد.",
      "noHistory": "لم ينضم إلى أي بطولة بعد.",
      "pb": "PB"
    },
```

- [ ] **Step 3: Add the page display helpers**

In `swimmer-profile.page.ts`, add the import:

```typescript
import { formatMsToTime } from '@features/championships/domain/model/race-time';
import { resolveRaceName } from '@features/championships/domain/model/swimmer-championship-history';
import { SwimmerChampionshipHistory, SwimmerChampionshipRace } from '@features/championships/domain/model/swimmer-championship-history';
```

Add these methods to the class (they mirror the existing `refLabel`/`fmtDate` helpers and use the injected `language`):

```typescript
  champName(c: SwimmerChampionshipHistory): string {
    return this.language.lang() === 'ar' ? (c.nameAr ?? c.nameEn) : c.nameEn;
  }
  champLocation(c: SwimmerChampionshipHistory): string {
    return this.language.lang() === 'ar' ? (c.locationAr ?? c.locationEn) : c.locationEn;
  }
  champDayLabel(r: SwimmerChampionshipRace): string {
    return this.language.lang() === 'ar' ? (r.dayLabelAr ?? r.dayLabelEn) : r.dayLabelEn;
  }
  champRaceName(r: SwimmerChampionshipRace): string {
    return resolveRaceName(this.vm.champDistances(), this.vm.champStrokes(), r.distanceId, r.strokeId, this.language.lang());
  }
  champTime(ms: number): string { return formatMsToTime(ms); }
  champDateRange(startIso: string, endIso: string): string {
    const opts: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short', year: 'numeric' };
    const locale = this.language.lang() === 'ar' ? 'ar-EG' : 'en-GB';
    const start = new Date(startIso), end = new Date(endIso);
    const fmt = (d: Date) => d.toLocaleDateString(locale, opts);
    return startIso === endIso ? fmt(start) : `${fmt(start)} – ${fmt(end)}`;
  }
```

- [ ] **Step 4: Add the tab section markup**

In `swimmer-profile.page.html`, add this block next to the other `@if (vm.activeTab() === ...)` sections (e.g. right after the `attendance` section, before `feedback`):

```html
    @if (vm.activeTab() === 'championships') {
      <section class="rounded-2xl border border-border bg-surface p-6 shadow-sm">
        <div class="mb-4 flex flex-wrap items-end justify-between gap-3 border-b border-border pb-3">
          <div>
            <h2 class="font-heading text-lg text-ink">{{ 'swimmerProfile.championships.title' | translate }}</h2>
            <p class="mt-0.5 text-sm text-text-secondary">{{ 'swimmerProfile.championships.subtitle' | translate }}</p>
          </div>
          @if (vm.championshipHistory().length > 0) {
            <div class="flex flex-col gap-1">
              <label for="champ-select" class="text-xs font-medium text-text-secondary">{{ 'swimmerProfile.championships.select' | translate }}</label>
              <select id="champ-select" class="h-9 min-w-[16rem] rounded-md border border-border bg-surface px-2 text-sm text-ink"
                      [value]="vm.selectedChampId()" (change)="vm.selectedChampId.set($any($event.target).value)">
                @for (c of vm.championshipHistory(); track c.eventId) {
                  <option [value]="c.eventId">{{ champName(c) }} · {{ champDateRange(c.startDate, c.endDate) }}</option>
                }
              </select>
            </div>
          }
        </div>

        @if (vm.loadingChampionships()) {
          <p class="text-sm text-text-secondary">{{ 'swimmerProfile.states.loading' | translate }}</p>
        } @else if (vm.selectedChampionship(); as c) {
          <section class="overflow-hidden rounded-xl border border-border">
            <header class="flex flex-wrap items-center justify-between gap-3 border-b border-border bg-surface-muted/40 p-4">
              <div class="flex min-w-0 items-center gap-3">
                <span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">🏆</span>
                <div class="min-w-0">
                  <h3 class="truncate font-heading text-base text-ink">{{ champName(c) }}</h3>
                  <p class="mt-0.5 text-xs text-text-secondary">{{ champDateRange(c.startDate, c.endDate) }} · {{ champLocation(c) }}</p>
                </div>
              </div>
              <span class="shrink-0 text-xs text-text-secondary">{{ c.races.length }} {{ 'swimmerProfile.championships.races' | translate }}</span>
            </header>

            @if (c.races.length === 0) {
              <p class="p-6 text-center text-sm text-text-secondary">{{ 'swimmerProfile.championships.empty' | translate }}</p>
            } @else {
              <table class="w-full text-start text-sm">
                <thead class="border-b border-border text-text-secondary">
                  <tr>
                    <th class="px-4 py-2.5 text-start font-medium">{{ 'swimmerProfile.championships.raceCol' | translate }}</th>
                    <th class="px-4 py-2.5 text-start font-medium">{{ 'swimmerProfile.championships.dayCol' | translate }}</th>
                    <th class="px-4 py-2.5 text-start font-medium">{{ 'swimmerProfile.championships.timeCol' | translate }}</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-border">
                  @for (r of c.races; track $index) {
                    <tr>
                      <td class="px-4 py-3 font-medium text-ink">{{ champRaceName(r) }}</td>
                      <td class="px-4 py-3 text-text-secondary">{{ champDayLabel(r) }}</td>
                      <td class="px-4 py-3">
                        <span class="inline-flex items-center gap-1.5 font-semibold text-ink">
                          {{ champTime(r.timeMs) }}
                          @if (r.isPersonalBest) {
                            <span class="rounded bg-primary/10 px-1.5 py-0.5 text-[10px] font-bold text-primary">{{ 'swimmerProfile.championships.pb' | translate }}</span>
                          }
                        </span>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          </section>
        } @else {
          <p class="text-sm text-text-secondary">{{ 'swimmerProfile.championships.noHistory' | translate }}</p>
        }
      </section>
    }
```

> If `bg-surface-muted` is not a token in this Tailwind config, use `bg-surface` (check a neighbouring section's header class and match it).

- [ ] **Step 5: Run the full swimmer-profile + championships suites**

Run: `cd frontend && npx jest src/app/features/swimmer-profile src/app/features/championships`
Expected: PASS (all green).

- [ ] **Step 6: Type-check / build the frontend**

Run: `cd frontend && npx tsc -p tsconfig.app.json --noEmit`
Expected: no type errors. Do NOT commit.

- [ ] **Step 7: Manual smoke (optional, no commit)**

Open a swimmer profile, click the **Championships** tab: the dropdown lists the swimmer's joined championships; selecting one shows the card (trophy, name, dates · venue, N races) and the Race/Day/Time(+PB) table; a joined championship with no results shows the empty message; a swimmer with no championships shows the "no championships joined" message.

---

## Self-Review

**1. Spec coverage:**
- Enable disabled tab → Task 6 Step 6. ✓
- Dropdown = enrolled, newest-first, empty-table-when-none → Task 1 (ordering + empty races) + Task 6 (selection) + Task 7 (markup incl. two empty states). ✓
- No rank / no points → DTO/model carry neither; table has 3 columns (Task 1, 4, 7). ✓
- Read-only, no auth change → no write use cases/endpoints; endpoint has no `[Authorize(Roles=...)]` beyond the controller's class-level `[Authorize]`. ✓
- Client-side name resolution → `resolveRaceName` + lookups loaded in `loadChampionships` (Task 4, 6, 7). ✓
- No migration/seed → none in any task. ✓
- Endpoint `GET /api/championships/swimmer/{swimmerId}/history` → Task 3. ✓

**2. Placeholder scan:** No TBD/TODO; every code step has concrete code. The two `>` notes (buildAndLoad helper, bg-surface-muted token) point the engineer to mirror an existing local pattern rather than leaving logic unspecified. ✓

**3. Type consistency:** `ChampionshipSwimmerHistoryDto`/`ChampionshipSwimmerRaceDto` (backend) ↔ `SwimmerChampionshipHistoryData`/`SwimmerChampionshipRaceData` (frontend DTO) ↔ `SwimmerChampionshipHistory`/`SwimmerChampionshipRace` (frontend model) all carry the same fields. `SwimmerRaceLineRow` fields match what `GetSwimmerRaceLinesAsync` selects and what the service consumes. `getSwimmerChampionshipHistory` name identical across interface, impl, use case, spec. `selectedChampId`/`selectedChampionship`/`championshipHistory` identical across viewmodel, spec, and template. ✓

**4. Review Focus:** all five items pinned to owning tasks' tests (zero-enrollments → Task 1/6/7; enrolled-no-results → Task 1/6; missing lookup → Task 4; ordering → Task 1; invalid payload → Task 4/5). ✓
