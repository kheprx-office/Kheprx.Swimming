# Swimmer Profile — InBody tab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a fourth Swimmer-Profile tab, **InBody**, with full CRUD over a swimmer's body-composition readings (`health.inbody_reading`) and a per-metric trend history table.

**Architecture:** New `health.inbody_reading` table in the **Health module** (its own `HealthDbContext`, migrations, DI) — mirrors the existing `HealthReading` slice + the exam-history CRUD. A new `InBodyReadingsController` exposes swimmer-scoped CRUD at `/api/swimmers/{id}/inbody-readings`; `recorded_by` = `CurrentUserId()`. The frontend adds an isolated InBody domain/data/usecase set and wires the `inbody` tab into the existing `swimmer-profile` slice (reading selector, coach Add/Edit/Delete, selected-reading display, and a client-computed Measurement History trend table).

**Tech Stack:** .NET 10 (C#, EF Core, FluentValidation, xUnit + Moq), Angular (standalone, signals, Jest), PostgreSQL.

**Spec:** `docs/superpowers/specs/2026-09-19-swimmer-profile-inbody-design.md`

## Global Constraints

- **No commits.** The user's standing instruction: do not commit until told. Each task ends by **staging** (`git add`), never `git commit`. All work stays on branch `feat/swimmer-profile-guardian`, stacked on `7b122ed` (Guardian) + `e212610` (Physiological).
- **Health module placement.** New `InBodyReading` entity/config/repository/service + `InBodyReadingsController`; `DbSet` on `HealthDbContext`; migration on `HealthDbContext`. NOT the Identity module.
- **Loose Guids, no cross-module FK.** `swimmer_id` and `recorded_by` are plain required Guid columns (no EF FK), matching `HealthReadingConfiguration`. No swimmer-existence check (Health-module norm).
- **`recorded_by` = `CurrentUserId()`** (JWT `sub`, via `BaseApiController.CurrentUserId()`), set on create, preserved on edit. **`reading_date`** is user-provided (`DateOnly`); **`created_at`** is server-set (`DateTime.UtcNow`) inside the entity ctor.
- **Canonical field order** everywhere backend/DTO/request: ReadingDate, HeightCm, WeightKg, FatPct, MusclePct, BoneDensity, BodyDensity (+ `RecordedBy` on the entity/read-DTO only).
- **Column precisions:** `height_cm`/`weight_kg` `numeric(5,1)`; `fat_pct`/`muscle_pct` `numeric(4,1)`; `bone_density`/`body_density` `numeric(4,2)`.
- **Validation ranges (client + server):** reading_date required; height/weight `>0` & `≤999.9`; fat/muscle `≥0` & `≤100`; bone/body `>0` & `≤99.99`.
- **Ownership guard** on PUT/DELETE: `reading.SwimmerId == route id`, else 404.
- **Stop the running backend before `dotnet build`/`dotnet test`/`dotnet ef`** (DLL lock; memory: dotnet-test-dev-server-lock).
- Backend tests: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter "FullyQualifiedName~<Class>"` and `…/Kheprx.BaseBackend.Api.UnitTests …`.
- Frontend tests: `cd frontend && npx jest <spec-path>`.

---

### Task 1: `InBodyReading` domain entity

**Files:**
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Entities/InBodyReading.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Entities/InBodyReadingTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `InBodyReading(Guid swimmerId, DateOnly readingDate, decimal heightCm, decimal weightKg, decimal fatPct, decimal musclePct, decimal boneDensity, decimal bodyDensity, Guid recordedBy)` with read-only props `Id, SwimmerId, ReadingDate, HeightCm, WeightKg, FatPct, MusclePct, BoneDensity, BodyDensity, RecordedBy, CreatedAt`; and `Update(DateOnly readingDate, decimal heightCm, decimal weightKg, decimal fatPct, decimal musclePct, decimal boneDensity, decimal bodyDensity)`.

- [ ] **Step 1: Write the failing test**

```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Entities;

public class InBodyReadingTests
{
    [Fact]
    public void Ctor_assigns_id_fields_recorder_and_created_at()
    {
        var swimmerId = Guid.NewGuid();
        var recordedBy = Guid.NewGuid();
        var date = new DateOnly(2024, 10, 4);

        var r = new InBodyReading(swimmerId, date, 180m, 74m, 12.8m, 42.1m, 1.35m, 1.07m, recordedBy);

        Assert.NotEqual(Guid.Empty, r.Id);
        Assert.Equal(swimmerId, r.SwimmerId);
        Assert.Equal(date, r.ReadingDate);
        Assert.Equal(180m, r.HeightCm);
        Assert.Equal(74m, r.WeightKg);
        Assert.Equal(12.8m, r.FatPct);
        Assert.Equal(42.1m, r.MusclePct);
        Assert.Equal(1.35m, r.BoneDensity);
        Assert.Equal(1.07m, r.BodyDensity);
        Assert.Equal(recordedBy, r.RecordedBy);
        Assert.NotEqual(default, r.CreatedAt);
    }

    [Fact]
    public void Update_mutates_reading_date_and_metrics_preserves_recorder_and_created_at()
    {
        var recordedBy = Guid.NewGuid();
        var r = new InBodyReading(Guid.NewGuid(), new DateOnly(2024, 1, 1), 1m, 1m, 1m, 1m, 1m, 1m, recordedBy);
        var createdAt = r.CreatedAt;

        r.Update(new DateOnly(2024, 8, 12), 182m, 75.5m, 14.1m, 41.2m, 1.33m, 1.06m);

        Assert.Equal(new DateOnly(2024, 8, 12), r.ReadingDate);
        Assert.Equal(182m, r.HeightCm);
        Assert.Equal(1.06m, r.BodyDensity);
        Assert.Equal(recordedBy, r.RecordedBy);   // preserved
        Assert.Equal(createdAt, r.CreatedAt);      // preserved
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter "FullyQualifiedName~InBodyReadingTests"`
Expected: FAIL — `InBodyReading` does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace Kheprx.BaseBackend.Health.Domain.Entities;

public sealed class InBodyReading
{
    public Guid Id { get; private set; }
    public Guid SwimmerId { get; private set; }
    public DateOnly ReadingDate { get; private set; }
    public decimal HeightCm { get; private set; }
    public decimal WeightKg { get; private set; }
    public decimal FatPct { get; private set; }
    public decimal MusclePct { get; private set; }
    public decimal BoneDensity { get; private set; }
    public decimal BodyDensity { get; private set; }
    public Guid RecordedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private InBodyReading() { } // EF Core

    public InBodyReading(Guid swimmerId, DateOnly readingDate, decimal heightCm, decimal weightKg,
        decimal fatPct, decimal musclePct, decimal boneDensity, decimal bodyDensity, Guid recordedBy)
    {
        Id = Guid.NewGuid();
        SwimmerId = swimmerId;
        ReadingDate = readingDate;
        HeightCm = heightCm;
        WeightKg = weightKg;
        FatPct = fatPct;
        MusclePct = musclePct;
        BoneDensity = boneDensity;
        BodyDensity = bodyDensity;
        RecordedBy = recordedBy;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(DateOnly readingDate, decimal heightCm, decimal weightKg,
        decimal fatPct, decimal musclePct, decimal boneDensity, decimal bodyDensity)
    {
        ReadingDate = readingDate;
        HeightCm = heightCm;
        WeightKg = weightKg;
        FatPct = fatPct;
        MusclePct = musclePct;
        BoneDensity = boneDensity;
        BodyDensity = bodyDensity;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter "FullyQualifiedName~InBodyReadingTests"`
Expected: PASS.

- [ ] **Step 5: Stage (do not commit)**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Entities/InBodyReading.cs \
        backend/tests/Kheprx.BaseBackend.Health.UnitTests/Entities/InBodyReadingTests.cs
```

---

### Task 2: Persistence — config, DbSet, repository, migration

**Files:**
- Create: `…/Health.Domain/Repositories/IInBodyReadingRepository.cs`
- Create: `…/Health.Infrastructure/Configurations/InBodyReadingConfiguration.cs`
- Create: `…/Health.Infrastructure/Repositories/InBodyReadingRepository.cs`
- Modify: `…/Health.Infrastructure/Data/HealthDbContext.cs` (add DbSet)
- Create (generated): migration `…_CreateInBodyReadingTable.cs` (+ `.Designer.cs`) + snapshot update under `…/Health.Infrastructure/Migrations/`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Repositories/InBodyReadingRepositoryTests.cs`

**Interfaces:**
- Consumes: `InBodyReading` (Task 1).
- Produces:
  - `IInBodyReadingRepository`: `ListBySwimmerAsync(Guid swimmerId, ct) → Task<IReadOnlyList<InBodyReading>>` (newest first), `AddAsync(InBodyReading, ct) → Task`, `GetTrackedAsync(Guid readingId, ct) → Task<InBodyReading?>`, `Remove(InBodyReading) → void`, `SaveChangesAsync(ct) → Task`.
  - `HealthDbContext.InBodyReadings` DbSet.

- [ ] **Step 1: Register the DbSet**

In `HealthDbContext.cs`, after `public DbSet<Observation> Observations => Set<Observation>();` add:

```csharp
    public DbSet<InBodyReading> InBodyReadings => Set<InBodyReading>();
```

- [ ] **Step 2: Write the failing repository tests**

```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Kheprx.BaseBackend.Health.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Repositories;

public class InBodyReadingRepositoryTests
{
    private static HealthDbContext NewDb()
        => new(new DbContextOptionsBuilder<HealthDbContext>().UseInMemoryDatabase($"health-{Guid.NewGuid()}").Options);

    private static InBodyReading Reading(Guid swimmerId, DateOnly date)
        => new(swimmerId, date, 180m, 74m, 12.8m, 42.1m, 1.35m, 1.07m, Guid.NewGuid());

    [Fact]
    public async Task ListBySwimmerAsync_returns_only_that_swimmer_newest_first()
    {
        await using var db = NewDb();
        var repo = new InBodyReadingRepository(db);
        var sw = Guid.NewGuid();
        await repo.AddAsync(Reading(sw, new DateOnly(2024, 6, 15)));
        await repo.AddAsync(Reading(sw, new DateOnly(2024, 10, 4)));
        await repo.AddAsync(Reading(Guid.NewGuid(), new DateOnly(2024, 9, 9))); // other swimmer
        await repo.SaveChangesAsync();

        var list = await repo.ListBySwimmerAsync(sw);

        Assert.Equal(2, list.Count);
        Assert.Equal(new DateOnly(2024, 10, 4), list[0].ReadingDate); // newest first
    }

    [Fact]
    public async Task GetTrackedAsync_returns_reading_and_Remove_deletes_it()
    {
        await using var db = NewDb();
        var repo = new InBodyReadingRepository(db);
        var reading = Reading(Guid.NewGuid(), new DateOnly(2024, 10, 4));
        await repo.AddAsync(reading);
        await repo.SaveChangesAsync();

        var tracked = await repo.GetTrackedAsync(reading.Id);
        Assert.NotNull(tracked);
        repo.Remove(tracked!);
        await repo.SaveChangesAsync();

        Assert.Null(await repo.GetTrackedAsync(reading.Id));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter "FullyQualifiedName~InBodyReadingRepositoryTests"`
Expected: FAIL — repository/interface don't exist (compile errors).

- [ ] **Step 4: Write the repository interface**

```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;

namespace Kheprx.BaseBackend.Health.Domain.Repositories;

public interface IInBodyReadingRepository
{
    Task<IReadOnlyList<InBodyReading>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default);
    Task AddAsync(InBodyReading reading, CancellationToken ct = default);
    Task<InBodyReading?> GetTrackedAsync(Guid readingId, CancellationToken ct = default);
    void Remove(InBodyReading reading);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

- [ ] **Step 5: Write the EF configuration**

```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Health.Infrastructure.Configurations;

internal sealed class InBodyReadingConfiguration : IEntityTypeConfiguration<InBodyReading>
{
    public void Configure(EntityTypeBuilder<InBodyReading> builder)
    {
        builder.ToTable("inbody_reading", "health");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.SwimmerId).IsRequired();     // loose Guid — no cross-module FK
        builder.Property(r => r.ReadingDate).IsRequired();   // DateOnly → date
        builder.Property(r => r.HeightCm).HasPrecision(5, 1).IsRequired();
        builder.Property(r => r.WeightKg).HasPrecision(5, 1).IsRequired();
        builder.Property(r => r.FatPct).HasPrecision(4, 1).IsRequired();
        builder.Property(r => r.MusclePct).HasPrecision(4, 1).IsRequired();
        builder.Property(r => r.BoneDensity).HasPrecision(4, 2).IsRequired();
        builder.Property(r => r.BodyDensity).HasPrecision(4, 2).IsRequired();
        builder.Property(r => r.RecordedBy).IsRequired();    // loose Guid — no cross-module FK
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.HasIndex(r => new { r.SwimmerId, r.ReadingDate });
    }
}
```

- [ ] **Step 6: Write the repository implementation**

```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Health.Infrastructure.Repositories;

internal sealed class InBodyReadingRepository : IInBodyReadingRepository
{
    private readonly HealthDbContext _db;
    public InBodyReadingRepository(HealthDbContext db) => _db = db;

    public async Task<IReadOnlyList<InBodyReading>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default)
        => await _db.InBodyReadings.AsNoTracking()
              .Where(r => r.SwimmerId == swimmerId)
              .OrderByDescending(r => r.ReadingDate).ThenByDescending(r => r.CreatedAt)
              .ToListAsync(ct);

    public async Task AddAsync(InBodyReading reading, CancellationToken ct = default)
        => await _db.InBodyReadings.AddAsync(reading, ct);

    public Task<InBodyReading?> GetTrackedAsync(Guid readingId, CancellationToken ct = default)
        => _db.InBodyReadings.FirstOrDefaultAsync(r => r.Id == readingId, ct);

    public void Remove(InBodyReading reading) => _db.InBodyReadings.Remove(reading);

    public async Task SaveChangesAsync(CancellationToken ct = default) => await _db.SaveChangesAsync(ct);
}
```

- [ ] **Step 7: Run the repository tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter "FullyQualifiedName~InBodyReadingRepositoryTests"`
Expected: PASS.

- [ ] **Step 8: Generate the migration (do NOT apply it)**

Ensure the API is not running, then from repo root:

```bash
dotnet ef migrations add CreateInBodyReadingTable \
  --project backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure \
  --startup-project backend/Kheprx.BaseBackend.Api \
  --context HealthDbContext
```

Confirm the generated `Up()` creates `health.inbody_reading` with a `date` `reading_date`, the six numeric columns at the right precisions, a `timestamp with time zone` `created_at`, and a `(swimmer_id, reading_date)` index (no FKs — loose Guids). **Do not** run `dotnet ef database update`.

- [ ] **Step 9: Stage (do not commit)**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Repositories/IInBodyReadingRepository.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Configurations/InBodyReadingConfiguration.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Repositories/InBodyReadingRepository.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Data/HealthDbContext.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Migrations/ \
        backend/tests/Kheprx.BaseBackend.Health.UnitTests/Repositories/InBodyReadingRepositoryTests.cs
```

---

### Task 3: Application DTOs + validator + messages

**Files:**
- Create: `…/Health.Application/DTOs/InBodyReadingDtos.cs`
- Create: `…/Health.Application/Validators/CreateInBodyReadingRequestValidator.cs`
- Create: `…/Health.Application/Resources/InBodyReadingMessages.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Validators/CreateInBodyReadingRequestValidatorTests.cs`

**Interfaces:**
- Consumes: `AppLanguage` (`Kheprx.BaseBackend.SharedKernel.Resources`).
- Produces:
  - `CreateInBodyReadingRequest(DateOnly ReadingDate, decimal HeightCm, decimal WeightKg, decimal FatPct, decimal MusclePct, decimal BoneDensity, decimal BodyDensity)`.
  - `InBodyReadingDto(Guid Id, DateOnly ReadingDate, decimal HeightCm, decimal WeightKg, decimal FatPct, decimal MusclePct, decimal BoneDensity, decimal BodyDensity, Guid RecordedBy)`.
  - `CreateInBodyReadingRequestValidator`.
  - `InBodyReadingMessages` (Success: `Listed`, `Created`, `Updated`, `Deleted`; Errors: `NotFound`, `DateRequired`, `ValueInvalid`).

- [ ] **Step 1: Write the DTOs**

```csharp
namespace Kheprx.BaseBackend.Health.Application.DTOs;

/// <summary>Payload to create/update an InBody reading (date user-provided; recorded_by from the current user).</summary>
public sealed record CreateInBodyReadingRequest(
    DateOnly ReadingDate,
    decimal HeightCm,
    decimal WeightKg,
    decimal FatPct,
    decimal MusclePct,
    decimal BoneDensity,
    decimal BodyDensity);

/// <summary>An InBody reading.</summary>
public sealed record InBodyReadingDto(
    Guid Id,
    DateOnly ReadingDate,
    decimal HeightCm,
    decimal WeightKg,
    decimal FatPct,
    decimal MusclePct,
    decimal BoneDensity,
    decimal BodyDensity,
    Guid RecordedBy);
```

- [ ] **Step 2: Write the messages**

```csharp
namespace Kheprx.BaseBackend.Health.Application.Resources;

/// <summary>Localized messages for InBody readings.</summary>
public static class InBodyReadingMessages
{
    public static class Success
    {
        public static string Listed(string lang) => lang switch { "ar" => "قياسات InBody", _ => "InBody readings" };
        public static string Created(string lang) => lang switch { "ar" => "تم تسجيل القياس", _ => "Reading recorded" };
        public static string Updated(string lang) => lang switch { "ar" => "تم تحديث القياس", _ => "Reading updated" };
        public static string Deleted(string lang) => lang switch { "ar" => "تم حذف القياس", _ => "Reading deleted" };
    }

    public static class Errors
    {
        public static string NotFound(string lang) => lang switch { "ar" => "القياس غير موجود", _ => "Reading not found" };
        public static string DateRequired(string lang) => lang switch { "ar" => "تاريخ القياس مطلوب", _ => "Reading date is required" };
        public static string ValueInvalid(string lang) => lang switch { "ar" => "قيمة قياس غير صالحة", _ => "A measurement value is invalid" };
    }
}
```

- [ ] **Step 3: Write the failing validator test**

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Validators;

public class CreateInBodyReadingRequestValidatorTests
{
    private readonly CreateInBodyReadingRequestValidator _validator = new();

    private static CreateInBodyReadingRequest Valid()
        => new(new DateOnly(2024, 10, 4), 180m, 74m, 12.8m, 42.1m, 1.35m, 1.07m);

    [Fact]
    public void Valid_request_passes() => Assert.True(_validator.Validate(Valid()).IsValid);

    [Fact]
    public void Missing_date_fails()
    {
        var result = _validator.Validate(Valid() with { ReadingDate = default });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ReadingDate");
    }

    [Fact]
    public void Non_positive_height_fails()
        => Assert.False(_validator.Validate(Valid() with { HeightCm = 0m }).IsValid);

    [Fact]
    public void Fat_over_100_fails()
    {
        var result = _validator.Validate(Valid() with { FatPct = 101m });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "FatPct");
    }

    [Fact]
    public void Bone_density_over_column_max_fails()
        => Assert.False(_validator.Validate(Valid() with { BoneDensity = 100m }).IsValid); // numeric(4,2) max 99.99
}
```

- [ ] **Step 4: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter "FullyQualifiedName~CreateInBodyReadingRequestValidatorTests"`
Expected: FAIL — validator does not exist.

- [ ] **Step 5: Write the validator**

```csharp
using FluentValidation;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Health.Application.Validators;

public sealed class CreateInBodyReadingRequestValidator : AbstractValidator<CreateInBodyReadingRequest>
{
    public CreateInBodyReadingRequestValidator()
    {
        RuleFor(x => x.ReadingDate)
            .NotEqual(default(DateOnly)).WithMessage(_ => InBodyReadingMessages.Errors.DateRequired(AppLanguage.Current));
        RuleFor(x => x.HeightCm).GreaterThan(0m).LessThanOrEqualTo(999.9m).WithMessage(_ => InBodyReadingMessages.Errors.ValueInvalid(AppLanguage.Current));
        RuleFor(x => x.WeightKg).GreaterThan(0m).LessThanOrEqualTo(999.9m).WithMessage(_ => InBodyReadingMessages.Errors.ValueInvalid(AppLanguage.Current));
        RuleFor(x => x.FatPct).InclusiveBetween(0m, 100m).WithMessage(_ => InBodyReadingMessages.Errors.ValueInvalid(AppLanguage.Current));
        RuleFor(x => x.MusclePct).InclusiveBetween(0m, 100m).WithMessage(_ => InBodyReadingMessages.Errors.ValueInvalid(AppLanguage.Current));
        RuleFor(x => x.BoneDensity).GreaterThan(0m).LessThanOrEqualTo(99.99m).WithMessage(_ => InBodyReadingMessages.Errors.ValueInvalid(AppLanguage.Current));
        RuleFor(x => x.BodyDensity).GreaterThan(0m).LessThanOrEqualTo(99.99m).WithMessage(_ => InBodyReadingMessages.Errors.ValueInvalid(AppLanguage.Current));
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter "FullyQualifiedName~CreateInBodyReadingRequestValidatorTests"`
Expected: PASS.

- [ ] **Step 7: Stage (do not commit)**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/DTOs/InBodyReadingDtos.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Validators/CreateInBodyReadingRequestValidator.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Resources/InBodyReadingMessages.cs \
        backend/tests/Kheprx.BaseBackend.Health.UnitTests/Validators/CreateInBodyReadingRequestValidatorTests.cs
```

---

### Task 4: Service + DI registration

**Files:**
- Create: `…/Health.Application/Services/Interfaces/IInBodyReadingService.cs`
- Create: `…/Health.Application/Services/InBodyReadingService.cs`
- Modify: `…/Health.Infrastructure/Extensions/HealthModuleExtensions.cs` (register repo + service)
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Services/InBodyReadingServiceTests.cs`

**Interfaces:**
- Consumes: `IInBodyReadingRepository` (Task 2), DTOs (Task 3), `InBodyReading` (Task 1).
- Produces `IInBodyReadingService`:
  - `ListAsync(Guid swimmerId, ct) → Task<IReadOnlyList<InBodyReadingDto>>`.
  - `CreateAsync(Guid swimmerId, CreateInBodyReadingRequest req, Guid recordedBy, ct) → Task<InBodyReadingDto>`.
  - `UpdateAsync(Guid swimmerId, Guid readingId, CreateInBodyReadingRequest req, ct) → Task<InBodyReadingDto?>` (null ⇒ missing/foreign).
  - `DeleteAsync(Guid swimmerId, Guid readingId, ct) → Task<bool>` (false ⇒ missing/foreign).

- [ ] **Step 1: Write the interface**

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;

namespace Kheprx.BaseBackend.Health.Application.Services.Interfaces;

public interface IInBodyReadingService
{
    Task<IReadOnlyList<InBodyReadingDto>> ListAsync(Guid swimmerId, CancellationToken ct = default);
    Task<InBodyReadingDto> CreateAsync(Guid swimmerId, CreateInBodyReadingRequest request, Guid recordedBy, CancellationToken ct = default);
    Task<InBodyReadingDto?> UpdateAsync(Guid swimmerId, Guid readingId, CreateInBodyReadingRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid swimmerId, Guid readingId, CancellationToken ct = default);
}
```

- [ ] **Step 2: Write the failing service tests**

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Services;

public class InBodyReadingServiceTests
{
    private static (InBodyReadingService svc, Mock<IInBodyReadingRepository> repo) Build()
    {
        var repo = new Mock<IInBodyReadingRepository>();
        return (new InBodyReadingService(repo.Object), repo);
    }

    private static CreateInBodyReadingRequest Req() => new(new DateOnly(2024, 10, 4), 180m, 74m, 12.8m, 42.1m, 1.35m, 1.07m);

    [Fact]
    public async Task Create_stamps_recorder_saves_and_returns_dto()
    {
        var (svc, repo) = Build();
        InBodyReading? added = null;
        repo.Setup(r => r.AddAsync(It.IsAny<InBodyReading>(), It.IsAny<CancellationToken>()))
            .Callback<InBodyReading, CancellationToken>((r, _) => added = r).Returns(Task.CompletedTask);
        var swimmerId = Guid.NewGuid();
        var recordedBy = Guid.NewGuid();

        var dto = await svc.CreateAsync(swimmerId, Req(), recordedBy);

        Assert.NotNull(added);
        Assert.Equal(swimmerId, added!.SwimmerId);
        Assert.Equal(recordedBy, added.RecordedBy);
        Assert.Equal(180m, dto.HeightCm);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task List_maps_rows()
    {
        var (svc, repo) = Build();
        var sw = Guid.NewGuid();
        repo.Setup(r => r.ListBySwimmerAsync(sw, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InBodyReading> { new(sw, new DateOnly(2024, 10, 4), 180m, 74m, 12.8m, 42.1m, 1.35m, 1.07m, Guid.NewGuid()) });

        var list = await svc.ListAsync(sw);

        Assert.Single(list);
        Assert.Equal(74m, list[0].WeightKg);
    }

    [Fact]
    public async Task Update_applies_when_owned_and_returns_null_when_foreign_or_missing()
    {
        var (svc, repo) = Build();
        var sw = Guid.NewGuid();
        var owned = new InBodyReading(sw, new DateOnly(2024, 1, 1), 1m, 1m, 1m, 1m, 1m, 1m, Guid.NewGuid());
        repo.Setup(r => r.GetTrackedAsync(owned.Id, It.IsAny<CancellationToken>())).ReturnsAsync(owned);
        var foreign = new InBodyReading(Guid.NewGuid(), new DateOnly(2024, 1, 1), 1m, 1m, 1m, 1m, 1m, 1m, Guid.NewGuid());
        repo.Setup(r => r.GetTrackedAsync(foreign.Id, It.IsAny<CancellationToken>())).ReturnsAsync(foreign);

        var ok = await svc.UpdateAsync(sw, owned.Id, Req());
        var foreignResult = await svc.UpdateAsync(sw, foreign.Id, Req());
        var missing = await svc.UpdateAsync(sw, Guid.NewGuid(), Req());

        Assert.NotNull(ok);
        Assert.Equal(180m, owned.HeightCm);       // updated in place
        Assert.Null(foreignResult);                // ownership guard
        Assert.Null(missing);
    }

    [Fact]
    public async Task Delete_removes_when_owned_false_when_foreign_or_missing()
    {
        var (svc, repo) = Build();
        var sw = Guid.NewGuid();
        var owned = new InBodyReading(sw, new DateOnly(2024, 1, 1), 1m, 1m, 1m, 1m, 1m, 1m, Guid.NewGuid());
        repo.Setup(r => r.GetTrackedAsync(owned.Id, It.IsAny<CancellationToken>())).ReturnsAsync(owned);

        Assert.True(await svc.DeleteAsync(sw, owned.Id));
        repo.Verify(r => r.Remove(owned), Times.Once);
        Assert.False(await svc.DeleteAsync(sw, Guid.NewGuid()));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter "FullyQualifiedName~InBodyReadingServiceTests"`
Expected: FAIL — service does not exist.

- [ ] **Step 4: Write the service**

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;

namespace Kheprx.BaseBackend.Health.Application.Services;

internal sealed class InBodyReadingService : IInBodyReadingService
{
    private readonly IInBodyReadingRepository _readings;
    public InBodyReadingService(IInBodyReadingRepository readings) => _readings = readings;

    public async Task<IReadOnlyList<InBodyReadingDto>> ListAsync(Guid swimmerId, CancellationToken ct = default)
    {
        var rows = await _readings.ListBySwimmerAsync(swimmerId, ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<InBodyReadingDto> CreateAsync(Guid swimmerId, CreateInBodyReadingRequest request, Guid recordedBy, CancellationToken ct = default)
    {
        var reading = new InBodyReading(swimmerId, request.ReadingDate, request.HeightCm, request.WeightKg,
            request.FatPct, request.MusclePct, request.BoneDensity, request.BodyDensity, recordedBy);
        await _readings.AddAsync(reading, ct);
        await _readings.SaveChangesAsync(ct);
        return ToDto(reading);
    }

    public async Task<InBodyReadingDto?> UpdateAsync(Guid swimmerId, Guid readingId, CreateInBodyReadingRequest request, CancellationToken ct = default)
    {
        var reading = await _readings.GetTrackedAsync(readingId, ct);
        if (reading is null || reading.SwimmerId != swimmerId) return null;

        reading.Update(request.ReadingDate, request.HeightCm, request.WeightKg,
            request.FatPct, request.MusclePct, request.BoneDensity, request.BodyDensity);
        await _readings.SaveChangesAsync(ct);
        return ToDto(reading);
    }

    public async Task<bool> DeleteAsync(Guid swimmerId, Guid readingId, CancellationToken ct = default)
    {
        var reading = await _readings.GetTrackedAsync(readingId, ct);
        if (reading is null || reading.SwimmerId != swimmerId) return false;

        _readings.Remove(reading);
        await _readings.SaveChangesAsync(ct);
        return true;
    }

    private static InBodyReadingDto ToDto(InBodyReading r) =>
        new(r.Id, r.ReadingDate, r.HeightCm, r.WeightKg, r.FatPct, r.MusclePct, r.BoneDensity, r.BodyDensity, r.RecordedBy);
}
```

- [ ] **Step 5: Register repository + service in DI**

In `HealthModuleExtensions.cs`, after the `IHealthReadingService` registration add (both `using`s are already present):

```csharp
        services.AddScoped<IInBodyReadingRepository, InBodyReadingRepository>();
        services.AddScoped<IInBodyReadingService, InBodyReadingService>();
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter "FullyQualifiedName~InBodyReadingServiceTests"`
Expected: PASS.

- [ ] **Step 7: Stage (do not commit)**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/Interfaces/IInBodyReadingService.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/InBodyReadingService.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Extensions/HealthModuleExtensions.cs \
        backend/tests/Kheprx.BaseBackend.Health.UnitTests/Services/InBodyReadingServiceTests.cs
```

---

### Task 5: Controller

**Files:**
- Create: `backend/Kheprx.BaseBackend.Api/Controllers/InBodyReadingsController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/InBodyReadingsControllerTests.cs`

**Interfaces:**
- Consumes: `IInBodyReadingService` (Task 4), DTOs (Task 3), `InBodyReadingMessages` (Task 3), `BaseApiController.CurrentUserId()`.
- Produces: HTTP `GET/POST /api/swimmers/{id}/inbody-readings` and `PUT/DELETE /api/swimmers/{id}/inbody-readings/{readingId}`.

- [ ] **Step 1: Write the failing controller tests**

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

public class InBodyReadingsControllerTests
{
    private static InBodyReadingsController Controller(IInBodyReadingService svc)
        => new(svc) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    private static InBodyReadingDto Dto(Guid id) => new(id, new DateOnly(2024, 10, 4), 180m, 74m, 12.8m, 42.1m, 1.35m, 1.07m, Guid.NewGuid());
    private static CreateInBodyReadingRequest Req() => new(new DateOnly(2024, 10, 4), 180m, 74m, 12.8m, 42.1m, 1.35m, 1.07m);

    [Fact]
    public async Task List_returns_200_with_readings()
    {
        var svc = new Mock<IInBodyReadingService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.ListAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Dto(Guid.NewGuid()) });

        var result = await Controller(svc.Object).List(id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<InBodyReadingDto>>>(ok.Value);
        Assert.True(body.SuccessStatus);
        Assert.Single(body.Data!);
    }

    [Fact]
    public async Task Create_returns_201()
    {
        var svc = new Mock<IInBodyReadingService>();
        var id = Guid.NewGuid();
        svc.Setup(s => s.CreateAsync(id, It.IsAny<CreateInBodyReadingRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Dto(Guid.NewGuid()));

        var result = await Controller(svc.Object).Create(id, Req(), CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
    }

    [Fact]
    public async Task Update_returns_200_when_found_404_when_null()
    {
        var svc = new Mock<IInBodyReadingService>();
        var id = Guid.NewGuid();
        var rid = Guid.NewGuid();
        svc.Setup(s => s.UpdateAsync(id, rid, It.IsAny<CreateInBodyReadingRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Dto(rid));
        svc.Setup(s => s.UpdateAsync(id, It.Is<Guid>(g => g != rid), It.IsAny<CreateInBodyReadingRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((InBodyReadingDto?)null);

        var ok = await Controller(svc.Object).Update(id, rid, Req(), CancellationToken.None);
        Assert.IsType<OkObjectResult>(ok.Result);

        var nf = await Controller(svc.Object).Update(id, Guid.NewGuid(), Req(), CancellationToken.None);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(nf.Result).StatusCode);
    }

    [Fact]
    public async Task Delete_returns_200_when_true_404_when_false()
    {
        var svc = new Mock<IInBodyReadingService>();
        var id = Guid.NewGuid();
        var rid = Guid.NewGuid();
        svc.Setup(s => s.DeleteAsync(id, rid, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        svc.Setup(s => s.DeleteAsync(id, It.Is<Guid>(g => g != rid), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Assert.IsType<OkObjectResult>((await Controller(svc.Object).Delete(id, rid, CancellationToken.None)).Result);
        var nf = await Controller(svc.Object).Delete(id, Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(nf.Result).StatusCode);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter "FullyQualifiedName~InBodyReadingsControllerTests"`
Expected: FAIL — controller does not exist.

- [ ] **Step 3: Write the controller**

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

[Route("api/swimmers/{id:guid}/inbody-readings")]
public sealed class InBodyReadingsController : BaseApiController
{
    private readonly IInBodyReadingService _service;
    public InBodyReadingsController(IInBodyReadingService service) => _service = service;

    /// <summary>Lists a swimmer's InBody readings, newest first.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<InBodyReadingDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<InBodyReadingDto>>>> List(Guid id, CancellationToken ct)
    {
        var list = await _service.ListAsync(id, ct);
        return Ok(ApiResponse<IReadOnlyList<InBodyReadingDto>>.Success(InBodyReadingMessages.Success.Listed(AppLanguage.Current), list));
    }

    /// <summary>Records a new InBody reading. Head Coach or Captain only.</summary>
    [HttpPost]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<InBodyReadingDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<InBodyReadingDto>>> Create(Guid id, CreateInBodyReadingRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(id, request, CurrentUserId(), ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<InBodyReadingDto>.Success(InBodyReadingMessages.Success.Created(AppLanguage.Current), created));
    }

    /// <summary>Updates an InBody reading. Head Coach or Captain only.</summary>
    [HttpPut("{readingId:guid}")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<InBodyReadingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InBodyReadingDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<InBodyReadingDto>>> Update(Guid id, Guid readingId, CreateInBodyReadingRequest request, CancellationToken ct)
    {
        var updated = await _service.UpdateAsync(id, readingId, request, ct);
        if (updated is null)
        {
            var nf = ApiResponse<InBodyReadingDto>.Failure(InBodyReadingMessages.Errors.NotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<InBodyReadingDto>.Success(InBodyReadingMessages.Success.Updated(AppLanguage.Current), updated));
    }

    /// <summary>Deletes an InBody reading. Head Coach or Captain only.</summary>
    [HttpDelete("{readingId:guid}")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, Guid readingId, CancellationToken ct)
    {
        var deleted = await _service.DeleteAsync(id, readingId, ct);
        if (!deleted)
        {
            var nf = ApiResponse<object>.Failure(InBodyReadingMessages.Errors.NotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<object>.Success(InBodyReadingMessages.Success.Deleted(AppLanguage.Current), null));
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter "FullyQualifiedName~InBodyReadingsControllerTests"`
Expected: PASS.

- [ ] **Step 5: Full backend sweep**

Run: `dotnet build backend` then `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests` and `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests`
Expected: build clean; all green (new + existing).

- [ ] **Step 6: Stage (do not commit)**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/InBodyReadingsController.cs \
        backend/tests/Kheprx.BaseBackend.Api.UnitTests/InBodyReadingsControllerTests.cs
```

---

### Task 6: Frontend model + DTO + mapper

**Files:**
- Create: `frontend/src/app/features/swimmer-profile/domain/model/inbody-reading.ts`
- Create: `frontend/src/app/features/swimmer-profile/data/dto/inbody-reading.dto.ts`
- Create: `frontend/src/app/features/swimmer-profile/data/dto/inbody-reading.mapper.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/data/dto/inbody-reading.mapper.spec.ts`

**Interfaces:**
- Consumes: `BaseResponseRs<T>` from `@core/network/api/base-response-rs`.
- Produces:
  - Model `InBodyReading { id; readingDate; heightCm; weightKg; fatPct; musclePct; boneDensity; bodyDensity }` (readingDate string, rest numbers).
  - DTOs `InBodyReadingDtoRs`, `InBodyReadingListDtoRs` (`BaseResponseRs<InBodyReadingDtoRs[]>`), `InBodyReadingItemDtoRs` (`BaseResponseRs<InBodyReadingDtoRs>`), `DeleteInBodyReadingItemDtoRs` (`BaseResponseRs<unknown>`), `CreateInBodyReadingDtoRq`, guards `isInBodyReadingListValid` + `isInBodyReadingDtoRsValid`.
  - Mapper `toInBodyReading(dto)` + `toInBodyReadingList(list)`.

- [ ] **Step 1: Write the model**

```ts
export interface InBodyReading {
  id: string;
  readingDate: string;
  heightCm: number;
  weightKg: number;
  fatPct: number;
  musclePct: number;
  boneDensity: number;
  bodyDensity: number;
}
```

- [ ] **Step 2: Write the DTOs**

```ts
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface InBodyReadingDtoRs {
  id: string;
  readingDate: string;
  heightCm: number;
  weightKg: number;
  fatPct: number;
  musclePct: number;
  boneDensity: number;
  bodyDensity: number;
  recordedBy: string;
}
export interface InBodyReadingListDtoRs extends BaseResponseRs<InBodyReadingDtoRs[]> {}
export interface InBodyReadingItemDtoRs extends BaseResponseRs<InBodyReadingDtoRs> {}
export interface DeleteInBodyReadingItemDtoRs extends BaseResponseRs<unknown> {}

export interface CreateInBodyReadingDtoRq {
  readingDate: string;
  heightCm: number;
  weightKg: number;
  fatPct: number;
  musclePct: number;
  boneDensity: number;
  bodyDensity: number;
}

const VALUE_KEYS = ['heightCm', 'weightKg', 'fatPct', 'musclePct', 'boneDensity', 'bodyDensity'] as const;

export function isInBodyReadingDtoRsValid(x: unknown): x is InBodyReadingDtoRs {
  const d = x as InBodyReadingDtoRs;
  if (!d || typeof d !== 'object') return false;
  if (typeof d.id !== 'string' || typeof d.readingDate !== 'string') return false;
  return VALUE_KEYS.every((k) => typeof (d as unknown as Record<string, unknown>)[k] === 'number');
}

export function isInBodyReadingListValid(data: unknown): data is InBodyReadingDtoRs[] {
  return Array.isArray(data) && data.every(isInBodyReadingDtoRsValid);
}
```

- [ ] **Step 3: Write the failing mapper test**

```ts
import { toInBodyReading, toInBodyReadingList } from '@features/swimmer-profile/data/dto/inbody-reading.mapper';
import { InBodyReadingDtoRs } from '@features/swimmer-profile/data/dto/inbody-reading.dto';

const DTO: InBodyReadingDtoRs = {
  id: 'r1', readingDate: '2024-10-04', heightCm: 180, weightKg: 74, fatPct: 12.8, musclePct: 42.1,
  boneDensity: 1.35, bodyDensity: 1.07, recordedBy: 'u1',
};

describe('inbody-reading.mapper', () => {
  it('maps a reading (drops recordedBy)', () => {
    const m = toInBodyReading(DTO);
    expect(m.id).toBe('r1');
    expect(m.readingDate).toBe('2024-10-04');
    expect(m.weightKg).toBe(74);
    expect(m.bodyDensity).toBe(1.07);
    expect((m as unknown as Record<string, unknown>).recordedBy).toBeUndefined();
  });

  it('maps a list', () => {
    expect(toInBodyReadingList([DTO])).toHaveLength(1);
  });
});
```

- [ ] **Step 4: Run test to verify it fails**

Run: `cd frontend && npx jest src/app/features/swimmer-profile/testing/data/dto/inbody-reading.mapper.spec.ts`
Expected: FAIL — mapper not found.

- [ ] **Step 5: Write the mapper**

```ts
import { InBodyReadingDtoRs } from '@features/swimmer-profile/data/dto/inbody-reading.dto';
import { InBodyReading } from '@features/swimmer-profile/domain/model/inbody-reading';

export function toInBodyReading(d: InBodyReadingDtoRs): InBodyReading {
  return {
    id: d.id,
    readingDate: d.readingDate,
    heightCm: d.heightCm,
    weightKg: d.weightKg,
    fatPct: d.fatPct,
    musclePct: d.musclePct,
    boneDensity: d.boneDensity,
    bodyDensity: d.bodyDensity,
  };
}

export function toInBodyReadingList(list: InBodyReadingDtoRs[]): InBodyReading[] {
  return list.map(toInBodyReading);
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `cd frontend && npx jest src/app/features/swimmer-profile/testing/data/dto/inbody-reading.mapper.spec.ts`
Expected: PASS.

- [ ] **Step 7: Stage (do not commit)**

```bash
git add frontend/src/app/features/swimmer-profile/domain/model/inbody-reading.ts \
        frontend/src/app/features/swimmer-profile/data/dto/inbody-reading.dto.ts \
        frontend/src/app/features/swimmer-profile/data/dto/inbody-reading.mapper.ts \
        frontend/src/app/features/swimmer-profile/testing/data/dto/inbody-reading.mapper.spec.ts
```

---

### Task 7: Frontend repository interface + impl

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/domain/repositories/swimmer-profile.repository.ts`
- Modify: `frontend/src/app/features/swimmer-profile/data/repositories/swimmer-profile.repository.impl.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/data/repositories/swimmer-profile.repository.impl.spec.ts` (add tests)

**Interfaces:**
- Consumes: DTOs (Task 6); `HttpClientService`.
- Produces on `ISwimmerProfileRepository`:
  - `getInBodyReadings(id): Promise<InBodyReadingListDtoRs>`
  - `createInBodyReading(id, rq): Promise<InBodyReadingItemDtoRs>`
  - `updateInBodyReading(id, readingId, rq): Promise<InBodyReadingItemDtoRs>`
  - `deleteInBodyReading(id, readingId): Promise<DeleteInBodyReadingItemDtoRs>`

- [ ] **Step 1: Write the failing repo-impl tests**

Append inside the existing `describe('SwimmerProfileRepositoryImpl', …)`:

```ts
  it('getInBodyReadings GETs the readings endpoint', async () => {
    (http.get as jest.Mock).mockResolvedValue({ successStatus: true, data: [] });
    await repo.getInBodyReadings('sw1');
    expect(http.get).toHaveBeenCalledWith('/api/swimmers/sw1/inbody-readings');
  });

  it('createInBodyReading POSTs to the readings endpoint', async () => {
    (http.post as jest.Mock).mockResolvedValue({ successStatus: true, data: {} });
    const rq = { readingDate: '2024-10-04', heightCm: 180, weightKg: 74, fatPct: 12.8, musclePct: 42.1, boneDensity: 1.35, bodyDensity: 1.07 };
    await repo.createInBodyReading('sw1', rq);
    expect(http.post).toHaveBeenCalledWith('/api/swimmers/sw1/inbody-readings', { body: rq });
  });

  it('updateInBodyReading PUTs the reading endpoint', async () => {
    (http.put as jest.Mock).mockResolvedValue({ successStatus: true, data: {} });
    const rq = { readingDate: '2024-10-04', heightCm: 180, weightKg: 74, fatPct: 12.8, musclePct: 42.1, boneDensity: 1.35, bodyDensity: 1.07 };
    await repo.updateInBodyReading('sw1', 'r1', rq);
    expect(http.put).toHaveBeenCalledWith('/api/swimmers/sw1/inbody-readings/r1', { body: rq });
  });

  it('deleteInBodyReading DELETEs the reading endpoint', async () => {
    ((http as unknown as { delete: jest.Mock }).delete) = jest.fn().mockResolvedValue({ successStatus: true, data: null });
    await repo.deleteInBodyReading('sw1', 'r1');
    expect((http as unknown as { delete: jest.Mock }).delete).toHaveBeenCalledWith('/api/swimmers/sw1/inbody-readings/r1');
  });
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd frontend && npx jest src/app/features/swimmer-profile/testing/data/repositories/swimmer-profile.repository.impl.spec.ts`
Expected: FAIL — methods don't exist.

- [ ] **Step 3: Add the interface methods**

In `swimmer-profile.repository.ts`, add the import and four methods after `createBodyMeasurement`:

```ts
import { InBodyReadingListDtoRs, InBodyReadingItemDtoRs, DeleteInBodyReadingItemDtoRs, CreateInBodyReadingDtoRq } from '@features/swimmer-profile/data/dto/inbody-reading.dto';
```

```ts
  getInBodyReadings(id: string): Promise<InBodyReadingListDtoRs>;
  createInBodyReading(id: string, rq: CreateInBodyReadingDtoRq): Promise<InBodyReadingItemDtoRs>;
  updateInBodyReading(id: string, readingId: string, rq: CreateInBodyReadingDtoRq): Promise<InBodyReadingItemDtoRs>;
  deleteInBodyReading(id: string, readingId: string): Promise<DeleteInBodyReadingItemDtoRs>;
```

- [ ] **Step 4: Implement the methods**

In `swimmer-profile.repository.impl.ts`, add the same import, then after `createBodyMeasurement`:

```ts
  getInBodyReadings(id: string): Promise<InBodyReadingListDtoRs> {
    return this.http.get<InBodyReadingListDtoRs>(`/api/swimmers/${id}/inbody-readings`);
  }
  createInBodyReading(id: string, rq: CreateInBodyReadingDtoRq): Promise<InBodyReadingItemDtoRs> {
    return this.http.post<InBodyReadingItemDtoRs>(`/api/swimmers/${id}/inbody-readings`, { body: rq });
  }
  updateInBodyReading(id: string, readingId: string, rq: CreateInBodyReadingDtoRq): Promise<InBodyReadingItemDtoRs> {
    return this.http.put<InBodyReadingItemDtoRs>(`/api/swimmers/${id}/inbody-readings/${readingId}`, { body: rq });
  }
  deleteInBodyReading(id: string, readingId: string): Promise<DeleteInBodyReadingItemDtoRs> {
    return this.http.delete<DeleteInBodyReadingItemDtoRs>(`/api/swimmers/${id}/inbody-readings/${readingId}`);
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
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/list-inbody-readings.use-case.ts`
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/create-inbody-reading.use-case.ts`
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/update-inbody-reading.use-case.ts`
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/delete-inbody-reading.use-case.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/domain/usecases/inbody-readings.use-cases.spec.ts`

**Interfaces:**
- Consumes: repository (Task 7); DTO guards + mapper (Task 6); `UseCase`, `AppError`, `SWIMMER_PROFILE_REPOSITORY`.
- Produces:
  - `ListInBodyReadingsUseCase extends UseCase<string, InBodyReading[]>`.
  - `CreateInBodyReadingUseCase extends UseCase<{id: string; rq: CreateInBodyReadingDtoRq}, InBodyReading>`.
  - `UpdateInBodyReadingUseCase extends UseCase<{id: string; readingId: string; rq: CreateInBodyReadingDtoRq}, InBodyReading>`.
  - `DeleteInBodyReadingUseCase extends UseCase<{id: string; readingId: string}, void>`.

- [ ] **Step 1: Write the failing use-case tests**

```ts
import { TestBed } from '@angular/core/testing';
import { ListInBodyReadingsUseCase } from '@features/swimmer-profile/domain/usecases/list-inbody-readings.use-case';
import { CreateInBodyReadingUseCase } from '@features/swimmer-profile/domain/usecases/create-inbody-reading.use-case';
import { UpdateInBodyReadingUseCase } from '@features/swimmer-profile/domain/usecases/update-inbody-reading.use-case';
import { DeleteInBodyReadingUseCase } from '@features/swimmer-profile/domain/usecases/delete-inbody-reading.use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

const READING = { id: 'r1', readingDate: '2024-10-04', heightCm: 180, weightKg: 74, fatPct: 12.8, musclePct: 42.1, boneDensity: 1.35, bodyDensity: 1.07, recordedBy: 'u1' };
const RQ = { readingDate: '2024-10-04', heightCm: 180, weightKg: 74, fatPct: 12.8, musclePct: 42.1, boneDensity: 1.35, bodyDensity: 1.07 };

describe('inbody use cases', () => {
  const repo = { getInBodyReadings: jest.fn(), createInBodyReading: jest.fn(), updateInBodyReading: jest.fn(), deleteInBodyReading: jest.fn() } as any;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [
      ListInBodyReadingsUseCase, CreateInBodyReadingUseCase, UpdateInBodyReadingUseCase, DeleteInBodyReadingUseCase,
      { provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo },
    ] });
  });

  it('list maps valid readings', async () => {
    repo.getInBodyReadings.mockResolvedValue({ successStatus: true, data: [READING] });
    const res = await TestBed.inject(ListInBodyReadingsUseCase).run('sw1');
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data[0].weightKg).toBe(74);
  });

  it('list fails on invalid payload', async () => {
    repo.getInBodyReadings.mockResolvedValue({ successStatus: true, data: [{ id: 5 }] });
    const res = await TestBed.inject(ListInBodyReadingsUseCase).run('sw1');
    expect(res.ok).toBe(false);
  });

  it('create maps the returned reading', async () => {
    repo.createInBodyReading.mockResolvedValue({ successStatus: true, data: READING });
    const res = await TestBed.inject(CreateInBodyReadingUseCase).run({ id: 'sw1', rq: RQ });
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data.id).toBe('r1');
    expect(repo.createInBodyReading).toHaveBeenCalledWith('sw1', RQ);
  });

  it('update maps the returned reading', async () => {
    repo.updateInBodyReading.mockResolvedValue({ successStatus: true, data: READING });
    const res = await TestBed.inject(UpdateInBodyReadingUseCase).run({ id: 'sw1', readingId: 'r1', rq: RQ });
    expect(res.ok).toBe(true);
    expect(repo.updateInBodyReading).toHaveBeenCalledWith('sw1', 'r1', RQ);
  });

  it('delete calls the repo', async () => {
    repo.deleteInBodyReading.mockResolvedValue({ successStatus: true, data: null });
    const res = await TestBed.inject(DeleteInBodyReadingUseCase).run({ id: 'sw1', readingId: 'r1' });
    expect(res.ok).toBe(true);
    expect(repo.deleteInBodyReading).toHaveBeenCalledWith('sw1', 'r1');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest src/app/features/swimmer-profile/testing/domain/usecases/inbody-readings.use-cases.spec.ts`
Expected: FAIL — use-case modules not found.

- [ ] **Step 3: Write the list use case**

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { isInBodyReadingListValid } from '@features/swimmer-profile/data/dto/inbody-reading.dto';
import { toInBodyReadingList } from '@features/swimmer-profile/data/dto/inbody-reading.mapper';
import { InBodyReading } from '@features/swimmer-profile/domain/model/inbody-reading';

@Injectable({ providedIn: 'root' })
export class ListInBodyReadingsUseCase extends UseCase<string, InBodyReading[]> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('ListInBodyReadings'); }
  protected async execute(id: string): Promise<InBodyReading[]> {
    const res = await this.repo.getInBodyReadings(id);
    if (!isInBodyReadingListValid(res.data)) throw new AppError('Invalid InBody readings received', 'validation');
    return toInBodyReadingList(res.data);
  }
}
```

- [ ] **Step 4: Write the create use case**

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { CreateInBodyReadingDtoRq, isInBodyReadingDtoRsValid } from '@features/swimmer-profile/data/dto/inbody-reading.dto';
import { toInBodyReading } from '@features/swimmer-profile/data/dto/inbody-reading.mapper';
import { InBodyReading } from '@features/swimmer-profile/domain/model/inbody-reading';

export interface CreateInBodyReadingInput { id: string; rq: CreateInBodyReadingDtoRq; }

@Injectable({ providedIn: 'root' })
export class CreateInBodyReadingUseCase extends UseCase<CreateInBodyReadingInput, InBodyReading> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('CreateInBodyReading'); }
  protected async execute(input: CreateInBodyReadingInput): Promise<InBodyReading> {
    const res = await this.repo.createInBodyReading(input.id, input.rq);
    if (!isInBodyReadingDtoRsValid(res.data)) throw new AppError('Invalid InBody reading received', 'validation');
    return toInBodyReading(res.data);
  }
}
```

- [ ] **Step 5: Write the update use case**

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { CreateInBodyReadingDtoRq, isInBodyReadingDtoRsValid } from '@features/swimmer-profile/data/dto/inbody-reading.dto';
import { toInBodyReading } from '@features/swimmer-profile/data/dto/inbody-reading.mapper';
import { InBodyReading } from '@features/swimmer-profile/domain/model/inbody-reading';

export interface UpdateInBodyReadingInput { id: string; readingId: string; rq: CreateInBodyReadingDtoRq; }

@Injectable({ providedIn: 'root' })
export class UpdateInBodyReadingUseCase extends UseCase<UpdateInBodyReadingInput, InBodyReading> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('UpdateInBodyReading'); }
  protected async execute(input: UpdateInBodyReadingInput): Promise<InBodyReading> {
    const res = await this.repo.updateInBodyReading(input.id, input.readingId, input.rq);
    if (!isInBodyReadingDtoRsValid(res.data)) throw new AppError('Invalid InBody reading received', 'validation');
    return toInBodyReading(res.data);
  }
}
```

- [ ] **Step 6: Write the delete use case**

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

export interface DeleteInBodyReadingInput { id: string; readingId: string; }

@Injectable({ providedIn: 'root' })
export class DeleteInBodyReadingUseCase extends UseCase<DeleteInBodyReadingInput, void> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('DeleteInBodyReading'); }
  protected async execute(input: DeleteInBodyReadingInput): Promise<void> {
    await this.repo.deleteInBodyReading(input.id, input.readingId);
  }
}
```

- [ ] **Step 7: Run test to verify it passes**

Run: `cd frontend && npx jest src/app/features/swimmer-profile/testing/domain/usecases/inbody-readings.use-cases.spec.ts`
Expected: PASS.

- [ ] **Step 8: Stage (do not commit)**

```bash
git add frontend/src/app/features/swimmer-profile/domain/usecases/list-inbody-readings.use-case.ts \
        frontend/src/app/features/swimmer-profile/domain/usecases/create-inbody-reading.use-case.ts \
        frontend/src/app/features/swimmer-profile/domain/usecases/update-inbody-reading.use-case.ts \
        frontend/src/app/features/swimmer-profile/domain/usecases/delete-inbody-reading.use-case.ts \
        frontend/src/app/features/swimmer-profile/testing/domain/usecases/inbody-readings.use-cases.spec.ts
```

---

### Task 9: ViewModel InBody state + trend matrix + tab wiring

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.ts`
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts` (extend)

**Interfaces:**
- Consumes: the four use cases (Task 8); `InBodyReading` model (Task 6).
- Produces (on `SwimmerProfileViewModel`): signals `inbodyReadings`, `selectedInBodyId`, `editingInBody`, `editingInBodyId`, `savingInBody`, `loadingInBody`, `confirmingInBodyDelete`, `deletingInBody`, draft `ibDate/ibHeight/ibWeight/ibFat/ibMuscle/ibBone/ibBody`; computed `selectedInBodyReading`, `canSaveInBody`, `inbodyHistory`; methods `startAddInBody()`, `startEditInBody()`, `cancelEditInBody()`, `saveInBody()`, `askDeleteInBody()`, `cancelDeleteInBody()`, `confirmDeleteInBody()`; `activeTab`/`setTab` widened with `'inbody'`. On `SwimmerProfilePage`: `enabledTabs` includes `'inbody'`.

- [ ] **Step 1: Extend the failing viewmodel test**

In `swimmer-profile.viewmodel.spec.ts`:

(a) Add imports:

```ts
import { ListInBodyReadingsUseCase } from '@features/swimmer-profile/domain/usecases/list-inbody-readings.use-case';
import { CreateInBodyReadingUseCase } from '@features/swimmer-profile/domain/usecases/create-inbody-reading.use-case';
import { UpdateInBodyReadingUseCase } from '@features/swimmer-profile/domain/usecases/update-inbody-reading.use-case';
import { DeleteInBodyReadingUseCase } from '@features/swimmer-profile/domain/usecases/delete-inbody-reading.use-case';
```

(b) In `build(...)`, add mocks + providers and return them. Add alongside the body-measurement mocks:

```ts
  const R1 = { id: 'r1', readingDate: '2024-06-15', heightCm: 180, weightKg: 72.5, fatPct: 15.2, musclePct: 40.1, boneDensity: 1.30, bodyDensity: 1.05 };
  const R2 = { id: 'r2', readingDate: '2024-10-04', heightCm: 181, weightKg: 74.0, fatPct: 12.8, musclePct: 42.1, boneDensity: 1.35, bodyDensity: 1.07 };
  const listInBodyUc = { run: jest.fn().mockResolvedValue((over as any).listInBody ?? { ok: true, data: [R2, R1] }) };   // newest first
  const createInBodyUc = { run: jest.fn().mockResolvedValue((over as any).createInBody ?? { ok: true, data: R2 }) };
  const updateInBodyUc = { run: jest.fn().mockResolvedValue((over as any).updateInBody ?? { ok: true, data: R2 }) };
  const deleteInBodyUc = { run: jest.fn().mockResolvedValue((over as any).deleteInBody ?? { ok: true, data: undefined }) };
```

Add to `providers`:

```ts
    { provide: ListInBodyReadingsUseCase, useValue: listInBodyUc },
    { provide: CreateInBodyReadingUseCase, useValue: createInBodyUc },
    { provide: UpdateInBodyReadingUseCase, useValue: updateInBodyUc },
    { provide: DeleteInBodyReadingUseCase, useValue: deleteInBodyUc },
```

Add to the returned object: `listInBodyUc, createInBodyUc, updateInBodyUc, deleteInBodyUc`.

(c) Add a describe block:

```ts
  describe('inbody tab', () => {
    it('setTab("inbody") lazy-loads readings once and selects the latest', async () => {
      const { vm, listInBodyUc } = build();
      await vm.load('s1');
      vm.setTab('inbody');
      await Promise.resolve(); await Promise.resolve();
      expect(vm.activeTab()).toBe('inbody');
      expect(vm.inbodyReadings()).toHaveLength(2);
      expect(vm.selectedInBodyId()).toBe('r2');            // newest first
      expect(vm.selectedInBodyReading()?.id).toBe('r2');
      expect(listInBodyUc.run).toHaveBeenCalledTimes(1);
      vm.setTab('identityVitals');
      vm.setTab('inbody');
      await Promise.resolve();
      expect(listInBodyUc.run).toHaveBeenCalledTimes(1);   // not reloaded
    });

    it('inbodyHistory builds a metric matrix with latest-vs-previous change', async () => {
      const { vm } = build();
      await vm.load('s1');
      vm.setTab('inbody');
      await Promise.resolve(); await Promise.resolve();
      const h = vm.inbodyHistory();
      expect(h.dates).toEqual(['2024-06-15', '2024-10-04']);  // oldest -> newest
      const weight = h.rows.find((r) => r.labelKey === 'swimmerProfile.inbody.weight')!;
      expect(weight.values).toEqual([72.5, 74.0]);
      expect(weight.change).toBe(1.5);                        // 74.0 - 72.5
    });

    it('canSaveInBody enforces date + metric ranges', async () => {
      const { vm } = build();
      await vm.load('s1');
      vm.startAddInBody();
      vm.ibDate.set('');   // startAddInBody sets today; clear it
      expect(vm.canSaveInBody()).toBe(false);
      vm.ibDate.set('2024-10-04');
      vm.ibHeight.set('180'); vm.ibWeight.set('74'); vm.ibFat.set('12.8'); vm.ibMuscle.set('42.1'); vm.ibBone.set('1.35'); vm.ibBody.set('1.07');
      expect(vm.canSaveInBody()).toBe(true);
      vm.ibFat.set('101');   // out of 0..100
      expect(vm.canSaveInBody()).toBe(false);
    });

    it('saveInBody creates, toasts and reloads; delete flow confirms and reloads', async () => {
      const { vm, createInBodyUc, deleteInBodyUc, listInBodyUc, notify } = build();
      await vm.load('s1');
      vm.setTab('inbody');
      await Promise.resolve(); await Promise.resolve();
      vm.startAddInBody();
      vm.ibHeight.set('181'); vm.ibWeight.set('74'); vm.ibFat.set('12.8'); vm.ibMuscle.set('42.1'); vm.ibBone.set('1.35'); vm.ibBody.set('1.07');
      await vm.saveInBody();
      expect(createInBodyUc.run).toHaveBeenCalled();
      expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.readingSaved');
      expect(listInBodyUc.run).toHaveBeenCalledTimes(2);     // load + reload after save

      vm.askDeleteInBody();
      expect(vm.confirmingInBodyDelete()).toBe(true);
      await vm.confirmDeleteInBody();
      expect(deleteInBodyUc.run).toHaveBeenCalled();
      expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.readingDeleted');
      expect(listInBodyUc.run).toHaveBeenCalledTimes(3);     // reload after delete
    });
  });
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd frontend && npx jest src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts`
Expected: FAIL — new members/use cases don't exist.

- [ ] **Step 3: Wire the use cases + state into the viewmodel**

In `swimmer-profile.viewmodel.ts`:

(a) Add imports:

```ts
import { ListInBodyReadingsUseCase } from '@features/swimmer-profile/domain/usecases/list-inbody-readings.use-case';
import { CreateInBodyReadingUseCase } from '@features/swimmer-profile/domain/usecases/create-inbody-reading.use-case';
import { UpdateInBodyReadingUseCase } from '@features/swimmer-profile/domain/usecases/update-inbody-reading.use-case';
import { DeleteInBodyReadingUseCase } from '@features/swimmer-profile/domain/usecases/delete-inbody-reading.use-case';
import { InBodyReading } from '@features/swimmer-profile/domain/model/inbody-reading';
```

(b) Inject the use cases (beside the body-measurement injects):

```ts
  private readonly listInBodyUc = inject(ListInBodyReadingsUseCase);
  private readonly createInBodyUc = inject(CreateInBodyReadingUseCase);
  private readonly updateInBodyUc = inject(UpdateInBodyReadingUseCase);
  private readonly deleteInBodyUc = inject(DeleteInBodyReadingUseCase);
```

(c) Widen the tab type in BOTH the `activeTab` signal and the `setTab` parameter to `'identityVitals' | 'guardian' | 'physiological' | 'inbody'`. Add `private inbodyLoaded = false;` beside `bodyMeasurementLoaded`.

(d) Add InBody state (after the body-measurement state block):

```ts
  // InBody state
  readonly inbodyReadings = signal<InBodyReading[]>([]);
  readonly selectedInBodyId = signal('');
  readonly loadingInBody = signal(false);
  readonly editingInBody = signal(false);
  readonly editingInBodyId = signal<string | null>(null);
  readonly savingInBody = signal(false);
  readonly confirmingInBodyDelete = signal(false);
  readonly deletingInBody = signal(false);
  readonly ibDate = signal(''); readonly ibHeight = signal(''); readonly ibWeight = signal('');
  readonly ibFat = signal(''); readonly ibMuscle = signal(''); readonly ibBone = signal(''); readonly ibBody = signal('');

  readonly selectedInBodyReading = computed(() =>
    this.inbodyReadings().find((r) => r.id === this.selectedInBodyId()) ?? this.inbodyReadings()[0] ?? null);

  readonly canSaveInBody = computed(() => {
    const num = (v: string) => (v.trim().length > 0 && Number.isFinite(Number(v)) ? Number(v) : NaN);
    const h = num(this.ibHeight()), w = num(this.ibWeight()), f = num(this.ibFat()),
          mu = num(this.ibMuscle()), bo = num(this.ibBone()), bd = num(this.ibBody());
    return this.ibDate().length > 0
      && h > 0 && h <= 999.9 && w > 0 && w <= 999.9
      && f >= 0 && f <= 100 && mu >= 0 && mu <= 100
      && bo > 0 && bo <= 99.99 && bd > 0 && bd <= 99.99;
  });

  readonly inbodyHistory = computed(() => {
    const readings = [...this.inbodyReadings()].sort((a, b) => a.readingDate.localeCompare(b.readingDate)); // oldest -> newest
    const metrics = [
      { key: 'weightKg' as const, labelKey: 'swimmerProfile.inbody.weight', unit: 'kg' },
      { key: 'fatPct' as const, labelKey: 'swimmerProfile.inbody.fatPercent', unit: '%' },
      { key: 'musclePct' as const, labelKey: 'swimmerProfile.inbody.musclePercent', unit: '%' },
      { key: 'boneDensity' as const, labelKey: 'swimmerProfile.inbody.boneDensity', unit: '' },
      { key: 'bodyDensity' as const, labelKey: 'swimmerProfile.inbody.bodyDensity', unit: '' },
    ];
    return {
      dates: readings.map((r) => r.readingDate),
      rows: metrics.map((m) => {
        const values = readings.map((r) => r[m.key]);
        const n = values.length;
        const change = n >= 2 ? Math.round((values[n - 1] - values[n - 2]) * 100) / 100 : null;
        return { labelKey: m.labelKey, unit: m.unit, values, change };
      }),
    };
  });
```

(e) In `load(...)`, alongside the other resets, add:

```ts
    this.inbodyLoaded = false;
    this.inbodyReadings.set([]);
    this.editingInBody.set(false);
    this.confirmingInBodyDelete.set(false);
```

(f) Update `setTab(...)`:

```ts
  setTab(key: 'identityVitals' | 'guardian' | 'physiological' | 'inbody'): void {
    this.activeTab.set(key);
    if (key === 'guardian' && !this.guardiansLoaded) void this.loadGuardians();
    if (key === 'physiological' && !this.bodyMeasurementLoaded) void this.loadBodyMeasurement();
    if (key === 'inbody' && !this.inbodyLoaded) void this.loadInBody();
  }
```

(g) Add the InBody methods (after `saveBodyMeasurement()`):

```ts
  private async loadInBody(): Promise<void> {
    this.inbodyLoaded = true;
    this.loadingInBody.set(true);
    const r = await this.listInBodyUc.run(this.swimmerId);
    this.loadingInBody.set(false);
    if (r.ok) {
      this.inbodyReadings.set(r.data);
      this.selectedInBodyId.set(r.data[0]?.id ?? '');
    } else {
      this.inbodyLoaded = false;
      this.inbodyReadings.set([]);
      this.selectedInBodyId.set('');
    }
  }

  startAddInBody(): void {
    this.editingInBodyId.set(null);
    this.ibDate.set(new Date().toISOString().slice(0, 10));
    this.ibHeight.set(''); this.ibWeight.set(''); this.ibFat.set(''); this.ibMuscle.set(''); this.ibBone.set(''); this.ibBody.set('');
    this.editingInBody.set(true);
  }

  startEditInBody(): void {
    const r = this.selectedInBodyReading();
    if (!r) return;
    this.editingInBodyId.set(r.id);
    this.ibDate.set(r.readingDate);
    this.ibHeight.set(String(r.heightCm)); this.ibWeight.set(String(r.weightKg));
    this.ibFat.set(String(r.fatPct)); this.ibMuscle.set(String(r.musclePct));
    this.ibBone.set(String(r.boneDensity)); this.ibBody.set(String(r.bodyDensity));
    this.editingInBody.set(true);
  }

  cancelEditInBody(): void { this.editingInBody.set(false); this.editingInBodyId.set(null); }

  async saveInBody(): Promise<void> {
    if (!this.canSaveInBody() || this.savingInBody()) return;
    this.savingInBody.set(true);
    const rq = {
      readingDate: this.ibDate(),
      heightCm: Number(this.ibHeight()), weightKg: Number(this.ibWeight()),
      fatPct: Number(this.ibFat()), musclePct: Number(this.ibMuscle()),
      boneDensity: Number(this.ibBone()), bodyDensity: Number(this.ibBody()),
    };
    const readingId = this.editingInBodyId();
    const r = readingId
      ? await this.updateInBodyUc.run({ id: this.swimmerId, readingId, rq })
      : await this.createInBodyUc.run({ id: this.swimmerId, rq });
    this.savingInBody.set(false);
    if (r.ok) {
      this.notify.success(this.i18n.t('swimmerProfile.toasts.readingSaved'));
      this.editingInBody.set(false);
      this.editingInBodyId.set(null);
      await this.loadInBody();
      this.selectedInBodyId.set(r.data.id);
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.saveFailed'));
    }
  }

  askDeleteInBody(): void { this.confirmingInBodyDelete.set(true); }
  cancelDeleteInBody(): void { this.confirmingInBodyDelete.set(false); }

  async confirmDeleteInBody(): Promise<void> {
    const r = this.selectedInBodyReading();
    if (!r || this.deletingInBody()) return;
    this.deletingInBody.set(true);
    const res = await this.deleteInBodyUc.run({ id: this.swimmerId, readingId: r.id });
    this.deletingInBody.set(false);
    if (res.ok) {
      this.notify.success(this.i18n.t('swimmerProfile.toasts.readingDeleted'));
      this.confirmingInBodyDelete.set(false);
      await this.loadInBody();
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.deleteFailed'));
    }
  }
```

- [ ] **Step 4: Enable the tab on the page component**

In `swimmer-profile.page.ts`, change `enabledTabs` to:

```ts
  protected readonly enabledTabs = new Set(['identityVitals', 'guardian', 'physiological', 'inbody']);
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

### Task 10: InBody section markup + i18n

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.html`
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`

**Interfaces:**
- Consumes: viewmodel members (Task 9); i18n keys added below.
- Produces: the rendered InBody section under the `inbody` tab.

- [ ] **Step 1: Add the i18n keys (EN)**

In `en.json` → `swimmerProfile`:
- In `sections`, add `"inbody": "InBody"`.
- Add a new block after the `physiological` block:

```json
    "inbody": {
      "reading": "Reading",
      "addReading": "Add reading",
      "editReading": "Edit reading",
      "deleteReading": "Delete reading",
      "confirmDelete": "Delete this reading?",
      "date": "Date",
      "height": "Height (cm)",
      "weight": "Weight (kg)",
      "fatPercent": "Fat Percentage (%)",
      "musclePercent": "Muscle Percentage (%)",
      "boneDensity": "Bone Density",
      "bodyDensity": "Body Density",
      "measurementHistory": "Measurement History",
      "metric": "Metric",
      "change": "Change",
      "noReadings": "No readings on record yet.",
      "readingsCount": "readings on record"
    },
```
- In `toasts`, add `"readingSaved": "Reading saved"` and `"readingDeleted": "Reading deleted"`.

- [ ] **Step 2: Add the i18n keys (AR)**

In `ar.json` → `swimmerProfile`:
- In `sections`, add `"inbody": "InBody"`.
- Add after `physiological`:

```json
    "inbody": {
      "reading": "القياس",
      "addReading": "إضافة قياس",
      "editReading": "تحرير القياس",
      "deleteReading": "حذف القياس",
      "confirmDelete": "حذف هذا القياس؟",
      "date": "التاريخ",
      "height": "الطول (سم)",
      "weight": "الوزن (كجم)",
      "fatPercent": "نسبة الدهون (%)",
      "musclePercent": "نسبة العضلات (%)",
      "boneDensity": "كثافة العظام",
      "bodyDensity": "كثافة الجسم",
      "measurementHistory": "سجل القياسات",
      "metric": "المؤشر",
      "change": "التغير",
      "noReadings": "لا توجد قياسات مسجلة بعد.",
      "readingsCount": "قياس مسجل"
    },
```
- In `toasts`, add `"readingSaved": "تم حفظ القياس"` and `"readingDeleted": "تم حذف القياس"`.

- [ ] **Step 3: Add the InBody section markup**

In `swimmer-profile.page.html`, immediately after the closing `}` of the `@if (vm.activeTab() === 'physiological') { … }` block (the `}` on the line before the outer `}` / `</div>`) insert:

```html
    @if (vm.activeTab() === 'inbody') {
      <section class="rounded-2xl border border-border bg-surface p-6 shadow-sm">
        <div class="mb-4 flex flex-wrap items-end justify-between gap-3 border-b border-border pb-3">
          <div>
            <h2 class="font-heading text-lg text-ink">{{ 'swimmerProfile.tabs.inbody' | translate }}</h2>
            <p class="mt-0.5 text-sm text-text-secondary">{{ vm.inbodyReadings().length }} {{ 'swimmerProfile.inbody.readingsCount' | translate }}</p>
          </div>
          <div class="flex items-end gap-2">
            @if (vm.inbodyReadings().length > 0) {
              <select class="h-9 rounded-md border border-border bg-surface px-2 text-sm" [value]="vm.selectedInBodyId()" (change)="vm.selectedInBodyId.set($any($event.target).value)">
                @for (r of vm.inbodyReadings(); track r.id) {
                  <option [value]="r.id">{{ r.readingDate }}</option>
                }
              </select>
            }
            @if (vm.canEdit()) {
              <button type="button" class="rounded-md px-3 py-1.5 text-sm font-medium text-primary" (click)="vm.startAddInBody()">{{ 'swimmerProfile.inbody.addReading' | translate }}</button>
              @if (vm.selectedInBodyReading()) {
                <button type="button" class="rounded-md px-3 py-1.5 text-sm font-medium text-primary" (click)="vm.startEditInBody()">{{ 'swimmerProfile.inbody.editReading' | translate }}</button>
                <button type="button" class="rounded-md px-3 py-1.5 text-sm font-medium text-danger" (click)="vm.askDeleteInBody()">{{ 'swimmerProfile.inbody.deleteReading' | translate }}</button>
              }
            }
          </div>
        </div>

        @if (vm.confirmingInBodyDelete()) {
          <div class="mb-4 flex items-center gap-3 rounded-lg border border-danger/40 bg-danger/5 p-3 text-sm">
            <span class="text-ink">{{ 'swimmerProfile.inbody.confirmDelete' | translate }}</span>
            <button type="button" class="rounded-md bg-danger px-3 py-1 text-white disabled:opacity-50" [disabled]="vm.deletingInBody()" (click)="vm.confirmDeleteInBody()">{{ 'swimmerProfile.confirmYes' | translate }}</button>
            <button type="button" class="rounded-md px-3 py-1 text-text-secondary" (click)="vm.cancelDeleteInBody()">{{ 'swimmerProfile.confirmNo' | translate }}</button>
          </div>
        }

        @if (vm.loadingInBody()) {
          <p class="text-sm text-text-secondary">{{ 'swimmerProfile.states.loading' | translate }}</p>
        } @else if (vm.editingInBody()) {
          <form class="mb-6 grid grid-cols-2 gap-4 md:grid-cols-4" (submit)="$event.preventDefault(); vm.saveInBody()">
            <app-text-field [label]="'swimmerProfile.inbody.date' | translate" [value]="vm.ibDate()" (valueChange)="vm.ibDate.set($event)"></app-text-field>
            <app-text-field [label]="'swimmerProfile.inbody.height' | translate" [value]="vm.ibHeight()" (valueChange)="vm.ibHeight.set($event)"></app-text-field>
            <app-text-field [label]="'swimmerProfile.inbody.weight' | translate" [value]="vm.ibWeight()" (valueChange)="vm.ibWeight.set($event)"></app-text-field>
            <app-text-field [label]="'swimmerProfile.inbody.fatPercent' | translate" [value]="vm.ibFat()" (valueChange)="vm.ibFat.set($event)"></app-text-field>
            <app-text-field [label]="'swimmerProfile.inbody.musclePercent' | translate" [value]="vm.ibMuscle()" (valueChange)="vm.ibMuscle.set($event)"></app-text-field>
            <app-text-field [label]="'swimmerProfile.inbody.boneDensity' | translate" [value]="vm.ibBone()" (valueChange)="vm.ibBone.set($event)"></app-text-field>
            <app-text-field [label]="'swimmerProfile.inbody.bodyDensity' | translate" [value]="vm.ibBody()" (valueChange)="vm.ibBody.set($event)"></app-text-field>
            <div class="col-span-2 flex items-end gap-2 md:col-span-4">
              <button type="submit" class="rounded-md bg-primary px-5 py-2.5 text-sm font-semibold text-white disabled:opacity-50" [disabled]="!vm.canSaveInBody() || vm.savingInBody()">{{ 'common.save' | translate }}</button>
              <button type="button" class="rounded-md px-5 py-2.5 text-sm text-text-secondary" (click)="vm.cancelEditInBody()">{{ 'common.cancel' | translate }}</button>
            </div>
          </form>
        } @else if (vm.selectedInBodyReading(); as r) {
          <ul class="mb-6 max-w-2xl divide-y divide-border rounded-lg border border-border">
            <li class="flex items-center gap-3 px-4 py-3 text-sm"><span class="flex-1 text-text-secondary">{{ 'swimmerProfile.inbody.height' | translate }}</span><span class="font-medium text-ink">{{ r.heightCm }}</span></li>
            <li class="flex items-center gap-3 px-4 py-3 text-sm"><span class="flex-1 text-text-secondary">{{ 'swimmerProfile.inbody.weight' | translate }}</span><span class="font-medium text-ink">{{ r.weightKg }}</span></li>
            <li class="flex items-center gap-3 px-4 py-3 text-sm"><span class="flex-1 text-text-secondary">{{ 'swimmerProfile.inbody.fatPercent' | translate }}</span><span class="font-medium text-ink">{{ r.fatPct }}</span></li>
            <li class="flex items-center gap-3 px-4 py-3 text-sm"><span class="flex-1 text-text-secondary">{{ 'swimmerProfile.inbody.musclePercent' | translate }}</span><span class="font-medium text-ink">{{ r.musclePct }}</span></li>
            <li class="flex items-center gap-3 px-4 py-3 text-sm"><span class="flex-1 text-text-secondary">{{ 'swimmerProfile.inbody.boneDensity' | translate }}</span><span class="font-medium text-ink">{{ r.boneDensity }}</span></li>
            <li class="flex items-center gap-3 px-4 py-3 text-sm"><span class="flex-1 text-text-secondary">{{ 'swimmerProfile.inbody.bodyDensity' | translate }}</span><span class="font-medium text-ink">{{ r.bodyDensity }}</span></li>
          </ul>

          @if (vm.inbodyHistory().dates.length > 0) {
            <h3 class="mb-2 text-base font-semibold text-ink">{{ 'swimmerProfile.inbody.measurementHistory' | translate }}</h3>
            <div class="overflow-x-auto">
              <table class="w-full min-w-[32rem] border-collapse text-sm">
                <thead>
                  <tr class="border-b border-border text-left text-text-secondary">
                    <th class="py-2 pe-4 font-semibold">{{ 'swimmerProfile.inbody.metric' | translate }}</th>
                    @for (d of vm.inbodyHistory().dates; track d) { <th class="py-2 pe-4 font-semibold">{{ d }}</th> }
                    <th class="py-2 font-semibold">{{ 'swimmerProfile.inbody.change' | translate }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of vm.inbodyHistory().rows; track row.labelKey) {
                    <tr class="border-b border-border/60">
                      <td class="py-2 pe-4 text-text-secondary">{{ row.labelKey | translate }}</td>
                      @for (v of row.values; track $index) { <td class="py-2 pe-4 text-ink">{{ v }} {{ row.unit }}</td> }
                      <td class="py-2 font-medium" [class.text-success]="(row.change ?? 0) >= 0" [class.text-danger]="(row.change ?? 0) < 0">
                        @if (row.change !== null) { {{ row.change > 0 ? '+' : '' }}{{ row.change }} {{ row.unit }} } @else { — }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        } @else {
          <p class="text-sm text-text-secondary">{{ 'swimmerProfile.inbody.noReadings' | translate }}</p>
        }
      </section>
    }
```

- [ ] **Step 4: Build the frontend + run the full swimmer-profile suite**

Run: `cd frontend && npx ng build` and `npx jest src/app/features/swimmer-profile`
Expected: build succeeds; all swimmer-profile specs pass.

- [ ] **Step 5: Manual visual check (optional)**

Run the app, open a swimmer profile, click the **InBody** tab. Confirm: empty state; coach sees Add; adding a reading with all fields saves and appears in the selector; the selected reading shows values; with ≥2 readings the Measurement History table shows dates + Change deltas (green up / red down); Edit and Delete work; a non-coach sees no Add/Edit/Delete. Toggle EN/AR.

- [ ] **Step 6: Stage (do not commit)**

```bash
git add frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.html \
        frontend/src/app/core/i18n/en.json \
        frontend/src/app/core/i18n/ar.json
```

---

## Final verification (before handing back)

- [ ] Backend: `dotnet build backend` clean; `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests` and `…/Kheprx.BaseBackend.Api.UnitTests` all green (new + existing).
- [ ] Frontend: build succeeds; `cd frontend && npx jest` all green.
- [ ] Migration `CreateInBodyReadingTable` exists on `HealthDbContext` and is **not** applied.
- [ ] Everything staged, **nothing committed** (per the user's standing instruction). Report the staged file list.

## Notes for the executor

- This stacks on the committed Guardian + Physiological work; do not reset those commits.
- If `dotnet ef`/`dotnet test` reports locked DLLs, stop the running API first.
- InBody lives in the **Health module** (loose Guids for swimmer_id/recorded_by, no cross-module FK, no swimmer-exists check) — do not fold it into Identity.
- The `swimmerProfile.tabs.inbody` label already exists in the page tab strip and both i18n bundles; this plan only enables the tab and adds the section + its own keys.
- The controller unit tests set a `DefaultHttpContext` so `CurrentUserId()` (JWT `sub`) resolves to `Guid.Empty` without an NRE; the recorded_by flow is verified at the service level.
