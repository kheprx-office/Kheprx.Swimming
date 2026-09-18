# Health Monitoring — Log a Test Reading — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the existing Captain Panel "Health Monitoring" card to a working "Log a test reading" form that records a `health.health_reading` (server-stamped `reading_date` = creation moment, `recorded_by` = current user).

**Architecture:** Backend mirrors the just-built **Medical Tests** feature layer-for-layer inside the Health module (Domain → Application → Infrastructure → Api) plus an EF migration. Frontend adds a clean-architecture `health-readings` feature slice (repository/use-case) and a Captain Panel page whose viewmodel **reuses** the existing `ListSwimmersUseCase` and `ListMedicalTestsUseCase` for its two dropdowns.

**Tech Stack:** .NET 10 / EF Core / Npgsql / FluentValidation / xUnit + Moq (backend); Angular standalone + signals / Jest (frontend).

**Spec:** `docs/superpowers/specs/2026-09-18-health-monitoring-design.md`

## Global Constraints

- **Scope is write-path only.** No GET/list endpoint, no swimmer-detail page, no edit/delete. (Spec §9.)
- **Roles:** `POST /api/health-readings` → `head_coach,captain`. The `GET /api/medical-tests` list is relaxed to `head_coach,captain`; its create/delete stay `head_coach`.
- **`reading_date` = created moment:** set in the entity ctor to `DateTime.UtcNow`, stored as `timestamptz`. No date input on the form.
- **Cross-module refs are loose Guids:** `swimmer_id` and `recorded_by` have **no** FK (matches `medical_test.created_by`). `medical_test_id` **is** a real FK (same `health` schema).
- **Status is derived, not stored:** `normal` when `lower_bound ≤ value ≤ upper_bound`, else `out`. Computed in the service for the response DTO only.
- **DLL lock:** the running backend locks build outputs — **stop the dev server before `dotnet ef` / `dotnet test`** (see the `dotnet-test-dev-server-lock` note).
- **Commits:** this project has followed a "no commits on `main`" convention. Each task below ends with a commit step per the plan format; **skip/defer the commit if the user is keeping the branch uncommitted.**
- **No controller unit test:** the spec §8 mentions one, but the analogous `MedicalTestsController` has none — controllers calling `CurrentUserId()` need `HttpContext`/claims plumbing absent from these unit tests. The service test (Task 3) covers behavior. This is an intentional, documented deviation.

**Paths:**
- Health module root: `backend/src/Modules/Health/`
- Health tests: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/`
- Api controllers: `backend/Kheprx.BaseBackend.Api/Controllers/`
- Frontend app: `frontend/src/app/`

---

## Task 1: `HealthReading` domain entity

**Files:**
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Entities/HealthReading.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Entities/HealthReadingTests.cs`

**Interfaces:**
- Produces: `HealthReading(Guid swimmerId, Guid medicalTestId, decimal value, Guid recordedBy)` with read-only props `Id, SwimmerId, MedicalTestId, Value, ReadingDate, RecordedBy`. Ctor sets `Id = Guid.NewGuid()` and `ReadingDate = DateTime.UtcNow`.

- [ ] **Step 1: Write the failing test**

`backend/tests/Kheprx.BaseBackend.Health.UnitTests/Entities/HealthReadingTests.cs`:

```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Entities;

public class HealthReadingTests
{
    [Fact]
    public void Ctor_assigns_id_fields_recorder_and_reading_date()
    {
        var swimmerId = Guid.NewGuid();
        var testId = Guid.NewGuid();
        var recordedBy = Guid.NewGuid();

        var r = new HealthReading(swimmerId, testId, 95.5m, recordedBy);

        Assert.NotEqual(Guid.Empty, r.Id);
        Assert.Equal(swimmerId, r.SwimmerId);
        Assert.Equal(testId, r.MedicalTestId);
        Assert.Equal(95.5m, r.Value);
        Assert.Equal(recordedBy, r.RecordedBy);
        Assert.NotEqual(default, r.ReadingDate);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~HealthReadingTests`
Expected: FAIL — `HealthReading` does not exist (compile error).

- [ ] **Step 3: Write minimal implementation**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Entities/HealthReading.cs`:

```csharp
namespace Kheprx.BaseBackend.Health.Domain.Entities;

public sealed class HealthReading
{
    public Guid Id { get; private set; }
    public Guid SwimmerId { get; private set; }
    public Guid MedicalTestId { get; private set; }
    public decimal Value { get; private set; }
    public DateTime ReadingDate { get; private set; }
    public Guid RecordedBy { get; private set; }

    private HealthReading() { } // EF Core

    public HealthReading(Guid swimmerId, Guid medicalTestId, decimal value, Guid recordedBy)
    {
        Id = Guid.NewGuid();
        SwimmerId = swimmerId;
        MedicalTestId = medicalTestId;
        Value = value;
        ReadingDate = DateTime.UtcNow;
        RecordedBy = recordedBy;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~HealthReadingTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Entities/HealthReading.cs \
        backend/tests/Kheprx.BaseBackend.Health.UnitTests/Entities/HealthReadingTests.cs
git commit -m "feat(health): add HealthReading domain entity"
```

---

## Task 2: Persistence — repository, EF config, DbSet, migration

**Files:**
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Repositories/IHealthReadingRepository.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Configurations/HealthReadingConfiguration.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Repositories/HealthReadingRepository.cs`
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Data/HealthDbContext.cs`
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Extensions/HealthModuleExtensions.cs`
- Generated: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Migrations/<timestamp>_CreateHealthReadingTable.cs` (+ snapshot update)
- Modify (doc): `docs/references/swimming-database-diagram.html`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Repositories/HealthReadingRepositoryTests.cs`

**Interfaces:**
- Consumes: `HealthReading` (Task 1); `HealthDbContext`, `MedicalTest`.
- Produces: `IHealthReadingRepository { Task AddAsync(HealthReading, CancellationToken); Task SaveChangesAsync(CancellationToken); }`; `HealthDbContext.HealthReadings` DbSet.

- [ ] **Step 1: Write the failing test**

`backend/tests/Kheprx.BaseBackend.Health.UnitTests/Repositories/HealthReadingRepositoryTests.cs`:

```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Kheprx.BaseBackend.Health.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Repositories;

public class HealthReadingRepositoryTests
{
    private static HealthDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<HealthDbContext>()
            .UseInMemoryDatabase($"health-{Guid.NewGuid()}")
            .Options;
        return new HealthDbContext(options);
    }

    [Fact]
    public async Task Add_then_Save_persists_reading()
    {
        await using var db = NewDb();
        var repo = new HealthReadingRepository(db);
        var recordedBy = Guid.NewGuid();
        var reading = new HealthReading(Guid.NewGuid(), Guid.NewGuid(), 95m, recordedBy);

        await repo.AddAsync(reading);
        await repo.SaveChangesAsync();

        var saved = await db.HealthReadings.AsNoTracking().SingleAsync();
        Assert.Equal(95m, saved.Value);
        Assert.Equal(recordedBy, saved.RecordedBy);
        Assert.NotEqual(default, saved.ReadingDate);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~HealthReadingRepositoryTests`
Expected: FAIL — `HealthReadingRepository` and `HealthDbContext.HealthReadings` do not exist.

- [ ] **Step 3: Create the repository interface**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Repositories/IHealthReadingRepository.cs`:

```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;

namespace Kheprx.BaseBackend.Health.Domain.Repositories;

public interface IHealthReadingRepository
{
    Task AddAsync(HealthReading reading, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

- [ ] **Step 4: Create the EF configuration**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Configurations/HealthReadingConfiguration.cs`:

```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Health.Infrastructure.Configurations;

internal sealed class HealthReadingConfiguration : IEntityTypeConfiguration<HealthReading>
{
    public void Configure(EntityTypeBuilder<HealthReading> builder)
    {
        builder.ToTable("health_reading", "health");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.SwimmerId).IsRequired();     // loose Guid — no cross-module FK
        builder.Property(r => r.MedicalTestId).IsRequired();
        builder.Property(r => r.Value).HasColumnType("numeric(8,2)").IsRequired();
        builder.Property(r => r.ReadingDate).IsRequired();   // DateTime → timestamptz (npgsql default)
        builder.Property(r => r.RecordedBy).IsRequired();    // loose Guid — no cross-module FK

        builder.HasOne<MedicalTest>()
            .WithMany()
            .HasForeignKey(r => r.MedicalTestId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 5: Create the repository implementation**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Repositories/HealthReadingRepository.cs`:

```csharp
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Kheprx.BaseBackend.Health.Infrastructure.Data;

namespace Kheprx.BaseBackend.Health.Infrastructure.Repositories;

internal sealed class HealthReadingRepository : IHealthReadingRepository
{
    private readonly HealthDbContext _db;
    public HealthReadingRepository(HealthDbContext db) => _db = db;

    public async Task AddAsync(HealthReading reading, CancellationToken ct = default)
        => await _db.HealthReadings.AddAsync(reading, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
```

- [ ] **Step 6: Add the DbSet**

In `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Data/HealthDbContext.cs`, add the DbSet next to the existing one:

```csharp
    public DbSet<MedicalTest> MedicalTests => Set<MedicalTest>();
    public DbSet<HealthReading> HealthReadings => Set<HealthReading>();
```

(The existing `ApplyConfigurationsFromAssembly` call auto-registers `HealthReadingConfiguration` — no change needed there.)

- [ ] **Step 7: Register repository in DI**

In `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Extensions/HealthModuleExtensions.cs`, add below the medical-test registration:

```csharp
        services.AddScoped<IMedicalTestRepository, MedicalTestRepository>();
        services.AddScoped<IMedicalTestService, MedicalTestService>();
        services.AddScoped<IHealthReadingRepository, HealthReadingRepository>();
```

(The `IHealthReadingService` line is added in Task 3.)

- [ ] **Step 8: Run the repository test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~HealthReadingRepositoryTests`
Expected: PASS.

- [ ] **Step 9: Generate the EF migration**

Stop the backend dev server first (DLL lock). Then from the repo root:

```bash
dotnet ef migrations add CreateHealthReadingTable \
  --project backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure \
  --startup-project backend/Kheprx.BaseBackend.Api \
  --context HealthDbContext
```

Verify the generated `Up()` creates the `health.health_reading` table with a `numeric(8,2)` `Value`, a `timestamp with time zone` `ReadingDate`, and a foreign key + index on `MedicalTestId` → `health.medical_test`. Confirm `HealthDbContextModelSnapshot.cs` was updated.

- [ ] **Step 10: Update the DB diagram doc**

In `docs/references/swimming-database-diagram.html`, in the `health_reading` table definition, change the `reading_date` column type from `date` to `timestamptz`:

```javascript
   {c:"reading_date",t:"timestamptz",k:"",n:false},
```

- [ ] **Step 11: Build to confirm everything compiles**

Run: `dotnet build backend/backend.sln` (or the Health solution/projects).
Expected: build succeeds.

- [ ] **Step 12: Commit**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Repositories/IHealthReadingRepository.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Configurations/HealthReadingConfiguration.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Repositories/HealthReadingRepository.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Data/HealthDbContext.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Extensions/HealthModuleExtensions.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Migrations/ \
        backend/tests/Kheprx.BaseBackend.Health.UnitTests/Repositories/HealthReadingRepositoryTests.cs \
        docs/references/swimming-database-diagram.html
git commit -m "feat(health): add health_reading persistence + migration"
```

---

## Task 3: Application — DTOs, validator, service, messages

**Files:**
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/DTOs/HealthReadingDtos.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Resources/HealthReadingMessages.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/Interfaces/IHealthReadingService.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/HealthReadingService.cs`
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Validators/CreateHealthReadingRequestValidator.cs`
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Extensions/HealthModuleExtensions.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Services/HealthReadingServiceTests.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Validators/CreateHealthReadingRequestValidatorTests.cs`

**Interfaces:**
- Consumes: `IHealthReadingRepository` (Task 2), `IMedicalTestRepository.GetByIdAsync` (existing), `HealthReading`, `MedicalTest`.
- Produces:
  - `CreateHealthReadingRequest(Guid SwimmerId, Guid MedicalTestId, decimal Value)`
  - `HealthReadingDto(Guid Id, Guid SwimmerId, Guid MedicalTestId, decimal Value, DateTime ReadingDate, Guid RecordedBy, string Status)`
  - `IHealthReadingService.CreateAsync(CreateHealthReadingRequest request, Guid recordedBy, CancellationToken) -> Task<HealthReadingDto?>` (null = referenced test not found)

- [ ] **Step 1: Write the failing service test**

`backend/tests/Kheprx.BaseBackend.Health.UnitTests/Services/HealthReadingServiceTests.cs`:

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Services;

public class HealthReadingServiceTests
{
    private static MedicalTest Glucose() =>
        new("Glucose", "الجلوكوز", "mg/dL", 70m, 110m, Guid.NewGuid());

    private static (HealthReadingService svc, Mock<IHealthReadingRepository> readings) Build(MedicalTest? test)
    {
        var tests = new Mock<IMedicalTestRepository>();
        tests.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(test);
        var readings = new Mock<IHealthReadingRepository>();
        return (new HealthReadingService(readings.Object, tests.Object), readings);
    }

    [Fact]
    public async Task Create_persists_reading_stamps_recorder_and_returns_dto()
    {
        var test = Glucose();
        var (svc, readings) = Build(test);
        HealthReading? added = null;
        readings.Setup(r => r.AddAsync(It.IsAny<HealthReading>(), It.IsAny<CancellationToken>()))
            .Callback<HealthReading, CancellationToken>((r, _) => added = r)
            .Returns(Task.CompletedTask);
        var recordedBy = Guid.NewGuid();
        var req = new CreateHealthReadingRequest(Guid.NewGuid(), test.Id, 95m);

        var dto = await svc.CreateAsync(req, recordedBy);

        Assert.NotNull(dto);
        Assert.NotNull(added);
        Assert.Equal(recordedBy, added!.RecordedBy);
        Assert.Equal(95m, dto!.Value);
        Assert.NotEqual(default, dto.ReadingDate);
        readings.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_derives_normal_status_inside_bounds_and_out_outside()
    {
        var test = Glucose(); // 70..110
        var (svc, _) = Build(test);

        var inside = await svc.CreateAsync(new CreateHealthReadingRequest(Guid.NewGuid(), test.Id, 90m), Guid.NewGuid());
        var low = await svc.CreateAsync(new CreateHealthReadingRequest(Guid.NewGuid(), test.Id, 40m), Guid.NewGuid());
        var boundary = await svc.CreateAsync(new CreateHealthReadingRequest(Guid.NewGuid(), test.Id, 110m), Guid.NewGuid());

        Assert.Equal("normal", inside!.Status);
        Assert.Equal("out", low!.Status);
        Assert.Equal("normal", boundary!.Status); // inclusive upper bound
    }

    [Fact]
    public async Task Create_returns_null_when_test_missing()
    {
        var (svc, readings) = Build(test: null);

        var dto = await svc.CreateAsync(new CreateHealthReadingRequest(Guid.NewGuid(), Guid.NewGuid(), 95m), Guid.NewGuid());

        Assert.Null(dto);
        readings.Verify(r => r.AddAsync(It.IsAny<HealthReading>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~HealthReadingServiceTests`
Expected: FAIL — service/DTOs do not exist.

- [ ] **Step 3: Create the DTOs**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/DTOs/HealthReadingDtos.cs`:

```csharp
namespace Kheprx.BaseBackend.Health.Application.DTOs;

/// <summary>Payload to log a health reading (POST /api/health-readings).</summary>
public sealed record CreateHealthReadingRequest(
    Guid SwimmerId,
    Guid MedicalTestId,
    decimal Value);

/// <summary>A logged health reading. Status is derived vs the test bounds, not persisted.</summary>
public sealed record HealthReadingDto(
    Guid Id,
    Guid SwimmerId,
    Guid MedicalTestId,
    decimal Value,
    DateTime ReadingDate,
    Guid RecordedBy,
    string Status);
```

- [ ] **Step 4: Create the messages**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Resources/HealthReadingMessages.cs`:

```csharp
namespace Kheprx.BaseBackend.Health.Application.Resources;

/// <summary>Localized messages for health readings.</summary>
public static class HealthReadingMessages
{
    public static class Success
    {
        public static string Logged(string lang) => lang switch { "ar" => "تم تسجيل القراءة", _ => "Reading logged" };
    }

    public static class Errors
    {
        public static string TestNotFound(string lang) => lang switch { "ar" => "الفحص الطبي غير موجود", _ => "Medical test not found" };
        public static string SwimmerRequired(string lang) => lang switch { "ar" => "السبّاح مطلوب", _ => "Swimmer is required" };
        public static string TestRequired(string lang) => lang switch { "ar" => "الفحص مطلوب", _ => "Test is required" };
    }
}
```

- [ ] **Step 5: Create the service interface**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/Interfaces/IHealthReadingService.cs`:

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;

namespace Kheprx.BaseBackend.Health.Application.Services.Interfaces;

public interface IHealthReadingService
{
    /// <summary>Logs a reading. Returns null when the referenced medical test does not exist.</summary>
    Task<HealthReadingDto?> CreateAsync(CreateHealthReadingRequest request, Guid recordedBy, CancellationToken ct = default);
}
```

- [ ] **Step 6: Create the service**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/HealthReadingService.cs`:

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

    private static string DeriveStatus(decimal value, MedicalTest test) =>
        value >= test.LowerBound && value <= test.UpperBound ? "normal" : "out";

    private static HealthReadingDto ToDto(HealthReading r, string status) =>
        new(r.Id, r.SwimmerId, r.MedicalTestId, r.Value, r.ReadingDate, r.RecordedBy, status);
}
```

- [ ] **Step 7: Register the service in DI**

In `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Extensions/HealthModuleExtensions.cs`, add below the `IHealthReadingRepository` registration from Task 2:

```csharp
        services.AddScoped<IHealthReadingRepository, HealthReadingRepository>();
        services.AddScoped<IHealthReadingService, HealthReadingService>();
```

Add the required using at the top if not already present:

```csharp
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
```

(`Kheprx.BaseBackend.Health.Application.Services` is already imported for `MedicalTestService`.)

- [ ] **Step 8: Run the service test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~HealthReadingServiceTests`
Expected: PASS.

- [ ] **Step 9: Write the failing validator test**

`backend/tests/Kheprx.BaseBackend.Health.UnitTests/Validators/CreateHealthReadingRequestValidatorTests.cs`:

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Validators;

public class CreateHealthReadingRequestValidatorTests
{
    private static CreateHealthReadingRequest Valid() =>
        new(SwimmerId: Guid.NewGuid(), MedicalTestId: Guid.NewGuid(), Value: 95m);

    private readonly CreateHealthReadingRequestValidator _v = new();

    [Fact] public void Valid_request_passes() => Assert.True(_v.Validate(Valid()).IsValid);

    [Fact]
    public void Empty_ids_fail()
    {
        Assert.False(_v.Validate(Valid() with { SwimmerId = Guid.Empty }).IsValid);
        Assert.False(_v.Validate(Valid() with { MedicalTestId = Guid.Empty }).IsValid);
    }
}
```

- [ ] **Step 10: Run the validator test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~CreateHealthReadingRequestValidatorTests`
Expected: FAIL — validator does not exist.

- [ ] **Step 11: Create the validator**

`backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Validators/CreateHealthReadingRequestValidator.cs`:

```csharp
using FluentValidation;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Health.Application.Validators;

public sealed class CreateHealthReadingRequestValidator : AbstractValidator<CreateHealthReadingRequest>
{
    public CreateHealthReadingRequestValidator()
    {
        RuleFor(x => x.SwimmerId)
            .NotEmpty().WithMessage(_ => HealthReadingMessages.Errors.SwimmerRequired(AppLanguage.Current));
        RuleFor(x => x.MedicalTestId)
            .NotEmpty().WithMessage(_ => HealthReadingMessages.Errors.TestRequired(AppLanguage.Current));
    }
}
```

- [ ] **Step 12: Run the validator test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~CreateHealthReadingRequestValidatorTests`
Expected: PASS.

- [ ] **Step 13: Commit**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/ \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Extensions/HealthModuleExtensions.cs \
        backend/tests/Kheprx.BaseBackend.Health.UnitTests/Services/HealthReadingServiceTests.cs \
        backend/tests/Kheprx.BaseBackend.Health.UnitTests/Validators/CreateHealthReadingRequestValidatorTests.cs
git commit -m "feat(health): add health-reading application service + validator"
```

---

## Task 4: Api — controller + relax medical-tests list auth

**Files:**
- Create: `backend/Kheprx.BaseBackend.Api/Controllers/HealthReadingsController.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/MedicalTestsController.cs`

**Interfaces:**
- Consumes: `IHealthReadingService.CreateAsync` (Task 3); `BaseApiController.CurrentUserId()`, `ApiResponse<T>`, `HealthReadingMessages`, `AppLanguage`.
- Produces: `POST /api/health-readings` (201 on success, 404 when the test is missing), gated to `head_coach,captain`.

- [ ] **Step 1: Create the controller**

`backend/Kheprx.BaseBackend.Api/Controllers/HealthReadingsController.cs`:

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
[Authorize(Roles = "head_coach,captain")]
public sealed class HealthReadingsController : BaseApiController
{
    private readonly IHealthReadingService _service;
    public HealthReadingsController(IHealthReadingService service) => _service = service;

    /// <summary>Logs a swimmer's test reading. Head Coach or Captain only.</summary>
    [HttpPost]
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
}
```

- [ ] **Step 2: Relax the medical-tests list authorization**

In `backend/Kheprx.BaseBackend.Api/Controllers/MedicalTestsController.cs`:

Remove the controller-level attribute:
```csharp
[Route("api/medical-tests")]
[Authorize(Roles = "head_coach")]
public sealed class MedicalTestsController : BaseApiController
```
becomes:
```csharp
[Route("api/medical-tests")]
public sealed class MedicalTestsController : BaseApiController
```

Then add per-action attributes:
- On `List` (above `[HttpGet]`): `[Authorize(Roles = "head_coach,captain")]`
- On `Create` (above `[HttpPost]`): `[Authorize(Roles = "head_coach")]`
- On `Delete` (above `[HttpDelete("{id:guid}")]`): `[Authorize(Roles = "head_coach")]`

- [ ] **Step 3: Build and run the full backend test suite**

Stop the dev server first. Run:
```bash
dotnet build backend/backend.sln
dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests
dotnet test backend/tests/Kheprx.BaseBackend.ArchitectureTests
```
Expected: build succeeds; all Health unit tests pass; architecture tests stay green.

- [ ] **Step 4: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/HealthReadingsController.cs \
        backend/Kheprx.BaseBackend.Api/Controllers/MedicalTestsController.cs
git commit -m "feat(health): add health-readings endpoint; open medical-tests list to captains"
```

---

## Task 5: Frontend `health-readings` feature slice

**Files:**
- Create: `frontend/src/app/features/health-readings/domain/model/health-reading.ts`
- Create: `frontend/src/app/features/health-readings/data/dto/health-reading.dto.ts`
- Create: `frontend/src/app/features/health-readings/domain/repositories/health-reading.repository.ts`
- Create: `frontend/src/app/features/health-readings/data/repositories/health-reading.repository.impl.ts`
- Create: `frontend/src/app/features/health-readings/domain/usecases/create-health-reading.use-case.ts`
- Create: `frontend/src/app/features/health-readings/data/health-reading.providers.ts`
- Modify: `frontend/src/app/app.config.ts`
- Test: `frontend/src/app/features/health-readings/testing/data/repositories/health-reading.repository.impl.spec.ts`
- Test: `frontend/src/app/features/health-readings/testing/domain/usecases/create-health-reading.use-case.spec.ts`

**Interfaces:**
- Produces:
  - `HealthReading { id, swimmerId, medicalTestId, value, readingDate, recordedBy, status }`
  - `CreateHealthReadingDtoRq { swimmerId, medicalTestId, value }`
  - `HEALTH_READING_REPOSITORY` token; `IHealthReadingRepository { create(rq): Promise<HealthReadingItemDtoRs> }`
  - `CreateHealthReadingUseCase extends UseCase<CreateHealthReadingDtoRq, HealthReading>`
  - `HEALTH_READING_PROVIDERS: Provider[]`

- [ ] **Step 1: Create the domain model**

`frontend/src/app/features/health-readings/domain/model/health-reading.ts`:

```typescript
export interface HealthReading {
  id: string;
  swimmerId: string;
  medicalTestId: string;
  value: number;
  readingDate: string;
  recordedBy: string;
  status: string;
}
```

- [ ] **Step 2: Create the DTOs + validation guard**

`frontend/src/app/features/health-readings/data/dto/health-reading.dto.ts`:

```typescript
// health-reading.dto.ts — health-reading request/response DTOs (API_FLOW convention).
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface HealthReadingDtoRs {
  id: string;
  swimmerId: string;
  medicalTestId: string;
  value: number;
  readingDate: string;
  recordedBy: string;
  status: string;
}

export interface HealthReadingItemDtoRs extends BaseResponseRs<HealthReadingDtoRs> {}

export interface CreateHealthReadingDtoRq {
  swimmerId: string;
  medicalTestId: string;
  value: number;
}

export function isHealthReadingDtoRsValid(dto: unknown): dto is HealthReadingDtoRs {
  const d = dto as HealthReadingDtoRs;
  return !!d
    && typeof d.id === 'string'
    && typeof d.swimmerId === 'string'
    && typeof d.medicalTestId === 'string'
    && typeof d.value === 'number'
    && typeof d.readingDate === 'string'
    && typeof d.recordedBy === 'string'
    && typeof d.status === 'string';
}
```

- [ ] **Step 3: Create the repository interface + token**

`frontend/src/app/features/health-readings/domain/repositories/health-reading.repository.ts`:

```typescript
import { InjectionToken } from '@angular/core';
import {
  HealthReadingItemDtoRs,
  CreateHealthReadingDtoRq,
} from '@features/health-readings/data/dto/health-reading.dto';

export interface IHealthReadingRepository {
  create(rq: CreateHealthReadingDtoRq): Promise<HealthReadingItemDtoRs>;
}

export const HEALTH_READING_REPOSITORY = new InjectionToken<IHealthReadingRepository>('HEALTH_READING_REPOSITORY');
```

- [ ] **Step 4: Write the failing repository-impl test**

`frontend/src/app/features/health-readings/testing/data/repositories/health-reading.repository.impl.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { HealthReadingRepositoryImpl } from '@features/health-readings/data/repositories/health-reading.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('HealthReadingRepositoryImpl', () => {
  const http = { post: jest.fn() } as unknown as HttpClientService;
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
});
```

- [ ] **Step 5: Run the test to verify it fails**

Run (from `frontend/`): `npx jest src/app/features/health-readings/testing/data/repositories/health-reading.repository.impl.spec.ts`
Expected: FAIL — `HealthReadingRepositoryImpl` does not exist.

- [ ] **Step 6: Create the repository implementation**

`frontend/src/app/features/health-readings/data/repositories/health-reading.repository.impl.ts`:

```typescript
// health-reading.repository.impl.ts — health-readings repository. Returns the API DTO envelope.
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { IHealthReadingRepository } from '@features/health-readings/domain/repositories/health-reading.repository';
import {
  HealthReadingItemDtoRs,
  CreateHealthReadingDtoRq,
} from '@features/health-readings/data/dto/health-reading.dto';

@Injectable({ providedIn: 'root' })
export class HealthReadingRepositoryImpl implements IHealthReadingRepository {
  private readonly http = inject(HttpClientService);

  create(rq: CreateHealthReadingDtoRq): Promise<HealthReadingItemDtoRs> {
    return this.http.post<HealthReadingItemDtoRs>('/api/health-readings', { body: rq });
  }
}
```

- [ ] **Step 7: Run the repository test to verify it passes**

Run (from `frontend/`): `npx jest src/app/features/health-readings/testing/data/repositories/health-reading.repository.impl.spec.ts`
Expected: PASS.

- [ ] **Step 8: Write the failing use-case test**

`frontend/src/app/features/health-readings/testing/domain/usecases/create-health-reading.use-case.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { CreateHealthReadingUseCase } from '@features/health-readings/domain/usecases/create-health-reading.use-case';
import { HEALTH_READING_REPOSITORY, IHealthReadingRepository } from '@features/health-readings/domain/repositories/health-reading.repository';

const RQ = { swimmerId: 's1', medicalTestId: 't1', value: 95 };
const DTO = { id: 'r1', swimmerId: 's1', medicalTestId: 't1', value: 95, readingDate: '2026-09-18T00:00:00Z', recordedBy: 'u1', status: 'normal' };

function build(repo: IHealthReadingRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: HEALTH_READING_REPOSITORY, useValue: repo }] });
  return TestBed.inject(CreateHealthReadingUseCase);
}

describe('CreateHealthReadingUseCase', () => {
  it('maps the created DTO to a model', async () => {
    const repo = { create: async () => ({ data: DTO }) } as unknown as IHealthReadingRepository;
    const uc = build(repo);
    const r = await uc.run(RQ);
    expect(r.ok).toBe(true);
    if (r.ok) { expect(r.data.id).toBe('r1'); expect(r.data.status).toBe('normal'); }
  });

  it('fails validation when the response is malformed', async () => {
    const repo = { create: async () => ({ data: { id: 'r1' } }) } as unknown as IHealthReadingRepository;
    const uc = build(repo);
    const r = await uc.run(RQ);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
```

- [ ] **Step 9: Run the test to verify it fails**

Run (from `frontend/`): `npx jest src/app/features/health-readings/testing/domain/usecases/create-health-reading.use-case.spec.ts`
Expected: FAIL — `CreateHealthReadingUseCase` does not exist.

- [ ] **Step 10: Create the use-case**

`frontend/src/app/features/health-readings/domain/usecases/create-health-reading.use-case.ts`:

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { HEALTH_READING_REPOSITORY } from '@features/health-readings/domain/repositories/health-reading.repository';
import { CreateHealthReadingDtoRq, isHealthReadingDtoRsValid } from '@features/health-readings/data/dto/health-reading.dto';
import { HealthReading } from '@features/health-readings/domain/model/health-reading';

@Injectable({ providedIn: 'root' })
export class CreateHealthReadingUseCase extends UseCase<CreateHealthReadingDtoRq, HealthReading> {
  private readonly repo = inject(HEALTH_READING_REPOSITORY);
  constructor() { super('CreateHealthReading'); }

  protected async execute(input: CreateHealthReadingDtoRq): Promise<HealthReading> {
    const res = await this.repo.create(input);
    if (!isHealthReadingDtoRsValid(res.data)) throw new AppError('Invalid created health reading received', 'validation');
    const d = res.data;
    return {
      id: d.id, swimmerId: d.swimmerId, medicalTestId: d.medicalTestId,
      value: d.value, readingDate: d.readingDate, recordedBy: d.recordedBy, status: d.status,
    };
  }
}
```

- [ ] **Step 11: Run the use-case test to verify it passes**

Run (from `frontend/`): `npx jest src/app/features/health-readings/testing/domain/usecases/create-health-reading.use-case.spec.ts`
Expected: PASS.

- [ ] **Step 12: Create the providers and register them**

`frontend/src/app/features/health-readings/data/health-reading.providers.ts`:

```typescript
import { Provider } from '@angular/core';
import { HEALTH_READING_REPOSITORY } from '@features/health-readings/domain/repositories/health-reading.repository';
import { HealthReadingRepositoryImpl } from '@features/health-readings/data/repositories/health-reading.repository.impl';

// Live wiring: bind the health-readings repository port to the HTTP impl (/api/health-readings).
export const HEALTH_READING_PROVIDERS: Provider[] = [
  { provide: HEALTH_READING_REPOSITORY, useClass: HealthReadingRepositoryImpl },
];
```

In `frontend/src/app/app.config.ts`, add the import next to the other feature providers:
```typescript
import { HEALTH_READING_PROVIDERS } from '@features/health-readings/data/health-reading.providers';
```
and spread it into the providers array next to `...MEDICAL_TEST_PROVIDERS`:
```typescript
    ...MEDICAL_TEST_PROVIDERS,
    ...HEALTH_READING_PROVIDERS,
```

- [ ] **Step 13: Commit**

```bash
git add frontend/src/app/features/health-readings/ frontend/src/app/app.config.ts
git commit -m "feat(health): add health-readings frontend feature slice"
```

---

## Task 6: Frontend Health Monitoring page + wiring

**Files:**
- Create: `frontend/src/app/features/captain-panel/presentation/pages/health-monitoring/health-monitoring.viewmodel.ts`
- Create: `frontend/src/app/features/captain-panel/presentation/pages/health-monitoring/health-monitoring.page.ts`
- Create: `frontend/src/app/features/captain-panel/presentation/pages/health-monitoring/health-monitoring.page.html`
- Modify: `frontend/src/app/features/captain-panel/index.ts`
- Modify: `frontend/src/app/features/captain-panel/presentation/pages/captain-panel/captain-panel.page.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`
- Test: `frontend/src/app/features/captain-panel/testing/presentation/pages/health-monitoring/health-monitoring.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `ListSwimmersUseCase` (`@features/swimmers/domain/usecases/list-swimmers.use-case`), `ListMedicalTestsUseCase` (`@features/medical-tests/domain/usecases/list-medical-tests.use-case`), `CreateHealthReadingUseCase` (Task 5), `NotificationService`, `TranslateService`, `SelectFieldComponent`, `TextFieldComponent`, `LookupItem`.
- Produces: `HealthMonitoringViewModel`, `HealthMonitoringPage`, route `captain-panel/health-monitoring`.

- [ ] **Step 1: Write the failing viewmodel test**

`frontend/src/app/features/captain-panel/testing/presentation/pages/health-monitoring/health-monitoring.viewmodel.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { HealthMonitoringViewModel } from '@features/captain-panel/presentation/pages/health-monitoring/health-monitoring.viewmodel';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { ListMedicalTestsUseCase } from '@features/medical-tests/domain/usecases/list-medical-tests.use-case';
import { CreateHealthReadingUseCase } from '@features/health-readings/domain/usecases/create-health-reading.use-case';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';

const swimmer = { id: 's1', uid: 'SW-1', nameEn: 'Ahmed', nameAr: null, clubNameEn: null, clubNameAr: null, gender: 'male', age: 15 };
const test1 = { id: 't1', nameEn: 'Glucose', nameAr: 'جلوكوز', unit: 'mg/dL', lowerBound: 70, upperBound: 110, createdAt: 'x' };

function build(over: { create?: unknown } = {}) {
  const swimmersUc = { run: jest.fn().mockResolvedValue({ ok: true, data: [swimmer] }) };
  const testsUc = { run: jest.fn().mockResolvedValue({ ok: true, data: [test1] }) };
  const createUc = { run: jest.fn().mockResolvedValue(over.create ?? { ok: true, data: { id: 'r1', status: 'normal' } }) };
  const notify = { success: jest.fn(), error: jest.fn() };
  const i18n = { t: (k: string) => k };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [
    HealthMonitoringViewModel,
    { provide: ListSwimmersUseCase, useValue: swimmersUc },
    { provide: ListMedicalTestsUseCase, useValue: testsUc },
    { provide: CreateHealthReadingUseCase, useValue: createUc },
    { provide: NotificationService, useValue: notify },
    { provide: TranslateService, useValue: i18n },
  ] });
  return { vm: TestBed.inject(HealthMonitoringViewModel), createUc, notify };
}

describe('HealthMonitoringViewModel', () => {
  it('loads swimmers and tests on construction', async () => {
    const { vm } = build();
    await Promise.resolve(); await Promise.resolve();
    expect(vm.swimmerOptions()).toHaveLength(1);
    expect(vm.testOptions()).toHaveLength(1);
  });

  it('canSubmit requires swimmer, test and a numeric value', async () => {
    const { vm } = build();
    await Promise.resolve(); await Promise.resolve();
    expect(vm.canSubmit()).toBe(false);
    vm.swimmerId.set('s1'); vm.testId.set('t1');
    expect(vm.canSubmit()).toBe(false);
    vm.value.set('95');
    expect(vm.canSubmit()).toBe(true);
  });

  it('selectedTest exposes the chosen test for the range helper', async () => {
    const { vm } = build();
    await Promise.resolve(); await Promise.resolve();
    vm.testId.set('t1');
    expect(vm.selectedTest()?.upperBound).toBe(110);
  });

  it('submit logs the reading, toasts success and clears the value', async () => {
    const { vm, notify } = build();
    await Promise.resolve(); await Promise.resolve();
    vm.swimmerId.set('s1'); vm.testId.set('t1'); vm.value.set('95');
    await vm.submit();
    expect(notify.success).toHaveBeenCalledWith('healthMonitoring.toasts.loggedNormal');
    expect(vm.value()).toBe('');
  });

  it('submit shows an error toast on failure', async () => {
    const { vm, notify } = build({ create: { ok: false, error: { status: 500 } } });
    await Promise.resolve(); await Promise.resolve();
    vm.swimmerId.set('s1'); vm.testId.set('t1'); vm.value.set('95');
    await vm.submit();
    expect(notify.error).toHaveBeenCalledWith('healthMonitoring.toasts.createFailed');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run (from `frontend/`): `npx jest src/app/features/captain-panel/testing/presentation/pages/health-monitoring/health-monitoring.viewmodel.spec.ts`
Expected: FAIL — `HealthMonitoringViewModel` does not exist.

- [ ] **Step 3: Create the viewmodel**

`frontend/src/app/features/captain-panel/presentation/pages/health-monitoring/health-monitoring.viewmodel.ts`:

```typescript
import { Injectable, computed, inject, signal } from '@angular/core';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { ListMedicalTestsUseCase } from '@features/medical-tests/domain/usecases/list-medical-tests.use-case';
import { CreateHealthReadingUseCase } from '@features/health-readings/domain/usecases/create-health-reading.use-case';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';
import { MedicalTest } from '@features/medical-tests/domain/model/medical-test';
import { LookupItem } from '@features/reference/domain/model/reference';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';

@Injectable()
export class HealthMonitoringViewModel {
  private readonly listSwimmers = inject(ListSwimmersUseCase);
  private readonly listTests = inject(ListMedicalTestsUseCase);
  private readonly createReading = inject(CreateHealthReadingUseCase);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  readonly swimmers = signal<SwimmerListItem[]>([]);
  readonly tests = signal<MedicalTest[]>([]);
  readonly loading = signal(false);
  readonly error = signal(false);

  readonly swimmerId = signal('');
  readonly testId = signal('');
  readonly value = signal('');
  readonly submitting = signal(false);

  readonly swimmerOptions = computed<LookupItem[]>(() =>
    this.swimmers().map((s) => ({ id: s.id, nameEn: s.nameEn, nameAr: s.nameAr })));

  readonly testOptions = computed<LookupItem[]>(() =>
    this.tests().map((t) => ({ id: t.id, nameEn: t.nameEn, nameAr: t.nameAr })));

  readonly selectedTest = computed<MedicalTest | null>(() =>
    this.tests().find((t) => t.id === this.testId()) ?? null);

  readonly canSubmit = computed(() => {
    const v = Number(this.value());
    return this.swimmerId().length > 0
      && this.testId().length > 0
      && this.value().trim().length > 0
      && Number.isFinite(v);
  });

  constructor() { void this.load(); }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(false);
    const [s, t] = await Promise.all([this.listSwimmers.run(undefined), this.listTests.run()]);
    this.loading.set(false);
    if (s.ok) this.swimmers.set(s.data); else this.error.set(true);
    if (t.ok) this.tests.set(t.data); else this.error.set(true);
  }

  async submit(): Promise<void> {
    if (!this.canSubmit() || this.submitting()) return;
    this.submitting.set(true);
    const r = await this.createReading.run({
      swimmerId: this.swimmerId(),
      medicalTestId: this.testId(),
      value: Number(this.value()),
    });
    this.submitting.set(false);
    if (r.ok) {
      const key = r.data.status === 'normal'
        ? 'healthMonitoring.toasts.loggedNormal'
        : 'healthMonitoring.toasts.loggedOut';
      this.notify.success(this.i18n.t(key));
      this.value.set('');
    } else {
      this.notify.error(this.i18n.t('healthMonitoring.toasts.createFailed'));
    }
  }
}
```

- [ ] **Step 4: Run the viewmodel test to verify it passes**

Run (from `frontend/`): `npx jest src/app/features/captain-panel/testing/presentation/pages/health-monitoring/health-monitoring.viewmodel.spec.ts`
Expected: PASS.

- [ ] **Step 5: Create the page component**

`frontend/src/app/features/captain-panel/presentation/pages/health-monitoring/health-monitoring.page.ts`:

```typescript
import { Component, inject } from '@angular/core';
import { TranslatePipe } from '@core/i18n';
import { SelectFieldComponent } from '@core/ui/components/select-field.component';
import { TextFieldComponent } from '@core/ui/components/text-field.component';
import { HealthMonitoringViewModel } from './health-monitoring.viewmodel';

@Component({
  selector: 'app-health-monitoring-page',
  standalone: true,
  imports: [TranslatePipe, SelectFieldComponent, TextFieldComponent],
  templateUrl: './health-monitoring.page.html',
})
export class HealthMonitoringPage {
  protected readonly vm = inject(HealthMonitoringViewModel);
}
```

- [ ] **Step 6: Create the page template**

`frontend/src/app/features/captain-panel/presentation/pages/health-monitoring/health-monitoring.page.html`:

```html
<div>
  <header class="mb-8">
    <h1 class="font-heading text-3xl text-ink">{{ 'healthMonitoring.title' | translate }}</h1>
    <p class="mt-2 text-muted">{{ 'healthMonitoring.description' | translate }}</p>
  </header>

  @if (vm.loading()) {
    <p class="text-muted">{{ 'healthMonitoring.states.loading' | translate }}</p>
  } @else if (vm.error()) {
    <p class="text-danger">{{ 'healthMonitoring.states.error' | translate }}</p>
  } @else {
    <section class="rounded-2xl border border-border bg-card p-6 shadow-sm">
      <h2 class="font-heading text-xl text-ink">{{ 'healthMonitoring.log.title' | translate }}</h2>
      <form class="mt-4 grid grid-cols-1 gap-5 md:grid-cols-2 lg:grid-cols-3"
            (submit)="$event.preventDefault(); vm.submit()">
        <app-select-field
          [label]="'healthMonitoring.fields.swimmer' | translate"
          [placeholder]="'healthMonitoring.placeholders.swimmer' | translate"
          [options]="vm.swimmerOptions()"
          [value]="vm.swimmerId()" (valueChange)="vm.swimmerId.set($event)"></app-select-field>

        <app-select-field
          [label]="'healthMonitoring.fields.test' | translate"
          [placeholder]="'healthMonitoring.placeholders.test' | translate"
          [options]="vm.testOptions()"
          [value]="vm.testId()" (valueChange)="vm.testId.set($event)"></app-select-field>

        <app-text-field
          [label]="'healthMonitoring.fields.value' | translate"
          [value]="vm.value()" (valueChange)="vm.value.set($event)"></app-text-field>

        @if (vm.selectedTest(); as t) {
          <p class="md:col-span-2 lg:col-span-3 text-xs text-muted">
            {{ 'healthMonitoring.normalRange' | translate }}: {{ t.lowerBound }}–{{ t.upperBound }} {{ t.unit }}
          </p>
        }

        <div class="flex items-end">
          <button type="submit" class="rounded-md bg-primary px-5 py-2.5 text-sm font-semibold text-white disabled:opacity-50"
            [disabled]="!vm.canSubmit() || vm.submitting()">
            {{ 'healthMonitoring.log.submit' | translate }}
          </button>
        </div>

        <p class="md:col-span-2 lg:col-span-3 text-xs text-muted">{{ 'healthMonitoring.log.helper' | translate }}</p>
      </form>
    </section>
  }
</div>
```

- [ ] **Step 7: Export page + viewmodel from the captain-panel barrel**

In `frontend/src/app/features/captain-panel/index.ts`, append:

```typescript
export { HealthMonitoringPage } from './presentation/pages/health-monitoring/health-monitoring.page';
export { HealthMonitoringViewModel } from './presentation/pages/health-monitoring/health-monitoring.viewmodel';
```

- [ ] **Step 8: Wire the route**

In `frontend/src/app/app.routes.ts`:

Add `HealthMonitoringViewModel` to the captain-panel import:
```typescript
import { RegisterSwimmerViewModel, RegisterCoachViewModel, MedicalTestsViewModel, HealthMonitoringViewModel } from '@features/captain-panel';
```

Add the route object immediately after the `captain-panel/medical-tests` route:
```typescript
      {
        path: 'captain-panel/health-monitoring',
        canActivate: [firstLoginGuard, roleGuard('head_coach', 'captain')],
        loadComponent: () => import('@features/captain-panel').then((m) => m.HealthMonitoringPage),
        providers: [HealthMonitoringViewModel],
      },
```

- [ ] **Step 9: Point the Captain Panel card at the route**

In `frontend/src/app/features/captain-panel/presentation/pages/captain-panel/captain-panel.page.ts`, give the `healthMonitoring` card a route:
```typescript
      { key: 'healthMonitoring', icon: LucideHeartPulse, route: '/captain-panel/health-monitoring' },
```

- [ ] **Step 10: Add English i18n**

In `frontend/src/app/core/i18n/en.json`, add a top-level `healthMonitoring` block (sibling of `medicalTests`):

```json
  "healthMonitoring": {
    "title": "Health Monitoring",
    "description": "Log a swimmer's medical test result against that test's normal bounds.",
    "log": {
      "title": "Log a test reading",
      "helper": "Logged readings are stamped with the current date and time and compared against the test's bounds.",
      "submit": "Log"
    },
    "fields": {
      "swimmer": "Select Swimmer",
      "test": "Test",
      "value": "Value"
    },
    "placeholders": {
      "swimmer": "Choose a swimmer",
      "test": "Choose a test"
    },
    "normalRange": "Normal range",
    "states": {
      "loading": "Loading…",
      "error": "Couldn't load swimmers or tests. Please try again."
    },
    "toasts": {
      "loggedNormal": "Reading logged — within the normal range",
      "loggedOut": "Reading logged — outside the normal range",
      "createFailed": "Couldn't log the reading"
    }
  }
```

- [ ] **Step 11: Add Arabic i18n**

In `frontend/src/app/core/i18n/ar.json`, add the matching `healthMonitoring` block:

```json
  "healthMonitoring": {
    "title": "مراقبة الصحة",
    "description": "سجّل نتيجة فحص طبي لسبّاح مقابل الحدود الطبيعية لذلك الفحص.",
    "log": {
      "title": "تسجيل قراءة فحص",
      "helper": "تُختم القراءات المسجّلة بالتاريخ والوقت الحاليين وتُقارن بحدود الفحص.",
      "submit": "تسجيل"
    },
    "fields": {
      "swimmer": "اختر السبّاح",
      "test": "الفحص",
      "value": "القيمة"
    },
    "placeholders": {
      "swimmer": "اختر سبّاحًا",
      "test": "اختر فحصًا"
    },
    "normalRange": "النطاق الطبيعي",
    "states": {
      "loading": "جارٍ التحميل…",
      "error": "تعذّر تحميل السبّاحين أو الفحوصات. حاول مرة أخرى."
    },
    "toasts": {
      "loggedNormal": "تم تسجيل القراءة — ضمن النطاق الطبيعي",
      "loggedOut": "تم تسجيل القراءة — خارج النطاق الطبيعي",
      "createFailed": "تعذّر تسجيل القراءة"
    }
  }
```

- [ ] **Step 12: Run the full frontend test suite + lint**

Run (from `frontend/`):
```bash
npx jest src/app/features/health-readings src/app/features/captain-panel
npm run lint
```
Expected: all specs pass; lint clean. (If a full run is preferred, `npm test` runs the whole Jest suite — it must stay green.)

- [ ] **Step 13: Manually verify (optional but recommended)**

Start the backend and frontend, log in as Head Coach or Captain, open Captain Panel → Health Monitoring, pick a swimmer + test, enter a value, and confirm the success toast reflects within/outside range. Confirm the reading row appears in the `health.health_reading` table.

- [ ] **Step 14: Commit**

```bash
git add frontend/src/app/features/captain-panel/ frontend/src/app/app.routes.ts frontend/src/app/core/i18n/en.json frontend/src/app/core/i18n/ar.json
git commit -m "feat(health): add Health Monitoring page + route + i18n"
```

---

## Self-Review Notes (author)

- **Spec coverage:** §2 data model → Tasks 1–2 (+ diagram doc in Task 2 Step 10); §3 backend layers → Tasks 1–4; auth relaxation → Task 4 Step 2; §4 frontend slice → Task 5; §4 page/routing/card/i18n → Task 6; §5 data flow → Task 6 viewmodel; §6 error handling → Task 3 (404) + Task 6 (error toast); §7 testing → entity/repo/service/validator (backend) + repo/use-case/viewmodel (frontend). The spec's optional controller unit test is intentionally omitted (see Global Constraints — matches the `MedicalTestsController` precedent).
- **Type consistency:** `CreateHealthReadingDtoRq { swimmerId, medicalTestId, value }` (frontend) maps to `CreateHealthReadingRequest(SwimmerId, MedicalTestId, Value)` (backend). `HealthReadingDto`/`HealthReadingDtoRs` fields align (incl. derived `status`). `IHealthReadingService.CreateAsync` returns `HealthReadingDto?`; controller maps null → 404. `IHealthReadingRepository` (backend, `AddAsync`/`SaveChangesAsync`) is distinct from the frontend `IHealthReadingRepository` (`create`).
- **Status rule:** inclusive bounds — `value >= lower && value <= upper` → `normal`, else `out`; asserted by the boundary case (110) in Task 3.
