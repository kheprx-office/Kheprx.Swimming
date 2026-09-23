# Championship Detail page + Enrollment tab — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a routed championship detail page (`/championships/:id`) whose first tab lets a coach enroll swimmers in a competition event, backed by a new `championships.championship_enrollment` table.

**Architecture:** Extend the existing `Championships` backend module with an enrollment entity + repository + service methods + three controller endpoints (GET event-by-id, GET/PUT enrollments). The frontend extends `features/championships` with clean-arch layers (DTO → repo → use-cases → route-scoped ViewModel → page), reusing the existing swimmer roster loader. Enrollment Save is an atomic replace of the event's enrolled-swimmer set (mirrors the guardians upsert).

**Tech Stack:** .NET 10 / EF Core (Npgsql) / xUnit + Moq on the backend; Angular (standalone components, signals) / Jest on the frontend; PostgreSQL (Aiven `Swimming_Production`).

**Spec:** `docs/superpowers/specs/2026-09-23-championship-enrollment-design.md`

## Global Constraints

- **No commits until the user explicitly says so** (user directive for this whole effort). The commit steps below stage + commit locally; do them only once the user has lifted the hold, otherwise stop after the "tests pass" step of each task.
- **EF naming:** table + schema are snake_case via `ToTable("name","schema")`; **columns keep PascalCase property names** — so raw SQL/seed must double-quote them (`"Id"`, `"EventId"`, `"SwimmerId"`).
- **Cross-module ids are loose Guids** — no EF navigation across modules, no DB-level FK (separate `DbContext`s). The only DB invariant enforced here is the unique `(EventId, SwimmerId)` index.
- **Roles:** management actions are `head_coach` or `captain` only (matches the list page's Create).
- **i18n:** every user-facing string goes through `translate`; add EN **and** AR keys together (`app/core/i18n/en.json`, `app/core/i18n/ar.json`).
- **Bilingual columns:** names/locations render EN or AR by the active `LanguageStore`.
- **Backend build lock:** `dotnet` build / `dotnet ef` require the running API to be **stopped** first (it locks the output DLLs).
- **Frontend tests are Jest**, not Karma. Spec files live under `features/<f>/testing/...` mirroring the source path.

---

## Backend

### Task 1: `ChampionshipEnrollment` entity

**Files:**
- Create: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Domain/Entities/ChampionshipEnrollment.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Entities/ChampionshipEnrollmentTests.cs`

**Interfaces:**
- Produces: `ChampionshipEnrollment(Guid eventId, Guid swimmerId)` with read-only props `Id`, `EventId`, `SwimmerId` (all `Guid`), and a private parameterless ctor for EF.

- [ ] **Step 1: Write the failing test**

```csharp
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Entities;

public class ChampionshipEnrollmentTests
{
    [Fact]
    public void Ctor_sets_ids_and_generates_a_new_id()
    {
        var eventId = Guid.NewGuid();
        var swimmerId = Guid.NewGuid();

        var e = new ChampionshipEnrollment(eventId, swimmerId);

        Assert.NotEqual(Guid.Empty, e.Id);
        Assert.Equal(eventId, e.EventId);
        Assert.Equal(swimmerId, e.SwimmerId);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests --filter FullyQualifiedName~ChampionshipEnrollmentTests`
Expected: FAIL — `ChampionshipEnrollment` does not exist (compile error).

- [ ] **Step 3: Write the entity**

```csharp
namespace Kheprx.BaseBackend.Championships.Domain.Entities;

public sealed class ChampionshipEnrollment
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid SwimmerId { get; private set; }

    private ChampionshipEnrollment() { } // EF Core

    public ChampionshipEnrollment(Guid eventId, Guid swimmerId)
    {
        Id = Guid.NewGuid();
        EventId = eventId;
        SwimmerId = swimmerId;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests --filter FullyQualifiedName~ChampionshipEnrollmentTests`
Expected: PASS.

- [ ] **Step 5: Commit** (only once the hold is lifted)

```bash
git add backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Domain/Entities/ChampionshipEnrollment.cs \
        backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Entities/ChampionshipEnrollmentTests.cs
git commit -m "feat(championships): add ChampionshipEnrollment entity"
```

---

### Task 2: Persistence — config, DbSet, repository, migration

**Files:**
- Create: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure/Configurations/ChampionshipEnrollmentConfiguration.cs`
- Create: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Domain/Repositories/IChampionshipEnrollmentRepository.cs`
- Create: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure/Repositories/ChampionshipEnrollmentRepository.cs`
- Modify: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure/Data/ChampionshipsDbContext.cs` (add `DbSet`)
- Modify: `backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure/Extensions/ChampionshipsModuleExtensions.cs` (register repo)
- Create (via CLI): migration `…_CreateChampionshipEnrollmentTable.cs` (+ Designer + snapshot update)
- Test: `backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Repositories/ChampionshipEnrollmentRepositoryTests.cs`

**Interfaces:**
- Consumes: `ChampionshipEnrollment` (Task 1); `ChampionshipsDbContext`.
- Produces: `IChampionshipEnrollmentRepository` with
  - `Task<IReadOnlyList<Guid>> ListSwimmerIdsAsync(Guid eventId, CancellationToken ct = default)`
  - `Task ReplaceAsync(Guid eventId, IReadOnlyList<Guid> swimmerIds, CancellationToken ct = default)`
  and `ChampionshipsDbContext.Enrollments` (`DbSet<ChampionshipEnrollment>`).

- [ ] **Step 1: Write the failing repository tests**

```csharp
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Kheprx.BaseBackend.Championships.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Repositories;

public class ChampionshipEnrollmentRepositoryTests
{
    private static ChampionshipsDbContext NewDb()
        => new(new DbContextOptionsBuilder<ChampionshipsDbContext>()
            .UseInMemoryDatabase($"enr-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task ListSwimmerIdsAsync_returns_only_ids_for_that_event()
    {
        await using var db = NewDb();
        var eventA = Guid.NewGuid();
        var eventB = Guid.NewGuid();
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        db.Enrollments.Add(new ChampionshipEnrollment(eventA, s1));
        db.Enrollments.Add(new ChampionshipEnrollment(eventA, s2));
        db.Enrollments.Add(new ChampionshipEnrollment(eventB, s1));
        await db.SaveChangesAsync();

        var ids = await new ChampionshipEnrollmentRepository(db).ListSwimmerIdsAsync(eventA);

        Assert.Equal(2, ids.Count);
        Assert.Contains(s1, ids);
        Assert.Contains(s2, ids);
    }

    [Fact]
    public async Task ReplaceAsync_deletes_prior_rows_and_inserts_the_new_deduped_set()
    {
        await using var db = NewDb();
        var eventId = Guid.NewGuid();
        var old = Guid.NewGuid();
        db.Enrollments.Add(new ChampionshipEnrollment(eventId, old));
        await db.SaveChangesAsync();

        var keep = Guid.NewGuid();
        await new ChampionshipEnrollmentRepository(db).ReplaceAsync(eventId, new[] { keep, keep });

        var ids = await new ChampionshipEnrollmentRepository(db).ListSwimmerIdsAsync(eventId);
        Assert.Single(ids);            // deduped
        Assert.Equal(keep, ids[0]);    // old row gone
    }

    [Fact]
    public async Task ReplaceAsync_with_empty_set_clears_the_event()
    {
        await using var db = NewDb();
        var eventId = Guid.NewGuid();
        db.Enrollments.Add(new ChampionshipEnrollment(eventId, Guid.NewGuid()));
        await db.SaveChangesAsync();

        await new ChampionshipEnrollmentRepository(db).ReplaceAsync(eventId, Array.Empty<Guid>());

        Assert.Empty(await new ChampionshipEnrollmentRepository(db).ListSwimmerIdsAsync(eventId));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests --filter FullyQualifiedName~ChampionshipEnrollmentRepositoryTests`
Expected: FAIL — `Enrollments`, `IChampionshipEnrollmentRepository`, `ChampionshipEnrollmentRepository` don't exist.

- [ ] **Step 3: Add the DbSet to the context**

In `ChampionshipsDbContext.cs`, add below the existing `CompetitionEvents` set:

```csharp
    public DbSet<ChampionshipEnrollment> Enrollments => Set<ChampionshipEnrollment>();
```

- [ ] **Step 4: Add the EF configuration (unique composite index)**

```csharp
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Configurations;

internal sealed class ChampionshipEnrollmentConfiguration : IEntityTypeConfiguration<ChampionshipEnrollment>
{
    public void Configure(EntityTypeBuilder<ChampionshipEnrollment> builder)
    {
        builder.ToTable("championship_enrollment", "championships");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EventId).IsRequired();     // loose Guid → championships.competition_event
        builder.Property(e => e.SwimmerId).IsRequired();   // loose Guid → identity.swimmer_profile
        builder.HasIndex(e => new { e.EventId, e.SwimmerId }).IsUnique();
    }
}
```

- [ ] **Step 5: Write the repository interface**

```csharp
namespace Kheprx.BaseBackend.Championships.Domain.Repositories;

public interface IChampionshipEnrollmentRepository
{
    Task<IReadOnlyList<Guid>> ListSwimmerIdsAsync(Guid eventId, CancellationToken ct = default);
    Task ReplaceAsync(Guid eventId, IReadOnlyList<Guid> swimmerIds, CancellationToken ct = default);
}
```

- [ ] **Step 6: Write the repository implementation**

```csharp
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Repositories;

internal sealed class ChampionshipEnrollmentRepository : IChampionshipEnrollmentRepository
{
    private readonly ChampionshipsDbContext _db;
    public ChampionshipEnrollmentRepository(ChampionshipsDbContext db) => _db = db;

    public async Task<IReadOnlyList<Guid>> ListSwimmerIdsAsync(Guid eventId, CancellationToken ct = default)
        => await _db.Enrollments.AsNoTracking()
              .Where(e => e.EventId == eventId)
              .Select(e => e.SwimmerId)
              .ToListAsync(ct);

    public async Task ReplaceAsync(Guid eventId, IReadOnlyList<Guid> swimmerIds, CancellationToken ct = default)
    {
        var existing = await _db.Enrollments.Where(e => e.EventId == eventId).ToListAsync(ct);
        _db.Enrollments.RemoveRange(existing);

        var distinct = swimmerIds.Distinct();
        foreach (var swimmerId in distinct)
            _db.Enrollments.Add(new ChampionshipEnrollment(eventId, swimmerId));

        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 7: Register the repository**

In `ChampionshipsModuleExtensions.AddChampionshipsModule`, below the existing `ICompetitionEventRepository` registration:

```csharp
        services.AddScoped<IChampionshipEnrollmentRepository, ChampionshipEnrollmentRepository>();
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests --filter FullyQualifiedName~ChampionshipEnrollmentRepositoryTests`
Expected: PASS (in-memory provider ignores the relational index, but exercises Replace/List logic).

- [ ] **Step 9: Generate the EF migration**

Stop the running API first. Then:

```bash
dotnet ef migrations add CreateChampionshipEnrollmentTable \
  --project backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure \
  --context ChampionshipsDbContext
```

Verify the generated migration creates `championships.championship_enrollment` with `Id`/`EventId`/`SwimmerId` and a **unique** index on `("EventId","SwimmerId")`. Do **not** apply to any DB yet (that is Task 9, which needs the Aiven password).

- [ ] **Step 10: Build to confirm the migration compiles**

Run: `dotnet build backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure`
Expected: Build succeeded.

- [ ] **Step 11: Commit** (once the hold is lifted)

```bash
git add backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure \
        backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Domain/Repositories/IChampionshipEnrollmentRepository.cs \
        backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Repositories/ChampionshipEnrollmentRepositoryTests.cs
git commit -m "feat(championships): championship_enrollment table + repository + migration"
```

---

### Task 3: Service layer — event-by-id + enrollment read/replace

**Files:**
- Modify: `…Championships.Domain/Repositories/ICompetitionEventRepository.cs` (add `GetByIdAsync`)
- Modify: `…Championships.Infrastructure/Repositories/CompetitionEventRepository.cs` (implement `GetByIdAsync`)
- Modify: `…Championships.Application/DTOs/CompetitionEventDtos.cs` (add `SetEnrollmentsRequest`)
- Modify: `…Championships.Application/Services/Interfaces/IChampionshipService.cs`
- Modify: `…Championships.Application/Services/ChampionshipService.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Services/ChampionshipServiceTests.cs` (add cases)
- Test: `backend/tests/Kheprx.BaseBackend.Championships.UnitTests/Repositories/CompetitionEventRepositoryTests.cs` (add `GetByIdAsync` case)

**Interfaces:**
- Consumes: `ICompetitionEventRepository`, `IChampionshipEnrollmentRepository` (Task 2), `CompetitionEventDto`.
- Produces on `IChampionshipService`:
  - `Task<CompetitionEventDto?> GetByIdAsync(Guid id, CancellationToken ct = default)`
  - `Task<IReadOnlyList<Guid>?> GetEnrolledSwimmerIdsAsync(Guid eventId, CancellationToken ct = default)` — `null` ⇒ event missing
  - `Task<bool> SetEnrollmentsAsync(Guid eventId, IReadOnlyList<Guid> swimmerIds, CancellationToken ct = default)` — `false` ⇒ event missing
  - on `ICompetitionEventRepository`: `Task<CompetitionEvent?> GetByIdAsync(Guid id, CancellationToken ct = default)`
  - DTO: `SetEnrollmentsRequest(IReadOnlyList<Guid> SwimmerIds)`

- [ ] **Step 1: Write the failing service tests** (append to `ChampionshipServiceTests`)

```csharp
    [Fact]
    public async Task GetByIdAsync_maps_the_event_with_empty_status_fields()
    {
        var statusId = Guid.NewGuid();
        var ev = new CompetitionEvent("Nats", null, new DateOnly(2023, 11, 15),
            new DateOnly(2023, 11, 16), "Cairo", null, statusId, Guid.NewGuid());
        var events = new Mock<ICompetitionEventRepository>();
        events.Setup(r => r.GetByIdAsync(ev.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ev);
        var enr = new Mock<IChampionshipEnrollmentRepository>();

        var svc = new ChampionshipService(events.Object, enr.Object);
        var dto = await svc.GetByIdAsync(ev.Id);

        Assert.NotNull(dto);
        Assert.Equal("Nats", dto!.NameEn);
        Assert.Equal(statusId, dto.StatusId);
        Assert.Equal("", dto.StatusCode);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_missing()
    {
        var events = new Mock<ICompetitionEventRepository>();
        events.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((CompetitionEvent?)null);
        var svc = new ChampionshipService(events.Object, new Mock<IChampionshipEnrollmentRepository>().Object);

        Assert.Null(await svc.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetEnrolledSwimmerIdsAsync_returns_null_when_event_missing()
    {
        var events = new Mock<ICompetitionEventRepository>();
        events.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((CompetitionEvent?)null);
        var enr = new Mock<IChampionshipEnrollmentRepository>();
        var svc = new ChampionshipService(events.Object, enr.Object);

        Assert.Null(await svc.GetEnrolledSwimmerIdsAsync(Guid.NewGuid()));
        enr.Verify(r => r.ListSwimmerIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetEnrolledSwimmerIdsAsync_returns_ids_when_event_exists()
    {
        var ev = new CompetitionEvent("Nats", null, new DateOnly(2023, 11, 15),
            new DateOnly(2023, 11, 16), "Cairo", null, Guid.NewGuid(), Guid.NewGuid());
        var s1 = Guid.NewGuid();
        var events = new Mock<ICompetitionEventRepository>();
        events.Setup(r => r.GetByIdAsync(ev.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ev);
        var enr = new Mock<IChampionshipEnrollmentRepository>();
        enr.Setup(r => r.ListSwimmerIdsAsync(ev.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { s1 });
        var svc = new ChampionshipService(events.Object, enr.Object);

        var ids = await svc.GetEnrolledSwimmerIdsAsync(ev.Id);
        Assert.NotNull(ids);
        Assert.Equal(new[] { s1 }, ids!);
    }

    [Fact]
    public async Task SetEnrollmentsAsync_returns_false_when_event_missing_and_does_not_replace()
    {
        var events = new Mock<ICompetitionEventRepository>();
        events.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((CompetitionEvent?)null);
        var enr = new Mock<IChampionshipEnrollmentRepository>();
        var svc = new ChampionshipService(events.Object, enr.Object);

        Assert.False(await svc.SetEnrollmentsAsync(Guid.NewGuid(), new[] { Guid.NewGuid() }));
        enr.Verify(r => r.ReplaceAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetEnrollmentsAsync_replaces_and_returns_true_when_event_exists()
    {
        var ev = new CompetitionEvent("Nats", null, new DateOnly(2023, 11, 15),
            new DateOnly(2023, 11, 16), "Cairo", null, Guid.NewGuid(), Guid.NewGuid());
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var events = new Mock<ICompetitionEventRepository>();
        events.Setup(r => r.GetByIdAsync(ev.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ev);
        var enr = new Mock<IChampionshipEnrollmentRepository>();
        var svc = new ChampionshipService(events.Object, enr.Object);

        Assert.True(await svc.SetEnrollmentsAsync(ev.Id, ids));
        enr.Verify(r => r.ReplaceAsync(ev.Id, ids, It.IsAny<CancellationToken>()), Times.Once);
    }
```

Add `using Kheprx.BaseBackend.Championships.Domain.Repositories;` to the test file's usings if not already present (it is).

- [ ] **Step 2: Write the failing repository test** (append to `CompetitionEventRepositoryTests`)

```csharp
    [Fact]
    public async Task GetByIdAsync_returns_the_event_or_null()
    {
        await using var db = NewDb();
        var ev = new CompetitionEvent("Nov", null, new DateOnly(2023, 11, 15), new DateOnly(2023, 11, 16), "Cairo", null, Guid.NewGuid(), Guid.NewGuid());
        db.CompetitionEvents.Add(ev);
        await db.SaveChangesAsync();
        var repo = new CompetitionEventRepository(db);

        Assert.Equal("Nov", (await repo.GetByIdAsync(ev.Id))!.NameEn);
        Assert.Null(await repo.GetByIdAsync(Guid.NewGuid()));
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests`
Expected: FAIL — new members/ctor arity don't exist.

- [ ] **Step 4: Extend the repository interface + implementation**

In `ICompetitionEventRepository`:

```csharp
    Task<CompetitionEvent?> GetByIdAsync(Guid id, CancellationToken ct = default);
```

In `CompetitionEventRepository`:

```csharp
    public async Task<CompetitionEvent?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.CompetitionEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
```

- [ ] **Step 5: Add the request DTO**

Append to `CompetitionEventDtos.cs`:

```csharp
/// <summary>Replace-the-whole-set request for an event's swimmer enrollment.</summary>
public sealed record SetEnrollmentsRequest(IReadOnlyList<Guid> SwimmerIds);
```

- [ ] **Step 6: Extend the service interface**

Add to `IChampionshipService`:

```csharp
    Task<CompetitionEventDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>?> GetEnrolledSwimmerIdsAsync(Guid eventId, CancellationToken ct = default);
    Task<bool> SetEnrollmentsAsync(Guid eventId, IReadOnlyList<Guid> swimmerIds, CancellationToken ct = default);
```

- [ ] **Step 7: Implement in `ChampionshipService`**

Change the constructor to take both repositories and add the methods:

```csharp
    private readonly ICompetitionEventRepository _events;
    private readonly IChampionshipEnrollmentRepository _enrollments;

    public ChampionshipService(ICompetitionEventRepository events, IChampionshipEnrollmentRepository enrollments)
    {
        _events = events;
        _enrollments = enrollments;
    }

    public async Task<CompetitionEventDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _events.GetByIdAsync(id, ct);
        return e is null ? null : ToDto(e);
    }

    public async Task<IReadOnlyList<Guid>?> GetEnrolledSwimmerIdsAsync(Guid eventId, CancellationToken ct = default)
    {
        var e = await _events.GetByIdAsync(eventId, ct);
        if (e is null) return null;
        return await _enrollments.ListSwimmerIdsAsync(eventId, ct);
    }

    public async Task<bool> SetEnrollmentsAsync(Guid eventId, IReadOnlyList<Guid> swimmerIds, CancellationToken ct = default)
    {
        var e = await _events.GetByIdAsync(eventId, ct);
        if (e is null) return false;
        await _enrollments.ReplaceAsync(eventId, swimmerIds, ct);
        return true;
    }
```

Add `using Kheprx.BaseBackend.Championships.Domain.Repositories;` if not present (it is, via existing usings). Keep the existing `ListAsync`/`CreateAsync`/`ToDto` unchanged.

- [ ] **Step 8: Fix the existing service test's constructor call**

The pre-existing `ChampionshipServiceTests` build `new ChampionshipService(repo.Object)`. Update those two constructions to pass an enrollment mock:

```csharp
var svc = new ChampionshipService(repo.Object, new Mock<IChampionshipEnrollmentRepository>().Object);
```

- [ ] **Step 9: Run the whole module test project to verify green**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Championships.UnitTests`
Expected: PASS (all old + new tests).

- [ ] **Step 10: Commit** (once the hold is lifted)

```bash
git add backend/src/Modules/Championships backend/tests/Kheprx.BaseBackend.Championships.UnitTests
git commit -m "feat(championships): service GetById + enrollment read/replace"
```

---

### Task 4: Controller endpoints + messages

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/ChampionshipsController.cs`
- Modify: `…Championships.Application/Resources/ChampionshipMessages.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/ChampionshipsControllerTests.cs` (add cases)

**Interfaces:**
- Consumes: `IChampionshipService` (Task 3), `IReferenceService.GetCompetitionStatusesAsync`, `CompetitionEventDto`, `SetEnrollmentsRequest`.
- Produces HTTP:
  - `GET /api/championships/{id:guid}` → `ApiResponse<CompetitionEventDto>` (200 / 404)
  - `GET /api/championships/{eventId:guid}/enrollments` → `ApiResponse<IReadOnlyList<Guid>>` (200 / 404)
  - `PUT /api/championships/{eventId:guid}/enrollments` (body `SetEnrollmentsRequest`) → `ApiResponse<object>` (200 / 404; 403 via role attribute)

- [ ] **Step 1: Write the failing controller tests** (append to `ChampionshipsControllerTests`)

```csharp
    [Fact]
    public async Task GetById_returns_200_and_resolves_status()
    {
        var id = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new CompetitionEventDto(id, "Nats", null, new DateOnly(2023, 11, 15),
               new DateOnly(2023, 11, 16), "Cairo", null, statusId, "", "", null));
        var reference = new Mock<IReferenceService>();
        reference.Setup(r => r.GetCompetitionStatusesAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new[] { new CodedLookupDto(statusId, "upcoming", "Upcoming", "قادمة") });

        var result = await NewController(svc.Object, reference.Object).GetById(id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<CompetitionEventDto>>(ok.Value);
        Assert.Equal("upcoming", body.Data!.StatusCode);
        Assert.Equal("Upcoming", body.Data!.StatusNameEn);
    }

    [Fact]
    public async Task GetById_returns_404_when_missing()
    {
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((CompetitionEventDto?)null);
        var reference = new Mock<IReferenceService>();

        var result = await NewController(svc.Object, reference.Object).GetById(Guid.NewGuid(), CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task GetEnrollments_returns_200_with_ids()
    {
        var eventId = Guid.NewGuid();
        var s1 = Guid.NewGuid();
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.GetEnrolledSwimmerIdsAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { s1 });

        var result = await NewController(svc.Object, new Mock<IReferenceService>().Object).GetEnrollments(eventId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<Guid>>>(ok.Value);
        Assert.Equal(new[] { s1 }, body.Data!);
    }

    [Fact]
    public async Task GetEnrollments_returns_404_when_event_missing()
    {
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.GetEnrolledSwimmerIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<Guid>?)null);

        var result = await NewController(svc.Object, new Mock<IReferenceService>().Object).GetEnrollments(Guid.NewGuid(), CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task SetEnrollments_returns_200_when_replaced()
    {
        var eventId = Guid.NewGuid();
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.SetEnrollmentsAsync(eventId, It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var request = new SetEnrollmentsRequest(new[] { Guid.NewGuid() });
        var result = await NewController(svc.Object, new Mock<IReferenceService>().Object).SetEnrollments(eventId, request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task SetEnrollments_returns_404_when_event_missing()
    {
        var svc = new Mock<IChampionshipService>();
        svc.Setup(s => s.SetEnrollmentsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var request = new SetEnrollmentsRequest(Array.Empty<Guid>());
        var result = await NewController(svc.Object, new Mock<IReferenceService>().Object).SetEnrollments(Guid.NewGuid(), request, CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public void SetEnrollments_is_restricted_to_head_coach_and_captain()
    {
        var attr = typeof(ChampionshipsController).GetMethod(nameof(ChampionshipsController.SetEnrollments))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single();
        Assert.Equal("head_coach,captain", attr.Roles);
    }
```

Add `using System.Linq;` if the file doesn't already have it (it uses `System.Globalization`; `System.Linq` is implicitly available via `ImplicitUsings`, so no change needed).

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter FullyQualifiedName~ChampionshipsControllerTests`
Expected: FAIL — `GetById`/`GetEnrollments`/`SetEnrollments` don't exist.

- [ ] **Step 3: Add localized messages**

Append inside `ChampionshipMessages`:

```csharp
    public static class EnrollmentSuccess
    {
        public static string Retrieved(string lang) => lang switch { "ar" => "التسجيلات", _ => "Enrollments" };
        public static string Saved(string lang) => lang switch { "ar" => "تم حفظ التسجيلات", _ => "Enrollments saved" };
    }

    public static class NotFound
    {
        public static string Event(string lang) => lang switch { "ar" => "البطولة غير موجودة", _ => "Championship not found" };
    }
```

- [ ] **Step 4: Add the controller actions**

Add these to `ChampionshipsController` (below `Create`):

```csharp
    /// <summary>Returns a single championship event with its status resolved. 404 when unknown.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CompetitionEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CompetitionEventDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CompetitionEventDto>>> GetById(Guid id, CancellationToken ct)
    {
        var lang = AppLanguage.Current;
        var dto = await _service.GetByIdAsync(id, ct);
        if (dto is null)
            return StatusCode(StatusCodes.Status404NotFound,
                ApiResponse<CompetitionEventDto>.Failure(ChampionshipMessages.NotFound.Event(lang), "not_found"));

        var statuses = await _reference.GetCompetitionStatusesAsync(ct);
        var s = statuses.FirstOrDefault(x => x.Id == dto.StatusId);
        var enriched = s is null ? dto
            : dto with { StatusCode = s.Code, StatusNameEn = s.NameEn, StatusNameAr = s.NameAr };

        return Ok(ApiResponse<CompetitionEventDto>.Success(ChampionshipMessages.Success.Listed(lang), enriched));
    }

    /// <summary>Lists the swimmer ids enrolled in an event. 404 when the event is unknown.</summary>
    [HttpGet("{eventId:guid}/enrollments")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<Guid>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<Guid>>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<Guid>>>> GetEnrollments(Guid eventId, CancellationToken ct)
    {
        var lang = AppLanguage.Current;
        var ids = await _service.GetEnrolledSwimmerIdsAsync(eventId, ct);
        if (ids is null)
            return StatusCode(StatusCodes.Status404NotFound,
                ApiResponse<IReadOnlyList<Guid>>.Failure(ChampionshipMessages.NotFound.Event(lang), "not_found"));

        return Ok(ApiResponse<IReadOnlyList<Guid>>.Success(ChampionshipMessages.EnrollmentSuccess.Retrieved(lang), ids));
    }

    /// <summary>Replaces the whole enrolled-swimmer set for an event. Head Coach or Captain only.</summary>
    [HttpPut("{eventId:guid}/enrollments")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> SetEnrollments(Guid eventId, SetEnrollmentsRequest request, CancellationToken ct)
    {
        var lang = AppLanguage.Current;
        var swimmerIds = request.SwimmerIds ?? new List<Guid>();
        var ok = await _service.SetEnrollmentsAsync(eventId, swimmerIds, ct);
        if (!ok)
            return StatusCode(StatusCodes.Status404NotFound,
                ApiResponse<object>.Failure(ChampionshipMessages.NotFound.Event(lang), "not_found"));

        return Ok(ApiResponse<object>.Success(ChampionshipMessages.EnrollmentSuccess.Saved(lang), null));
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter FullyQualifiedName~ChampionshipsControllerTests`
Expected: PASS.

- [ ] **Step 6: Run the full backend suite (regression)**

Stop the API first. Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: PASS (all projects; the old 371 + the new tests).

- [ ] **Step 7: Commit** (once the hold is lifted)

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/ChampionshipsController.cs \
        backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Application/Resources/ChampionshipMessages.cs \
        backend/tests/Kheprx.BaseBackend.Api.UnitTests/ChampionshipsControllerTests.cs
git commit -m "feat(championships): GET {id} + GET/PUT enrollments endpoints"
```

---

## Frontend

### Task 5: Enrollment DTOs + validators

**Files:**
- Create: `frontend/src/app/features/championships/data/dto/enrollment.dto.ts`
- Test: `frontend/src/app/features/championships/testing/data/dto/enrollment.dto.spec.ts`

**Interfaces:**
- Produces:
  - `interface SetEnrollmentsRq { swimmerIds: string[] }`
  - `interface EnrollmentIdsDtoRs extends BaseResponseRs<string[]> {}`
  - `function isEnrollmentIdsValid(data: unknown): data is string[]`

- [ ] **Step 1: Write the failing test**

```typescript
import { isEnrollmentIdsValid } from '@features/championships/data/dto/enrollment.dto';

describe('enrollment.dto', () => {
  it('accepts an array of strings', () => {
    expect(isEnrollmentIdsValid(['a', 'b'])).toBe(true);
    expect(isEnrollmentIdsValid([])).toBe(true);
  });

  it('rejects non-arrays and non-string members', () => {
    expect(isEnrollmentIdsValid(null)).toBe(false);
    expect(isEnrollmentIdsValid([1, 2])).toBe(false);
    expect(isEnrollmentIdsValid('a')).toBe(false);
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd frontend && npx jest enrollment.dto`
Expected: FAIL — module not found.

- [ ] **Step 3: Write the DTO module**

```typescript
import { BaseResponseRs } from '@core/network/api/base-response-rs';

/** Replace-the-whole-set request for an event's enrollment. */
export interface SetEnrollmentsRq {
  swimmerIds: string[];
}

/** GET enrollments returns the enrolled swimmer ids (Guids serialize as strings). */
export interface EnrollmentIdsDtoRs extends BaseResponseRs<string[]> {}

export function isEnrollmentIdsValid(data: unknown): data is string[] {
  return Array.isArray(data) && data.every((x) => typeof x === 'string');
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `cd frontend && npx jest enrollment.dto`
Expected: PASS.

- [ ] **Step 5: Commit** (once the hold is lifted)

```bash
git add frontend/src/app/features/championships/data/dto/enrollment.dto.ts \
        frontend/src/app/features/championships/testing/data/dto/enrollment.dto.spec.ts
git commit -m "feat(championships): enrollment DTOs + validator"
```

---

### Task 6: Repository — event-by-id + enrollment read/save

**Files:**
- Modify: `frontend/src/app/features/championships/domain/repositories/championships.repository.ts`
- Modify: `frontend/src/app/features/championships/data/repositories/championships.repository.impl.ts`
- Test: `frontend/src/app/features/championships/testing/data/repositories/championships.repository.impl.spec.ts`

**Interfaces:**
- Consumes: `CompetitionEventItemDtoRs` (existing), `EnrollmentIdsDtoRs`, `SetEnrollmentsRq` (Task 5), `HttpClientService`.
- Produces on `IChampionshipsRepository`:
  - `getChampionship(id: string): Promise<CompetitionEventItemDtoRs>`
  - `getEnrollments(eventId: string): Promise<EnrollmentIdsDtoRs>`
  - `setEnrollments(eventId: string, rq: SetEnrollmentsRq): Promise<EnrollmentIdsDtoRs>`

- [ ] **Step 1: Write the failing test**

```typescript
import { TestBed } from '@angular/core/testing';
import { ChampionshipsRepositoryImpl } from '@features/championships/data/repositories/championships.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('ChampionshipsRepositoryImpl (detail + enrollment)', () => {
  const http = { get: jest.fn(), post: jest.fn(), put: jest.fn() } as unknown as HttpClientService;
  let repo: ChampionshipsRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [ChampionshipsRepositoryImpl, { provide: HttpClientService, useValue: http }],
    });
    repo = TestBed.inject(ChampionshipsRepositoryImpl);
  });

  it('getChampionship GETs /api/championships/{id}', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: null });
    await repo.getChampionship('e1');
    expect(http.get).toHaveBeenCalledWith('/api/championships/e1');
  });

  it('getEnrollments GETs the enrollments endpoint', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.getEnrollments('e1');
    expect(http.get).toHaveBeenCalledWith('/api/championships/e1/enrollments');
  });

  it('setEnrollments PUTs the payload as the body', async () => {
    (http.put as jest.Mock).mockResolvedValue({ data: [] });
    const rq = { swimmerIds: ['s1', 's2'] };
    await repo.setEnrollments('e1', rq);
    expect(http.put).toHaveBeenCalledWith('/api/championships/e1/enrollments', { body: rq });
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd frontend && npx jest championships.repository.impl`
Expected: FAIL — methods don't exist.

- [ ] **Step 3: Extend the repository interface**

Add to `IChampionshipsRepository` (and import the new types):

```typescript
  getChampionship(id: string): Promise<CompetitionEventItemDtoRs>;
  getEnrollments(eventId: string): Promise<EnrollmentIdsDtoRs>;
  setEnrollments(eventId: string, rq: SetEnrollmentsRq): Promise<EnrollmentIdsDtoRs>;
```

Update the import line at the top of `championships.repository.ts`:

```typescript
import { CompetitionEventListDtoRs, CompetitionEventItemDtoRs, CreateChampionshipRq } from '@features/championships/data/dto/competition-event.dto';
import { EnrollmentIdsDtoRs, SetEnrollmentsRq } from '@features/championships/data/dto/enrollment.dto';
```

- [ ] **Step 4: Implement in `ChampionshipsRepositoryImpl`**

Add the methods and extend the imports:

```typescript
import { EnrollmentIdsDtoRs, SetEnrollmentsRq } from '@features/championships/data/dto/enrollment.dto';
```

```typescript
  getChampionship(id: string): Promise<CompetitionEventItemDtoRs> {
    return this.http.get<CompetitionEventItemDtoRs>(`/api/championships/${id}`);
  }

  getEnrollments(eventId: string): Promise<EnrollmentIdsDtoRs> {
    return this.http.get<EnrollmentIdsDtoRs>(`/api/championships/${eventId}/enrollments`);
  }

  setEnrollments(eventId: string, rq: SetEnrollmentsRq): Promise<EnrollmentIdsDtoRs> {
    return this.http.put<EnrollmentIdsDtoRs>(`/api/championships/${eventId}/enrollments`, { body: rq });
  }
```

- [ ] **Step 5: Run to verify it passes**

Run: `cd frontend && npx jest championships.repository.impl`
Expected: PASS.

- [ ] **Step 6: Commit** (once the hold is lifted)

```bash
git add frontend/src/app/features/championships/domain/repositories/championships.repository.ts \
        frontend/src/app/features/championships/data/repositories/championships.repository.impl.ts \
        frontend/src/app/features/championships/testing/data/repositories/championships.repository.impl.spec.ts
git commit -m "feat(championships): repository methods for detail + enrollment"
```

---

### Task 7: Use-cases — load championship, load enrollments, save enrollments

**Files:**
- Create: `frontend/src/app/features/championships/domain/usecases/load-championship.use-case.ts`
- Create: `frontend/src/app/features/championships/domain/usecases/load-enrollments.use-case.ts`
- Create: `frontend/src/app/features/championships/domain/usecases/save-enrollments.use-case.ts`
- Test: `frontend/src/app/features/championships/testing/domain/usecases/load-championship.use-case.spec.ts`
- Test: `frontend/src/app/features/championships/testing/domain/usecases/enrollment.use-cases.spec.ts`

**Interfaces:**
- Consumes: `CHAMPIONSHIPS_REPOSITORY`, `toChampionship` (existing mapper), `isCompetitionEventDtoRsValid` (existing), `isEnrollmentIdsValid`, `SetEnrollmentsRq`.
- Produces:
  - `LoadChampionshipUseCase extends UseCase<string, Championship>` — validates + maps a single event; throws `AppError('...','validation')` on bad payload.
  - `LoadEnrollmentsUseCase extends UseCase<string, string[]>` — returns enrolled swimmer ids.
  - `SaveEnrollmentsUseCase extends UseCase<{ eventId: string; swimmerIds: string[] }, string[]>` — PUTs and returns the saved ids.

- [ ] **Step 1: Write the failing tests**

`load-championship.use-case.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { LoadChampionshipUseCase } from '@features/championships/domain/usecases/load-championship.use-case';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';

const validDto = {
  id: 'e1', nameEn: 'Nats', nameAr: null, startDate: '2023-11-15', endDate: '2023-11-16',
  locationEn: 'Cairo', locationAr: null, statusId: 'st1', statusCode: 'upcoming', statusNameEn: 'Upcoming', statusNameAr: null,
};

function make(getChampionship: (id: string) => Promise<unknown>) {
  TestBed.configureTestingModule({
    providers: [
      LoadChampionshipUseCase,
      { provide: CHAMPIONSHIPS_REPOSITORY, useValue: { getChampionship } },
    ],
  });
  return TestBed.inject(LoadChampionshipUseCase);
}

describe('LoadChampionshipUseCase', () => {
  it('maps a valid event', async () => {
    const uc = make(async () => ({ data: validDto }));
    const res = await uc.run('e1');
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data.nameEn).toBe('Nats');
  });

  it('fails on an invalid payload', async () => {
    const uc = make(async () => ({ data: { id: 5 } }));
    const res = await uc.run('e1');
    expect(res.ok).toBe(false);
  });
});
```

`enrollment.use-cases.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { LoadEnrollmentsUseCase } from '@features/championships/domain/usecases/load-enrollments.use-case';
import { SaveEnrollmentsUseCase } from '@features/championships/domain/usecases/save-enrollments.use-case';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';

function make(repo: Record<string, unknown>) {
  TestBed.configureTestingModule({
    providers: [
      LoadEnrollmentsUseCase,
      SaveEnrollmentsUseCase,
      { provide: CHAMPIONSHIPS_REPOSITORY, useValue: repo },
    ],
  });
  return {
    load: TestBed.inject(LoadEnrollmentsUseCase),
    save: TestBed.inject(SaveEnrollmentsUseCase),
  };
}

describe('enrollment use-cases', () => {
  it('LoadEnrollments returns the id list', async () => {
    const { load } = make({ getEnrollments: async () => ({ data: ['s1', 's2'] }) });
    const res = await load.run('e1');
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data).toEqual(['s1', 's2']);
  });

  it('LoadEnrollments fails on an invalid payload', async () => {
    const { load } = make({ getEnrollments: async () => ({ data: [1, 2] }) });
    const res = await load.run('e1');
    expect(res.ok).toBe(false);
  });

  it('SaveEnrollments PUTs the set and returns saved ids', async () => {
    const setEnrollments = jest.fn().mockResolvedValue({ data: ['s1'] });
    const { save } = make({ setEnrollments });
    const res = await save.run({ eventId: 'e1', swimmerIds: ['s1'] });
    expect(setEnrollments).toHaveBeenCalledWith('e1', { swimmerIds: ['s1'] });
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data).toEqual(['s1']);
  });
});
```

- [ ] **Step 2: Run to verify they fail**

Run: `cd frontend && npx jest "load-championship|enrollment.use-cases"`
Expected: FAIL — modules not found.

- [ ] **Step 3: Write `LoadChampionshipUseCase`**

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { isCompetitionEventDtoRsValid } from '@features/championships/data/dto/competition-event.dto';
import { toChampionship } from '@features/championships/data/dto/competition-event.mapper';
import { Championship } from '@features/championships/domain/model/championship';

@Injectable({ providedIn: 'root' })
export class LoadChampionshipUseCase extends UseCase<string, Championship> {
  private readonly repo = inject(CHAMPIONSHIPS_REPOSITORY);
  constructor() { super('LoadChampionship'); }

  protected async execute(id: string): Promise<Championship> {
    const res = await this.repo.getChampionship(id);
    if (!isCompetitionEventDtoRsValid(res.data)) throw new AppError('Invalid championship received', 'validation');
    return toChampionship(res.data);
  }
}
```

- [ ] **Step 4: Write `LoadEnrollmentsUseCase`**

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { isEnrollmentIdsValid } from '@features/championships/data/dto/enrollment.dto';

@Injectable({ providedIn: 'root' })
export class LoadEnrollmentsUseCase extends UseCase<string, string[]> {
  private readonly repo = inject(CHAMPIONSHIPS_REPOSITORY);
  constructor() { super('LoadEnrollments'); }

  protected async execute(eventId: string): Promise<string[]> {
    const res = await this.repo.getEnrollments(eventId);
    if (!isEnrollmentIdsValid(res.data)) throw new AppError('Invalid enrollments received', 'validation');
    return res.data;
  }
}
```

- [ ] **Step 5: Write `SaveEnrollmentsUseCase`**

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { isEnrollmentIdsValid } from '@features/championships/data/dto/enrollment.dto';

export interface SaveEnrollmentsInput {
  eventId: string;
  swimmerIds: string[];
}

@Injectable({ providedIn: 'root' })
export class SaveEnrollmentsUseCase extends UseCase<SaveEnrollmentsInput, string[]> {
  private readonly repo = inject(CHAMPIONSHIPS_REPOSITORY);
  constructor() { super('SaveEnrollments'); }

  protected async execute(input: SaveEnrollmentsInput): Promise<string[]> {
    const res = await this.repo.setEnrollments(input.eventId, { swimmerIds: input.swimmerIds });
    if (!isEnrollmentIdsValid(res.data)) throw new AppError('Invalid enrollments received', 'validation');
    return res.data;
  }
}
```

- [ ] **Step 6: Run to verify they pass**

Run: `cd frontend && npx jest "load-championship|enrollment.use-cases"`
Expected: PASS.

- [ ] **Step 7: Commit** (once the hold is lifted)

```bash
git add frontend/src/app/features/championships/domain/usecases/load-championship.use-case.ts \
        frontend/src/app/features/championships/domain/usecases/load-enrollments.use-case.ts \
        frontend/src/app/features/championships/domain/usecases/save-enrollments.use-case.ts \
        frontend/src/app/features/championships/testing/domain/usecases/load-championship.use-case.spec.ts \
        frontend/src/app/features/championships/testing/domain/usecases/enrollment.use-cases.spec.ts
git commit -m "feat(championships): detail + enrollment use-cases"
```

---

### Task 8: Detail ViewModel

**Files:**
- Create: `frontend/src/app/features/championships/presentation/pages/championship-detail/championship-detail.viewmodel.ts`
- Test: `frontend/src/app/features/championships/testing/presentation/pages/championship-detail/championship-detail.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `LoadChampionshipUseCase`, `LoadEnrollmentsUseCase`, `SaveEnrollmentsUseCase` (Task 7), `ListSwimmersUseCase` (`@features/swimmers/domain/usecases/list-swimmers.use-case`), `SwimmerListItem` (`@features/swimmers/domain/model/swimmer`), `LanguageStore`, `TranslateService`, `NotificationService`, `AuthSessionStore`, `Championship`, `formatDateRange`.
- Produces class `ChampionshipDetailViewModel` with:
  - signals `loading`, `error`, `notFound`, `championship`, `roster`, `search`, `saving`, `activeTab`
  - computeds `canManage`, `filtered()`, `enrolledCount()`, `total()`, `dirty()`
  - methods `load(id)`, `setTab(k)`, `isEnrolled(id)`, `toggle(id)`, `setSearch(v)`, `save()`, `name()`, `location()`, `dateRange()`, `swimmerName(s)`, `initials(s)`, `club(s)`

- [ ] **Step 1: Write the failing test**

```typescript
import { TestBed } from '@angular/core/testing';
import { ok } from '@core/domain/result/result';
import { LanguageStore } from '@core/i18n/language.store';
import { TranslateService } from '@core/i18n';
import { NotificationService } from '@core/ui/notification.service';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { ChampionshipDetailViewModel } from '@features/championships/presentation/pages/championship-detail/championship-detail.viewmodel';
import { LoadChampionshipUseCase } from '@features/championships/domain/usecases/load-championship.use-case';
import { LoadEnrollmentsUseCase } from '@features/championships/domain/usecases/load-enrollments.use-case';
import { SaveEnrollmentsUseCase } from '@features/championships/domain/usecases/save-enrollments.use-case';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { Championship } from '@features/championships/domain/model/championship';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';
import { AppError } from '@core/domain/errors/app-error';

const champ: Championship = {
  id: 'e1', nameEn: 'National Junior', nameAr: 'الناشئين', startDate: '2023-11-15', endDate: '2023-11-16',
  locationEn: 'Cairo', locationAr: 'القاهرة', statusId: 's1', statusCode: 'upcoming', statusNameEn: 'Upcoming', statusNameAr: null,
};
const roster: SwimmerListItem[] = [
  { id: 's1', uid: 'U1', nameEn: 'Ahmed', nameAr: 'أحمد', clubNameEn: 'Oasis', clubNameAr: null, gender: 'male', age: 18 },
  { id: 's2', uid: 'U2', nameEn: 'Sara', nameAr: 'سارة', clubNameEn: 'Oasis', clubNameAr: null, gender: 'female', age: 16 },
  { id: 's3', uid: 'U3', nameEn: 'Omar', nameAr: 'عمر', clubNameEn: 'North', clubNameAr: null, gender: 'male', age: 15 },
];

interface Opts {
  role?: string;
  champRun?: jest.Mock;
  enrRun?: jest.Mock;
  saveRun?: jest.Mock;
  rosterRun?: jest.Mock;
}

function setup(opts: Opts = {}) {
  const champRun = opts.champRun ?? jest.fn().mockResolvedValue(ok(champ));
  const enrRun = opts.enrRun ?? jest.fn().mockResolvedValue(ok(['s1']));
  const saveRun = opts.saveRun ?? jest.fn().mockResolvedValue(ok(['s1']));
  const rosterRun = opts.rosterRun ?? jest.fn().mockResolvedValue(ok(roster));
  const notify = { success: jest.fn(), error: jest.fn() };
  TestBed.configureTestingModule({
    providers: [
      ChampionshipDetailViewModel,
      { provide: LoadChampionshipUseCase, useValue: { run: champRun } },
      { provide: LoadEnrollmentsUseCase, useValue: { run: enrRun } },
      { provide: SaveEnrollmentsUseCase, useValue: { run: saveRun } },
      { provide: ListSwimmersUseCase, useValue: { run: rosterRun } },
      { provide: LanguageStore, useValue: { lang: () => 'en' } },
      { provide: AuthSessionStore, useValue: { role: () => opts.role ?? 'head_coach' } },
      { provide: NotificationService, useValue: notify },
      { provide: TranslateService, useValue: { t: (k: string) => k } },
    ],
  });
  return { vm: TestBed.inject(ChampionshipDetailViewModel), champRun, enrRun, saveRun, rosterRun, notify };
}

describe('ChampionshipDetailViewModel', () => {
  it('loads the event, roster and enrolled set', async () => {
    const { vm } = setup();
    await vm.load('e1');
    expect(vm.loading()).toBe(false);
    expect(vm.total()).toBe(3);
    expect(vm.enrolledCount()).toBe(1);
    expect(vm.isEnrolled('s1')).toBe(true);
    expect(vm.isEnrolled('s2')).toBe(false);
  });

  it('sets notFound on a 404', async () => {
    const champRun = jest.fn().mockResolvedValue({ ok: false, error: new AppError('x', 'http', 404) });
    const { vm } = setup({ champRun });
    await vm.load('e1');
    expect(vm.notFound()).toBe(true);
  });

  it('sets error on a non-404 failure', async () => {
    const champRun = jest.fn().mockResolvedValue({ ok: false, error: new AppError('x', 'network') });
    const { vm } = setup({ champRun });
    await vm.load('e1');
    expect(vm.error()).toBe(true);
  });

  it('toggle adds/removes and drives dirty + count', async () => {
    const { vm } = setup();
    await vm.load('e1');
    expect(vm.dirty()).toBe(false);
    vm.toggle('s2');
    expect(vm.isEnrolled('s2')).toBe(true);
    expect(vm.enrolledCount()).toBe(2);
    expect(vm.dirty()).toBe(true);
    vm.toggle('s2');
    expect(vm.dirty()).toBe(false);   // back to the baseline set
  });

  it('toggle is a no-op for a non-manager', async () => {
    const { vm } = setup({ role: 'swimmer' });
    await vm.load('e1');
    vm.toggle('s2');
    expect(vm.isEnrolled('s2')).toBe(false);
    expect(vm.dirty()).toBe(false);
  });

  it('filters the roster by name (EN + AR)', async () => {
    const { vm } = setup();
    await vm.load('e1');
    vm.setSearch('sara');
    expect(vm.filtered().map((s) => s.id)).toEqual(['s2']);
    vm.setSearch('عمر');
    expect(vm.filtered().map((s) => s.id)).toEqual(['s3']);
  });

  it('save persists the working set, toasts success and clears dirty', async () => {
    const { vm, saveRun, notify } = setup();
    await vm.load('e1');
    vm.toggle('s2');
    saveRun.mockResolvedValue(ok(['s1', 's2']));
    await vm.save();
    expect(saveRun).toHaveBeenCalledWith({ eventId: 'e1', swimmerIds: ['s1', 's2'] });
    expect(notify.success).toHaveBeenCalled();
    expect(vm.dirty()).toBe(false);
  });

  it('save surfaces an error toast and keeps the working set on failure', async () => {
    const saveRun = jest.fn().mockResolvedValue({ ok: false, error: new AppError('x', 'network') });
    const { vm, notify } = setup({ saveRun });
    await vm.load('e1');
    vm.toggle('s2');
    await vm.save();
    expect(notify.error).toHaveBeenCalled();
    expect(vm.isEnrolled('s2')).toBe(true);
    expect(vm.dirty()).toBe(true);
  });

  it('save is a no-op for a non-manager', async () => {
    const saveRun = jest.fn();
    const { vm } = setup({ role: 'swimmer', saveRun });
    await vm.load('e1');
    await vm.save();
    expect(saveRun).not.toHaveBeenCalled();
  });

  it('canManage reflects the role', () => {
    expect(setup({ role: 'captain' }).vm.canManage()).toBe(true);
    expect(setup({ role: 'swimmer' }).vm.canManage()).toBe(false);
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd frontend && npx jest championship-detail.viewmodel`
Expected: FAIL — module not found.

- [ ] **Step 3: Write the ViewModel**

```typescript
import { Injectable, computed, inject, signal } from '@angular/core';
import { LanguageStore } from '@core/i18n/language.store';
import { TranslateService } from '@core/i18n';
import { NotificationService } from '@core/ui/notification.service';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { LoadChampionshipUseCase } from '@features/championships/domain/usecases/load-championship.use-case';
import { LoadEnrollmentsUseCase } from '@features/championships/domain/usecases/load-enrollments.use-case';
import { SaveEnrollmentsUseCase } from '@features/championships/domain/usecases/save-enrollments.use-case';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';
import { Championship } from '@features/championships/domain/model/championship';
import { formatDateRange } from '@features/championships/presentation/pages/championships/format-date-range';

export type DetailTab = 'enrollment' | 'days' | 'finished' | 'results';

@Injectable()
export class ChampionshipDetailViewModel {
  private readonly loadChampionshipUc = inject(LoadChampionshipUseCase);
  private readonly loadEnrollmentsUc = inject(LoadEnrollmentsUseCase);
  private readonly saveEnrollmentsUc = inject(SaveEnrollmentsUseCase);
  private readonly listSwimmersUc = inject(ListSwimmersUseCase);
  private readonly language = inject(LanguageStore);
  private readonly session = inject(AuthSessionStore);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  private eventId = '';

  readonly loading = signal(true);
  readonly error = signal(false);
  readonly notFound = signal(false);
  readonly saving = signal(false);
  readonly championship = signal<Championship | null>(null);
  readonly roster = signal<SwimmerListItem[]>([]);
  readonly search = signal('');
  readonly activeTab = signal<DetailTab>('enrollment');

  // Enrolled set the server returned (baseline) vs the user's working edits.
  private readonly baseline = signal<ReadonlySet<string>>(new Set());
  private readonly working = signal<ReadonlySet<string>>(new Set());

  readonly canManage = computed(() => {
    const r = this.session.role();
    return r === 'head_coach' || r === 'captain';
  });

  readonly total = computed(() => this.roster().length);
  readonly enrolledCount = computed(() => this.working().size);

  readonly dirty = computed(() => {
    const a = this.baseline();
    const b = this.working();
    if (a.size !== b.size) return true;
    for (const id of b) if (!a.has(id)) return true;
    return false;
  });

  readonly filtered = computed<SwimmerListItem[]>(() => {
    const q = this.search().trim().toLowerCase();
    if (!q) return this.roster();
    return this.roster().filter(
      (s) => s.nameEn.toLowerCase().includes(q) || (s.nameAr ?? '').toLowerCase().includes(q),
    );
  });

  async load(id: string): Promise<void> {
    this.eventId = id;
    this.activeTab.set('enrollment');
    this.search.set('');
    this.loading.set(true);
    this.error.set(false);
    this.notFound.set(false);

    const champRes = await this.loadChampionshipUc.run(id);
    if (!champRes.ok) {
      this.loading.set(false);
      this.championship.set(null);
      if (champRes.error.status === 404) this.notFound.set(true);
      else this.error.set(true);
      return;
    }
    this.championship.set(champRes.data);

    const [rosterRes, enrRes] = await Promise.all([
      this.listSwimmersUc.run(undefined),
      this.loadEnrollmentsUc.run(id),
    ]);
    this.roster.set(rosterRes.ok ? rosterRes.data : []);
    const enrolled = new Set(enrRes.ok ? enrRes.data : []);
    this.baseline.set(new Set(enrolled));
    this.working.set(new Set(enrolled));
    if (!rosterRes.ok || !enrRes.ok) this.error.set(true);
    this.loading.set(false);
  }

  setTab(tab: DetailTab): void { this.activeTab.set(tab); }
  setSearch(v: string): void { this.search.set(v); }
  isEnrolled(swimmerId: string): boolean { return this.working().has(swimmerId); }

  toggle(swimmerId: string): void {
    if (!this.canManage()) return;
    const next = new Set(this.working());
    if (next.has(swimmerId)) next.delete(swimmerId);
    else next.add(swimmerId);
    this.working.set(next);
  }

  async save(): Promise<void> {
    if (!this.canManage() || this.saving() || !this.dirty()) return;
    this.saving.set(true);
    try {
      const swimmerIds = Array.from(this.working());
      const res = await this.saveEnrollmentsUc.run({ eventId: this.eventId, swimmerIds });
      if (res.ok) {
        this.notify.success(this.i18n.t('championships.enrollment.saved'));
        this.baseline.set(new Set(res.data));
        this.working.set(new Set(res.data));
      } else {
        this.notify.error(this.i18n.t('championships.enrollment.saveFailed'));
      }
    } finally {
      this.saving.set(false);
    }
  }

  name(): string {
    const c = this.championship();
    if (!c) return '';
    return this.language.lang() === 'ar' ? (c.nameAr ?? c.nameEn) : c.nameEn;
  }
  location(): string {
    const c = this.championship();
    if (!c) return '';
    return (this.language.lang() === 'ar' ? (c.locationAr ?? c.locationEn) : c.locationEn) ?? '';
  }
  dateRange(): string {
    const c = this.championship();
    if (!c) return '';
    return formatDateRange(c.startDate, c.endDate, this.language.lang() === 'ar' ? 'ar' : 'en');
  }
  swimmerName(s: SwimmerListItem): string {
    return this.language.lang() === 'ar' ? (s.nameAr ?? s.nameEn) : s.nameEn;
  }
  club(s: SwimmerListItem): string {
    return (this.language.lang() === 'ar' ? (s.clubNameAr ?? s.clubNameEn) : s.clubNameEn) ?? '';
  }
  initials(s: SwimmerListItem): string {
    return this.swimmerName(s).split(' ').map((p) => p[0]).join('').slice(0, 2).toUpperCase();
  }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `cd frontend && npx jest championship-detail.viewmodel`
Expected: PASS.

- [ ] **Step 5: Commit** (once the hold is lifted)

```bash
git add frontend/src/app/features/championships/presentation/pages/championship-detail/championship-detail.viewmodel.ts \
        frontend/src/app/features/championships/testing/presentation/pages/championship-detail/championship-detail.viewmodel.spec.ts
git commit -m "feat(championships): championship detail view-model"
```

---

### Task 9: Detail page (tabs + Enrollment UI) + i18n + routing + barrel

**Files:**
- Create: `frontend/src/app/features/championships/presentation/pages/championship-detail/championship-detail.page.ts`
- Create: `frontend/src/app/features/championships/presentation/pages/championship-detail/championship-detail.page.html`
- Modify: `frontend/src/app/features/championships/index.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`
- Test: `frontend/src/app/features/championships/testing/presentation/pages/championship-detail/championship-detail.page.spec.ts`

**Interfaces:**
- Consumes: `ChampionshipDetailViewModel` (Task 8), `ActivatedRoute`, `TranslatePipe`, `FormsModule`, `RouterLink`.
- Produces: `ChampionshipDetailPage` (exported from the barrel; lazy-loaded on `championships/:id`).

- [ ] **Step 1: Add i18n keys**

In `en.json`, inside the existing `"championships"` object, add a `detail` and `enrollment` block (place after `"form": { … }`, adding a comma after the `form` block):

```json
    "detail": {
      "back": "Back",
      "comingSoon": "Coming soon",
      "loading": "Loading championship…",
      "notFound": "Championship not found.",
      "error": "Couldn't load this championship.",
      "tabs": {
        "enrollment": "Enrollment",
        "days": "Competition Days",
        "finished": "Finished races",
        "results": "Results"
      }
    },
    "enrollment": {
      "title": "Swimmer Enrollment",
      "search": "Search swimmers…",
      "save": "Save",
      "saved": "Enrollment saved",
      "saveFailed": "Couldn't save the enrollment.",
      "emptyRoster": "No swimmers to enroll yet.",
      "years": "yrs"
    }
```

In `ar.json`, mirror it inside `"championships"`:

```json
    "detail": {
      "back": "العودة",
      "comingSoon": "قريبًا",
      "loading": "جارٍ تحميل البطولة…",
      "notFound": "البطولة غير موجودة.",
      "error": "تعذّر تحميل هذه البطولة.",
      "tabs": {
        "enrollment": "التسجيل",
        "days": "أيام البطولة",
        "finished": "سباقات منتهية",
        "results": "النتائج"
      }
    },
    "enrollment": {
      "title": "تسجيل السباحين",
      "search": "بحث عن سباحين…",
      "save": "حفظ",
      "saved": "تم حفظ التسجيل",
      "saveFailed": "تعذّر حفظ التسجيل.",
      "emptyRoster": "لا يوجد سباحون للتسجيل بعد.",
      "years": "سنة"
    }
```

- [ ] **Step 2: Write the page component**

```typescript
import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@core/i18n';
import { ChampionshipDetailViewModel, DetailTab } from './championship-detail.viewmodel';

interface DetailTabDef { key: DetailTab; labelKey: string; }

@Component({
  selector: 'app-championship-detail-page',
  standalone: true,
  imports: [FormsModule, TranslatePipe, RouterLink],
  templateUrl: './championship-detail.page.html',
})
export class ChampionshipDetailPage implements OnInit {
  readonly vm = inject(ChampionshipDetailViewModel);
  private readonly route = inject(ActivatedRoute);

  // Full strip for visual fidelity; only 'enrollment' is enabled this pass.
  protected readonly enabledTabs = new Set<DetailTab>(['enrollment']);
  isEnabled(key: DetailTab): boolean { return this.enabledTabs.has(key); }

  protected readonly tabs: DetailTabDef[] = [
    { key: 'enrollment', labelKey: 'championships.detail.tabs.enrollment' },
    { key: 'days', labelKey: 'championships.detail.tabs.days' },
    { key: 'finished', labelKey: 'championships.detail.tabs.finished' },
    { key: 'results', labelKey: 'championships.detail.tabs.results' },
  ];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    void this.vm.load(id);
  }
}
```

- [ ] **Step 3: Write the page template**

```html
<div class="mx-auto max-w-5xl px-4 py-8 sm:px-6">
  <a routerLink="/championships" class="mb-4 inline-flex items-center gap-1.5 text-sm font-medium text-text-secondary hover:text-primary">
    <span aria-hidden="true">←</span> {{ 'championships.detail.back' | translate }}
  </a>

  @if (vm.loading()) {
    <p class="text-text-secondary">{{ 'championships.detail.loading' | translate }}</p>
  } @else if (vm.notFound()) {
    <p class="text-danger">{{ 'championships.detail.notFound' | translate }}</p>
  } @else if (vm.error() && !vm.championship()) {
    <p class="text-danger">{{ 'championships.detail.error' | translate }}</p>
  } @else if (vm.championship(); as c) {
    <!-- Event header -->
    <div class="mb-6 rounded-2xl border border-border bg-surface p-6 shadow-sm">
      <h1 class="font-heading text-2xl text-ink">{{ vm.name() }}</h1>
      <p class="mt-1 text-sm text-text-secondary">{{ vm.dateRange() }} · {{ vm.location() }}</p>
    </div>

    <!-- Tab strip: only Enrollment enabled -->
    <div class="mb-6 flex flex-wrap gap-2 border-b border-border pb-2">
      @for (t of tabs; track t.key) {
        <button type="button"
                class="rounded-md px-3 py-1.5 text-sm"
                [class.bg-primary]="t.key === vm.activeTab()"
                [class.text-white]="t.key === vm.activeTab()"
                [class.text-text-secondary]="t.key !== vm.activeTab()"
                [class.opacity-50]="!isEnabled(t.key)"
                [class.cursor-not-allowed]="!isEnabled(t.key)"
                [disabled]="!isEnabled(t.key)"
                [attr.aria-disabled]="!isEnabled(t.key)"
                [attr.title]="!isEnabled(t.key) ? ('championships.detail.comingSoon' | translate) : null"
                (click)="isEnabled(t.key) && vm.setTab(t.key)">
          {{ t.labelKey | translate }}
        </button>
      }
    </div>

    @if (vm.activeTab() === 'enrollment') {
      <section class="rounded-2xl border border-border bg-surface shadow-sm">
        <!-- Header: counter + Save -->
        <div class="flex flex-col gap-3 border-b border-border p-5 sm:flex-row sm:items-center sm:justify-between">
          <h2 class="font-heading text-xl text-ink">{{ 'championships.enrollment.title' | translate }}</h2>
          <div class="flex items-center gap-3">
            <span class="text-sm text-text-secondary">
              <span class="font-bold text-primary">{{ vm.enrolledCount() }}</span> / {{ vm.total() }}
            </span>
            @if (vm.canManage()) {
              <button type="button" (click)="vm.save()" [disabled]="!vm.dirty() || vm.saving()"
                      class="inline-flex h-9 items-center rounded-lg bg-primary px-4 text-sm font-medium text-white hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-50">
                {{ 'championships.enrollment.save' | translate }}
              </button>
            }
          </div>
        </div>

        <!-- Search -->
        <div class="border-b border-border p-4">
          <input type="text" [ngModel]="vm.search()" (ngModelChange)="vm.setSearch($event)"
                 [placeholder]="'championships.enrollment.search' | translate"
                 class="h-10 w-full max-w-md rounded-lg border border-border bg-surface px-3 text-sm text-ink" />
        </div>

        <!-- Roster -->
        @if (vm.filtered().length === 0) {
          <p class="p-6 text-center text-sm text-text-secondary">{{ 'championships.enrollment.emptyRoster' | translate }}</p>
        } @else {
          <ul class="divide-y divide-border">
            @for (s of vm.filtered(); track s.id) {
              <li class="flex items-center gap-4 p-4" [class.bg-primary]="vm.isEnrolled(s.id)" [class.bg-opacity-5]="vm.isEnrolled(s.id)">
                <input type="checkbox" [checked]="vm.isEnrolled(s.id)" [disabled]="!vm.canManage()"
                       (change)="vm.toggle(s.id)"
                       [attr.aria-label]="vm.swimmerName(s)"
                       class="h-5 w-5 rounded border-border text-primary" />
                <div class="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-muted text-xs font-bold text-text-secondary">
                  {{ vm.initials(s) }}
                </div>
                <div class="min-w-0">
                  <p class="truncate font-medium text-ink">{{ vm.swimmerName(s) }}</p>
                  <p class="text-xs text-text-secondary">
                    {{ vm.club(s) }}@if (s.age !== null) { · {{ s.age }} {{ 'championships.enrollment.years' | translate }} }
                  </p>
                </div>
              </li>
            }
          </ul>
        }
      </section>
    }
  }
</div>
```

- [ ] **Step 4: Export from the barrel**

In `features/championships/index.ts`, add:

```typescript
export { ChampionshipDetailPage } from './presentation/pages/championship-detail/championship-detail.page';
export { ChampionshipDetailViewModel } from './presentation/pages/championship-detail/championship-detail.viewmodel';
```

- [ ] **Step 5: Register the route**

In `app.routes.ts`, add `ChampionshipDetailViewModel` to the existing `@features/championships` import, and add this route directly **after** the existing `championships` route (so `championships/:id` is a sibling):

```typescript
      {
        path: 'championships/:id',
        canActivate: [firstLoginGuard],
        loadComponent: () => import('@features/championships').then((m) => m.ChampionshipDetailPage),
        providers: [ChampionshipDetailViewModel],
      },
```

Update the import line:

```typescript
import { ChampionshipsViewModel, ChampionshipDetailViewModel } from '@features/championships';
```

- [ ] **Step 6: Write the page test**

```typescript
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { ok } from '@core/domain/result/result';
import { LanguageStore } from '@core/i18n/language.store';
import { TranslateService } from '@core/i18n';
import { NotificationService } from '@core/ui/notification.service';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { ChampionshipDetailPage } from '@features/championships/presentation/pages/championship-detail/championship-detail.page';
import { ChampionshipDetailViewModel } from '@features/championships/presentation/pages/championship-detail/championship-detail.viewmodel';
import { LoadChampionshipUseCase } from '@features/championships/domain/usecases/load-championship.use-case';
import { LoadEnrollmentsUseCase } from '@features/championships/domain/usecases/load-enrollments.use-case';
import { SaveEnrollmentsUseCase } from '@features/championships/domain/usecases/save-enrollments.use-case';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { Championship } from '@features/championships/domain/model/championship';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';

const champ: Championship = {
  id: 'e1', nameEn: 'National Junior Championship', nameAr: null, startDate: '2023-11-15', endDate: '2023-11-16',
  locationEn: 'Cairo Olympic Pool', locationAr: null, statusId: 's1', statusCode: 'upcoming', statusNameEn: 'Upcoming', statusNameAr: null,
};
const roster: SwimmerListItem[] = [
  { id: 's1', uid: 'U1', nameEn: 'Ahmed Al-Rashidi', nameAr: null, clubNameEn: 'Oasis', clubNameAr: null, gender: 'male', age: 18 },
];

describe('ChampionshipDetailPage', () => {
  it('renders the event header, the enabled + disabled tabs and the roster', async () => {
    TestBed.configureTestingModule({
      imports: [ChampionshipDetailPage],
      providers: [
        ChampionshipDetailViewModel,
        { provide: LoadChampionshipUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(champ)) } },
        { provide: LoadEnrollmentsUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(['s1'])) } },
        { provide: SaveEnrollmentsUseCase, useValue: { run: jest.fn() } },
        { provide: ListSwimmersUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(roster)) } },
        { provide: LanguageStore, useValue: { lang: () => 'en' } },
        { provide: AuthSessionStore, useValue: { role: () => 'head_coach' } },
        { provide: NotificationService, useValue: { success: jest.fn(), error: jest.fn() } },
        { provide: TranslateService, useValue: { t: (k: string) => k } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'e1' } } } },
      ],
    });
    const fixture = TestBed.createComponent(ChampionshipDetailPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('National Junior Championship');
    expect(text).toContain('Ahmed Al-Rashidi');

    // The three unbuilt tabs are disabled; Enrollment is not.
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
    const disabledLabels = buttons.filter((b) => b.disabled).map((b) => b.textContent?.trim());
    expect(disabledLabels).toEqual(
      expect.arrayContaining([
        'championships.detail.tabs.days',
        'championships.detail.tabs.finished',
        'championships.detail.tabs.results',
      ]),
    );
  });
});
```

- [ ] **Step 7: Run the detail-page test + the i18n key-parity check**

Run: `cd frontend && npx jest championship-detail.page`
Expected: PASS.

Then verify EN/AR structural parity (the repo has an i18n parity check — run the full suite in Step 8; if a standalone script exists under `frontend/scripts`, run it). Manually confirm both JSON files parse (no trailing-comma errors).

- [ ] **Step 8: Run the whole frontend suite (regression)**

Run: `cd frontend && npx jest`
Expected: PASS (existing championships specs + all new specs).

- [ ] **Step 9: Commit** (once the hold is lifted)

```bash
git add frontend/src/app/features/championships/presentation/pages/championship-detail \
        frontend/src/app/features/championships/index.ts \
        frontend/src/app/app.routes.ts \
        frontend/src/app/core/i18n/en.json frontend/src/app/core/i18n/ar.json \
        frontend/src/app/features/championships/testing/presentation/pages/championship-detail
git commit -m "feat(championships): detail page with Enrollment tab + route + i18n"
```

---

### Task 10: List → detail navigation

**Files:**
- Modify: `frontend/src/app/features/championships/presentation/pages/championships/championships.page.ts`
- Modify: `frontend/src/app/features/championships/presentation/pages/championships/championships.page.html`
- Test: `frontend/src/app/features/championships/testing/presentation/pages/championships/championships.page.spec.ts` (add a case)

**Interfaces:**
- Consumes: Angular `RouterLink`.
- Produces: each list row links to `/championships/{id}`.

- [ ] **Step 1: Add the failing navigation test** (append inside the existing `describe('ChampionshipsPage', …)`)

First extend the existing test module setup to import `RouterModule.forRoot([])` so `routerLink` resolves. Add this test:

```typescript
  it('links each row to its detail route', async () => {
    const run = jest.fn().mockResolvedValue(ok(sample));
    TestBed.configureTestingModule({
      imports: [ChampionshipsPage, RouterModule.forRoot([])],
      providers: [
        ChampionshipsViewModel,
        { provide: LoadChampionshipsUseCase, useValue: { run } },
        { provide: CreateChampionshipUseCase, useValue: { run: jest.fn() } },
        { provide: LanguageStore, useValue: { lang: () => 'en' } },
        { provide: AuthSessionStore, useValue: { role: () => 'head_coach' } },
        { provide: NotificationService, useValue: { success: jest.fn(), error: jest.fn() } },
        { provide: TranslateService, useValue: { t: (k: string) => k } },
      ],
    });
    const fixture = TestBed.createComponent(ChampionshipsPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const link = fixture.nativeElement.querySelector('a[href="/championships/1"]');
    expect(link).toBeTruthy();
  });
```

Add the import at the top of the spec file:

```typescript
import { RouterModule } from '@angular/router';
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd frontend && npx jest championships.page`
Expected: FAIL — no `a[href="/championships/1"]`.

- [ ] **Step 3: Import `RouterLink` in the list page**

In `championships.page.ts`:

```typescript
import { RouterLink } from '@angular/router';
```

and add `RouterLink` to the component's `imports` array.

- [ ] **Step 4: Make rows navigable**

In `championships.page.html`, wrap the championship name cell in a link (replace the current name cell `<td>`):

```html
                <td class="px-5 py-3.5">
                  <a [routerLink]="['/championships', e.id]" class="text-sm font-medium text-primary hover:underline">{{ vm.name(e) }}</a>
                </td>
```

- [ ] **Step 5: Run to verify it passes**

Run: `cd frontend && npx jest championships.page`
Expected: PASS (both the render test and the new link test).

- [ ] **Step 6: Commit** (once the hold is lifted)

```bash
git add frontend/src/app/features/championships/presentation/pages/championships/championships.page.ts \
        frontend/src/app/features/championships/presentation/pages/championships/championships.page.html \
        frontend/src/app/features/championships/testing/presentation/pages/championships/championships.page.spec.ts
git commit -m "feat(championships): navigate from the list to the detail page"
```

---

### Task 11: Apply migration + seed Aiven `Swimming_Production` (needs the password — CHECKPOINT)

**Files:**
- Create: `scripts/seed-championships-enrollment-aiven.sql`

**This task requires the user to provide the Aiven `Swimming_Production` connection password and to have approved the run** (classifier). Stop and ask for it before running anything against Aiven.

- [ ] **Step 1: Write the idempotent seed script**

```sql
-- seed-championships-enrollment-aiven.sql
-- Enrolls a handful of real swimmers into the 3 demo events from
-- seed-championships-aiven.sql so the Enrollment tab shows data.
-- Idempotent (ON CONFLICT on the unique (EventId, SwimmerId) index).
-- NOTE: EF column names are PascalCase -> must be double-quoted in Postgres.

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- The first 4 swimmers (deterministic order) go into the two dated events.
WITH pick AS (
  SELECT "Id" AS swimmer_id, row_number() OVER (ORDER BY "Id") AS rn
  FROM identity.swimmer_profile
)
INSERT INTO championships.championship_enrollment ("Id","EventId","SwimmerId")
SELECT gen_random_uuid(), ev.event_id, pick.swimmer_id
FROM (
  VALUES
    ('33333333-3333-3333-3333-333333333301'::uuid),   -- National Junior → 4 swimmers
    ('33333333-3333-3333-3333-333333333302'::uuid)    -- Regional Sprint → 4 swimmers
) AS ev(event_id)
JOIN pick ON pick.rn <= 4
WHERE EXISTS (SELECT 1 FROM championships.competition_event e WHERE e."Id" = ev.event_id)
ON CONFLICT ("EventId","SwimmerId") DO NOTHING;
-- Winter Open (…3303) intentionally left with zero enrollments.

COMMIT;

-- Verify:
--   SELECT e."NameEn", count(en.*) AS enrolled
--   FROM championships.competition_event e
--   LEFT JOIN championships.championship_enrollment en ON en."EventId" = e."Id"
--   GROUP BY e."NameEn" ORDER BY e."NameEn";
```

- [ ] **Step 2: Apply the migration to Aiven** (API stopped; password from the user)

```bash
dotnet ef database update \
  --project backend/src/Modules/Championships/Kheprx.BaseBackend.Championships.Infrastructure \
  --context ChampionshipsDbContext \
  --connection "Host=kheprx-service-kheprx.b.aivencloud.com;Port=14647;Database=Swimming_Production;Username=avnadmin;Password=<PASSWORD>;SslMode=Require"
```

Expected: the migration applies; `dotnet ef migrations list --context ChampionshipsDbContext --connection …` shows 0 pending.

- [ ] **Step 3: Run the seed against Aiven**

Run the SQL in `scripts/seed-championships-enrollment-aiven.sql` against `Swimming_Production` (via `psql` or the client the team uses). Then run the verify query and confirm: National Junior = 4, Regional Sprint = 4, Winter Open = 0 (counts may be lower if fewer than 4 swimmers exist).

- [ ] **Step 4: Manual end-to-end check**

Start the API + frontend, log in as a head coach, open `/championships`, click **National Junior Championship**, confirm the Enrollment tab shows 4 pre-ticked swimmers and `4 / N`; toggle one, Save, reload, confirm it persisted. Open **Winter Open** and confirm `0 / N`.

- [ ] **Step 5: Commit** (once the hold is lifted)

```bash
git add scripts/seed-championships-enrollment-aiven.sql
git commit -m "chore(championships): seed enrollment demo data for Aiven"
```

---

## Self-Review

**Spec coverage:**
- Table `championship_enrollment` (Id/EventId/SwimmerId, unique) → Task 2. ✅
- Loose Guids, no cross-context FK → Task 2 config (no `HasForeignKey`). ✅
- `GET /{id}` (status resolved, 404) → Tasks 3, 4. ✅
- `GET /{eventId}/enrollments` (ids, 404) → Tasks 3, 4. ✅
- `PUT /{eventId}/enrollments` (atomic replace, 200/404/403 role) → Tasks 2 (ReplaceAsync), 3, 4. ✅
- Ids-only over the wire → Tasks 4, 5. ✅
- Frontend DTO/repo/use-cases → Tasks 5–7. ✅
- Roster reuse via `ListSwimmersUseCase` → Task 8. ✅
- Detail VM (working/baseline/dirty/canManage/filter/save) → Task 8. ✅
- Detail page: header + full tab strip (Enrollment enabled, 3 disabled) + checklist + counter + Save → Task 9. ✅
- Route `/championships/:id`, barrel, i18n EN+AR → Task 9. ✅
- List → detail navigation → Task 10. ✅
- Migration + Aiven seed (needs password) → Tasks 2 (add), 11 (apply+seed). ✅
- Non-manager read-only (checkboxes + Save disabled) → Task 9 template (`[disabled]="!vm.canManage()"`, Save hidden). ✅
- Testing backend (repo/service/controller incl. 403 attribute) + frontend (dto/repo/use-cases/vm/page/nav) → each task's tests. ✅
- Out-of-scope tabs stay disabled, nothing else built. ✅

**Placeholder scan:** No TBD/TODO; every code step has concrete content. The only intentional runtime placeholder is `<PASSWORD>` in Task 11 Step 2, which is a user-supplied secret by design (flagged as a checkpoint). ✅

**Type consistency:**
- `ChampionshipService` ctor arity changes from 1 → 2 args; Task 3 Step 8 updates the pre-existing tests that used the 1-arg ctor. ✅
- Service method names (`GetByIdAsync`, `GetEnrolledSwimmerIdsAsync`, `SetEnrollmentsAsync`) and repo methods (`ListSwimmerIdsAsync`, `ReplaceAsync`, `GetByIdAsync`) are used identically across Tasks 2–4. ✅
- Frontend repo methods (`getChampionship`, `getEnrollments`, `setEnrollments`) match across Tasks 6–8. ✅
- `SetEnrollmentsRq { swimmerIds }` (FE) ↔ `SetEnrollmentsRequest(SwimmerIds)` (BE) bind via default camelCase JSON. ✅
- VM API (`isEnrolled`, `toggle`, `enrolledCount`, `total`, `dirty`, `filtered`, `save`, `name`, `location`, `dateRange`, `swimmerName`, `club`, `initials`, `activeTab`, `setTab`) used consistently in the page (Task 9) and tests (Task 8). ✅
- `DetailTab` type shared by VM + page. ✅
