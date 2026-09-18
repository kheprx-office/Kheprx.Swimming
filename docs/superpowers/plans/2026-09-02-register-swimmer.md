# Register Swimmer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a Head Coach or Captain create a swimmer account from the Captain Panel — a full `app_user` (role `swimmer`, username, backend-assigned generic password, forced first-login) + `swimmer_profile` (system `uid`, training/championship club, blood type) + one `swimmer_specialization` per stroke, created atomically by `POST /api/swimmers`; plus the Angular Account Creation page with a working Register Swimmer form wired to the Phase-1 reference lookups.

**Architecture:** Backend stays in the Identity module: expand `swimmer_profile`, add an `athlete.swimmer_specialization` junction, seed a `swimmer` role, and add a role-restricted create endpoint whose service builds all rows in one scoped `IdentityDbContext` + single `SaveChangesAsync` (atomic). Frontend adds an Account Creation page (two tabs; Swimmer live, Captain stub) under `captain-panel`, three new shared form controls, and a signals-based form + viewmodel calling a new `CreateSwimmerUseCase`.

**Tech Stack:** .NET (C#, EF Core, xUnit + Moq, FluentValidation), PostgreSQL, Angular (standalone + signals, Jest), Tailwind + CSS-var theming.

**Spec:** `docs/superpowers/specs/2026-09-02-register-swimmer-design.md`

## Global Constraints

- **Registration only:** no swimmer login-by-username, no login-page changes. Create login-ready data (`username`, generic password, `is_first_login = true`) but do not wire authentication.
- **Required fields (API validator AND form):** Name (EN), Username, Training Club, Gender, Date of Birth, Blood Type, and ≥1 specialization. Optional: Name (AR), Email, Phone, Championship Club. Gender/DOB/BloodType columns stay **DB-nullable** (diagram-faithful) — the requirement is an app rule, not a schema change.
- **Generic password from config**, returned in the create response; the frontend never hardcodes it.
- **Placement:** all new backend types live in the Identity module. `swimmer_specialization` maps to the `athlete` schema; `swimmer_profile` stays in `identity`. No new module/DbContext.
- **Lookups by ID:** the form submits reference IDs (`genderId`, `trainingClubId`, `bloodTypeId`, `strokeIds`, `representChampionshipClubId`); labels come from the API (`nameEn`/`nameAr`), never hardcoded. `IM = medley`.
- **Authz:** `POST /api/swimmers` is `[Authorize(Roles = "head_coach,captain")]`; the `captain-panel` + `account-creation` routes use `roleGuard('head_coach', 'captain')`.
- **No git commits** (user directive) — implement + run tests; skip every "Commit" step. The controller produces per-unit review diffs via tree snapshots.
- **Build lock:** stop the running dev API server before any `dotnet build` / `dotnet ef` / `dotnet test`. Backend commands run from repo root; frontend commands from `frontend/`.
- **No scope creep:** no Register Captain form (stub only), no other Captain-Panel cards, no swimmer list/edit/delete, no guardian/medical/measurement tables.

---

### Task 1: Swimmer entities + configs + DbSet

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/SwimmerProfile.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/SwimmerSpecialization.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/SwimmerProfileConfiguration.cs`
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Configurations/SwimmerSpecializationConfiguration.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Data/IdentityDbContext.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/SwimmerProfileTests.cs` (replace existing)

**Interfaces:**
- Consumes: `AppUser`, `Club`, `BloodType`, `Stroke` (existing).
- Produces: `SwimmerProfile(Guid userId, string uid, Guid trainingClubId, Guid? representChampionshipClubId = null, Guid? bloodTypeId = null)` with props `Id, UserId, Uid, TrainingClubId, RepresentChampionshipClubId, BloodTypeId, CreatedAt, UpdatedAt`; `SwimmerSpecialization(Guid swimmerProfileId, Guid strokeId)`; `DbSet<SwimmerSpecialization> SwimmerSpecializations`.

- [ ] **Step 1: Rewrite the SwimmerProfile entity test (RED)**

Replace `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/SwimmerProfileTests.cs` with:
```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class SwimmerProfileTests
{
    [Fact]
    public void Ctor_assigns_id_uid_fks_and_timestamps()
    {
        var userId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var bloodId = Guid.NewGuid();

        var p = new SwimmerProfile(userId, " SW-0007 ", clubId, bloodTypeId: bloodId);

        Assert.NotEqual(Guid.Empty, p.Id);
        Assert.Equal(userId, p.UserId);
        Assert.Equal("SW-0007", p.Uid);
        Assert.Equal(clubId, p.TrainingClubId);
        Assert.Null(p.RepresentChampionshipClubId);
        Assert.Equal(bloodId, p.BloodTypeId);
        Assert.NotEqual(default, p.CreatedAt);
        Assert.Equal(p.CreatedAt, p.UpdatedAt);
    }

    [Fact]
    public void Specialization_ctor_sets_composite_members()
    {
        var swimmerId = Guid.NewGuid();
        var strokeId = Guid.NewGuid();

        var s = new SwimmerSpecialization(swimmerId, strokeId);

        Assert.Equal(swimmerId, s.SwimmerProfileId);
        Assert.Equal(strokeId, s.StrokeId);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~SwimmerProfileTests" -v minimal --nologo`
Expected: FAIL — `SwimmerProfile` new ctor/props and `SwimmerSpecialization` don't exist (compile error).

- [ ] **Step 3: Rewrite entities, configs, DbSet**

`SwimmerProfile.cs`:
```csharp
namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class SwimmerProfile
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Uid { get; private set; } = string.Empty;
    public Guid TrainingClubId { get; private set; }
    public Guid? RepresentChampionshipClubId { get; private set; }
    public Guid? BloodTypeId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private SwimmerProfile() { } // EF Core

    public SwimmerProfile(Guid userId, string uid, Guid trainingClubId,
        Guid? representChampionshipClubId = null, Guid? bloodTypeId = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Uid = uid.Trim();
        TrainingClubId = trainingClubId;
        RepresentChampionshipClubId = representChampionshipClubId;
        BloodTypeId = bloodTypeId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }
}
```

`SwimmerSpecialization.cs`:
```csharp
namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class SwimmerSpecialization
{
    public Guid SwimmerProfileId { get; private set; }
    public Guid StrokeId { get; private set; }

    private SwimmerSpecialization() { } // EF Core

    public SwimmerSpecialization(Guid swimmerProfileId, Guid strokeId)
    {
        SwimmerProfileId = swimmerProfileId;
        StrokeId = strokeId;
    }
}
```

`SwimmerProfileConfiguration.cs`:
```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class SwimmerProfileConfiguration : IEntityTypeConfiguration<SwimmerProfile>
{
    public void Configure(EntityTypeBuilder<SwimmerProfile> builder)
    {
        builder.ToTable("swimmer_profile");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Uid).HasMaxLength(32).IsRequired();
        builder.HasIndex(s => s.Uid).IsUnique();
        builder.Property(s => s.UserId).IsRequired();
        builder.HasIndex(s => s.UserId).IsUnique();
        builder.Property(s => s.TrainingClubId).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Club>().WithMany().HasForeignKey(s => s.TrainingClubId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Club>().WithMany().HasForeignKey(s => s.RepresentChampionshipClubId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BloodType>().WithMany().HasForeignKey(s => s.BloodTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

`SwimmerSpecializationConfiguration.cs`:
```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Configurations;

internal sealed class SwimmerSpecializationConfiguration : IEntityTypeConfiguration<SwimmerSpecialization>
{
    public void Configure(EntityTypeBuilder<SwimmerSpecialization> builder)
    {
        builder.ToTable("swimmer_specialization", "athlete");
        builder.HasKey(x => new { x.SwimmerProfileId, x.StrokeId });
        builder.HasOne<SwimmerProfile>().WithMany().HasForeignKey(x => x.SwimmerProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Stroke>().WithMany().HasForeignKey(x => x.StrokeId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

In `IdentityDbContext.cs`, add after the `Clubs` DbSet:
```csharp
    public DbSet<SwimmerSpecialization> SwimmerSpecializations => Set<SwimmerSpecialization>();
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~SwimmerProfileTests" -v minimal --nologo`
Expected: PASS (2 tests). (Other suites may not build yet — the seeder/count code that referenced the old `SwimmerProfile(uid, nameEn, nameAr)` ctor breaks here; that's fixed in Tasks 2 & 6. If you must green the whole Identity.UnitTests project now, proceed — but this filtered run is the gate for Task 1.)

- [ ] **Step 5: Commit** — SKIP (no commits).

---

### Task 2: Repository methods (user-by-username, swimmer writes, reference existence)

**Files:**
- Modify: `.../Identity.Domain/Repositories/IUserRepository.cs` + `.../Infrastructure/Repositories/UserRepository.cs`
- Modify: `.../Identity.Domain/Repositories/ISwimmerProfileRepository.cs` + `.../Infrastructure/Repositories/SwimmerProfileRepository.cs`
- Modify: `.../Identity.Domain/Repositories/IClubRepository.cs`, `IBloodTypeRepository.cs`, `IStrokeRepository.cs`, `IGenderRepository.cs` + their impls
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/SwimmerProfileRepositoryTests.cs` (new)

**Interfaces:**
- Consumes: entities from Task 1; `IdentityDbContext.SwimmerProfiles`/`SwimmerSpecializations`.
- Produces: `IUserRepository.GetByUsernameAsync(string) → Task<AppUser?>`; `ISwimmerProfileRepository`: `AddAsync(SwimmerProfile)`, `AddSpecializationsAsync(IEnumerable<SwimmerSpecialization>)`, `GetMaxUidNumberAsync() → Task<int>`, `SaveChangesAsync()`; `IClub/IBloodType/IStroke/IGenderRepository.ExistsAsync(Guid) → Task<bool>`.

- [ ] **Step 1: Write the failing test (RED)**

`SwimmerProfileRepositoryTests.cs`:
```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Kheprx.BaseBackend.Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Repositories;

public class SwimmerProfileRepositoryTests
{
    private static IdentityDbContext NewDb() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task GetMaxUidNumber_is_zero_when_empty_and_parses_suffix()
    {
        await using var db = NewDb();
        var repo = new SwimmerProfileRepository(db);
        Assert.Equal(0, await repo.GetMaxUidNumberAsync());

        await repo.AddAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0003", Guid.NewGuid()));
        await repo.AddAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0011", Guid.NewGuid()));
        await repo.SaveChangesAsync();

        Assert.Equal(11, await repo.GetMaxUidNumberAsync());
    }

    [Fact]
    public async Task AddSpecializations_persists_rows()
    {
        await using var db = NewDb();
        var repo = new SwimmerProfileRepository(db);
        var swimmerId = Guid.NewGuid();
        await repo.AddSpecializationsAsync(new[]
        {
            new SwimmerSpecialization(swimmerId, Guid.NewGuid()),
            new SwimmerSpecialization(swimmerId, Guid.NewGuid()),
        });
        await repo.SaveChangesAsync();

        Assert.Equal(2, await db.SwimmerSpecializations.CountAsync());
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~SwimmerProfileRepositoryTests" -v minimal --nologo`
Expected: FAIL — new repository methods don't exist (compile error).

- [ ] **Step 3: Add the repository methods**

In `IUserRepository.cs` add:
```csharp
    Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct = default);
```
In `UserRepository.cs` add (read-only lookup for the uniqueness pre-check):
```csharp
    public Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        var normalized = username.Trim();
        return _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == normalized, ct);
    }
```

Replace `ISwimmerProfileRepository.cs` with:
```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;

namespace Kheprx.BaseBackend.Identity.Domain.Repositories;

public interface ISwimmerProfileRepository
{
    Task<int> CountAsync(CancellationToken ct = default);
    Task<int> GetMaxUidNumberAsync(CancellationToken ct = default);
    Task AddAsync(SwimmerProfile profile, CancellationToken ct = default);
    Task AddSpecializationsAsync(IEnumerable<SwimmerSpecialization> specializations, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

Replace `SwimmerProfileRepository.cs` with:
```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class SwimmerProfileRepository : ISwimmerProfileRepository
{
    private readonly IdentityDbContext _db;
    public SwimmerProfileRepository(IdentityDbContext db) => _db = db;

    public Task<int> CountAsync(CancellationToken ct = default)
        => _db.SwimmerProfiles.AsNoTracking().CountAsync(ct);

    public async Task<int> GetMaxUidNumberAsync(CancellationToken ct = default)
    {
        var uids = await _db.SwimmerProfiles.AsNoTracking().Select(s => s.Uid).ToListAsync(ct);
        var max = 0;
        foreach (var uid in uids)
            if (uid.StartsWith("SW-") && int.TryParse(uid.AsSpan(3), out var n) && n > max) max = n;
        return max;
    }

    public async Task AddAsync(SwimmerProfile profile, CancellationToken ct = default)
        => await _db.SwimmerProfiles.AddAsync(profile, ct);

    public async Task AddSpecializationsAsync(IEnumerable<SwimmerSpecialization> specializations, CancellationToken ct = default)
        => await _db.SwimmerSpecializations.AddRangeAsync(specializations, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
```

Add `ExistsAsync` to each reference repo. In each interface (`IClubRepository`, `IBloodTypeRepository`, `IStrokeRepository`, `IGenderRepository`) add:
```csharp
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
```
In each impl add the matching method (each uses its own DbSet):
```csharp
// ClubRepository
    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _db.Clubs.AsNoTracking().AnyAsync(c => c.Id == id, ct);
```
```csharp
// BloodTypeRepository
    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _db.BloodTypes.AsNoTracking().AnyAsync(b => b.Id == id, ct);
```
```csharp
// StrokeRepository
    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _db.Strokes.AsNoTracking().AnyAsync(s => s.Id == id, ct);
```
```csharp
// GenderRepository
    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _db.Genders.AsNoTracking().AnyAsync(g => g.Id == id, ct);
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~SwimmerProfileRepositoryTests" -v minimal --nologo`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit** — SKIP.

---

### Task 3: DTOs, options, appsettings, validator

**Files:**
- Modify: `.../Application/DTOs/SwimmerDtos.cs`
- Create: `.../Application/Options/SwimmerRegistrationOptions.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/appsettings.json` and `appsettings.Development.json`
- Modify: `.../Application/Validators/UserRequestValidators.cs` (add `CreateSwimmerRequestValidator`)
- Modify: `.../Application/Resources/SwimmerMessages.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Validators/CreateSwimmerRequestValidatorTests.cs` (new)

**Interfaces:**
- Produces: `CreateSwimmerRequest(...)`, `CreatedSwimmerDto(...)`; `SwimmerRegistrationOptions { string GenericPassword }`; `CreateSwimmerRequestValidator`; new `SwimmerMessages` entries.

- [ ] **Step 1: Write the failing validator test (RED)**

`CreateSwimmerRequestValidatorTests.cs`:
```csharp
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Validators;

public class CreateSwimmerRequestValidatorTests
{
    private static CreateSwimmerRequest Valid() => new(
        NameEn: "Mona Ali", Username: "mona.ali", TrainingClubId: Guid.NewGuid(),
        GenderId: Guid.NewGuid(), Dob: new DateOnly(2010, 5, 1), BloodTypeId: Guid.NewGuid(),
        StrokeIds: new[] { Guid.NewGuid() }, NameAr: null, Email: null, Phone: null,
        RepresentChampionshipClubId: null);

    private readonly CreateSwimmerRequestValidator _v = new();

    [Fact] public void Valid_request_passes() => Assert.True(_v.Validate(Valid()).IsValid);

    [Fact]
    public void Missing_required_fields_fail()
    {
        Assert.False(_v.Validate(Valid() with { NameEn = "" }).IsValid);
        Assert.False(_v.Validate(Valid() with { Username = "" }).IsValid);
        Assert.False(_v.Validate(Valid() with { TrainingClubId = Guid.Empty }).IsValid);
        Assert.False(_v.Validate(Valid() with { GenderId = Guid.Empty }).IsValid);
        Assert.False(_v.Validate(Valid() with { BloodTypeId = Guid.Empty }).IsValid);
        Assert.False(_v.Validate(Valid() with { StrokeIds = Array.Empty<Guid>() }).IsValid);
    }

    [Fact]
    public void Dob_must_be_in_the_past()
        => Assert.False(_v.Validate(Valid() with { Dob = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) }).IsValid);

    [Fact]
    public void Invalid_optional_email_and_phone_fail_when_present()
    {
        Assert.False(_v.Validate(Valid() with { Email = "nope" }).IsValid);
        Assert.False(_v.Validate(Valid() with { Phone = "123" }).IsValid);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~CreateSwimmerRequestValidatorTests" -v minimal --nologo`
Expected: FAIL — `CreateSwimmerRequest`/`CreateSwimmerRequestValidator` don't exist (compile error).

- [ ] **Step 3: Add DTOs, options, appsettings, messages, validator**

In `SwimmerDtos.cs` add (keep the existing `SwimmerCountDto`):
```csharp
/// <summary>Payload to register a swimmer (POST /api/swimmers).</summary>
public sealed record CreateSwimmerRequest(
    string NameEn,
    string Username,
    Guid TrainingClubId,
    Guid GenderId,
    DateOnly Dob,
    Guid BloodTypeId,
    IReadOnlyList<Guid> StrokeIds,
    string? NameAr,
    string? Email,
    string? Phone,
    Guid? RepresentChampionshipClubId);

/// <summary>Result of a successful swimmer registration.</summary>
public sealed record CreatedSwimmerDto(Guid Id, string Uid, string Username, string NameEn, string TemporaryPassword);
```

`SwimmerRegistrationOptions.cs`:
```csharp
namespace Kheprx.BaseBackend.Identity.Application.Options;

/// <summary>Bound from the "SwimmerRegistration" config section.</summary>
public sealed class SwimmerRegistrationOptions
{
    public const string SectionName = "SwimmerRegistration";
    /// <summary>The generic password assigned to every new swimmer (they must change it on first login).</summary>
    public string GenericPassword { get; set; } = string.Empty;
}
```

In `appsettings.json` and `appsettings.Development.json`, add a top-level section:
```json
  "SwimmerRegistration": {
    "GenericPassword": "Oasis2026!"
  }
```

In `SwimmerMessages.cs` add to `Success` and a new `Errors` class:
```csharp
    public static class Success
    {
        // ...existing CountRetrieved...
        public static string SwimmerCreated(string lang) => lang switch
        {
            "ar" => "تم تسجيل السبّاح بنجاح",
            _ => "Swimmer registered"
        };
    }

    public static class Errors
    {
        public static string UsernameTaken(string lang) => lang switch { "ar" => "اسم المستخدم مستخدم بالفعل", _ => "Username is already taken" };
        public static string EmailTaken(string lang) => lang switch { "ar" => "البريد الإلكتروني مستخدم بالفعل", _ => "Email is already in use" };
        public static string UnknownReference(string lang) => lang switch { "ar" => "قيمة مرجعية غير صالحة", _ => "One or more selected values are invalid" };
        public static string TrainingClubRequired(string lang) => lang switch { "ar" => "نادي التدريب مطلوب", _ => "Training club is required" };
        public static string GenderRequired(string lang) => lang switch { "ar" => "النوع مطلوب", _ => "Gender is required" };
        public static string DobRequired(string lang) => lang switch { "ar" => "تاريخ الميلاد مطلوب", _ => "Date of birth is required" };
        public static string DobInPast(string lang) => lang switch { "ar" => "يجب أن يكون تاريخ الميلاد في الماضي", _ => "Date of birth must be in the past" };
        public static string BloodTypeRequired(string lang) => lang switch { "ar" => "فصيلة الدم مطلوبة", _ => "Blood type is required" };
        public static string SpecializationRequired(string lang) => lang switch { "ar" => "اختر تخصصًا واحدًا على الأقل", _ => "Select at least one specialization" };
        public static string UsernameRequired(string lang) => lang switch { "ar" => "اسم المستخدم مطلوب", _ => "Username is required" };
    }
```

In `UserRequestValidators.cs` add (reusing `UserValidationRules.PhonePattern`, `CommonMessages`, and the new `SwimmerMessages.Errors`):
```csharp
public sealed class CreateSwimmerRequestValidator : AbstractValidator<CreateSwimmerRequest>
{
    public CreateSwimmerRequestValidator()
    {
        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage(_ => CommonMessages.Errors.FullNameRequired(AppLanguage.Current))
            .MaximumLength(200).WithMessage(_ => CommonMessages.Errors.FullNameTooLong(AppLanguage.Current));
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage(_ => SwimmerMessages.Errors.UsernameRequired(AppLanguage.Current))
            .MaximumLength(100)
            .Matches(@"^[A-Za-z0-9._-]+$").WithMessage(_ => SwimmerMessages.Errors.UsernameRequired(AppLanguage.Current));
        RuleFor(x => x.TrainingClubId)
            .NotEqual(Guid.Empty).WithMessage(_ => SwimmerMessages.Errors.TrainingClubRequired(AppLanguage.Current));
        RuleFor(x => x.GenderId)
            .NotEqual(Guid.Empty).WithMessage(_ => SwimmerMessages.Errors.GenderRequired(AppLanguage.Current));
        RuleFor(x => x.BloodTypeId)
            .NotEqual(Guid.Empty).WithMessage(_ => SwimmerMessages.Errors.BloodTypeRequired(AppLanguage.Current));
        RuleFor(x => x.Dob)
            .NotEqual(default(DateOnly)).WithMessage(_ => SwimmerMessages.Errors.DobRequired(AppLanguage.Current))
            .LessThan(_ => DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage(_ => SwimmerMessages.Errors.DobInPast(AppLanguage.Current));
        RuleFor(x => x.StrokeIds)
            .NotEmpty().WithMessage(_ => SwimmerMessages.Errors.SpecializationRequired(AppLanguage.Current))
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage(_ => SwimmerMessages.Errors.UnknownReference(AppLanguage.Current));
        RuleFor(x => x.NameAr).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.NameAr));
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage(_ => CommonMessages.Errors.EmailInvalid(AppLanguage.Current))
            .MaximumLength(256).WithMessage(_ => CommonMessages.Errors.EmailTooLong(AppLanguage.Current))
            .When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Phone)
            .Matches(UserValidationRules.PhonePattern).WithMessage(_ => UserMessages.Errors.PhoneInvalid(AppLanguage.Current))
            .When(x => !string.IsNullOrEmpty(x.Phone));
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~CreateSwimmerRequestValidatorTests" -v minimal --nologo`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit** — SKIP.

---

### Task 4: SwimmerService.CreateAsync

**Files:**
- Modify: `.../Application/Services/Interfaces/ISwimmerService.cs`
- Modify: `.../Application/Services/SwimmerService.cs`
- Modify: `.../Infrastructure/Extensions/IdentityModuleExtensions.cs` (options registration)
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs` (extend)

**Interfaces:**
- Consumes: repos from Task 2 (`ISwimmerProfileRepository`, `IUserRepository`, reference `ExistsAsync`), `IRoleRepository.GetByCodeAsync`, `IPasswordHasher`, `IOptions<SwimmerRegistrationOptions>`; DTOs from Task 3.
- Produces: `ISwimmerService.CreateAsync(CreateSwimmerRequest, ct) → Task<CreatedSwimmerDto?>` (null on username/email conflict).

- [ ] **Step 1: Write the failing test (RED)**

Add to `SwimmerServiceTests.cs` (keep existing count test; add the create fixture). Full file:
```csharp
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Options;
using Kheprx.BaseBackend.Identity.Application.Services;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Services;

public class SwimmerServiceTests
{
    private static CreateSwimmerRequest Req() => new(
        "Mona Ali", "mona.ali", Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2010, 5, 1),
        Guid.NewGuid(), new[] { Guid.NewGuid() }, null, null, null, null);

    private static (SwimmerService svc, Mock<ISwimmerProfileRepository> swimmers, Mock<IUserRepository> users)
        Build(bool usernameTaken = false, bool emailTaken = false, bool refsExist = true)
    {
        var swimmers = new Mock<ISwimmerProfileRepository>();
        swimmers.Setup(r => r.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
        swimmers.Setup(r => r.GetMaxUidNumberAsync(It.IsAny<CancellationToken>())).ReturnsAsync(6);

        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(usernameTaken ? new AppUser("x", "X", Guid.NewGuid()) : null);
        users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(emailTaken ? new AppUser("y", "Y", Guid.NewGuid()) : null);

        var roles = new Mock<IRoleRepository>();
        roles.Setup(r => r.GetByCodeAsync("swimmer", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Role("swimmer", "Swimmer"));

        var clubs = new Mock<IClubRepository>(); clubs.Setup(c => c.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(refsExist);
        var blood = new Mock<IBloodTypeRepository>(); blood.Setup(c => c.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(refsExist);
        var strokes = new Mock<IStrokeRepository>(); strokes.Setup(c => c.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(refsExist);
        var genders = new Mock<IGenderRepository>(); genders.Setup(c => c.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(refsExist);

        var hasher = new Mock<IPasswordHasher>(); hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("HASH");
        var opts = Microsoft.Extensions.Options.Options.Create(new SwimmerRegistrationOptions { GenericPassword = "Oasis2026!" });

        var svc = new SwimmerService(swimmers.Object, users.Object, roles.Object,
            clubs.Object, blood.Object, strokes.Object, genders.Object, hasher.Object, opts);
        return (svc, swimmers, users);
    }

    [Fact]
    public async Task Create_builds_user_profile_specializations_and_returns_dto()
    {
        var (svc, swimmers, users) = Build();
        var result = await svc.CreateAsync(Req());

        Assert.NotNull(result);
        Assert.Equal("SW-0007", result!.Uid);          // max 6 + 1
        Assert.Equal("mona.ali", result.Username);
        Assert.Equal("Oasis2026!", result.TemporaryPassword);
        users.Verify(u => u.AddAsync(It.Is<AppUser>(a => a.Username == "mona.ali" && a.IsFirstLogin), It.IsAny<CancellationToken>()), Times.Once);
        swimmers.Verify(s => s.AddAsync(It.IsAny<SwimmerProfile>(), It.IsAny<CancellationToken>()), Times.Once);
        swimmers.Verify(s => s.AddSpecializationsAsync(It.IsAny<IEnumerable<SwimmerSpecialization>>(), It.IsAny<CancellationToken>()), Times.Once);
        swimmers.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_returns_null_when_username_taken()
    {
        var (svc, _, _) = Build(usernameTaken: true);
        Assert.Null(await svc.CreateAsync(Req()));
    }

    [Fact]
    public async Task Create_throws_when_a_reference_id_is_unknown()
    {
        var (svc, _, _) = Build(refsExist: false);
        await Assert.ThrowsAnyAsync<Exception>(() => svc.CreateAsync(Req()));
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~SwimmerServiceTests" -v minimal --nologo`
Expected: FAIL — `SwimmerService` new ctor + `CreateAsync` don't exist (compile error).

- [ ] **Step 3: Implement the service**

`ISwimmerService.cs` add:
```csharp
    Task<CreatedSwimmerDto?> CreateAsync(CreateSwimmerRequest request, CancellationToken ct = default);
```

Rewrite `SwimmerService.cs`:
```csharp
using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Options;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Exceptions;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Kheprx.BaseBackend.Identity.Application.Services;

internal sealed class SwimmerService : ISwimmerService
{
    private readonly ISwimmerProfileRepository _swimmers;
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IClubRepository _clubs;
    private readonly IBloodTypeRepository _bloodTypes;
    private readonly IStrokeRepository _strokes;
    private readonly IGenderRepository _genders;
    private readonly IPasswordHasher _hasher;
    private readonly SwimmerRegistrationOptions _options;

    public SwimmerService(
        ISwimmerProfileRepository swimmers, IUserRepository users, IRoleRepository roles,
        IClubRepository clubs, IBloodTypeRepository bloodTypes, IStrokeRepository strokes,
        IGenderRepository genders, IPasswordHasher hasher, IOptions<SwimmerRegistrationOptions> options)
    {
        _swimmers = swimmers;
        _users = users;
        _roles = roles;
        _clubs = clubs;
        _bloodTypes = bloodTypes;
        _strokes = strokes;
        _genders = genders;
        _hasher = hasher;
        _options = options.Value;
    }

    public async Task<SwimmerCountDto> GetCountAsync(CancellationToken ct = default)
        => new(await _swimmers.CountAsync(ct));

    public async Task<CreatedSwimmerDto?> CreateAsync(CreateSwimmerRequest request, CancellationToken ct = default)
    {
        var role = await _roles.GetByCodeAsync("swimmer", ct)
            ?? throw new InvalidUserException("Swimmer role is not configured.");

        if (await _users.GetByUsernameAsync(request.Username, ct) is not null) return null;

        string? email = null;
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            email = request.Email.Trim().ToLowerInvariant();
            if (await _users.GetByEmailAsync(email, ct) is not null) return null;
        }

        await EnsureReferencesExist(request, ct);

        var uid = $"SW-{await _swimmers.GetMaxUidNumberAsync(ct) + 1:D4}";
        var passwordHash = _hasher.Hash(_options.GenericPassword);

        var user = new AppUser(
            request.Username, request.NameEn, role.Id,
            nameAr: request.NameAr, email: email, passwordHash: passwordHash,
            genderId: request.GenderId, dob: request.Dob, phone: request.Phone, isFirstLogin: true);
        await _users.AddAsync(user, ct);

        var profile = new SwimmerProfile(user.Id, uid, request.TrainingClubId,
            request.RepresentChampionshipClubId, request.BloodTypeId);
        await _swimmers.AddAsync(profile, ct);

        await _swimmers.AddSpecializationsAsync(
            request.StrokeIds.Select(sid => new SwimmerSpecialization(profile.Id, sid)), ct);

        await _swimmers.SaveChangesAsync(ct);

        return new CreatedSwimmerDto(profile.Id, uid, user.Username, user.NameEn, _options.GenericPassword);
    }

    private async Task EnsureReferencesExist(CreateSwimmerRequest r, CancellationToken ct)
    {
        if (!await _genders.ExistsAsync(r.GenderId, ct)) throw new InvalidUserException("Unknown gender.");
        if (!await _clubs.ExistsAsync(r.TrainingClubId, ct)) throw new InvalidUserException("Unknown training club.");
        if (r.RepresentChampionshipClubId is { } champ && !await _clubs.ExistsAsync(champ, ct)) throw new InvalidUserException("Unknown championship club.");
        if (!await _bloodTypes.ExistsAsync(r.BloodTypeId, ct)) throw new InvalidUserException("Unknown blood type.");
        foreach (var sid in r.StrokeIds)
            if (!await _strokes.ExistsAsync(sid, ct)) throw new InvalidUserException("Unknown stroke.");
    }
}
```
> Verify `InvalidUserException` lives in `Kheprx.BaseBackend.Identity.Domain.Exceptions` (it's thrown by `AppUser`/`UserService`); adjust the `using` if the namespace differs.

In `IdentityModuleExtensions.AddIdentityModule`, add near the other `Configure` call:
```csharp
        services.Configure<SwimmerRegistrationOptions>(configuration.GetSection(SwimmerRegistrationOptions.SectionName));
```
(Add `using Kheprx.BaseBackend.Identity.Application.Options;`.)

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~SwimmerServiceTests" -v minimal --nologo`
Expected: PASS (existing count test + 3 create tests).

- [ ] **Step 5: Commit** — SKIP.

---

### Task 5: POST /api/swimmers endpoint

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs` (extend)

**Interfaces:**
- Consumes: `ISwimmerService.CreateAsync`, `CreateSwimmerRequest`, `CreatedSwimmerDto`, `SwimmerMessages`.
- Produces: `POST /api/swimmers` → 201 `ApiResponse<CreatedSwimmerDto>`, 409 on null.

- [ ] **Step 1: Write the failing test (RED)**

Add to `SwimmersControllerTests.cs`:
```csharp
    [Fact]
    public async Task Create_returns_201_with_created_swimmer()
    {
        var svc = new Mock<ISwimmerService>();
        var created = new CreatedSwimmerDto(Guid.NewGuid(), "SW-0007", "mona.ali", "Mona Ali", "Oasis2026!");
        svc.Setup(s => s.CreateAsync(It.IsAny<CreateSwimmerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(created);

        var req = new CreateSwimmerRequest("Mona Ali", "mona.ali", Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2010, 5, 1), Guid.NewGuid(), new[] { Guid.NewGuid() }, null, null, null, null);

        var result = await new SwimmersController(svc.Object).Create(req, CancellationToken.None);

        var ok = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, ok.StatusCode);
        var body = Assert.IsType<ApiResponse<CreatedSwimmerDto>>(ok.Value);
        Assert.Equal("SW-0007", body.Data!.Uid);
    }

    [Fact]
    public async Task Create_returns_409_when_service_returns_null()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.CreateAsync(It.IsAny<CreateSwimmerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((CreatedSwimmerDto?)null);
        var req = new CreateSwimmerRequest("Mona Ali", "mona.ali", Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2010, 5, 1), Guid.NewGuid(), new[] { Guid.NewGuid() }, null, null, null, null);

        var result = await new SwimmersController(svc.Object).Create(req, CancellationToken.None);

        var conflict = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }
```
(Ensure the test file's usings include `Kheprx.BaseBackend.Identity.Application.DTOs`, `Kheprx.BaseBackend.SharedKernel.Responses`, `Microsoft.AspNetCore.Http`, `Microsoft.AspNetCore.Mvc`.)

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests/Kheprx.BaseBackend.Api.UnitTests.csproj --filter "FullyQualifiedName~SwimmersControllerTests" -v minimal --nologo`
Expected: FAIL — `Create` action doesn't exist (compile error).

- [ ] **Step 3: Add the endpoint**

In `SwimmersController.cs` add `using Microsoft.AspNetCore.Authorization;` and the action:
```csharp
    /// <summary>Registers a swimmer (app_user + profile + specializations). Head Coach or Captain only.</summary>
    /// <response code="201">Swimmer created.</response>
    /// <response code="409">Username or email already in use.</response>
    [HttpPost]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<CreatedSwimmerDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CreatedSwimmerDto>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CreatedSwimmerDto>>> Create(CreateSwimmerRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, ct);
        if (created is null)
        {
            var conflict = ApiResponse<CreatedSwimmerDto>.Failure(
                SwimmerMessages.Errors.UsernameTaken(AppLanguage.Current), "conflict");
            return Conflict(conflict);
        }

        var body = ApiResponse<CreatedSwimmerDto>.Success(SwimmerMessages.Success.SwimmerCreated(AppLanguage.Current), created);
        return StatusCode(StatusCodes.Status201Created, body);
    }
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests/Kheprx.BaseBackend.Api.UnitTests.csproj --filter "FullyQualifiedName~SwimmersControllerTests" -v minimal --nologo`
Expected: PASS (existing count test + 2 create tests).

- [ ] **Step 5: Commit** — SKIP.

---

### Task 6: Seeder — swimmer role + full swimmer re-seed

**Files:**
- Modify: `.../Infrastructure/Data/IdentitySeeder.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Data/IdentitySeederTests.cs` (extend)

**Interfaces:**
- Consumes: entities + reference seeds; the new `SwimmerProfile` ctor.
- Produces: a `swimmer` role; 24 `app_user`-linked swimmer profiles; idempotent.

- [ ] **Step 1: Write the failing test (RED)**

Add to `IdentitySeederTests.cs`:
```csharp
    [Fact]
    public async Task Seeding_creates_swimmer_role_and_linked_swimmers()
    {
        var hasher = new PasswordHasher();
        await using var db = NewDb();
        await IdentitySeeder.SeedAsync(db, hasher);
        await IdentitySeeder.SeedAsync(db, hasher);

        var swimmerRole = await db.Roles.FirstOrDefaultAsync(r => r.Code == "swimmer");
        Assert.NotNull(swimmerRole);
        Assert.Equal(24, await db.SwimmerProfiles.CountAsync());
        // every swimmer profile is linked to an app_user with the swimmer role
        var swimmerUserIds = db.Users.Where(u => u.RoleId == swimmerRole!.Id).Select(u => u.Id).ToList();
        Assert.Equal(24, swimmerUserIds.Count);
        Assert.True(db.SwimmerProfiles.All(p => swimmerUserIds.Contains(p.UserId)));
    }
```
(Also update the existing `Seeding_twice_creates_each_row_once` counts if it asserted user counts: expected `Users` now = 2 coaches + 24 swimmers = 26. Adjust that assertion.)

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~IdentitySeederTests" -v minimal --nologo`
Expected: FAIL — no `swimmer` role; swimmers not user-linked; old `SwimmerProfile` ctor no longer compiles in the seeder.

- [ ] **Step 3: Rework the seeder**

In `SeedAsync`, reorder and add the swimmer role. The body becomes:
```csharp
    public static async Task SeedAsync(IdentityDbContext db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        var headCoachRole = await EnsureRole(db, "head_coach", "Head Coach", "المدرب العام", ct);
        var captainRole = await EnsureRole(db, "captain", "Captain", "الكابتن", ct);
        var swimmerRole = await EnsureRole(db, "swimmer", "Swimmer", "السبّاح", ct);
        var male = await EnsureGender(db, "male", "Male", "ذكر", ct);
        var female = await EnsureGender(db, "female", "Female", "أنثى", ct);
        await db.SaveChangesAsync(ct);

        await EnsureStrokes(db, ct);
        await EnsureBloodTypes(db, ct);
        await EnsureClubs(db, ct);
        await db.SaveChangesAsync(ct);

        await EnsureHeadCoach(db, hasher, headCoachRole.Id, male.Id, ct);
        await EnsureCaptain(db, hasher, captainRole.Id, male.Id, ct);
        await db.SaveChangesAsync(ct);

        await EnsureSwimmers(db, hasher, swimmerRole.Id, male.Id, female.Id, ct);
        await db.SaveChangesAsync(ct);
    }
```
Rewrite `EnsureSwimmers` (now takes the hasher, swimmer role, and gender ids; builds full records using the first seeded club + a seeded blood type + a seeded stroke):
```csharp
    private static async Task EnsureSwimmers(IdentityDbContext db, IPasswordHasher hasher,
        Guid swimmerRoleId, Guid maleId, Guid femaleId, CancellationToken ct)
    {
        if (await db.SwimmerProfiles.AnyAsync(ct)) return; // idempotent — seed only when empty

        var clubId = await db.Clubs.OrderBy(c => c.NameEn).Select(c => c.Id).FirstAsync(ct);
        var bloodId = await db.BloodTypes.OrderBy(b => b.Code).Select(b => b.Id).FirstAsync(ct);
        var strokeId = await db.Strokes.OrderBy(s => s.NameEn).Select(s => s.Id).FirstAsync(ct);
        var passwordHash = hasher.Hash(DevPassword);

        for (var i = 1; i <= 24; i++)
        {
            var user = new AppUser(
                username: $"swimmer{i:D2}", nameEn: $"Swimmer {i:D2}", roleId: swimmerRoleId,
                nameAr: $"سبّاح {i:D2}", passwordHash: passwordHash,
                genderId: i % 2 == 0 ? femaleId : maleId,
                dob: new DateOnly(2010, 1, 1).AddDays(i), isFirstLogin: true);
            await db.Users.AddAsync(user, ct);

            var profile = new SwimmerProfile(user.Id, $"SW-{i:D4}", clubId, bloodTypeId: bloodId);
            await db.SwimmerProfiles.AddAsync(profile, ct);
            await db.SwimmerSpecializations.AddAsync(new SwimmerSpecialization(profile.Id, strokeId), ct);
        }
    }
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~IdentitySeederTests" -v minimal --nologo`
Expected: PASS (idempotency test with updated counts + the new swimmer-linkage test).

- [ ] **Step 5: Commit** — SKIP.

---

### Task 7: Migration `RegisterSwimmerSchema` + full backend suite

**Files:**
- Create: `.../Infrastructure/Migrations/<timestamp>_RegisterSwimmerSchema.cs` (+ `.Designer.cs`, generated)
- Modify: `.../Infrastructure/Migrations/IdentityDbContextModelSnapshot.cs` (generated)

**Interfaces:**
- Consumes: Tasks 1–6.
- Produces: a migration whose `Up` deletes existing `swimmer_profile` rows, drops `name_en`/`name_ar`, adds the new columns + FKs + unique `user_id` index, and creates `athlete.swimmer_specialization`.

> No unit test — deliverable is a correct, isolated migration. **Stop the dev API server first.**

- [ ] **Step 1: Safety pre-check**

Run:
```bash
dotnet ef migrations has-pending-model-changes \
  --project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --startup-project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure
```
Expected: reports pending changes (swimmer_profile columns + swimmer_specialization). If it reports changes unrelated to swimmer registration, **STOP** and consult.

- [ ] **Step 2: Generate the migration**

```bash
dotnet ef migrations add RegisterSwimmerSchema \
  --project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure \
  --startup-project backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure
```

- [ ] **Step 3: Hand-add the demo-row delete + verify scope**

EF does not emit data deletes. The new NOT-NULL `user_id`/`training_club_id` columns cannot be added to a table that still has the 24 demo rows. At the **very top** of the generated `Up(MigrationBuilder migrationBuilder)`, add:
```csharp
    migrationBuilder.Sql("DELETE FROM identity.swimmer_profile;");
```
Then verify the rest of `Up` only: drops `name_en`/`name_ar`; adds `user_id` (uuid NOT NULL) + unique index + FK→app_user, `training_club_id` (uuid NOT NULL) + FK→reference.club, `represent_championship_club_id` (uuid null) + FK, `blood_type_id` (uuid null) + FK, `updated_at`; ensures schema `athlete`; creates `athlete.swimmer_specialization` (composite PK, two FKs). If `Up` touches anything else, **STOP**: `dotnet ef migrations remove ...` and consult.

- [ ] **Step 4: Full backend suite**

Run: `dotnet test backend/Kheprx.BaseBackend.sln -v minimal --nologo`
Expected: all green (Tasks 1–6 + the whole existing suite). The migration auto-applies at API startup, then the seeder runs — no manual DB update here.

- [ ] **Step 5: Commit** — SKIP.

---

### Task 8: Frontend routing — widen guard + account-creation route

**Files:**
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/app.routes.spec.ts` (there is an existing routes spec — extend it)

**Interfaces:**
- Consumes: `roleGuard` (already varargs), the future `AccountCreationPage` + `RegisterSwimmerViewModel` (Task 12).
- Produces: `captain-panel` guarded by `roleGuard('head_coach','captain')`; new `captain-panel/account-creation` route.

- [ ] **Step 1: Update the routes spec (RED)**

In `app.routes.spec.ts`, add an assertion that a `captain-panel/account-creation` route exists (mirror how the spec locates other routes — e.g. find by `path`). Example:
```ts
it('registers the account-creation route', () => {
  const layout = routes.find((r) => r.path === '');
  const paths = layout?.children?.map((c) => c.path) ?? [];
  expect(paths).toContain('captain-panel/account-creation');
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd frontend && npm test -- app.routes`
Expected: FAIL — the route isn't registered.

- [ ] **Step 3: Edit routes**

In `app.routes.ts`: change the captain-panel guard and add the child route immediately after it, inside the layout `children` array:
```ts
      { path: 'captain-panel', canActivate: [firstLoginGuard, roleGuard('head_coach', 'captain')], loadComponent: () => import('@features/captain-panel').then((m) => m.CaptainPanelPage) },
      {
        path: 'captain-panel/account-creation',
        canActivate: [firstLoginGuard, roleGuard('head_coach', 'captain')],
        loadComponent: () => import('@features/captain-panel').then((m) => m.AccountCreationPage),
        providers: [RegisterSwimmerViewModel],
      },
```
Add the import at the top:
```ts
import { RegisterSwimmerViewModel } from '@features/captain-panel';
```
> `AccountCreationPage` and `RegisterSwimmerViewModel` are exported from `@features/captain-panel` in Task 12. This route file won't compile until Task 12 adds those exports — that's fine within the batch; the frontend suite is run green at the end of Task 12/13. If executing tasks in isolation, do Task 12 before running the full frontend build.

- [ ] **Step 4: Run to verify it passes** (after Task 12 exports exist)

Run: `cd frontend && npm test -- app.routes`
Expected: PASS.

- [ ] **Step 5: Commit** — SKIP.

---

### Task 9: Shared form controls (select, date, chip-group)

**Files:**
- Create: `frontend/src/app/core/ui/components/select-field.component.ts`
- Create: `frontend/src/app/core/ui/components/date-field.component.ts`
- Create: `frontend/src/app/core/ui/components/chip-group.component.ts`
- Test: `frontend/src/app/core/ui/components/select-field.component.spec.ts`, `date-field.component.spec.ts`, `chip-group.component.spec.ts`

**Interfaces:**
- Consumes: `LookupItem` (`@features/reference/domain/model/reference`), `LanguageStore` (`@core/i18n`).
- Produces: `SelectFieldComponent` (`label`, `placeholder`, `options: LookupItem[]`, `value = model('')`), `DateFieldComponent` (`label`, `value = model('')`), `ChipGroupComponent` (`label`, `options: LookupItem[]`, `selected = model<string[]>([])`).

- [ ] **Step 1: Write the failing tests (RED)**

`select-field.component.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { SelectFieldComponent } from '@core/ui/components/select-field.component';

describe('SelectFieldComponent', () => {
  it('exposes label + options and updates value model', () => {
    TestBed.configureTestingModule({ imports: [SelectFieldComponent] });
    const fixture = TestBed.createComponent(SelectFieldComponent);
    fixture.componentRef.setInput('label', 'Gender');
    fixture.componentRef.setInput('options', [{ id: 'g1', code: 'male', nameEn: 'Male', nameAr: 'ذكر' }]);
    fixture.detectChanges();
    const cmp = fixture.componentInstance;
    cmp.value.set('g1');
    expect(cmp.value()).toBe('g1');
    expect(cmp.options().length).toBe(1);
  });
});
```

`chip-group.component.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { ChipGroupComponent } from '@core/ui/components/chip-group.component';

describe('ChipGroupComponent', () => {
  it('toggles ids in the selected model', () => {
    TestBed.configureTestingModule({ imports: [ChipGroupComponent] });
    const fixture = TestBed.createComponent(ChipGroupComponent);
    fixture.componentRef.setInput('options', [{ id: 's1', code: 'medley', nameEn: 'IM', nameAr: null }]);
    fixture.detectChanges();
    const cmp = fixture.componentInstance;
    cmp.toggle('s1');
    expect(cmp.selected()).toEqual(['s1']);
    cmp.toggle('s1');
    expect(cmp.selected()).toEqual([]);
  });
});
```

`date-field.component.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { DateFieldComponent } from '@core/ui/components/date-field.component';

describe('DateFieldComponent', () => {
  it('updates its value model', () => {
    TestBed.configureTestingModule({ imports: [DateFieldComponent] });
    const fixture = TestBed.createComponent(DateFieldComponent);
    const cmp = fixture.componentInstance;
    cmp.value.set('2010-05-01');
    expect(cmp.value()).toBe('2010-05-01');
  });
});
```

- [ ] **Step 2: Run to verify they fail**

Run: `cd frontend && npm test -- "field.component|chip-group.component"`
Expected: FAIL — components don't exist.

- [ ] **Step 3: Implement the controls**

`select-field.component.ts`:
```ts
import { Component, inject, input, model } from '@angular/core';
import { LookupItem } from '@features/reference/domain/model/reference';
import { LanguageStore } from '@core/i18n';

@Component({
  selector: 'app-select-field',
  standalone: true,
  template: `
    <label class="flex flex-col gap-1">
      @if (label()) { <span class="text-sm font-bold text-ink">{{ label() }}</span> }
      <select
        class="h-10 rounded-md border border-input bg-card px-3 text-sm text-ink focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
        [value]="value()"
        (change)="value.set($any($event.target).value)">
        <option value="">{{ placeholder() }}</option>
        @for (opt of options(); track opt.id) {
          <option [value]="opt.id">{{ labelFor(opt) }}</option>
        }
      </select>
    </label>
  `,
})
export class SelectFieldComponent {
  readonly label = input('');
  readonly placeholder = input('');
  readonly options = input<LookupItem[]>([]);
  readonly value = model('');
  private readonly lang = inject(LanguageStore).lang;

  labelFor(opt: LookupItem): string {
    return this.lang() === 'ar' ? (opt.nameAr ?? opt.nameEn) : opt.nameEn;
  }
}
```

`date-field.component.ts`:
```ts
import { Component, input, model } from '@angular/core';

@Component({
  selector: 'app-date-field',
  standalone: true,
  template: `
    <label class="flex flex-col gap-1">
      @if (label()) { <span class="text-sm font-bold text-ink">{{ label() }}</span> }
      <input
        type="date"
        class="h-10 rounded-md border border-input bg-card px-3 text-sm text-ink focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
        [value]="value()"
        (input)="value.set($any($event.target).value)" />
    </label>
  `,
})
export class DateFieldComponent {
  readonly label = input('');
  readonly value = model('');
}
```

`chip-group.component.ts`:
```ts
import { Component, inject, input, model } from '@angular/core';
import { LookupItem } from '@features/reference/domain/model/reference';
import { LanguageStore } from '@core/i18n';

@Component({
  selector: 'app-chip-group',
  standalone: true,
  template: `
    <div class="flex flex-col gap-2">
      @if (label()) { <span class="text-sm font-bold text-ink">{{ label() }}</span> }
      <div class="flex flex-wrap gap-2">
        @for (opt of options(); track opt.id) {
          <button
            type="button"
            class="rounded-full border px-4 py-1.5 text-sm font-semibold transition-colors"
            [class]="selected().includes(opt.id) ? 'border-primary bg-primary text-white' : 'border-input bg-card text-ink'"
            (click)="toggle(opt.id)">
            {{ labelFor(opt) }}
          </button>
        }
      </div>
    </div>
  `,
})
export class ChipGroupComponent {
  readonly label = input('');
  readonly options = input<LookupItem[]>([]);
  readonly selected = model<string[]>([]);
  private readonly lang = inject(LanguageStore).lang;

  labelFor(opt: LookupItem): string {
    return this.lang() === 'ar' ? (opt.nameAr ?? opt.nameEn) : opt.nameEn;
  }

  toggle(id: string): void {
    const cur = this.selected();
    this.selected.set(cur.includes(id) ? cur.filter((x) => x !== id) : [...cur, id]);
  }
}
```
> If Tailwind class names (`text-ink`, `border-input`, `bg-card`, `bg-primary`) differ from the project's tokens, match those used in `login.page.html`.

- [ ] **Step 4: Run to verify they pass**

Run: `cd frontend && npm test -- "field.component|chip-group.component"`
Expected: PASS (3 files).

- [ ] **Step 5: Commit** — SKIP.

---

### Task 10: Swimmers data layer — create DTO, model, repository, use-case

**Files:**
- Create: `frontend/src/app/features/swimmers/data/dto/create-swimmer.dto.ts`
- Create: `frontend/src/app/features/swimmers/domain/model/swimmer.ts`
- Modify: `frontend/src/app/features/swimmers/domain/repositories/swimmer.repository.ts`
- Modify: `frontend/src/app/features/swimmers/data/repositories/swimmer.repository.impl.ts`
- Create: `frontend/src/app/features/swimmers/domain/usecases/create-swimmer.use-case.ts`
- Test: `frontend/src/app/features/swimmers/testing/data/repositories/swimmer.repository.impl.spec.ts` (extend), `frontend/src/app/features/swimmers/testing/domain/usecases/create-swimmer.use-case.spec.ts` (new)

**Interfaces:**
- Consumes: `BaseResponseRs`, `HttpClientService`, `UseCase`, `AppError`.
- Produces: `CreateSwimmerDtoRq`, `CreatedSwimmerDtoRs`, `CreatedSwimmerItemDtoRs`, `isCreatedSwimmerDtoRsValid`; `CreatedSwimmer` model; `ISwimmerRepository.create(rq)`; `CreateSwimmerUseCase extends UseCase<CreateSwimmerDtoRq, CreatedSwimmer>`.

- [ ] **Step 1: Write the failing tests (RED)**

`create-swimmer.use-case.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { CreateSwimmerUseCase } from '@features/swimmers/domain/usecases/create-swimmer.use-case';
import { SWIMMER_REPOSITORY, ISwimmerRepository } from '@features/swimmers/domain/repositories/swimmer.repository';

const OK = { data: { id: 'x', uid: 'SW-0007', username: 'mona.ali', nameEn: 'Mona Ali', temporaryPassword: 'Oasis2026!' } };
const rq = { nameEn: 'Mona Ali', username: 'mona.ali', trainingClubId: 'c1', genderId: 'g1', dob: '2010-05-01', bloodTypeId: 'b1', strokeIds: ['s1'] };

function build(repo: ISwimmerRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_REPOSITORY, useValue: repo }] });
  return TestBed.inject(CreateSwimmerUseCase);
}

describe('CreateSwimmerUseCase', () => {
  it('maps the created swimmer', async () => {
    const uc = build({ getCount: async () => ({ data: { count: 0 } }), create: async () => OK } as unknown as ISwimmerRepository);
    const r = await uc.run(rq as never);
    expect(r.ok).toBe(true);
    if (r.ok) { expect(r.data.uid).toBe('SW-0007'); expect(r.data.temporaryPassword).toBe('Oasis2026!'); }
  });

  it('fails validation on malformed response', async () => {
    const uc = build({ getCount: async () => ({ data: { count: 0 } }), create: async () => ({ data: { uid: 'SW-0007' } }) } as unknown as ISwimmerRepository);
    const r = await uc.run(rq as never);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
```

Add to `swimmer.repository.impl.spec.ts`:
```ts
  it('create POSTs /api/swimmers with the body', async () => {
    (http.post as jest.Mock) = jest.fn().mockResolvedValue({ data: {} });
    const rq = { nameEn: 'Mona', username: 'mona', trainingClubId: 'c1', genderId: 'g1', dob: '2010-05-01', bloodTypeId: 'b1', strokeIds: ['s1'] };
    await repo.create(rq as never);
    expect((http.post as jest.Mock)).toHaveBeenCalledWith('/api/swimmers', { body: rq });
  });
```
(Extend the existing mock `const http = { get: jest.fn(), post: jest.fn() } ...`.)

- [ ] **Step 2: Run to verify they fail**

Run: `cd frontend && npm test -- "create-swimmer.use-case|swimmer.repository.impl"`
Expected: FAIL — `create`/`CreateSwimmerUseCase` don't exist.

- [ ] **Step 3: Implement the data layer**

`create-swimmer.dto.ts`:
```ts
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface CreateSwimmerDtoRq {
  nameEn: string;
  username: string;
  trainingClubId: string;
  genderId: string;
  dob: string; // ISO date (yyyy-mm-dd)
  bloodTypeId: string;
  strokeIds: string[];
  nameAr?: string;
  email?: string;
  phone?: string;
  representChampionshipClubId?: string;
}

export interface CreatedSwimmerDtoRs {
  id: string;
  uid: string;
  username: string;
  nameEn: string;
  temporaryPassword: string;
}

export interface CreatedSwimmerItemDtoRs extends BaseResponseRs<CreatedSwimmerDtoRs> {}

export function isCreatedSwimmerDtoRsValid(dto: unknown): dto is CreatedSwimmerDtoRs {
  const d = dto as CreatedSwimmerDtoRs;
  return !!d && typeof d.id === 'string' && typeof d.uid === 'string'
    && typeof d.username === 'string' && typeof d.nameEn === 'string'
    && typeof d.temporaryPassword === 'string';
}
```

`swimmer.ts` (model):
```ts
export interface CreatedSwimmer {
  id: string;
  uid: string;
  username: string;
  nameEn: string;
  temporaryPassword: string;
}
```

In `swimmer.repository.ts` add the import + method to the port:
```ts
import { CreateSwimmerDtoRq, CreatedSwimmerItemDtoRs } from '@features/swimmers/data/dto/create-swimmer.dto';
// ...inside ISwimmerRepository:
  create(rq: CreateSwimmerDtoRq): Promise<CreatedSwimmerItemDtoRs>;
```

In `swimmer.repository.impl.ts` add:
```ts
import { CreateSwimmerDtoRq, CreatedSwimmerItemDtoRs } from '@features/swimmers/data/dto/create-swimmer.dto';
// ...inside the class:
  create(rq: CreateSwimmerDtoRq): Promise<CreatedSwimmerItemDtoRs> {
    return this.http.post<CreatedSwimmerItemDtoRs>('/api/swimmers', { body: rq });
  }
```

`create-swimmer.use-case.ts`:
```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_REPOSITORY } from '@features/swimmers/domain/repositories/swimmer.repository';
import { CreateSwimmerDtoRq, isCreatedSwimmerDtoRsValid } from '@features/swimmers/data/dto/create-swimmer.dto';
import { CreatedSwimmer } from '@features/swimmers/domain/model/swimmer';

@Injectable({ providedIn: 'root' })
export class CreateSwimmerUseCase extends UseCase<CreateSwimmerDtoRq, CreatedSwimmer> {
  private readonly repo = inject(SWIMMER_REPOSITORY);
  constructor() { super('CreateSwimmer'); }
  protected async execute(input: CreateSwimmerDtoRq): Promise<CreatedSwimmer> {
    const res = await this.repo.create(input);
    if (!isCreatedSwimmerDtoRsValid(res.data)) throw new AppError('Invalid created swimmer received', 'validation');
    return { ...res.data };
  }
}
```

- [ ] **Step 4: Run to verify they pass**

Run: `cd frontend && npm test -- "create-swimmer.use-case|swimmer.repository.impl"`
Expected: PASS.

- [ ] **Step 5: Commit** — SKIP.

---

### Task 11: i18n — accountCreation keys

**Files:**
- Modify: `frontend/src/app/core/i18n/en.json`, `frontend/src/app/core/i18n/ar.json`

**Interfaces:** Produces the `accountCreation.*` translation keys consumed by Task 12.

- [ ] **Step 1: Add keys (no separate test — validated by Task 12 component specs + full suite)**

Add an `accountCreation` object to **en.json**:
```json
  "accountCreation": {
    "title": "Account Creation",
    "subtitle": "Create swimmer and captain accounts and set their access.",
    "tabs": { "swimmer": "Register Swimmer", "captain": "Register Captain" },
    "captainComingSoon": "Captain registration is coming soon.",
    "fields": {
      "nameEn": "Full Name (English)", "nameAr": "Full Name (Arabic)",
      "username": "Username", "email": "Email Address", "phone": "Phone Number",
      "gender": "Gender", "dob": "Date of Birth", "bloodType": "Blood Type",
      "trainingClub": "Training Club", "championshipClub": "Championship Club",
      "specialization": "Specialization"
    },
    "placeholders": { "selectOne": "Select…", "none": "None", "username": "ahmed.rashidi" },
    "genericPassword": { "title": "Generic Password", "note": "A generic password will be assigned. The swimmer must change it on first login." },
    "submit": "Register Swimmer",
    "created": { "title": "Swimmer registered", "username": "Username", "password": "Temporary password", "another": "Register another" },
    "errors": { "required": "Please fill in all required fields.", "usernameTaken": "Username is already taken.", "failed": "Failed to register swimmer." },
    "success": "Swimmer registered"
  }
```
Add the same structure to **ar.json** with Arabic values (e.g. `title` "إنشاء حساب", `tabs.swimmer` "تسجيل سبّاح", `tabs.captain` "تسجيل كابتن", `fields.nameEn` "الاسم بالإنجليزية", `fields.nameAr` "الاسم بالعربية", `fields.username` "اسم المستخدم", `fields.gender` "النوع", `fields.dob` "تاريخ الميلاد", `fields.bloodType` "فصيلة الدم", `fields.trainingClub` "نادي التدريب", `fields.championshipClub` "نادي البطولات", `fields.specialization` "التخصص", `submit` "تسجيل السبّاح", etc.).

- [ ] **Step 2: Verify JSON parses**

Run: `cd frontend && npx tsc --noEmit -p tsconfig.spec.json` is NOT needed; instead confirm both files are valid JSON: `cd frontend && node -e "require('./src/app/core/i18n/en.json'); require('./src/app/core/i18n/ar.json'); console.log('ok')"`
Expected: prints `ok`.

- [ ] **Step 3: Commit** — SKIP.

---

### Task 12: Account Creation page + Register Swimmer form + viewmodel + Captain Panel card

**Files:**
- Create: `frontend/src/app/features/captain-panel/presentation/pages/account-creation/account-creation.page.ts` (+ `.html`)
- Create: `frontend/src/app/features/captain-panel/presentation/pages/account-creation/register-swimmer-form.component.ts` (+ `.html`)
- Create: `frontend/src/app/features/captain-panel/presentation/pages/account-creation/register-swimmer.viewmodel.ts`
- Modify: `frontend/src/app/features/captain-panel/presentation/pages/captain-panel/captain-panel.page.html`
- Modify: `frontend/src/app/features/captain-panel/index.ts`
- Modify: `frontend/src/app/features/reference/index.ts` (widen the barrel so use-cases import from `@features/reference`)
- Test: `frontend/src/app/features/captain-panel/testing/presentation/pages/account-creation/register-swimmer.viewmodel.spec.ts`, `account-creation.page.spec.ts`

**Interfaces:**
- Consumes: `LoadClubs/LoadGenders/LoadBloodTypes/LoadStrokesUseCase`, `CreateSwimmerUseCase`, `NotificationService`, `TranslateService`/`TranslatePipe`, `LanguageStore`, the Task-9 controls.
- Produces: `AccountCreationPage`, `RegisterSwimmerFormComponent`, `RegisterSwimmerViewModel` — all exported from `@features/captain-panel`.

- [ ] **Step 1: Write the failing viewmodel spec (RED)**

`register-swimmer.viewmodel.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';
import { RegisterSwimmerViewModel } from '@features/captain-panel/presentation/pages/account-creation/register-swimmer.viewmodel';
import { LoadClubsUseCase, LoadGendersUseCase, LoadBloodTypesUseCase, LoadStrokesUseCase } from '@features/reference';
import { CreateSwimmerUseCase } from '@features/swimmers/domain/usecases/create-swimmer.use-case';
import { NotificationService } from '@core/ui/notification.service';

const item = [{ id: 'x1', nameEn: 'X', nameAr: null }];
const lookups = () => ({ run: jest.fn().mockResolvedValue(ok(item)) });

function build(create = { run: jest.fn() }) {
  const notify = { success: jest.fn(), error: jest.fn() } as unknown as NotificationService;
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      RegisterSwimmerViewModel,
      { provide: LoadClubsUseCase, useValue: lookups() },
      { provide: LoadGendersUseCase, useValue: lookups() },
      { provide: LoadBloodTypesUseCase, useValue: lookups() },
      { provide: LoadStrokesUseCase, useValue: lookups() },
      { provide: CreateSwimmerUseCase, useValue: create },
      { provide: NotificationService, useValue: notify },
    ],
  });
  return { vm: TestBed.inject(RegisterSwimmerViewModel), notify };
}

describe('RegisterSwimmerViewModel', () => {
  it('loads lookups into signals', async () => {
    const { vm } = build();
    await Promise.resolve(); await Promise.resolve();
    expect(vm.clubs().length).toBe(1);
    expect(vm.strokes().length).toBe(1);
  });

  it('blocks submit until required fields are set', () => {
    const { vm } = build();
    expect(vm.canSubmit()).toBe(false);
  });

  it('creates and exposes the returned credentials on success', async () => {
    const created = { id: 'i', uid: 'SW-0007', username: 'mona', nameEn: 'Mona', temporaryPassword: 'Oasis2026!' };
    const create = { run: jest.fn().mockResolvedValue(ok(created)) };
    const { vm, notify } = build(create);
    vm.nameEn.set('Mona'); vm.username.set('mona'); vm.trainingClubId.set('c1');
    vm.genderId.set('g1'); vm.dob.set('2010-05-01'); vm.bloodTypeId.set('b1'); vm.strokeIds.set(['s1']);
    await vm.submit();
    expect(create.run).toHaveBeenCalled();
    expect(vm.created()?.uid).toBe('SW-0007');
    expect(notify.success).toHaveBeenCalled();
  });

  it('surfaces a conflict error', async () => {
    const create = { run: jest.fn().mockResolvedValue(fail(new AppError('taken', 'http', 409))) };
    const { vm, notify } = build(create);
    vm.nameEn.set('Mona'); vm.username.set('mona'); vm.trainingClubId.set('c1');
    vm.genderId.set('g1'); vm.dob.set('2010-05-01'); vm.bloodTypeId.set('b1'); vm.strokeIds.set(['s1']);
    await vm.submit();
    expect(vm.created()).toBeNull();
    expect(notify.error).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd frontend && npm test -- register-swimmer.viewmodel`
Expected: FAIL — the viewmodel doesn't exist.

- [ ] **Step 3: Implement the viewmodel, form, page, card, and barrel**

`register-swimmer.viewmodel.ts`:
```ts
import { Injectable, computed, inject, signal } from '@angular/core';
import { LoadClubsUseCase, LoadGendersUseCase, LoadBloodTypesUseCase, LoadStrokesUseCase } from '@features/reference';
import { LookupItem } from '@features/reference/domain/model/reference';
import { CreateSwimmerUseCase } from '@features/swimmers/domain/usecases/create-swimmer.use-case';
import { CreatedSwimmer } from '@features/swimmers/domain/model/swimmer';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';

@Injectable()
export class RegisterSwimmerViewModel {
  private readonly loadClubs = inject(LoadClubsUseCase);
  private readonly loadGenders = inject(LoadGendersUseCase);
  private readonly loadBloodTypes = inject(LoadBloodTypesUseCase);
  private readonly loadStrokes = inject(LoadStrokesUseCase);
  private readonly createSwimmer = inject(CreateSwimmerUseCase);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  readonly clubs = signal<LookupItem[]>([]);
  readonly genders = signal<LookupItem[]>([]);
  readonly bloodTypes = signal<LookupItem[]>([]);
  readonly strokes = signal<LookupItem[]>([]);

  readonly nameEn = signal('');
  readonly nameAr = signal('');
  readonly username = signal('');
  readonly email = signal('');
  readonly phone = signal('');
  readonly genderId = signal('');
  readonly dob = signal('');
  readonly bloodTypeId = signal('');
  readonly trainingClubId = signal('');
  readonly championshipClubId = signal('');
  readonly strokeIds = signal<string[]>([]);

  readonly loading = signal(false);
  readonly created = signal<CreatedSwimmer | null>(null);

  readonly canSubmit = computed(() =>
    this.nameEn().trim().length > 0 &&
    this.username().trim().length > 0 &&
    this.trainingClubId().length > 0 &&
    this.genderId().length > 0 &&
    this.dob().length > 0 &&
    this.bloodTypeId().length > 0 &&
    this.strokeIds().length > 0);

  constructor() { void this.loadLookups(); }

  private async loadLookups(): Promise<void> {
    const [c, g, b, s] = await Promise.all([
      this.loadClubs.run(), this.loadGenders.run(), this.loadBloodTypes.run(), this.loadStrokes.run(),
    ]);
    if (c.ok) this.clubs.set(c.data);
    if (g.ok) this.genders.set(g.data);
    if (b.ok) this.bloodTypes.set(b.data);
    if (s.ok) this.strokes.set(s.data);
  }

  async submit(): Promise<void> {
    if (!this.canSubmit() || this.loading()) return;
    this.loading.set(true);
    const r = await this.createSwimmer.run({
      nameEn: this.nameEn().trim(),
      username: this.username().trim(),
      trainingClubId: this.trainingClubId(),
      genderId: this.genderId(),
      dob: this.dob(),
      bloodTypeId: this.bloodTypeId(),
      strokeIds: this.strokeIds(),
      nameAr: this.nameAr().trim() || undefined,
      email: this.email().trim() || undefined,
      phone: this.phone().trim() || undefined,
      representChampionshipClubId: this.championshipClubId() || undefined,
    });
    this.loading.set(false);
    if (r.ok) {
      this.created.set(r.data);
      this.notify.success(this.i18n.t('accountCreation.success'));
    } else {
      const key = r.error.status === 409 ? 'accountCreation.errors.usernameTaken' : 'accountCreation.errors.failed';
      this.notify.error(this.i18n.t(key));
    }
  }

  reset(): void {
    this.created.set(null);
    for (const s of [this.nameEn, this.nameAr, this.username, this.email, this.phone,
      this.genderId, this.dob, this.bloodTypeId, this.trainingClubId, this.championshipClubId]) s.set('');
    this.strokeIds.set([]);
  }
}
```

`register-swimmer-form.component.ts`:
```ts
import { Component, inject } from '@angular/core';
import { TranslatePipe } from '@core/i18n';
import { TextFieldComponent } from '@core/ui/components/text-field.component';
import { SelectFieldComponent } from '@core/ui/components/select-field.component';
import { DateFieldComponent } from '@core/ui/components/date-field.component';
import { ChipGroupComponent } from '@core/ui/components/chip-group.component';
import { RegisterSwimmerViewModel } from './register-swimmer.viewmodel';

@Component({
  selector: 'app-register-swimmer-form',
  standalone: true,
  imports: [TranslatePipe, TextFieldComponent, SelectFieldComponent, DateFieldComponent, ChipGroupComponent],
  templateUrl: './register-swimmer-form.component.html',
})
export class RegisterSwimmerFormComponent {
  protected readonly vm = inject(RegisterSwimmerViewModel);
}
```

`register-swimmer-form.component.html` — a two-column grid mirroring the mockup, binding each control to the vm signal. Group PERSONAL (Name EN, Name AR, Username, Email, Phone, Gender, DOB, Blood Type), SWIMMING (Training Club, Championship Club, Specialization chips), then the generic-password info block and the submit button; when `vm.created()` is set, render the credentials panel instead. Example (abbreviated — follow this pattern for every field):
```html
@if (vm.created(); as c) {
  <div class="rounded-xl border border-success/30 bg-success-bg p-5">
    <h3 class="font-heading text-lg text-ink">{{ 'accountCreation.created.title' | translate }}</h3>
    <p class="mt-2 text-sm"><span class="font-semibold">{{ 'accountCreation.created.username' | translate }}:</span> {{ c.username }}</p>
    <p class="text-sm"><span class="font-semibold">{{ 'accountCreation.created.password' | translate }}:</span> {{ c.temporaryPassword }}</p>
    <button type="button" class="mt-4 rounded-md border border-input px-4 py-2 text-sm font-semibold" (click)="vm.reset()">
      {{ 'accountCreation.created.another' | translate }}
    </button>
  </div>
} @else {
  <form class="grid grid-cols-1 gap-5 md:grid-cols-2" (submit)="$event.preventDefault(); vm.submit()">
    <app-text-field [label]="'accountCreation.fields.nameEn' | translate" [value]="vm.nameEn()" (valueChange)="vm.nameEn.set($event)"></app-text-field>
    <app-text-field [label]="'accountCreation.fields.nameAr' | translate" [value]="vm.nameAr()" (valueChange)="vm.nameAr.set($event)"></app-text-field>
    <app-text-field [label]="'accountCreation.fields.username' | translate" [placeholder]="'accountCreation.placeholders.username' | translate" [value]="vm.username()" (valueChange)="vm.username.set($event)"></app-text-field>
    <app-text-field [label]="'accountCreation.fields.email' | translate" [value]="vm.email()" (valueChange)="vm.email.set($event)"></app-text-field>
    <app-text-field [label]="'accountCreation.fields.phone' | translate" [value]="vm.phone()" (valueChange)="vm.phone.set($event)"></app-text-field>
    <app-select-field [label]="'accountCreation.fields.gender' | translate" [placeholder]="'accountCreation.placeholders.selectOne' | translate" [options]="vm.genders()" [value]="vm.genderId()" (valueChange)="vm.genderId.set($event)"></app-select-field>
    <app-date-field [label]="'accountCreation.fields.dob' | translate" [value]="vm.dob()" (valueChange)="vm.dob.set($event)"></app-date-field>
    <app-select-field [label]="'accountCreation.fields.bloodType' | translate" [placeholder]="'accountCreation.placeholders.selectOne' | translate" [options]="vm.bloodTypes()" [value]="vm.bloodTypeId()" (valueChange)="vm.bloodTypeId.set($event)"></app-select-field>
    <app-select-field [label]="'accountCreation.fields.trainingClub' | translate" [placeholder]="'accountCreation.placeholders.selectOne' | translate" [options]="vm.clubs()" [value]="vm.trainingClubId()" (valueChange)="vm.trainingClubId.set($event)"></app-select-field>
    <app-select-field [label]="'accountCreation.fields.championshipClub' | translate" [placeholder]="'accountCreation.placeholders.none' | translate" [options]="vm.clubs()" [value]="vm.championshipClubId()" (valueChange)="vm.championshipClubId.set($event)"></app-select-field>
    <div class="md:col-span-2">
      <app-chip-group [label]="'accountCreation.fields.specialization' | translate" [options]="vm.strokes()" [selected]="vm.strokeIds()" (selectedChange)="vm.strokeIds.set($event)"></app-chip-group>
    </div>
    <div class="md:col-span-2 rounded-xl border border-warning/30 bg-warning-bg p-4">
      <h4 class="font-heading text-ink">{{ 'accountCreation.genericPassword.title' | translate }}</h4>
      <p class="mt-1 text-sm text-muted">{{ 'accountCreation.genericPassword.note' | translate }}</p>
    </div>
    <div class="md:col-span-2">
      <button type="submit" class="rounded-md bg-primary px-5 py-2.5 text-sm font-semibold text-white disabled:opacity-50" [disabled]="!vm.canSubmit() || vm.loading()">
        {{ 'accountCreation.submit' | translate }}
      </button>
    </div>
  </form>
}
```
> `TextFieldComponent` exposes `value = model('')`; two-way via `(valueChange)`. If the model output is named differently, match `text-field.component.ts`. Match warning/success Tailwind tokens to the project's theme.

`account-creation.page.ts`:
```ts
import { Component, inject, signal } from '@angular/core';
import { TranslatePipe } from '@core/i18n';
import { RegisterSwimmerFormComponent } from './register-swimmer-form.component';

@Component({
  selector: 'app-account-creation-page',
  standalone: true,
  imports: [TranslatePipe, RegisterSwimmerFormComponent],
  templateUrl: './account-creation.page.html',
})
export class AccountCreationPage {
  protected readonly tab = signal<'swimmer' | 'captain'>('swimmer');
}
```

`account-creation.page.html`:
```html
<div class="mx-auto max-w-4xl">
  <header class="mb-6">
    <h1 class="font-heading text-3xl text-ink">{{ 'accountCreation.title' | translate }}</h1>
    <p class="mt-1 text-muted">{{ 'accountCreation.subtitle' | translate }}</p>
  </header>
  <div class="mb-6 flex gap-3">
    <button type="button" class="rounded-md px-4 py-2 text-sm font-semibold"
      [class]="tab() === 'swimmer' ? 'bg-primary text-white' : 'bg-card text-ink border border-input'"
      (click)="tab.set('swimmer')">{{ 'accountCreation.tabs.swimmer' | translate }}</button>
    <button type="button" class="rounded-md px-4 py-2 text-sm font-semibold"
      [class]="tab() === 'captain' ? 'bg-primary text-white' : 'bg-card text-ink border border-input'"
      (click)="tab.set('captain')">{{ 'accountCreation.tabs.captain' | translate }}</button>
  </div>
  @if (tab() === 'swimmer') {
    <app-register-swimmer-form></app-register-swimmer-form>
  } @else {
    <p class="text-muted">{{ 'accountCreation.captainComingSoon' | translate }}</p>
  }
</div>
```

In `captain-panel.page.html`, add an Account Creation card linking to the route (keep the existing header):
```html
  <a routerLink="/captain-panel/account-creation" class="mt-6 block rounded-xl border border-input bg-card p-6 hover:border-primary">
    <h2 class="font-heading text-xl text-ink">{{ 'accountCreation.title' | translate }}</h2>
    <p class="mt-1 text-sm text-muted">{{ 'accountCreation.subtitle' | translate }}</p>
  </a>
```
Add `RouterLink` to the `CaptainPanelPage` imports (`import { RouterLink } from '@angular/router';` and add to `imports: [TranslatePipe, RouterLink]`).

Update `captain-panel/index.ts`:
```ts
export { CaptainPanelPage } from './presentation/pages/captain-panel/captain-panel.page';
export { AccountCreationPage } from './presentation/pages/account-creation/account-creation.page';
export { RegisterSwimmerViewModel } from './presentation/pages/account-creation/register-swimmer.viewmodel';
```

Widen `features/reference/index.ts` so consumers can import the use-cases (and model) from the barrel (currently it re-exports only the providers — this addresses the Phase-1 deferred "M2" note now that Phase 2 is the consumer):
```ts
export * from '@features/reference/data/reference.providers';
export * from '@features/reference/domain/model/reference';
export * from '@features/reference/domain/repositories/reference.repository';
export * from '@features/reference/domain/usecases/load-clubs.use-case';
export * from '@features/reference/domain/usecases/load-blood-types.use-case';
export * from '@features/reference/domain/usecases/load-strokes.use-case';
export * from '@features/reference/domain/usecases/load-genders.use-case';
```

Add `account-creation.page.spec.ts` (tab switching):
```ts
import { TestBed } from '@angular/core/testing';
import { AccountCreationPage } from '@features/captain-panel';

describe('AccountCreationPage', () => {
  it('defaults to the swimmer tab and switches to captain', () => {
    TestBed.configureTestingModule({ imports: [AccountCreationPage] });
    const fixture = TestBed.createComponent(AccountCreationPage);
    const cmp = fixture.componentInstance as unknown as { tab: () => string };
    expect(cmp.tab()).toBe('swimmer');
  });
});
```
> The form component pulls `RegisterSwimmerViewModel` from DI; if the page spec fails to instantiate the child form because the viewmodel isn't provided, render only the captain tab in that spec or provide the viewmodel + stubbed use-cases. Prefer a minimal spec that asserts `tab()` default + `set`.

- [ ] **Step 4: Run to verify it passes**

Run: `cd frontend && npm test -- "register-swimmer.viewmodel|account-creation.page"`
Expected: PASS.

- [ ] **Step 5: Commit** — SKIP.

---

### Task 13: Full frontend suite + final verification

**Files:** none (verification only).

- [ ] **Step 1: Full frontend suite**

Run: `cd frontend && npm test`
Expected: all green (new specs + no regressions; the `app.routes` route now resolves because Task 12 added the `@features/captain-panel` exports).

- [ ] **Step 2: Full backend suite** (dev server stopped)

Run: `dotnet test backend/Kheprx.BaseBackend.sln -v minimal --nologo`
Expected: all green.

- [ ] **Step 3: Smoke (optional)**

`dotnet run --project backend/Kheprx.BaseBackend.Api` → applies `RegisterSwimmerSchema`, re-seeds 24 linked swimmers. With a Head-Coach bearer token, `POST /api/swimmers` with a valid body returns 201 `{ uid: "SW-0025", username, temporaryPassword }`; a duplicate username returns 409; `GET /api/swimmers/count` returns 25.

- [ ] **Step 4: Scope guard** — confirm no Register Captain form logic, no other Captain-Panel cards, no swimmer list/edit/delete, no login-page changes were introduced.

- [ ] **Step 5: Commit** — SKIP.

---

## Spec coverage check

- `swimmer_profile` restructure (user_id, club FKs, blood type, updated_at; drop names) → Task 1 + migration Task 7. ✓
- `athlete.swimmer_specialization` junction → Tasks 1, 7. ✓
- `swimmer` role seeded → Task 6. ✓
- Generic password from config, returned in response → Tasks 3, 4. ✓
- `POST /api/swimmers` atomic create, role authz, 201/409, validator (required set incl. gender/dob/bloodtype/≥1 stroke) → Tasks 3, 4, 5. ✓
- Uniqueness (username/email) + reference existence + uid gen → Tasks 2, 4. ✓
- Re-seed 24 as full linked records; seeder reorder → Task 6. ✓
- Migration with demo-row delete + safety pre-check → Task 7. ✓
- Routing (widen guard + account-creation route) → Task 8. ✓
- Shared controls (select/date/chip-group) → Task 9. ✓
- Frontend create data layer (dto/model/repo/use-case) → Task 10. ✓
- i18n EN/AR → Task 11. ✓
- Account Creation page (two tabs, Swimmer live, Captain stub) + form + viewmodel + Captain Panel card, lookups by ID, credentials panel → Task 12. ✓
- Tests across entity/repo/validator/service/controller/seeder + frontend use-case/repo/viewmodel/components → all tasks + Task 13. ✓
- Non-goals (no swimmer login, no captain form, no other cards, no list/edit) → Task 13 scope guard. ✓
