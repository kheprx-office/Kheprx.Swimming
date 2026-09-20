# Swimmer Profile — Records tab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a **Records** tab to the swimmer profile page that lists a swimmer's `health.observation` rows grouped by category, with coach edit + remove.

**Architecture:** Extend the existing flat `ObservationsController` (Health module) with read/update/delete methods mirroring the InBody CRUD slice, then build the Records tab inside the `swimmer-profile` frontend feature mirroring the InBody tab. Category names are resolved client-side via the existing `GET /api/reference/observation-categories`. No schema change, no new route.

**Tech Stack:** .NET 10 (C#, EF Core, FluentValidation, xUnit + Moq), Angular (standalone components, signals, Jest, clean-architecture slices).

**Spec:** `docs/superpowers/specs/2026-09-20-swimmer-profile-records-tab-design.md`

## Global Constraints

- **NO git commits until the user explicitly authorizes.** Each task ends with a "Commit" step for the intended commit unit + message; do NOT run it until the user says commits are allowed. Until then, leave changes in the working tree.
- **TDD:** write the failing test first, watch it fail, implement minimally, watch it pass.
- **Backend auth:** `GET` = `[Authorize]` (any authenticated); `POST`/`PUT`/`DELETE` = `[Authorize(Roles = "head_coach,captain")]`.
- **Immutable on edit:** `Observation.ObservedDate` and `Observation.RecordedBy` never change after creation.
- **Validation limits (verbatim):** `CategoryId` not empty; `FieldLabel` required, `MaximumLength(200)`; `Value` required, `MaximumLength(500)`.
- **No Add on this tab** (adding stays in Captain Panel → Swimmer Data Fields). No range/status/trend, no date filter.
- **`dotnet test`/`dotnet build` require the running API to be stopped** (DLL lock) — stop it before backend steps.
- FluentValidation validators in the Health.Application assembly are **auto-registered** (`AddValidatorsFromAssemblies`); new validators need no DI wiring.

---

## File Structure

**Backend — modify**
- `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Entities/Observation.cs` — add `Update(...)`.
- `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Repositories/IObservationRepository.cs` — add list/get-tracked/remove.
- `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Repositories/ObservationRepository.cs` — implement them.
- `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/DTOs/ObservationDtos.cs` — add `UpdateObservationRequest`.
- `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Resources/ObservationMessages.cs` — add Listed/Updated/Deleted/NotFound.
- `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/Interfaces/IObservationService.cs` — add list/update/delete.
- `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/ObservationService.cs` — implement them.
- `backend/Kheprx.BaseBackend.Api/Controllers/ObservationsController.cs` — add GET/PUT/DELETE; move role attr to per-method.

**Backend — create**
- `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Validators/UpdateObservationRequestValidator.cs`
- `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Validators/UpdateObservationRequestValidatorTests.cs`
- `backend/tests/Kheprx.BaseBackend.Api.UnitTests/ObservationsControllerTests.cs`

**Backend — modify tests**
- `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Entities/ObservationTests.cs`
- `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Repositories/ObservationRepositoryTests.cs`
- `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Services/ObservationServiceTests.cs`

**Frontend — create** (all under `frontend/src/app/features/swimmer-profile/`)
- `domain/model/record-entry.ts`
- `data/dto/record.dto.ts`
- `data/dto/record.mapper.ts`
- `domain/usecases/list-records.use-case.ts`
- `domain/usecases/update-record.use-case.ts`
- `domain/usecases/delete-record.use-case.ts`
- `testing/data/dto/record.mapper.spec.ts`
- `testing/domain/usecases/records.use-cases.spec.ts`

**Frontend — modify**
- `domain/repositories/swimmer-profile.repository.ts`
- `data/repositories/swimmer-profile.repository.impl.ts`
- `presentation/pages/swimmer-profile/swimmer-profile.viewmodel.ts`
- `presentation/pages/swimmer-profile/swimmer-profile.page.ts`
- `presentation/pages/swimmer-profile/swimmer-profile.page.html`
- `testing/data/repositories/swimmer-profile.repository.impl.spec.ts`
- `testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts`
- `frontend/src/app/core/i18n/en.json`, `frontend/src/app/core/i18n/ar.json`

---

## Task 1: Observation entity — `Update` method

**Files:**
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Entities/Observation.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Entities/ObservationTests.cs`

**Interfaces:**
- Produces: `Observation.Update(Guid categoryId, string fieldLabel, string value)` — mutates category/label/value (trimmed), preserves `SwimmerId`, `ObservedDate`, `RecordedBy`.

- [ ] **Step 1: Write the failing test** — append to `ObservationTests.cs` (inside the class):

```csharp
    [Fact]
    public void Update_changes_category_label_value_trims_and_preserves_swimmer_observed_date_and_recorder()
    {
        var swimmerId = Guid.NewGuid();
        var recordedBy = Guid.NewGuid();
        var o = new Observation(swimmerId, Guid.NewGuid(), "Penicillin", "Severe", recordedBy);
        var observed = o.ObservedDate;
        var newCat = Guid.NewGuid();

        o.Update(newCat, " Pollen ", " Mild ");

        Assert.Equal(newCat, o.CategoryId);
        Assert.Equal("Pollen", o.FieldLabel);
        Assert.Equal("Mild", o.Value);
        Assert.Equal(swimmerId, o.SwimmerId);       // preserved
        Assert.Equal(recordedBy, o.RecordedBy);     // preserved
        Assert.Equal(observed, o.ObservedDate);     // preserved
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~ObservationTests`
Expected: FAIL — `Observation` does not contain a definition for `Update`.

- [ ] **Step 3: Add the `Update` method** to `Observation.cs` (after the constructor, before the closing brace):

```csharp
    public void Update(Guid categoryId, string fieldLabel, string value)
    {
        CategoryId = categoryId;
        FieldLabel = fieldLabel.Trim();
        Value = value.Trim();
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~ObservationTests`
Expected: PASS.

- [ ] **Step 5: Commit** (only once commits are authorized)

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Entities/Observation.cs backend/tests/Kheprx.BaseBackend.Health.UnitTests/Entities/ObservationTests.cs
git commit -m "feat(health): Observation.Update for editable records"
```

---

## Task 2: Observation repository — list / get-tracked / remove

**Files:**
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Repositories/IObservationRepository.cs`
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Repositories/ObservationRepository.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Repositories/ObservationRepositoryTests.cs`

**Interfaces:**
- Consumes: `Observation` (Task 1), `HealthDbContext.Observations`.
- Produces:
  - `Task<IReadOnlyList<Observation>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default)` — newest-first by `ObservedDate`.
  - `Task<Observation?> GetTrackedAsync(Guid id, CancellationToken ct = default)`.
  - `void Remove(Observation observation)`.

- [ ] **Step 1: Write the failing tests** — append to `ObservationRepositoryTests.cs` (inside the class):

```csharp
    [Fact]
    public async Task ListBySwimmerAsync_returns_only_that_swimmer_newest_first()
    {
        await using var db = NewDb();
        var repo = new ObservationRepository(db);
        var sw = Guid.NewGuid();
        await repo.AddAsync(new Observation(sw, Guid.NewGuid(), "A", "1", Guid.NewGuid()));
        await repo.SaveChangesAsync();
        await repo.AddAsync(new Observation(sw, Guid.NewGuid(), "B", "2", Guid.NewGuid()));
        await repo.AddAsync(new Observation(Guid.NewGuid(), Guid.NewGuid(), "C", "3", Guid.NewGuid())); // other swimmer
        await repo.SaveChangesAsync();

        var list = await repo.ListBySwimmerAsync(sw);

        Assert.Equal(2, list.Count);
        Assert.All(list, o => Assert.Equal(sw, o.SwimmerId));
        Assert.True(list[0].ObservedDate >= list[1].ObservedDate); // newest first
    }

    [Fact]
    public async Task GetTrackedAsync_returns_observation_and_Remove_deletes_it()
    {
        await using var db = NewDb();
        var repo = new ObservationRepository(db);
        var o = new Observation(Guid.NewGuid(), Guid.NewGuid(), "Penicillin", "Severe", Guid.NewGuid());
        await repo.AddAsync(o);
        await repo.SaveChangesAsync();

        var tracked = await repo.GetTrackedAsync(o.Id);
        Assert.NotNull(tracked);
        repo.Remove(tracked!);
        await repo.SaveChangesAsync();

        Assert.Null(await repo.GetTrackedAsync(o.Id));
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~ObservationRepositoryTests`
Expected: FAIL — methods `ListBySwimmerAsync` / `GetTrackedAsync` / `Remove` not defined.

- [ ] **Step 3a: Extend the interface** — replace the body of `IObservationRepository.cs` with:

```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;

namespace Kheprx.BaseBackend.Health.Domain.Repositories;

public interface IObservationRepository
{
    Task<IReadOnlyList<Observation>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default);
    Task AddAsync(Observation observation, CancellationToken ct = default);
    Task<Observation?> GetTrackedAsync(Guid id, CancellationToken ct = default);
    void Remove(Observation observation);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

- [ ] **Step 3b: Implement in `ObservationRepository.cs`** — add `using Microsoft.EntityFrameworkCore;` at the top, and add these methods to the class:

```csharp
    public async Task<IReadOnlyList<Observation>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default)
        => await _db.Observations.AsNoTracking()
              .Where(o => o.SwimmerId == swimmerId)
              .OrderByDescending(o => o.ObservedDate)
              .ToListAsync(ct);

    public Task<Observation?> GetTrackedAsync(Guid id, CancellationToken ct = default)
        => _db.Observations.FirstOrDefaultAsync(o => o.Id == id, ct);

    public void Remove(Observation observation) => _db.Observations.Remove(observation);
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~ObservationRepositoryTests`
Expected: PASS.

- [ ] **Step 5: Commit** (when authorized)

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Repositories/IObservationRepository.cs backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Repositories/ObservationRepository.cs backend/tests/Kheprx.BaseBackend.Health.UnitTests/Repositories/ObservationRepositoryTests.cs
git commit -m "feat(health): observation repo list/get-tracked/remove"
```

---

## Task 3: DTO + validator + messages

**Files:**
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/DTOs/ObservationDtos.cs`
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Resources/ObservationMessages.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Validators/UpdateObservationRequestValidator.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Validators/UpdateObservationRequestValidatorTests.cs`

**Interfaces:**
- Produces: `UpdateObservationRequest(Guid CategoryId, string FieldLabel, string Value)`; `UpdateObservationRequestValidator`; `ObservationMessages.Success.{Listed,Updated,Deleted}`, `ObservationMessages.Errors.NotFound`.

- [ ] **Step 1: Write the failing validator test** — create `UpdateObservationRequestValidatorTests.cs`:

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Validators;

public class UpdateObservationRequestValidatorTests
{
    private static UpdateObservationRequest Valid() =>
        new(CategoryId: Guid.NewGuid(), FieldLabel: "Penicillin", Value: "Severe");

    private readonly UpdateObservationRequestValidator _v = new();

    [Fact] public void Valid_request_passes() => Assert.True(_v.Validate(Valid()).IsValid);

    [Fact]
    public void Empty_fields_fail()
    {
        Assert.False(_v.Validate(Valid() with { CategoryId = Guid.Empty }).IsValid);
        Assert.False(_v.Validate(Valid() with { FieldLabel = "" }).IsValid);
        Assert.False(_v.Validate(Valid() with { Value = "" }).IsValid);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~UpdateObservationRequestValidatorTests`
Expected: FAIL — `UpdateObservationRequest` / `UpdateObservationRequestValidator` do not exist (compile error).

- [ ] **Step 3a: Add the DTO** — append to `ObservationDtos.cs`:

```csharp

/// <summary>Payload to edit a swimmer data field (PUT /api/observations/{id}).</summary>
public sealed record UpdateObservationRequest(
    Guid CategoryId,
    string FieldLabel,
    string Value);
```

- [ ] **Step 3b: Add the messages** — in `ObservationMessages.cs`, add to `Success`:

```csharp
        public static string Listed(string lang) => lang switch { "ar" => "بيانات السبّاح", _ => "Swimmer data fields" };
        public static string Updated(string lang) => lang switch { "ar" => "تم تحديث البيان", _ => "Data field updated" };
        public static string Deleted(string lang) => lang switch { "ar" => "تم حذف البيان", _ => "Data field deleted" };
```

and add to `Errors`:

```csharp
        public static string NotFound(string lang) => lang switch { "ar" => "البيان غير موجود", _ => "Data field not found" };
```

- [ ] **Step 3c: Create the validator** — `UpdateObservationRequestValidator.cs`:

```csharp
using FluentValidation;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Health.Application.Validators;

public sealed class UpdateObservationRequestValidator : AbstractValidator<UpdateObservationRequest>
{
    public UpdateObservationRequestValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage(_ => ObservationMessages.Errors.CategoryRequired(AppLanguage.Current));
        RuleFor(x => x.FieldLabel)
            .NotEmpty().WithMessage(_ => ObservationMessages.Errors.FieldLabelRequired(AppLanguage.Current))
            .MaximumLength(200);
        RuleFor(x => x.Value)
            .NotEmpty().WithMessage(_ => ObservationMessages.Errors.ValueRequired(AppLanguage.Current))
            .MaximumLength(500);
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~UpdateObservationRequestValidatorTests`
Expected: PASS.

- [ ] **Step 5: Commit** (when authorized)

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/DTOs/ObservationDtos.cs backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Resources/ObservationMessages.cs backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Validators/UpdateObservationRequestValidator.cs backend/tests/Kheprx.BaseBackend.Health.UnitTests/Validators/UpdateObservationRequestValidatorTests.cs
git commit -m "feat(health): UpdateObservationRequest DTO, validator, messages"
```

---

## Task 4: Observation service — list / update / delete

**Files:**
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/Interfaces/IObservationService.cs`
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/ObservationService.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Services/ObservationServiceTests.cs`

**Interfaces:**
- Consumes: repo methods (Task 2), `UpdateObservationRequest` (Task 3), `Observation.Update` (Task 1).
- Produces:
  - `Task<IReadOnlyList<ObservationDto>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default)`.
  - `Task<ObservationDto?> UpdateAsync(Guid id, UpdateObservationRequest request, CancellationToken ct = default)` — `null` when missing.
  - `Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)` — `false` when missing.

- [ ] **Step 1: Write the failing tests** — append to `ObservationServiceTests.cs` (inside the class):

```csharp
    [Fact]
    public async Task ListBySwimmer_maps_rows()
    {
        var repo = new Mock<IObservationRepository>();
        var sw = Guid.NewGuid();
        repo.Setup(r => r.ListBySwimmerAsync(sw, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Observation> { new(sw, Guid.NewGuid(), "Penicillin", "Severe", Guid.NewGuid()) });
        var svc = new ObservationService(repo.Object);

        var list = await svc.ListBySwimmerAsync(sw);

        Assert.Single(list);
        Assert.Equal("Penicillin", list[0].FieldLabel);
    }

    [Fact]
    public async Task Update_applies_when_found_and_returns_null_when_missing()
    {
        var repo = new Mock<IObservationRepository>();
        var existing = new Observation(Guid.NewGuid(), Guid.NewGuid(), "Penicillin", "Severe", Guid.NewGuid());
        repo.Setup(r => r.GetTrackedAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var svc = new ObservationService(repo.Object);
        var newCat = Guid.NewGuid();

        var ok = await svc.UpdateAsync(existing.Id, new UpdateObservationRequest(newCat, "Pollen", "Mild"));
        var missing = await svc.UpdateAsync(Guid.NewGuid(), new UpdateObservationRequest(newCat, "X", "Y"));

        Assert.NotNull(ok);
        Assert.Equal("Pollen", existing.FieldLabel);
        Assert.Equal("Mild", existing.Value);
        Assert.Equal(newCat, existing.CategoryId);
        Assert.Null(missing);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_removes_when_found_false_when_missing()
    {
        var repo = new Mock<IObservationRepository>();
        var existing = new Observation(Guid.NewGuid(), Guid.NewGuid(), "Penicillin", "Severe", Guid.NewGuid());
        repo.Setup(r => r.GetTrackedAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var svc = new ObservationService(repo.Object);

        Assert.True(await svc.DeleteAsync(existing.Id));
        repo.Verify(r => r.Remove(existing), Times.Once);
        Assert.False(await svc.DeleteAsync(Guid.NewGuid()));
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~ObservationServiceTests`
Expected: FAIL — `ListBySwimmerAsync` / `UpdateAsync` / `DeleteAsync` not defined.

- [ ] **Step 3a: Extend the interface** — replace the body of `IObservationService.cs`:

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;

namespace Kheprx.BaseBackend.Health.Application.Services.Interfaces;

public interface IObservationService
{
    Task<ObservationDto> CreateAsync(CreateObservationRequest request, Guid recordedBy, CancellationToken ct = default);
    Task<IReadOnlyList<ObservationDto>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default);
    Task<ObservationDto?> UpdateAsync(Guid id, UpdateObservationRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
```

- [ ] **Step 3b: Implement in `ObservationService.cs`** — add these methods above the private `ToDto`:

```csharp
    public async Task<IReadOnlyList<ObservationDto>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default)
    {
        var rows = await _observations.ListBySwimmerAsync(swimmerId, ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ObservationDto?> UpdateAsync(Guid id, UpdateObservationRequest request, CancellationToken ct = default)
    {
        var o = await _observations.GetTrackedAsync(id, ct);
        if (o is null) return null;

        o.Update(request.CategoryId, request.FieldLabel, request.Value);
        await _observations.SaveChangesAsync(ct);
        return ToDto(o);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var o = await _observations.GetTrackedAsync(id, ct);
        if (o is null) return false;

        _observations.Remove(o);
        await _observations.SaveChangesAsync(ct);
        return true;
    }
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~ObservationServiceTests`
Expected: PASS.

- [ ] **Step 5: Commit** (when authorized)

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/Interfaces/IObservationService.cs backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/ObservationService.cs backend/tests/Kheprx.BaseBackend.Health.UnitTests/Services/ObservationServiceTests.cs
git commit -m "feat(health): observation service list/update/delete"
```

---

## Task 5: ObservationsController — GET / PUT / DELETE (+ full backend gate)

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/ObservationsController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/ObservationsControllerTests.cs` (create)

**Interfaces:**
- Consumes: `IObservationService` (Task 4), `UpdateObservationRequest` (Task 3).
- Produces HTTP: `GET /api/observations?swimmerId={id}` (200), `PUT /api/observations/{id}` (200/404/400), `DELETE /api/observations/{id}` (200/404).

- [ ] **Step 1: Write the failing controller tests** — create `ObservationsControllerTests.cs`:

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

public class ObservationsControllerTests
{
    private static ObservationsController Controller(IObservationService svc)
        => new(svc) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    private static ObservationDto Dto(Guid id) => new(id, Guid.NewGuid(), Guid.NewGuid(), "Penicillin", "Severe", DateTime.UtcNow, Guid.NewGuid());
    private static UpdateObservationRequest Req() => new(Guid.NewGuid(), "Penicillin", "Moderate");

    [Fact]
    public async Task List_returns_200_with_records()
    {
        var svc = new Mock<IObservationService>();
        var sw = Guid.NewGuid();
        svc.Setup(s => s.ListBySwimmerAsync(sw, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Dto(Guid.NewGuid()) });

        var result = await Controller(svc.Object).List(sw, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<ObservationDto>>>(ok.Value);
        Assert.True(body.SuccessStatus);
        Assert.Single(body.Data!);
    }

    [Fact]
    public async Task Update_returns_200_when_found_404_when_null()
    {
        var svc = new Mock<IObservationService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.UpdateAsync(id, It.IsAny<UpdateObservationRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Dto(id));
        svc.Setup(s => s.UpdateAsync(It.Is<Guid>(g => g != id), It.IsAny<UpdateObservationRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((ObservationDto?)null);

        Assert.IsType<OkObjectResult>((await Controller(svc.Object).Update(id, Req(), CancellationToken.None)).Result);
        var nf = await Controller(svc.Object).Update(Guid.NewGuid(), Req(), CancellationToken.None);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(nf.Result).StatusCode);
    }

    [Fact]
    public async Task Delete_returns_200_when_true_404_when_false()
    {
        var svc = new Mock<IObservationService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.DeleteAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        svc.Setup(s => s.DeleteAsync(It.Is<Guid>(g => g != id), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Assert.IsType<OkObjectResult>((await Controller(svc.Object).Delete(id, CancellationToken.None)).Result);
        var nf = await Controller(svc.Object).Delete(Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(nf.Result).StatusCode);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter FullyQualifiedName~ObservationsControllerTests`
Expected: FAIL — controller has no `List` / `Update` / `Delete`.

- [ ] **Step 3: Extend the controller** — replace `ObservationsController.cs` with:

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

[Route("api/observations")]
[Authorize]
public sealed class ObservationsController : BaseApiController
{
    private readonly IObservationService _service;
    public ObservationsController(IObservationService service) => _service = service;

    /// <summary>Lists a swimmer's data fields (observations), newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ObservationDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ObservationDto>>>> List([FromQuery] Guid swimmerId, CancellationToken ct)
    {
        var list = await _service.ListBySwimmerAsync(swimmerId, ct);
        return Ok(ApiResponse<IReadOnlyList<ObservationDto>>.Success(ObservationMessages.Success.Listed(AppLanguage.Current), list));
    }

    /// <summary>Adds a swimmer data field (observation). Head Coach or Captain only.</summary>
    [HttpPost]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<ObservationDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<ObservationDto>>> Create(CreateObservationRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, CurrentUserId(), ct);
        var body = ApiResponse<ObservationDto>.Success(
            ObservationMessages.Success.Added(AppLanguage.Current), created);
        return StatusCode(StatusCodes.Status201Created, body);
    }

    /// <summary>Edits a swimmer data field. Head Coach or Captain only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<ObservationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ObservationDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ObservationDto>>> Update(Guid id, UpdateObservationRequest request, CancellationToken ct)
    {
        var updated = await _service.UpdateAsync(id, request, ct);
        if (updated is null)
        {
            var nf = ApiResponse<ObservationDto>.Failure(ObservationMessages.Errors.NotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<ObservationDto>.Success(ObservationMessages.Success.Updated(AppLanguage.Current), updated));
    }

    /// <summary>Deletes a swimmer data field. Head Coach or Captain only.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _service.DeleteAsync(id, ct);
        if (!deleted)
        {
            var nf = ApiResponse<object>.Failure(ObservationMessages.Errors.NotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<object>.Success(ObservationMessages.Success.Deleted(AppLanguage.Current), null));
    }
}
```

- [ ] **Step 4: Run the controller tests, then the whole backend suite**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter FullyQualifiedName~ObservationsControllerTests`
Expected: PASS.
Then run the full backend gate (API must be stopped): `dotnet test backend`
Expected: PASS — all suites green, including `ArchitectureTests`.

- [ ] **Step 5: Commit** (when authorized)

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/ObservationsController.cs backend/tests/Kheprx.BaseBackend.Api.UnitTests/ObservationsControllerTests.cs
git commit -m "feat(api): observations GET/PUT/DELETE for records tab"
```

---

## Task 6: Frontend — record model, DTO, mapper

**Files:**
- Create: `frontend/src/app/features/swimmer-profile/domain/model/record-entry.ts`
- Create: `frontend/src/app/features/swimmer-profile/data/dto/record.dto.ts`
- Create: `frontend/src/app/features/swimmer-profile/data/dto/record.mapper.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/data/dto/record.mapper.spec.ts`

**Interfaces:**
- Produces: `RecordEntry`; `RecordDtoRs`, `RecordListDtoRs`, `RecordItemDtoRs`, `DeleteRecordItemDtoRs`, `UpdateRecordDtoRq`, `isRecordDtoRsValid`, `isRecordListValid`; `toRecordEntry`, `toRecordEntryList`.

- [ ] **Step 1: Write the failing mapper test** — create `record.mapper.spec.ts`:

```typescript
import { toRecordEntry, toRecordEntryList } from '@features/swimmer-profile/data/dto/record.mapper';
import { RecordDtoRs } from '@features/swimmer-profile/data/dto/record.dto';

const DTO: RecordDtoRs = {
  id: 'o1', swimmerId: 's1', categoryId: 'c1', fieldLabel: 'Penicillin', value: 'Severe',
  observedDate: '2026-09-20T10:00:00Z', recordedBy: 'u1',
};

describe('record.mapper', () => {
  it('maps a record (drops recordedBy)', () => {
    const m = toRecordEntry(DTO);
    expect(m.id).toBe('o1');
    expect(m.categoryId).toBe('c1');
    expect(m.fieldLabel).toBe('Penicillin');
    expect(m.observedDate).toBe('2026-09-20T10:00:00Z');
    expect((m as unknown as Record<string, unknown>).recordedBy).toBeUndefined();
  });

  it('maps a list', () => {
    expect(toRecordEntryList([DTO])).toHaveLength(1);
  });
});
```

- [ ] **Step 2: Run to verify failure**

Run: `cd frontend && npx jest record.mapper`
Expected: FAIL — cannot resolve `record.dto` / `record.mapper`.

- [ ] **Step 3a: Create the model** — `record-entry.ts`:

```typescript
export interface RecordEntry {
  id: string;
  swimmerId: string;
  categoryId: string;
  fieldLabel: string;
  value: string;
  observedDate: string;
}
```

- [ ] **Step 3b: Create the DTO** — `record.dto.ts`:

```typescript
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface RecordDtoRs {
  id: string;
  swimmerId: string;
  categoryId: string;
  fieldLabel: string;
  value: string;
  observedDate: string;
  recordedBy: string;
}
export interface RecordListDtoRs extends BaseResponseRs<RecordDtoRs[]> {}
export interface RecordItemDtoRs extends BaseResponseRs<RecordDtoRs> {}
export interface DeleteRecordItemDtoRs extends BaseResponseRs<unknown> {}

export interface UpdateRecordDtoRq {
  categoryId: string;
  fieldLabel: string;
  value: string;
}

export function isRecordDtoRsValid(x: unknown): x is RecordDtoRs {
  const d = x as RecordDtoRs;
  if (!d || typeof d !== 'object') return false;
  return typeof d.id === 'string'
    && typeof d.swimmerId === 'string'
    && typeof d.categoryId === 'string'
    && typeof d.fieldLabel === 'string'
    && typeof d.value === 'string'
    && typeof d.observedDate === 'string';
}

export function isRecordListValid(data: unknown): data is RecordDtoRs[] {
  return Array.isArray(data) && data.every(isRecordDtoRsValid);
}
```

- [ ] **Step 3c: Create the mapper** — `record.mapper.ts`:

```typescript
import { RecordDtoRs } from '@features/swimmer-profile/data/dto/record.dto';
import { RecordEntry } from '@features/swimmer-profile/domain/model/record-entry';

export function toRecordEntry(d: RecordDtoRs): RecordEntry {
  return {
    id: d.id,
    swimmerId: d.swimmerId,
    categoryId: d.categoryId,
    fieldLabel: d.fieldLabel,
    value: d.value,
    observedDate: d.observedDate,
  };
}

export function toRecordEntryList(list: RecordDtoRs[]): RecordEntry[] {
  return list.map(toRecordEntry);
}
```

- [ ] **Step 4: Run to verify pass**

Run: `cd frontend && npx jest record.mapper`
Expected: PASS.

- [ ] **Step 5: Commit** (when authorized)

```bash
git add frontend/src/app/features/swimmer-profile/domain/model/record-entry.ts frontend/src/app/features/swimmer-profile/data/dto/record.dto.ts frontend/src/app/features/swimmer-profile/data/dto/record.mapper.ts frontend/src/app/features/swimmer-profile/testing/data/dto/record.mapper.spec.ts
git commit -m "feat(fe): record model, dto, mapper for records tab"
```

---

## Task 7: Frontend — repository methods

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/domain/repositories/swimmer-profile.repository.ts`
- Modify: `frontend/src/app/features/swimmer-profile/data/repositories/swimmer-profile.repository.impl.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/data/repositories/swimmer-profile.repository.impl.spec.ts`

**Interfaces:**
- Consumes: DTOs from Task 6.
- Produces on `ISwimmerProfileRepository`:
  - `listRecords(id: string): Promise<RecordListDtoRs>` → `GET /api/observations?swimmerId={id}`
  - `updateRecord(recordId: string, rq: UpdateRecordDtoRq): Promise<RecordItemDtoRs>` → `PUT /api/observations/{recordId}`
  - `deleteRecord(recordId: string): Promise<DeleteRecordItemDtoRs>` → `DELETE /api/observations/{recordId}`

- [ ] **Step 1: Write the failing repo tests** — append inside the `describe` in `swimmer-profile.repository.impl.spec.ts`:

```typescript
  it('listRecords GETs /api/observations with swimmerId query', async () => {
    (http.get as jest.Mock).mockResolvedValue({ successStatus: true, data: [] });
    await repo.listRecords('s1');
    expect(http.get).toHaveBeenCalledWith('/api/observations?swimmerId=s1');
  });

  it('updateRecord PUTs /api/observations/{recordId} with the body', async () => {
    (http.put as jest.Mock).mockResolvedValue({ successStatus: true, data: {} });
    const rq = { categoryId: 'c1', fieldLabel: 'Penicillin', value: 'Severe' };
    await repo.updateRecord('o1', rq);
    expect(http.put).toHaveBeenCalledWith('/api/observations/o1', { body: rq });
  });

  it('deleteRecord DELETEs /api/observations/{recordId}', async () => {
    ((http as unknown as { delete: jest.Mock }).delete) = jest.fn().mockResolvedValue({ successStatus: true, data: null });
    await repo.deleteRecord('o1');
    expect((http as unknown as { delete: jest.Mock }).delete).toHaveBeenCalledWith('/api/observations/o1');
  });
```

- [ ] **Step 2: Run to verify failure**

Run: `cd frontend && npx jest swimmer-profile.repository.impl`
Expected: FAIL — `repo.listRecords` etc. are not functions.

- [ ] **Step 3a: Extend the port** — in `swimmer-profile.repository.ts`, add to the imports line for `record.dto`:

```typescript
import { RecordListDtoRs, RecordItemDtoRs, DeleteRecordItemDtoRs, UpdateRecordDtoRq } from '@features/swimmer-profile/data/dto/record.dto';
```

and add to the `ISwimmerProfileRepository` interface (after `deleteInBodyReading`):

```typescript
  listRecords(id: string): Promise<RecordListDtoRs>;
  updateRecord(recordId: string, rq: UpdateRecordDtoRq): Promise<RecordItemDtoRs>;
  deleteRecord(recordId: string): Promise<DeleteRecordItemDtoRs>;
```

- [ ] **Step 3b: Implement** — in `swimmer-profile.repository.impl.ts`, add the same import, and add these methods to the class:

```typescript
  listRecords(id: string): Promise<RecordListDtoRs> {
    return this.http.get<RecordListDtoRs>(`/api/observations?swimmerId=${id}`);
  }
  updateRecord(recordId: string, rq: UpdateRecordDtoRq): Promise<RecordItemDtoRs> {
    return this.http.put<RecordItemDtoRs>(`/api/observations/${recordId}`, { body: rq });
  }
  deleteRecord(recordId: string): Promise<DeleteRecordItemDtoRs> {
    return this.http.delete<DeleteRecordItemDtoRs>(`/api/observations/${recordId}`);
  }
```

- [ ] **Step 4: Run to verify pass**

Run: `cd frontend && npx jest swimmer-profile.repository.impl`
Expected: PASS.

- [ ] **Step 5: Commit** (when authorized)

```bash
git add frontend/src/app/features/swimmer-profile/domain/repositories/swimmer-profile.repository.ts frontend/src/app/features/swimmer-profile/data/repositories/swimmer-profile.repository.impl.ts frontend/src/app/features/swimmer-profile/testing/data/repositories/swimmer-profile.repository.impl.spec.ts
git commit -m "feat(fe): swimmer-profile repo records list/update/delete"
```

---

## Task 8: Frontend — record use-cases

**Files:**
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/list-records.use-case.ts`
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/update-record.use-case.ts`
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/delete-record.use-case.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/domain/usecases/records.use-cases.spec.ts`

**Interfaces:**
- Consumes: repo methods (Task 7), DTO validators + mapper (Task 6).
- Produces: `ListRecordsUseCase` (`run(id: string)`), `UpdateRecordUseCase` (`run({ recordId, rq })`, `UpdateRecordInput`), `DeleteRecordUseCase` (`run({ recordId })`, `DeleteRecordInput`).

- [ ] **Step 1: Write the failing use-case tests** — create `records.use-cases.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { ListRecordsUseCase } from '@features/swimmer-profile/domain/usecases/list-records.use-case';
import { UpdateRecordUseCase } from '@features/swimmer-profile/domain/usecases/update-record.use-case';
import { DeleteRecordUseCase } from '@features/swimmer-profile/domain/usecases/delete-record.use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

const REC = { id: 'o1', swimmerId: 's1', categoryId: 'c1', fieldLabel: 'Penicillin', value: 'Severe', observedDate: '2026-09-20T10:00:00Z', recordedBy: 'u1' };
const RQ = { categoryId: 'c1', fieldLabel: 'Penicillin', value: 'Severe' };

describe('records use cases', () => {
  const repo = { listRecords: jest.fn(), updateRecord: jest.fn(), deleteRecord: jest.fn() } as any;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [
      ListRecordsUseCase, UpdateRecordUseCase, DeleteRecordUseCase,
      { provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo },
    ] });
  });

  it('list maps valid records', async () => {
    repo.listRecords.mockResolvedValue({ successStatus: true, data: [REC] });
    const res = await TestBed.inject(ListRecordsUseCase).run('s1');
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data[0].fieldLabel).toBe('Penicillin');
  });

  it('list fails on invalid payload', async () => {
    repo.listRecords.mockResolvedValue({ successStatus: true, data: [{ id: 5 }] });
    const res = await TestBed.inject(ListRecordsUseCase).run('s1');
    expect(res.ok).toBe(false);
  });

  it('update maps the returned record', async () => {
    repo.updateRecord.mockResolvedValue({ successStatus: true, data: REC });
    const res = await TestBed.inject(UpdateRecordUseCase).run({ recordId: 'o1', rq: RQ });
    expect(res.ok).toBe(true);
    expect(repo.updateRecord).toHaveBeenCalledWith('o1', RQ);
  });

  it('delete calls the repo', async () => {
    repo.deleteRecord.mockResolvedValue({ successStatus: true, data: null });
    const res = await TestBed.inject(DeleteRecordUseCase).run({ recordId: 'o1' });
    expect(res.ok).toBe(true);
    expect(repo.deleteRecord).toHaveBeenCalledWith('o1');
  });
});
```

- [ ] **Step 2: Run to verify failure**

Run: `cd frontend && npx jest records.use-cases`
Expected: FAIL — use-case modules not found.

- [ ] **Step 3a: Create `list-records.use-case.ts`:**

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { isRecordListValid } from '@features/swimmer-profile/data/dto/record.dto';
import { toRecordEntryList } from '@features/swimmer-profile/data/dto/record.mapper';
import { RecordEntry } from '@features/swimmer-profile/domain/model/record-entry';

@Injectable({ providedIn: 'root' })
export class ListRecordsUseCase extends UseCase<string, RecordEntry[]> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('ListRecords'); }
  protected async execute(id: string): Promise<RecordEntry[]> {
    const res = await this.repo.listRecords(id);
    if (!isRecordListValid(res.data)) throw new AppError('Invalid records received', 'validation');
    return toRecordEntryList(res.data);
  }
}
```

- [ ] **Step 3b: Create `update-record.use-case.ts`:**

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { UpdateRecordDtoRq, isRecordDtoRsValid } from '@features/swimmer-profile/data/dto/record.dto';
import { toRecordEntry } from '@features/swimmer-profile/data/dto/record.mapper';
import { RecordEntry } from '@features/swimmer-profile/domain/model/record-entry';

export interface UpdateRecordInput { recordId: string; rq: UpdateRecordDtoRq; }

@Injectable({ providedIn: 'root' })
export class UpdateRecordUseCase extends UseCase<UpdateRecordInput, RecordEntry> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('UpdateRecord'); }
  protected async execute(input: UpdateRecordInput): Promise<RecordEntry> {
    const res = await this.repo.updateRecord(input.recordId, input.rq);
    if (!isRecordDtoRsValid(res.data)) throw new AppError('Invalid record received', 'validation');
    return toRecordEntry(res.data);
  }
}
```

- [ ] **Step 3c: Create `delete-record.use-case.ts`:**

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

export interface DeleteRecordInput { recordId: string; }

@Injectable({ providedIn: 'root' })
export class DeleteRecordUseCase extends UseCase<DeleteRecordInput, void> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('DeleteRecord'); }
  protected async execute(input: DeleteRecordInput): Promise<void> {
    await this.repo.deleteRecord(input.recordId);
  }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `cd frontend && npx jest records.use-cases`
Expected: PASS.

- [ ] **Step 5: Commit** (when authorized)

```bash
git add frontend/src/app/features/swimmer-profile/domain/usecases/list-records.use-case.ts frontend/src/app/features/swimmer-profile/domain/usecases/update-record.use-case.ts frontend/src/app/features/swimmer-profile/domain/usecases/delete-record.use-case.ts frontend/src/app/features/swimmer-profile/testing/domain/usecases/records.use-cases.spec.ts
git commit -m "feat(fe): list/update/delete record use-cases"
```

---

## Task 9: Frontend — view-model records state

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `ListRecordsUseCase`, `UpdateRecordUseCase`, `DeleteRecordUseCase` (Task 8), `LoadObservationCategoriesUseCase` (existing), `RecordEntry`, `LookupItem`.
- Produces on the view-model: `records`, `recordCategories`, `loadingRecords`, `editingRecordId`, `confirmingRecordDeleteId`, `savingRecord`, `deletingRecord`, `recCategoryId`, `recLabel`, `recValue` signals; `canSaveRecord`, `recordGroups` computed; `startEditRecord(rec)`, `cancelEditRecord()`, `saveRecord()`, `askDeleteRecord(recordId)`, `cancelDeleteRecord()`, `confirmDeleteRecord()`. `activeTab`/`setTab` gain `'records'`. `recordGroups` shape: `{ categoryId: string; category: LookupItem | null; rows: RecordEntry[] }[]`.

- [ ] **Step 1: Extend the test harness + write failing tests** — in `swimmer-profile.viewmodel.spec.ts`:

  (a) add imports:
```typescript
import { ListRecordsUseCase } from '@features/swimmer-profile/domain/usecases/list-records.use-case';
import { UpdateRecordUseCase } from '@features/swimmer-profile/domain/usecases/update-record.use-case';
import { DeleteRecordUseCase } from '@features/swimmer-profile/domain/usecases/delete-record.use-case';
import { LoadObservationCategoriesUseCase } from '@features/reference/domain/usecases/load-observation-categories.use-case';
```

  (b) inside `build(...)`, before `TestBed.resetTestingModule()`, add the mocks:
```typescript
  const CAT_A = { id: 'c1', code: 'allergy', nameEn: 'Allergy', nameAr: 'حساسية' };
  const CAT_B = { id: 'c2', code: 'surgery', nameEn: 'Surgery', nameAr: 'جراحة' };
  const REC1 = { id: 'o1', swimmerId: 's1', categoryId: 'c1', fieldLabel: 'Penicillin', value: 'Severe', observedDate: '2026-09-10T10:00:00Z' };
  const REC2 = { id: 'o2', swimmerId: 's1', categoryId: 'c1', fieldLabel: 'Pollen', value: 'Mild', observedDate: '2026-09-20T10:00:00Z' };
  const REC3 = { id: 'o3', swimmerId: 's1', categoryId: 'c2', fieldLabel: 'Knee', value: '2019', observedDate: '2026-09-15T10:00:00Z' };
  const listRecordsUc = { run: jest.fn().mockResolvedValue((over as any).listRecords ?? { ok: true, data: [REC2, REC3, REC1] }) };
  const updateRecordUc = { run: jest.fn().mockResolvedValue((over as any).updateRecord ?? { ok: true, data: REC2 }) };
  const deleteRecordUc = { run: jest.fn().mockResolvedValue((over as any).deleteRecord ?? { ok: true, data: undefined }) };
  const loadObsCatsUc = { run: jest.fn().mockResolvedValue((over as any).categories ?? { ok: true, data: [CAT_A, CAT_B] }) };
```

  (c) add to the `providers` array:
```typescript
    { provide: ListRecordsUseCase, useValue: listRecordsUc },
    { provide: UpdateRecordUseCase, useValue: updateRecordUc },
    { provide: DeleteRecordUseCase, useValue: deleteRecordUc },
    { provide: LoadObservationCategoriesUseCase, useValue: loadObsCatsUc },
```

  (d) add to the returned object: `listRecordsUc, updateRecordUc, deleteRecordUc, loadObsCatsUc`.

  (e) append this `describe` block (inside the top-level `describe('SwimmerProfileViewModel', ...)`):
```typescript
  describe('records tab', () => {
    const R = (id: string, cat: string, date: string) => ({ id, swimmerId: 's1', categoryId: cat, fieldLabel: 'L', value: 'V', observedDate: date });

    it('setTab("records") lazy-loads records + categories once and groups newest-first', async () => {
      const { vm, listRecordsUc, loadObsCatsUc } = build();
      await vm.load('s1');
      vm.setTab('records');
      await Promise.resolve(); await Promise.resolve();
      expect(vm.activeTab()).toBe('records');
      expect(listRecordsUc.run).toHaveBeenCalledTimes(1);
      expect(loadObsCatsUc.run).toHaveBeenCalledTimes(1);
      const groups = vm.recordGroups();
      expect(groups).toHaveLength(2);                        // c1 + c2
      expect(groups[0].categoryId).toBe('c1');              // seeded order
      expect(groups[0].rows.map((r) => r.id)).toEqual(['o2', 'o1']); // newest first
      vm.setTab('identityVitals');
      vm.setTab('records');
      await Promise.resolve();
      expect(listRecordsUc.run).toHaveBeenCalledTimes(1);   // not reloaded
    });

    it('canSaveRecord requires category + label + value', async () => {
      const { vm } = build();
      await vm.load('s1');
      vm.startEditRecord(R('o1', 'c1', '2026-09-10T10:00:00Z'));
      expect(vm.canSaveRecord()).toBe(true);
      vm.recValue.set('');
      expect(vm.canSaveRecord()).toBe(false);
    });

    it('saveRecord updates, toasts and reloads', async () => {
      const { vm, updateRecordUc, listRecordsUc, notify } = build();
      await vm.load('s1');
      vm.setTab('records');
      await Promise.resolve(); await Promise.resolve();
      vm.startEditRecord(R('o1', 'c1', '2026-09-10T10:00:00Z'));
      vm.recLabel.set('Penicillin'); vm.recValue.set('Moderate');
      await vm.saveRecord();
      expect(updateRecordUc.run).toHaveBeenCalledWith({ recordId: 'o1', rq: { categoryId: 'c1', fieldLabel: 'Penicillin', value: 'Moderate' } });
      expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.recordSaved');
      expect(vm.editingRecordId()).toBeNull();
      expect(listRecordsUc.run).toHaveBeenCalledTimes(2);
    });

    it('delete flow confirms, deletes, toasts and reloads', async () => {
      const { vm, deleteRecordUc, listRecordsUc, notify } = build();
      await vm.load('s1');
      vm.setTab('records');
      await Promise.resolve(); await Promise.resolve();
      vm.askDeleteRecord('o1');
      expect(vm.confirmingRecordDeleteId()).toBe('o1');
      await vm.confirmDeleteRecord();
      expect(deleteRecordUc.run).toHaveBeenCalledWith({ recordId: 'o1' });
      expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.recordRemoved');
      expect(vm.confirmingRecordDeleteId()).toBeNull();
      expect(listRecordsUc.run).toHaveBeenCalledTimes(2);
    });
  });
```

- [ ] **Step 2: Run to verify failure**

Run: `cd frontend && npx jest swimmer-profile.viewmodel`
Expected: FAIL — DI cannot resolve the new use-cases / `vm.recordGroups` etc. undefined.

- [ ] **Step 3a: Add imports** to `swimmer-profile.viewmodel.ts`:

```typescript
import { ListRecordsUseCase } from '@features/swimmer-profile/domain/usecases/list-records.use-case';
import { UpdateRecordUseCase } from '@features/swimmer-profile/domain/usecases/update-record.use-case';
import { DeleteRecordUseCase } from '@features/swimmer-profile/domain/usecases/delete-record.use-case';
import { LoadObservationCategoriesUseCase } from '@features/reference/domain/usecases/load-observation-categories.use-case';
import { RecordEntry } from '@features/swimmer-profile/domain/model/record-entry';
```

- [ ] **Step 3b: Inject the use-cases** — add near the other `inject(...)` lines:

```typescript
  private readonly listRecordsUc = inject(ListRecordsUseCase);
  private readonly updateRecordUc = inject(UpdateRecordUseCase);
  private readonly deleteRecordUc = inject(DeleteRecordUseCase);
  private readonly loadObservationCategories = inject(LoadObservationCategoriesUseCase);
```

- [ ] **Step 3c: Widen the tab type** — change the `activeTab` signal declaration to:

```typescript
  readonly activeTab = signal<'identityVitals' | 'guardian' | 'physiological' | 'inbody' | 'records'>('identityVitals');
```

and add the loaded flag next to `inbodyLoaded`:

```typescript
  private recordsLoaded = false;
```

- [ ] **Step 3d: Add records state + computed** — after the InBody state block (after `inbodyHistory`):

```typescript
  // Records state
  readonly records = signal<RecordEntry[]>([]);
  readonly recordCategories = signal<LookupItem[]>([]);
  readonly loadingRecords = signal(false);
  readonly editingRecordId = signal<string | null>(null);
  readonly confirmingRecordDeleteId = signal<string | null>(null);
  readonly savingRecord = signal(false);
  readonly deletingRecord = signal(false);
  readonly recCategoryId = signal(''); readonly recLabel = signal(''); readonly recValue = signal('');

  readonly canSaveRecord = computed(() => {
    const label = this.recLabel().trim(), value = this.recValue().trim();
    return this.recCategoryId().length > 0
      && label.length > 0 && label.length <= 200
      && value.length > 0 && value.length <= 500;
  });

  readonly recordGroups = computed(() => {
    const cats = this.recordCategories();
    const byId = new Map(cats.map((c) => [c.id, c]));
    const order = new Map(cats.map((c, i) => [c.id, i]));
    const groups = new Map<string, { category: LookupItem | null; rows: RecordEntry[] }>();
    for (const rec of this.records()) {
      let g = groups.get(rec.categoryId);
      if (!g) { g = { category: byId.get(rec.categoryId) ?? null, rows: [] }; groups.set(rec.categoryId, g); }
      g.rows.push(rec);
    }
    const result = [...groups.entries()].map(([categoryId, g]) => ({ categoryId, category: g.category, rows: g.rows }));
    for (const g of result) g.rows.sort((a, b) => b.observedDate.localeCompare(a.observedDate)); // newest first
    result.sort((a, b) => (order.get(a.categoryId) ?? 999) - (order.get(b.categoryId) ?? 999));   // seeded category order
    return result;
  });
```

- [ ] **Step 3e: Reset records in `load()`** — inside `load(id)`, alongside the InBody resets (after `this.confirmingInBodyDelete.set(false);`):

```typescript
    this.recordsLoaded = false;
    this.records.set([]);
    this.recordCategories.set([]);
    this.editingRecordId.set(null);
    this.confirmingRecordDeleteId.set(null);
```

- [ ] **Step 3f: Lazy-load in `setTab`** — widen the signature and add the branch:

```typescript
  setTab(key: 'identityVitals' | 'guardian' | 'physiological' | 'inbody' | 'records'): void {
    this.activeTab.set(key);
    if (key === 'guardian' && !this.guardiansLoaded) void this.loadGuardians();
    if (key === 'physiological' && !this.bodyMeasurementLoaded) void this.loadBodyMeasurement();
    if (key === 'inbody' && !this.inbodyLoaded) void this.loadInBody();
    if (key === 'records' && !this.recordsLoaded) void this.loadRecords();
  }
```

- [ ] **Step 3g: Add the records methods** — after `confirmDeleteInBody()` (before the class closing brace):

```typescript
  private async loadRecords(): Promise<void> {
    this.recordsLoaded = true;
    this.loadingRecords.set(true);
    const [recRes, catRes] = await Promise.all([
      this.listRecordsUc.run(this.swimmerId),
      this.loadObservationCategories.run(),
    ]);
    this.loadingRecords.set(false);
    if (catRes.ok) this.recordCategories.set(catRes.data);
    if (recRes.ok) {
      this.records.set(recRes.data);
    } else {
      this.recordsLoaded = false;
      this.records.set([]);
    }
  }

  startEditRecord(rec: RecordEntry): void {
    this.confirmingRecordDeleteId.set(null);
    this.editingRecordId.set(rec.id);
    this.recCategoryId.set(rec.categoryId);
    this.recLabel.set(rec.fieldLabel);
    this.recValue.set(rec.value);
  }

  cancelEditRecord(): void { this.editingRecordId.set(null); }

  async saveRecord(): Promise<void> {
    const recordId = this.editingRecordId();
    if (!recordId || !this.canSaveRecord() || this.savingRecord()) return;
    this.savingRecord.set(true);
    const rq = { categoryId: this.recCategoryId(), fieldLabel: this.recLabel().trim(), value: this.recValue().trim() };
    const r = await this.updateRecordUc.run({ recordId, rq });
    this.savingRecord.set(false);
    if (r.ok) {
      this.notify.success(this.i18n.t('swimmerProfile.toasts.recordSaved'));
      this.editingRecordId.set(null);
      this.recordsLoaded = false;
      await this.loadRecords();
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.saveFailed'));
    }
  }

  askDeleteRecord(recordId: string): void { this.editingRecordId.set(null); this.confirmingRecordDeleteId.set(recordId); }
  cancelDeleteRecord(): void { this.confirmingRecordDeleteId.set(null); }

  async confirmDeleteRecord(): Promise<void> {
    const recordId = this.confirmingRecordDeleteId();
    if (!recordId || this.deletingRecord()) return;
    this.deletingRecord.set(true);
    const res = await this.deleteRecordUc.run({ recordId });
    this.deletingRecord.set(false);
    if (res.ok) {
      this.notify.success(this.i18n.t('swimmerProfile.toasts.recordRemoved'));
      this.confirmingRecordDeleteId.set(null);
      this.recordsLoaded = false;
      await this.loadRecords();
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.deleteFailed'));
    }
  }
```

- [ ] **Step 4: Run to verify pass**

Run: `cd frontend && npx jest swimmer-profile.viewmodel`
Expected: PASS (records describe + all existing tests).

- [ ] **Step 5: Commit** (when authorized)

```bash
git add frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.ts frontend/src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts
git commit -m "feat(fe): records tab view-model state, grouping, edit/remove"
```

---

## Task 10: Frontend — enable tab, template, i18n (+ build/manual gate)

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.ts`
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.html`
- Modify: `frontend/src/app/core/i18n/en.json`, `frontend/src/app/core/i18n/ar.json`

**Interfaces:**
- Consumes: view-model API from Task 9; existing `refLabel(...)` helper.
- Produces: `fmtDate(iso: string): string` helper; `'records'` in `enabledTabs`; the Records section markup; `swimmerProfile.records.*` + two new toasts in both locales.

- [ ] **Step 1: Enable the tab + add the date helper** — in `swimmer-profile.page.ts`:

  (a) change `enabledTabs`:
```typescript
  protected readonly enabledTabs = new Set(['identityVitals', 'guardian', 'physiological', 'inbody', 'records']);
```

  (b) add a helper method to the class (next to `refLabel`):
```typescript
  fmtDate(iso: string): string { return iso ? iso.slice(0, 10) : '—'; }
```

- [ ] **Step 2: Add the i18n keys.**

  (a) `en.json` — inside `swimmerProfile`, add a `records` object after the `inbody` object (after its closing `},` on the `inbody` block):
```json
    "records": {
      "intro": "Recorded data fields, grouped by category.",
      "field": "Field",
      "value": "Value",
      "date": "Date",
      "category": "Category",
      "edit": "Edit",
      "remove": "Remove",
      "confirmDelete": "Remove this record?",
      "noRecords": "No records yet."
    },
```
  and add to `swimmerProfile.toasts` (after `"readingDeleted": "Reading deleted"` — add a comma):
```json
      "recordSaved": "Record saved",
      "recordRemoved": "Record removed"
```

  (b) `ar.json` — inside `swimmerProfile`, add after the `inbody` object:
```json
    "records": {
      "intro": "بيانات مسجّلة، مجمّعة حسب الفئة.",
      "field": "الحقل",
      "value": "القيمة",
      "date": "التاريخ",
      "category": "الفئة",
      "edit": "تحرير",
      "remove": "حذف",
      "confirmDelete": "حذف هذا السجل؟",
      "noRecords": "لا توجد سجلات بعد."
    },
```
  and add to `swimmerProfile.toasts` (after `"readingDeleted": "تم حذف القياس"` — add a comma):
```json
      "recordSaved": "تم حفظ السجل",
      "recordRemoved": "تم حذف السجل"
```

- [ ] **Step 3: Add the Records section** — in `swimmer-profile.page.html`, insert this block immediately after the `@if (vm.activeTab() === 'inbody') { ... }` block (before the final `}` that closes `@else if (vm.profile(); as p)`):

```html
    @if (vm.activeTab() === 'records') {
      <section class="rounded-2xl border border-border bg-surface p-6 shadow-sm">
        <div class="mb-4 border-b border-border pb-3">
          <h2 class="font-heading text-lg text-ink">{{ 'swimmerProfile.tabs.records' | translate }}</h2>
          <p class="mt-0.5 text-sm text-text-secondary">{{ 'swimmerProfile.records.intro' | translate }}</p>
        </div>

        @if (vm.loadingRecords()) {
          <p class="text-sm text-text-secondary">{{ 'swimmerProfile.states.loading' | translate }}</p>
        } @else if (vm.recordGroups().length === 0) {
          <p class="text-sm text-text-secondary">{{ 'swimmerProfile.records.noRecords' | translate }}</p>
        } @else {
          <div class="space-y-6">
            @for (group of vm.recordGroups(); track group.categoryId) {
              <div>
                <h3 class="mb-2 flex items-center gap-2 text-base font-semibold text-ink">
                  <span>{{ refLabel(group.category) }}</span>
                  <span class="text-xs font-normal text-text-secondary">({{ group.rows.length }})</span>
                </h3>
                <ul class="divide-y divide-border rounded-lg border border-border">
                  @for (rec of group.rows; track rec.id) {
                    <li class="px-4 py-3 text-sm">
                      @if (vm.editingRecordId() === rec.id) {
                        <form class="grid grid-cols-1 gap-3 md:grid-cols-4" (submit)="$event.preventDefault(); vm.saveRecord()">
                          <label class="flex flex-col gap-1">
                            <span class="text-xs font-bold text-text-secondary">{{ 'swimmerProfile.records.category' | translate }}</span>
                            <select class="h-10 rounded-md border border-input bg-card px-2 text-sm text-ink" [value]="vm.recCategoryId()" (change)="vm.recCategoryId.set($any($event.target).value)">
                              @for (c of vm.recordCategories(); track c.id) { <option [value]="c.id">{{ refLabel(c) }}</option> }
                            </select>
                          </label>
                          <app-text-field [label]="'swimmerProfile.records.field' | translate" [value]="vm.recLabel()" (valueChange)="vm.recLabel.set($event)"></app-text-field>
                          <app-text-field [label]="'swimmerProfile.records.value' | translate" [value]="vm.recValue()" (valueChange)="vm.recValue.set($event)"></app-text-field>
                          <div class="flex items-end gap-2">
                            <button type="submit" class="rounded-md bg-primary px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" [disabled]="!vm.canSaveRecord() || vm.savingRecord()">{{ 'common.save' | translate }}</button>
                            <button type="button" class="rounded-md px-4 py-2 text-sm text-text-secondary" (click)="vm.cancelEditRecord()">{{ 'common.cancel' | translate }}</button>
                          </div>
                        </form>
                      } @else {
                        <div class="flex flex-wrap items-center gap-3">
                          <div class="min-w-0 flex-1">
                            <p class="font-medium text-ink">{{ rec.fieldLabel }}</p>
                            <p class="text-text-secondary">{{ rec.value }}</p>
                          </div>
                          <span class="text-xs text-text-secondary">{{ fmtDate(rec.observedDate) }}</span>
                          @if (vm.canEdit()) {
                            @if (vm.confirmingRecordDeleteId() === rec.id) {
                              <span class="text-sm text-danger">{{ 'swimmerProfile.records.confirmDelete' | translate }}</span>
                              <button type="button" class="text-sm font-medium text-danger disabled:opacity-50" [disabled]="vm.deletingRecord()" (click)="vm.confirmDeleteRecord()">{{ 'swimmerProfile.confirmYes' | translate }}</button>
                              <button type="button" class="text-sm text-text-secondary" (click)="vm.cancelDeleteRecord()">{{ 'swimmerProfile.confirmNo' | translate }}</button>
                            } @else {
                              <button type="button" class="text-sm font-medium text-primary" (click)="vm.startEditRecord(rec)">{{ 'swimmerProfile.records.edit' | translate }}</button>
                              <button type="button" class="text-sm font-medium text-danger" (click)="vm.askDeleteRecord(rec.id)">{{ 'swimmerProfile.records.remove' | translate }}</button>
                            }
                          }
                        </div>
                      }
                    </li>
                  }
                </ul>
              </div>
            }
          </div>
        }
      </section>
    }
```

- [ ] **Step 4: Verify the frontend build + full suite, then manual walkthrough**

Run: `cd frontend && npx jest` — Expected: all specs green.
Run: `cd frontend && npm run build` — Expected: build succeeds (no template/type errors).
Manual (dev server + logged in as head_coach/captain, on a swimmer with observations):
  - Open `swimmers/:id` → click **Records**: records appear grouped by category, newest-first, each with field label · value · date.
  - **Edit** a record: change category/label/value → Save → success toast, row reflects the change.
  - **Remove** a record: confirm → success toast, row disappears.
  - Swimmer with no observations → empty-state text.
  - As a non-coach role → no Edit/Remove buttons.

- [ ] **Step 5: Commit** (when authorized)

```bash
git add frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.ts frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.html frontend/src/app/core/i18n/en.json frontend/src/app/core/i18n/ar.json
git commit -m "feat(fe): Records tab UI + i18n on swimmer profile"
```

---

## Self-Review

**1. Spec coverage**
- All-categories, grouped by category, newest-first → Task 9 `recordGroups`. ✓
- Real fields only (label · value · date) → Task 10 template; no range/status/trend. ✓
- Read + edit + delete only, no Add → Tasks 5/7/8/9/10 (no create path added). ✓
- Edit = label + value + category; date/recorder immutable → Task 1 `Update`, Task 4 service, Task 9 form. ✓
- Flat routes `GET ?swimmerId` / `PUT {id}` / `DELETE {id}` → Tasks 5/7. ✓
- Auth GET any / writes coach → Task 5 attributes. ✓
- Category names client-side via existing reference endpoint → Task 9 `loadRecords` + `LoadObservationCategoriesUseCase`. ✓
- Empty groups hidden + global empty-state + uncategorized fallback → Task 9 (`recordGroups` only builds groups that have rows; `category: null` when unknown) + Task 10 (`noRecords`, `refLabel(null)` → '—'). ✓
- Tests backend (entity/repo/service/controller/validator) + frontend (mapper/repo/use-cases/viewmodel) → Tasks 1–9. ✓
- No schema change / no commits → no migration task; commit steps gated. ✓

**2. Placeholder scan:** No TBD/TODO; every code step has concrete code. ✓

**3. Type consistency:**
- Backend: `UpdateObservationRequest(CategoryId, FieldLabel, Value)` identical across DTO (T3), validator (T3), service (T4), controller (T5), tests. `UpdateAsync(Guid id, UpdateObservationRequest, ct)` / `DeleteAsync(Guid id, ct)` / `ListBySwimmerAsync(Guid, ct)` consistent between `IObservationService`/`ObservationService`/tests. Repo `ListBySwimmerAsync`/`GetTrackedAsync`/`Remove` consistent between interface/impl/service/tests. ✓
- Frontend: `RecordEntry` fields match `RecordDtoRs` (minus `recordedBy`) and the mapper. Repo `listRecords(id)`/`updateRecord(recordId, rq)`/`deleteRecord(recordId)` identical across port/impl/use-cases/viewmodel/tests. `UpdateRecordInput { recordId, rq }` and `DeleteRecordInput { recordId }` match the viewmodel calls (`{ recordId, rq }`, `{ recordId }`). `recordGroups` item shape `{ categoryId, category, rows }` matches template (`group.categoryId`, `refLabel(group.category)`, `group.rows`). i18n keys used in the template (`swimmerProfile.records.*`, `swimmerProfile.toasts.recordSaved/recordRemoved`) are all added in Task 10. ✓

Consistent; no gaps found.
