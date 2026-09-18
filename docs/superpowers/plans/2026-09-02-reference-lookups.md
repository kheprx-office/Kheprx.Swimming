# Reference Lookups Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `reference.stroke`, `reference.blood_type`, and `reference.club` lookups (entities, seed data, read endpoints) plus an Angular `reference` slice that loads them (and genders) by ID, so the upcoming Account Creation forms can populate every dropdown/chip.

**Architecture:** Reference lookups live in the existing **Identity** module mapped to the `reference` schema, exactly like the current `Gender`/`Role` lookups. Each new lookup gets an entity → EF config → repository → aggregated by one `ReferenceService` → served read-only by one `ReferenceController` (`GET /api/reference/{clubs|blood-types|strokes|genders}`). The frontend adds a fetch-only `features/reference` slice (DTO + validator → repository port/impl → use-cases) mirroring the existing `features/swimmers` slice.

**Tech Stack:** .NET (C#, EF Core, xUnit + Moq), PostgreSQL, Angular (standalone + signals, Jest), clean-architecture layering.

**Spec:** `docs/superpowers/specs/2026-09-02-reference-lookups-design.md`

## Global Constraints

- **Placement:** new reference entities go in `Identity.Domain/Entities`, configs in `Identity.Infrastructure/Configurations`, mapped to the `reference` schema (`ToTable("<table>", "reference")`) — same as `Gender`/`Role`. No new module or DbContext.
- **Read-only phase:** lookups are seeded and served only. No create/update/delete of reference data. No form UI (Phases 2–3). No `swimmer_profile`/`captain_profile` schema changes. No `distance` table. No "Assigned Clubs".
- **Endpoints authed:** the new reference endpoints require an authenticated user — do **not** add `[AllowAnonymous]` (the API's default authorization applies). `[Authorize]` is stated explicitly on the controller for clarity.
- **IDs, not strings:** DTOs/endpoints expose `id` + labels; consumers use `id` as the value. **`IM` = stroke code `medley`.**
- **Labels API-sourced:** dropdown/chip text comes from each row's `nameEn`/`nameAr`; do not duplicate lookup names in Angular i18n dictionaries.
- **Build lock:** stop the running dev API server (`Kheprx.BaseBackend.Api`) before any `dotnet build` / `dotnet ef` / `dotnet test` that rebuilds the API project — it holds a lock on its `bin` output.
- **Commits:** the user asked not to commit unless they explicitly request it. Each task below ends with a commit step; **hold those commits** and instead pause for the user unless they have said to commit.
- **Frontend commands run from `frontend/`; backend commands run from the repo root.**

---

### Task 1: Reference entities (Stroke, BloodType, Club)

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/Stroke.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/BloodType.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/Club.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/ReferenceEntityTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `Stroke(string code, string nameEn, string? nameAr = null)`, `BloodType(string code, string nameEn, string? nameAr = null)`, `Club(string nameEn, string? nameAr = null)`. All have `public Guid Id`, string props with private setters; `Stroke`/`BloodType` have `Code`/`NameEn`/`NameAr`; `Club` has `NameEn`/`NameAr`/`DateTime CreatedAt`.

- [ ] **Step 1: Write the failing test**

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class ReferenceEntityTests
{
    [Fact]
    public void Stroke_ctor_trims_and_assigns_id()
    {
        var s = new Stroke("  medley ", " IM ", "  متنوع فردي ");
        Assert.NotEqual(Guid.Empty, s.Id);
        Assert.Equal("medley", s.Code);
        Assert.Equal("IM", s.NameEn);
        Assert.Equal("متنوع فردي", s.NameAr);
    }

    [Fact]
    public void BloodType_blank_name_ar_becomes_null()
    {
        var b = new BloodType("O+", "O+", "   ");
        Assert.Equal("O+", b.Code);
        Assert.Null(b.NameAr);
    }

    [Fact]
    public void Club_ctor_sets_id_created_at_and_has_no_code()
    {
        var c = new Club("Al Ahly", "الأهلي");
        Assert.NotEqual(Guid.Empty, c.Id);
        Assert.Equal("Al Ahly", c.NameEn);
        Assert.Equal("الأهلي", c.NameAr);
        Assert.NotEqual(default, c.CreatedAt);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~ReferenceEntityTests" -v minimal --nologo`
Expected: FAIL — `Stroke`/`BloodType`/`Club` do not exist (compile error).

- [ ] **Step 3: Write the entities**

`Stroke.cs`:
```csharp
namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class Stroke
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }

    private Stroke() { } // EF Core

    public Stroke(string code, string nameEn, string? nameAr = null)
    {
        Id = Guid.NewGuid();
        Code = code.Trim();
        NameEn = nameEn.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
    }
}
```

`BloodType.cs`:
```csharp
namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class BloodType
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }

    private BloodType() { } // EF Core

    public BloodType(string code, string nameEn, string? nameAr = null)
    {
        Id = Guid.NewGuid();
        Code = code.Trim();
        NameEn = nameEn.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
    }
}
```

`Club.cs`:
```csharp
namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class Club
{
    public Guid Id { get; private set; }
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Club() { } // EF Core

    public Club(string nameEn, string? nameAr = null)
    {
        Id = Guid.NewGuid();
        NameEn = nameEn.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
        CreatedAt = DateTime.UtcNow;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~ReferenceEntityTests" -v minimal --nologo`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit** (hold per Global Constraints)

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/Stroke.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/BloodType.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/Club.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/ReferenceEntityTests.cs
git commit -m "feat(identity): add Stroke/BloodType/Club reference entities"
```

---

### Task 2: EF configs, DbSets, and repositories

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/StrokeConfiguration.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/BloodTypeConfiguration.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/ClubConfiguration.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentityDbContext.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/IStrokeRepository.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/IBloodTypeRepository.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/IClubRepository.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/IGenderRepository.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/StrokeRepository.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/BloodTypeRepository.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/ClubRepository.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/GenderRepository.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/ReferenceRepositoryTests.cs`

**Interfaces:**
- Consumes: `Stroke`, `BloodType`, `Club` (Task 1); existing `Gender` entity and `IdentityDbContext`.
- Produces: `IStrokeRepository.GetAllAsync(ct) → Task<IReadOnlyList<Stroke>>` (ordered by `NameEn`); `IBloodTypeRepository.GetAllAsync(ct) → Task<IReadOnlyList<BloodType>>` (ordered by `Code`); `IClubRepository.GetAllAsync(ct) → Task<IReadOnlyList<Club>>` (ordered by `NameEn`); `IGenderRepository.GetAllAsync(ct) → Task<IReadOnlyList<Gender>>` (ordered by `NameEn`). New DbSets `Strokes`, `BloodTypes`, `Clubs` on `IdentityDbContext`.

- [ ] **Step 1: Write the failing test**

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Kheprx.BaseBackend.Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Repositories;

public class ReferenceRepositoryTests
{
    private static IdentityDbContext NewDb() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task StrokeRepository_returns_all_ordered_by_name_en()
    {
        await using var db = NewDb();
        db.Strokes.AddRange(new Stroke("butterfly", "Butterfly"), new Stroke("backstroke", "Backstroke"));
        await db.SaveChangesAsync();

        var result = await new StrokeRepository(db).GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Backstroke", result[0].NameEn);
    }

    [Fact]
    public async Task BloodTypeRepository_returns_all_ordered_by_code()
    {
        await using var db = NewDb();
        db.BloodTypes.AddRange(new BloodType("O+", "O+"), new BloodType("A+", "A+"));
        await db.SaveChangesAsync();

        var result = await new BloodTypeRepository(db).GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("A+", result[0].Code);
    }

    [Fact]
    public async Task ClubRepository_returns_all_ordered_by_name_en()
    {
        await using var db = NewDb();
        db.Clubs.AddRange(new Club("Zamalek"), new Club("Al Ahly"));
        await db.SaveChangesAsync();

        var result = await new ClubRepository(db).GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Al Ahly", result[0].NameEn);
    }

    [Fact]
    public async Task GenderRepository_returns_all_ordered_by_name_en()
    {
        await using var db = NewDb();
        db.Genders.AddRange(new Gender("male", "Male"), new Gender("female", "Female"));
        await db.SaveChangesAsync();

        var result = await new GenderRepository(db).GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Female", result[0].NameEn);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~ReferenceRepositoryTests" -v minimal --nologo`
Expected: FAIL — repositories and `db.Strokes`/`db.BloodTypes`/`db.Clubs` do not exist (compile error).

- [ ] **Step 3: Write configs, DbSets, and repositories**

`StrokeConfiguration.cs`:
```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class StrokeConfiguration : IEntityTypeConfiguration<Stroke>
{
    public void Configure(EntityTypeBuilder<Stroke> builder)
    {
        builder.ToTable("stroke", "reference");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(s => s.Code).IsUnique();
        builder.Property(s => s.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(s => s.NameAr).HasMaxLength(100);
    }
}
```

`BloodTypeConfiguration.cs`:
```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class BloodTypeConfiguration : IEntityTypeConfiguration<BloodType>
{
    public void Configure(EntityTypeBuilder<BloodType> builder)
    {
        builder.ToTable("blood_type", "reference");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Code).HasMaxLength(10).IsRequired();
        builder.HasIndex(b => b.Code).IsUnique();
        builder.Property(b => b.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(b => b.NameAr).HasMaxLength(100);
    }
}
```

`ClubConfiguration.cs`:
```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class ClubConfiguration : IEntityTypeConfiguration<Club>
{
    public void Configure(EntityTypeBuilder<Club> builder)
    {
        builder.ToTable("club", "reference");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(c => c.NameAr).HasMaxLength(200);
        builder.Property(c => c.CreatedAt).IsRequired();
    }
}
```

In `IdentityDbContext.cs`, add three DbSets alongside the existing ones (after `SwimmerProfiles`):
```csharp
    public DbSet<Stroke> Strokes => Set<Stroke>();
    public DbSet<BloodType> BloodTypes => Set<BloodType>();
    public DbSet<Club> Clubs => Set<Club>();
```

Repository interfaces (`Domain/Repositories/`):
```csharp
// IStrokeRepository.cs
using Kheprx.BaseBackend.Identity.Domain.Entities;
namespace Kheprx.BaseBackend.Identity.Domain.Repositories;
public interface IStrokeRepository
{
    Task<IReadOnlyList<Stroke>> GetAllAsync(CancellationToken ct = default);
}
```
```csharp
// IBloodTypeRepository.cs
using Kheprx.BaseBackend.Identity.Domain.Entities;
namespace Kheprx.BaseBackend.Identity.Domain.Repositories;
public interface IBloodTypeRepository
{
    Task<IReadOnlyList<BloodType>> GetAllAsync(CancellationToken ct = default);
}
```
```csharp
// IClubRepository.cs
using Kheprx.BaseBackend.Identity.Domain.Entities;
namespace Kheprx.BaseBackend.Identity.Domain.Repositories;
public interface IClubRepository
{
    Task<IReadOnlyList<Club>> GetAllAsync(CancellationToken ct = default);
}
```
```csharp
// IGenderRepository.cs
using Kheprx.BaseBackend.Identity.Domain.Entities;
namespace Kheprx.BaseBackend.Identity.Domain.Repositories;
public interface IGenderRepository
{
    Task<IReadOnlyList<Gender>> GetAllAsync(CancellationToken ct = default);
}
```

Repository implementations (`Infrastructure/Repositories/`), mirroring `RoleRepository`:
```csharp
// StrokeRepository.cs
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class StrokeRepository : IStrokeRepository
{
    private readonly IdentityDbContext _db;
    public StrokeRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<Stroke>> GetAllAsync(CancellationToken ct = default)
        => await _db.Strokes.AsNoTracking().OrderBy(s => s.NameEn).ToListAsync(ct);
}
```
```csharp
// BloodTypeRepository.cs
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class BloodTypeRepository : IBloodTypeRepository
{
    private readonly IdentityDbContext _db;
    public BloodTypeRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<BloodType>> GetAllAsync(CancellationToken ct = default)
        => await _db.BloodTypes.AsNoTracking().OrderBy(b => b.Code).ToListAsync(ct);
}
```
```csharp
// ClubRepository.cs
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class ClubRepository : IClubRepository
{
    private readonly IdentityDbContext _db;
    public ClubRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<Club>> GetAllAsync(CancellationToken ct = default)
        => await _db.Clubs.AsNoTracking().OrderBy(c => c.NameEn).ToListAsync(ct);
}
```
```csharp
// GenderRepository.cs
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class GenderRepository : IGenderRepository
{
    private readonly IdentityDbContext _db;
    public GenderRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<Gender>> GetAllAsync(CancellationToken ct = default)
        => await _db.Genders.AsNoTracking().OrderBy(g => g.NameEn).ToListAsync(ct);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~ReferenceRepositoryTests" -v minimal --nologo`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit** (hold per Global Constraints)

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/StrokeConfiguration.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/BloodTypeConfiguration.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/ClubConfiguration.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentityDbContext.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/IStrokeRepository.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/IBloodTypeRepository.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/IClubRepository.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/IGenderRepository.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/StrokeRepository.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/BloodTypeRepository.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/ClubRepository.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/GenderRepository.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/ReferenceRepositoryTests.cs
git commit -m "feat(identity): map reference lookups + add read repositories"
```

---

### Task 3: DTOs and ReferenceService

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/ReferenceDtos.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/IReferenceService.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/ReferenceService.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/ReferenceServiceTests.cs`

**Interfaces:**
- Consumes: `IStrokeRepository`, `IBloodTypeRepository`, `IClubRepository`, `IGenderRepository` (Task 2).
- Produces: `CodedLookupDto(Guid Id, string Code, string NameEn, string? NameAr)`, `ClubDto(Guid Id, string NameEn, string? NameAr)`; `IReferenceService` with `GetClubsAsync(ct) → Task<IReadOnlyList<ClubDto>>`, `GetBloodTypesAsync(ct) / GetStrokesAsync(ct) / GetGendersAsync(ct) → Task<IReadOnlyList<CodedLookupDto>>`.

- [ ] **Step 1: Write the failing test**

```csharp
using Kheprx.BaseBackend.Identity.Application.Services;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Services;

public class ReferenceServiceTests
{
    private static ReferenceService NewService(
        IClubRepository? clubs = null, IBloodTypeRepository? blood = null,
        IStrokeRepository? strokes = null, IGenderRepository? genders = null)
        => new(clubs ?? Mock.Of<IClubRepository>(), blood ?? Mock.Of<IBloodTypeRepository>(),
               strokes ?? Mock.Of<IStrokeRepository>(), genders ?? Mock.Of<IGenderRepository>());

    [Fact]
    public async Task GetStrokes_maps_entities_to_coded_dtos()
    {
        var strokes = new Mock<IStrokeRepository>();
        strokes.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new[] { new Stroke("medley", "IM", "متنوع فردي") });

        var result = await NewService(strokes: strokes.Object).GetStrokesAsync();

        Assert.Single(result);
        Assert.Equal("medley", result[0].Code);
        Assert.Equal("IM", result[0].NameEn);
        Assert.Equal("متنوع فردي", result[0].NameAr);
    }

    [Fact]
    public async Task GetClubs_maps_entities_to_club_dtos()
    {
        var clubs = new Mock<IClubRepository>();
        clubs.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new[] { new Club("Al Ahly", "الأهلي") });

        var result = await NewService(clubs: clubs.Object).GetClubsAsync();

        Assert.Single(result);
        Assert.Equal("Al Ahly", result[0].NameEn);
        Assert.Equal("الأهلي", result[0].NameAr);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~ReferenceServiceTests" -v minimal --nologo`
Expected: FAIL — `ReferenceService`, `CodedLookupDto`, `ClubDto` do not exist (compile error).

- [ ] **Step 3: Write DTOs, interface, and service**

`ReferenceDtos.cs`:
```csharp
namespace Kheprx.BaseBackend.Identity.Application.DTOs;

/// <summary>A reference lookup row that carries a stable code (stroke, blood type, gender).</summary>
public sealed record CodedLookupDto(Guid Id, string Code, string NameEn, string? NameAr);

/// <summary>A club lookup row (no code — clubs are identified by id).</summary>
public sealed record ClubDto(Guid Id, string NameEn, string? NameAr);
```

`IReferenceService.cs`:
```csharp
using Kheprx.BaseBackend.Identity.Application.DTOs;

namespace Kheprx.BaseBackend.Identity.Application.Services.Interfaces;

public interface IReferenceService
{
    Task<IReadOnlyList<ClubDto>> GetClubsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CodedLookupDto>> GetBloodTypesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CodedLookupDto>> GetStrokesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CodedLookupDto>> GetGendersAsync(CancellationToken ct = default);
}
```

`ReferenceService.cs`:
```csharp
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Domain.Repositories;

namespace Kheprx.BaseBackend.Identity.Application.Services;

internal sealed class ReferenceService : IReferenceService
{
    private readonly IClubRepository _clubs;
    private readonly IBloodTypeRepository _bloodTypes;
    private readonly IStrokeRepository _strokes;
    private readonly IGenderRepository _genders;

    public ReferenceService(
        IClubRepository clubs, IBloodTypeRepository bloodTypes,
        IStrokeRepository strokes, IGenderRepository genders)
    {
        _clubs = clubs;
        _bloodTypes = bloodTypes;
        _strokes = strokes;
        _genders = genders;
    }

    public async Task<IReadOnlyList<ClubDto>> GetClubsAsync(CancellationToken ct = default)
        => (await _clubs.GetAllAsync(ct)).Select(c => new ClubDto(c.Id, c.NameEn, c.NameAr)).ToList();

    public async Task<IReadOnlyList<CodedLookupDto>> GetBloodTypesAsync(CancellationToken ct = default)
        => (await _bloodTypes.GetAllAsync(ct)).Select(b => new CodedLookupDto(b.Id, b.Code, b.NameEn, b.NameAr)).ToList();

    public async Task<IReadOnlyList<CodedLookupDto>> GetStrokesAsync(CancellationToken ct = default)
        => (await _strokes.GetAllAsync(ct)).Select(s => new CodedLookupDto(s.Id, s.Code, s.NameEn, s.NameAr)).ToList();

    public async Task<IReadOnlyList<CodedLookupDto>> GetGendersAsync(CancellationToken ct = default)
        => (await _genders.GetAllAsync(ct)).Select(g => new CodedLookupDto(g.Id, g.Code, g.NameEn, g.NameAr)).ToList();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~ReferenceServiceTests" -v minimal --nologo`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit** (hold per Global Constraints)

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/ReferenceDtos.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/IReferenceService.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/ReferenceService.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/ReferenceServiceTests.cs
git commit -m "feat(identity): add ReferenceService + lookup DTOs"
```

---

### Task 4: ReferenceMessages, ReferenceController, and DI wiring

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Resources/ReferenceMessages.cs`
- Create: `backend/Kheprx.BaseBackend.Api/Controllers/ReferenceController.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/ReferenceControllerTests.cs`

**Interfaces:**
- Consumes: `IReferenceService` (Task 3); `BaseApiController`, `ApiResponse<T>`, `AppLanguage.Current`.
- Produces: `GET /api/reference/clubs` → `ApiResponse<IReadOnlyList<ClubDto>>`; `GET /api/reference/blood-types|strokes|genders` → `ApiResponse<IReadOnlyList<CodedLookupDto>>`. All authed.

- [ ] **Step 1: Write the failing test**

```csharp
using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class ReferenceControllerTests
{
    [Fact]
    public async Task Clubs_returns_200_with_clubs()
    {
        var svc = new Mock<IReferenceService>();
        svc.Setup(s => s.GetClubsAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<ClubDto> { new(Guid.NewGuid(), "Al Ahly", "الأهلي") });

        var result = await new ReferenceController(svc.Object).Clubs(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<ClubDto>>>(ok.Value);
        Assert.Single(body.Data!);
        Assert.Equal("Al Ahly", body.Data![0].NameEn);
    }

    [Fact]
    public async Task Strokes_returns_200_with_coded_lookups()
    {
        var svc = new Mock<IReferenceService>();
        svc.Setup(s => s.GetStrokesAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<CodedLookupDto> { new(Guid.NewGuid(), "medley", "IM", "متنوع فردي") });

        var result = await new ReferenceController(svc.Object).Strokes(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<CodedLookupDto>>>(ok.Value);
        Assert.Equal("medley", body.Data![0].Code);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests/Kheprx.BaseBackend.Api.UnitTests.csproj --filter "FullyQualifiedName~ReferenceControllerTests" -v minimal --nologo`
Expected: FAIL — `ReferenceController` does not exist (compile error).

- [ ] **Step 3: Write messages, controller, and DI**

`ReferenceMessages.cs`:
```csharp
namespace Kheprx.BaseBackend.Identity.Application.Resources;

/// <summary>Localized success messages for reference lookup queries.</summary>
public static class ReferenceMessages
{
    public static class Success
    {
        public static string ClubsListed(string lang) => lang switch { "ar" => "قائمة الأندية", _ => "Clubs" };
        public static string BloodTypesListed(string lang) => lang switch { "ar" => "فصائل الدم", _ => "Blood types" };
        public static string StrokesListed(string lang) => lang switch { "ar" => "أنواع السباحة", _ => "Strokes" };
        public static string GendersListed(string lang) => lang switch { "ar" => "الأنواع", _ => "Genders" };
    }
}
```

`ReferenceController.cs` (mirrors `RolesController`; authed — no `[AllowAnonymous]`):
```csharp
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Authorize]
public sealed class ReferenceController : BaseApiController
{
    private readonly IReferenceService _service;

    public ReferenceController(IReferenceService service)
    {
        _service = service;
    }

    /// <summary>Lists all clubs (training / championship club selectors).</summary>
    [HttpGet("clubs")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ClubDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ClubDto>>>> Clubs(CancellationToken ct)
    {
        var data = await _service.GetClubsAsync(ct);
        var body = ApiResponse<IReadOnlyList<ClubDto>>.Success(
            ReferenceMessages.Success.ClubsListed(AppLanguage.Current), data);
        return Ok(body);
    }

    /// <summary>Lists all blood types.</summary>
    [HttpGet("blood-types")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CodedLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CodedLookupDto>>>> BloodTypes(CancellationToken ct)
    {
        var data = await _service.GetBloodTypesAsync(ct);
        var body = ApiResponse<IReadOnlyList<CodedLookupDto>>.Success(
            ReferenceMessages.Success.BloodTypesListed(AppLanguage.Current), data);
        return Ok(body);
    }

    /// <summary>Lists all swim strokes (specialization chips; IM = medley).</summary>
    [HttpGet("strokes")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CodedLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CodedLookupDto>>>> Strokes(CancellationToken ct)
    {
        var data = await _service.GetStrokesAsync(ct);
        var body = ApiResponse<IReadOnlyList<CodedLookupDto>>.Success(
            ReferenceMessages.Success.StrokesListed(AppLanguage.Current), data);
        return Ok(body);
    }

    /// <summary>Lists all genders.</summary>
    [HttpGet("genders")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CodedLookupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CodedLookupDto>>>> Genders(CancellationToken ct)
    {
        var data = await _service.GetGendersAsync(ct);
        var body = ApiResponse<IReadOnlyList<CodedLookupDto>>.Success(
            ReferenceMessages.Success.GendersListed(AppLanguage.Current), data);
        return Ok(body);
    }
}
```

In `IdentityModuleExtensions.AddIdentityModule`, register the repositories and service (add after the existing `ISwimmerService` line, before `return services;`):
```csharp
        services.AddScoped<IClubRepository, ClubRepository>();
        services.AddScoped<IBloodTypeRepository, BloodTypeRepository>();
        services.AddScoped<IStrokeRepository, StrokeRepository>();
        services.AddScoped<IGenderRepository, GenderRepository>();
        services.AddScoped<IReferenceService, ReferenceService>();
```

- [ ] **Step 4: Run the test (and a full build) to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests/Kheprx.BaseBackend.Api.UnitTests.csproj --filter "FullyQualifiedName~ReferenceControllerTests" -v minimal --nologo`
Expected: PASS (2 tests). (The build compiling confirms the DI wiring references resolve.)

- [ ] **Step 5: Commit** (hold per Global Constraints)

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Resources/ReferenceMessages.cs \
        backend/Kheprx.BaseBackend.Api/Controllers/ReferenceController.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs \
        backend/tests/Kheprx.BaseBackend.Api.UnitTests/ReferenceControllerTests.cs
git commit -m "feat(api): expose GET /api/reference/{clubs,blood-types,strokes,genders}"
```

---

### Task 5: Seed strokes, blood types, and clubs

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentitySeeder.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Data/IdentitySeederTests.cs` (add one `[Fact]`)

**Interfaces:**
- Consumes: `Stroke`, `BloodType`, `Club` entities + DbSets (Tasks 1–2).
- Produces: after `SeedAsync`, exactly 5 strokes, 8 blood types, 70 clubs exist; re-running does not duplicate.

- [ ] **Step 1: Write the failing test** (add to `IdentitySeederTests`)

```csharp
    [Fact]
    public async Task Seeding_creates_reference_lookups_once()
    {
        var hasher = new PasswordHasher();
        await using var db = NewDb();
        await IdentitySeeder.SeedAsync(db, hasher);
        await IdentitySeeder.SeedAsync(db, hasher);

        Assert.Equal(5, await db.Strokes.CountAsync());
        Assert.Equal(8, await db.BloodTypes.CountAsync());
        Assert.Equal(70, await db.Clubs.CountAsync());
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~IdentitySeederTests" -v minimal --nologo`
Expected: FAIL — `db.Strokes`/`db.BloodTypes`/`db.Clubs` are empty (counts are 0).

- [ ] **Step 3: Extend the seeder**

In `SeedAsync`, after the genders block (`await EnsureGender(db, "female", ...)`) and its `await db.SaveChangesAsync(ct);`, add:
```csharp
        await EnsureStrokes(db, ct);
        await EnsureBloodTypes(db, ct);
        await EnsureClubs(db, ct);
        await db.SaveChangesAsync(ct);
```

Add these helpers and the club data to the `IdentitySeeder` class:
```csharp
    private static async Task EnsureStrokes(IdentityDbContext db, CancellationToken ct)
    {
        await EnsureStroke(db, "freestyle", "Freestyle", "حرة", ct);
        await EnsureStroke(db, "backstroke", "Backstroke", "ظهر", ct);
        await EnsureStroke(db, "butterfly", "Butterfly", "فراشة", ct);
        await EnsureStroke(db, "breaststroke", "Breaststroke", "صدر", ct);
        await EnsureStroke(db, "medley", "IM", "متنوع فردي", ct);
    }

    private static async Task EnsureStroke(IdentityDbContext db, string code, string en, string ar, CancellationToken ct)
    {
        if (!await db.Strokes.AnyAsync(s => s.Code == code, ct))
            await db.Strokes.AddAsync(new Stroke(code, en, ar), ct);
    }

    private static async Task EnsureBloodTypes(IdentityDbContext db, CancellationToken ct)
    {
        foreach (var code in new[] { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-" })
            if (!await db.BloodTypes.AnyAsync(b => b.Code == code, ct))
                await db.BloodTypes.AddAsync(new BloodType(code, code, code), ct);
    }

    private static async Task EnsureClubs(IdentityDbContext db, CancellationToken ct)
    {
        foreach (var (en, ar) in Clubs)
            if (!await db.Clubs.AnyAsync(c => c.NameEn == en, ct))
                await db.Clubs.AddAsync(new Club(en, ar), ct);
    }

    // Fixed Egyptian clubs — spec 2026-08-30-swimming-database-design §10 (English transliterations best-effort).
    private static readonly (string En, string Ar)[] Clubs =
    {
        ("Al Ahly", "الأهلي"),
        ("Zamalek", "الزمالك"),
        ("Pyramids", "بيراميدز"),
        ("Al Ittihad Alexandria", "الاتحاد السكندري"),
        ("Al Masry (Port Said)", "المصري البورسعيدي"),
        ("Ismaily", "الإسماعيلي"),
        ("Smouha", "سموحة"),
        ("ENPPI", "إنبي"),
        ("Wadi Degla", "وادي دجلة"),
        ("ZED FC", "زد إف سي"),
        ("Ceramica Cleopatra", "سيراميكا كليوباترا"),
        ("Modern Sport", "مودرن سبورت"),
        ("National Bank of Egypt", "البنك الأهلي المصري"),
        ("El Gouna", "الجونة"),
        ("Pharco", "فاركو"),
        ("Petrojet", "بتروجت"),
        ("Ghazl El Mahalla", "غزل المحلة"),
        ("Haras El Hodood", "حرس الحدود"),
        ("Tala'ea El Gaish", "طلائع الجيش"),
        ("Arab Contractors", "المقاولون العرب"),
        ("Ismailia Electricity", "كهرباء الإسماعيلية"),
        ("El Tersana", "الترسانة"),
        ("Tanta", "طنطا"),
        ("El Sekka El Hadeed (Railways)", "السكة الحديد"),
        ("Aswan", "أسوان"),
        ("El Qanah", "القناة"),
        ("La Viena", "لافيينا"),
        ("Abu Qir Fertilizers", "أبو قير للأسمدة"),
        ("Telecom Egypt", "المصرية للاتصالات"),
        ("Asyut Petroleum", "بترول أسيوط"),
        ("El Mansoura", "المنصورة"),
        ("Baladeyet El Mahalla", "بلدية المحلة"),
        ("El Dakhleya", "الداخلية"),
        ("El Entag El Harby", "الإنتاج الحربي"),
        ("Suez Team", "منتخب السويس"),
        ("El Shams", "الشمس"),
        ("El Nasr", "النصر"),
        ("El Obour", "العبور"),
        ("El Merreikh", "المريخ"),
        ("Port Fouad", "بورفؤاد"),
        ("Eastern Company", "إيسترن كومباني"),
        ("El Nogoom", "النجوم"),
        ("Gomhoreyet Shebin", "جمهورية شبين"),
        ("Benha", "بنها"),
        ("El Plastic", "البلاستيك"),
        ("Sporting Alexandria", "سبورتنج السكندري"),
        ("El Olympi", "الأوليمبي"),
        ("El Hammam", "الحمام"),
        ("Damanhour", "دمنهور"),
        ("Kafr El Sheikh", "كفر الشيخ"),
        ("Damietta", "دمياط"),
        ("Dekernes", "دكرنس"),
        ("Beni Ebeid", "بني عبيد"),
        ("Nabaroh", "نبروه"),
        ("El Minya", "المنيا"),
        ("El Fayoum", "الفيوم"),
        ("Misr El Makkasa", "مصر المقاصة"),
        ("Beni Suef Telecom", "تليفونات بني سويف"),
        ("Aluminium", "الألومنيوم"),
        ("Kima Aswan", "كيما أسوان"),
        ("Tahta", "طهطا"),
        ("Luxor", "الأقصر"),
        ("El Nasr Mining", "النصر للتعدين"),
        ("Asyut Cement", "أسمنت أسيوط"),
        ("Shoban Muslimeen Qena", "شبان مسلمين قنا"),
        ("El Badari", "البداري"),
        ("Aviation Club", "نادي الطيران"),
        ("Shooting Club", "نادي الصيد"),
        ("Palm Hills", "بالم هيلز"),
        ("6th of October Club", "نادي 6 أكتوبر"),
    };
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~IdentitySeederTests" -v minimal --nologo`
Expected: PASS (both the existing idempotency test and the new lookups test).

- [ ] **Step 5: Commit** (hold per Global Constraints)

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentitySeeder.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Data/IdentitySeederTests.cs
git commit -m "feat(identity): seed 5 strokes, 8 blood types, 70 clubs (idempotent)"
```

---

### Task 6: Migration `AddReferenceLookups`

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Migrations/<timestamp>_AddReferenceLookups.cs` (+ `.Designer.cs`, generated)
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Migrations/IdentityDbContextModelSnapshot.cs` (generated)

**Interfaces:**
- Consumes: entities + configs + DbSets (Tasks 1–2).
- Produces: a migration whose `Up` creates only `reference.stroke`, `reference.blood_type`, `reference.club` and the two unique indexes on `stroke.code` / `blood_type.code`.

> This task has no unit test — the deliverable is a correct, isolated migration. **Stop the running dev API server first** (build lock).

- [ ] **Step 1: Safety pre-check — confirm no unrelated pending model changes**

Run:
```bash
dotnet ef migrations has-pending-model-changes \
  --project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --startup-project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure
```
Expected: it reports pending changes (our three new tables). If `dotnet ef` is missing: `dotnet tool install --global dotnet-ef`.
**If it reports changes unrelated to the reference lookups, STOP** and resolve with the user before continuing — do not let a mixed migration through.

- [ ] **Step 2: Generate the migration**

Run:
```bash
dotnet ef migrations add AddReferenceLookups \
  --project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --startup-project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure
```

- [ ] **Step 3: Inspect the generated `Up`**

Open the new `<timestamp>_AddReferenceLookups.cs`. Verify its `Up` **only**:
- creates table `stroke` (schema `reference`) with `id, code, name_en, name_ar`,
- creates table `blood_type` (schema `reference`) with `id, code, name_en, name_ar`,
- creates table `club` (schema `reference`) with `id, name_en, name_ar, created_at`,
- creates unique indexes on `stroke.code` and `blood_type.code`.

If the `Up` contains anything else (columns/tables from the unrelated in-progress refactor), STOP: run `dotnet ef migrations remove --project … --startup-project …` and resolve with the user.

- [ ] **Step 4: Build + full backend test suite**

Run: `dotnet test backend/Kheprx.BaseBackend.sln -v minimal --nologo`
Expected: PASS (all tests, including Tasks 1–5). The migration auto-applies at API startup via `MigrationExtensions`, then the seeder runs — no manual `database update` needed here.

- [ ] **Step 5: Commit** (hold per Global Constraints)

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Migrations/
git commit -m "feat(identity): add AddReferenceLookups migration (stroke, blood_type, club)"
```

---

### Task 7: Frontend reference DTOs + validators

**Files:**
- Create: `frontend/src/app/features/reference/data/dto/reference.dto.ts`
- Test: `frontend/src/app/features/reference/testing/data/dto/reference.dto.spec.ts`

**Interfaces:**
- Consumes: `BaseResponseRs<T>` from `@core/network/api/base-response-rs`.
- Produces: `CodedLookupDtoRs { id; code; nameEn; nameAr }`, `ClubDtoRs { id; nameEn; nameAr }`, `CodedLookupListDtoRs extends BaseResponseRs<CodedLookupDtoRs[]>`, `ClubListDtoRs extends BaseResponseRs<ClubDtoRs[]>`, and `isCodedLookupListValid(data): boolean`, `isClubListValid(data): boolean`.

- [ ] **Step 1: Write the failing test**

```ts
import { isClubListValid, isCodedLookupListValid } from '@features/reference/data/dto/reference.dto';

describe('reference.dto validators', () => {
  it('accepts a valid club list', () => {
    expect(isClubListValid([{ id: 'c1', nameEn: 'Al Ahly', nameAr: 'الأهلي' }])).toBe(true);
  });

  it('rejects a club missing id', () => {
    expect(isClubListValid([{ nameEn: 'X', nameAr: null }])).toBe(false);
  });

  it('accepts a valid coded-lookup list', () => {
    expect(isCodedLookupListValid([{ id: 's1', code: 'medley', nameEn: 'IM', nameAr: null }])).toBe(true);
  });

  it('rejects a coded lookup missing code', () => {
    expect(isCodedLookupListValid([{ id: 's1', nameEn: 'IM', nameAr: null }])).toBe(false);
  });

  it('rejects a non-array', () => {
    expect(isClubListValid('nope')).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npm test -- reference.dto`
Expected: FAIL — module `reference.dto` not found.

- [ ] **Step 3: Write the DTOs + validators**

```ts
// reference.dto.ts — reference lookup response DTOs (API_FLOW convention).
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface CodedLookupDtoRs {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string | null;
}

export interface ClubDtoRs {
  id: string;
  nameEn: string;
  nameAr: string | null;
}

export interface CodedLookupListDtoRs extends BaseResponseRs<CodedLookupDtoRs[]> {}
export interface ClubListDtoRs extends BaseResponseRs<ClubDtoRs[]> {}

function isNonEmptyString(v: unknown): v is string {
  return typeof v === 'string' && v.length > 0;
}

export function isCodedLookupListValid(data: unknown): data is CodedLookupDtoRs[] {
  return (
    Array.isArray(data) &&
    data.every(
      (x) =>
        x != null &&
        typeof x === 'object' &&
        isNonEmptyString((x as CodedLookupDtoRs).id) &&
        isNonEmptyString((x as CodedLookupDtoRs).code) &&
        isNonEmptyString((x as CodedLookupDtoRs).nameEn),
    )
  );
}

export function isClubListValid(data: unknown): data is ClubDtoRs[] {
  return (
    Array.isArray(data) &&
    data.every(
      (x) =>
        x != null &&
        typeof x === 'object' &&
        isNonEmptyString((x as ClubDtoRs).id) &&
        isNonEmptyString((x as ClubDtoRs).nameEn),
    )
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npm test -- reference.dto`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit** (hold per Global Constraints)

```bash
git add frontend/src/app/features/reference/data/dto/reference.dto.ts \
        frontend/src/app/features/reference/testing/data/dto/reference.dto.spec.ts
git commit -m "feat(reference): add lookup response DTOs + validators"
```

---

### Task 8: Frontend model, repository port/impl, and providers

**Files:**
- Create: `frontend/src/app/features/reference/domain/model/reference.ts`
- Create: `frontend/src/app/features/reference/domain/repositories/reference.repository.ts`
- Create: `frontend/src/app/features/reference/data/repositories/reference.repository.impl.ts`
- Create: `frontend/src/app/features/reference/data/reference.providers.ts`
- Create: `frontend/src/app/features/reference/index.ts`
- Modify: `frontend/src/app/app.config.ts`
- Test: `frontend/src/app/features/reference/testing/data/repositories/reference.repository.impl.spec.ts`

**Interfaces:**
- Consumes: `ClubListDtoRs`, `CodedLookupListDtoRs` (Task 7); `HttpClientService` from `@core/network/api/http-client`.
- Produces: `LookupItem { id: string; code?: string; nameEn: string; nameAr: string | null }`; `IReferenceRepository { getClubs(): Promise<ClubListDtoRs>; getBloodTypes/getStrokes/getGenders(): Promise<CodedLookupListDtoRs> }` + `REFERENCE_REPOSITORY` token; `ReferenceRepositoryImpl`; `REFERENCE_PROVIDERS`.

- [ ] **Step 1: Write the failing test**

```ts
import { TestBed } from '@angular/core/testing';
import { ReferenceRepositoryImpl } from '@features/reference/data/repositories/reference.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('ReferenceRepositoryImpl', () => {
  const http = { get: jest.fn() } as unknown as HttpClientService;
  let repo: ReferenceRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [ReferenceRepositoryImpl, { provide: HttpClientService, useValue: http }],
    });
    repo = TestBed.inject(ReferenceRepositoryImpl);
  });

  it('getClubs GETs /api/reference/clubs', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.getClubs();
    expect(http.get).toHaveBeenCalledWith('/api/reference/clubs');
  });

  it('getBloodTypes GETs /api/reference/blood-types', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.getBloodTypes();
    expect(http.get).toHaveBeenCalledWith('/api/reference/blood-types');
  });

  it('getStrokes GETs /api/reference/strokes', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.getStrokes();
    expect(http.get).toHaveBeenCalledWith('/api/reference/strokes');
  });

  it('getGenders GETs /api/reference/genders', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.getGenders();
    expect(http.get).toHaveBeenCalledWith('/api/reference/genders');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npm test -- reference.repository.impl`
Expected: FAIL — module `reference.repository.impl` not found.

- [ ] **Step 3: Write model, port, impl, providers, barrel, and wire app.config**

```ts
// reference.ts — a resolved lookup option (value = id; label = nameEn/nameAr).
export interface LookupItem {
  id: string;
  code?: string;
  nameEn: string;
  nameAr: string | null;
}
```

```ts
// reference.repository.ts — port + DI token.
import { InjectionToken } from '@angular/core';
import { ClubListDtoRs, CodedLookupListDtoRs } from '@features/reference/data/dto/reference.dto';

export interface IReferenceRepository {
  getClubs(): Promise<ClubListDtoRs>;
  getBloodTypes(): Promise<CodedLookupListDtoRs>;
  getStrokes(): Promise<CodedLookupListDtoRs>;
  getGenders(): Promise<CodedLookupListDtoRs>;
}

export const REFERENCE_REPOSITORY = new InjectionToken<IReferenceRepository>('REFERENCE_REPOSITORY');
```

```ts
// reference.repository.impl.ts — fetch-only reference repository (/api/reference/*).
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { IReferenceRepository } from '@features/reference/domain/repositories/reference.repository';
import { ClubListDtoRs, CodedLookupListDtoRs } from '@features/reference/data/dto/reference.dto';

@Injectable({ providedIn: 'root' })
export class ReferenceRepositoryImpl implements IReferenceRepository {
  private readonly http = inject(HttpClientService);

  getClubs(): Promise<ClubListDtoRs> {
    return this.http.get<ClubListDtoRs>('/api/reference/clubs');
  }
  getBloodTypes(): Promise<CodedLookupListDtoRs> {
    return this.http.get<CodedLookupListDtoRs>('/api/reference/blood-types');
  }
  getStrokes(): Promise<CodedLookupListDtoRs> {
    return this.http.get<CodedLookupListDtoRs>('/api/reference/strokes');
  }
  getGenders(): Promise<CodedLookupListDtoRs> {
    return this.http.get<CodedLookupListDtoRs>('/api/reference/genders');
  }
}
```

```ts
// reference.providers.ts — bind the reference repository port to the fetch-only impl.
import { Provider } from '@angular/core';
import { REFERENCE_REPOSITORY } from '@features/reference/domain/repositories/reference.repository';
import { ReferenceRepositoryImpl } from '@features/reference/data/repositories/reference.repository.impl';

export const REFERENCE_PROVIDERS: Provider[] = [
  { provide: REFERENCE_REPOSITORY, useClass: ReferenceRepositoryImpl },
];
```

```ts
// index.ts — reference feature barrel.
export * from '@features/reference/data/reference.providers';
```

In `app.config.ts`: add the import near the other feature-provider imports, and spread `...REFERENCE_PROVIDERS` into the `providers` array alongside `...SWIMMER_PROVIDERS`:
```ts
import { REFERENCE_PROVIDERS } from '@features/reference/data/reference.providers';
// ...
  providers: [
    // ...existing...
    ...SWIMMER_PROVIDERS,
    ...REFERENCE_PROVIDERS,
  ],
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npm test -- reference.repository.impl`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit** (hold per Global Constraints)

```bash
git add frontend/src/app/features/reference/domain/model/reference.ts \
        frontend/src/app/features/reference/domain/repositories/reference.repository.ts \
        frontend/src/app/features/reference/data/repositories/reference.repository.impl.ts \
        frontend/src/app/features/reference/data/reference.providers.ts \
        frontend/src/app/features/reference/index.ts \
        frontend/src/app/app.config.ts \
        frontend/src/app/features/reference/testing/data/repositories/reference.repository.impl.spec.ts
git commit -m "feat(reference): add repository port/impl + DI wiring"
```

---

### Task 9: Frontend load use-cases

**Files:**
- Create: `frontend/src/app/features/reference/domain/usecases/load-clubs.use-case.ts`
- Create: `frontend/src/app/features/reference/domain/usecases/load-blood-types.use-case.ts`
- Create: `frontend/src/app/features/reference/domain/usecases/load-strokes.use-case.ts`
- Create: `frontend/src/app/features/reference/domain/usecases/load-genders.use-case.ts`
- Test: `frontend/src/app/features/reference/testing/domain/usecases/load-reference.use-cases.spec.ts`

**Interfaces:**
- Consumes: `REFERENCE_REPOSITORY`, `IReferenceRepository` (Task 8); `LookupItem` (Task 8); `isClubListValid`, `isCodedLookupListValid` (Task 7); `UseCase`, `AppError`.
- Produces: `LoadClubsUseCase`, `LoadBloodTypesUseCase`, `LoadStrokesUseCase`, `LoadGendersUseCase` — each `UseCase<void, LookupItem[]>` returning mapped items (clubs have `code` undefined; the others carry `code`).

- [ ] **Step 1: Write the failing test**

```ts
import { TestBed } from '@angular/core/testing';
import { LoadClubsUseCase } from '@features/reference/domain/usecases/load-clubs.use-case';
import { LoadStrokesUseCase } from '@features/reference/domain/usecases/load-strokes.use-case';
import { REFERENCE_REPOSITORY, IReferenceRepository } from '@features/reference/domain/repositories/reference.repository';

function makeRepo(overrides: Partial<IReferenceRepository> = {}): IReferenceRepository {
  return {
    getClubs: async () => ({ data: [{ id: 'c1', nameEn: 'Al Ahly', nameAr: 'الأهلي' }] }),
    getBloodTypes: async () => ({ data: [{ id: 'b1', code: 'O+', nameEn: 'O+', nameAr: 'O+' }] }),
    getStrokes: async () => ({ data: [{ id: 's1', code: 'medley', nameEn: 'IM', nameAr: null }] }),
    getGenders: async () => ({ data: [{ id: 'g1', code: 'male', nameEn: 'Male', nameAr: 'ذكر' }] }),
    ...overrides,
  } as unknown as IReferenceRepository;
}

function build<T>(type: new (...args: never[]) => T, repo: IReferenceRepository): T {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: REFERENCE_REPOSITORY, useValue: repo }] });
  return TestBed.inject(type);
}

describe('reference load use-cases', () => {
  it('LoadClubs maps to LookupItem[] with no code', async () => {
    const uc = build(LoadClubsUseCase, makeRepo());
    const r = await uc.run();
    expect(r.ok).toBe(true);
    if (r.ok) {
      expect(r.data[0].id).toBe('c1');
      expect(r.data[0].nameEn).toBe('Al Ahly');
      expect(r.data[0].code).toBeUndefined();
    }
  });

  it('LoadStrokes carries the stroke code through', async () => {
    const uc = build(LoadStrokesUseCase, makeRepo());
    const r = await uc.run();
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.data[0].code).toBe('medley');
  });

  it('LoadClubs fails validation on malformed data', async () => {
    const uc = build(LoadClubsUseCase, makeRepo({ getClubs: async () => ({ data: [{ nameEn: 'x', nameAr: null }] }) as never }));
    const r = await uc.run();
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npm test -- load-reference.use-cases`
Expected: FAIL — the use-case modules do not exist.

- [ ] **Step 3: Write the four use-cases**

```ts
// load-clubs.use-case.ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { REFERENCE_REPOSITORY } from '@features/reference/domain/repositories/reference.repository';
import { isClubListValid } from '@features/reference/data/dto/reference.dto';
import { LookupItem } from '@features/reference/domain/model/reference';

@Injectable({ providedIn: 'root' })
export class LoadClubsUseCase extends UseCase<void, LookupItem[]> {
  private readonly repo = inject(REFERENCE_REPOSITORY);
  constructor() { super('LoadClubs'); }
  protected async execute(): Promise<LookupItem[]> {
    const res = await this.repo.getClubs();
    if (!isClubListValid(res.data)) throw new AppError('Invalid clubs received', 'validation');
    return res.data.map((c) => ({ id: c.id, nameEn: c.nameEn, nameAr: c.nameAr }));
  }
}
```

```ts
// load-blood-types.use-case.ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { REFERENCE_REPOSITORY } from '@features/reference/domain/repositories/reference.repository';
import { isCodedLookupListValid } from '@features/reference/data/dto/reference.dto';
import { LookupItem } from '@features/reference/domain/model/reference';

@Injectable({ providedIn: 'root' })
export class LoadBloodTypesUseCase extends UseCase<void, LookupItem[]> {
  private readonly repo = inject(REFERENCE_REPOSITORY);
  constructor() { super('LoadBloodTypes'); }
  protected async execute(): Promise<LookupItem[]> {
    const res = await this.repo.getBloodTypes();
    if (!isCodedLookupListValid(res.data)) throw new AppError('Invalid blood types received', 'validation');
    return res.data.map((b) => ({ id: b.id, code: b.code, nameEn: b.nameEn, nameAr: b.nameAr }));
  }
}
```

```ts
// load-strokes.use-case.ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { REFERENCE_REPOSITORY } from '@features/reference/domain/repositories/reference.repository';
import { isCodedLookupListValid } from '@features/reference/data/dto/reference.dto';
import { LookupItem } from '@features/reference/domain/model/reference';

@Injectable({ providedIn: 'root' })
export class LoadStrokesUseCase extends UseCase<void, LookupItem[]> {
  private readonly repo = inject(REFERENCE_REPOSITORY);
  constructor() { super('LoadStrokes'); }
  protected async execute(): Promise<LookupItem[]> {
    const res = await this.repo.getStrokes();
    if (!isCodedLookupListValid(res.data)) throw new AppError('Invalid strokes received', 'validation');
    return res.data.map((s) => ({ id: s.id, code: s.code, nameEn: s.nameEn, nameAr: s.nameAr }));
  }
}
```

```ts
// load-genders.use-case.ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { REFERENCE_REPOSITORY } from '@features/reference/domain/repositories/reference.repository';
import { isCodedLookupListValid } from '@features/reference/data/dto/reference.dto';
import { LookupItem } from '@features/reference/domain/model/reference';

@Injectable({ providedIn: 'root' })
export class LoadGendersUseCase extends UseCase<void, LookupItem[]> {
  private readonly repo = inject(REFERENCE_REPOSITORY);
  constructor() { super('LoadGenders'); }
  protected async execute(): Promise<LookupItem[]> {
    const res = await this.repo.getGenders();
    if (!isCodedLookupListValid(res.data)) throw new AppError('Invalid genders received', 'validation');
    return res.data.map((g) => ({ id: g.id, code: g.code, nameEn: g.nameEn, nameAr: g.nameAr }));
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npm test -- load-reference.use-cases`
Expected: PASS (3 tests).

- [ ] **Step 5: Run the full frontend suite + typecheck**

Run: `cd frontend && npm test`
Expected: PASS (whole suite; no regressions).

- [ ] **Step 6: Commit** (hold per Global Constraints)

```bash
git add frontend/src/app/features/reference/domain/usecases/ \
        frontend/src/app/features/reference/testing/domain/usecases/load-reference.use-cases.spec.ts
git commit -m "feat(reference): add Load{Clubs,BloodTypes,Strokes,Genders} use-cases"
```

---

## Final verification (after all tasks)

- [ ] Backend: `dotnet test backend/Kheprx.BaseBackend.sln -v minimal --nologo` — all green (dev API stopped first).
- [ ] Frontend: `cd frontend && npm test` — all green.
- [ ] Smoke: `dotnet run --project backend/Kheprx.BaseBackend.Api` starts, applies `AddReferenceLookups`, seeds; then (with a valid bearer token) `GET /api/reference/clubs` returns 70 clubs, `/strokes` 5, `/blood-types` 8, `/genders` 2; unauthenticated calls return 401.
- [ ] Confirm no `distance` table, no form UI, and no `captain_club` were introduced (scope guard).

## Spec coverage check

- reference.stroke / blood_type / club entities + configs → Tasks 1–2. ✓
- Read endpoints (clubs/blood-types/strokes/genders), authed → Task 4. ✓
- Genders read path (new repository) → Tasks 2–4. ✓
- Seeds: 5 strokes (IM=medley), 8 blood types, 70 §10 clubs, idempotent → Task 5. ✓
- Migration with mixed-migration safety pre-check → Task 6. ✓
- Angular `features/reference` slice (DTO+validator, model, port/impl, providers, 4 use-cases), labels by id → Tasks 7–9. ✓
- Excluded (verified absent): distance, form UI, profile schema changes, Assigned Clubs → Final verification scope guard. ✓
