# Swimmer Profile — Guardian tab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the second Swimmer Profile tab, **Guardian** — a coach-editable "Guardian Details" card showing a swimmer's father and mother (full name, national ID, phone).

**Architecture:** New `athlete.guardian` table + `reference.guardian_relation` lookup (seeded father/mother) in the Identity module, mirroring how `medical_exam` + `fitness_assessment` were added. A dedicated read/write endpoint pair (`GET`/`PUT /api/swimmers/{id}/guardians`) with a single atomic upsert of both rows. Frontend extends the existing `swimmer-profile` slice with a Guardian domain, makes the tab strip switch between the two built tabs, and renders a view/edit Guardian section lazy-loaded on first open.

**Tech Stack:** .NET 10 (ASP.NET Core controllers, EF Core + Npgsql, FluentValidation, xUnit + Moq), Angular (standalone components, signals, Jest), clean-architecture slices (domain/data/presentation).

**Spec:** `docs/superpowers/specs/2026-09-19-swimmer-profile-guardian-design.md`

## Global Constraints

- **Module:** all backend types live in the **Identity** module (`backend/src/Modules/Identity/...`); guardians persist via `IdentityDbContext` / `ISwimmerProfileRepository` (NOT a standalone repo — mirror how exams are handled).
- **Postgres schemas:** `athlete.guardian`, `reference.guardian_relation` (set via `ToTable(name, schema)`).
- **National ID:** exactly 14 digits — regex `^\d{14}$`, `HasMaxLength(14)`, client + server.
- **Both slots required:** a `PUT` must carry a complete father AND mother (name + national ID + phone each).
- **Auth:** read `[Authorize]`; write `[Authorize(Roles = "head_coach,captain")]`.
- **Response envelope:** every endpoint returns `ApiResponse<T>` via `ApiResponse<T>.Success(msg, data)` / `.Failure(msg, code)`, message from `SwimmerMessages` with `AppLanguage.Current`.
- **Relation codes:** the literal strings `"father"` and `"mother"` (lowercase) key the two slots throughout.
- **Migrations are NOT auto-applied.** After Task 2, run `dotnet ef database update` manually against the Identity module when deploying.
- **Backend test note (from project memory):** running the backend dev server locks the build DLLs — **stop any running backend before `dotnet test`**.
- **Frontend commands:** from `frontend/`: `npm test` (Jest), `npx jest <path>` (single file), `npm run build`.

---

### Task 1: Guardian + GuardianRelation entities, EF configs, DbSets, seeding

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/Guardian.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/GuardianRelation.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/GuardianConfiguration.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/GuardianRelationConfiguration.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentityDbContext.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentitySeeder.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/GuardianTests.cs`

**Interfaces:**
- Produces: `Guardian` entity — ctor `Guardian(Guid swimmerId, Guid relationId, string name, string nationalId, string phone)`, mutator `Update(string name, string nationalId, string phone)`, read-only props `Id, SwimmerId, RelationId, Name, NationalId, Phone`. `GuardianRelation` entity — ctor `GuardianRelation(string code, string nameEn, string? nameAr = null)`, props `Id, Code, NameEn, NameAr`.
- Produces: `IdentityDbContext.Guardians`, `IdentityDbContext.GuardianRelations` DbSets.

- [ ] **Step 1: Write the failing test**

Create `GuardianTests.cs`:
```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class GuardianTests
{
    [Fact]
    public void Ctor_sets_fields_and_new_id()
    {
        var swimmerId = Guid.NewGuid();
        var relationId = Guid.NewGuid();

        var g = new Guardian(swimmerId, relationId, " Hassan Ali ", "27001010123456", "+201009876543");

        Assert.NotEqual(Guid.Empty, g.Id);
        Assert.Equal(swimmerId, g.SwimmerId);
        Assert.Equal(relationId, g.RelationId);
        Assert.Equal("Hassan Ali", g.Name); // trimmed
        Assert.Equal("27001010123456", g.NationalId);
        Assert.Equal("+201009876543", g.Phone);
    }

    [Fact]
    public void Update_mutates_name_nationalId_phone()
    {
        var g = new Guardian(Guid.NewGuid(), Guid.NewGuid(), "Old", "27001010123456", "+201000000000");

        g.Update(" Fatima Ibrahim ", "27505050123456", "+201005554444");

        Assert.Equal("Fatima Ibrahim", g.Name);
        Assert.Equal("27505050123456", g.NationalId);
        Assert.Equal("+201005554444", g.Phone);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~GuardianTests"`
Expected: FAIL to compile — `Guardian` does not exist.

- [ ] **Step 3: Write the entities**

`Guardian.cs`:
```csharp
namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class Guardian
{
    public Guid Id { get; private set; }
    public Guid SwimmerId { get; private set; }
    public Guid RelationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NationalId { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;

    private Guardian() { } // EF Core

    public Guardian(Guid swimmerId, Guid relationId, string name, string nationalId, string phone)
    {
        Id = Guid.NewGuid();
        SwimmerId = swimmerId;
        RelationId = relationId;
        Name = name.Trim();
        NationalId = nationalId.Trim();
        Phone = phone.Trim();
    }

    public void Update(string name, string nationalId, string phone)
    {
        Name = name.Trim();
        NationalId = nationalId.Trim();
        Phone = phone.Trim();
    }
}
```

`GuardianRelation.cs` (mirror `FitnessAssessment.cs`):
```csharp
namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class GuardianRelation
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }

    private GuardianRelation() { } // EF Core

    public GuardianRelation(string code, string nameEn, string? nameAr = null)
    {
        Id = Guid.NewGuid();
        Code = code.Trim();
        NameEn = nameEn.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
    }
}
```

- [ ] **Step 4: Write the EF configurations**

`GuardianRelationConfiguration.cs` (mirror `FitnessAssessmentConfiguration.cs`):
```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class GuardianRelationConfiguration : IEntityTypeConfiguration<GuardianRelation>
{
    public void Configure(EntityTypeBuilder<GuardianRelation> builder)
    {
        builder.ToTable("guardian_relation", "reference");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();
        builder.Property(r => r.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(r => r.NameAr).HasMaxLength(100);
    }
}
```

`GuardianConfiguration.cs` (mirror `MedicalExamConfiguration.cs`; note the unique composite index):
```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class GuardianConfiguration : IEntityTypeConfiguration<Guardian>
{
    public void Configure(EntityTypeBuilder<Guardian> builder)
    {
        builder.ToTable("guardian", "athlete");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.SwimmerId).IsRequired();
        builder.Property(g => g.RelationId).IsRequired();
        builder.Property(g => g.Name).HasMaxLength(200).IsRequired();
        builder.Property(g => g.NationalId).HasMaxLength(14).IsRequired();
        builder.Property(g => g.Phone).HasMaxLength(30).IsRequired();
        builder.HasIndex(g => new { g.SwimmerId, g.RelationId }).IsUnique();
        builder.HasOne<SwimmerProfile>().WithMany().HasForeignKey(g => g.SwimmerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<GuardianRelation>().WithMany().HasForeignKey(g => g.RelationId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 5: Register DbSets**

In `IdentityDbContext.cs`, after the `MedicalExams` line (L19), add:
```csharp
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<GuardianRelation> GuardianRelations => Set<GuardianRelation>();
```

- [ ] **Step 6: Seed the relation lookup**

In `IdentitySeeder.cs`, add the call inside `SeedAsync` right after `await EnsureFitnessAssessments(db, ct);` (L23):
```csharp
        await EnsureGuardianRelations(db, ct);
```
Then add the method (mirror `EnsureFitnessAssessments`):
```csharp
    private static async Task EnsureGuardianRelations(IdentityDbContext db, CancellationToken ct)
    {
        var rows = new (string Code, string En, string Ar)[]
        {
            ("father", "Father", "الأب"),
            ("mother", "Mother", "الأم"),
        };
        foreach (var (code, en, ar) in rows)
            if (!await db.GuardianRelations.AnyAsync(r => r.Code == code, ct))
                await db.GuardianRelations.AddAsync(new GuardianRelation(code, en, ar), ct);
    }
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~GuardianTests"`
Expected: PASS (2 tests).

- [ ] **Step 8: Commit**

```bash
git add backend/src/Modules/Identity backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/GuardianTests.cs
git commit -m "feat(guardian): entities, EF configs, DbSets, relation seeding"
```

---

### Task 2: EF migration `AddGuardianAndGuardianRelation`

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Migrations/*_AddGuardianAndGuardianRelation.cs` (generated)
- Modify: `.../Migrations/IdentityDbContextModelSnapshot.cs` (generated)

**Interfaces:**
- Consumes: entities + configs + DbSets from Task 1.
- Produces: a migration creating `reference.guardian_relation` and `athlete.guardian` with the unique `(swimmer_id, relation_id)` index.

- [ ] **Step 1: Ensure no backend dev server is running** (it locks the DLLs the EF tools rebuild).

- [ ] **Step 2: Generate the migration**

The EF tooling targets the Infrastructure project with the design-time factory (`IdentityDbContextFactory`). From repo root:
```bash
dotnet ef migrations add AddGuardianAndGuardianRelation \
  --project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --startup-project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure
```
(If the existing migration `20260918224209_AddFitnessAssessmentAndMedicalExam` used a different `--startup-project`, match that invocation — check the Identity module README or how it was generated.)

- [ ] **Step 3: Inspect the generated migration**

Open the new `*_AddGuardianAndGuardianRelation.cs`. Verify it:
- Creates table `guardian_relation` in schema `reference` (Id, Code, NameEn, NameAr) with a unique index on `Code`.
- Creates table `guardian` in schema `athlete` (Id, SwimmerId, RelationId, Name, NationalId varchar(14), Phone) with FKs to `swimmer_profile` (cascade) and `guardian_relation` (restrict) and a **unique** index on `(SwimmerId, RelationId)`.
It must NOT alter any existing table. If it contains unrelated changes, the model has drifted — stop and investigate.

- [ ] **Step 4: Verify the solution builds**

Run: `dotnet build backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Migrations
git commit -m "feat(guardian): EF migration AddGuardianAndGuardianRelation (not applied)"
```

---

### Task 3: Guardian read model + repository methods

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/ReadModels/GuardianRow.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/ISwimmerProfileRepository.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/SwimmerProfileRepository.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/SwimmerProfileRepositoryTests.cs` (add cases)

**Interfaces:**
- Consumes: `Guardian`, `GuardianRelation`, DbSets (Task 1).
- Produces on `ISwimmerProfileRepository`:
  - `Task<IReadOnlyList<GuardianRow>> ListGuardiansAsync(Guid swimmerId, CancellationToken ct = default)`
  - `Task<Guid?> GetGuardianRelationIdByCodeAsync(string code, CancellationToken ct = default)`
  - `Task<Guardian?> GetGuardianTrackedAsync(Guid swimmerId, Guid relationId, CancellationToken ct = default)`
  - `Task AddGuardianAsync(Guardian guardian, CancellationToken ct = default)`
- Produces: `GuardianRow(Guid Id, string RelationCode, string Name, string NationalId, string Phone)`.

- [ ] **Step 1: Write the failing test**

Open `SwimmerProfileRepositoryTests.cs` and inspect its existing harness (it uses `new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())`). Follow the same helper it already uses to construct a context + repository. Add these cases (adapt the context/repo construction to the file's existing helper):

```csharp
[Fact]
public async Task ListGuardiansAsync_returns_rows_joined_to_relation_code()
{
    await using var db = NewDb(); // use the file's existing context factory helper
    var repo = new SwimmerProfileRepository(db);
    var swimmerId = Guid.NewGuid();
    var father = new GuardianRelation("father", "Father", "الأب");
    var mother = new GuardianRelation("mother", "Mother", "الأم");
    db.GuardianRelations.AddRange(father, mother);
    db.Guardians.Add(new Guardian(swimmerId, father.Id, "Hassan Ali", "27001010123456", "+201009876543"));
    db.Guardians.Add(new Guardian(swimmerId, mother.Id, "Fatima Ibrahim", "27505050123456", "+201005554444"));
    await db.SaveChangesAsync();

    var rows = await repo.ListGuardiansAsync(swimmerId);

    Assert.Equal(2, rows.Count);
    Assert.Contains(rows, r => r.RelationCode == "father" && r.Name == "Hassan Ali" && r.NationalId == "27001010123456");
    Assert.Contains(rows, r => r.RelationCode == "mother" && r.Name == "Fatima Ibrahim");
}

[Fact]
public async Task GetGuardianRelationIdByCodeAsync_resolves_seeded_code()
{
    await using var db = NewDb();
    var repo = new SwimmerProfileRepository(db);
    var father = new GuardianRelation("father", "Father", "الأب");
    db.GuardianRelations.Add(father);
    await db.SaveChangesAsync();

    Assert.Equal(father.Id, await repo.GetGuardianRelationIdByCodeAsync("father"));
    Assert.Null(await repo.GetGuardianRelationIdByCodeAsync("nonexistent"));
}

[Fact]
public async Task GetGuardianTrackedAsync_returns_existing_row_for_swimmer_and_relation()
{
    await using var db = NewDb();
    var repo = new SwimmerProfileRepository(db);
    var swimmerId = Guid.NewGuid();
    var relationId = Guid.NewGuid();
    db.Guardians.Add(new Guardian(swimmerId, relationId, "Hassan Ali", "27001010123456", "+201009876543"));
    await db.SaveChangesAsync();

    var found = await repo.GetGuardianTrackedAsync(swimmerId, relationId);
    Assert.NotNull(found);
    Assert.Equal("Hassan Ali", found!.Name);
    Assert.Null(await repo.GetGuardianTrackedAsync(swimmerId, Guid.NewGuid()));
}
```
> If the test file does not already expose a `NewDb()`/context helper, reuse whatever construction the existing tests in this file use (they already build an in-memory `IdentityDbContext`); name your helper to match.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~SwimmerProfileRepositoryTests"`
Expected: FAIL to compile — the methods and `GuardianRow` do not exist.

- [ ] **Step 3: Add the read model**

`GuardianRow.cs`:
```csharp
namespace Kheprx.BaseBackend.Identity.Domain.ReadModels;

/// <summary>Flattened guardian row — guardian joined to its relation code.</summary>
public sealed record GuardianRow(
    Guid Id,
    string RelationCode,
    string Name,
    string NationalId,
    string Phone);
```

- [ ] **Step 4: Declare the interface methods**

In `ISwimmerProfileRepository.cs`, add before the closing brace:
```csharp
    Task<IReadOnlyList<GuardianRow>> ListGuardiansAsync(Guid swimmerId, CancellationToken ct = default);
    Task<Guid?> GetGuardianRelationIdByCodeAsync(string code, CancellationToken ct = default);
    Task<Guardian?> GetGuardianTrackedAsync(Guid swimmerId, Guid relationId, CancellationToken ct = default);
    Task AddGuardianAsync(Guardian guardian, CancellationToken ct = default);
```

- [ ] **Step 5: Implement in the repository**

In `SwimmerProfileRepository.cs`, add these methods (the class already has `using` for Entities, ReadModels, EntityFrameworkCore):
```csharp
    public async Task<IReadOnlyList<GuardianRow>> ListGuardiansAsync(Guid swimmerId, CancellationToken ct = default)
        => await (from g in _db.Guardians.AsNoTracking()
                  where g.SwimmerId == swimmerId
                  join r in _db.GuardianRelations.AsNoTracking() on g.RelationId equals r.Id
                  select new GuardianRow(g.Id, r.Code, g.Name, g.NationalId, g.Phone))
                 .ToListAsync(ct);

    public Task<Guid?> GetGuardianRelationIdByCodeAsync(string code, CancellationToken ct = default)
        => _db.GuardianRelations.AsNoTracking()
              .Where(r => r.Code == code)
              .Select(r => (Guid?)r.Id)
              .FirstOrDefaultAsync(ct);

    public Task<Guardian?> GetGuardianTrackedAsync(Guid swimmerId, Guid relationId, CancellationToken ct = default)
        => _db.Guardians.FirstOrDefaultAsync(g => g.SwimmerId == swimmerId && g.RelationId == relationId, ct);

    public Task AddGuardianAsync(Guardian guardian, CancellationToken ct = default)
        => _db.Guardians.AddAsync(guardian, ct).AsTask();
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~SwimmerProfileRepositoryTests"`
Expected: PASS (existing + 3 new).

- [ ] **Step 7: Commit**

```bash
git add backend/src/Modules/Identity backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/SwimmerProfileRepositoryTests.cs
git commit -m "feat(guardian): GuardianRow + repository read/upsert methods"
```

---

### Task 4: DTOs + request validator

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/GuardianDtos.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Validators/GuardianRequestValidators.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Validators/UpsertGuardiansRequestValidatorTests.cs`

**Interfaces:**
- Produces:
  - `GuardianDto(Guid Id, string RelationCode, string Name, string NationalId, string Phone)`
  - `GuardianInputDto(string Name, string NationalId, string Phone)`
  - `SwimmerGuardiansDto(GuardianDto? Father, GuardianDto? Mother)`
  - `UpsertGuardiansRequest(GuardianInputDto Father, GuardianInputDto Mother)`
  - `UpsertGuardiansRequestValidator : AbstractValidator<UpsertGuardiansRequest>` (auto-registered by the assembly scan; run by the global `ValidationFilter`).

- [ ] **Step 1: Write the failing test**

`UpsertGuardiansRequestValidatorTests.cs` (mirror `UpdateSwimmerIdentityRequestValidatorTests.cs` style):
```csharp
using FluentValidation.TestHelper;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Validators;

public class UpsertGuardiansRequestValidatorTests
{
    private readonly UpsertGuardiansRequestValidator _validator = new();

    private static UpsertGuardiansRequest Valid() => new(
        new GuardianInputDto("Hassan Ali", "27001010123456", "+201009876543"),
        new GuardianInputDto("Fatima Ibrahim", "27505050123456", "+201005554444"));

    [Fact]
    public void Valid_request_passes()
        => _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Father_national_id_not_14_digits_fails()
    {
        var req = Valid() with { Father = new GuardianInputDto("Hassan Ali", "123", "+201009876543") };
        _validator.TestValidate(req).ShouldHaveValidationErrorFor("Father.NationalId");
    }

    [Fact]
    public void Mother_empty_name_fails()
    {
        var req = Valid() with { Mother = new GuardianInputDto("", "27505050123456", "+201005554444") };
        _validator.TestValidate(req).ShouldHaveValidationErrorFor("Mother.Name");
    }

    [Fact]
    public void Empty_phone_fails()
    {
        var req = Valid() with { Father = new GuardianInputDto("Hassan Ali", "27001010123456", "") };
        _validator.TestValidate(req).ShouldHaveValidationErrorFor("Father.Phone");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~UpsertGuardiansRequestValidatorTests"`
Expected: FAIL to compile — DTOs/validator do not exist.

- [ ] **Step 3: Write the DTOs**

`GuardianDtos.cs`:
```csharp
namespace Kheprx.BaseBackend.Identity.Application.DTOs;

/// <summary>One guardian for the read shape (GET /api/swimmers/{id}/guardians).</summary>
public sealed record GuardianDto(Guid Id, string RelationCode, string Name, string NationalId, string Phone);

/// <summary>A swimmer's guardians — father/mother slots, either may be null.</summary>
public sealed record SwimmerGuardiansDto(GuardianDto? Father, GuardianDto? Mother);

/// <summary>One guardian slot in an upsert payload.</summary>
public sealed record GuardianInputDto(string Name, string NationalId, string Phone);

/// <summary>Payload to upsert both guardians (PUT /api/swimmers/{id}/guardians).</summary>
public sealed record UpsertGuardiansRequest(GuardianInputDto Father, GuardianInputDto Mother);
```

- [ ] **Step 4: Write the validator**

`GuardianRequestValidators.cs` (reuse `UserValidationRules.PhonePattern` and `CoachMessages` used by the existing coach validator; confirm those namespaces from `UserRequestValidators.cs` and add the matching `using`s):
```csharp
using FluentValidation;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Identity.Application.Validators;

public sealed class UpsertGuardiansRequestValidator : AbstractValidator<UpsertGuardiansRequest>
{
    public UpsertGuardiansRequestValidator()
    {
        RuleFor(x => x.Father).NotNull().SetValidator(new GuardianInputDtoValidator());
        RuleFor(x => x.Mother).NotNull().SetValidator(new GuardianInputDtoValidator());
    }
}

internal sealed class GuardianInputDtoValidator : AbstractValidator<GuardianInputDto>
{
    public GuardianInputDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(_ => CommonMessages.Errors.FullNameRequired(AppLanguage.Current))
            .MaximumLength(200).WithMessage(_ => CommonMessages.Errors.FullNameTooLong(AppLanguage.Current));
        RuleFor(x => x.NationalId)
            .NotEmpty().Matches(@"^\d{14}$").WithMessage(_ => CoachMessages.Errors.NationalIdInvalid(AppLanguage.Current));
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage(_ => CoachMessages.Errors.PhoneRequired(AppLanguage.Current))
            .Matches(UserValidationRules.PhonePattern).WithMessage(_ => UserMessages.Errors.PhoneInvalid(AppLanguage.Current));
    }
}
```
> Verify the exact `AppLanguage`, `CommonMessages`, `CoachMessages`, `UserMessages`, `UserValidationRules` namespaces from the top of `UserRequestValidators.cs` and copy its `using` block — those helpers already back the coach validator.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~UpsertGuardiansRequestValidatorTests"`
Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add backend/src/Modules/Identity backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Validators/UpsertGuardiansRequestValidatorTests.cs
git commit -m "feat(guardian): DTOs + upsert request validator"
```

---

### Task 5: Service methods + messages

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/ISwimmerService.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Resources/SwimmerMessages.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs` (add cases)

**Interfaces:**
- Consumes: repository methods (Task 3), DTOs (Task 4).
- Produces on `ISwimmerService`:
  - `Task<SwimmerGuardiansDto?> GetGuardiansAsync(Guid id, CancellationToken ct = default)` — `null` ⇒ swimmer not found.
  - `Task<bool> UpsertGuardiansAsync(Guid id, UpsertGuardiansRequest request, CancellationToken ct = default)` — `false` ⇒ swimmer not found.
- Produces on `SwimmerMessages.Success`: `GuardiansRetrieved(lang)`, `GuardiansSaved(lang)`.

- [ ] **Step 1: Write the failing test**

Open `SwimmerServiceTests.cs`, note its existing factory helper `CreateService(...)` (it returns `(SwimmerService svc, Mock<ISwimmerProfileRepository> swimmers, ...)`). Add:
```csharp
[Fact]
public async Task GetGuardiansAsync_returns_null_when_swimmer_missing()
{
    var (svc, swimmers, _) = CreateService();
    var id = Guid.NewGuid();
    swimmers.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((SwimmerProfile?)null);

    Assert.Null(await svc.GetGuardiansAsync(id));
}

[Fact]
public async Task GetGuardiansAsync_maps_father_and_mother_slots()
{
    var (svc, swimmers, _) = CreateService();
    var id = Guid.NewGuid();
    swimmers.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid()));
    swimmers.Setup(r => r.ListGuardiansAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<GuardianRow>
    {
        new(Guid.NewGuid(), "father", "Hassan Ali", "27001010123456", "+201009876543"),
        new(Guid.NewGuid(), "mother", "Fatima Ibrahim", "27505050123456", "+201005554444"),
    });

    var dto = await svc.GetGuardiansAsync(id);

    Assert.NotNull(dto);
    Assert.Equal("Hassan Ali", dto!.Father!.Name);
    Assert.Equal("Fatima Ibrahim", dto.Mother!.Name);
}

[Fact]
public async Task UpsertGuardiansAsync_returns_false_when_swimmer_missing()
{
    var (svc, swimmers, _) = CreateService();
    var id = Guid.NewGuid();
    swimmers.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((SwimmerProfile?)null);

    var req = new UpsertGuardiansRequest(
        new GuardianInputDto("Hassan Ali", "27001010123456", "+201009876543"),
        new GuardianInputDto("Fatima Ibrahim", "27505050123456", "+201005554444"));

    Assert.False(await svc.UpsertGuardiansAsync(id, req));
}

[Fact]
public async Task UpsertGuardiansAsync_inserts_when_slot_absent_and_updates_when_present()
{
    var (svc, swimmers, _) = CreateService();
    var id = Guid.NewGuid();
    var fatherRel = Guid.NewGuid();
    var motherRel = Guid.NewGuid();
    swimmers.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid()));
    swimmers.Setup(r => r.GetGuardianRelationIdByCodeAsync("father", It.IsAny<CancellationToken>())).ReturnsAsync(fatherRel);
    swimmers.Setup(r => r.GetGuardianRelationIdByCodeAsync("mother", It.IsAny<CancellationToken>())).ReturnsAsync(motherRel);
    // father already exists → updated; mother absent → inserted
    var existingFather = new Guardian(id, fatherRel, "Old Name", "27001010123456", "+201000000000");
    swimmers.Setup(r => r.GetGuardianTrackedAsync(id, fatherRel, It.IsAny<CancellationToken>())).ReturnsAsync(existingFather);
    swimmers.Setup(r => r.GetGuardianTrackedAsync(id, motherRel, It.IsAny<CancellationToken>())).ReturnsAsync((Guardian?)null);

    var req = new UpsertGuardiansRequest(
        new GuardianInputDto("Hassan Ali", "27001010123456", "+201009876543"),
        new GuardianInputDto("Fatima Ibrahim", "27505050123456", "+201005554444"));

    var ok = await svc.UpsertGuardiansAsync(id, req);

    Assert.True(ok);
    Assert.Equal("Hassan Ali", existingFather.Name); // updated in place
    swimmers.Verify(r => r.AddGuardianAsync(It.Is<Guardian>(g => g.RelationId == motherRel && g.Name == "Fatima Ibrahim"), It.IsAny<CancellationToken>()), Times.Once);
    swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
}
```
> Match the exact tuple/positional shape of this file's `CreateService()` helper and its `using`s (it already references `SwimmerProfile`, `Moq`, the DTO namespace). Add `using` for `Kheprx.BaseBackend.Identity.Domain.ReadModels;` and `Kheprx.BaseBackend.Identity.Domain.Entities;` if not present.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~SwimmerServiceTests"`
Expected: FAIL to compile — service methods do not exist.

- [ ] **Step 3: Add messages**

In `SwimmerMessages.Success` add:
```csharp
        public static string GuardiansRetrieved(string lang) => lang switch { "ar" => "بيانات ولي الأمر", _ => "Guardian details" };
        public static string GuardiansSaved(string lang) => lang switch { "ar" => "تم حفظ بيانات ولي الأمر", _ => "Guardian details saved" };
```

- [ ] **Step 4: Declare the interface methods**

In `ISwimmerService.cs` add:
```csharp
    Task<SwimmerGuardiansDto?> GetGuardiansAsync(Guid id, CancellationToken ct = default);
    Task<bool> UpsertGuardiansAsync(Guid id, UpsertGuardiansRequest request, CancellationToken ct = default);
```

- [ ] **Step 5: Implement in the service**

In `SwimmerService.cs` add (uses the injected `_swimmers` repository; add a `using Kheprx.BaseBackend.Identity.Domain.Exceptions;` — already present):
```csharp
    public async Task<SwimmerGuardiansDto?> GetGuardiansAsync(Guid id, CancellationToken ct = default)
    {
        var profile = await _swimmers.GetByIdTrackedAsync(id, ct);
        if (profile is null) return null;

        var rows = await _swimmers.ListGuardiansAsync(id, ct);
        return new SwimmerGuardiansDto(
            MapGuardian(rows.FirstOrDefault(r => r.RelationCode == "father")),
            MapGuardian(rows.FirstOrDefault(r => r.RelationCode == "mother")));
    }

    private static GuardianDto? MapGuardian(Kheprx.BaseBackend.Identity.Domain.ReadModels.GuardianRow? r)
        => r is null ? null : new GuardianDto(r.Id, r.RelationCode, r.Name, r.NationalId, r.Phone);

    public async Task<bool> UpsertGuardiansAsync(Guid id, UpsertGuardiansRequest request, CancellationToken ct = default)
    {
        var profile = await _swimmers.GetByIdTrackedAsync(id, ct);
        if (profile is null) return false;

        await UpsertOne(id, "father", request.Father, ct);
        await UpsertOne(id, "mother", request.Mother, ct);
        await _swimmers.SaveChangesAsync(ct);
        return true;
    }

    private async Task UpsertOne(Guid swimmerId, string relationCode, GuardianInputDto input, CancellationToken ct)
    {
        var relationId = await _swimmers.GetGuardianRelationIdByCodeAsync(relationCode, ct)
            ?? throw new InvalidUserException($"Guardian relation '{relationCode}' is not configured.");

        var existing = await _swimmers.GetGuardianTrackedAsync(swimmerId, relationId, ct);
        if (existing is null)
            await _swimmers.AddGuardianAsync(new Guardian(swimmerId, relationId, input.Name, input.NationalId, input.Phone), ct);
        else
            existing.Update(input.Name, input.NationalId, input.Phone);
    }
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~SwimmerServiceTests"`
Expected: PASS (existing + 4 new).

- [ ] **Step 7: Commit**

```bash
git add backend/src/Modules/Identity backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs
git commit -m "feat(guardian): service get/upsert + messages"
```

---

### Task 6: Controller endpoints

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs` (add cases)

**Interfaces:**
- Consumes: `ISwimmerService.GetGuardiansAsync`, `UpsertGuardiansAsync`, `SwimmerGuardiansDto`, `UpsertGuardiansRequest` (Task 5).
- Produces: `GET api/swimmers/{id}/guardians` (200/404), `PUT api/swimmers/{id}/guardians` (200/404). Validation (400) is handled by the global `ValidationFilter` before the action runs.

- [ ] **Step 1: Write the failing test**

In `SwimmersControllerTests.cs` add (mirror the existing `GetById`/`UpdateIdentity` cases):
```csharp
[Fact]
public async Task GetGuardians_returns_200_with_guardians()
{
    var svc = new Mock<ISwimmerService>();
    var id = Guid.NewGuid();
    var dto = new SwimmerGuardiansDto(
        new GuardianDto(Guid.NewGuid(), "father", "Hassan Ali", "27001010123456", "+201009876543"),
        new GuardianDto(Guid.NewGuid(), "mother", "Fatima Ibrahim", "27505050123456", "+201005554444"));
    svc.Setup(s => s.GetGuardiansAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

    var result = await new SwimmersController(svc.Object).GetGuardians(id, CancellationToken.None);

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var body = Assert.IsType<ApiResponse<SwimmerGuardiansDto>>(ok.Value);
    Assert.True(body.SuccessStatus);
    Assert.Equal("Hassan Ali", body.Data!.Father!.Name);
}

[Fact]
public async Task GetGuardians_returns_404_when_service_returns_null()
{
    var svc = new Mock<ISwimmerService>();
    var id = Guid.NewGuid();
    svc.Setup(s => s.GetGuardiansAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((SwimmerGuardiansDto?)null);

    var result = await new SwimmersController(svc.Object).GetGuardians(id, CancellationToken.None);

    var nf = Assert.IsType<ObjectResult>(result.Result);
    Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
}

[Fact]
public async Task UpsertGuardians_returns_200_when_saved()
{
    var svc = new Mock<ISwimmerService>();
    var id = Guid.NewGuid();
    svc.Setup(s => s.UpsertGuardiansAsync(id, It.IsAny<UpsertGuardiansRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    var req = new UpsertGuardiansRequest(
        new GuardianInputDto("Hassan Ali", "27001010123456", "+201009876543"),
        new GuardianInputDto("Fatima Ibrahim", "27505050123456", "+201005554444"));

    var result = await new SwimmersController(svc.Object).UpsertGuardians(id, req, CancellationToken.None);

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var body = Assert.IsType<ApiResponse<object>>(ok.Value);
    Assert.True(body.SuccessStatus);
}

[Fact]
public async Task UpsertGuardians_returns_404_when_service_returns_false()
{
    var svc = new Mock<ISwimmerService>();
    var id = Guid.NewGuid();
    svc.Setup(s => s.UpsertGuardiansAsync(id, It.IsAny<UpsertGuardiansRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
    var req = new UpsertGuardiansRequest(
        new GuardianInputDto("Hassan Ali", "27001010123456", "+201009876543"),
        new GuardianInputDto("Fatima Ibrahim", "27505050123456", "+201005554444"));

    var result = await new SwimmersController(svc.Object).UpsertGuardians(id, req, CancellationToken.None);

    var nf = Assert.IsType<ObjectResult>(result.Result);
    Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter "FullyQualifiedName~SwimmersControllerTests"`
Expected: FAIL to compile — controller actions do not exist.

- [ ] **Step 3: Add the endpoints**

In `SwimmersController.cs`, before the final closing brace add a new region (mirror the exam endpoints exactly):
```csharp
    #region Guardians — GET / PUT api/swimmers/{id}/guardians

    /// <summary>Returns a swimmer's guardians (father + mother; either may be null).</summary>
    /// <response code="200">The guardians.</response>
    /// <response code="404">No swimmer with that id.</response>
    [HttpGet("{id:guid}/guardians")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SwimmerGuardiansDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SwimmerGuardiansDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SwimmerGuardiansDto>>> GetGuardians(Guid id, CancellationToken ct)
    {
        var dto = await _service.GetGuardiansAsync(id, ct);
        if (dto is null)
        {
            var nf = ApiResponse<SwimmerGuardiansDto>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<SwimmerGuardiansDto>.Success(SwimmerMessages.Success.GuardiansRetrieved(AppLanguage.Current), dto));
    }

    /// <summary>Upserts both guardians (father + mother). Head Coach or Captain only.</summary>
    /// <response code="200">Guardians saved.</response>
    /// <response code="404">No swimmer with that id.</response>
    [HttpPut("{id:guid}/guardians")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> UpsertGuardians(Guid id, UpsertGuardiansRequest request, CancellationToken ct)
    {
        var saved = await _service.UpsertGuardiansAsync(id, request, ct);
        if (!saved)
        {
            var nf = ApiResponse<object>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<object>.Success(SwimmerMessages.Success.GuardiansSaved(AppLanguage.Current), null));
    }

    #endregion
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter "FullyQualifiedName~SwimmersControllerTests"`
Expected: PASS (existing + 4 new).

- [ ] **Step 5: Full backend build + test**

Run: `dotnet build backend` then `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests backend/tests/Kheprx.BaseBackend.Api.UnitTests`
Expected: Build succeeded; all tests pass. (Stop any running backend first — DLL lock.)

- [ ] **Step 6: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs
git commit -m "feat(guardian): GET/PUT /api/swimmers/{id}/guardians endpoints"
```

---

### Task 7: Frontend domain model, DTOs, mapper

**Files:**
- Create: `frontend/src/app/features/swimmer-profile/domain/model/swimmer-guardians.ts`
- Create: `frontend/src/app/features/swimmer-profile/data/dto/guardians.dto.ts`
- Create: `frontend/src/app/features/swimmer-profile/data/dto/guardians.mapper.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/data/dto/guardians.mapper.spec.ts`

**Interfaces:**
- Produces:
  - `Guardian { id: string; relationCode: string; name: string; nationalId: string; phone: string }`
  - `SwimmerGuardians { father: Guardian | null; mother: Guardian | null }`
  - `GuardianDtoRs`, `SwimmerGuardiansDtoRs`, `GuardiansItemDtoRs extends BaseResponseRs<SwimmerGuardiansDtoRs>`
  - `GuardianInputDtoRq { name; nationalId; phone }`, `UpsertGuardiansDtoRq { father; mother }`, `UpsertGuardiansItemDtoRs extends BaseResponseRs<unknown>`
  - `isSwimmerGuardiansDtoRsValid(dto): dto is SwimmerGuardiansDtoRs`
  - `toSwimmerGuardians(d: SwimmerGuardiansDtoRs): SwimmerGuardians`

- [ ] **Step 1: Write the failing test**

`guardians.mapper.spec.ts`:
```ts
import { toSwimmerGuardians } from '@features/swimmer-profile/data/dto/guardians.mapper';
import { SwimmerGuardiansDtoRs } from '@features/swimmer-profile/data/dto/guardians.dto';

describe('toSwimmerGuardians', () => {
  it('maps both slots', () => {
    const dto: SwimmerGuardiansDtoRs = {
      father: { id: 'f1', relationCode: 'father', name: 'Hassan Ali', nationalId: '27001010123456', phone: '+201009876543' },
      mother: { id: 'm1', relationCode: 'mother', name: 'Fatima Ibrahim', nationalId: '27505050123456', phone: '+201005554444' },
    };
    const g = toSwimmerGuardians(dto);
    expect(g.father?.name).toBe('Hassan Ali');
    expect(g.mother?.nationalId).toBe('27505050123456');
  });

  it('keeps null slots null', () => {
    const dto: SwimmerGuardiansDtoRs = { father: null, mother: null };
    const g = toSwimmerGuardians(dto);
    expect(g.father).toBeNull();
    expect(g.mother).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest guardians.mapper`
Expected: FAIL — cannot find module `guardians.mapper`.

- [ ] **Step 3: Write the model**

`swimmer-guardians.ts`:
```ts
export interface Guardian {
  id: string;
  relationCode: string; // 'father' | 'mother'
  name: string;
  nationalId: string;
  phone: string;
}

export interface SwimmerGuardians {
  father: Guardian | null;
  mother: Guardian | null;
}
```

- [ ] **Step 4: Write the DTOs**

`guardians.dto.ts` (mirror `swimmer-profile.dto.ts`):
```ts
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface GuardianDtoRs { id: string; relationCode: string; name: string; nationalId: string; phone: string; }
export interface SwimmerGuardiansDtoRs { father: GuardianDtoRs | null; mother: GuardianDtoRs | null; }
export interface GuardiansItemDtoRs extends BaseResponseRs<SwimmerGuardiansDtoRs> {}

export interface GuardianInputDtoRq { name: string; nationalId: string; phone: string; }
export interface UpsertGuardiansDtoRq { father: GuardianInputDtoRq; mother: GuardianInputDtoRq; }
export interface UpsertGuardiansItemDtoRs extends BaseResponseRs<unknown> {}

export function isSwimmerGuardiansDtoRsValid(dto: unknown): dto is SwimmerGuardiansDtoRs {
  const d = dto as SwimmerGuardiansDtoRs;
  if (!d || typeof d !== 'object') return false;
  const slotOk = (s: GuardianDtoRs | null) =>
    s === null || (typeof s.id === 'string' && typeof s.name === 'string' && typeof s.relationCode === 'string');
  return slotOk(d.father) && slotOk(d.mother);
}
```

- [ ] **Step 5: Write the mapper**

`guardians.mapper.ts`:
```ts
import { GuardianDtoRs, SwimmerGuardiansDtoRs } from '@features/swimmer-profile/data/dto/guardians.dto';
import { Guardian, SwimmerGuardians } from '@features/swimmer-profile/domain/model/swimmer-guardians';

function toGuardian(d: GuardianDtoRs): Guardian {
  return { id: d.id, relationCode: d.relationCode, name: d.name, nationalId: d.nationalId, phone: d.phone };
}

export function toSwimmerGuardians(d: SwimmerGuardiansDtoRs): SwimmerGuardians {
  return { father: d.father ? toGuardian(d.father) : null, mother: d.mother ? toGuardian(d.mother) : null };
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `cd frontend && npx jest guardians.mapper`
Expected: PASS (2 tests).

- [ ] **Step 7: Commit**

```bash
git add frontend/src/app/features/swimmer-profile
git commit -m "feat(guardian-fe): domain model, DTOs, mapper"
```

---

### Task 8: Frontend repository methods

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/domain/repositories/swimmer-profile.repository.ts`
- Modify: `frontend/src/app/features/swimmer-profile/data/repositories/swimmer-profile.repository.impl.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/data/repositories/swimmer-profile.repository.impl.spec.ts` (add cases)

**Interfaces:**
- Consumes: `GuardiansItemDtoRs`, `UpsertGuardiansDtoRq`, `UpsertGuardiansItemDtoRs` (Task 7).
- Produces on `ISwimmerProfileRepository` (FE): `getGuardians(id: string): Promise<GuardiansItemDtoRs>`, `upsertGuardians(id: string, rq: UpsertGuardiansDtoRq): Promise<UpsertGuardiansItemDtoRs>`.

- [ ] **Step 1: Write the failing test**

Inspect the existing `swimmer-profile.repository.impl.spec.ts` to reuse its `HttpClientService` mock setup. Add:
```ts
it('getGuardians GETs the guardians endpoint', async () => {
  http.get.mockResolvedValue({ successStatus: true, data: { father: null, mother: null } });
  await repo.getGuardians('sw1');
  expect(http.get).toHaveBeenCalledWith('/api/swimmers/sw1/guardians');
});

it('upsertGuardians PUTs to the guardians endpoint', async () => {
  const rq = {
    father: { name: 'Hassan Ali', nationalId: '27001010123456', phone: '+201009876543' },
    mother: { name: 'Fatima Ibrahim', nationalId: '27505050123456', phone: '+201005554444' },
  };
  http.put.mockResolvedValue({ successStatus: true, data: null });
  await repo.upsertGuardians('sw1', rq);
  expect(http.put).toHaveBeenCalledWith('/api/swimmers/sw1/guardians', { body: rq });
});
```
> Match the spec file's existing variable names (`http`, `repo`) and mock style. If it builds the mock differently (e.g. a `jest.Mocked<HttpClientService>`), follow that.

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest swimmer-profile.repository.impl`
Expected: FAIL — `repo.getGuardians` is not a function.

- [ ] **Step 3: Declare interface methods**

In `swimmer-profile.repository.ts`, add the import and two methods:
```ts
import { GuardiansItemDtoRs, UpsertGuardiansDtoRq, UpsertGuardiansItemDtoRs } from '@features/swimmer-profile/data/dto/guardians.dto';
```
```ts
  getGuardians(id: string): Promise<GuardiansItemDtoRs>;
  upsertGuardians(id: string, rq: UpsertGuardiansDtoRq): Promise<UpsertGuardiansItemDtoRs>;
```

- [ ] **Step 4: Implement in the repository**

In `swimmer-profile.repository.impl.ts`, add the same import and:
```ts
  getGuardians(id: string): Promise<GuardiansItemDtoRs> {
    return this.http.get<GuardiansItemDtoRs>(`/api/swimmers/${id}/guardians`);
  }
  upsertGuardians(id: string, rq: UpsertGuardiansDtoRq): Promise<UpsertGuardiansItemDtoRs> {
    return this.http.put<UpsertGuardiansItemDtoRs>(`/api/swimmers/${id}/guardians`, { body: rq });
  }
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd frontend && npx jest swimmer-profile.repository.impl`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/app/features/swimmer-profile
git commit -m "feat(guardian-fe): repository getGuardians/upsertGuardians"
```

---

### Task 9: Frontend use cases (get + upsert)

**Files:**
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/get-swimmer-guardians.use-case.ts`
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/upsert-swimmer-guardians.use-case.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/domain/usecases/get-swimmer-guardians.use-case.spec.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/domain/usecases/upsert-swimmer-guardians.use-case.spec.ts`

**Interfaces:**
- Consumes: `SWIMMER_PROFILE_REPOSITORY`, `isSwimmerGuardiansDtoRsValid`, `toSwimmerGuardians`, `SwimmerGuardians`, `UpsertGuardiansDtoRq`.
- Produces:
  - `GetSwimmerGuardiansUseCase extends UseCase<string, SwimmerGuardians>`
  - `UpsertSwimmerGuardiansInput { id: string; rq: UpsertGuardiansDtoRq }`; `UpsertSwimmerGuardiansUseCase extends UseCase<UpsertSwimmerGuardiansInput, void>`

- [ ] **Step 1: Write the failing tests**

`get-swimmer-guardians.use-case.spec.ts` (mirror an existing use-case spec's TestBed/provider setup for `SWIMMER_PROFILE_REPOSITORY`):
```ts
import { TestBed } from '@angular/core/testing';
import { GetSwimmerGuardiansUseCase } from '@features/swimmer-profile/domain/usecases/get-swimmer-guardians.use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

describe('GetSwimmerGuardiansUseCase', () => {
  const repo = { getGuardians: jest.fn() } as any;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [GetSwimmerGuardiansUseCase, { provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }],
    });
  });

  it('maps a valid response to the domain model', async () => {
    repo.getGuardians.mockResolvedValue({ successStatus: true, data: {
      father: { id: 'f1', relationCode: 'father', name: 'Hassan Ali', nationalId: '27001010123456', phone: '+201009876543' },
      mother: null } });
    const res = await TestBed.inject(GetSwimmerGuardiansUseCase).run('sw1');
    expect(res.ok).toBe(true);
    if (res.ok) { expect(res.data.father?.name).toBe('Hassan Ali'); expect(res.data.mother).toBeNull(); }
  });

  it('fails on an invalid response', async () => {
    repo.getGuardians.mockResolvedValue({ successStatus: true, data: { father: { id: 5 } } });
    const res = await TestBed.inject(GetSwimmerGuardiansUseCase).run('sw1');
    expect(res.ok).toBe(false);
  });
});
```

`upsert-swimmer-guardians.use-case.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { UpsertSwimmerGuardiansUseCase } from '@features/swimmer-profile/domain/usecases/upsert-swimmer-guardians.use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

describe('UpsertSwimmerGuardiansUseCase', () => {
  const repo = { upsertGuardians: jest.fn() } as any;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [UpsertSwimmerGuardiansUseCase, { provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }],
    });
  });

  it('calls the repository and succeeds', async () => {
    repo.upsertGuardians.mockResolvedValue({ successStatus: true, data: null });
    const rq = {
      father: { name: 'Hassan Ali', nationalId: '27001010123456', phone: '+201009876543' },
      mother: { name: 'Fatima Ibrahim', nationalId: '27505050123456', phone: '+201005554444' },
    };
    const res = await TestBed.inject(UpsertSwimmerGuardiansUseCase).run({ id: 'sw1', rq });
    expect(res.ok).toBe(true);
    expect(repo.upsertGuardians).toHaveBeenCalledWith('sw1', rq);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd frontend && npx jest swimmer-guardians.use-case`
Expected: FAIL — cannot find the use-case modules.

- [ ] **Step 3: Write the get use case**

`get-swimmer-guardians.use-case.ts` (mirror `list-medical-exams.use-case.ts`):
```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { isSwimmerGuardiansDtoRsValid } from '@features/swimmer-profile/data/dto/guardians.dto';
import { toSwimmerGuardians } from '@features/swimmer-profile/data/dto/guardians.mapper';
import { SwimmerGuardians } from '@features/swimmer-profile/domain/model/swimmer-guardians';

@Injectable({ providedIn: 'root' })
export class GetSwimmerGuardiansUseCase extends UseCase<string, SwimmerGuardians> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('GetSwimmerGuardians'); }
  protected async execute(id: string): Promise<SwimmerGuardians> {
    const res = await this.repo.getGuardians(id);
    if (!isSwimmerGuardiansDtoRsValid(res.data)) throw new AppError('Invalid guardians received', 'validation');
    return toSwimmerGuardians(res.data);
  }
}
```

- [ ] **Step 4: Write the upsert use case**

`upsert-swimmer-guardians.use-case.ts` (mirror `update-medical-exam.use-case.ts`):
```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { UpsertGuardiansDtoRq } from '@features/swimmer-profile/data/dto/guardians.dto';

export interface UpsertSwimmerGuardiansInput { id: string; rq: UpsertGuardiansDtoRq; }

@Injectable({ providedIn: 'root' })
export class UpsertSwimmerGuardiansUseCase extends UseCase<UpsertSwimmerGuardiansInput, void> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('UpsertSwimmerGuardians'); }
  protected async execute(input: UpsertSwimmerGuardiansInput): Promise<void> {
    await this.repo.upsertGuardians(input.id, input.rq);
  }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd frontend && npx jest swimmer-guardians.use-case`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add frontend/src/app/features/swimmer-profile
git commit -m "feat(guardian-fe): get + upsert use cases"
```

---

### Task 10: ViewModel — tab state + guardian state/actions

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts` (add cases)

**Interfaces:**
- Consumes: `GetSwimmerGuardiansUseCase`, `UpsertSwimmerGuardiansUseCase`, `SwimmerGuardians`.
- Produces on `SwimmerProfileViewModel`:
  - `activeTab: WritableSignal<string>` (default `'identityVitals'`); `setTab(key: string): void` — for `'guardian'`, lazy-calls `loadGuardians()` once.
  - `guardians: Signal<SwimmerGuardians | null>`, `loadingGuardians`, `editingGuardians`, `savingGuardians` signals.
  - Edit signals: `gFatherName, gFatherNationalId, gFatherPhone, gMotherName, gMotherNationalId, gMotherPhone`.
  - `canSaveGuardians: Signal<boolean>` — all six non-empty, both national IDs match `^\d{14}$`.
  - `startEditGuardians()`, `cancelEditGuardians()`, `saveGuardians()`.

- [ ] **Step 1: Write the failing test**

Inspect `swimmer-profile.viewmodel.spec.ts` for its TestBed setup (it provides the use cases + `NotificationService`/`TranslateService`/`AuthSessionStore`). Add a describe block; you must extend the providers to include `GetSwimmerGuardiansUseCase` and `UpsertSwimmerGuardiansUseCase` mocks:
```ts
describe('guardian tab', () => {
  it('setTab("guardian") lazy-loads guardians once', async () => {
    getGuardians.run.mockResolvedValue({ ok: true, data: { father: null, mother: null } });
    vm.setTab('guardian');
    await Promise.resolve(); await Promise.resolve();
    expect(vm.activeTab()).toBe('guardian');
    expect(getGuardians.run).toHaveBeenCalledTimes(1);
    vm.setTab('identityVitals');
    vm.setTab('guardian');
    await Promise.resolve();
    expect(getGuardians.run).toHaveBeenCalledTimes(1); // not reloaded
  });

  it('canSaveGuardians requires all six fields and 14-digit national IDs', () => {
    vm.startEditGuardians();
    expect(vm.canSaveGuardians()).toBe(false);
    vm.gFatherName.set('Hassan'); vm.gFatherNationalId.set('27001010123456'); vm.gFatherPhone.set('+201009876543');
    vm.gMotherName.set('Fatima'); vm.gMotherNationalId.set('123'); vm.gMotherPhone.set('+201005554444');
    expect(vm.canSaveGuardians()).toBe(false); // mother national id invalid
    vm.gMotherNationalId.set('27505050123456');
    expect(vm.canSaveGuardians()).toBe(true);
  });
});
```
> Adapt mock variable names (`getGuardians`, `upsertGuardians`) to the spec file's existing convention for mocking use cases. The two `await Promise.resolve()` flush the microtask queue after the async load.

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest swimmer-profile.viewmodel`
Expected: FAIL — `vm.setTab`/`vm.activeTab` do not exist.

- [ ] **Step 3: Implement in the viewmodel**

Add the imports at the top of `swimmer-profile.viewmodel.ts`:
```ts
import { GetSwimmerGuardiansUseCase } from '@features/swimmer-profile/domain/usecases/get-swimmer-guardians.use-case';
import { UpsertSwimmerGuardiansUseCase } from '@features/swimmer-profile/domain/usecases/upsert-swimmer-guardians.use-case';
import { SwimmerGuardians } from '@features/swimmer-profile/domain/model/swimmer-guardians';
```
Inject the use cases (beside the others):
```ts
  private readonly getGuardiansUc = inject(GetSwimmerGuardiansUseCase);
  private readonly upsertGuardiansUc = inject(UpsertSwimmerGuardiansUseCase);
```
Add tab + guardian state (after the exam state block; `signal`/`computed` are already imported):
```ts
  // Tab state — only the two built tabs are switchable.
  readonly activeTab = signal<'identityVitals' | 'guardian'>('identityVitals');
  private guardiansLoaded = false;

  // Guardian state
  readonly guardians = signal<SwimmerGuardians | null>(null);
  readonly loadingGuardians = signal(false);
  readonly editingGuardians = signal(false);
  readonly savingGuardians = signal(false);
  readonly gFatherName = signal(''); readonly gFatherNationalId = signal(''); readonly gFatherPhone = signal('');
  readonly gMotherName = signal(''); readonly gMotherNationalId = signal(''); readonly gMotherPhone = signal('');

  private static readonly NATIONAL_ID = /^\d{14}$/;
  readonly canSaveGuardians = computed(() => {
    const slotOk = (name: string, nid: string, phone: string) =>
      name.trim().length > 0 && SwimmerProfileViewModel.NATIONAL_ID.test(nid.trim()) && phone.trim().length > 0;
    return slotOk(this.gFatherName(), this.gFatherNationalId(), this.gFatherPhone())
        && slotOk(this.gMotherName(), this.gMotherNationalId(), this.gMotherPhone());
  });

  setTab(key: 'identityVitals' | 'guardian'): void {
    this.activeTab.set(key);
    if (key === 'guardian' && !this.guardiansLoaded) void this.loadGuardians();
  }

  private async loadGuardians(): Promise<void> {
    this.guardiansLoaded = true;
    this.loadingGuardians.set(true);
    const r = await this.getGuardiansUc.run(this.swimmerId);
    this.loadingGuardians.set(false);
    if (r.ok) this.guardians.set(r.data);
    else { this.guardiansLoaded = false; this.guardians.set(null); }
  }

  startEditGuardians(): void {
    const g = this.guardians();
    this.gFatherName.set(g?.father?.name ?? ''); this.gFatherNationalId.set(g?.father?.nationalId ?? ''); this.gFatherPhone.set(g?.father?.phone ?? '');
    this.gMotherName.set(g?.mother?.name ?? ''); this.gMotherNationalId.set(g?.mother?.nationalId ?? ''); this.gMotherPhone.set(g?.mother?.phone ?? '');
    this.editingGuardians.set(true);
  }

  cancelEditGuardians(): void { this.editingGuardians.set(false); }

  async saveGuardians(): Promise<void> {
    if (!this.canSaveGuardians() || this.savingGuardians()) return;
    this.savingGuardians.set(true);
    const rq = {
      father: { name: this.gFatherName().trim(), nationalId: this.gFatherNationalId().trim(), phone: this.gFatherPhone().trim() },
      mother: { name: this.gMotherName().trim(), nationalId: this.gMotherNationalId().trim(), phone: this.gMotherPhone().trim() },
    };
    const r = await this.upsertGuardiansUc.run({ id: this.swimmerId, rq });
    this.savingGuardians.set(false);
    if (r.ok) {
      this.notify.success(this.i18n.t('swimmerProfile.toasts.guardiansSaved'));
      this.editingGuardians.set(false);
      this.guardiansLoaded = false;
      await this.loadGuardians();
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.saveFailed'));
    }
  }
```
> `this.swimmerId` is the existing private field set in `load(id)`. `notify`/`i18n` are the existing injected `NotificationService`/`TranslateService`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd frontend && npx jest swimmer-profile.viewmodel`
Expected: PASS (existing + 2 new).

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/features/swimmer-profile
git commit -m "feat(guardian-fe): viewmodel tab state + guardian load/edit/save"
```

---

### Task 11: Page — interactive tabs + Guardian section + i18n

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.ts`
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.html`
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`

**Interfaces:**
- Consumes: `vm.activeTab`, `vm.setTab`, and all guardian signals/actions (Task 10).

- [ ] **Step 1: Add a helper for enabled tabs in `swimmer-profile.page.ts`**

Add a set of the two built keys and a helper:
```ts
  protected readonly enabledTabs = new Set(['identityVitals', 'guardian']);
  isEnabled(key: string): boolean { return this.enabledTabs.has(key); }
```

- [ ] **Step 2: Make the tab strip interactive in `swimmer-profile.page.html`**

Replace the tab-strip block (the `@for (t of tabs...)` `<span>` at ~L28–39) with buttons that switch enabled tabs and keep the rest disabled:
```html
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
                (click)="isEnabled(t.key) && vm.setTab(t.key)">
          {{ t.labelKey | translate }}
        </button>
      }
    </div>
```

- [ ] **Step 3: Gate the existing Identity + Vitals sections behind the active tab**

Wrap the two existing `<section>` blocks (Identity section ~L42 and Vitals section ~L83) in a guard so they only show on the first tab:
```html
    @if (vm.activeTab() === 'identityVitals') {
      <!-- existing Identity <section> ... -->
      <!-- existing Vitals <section> ... -->
    }
```

- [ ] **Step 4: Add the Guardian section**

After the Identity/Vitals `@if` block (still inside `@else if (vm.profile(); as p)`), add:
```html
    @if (vm.activeTab() === 'guardian') {
      <section class="rounded-2xl border border-border bg-surface p-6 shadow-sm">
        <div class="mb-4 flex items-center justify-between border-b border-border pb-2">
          <h2 class="font-heading text-lg text-ink">{{ 'swimmerProfile.sections.guardian' | translate }}</h2>
          @if (vm.canEdit() && !vm.editingGuardians()) {
            <button type="button" class="text-sm font-medium text-primary" (click)="vm.startEditGuardians()">{{ 'swimmerProfile.edit' | translate }}</button>
          }
        </div>

        @if (vm.loadingGuardians()) {
          <p class="text-sm text-text-secondary">{{ 'swimmerProfile.states.loading' | translate }}</p>
        } @else if (vm.editingGuardians()) {
          <form class="grid grid-cols-1 gap-8 md:grid-cols-2" (submit)="$event.preventDefault(); vm.saveGuardians()">
            <div class="space-y-4 rounded-xl border border-border p-4">
              <h4 class="border-b border-border pb-2 text-sm font-semibold text-ink">{{ 'swimmerProfile.guardian.fatherInfo' | translate }}</h4>
              <app-text-field [label]="'swimmerProfile.guardian.fullName' | translate" [value]="vm.gFatherName()" (valueChange)="vm.gFatherName.set($event)"></app-text-field>
              <app-text-field [label]="'swimmerProfile.guardian.nationalId' | translate" [value]="vm.gFatherNationalId()" (valueChange)="vm.gFatherNationalId.set($event)"></app-text-field>
              <app-text-field [label]="'swimmerProfile.guardian.phone' | translate" [value]="vm.gFatherPhone()" (valueChange)="vm.gFatherPhone.set($event)"></app-text-field>
            </div>
            <div class="space-y-4 rounded-xl border border-border p-4">
              <h4 class="border-b border-border pb-2 text-sm font-semibold text-ink">{{ 'swimmerProfile.guardian.motherInfo' | translate }}</h4>
              <app-text-field [label]="'swimmerProfile.guardian.fullName' | translate" [value]="vm.gMotherName()" (valueChange)="vm.gMotherName.set($event)"></app-text-field>
              <app-text-field [label]="'swimmerProfile.guardian.nationalId' | translate" [value]="vm.gMotherNationalId()" (valueChange)="vm.gMotherNationalId.set($event)"></app-text-field>
              <app-text-field [label]="'swimmerProfile.guardian.phone' | translate" [value]="vm.gMotherPhone()" (valueChange)="vm.gMotherPhone.set($event)"></app-text-field>
            </div>
            <div class="flex items-end gap-2 md:col-span-2">
              <button type="submit" class="rounded-md bg-primary px-5 py-2.5 text-sm font-semibold text-white disabled:opacity-50" [disabled]="!vm.canSaveGuardians() || vm.savingGuardians()">{{ 'common.save' | translate }}</button>
              <button type="button" class="rounded-md px-5 py-2.5 text-sm text-text-secondary" (click)="vm.cancelEditGuardians()">{{ 'common.cancel' | translate }}</button>
            </div>
          </form>
        } @else {
          <div class="grid grid-cols-1 gap-8 md:grid-cols-2">
            @for (slot of [{ key: 'father', g: vm.guardians()?.father, title: 'swimmerProfile.guardian.fatherInfo' },
                           { key: 'mother', g: vm.guardians()?.mother, title: 'swimmerProfile.guardian.motherInfo' }]; track slot.key) {
              <div class="space-y-4 rounded-xl border border-border p-4">
                <h4 class="border-b border-border pb-2 text-sm font-semibold text-ink">{{ slot.title | translate }}</h4>
                @if (slot.g; as g) {
                  <dl class="space-y-3">
                    <div><dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.guardian.fullName' | translate }}</dt><dd class="text-ink">{{ g.name }}</dd></div>
                    <div><dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.guardian.nationalId' | translate }}</dt><dd class="text-ink">{{ g.nationalId }}</dd></div>
                    <div><dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.guardian.phone' | translate }}</dt><dd class="text-ink">{{ g.phone }}</dd></div>
                  </dl>
                } @else {
                  <p class="text-sm text-text-secondary">{{ 'swimmerProfile.guardian.noRecord' | translate }}</p>
                }
              </div>
            }
          </div>
        }
      </section>
    }
```
> Confirm `common.save`/`common.cancel` keys exist (the Identity form uses them). If the codebase's control-flow `@for` cannot bind an inline object array cleanly, fall back to two explicit slot blocks (father then mother) — the rendered result is identical.

- [ ] **Step 5: Add i18n keys (EN)**

In `frontend/src/app/core/i18n/en.json`, under `swimmerProfile`: add `"guardian"` in `sections` and a new `guardian` block + a toast. Set `sections` to:
```json
    "sections": { "identity": "Identity", "vitals": "Vitals", "guardian": "Guardian Details" },
    "guardian": {
      "fatherInfo": "Father Info",
      "motherInfo": "Mother Info",
      "fullName": "Full Name",
      "nationalId": "National ID",
      "phone": "Phone",
      "noRecord": "No guardian on record yet."
    },
```
And add to the `toasts` object:
```json
      "guardiansSaved": "Guardian details saved",
```

- [ ] **Step 6: Add i18n keys (AR)**

In `frontend/src/app/core/i18n/ar.json`, mirror the same keys under `swimmerProfile` (match that file's existing Arabic structure):
```json
    "guardian": {
      "fatherInfo": "بيانات الأب",
      "motherInfo": "بيانات الأم",
      "fullName": "الاسم الكامل",
      "nationalId": "الرقم القومي",
      "phone": "الهاتف",
      "noRecord": "لا يوجد ولي أمر مسجل بعد."
    },
```
Add `"guardian": "بيانات ولي الأمر"` to `swimmerProfile.sections` and `"guardiansSaved": "تم حفظ بيانات ولي الأمر"` to `swimmerProfile.toasts` in `ar.json`.

- [ ] **Step 7: Build the frontend**

Run: `cd frontend && npm run build`
Expected: Build succeeds with no template/type errors.

- [ ] **Step 8: Run the full frontend test suite**

Run: `cd frontend && npm test`
Expected: All Jest suites pass.

- [ ] **Step 9: Commit**

```bash
git add frontend/src/app/features/swimmer-profile frontend/src/app/core/i18n/en.json frontend/src/app/core/i18n/ar.json
git commit -m "feat(guardian-fe): interactive tabs + Guardian section + i18n"
```

---

### Task 12: Full verification + manual check

**Files:** none (verification only).

- [ ] **Step 1: Full backend build + tests**

Stop any running backend (DLL lock), then:
Run: `dotnet build backend && dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests backend/tests/Kheprx.BaseBackend.Api.UnitTests`
Expected: Build succeeded; all tests pass (including the pre-existing suites — no regressions).

- [ ] **Step 2: Full frontend build + tests**

Run: `cd frontend && npm run build && npm test`
Expected: Build succeeds; all Jest suites pass.

- [ ] **Step 3: Apply the migration to a dev database and smoke-test the flow**

Run (dev DB configured): `dotnet ef database update --project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure --startup-project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure`
Then start the backend + frontend, sign in as head coach, open a swimmer profile, switch to the **Guardian** tab, click **Edit**, fill father + mother, **Save**, and confirm the values persist on reload. Confirm the empty state ("No guardian on record yet.") shows for a swimmer with no guardians, and that the other seven tabs remain disabled.

- [ ] **Step 4: Code review**

Use superpowers:requesting-code-review for the whole branch before merging. Expected: no Critical/Important findings.

---

## Self-Review

**Spec coverage:**
- `athlete.guardian` + `reference.guardian_relation` (seeded father/mother), Identity module, unique `(swimmer_id, relation_id)` → Task 1. ✓
- Un-applied EF migration → Task 2 + applied in Task 12. ✓
- `GET`/`PUT /api/swimmers/{id}/guardians`, read nullable slots, atomic upsert of both, both required, national-ID 14 digits, coach-only write, 404 unknown swimmer → Tasks 3–6. ✓
- Guardian persistence folded into `ISwimmerProfileRepository` (spec refinement) → Task 3. ✓
- Validation via FluentValidation + global filter → Task 4. ✓
- FE model/DTO/mapper, repository, use cases, viewmodel (lazy load, canSave), interactive tabs, Guardian section, i18n → Tasks 7–11. ✓
- Testing: backend service/API/validator/repo tests, FE mapper/repo/usecase/viewmodel specs, both builds, review → all tasks + Task 12. ✓

**Placeholder scan:** No TBD/TODO; every code step has concrete code. The `> …` notes point the executor at the exact existing helper to mirror (test-harness construction differs per file and must be read, not guessed) — these are instructions, not placeholders. ✓

**Type consistency:** `Guardian`, `GuardianRelation`, `GuardianRow(Id, RelationCode, Name, NationalId, Phone)`, `GuardianDto`, `GuardianInputDto`, `SwimmerGuardiansDto(Father, Mother)`, `UpsertGuardiansRequest(Father, Mother)`, repo method names (`ListGuardiansAsync`, `GetGuardianRelationIdByCodeAsync`, `GetGuardianTrackedAsync`, `AddGuardianAsync`), service methods (`GetGuardiansAsync`, `UpsertGuardiansAsync`), FE `SwimmerGuardians`/`Guardian`, `getGuardians`/`upsertGuardians`, use-case names, and viewmodel `setTab`/`activeTab`/`canSaveGuardians`/`gFather*`/`gMother*` are used consistently across producer and consumer tasks. Relation codes are the literal `"father"`/`"mother"` throughout. ✓

**Note on the unique-constraint test:** repo tests run on EF InMemory, which does not enforce the unique `(swimmer_id, relation_id)` index; that guarantee is verified by the migration (Task 2, Step 3) and the upsert's read-then-insert/update logic (Task 5 test), not by a DB-constraint unit test — intentionally, to avoid a test that can't pass on InMemory.
