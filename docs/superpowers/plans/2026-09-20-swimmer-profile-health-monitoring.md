# Swimmer Profile — Health Monitoring tab — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a read + edit + delete **Health Monitoring** tab to the swimmer profile page (`swimmers/:id`) that lists every `health.health_reading` for the swimmer, each measured against its test's normal bounds.

**Architecture:** Extend the existing flat `HealthReadingsController` (currently POST-only) with `GET ?swimmerId`, `PUT /{id}` (value-only), `DELETE /{id}`; the list/PUT rows are enriched with the test's name/unit/bounds + a server-derived binary status (`normal`/`out`) by looking up the (small, head-coach-managed) test catalog via `IMedicalTestRepository.ListAsync()`. Frontend extends the existing `health-readings` slice (list/update/delete) and adds a Health Monitoring section to the swimmer-profile viewmodel/page, mirroring the committed Records tab. No DB migration.

**Tech Stack:** .NET 10 (C#, EF Core, xUnit + Moq, FluentValidation), Angular (standalone components, signals, Jest), Tailwind.

**Spec:** `docs/superpowers/specs/2026-09-20-swimmer-profile-health-monitoring-design.md`

## Global Constraints

- **NO GIT COMMITS until the user explicitly authorizes.** Each task ends with a "Checkpoint" step: run the build/tests and **stage** (`git add`) only — do **not** run `git commit`. Once the user says to commit, the suggested message is given in each checkpoint.
- **`dotnet build` / `dotnet test` require the running API to be stopped** (the dev server locks the module DLLs). Stop it before backend build/test steps.
- **No DB migration** — `health.health_reading` and `health.medical_test` already exist (applied to Aiven).
- **Edit is value-only** — `HealthReading.Value` is the only mutable field; `SwimmerId`, `MedicalTestId`, `ReadingDate`, `RecordedBy` are immutable on edit.
- **Status is binary + server-derived** — `value ∈ [lower, upper] ⇒ "normal"`, else `"out"`; UI maps `normal → In range` (green/`success`), `out → Watch` (amber/`warning`), anything else → neutral "Unknown". No third band.
- **Auth** — GET `[Authorize]` (any authenticated); POST/PUT/DELETE `head_coach,captain`. No ownership guard beyond "row exists".
- **Routes stay flat** on `HealthReadingsController`: `GET /api/health-readings?swimmerId={id}`, `PUT /api/health-readings/{id}`, `DELETE /api/health-readings/{id}`.
- **Backend base paths:**
  - Module: `backend/src/Modules/Health/`
  - `Domain` = `Kheprx.BaseBackend.Health.Domain`, `Infrastructure` = `…Health.Infrastructure`, `Application` = `…Health.Application`
  - Controllers: `backend/Kheprx.BaseBackend.Api/Controllers/`
  - Tests: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/` and `backend/tests/Kheprx.BaseBackend.Api.UnitTests/`
- **Frontend base paths:**
  - Health-readings slice: `frontend/src/app/features/health-readings/`
  - Swimmer-profile slice: `frontend/src/app/features/swimmer-profile/`
  - i18n: `frontend/src/app/core/i18n/{en,ar}.json`

---

## File Structure

**Backend — modify:**
- `Health.Domain/Entities/HealthReading.cs` — add `Update(decimal value)`
- `Health.Domain/Repositories/IHealthReadingRepository.cs` — add `ListBySwimmerAsync`, `GetTrackedAsync`, `Remove`
- `Health.Infrastructure/Repositories/HealthReadingRepository.cs` — implement the three
- `Health.Application/DTOs/HealthReadingDtos.cs` — add `UpdateHealthReadingRequest`, `HealthReadingListItemDto`
- `Health.Application/Resources/HealthReadingMessages.cs` — add `Listed/Updated/Deleted/NotFound/ValuePositive`
- `Health.Application/Validators/` — add `UpdateHealthReadingRequestValidator.cs`
- `Health.Application/Services/Interfaces/IHealthReadingService.cs` — add `ListBySwimmerAsync/UpdateAsync/DeleteAsync`
- `Health.Application/Services/HealthReadingService.cs` — implement the three + `ToListItem`
- `Api/Controllers/HealthReadingsController.cs` — add GET/PUT/DELETE, move role restriction onto write actions

**Backend — tests:**
- `Health.UnitTests/Entities/HealthReadingTests.cs` — extend (Update)
- `Health.UnitTests/Repositories/HealthReadingRepositoryTests.cs` — extend (list/get-tracked/remove)
- `Health.UnitTests/Services/HealthReadingServiceTests.cs` — extend (list enrich/status, update, delete)
- `Health.UnitTests/Validators/UpdateHealthReadingRequestValidatorTests.cs` — create
- `Api.UnitTests/HealthReadingsControllerTests.cs` — create (List/Update/Delete)

**Frontend — health-readings slice:**
- `data/dto/health-reading.dto.ts` — modify (row DTO, wrappers, update req, validators)
- `data/dto/health-reading-row.mapper.ts` — create
- `domain/model/health-reading-list-item.ts` — create
- `domain/repositories/health-reading.repository.ts` — modify (interface)
- `data/repositories/health-reading.repository.impl.ts` — modify (list/update/remove)
- `domain/usecases/list-health-readings.use-case.ts` — create
- `domain/usecases/update-health-reading.use-case.ts` — create
- `domain/usecases/delete-health-reading.use-case.ts` — create
- `testing/data/repositories/health-reading.repository.impl.spec.ts` — extend
- `testing/data/dto/health-reading-row.mapper.spec.ts` — create
- `testing/domain/usecases/health-reading-crud.use-cases.spec.ts` — create

**Frontend — swimmer-profile slice:**
- `presentation/pages/swimmer-profile/swimmer-profile.page.ts` — modify (enable tab + `testName` helper)
- `presentation/pages/swimmer-profile/swimmer-profile.viewmodel.ts` — modify (state + methods)
- `presentation/pages/swimmer-profile/swimmer-profile.page.html` — modify (tab section)
- `testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts` — extend
- `core/i18n/en.json`, `core/i18n/ar.json` — modify

---

## Task 1: Backend — domain (`HealthReading.Update` + repository interface)

**Files:**
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Entities/HealthReading.cs`
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Repositories/IHealthReadingRepository.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Entities/HealthReadingTests.cs`

**Interfaces:**
- Produces: `HealthReading.Update(decimal value)` (mutates `Value` only); `IHealthReadingRepository.ListBySwimmerAsync(Guid, CancellationToken) → Task<IReadOnlyList<HealthReading>>`, `GetTrackedAsync(Guid, CancellationToken) → Task<HealthReading?>`, `void Remove(HealthReading)`.

- [ ] **Step 1: Write the failing test** — append to `HealthReadingTests.cs`:

```csharp
    [Fact]
    public void Update_changes_value_only_and_preserves_other_fields()
    {
        var swimmerId = Guid.NewGuid();
        var testId = Guid.NewGuid();
        var recordedBy = Guid.NewGuid();
        var r = new HealthReading(swimmerId, testId, 95.5m, recordedBy);
        var originalDate = r.ReadingDate;

        r.Update(120m);

        Assert.Equal(120m, r.Value);
        Assert.Equal(swimmerId, r.SwimmerId);
        Assert.Equal(testId, r.MedicalTestId);
        Assert.Equal(recordedBy, r.RecordedBy);
        Assert.Equal(originalDate, r.ReadingDate);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~HealthReadingTests`
Expected: FAIL — `HealthReading` has no `Update` method (compile error).

- [ ] **Step 3: Add `Update` to the entity** — in `HealthReading.cs`, after the constructor:

```csharp
    public void Update(decimal value) => Value = value;
```

- [ ] **Step 4: Extend the repository interface** — replace the body of `IHealthReadingRepository.cs`:

```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;

namespace Kheprx.BaseBackend.Health.Domain.Repositories;

public interface IHealthReadingRepository
{
    Task AddAsync(HealthReading reading, CancellationToken ct = default);
    Task<IReadOnlyList<HealthReading>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default);
    Task<HealthReading?> GetTrackedAsync(Guid id, CancellationToken ct = default);
    void Remove(HealthReading reading);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~HealthReadingTests`
Expected: PASS. (The repository interface now has unimplemented members — `HealthReadingRepository` won't compile until Task 2, so the wider solution build fails; that's expected between tasks. The `HealthReadingTests` still compile/run because they don't touch the repo impl. If the test project fails to build due to the impl, do Task 2 Step 3 first, then re-run.)

- [ ] **Step 6: Checkpoint (stage only — DO NOT COMMIT)**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Entities/HealthReading.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Repositories/IHealthReadingRepository.cs \
        backend/tests/Kheprx.BaseBackend.Health.UnitTests/Entities/HealthReadingTests.cs
```
Suggested message when authorized: `feat(health): HealthReading.Update + repository read/edit/delete interface`

---

## Task 2: Backend — infrastructure (repository impl)

**Files:**
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Repositories/HealthReadingRepository.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Repositories/HealthReadingRepositoryTests.cs`

**Interfaces:**
- Consumes: `IHealthReadingRepository` (Task 1).
- Produces: concrete `HealthReadingRepository` implementing all interface members over `HealthDbContext.HealthReadings`.

- [ ] **Step 1: Write the failing tests** — append to `HealthReadingRepositoryTests.cs`:

```csharp
    [Fact]
    public async Task ListBySwimmer_returns_only_that_swimmer_newest_first()
    {
        await using var db = NewDb();
        var repo = new HealthReadingRepository(db);
        var swimmer = Guid.NewGuid();
        var other = Guid.NewGuid();
        var test = Guid.NewGuid();
        var older = new HealthReading(swimmer, test, 10m, Guid.NewGuid());
        var newer = new HealthReading(swimmer, test, 20m, Guid.NewGuid());
        typeof(HealthReading).GetProperty("ReadingDate")!.SetValue(older, DateTime.UtcNow.AddDays(-2));
        typeof(HealthReading).GetProperty("ReadingDate")!.SetValue(newer, DateTime.UtcNow);
        await repo.AddAsync(older);
        await repo.AddAsync(newer);
        await repo.AddAsync(new HealthReading(other, test, 30m, Guid.NewGuid()));
        await repo.SaveChangesAsync();

        var list = await repo.ListBySwimmerAsync(swimmer);

        Assert.Equal(2, list.Count);
        Assert.Equal(20m, list[0].Value); // newest first
        Assert.Equal(10m, list[1].Value);
    }

    [Fact]
    public async Task GetTracked_then_Remove_deletes_the_reading()
    {
        await using var db = NewDb();
        var repo = new HealthReadingRepository(db);
        var reading = new HealthReading(Guid.NewGuid(), Guid.NewGuid(), 50m, Guid.NewGuid());
        await repo.AddAsync(reading);
        await repo.SaveChangesAsync();

        var tracked = await repo.GetTrackedAsync(reading.Id);
        Assert.NotNull(tracked);
        repo.Remove(tracked!);
        await repo.SaveChangesAsync();

        Assert.Empty(await db.HealthReadings.AsNoTracking().ToListAsync());
    }
```

> Note: `HealthReading.ReadingDate` has a private setter, so the test uses reflection to force distinct dates (the constructor stamps `DateTime.UtcNow`). Add `using System.Reflection;` is not required — `typeof(...).GetProperty(...)` needs `using System;` (already implicitly available via global usings).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~HealthReadingRepositoryTests`
Expected: FAIL — `ListBySwimmerAsync`/`GetTrackedAsync`/`Remove` not implemented.

- [ ] **Step 3: Implement the methods** — replace the body of `HealthReadingRepository.cs`:

```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Health.Infrastructure.Repositories;

internal sealed class HealthReadingRepository : IHealthReadingRepository
{
    private readonly HealthDbContext _db;
    public HealthReadingRepository(HealthDbContext db) => _db = db;

    public async Task AddAsync(HealthReading reading, CancellationToken ct = default)
        => await _db.HealthReadings.AddAsync(reading, ct);

    public async Task<IReadOnlyList<HealthReading>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default)
        => await _db.HealthReadings.AsNoTracking()
              .Where(r => r.SwimmerId == swimmerId)
              .OrderByDescending(r => r.ReadingDate)
              .ThenByDescending(r => r.Id)
              .ToListAsync(ct);

    public Task<HealthReading?> GetTrackedAsync(Guid id, CancellationToken ct = default)
        => _db.HealthReadings.FirstOrDefaultAsync(r => r.Id == id, ct);

    public void Remove(HealthReading reading) => _db.HealthReadings.Remove(reading);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~HealthReadingRepositoryTests`
Expected: PASS (all three, including the pre-existing `Add_then_Save_persists_reading`).

- [ ] **Step 5: Checkpoint (stage only — DO NOT COMMIT)**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Repositories/HealthReadingRepository.cs \
        backend/tests/Kheprx.BaseBackend.Health.UnitTests/Repositories/HealthReadingRepositoryTests.cs
```
Suggested message: `feat(health): HealthReadingRepository list/get-tracked/remove`

---

## Task 3: Backend — application DTOs, messages, validator

**Files:**
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/DTOs/HealthReadingDtos.cs`
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Resources/HealthReadingMessages.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Validators/UpdateHealthReadingRequestValidator.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Validators/UpdateHealthReadingRequestValidatorTests.cs`

**Interfaces:**
- Produces: `UpdateHealthReadingRequest(decimal Value)`; `HealthReadingListItemDto(Guid Id, Guid MedicalTestId, string TestNameEn, string TestNameAr, string Unit, decimal Value, decimal LowerBound, decimal UpperBound, DateTime ReadingDate, string Status)`; `HealthReadingMessages.Success.{Listed,Updated,Deleted}`, `HealthReadingMessages.Errors.{NotFound,ValuePositive}`; `UpdateHealthReadingRequestValidator`.

- [ ] **Step 1: Add the DTOs** — append to `HealthReadingDtos.cs`:

```csharp

/// <summary>Payload to edit a health reading (PUT /api/health-readings/{id}). Value only.</summary>
public sealed record UpdateHealthReadingRequest(decimal Value);

/// <summary>An enriched health reading row (test name/unit/bounds + derived status).</summary>
public sealed record HealthReadingListItemDto(
    Guid Id,
    Guid MedicalTestId,
    string TestNameEn,
    string TestNameAr,
    string Unit,
    decimal Value,
    decimal LowerBound,
    decimal UpperBound,
    DateTime ReadingDate,
    string Status);
```

- [ ] **Step 2: Add the messages** — in `HealthReadingMessages.cs`, add to `Success`:

```csharp
        public static string Listed(string lang) => lang switch { "ar" => "قراءات السبّاح", _ => "Swimmer readings" };
        public static string Updated(string lang) => lang switch { "ar" => "تم تحديث القراءة", _ => "Reading updated" };
        public static string Deleted(string lang) => lang switch { "ar" => "تم حذف القراءة", _ => "Reading deleted" };
```

  and to `Errors`:

```csharp
        public static string NotFound(string lang) => lang switch { "ar" => "القراءة غير موجودة", _ => "Reading not found" };
        public static string ValuePositive(string lang) => lang switch { "ar" => "يجب أن تكون القيمة أكبر من صفر", _ => "Value must be greater than zero" };
```

- [ ] **Step 3: Write the failing validator test** — create `UpdateHealthReadingRequestValidatorTests.cs`:

```csharp
using FluentValidation.TestHelper;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Validators;

public class UpdateHealthReadingRequestValidatorTests
{
    private readonly UpdateHealthReadingRequestValidator _validator = new();

    [Fact]
    public void Passes_for_a_positive_value()
        => _validator.TestValidate(new UpdateHealthReadingRequest(95m)).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Fails_for_zero_or_negative_value(decimal value)
        => _validator.TestValidate(new UpdateHealthReadingRequest(value)).ShouldHaveValidationErrorFor(x => x.Value);
}
```

> Confirm `FluentValidation.TestHelper` is the helper used by `CreateHealthReadingRequestValidatorTests.cs`; if that file uses a different assertion style, match it.

- [ ] **Step 4: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~UpdateHealthReadingRequestValidatorTests`
Expected: FAIL — `UpdateHealthReadingRequestValidator` does not exist (compile error).

- [ ] **Step 5: Create the validator** — `UpdateHealthReadingRequestValidator.cs`:

```csharp
using FluentValidation;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Health.Application.Validators;

public sealed class UpdateHealthReadingRequestValidator : AbstractValidator<UpdateHealthReadingRequest>
{
    public UpdateHealthReadingRequestValidator()
    {
        RuleFor(x => x.Value)
            .GreaterThan(0m).WithMessage(_ => HealthReadingMessages.Errors.ValuePositive(AppLanguage.Current));
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~UpdateHealthReadingRequestValidatorTests`
Expected: PASS.

- [ ] **Step 7: Checkpoint (stage only — DO NOT COMMIT)**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/DTOs/HealthReadingDtos.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Resources/HealthReadingMessages.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Validators/UpdateHealthReadingRequestValidator.cs \
        backend/tests/Kheprx.BaseBackend.Health.UnitTests/Validators/UpdateHealthReadingRequestValidatorTests.cs
```
Suggested message: `feat(health): health-reading update/list DTOs, messages, validator`

---

## Task 4: Backend — service (list enrich + status, update, delete)

**Files:**
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/Interfaces/IHealthReadingService.cs`
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/HealthReadingService.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Services/HealthReadingServiceTests.cs`

**Interfaces:**
- Consumes: `IHealthReadingRepository` (Task 1/2), `IMedicalTestRepository.ListAsync()`/`GetByIdAsync()`, DTOs (Task 3).
- Produces: `IHealthReadingService.ListBySwimmerAsync(Guid, CancellationToken) → Task<IReadOnlyList<HealthReadingListItemDto>>`, `UpdateAsync(Guid id, UpdateHealthReadingRequest, CancellationToken) → Task<HealthReadingListItemDto?>` (null ⇒ 404), `DeleteAsync(Guid id, CancellationToken) → Task<bool>` (false ⇒ 404).

- [ ] **Step 1: Write the failing tests** — append to `HealthReadingServiceTests.cs`:

```csharp
    private static (HealthReadingService svc, Mock<IHealthReadingRepository> readings, Mock<IMedicalTestRepository> tests) BuildFull(MedicalTest test)
    {
        var tests = new Mock<IMedicalTestRepository>();
        tests.Setup(t => t.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { test });
        tests.Setup(t => t.GetByIdAsync(test.Id, It.IsAny<CancellationToken>())).ReturnsAsync(test);
        var readings = new Mock<IHealthReadingRepository>();
        return (new HealthReadingService(readings.Object, tests.Object), readings, tests);
    }

    [Fact]
    public async Task List_enriches_rows_with_test_details_and_derives_status()
    {
        var test = Glucose(); // 70..110, unit mg/dL
        var (svc, readings, _) = BuildFull(test);
        var swimmer = Guid.NewGuid();
        var inside = new HealthReading(swimmer, test.Id, 90m, Guid.NewGuid());
        var low = new HealthReading(swimmer, test.Id, 40m, Guid.NewGuid());
        readings.Setup(r => r.ListBySwimmerAsync(swimmer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { inside, low });

        var list = await svc.ListBySwimmerAsync(swimmer);

        Assert.Equal(2, list.Count);
        Assert.Equal("Glucose", list[0].TestNameEn);
        Assert.Equal("mg/dL", list[0].Unit);
        Assert.Equal(70m, list[0].LowerBound);
        Assert.Equal(110m, list[0].UpperBound);
        Assert.Equal("normal", list[0].Status);
        Assert.Equal("out", list[1].Status);
    }

    [Fact]
    public async Task List_falls_back_when_test_missing_from_catalog()
    {
        var test = Glucose();
        var tests = new Mock<IMedicalTestRepository>();
        tests.Setup(t => t.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<MedicalTest>());
        var readings = new Mock<IHealthReadingRepository>();
        var swimmer = Guid.NewGuid();
        readings.Setup(r => r.ListBySwimmerAsync(swimmer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new HealthReading(swimmer, Guid.NewGuid(), 90m, Guid.NewGuid()) });
        var svc = new HealthReadingService(readings.Object, tests.Object);

        var list = await svc.ListBySwimmerAsync(swimmer);

        Assert.Single(list);
        Assert.Equal("unknown", list[0].Status);
        Assert.Equal(string.Empty, list[0].Unit);
    }

    [Fact]
    public async Task Update_changes_value_rederives_status_and_returns_row()
    {
        var test = Glucose(); // 70..110
        var (svc, readings, _) = BuildFull(test);
        var reading = new HealthReading(Guid.NewGuid(), test.Id, 90m, Guid.NewGuid());
        readings.Setup(r => r.GetTrackedAsync(reading.Id, It.IsAny<CancellationToken>())).ReturnsAsync(reading);

        var dto = await svc.UpdateAsync(reading.Id, new UpdateHealthReadingRequest(40m));

        Assert.NotNull(dto);
        Assert.Equal(40m, dto!.Value);
        Assert.Equal("out", dto.Status);
        readings.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_returns_null_when_missing()
    {
        var (svc, readings, _) = BuildFull(Glucose());
        readings.Setup(r => r.GetTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((HealthReading?)null);

        Assert.Null(await svc.UpdateAsync(Guid.NewGuid(), new UpdateHealthReadingRequest(50m)));
    }

    [Fact]
    public async Task Delete_true_when_found_false_when_missing()
    {
        var (svc, readings, _) = BuildFull(Glucose());
        var reading = new HealthReading(Guid.NewGuid(), Guid.NewGuid(), 90m, Guid.NewGuid());
        readings.Setup(r => r.GetTrackedAsync(reading.Id, It.IsAny<CancellationToken>())).ReturnsAsync(reading);
        readings.Setup(r => r.GetTrackedAsync(It.Is<Guid>(g => g != reading.Id), It.IsAny<CancellationToken>())).ReturnsAsync((HealthReading?)null);

        Assert.True(await svc.DeleteAsync(reading.Id));
        Assert.False(await svc.DeleteAsync(Guid.NewGuid()));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~HealthReadingServiceTests`
Expected: FAIL — `ListBySwimmerAsync`/`UpdateAsync`/`DeleteAsync` not defined on the service.

- [ ] **Step 3: Extend the service interface** — replace `IHealthReadingService.cs`:

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;

namespace Kheprx.BaseBackend.Health.Application.Services.Interfaces;

public interface IHealthReadingService
{
    /// <summary>Logs a reading. Returns null when the referenced medical test does not exist.</summary>
    Task<HealthReadingDto?> CreateAsync(CreateHealthReadingRequest request, Guid recordedBy, CancellationToken ct = default);

    /// <summary>Lists a swimmer's readings (newest first), enriched with test details + derived status.</summary>
    Task<IReadOnlyList<HealthReadingListItemDto>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default);

    /// <summary>Edits a reading's value; re-derives status. Returns null when the reading does not exist.</summary>
    Task<HealthReadingListItemDto?> UpdateAsync(Guid id, UpdateHealthReadingRequest request, CancellationToken ct = default);

    /// <summary>Deletes a reading. Returns false when the reading does not exist.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
```

- [ ] **Step 4: Implement the service methods** — replace `HealthReadingService.cs`:

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;

namespace Kheprx.BaseBackend.Health.Application.Services;

internal sealed class HealthReadingService : IHealthReadingService
{
    private readonly IHealthReadingRepository _readings;
    private readonly IMedicalTestRepository _tests;

    public HealthReadingService(IHealthReadingRepository readings, IMedicalTestRepository tests)
    {
        _readings = readings;
        _tests = tests;
    }

    public async Task<HealthReadingDto?> CreateAsync(CreateHealthReadingRequest request, Guid recordedBy, CancellationToken ct = default)
    {
        var test = await _tests.GetByIdAsync(request.MedicalTestId, ct);
        if (test is null) return null;

        var reading = new HealthReading(request.SwimmerId, request.MedicalTestId, request.Value, recordedBy);
        await _readings.AddAsync(reading, ct);
        await _readings.SaveChangesAsync(ct);

        return ToDto(reading, DeriveStatus(reading.Value, test));
    }

    public async Task<IReadOnlyList<HealthReadingListItemDto>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default)
    {
        var readings = await _readings.ListBySwimmerAsync(swimmerId, ct);
        var tests = (await _tests.ListAsync(ct)).ToDictionary(t => t.Id);
        return readings.Select(r =>
        {
            tests.TryGetValue(r.MedicalTestId, out var test);
            return ToListItem(r, test);
        }).ToList();
    }

    public async Task<HealthReadingListItemDto?> UpdateAsync(Guid id, UpdateHealthReadingRequest request, CancellationToken ct = default)
    {
        var reading = await _readings.GetTrackedAsync(id, ct);
        if (reading is null) return null;

        reading.Update(request.Value);
        await _readings.SaveChangesAsync(ct);

        var test = await _tests.GetByIdAsync(reading.MedicalTestId, ct);
        return ToListItem(reading, test);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var reading = await _readings.GetTrackedAsync(id, ct);
        if (reading is null) return false;

        _readings.Remove(reading);
        await _readings.SaveChangesAsync(ct);
        return true;
    }

    private static string DeriveStatus(decimal value, MedicalTest test) =>
        value >= test.LowerBound && value <= test.UpperBound ? "normal" : "out";

    private static HealthReadingDto ToDto(HealthReading r, string status) =>
        new(r.Id, r.SwimmerId, r.MedicalTestId, r.Value, r.ReadingDate, r.RecordedBy, status);

    private static HealthReadingListItemDto ToListItem(HealthReading r, MedicalTest? test) => new(
        r.Id,
        r.MedicalTestId,
        test?.NameEn ?? string.Empty,
        test?.NameAr ?? string.Empty,
        test?.Unit ?? string.Empty,
        r.Value,
        test?.LowerBound ?? 0m,
        test?.UpperBound ?? 0m,
        r.ReadingDate,
        test is null ? "unknown" : DeriveStatus(r.Value, test));
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~HealthReadingServiceTests`
Expected: PASS (new tests + the three pre-existing `Create_*` tests).

- [ ] **Step 6: Checkpoint (stage only — DO NOT COMMIT)**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/Interfaces/IHealthReadingService.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/HealthReadingService.cs \
        backend/tests/Kheprx.BaseBackend.Health.UnitTests/Services/HealthReadingServiceTests.cs
```
Suggested message: `feat(health): HealthReadingService list(enrich+status)/update/delete`

---

## Task 5: Backend — API controller (GET/PUT/DELETE)

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/HealthReadingsController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/HealthReadingsControllerTests.cs` (create)

**Interfaces:**
- Consumes: `IHealthReadingService` (Task 4).
- Produces: HTTP endpoints — `GET api/health-readings?swimmerId={id}` (200 list), `PUT api/health-readings/{id}` (200 row / 404), `DELETE api/health-readings/{id}` (200 / 404). POST unchanged.

- [ ] **Step 1: Write the failing controller tests** — create `HealthReadingsControllerTests.cs`:

```csharp
using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class HealthReadingsControllerTests
{
    private static HealthReadingsController Controller(IHealthReadingService svc)
        => new(svc) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    private static HealthReadingListItemDto Row(Guid id) =>
        new(id, Guid.NewGuid(), "Glucose", "الجلوكوز", "mg/dL", 90m, 70m, 110m, DateTime.UtcNow, "normal");

    [Fact]
    public async Task List_returns_200_with_rows()
    {
        var svc = new Mock<IHealthReadingService>();
        var swimmerId = Guid.NewGuid();
        svc.Setup(s => s.ListBySwimmerAsync(swimmerId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Row(Guid.NewGuid()) });

        var result = await Controller(svc.Object).List(swimmerId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<HealthReadingListItemDto>>>(ok.Value);
        Assert.True(body.SuccessStatus);
        Assert.Single(body.Data!);
    }

    [Fact]
    public async Task Update_returns_200_when_found_404_when_null()
    {
        var svc = new Mock<IHealthReadingService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.UpdateAsync(id, It.IsAny<UpdateHealthReadingRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Row(id));
        svc.Setup(s => s.UpdateAsync(It.Is<Guid>(g => g != id), It.IsAny<UpdateHealthReadingRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((HealthReadingListItemDto?)null);

        var ok = await Controller(svc.Object).Update(id, new UpdateHealthReadingRequest(90m), CancellationToken.None);
        Assert.IsType<OkObjectResult>(ok.Result);

        var nf = await Controller(svc.Object).Update(Guid.NewGuid(), new UpdateHealthReadingRequest(90m), CancellationToken.None);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(nf.Result).StatusCode);
    }

    [Fact]
    public async Task Delete_returns_200_when_true_404_when_false()
    {
        var svc = new Mock<IHealthReadingService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.DeleteAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        svc.Setup(s => s.DeleteAsync(It.Is<Guid>(g => g != id), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Assert.IsType<OkObjectResult>((await Controller(svc.Object).Delete(id, CancellationToken.None)).Result);
        var nf = await Controller(svc.Object).Delete(Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(nf.Result).StatusCode);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter FullyQualifiedName~HealthReadingsControllerTests`
Expected: FAIL — `List`/`Update`/`Delete` methods don't exist on the controller (compile error).

- [ ] **Step 3: Extend the controller** — replace `HealthReadingsController.cs`:

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Route("api/health-readings")]
[Authorize]
public sealed class HealthReadingsController : BaseApiController
{
    private readonly IHealthReadingService _service;
    public HealthReadingsController(IHealthReadingService service) => _service = service;

    /// <summary>Lists a swimmer's readings (newest first), enriched with test details + status.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<HealthReadingListItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<HealthReadingListItemDto>>>> List([FromQuery] Guid swimmerId, CancellationToken ct)
    {
        var list = await _service.ListBySwimmerAsync(swimmerId, ct);
        return Ok(ApiResponse<IReadOnlyList<HealthReadingListItemDto>>.Success(
            HealthReadingMessages.Success.Listed(AppLanguage.Current), list));
    }

    /// <summary>Logs a swimmer's test reading. Head Coach or Captain only.</summary>
    [HttpPost]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<HealthReadingDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<HealthReadingDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<HealthReadingDto>>> Create(CreateHealthReadingRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, CurrentUserId(), ct);
        if (created is null)
        {
            var notFound = ApiResponse<HealthReadingDto>.Failure(
                HealthReadingMessages.Errors.TestNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, notFound);
        }

        var body = ApiResponse<HealthReadingDto>.Success(
            HealthReadingMessages.Success.Logged(AppLanguage.Current), created);
        return StatusCode(StatusCodes.Status201Created, body);
    }

    /// <summary>Edits a reading's value. Head Coach or Captain only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<HealthReadingListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HealthReadingListItemDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<HealthReadingListItemDto>>> Update(Guid id, UpdateHealthReadingRequest request, CancellationToken ct)
    {
        var updated = await _service.UpdateAsync(id, request, ct);
        if (updated is null)
        {
            var nf = ApiResponse<HealthReadingListItemDto>.Failure(
                HealthReadingMessages.Errors.NotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<HealthReadingListItemDto>.Success(
            HealthReadingMessages.Success.Updated(AppLanguage.Current), updated));
    }

    /// <summary>Deletes a reading. Head Coach or Captain only.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _service.DeleteAsync(id, ct);
        if (!deleted)
        {
            var nf = ApiResponse<object>.Failure(
                HealthReadingMessages.Errors.NotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<object>.Success(HealthReadingMessages.Success.Deleted(AppLanguage.Current), null));
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter FullyQualifiedName~HealthReadingsControllerTests`
Expected: PASS.

- [ ] **Step 5: Full backend build + test**

Run: `dotnet build backend` then `dotnet test backend`
Expected: solution builds; all backend tests green. (Stop the dev server first — DLL lock.)

- [ ] **Step 6: Checkpoint (stage only — DO NOT COMMIT)**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/HealthReadingsController.cs \
        backend/tests/Kheprx.BaseBackend.Api.UnitTests/HealthReadingsControllerTests.cs
```
Suggested message: `feat(health): HealthReadingsController GET/PUT/DELETE`

---

## Task 6: Frontend — health-readings data layer (DTO, mapper, model, repository)

**Files:**
- Modify: `frontend/src/app/features/health-readings/data/dto/health-reading.dto.ts`
- Create: `frontend/src/app/features/health-readings/data/dto/health-reading-row.mapper.ts`
- Create: `frontend/src/app/features/health-readings/domain/model/health-reading-list-item.ts`
- Modify: `frontend/src/app/features/health-readings/domain/repositories/health-reading.repository.ts`
- Modify: `frontend/src/app/features/health-readings/data/repositories/health-reading.repository.impl.ts`
- Test: `frontend/src/app/features/health-readings/testing/data/repositories/health-reading.repository.impl.spec.ts` (extend)
- Test: `frontend/src/app/features/health-readings/testing/data/dto/health-reading-row.mapper.spec.ts` (create)

**Interfaces:**
- Produces: `HealthReadingListItem` model; `HealthReadingRowDtoRs`, `HealthReadingListDtoRs`, `HealthReadingRowItemDtoRs`, `DeleteHealthReadingItemDtoRs`, `UpdateHealthReadingDtoRq`, `isHealthReadingRowDtoRsValid`, `isHealthReadingRowListValid`; `toHealthReadingListItem`, `toHealthReadingListItemList`; `IHealthReadingRepository.list/update/remove`.

- [ ] **Step 1: Create the model** — `domain/model/health-reading-list-item.ts`:

```ts
export interface HealthReadingListItem {
  id: string;
  medicalTestId: string;
  testNameEn: string;
  testNameAr: string;
  unit: string;
  value: number;
  lowerBound: number;
  upperBound: number;
  readingDate: string;
  status: string;
}
```

- [ ] **Step 2: Extend the DTO file** — append to `data/dto/health-reading.dto.ts`:

```ts

export interface HealthReadingRowDtoRs {
  id: string;
  medicalTestId: string;
  testNameEn: string;
  testNameAr: string;
  unit: string;
  value: number;
  lowerBound: number;
  upperBound: number;
  readingDate: string;
  status: string;
}

export interface HealthReadingListDtoRs extends BaseResponseRs<HealthReadingRowDtoRs[]> {}
export interface HealthReadingRowItemDtoRs extends BaseResponseRs<HealthReadingRowDtoRs> {}
export interface DeleteHealthReadingItemDtoRs extends BaseResponseRs<unknown> {}

export interface UpdateHealthReadingDtoRq {
  value: number;
}

export function isHealthReadingRowDtoRsValid(x: unknown): x is HealthReadingRowDtoRs {
  const d = x as HealthReadingRowDtoRs;
  return !!d && typeof d === 'object'
    && typeof d.id === 'string'
    && typeof d.medicalTestId === 'string'
    && typeof d.testNameEn === 'string'
    && typeof d.testNameAr === 'string'
    && typeof d.unit === 'string'
    && typeof d.value === 'number'
    && typeof d.lowerBound === 'number'
    && typeof d.upperBound === 'number'
    && typeof d.readingDate === 'string'
    && typeof d.status === 'string';
}

export function isHealthReadingRowListValid(data: unknown): data is HealthReadingRowDtoRs[] {
  return Array.isArray(data) && data.every(isHealthReadingRowDtoRsValid);
}
```

- [ ] **Step 3: Create the mapper** — `data/dto/health-reading-row.mapper.ts`:

```ts
import { HealthReadingRowDtoRs } from '@features/health-readings/data/dto/health-reading.dto';
import { HealthReadingListItem } from '@features/health-readings/domain/model/health-reading-list-item';

export function toHealthReadingListItem(d: HealthReadingRowDtoRs): HealthReadingListItem {
  return {
    id: d.id,
    medicalTestId: d.medicalTestId,
    testNameEn: d.testNameEn,
    testNameAr: d.testNameAr,
    unit: d.unit,
    value: d.value,
    lowerBound: d.lowerBound,
    upperBound: d.upperBound,
    readingDate: d.readingDate,
    status: d.status,
  };
}

export function toHealthReadingListItemList(list: HealthReadingRowDtoRs[]): HealthReadingListItem[] {
  return list.map(toHealthReadingListItem);
}
```

- [ ] **Step 4: Extend the repository interface** — replace `domain/repositories/health-reading.repository.ts`:

```ts
import { InjectionToken } from '@angular/core';
import {
  HealthReadingItemDtoRs,
  CreateHealthReadingDtoRq,
  HealthReadingListDtoRs,
  HealthReadingRowItemDtoRs,
  DeleteHealthReadingItemDtoRs,
  UpdateHealthReadingDtoRq,
} from '@features/health-readings/data/dto/health-reading.dto';

export interface IHealthReadingRepository {
  create(rq: CreateHealthReadingDtoRq): Promise<HealthReadingItemDtoRs>;
  list(swimmerId: string): Promise<HealthReadingListDtoRs>;
  update(id: string, rq: UpdateHealthReadingDtoRq): Promise<HealthReadingRowItemDtoRs>;
  remove(id: string): Promise<DeleteHealthReadingItemDtoRs>;
}

export const HEALTH_READING_REPOSITORY = new InjectionToken<IHealthReadingRepository>('HEALTH_READING_REPOSITORY');
```

- [ ] **Step 5: Write the failing repository-impl tests** — replace `testing/data/repositories/health-reading.repository.impl.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { HealthReadingRepositoryImpl } from '@features/health-readings/data/repositories/health-reading.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('HealthReadingRepositoryImpl', () => {
  const http = { post: jest.fn(), get: jest.fn(), put: jest.fn(), delete: jest.fn() } as unknown as HttpClientService;
  let repo: HealthReadingRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [HealthReadingRepositoryImpl, { provide: HttpClientService, useValue: http }],
    });
    repo = TestBed.inject(HealthReadingRepositoryImpl);
  });

  it('create POSTs /api/health-readings with the body', async () => {
    (http.post as jest.Mock).mockResolvedValue({ data: {} });
    const rq = { swimmerId: 's1', medicalTestId: 't1', value: 95 };
    await repo.create(rq);
    expect(http.post).toHaveBeenCalledWith('/api/health-readings', { body: rq });
  });

  it('list GETs /api/health-readings filtered by swimmerId', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.list('s1');
    expect(http.get).toHaveBeenCalledWith('/api/health-readings?swimmerId=s1');
  });

  it('update PUTs /api/health-readings/{id} with the value body', async () => {
    (http.put as jest.Mock).mockResolvedValue({ data: {} });
    await repo.update('r1', { value: 12.3 });
    expect(http.put).toHaveBeenCalledWith('/api/health-readings/r1', { body: { value: 12.3 } });
  });

  it('remove DELETEs /api/health-readings/{id}', async () => {
    (http.delete as jest.Mock).mockResolvedValue({ data: null });
    await repo.remove('r1');
    expect(http.delete).toHaveBeenCalledWith('/api/health-readings/r1');
  });
});
```

- [ ] **Step 6: Run tests to verify they fail**

Run: `cd frontend && npx jest health-reading.repository.impl.spec`
Expected: FAIL — `list`/`update`/`remove` not implemented.

- [ ] **Step 7: Implement the repository** — replace `data/repositories/health-reading.repository.impl.ts`:

```ts
// health-reading.repository.impl.ts — health-readings repository. Returns the API DTO envelope.
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { IHealthReadingRepository } from '@features/health-readings/domain/repositories/health-reading.repository';
import {
  HealthReadingItemDtoRs,
  CreateHealthReadingDtoRq,
  HealthReadingListDtoRs,
  HealthReadingRowItemDtoRs,
  DeleteHealthReadingItemDtoRs,
  UpdateHealthReadingDtoRq,
} from '@features/health-readings/data/dto/health-reading.dto';

@Injectable({ providedIn: 'root' })
export class HealthReadingRepositoryImpl implements IHealthReadingRepository {
  private readonly http = inject(HttpClientService);

  create(rq: CreateHealthReadingDtoRq): Promise<HealthReadingItemDtoRs> {
    return this.http.post<HealthReadingItemDtoRs>('/api/health-readings', { body: rq });
  }
  list(swimmerId: string): Promise<HealthReadingListDtoRs> {
    return this.http.get<HealthReadingListDtoRs>(`/api/health-readings?swimmerId=${swimmerId}`);
  }
  update(id: string, rq: UpdateHealthReadingDtoRq): Promise<HealthReadingRowItemDtoRs> {
    return this.http.put<HealthReadingRowItemDtoRs>(`/api/health-readings/${id}`, { body: rq });
  }
  remove(id: string): Promise<DeleteHealthReadingItemDtoRs> {
    return this.http.delete<DeleteHealthReadingItemDtoRs>(`/api/health-readings/${id}`);
  }
}
```

- [ ] **Step 8: Write the mapper test** — create `testing/data/dto/health-reading-row.mapper.spec.ts`:

```ts
import { toHealthReadingListItem, toHealthReadingListItemList } from '@features/health-readings/data/dto/health-reading-row.mapper';
import { HealthReadingRowDtoRs } from '@features/health-readings/data/dto/health-reading.dto';

const ROW: HealthReadingRowDtoRs = {
  id: 'r1', medicalTestId: 't1', testNameEn: 'Glucose', testNameAr: 'الجلوكوز', unit: 'mg/dL',
  value: 90, lowerBound: 70, upperBound: 110, readingDate: '2024-10-04T00:00:00Z', status: 'normal',
};

describe('health-reading-row.mapper', () => {
  it('maps a row DTO to the model', () => {
    const m = toHealthReadingListItem(ROW);
    expect(m).toEqual({
      id: 'r1', medicalTestId: 't1', testNameEn: 'Glucose', testNameAr: 'الجلوكوز', unit: 'mg/dL',
      value: 90, lowerBound: 70, upperBound: 110, readingDate: '2024-10-04T00:00:00Z', status: 'normal',
    });
  });

  it('maps a list', () => {
    expect(toHealthReadingListItemList([ROW, ROW])).toHaveLength(2);
  });
});
```

- [ ] **Step 9: Run tests to verify all pass**

Run: `cd frontend && npx jest health-reading.repository.impl.spec health-reading-row.mapper.spec`
Expected: PASS.

- [ ] **Step 10: Checkpoint (stage only — DO NOT COMMIT)**

```bash
git add frontend/src/app/features/health-readings/data/dto/health-reading.dto.ts \
        frontend/src/app/features/health-readings/data/dto/health-reading-row.mapper.ts \
        frontend/src/app/features/health-readings/domain/model/health-reading-list-item.ts \
        frontend/src/app/features/health-readings/domain/repositories/health-reading.repository.ts \
        frontend/src/app/features/health-readings/data/repositories/health-reading.repository.impl.ts \
        frontend/src/app/features/health-readings/testing/data/repositories/health-reading.repository.impl.spec.ts \
        frontend/src/app/features/health-readings/testing/data/dto/health-reading-row.mapper.spec.ts
```
Suggested message: `feat(health-readings): list/update/remove repository + row DTO/mapper/model`

---

## Task 7: Frontend — use-cases (list, update, delete)

**Files:**
- Create: `frontend/src/app/features/health-readings/domain/usecases/list-health-readings.use-case.ts`
- Create: `frontend/src/app/features/health-readings/domain/usecases/update-health-reading.use-case.ts`
- Create: `frontend/src/app/features/health-readings/domain/usecases/delete-health-reading.use-case.ts`
- Test: `frontend/src/app/features/health-readings/testing/domain/usecases/health-reading-crud.use-cases.spec.ts` (create)

**Interfaces:**
- Consumes: `HEALTH_READING_REPOSITORY` + DTOs/mapper (Task 6).
- Produces: `ListHealthReadingsUseCase` (`run(swimmerId: string) → Result<HealthReadingListItem[]>`); `UpdateHealthReadingUseCase` (`run({ id, rq }) → Result<HealthReadingListItem>`) with `UpdateHealthReadingInput`; `DeleteHealthReadingUseCase` (`run({ id }) → Result<void>`) with `DeleteHealthReadingInput`.

- [ ] **Step 1: Create the list use-case** — `list-health-readings.use-case.ts`:

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { HEALTH_READING_REPOSITORY } from '@features/health-readings/domain/repositories/health-reading.repository';
import { isHealthReadingRowListValid } from '@features/health-readings/data/dto/health-reading.dto';
import { toHealthReadingListItemList } from '@features/health-readings/data/dto/health-reading-row.mapper';
import { HealthReadingListItem } from '@features/health-readings/domain/model/health-reading-list-item';

@Injectable({ providedIn: 'root' })
export class ListHealthReadingsUseCase extends UseCase<string, HealthReadingListItem[]> {
  private readonly repo = inject(HEALTH_READING_REPOSITORY);
  constructor() { super('ListHealthReadings'); }
  protected async execute(swimmerId: string): Promise<HealthReadingListItem[]> {
    const res = await this.repo.list(swimmerId);
    if (!isHealthReadingRowListValid(res.data)) throw new AppError('Invalid health readings received', 'validation');
    return toHealthReadingListItemList(res.data);
  }
}
```

- [ ] **Step 2: Create the update use-case** — `update-health-reading.use-case.ts`:

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { HEALTH_READING_REPOSITORY } from '@features/health-readings/domain/repositories/health-reading.repository';
import { UpdateHealthReadingDtoRq, isHealthReadingRowDtoRsValid } from '@features/health-readings/data/dto/health-reading.dto';
import { toHealthReadingListItem } from '@features/health-readings/data/dto/health-reading-row.mapper';
import { HealthReadingListItem } from '@features/health-readings/domain/model/health-reading-list-item';

export interface UpdateHealthReadingInput { id: string; rq: UpdateHealthReadingDtoRq; }

@Injectable({ providedIn: 'root' })
export class UpdateHealthReadingUseCase extends UseCase<UpdateHealthReadingInput, HealthReadingListItem> {
  private readonly repo = inject(HEALTH_READING_REPOSITORY);
  constructor() { super('UpdateHealthReading'); }
  protected async execute(input: UpdateHealthReadingInput): Promise<HealthReadingListItem> {
    const res = await this.repo.update(input.id, input.rq);
    if (!isHealthReadingRowDtoRsValid(res.data)) throw new AppError('Invalid health reading received', 'validation');
    return toHealthReadingListItem(res.data);
  }
}
```

- [ ] **Step 3: Create the delete use-case** — `delete-health-reading.use-case.ts`:

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { HEALTH_READING_REPOSITORY } from '@features/health-readings/domain/repositories/health-reading.repository';

export interface DeleteHealthReadingInput { id: string; }

@Injectable({ providedIn: 'root' })
export class DeleteHealthReadingUseCase extends UseCase<DeleteHealthReadingInput, void> {
  private readonly repo = inject(HEALTH_READING_REPOSITORY);
  constructor() { super('DeleteHealthReading'); }
  protected async execute(input: DeleteHealthReadingInput): Promise<void> {
    await this.repo.remove(input.id);
  }
}
```

- [ ] **Step 4: Write the use-case tests** — create `testing/domain/usecases/health-reading-crud.use-cases.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { ListHealthReadingsUseCase } from '@features/health-readings/domain/usecases/list-health-readings.use-case';
import { UpdateHealthReadingUseCase } from '@features/health-readings/domain/usecases/update-health-reading.use-case';
import { DeleteHealthReadingUseCase } from '@features/health-readings/domain/usecases/delete-health-reading.use-case';
import { HEALTH_READING_REPOSITORY } from '@features/health-readings/domain/repositories/health-reading.repository';

const ROW = {
  id: 'r1', medicalTestId: 't1', testNameEn: 'Glucose', testNameAr: 'الجلوكوز', unit: 'mg/dL',
  value: 90, lowerBound: 70, upperBound: 110, readingDate: '2024-10-04T00:00:00Z', status: 'normal',
};

function setup() {
  const repo = { create: jest.fn(), list: jest.fn(), update: jest.fn(), remove: jest.fn() };
  TestBed.configureTestingModule({ providers: [{ provide: HEALTH_READING_REPOSITORY, useValue: repo }] });
  return { repo };
}

describe('health-reading CRUD use-cases', () => {
  it('list maps valid rows', async () => {
    const { repo } = setup();
    repo.list.mockResolvedValue({ data: [ROW] });
    const res = await TestBed.inject(ListHealthReadingsUseCase).run('s1');
    expect(res.ok).toBe(true);
    if (res.ok) { expect(res.data).toHaveLength(1); expect(res.data[0].testNameEn).toBe('Glucose'); }
  });

  it('list fails validation for a malformed row', async () => {
    const { repo } = setup();
    repo.list.mockResolvedValue({ data: [{ id: 'r1' }] });
    const res = await TestBed.inject(ListHealthReadingsUseCase).run('s1');
    expect(res.ok).toBe(false);
  });

  it('update sends value and maps the row', async () => {
    const { repo } = setup();
    repo.update.mockResolvedValue({ data: { ...ROW, value: 40, status: 'out' } });
    const res = await TestBed.inject(UpdateHealthReadingUseCase).run({ id: 'r1', rq: { value: 40 } });
    expect(repo.update).toHaveBeenCalledWith('r1', { value: 40 });
    expect(res.ok).toBe(true);
    if (res.ok) { expect(res.data.value).toBe(40); expect(res.data.status).toBe('out'); }
  });

  it('delete calls remove', async () => {
    const { repo } = setup();
    repo.remove.mockResolvedValue({ data: null });
    const res = await TestBed.inject(DeleteHealthReadingUseCase).run({ id: 'r1' });
    expect(repo.remove).toHaveBeenCalledWith('r1');
    expect(res.ok).toBe(true);
  });
});
```

> The `run()` → `{ ok, data } | { ok:false, error }` shape is the `UseCase` base contract (same shape the viewmodel consumes as `r.ok`/`r.data`). If the base class exposes a different result shape, mirror how `create-health-reading.use-case.spec.ts` asserts it.

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd frontend && npx jest health-reading-crud.use-cases.spec`
Expected: PASS.

- [ ] **Step 6: Checkpoint (stage only — DO NOT COMMIT)**

```bash
git add frontend/src/app/features/health-readings/domain/usecases/list-health-readings.use-case.ts \
        frontend/src/app/features/health-readings/domain/usecases/update-health-reading.use-case.ts \
        frontend/src/app/features/health-readings/domain/usecases/delete-health-reading.use-case.ts \
        frontend/src/app/features/health-readings/testing/domain/usecases/health-reading-crud.use-cases.spec.ts
```
Suggested message: `feat(health-readings): list/update/delete use-cases`

---

## Task 8: Frontend — viewmodel + enable tab

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.ts`
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts` (extend)

**Interfaces:**
- Consumes: `ListHealthReadingsUseCase`, `UpdateHealthReadingUseCase`, `DeleteHealthReadingUseCase`, `HealthReadingListItem` (Task 6/7).
- Produces (viewmodel public API used by the template in Task 9): `healthReadings()`, `healthReadingRows()`, `loadingHealthReadings()`, `hmFrom()/hmFrom.set`, `hmTo()/hmTo.set`, `editingHealthReadingId()`, `hrValue()/hrValue.set`, `savingHealthReading()`, `confirmingHealthReadingDeleteId()`, `deletingHealthReading()`, `canSaveHealthReading()`, `startEditHealthReading(row)`, `cancelEditHealthReading()`, `saveHealthReading()`, `askDeleteHealthReading(id)`, `cancelDeleteHealthReading()`, `confirmDeleteHealthReading()`; `setTab('healthMonitoring')`. Page: `testName(row)`.

- [ ] **Step 1: Enable the tab in the page** — in `swimmer-profile.page.ts`, add `'healthMonitoring'` to `enabledTabs`:

```ts
  protected readonly enabledTabs = new Set(['identityVitals', 'guardian', 'physiological', 'inbody', 'records', 'healthMonitoring']);
```

  and add a `testName` helper (after `refLabel`):

```ts
  testName(r: { testNameEn: string; testNameAr: string }): string {
    return this.language.lang() === 'ar' ? (r.testNameAr || r.testNameEn) : r.testNameEn;
  }
```

- [ ] **Step 2: Wire the viewmodel imports + injects** — in `swimmer-profile.viewmodel.ts`, add imports near the other use-case imports:

```ts
import { ListHealthReadingsUseCase } from '@features/health-readings/domain/usecases/list-health-readings.use-case';
import { UpdateHealthReadingUseCase } from '@features/health-readings/domain/usecases/update-health-reading.use-case';
import { DeleteHealthReadingUseCase } from '@features/health-readings/domain/usecases/delete-health-reading.use-case';
import { HealthReadingListItem } from '@features/health-readings/domain/model/health-reading-list-item';
```

  and add injects near the other `inject(...)` lines:

```ts
  private readonly listHealthReadingsUc = inject(ListHealthReadingsUseCase);
  private readonly updateHealthReadingUc = inject(UpdateHealthReadingUseCase);
  private readonly deleteHealthReadingUc = inject(DeleteHealthReadingUseCase);
```

- [ ] **Step 3: Extend the tab union + add state** — update the `activeTab` signal type and add the `healthReadingsLoaded` flag (near the other `private *Loaded` flags):

```ts
  readonly activeTab = signal<'identityVitals' | 'guardian' | 'physiological' | 'inbody' | 'records' | 'healthMonitoring'>('identityVitals');
```

```ts
  private healthReadingsLoaded = false;
```

  Add the Health Monitoring state block (near the Records state):

```ts
  // Health Monitoring state
  readonly healthReadings = signal<HealthReadingListItem[]>([]);
  readonly loadingHealthReadings = signal(false);
  readonly hmFrom = signal('');
  readonly hmTo = signal('');
  readonly editingHealthReadingId = signal<string | null>(null);
  readonly hrValue = signal('');
  readonly savingHealthReading = signal(false);
  readonly confirmingHealthReadingDeleteId = signal<string | null>(null);
  readonly deletingHealthReading = signal(false);

  readonly canSaveHealthReading = computed(() => {
    const v = this.hrValue().trim();
    return v.length > 0 && Number.isFinite(Number(v)) && Number(v) > 0;
  });

  readonly healthReadingRows = computed(() => {
    const from = this.hmFrom();
    const to = this.hmTo();
    const inRange = (iso: string) => {
      const d = iso.slice(0, 10);
      if (from && d < from) return false;
      if (to && d > to) return false;
      return true;
    };
    return this.healthReadings().filter((r) => inRange(r.readingDate)); // server already returns newest-first
  });
```

- [ ] **Step 4: Reset state on load** — in `load()`, alongside the other per-swimmer resets, add:

```ts
    this.healthReadingsLoaded = false;
    this.healthReadings.set([]);
    this.editingHealthReadingId.set(null);
    this.confirmingHealthReadingDeleteId.set(null);
    this.hmFrom.set('');
    this.hmTo.set('');
```

- [ ] **Step 5: Extend `setTab`** — change its parameter type to include `'healthMonitoring'` and add the lazy-load branch:

```ts
  setTab(key: 'identityVitals' | 'guardian' | 'physiological' | 'inbody' | 'records' | 'healthMonitoring'): void {
    this.activeTab.set(key);
    if (key === 'guardian' && !this.guardiansLoaded) void this.loadGuardians();
    if (key === 'physiological' && !this.bodyMeasurementLoaded) void this.loadBodyMeasurement();
    if (key === 'inbody' && !this.inbodyLoaded) void this.loadInBody();
    if (key === 'records' && !this.recordsLoaded) void this.loadRecords();
    if (key === 'healthMonitoring' && !this.healthReadingsLoaded) void this.loadHealthReadings();
  }
```

- [ ] **Step 6: Add the load + edit + delete methods** — add near the Records methods:

```ts
  private async loadHealthReadings(): Promise<void> {
    this.healthReadingsLoaded = true;
    this.loadingHealthReadings.set(true);
    const r = await this.listHealthReadingsUc.run(this.swimmerId);
    this.loadingHealthReadings.set(false);
    if (r.ok) {
      this.healthReadings.set(r.data);
    } else {
      this.healthReadingsLoaded = false;
      this.healthReadings.set([]);
    }
  }

  startEditHealthReading(row: HealthReadingListItem): void {
    this.confirmingHealthReadingDeleteId.set(null);
    this.editingHealthReadingId.set(row.id);
    this.hrValue.set(String(row.value));
  }

  cancelEditHealthReading(): void { this.editingHealthReadingId.set(null); }

  async saveHealthReading(): Promise<void> {
    const id = this.editingHealthReadingId();
    if (!id || !this.canSaveHealthReading() || this.savingHealthReading()) return;
    this.savingHealthReading.set(true);
    const r = await this.updateHealthReadingUc.run({ id, rq: { value: Number(this.hrValue()) } });
    this.savingHealthReading.set(false);
    if (r.ok) {
      this.notify.success(this.i18n.t('swimmerProfile.toasts.healthReadingUpdated'));
      this.editingHealthReadingId.set(null);
      this.healthReadingsLoaded = false;
      await this.loadHealthReadings();
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.saveFailed'));
    }
  }

  askDeleteHealthReading(id: string): void {
    this.editingHealthReadingId.set(null);
    this.confirmingHealthReadingDeleteId.set(id);
  }
  cancelDeleteHealthReading(): void { this.confirmingHealthReadingDeleteId.set(null); }

  async confirmDeleteHealthReading(): Promise<void> {
    const id = this.confirmingHealthReadingDeleteId();
    if (!id || this.deletingHealthReading()) return;
    this.deletingHealthReading.set(true);
    const res = await this.deleteHealthReadingUc.run({ id });
    this.deletingHealthReading.set(false);
    if (res.ok) {
      this.notify.success(this.i18n.t('swimmerProfile.toasts.healthReadingRemoved'));
      this.confirmingHealthReadingDeleteId.set(null);
      this.healthReadingsLoaded = false;
      await this.loadHealthReadings();
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.deleteFailed'));
    }
  }
```

- [ ] **Step 7: Extend the viewmodel spec harness** — in `swimmer-profile.viewmodel.spec.ts`:

  Add imports:

```ts
import { ListHealthReadingsUseCase } from '@features/health-readings/domain/usecases/list-health-readings.use-case';
import { UpdateHealthReadingUseCase } from '@features/health-readings/domain/usecases/update-health-reading.use-case';
import { DeleteHealthReadingUseCase } from '@features/health-readings/domain/usecases/delete-health-reading.use-case';
```

  Inside `build()`, before `TestBed.resetTestingModule()`, add the mocks + fixtures:

```ts
  const HR1 = { id: 'h1', medicalTestId: 't1', testNameEn: 'Hemoglobin', testNameAr: 'هيموجلوبين', unit: 'g/dL', value: 14.8, lowerBound: 13.5, upperBound: 17.5, readingDate: '2023-10-12T00:00:00Z', status: 'normal' };
  const HR2 = { id: 'h2', medicalTestId: 't2', testNameEn: 'Ferritin', testNameAr: 'فيريتين', unit: 'ng/mL', value: 22, lowerBound: 30, upperBound: 400, readingDate: '2023-07-15T00:00:00Z', status: 'out' };
  const listHealthReadingsUc = { run: jest.fn().mockResolvedValue((over as any).listHealthReadings ?? { ok: true, data: [HR1, HR2] }) }; // newest first
  const updateHealthReadingUc = { run: jest.fn().mockResolvedValue((over as any).updateHealthReading ?? { ok: true, data: { ...HR2, value: 35, status: 'normal' } }) };
  const deleteHealthReadingUc = { run: jest.fn().mockResolvedValue((over as any).deleteHealthReading ?? { ok: true, data: undefined }) };
```

  Add to the `providers` array:

```ts
    { provide: ListHealthReadingsUseCase, useValue: listHealthReadingsUc },
    { provide: UpdateHealthReadingUseCase, useValue: updateHealthReadingUc },
    { provide: DeleteHealthReadingUseCase, useValue: deleteHealthReadingUc },
```

  Add `listHealthReadingsUc, updateHealthReadingUc, deleteHealthReadingUc` to the returned object.

  Then add a `describe` block with tests:

```ts
describe('SwimmerProfileViewModel — Health Monitoring', () => {
  it('setTab loads health readings once (lazy)', async () => {
    const { vm, listHealthReadingsUc } = build();
    await vm.load('s1');
    vm.setTab('healthMonitoring');
    await Promise.resolve(); await Promise.resolve();
    expect(listHealthReadingsUc.run).toHaveBeenCalledTimes(1);
    vm.setTab('inbody');
    vm.setTab('healthMonitoring');
    expect(listHealthReadingsUc.run).toHaveBeenCalledTimes(1); // not reloaded
    expect(vm.healthReadings()).toHaveLength(2);
  });

  it('healthReadingRows filters by From/To (inclusive)', async () => {
    const { vm } = build();
    await vm.load('s1');
    vm.setTab('healthMonitoring');
    await Promise.resolve(); await Promise.resolve();
    vm.hmFrom.set('2023-10-01');
    expect(vm.healthReadingRows().map((r) => r.id)).toEqual(['h1']); // h2 is 2023-07-15, excluded
    vm.hmFrom.set('');
    vm.hmTo.set('2023-08-01');
    expect(vm.healthReadingRows().map((r) => r.id)).toEqual(['h2']);
  });

  it('canSaveHealthReading requires a positive number', async () => {
    const { vm } = build();
    await vm.load('s1');
    vm.hrValue.set('0'); expect(vm.canSaveHealthReading()).toBe(false);
    vm.hrValue.set('abc'); expect(vm.canSaveHealthReading()).toBe(false);
    vm.hrValue.set('12.3'); expect(vm.canSaveHealthReading()).toBe(true);
  });

  it('saveHealthReading updates, toasts, and reloads', async () => {
    const { vm, updateHealthReadingUc, listHealthReadingsUc, notify } = build();
    await vm.load('s1');
    vm.setTab('healthMonitoring');
    await Promise.resolve(); await Promise.resolve();
    vm.startEditHealthReading({ id: 'h2', medicalTestId: 't2', testNameEn: 'Ferritin', testNameAr: 'فيريتين', unit: 'ng/mL', value: 22, lowerBound: 30, upperBound: 400, readingDate: '2023-07-15T00:00:00Z', status: 'out' });
    vm.hrValue.set('35');
    await vm.saveHealthReading();
    expect(updateHealthReadingUc.run).toHaveBeenCalledWith({ id: 'h2', rq: { value: 35 } });
    expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.healthReadingUpdated');
    expect(listHealthReadingsUc.run).toHaveBeenCalledTimes(2); // initial + reload
    expect(vm.editingHealthReadingId()).toBeNull();
  });

  it('confirmDeleteHealthReading deletes, toasts, and reloads', async () => {
    const { vm, deleteHealthReadingUc, notify } = build();
    await vm.load('s1');
    vm.setTab('healthMonitoring');
    await Promise.resolve(); await Promise.resolve();
    vm.askDeleteHealthReading('h1');
    await vm.confirmDeleteHealthReading();
    expect(deleteHealthReadingUc.run).toHaveBeenCalledWith({ id: 'h1' });
    expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.healthReadingRemoved');
    expect(vm.confirmingHealthReadingDeleteId()).toBeNull();
  });
});
```

- [ ] **Step 8: Run the viewmodel spec to verify it fails then passes**

Run: `cd frontend && npx jest swimmer-profile.viewmodel.spec`
Expected: FAIL first if any method/signal is missing; after Steps 1-6 it should PASS. Fix mismatches until green.

- [ ] **Step 9: Checkpoint (stage only — DO NOT COMMIT)**

```bash
git add frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.ts \
        frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.ts \
        frontend/src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts
```
Suggested message: `feat(swimmer-profile): Health Monitoring viewmodel + enable tab`

---

## Task 9: Frontend — template section + i18n

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.html`
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`

**Interfaces:**
- Consumes: viewmodel API (Task 8) + `testName` (page). Uses existing keys `common.save`, `common.cancel`, `swimmerProfile.confirmYes`, `swimmerProfile.confirmNo`, `swimmerProfile.states.loading`.

- [ ] **Step 1: Add the i18n keys (EN)** — in `core/i18n/en.json`, add a `healthMonitoring` block inside `swimmerProfile` (e.g., after the `records` block):

```json
    "healthMonitoring": {
      "title": "Test results",
      "subtitle": "Every logged test result, measured against its defined normal range.",
      "from": "From",
      "to": "To",
      "value": "Value",
      "inRange": "In range",
      "watch": "Watch",
      "unknown": "Unknown",
      "edit": "Edit",
      "remove": "Remove",
      "confirmDelete": "Remove this reading?",
      "noReadings": "No test readings yet.",
      "noneInRange": "No readings in the selected date range."
    },
```

  and add to `swimmerProfile.toasts`:

```json
      "healthReadingUpdated": "Reading updated",
      "healthReadingRemoved": "Reading removed"
```

  (add a comma after the previous last toast entry — `recordRemoved` — so the JSON stays valid).

- [ ] **Step 2: Add the i18n keys (AR)** — in `core/i18n/ar.json`, add the matching block inside `swimmerProfile`:

```json
    "healthMonitoring": {
      "title": "نتائج الاختبارات",
      "subtitle": "كل نتيجة اختبار مسجلة، مقارنة بالنطاق الطبيعي المحدد لها.",
      "from": "من",
      "to": "إلى",
      "value": "القيمة",
      "inRange": "ضمن النطاق",
      "watch": "تحذير",
      "unknown": "غير معروف",
      "edit": "تعديل",
      "remove": "حذف",
      "confirmDelete": "حذف هذه القراءة؟",
      "noReadings": "لا توجد قراءات بعد.",
      "noneInRange": "لا توجد قراءات ضمن النطاق الزمني المحدد."
    },
```

  and to `swimmerProfile.toasts` (AR):

```json
      "healthReadingUpdated": "تم تحديث القراءة",
      "healthReadingRemoved": "تم حذف القراءة"
```

- [ ] **Step 3: Add the tab section markup** — in `swimmer-profile.page.html`, add this block immediately after the `records` section's closing `}` (currently around line 415, before the final `}` that closes the `@else if (vm.profile(); as p)` block):

```html
    @if (vm.activeTab() === 'healthMonitoring') {
      <section class="rounded-2xl border border-border bg-surface p-6 shadow-sm">
        <div class="mb-4 flex flex-wrap items-end justify-between gap-3 border-b border-border pb-3">
          <div>
            <h2 class="font-heading text-lg text-ink">{{ 'swimmerProfile.healthMonitoring.title' | translate }}</h2>
            <p class="mt-0.5 text-sm text-text-secondary">{{ 'swimmerProfile.healthMonitoring.subtitle' | translate }}</p>
          </div>
          <div class="flex items-end gap-3">
            <label class="flex flex-col gap-1">
              <span class="text-xs font-bold text-text-secondary">{{ 'swimmerProfile.healthMonitoring.from' | translate }}</span>
              <input type="date" class="h-9 rounded-md border border-border bg-surface px-2 text-sm text-ink" [value]="vm.hmFrom()" (change)="vm.hmFrom.set($any($event.target).value)" />
            </label>
            <label class="flex flex-col gap-1">
              <span class="text-xs font-bold text-text-secondary">{{ 'swimmerProfile.healthMonitoring.to' | translate }}</span>
              <input type="date" class="h-9 rounded-md border border-border bg-surface px-2 text-sm text-ink" [value]="vm.hmTo()" (change)="vm.hmTo.set($any($event.target).value)" />
            </label>
          </div>
        </div>

        @if (vm.loadingHealthReadings()) {
          <p class="text-sm text-text-secondary">{{ 'swimmerProfile.states.loading' | translate }}</p>
        } @else if (vm.healthReadings().length === 0) {
          <p class="text-sm text-text-secondary">{{ 'swimmerProfile.healthMonitoring.noReadings' | translate }}</p>
        } @else if (vm.healthReadingRows().length === 0) {
          <p class="text-sm text-text-secondary">{{ 'swimmerProfile.healthMonitoring.noneInRange' | translate }}</p>
        } @else {
          <ul class="divide-y divide-border rounded-lg border border-border">
            @for (row of vm.healthReadingRows(); track row.id) {
              <li class="px-4 py-3 text-sm">
                @if (vm.editingHealthReadingId() === row.id) {
                  <form class="flex flex-wrap items-end gap-3" (submit)="$event.preventDefault(); vm.saveHealthReading()">
                    <span class="min-w-0 flex-1 font-medium text-ink">{{ testName(row) }}</span>
                    <app-text-field [label]="'swimmerProfile.healthMonitoring.value' | translate" [value]="vm.hrValue()" (valueChange)="vm.hrValue.set($event)"></app-text-field>
                    <button type="submit" class="rounded-md bg-primary px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" [disabled]="!vm.canSaveHealthReading() || vm.savingHealthReading()">{{ 'common.save' | translate }}</button>
                    <button type="button" class="rounded-md px-4 py-2 text-sm text-text-secondary" (click)="vm.cancelEditHealthReading()">{{ 'common.cancel' | translate }}</button>
                  </form>
                } @else {
                  <div class="flex flex-wrap items-center gap-3">
                    <span class="min-w-0 flex-1 font-medium text-ink">{{ testName(row) }}</span>
                    <span class="font-medium text-ink">{{ row.value }} {{ row.unit }}</span>
                    <span class="text-text-secondary">{{ row.lowerBound }} – {{ row.upperBound }}</span>
                    @if (row.status === 'normal') {
                      <span class="inline-flex items-center rounded-full bg-success/10 px-2 py-0.5 text-xs font-medium text-success">{{ 'swimmerProfile.healthMonitoring.inRange' | translate }}</span>
                    } @else if (row.status === 'out') {
                      <span class="inline-flex items-center rounded-full bg-warning/10 px-2 py-0.5 text-xs font-medium text-warning">{{ 'swimmerProfile.healthMonitoring.watch' | translate }}</span>
                    } @else {
                      <span class="inline-flex items-center rounded-full bg-border px-2 py-0.5 text-xs font-medium text-text-secondary">{{ 'swimmerProfile.healthMonitoring.unknown' | translate }}</span>
                    }
                    <span class="text-xs text-text-secondary">{{ fmtDate(row.readingDate) }}</span>
                    @if (vm.canEdit()) {
                      @if (vm.confirmingHealthReadingDeleteId() === row.id) {
                        <span class="text-sm text-danger">{{ 'swimmerProfile.healthMonitoring.confirmDelete' | translate }}</span>
                        <button type="button" class="text-sm font-medium text-danger disabled:opacity-50" [disabled]="vm.deletingHealthReading()" (click)="vm.confirmDeleteHealthReading()">{{ 'swimmerProfile.confirmYes' | translate }}</button>
                        <button type="button" class="text-sm text-text-secondary" (click)="vm.cancelDeleteHealthReading()">{{ 'swimmerProfile.confirmNo' | translate }}</button>
                      } @else {
                        <button type="button" class="text-sm font-medium text-primary" (click)="vm.startEditHealthReading(row)">{{ 'swimmerProfile.healthMonitoring.edit' | translate }}</button>
                        <button type="button" class="text-sm font-medium text-danger" (click)="vm.askDeleteHealthReading(row.id)">{{ 'swimmerProfile.healthMonitoring.remove' | translate }}</button>
                      }
                    }
                  </div>
                }
              </li>
            }
          </ul>
        }
      </section>
    }
```

- [ ] **Step 4: Build + lint + test the frontend**

Run: `cd frontend && npx tsc -p tsconfig.json --noEmit && npx jest && npm run build`
Expected: type-check clean, all Jest suites green, production build succeeds. (If the project uses a different build/test command, use the ones in `frontend/package.json` scripts.)

- [ ] **Step 5: Manual smoke check (optional but recommended)**

Start the app, open a swimmer profile (`swimmers/:id`), click **Health Monitoring**. Confirm: readings list newest-first with value+unit, `lower – upper` range, In range/Watch pill, and date; the From/To filter narrows rows; as a coach, edit a value (saves + re-derives status) and remove a reading (confirm step). As a non-coach role, the Edit/Remove buttons are hidden.

- [ ] **Step 6: Checkpoint (stage only — DO NOT COMMIT)**

```bash
git add frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.html \
        frontend/src/app/core/i18n/en.json \
        frontend/src/app/core/i18n/ar.json
```
Suggested message: `feat(swimmer-profile): Health Monitoring tab UI + i18n`

---

## Final verification (before requesting commit authorization)

- [ ] Backend: dev server stopped, `dotnet build backend` clean, `dotnet test backend` all green.
- [ ] Frontend: `npx tsc --noEmit` clean, `npx jest` all green, `npm run build` succeeds.
- [ ] Manual: Health Monitoring tab lists/filters/edits/deletes as specified; role gating works; AR labels render.
- [ ] Tell the user everything is staged and verified, and ask whether to commit (using the suggested per-task messages, or squashed).

---

## Self-review notes (spec coverage)

- Spec §"Backend design" → Tasks 1-5 (entity Update, repo, DTOs/messages/validator, service enrich+status+update+delete, controller GET/PUT/DELETE + auth restructure). ✔
- Spec §"Frontend design" → Tasks 6-9 (data layer, use-cases, viewmodel + enable tab, template + i18n). ✔
- Spec locked decisions 1-11: single flat card (Task 9), enriched rows (Tasks 4/9), binary server status (Task 4, UI map Task 9), read+edit+delete no-Add (Tasks 5/9), edit=value-only (Tasks 1/3/4/8), newest-first (Task 2 + server), client-side date filter (Task 8 `healthReadingRows`), flat routes (Task 5), auth (Task 5), no ownership guard (Task 5), no commits (Global Constraints + every checkpoint). ✔
- Spec §"Testing" → entity/repo/service/validator/controller (Tasks 1-5) + mapper/repo-impl/use-cases/viewmodel (Tasks 6-8). ✔
- No DB migration (Global Constraints). ✔
