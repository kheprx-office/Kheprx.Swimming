# Dashboard Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the `/home` launcher with a read-only data dashboard showing swimmer count + monthly growth, a current-month attendance ring, a last-7-days attendance chart, and a stroke (squad) split.

**Architecture:** One composite backend endpoint `GET /api/dashboard/summary` composed at the API layer across the Identity and Attendance modules (the pattern `AttendanceRecordsController` already uses). A new Angular `features/dashboard/` feature (clean-architecture layering) fetches it once and renders four widgets in the existing RTL/Arabic design system. No new database objects; no charting library.

**Tech Stack:** .NET 10 (C#, EF Core, xUnit + Moq + EF InMemory), Angular 20 (standalone components, signals, Jest), Tailwind, `@lucide/angular`.

**Spec:** `docs/superpowers/specs/2026-09-24-dashboard-page-design.md`

## Global Constraints

Every task's requirements implicitly include this section. Values are copied verbatim from the spec.

- **Endpoint:** `GET /api/dashboard/summary`, `[Authorize]` (any authenticated role — it replaces `/home`).
- **Attendance rate formula (reused, do not reinvent):** `attended = present + late`; `denom = present + late + absent` (**excused excluded** from both); `rate = round(attended * 100 / denom)`; **`null` when `denom == 0`**.
- **"This month"** = the current calendar month, computed in **UTC** (matches `SwimmerProfile.CreatedAt = DateTime.UtcNow`).
- **Weekly chart** = the **last 7 recorded session days** (most recent distinct dates that have any record), ordered **oldest→newest**. Per day: `Present = present+late`, `Absent = absent`.
- **Stroke split:** proportion bars are relative to the **largest stroke count**, never as a share of a total. The header shows the **real** `SwimmerCount`. A swimmer with N strokes counts in N rows; show the footnote "Swimmers may specialize in more than one stroke."
- **No new charting library.** Ring = hand-rolled SVG; bars = CSS/SVG.
- **No DB migration or seed.** Pure read-side aggregation.
- **i18n:** Arabic is the default. Add all new copy to **both** `core/i18n/en.json` and `core/i18n/ar.json`; render every string through `TranslatePipe`. Do not rely on translation-parameter interpolation — concatenate dynamic numbers in the template.
- **Backend reads** use `AsNoTracking()`. Cross-module references are loose `Guid`s (no cross-module FKs). Responses use the `ApiResponse<T>` envelope. The composite DTO lives in `Kheprx.BaseBackend.Api/Models/`.
- **Frontend layering:** model → dto (+`isXValid` guard) → mapper → repository interface (+`InjectionToken`) → repository impl (returns the `BaseResponseRs` envelope) → use-case (validates with the guard, maps to domain, extends `UseCase`) → ViewModel (signals) → page. Wire providers via a `*_PROVIDERS` array in `app.config.ts`.
- **Commits are deferred.** The user has a standing "don't commit anything until I tell you" instruction. Each task ends with a **Commit** step showing the exact command, but **do not run any `git commit`/`git push` until the user explicitly authorizes.** Treat each Commit step as a checkpoint: stage nothing, pause, and report.

## Review Focus

Input classes the spec implies but that are easy to leave untested — each is pinned to a test in the owning task below:

- **No attendance records this month** → ring rate is `null` and renders "—", not `0%` or a crash. (Task 4 controller test produces `null`; Task 5 mapper test preserves `null`; Task 7 template renders "—".)
- **Fewer than 7 recorded session days** → the chart shows exactly the days that exist, not 7 padded/empty bars. (Task 1 service test; Task 4 controller test.)
- **A swimmer specializing in multiple strokes** → counts in every one of their stroke rows (bars may sum above the roster). (Task 3 repository test.)
- **Zero swimmers / empty stroke split** → count `0`, empty stroke list, `null` rate, and no divide-by-zero in the bar/width math. (Task 4 controller test; Task 6 viewmodel test.)
- **Month/timezone boundary for "new this month"** → the cutoff is the first day of the current month at `00:00 UTC`, inclusive. (Task 2 repository + service tests.)

---

## Task 1: Attendance — recent-daily-status-counts query + service method

Provides the raw material for the weekly chart: per-date status counts for the last 7 recorded session days.

**Files:**
- Modify: `backend/src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Domain/Repositories/IAttendanceRecordRepository.cs`
- Modify: `backend/src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Infrastructure/Repositories/AttendanceRecordRepository.cs`
- Modify: `backend/src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Application/DTOs/AttendanceRecordDtos.cs`
- Modify: `backend/src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Application/Services/Interfaces/IAttendanceService.cs`
- Modify: `backend/src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Application/Services/AttendanceService.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Attendance.UnitTests/Repositories/AttendanceRecordRepositoryTests.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Attendance.UnitTests/Services/AttendanceServiceTests.cs`

**Interfaces:**
- Produces:
  - `IAttendanceRecordRepository.ListRecentDaysAsync(int dayCount, CancellationToken ct = default) : Task<IReadOnlyList<AttendanceRecord>>` — all records belonging to the `dayCount` most recent distinct `SessionDate`s.
  - `DailyStatusCountsDto(DateOnly Date, IReadOnlyDictionary<Guid, int> Counts)` in the Attendance Application DTOs.
  - `IAttendanceService.GetRecentDailyStatusCountsAsync(int days, CancellationToken ct = default) : Task<IReadOnlyList<DailyStatusCountsDto>>` — one entry per recorded day, ordered oldest→newest, each mapping `StatusId → count`.

- [ ] **Step 1: Write the failing repository test**

Add to `AttendanceRecordRepositoryTests.cs`:

```csharp
[Fact]
public async Task ListRecentDaysAsync_returns_records_for_the_most_recent_distinct_days_only()
{
    await using var db = NewDb();
    var st = Guid.NewGuid();
    // Four distinct dates; ask for the 3 most recent.
    db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 10), st, Guid.NewGuid()));
    db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 20), st, Guid.NewGuid()));
    db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 20), st, Guid.NewGuid())); // same day, 2 rows
    db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 25), st, Guid.NewGuid()));
    db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 28), st, Guid.NewGuid()));
    await db.SaveChangesAsync();

    var repo = new AttendanceRecordRepository(db);
    var rows = await repo.ListRecentDaysAsync(3);

    var days = rows.Select(r => r.SessionDate).Distinct().OrderBy(d => d).ToList();
    Assert.Equal(new[] { new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 25), new DateOnly(2026, 9, 28) }, days);
    Assert.Equal(4, rows.Count); // Sep 20 contributes 2 rows
}

[Fact]
public async Task ListRecentDaysAsync_returns_empty_when_no_records()
{
    await using var db = NewDb();
    var rows = await new AttendanceRecordRepository(db).ListRecentDaysAsync(7);
    Assert.Empty(rows);
}
```

- [ ] **Step 2: Run the repository test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Attendance.UnitTests --filter ListRecentDaysAsync`
Expected: FAIL — `ListRecentDaysAsync` does not exist (compile error).

- [ ] **Step 3: Add the interface method**

In `IAttendanceRecordRepository.cs`, add inside the interface:

```csharp
Task<IReadOnlyList<AttendanceRecord>> ListRecentDaysAsync(int dayCount, CancellationToken ct = default);
```

- [ ] **Step 4: Implement the repository method**

In `AttendanceRecordRepository.cs`, add:

```csharp
public async Task<IReadOnlyList<AttendanceRecord>> ListRecentDaysAsync(int dayCount, CancellationToken ct = default)
{
    var dates = await _db.AttendanceRecords.AsNoTracking()
        .Select(r => r.SessionDate).Distinct()
        .OrderByDescending(d => d).Take(dayCount)
        .ToListAsync(ct);
    if (dates.Count == 0) return Array.Empty<AttendanceRecord>();
    return await _db.AttendanceRecords.AsNoTracking()
        .Where(r => dates.Contains(r.SessionDate))
        .ToListAsync(ct);
}
```

- [ ] **Step 5: Run the repository test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Attendance.UnitTests --filter ListRecentDaysAsync`
Expected: PASS.

- [ ] **Step 6: Write the failing service test**

Add the DTO reference and this test to `AttendanceServiceTests.cs`:

```csharp
[Fact]
public async Task GetRecentDailyStatusCountsAsync_groups_by_date_oldest_first_with_status_counts()
{
    var repo = new Mock<IAttendanceRecordRepository>();
    repo.Setup(r => r.ListRecentDaysAsync(7, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new[]
        {
            new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 28), Present, Guid.NewGuid()),
            new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 28), Absent,  Guid.NewGuid()),
            new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 20), Present, Guid.NewGuid()),
        });

    var svc = new AttendanceService(repo.Object);
    var days = await svc.GetRecentDailyStatusCountsAsync(7);

    Assert.Equal(2, days.Count);
    Assert.Equal(new DateOnly(2026, 9, 20), days[0].Date); // oldest first
    Assert.Equal(new DateOnly(2026, 9, 28), days[1].Date);
    Assert.Equal(1, days[1].Counts[Present]);
    Assert.Equal(1, days[1].Counts[Absent]);
}
```

- [ ] **Step 7: Run the service test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Attendance.UnitTests --filter GetRecentDailyStatusCountsAsync`
Expected: FAIL — method/DTO not defined (compile error).

- [ ] **Step 8: Add the DTO**

In `AttendanceRecordDtos.cs`, add:

```csharp
/// <summary>Per-date status counts (StatusId -> count) for the dashboard weekly chart.</summary>
public sealed record DailyStatusCountsDto(DateOnly Date, IReadOnlyDictionary<Guid, int> Counts);
```

- [ ] **Step 9: Add the service interface method + implementation**

In `IAttendanceService.cs`, add:

```csharp
Task<IReadOnlyList<DailyStatusCountsDto>> GetRecentDailyStatusCountsAsync(int days, CancellationToken ct = default);
```

In `AttendanceService.cs`, add:

```csharp
public async Task<IReadOnlyList<DailyStatusCountsDto>> GetRecentDailyStatusCountsAsync(
    int days, CancellationToken ct = default)
{
    var rows = await _records.ListRecentDaysAsync(days, ct);
    return rows
        .GroupBy(r => r.SessionDate)
        .OrderBy(g => g.Key) // oldest -> newest
        .Select(g => new DailyStatusCountsDto(
            g.Key,
            (IReadOnlyDictionary<Guid, int>)g.GroupBy(r => r.StatusId)
                .ToDictionary(s => s.Key, s => s.Count())))
        .ToList();
}
```

- [ ] **Step 10: Run the whole Attendance test project to verify green**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Attendance.UnitTests`
Expected: PASS (all tests).

- [ ] **Step 11: Commit** *(checkpoint — do not run until the user authorizes)*

```bash
git add backend/src/Modules/Attendance backend/tests/Kheprx.BaseBackend.Attendance.UnitTests
git commit -m "feat(dashboard): attendance recent-daily-status-counts query + service"
```

---

## Task 2: Identity — new-this-month swimmer count

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/ISwimmerProfileRepository.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/SwimmerProfileRepository.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/ISwimmerService.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/SwimmerProfileRepositoryTests.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs` (create if absent — see Step 6)

**Interfaces:**
- Produces:
  - `ISwimmerProfileRepository.CountCreatedSinceAsync(DateTime sinceUtc, CancellationToken ct = default) : Task<int>` — swimmers with `CreatedAt >= sinceUtc`.
  - `ISwimmerService.GetNewThisMonthCountAsync(CancellationToken ct = default) : Task<int>` — count for the current calendar month (UTC).

- [ ] **Step 1: Write the failing repository test**

Add to `SwimmerProfileRepositoryTests.cs`:

```csharp
[Fact]
public async Task CountCreatedSinceAsync_counts_only_profiles_at_or_after_the_cutoff()
{
    await using var db = NewDb();
    var repo = new SwimmerProfileRepository(db);
    await repo.AddAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid()));
    await repo.AddAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0002", Guid.NewGuid()));
    await repo.SaveChangesAsync();

    // Both were created "now" (ctor stamps DateTime.UtcNow).
    Assert.Equal(2, await repo.CountCreatedSinceAsync(DateTime.UtcNow.AddDays(-1)));
    Assert.Equal(0, await repo.CountCreatedSinceAsync(DateTime.UtcNow.AddDays(1)));
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter CountCreatedSinceAsync`
Expected: FAIL — method not defined (compile error).

- [ ] **Step 3: Add the interface method**

In `ISwimmerProfileRepository.cs`, add:

```csharp
Task<int> CountCreatedSinceAsync(DateTime sinceUtc, CancellationToken ct = default);
```

- [ ] **Step 4: Implement it**

In `SwimmerProfileRepository.cs`, add (next to `CountAsync`):

```csharp
public Task<int> CountCreatedSinceAsync(DateTime sinceUtc, CancellationToken ct = default)
    => _db.SwimmerProfiles.AsNoTracking().Where(s => s.CreatedAt >= sinceUtc).CountAsync(ct);
```

- [ ] **Step 5: Run it to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter CountCreatedSinceAsync`
Expected: PASS.

- [ ] **Step 6: Write the failing service test**

If `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs` exists, add the test below to it. Otherwise create the file with this content (the `SwimmerService` constructor takes ten dependencies; only the swimmer repo is exercised here, the rest are loose mocks):

```csharp
using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Kheprx.BaseBackend.Identity.Application.Options;
using Kheprx.BaseBackend.Identity.Application.Services;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Services;

public class SwimmerServiceTests
{
    private static SwimmerService Build(Mock<ISwimmerProfileRepository> swimmers) => new(
        swimmers.Object,
        new Mock<IUserRepository>().Object, new Mock<IRoleRepository>().Object,
        new Mock<IClubRepository>().Object, new Mock<IStrokeRepository>().Object,
        new Mock<IGenderRepository>().Object, new Mock<IFitnessAssessmentRepository>().Object,
        new Mock<IBloodTypeRepository>().Object, new Mock<IPasswordHasher>().Object,
        Options.Create(new AccountCreationOptions()));

    [Fact]
    public async Task GetNewThisMonthCountAsync_uses_first_of_current_month_utc_as_cutoff()
    {
        var now = DateTime.UtcNow;
        var expectedCutoff = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var swimmers = new Mock<ISwimmerProfileRepository>();
        swimmers.Setup(r => r.CountCreatedSinceAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(4);

        var result = await Build(swimmers).GetNewThisMonthCountAsync();

        Assert.Equal(4, result);
        swimmers.Verify(r => r.CountCreatedSinceAsync(expectedCutoff, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

Note: verify the exact constructor parameter list and namespaces against `SwimmerService.cs` before running; adjust the loose-mock list if the constructor differs.

- [ ] **Step 7: Run it to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter GetNewThisMonthCountAsync`
Expected: FAIL — method not defined (compile error).

- [ ] **Step 8: Add the service interface method + implementation**

In `ISwimmerService.cs`, add:

```csharp
Task<int> GetNewThisMonthCountAsync(CancellationToken ct = default);
```

In `SwimmerService.cs`, add:

```csharp
public async Task<int> GetNewThisMonthCountAsync(CancellationToken ct = default)
{
    var now = DateTime.UtcNow;
    var firstOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    return await _swimmers.CountCreatedSinceAsync(firstOfMonth, ct);
}
```

- [ ] **Step 9: Run the Identity test project to verify green**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests`
Expected: PASS.

- [ ] **Step 10: Commit** *(checkpoint — do not run until the user authorizes)*

```bash
git add backend/src/Modules/Identity backend/tests/Kheprx.BaseBackend.Identity.UnitTests
git commit -m "feat(dashboard): identity new-this-month swimmer count"
```

---

## Task 3: Identity — stroke (squad) split query

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/ReadModels/StrokeCountRow.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/ISwimmerProfileRepository.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/SwimmerProfileRepository.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/SwimmerDtos.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/ISwimmerService.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/SwimmerProfileRepositoryTests.cs`

**Interfaces:**
- Produces:
  - `StrokeCountRow(Guid StrokeId, string Code, string NameEn, string? NameAr, int Count)` read model.
  - `ISwimmerProfileRepository.GetStrokeCountsAsync(CancellationToken ct = default) : Task<IReadOnlyList<StrokeCountRow>>` — one row per stroke in `reference.stroke`, `Count` = number of `SwimmerSpecialization` rows referencing it.
  - `StrokeSplitDto(Guid StrokeId, string Code, string NameEn, string? NameAr, int Count)` DTO.
  - `ISwimmerService.GetStrokeSplitAsync(CancellationToken ct = default) : Task<IReadOnlyList<StrokeSplitDto>>` — ordered by `Count` descending.

- [ ] **Step 1: Write the failing repository test**

Add to `SwimmerProfileRepositoryTests.cs`:

```csharp
[Fact]
public async Task GetStrokeCountsAsync_counts_swimmers_per_stroke_including_multi_stroke_overlap()
{
    await using var db = NewDb();
    var free = new Stroke("free", "Freestyle", "حرة");
    var back = new Stroke("back", "Backstroke", "ظهر");
    db.Strokes.AddRange(free, back);

    var swimmerA = Guid.NewGuid();
    var swimmerB = Guid.NewGuid();
    // A specializes in both strokes; B only in freestyle.
    db.SwimmerSpecializations.AddRange(
        new SwimmerSpecialization(swimmerA, free.Id),
        new SwimmerSpecialization(swimmerA, back.Id),
        new SwimmerSpecialization(swimmerB, free.Id));
    await db.SaveChangesAsync();

    var repo = new SwimmerProfileRepository(db);
    var rows = await repo.GetStrokeCountsAsync();

    Assert.Equal(2, rows.Single(r => r.Code == "free").Count); // A + B
    Assert.Equal(1, rows.Single(r => r.Code == "back").Count); // A only (overlap)
    Assert.Equal("حرة", rows.Single(r => r.Code == "free").NameAr);
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter GetStrokeCountsAsync`
Expected: FAIL — type/method not defined (compile error).

- [ ] **Step 3: Create the read model**

Create `StrokeCountRow.cs`:

```csharp
namespace Kheprx.BaseBackend.Identity.Domain.ReadModels;

/// <summary>Swimmers-per-stroke count for the dashboard stroke split.</summary>
public sealed record StrokeCountRow(Guid StrokeId, string Code, string NameEn, string? NameAr, int Count);
```

- [ ] **Step 4: Add the interface method**

In `ISwimmerProfileRepository.cs`, add:

```csharp
Task<IReadOnlyList<StrokeCountRow>> GetStrokeCountsAsync(CancellationToken ct = default);
```

- [ ] **Step 5: Implement the repository query**

In `SwimmerProfileRepository.cs`, add (the `ReadModels` namespace is already imported):

```csharp
public async Task<IReadOnlyList<StrokeCountRow>> GetStrokeCountsAsync(CancellationToken ct = default)
{
    return await (
        from st in _db.Strokes.AsNoTracking()
        join sp in _db.SwimmerSpecializations.AsNoTracking() on st.Id equals sp.StrokeId into specs
        select new StrokeCountRow(st.Id, st.Code, st.NameEn, st.NameAr, specs.Count()))
        .ToListAsync(ct);
}
```

- [ ] **Step 6: Run it to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter GetStrokeCountsAsync`
Expected: PASS.

- [ ] **Step 7: Write the failing service test**

Add to `SwimmerServiceTests.cs` (from Task 2):

```csharp
[Fact]
public async Task GetStrokeSplitAsync_orders_by_count_descending()
{
    var swimmers = new Mock<ISwimmerProfileRepository>();
    swimmers.Setup(r => r.GetStrokeCountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
    {
        new Kheprx.BaseBackend.Identity.Domain.ReadModels.StrokeCountRow(Guid.NewGuid(), "back", "Backstroke", "ظهر", 3),
        new Kheprx.BaseBackend.Identity.Domain.ReadModels.StrokeCountRow(Guid.NewGuid(), "free", "Freestyle", "حرة", 9),
    });

    var result = await Build(swimmers).GetStrokeSplitAsync();

    Assert.Equal("free", result[0].Code); // 9 before 3
    Assert.Equal("back", result[1].Code);
}
```

- [ ] **Step 8: Run it to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter GetStrokeSplitAsync`
Expected: FAIL — DTO/method not defined (compile error).

- [ ] **Step 9: Add the DTO, service interface method, and implementation**

In `SwimmerDtos.cs`, add:

```csharp
/// <summary>One stroke's swimmer count for the dashboard stroke split (GET /api/dashboard/summary).</summary>
public sealed record StrokeSplitDto(Guid StrokeId, string Code, string NameEn, string? NameAr, int Count);
```

In `ISwimmerService.cs`, add:

```csharp
Task<IReadOnlyList<StrokeSplitDto>> GetStrokeSplitAsync(CancellationToken ct = default);
```

In `SwimmerService.cs`, add:

```csharp
public async Task<IReadOnlyList<StrokeSplitDto>> GetStrokeSplitAsync(CancellationToken ct = default)
{
    var rows = await _swimmers.GetStrokeCountsAsync(ct);
    return rows
        .OrderByDescending(r => r.Count)
        .Select(r => new StrokeSplitDto(r.StrokeId, r.Code, r.NameEn, r.NameAr, r.Count))
        .ToList();
}
```

- [ ] **Step 10: Run the Identity test project to verify green**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests`
Expected: PASS.

- [ ] **Step 11: Commit** *(checkpoint — do not run until the user authorizes)*

```bash
git add backend/src/Modules/Identity backend/tests/Kheprx.BaseBackend.Identity.UnitTests
git commit -m "feat(dashboard): identity stroke split query"
```

---

## Task 4: API — DashboardController composing the summary

**Files:**
- Create: `backend/Kheprx.BaseBackend.Api/Models/DashboardDtos.cs`
- Create: `backend/Kheprx.BaseBackend.Api/Resources/DashboardMessages.cs`
- Create: `backend/Kheprx.BaseBackend.Api/Controllers/DashboardController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/DashboardControllerTests.cs`

**Interfaces:**
- Consumes: `ISwimmerService.GetCountAsync`, `ISwimmerService.GetNewThisMonthCountAsync` (Task 2), `ISwimmerService.GetStrokeSplitAsync` (Task 3), `IAttendanceService.GetMonthStatusCountsAsync` (existing), `IAttendanceService.GetRecentDailyStatusCountsAsync` (Task 1), `IReferenceService.GetAttendanceStatusesAsync` (existing, returns `IReadOnlyList<CodedLookupDto>` with `.Id`/`.Code`).
- Produces: `GET /api/dashboard/summary` → `ApiResponse<DashboardSummaryDto>`.

- [ ] **Step 1: Create the composite DTO**

Create `Models/DashboardDtos.cs`:

```csharp
namespace Kheprx.BaseBackend.Api.Models;

/// <summary>Everything the dashboard renders, in one payload (GET /api/dashboard/summary).</summary>
public sealed record DashboardSummaryDto(
    int SwimmerCount,
    int NewThisMonth,
    int? MonthAttendanceRatePct,
    IReadOnlyList<DailyAttendanceDto> Last7Days,
    IReadOnlyList<StrokeSplitItemDto> StrokeSplit);

public sealed record DailyAttendanceDto(DateOnly Date, int Present, int Absent);

public sealed record StrokeSplitItemDto(Guid StrokeId, string Code, string NameEn, string? NameAr, int Count);
```

- [ ] **Step 2: Create the message helper**

Create `Resources/DashboardMessages.cs` (mirrors the AttendanceMessages EN/AR pattern):

```csharp
namespace Kheprx.BaseBackend.Api.Resources;

public static class DashboardMessages
{
    public static string Loaded(string lang) => lang == "ar" ? "تم تحميل لوحة المعلومات." : "Dashboard loaded.";
}
```

- [ ] **Step 3: Write the failing controller test**

Create `DashboardControllerTests.cs`:

```csharp
using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Api.Models;
using Kheprx.BaseBackend.Attendance.Application.DTOs;
using Kheprx.BaseBackend.Attendance.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class DashboardControllerTests
{
    private static readonly Guid Present = Guid.NewGuid();
    private static readonly Guid Late = Guid.NewGuid();
    private static readonly Guid Absent = Guid.NewGuid();
    private static readonly Guid Excused = Guid.NewGuid();

    private static IReadOnlyList<CodedLookupDto> Statuses() => new[]
    {
        new CodedLookupDto(Present, "present", "Present", "حاضر"),
        new CodedLookupDto(Late, "late", "Late", "متأخر"),
        new CodedLookupDto(Absent, "absent", "Absent", "غائب"),
        new CodedLookupDto(Excused, "excused", "Excused", "معذور"),
    };

    private static DashboardController Build(
        Mock<ISwimmerService> swimmers, Mock<IAttendanceService> attendance, Mock<IReferenceService> reference)
        => new(swimmers.Object, attendance.Object, reference.Object);

    private static (Mock<ISwimmerService>, Mock<IAttendanceService>, Mock<IReferenceService>) Mocks()
    {
        var swimmers = new Mock<ISwimmerService>();
        swimmers.Setup(s => s.GetCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new SwimmerCountDto(452));
        swimmers.Setup(s => s.GetNewThisMonthCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(12);
        swimmers.Setup(s => s.GetStrokeSplitAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new StrokeSplitDto(Guid.NewGuid(), "free", "Freestyle", "حرة", 9) });
        var attendance = new Mock<IAttendanceService>();
        attendance.Setup(a => a.GetMonthStatusCountsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new Dictionary<Guid, IReadOnlyDictionary<Guid, int>>());
        attendance.Setup(a => a.GetRecentDailyStatusCountsAsync(7, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Array.Empty<DailyStatusCountsDto>());
        var reference = new Mock<IReferenceService>();
        reference.Setup(r => r.GetAttendanceStatusesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Statuses());
        return (swimmers, attendance, reference);
    }

    private static DashboardSummaryDto Data(ActionResult<ApiResponse<DashboardSummaryDto>> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<ApiResponse<DashboardSummaryDto>>(ok.Value).Data!;
    }

    [Fact]
    public async Task Summary_composes_counts_and_stroke_split()
    {
        var (sw, at, rf) = Mocks();
        var data = Data(await Build(sw, at, rf).Summary(CancellationToken.None));
        Assert.Equal(452, data.SwimmerCount);
        Assert.Equal(12, data.NewThisMonth);
        Assert.Single(data.StrokeSplit);
        Assert.Equal("free", data.StrokeSplit[0].Code);
    }

    [Fact]
    public async Task Summary_month_rate_uses_present_plus_late_over_present_late_absent_excused_excluded()
    {
        var (sw, at, rf) = Mocks();
        // Club month totals: present 6, late 2, absent 2, excused 5 -> attended 8 / denom 10 = 80.
        at.Setup(a => a.GetMonthStatusCountsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new Dictionary<Guid, IReadOnlyDictionary<Guid, int>>
          {
              [Guid.NewGuid()] = new Dictionary<Guid, int> { [Present] = 6, [Late] = 2, [Absent] = 2, [Excused] = 5 },
          });
        var data = Data(await Build(sw, at, rf).Summary(CancellationToken.None));
        Assert.Equal(80, data.MonthAttendanceRatePct);
    }

    [Fact]
    public async Task Summary_month_rate_is_null_when_no_records()
    {
        var (sw, at, rf) = Mocks(); // GetMonthStatusCountsAsync returns empty
        var data = Data(await Build(sw, at, rf).Summary(CancellationToken.None));
        Assert.Null(data.MonthAttendanceRatePct);
    }

    [Fact]
    public async Task Summary_maps_recent_days_present_is_present_plus_late_and_keeps_fewer_than_seven()
    {
        var (sw, at, rf) = Mocks();
        at.Setup(a => a.GetRecentDailyStatusCountsAsync(7, It.IsAny<CancellationToken>()))
          .ReturnsAsync(new[]
          {
              new DailyStatusCountsDto(new DateOnly(2026, 9, 27),
                  new Dictionary<Guid, int> { [Present] = 4, [Late] = 1, [Absent] = 2 }),
              new DailyStatusCountsDto(new DateOnly(2026, 9, 28),
                  new Dictionary<Guid, int> { [Present] = 5 }),
          });
        var data = Data(await Build(sw, at, rf).Summary(CancellationToken.None));
        Assert.Equal(2, data.Last7Days.Count); // fewer than 7 kept as-is
        Assert.Equal(5, data.Last7Days[0].Present); // 4 present + 1 late
        Assert.Equal(2, data.Last7Days[0].Absent);
        Assert.Equal(5, data.Last7Days[1].Present);
        Assert.Equal(0, data.Last7Days[1].Absent);
    }

    [Fact]
    public async Task Summary_handles_zero_swimmers()
    {
        var (sw, at, rf) = Mocks();
        sw.Setup(s => s.GetCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new SwimmerCountDto(0));
        sw.Setup(s => s.GetStrokeSplitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<StrokeSplitDto>());
        var data = Data(await Build(sw, at, rf).Summary(CancellationToken.None));
        Assert.Equal(0, data.SwimmerCount);
        Assert.Empty(data.StrokeSplit);
        Assert.Null(data.MonthAttendanceRatePct);
    }

    [Fact]
    public void Summary_requires_authorization()
    {
        var attr = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute?)Attribute
            .GetCustomAttribute(typeof(DashboardController), typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute));
        Assert.NotNull(attr);
    }
}
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter DashboardControllerTests`
Expected: FAIL — `DashboardController` not defined (compile error).

- [ ] **Step 5: Implement the controller**

Create `Controllers/DashboardController.cs`:

```csharp
using Kheprx.BaseBackend.Api.Models;
using Kheprx.BaseBackend.Api.Resources;
using Kheprx.BaseBackend.Attendance.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController : BaseApiController
{
    private readonly ISwimmerService _swimmers;
    private readonly IAttendanceService _attendance;
    private readonly IReferenceService _reference;

    public DashboardController(ISwimmerService swimmers, IAttendanceService attendance, IReferenceService reference)
    {
        _swimmers = swimmers;
        _attendance = attendance;
        _reference = reference;
    }

    /// <summary>Club-wide overview: swimmer count + growth, current-month attendance rate,
    /// last-7-recorded-days present/absent, and swimmers-per-stroke split.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<DashboardSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DashboardSummaryDto>>> Summary(CancellationToken ct)
    {
        var lang = AppLanguage.Current;
        var now = DateTime.UtcNow;

        var count = (await _swimmers.GetCountAsync(ct)).Count;
        var newThisMonth = await _swimmers.GetNewThisMonthCountAsync(ct);
        var strokeSplit = (await _swimmers.GetStrokeSplitAsync(ct))
            .Select(s => new StrokeSplitItemDto(s.StrokeId, s.Code, s.NameEn, s.NameAr, s.Count))
            .ToList();

        var statuses = await _reference.GetAttendanceStatusesAsync(ct);
        Guid IdOf(string code) => statuses.FirstOrDefault(s => s.Code == code)?.Id ?? Guid.Empty;
        var present = IdOf("present");
        var late = IdOf("late");
        var absent = IdOf("absent");
        int C(IReadOnlyDictionary<Guid, int> c, Guid id) => id != Guid.Empty && c.TryGetValue(id, out var n) ? n : 0;

        // Month rate — aggregate the per-swimmer month counts into club totals.
        var monthCounts = await _attendance.GetMonthStatusCountsAsync(now.Year, now.Month, ct);
        int mp = 0, ml = 0, ma = 0;
        foreach (var c in monthCounts.Values) { mp += C(c, present); ml += C(c, late); ma += C(c, absent); }
        var attended = mp + ml;
        var denom = attended + ma;
        int? rate = denom > 0 ? (int)Math.Round(attended * 100.0 / denom) : (int?)null;

        // Last 7 recorded days — Present = present+late, Absent = absent.
        var daily = await _attendance.GetRecentDailyStatusCountsAsync(7, ct);
        var last7 = daily
            .Select(d => new DailyAttendanceDto(d.Date, C(d.Counts, present) + C(d.Counts, late), C(d.Counts, absent)))
            .ToList();

        var dto = new DashboardSummaryDto(count, newThisMonth, rate, last7, strokeSplit);
        return Ok(ApiResponse<DashboardSummaryDto>.Success(DashboardMessages.Loaded(lang), dto));
    }
}
```

Note: confirm `BaseApiController`, `AppLanguage.Current`, and `ApiResponse<T>.Success(message, data)` signatures against `AttendanceRecordsController.cs`; they are used identically there.

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter DashboardControllerTests`
Expected: PASS (all six facts).

- [ ] **Step 7: Build the whole backend + run the full backend test suite**

Run: `dotnet test backend`
Expected: PASS (no regressions across all projects, including ArchitectureTests).

- [ ] **Step 8: Manually verify the endpoint shape (optional but recommended)**

Start the API and `GET /api/dashboard/summary` with a valid bearer token; confirm the JSON has `swimmerCount`, `newThisMonth`, `monthAttendanceRatePct`, `last7Days`, `strokeSplit`. Stop the API afterward (the running API locks DLLs for later `dotnet` builds).

- [ ] **Step 9: Commit** *(checkpoint — do not run until the user authorizes)*

```bash
git add backend/Kheprx.BaseBackend.Api backend/tests/Kheprx.BaseBackend.Api.UnitTests
git commit -m "feat(dashboard): GET /api/dashboard/summary composite endpoint"
```

---

## Task 5: Frontend — dashboard data + domain layer

**Files:**
- Create: `frontend/src/app/features/dashboard/domain/model/dashboard-summary.ts`
- Create: `frontend/src/app/features/dashboard/data/dto/dashboard-summary.dto.ts`
- Create: `frontend/src/app/features/dashboard/data/dto/dashboard-summary.mapper.ts`
- Create: `frontend/src/app/features/dashboard/domain/repositories/dashboard.repository.ts`
- Create: `frontend/src/app/features/dashboard/data/repositories/dashboard.repository.impl.ts`
- Create: `frontend/src/app/features/dashboard/data/dashboard.providers.ts`
- Test: `frontend/src/app/features/dashboard/testing/data/repositories/dashboard.repository.impl.spec.ts`
- Test: `frontend/src/app/features/dashboard/testing/data/dto/dashboard-summary.mapper.spec.ts`

**Interfaces:**
- Produces:
  - Domain model `DashboardSummary { swimmerCount, newThisMonth, monthAttendanceRatePct, last7Days, strokeSplit }` with `DailyAttendance { date, present, absent }` and `StrokeSplitItem { strokeId, code, nameEn, nameAr, count }`.
  - `IDashboardRepository.getSummary() : Promise<DashboardSummaryItemDtoRs>` + `DASHBOARD_REPOSITORY` token.
  - `toDashboardSummary(dto) : DashboardSummary`, `isDashboardSummaryDtoRsValid(dto)` guard.
  - `DASHBOARD_PROVIDERS` provider array.

- [ ] **Step 1: Create the domain model**

`domain/model/dashboard-summary.ts`:

```ts
export interface DailyAttendance {
  date: string; // 'YYYY-MM-DD'
  present: number;
  absent: number;
}

export interface StrokeSplitItem {
  strokeId: string;
  code: string;
  nameEn: string;
  nameAr: string | null;
  count: number;
}

export interface DashboardSummary {
  swimmerCount: number;
  newThisMonth: number;
  monthAttendanceRatePct: number | null;
  last7Days: DailyAttendance[];
  strokeSplit: StrokeSplitItem[];
}
```

- [ ] **Step 2: Create the DTO + validator**

`data/dto/dashboard-summary.dto.ts`:

```ts
// dashboard-summary.dto.ts — dashboard summary response DTO (API_FLOW convention).
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface DailyAttendanceDtoRs {
  date: string;
  present: number;
  absent: number;
}

export interface StrokeSplitItemDtoRs {
  strokeId: string;
  code: string;
  nameEn: string;
  nameAr?: string | null;
  count: number;
}

export interface DashboardSummaryDtoRs {
  swimmerCount: number;
  newThisMonth: number;
  monthAttendanceRatePct: number | null;
  last7Days: DailyAttendanceDtoRs[];
  strokeSplit: StrokeSplitItemDtoRs[];
}

export interface DashboardSummaryItemDtoRs extends BaseResponseRs<DashboardSummaryDtoRs> {}

export function isDashboardSummaryDtoRsValid(dto: unknown): dto is DashboardSummaryDtoRs {
  const d = dto as DashboardSummaryDtoRs;
  return (
    !!d &&
    typeof d.swimmerCount === 'number' &&
    typeof d.newThisMonth === 'number' &&
    (d.monthAttendanceRatePct === null || typeof d.monthAttendanceRatePct === 'number') &&
    Array.isArray(d.last7Days) &&
    Array.isArray(d.strokeSplit)
  );
}
```

- [ ] **Step 3: Create the mapper**

`data/dto/dashboard-summary.mapper.ts`:

```ts
import { DashboardSummaryDtoRs } from '@features/dashboard/data/dto/dashboard-summary.dto';
import { DashboardSummary } from '@features/dashboard/domain/model/dashboard-summary';

export function toDashboardSummary(d: DashboardSummaryDtoRs): DashboardSummary {
  return {
    swimmerCount: d.swimmerCount,
    newThisMonth: d.newThisMonth,
    monthAttendanceRatePct: d.monthAttendanceRatePct,
    last7Days: d.last7Days.map((x) => ({ date: x.date, present: x.present, absent: x.absent })),
    strokeSplit: d.strokeSplit.map((x) => ({
      strokeId: x.strokeId,
      code: x.code,
      nameEn: x.nameEn,
      nameAr: x.nameAr ?? null,
      count: x.count,
    })),
  };
}
```

- [ ] **Step 4: Create the repository interface + token**

`domain/repositories/dashboard.repository.ts`:

```ts
import { InjectionToken } from '@angular/core';
import { DashboardSummaryItemDtoRs } from '@features/dashboard/data/dto/dashboard-summary.dto';

export interface IDashboardRepository {
  getSummary(): Promise<DashboardSummaryItemDtoRs>;
}

export const DASHBOARD_REPOSITORY = new InjectionToken<IDashboardRepository>('DASHBOARD_REPOSITORY');
```

- [ ] **Step 5: Write the failing repository-impl test**

`testing/data/repositories/dashboard.repository.impl.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { DashboardRepositoryImpl } from '@features/dashboard/data/repositories/dashboard.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('DashboardRepositoryImpl', () => {
  const http = { get: jest.fn() } as unknown as HttpClientService;
  let repo: DashboardRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [DashboardRepositoryImpl, { provide: HttpClientService, useValue: http }],
    });
    repo = TestBed.inject(DashboardRepositoryImpl);
  });

  it('getSummary GETs the dashboard summary endpoint', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: null });
    await repo.getSummary();
    expect(http.get).toHaveBeenCalledWith('/api/dashboard/summary');
  });
});
```

- [ ] **Step 6: Run it to verify it fails**

Run: `cd frontend && npx jest dashboard.repository.impl`
Expected: FAIL — `DashboardRepositoryImpl` not found (cannot resolve import).

- [ ] **Step 7: Implement the repository**

`data/repositories/dashboard.repository.impl.ts`:

```ts
// dashboard.repository.impl.ts — dashboard repository. Returns the API DTO envelope.
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { IDashboardRepository } from '@features/dashboard/domain/repositories/dashboard.repository';
import { DashboardSummaryItemDtoRs } from '@features/dashboard/data/dto/dashboard-summary.dto';

@Injectable({ providedIn: 'root' })
export class DashboardRepositoryImpl implements IDashboardRepository {
  private readonly http = inject(HttpClientService);

  getSummary(): Promise<DashboardSummaryItemDtoRs> {
    return this.http.get<DashboardSummaryItemDtoRs>('/api/dashboard/summary');
  }
}
```

- [ ] **Step 8: Create the providers array**

`data/dashboard.providers.ts`:

```ts
import { Provider } from '@angular/core';
import { DASHBOARD_REPOSITORY } from '@features/dashboard/domain/repositories/dashboard.repository';
import { DashboardRepositoryImpl } from '@features/dashboard/data/repositories/dashboard.repository.impl';

export const DASHBOARD_PROVIDERS: Provider[] = [
  { provide: DASHBOARD_REPOSITORY, useClass: DashboardRepositoryImpl },
];
```

- [ ] **Step 9: Write the mapper test**

`testing/data/dto/dashboard-summary.mapper.spec.ts`:

```ts
import { toDashboardSummary } from '@features/dashboard/data/dto/dashboard-summary.mapper';
import { DashboardSummaryDtoRs } from '@features/dashboard/data/dto/dashboard-summary.dto';

describe('toDashboardSummary', () => {
  it('maps all fields and defaults missing nameAr to null', () => {
    const dto: DashboardSummaryDtoRs = {
      swimmerCount: 452,
      newThisMonth: 12,
      monthAttendanceRatePct: 88,
      last7Days: [{ date: '2026-09-28', present: 5, absent: 1 }],
      strokeSplit: [{ strokeId: 's1', code: 'free', nameEn: 'Freestyle', count: 9 }],
    };
    const model = toDashboardSummary(dto);
    expect(model.swimmerCount).toBe(452);
    expect(model.last7Days[0].present).toBe(5);
    expect(model.strokeSplit[0].nameAr).toBeNull();
  });

  it('preserves a null attendance rate', () => {
    const dto: DashboardSummaryDtoRs = {
      swimmerCount: 0, newThisMonth: 0, monthAttendanceRatePct: null, last7Days: [], strokeSplit: [],
    };
    expect(toDashboardSummary(dto).monthAttendanceRatePct).toBeNull();
  });
});
```

- [ ] **Step 10: Run the dashboard specs to verify they pass**

Run: `cd frontend && npx jest dashboard`
Expected: PASS (repository-impl spec + mapper spec).

- [ ] **Step 11: Commit** *(checkpoint — do not run until the user authorizes)*

```bash
git add frontend/src/app/features/dashboard
git commit -m "feat(dashboard): frontend data + domain layer"
```

---

## Task 6: Frontend — load use-case + ViewModel

**Files:**
- Create: `frontend/src/app/features/dashboard/domain/usecases/load-dashboard-summary.use-case.ts`
- Create: `frontend/src/app/features/dashboard/presentation/pages/dashboard/dashboard.viewmodel.ts`
- Test: `frontend/src/app/features/dashboard/testing/presentation/pages/dashboard/dashboard.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `DASHBOARD_REPOSITORY`, `toDashboardSummary`, `isDashboardSummaryDtoRsValid`, `AuthSessionStore.currentUserName` (existing signal).
- Produces:
  - `LoadDashboardSummaryUseCase extends UseCase<void, DashboardSummary>` with `run()`.
  - `DashboardViewModel` exposing `loading()`, `error()`, `summary()` signals, `currentUserName`, `maxStrokeCount()`, `strokeBarPct(count)`, `dayRatePct(day)`, and `load()`.

- [ ] **Step 1: Create the use-case**

`domain/usecases/load-dashboard-summary.use-case.ts`:

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { DASHBOARD_REPOSITORY } from '@features/dashboard/domain/repositories/dashboard.repository';
import { isDashboardSummaryDtoRsValid } from '@features/dashboard/data/dto/dashboard-summary.dto';
import { toDashboardSummary } from '@features/dashboard/data/dto/dashboard-summary.mapper';
import { DashboardSummary } from '@features/dashboard/domain/model/dashboard-summary';

@Injectable({ providedIn: 'root' })
export class LoadDashboardSummaryUseCase extends UseCase<void, DashboardSummary> {
  private readonly repo = inject(DASHBOARD_REPOSITORY);
  constructor() { super('LoadDashboardSummary'); }
  protected async execute(): Promise<DashboardSummary> {
    const res = await this.repo.getSummary();
    if (!isDashboardSummaryDtoRsValid(res.data)) {
      throw new AppError('Invalid dashboard summary received', 'validation');
    }
    return toDashboardSummary(res.data);
  }
}
```

- [ ] **Step 2: Write the failing ViewModel test**

`testing/presentation/pages/dashboard/dashboard.viewmodel.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { DashboardViewModel } from '@features/dashboard/presentation/pages/dashboard/dashboard.viewmodel';
import { LoadDashboardSummaryUseCase } from '@features/dashboard/domain/usecases/load-dashboard-summary.use-case';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { DashboardSummary } from '@features/dashboard/domain/model/dashboard-summary';

const SUMMARY: DashboardSummary = {
  swimmerCount: 452,
  newThisMonth: 12,
  monthAttendanceRatePct: 88,
  last7Days: [
    { date: '2026-09-27', present: 8, absent: 2 },
    { date: '2026-09-28', present: 0, absent: 0 },
  ],
  strokeSplit: [
    { strokeId: 's1', code: 'free', nameEn: 'Freestyle', nameAr: 'حرة', count: 9 },
    { strokeId: 's2', code: 'back', nameEn: 'Backstroke', nameAr: 'ظهر', count: 3 },
  ],
};

function build(overrides: { load?: unknown } = {}) {
  const loadUc = { run: jest.fn().mockResolvedValue(overrides.load ?? { ok: true, data: SUMMARY }) };
  const auth = { currentUserName: signal('John Coach') };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      DashboardViewModel,
      { provide: LoadDashboardSummaryUseCase, useValue: loadUc },
      { provide: AuthSessionStore, useValue: auth },
    ],
  });
  return { vm: TestBed.inject(DashboardViewModel), loadUc };
}

describe('DashboardViewModel', () => {
  it('load() populates summary on success and clears loading', async () => {
    const { vm } = build();
    await vm.load();
    expect(vm.summary()).toEqual(SUMMARY);
    expect(vm.loading()).toBe(false);
    expect(vm.error()).toBe(false);
  });

  it('load() sets error and null summary on failure', async () => {
    const { vm } = build({ load: { ok: false } });
    await vm.load();
    expect(vm.summary()).toBeNull();
    expect(vm.error()).toBe(true);
  });

  it('strokeBarPct is relative to the largest stroke', async () => {
    const { vm } = build();
    await vm.load();
    expect(vm.maxStrokeCount()).toBe(9);
    expect(vm.strokeBarPct(9)).toBe(100);
    expect(vm.strokeBarPct(3)).toBe(33);
  });

  it('dayRatePct returns 0 for a day with no records (no divide-by-zero)', async () => {
    const { vm } = build();
    await vm.load();
    expect(vm.dayRatePct({ date: '2026-09-28', present: 0, absent: 0 })).toBe(0);
    expect(vm.dayRatePct({ date: '2026-09-27', present: 8, absent: 2 })).toBe(80);
  });

  it('maxStrokeCount is 0 when the stroke split is empty', async () => {
    const empty = { ...SUMMARY, strokeSplit: [] };
    const { vm } = build({ load: { ok: true, data: empty } });
    await vm.load();
    expect(vm.maxStrokeCount()).toBe(0);
    expect(vm.strokeBarPct(5)).toBe(0);
  });
});
```

- [ ] **Step 3: Run it to verify it fails**

Run: `cd frontend && npx jest dashboard.viewmodel`
Expected: FAIL — `DashboardViewModel` not found.

- [ ] **Step 4: Implement the ViewModel**

`presentation/pages/dashboard/dashboard.viewmodel.ts`:

```ts
import { Injectable, computed, inject, signal } from '@angular/core';
import { LoadDashboardSummaryUseCase } from '@features/dashboard/domain/usecases/load-dashboard-summary.use-case';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { DashboardSummary, DailyAttendance } from '@features/dashboard/domain/model/dashboard-summary';

@Injectable()
export class DashboardViewModel {
  private readonly loadSummary = inject(LoadDashboardSummaryUseCase);
  private readonly auth = inject(AuthSessionStore);

  readonly loading = signal(false);
  readonly error = signal(false);
  readonly summary = signal<DashboardSummary | null>(null);

  readonly currentUserName = this.auth.currentUserName;

  // Largest stroke count — bar widths are relative to this, never to a total (multi-stroke overlap).
  readonly maxStrokeCount = computed(() => {
    const s = this.summary();
    if (!s || s.strokeSplit.length === 0) return 0;
    return Math.max(...s.strokeSplit.map((x) => x.count));
  });

  strokeBarPct(count: number): number {
    const max = this.maxStrokeCount();
    return max > 0 ? Math.round((count / max) * 100) : 0;
  }

  dayRatePct(d: DailyAttendance): number {
    const total = d.present + d.absent;
    return total > 0 ? Math.round((d.present / total) * 100) : 0;
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(false);
    const res = await this.loadSummary.run();
    if (res.ok) {
      this.summary.set(res.data);
    } else {
      this.error.set(true);
      this.summary.set(null);
    }
    this.loading.set(false);
  }
}
```

- [ ] **Step 5: Run it to verify it passes**

Run: `cd frontend && npx jest dashboard.viewmodel`
Expected: PASS (all five specs).

- [ ] **Step 6: Commit** *(checkpoint — do not run until the user authorizes)*

```bash
git add frontend/src/app/features/dashboard
git commit -m "feat(dashboard): frontend load use-case + view model"
```

---

## Task 7: Frontend — dashboard page component, template, and i18n

**Files:**
- Create: `frontend/src/app/features/dashboard/presentation/pages/dashboard/dashboard.page.ts`
- Create: `frontend/src/app/features/dashboard/presentation/pages/dashboard/dashboard.page.html`
- Create: `frontend/src/app/features/dashboard/index.ts`
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`
- Test: `frontend/src/app/features/dashboard/testing/presentation/pages/dashboard/dashboard.page.spec.ts`

**Interfaces:**
- Consumes: `DashboardViewModel` (Task 6), `TranslatePipe`, `LanguageStore`, `DecorBackgroundComponent` (`@core/ui/components/decor-background.component`), lucide icons.
- Produces: `DashboardPage` (default-exported via the barrel) and the `@features/dashboard` barrel exporting `DashboardPage` + `DashboardViewModel`.

- [ ] **Step 1: Add i18n keys to `en.json`**

Add a `"dashboard"` block (place it near the other top-level feature blocks, e.g. after `"attendanceEntry"`):

```json
"dashboard": {
  "greeting": "Welcome back",
  "subtitle": "Here is how your club is doing.",
  "swimmers": "Swimmers",
  "thisMonth": "this month",
  "attendanceThisMonth": "attendance this month",
  "noSessions": "No sessions recorded yet",
  "weeklyAttendance": "Weekly attendance",
  "present": "Present",
  "absent": "Absent",
  "noRecentSessions": "No recent sessions to show.",
  "strokeSplit": "Swimmers by stroke",
  "registeredSwimmers": "registered swimmers",
  "multiStrokeNote": "Swimmers may specialize in more than one stroke.",
  "noStrokes": "No stroke data yet.",
  "loadError": "Couldn’t load the dashboard. Please try again."
}
```

- [ ] **Step 2: Add the mirrored keys to `ar.json`**

```json
"dashboard": {
  "greeting": "مرحباً بعودتك",
  "subtitle": "إليك أداء ناديك.",
  "swimmers": "السباحون",
  "thisMonth": "هذا الشهر",
  "attendanceThisMonth": "نسبة الحضور هذا الشهر",
  "noSessions": "لا توجد جلسات مسجلة بعد",
  "weeklyAttendance": "الحضور الأسبوعي",
  "present": "حاضر",
  "absent": "غائب",
  "noRecentSessions": "لا توجد جلسات حديثة لعرضها.",
  "strokeSplit": "توزيع السباحين حسب السباحة",
  "registeredSwimmers": "سباح مسجل",
  "multiStrokeNote": "قد يتخصص السباح في أكثر من سباحة.",
  "noStrokes": "لا توجد بيانات سباحات بعد.",
  "loadError": "تعذّر تحميل لوحة المعلومات. حاول مرة أخرى."
}
```

- [ ] **Step 3: Write the failing page test**

`testing/presentation/pages/dashboard/dashboard.page.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { DashboardPage } from '@features/dashboard/presentation/pages/dashboard/dashboard.page';
import { DashboardViewModel } from '@features/dashboard/presentation/pages/dashboard/dashboard.viewmodel';
import { LanguageStore } from '@core/i18n/language.store';

describe('DashboardPage', () => {
  it('loads the summary on init', () => {
    const vm = {
      load: jest.fn().mockResolvedValue(undefined),
      loading: signal(false),
      error: signal(false),
      summary: signal(null),
      currentUserName: signal('John'),
      maxStrokeCount: signal(0),
      strokeBarPct: () => 0,
      dayRatePct: () => 0,
    };
    TestBed.configureTestingModule({
      imports: [DashboardPage],
      providers: [
        { provide: DashboardViewModel, useValue: vm },
        { provide: LanguageStore, useValue: { lang: signal('en') } },
      ],
    });
    const fixture = TestBed.createComponent(DashboardPage);
    fixture.detectChanges();
    expect(vm.load).toHaveBeenCalled();
  });
});
```

Note: the component provides `DashboardViewModel` at the route level (Task 8). Because this test overrides the provider via `TestBed`, override the component's own providers if Angular complains — use `TestBed.overrideComponent(DashboardPage, { set: { providers: [] } })` before `createComponent`.

- [ ] **Step 4: Run it to verify it fails**

Run: `cd frontend && npx jest dashboard.page`
Expected: FAIL — `DashboardPage` not found.

- [ ] **Step 5: Implement the page component**

`presentation/pages/dashboard/dashboard.page.ts`:

Match the codebase's lucide convention exactly (per `layout.component.ts` and `home.page.ts`): import `LucideDynamicIcon` plus the specific icon consts, add `LucideDynamicIcon` to `imports`, expose the icon consts as fields, and bind with `[lucideIcon]` in the template.

```ts
import { Component, OnInit, inject } from '@angular/core';
import { LucideDynamicIcon, LucideTrendingUp, LucideWaves, LucideUsers } from '@lucide/angular';
import { TranslatePipe } from '@core/i18n';
import { LanguageStore } from '@core/i18n/language.store';
import { DecorBackgroundComponent } from '@core/ui/components/decor-background.component';
import { DashboardViewModel } from './dashboard.viewmodel';
import { StrokeSplitItem } from '@features/dashboard/domain/model/dashboard-summary';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [TranslatePipe, LucideDynamicIcon, DecorBackgroundComponent],
  templateUrl: './dashboard.page.html',
})
export class DashboardPage implements OnInit {
  protected readonly vm = inject(DashboardViewModel);
  private readonly language = inject(LanguageStore);

  protected readonly TrendingUpIcon = LucideTrendingUp;
  protected readonly WavesIcon = LucideWaves;
  protected readonly UsersIcon = LucideUsers;

  // Ring geometry: r=52 → circumference ≈ 327 (matches the design reference).
  protected readonly ringCircumference = 327;

  ngOnInit(): void {
    void this.vm.load();
  }

  ringDashArray(pct: number | null): string {
    const filled = ((pct ?? 0) / 100) * this.ringCircumference;
    return `${filled} ${this.ringCircumference}`;
  }

  strokeName(s: StrokeSplitItem): string {
    return this.language.lang() === 'ar' ? (s.nameAr ?? s.nameEn) : s.nameEn;
  }
}
```

In the template, render each icon as `<svg [lucideIcon]="TrendingUpIcon" [size]="14"></svg>` (mirroring `home.page.html`). Verify `LucideTrendingUp` is exported by the installed `@lucide/angular` version; if not, substitute the nearest available trending/arrow-up icon const.

- [ ] **Step 6: Implement the template**

`presentation/pages/dashboard/dashboard.page.html` — RTL, existing design tokens (`bg-surface`, `border-border`, `text-ink`, `text-text-secondary`, `gradient-text`, `rounded-2xl`, `shadow-sm`, `animate-fade-in`), all copy via `TranslatePipe`, and:

- A root `<div dir="rtl" class="relative min-h-full w-full">` with `<app-decor-background />` and a `max-w-6xl` container (mirror `home.page.html`).
- `@if (vm.error())` → a card showing `'dashboard.loadError' | translate`.
- **Hero band** (`@if (vm.summary(); as s)`): greeting `('dashboard.greeting' | translate)` + `{{ vm.currentUserName() }}` via `gradient-text`; a stat block with `{{ s.swimmerCount }}`, label `'dashboard.swimmers' | translate`, and a growth line `+{{ s.newThisMonth }} {{ 'dashboard.thisMonth' | translate }}` with the `TrendingUpIcon`; and the **attendance ring** — an inline `<svg viewBox="0 0 120 120" class="-rotate-90">` with a track `<circle r="52" .../>` and a value `<circle r="52" stroke-linecap="round" [attr.stroke-dasharray]="ringDashArray(s.monthAttendanceRatePct)" />`, centered text showing `{{ s.monthAttendanceRatePct !== null ? s.monthAttendanceRatePct + '%' : '—' }}` and the caption `'dashboard.attendanceThisMonth' | translate`. When `s.monthAttendanceRatePct === null`, also show `'dashboard.noSessions' | translate`.
- **Weekly attendance card**: heading `'dashboard.weeklyAttendance' | translate`; `@if (s.last7Days.length) { @for (d of s.last7Days; track d.date) { … } } @else { 'dashboard.noRecentSessions' | translate }`. Each day renders a labeled bar whose present portion width is `vm.dayRatePct(d)`% (e.g. a flex track with a present segment `[style.width.%]="vm.dayRatePct(d)"` and the remainder as absent) plus the date label. Include a small legend with `'dashboard.present' | translate` / `'dashboard.absent' | translate`.
- **Stroke split card**: heading `'dashboard.strokeSplit' | translate`; header number `{{ s.swimmerCount }} {{ 'dashboard.registeredSwimmers' | translate }}`; `@if (s.strokeSplit.length) { @for (st of s.strokeSplit; track st.strokeId) { row: {{ strokeName(st) }}, a bar `[style.width.%]="vm.strokeBarPct(st.count)"`, and `{{ st.count }} } } @else { 'dashboard.noStrokes' | translate }`; footnote `'dashboard.multiStrokeNote' | translate`.

Keep classes consistent with `home.page.html` and the other feature pages; do not introduce new color tokens.

- [ ] **Step 7: Create the barrel**

`index.ts`:

```ts
export { DashboardPage } from './presentation/pages/dashboard/dashboard.page';
export { DashboardViewModel } from './presentation/pages/dashboard/dashboard.viewmodel';
```

- [ ] **Step 8: Run the page test to verify it passes**

Run: `cd frontend && npx jest dashboard.page`
Expected: PASS.

- [ ] **Step 9: Verify i18n JSON is valid**

Run: `cd frontend && node -e "require('./src/app/core/i18n/en.json'); require('./src/app/core/i18n/ar.json'); console.log('ok')"`
Expected: prints `ok` (no JSON parse error).

- [ ] **Step 10: Commit** *(checkpoint — do not run until the user authorizes)*

```bash
git add frontend/src/app/features/dashboard frontend/src/app/core/i18n
git commit -m "feat(dashboard): page component, template, and i18n"
```

---

## Task 8: Wire providers, swap the /home route, retire the launcher

**Files:**
- Modify: `frontend/src/app/app.config.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Delete: `frontend/src/app/features/home/` (entire folder: page, template, spec, barrel)

**Interfaces:**
- Consumes: `DASHBOARD_PROVIDERS` (Task 5), `DashboardPage` + `DashboardViewModel` (Tasks 6–7).

- [ ] **Step 1: Confirm nothing else imports `@features/home`**

Run: `cd frontend && grep -rn "@features/home" src --include=*.ts`
Expected: only `app.routes.ts` matches. If anything else matches, stop and reconcile before deleting.

- [ ] **Step 2: Wire the providers in `app.config.ts`**

Add the import (next to the other feature providers):

```ts
import { DASHBOARD_PROVIDERS } from '@features/dashboard/data/dashboard.providers';
```

And add to the `providers` array (next to `...CHAMPIONSHIPS_PROVIDERS`):

```ts
    ...DASHBOARD_PROVIDERS,
```

- [ ] **Step 3: Swap the `/home` route in `app.routes.ts`**

Replace the import of `HomePage` usage. Change the `home` child route from:

```ts
{ path: 'home', canActivate: [firstLoginGuard], loadComponent: () => import('@features/home').then((m) => m.HomePage) },
```

to:

```ts
{
  path: 'home',
  canActivate: [firstLoginGuard],
  loadComponent: () => import('@features/dashboard').then((m) => m.DashboardPage),
  providers: [DashboardViewModel],
},
```

Add `DashboardViewModel` to the top-of-file import from `@features/dashboard`:

```ts
import { DashboardViewModel } from '@features/dashboard';
```

Leave the `{ path: '', pathMatch: 'full', redirectTo: 'home' }` line and everything else untouched.

- [ ] **Step 4: Delete the retired launcher feature**

Run: `cd frontend && rm -rf src/app/features/home`

- [ ] **Step 5: Verify the frontend builds**

Run: `cd frontend && npx ng build`
Expected: build succeeds with no unresolved `@features/home` references.

- [ ] **Step 6: Run the full frontend test suite**

Run: `cd frontend && npx jest`
Expected: PASS. The old `home.page.spec.ts` is gone (deleted with the feature); no test references it.

- [ ] **Step 7: Manually verify in the browser (recommended)**

Run the app, log in, and confirm `/home` now shows the dashboard (hero + ring, weekly bars, stroke split), the sidebar "Dashboard" link lands here, and RTL/Arabic renders correctly. Try an account/club with no attendance this month to confirm the ring shows "—".

- [ ] **Step 8: Commit** *(checkpoint — do not run until the user authorizes)*

```bash
git add frontend/src/app/app.config.ts frontend/src/app/app.routes.ts
git add -A frontend/src/app/features/home
git commit -m "feat(dashboard): mount dashboard at /home and retire the launcher"
```

---

## Done-when

- `GET /api/dashboard/summary` returns the composite payload and the full backend test suite is green.
- `/home` renders the data dashboard in the existing RTL/Arabic design system; the launcher feature is deleted.
- All new frontend Jest specs pass and `ng build` succeeds.
- No database migration or seed was created.
- No commits or pushes were made without explicit user authorization.
