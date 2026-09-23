# Attendance Entry page (coach write path) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **⚠️ COMMIT POLICY (user instruction):** The user said *"don't commit anything until I tell you."* The `git commit` steps below are written for completeness but MUST NOT be run until the user explicitly authorizes committing. Complete each task's code + tests, keep the tree uncommitted, and let the user drive commits.

**Goal:** Build the coach-facing Attendance Entry page — pick a date, see the whole swimmer roster with a Present/Late/Absent/Excused toggle + optional note + each swimmer's month attendance rate, and save the session in one atomic upsert.

**Architecture:** Adds a write path to the existing (read-only) `Attendance` backend module: `AttendanceRecord.Update`, three repository methods (`ListByDateAsync`, `ListByMonthAsync`, `UpsertSessionAsync`), three service methods, and two new "session" actions on `AttendanceRecordsController` that compose Identity's roster + Attendance records/rates. The frontend builds out the placeholder `attendance` feature as a full clean-architecture slice (dto → mapper → model → repository → use-cases → viewmodel → page).

**Tech Stack:** .NET 10, EF Core (Npgsql; EF InMemory for tests), xUnit + Moq (backend); Angular (standalone components, signals), Jest (frontend). API envelope = `ApiResponse<T>` / `BaseResponseRs<T>`.

**Spec:** `docs/superpowers/specs/2026-09-23-attendance-entry-page-design.md`

## Global Constraints

- **No schema change / no migration** — `attendance.attendance_record` and `reference.attendance_status` already exist. Do not add a migration.
- **No new DI registration** — `IAttendanceService` + `IAttendanceRecordRepository` are already registered in `AttendanceModuleExtensions`; `ISwimmerService` + `IReferenceService` by `AddIdentityModule`. Only add methods to existing interfaces.
- **Loose Guids across modules** — `SwimmerId`, `StatusId`, `RecordedBy` are plain Guids (no cross-module FK). The Attendance module gets **no** reference to Identity.
- **Upsert key** = the existing unique index `(SwimmerId, SessionDate)`. Never insert a duplicate for the same swimmer+date.
- **Month rate** = `(present + late) / (present + late + absent)` as an integer percent (0–100), **excused excluded** from numerator and denominator; `null` when the denominator is 0.
- **Note language** — the single note field maps to `CoachNoteEn` when `AppLanguage.Current != "ar"`, else `CoachNoteAr`. On read, resolve to the current language, falling back to the other column.
- **Save authorization** — `PUT …/session` is `[Authorize(Roles = "head_coach,captain")]`. The GET and the page itself are open to any authenticated user.
- **Feedback is optional** — no save gating on notes. "Save Session" is enabled whenever the working roster is dirty.
- **Default-Present is a client concern** — the server returns `statusId: null, hasRecord: false` for swimmers with no record on the date; the frontend defaults those to the Present status id.
- **Localized strings** live in resource classes (backend `AttendanceMessages`) and i18n JSON (`en.json` / `ar.json`); never hardcode user-facing English in components.

---

## File Structure

**Backend (modify):**
- `backend/src/Modules/Attendance/…Domain/Entities/AttendanceRecord.cs` — add `Update(...)`.
- `backend/src/Modules/Attendance/…Domain/Repositories/IAttendanceRecordRepository.cs` — 3 new methods.
- `backend/src/Modules/Attendance/…Infrastructure/Repositories/AttendanceRecordRepository.cs` — implement them.
- `backend/src/Modules/Attendance/…Application/DTOs/AttendanceRecordDtos.cs` — add session DTOs + save request + service-input record.
- `backend/src/Modules/Attendance/…Application/Resources/AttendanceMessages.cs` — add Success/Errors messages.
- `backend/src/Modules/Attendance/…Application/Services/Interfaces/IAttendanceService.cs` — 3 new methods.
- `backend/src/Modules/Attendance/…Application/Services/AttendanceService.cs` — implement them.
- `backend/Kheprx.BaseBackend.Api/Controllers/AttendanceRecordsController.cs` — 2 new actions + inject `ISwimmerService`, `IReferenceService`.

**Backend (test):**
- `…Attendance.UnitTests/Entities/AttendanceRecordTests.cs` — Update tests (append).
- `…Attendance.UnitTests/Repositories/AttendanceRecordRepositoryTests.cs` — new-method tests (append).
- `…Attendance.UnitTests/Services/AttendanceServiceTests.cs` — **create**: month-rate buckets + save.
- `…Api.UnitTests/AttendanceRecordsControllerSessionTests.cs` — **create**: compose + save + auth.

**Frontend (create — feature `features/attendance`):**
- `data/dto/attendance-session.dto.ts` — response DTOs + validators + `SaveSessionDtoRq`.
- `data/dto/attendance-session.mapper.ts` — DtoRs → domain.
- `domain/model/attendance-session.ts` — `SessionRow`, `AttendanceSession`.
- `domain/repositories/attendance-entry.repository.ts` — port + `ATTENDANCE_ENTRY_REPOSITORY` token.
- `data/repositories/attendance-entry.repository.impl.ts` — HTTP impl.
- `data/attendance.providers.ts` — token → impl binding.
- `domain/usecases/load-attendance-session.use-case.ts`, `domain/usecases/save-attendance-session.use-case.ts`.
- `presentation/pages/attendance/attendance.viewmodel.ts` — page state.
- `presentation/pages/attendance/attendance.page.ts` + `.html` — **rebuild** the placeholder.
- `index.ts` — export the viewmodel too.

**Frontend (create — tests, under `features/attendance/testing/…` mirroring source path):**
- `data/dto/attendance-session.mapper.spec.ts`
- `data/repositories/attendance-entry.repository.impl.spec.ts`
- `domain/usecases/load-attendance-session.use-case.spec.ts`, `…/save-attendance-session.use-case.spec.ts`
- `presentation/pages/attendance/attendance.viewmodel.spec.ts`

**Frontend (modify):**
- `src/app/app.routes.ts` — add `providers` (viewmodel + attendance providers) to the `attendance` route.
- `src/app/core/i18n/en.json` + `ar.json` — add an `attendanceEntry` block.

---

## Task 1: Domain — `AttendanceRecord.Update`

**Files:**
- Modify: `backend/src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Domain/Entities/AttendanceRecord.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Attendance.UnitTests/Entities/AttendanceRecordTests.cs`

**Interfaces:**
- Produces: `AttendanceRecord.Update(Guid statusId, Guid recordedBy, string? coachNoteEn, string? coachNoteAr)` — mutates status/recorder/notes in place, trimming blanks to null (same rules as the constructor).

- [ ] **Step 1: Write the failing test** (append to `AttendanceRecordTests.cs`)

```csharp
[Fact]
public void Update_overwrites_status_recorder_and_trims_notes()
{
    var rec = new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 23),
        Guid.NewGuid(), Guid.NewGuid(), "old", "قديم");
    var newStatus = Guid.NewGuid();
    var newRecorder = Guid.NewGuid();

    rec.Update(newStatus, newRecorder, "  new note  ", "   ");

    Assert.Equal(newStatus, rec.StatusId);
    Assert.Equal(newRecorder, rec.RecordedBy);
    Assert.Equal("new note", rec.CoachNoteEn); // trimmed
    Assert.Null(rec.CoachNoteAr);              // whitespace → null
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Attendance.UnitTests --filter Update_overwrites_status_recorder_and_trims_notes`
Expected: FAIL — `AttendanceRecord` has no `Update` method (compile error).

> If the build reports a locked DLL, stop the running API first (see memory: "dotnet test dev-server lock").

- [ ] **Step 3: Add the method** (in `AttendanceRecord.cs`, after the constructor)

```csharp
public void Update(Guid statusId, Guid recordedBy, string? coachNoteEn = null, string? coachNoteAr = null)
{
    StatusId = statusId;
    RecordedBy = recordedBy;
    CoachNoteEn = string.IsNullOrWhiteSpace(coachNoteEn) ? null : coachNoteEn.Trim();
    CoachNoteAr = string.IsNullOrWhiteSpace(coachNoteAr) ? null : coachNoteAr.Trim();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Attendance.UnitTests --filter Update_overwrites_status_recorder_and_trims_notes`
Expected: PASS

- [ ] **Step 5: Commit** *(only when the user authorizes committing)*

```bash
git add backend/src/Modules/Attendance backend/tests/Kheprx.BaseBackend.Attendance.UnitTests
git commit -m "feat(attendance): AttendanceRecord.Update for re-marking a session"
```

---

## Task 2: Repository — list-by-date, list-by-month, upsert

**Files:**
- Modify: `backend/src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Domain/Repositories/IAttendanceRecordRepository.cs`
- Modify: `backend/src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Infrastructure/Repositories/AttendanceRecordRepository.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Attendance.UnitTests/Repositories/AttendanceRecordRepositoryTests.cs`

**Interfaces:**
- Consumes: `AttendanceRecord.Update` (Task 1).
- Produces (added to `IAttendanceRecordRepository`):
  - `Task<IReadOnlyList<AttendanceRecord>> ListByDateAsync(DateOnly date, CancellationToken ct = default)`
  - `Task<IReadOnlyList<AttendanceRecord>> ListByMonthAsync(int year, int month, CancellationToken ct = default)`
  - `Task UpsertSessionAsync(DateOnly date, IReadOnlyList<AttendanceRecord> incoming, CancellationToken ct = default)` — for each incoming record, updates the existing `(SwimmerId, date)` row (via `Update`) or inserts it; single `SaveChangesAsync`.

- [ ] **Step 1: Write the failing tests** (append to `AttendanceRecordRepositoryTests.cs`)

```csharp
[Fact]
public async Task ListByDateAsync_returns_only_that_date()
{
    await using var db = NewDb();
    var d = new DateOnly(2026, 9, 23);
    db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), d, Guid.NewGuid(), Guid.NewGuid()));
    db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 24), Guid.NewGuid(), Guid.NewGuid()));
    await db.SaveChangesAsync();

    var list = await new AttendanceRecordRepository(db).ListByDateAsync(d);

    Assert.Single(list);
    Assert.All(list, r => Assert.Equal(d, r.SessionDate));
}

[Fact]
public async Task ListByMonthAsync_returns_only_that_month()
{
    await using var db = NewDb();
    db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 3), Guid.NewGuid(), Guid.NewGuid()));
    db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 28), Guid.NewGuid(), Guid.NewGuid()));
    db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 8, 30), Guid.NewGuid(), Guid.NewGuid()));
    await db.SaveChangesAsync();

    var list = await new AttendanceRecordRepository(db).ListByMonthAsync(2026, 9);

    Assert.Equal(2, list.Count);
    Assert.All(list, r => Assert.Equal(9, r.SessionDate.Month));
}

[Fact]
public async Task UpsertSessionAsync_inserts_new_and_updates_existing_without_duplicating()
{
    await using var db = NewDb();
    var d = new DateOnly(2026, 9, 23);
    var swimmerA = Guid.NewGuid();
    var swimmerB = Guid.NewGuid();
    var statusPresent = Guid.NewGuid();
    var statusAbsent = Guid.NewGuid();
    var coach = Guid.NewGuid();

    // A already has a record for the date (Present); B has none.
    db.AttendanceRecords.Add(new AttendanceRecord(swimmerA, d, statusPresent, coach));
    await db.SaveChangesAsync();

    var repo = new AttendanceRecordRepository(db);
    await repo.UpsertSessionAsync(d, new[]
    {
        new AttendanceRecord(swimmerA, d, statusAbsent, coach, "changed", null), // update A → Absent
        new AttendanceRecord(swimmerB, d, statusPresent, coach),                 // insert B
    });

    var all = await repo.ListByDateAsync(d);
    Assert.Equal(2, all.Count); // no duplicate for A
    var a = all.Single(r => r.SwimmerId == swimmerA);
    Assert.Equal(statusAbsent, a.StatusId);
    Assert.Equal("changed", a.CoachNoteEn);
    Assert.Contains(all, r => r.SwimmerId == swimmerB && r.StatusId == statusPresent);
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Attendance.UnitTests --filter "ListByDateAsync_returns_only_that_date|ListByMonthAsync_returns_only_that_month|UpsertSessionAsync_inserts_new_and_updates_existing_without_duplicating"`
Expected: FAIL — methods not defined (compile error).

- [ ] **Step 3: Add the interface methods** (in `IAttendanceRecordRepository.cs`)

```csharp
Task<IReadOnlyList<AttendanceRecord>> ListByDateAsync(DateOnly date, CancellationToken ct = default);
Task<IReadOnlyList<AttendanceRecord>> ListByMonthAsync(int year, int month, CancellationToken ct = default);
Task UpsertSessionAsync(DateOnly date, IReadOnlyList<AttendanceRecord> incoming, CancellationToken ct = default);
```

- [ ] **Step 4: Implement them** (in `AttendanceRecordRepository.cs`)

```csharp
public async Task<IReadOnlyList<AttendanceRecord>> ListByDateAsync(DateOnly date, CancellationToken ct = default)
    => await _db.AttendanceRecords.AsNoTracking()
          .Where(r => r.SessionDate == date)
          .ToListAsync(ct);

public async Task<IReadOnlyList<AttendanceRecord>> ListByMonthAsync(int year, int month, CancellationToken ct = default)
    => await _db.AttendanceRecords.AsNoTracking()
          .Where(r => r.SessionDate.Year == year && r.SessionDate.Month == month)
          .ToListAsync(ct);

public async Task UpsertSessionAsync(DateOnly date, IReadOnlyList<AttendanceRecord> incoming, CancellationToken ct = default)
{
    // Tracked load (no AsNoTracking) so Update() mutations are persisted.
    var existing = await _db.AttendanceRecords.Where(r => r.SessionDate == date).ToListAsync(ct);
    var bySwimmer = existing.ToDictionary(r => r.SwimmerId);
    foreach (var rec in incoming)
    {
        if (bySwimmer.TryGetValue(rec.SwimmerId, out var current))
            current.Update(rec.StatusId, rec.RecordedBy, rec.CoachNoteEn, rec.CoachNoteAr);
        else
            _db.AttendanceRecords.Add(rec);
    }
    await _db.SaveChangesAsync(ct);
}
```

- [ ] **Step 5: Run to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Attendance.UnitTests`
Expected: PASS (all)

- [ ] **Step 6: Commit** *(only when the user authorizes committing)*

```bash
git add backend/src/Modules/Attendance backend/tests/Kheprx.BaseBackend.Attendance.UnitTests
git commit -m "feat(attendance): repo list-by-date, list-by-month, session upsert"
```

---

## Task 3: Application — DTOs, messages, service methods

**Files:**
- Modify: `backend/src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Application/DTOs/AttendanceRecordDtos.cs`
- Modify: `backend/src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Application/Resources/AttendanceMessages.cs`
- Modify: `backend/src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Application/Services/Interfaces/IAttendanceService.cs`
- Modify: `backend/src/Modules/Attendance/Kheprx.BaseBackend.Attendance.Application/Services/AttendanceService.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Attendance.UnitTests/Services/AttendanceServiceTests.cs` (create)

**Interfaces:**
- Consumes: repository methods (Task 2).
- Produces:
  - DTOs (in `AttendanceRecordDtos.cs`):
    ```csharp
    public sealed record SwimmerSessionRowDto(
        Guid SwimmerId, string Uid, string NameEn, string? NameAr,
        string? ClubNameEn, string? ClubNameAr, string GenderCode,
        Guid? StatusId, string? CoachNote, int? MonthRatePct, bool HasRecord);

    public sealed record AttendanceSessionDto(DateOnly Date, IReadOnlyList<SwimmerSessionRowDto> Rows);

    public sealed record SaveSessionRequest(DateOnly Date, IReadOnlyList<SaveSessionEntryRequest> Entries);
    public sealed record SaveSessionEntryRequest(Guid SwimmerId, Guid StatusId, string? CoachNote);

    public sealed record SaveSessionEntry(Guid SwimmerId, Guid StatusId, string? CoachNoteEn, string? CoachNoteAr);
    ```
  - `IAttendanceService`:
    - `Task<IReadOnlyList<AttendanceRecordDto>> ListByDateAsync(DateOnly date, CancellationToken ct = default)`
    - `Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, int>>> GetMonthStatusCountsAsync(int year, int month, CancellationToken ct = default)` — swimmerId → (statusId → count).
    - `Task SaveSessionAsync(DateOnly date, IReadOnlyList<SaveSessionEntry> entries, Guid recordedBy, CancellationToken ct = default)`

- [ ] **Step 1: Write the failing service tests** (create `AttendanceServiceTests.cs`)

```csharp
using Kheprx.BaseBackend.Attendance.Application.DTOs;
using Kheprx.BaseBackend.Attendance.Application.Services;
using Kheprx.BaseBackend.Attendance.Domain.Entities;
using Kheprx.BaseBackend.Attendance.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Attendance.UnitTests.Services;

public class AttendanceServiceTests
{
    private static readonly Guid Present = Guid.NewGuid();
    private static readonly Guid Late = Guid.NewGuid();
    private static readonly Guid Absent = Guid.NewGuid();
    private static readonly Guid Excused = Guid.NewGuid();

    [Fact]
    public async Task GetMonthStatusCountsAsync_groups_counts_by_swimmer_then_status()
    {
        var swimmer = Guid.NewGuid();
        var repo = new Mock<IAttendanceRecordRepository>();
        repo.Setup(r => r.ListByMonthAsync(2026, 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new AttendanceRecord(swimmer, new DateOnly(2026, 9, 1), Present, Guid.NewGuid()),
                new AttendanceRecord(swimmer, new DateOnly(2026, 9, 2), Present, Guid.NewGuid()),
                new AttendanceRecord(swimmer, new DateOnly(2026, 9, 3), Absent,  Guid.NewGuid()),
            });
        var svc = new AttendanceService(repo.Object);

        var counts = await svc.GetMonthStatusCountsAsync(2026, 9);

        Assert.Equal(2, counts[swimmer][Present]);
        Assert.Equal(1, counts[swimmer][Absent]);
        Assert.False(counts[swimmer].ContainsKey(Late));
    }

    [Fact]
    public async Task SaveSessionAsync_maps_entries_to_records_stamped_with_recorder()
    {
        var date = new DateOnly(2026, 9, 23);
        var recorder = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        IReadOnlyList<AttendanceRecord>? captured = null;
        var repo = new Mock<IAttendanceRecordRepository>();
        repo.Setup(r => r.UpsertSessionAsync(date, It.IsAny<IReadOnlyList<AttendanceRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<DateOnly, IReadOnlyList<AttendanceRecord>, CancellationToken>((_, recs, _) => captured = recs)
            .Returns(Task.CompletedTask);
        var svc = new AttendanceService(repo.Object);

        await svc.SaveSessionAsync(date, new[]
        {
            new SaveSessionEntry(swimmer, Present, "note en", null),
        }, recorder);

        Assert.NotNull(captured);
        var rec = Assert.Single(captured!);
        Assert.Equal(swimmer, rec.SwimmerId);
        Assert.Equal(date, rec.SessionDate);
        Assert.Equal(Present, rec.StatusId);
        Assert.Equal(recorder, rec.RecordedBy);
        Assert.Equal("note en", rec.CoachNoteEn);
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Attendance.UnitTests --filter AttendanceServiceTests`
Expected: FAIL — new DTOs/methods not defined (compile error).

- [ ] **Step 3: Add the DTOs** (append to `AttendanceRecordDtos.cs`)

Paste the four `record` definitions from the **Produces** block above.

- [ ] **Step 4: Add the service interface methods** (in `IAttendanceService.cs`)

```csharp
Task<IReadOnlyList<AttendanceRecordDto>> ListByDateAsync(DateOnly date, CancellationToken ct = default);
Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, int>>> GetMonthStatusCountsAsync(int year, int month, CancellationToken ct = default);
Task SaveSessionAsync(DateOnly date, IReadOnlyList<SaveSessionEntry> entries, Guid recordedBy, CancellationToken ct = default);
```

- [ ] **Step 5: Implement the service methods** (in `AttendanceService.cs`)

```csharp
public async Task<IReadOnlyList<AttendanceRecordDto>> ListByDateAsync(DateOnly date, CancellationToken ct = default)
{
    var rows = await _records.ListByDateAsync(date, ct);
    return rows.Select(ToDto).ToList();
}

public async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, int>>> GetMonthStatusCountsAsync(
    int year, int month, CancellationToken ct = default)
{
    var rows = await _records.ListByMonthAsync(year, month, ct);
    return rows
        .GroupBy(r => r.SwimmerId)
        .ToDictionary(
            g => g.Key,
            g => (IReadOnlyDictionary<Guid, int>)g.GroupBy(r => r.StatusId)
                    .ToDictionary(s => s.Key, s => s.Count()));
}

public async Task SaveSessionAsync(DateOnly date, IReadOnlyList<SaveSessionEntry> entries, Guid recordedBy,
    CancellationToken ct = default)
{
    var records = entries
        .Select(e => new AttendanceRecord(e.SwimmerId, date, e.StatusId, recordedBy, e.CoachNoteEn, e.CoachNoteAr))
        .ToList();
    await _records.UpsertSessionAsync(date, records, ct);
}
```

- [ ] **Step 6: Add controller-facing messages** (in `AttendanceMessages.cs`, extend the class)

```csharp
public static class Success
{
    public static string Listed(string lang) => lang switch { "ar" => "سجل الحضور", _ => "Attendance" };
    public static string SessionLoaded(string lang) => lang switch { "ar" => "جلسة الحضور", _ => "Attendance session" };
    public static string SessionSaved(string lang) => lang switch { "ar" => "تم حفظ الحضور", _ => "Attendance saved" };
}

public static class Errors
{
    public static string DuplicateSwimmer(string lang) => lang switch { "ar" => "سبّاح مكرر في الطلب", _ => "Duplicate swimmer in request" };
    public static string InvalidStatus(string lang) => lang switch { "ar" => "حالة حضور غير صالحة", _ => "Invalid attendance status" };
}
```

- [ ] **Step 7: Run to verify service tests pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Attendance.UnitTests`
Expected: PASS (all)

- [ ] **Step 8: Commit** *(only when the user authorizes committing)*

```bash
git add backend/src/Modules/Attendance backend/tests/Kheprx.BaseBackend.Attendance.UnitTests
git commit -m "feat(attendance): session DTOs + service (by-date, month counts, save)"
```

---

## Task 4: API — session GET + PUT on `AttendanceRecordsController`

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/AttendanceRecordsController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/AttendanceRecordsControllerSessionTests.cs` (create)

**Interfaces:**
- Consumes: `IAttendanceService` (Task 3), Identity `ISwimmerService.ListAsync`, `IReferenceService.GetAttendanceStatusesAsync` → `IReadOnlyList<CodedLookupDto>` (`Id, Code, NameEn, NameAr`), `BaseApiController.CurrentUserId()`.
- Produces: `GET /api/attendance-records/session?date=` → `ApiResponse<AttendanceSessionDto>`; `PUT /api/attendance-records/session` (body `SaveSessionRequest`) → `ApiResponse<AttendanceSessionDto>`.

- [ ] **Step 1: Write the failing controller tests** (create `AttendanceRecordsControllerSessionTests.cs`)

```csharp
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Attendance.Application.DTOs;
using Kheprx.BaseBackend.Attendance.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class AttendanceRecordsControllerSessionTests
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

    private static AttendanceRecordsController Build(
        Mock<IAttendanceService> svc, Mock<ISwimmerService> swimmers, Mock<IReferenceService> reference,
        Guid? userId = null)
    {
        var users = new Mock<IUserService>();
        var controller = new AttendanceRecordsController(svc.Object, users.Object, swimmers.Object, reference.Object);
        var claims = userId is { } id
            ? new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(JwtRegisteredClaimNames.Sub, id.ToString()) }))
            : new ClaimsPrincipal(new ClaimsIdentity());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = claims } };
        return controller;
    }

    [Fact]
    public async Task Session_composes_roster_with_records_and_rates()
    {
        var date = new DateOnly(2026, 9, 23);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var swimmers = new Mock<ISwimmerService>();
        swimmers.Setup(s => s.ListAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new SwimmerListItemDto(a, "SW-A", "Alice", null, "Oasis", null, "female", 15),
            new SwimmerListItemDto(b, "SW-B", "Bob", null, "Oasis", null, "male", 16),
        });
        var svc = new Mock<IAttendanceService>();
        // A has a record for the date; B has none.
        svc.Setup(s => s.ListByDateAsync(date, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new AttendanceRecordDto(Guid.NewGuid(), a, date, Present, "great", null, Guid.NewGuid(), "", null),
        });
        // A month: present+present+absent → rate = round(2/3*100)=67 (excused excluded, none here).
        svc.Setup(s => s.GetMonthStatusCountsAsync(2026, 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyDictionary<Guid, int>>
            {
                [a] = new Dictionary<Guid, int> { [Present] = 2, [Absent] = 1 },
            });
        var reference = new Mock<IReferenceService>();
        reference.Setup(r => r.GetAttendanceStatusesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Statuses());

        var controller = Build(svc, swimmers, reference);
        var result = await controller.Session(date, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<AttendanceSessionDto>>(ok.Value);
        var rows = body.Data!.Rows;
        Assert.Equal(2, rows.Count);
        var rowA = rows.Single(r => r.SwimmerId == a);
        Assert.True(rowA.HasRecord);
        Assert.Equal(Present, rowA.StatusId);
        Assert.Equal("great", rowA.CoachNote);
        Assert.Equal(67, rowA.MonthRatePct);
        var rowB = rows.Single(r => r.SwimmerId == b);
        Assert.False(rowB.HasRecord);
        Assert.Null(rowB.StatusId);
        Assert.Null(rowB.MonthRatePct);
    }

    [Fact]
    public async Task SaveSession_rejects_duplicate_swimmer()
    {
        var dup = Guid.NewGuid();
        var reference = new Mock<IReferenceService>();
        reference.Setup(r => r.GetAttendanceStatusesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Statuses());
        var controller = Build(new Mock<IAttendanceService>(), new Mock<ISwimmerService>(), reference, Guid.NewGuid());

        var req = new SaveSessionRequest(new DateOnly(2026, 9, 23), new[]
        {
            new SaveSessionEntryRequest(dup, Present, null),
            new SaveSessionEntryRequest(dup, Absent, null),
        });
        var result = await controller.SaveSession(req, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task SaveSession_rejects_unknown_status()
    {
        var reference = new Mock<IReferenceService>();
        reference.Setup(r => r.GetAttendanceStatusesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Statuses());
        var controller = Build(new Mock<IAttendanceService>(), new Mock<ISwimmerService>(), reference, Guid.NewGuid());

        var req = new SaveSessionRequest(new DateOnly(2026, 9, 23), new[]
        {
            new SaveSessionEntryRequest(Guid.NewGuid(), Guid.NewGuid() /* not a real status */, null),
        });
        var result = await controller.SaveSession(req, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task SaveSession_stamps_current_user_and_reloads_session()
    {
        var date = new DateOnly(2026, 9, 23);
        var me = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        var svc = new Mock<IAttendanceService>();
        svc.Setup(s => s.SaveSessionAsync(date, It.IsAny<IReadOnlyList<SaveSessionEntry>>(), me, It.IsAny<CancellationToken>()))
           .Returns(Task.CompletedTask);
        svc.Setup(s => s.ListByDateAsync(date, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<AttendanceRecordDto>());
        svc.Setup(s => s.GetMonthStatusCountsAsync(2026, 9, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new Dictionary<Guid, IReadOnlyDictionary<Guid, int>>());
        var swimmers = new Mock<ISwimmerService>();
        swimmers.Setup(s => s.ListAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<SwimmerListItemDto>());
        var reference = new Mock<IReferenceService>();
        reference.Setup(r => r.GetAttendanceStatusesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Statuses());

        var controller = Build(svc, swimmers, reference, me);
        var req = new SaveSessionRequest(date, new[] { new SaveSessionEntryRequest(swimmer, Present, "hi") });
        var result = await controller.SaveSession(req, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        svc.Verify(s => s.SaveSessionAsync(date, It.IsAny<IReadOnlyList<SaveSessionEntry>>(), me, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void SaveSession_requires_coach_roles()
    {
        var method = typeof(AttendanceRecordsController).GetMethod(nameof(AttendanceRecordsController.SaveSession))!;
        var attr = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute?)Attribute
            .GetCustomAttribute(method, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute));
        Assert.NotNull(attr);
        Assert.Equal("head_coach,captain", attr!.Roles);
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter AttendanceRecordsControllerSessionTests`
Expected: FAIL — constructor arity + `Session`/`SaveSession` don't exist (compile error).

- [ ] **Step 3: Extend the controller** — add the two Identity dependencies and both actions (in `AttendanceRecordsController.cs`)

Update the constructor/fields:

```csharp
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces; // ISwimmerService, IReferenceService, IUserService

private readonly IAttendanceService _service;
private readonly IUserService _users;
private readonly ISwimmerService _swimmers;
private readonly IReferenceService _reference;

public AttendanceRecordsController(IAttendanceService service, IUserService users,
    ISwimmerService swimmers, IReferenceService reference)
{
    _service = service;
    _users = users;
    _swimmers = swimmers;
    _reference = reference;
}
```

Add the actions (keep the existing `List` + `EnrichRecorders`):

```csharp
/// <summary>Loads the whole roster's attendance for a date: status + note (if any) + this month's rate.</summary>
[HttpGet("session")]
[ProducesResponseType(typeof(ApiResponse<AttendanceSessionDto>), StatusCodes.Status200OK)]
public async Task<ActionResult<ApiResponse<AttendanceSessionDto>>> Session([FromQuery] DateOnly date, CancellationToken ct)
{
    var lang = AppLanguage.Current;
    var dto = await BuildSessionAsync(date, lang, ct);
    return Ok(ApiResponse<AttendanceSessionDto>.Success(AttendanceMessages.Success.SessionLoaded(lang), dto));
}

/// <summary>Upserts the roster's attendance for a date (create or re-mark). Head Coach or Captain only.</summary>
[HttpPut("session")]
[Authorize(Roles = "head_coach,captain")]
[ProducesResponseType(typeof(ApiResponse<AttendanceSessionDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse<AttendanceSessionDto>), StatusCodes.Status400BadRequest)]
public async Task<ActionResult<ApiResponse<AttendanceSessionDto>>> SaveSession([FromBody] SaveSessionRequest request, CancellationToken ct)
{
    var lang = AppLanguage.Current;

    if (request.Entries.Select(e => e.SwimmerId).Distinct().Count() != request.Entries.Count)
        return BadRequest(ApiResponse<AttendanceSessionDto>.Failure(AttendanceMessages.Errors.DuplicateSwimmer(lang), "validation"));

    var statuses = await _reference.GetAttendanceStatusesAsync(ct);
    var validIds = statuses.Select(s => s.Id).ToHashSet();
    if (request.Entries.Any(e => !validIds.Contains(e.StatusId)))
        return BadRequest(ApiResponse<AttendanceSessionDto>.Failure(AttendanceMessages.Errors.InvalidStatus(lang), "validation"));

    var toAr = lang == "ar";
    var entries = request.Entries
        .Select(e => new SaveSessionEntry(e.SwimmerId, e.StatusId, toAr ? null : e.CoachNote, toAr ? e.CoachNote : null))
        .ToList();

    await _service.SaveSessionAsync(request.Date, entries, CurrentUserId(), ct);

    var dto = await BuildSessionAsync(request.Date, lang, ct);
    return Ok(ApiResponse<AttendanceSessionDto>.Success(AttendanceMessages.Success.SessionSaved(lang), dto));
}

// Composes roster (Identity) + records-for-date + month status-counts (Attendance) into session rows.
private async Task<AttendanceSessionDto> BuildSessionAsync(DateOnly date, string lang, CancellationToken ct)
{
    var roster = await _swimmers.ListAsync(null, ct);
    var records = await _service.ListByDateAsync(date, ct);
    var counts = await _service.GetMonthStatusCountsAsync(date.Year, date.Month, ct);
    var statuses = await _reference.GetAttendanceStatusesAsync(ct);

    Guid IdOf(string code) => statuses.FirstOrDefault(s => s.Code == code)?.Id ?? Guid.Empty;
    var present = IdOf("present");
    var late = IdOf("late");
    var absent = IdOf("absent");

    var recBySwimmer = records
        .GroupBy(r => r.SwimmerId)
        .ToDictionary(g => g.Key, g => g.First());

    var rows = roster.Select(s =>
    {
        recBySwimmer.TryGetValue(s.Id, out var rec);

        int? rate = null;
        if (counts.TryGetValue(s.Id, out var c))
        {
            int Count(Guid id) => id != Guid.Empty && c.TryGetValue(id, out var n) ? n : 0;
            var attended = Count(present) + Count(late);
            var denom = attended + Count(absent);
            rate = denom > 0 ? (int)Math.Round(attended * 100.0 / denom) : (int?)null;
        }

        var note = rec is null ? null
            : lang == "ar" ? (rec.CoachNoteAr ?? rec.CoachNoteEn) : (rec.CoachNoteEn ?? rec.CoachNoteAr);

        return new SwimmerSessionRowDto(
            s.Id, s.Uid, s.NameEn, s.NameAr, s.ClubNameEn, s.ClubNameAr, s.GenderCode,
            rec?.StatusId, note, rate, rec is not null);
    }).ToList();

    return new AttendanceSessionDto(date, rows);
}
```

Add `using Kheprx.BaseBackend.Attendance.Application.DTOs;` if not already present (it is, for `AttendanceRecordDto`).

- [ ] **Step 4: Run to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter AttendanceRecordsControllerSessionTests`
Expected: PASS

- [ ] **Step 5: Full backend build + test**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: PASS (all). If a DLL is locked, stop the running API first.

- [ ] **Step 6: Commit** *(only when the user authorizes committing)*

```bash
git add backend/Kheprx.BaseBackend.Api backend/tests/Kheprx.BaseBackend.Api.UnitTests
git commit -m "feat(attendance): session GET + PUT (compose roster, upsert, coach-only save)"
```

---

## Task 5: Frontend — model, DTOs, mapper

**Files:**
- Create: `frontend/src/app/features/attendance/domain/model/attendance-session.ts`
- Create: `frontend/src/app/features/attendance/data/dto/attendance-session.dto.ts`
- Create: `frontend/src/app/features/attendance/data/dto/attendance-session.mapper.ts`
- Test: `frontend/src/app/features/attendance/testing/data/dto/attendance-session.mapper.spec.ts`

**Interfaces:**
- Produces:
  - Model `SessionRow`, `AttendanceSession` (below).
  - DTO `SwimmerSessionRowDtoRs`, `AttendanceSessionDtoRs`, `AttendanceSessionItemDtoRs extends BaseResponseRs<AttendanceSessionDtoRs>`, `SaveSessionDtoRq`, `isAttendanceSessionDtoRsValid(x)`.
  - `toAttendanceSession(d: AttendanceSessionDtoRs): AttendanceSession`.

- [ ] **Step 1: Create the domain model** (`attendance-session.ts`)

```typescript
export interface SessionRow {
  swimmerId: string;
  uid: string;
  nameEn: string;
  nameAr: string | null;
  clubNameEn: string | null;
  clubNameAr: string | null;
  genderCode: string;
  statusId: string | null;   // null = no record yet for this date
  coachNote: string | null;
  monthRatePct: number | null;
  hasRecord: boolean;
}

export interface AttendanceSession {
  date: string;              // 'YYYY-MM-DD'
  rows: SessionRow[];
}
```

- [ ] **Step 2: Create the DTOs + validators** (`attendance-session.dto.ts`)

```typescript
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface SwimmerSessionRowDtoRs {
  swimmerId: string;
  uid: string;
  nameEn: string;
  nameAr: string | null;
  clubNameEn: string | null;
  clubNameAr: string | null;
  genderCode: string;
  statusId: string | null;
  coachNote: string | null;
  monthRatePct: number | null;
  hasRecord: boolean;
}

export interface AttendanceSessionDtoRs {
  date: string;
  rows: SwimmerSessionRowDtoRs[];
}

export interface AttendanceSessionItemDtoRs extends BaseResponseRs<AttendanceSessionDtoRs> {}

export interface SaveSessionEntryDtoRq {
  swimmerId: string;
  statusId: string;
  coachNote: string | null;
}
export interface SaveSessionDtoRq {
  date: string;
  entries: SaveSessionEntryDtoRq[];
}

export function isAttendanceSessionDtoRsValid(x: unknown): x is AttendanceSessionDtoRs {
  const d = x as AttendanceSessionDtoRs;
  return !!d && typeof d === 'object'
    && typeof d.date === 'string'
    && Array.isArray(d.rows)
    && d.rows.every((r) =>
      !!r && typeof r.swimmerId === 'string' && typeof r.nameEn === 'string'
      && typeof r.hasRecord === 'boolean');
}
```

- [ ] **Step 3: Create the mapper** (`attendance-session.mapper.ts`)

```typescript
import { AttendanceSessionDtoRs, SwimmerSessionRowDtoRs } from '@features/attendance/data/dto/attendance-session.dto';
import { AttendanceSession, SessionRow } from '@features/attendance/domain/model/attendance-session';

function toRow(d: SwimmerSessionRowDtoRs): SessionRow {
  return {
    swimmerId: d.swimmerId,
    uid: d.uid,
    nameEn: d.nameEn,
    nameAr: d.nameAr,
    clubNameEn: d.clubNameEn,
    clubNameAr: d.clubNameAr,
    genderCode: d.genderCode,
    statusId: d.statusId,
    coachNote: d.coachNote,
    monthRatePct: d.monthRatePct,
    hasRecord: d.hasRecord,
  };
}

export function toAttendanceSession(d: AttendanceSessionDtoRs): AttendanceSession {
  return { date: d.date, rows: d.rows.map(toRow) };
}
```

- [ ] **Step 4: Write the mapper test** (`attendance-session.mapper.spec.ts`)

```typescript
import { toAttendanceSession } from '@features/attendance/data/dto/attendance-session.mapper';
import { isAttendanceSessionDtoRsValid } from '@features/attendance/data/dto/attendance-session.dto';

describe('attendance-session mapper', () => {
  const dto = {
    date: '2026-09-23',
    rows: [{
      swimmerId: 's1', uid: 'SW-1', nameEn: 'Alice', nameAr: null,
      clubNameEn: 'Oasis', clubNameAr: null, genderCode: 'female',
      statusId: 'st1', coachNote: 'great', monthRatePct: 67, hasRecord: true,
    }],
  };

  it('accepts a valid dto and maps date + rows', () => {
    expect(isAttendanceSessionDtoRsValid(dto)).toBe(true);
    const session = toAttendanceSession(dto);
    expect(session.date).toBe('2026-09-23');
    expect(session.rows).toHaveLength(1);
    expect(session.rows[0].swimmerId).toBe('s1');
    expect(session.rows[0].hasRecord).toBe(true);
    expect(session.rows[0].monthRatePct).toBe(67);
  });

  it('rejects a malformed dto', () => {
    expect(isAttendanceSessionDtoRsValid({ date: 5, rows: 'x' })).toBe(false);
  });
});
```

- [ ] **Step 5: Run the test**

Run: `cd frontend && npx jest attendance-session.mapper`
Expected: PASS

- [ ] **Step 6: Commit** *(only when the user authorizes committing)*

```bash
git add frontend/src/app/features/attendance
git commit -m "feat(attendance-fe): session model, dtos, mapper"
```

---

## Task 6: Frontend — repository port + impl + providers

**Files:**
- Create: `frontend/src/app/features/attendance/domain/repositories/attendance-entry.repository.ts`
- Create: `frontend/src/app/features/attendance/data/repositories/attendance-entry.repository.impl.ts`
- Create: `frontend/src/app/features/attendance/data/attendance.providers.ts`
- Test: `frontend/src/app/features/attendance/testing/data/repositories/attendance-entry.repository.impl.spec.ts`

**Interfaces:**
- Consumes: DTOs (Task 5), `HttpClientService`.
- Produces:
  - `IAttendanceEntryRepository { getSession(date: string): Promise<AttendanceSessionItemDtoRs>; saveSession(rq: SaveSessionDtoRq): Promise<AttendanceSessionItemDtoRs>; }`
  - `ATTENDANCE_ENTRY_REPOSITORY` InjectionToken.
  - `ATTENDANCE_PROVIDERS: Provider[]`.

- [ ] **Step 1: Create the port** (`attendance-entry.repository.ts`)

```typescript
import { InjectionToken } from '@angular/core';
import { AttendanceSessionItemDtoRs, SaveSessionDtoRq } from '@features/attendance/data/dto/attendance-session.dto';

export interface IAttendanceEntryRepository {
  getSession(date: string): Promise<AttendanceSessionItemDtoRs>;
  saveSession(rq: SaveSessionDtoRq): Promise<AttendanceSessionItemDtoRs>;
}

export const ATTENDANCE_ENTRY_REPOSITORY = new InjectionToken<IAttendanceEntryRepository>('ATTENDANCE_ENTRY_REPOSITORY');
```

- [ ] **Step 2: Create the HTTP impl** (`attendance-entry.repository.impl.ts`)

```typescript
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { IAttendanceEntryRepository } from '@features/attendance/domain/repositories/attendance-entry.repository';
import { AttendanceSessionItemDtoRs, SaveSessionDtoRq } from '@features/attendance/data/dto/attendance-session.dto';

@Injectable({ providedIn: 'root' })
export class AttendanceEntryRepositoryImpl implements IAttendanceEntryRepository {
  private readonly http = inject(HttpClientService);

  getSession(date: string): Promise<AttendanceSessionItemDtoRs> {
    return this.http.get<AttendanceSessionItemDtoRs>('/api/attendance-records/session', { params: { date } });
  }

  saveSession(rq: SaveSessionDtoRq): Promise<AttendanceSessionItemDtoRs> {
    return this.http.put<AttendanceSessionItemDtoRs>('/api/attendance-records/session', { body: rq });
  }
}
```

- [ ] **Step 3: Create the providers** (`attendance.providers.ts`)

```typescript
import { Provider } from '@angular/core';
import { ATTENDANCE_ENTRY_REPOSITORY } from '@features/attendance/domain/repositories/attendance-entry.repository';
import { AttendanceEntryRepositoryImpl } from '@features/attendance/data/repositories/attendance-entry.repository.impl';

export const ATTENDANCE_PROVIDERS: Provider[] = [
  { provide: ATTENDANCE_ENTRY_REPOSITORY, useClass: AttendanceEntryRepositoryImpl },
];
```

- [ ] **Step 4: Write the impl test** (`attendance-entry.repository.impl.spec.ts`) — mirror `reference.repository.impl.spec.ts`

```typescript
import { AttendanceEntryRepositoryImpl } from '@features/attendance/data/repositories/attendance-entry.repository.impl';

describe('AttendanceEntryRepositoryImpl', () => {
  function make() {
    const http = { get: jest.fn().mockResolvedValue({ data: null }), put: jest.fn().mockResolvedValue({ data: null }) };
    const repo = new AttendanceEntryRepositoryImpl();
    (repo as unknown as { http: unknown }).http = http;
    return { repo, http };
  }

  it('getSession GETs the session endpoint with the date param', async () => {
    const { repo, http } = make();
    await repo.getSession('2026-09-23');
    expect(http.get).toHaveBeenCalledWith('/api/attendance-records/session', { params: { date: '2026-09-23' } });
  });

  it('saveSession PUTs the payload as the body', async () => {
    const { repo, http } = make();
    const rq = { date: '2026-09-23', entries: [{ swimmerId: 's1', statusId: 'st1', coachNote: null }] };
    await repo.saveSession(rq);
    expect(http.put).toHaveBeenCalledWith('/api/attendance-records/session', { body: rq });
  });
});
```

> Note: if the existing `reference.repository.impl.spec.ts` injects `HttpClientService` via `TestBed` instead of field assignment, follow that style verbatim. Check that spec before writing this one.

- [ ] **Step 5: Run the test**

Run: `cd frontend && npx jest attendance-entry.repository`
Expected: PASS

- [ ] **Step 6: Commit** *(only when the user authorizes committing)*

```bash
git add frontend/src/app/features/attendance
git commit -m "feat(attendance-fe): session repository port, http impl, providers"
```

---

## Task 7: Frontend — use-cases (load + save)

**Files:**
- Create: `frontend/src/app/features/attendance/domain/usecases/load-attendance-session.use-case.ts`
- Create: `frontend/src/app/features/attendance/domain/usecases/save-attendance-session.use-case.ts`
- Test: `frontend/src/app/features/attendance/testing/domain/usecases/load-attendance-session.use-case.spec.ts`
- Test: `frontend/src/app/features/attendance/testing/domain/usecases/save-attendance-session.use-case.spec.ts`

**Interfaces:**
- Consumes: `ATTENDANCE_ENTRY_REPOSITORY`, `isAttendanceSessionDtoRsValid`, `toAttendanceSession`, `SaveSessionDtoRq`.
- Produces:
  - `LoadAttendanceSessionUseCase extends UseCase<string, AttendanceSession>` (input = date).
  - `SaveAttendanceSessionUseCase extends UseCase<SaveSessionDtoRq, AttendanceSession>`.

- [ ] **Step 1: Create the load use-case** (`load-attendance-session.use-case.ts`)

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { ATTENDANCE_ENTRY_REPOSITORY } from '@features/attendance/domain/repositories/attendance-entry.repository';
import { isAttendanceSessionDtoRsValid } from '@features/attendance/data/dto/attendance-session.dto';
import { toAttendanceSession } from '@features/attendance/data/dto/attendance-session.mapper';
import { AttendanceSession } from '@features/attendance/domain/model/attendance-session';

@Injectable({ providedIn: 'root' })
export class LoadAttendanceSessionUseCase extends UseCase<string, AttendanceSession> {
  private readonly repo = inject(ATTENDANCE_ENTRY_REPOSITORY);
  constructor() { super('LoadAttendanceSession'); }
  protected async execute(date: string): Promise<AttendanceSession> {
    const res = await this.repo.getSession(date);
    if (!isAttendanceSessionDtoRsValid(res.data)) throw new AppError('Invalid attendance session received', 'validation');
    return toAttendanceSession(res.data);
  }
}
```

- [ ] **Step 2: Create the save use-case** (`save-attendance-session.use-case.ts`)

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { ATTENDANCE_ENTRY_REPOSITORY } from '@features/attendance/domain/repositories/attendance-entry.repository';
import { isAttendanceSessionDtoRsValid, SaveSessionDtoRq } from '@features/attendance/data/dto/attendance-session.dto';
import { toAttendanceSession } from '@features/attendance/data/dto/attendance-session.mapper';
import { AttendanceSession } from '@features/attendance/domain/model/attendance-session';

@Injectable({ providedIn: 'root' })
export class SaveAttendanceSessionUseCase extends UseCase<SaveSessionDtoRq, AttendanceSession> {
  private readonly repo = inject(ATTENDANCE_ENTRY_REPOSITORY);
  constructor() { super('SaveAttendanceSession'); }
  protected async execute(rq: SaveSessionDtoRq): Promise<AttendanceSession> {
    const res = await this.repo.saveSession(rq);
    if (!isAttendanceSessionDtoRsValid(res.data)) throw new AppError('Invalid attendance session received', 'validation');
    return toAttendanceSession(res.data);
  }
}
```

- [ ] **Step 3: Write the use-case tests**

`load-attendance-session.use-case.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { LoadAttendanceSessionUseCase } from '@features/attendance/domain/usecases/load-attendance-session.use-case';
import { ATTENDANCE_ENTRY_REPOSITORY } from '@features/attendance/domain/repositories/attendance-entry.repository';

const SESSION = {
  date: '2026-09-23',
  rows: [{ swimmerId: 's1', uid: 'SW-1', nameEn: 'Alice', nameAr: null, clubNameEn: null, clubNameAr: null,
           genderCode: 'female', statusId: null, coachNote: null, monthRatePct: null, hasRecord: false }],
};

describe('LoadAttendanceSessionUseCase', () => {
  it('maps a valid response to a domain session', async () => {
    const repo = { getSession: jest.fn().mockResolvedValue({ data: SESSION }), saveSession: jest.fn() };
    TestBed.configureTestingModule({ providers: [{ provide: ATTENDANCE_ENTRY_REPOSITORY, useValue: repo }] });
    const uc = TestBed.inject(LoadAttendanceSessionUseCase);

    const r = await uc.run('2026-09-23');
    expect(r.ok).toBe(true);
    if (r.ok) { expect(r.data.rows).toHaveLength(1); expect(r.data.rows[0].hasRecord).toBe(false); }
    expect(repo.getSession).toHaveBeenCalledWith('2026-09-23');
  });

  it('fails on an invalid response', async () => {
    const repo = { getSession: jest.fn().mockResolvedValue({ data: { date: 1 } }), saveSession: jest.fn() };
    TestBed.configureTestingModule({ providers: [{ provide: ATTENDANCE_ENTRY_REPOSITORY, useValue: repo }] });
    const uc = TestBed.inject(LoadAttendanceSessionUseCase);
    const r = await uc.run('2026-09-23');
    expect(r.ok).toBe(false);
  });
});
```

`save-attendance-session.use-case.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { SaveAttendanceSessionUseCase } from '@features/attendance/domain/usecases/save-attendance-session.use-case';
import { ATTENDANCE_ENTRY_REPOSITORY } from '@features/attendance/domain/repositories/attendance-entry.repository';

const SESSION = { date: '2026-09-23', rows: [] };

describe('SaveAttendanceSessionUseCase', () => {
  it('posts the payload and maps the refreshed session', async () => {
    const repo = { getSession: jest.fn(), saveSession: jest.fn().mockResolvedValue({ data: SESSION }) };
    TestBed.configureTestingModule({ providers: [{ provide: ATTENDANCE_ENTRY_REPOSITORY, useValue: repo }] });
    const uc = TestBed.inject(SaveAttendanceSessionUseCase);

    const rq = { date: '2026-09-23', entries: [{ swimmerId: 's1', statusId: 'st1', coachNote: null }] };
    const r = await uc.run(rq);
    expect(r.ok).toBe(true);
    expect(repo.saveSession).toHaveBeenCalledWith(rq);
  });
});
```

- [ ] **Step 4: Run the tests**

Run: `cd frontend && npx jest attendance-session.use-case`
Expected: PASS (both)

- [ ] **Step 5: Commit** *(only when the user authorizes committing)*

```bash
git add frontend/src/app/features/attendance
git commit -m "feat(attendance-fe): load + save session use-cases"
```

---

## Task 8: Frontend — page viewmodel

**Files:**
- Create: `frontend/src/app/features/attendance/presentation/pages/attendance/attendance.viewmodel.ts`
- Test: `frontend/src/app/features/attendance/testing/presentation/pages/attendance/attendance.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `LoadAttendanceSessionUseCase`, `SaveAttendanceSessionUseCase`, `LoadAttendanceStatusesUseCase` (from `@features/reference`), `AuthSessionStore`, `NotificationService`, `TranslateService`, `LanguageStore`.
- Produces the `AttendanceEntryViewModel` public surface used by the page:
  - signals `loading`, `saving`, `error`, `selectedDate`, `rows` (`EntryRow[]`), `statuses` (`LookupItem[]`)
  - computed `canSave`, `dirty`, `presentCount`, `total`
  - methods `load(date?: string)`, `setDate(date)`, `setStatus(swimmerId, statusId)`, `setNote(swimmerId, note)`, `markAllPresent()`, `save()`
  - helpers `statusCode(statusId)`, `rateBand(pct)` → `'red' | 'amber' | 'green' | 'muted'`

`EntryRow` = the domain `SessionRow` but with a non-null `statusId` (defaulted to Present when the server returned null).

- [ ] **Step 1: Write the failing viewmodel spec** (`attendance.viewmodel.spec.ts`)

```typescript
import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { AttendanceEntryViewModel } from '@features/attendance/presentation/pages/attendance/attendance.viewmodel';
import { LoadAttendanceSessionUseCase } from '@features/attendance/domain/usecases/load-attendance-session.use-case';
import { SaveAttendanceSessionUseCase } from '@features/attendance/domain/usecases/save-attendance-session.use-case';
import { LoadAttendanceStatusesUseCase } from '@features/reference/domain/usecases/load-attendance-statuses.use-case';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';
import { LanguageStore } from '@core/i18n/language.store';

const STATUSES = [
  { id: 'st1', code: 'present', nameEn: 'Present', nameAr: 'حاضر' },
  { id: 'st2', code: 'late',    nameEn: 'Late',    nameAr: 'متأخر' },
  { id: 'st3', code: 'absent',  nameEn: 'Absent',  nameAr: 'غائب' },
  { id: 'st4', code: 'excused', nameEn: 'Excused', nameAr: 'معذور' },
];

// Two swimmers: A already Present (has record); B has no record.
const SESSION = {
  date: '2026-09-23',
  rows: [
    { swimmerId: 'a', uid: 'SW-A', nameEn: 'Alice', nameAr: null, clubNameEn: null, clubNameAr: null,
      genderCode: 'female', statusId: 'st1', coachNote: 'ok', monthRatePct: 90, hasRecord: true },
    { swimmerId: 'b', uid: 'SW-B', nameEn: 'Bob', nameAr: null, clubNameEn: null, clubNameAr: null,
      genderCode: 'male', statusId: null, coachNote: null, monthRatePct: null, hasRecord: false },
  ],
};

function build(overrides: { load?: unknown; save?: unknown; role?: 'head_coach' | 'captain' | 'swimmer' | null } = {}) {
  const loadUc = { run: jest.fn().mockResolvedValue(overrides.load ?? { ok: true, data: SESSION }) };
  const saveUc = { run: jest.fn().mockResolvedValue(overrides.save ?? { ok: true, data: SESSION }) };
  const statusesUc = { run: jest.fn().mockResolvedValue({ ok: true, data: STATUSES }) };
  const notify = { success: jest.fn(), error: jest.fn() };
  const i18n = { t: (k: string) => k };
  const session = { role: signal<'head_coach' | 'captain' | 'swimmer' | null>(overrides.role ?? 'head_coach') };
  const language = { lang: signal<'en' | 'ar'>('en') };

  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      AttendanceEntryViewModel,
      { provide: LoadAttendanceSessionUseCase, useValue: loadUc },
      { provide: SaveAttendanceSessionUseCase, useValue: saveUc },
      { provide: LoadAttendanceStatusesUseCase, useValue: statusesUc },
      { provide: AuthSessionStore, useValue: session },
      { provide: NotificationService, useValue: notify },
      { provide: TranslateService, useValue: i18n },
      { provide: LanguageStore, useValue: language },
    ],
  });
  return { vm: TestBed.inject(AttendanceEntryViewModel), loadUc, saveUc, statusesUc, notify, session };
}

describe('AttendanceEntryViewModel', () => {
  it('loads a session and defaults unrecorded rows to Present', async () => {
    const { vm, loadUc } = build();
    await vm.load('2026-09-23');

    expect(loadUc.run).toHaveBeenCalledWith('2026-09-23');
    const rows = vm.rows();
    expect(rows).toHaveLength(2);
    expect(rows.find((r) => r.swimmerId === 'a')!.statusId).toBe('st1');
    // B had null → defaulted to Present.
    expect(rows.find((r) => r.swimmerId === 'b')!.statusId).toBe('st1');
  });

  it('is dirty right after load because B has no saved record (fresh default)', async () => {
    const { vm } = build();
    await vm.load('2026-09-23');
    expect(vm.dirty()).toBe(true);
  });

  it('markAllPresent sets every row to the Present status', async () => {
    const { vm } = build();
    await vm.load('2026-09-23');
    vm.setStatus('a', 'st3'); // Absent
    vm.markAllPresent();
    expect(vm.rows().every((r) => r.statusId === 'st1')).toBe(true);
  });

  it('presentCount counts present and late', async () => {
    const { vm } = build();
    await vm.load('2026-09-23');
    vm.setStatus('a', 'st2'); // Late
    vm.setStatus('b', 'st3'); // Absent
    expect(vm.presentCount()).toBe(1); // only the late one counts as "in"
    expect(vm.total()).toBe(2);
  });

  it('save posts every row and refreshes from the response', async () => {
    const { vm, saveUc } = build();
    await vm.load('2026-09-23');
    await vm.save();

    expect(saveUc.run).toHaveBeenCalledTimes(1);
    const payload = saveUc.run.mock.calls[0][0];
    expect(payload.date).toBe('2026-09-23');
    expect(payload.entries).toHaveLength(2); // all rows sent, incl. defaulted B
    expect(payload.entries.every((e: { statusId: string }) => typeof e.statusId === 'string')).toBe(true);
  });

  it('canSave is false for a non-coach role', async () => {
    const { vm } = build({ role: 'swimmer' });
    await vm.load('2026-09-23');
    expect(vm.canSave()).toBe(false);
  });

  it('rateBand maps thresholds', () => {
    const { vm } = build();
    expect(vm.rateBand(null)).toBe('muted');
    expect(vm.rateBand(65)).toBe('red');
    expect(vm.rateBand(80)).toBe('amber');
    expect(vm.rateBand(90)).toBe('green');
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd frontend && npx jest attendance.viewmodel`
Expected: FAIL — `AttendanceEntryViewModel` does not exist.

- [ ] **Step 3: Implement the viewmodel** (`attendance.viewmodel.ts`)

```typescript
import { Injectable, computed, inject, signal } from '@angular/core';
import { LoadAttendanceSessionUseCase } from '@features/attendance/domain/usecases/load-attendance-session.use-case';
import { SaveAttendanceSessionUseCase } from '@features/attendance/domain/usecases/save-attendance-session.use-case';
import { LoadAttendanceStatusesUseCase } from '@features/reference/domain/usecases/load-attendance-statuses.use-case';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';
import { LanguageStore } from '@core/i18n/language.store';
import { LookupItem } from '@features/reference/domain/model/reference';
import { AttendanceSession, SessionRow } from '@features/attendance/domain/model/attendance-session';

export interface EntryRow extends Omit<SessionRow, 'statusId'> {
  statusId: string; // never null — defaulted to Present on load
}

function todayIso(): string {
  const d = new Date();
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
}

@Injectable()
export class AttendanceEntryViewModel {
  private readonly loadSession = inject(LoadAttendanceSessionUseCase);
  private readonly saveSession = inject(SaveAttendanceSessionUseCase);
  private readonly loadStatuses = inject(LoadAttendanceStatusesUseCase);
  private readonly session = inject(AuthSessionStore);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);
  private readonly language = inject(LanguageStore);

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal(false);
  readonly selectedDate = signal<string>(todayIso());
  readonly statuses = signal<LookupItem[]>([]);
  readonly rows = signal<EntryRow[]>([]);

  // swimmerId → the server's saved state, for dirty comparison
  private original = new Map<string, { statusId: string | null; coachNote: string | null }>();

  readonly total = computed(() => this.rows().length);
  readonly presentCount = computed(() =>
    this.rows().filter((r) => { const c = this.statusCode(r.statusId); return c === 'present' || c === 'late'; }).length);

  readonly canSave = computed(() => {
    const role = this.session.role();
    return role === 'head_coach' || role === 'captain';
  });

  readonly dirty = computed(() => this.rows().some((r) => this.isRowDirty(r)));

  private presentId(): string | null {
    return this.statuses().find((s) => s.code === 'present')?.id ?? null;
  }

  statusCode(statusId: string | null): string {
    return this.statuses().find((s) => s.id === statusId)?.code ?? '';
  }

  rateBand(pct: number | null): 'red' | 'amber' | 'green' | 'muted' {
    if (pct === null || pct === undefined) return 'muted';
    if (pct < 70) return 'red';
    if (pct < 85) return 'amber';
    return 'green';
  }

  private isRowDirty(r: EntryRow): boolean {
    if (!r.hasRecord) return true; // fresh default — will be created on save
    const o = this.original.get(r.swimmerId);
    return r.statusId !== o?.statusId || (r.coachNote ?? '') !== (o?.coachNote ?? '');
  }

  setDate(date: string): void {
    this.selectedDate.set(date);
    void this.load(date);
  }

  setStatus(swimmerId: string, statusId: string): void {
    this.rows.update((rows) => rows.map((r) => (r.swimmerId === swimmerId ? { ...r, statusId } : r)));
  }

  setNote(swimmerId: string, coachNote: string): void {
    this.rows.update((rows) => rows.map((r) => (r.swimmerId === swimmerId ? { ...r, coachNote } : r)));
  }

  markAllPresent(): void {
    const pid = this.presentId();
    if (!pid) return;
    this.rows.update((rows) => rows.map((r) => ({ ...r, statusId: pid })));
  }

  async load(date?: string): Promise<void> {
    const d = date ?? this.selectedDate();
    this.selectedDate.set(d);
    this.loading.set(true);
    this.error.set(false);

    if (this.statuses().length === 0) {
      const s = await this.loadStatuses.run();
      if (s.ok) this.statuses.set(s.data);
    }

    const res = await this.loadSession.run(d);
    if (res.ok) {
      this.applySession(res.data);
    } else {
      this.error.set(true);
      this.rows.set([]);
    }
    this.loading.set(false);
  }

  async save(): Promise<void> {
    if (!this.canSave() || this.saving()) return;
    this.saving.set(true);
    const payload = {
      date: this.selectedDate(),
      entries: this.rows().map((r) => ({ swimmerId: r.swimmerId, statusId: r.statusId, coachNote: r.coachNote })),
    };
    const res = await this.saveSession.run(payload);
    if (res.ok) {
      this.applySession(res.data);
      this.notify.success(this.i18n.t('attendanceEntry.saved'));
    } else {
      this.notify.error(this.i18n.t('attendanceEntry.saveFailed'));
    }
    this.saving.set(false);
  }

  private applySession(session: AttendanceSession): void {
    const pid = this.presentId();
    this.original = new Map(session.rows.map((r) => [r.swimmerId, { statusId: r.statusId, coachNote: r.coachNote }]));
    this.rows.set(session.rows.map((r) => ({ ...r, statusId: r.statusId ?? pid ?? '' })));
  }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `cd frontend && npx jest attendance.viewmodel`
Expected: PASS

> If `LanguageStore` / `NotificationService` / `TranslateService` import paths differ, copy the exact import specifiers from `swimmer-profile.viewmodel.ts` (it imports all three).

- [ ] **Step 5: Commit** *(only when the user authorizes committing)*

```bash
git add frontend/src/app/features/attendance
git commit -m "feat(attendance-fe): entry page viewmodel (load, default-present, save, role gate)"
```

---

## Task 9: Frontend — page, template, route wiring, i18n

**Files:**
- Modify: `frontend/src/app/features/attendance/presentation/pages/attendance/attendance.page.ts`
- Modify: `frontend/src/app/features/attendance/presentation/pages/attendance/attendance.page.html`
- Modify: `frontend/src/app/features/attendance/index.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`

**Interfaces:**
- Consumes: `AttendanceEntryViewModel` (Task 8), `ATTENDANCE_PROVIDERS` (Task 6).

- [ ] **Step 1: Rebuild the page component** (`attendance.page.ts`)

```typescript
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@core/i18n';
import { LanguageStore } from '@core/i18n/language.store';
import { AttendanceEntryViewModel, EntryRow } from './attendance.viewmodel';

@Component({
  selector: 'app-attendance-page',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './attendance.page.html',
})
export class AttendancePage implements OnInit {
  protected readonly vm = inject(AttendanceEntryViewModel);
  private readonly language = inject(LanguageStore);

  ngOnInit(): void { void this.vm.load(); }

  displayName(r: EntryRow): string {
    return this.language.lang() === 'ar' ? (r.nameAr ?? r.nameEn) : r.nameEn;
  }

  initials(r: EntryRow): string {
    const parts = r.nameEn.trim().split(/\s+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase();
  }
}
```

- [ ] **Step 2: Rebuild the template** (`attendance.page.html`) — header (date picker + mark-all + counter), roster rows (status toggle + note), save bar. Use the read-only tab's status→color classes for consistency. Reference: `Desktop/mcp/2.png`, `Desktop/mcp/3.png`, and `CoachAttendanceView` in the React prototype.

```html
<div class="mx-auto max-w-5xl p-6 space-y-6">
  <header class="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
    <div>
      <h1 class="font-heading text-3xl text-ink">{{ 'shell.nav.attendance' | translate }}</h1>
      <p class="text-sm text-muted">{{ 'attendanceEntry.subtitle' | translate }}</p>
    </div>
    <div class="flex flex-wrap items-center gap-3">
      <input type="date" [ngModel]="vm.selectedDate()" (ngModelChange)="vm.setDate($event)"
             class="h-10 rounded-lg border border-border bg-card px-3 text-sm" />
      <button type="button" (click)="vm.markAllPresent()"
              class="h-10 rounded-lg border border-border px-3 text-sm font-bold">
        {{ 'attendanceEntry.markAllPresent' | translate }}
      </button>
      <span class="rounded-lg bg-green-500/10 px-3 py-1.5 text-sm font-bold text-green-600">
        {{ vm.presentCount() }}/{{ vm.total() }}
      </span>
    </div>
  </header>

  @if (vm.loading()) {
    <p class="text-sm text-muted">{{ 'common.loading' | translate }}</p>
  } @else if (vm.error()) {
    <p class="text-sm text-danger">{{ 'attendanceEntry.loadFailed' | translate }}</p>
  } @else if (vm.total() === 0) {
    <p class="text-sm text-muted">{{ 'attendanceEntry.empty' | translate }}</p>
  } @else {
    <div class="space-y-3">
      @for (row of vm.rows(); track row.swimmerId) {
        <div class="rounded-xl border border-border bg-card p-4">
          <div class="flex items-center gap-3">
            <div class="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10 text-sm font-bold text-primary">
              {{ initials(row) }}
            </div>
            <div class="min-w-0 flex-1">
              <p class="truncate font-bold text-ink">{{ displayName(row) }}</p>
              @if (row.monthRatePct !== null) {
                <p class="text-xs font-bold"
                   [class.text-red-500]="vm.rateBand(row.monthRatePct) === 'red'"
                   [class.text-amber-500]="vm.rateBand(row.monthRatePct) === 'amber'"
                   [class.text-green-500]="vm.rateBand(row.monthRatePct) === 'green'">
                  {{ row.monthRatePct }}% · {{ 'attendanceEntry.thisMonth' | translate }}
                </p>
              } @else {
                <p class="text-xs text-muted">— {{ 'attendanceEntry.thisMonth' | translate }}</p>
              }
            </div>
          </div>

          <div class="mt-3 flex flex-wrap gap-1.5">
            @for (st of vm.statuses(); track st.id) {
              <button type="button" (click)="vm.setStatus(row.swimmerId, st.id)"
                      class="h-8 rounded-md border px-3 text-xs font-medium"
                      [class.border-primary]="row.statusId === st.id"
                      [class.bg-primary]="row.statusId === st.id"
                      [class.text-white]="row.statusId === st.id">
                {{ ('attendanceEntry.status.' + st.code) | translate }}
              </button>
            }
          </div>

          <textarea rows="2" [ngModel]="row.coachNote ?? ''" (ngModelChange)="vm.setNote(row.swimmerId, $event)"
                    [placeholder]="'attendanceEntry.notePlaceholder' | translate"
                    class="mt-3 w-full rounded-lg border border-border bg-background px-3 py-2 text-sm"></textarea>
        </div>
      }
    </div>

    <div class="flex items-center justify-end gap-3 border-t border-border pt-4">
      @if (!vm.canSave()) {
        <span class="text-xs text-muted">{{ 'attendanceEntry.notPermitted' | translate }}</span>
      }
      <button type="button" (click)="vm.save()" [disabled]="!vm.canSave() || !vm.dirty() || vm.saving()"
              class="h-10 rounded-md bg-primary px-4 text-sm font-medium text-white disabled:opacity-40">
        {{ 'attendanceEntry.save' | translate }}
      </button>
    </div>
  }
</div>
```

> Tailwind class names (`text-ink`, `text-muted`, `border-border`, `bg-card`, `text-primary`, `text-danger`) must match the tokens the app already uses. Copy them from `swimmers.page.html` / the read-only attendance tab template if any differ.

- [ ] **Step 3: Export the viewmodel** (`index.ts`)

```typescript
export { AttendancePage } from './presentation/pages/attendance/attendance.page';
export { AttendanceEntryViewModel } from './presentation/pages/attendance/attendance.viewmodel';
```

- [ ] **Step 4: Wire the route** (`app.routes.ts`) — replace the `attendance` route line

```typescript
import { AttendanceEntryViewModel } from '@features/attendance';
import { ATTENDANCE_PROVIDERS } from '@features/attendance/data/attendance.providers';
// ...
{
  path: 'attendance',
  canActivate: [firstLoginGuard],
  loadComponent: () => import('@features/attendance').then((m) => m.AttendancePage),
  providers: [AttendanceEntryViewModel, ...ATTENDANCE_PROVIDERS],
},
```

- [ ] **Step 5: Add i18n keys** — insert an `attendanceEntry` block in **both** `en.json` and `ar.json` (place it near other top-level feature blocks; match the file's existing nesting/indent).

`en.json`:
```json
"attendanceEntry": {
  "subtitle": "Log a training session's attendance.",
  "markAllPresent": "Mark all present",
  "thisMonth": "this month",
  "notePlaceholder": "General feedback (optional)…",
  "save": "Save Session",
  "saved": "Attendance saved",
  "saveFailed": "Could not save attendance",
  "loadFailed": "Could not load attendance",
  "empty": "No swimmers to show.",
  "notPermitted": "Only a coach can save attendance.",
  "status": { "present": "Present", "late": "Late", "absent": "Absent", "excused": "Excused" }
}
```

`ar.json`:
```json
"attendanceEntry": {
  "subtitle": "سجّل حضور جلسة تدريب.",
  "markAllPresent": "تحديد الكل حاضر",
  "thisMonth": "هذا الشهر",
  "notePlaceholder": "تقييم عام (اختياري)…",
  "save": "حفظ الجلسة",
  "saved": "تم حفظ الحضور",
  "saveFailed": "تعذّر حفظ الحضور",
  "loadFailed": "تعذّر تحميل الحضور",
  "empty": "لا يوجد سبّاحون للعرض.",
  "notPermitted": "يمكن للكابتن فقط حفظ الحضور.",
  "status": { "present": "حاضر", "late": "متأخر", "absent": "غائب", "excused": "بعذر" }
}
```

> Confirm `common.loading` exists in the i18n files; if not, add it or reuse an existing loading key. Confirm `shell.nav.attendance` exists (it does).

- [ ] **Step 6: Run the frontend suite + build**

Run: `cd frontend && npx jest attendance && npm run build`
Expected: PASS (all attendance specs) and a clean production build (no template/type errors).

- [ ] **Step 7: Manual smoke (optional but recommended)**

Start the API + frontend, sign in as a head coach, open `/attendance`, pick a date, toggle statuses, "Mark all present", add a note, Save; reload the date and confirm it persisted; switch UI language and confirm labels + note language behavior.

- [ ] **Step 8: Commit** *(only when the user authorizes committing)*

```bash
git add frontend/src/app/features/attendance frontend/src/app/app.routes.ts frontend/src/app/core/i18n
git commit -m "feat(attendance-fe): coach attendance entry page + route + i18n"
```

---

## Self-Review

**1. Spec coverage**
- Approach A session endpoints → Task 4 (GET `session`, PUT `session`). ✓
- Composition at API layer (roster + records + rates) → Task 4 `BuildSessionAsync`. ✓
- Upsert on `(SwimmerId, SessionDate)` → Task 2 `UpsertSessionAsync` + Task 1 `Update`. ✓
- Month rate `(present+late)/(present+late+absent)`, excused excluded, null when denom 0 → Task 4 rate calc; service buckets in Task 3. ✓
- Default-Present client-side; server returns null/`hasRecord:false` → Task 4 rows; Task 8 `applySession`. ✓
- Feedback optional; note by UI language → Task 4 (save routes note to En/Ar by `AppLanguage.Current`; read resolves by language). ✓
- Save auth `head_coach,captain` → Task 4 `[Authorize(Roles)]` + reflection test; frontend `canSave` → Task 8. ✓
- Date picker default today → Task 8 `todayIso`/`selectedDate`, Task 9 `<input type="date">`. ✓
- Mark all present, present+late counter → Task 8 `markAllPresent`/`presentCount`, Task 9 template. ✓
- Validation (duplicate swimmer, invalid status) → Task 4 tests + guards. ✓
- No migration / no DI change → Global Constraints; nothing adds either. ✓
- Frontend Jest tests → Tasks 5–9 specs. ✓

**2. Placeholder scan** — no TBD/TODO; every code step has concrete code. The two "confirm import path / Tailwind token" notes point at exact reference files, not vague instructions. ✓

**3. Type consistency**
- Backend record names used identically across tasks: `SwimmerSessionRowDto`, `AttendanceSessionDto`, `SaveSessionRequest`, `SaveSessionEntryRequest`, `SaveSessionEntry`. ✓
- Service signatures match between Task 3 (definition) and Task 4 (usage): `ListByDateAsync`, `GetMonthStatusCountsAsync(int,int,…)` returning `IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid,int>>`, `SaveSessionAsync(DateOnly, IReadOnlyList<SaveSessionEntry>, Guid, …)`. ✓
- Controller constructor arity (4 args) matches the Task 4 test's `Build(...)`. ✓
- Frontend: `AttendanceSessionItemDtoRs` (repo) → `res.data` is `AttendanceSessionDtoRs` (use-cases) → `toAttendanceSession` → `AttendanceSession`/`SessionRow` (viewmodel `applySession`). ✓
- `EntryRow.statusId: string` (non-null) vs `SessionRow.statusId: string | null` — reconciled in `applySession` via `?? pid ?? ''`. ✓

**Divergence flagged for the user (not a plan defect):** the existing read-only Attendance tab computes its month rate as `present / all-recorded (incl. excused)`, whereas this entry page uses the approved `(present+late)/(present+late+absent)` (excused excluded). The two rate figures can differ for the same swimmer/month. Building to the approved spec; raise with the user if a single shared definition is preferred.
