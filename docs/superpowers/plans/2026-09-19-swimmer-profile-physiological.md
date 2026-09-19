# Swimmer Profile — Physiological tab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a third Swimmer-Profile tab, **Physiological**, showing a swimmer's latest body measurements with a coach-only edit that records a new dated measurement.

**Architecture:** New `athlete.body_measurement` table in the Identity module (append-only, latest shown). Backend read/write folds into the existing `ISwimmerProfileRepository` / `SwimmerService` / `SwimmersController` exactly as the medical-exam and guardian slices do. Frontend adds an isolated body-measurement domain/data/usecase set and wires the `physiological` tab into the existing `swimmer-profile` slice (viewmodel + page).

**Tech Stack:** .NET 10 (C#, EF Core, FluentValidation, xUnit + Moq), Angular (standalone components, signals, Jest), PostgreSQL.

**Spec:** `docs/superpowers/specs/2026-09-19-swimmer-profile-physiological-design.md`

## Global Constraints

- **No commits.** The user instructed: do not commit anything until they say so. Each task ends by **staging** its files (`git add`), never `git commit`. All work stays on the current branch `feat/swimmer-profile-guardian`, stacked on the uncommitted Guardian work.
- **Dated table, latest shown, append-only.** GET returns the latest row; POST always inserts a NEW row dated today (server clock). No update/delete of past rows this pass.
- **Server-set date.** `measured_at` is set inside the entity constructor to `DateOnly.FromDateTime(DateTime.UtcNow)`; there is no date field in the request or form.
- **Seven values, all required.** Columns `right_arm_cm, left_arm_cm, right_leg_cm, left_leg_cm, torso_cm, bust_diameter_cm, waist_diameter_cm`, each `numeric(5,1)`. Validation (client + server): each is required, `> 0`, and `≤ 999.9`.
- **Auth.** GET `[Authorize]`; POST `[Authorize(Roles="head_coach,captain")]`. Unknown swimmer id ⇒ 404 on both.
- **Canonical field order** everywhere backend/DTO/request: RightArm, LeftArm, RightLeg, LeftLeg, Torso, BustDiameter, WaistDiameter (matches DB columns). UI markup renders rows in the mockup's visual order (RightArm, LeftArm, LeftLeg, RightLeg, Torso, BustDiameter, WaistDiameter) — order is independent of the model.
- **Stop the running backend before `dotnet build`/`dotnet test`/`dotnet ef`** — a running API locks the module DLLs (see memory: dotnet-test-dev-server-lock).
- Backend test run (from repo root), e.g.: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~<ClassName>"`.
- Frontend test run (from repo root): `cd frontend && npx jest <spec-path>`.

---

### Task 1: `BodyMeasurement` domain entity

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/BodyMeasurement.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/BodyMeasurementTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `BodyMeasurement(Guid swimmerId, decimal rightArmCm, decimal leftArmCm, decimal rightLegCm, decimal leftLegCm, decimal torsoCm, decimal bustDiameterCm, decimal waistDiameterCm)` with read-only props `Id, SwimmerId, MeasuredAt (DateOnly), RightArmCm, LeftArmCm, RightLegCm, LeftLegCm, TorsoCm, BustDiameterCm, WaistDiameterCm`. Ctor sets `Id = Guid.NewGuid()` and `MeasuredAt = DateOnly.FromDateTime(DateTime.UtcNow)`.

- [ ] **Step 1: Write the failing test**

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class BodyMeasurementTests
{
    [Fact]
    public void Ctor_sets_values_new_id_and_today()
    {
        var swimmerId = Guid.NewGuid();

        var m = new BodyMeasurement(swimmerId, 78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m);

        Assert.NotEqual(Guid.Empty, m.Id);
        Assert.Equal(swimmerId, m.SwimmerId);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), m.MeasuredAt);
        Assert.Equal(78.5m, m.RightArmCm);
        Assert.Equal(78.2m, m.LeftArmCm);
        Assert.Equal(96.2m, m.RightLegCm);
        Assert.Equal(96.0m, m.LeftLegCm);
        Assert.Equal(52.8m, m.TorsoCm);
        Assert.Equal(94.0m, m.BustDiameterCm);
        Assert.Equal(76.5m, m.WaistDiameterCm);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~BodyMeasurementTests"`
Expected: FAIL — `BodyMeasurement` does not exist (compile error).

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class BodyMeasurement
{
    public Guid Id { get; private set; }
    public Guid SwimmerId { get; private set; }
    public DateOnly MeasuredAt { get; private set; }
    public decimal RightArmCm { get; private set; }
    public decimal LeftArmCm { get; private set; }
    public decimal RightLegCm { get; private set; }
    public decimal LeftLegCm { get; private set; }
    public decimal TorsoCm { get; private set; }
    public decimal BustDiameterCm { get; private set; }
    public decimal WaistDiameterCm { get; private set; }

    private BodyMeasurement() { } // EF Core

    public BodyMeasurement(Guid swimmerId, decimal rightArmCm, decimal leftArmCm, decimal rightLegCm,
        decimal leftLegCm, decimal torsoCm, decimal bustDiameterCm, decimal waistDiameterCm)
    {
        Id = Guid.NewGuid();
        SwimmerId = swimmerId;
        MeasuredAt = DateOnly.FromDateTime(DateTime.UtcNow);
        RightArmCm = rightArmCm;
        LeftArmCm = leftArmCm;
        RightLegCm = rightLegCm;
        LeftLegCm = leftLegCm;
        TorsoCm = torsoCm;
        BustDiameterCm = bustDiameterCm;
        WaistDiameterCm = waistDiameterCm;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~BodyMeasurementTests"`
Expected: PASS.

- [ ] **Step 5: Stage (do not commit)**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/BodyMeasurement.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/BodyMeasurementTests.cs
```

---

### Task 2: Persistence — read model, EF config, DbSet, repository, migration

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/ReadModels/BodyMeasurementRow.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/BodyMeasurementConfiguration.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentityDbContext.cs` (add `DbSet<BodyMeasurement>`)
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/ISwimmerProfileRepository.cs` (add 2 methods)
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/SwimmerProfileRepository.cs` (implement 2 methods)
- Create (generated): migration `..._AddBodyMeasurement.cs` (+ `.Designer.cs`) + snapshot update under `.../Infrastructure/Migrations/`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/SwimmerProfileRepositoryTests.cs` (add tests)

**Interfaces:**
- Consumes: `BodyMeasurement` (Task 1).
- Produces:
  - `BodyMeasurementRow(Guid Id, DateOnly MeasuredAt, decimal RightArmCm, decimal LeftArmCm, decimal RightLegCm, decimal LeftLegCm, decimal TorsoCm, decimal BustDiameterCm, decimal WaistDiameterCm)`.
  - `ISwimmerProfileRepository.GetLatestBodyMeasurementAsync(Guid swimmerId, CancellationToken) → Task<BodyMeasurementRow?>` (latest by `MeasuredAt` desc, `Id` desc; null when none).
  - `ISwimmerProfileRepository.AddBodyMeasurementAsync(BodyMeasurement measurement, CancellationToken) → Task`.
  - `IdentityDbContext.BodyMeasurements` DbSet.

- [ ] **Step 1: Write the read model** (no test of its own — a plain record; covered by repository tests)

```csharp
namespace Kheprx.BaseBackend.Identity.Domain.ReadModels;

/// <summary>Flattened latest body-measurement row.</summary>
public sealed record BodyMeasurementRow(
    Guid Id,
    DateOnly MeasuredAt,
    decimal RightArmCm,
    decimal LeftArmCm,
    decimal RightLegCm,
    decimal LeftLegCm,
    decimal TorsoCm,
    decimal BustDiameterCm,
    decimal WaistDiameterCm);
```

- [ ] **Step 2: Write the failing repository tests**

Add to `SwimmerProfileRepositoryTests.cs` (this file already has `private static IdentityDbContext NewDb() => new(new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);` and `using` for entities/repository). Append:

```csharp
    [Fact]
    public async Task GetLatestBodyMeasurementAsync_returns_null_when_none()
    {
        await using var db = NewDb();
        var repo = new SwimmerProfileRepository(db);

        Assert.Null(await repo.GetLatestBodyMeasurementAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetLatestBodyMeasurementAsync_returns_a_deterministic_row_when_several_exist()
    {
        await using var db = NewDb();
        var swimmerId = Guid.NewGuid();
        // The entity stamps MeasuredAt = today, so both rows share a date; the OrderBy(MeasuredAt desc).ThenBy(Id desc)
        // tiebreak makes the lookup deterministic and non-null. (Cross-day ordering is exercised via the DB, not in-memory.)
        db.BodyMeasurements.Add(new BodyMeasurement(swimmerId, 70m, 70m, 90m, 90m, 50m, 90m, 70m));
        db.BodyMeasurements.Add(new BodyMeasurement(swimmerId, 78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m));
        await db.SaveChangesAsync();
        var repo = new SwimmerProfileRepository(db);

        var row = await repo.GetLatestBodyMeasurementAsync(swimmerId);

        Assert.NotNull(row);
        Assert.Contains(row!.RightArmCm, new[] { 70m, 78.5m }); // one of the two same-day rows (the Id-max)
    }

    [Fact]
    public async Task AddBodyMeasurementAsync_inserts_and_is_readable()
    {
        await using var db = NewDb();
        var swimmerId = Guid.NewGuid();
        var repo = new SwimmerProfileRepository(db);

        await repo.AddBodyMeasurementAsync(new BodyMeasurement(swimmerId, 78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m));
        await db.SaveChangesAsync();

        var row = await repo.GetLatestBodyMeasurementAsync(swimmerId);
        Assert.NotNull(row);
        Assert.Equal(76.5m, row!.WaistDiameterCm);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), row.MeasuredAt);
    }
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~SwimmerProfileRepositoryTests"`
Expected: FAIL — `BodyMeasurements`, `GetLatestBodyMeasurementAsync`, `AddBodyMeasurementAsync` don't exist (compile errors).

- [ ] **Step 4: Register the DbSet**

In `IdentityDbContext.cs`, beside `public DbSet<Guardian> Guardians => Set<Guardian>();` add:

```csharp
    public DbSet<BodyMeasurement> BodyMeasurements => Set<BodyMeasurement>();
```

- [ ] **Step 5: Write the EF configuration**

`BodyMeasurementConfiguration.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class BodyMeasurementConfiguration : IEntityTypeConfiguration<BodyMeasurement>
{
    public void Configure(EntityTypeBuilder<BodyMeasurement> builder)
    {
        builder.ToTable("body_measurement", "athlete");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.SwimmerId).IsRequired();
        builder.Property(m => m.MeasuredAt).IsRequired();
        builder.Property(m => m.RightArmCm).HasPrecision(5, 1).IsRequired();
        builder.Property(m => m.LeftArmCm).HasPrecision(5, 1).IsRequired();
        builder.Property(m => m.RightLegCm).HasPrecision(5, 1).IsRequired();
        builder.Property(m => m.LeftLegCm).HasPrecision(5, 1).IsRequired();
        builder.Property(m => m.TorsoCm).HasPrecision(5, 1).IsRequired();
        builder.Property(m => m.BustDiameterCm).HasPrecision(5, 1).IsRequired();
        builder.Property(m => m.WaistDiameterCm).HasPrecision(5, 1).IsRequired();
        builder.HasIndex(m => new { m.SwimmerId, m.MeasuredAt });
        builder.HasOne<SwimmerProfile>().WithMany().HasForeignKey(m => m.SwimmerId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 6: Add the repository interface methods**

In `ISwimmerProfileRepository.cs`, after `Task AddGuardianAsync(Guardian guardian, CancellationToken ct = default);` add:

```csharp
    Task<BodyMeasurementRow?> GetLatestBodyMeasurementAsync(Guid swimmerId, CancellationToken ct = default);
    Task AddBodyMeasurementAsync(BodyMeasurement measurement, CancellationToken ct = default);
```

- [ ] **Step 7: Implement the repository methods**

In `SwimmerProfileRepository.cs`, after `AddGuardianAsync` (before the private `ExamRows()` helper) add:

```csharp
    public Task<BodyMeasurementRow?> GetLatestBodyMeasurementAsync(Guid swimmerId, CancellationToken ct = default)
        => _db.BodyMeasurements.AsNoTracking()
              .Where(m => m.SwimmerId == swimmerId)
              .OrderByDescending(m => m.MeasuredAt).ThenByDescending(m => m.Id)
              .Select(m => new BodyMeasurementRow(
                  m.Id, m.MeasuredAt,
                  m.RightArmCm, m.LeftArmCm, m.RightLegCm, m.LeftLegCm,
                  m.TorsoCm, m.BustDiameterCm, m.WaistDiameterCm))
              .FirstOrDefaultAsync(ct);

    public Task AddBodyMeasurementAsync(BodyMeasurement measurement, CancellationToken ct = default)
        => _db.BodyMeasurements.AddAsync(measurement, ct).AsTask();
```

- [ ] **Step 8: Run the repository tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~SwimmerProfileRepositoryTests"`
Expected: PASS.

- [ ] **Step 9: Generate the migration (do NOT apply it)**

Ensure the API is not running (DLL lock), then run from repo root:

```bash
dotnet ef migrations add AddBodyMeasurement \
  --project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --startup-project backend/Kheprx.BaseBackend.Api \
  --context IdentityDbContext
```

Open the generated `..._AddBodyMeasurement.cs` and confirm it creates `athlete.body_measurement` with the seven `numeric(5,1)` columns, `measured_at date`, a `(swimmer_id, measured_at)` index, and the `swimmer_id` FK. **Do not** run `dotnet ef database update`.

- [ ] **Step 10: Stage (do not commit)**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/ReadModels/BodyMeasurementRow.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/BodyMeasurementConfiguration.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentityDbContext.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/ISwimmerProfileRepository.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/SwimmerProfileRepository.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Migrations/ \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/SwimmerProfileRepositoryTests.cs
```

---

### Task 3: Application DTOs + validator

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/BodyMeasurementDtos.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Validators/BodyMeasurementRequestValidators.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Validators/CreateBodyMeasurementRequestValidatorTests.cs`

**Interfaces:**
- Consumes: `SwimmerMessages.Errors.MeasurementInvalid` (already exists).
- Produces:
  - `BodyMeasurementDto(Guid Id, DateOnly MeasuredAt, decimal RightArmCm, decimal LeftArmCm, decimal RightLegCm, decimal LeftLegCm, decimal TorsoCm, decimal BustDiameterCm, decimal WaistDiameterCm)`.
  - `SwimmerBodyMeasurementDto(BodyMeasurementDto? Latest)`.
  - `CreateBodyMeasurementRequest(decimal RightArmCm, decimal LeftArmCm, decimal RightLegCm, decimal LeftLegCm, decimal TorsoCm, decimal BustDiameterCm, decimal WaistDiameterCm)`.
  - `CreateBodyMeasurementRequestValidator`.

- [ ] **Step 1: Write the DTOs**

```csharp
namespace Kheprx.BaseBackend.Identity.Application.DTOs;

/// <summary>One body-measurement row for the read shape.</summary>
public sealed record BodyMeasurementDto(
    Guid Id,
    DateOnly MeasuredAt,
    decimal RightArmCm,
    decimal LeftArmCm,
    decimal RightLegCm,
    decimal LeftLegCm,
    decimal TorsoCm,
    decimal BustDiameterCm,
    decimal WaistDiameterCm);

/// <summary>A swimmer's latest body measurement (null when none recorded yet).</summary>
public sealed record SwimmerBodyMeasurementDto(BodyMeasurementDto? Latest);

/// <summary>Payload to record a new dated body measurement (POST). Date is server-set.</summary>
public sealed record CreateBodyMeasurementRequest(
    decimal RightArmCm,
    decimal LeftArmCm,
    decimal RightLegCm,
    decimal LeftLegCm,
    decimal TorsoCm,
    decimal BustDiameterCm,
    decimal WaistDiameterCm);
```

- [ ] **Step 2: Write the failing validator test**

```csharp
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Validators;

public class CreateBodyMeasurementRequestValidatorTests
{
    private readonly CreateBodyMeasurementRequestValidator _validator = new();

    private static CreateBodyMeasurementRequest Valid()
        => new(78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m);

    [Fact]
    public void Valid_request_passes()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Fact]
    public void Zero_value_fails()
    {
        var req = Valid() with { RightArmCm = 0m };
        var result = _validator.Validate(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "RightArmCm");
    }

    [Fact]
    public void Negative_value_fails()
    {
        var req = Valid() with { TorsoCm = -1m };
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Out_of_range_value_fails()
    {
        var req = Valid() with { WaistDiameterCm = 1000m };
        var result = _validator.Validate(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "WaistDiameterCm");
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~CreateBodyMeasurementRequestValidatorTests"`
Expected: FAIL — validator does not exist.

- [ ] **Step 4: Write the validator**

```csharp
using FluentValidation;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Identity.Application.Validators;

public sealed class CreateBodyMeasurementRequestValidator : AbstractValidator<CreateBodyMeasurementRequest>
{
    public CreateBodyMeasurementRequestValidator()
    {
        Rule(x => x.RightArmCm);
        Rule(x => x.LeftArmCm);
        Rule(x => x.RightLegCm);
        Rule(x => x.LeftLegCm);
        Rule(x => x.TorsoCm);
        Rule(x => x.BustDiameterCm);
        Rule(x => x.WaistDiameterCm);
    }

    private void Rule(System.Linq.Expressions.Expression<System.Func<CreateBodyMeasurementRequest, decimal>> selector)
        => RuleFor(selector)
            .GreaterThan(0m)
            .LessThanOrEqualTo(999.9m)
            .WithMessage(_ => SwimmerMessages.Errors.MeasurementInvalid(AppLanguage.Current));
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~CreateBodyMeasurementRequestValidatorTests"`
Expected: PASS.

- [ ] **Step 6: Stage (do not commit)**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/BodyMeasurementDtos.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Validators/BodyMeasurementRequestValidators.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Validators/CreateBodyMeasurementRequestValidatorTests.cs
```

---

### Task 4: Service methods + messages

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/ISwimmerService.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Resources/SwimmerMessages.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs` (add tests)

**Interfaces:**
- Consumes: `ISwimmerProfileRepository.GetByIdTrackedAsync`, `.GetLatestBodyMeasurementAsync`, `.AddBodyMeasurementAsync`, `.SaveChangesAsync` (Task 2); DTOs (Task 3); `BodyMeasurement` (Task 1).
- Produces:
  - `ISwimmerService.GetBodyMeasurementAsync(Guid id, CancellationToken) → Task<SwimmerBodyMeasurementDto?>` (null ⇒ swimmer not found).
  - `ISwimmerService.AddBodyMeasurementAsync(Guid id, CreateBodyMeasurementRequest req, CancellationToken) → Task<bool>` (false ⇒ swimmer not found).
  - `SwimmerMessages.Success.BodyMeasurementRetrieved(string)`, `.BodyMeasurementSaved(string)`.

- [ ] **Step 1: Write the failing service tests**

Append to `SwimmerServiceTests.cs` (uses the existing `Build()` helper, `Moq`, `ReadModels`):

```csharp
    // ── Body measurement tests ───────────────────────────────────────────────

    [Fact]
    public async Task GetBodyMeasurementAsync_returns_null_when_swimmer_missing()
    {
        var (svc, swimmers, _) = Build();
        var id = Guid.NewGuid();
        swimmers.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((SwimmerProfile?)null);

        Assert.Null(await svc.GetBodyMeasurementAsync(id));
    }

    [Fact]
    public async Task GetBodyMeasurementAsync_returns_wrapper_with_null_latest_when_no_rows()
    {
        var (svc, swimmers, _) = Build();
        var id = Guid.NewGuid();
        swimmers.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid()));
        swimmers.Setup(r => r.GetLatestBodyMeasurementAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((BodyMeasurementRow?)null);

        var dto = await svc.GetBodyMeasurementAsync(id);

        Assert.NotNull(dto);
        Assert.Null(dto!.Latest);
    }

    [Fact]
    public async Task GetBodyMeasurementAsync_maps_latest_row()
    {
        var (svc, swimmers, _) = Build();
        var id = Guid.NewGuid();
        swimmers.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid()));
        swimmers.Setup(r => r.GetLatestBodyMeasurementAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BodyMeasurementRow(Guid.NewGuid(), new DateOnly(2026, 9, 19), 78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m));

        var dto = await svc.GetBodyMeasurementAsync(id);

        Assert.NotNull(dto!.Latest);
        Assert.Equal(78.5m, dto.Latest!.RightArmCm);
        Assert.Equal(76.5m, dto.Latest.WaistDiameterCm);
    }

    [Fact]
    public async Task AddBodyMeasurementAsync_returns_false_when_swimmer_missing()
    {
        var (svc, swimmers, _) = Build();
        var id = Guid.NewGuid();
        swimmers.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((SwimmerProfile?)null);

        var req = new CreateBodyMeasurementRequest(78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m);
        Assert.False(await svc.AddBodyMeasurementAsync(id, req));
    }

    [Fact]
    public async Task AddBodyMeasurementAsync_inserts_and_saves_when_swimmer_exists()
    {
        var (svc, swimmers, _) = Build();
        var id = Guid.NewGuid();
        swimmers.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid()));

        var req = new CreateBodyMeasurementRequest(78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m);
        var ok = await svc.AddBodyMeasurementAsync(id, req);

        Assert.True(ok);
        swimmers.Verify(r => r.AddBodyMeasurementAsync(
            It.Is<BodyMeasurement>(m => m.SwimmerId == id && m.RightArmCm == 78.5m && m.WaistDiameterCm == 76.5m),
            It.IsAny<CancellationToken>()), Times.Once);
        swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~SwimmerServiceTests"`
Expected: FAIL — `GetBodyMeasurementAsync` / `AddBodyMeasurementAsync` don't exist.

- [ ] **Step 3: Add the interface methods**

In `ISwimmerService.cs`, after `Task<bool> UpsertGuardiansAsync(...)` add:

```csharp
    Task<SwimmerBodyMeasurementDto?> GetBodyMeasurementAsync(Guid id, CancellationToken ct = default);
    Task<bool> AddBodyMeasurementAsync(Guid id, CreateBodyMeasurementRequest request, CancellationToken ct = default);
```

- [ ] **Step 4: Implement the service methods**

In `SwimmerService.cs`, after `UpsertOne(...)` (end of class) add:

```csharp
    public async Task<SwimmerBodyMeasurementDto?> GetBodyMeasurementAsync(Guid id, CancellationToken ct = default)
    {
        var profile = await _swimmers.GetByIdTrackedAsync(id, ct);
        if (profile is null) return null;

        var row = await _swimmers.GetLatestBodyMeasurementAsync(id, ct);
        return new SwimmerBodyMeasurementDto(MapBodyMeasurement(row));
    }

    private static BodyMeasurementDto? MapBodyMeasurement(Kheprx.BaseBackend.Identity.Domain.ReadModels.BodyMeasurementRow? r)
        => r is null
            ? null
            : new BodyMeasurementDto(r.Id, r.MeasuredAt, r.RightArmCm, r.LeftArmCm, r.RightLegCm, r.LeftLegCm, r.TorsoCm, r.BustDiameterCm, r.WaistDiameterCm);

    public async Task<bool> AddBodyMeasurementAsync(Guid id, CreateBodyMeasurementRequest request, CancellationToken ct = default)
    {
        var profile = await _swimmers.GetByIdTrackedAsync(id, ct);
        if (profile is null) return false;

        var measurement = new BodyMeasurement(id,
            request.RightArmCm, request.LeftArmCm, request.RightLegCm, request.LeftLegCm,
            request.TorsoCm, request.BustDiameterCm, request.WaistDiameterCm);
        await _swimmers.AddBodyMeasurementAsync(measurement, ct);
        await _swimmers.SaveChangesAsync(ct);
        return true;
    }
```

- [ ] **Step 5: Add the success messages**

In `SwimmerMessages.cs`, inside `Success`, after `GuardiansSaved`, add:

```csharp
        public static string BodyMeasurementRetrieved(string lang) => lang switch { "ar" => "قياسات الجسم", _ => "Body measurements" };
        public static string BodyMeasurementSaved(string lang) => lang switch { "ar" => "تم حفظ قياسات الجسم", _ => "Body measurements saved" };
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~SwimmerServiceTests"`
Expected: PASS.

- [ ] **Step 7: Stage (do not commit)**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/ISwimmerService.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Resources/SwimmerMessages.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs
```

---

### Task 5: Controller endpoints

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs` (add tests)

**Interfaces:**
- Consumes: `ISwimmerService.GetBodyMeasurementAsync`, `.AddBodyMeasurementAsync` (Task 4); DTOs (Task 3); `SwimmerMessages` (Task 4).
- Produces: HTTP `GET api/swimmers/{id:guid}/body-measurements/latest` and `POST api/swimmers/{id:guid}/body-measurements`.

- [ ] **Step 1: Write the failing controller tests**

Append to `SwimmersControllerTests.cs` (uses `Moq`, `ApiResponse`, `StatusCodes`, DTO namespace already imported):

```csharp
    [Fact]
    public async Task GetLatestBodyMeasurement_returns_200_with_latest()
    {
        var svc = new Mock<ISwimmerService>();
        var id = Guid.NewGuid();
        var dto = new SwimmerBodyMeasurementDto(
            new BodyMeasurementDto(Guid.NewGuid(), new DateOnly(2026, 9, 19), 78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m));
        svc.Setup(s => s.GetBodyMeasurementAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var result = await new SwimmersController(svc.Object).GetLatestBodyMeasurement(id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<SwimmerBodyMeasurementDto>>(ok.Value);
        Assert.True(body.SuccessStatus);
        Assert.Equal(78.5m, body.Data!.Latest!.RightArmCm);
    }

    [Fact]
    public async Task GetLatestBodyMeasurement_returns_404_when_service_returns_null()
    {
        var svc = new Mock<ISwimmerService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.GetBodyMeasurementAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((SwimmerBodyMeasurementDto?)null);

        var result = await new SwimmersController(svc.Object).GetLatestBodyMeasurement(id, CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task CreateBodyMeasurement_returns_200_when_saved()
    {
        var svc = new Mock<ISwimmerService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.AddBodyMeasurementAsync(id, It.IsAny<CreateBodyMeasurementRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var req = new CreateBodyMeasurementRequest(78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m);

        var result = await new SwimmersController(svc.Object).CreateBodyMeasurement(id, req, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<object>>(ok.Value);
        Assert.True(body.SuccessStatus);
    }

    [Fact]
    public async Task CreateBodyMeasurement_returns_404_when_service_returns_false()
    {
        var svc = new Mock<ISwimmerService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.AddBodyMeasurementAsync(id, It.IsAny<CreateBodyMeasurementRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var req = new CreateBodyMeasurementRequest(78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m);

        var result = await new SwimmersController(svc.Object).CreateBodyMeasurement(id, req, CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter "FullyQualifiedName~SwimmersControllerTests"`
Expected: FAIL — controller actions don't exist.

- [ ] **Step 3: Add the controller endpoints**

In `SwimmersController.cs`, before the final closing brace (after the Guardians region), add:

```csharp
    #region Body measurements — GET latest / POST api/swimmers/{id}/body-measurements

    /// <summary>Returns a swimmer's latest body measurement (null when none recorded yet).</summary>
    /// <response code="200">The latest body measurement (or empty).</response>
    /// <response code="404">No swimmer with that id.</response>
    [HttpGet("{id:guid}/body-measurements/latest")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SwimmerBodyMeasurementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SwimmerBodyMeasurementDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SwimmerBodyMeasurementDto>>> GetLatestBodyMeasurement(Guid id, CancellationToken ct)
    {
        var dto = await _service.GetBodyMeasurementAsync(id, ct);
        if (dto is null)
        {
            var nf = ApiResponse<SwimmerBodyMeasurementDto>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<SwimmerBodyMeasurementDto>.Success(SwimmerMessages.Success.BodyMeasurementRetrieved(AppLanguage.Current), dto));
    }

    /// <summary>Records a new dated body measurement for a swimmer. Head Coach or Captain only.</summary>
    /// <response code="200">Body measurement saved.</response>
    /// <response code="404">No swimmer with that id.</response>
    [HttpPost("{id:guid}/body-measurements")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> CreateBodyMeasurement(Guid id, CreateBodyMeasurementRequest request, CancellationToken ct)
    {
        var saved = await _service.AddBodyMeasurementAsync(id, request, ct);
        if (!saved)
        {
            var nf = ApiResponse<object>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<object>.Success(SwimmerMessages.Success.BodyMeasurementSaved(AppLanguage.Current), null));
    }

    #endregion
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter "FullyQualifiedName~SwimmersControllerTests"`
Expected: PASS.

- [ ] **Step 5: Full backend build + test sweep**

Run: `dotnet build backend` then `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests backend/tests/Kheprx.BaseBackend.Api.UnitTests`
Expected: build clean; all green (including existing suites + the new tests).

- [ ] **Step 6: Stage (do not commit)**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs \
        backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs
```

---

### Task 6: Frontend model + DTO + mapper

**Files:**
- Create: `frontend/src/app/features/swimmer-profile/domain/model/body-measurement.ts`
- Create: `frontend/src/app/features/swimmer-profile/data/dto/body-measurement.dto.ts`
- Create: `frontend/src/app/features/swimmer-profile/data/dto/body-measurement.mapper.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/data/dto/body-measurement.mapper.spec.ts`

**Interfaces:**
- Consumes: `BaseResponseRs<T>` from `@core/network/api/base-response-rs`.
- Produces:
  - Model `BodyMeasurement { id; measuredAt; rightArmCm; leftArmCm; rightLegCm; leftLegCm; torsoCm; bustDiameterCm; waistDiameterCm }` (measuredAt string, rest numbers).
  - DTOs `BodyMeasurementDtoRs`, `SwimmerBodyMeasurementDtoRs { latest: BodyMeasurementDtoRs | null }`, `BodyMeasurementItemDtoRs`, `CreateBodyMeasurementDtoRq`, `CreateBodyMeasurementItemDtoRs`, guard `isSwimmerBodyMeasurementDtoRsValid`.
  - Mapper `toLatestBodyMeasurement(dto): BodyMeasurement | null`.

- [ ] **Step 1: Write the model**

```ts
export interface BodyMeasurement {
  id: string;
  measuredAt: string;
  rightArmCm: number;
  leftArmCm: number;
  rightLegCm: number;
  leftLegCm: number;
  torsoCm: number;
  bustDiameterCm: number;
  waistDiameterCm: number;
}
```

- [ ] **Step 2: Write the DTOs**

```ts
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface BodyMeasurementDtoRs {
  id: string;
  measuredAt: string;
  rightArmCm: number;
  leftArmCm: number;
  rightLegCm: number;
  leftLegCm: number;
  torsoCm: number;
  bustDiameterCm: number;
  waistDiameterCm: number;
}
export interface SwimmerBodyMeasurementDtoRs { latest: BodyMeasurementDtoRs | null; }
export interface BodyMeasurementItemDtoRs extends BaseResponseRs<SwimmerBodyMeasurementDtoRs> {}

export interface CreateBodyMeasurementDtoRq {
  rightArmCm: number;
  leftArmCm: number;
  rightLegCm: number;
  leftLegCm: number;
  torsoCm: number;
  bustDiameterCm: number;
  waistDiameterCm: number;
}
export interface CreateBodyMeasurementItemDtoRs extends BaseResponseRs<unknown> {}

const VALUE_KEYS = ['rightArmCm', 'leftArmCm', 'rightLegCm', 'leftLegCm', 'torsoCm', 'bustDiameterCm', 'waistDiameterCm'] as const;

export function isSwimmerBodyMeasurementDtoRsValid(dto: unknown): dto is SwimmerBodyMeasurementDtoRs {
  const d = dto as SwimmerBodyMeasurementDtoRs;
  if (!d || typeof d !== 'object') return false;
  const m = d.latest;
  if (m === null) return true;
  if (typeof m.id !== 'string') return false;
  return VALUE_KEYS.every((k) => typeof (m as Record<string, unknown>)[k] === 'number');
}
```

- [ ] **Step 3: Write the failing mapper test**

```ts
import { toLatestBodyMeasurement } from '@features/swimmer-profile/data/dto/body-measurement.mapper';
import { SwimmerBodyMeasurementDtoRs } from '@features/swimmer-profile/data/dto/body-measurement.dto';

describe('toLatestBodyMeasurement', () => {
  it('maps a present latest measurement', () => {
    const dto: SwimmerBodyMeasurementDtoRs = {
      latest: { id: 'b1', measuredAt: '2026-09-19', rightArmCm: 78.5, leftArmCm: 78.2, rightLegCm: 96.2, leftLegCm: 96.0, torsoCm: 52.8, bustDiameterCm: 94.0, waistDiameterCm: 76.5 },
    };
    const m = toLatestBodyMeasurement(dto);
    expect(m?.rightArmCm).toBe(78.5);
    expect(m?.waistDiameterCm).toBe(76.5);
    expect(m?.measuredAt).toBe('2026-09-19');
  });

  it('returns null when latest is null', () => {
    expect(toLatestBodyMeasurement({ latest: null })).toBeNull();
  });
});
```

- [ ] **Step 4: Run test to verify it fails**

Run: `cd frontend && npx jest src/app/features/swimmer-profile/testing/data/dto/body-measurement.mapper.spec.ts`
Expected: FAIL — mapper module not found.

- [ ] **Step 5: Write the mapper**

```ts
import { BodyMeasurementDtoRs, SwimmerBodyMeasurementDtoRs } from '@features/swimmer-profile/data/dto/body-measurement.dto';
import { BodyMeasurement } from '@features/swimmer-profile/domain/model/body-measurement';

function toBodyMeasurement(d: BodyMeasurementDtoRs): BodyMeasurement {
  return {
    id: d.id,
    measuredAt: d.measuredAt,
    rightArmCm: d.rightArmCm,
    leftArmCm: d.leftArmCm,
    rightLegCm: d.rightLegCm,
    leftLegCm: d.leftLegCm,
    torsoCm: d.torsoCm,
    bustDiameterCm: d.bustDiameterCm,
    waistDiameterCm: d.waistDiameterCm,
  };
}

export function toLatestBodyMeasurement(d: SwimmerBodyMeasurementDtoRs): BodyMeasurement | null {
  return d.latest ? toBodyMeasurement(d.latest) : null;
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `cd frontend && npx jest src/app/features/swimmer-profile/testing/data/dto/body-measurement.mapper.spec.ts`
Expected: PASS.

- [ ] **Step 7: Stage (do not commit)**

```bash
git add frontend/src/app/features/swimmer-profile/domain/model/body-measurement.ts \
        frontend/src/app/features/swimmer-profile/data/dto/body-measurement.dto.ts \
        frontend/src/app/features/swimmer-profile/data/dto/body-measurement.mapper.ts \
        frontend/src/app/features/swimmer-profile/testing/data/dto/body-measurement.mapper.spec.ts
```

---

### Task 7: Frontend repository interface + impl

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/domain/repositories/swimmer-profile.repository.ts`
- Modify: `frontend/src/app/features/swimmer-profile/data/repositories/swimmer-profile.repository.impl.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/data/repositories/swimmer-profile.repository.impl.spec.ts` (add tests)

**Interfaces:**
- Consumes: DTOs (Task 6); `HttpClientService`.
- Produces:
  - `ISwimmerProfileRepository.getBodyMeasurement(id): Promise<BodyMeasurementItemDtoRs>`.
  - `ISwimmerProfileRepository.createBodyMeasurement(id, rq): Promise<CreateBodyMeasurementItemDtoRs>`.

- [ ] **Step 1: Write the failing repository-impl tests**

Append inside the existing `describe('SwimmerProfileRepositoryImpl', ...)` block:

```ts
  it('getBodyMeasurement GETs the latest endpoint', async () => {
    (http.get as jest.Mock).mockResolvedValue({ successStatus: true, data: { latest: null } });
    await repo.getBodyMeasurement('sw1');
    expect(http.get).toHaveBeenCalledWith('/api/swimmers/sw1/body-measurements/latest');
  });

  it('createBodyMeasurement POSTs to the collection endpoint with the body', async () => {
    (http.post as jest.Mock).mockResolvedValue({ successStatus: true, data: null });
    const rq = { rightArmCm: 78.5, leftArmCm: 78.2, rightLegCm: 96.2, leftLegCm: 96.0, torsoCm: 52.8, bustDiameterCm: 94.0, waistDiameterCm: 76.5 };
    await repo.createBodyMeasurement('sw1', rq);
    expect(http.post).toHaveBeenCalledWith('/api/swimmers/sw1/body-measurements', { body: rq });
  });
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd frontend && npx jest src/app/features/swimmer-profile/testing/data/repositories/swimmer-profile.repository.impl.spec.ts`
Expected: FAIL — methods don't exist (TS compile error in the spec).

- [ ] **Step 3: Add the interface methods**

In `swimmer-profile.repository.ts`, add the import and two interface methods:

```ts
import { BodyMeasurementItemDtoRs, CreateBodyMeasurementDtoRq, CreateBodyMeasurementItemDtoRs } from '@features/swimmer-profile/data/dto/body-measurement.dto';
```

Add to the `ISwimmerProfileRepository` interface (after `upsertGuardians`):

```ts
  getBodyMeasurement(id: string): Promise<BodyMeasurementItemDtoRs>;
  createBodyMeasurement(id: string, rq: CreateBodyMeasurementDtoRq): Promise<CreateBodyMeasurementItemDtoRs>;
```

- [ ] **Step 4: Implement the methods**

In `swimmer-profile.repository.impl.ts`, add the same import, then after `upsertGuardians`:

```ts
  getBodyMeasurement(id: string): Promise<BodyMeasurementItemDtoRs> {
    return this.http.get<BodyMeasurementItemDtoRs>(`/api/swimmers/${id}/body-measurements/latest`);
  }
  createBodyMeasurement(id: string, rq: CreateBodyMeasurementDtoRq): Promise<CreateBodyMeasurementItemDtoRs> {
    return this.http.post<CreateBodyMeasurementItemDtoRs>(`/api/swimmers/${id}/body-measurements`, { body: rq });
  }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd frontend && npx jest src/app/features/swimmer-profile/testing/data/repositories/swimmer-profile.repository.impl.spec.ts`
Expected: PASS.

- [ ] **Step 6: Stage (do not commit)**

```bash
git add frontend/src/app/features/swimmer-profile/domain/repositories/swimmer-profile.repository.ts \
        frontend/src/app/features/swimmer-profile/data/repositories/swimmer-profile.repository.impl.ts \
        frontend/src/app/features/swimmer-profile/testing/data/repositories/swimmer-profile.repository.impl.spec.ts
```

---

### Task 8: Frontend use cases

**Files:**
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/get-latest-body-measurement.use-case.ts`
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/create-body-measurement.use-case.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/domain/usecases/get-latest-body-measurement.use-case.spec.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/domain/usecases/create-body-measurement.use-case.spec.ts`

**Interfaces:**
- Consumes: repository (Task 7); DTO guard + mapper (Task 6); `UseCase`, `AppError`, `SWIMMER_PROFILE_REPOSITORY`.
- Produces:
  - `GetLatestBodyMeasurementUseCase extends UseCase<string, BodyMeasurement | null>`.
  - `CreateBodyMeasurementUseCase extends UseCase<CreateBodyMeasurementInput, void>` where `CreateBodyMeasurementInput { id: string; rq: CreateBodyMeasurementDtoRq }`.

- [ ] **Step 1: Write the failing use-case tests**

`get-latest-body-measurement.use-case.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { GetLatestBodyMeasurementUseCase } from '@features/swimmer-profile/domain/usecases/get-latest-body-measurement.use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

describe('GetLatestBodyMeasurementUseCase', () => {
  const repo = { getBodyMeasurement: jest.fn() } as any;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [GetLatestBodyMeasurementUseCase, { provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }],
    });
  });

  it('maps a present latest measurement', async () => {
    repo.getBodyMeasurement.mockResolvedValue({ successStatus: true, data: {
      latest: { id: 'b1', measuredAt: '2026-09-19', rightArmCm: 78.5, leftArmCm: 78.2, rightLegCm: 96.2, leftLegCm: 96.0, torsoCm: 52.8, bustDiameterCm: 94.0, waistDiameterCm: 76.5 } } });
    const res = await TestBed.inject(GetLatestBodyMeasurementUseCase).run('sw1');
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data?.rightArmCm).toBe(78.5);
  });

  it('returns null data when there is no measurement', async () => {
    repo.getBodyMeasurement.mockResolvedValue({ successStatus: true, data: { latest: null } });
    const res = await TestBed.inject(GetLatestBodyMeasurementUseCase).run('sw1');
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data).toBeNull();
  });

  it('fails on an invalid response', async () => {
    repo.getBodyMeasurement.mockResolvedValue({ successStatus: true, data: { latest: { id: 5 } } });
    const res = await TestBed.inject(GetLatestBodyMeasurementUseCase).run('sw1');
    expect(res.ok).toBe(false);
  });
});
```

`create-body-measurement.use-case.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { CreateBodyMeasurementUseCase } from '@features/swimmer-profile/domain/usecases/create-body-measurement.use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

describe('CreateBodyMeasurementUseCase', () => {
  const repo = { createBodyMeasurement: jest.fn() } as any;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CreateBodyMeasurementUseCase, { provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }],
    });
  });

  it('calls the repository and succeeds', async () => {
    repo.createBodyMeasurement.mockResolvedValue({ successStatus: true, data: null });
    const rq = { rightArmCm: 78.5, leftArmCm: 78.2, rightLegCm: 96.2, leftLegCm: 96.0, torsoCm: 52.8, bustDiameterCm: 94.0, waistDiameterCm: 76.5 };
    const res = await TestBed.inject(CreateBodyMeasurementUseCase).run({ id: 'sw1', rq });
    expect(res.ok).toBe(true);
    expect(repo.createBodyMeasurement).toHaveBeenCalledWith('sw1', rq);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd frontend && npx jest src/app/features/swimmer-profile/testing/domain/usecases/get-latest-body-measurement.use-case.spec.ts src/app/features/swimmer-profile/testing/domain/usecases/create-body-measurement.use-case.spec.ts`
Expected: FAIL — use-case modules not found.

- [ ] **Step 3: Write the get use case**

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { isSwimmerBodyMeasurementDtoRsValid } from '@features/swimmer-profile/data/dto/body-measurement.dto';
import { toLatestBodyMeasurement } from '@features/swimmer-profile/data/dto/body-measurement.mapper';
import { BodyMeasurement } from '@features/swimmer-profile/domain/model/body-measurement';

@Injectable({ providedIn: 'root' })
export class GetLatestBodyMeasurementUseCase extends UseCase<string, BodyMeasurement | null> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('GetLatestBodyMeasurement'); }
  protected async execute(id: string): Promise<BodyMeasurement | null> {
    const res = await this.repo.getBodyMeasurement(id);
    if (!isSwimmerBodyMeasurementDtoRsValid(res.data)) throw new AppError('Invalid body measurement received', 'validation');
    return toLatestBodyMeasurement(res.data);
  }
}
```

- [ ] **Step 4: Write the create use case**

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { CreateBodyMeasurementDtoRq } from '@features/swimmer-profile/data/dto/body-measurement.dto';

export interface CreateBodyMeasurementInput { id: string; rq: CreateBodyMeasurementDtoRq; }

@Injectable({ providedIn: 'root' })
export class CreateBodyMeasurementUseCase extends UseCase<CreateBodyMeasurementInput, void> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('CreateBodyMeasurement'); }
  protected async execute(input: CreateBodyMeasurementInput): Promise<void> {
    await this.repo.createBodyMeasurement(input.id, input.rq);
  }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd frontend && npx jest src/app/features/swimmer-profile/testing/domain/usecases/get-latest-body-measurement.use-case.spec.ts src/app/features/swimmer-profile/testing/domain/usecases/create-body-measurement.use-case.spec.ts`
Expected: PASS.

- [ ] **Step 6: Stage (do not commit)**

```bash
git add frontend/src/app/features/swimmer-profile/domain/usecases/get-latest-body-measurement.use-case.ts \
        frontend/src/app/features/swimmer-profile/domain/usecases/create-body-measurement.use-case.ts \
        frontend/src/app/features/swimmer-profile/testing/domain/usecases/get-latest-body-measurement.use-case.spec.ts \
        frontend/src/app/features/swimmer-profile/testing/domain/usecases/create-body-measurement.use-case.spec.ts
```

---

### Task 9: ViewModel state + tab wiring

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.ts`
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts` (extend)

**Interfaces:**
- Consumes: `GetLatestBodyMeasurementUseCase`, `CreateBodyMeasurementUseCase` (Task 8); `BodyMeasurement` model (Task 6).
- Produces (on `SwimmerProfileViewModel`): signals `bodyMeasurement`, `loadingBodyMeasurement`, `editingBodyMeasurement`, `savingBodyMeasurement`, `bmRightArm/bmLeftArm/bmRightLeg/bmLeftLeg/bmTorso/bmBustDiameter/bmWaistDiameter`, computed `canSaveBodyMeasurement`; methods `startEditBodyMeasurement()`, `cancelEditBodyMeasurement()`, `saveBodyMeasurement()`; `activeTab` type widened to include `'physiological'`; `setTab` accepts `'physiological'` and lazy-loads. On `SwimmerProfilePage`: `enabledTabs` includes `'physiological'`.

- [ ] **Step 1: Extend the failing viewmodel test**

In `swimmer-profile.viewmodel.spec.ts`:

(a) Add imports:

```ts
import { GetLatestBodyMeasurementUseCase } from '@features/swimmer-profile/domain/usecases/get-latest-body-measurement.use-case';
import { CreateBodyMeasurementUseCase } from '@features/swimmer-profile/domain/usecases/create-body-measurement.use-case';
```

(b) In `build(...)`, add a `getBodyMeasurement`/`createBodyMeasurement` override field and two mocks, register providers, and return them. Add these lines alongside the guardian mocks:

```ts
  const getBodyMeasurementUc = { run: jest.fn().mockResolvedValue((over as any).getBodyMeasurement ?? { ok: true, data: null }) };
  const createBodyMeasurementUc = { run: jest.fn().mockResolvedValue((over as any).createBodyMeasurement ?? { ok: true, data: undefined }) };
```

Add to the `providers` array:

```ts
    { provide: GetLatestBodyMeasurementUseCase, useValue: getBodyMeasurementUc },
    { provide: CreateBodyMeasurementUseCase, useValue: createBodyMeasurementUc },
```

Add to the returned object: `getBodyMeasurementUc, createBodyMeasurementUc`.

(c) Add a describe block:

```ts
  describe('physiological tab', () => {
    const M = { id: 'b1', measuredAt: '2026-09-19', rightArmCm: 78.5, leftArmCm: 78.2, rightLegCm: 96.2, leftLegCm: 96.0, torsoCm: 52.8, bustDiameterCm: 94.0, waistDiameterCm: 76.5 };

    it('setTab("physiological") lazy-loads the measurement once', async () => {
      const { vm, getBodyMeasurementUc } = build({ getBodyMeasurement: { ok: true, data: M } } as any);
      await vm.load('s1');
      vm.setTab('physiological');
      await Promise.resolve(); await Promise.resolve();
      expect(vm.activeTab()).toBe('physiological');
      expect(vm.bodyMeasurement()?.rightArmCm).toBe(78.5);
      expect(getBodyMeasurementUc.run).toHaveBeenCalledTimes(1);
      vm.setTab('identityVitals');
      vm.setTab('physiological');
      await Promise.resolve();
      expect(getBodyMeasurementUc.run).toHaveBeenCalledTimes(1); // not reloaded
    });

    it('canSaveBodyMeasurement requires all seven positive, in-range numbers', () => {
      const { vm } = build();
      vm.startEditBodyMeasurement();
      expect(vm.canSaveBodyMeasurement()).toBe(false);
      vm.bmRightArm.set('78.5'); vm.bmLeftArm.set('78.2'); vm.bmRightLeg.set('96.2'); vm.bmLeftLeg.set('96');
      vm.bmTorso.set('52.8'); vm.bmBustDiameter.set('94'); vm.bmWaistDiameter.set('1000'); // out of range
      expect(vm.canSaveBodyMeasurement()).toBe(false);
      vm.bmWaistDiameter.set('76.5');
      expect(vm.canSaveBodyMeasurement()).toBe(true);
      vm.bmRightArm.set('0'); // not positive
      expect(vm.canSaveBodyMeasurement()).toBe(false);
    });

    it('saveBodyMeasurement posts, toasts success and reloads', async () => {
      const { vm, createBodyMeasurementUc, getBodyMeasurementUc, notify } = build({ getBodyMeasurement: { ok: true, data: M } } as any);
      await vm.load('s1');
      vm.setTab('physiological');
      await Promise.resolve(); await Promise.resolve();
      vm.startEditBodyMeasurement();
      await vm.saveBodyMeasurement();
      expect(createBodyMeasurementUc.run).toHaveBeenCalled();
      expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.bodyMeasurementSaved');
      expect(vm.editingBodyMeasurement()).toBe(false);
      expect(getBodyMeasurementUc.run).toHaveBeenCalledTimes(2); // initial tab open + reload after save
    });
  });
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd frontend && npx jest src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts`
Expected: FAIL — new use cases/members don't exist (DI + TS errors).

- [ ] **Step 3: Wire the use cases and state into the viewmodel**

In `swimmer-profile.viewmodel.ts`:

(a) Add imports:

```ts
import { GetLatestBodyMeasurementUseCase } from '@features/swimmer-profile/domain/usecases/get-latest-body-measurement.use-case';
import { CreateBodyMeasurementUseCase } from '@features/swimmer-profile/domain/usecases/create-body-measurement.use-case';
import { BodyMeasurement } from '@features/swimmer-profile/domain/model/body-measurement';
```

(b) Inject the use cases (beside the guardian injects):

```ts
  private readonly getBodyMeasurementUc = inject(GetLatestBodyMeasurementUseCase);
  private readonly createBodyMeasurementUc = inject(CreateBodyMeasurementUseCase);
```

(c) Widen the tab type (both the `activeTab` signal and `setTab` parameter) from `'identityVitals' | 'guardian'` to `'identityVitals' | 'guardian' | 'physiological'`. Add a `private bodyMeasurementLoaded = false;` beside `guardiansLoaded`.

(d) Add body-measurement state (after the guardian state block):

```ts
  // Body measurement (physiological) state
  readonly bodyMeasurement = signal<BodyMeasurement | null>(null);
  readonly loadingBodyMeasurement = signal(false);
  readonly editingBodyMeasurement = signal(false);
  readonly savingBodyMeasurement = signal(false);
  readonly bmRightArm = signal(''); readonly bmLeftArm = signal(''); readonly bmRightLeg = signal(''); readonly bmLeftLeg = signal('');
  readonly bmTorso = signal(''); readonly bmBustDiameter = signal(''); readonly bmWaistDiameter = signal('');

  readonly canSaveBodyMeasurement = computed(() => {
    const nums = [this.bmRightArm(), this.bmLeftArm(), this.bmRightLeg(), this.bmLeftLeg(), this.bmTorso(), this.bmBustDiameter(), this.bmWaistDiameter()];
    return nums.every((v) => {
      const n = Number(v);
      return v.trim().length > 0 && Number.isFinite(n) && n > 0 && n <= 999.9;
    });
  });
```

(e) In `load(...)`, alongside the guardian reset lines, reset the physiological state:

```ts
    this.bodyMeasurementLoaded = false;
    this.bodyMeasurement.set(null);
    this.editingBodyMeasurement.set(false);
```

(f) Update `setTab(...)` to lazy-load physiological:

```ts
  setTab(key: 'identityVitals' | 'guardian' | 'physiological'): void {
    this.activeTab.set(key);
    if (key === 'guardian' && !this.guardiansLoaded) void this.loadGuardians();
    if (key === 'physiological' && !this.bodyMeasurementLoaded) void this.loadBodyMeasurement();
  }
```

(g) Add the load/edit/save methods (after `saveGuardians()`):

```ts
  private async loadBodyMeasurement(): Promise<void> {
    this.bodyMeasurementLoaded = true;
    this.loadingBodyMeasurement.set(true);
    const r = await this.getBodyMeasurementUc.run(this.swimmerId);
    this.loadingBodyMeasurement.set(false);
    if (r.ok) this.bodyMeasurement.set(r.data);
    else { this.bodyMeasurementLoaded = false; this.bodyMeasurement.set(null); }
  }

  startEditBodyMeasurement(): void {
    const m = this.bodyMeasurement();
    this.bmRightArm.set(m ? String(m.rightArmCm) : '');
    this.bmLeftArm.set(m ? String(m.leftArmCm) : '');
    this.bmRightLeg.set(m ? String(m.rightLegCm) : '');
    this.bmLeftLeg.set(m ? String(m.leftLegCm) : '');
    this.bmTorso.set(m ? String(m.torsoCm) : '');
    this.bmBustDiameter.set(m ? String(m.bustDiameterCm) : '');
    this.bmWaistDiameter.set(m ? String(m.waistDiameterCm) : '');
    this.editingBodyMeasurement.set(true);
  }

  cancelEditBodyMeasurement(): void { this.editingBodyMeasurement.set(false); }

  async saveBodyMeasurement(): Promise<void> {
    if (!this.canSaveBodyMeasurement() || this.savingBodyMeasurement()) return;
    this.savingBodyMeasurement.set(true);
    const rq = {
      rightArmCm: Number(this.bmRightArm()), leftArmCm: Number(this.bmLeftArm()),
      rightLegCm: Number(this.bmRightLeg()), leftLegCm: Number(this.bmLeftLeg()),
      torsoCm: Number(this.bmTorso()), bustDiameterCm: Number(this.bmBustDiameter()), waistDiameterCm: Number(this.bmWaistDiameter()),
    };
    const r = await this.createBodyMeasurementUc.run({ id: this.swimmerId, rq });
    this.savingBodyMeasurement.set(false);
    if (r.ok) {
      this.notify.success(this.i18n.t('swimmerProfile.toasts.bodyMeasurementSaved'));
      this.editingBodyMeasurement.set(false);
      this.bodyMeasurementLoaded = false;
      await this.loadBodyMeasurement();
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.saveFailed'));
    }
  }
```

- [ ] **Step 4: Enable the tab on the page component**

In `swimmer-profile.page.ts`, change:

```ts
  protected readonly enabledTabs = new Set(['identityVitals', 'guardian']);
```

to:

```ts
  protected readonly enabledTabs = new Set(['identityVitals', 'guardian', 'physiological']);
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `cd frontend && npx jest src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts`
Expected: PASS.

- [ ] **Step 6: Stage (do not commit)**

```bash
git add frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.ts \
        frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.ts \
        frontend/src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts
```

---

### Task 10: Physiological section markup + i18n

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.html`
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`

**Interfaces:**
- Consumes: viewmodel members (Task 9); i18n keys added below.
- Produces: rendered Body Measurements section under the `physiological` tab.

- [ ] **Step 1: Add the i18n keys (EN)**

In `en.json` → `swimmerProfile`:

- In `sections`, add `"physiological": "Body Measurements"` →
  `"sections": { "identity": "Identity", "vitals": "Vitals", "guardian": "Guardian Details", "physiological": "Body Measurements" },`
- Add a new block after the `guardian` block:

```json
    "physiological": {
      "rightArm": "Right Arm",
      "leftArm": "Left Arm",
      "leftLeg": "Left Leg",
      "rightLeg": "Right Leg",
      "torsoLength": "Torso Length",
      "bustDiameter": "Bust Diameter",
      "waistDiameter": "Waist Diameter",
      "cm": "cm",
      "noRecord": "No measurements recorded yet."
    },
```
- In `toasts`, add `"bodyMeasurementSaved": "Body measurements saved"`.

- [ ] **Step 2: Add the i18n keys (AR)**

In `ar.json` → `swimmerProfile`:

- In `sections`, add `"physiological": "قياسات الجسم"`.
- Add the block after `guardian`:

```json
    "physiological": {
      "rightArm": "الذراع اليمنى",
      "leftArm": "الذراع اليسرى",
      "leftLeg": "الساق اليسرى",
      "rightLeg": "الساق اليمنى",
      "torsoLength": "طول الجذع",
      "bustDiameter": "محيط الصدر",
      "waistDiameter": "محيط الخصر",
      "cm": "سم",
      "noRecord": "لا توجد قياسات مسجلة بعد."
    },
```
- In `toasts`, add `"bodyMeasurementSaved": "تم حفظ قياسات الجسم"`.

- [ ] **Step 3: Add the Physiological section markup**

In `swimmer-profile.page.html`, immediately after the closing `}` of the `@if (vm.activeTab() === 'guardian') { ... }` block (line ~216) and before the outer closing `}` (line ~217), insert:

```html
    @if (vm.activeTab() === 'physiological') {
      <section class="rounded-2xl border border-border bg-surface p-6 shadow-sm">
        <div class="mb-4 flex items-center justify-between border-b border-border pb-2">
          <h2 class="font-heading text-lg text-ink">{{ 'swimmerProfile.sections.physiological' | translate }}</h2>
          @if (vm.canEdit() && !vm.editingBodyMeasurement()) {
            <button type="button" class="text-sm font-medium text-primary" (click)="vm.startEditBodyMeasurement()">{{ 'swimmerProfile.edit' | translate }}</button>
          }
        </div>

        @if (vm.loadingBodyMeasurement()) {
          <p class="text-sm text-text-secondary">{{ 'swimmerProfile.states.loading' | translate }}</p>
        } @else if (vm.editingBodyMeasurement()) {
          <form class="grid grid-cols-1 gap-4 md:grid-cols-2" (submit)="$event.preventDefault(); vm.saveBodyMeasurement()">
            <app-text-field [label]="'swimmerProfile.physiological.rightArm' | translate" [value]="vm.bmRightArm()" (valueChange)="vm.bmRightArm.set($event)"></app-text-field>
            <app-text-field [label]="'swimmerProfile.physiological.leftArm' | translate" [value]="vm.bmLeftArm()" (valueChange)="vm.bmLeftArm.set($event)"></app-text-field>
            <app-text-field [label]="'swimmerProfile.physiological.leftLeg' | translate" [value]="vm.bmLeftLeg()" (valueChange)="vm.bmLeftLeg.set($event)"></app-text-field>
            <app-text-field [label]="'swimmerProfile.physiological.rightLeg' | translate" [value]="vm.bmRightLeg()" (valueChange)="vm.bmRightLeg.set($event)"></app-text-field>
            <app-text-field [label]="'swimmerProfile.physiological.torsoLength' | translate" [value]="vm.bmTorso()" (valueChange)="vm.bmTorso.set($event)"></app-text-field>
            <app-text-field [label]="'swimmerProfile.physiological.bustDiameter' | translate" [value]="vm.bmBustDiameter()" (valueChange)="vm.bmBustDiameter.set($event)"></app-text-field>
            <app-text-field [label]="'swimmerProfile.physiological.waistDiameter' | translate" [value]="vm.bmWaistDiameter()" (valueChange)="vm.bmWaistDiameter.set($event)"></app-text-field>
            <div class="flex items-end gap-2 md:col-span-2">
              <button type="submit" class="rounded-md bg-primary px-5 py-2.5 text-sm font-semibold text-white disabled:opacity-50" [disabled]="!vm.canSaveBodyMeasurement() || vm.savingBodyMeasurement()">{{ 'common.save' | translate }}</button>
              <button type="button" class="rounded-md px-5 py-2.5 text-sm text-text-secondary" (click)="vm.cancelEditBodyMeasurement()">{{ 'common.cancel' | translate }}</button>
            </div>
          </form>
        } @else if (vm.bodyMeasurement(); as m) {
          <ul class="max-w-2xl divide-y divide-border rounded-lg border border-border">
            <li class="flex items-center gap-3 px-4 py-3 text-sm"><span class="flex-1 text-text-secondary">{{ 'swimmerProfile.physiological.rightArm' | translate }}</span><span class="font-medium text-ink">{{ m.rightArmCm }} <span class="font-normal text-text-secondary">{{ 'swimmerProfile.physiological.cm' | translate }}</span></span></li>
            <li class="flex items-center gap-3 px-4 py-3 text-sm"><span class="flex-1 text-text-secondary">{{ 'swimmerProfile.physiological.leftArm' | translate }}</span><span class="font-medium text-ink">{{ m.leftArmCm }} <span class="font-normal text-text-secondary">{{ 'swimmerProfile.physiological.cm' | translate }}</span></span></li>
            <li class="flex items-center gap-3 px-4 py-3 text-sm"><span class="flex-1 text-text-secondary">{{ 'swimmerProfile.physiological.leftLeg' | translate }}</span><span class="font-medium text-ink">{{ m.leftLegCm }} <span class="font-normal text-text-secondary">{{ 'swimmerProfile.physiological.cm' | translate }}</span></span></li>
            <li class="flex items-center gap-3 px-4 py-3 text-sm"><span class="flex-1 text-text-secondary">{{ 'swimmerProfile.physiological.rightLeg' | translate }}</span><span class="font-medium text-ink">{{ m.rightLegCm }} <span class="font-normal text-text-secondary">{{ 'swimmerProfile.physiological.cm' | translate }}</span></span></li>
            <li class="flex items-center gap-3 px-4 py-3 text-sm"><span class="flex-1 text-text-secondary">{{ 'swimmerProfile.physiological.torsoLength' | translate }}</span><span class="font-medium text-ink">{{ m.torsoCm }} <span class="font-normal text-text-secondary">{{ 'swimmerProfile.physiological.cm' | translate }}</span></span></li>
            <li class="flex items-center gap-3 px-4 py-3 text-sm"><span class="flex-1 text-text-secondary">{{ 'swimmerProfile.physiological.bustDiameter' | translate }}</span><span class="font-medium text-ink">{{ m.bustDiameterCm }} <span class="font-normal text-text-secondary">{{ 'swimmerProfile.physiological.cm' | translate }}</span></span></li>
            <li class="flex items-center gap-3 px-4 py-3 text-sm"><span class="flex-1 text-text-secondary">{{ 'swimmerProfile.physiological.waistDiameter' | translate }}</span><span class="font-medium text-ink">{{ m.waistDiameterCm }} <span class="font-normal text-text-secondary">{{ 'swimmerProfile.physiological.cm' | translate }}</span></span></li>
          </ul>
        } @else {
          <p class="text-sm text-text-secondary">{{ 'swimmerProfile.physiological.noRecord' | translate }}</p>
        }
      </section>
    }
```

- [ ] **Step 4: Build the frontend + run the full swimmer-profile suite**

Run: `cd frontend && npx ng build` (or the project's build script) and `npx jest src/app/features/swimmer-profile`
Expected: build succeeds; all swimmer-profile specs pass.

- [ ] **Step 5: Manual visual check (optional but recommended)**

Run the app, open a swimmer profile, click the **Physiological** tab. Confirm: empty state shows "No measurements recorded yet."; a coach sees **Edit**; entering seven values and Save shows the toast and renders the list with `cm` units; a non-coach sees no Edit button. Toggle EN/AR to confirm labels.

- [ ] **Step 6: Stage (do not commit)**

```bash
git add frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.html \
        frontend/src/app/core/i18n/en.json \
        frontend/src/app/core/i18n/ar.json
```

---

## Final verification (before handing back)

- [ ] Backend: `dotnet build backend` clean; `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests backend/tests/Kheprx.BaseBackend.Api.UnitTests` all green (new + existing).
- [ ] Frontend: build succeeds; `cd frontend && npx jest` all green.
- [ ] Migration `AddBodyMeasurement` exists and is **not** applied (no `database update` was run).
- [ ] Everything staged, **nothing committed** (per the user's standing instruction). Report the staged file list back to the user.

## Notes for the executor

- This plan stacks on the uncommitted Guardian work already in the index on `feat/swimmer-profile-guardian`. Do not reset or stash it.
- If `dotnet ef` or `dotnet test` complains about locked DLLs, stop the running API first (memory: dotnet-test-dev-server-lock).
- `SwimmerMessages.Errors.MeasurementInvalid` already exists — reuse it; do not add a new one.
- The nine-tab strip and the `swimmerProfile.tabs.physiological` label already exist in the page and both i18n bundles; this plan only enables the tab and adds the section + its own keys.
