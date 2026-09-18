# Register Captain / Head Coach Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a Head Coach register a Captain or Head Coach from the Captain Panel — a full `app_user` (chosen role, username, required email, backend-assigned generic password, forced first-login) + a `captain_profile` **or** `head_coach_profile` (`national_id`), created atomically by `POST /api/coaches`; plus the Angular Register Captain form filling the stubbed tab.

**Architecture:** No schema change — `captain_profile`/`head_coach_profile` already exist. A new `CoachService` (mirroring `SwimmerService`) builds app_user + the matching profile in one scoped `IdentityDbContext` + single `SaveChangesAsync`; a single `POST /api/coaches` (role discriminator, head-coach-only) serves both. Frontend adds a `features/coaches` data slice + a signals-based Register Captain form reusing the Phase-2 shared controls, wired to the Phase-1 gender lookup.

**Tech Stack:** .NET (C#, EF Core, xUnit + Moq, FluentValidation), PostgreSQL, Angular (standalone + signals, Jest).

**Spec:** `docs/superpowers/specs/2026-09-02-register-coach-design.md`

## Global Constraints

- **No migration / no schema change** — reuse the existing `captain_profile`/`head_coach_profile` tables (`national_id` varchar(14) unique, `user_id` unique FK→app_user cascade).
- **One endpoint** `POST /api/coaches` with `role` ∈ {`captain`,`head_coach`}; `[Authorize(Roles="head_coach")]` (only a Head Coach creates coaches).
- **Required (API validator AND form):** Name (EN), Username, Email, National ID (`^\d{14}$`), Captain Type (role), Gender, Date of Birth, Phone. Only **Name (AR)** optional. (Gender/DOB columns stay DB-nullable — app rule stricter than schema.)
- **Generic password from a shared config option** `AccountCreationOptions` (section `AccountCreation`), returned in the create response; never hardcoded in the frontend.
- **Conflict handling:** `ICoachProfileRepository.SaveChangesAsync` returns `Task<bool>` (false = Postgres `23505` unique-violation) — a uniqueness race → 409, Application stays EF-free. Mirrors the Phase-2 swimmer repo.
- **Placement:** all new backend types in the Identity module. **Coaches log in by email** (already supported) — no login changes.
- **No git commits** (user directive) — implement + run tests; skip every "Commit" step.
- **Build lock:** stop the dev API server before `dotnet build`/`test`. Backend commands from repo root; frontend from `frontend/`.
- **No scope creep:** no Assigned Clubs, no permissions persistence (display-only), no coach list/edit/delete, no swimmer/login changes.

---

### Task 1: Generalize the generic-password option (rename)

**Files:**
- Rename/replace: `.../Application/Options/SwimmerRegistrationOptions.cs` → `AccountCreationOptions.cs`
- Modify: `.../Application/Services/SwimmerService.cs` (options type)
- Modify: `.../Infrastructure/Extensions/IdentityModuleExtensions.cs` (options registration)
- Modify: `backend/Kheprx.BaseBackend.Api/appsettings.json` + `appsettings.Development.json` (section rename)
- Modify: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs` (options type in the fixture)

**Interfaces:**
- Produces: `AccountCreationOptions { const SectionName = "AccountCreation"; string GenericPassword }` — consumed by `SwimmerService` (this task) and `CoachService` (Task 4).

- [ ] **Step 1: Replace the options class**

Create `AccountCreationOptions.cs` (delete `SwimmerRegistrationOptions.cs`):
```csharp
namespace Kheprx.BaseBackend.Identity.Application.Options;

/// <summary>Bound from the "AccountCreation" config section — the generic password assigned to
/// every new swimmer/coach on registration (they must change it on first login).</summary>
public sealed class AccountCreationOptions
{
    public const string SectionName = "AccountCreation";
    public string GenericPassword { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Update every reference**

- `SwimmerService.cs`: change the field type `SwimmerRegistrationOptions _options` → `AccountCreationOptions _options` and the ctor param `IOptions<SwimmerRegistrationOptions>` → `IOptions<AccountCreationOptions>`.
- `IdentityModuleExtensions.cs`: change `services.Configure<SwimmerRegistrationOptions>(configuration.GetSection(SwimmerRegistrationOptions.SectionName))` → `services.Configure<AccountCreationOptions>(configuration.GetSection(AccountCreationOptions.SectionName))`.
- `appsettings.json` and `appsettings.Development.json`: rename the `"SwimmerRegistration"` object key to `"AccountCreation"` (keep `"GenericPassword": "Oasis2026!"`).
- `SwimmerServiceTests.cs`: in the `Build(...)` fixture, `Options.Create(new SwimmerRegistrationOptions { GenericPassword = "Oasis2026!" })` → `new AccountCreationOptions { ... }`; update the `using` if it referenced the old type name (same namespace).

- [ ] **Step 3: Verify swimmer tests stay green (no behavior change)**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~SwimmerServiceTests" -v minimal --nologo`
Expected: PASS (all swimmer service tests, unchanged behavior). A repo-wide search for `SwimmerRegistrationOptions` should return zero hits.

- [ ] **Step 4: Commit** — SKIP.

---

### Task 2: Coach DTOs, messages, validator

**Files:**
- Create: `.../Application/DTOs/CoachDtos.cs`
- Create: `.../Application/Resources/CoachMessages.cs`
- Modify: `.../Application/Validators/UserRequestValidators.cs` (add `CreateCoachRequestValidator`)
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Validators/CreateCoachRequestValidatorTests.cs`

**Interfaces:**
- Produces: `CreateCoachRequest(...)`, `CreatedCoachDto(...)`; `CoachMessages`; `CreateCoachRequestValidator`.

- [ ] **Step 1: Write the failing validator test (RED)**

```csharp
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Validators;

public class CreateCoachRequestValidatorTests
{
    private static CreateCoachRequest Valid() => new(
        Role: "captain", NameEn: "Dave Coach", Username: "dave.coach", Email: "dave@oasis.com",
        NationalId: "29001011234567", GenderId: Guid.NewGuid(), Dob: new DateOnly(1990, 1, 1),
        Phone: "01000000000", NameAr: null);

    private readonly CreateCoachRequestValidator _v = new();

    [Fact] public void Valid_request_passes() => Assert.True(_v.Validate(Valid()).IsValid);

    [Fact]
    public void Role_must_be_captain_or_head_coach()
    {
        Assert.True(_v.Validate(Valid() with { Role = "head_coach" }).IsValid);
        Assert.False(_v.Validate(Valid() with { Role = "swimmer" }).IsValid);
        Assert.False(_v.Validate(Valid() with { Role = "" }).IsValid);
    }

    [Fact]
    public void National_id_must_be_14_digits()
    {
        Assert.False(_v.Validate(Valid() with { NationalId = "123" }).IsValid);
        Assert.False(_v.Validate(Valid() with { NationalId = "2900101123456A" }).IsValid);
    }

    [Fact]
    public void Required_fields_and_formats_are_enforced()
    {
        Assert.False(_v.Validate(Valid() with { NameEn = "" }).IsValid);
        Assert.False(_v.Validate(Valid() with { Username = "" }).IsValid);
        Assert.False(_v.Validate(Valid() with { Email = "nope" }).IsValid);
        Assert.False(_v.Validate(Valid() with { GenderId = Guid.Empty }).IsValid);
        Assert.False(_v.Validate(Valid() with { Phone = "123" }).IsValid);
        Assert.False(_v.Validate(Valid() with { Dob = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) }).IsValid);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~CreateCoachRequestValidatorTests" -v minimal --nologo`
Expected: FAIL — types don't exist (compile error).

- [ ] **Step 3: Add DTOs, messages, validator**

`CoachDtos.cs`:
```csharp
namespace Kheprx.BaseBackend.Identity.Application.DTOs;

/// <summary>Payload to register a captain or head coach (POST /api/coaches).</summary>
public sealed record CreateCoachRequest(
    string Role, string NameEn, string Username, string Email, string NationalId,
    Guid GenderId, DateOnly Dob, string Phone, string? NameAr);

/// <summary>Result of a successful coach registration.</summary>
public sealed record CreatedCoachDto(Guid Id, string Username, string NameEn, string Role, string TemporaryPassword);
```

`CoachMessages.cs`:
```csharp
namespace Kheprx.BaseBackend.Identity.Application.Resources;

/// <summary>Localized messages for captain/head-coach registration.</summary>
public static class CoachMessages
{
    public static class Success
    {
        public static string CoachCreated(string lang) => lang switch { "ar" => "تم تسجيل الحساب بنجاح", _ => "Account registered" };
    }

    public static class Errors
    {
        public static string InvalidRole(string lang) => lang switch { "ar" => "النوع غير صالح", _ => "Invalid captain type" };
        public static string UsernameRequired(string lang) => lang switch { "ar" => "اسم المستخدم مطلوب", _ => "Username is required" };
        public static string GenderRequired(string lang) => lang switch { "ar" => "النوع مطلوب", _ => "Gender is required" };
        public static string DobRequired(string lang) => lang switch { "ar" => "تاريخ الميلاد مطلوب", _ => "Date of birth is required" };
        public static string DobInPast(string lang) => lang switch { "ar" => "يجب أن يكون تاريخ الميلاد في الماضي", _ => "Date of birth must be in the past" };
        public static string PhoneRequired(string lang) => lang switch { "ar" => "رقم الهاتف مطلوب", _ => "Phone is required" };
        public static string NationalIdInvalid(string lang) => lang switch { "ar" => "الرقم القومي يجب أن يكون ١٤ رقمًا", _ => "National ID must be 14 digits" };
        public static string UsernameTaken(string lang) => lang switch { "ar" => "اسم المستخدم مستخدم بالفعل", _ => "Username is already taken" };
        public static string EmailTaken(string lang) => lang switch { "ar" => "البريد الإلكتروني مستخدم بالفعل", _ => "Email is already in use" };
        public static string NationalIdTaken(string lang) => lang switch { "ar" => "الرقم القومي مستخدم بالفعل", _ => "National ID is already in use" };
    }
}
```

In `UserRequestValidators.cs` add (reuse `CommonMessages`, `UserMessages.Errors.PhoneInvalid`, `UserValidationRules.PhonePattern`, and `CoachMessages`):
```csharp
public sealed class CreateCoachRequestValidator : AbstractValidator<CreateCoachRequest>
{
    public CreateCoachRequestValidator()
    {
        RuleFor(x => x.Role)
            .NotEmpty().Must(r => r is "captain" or "head_coach")
            .WithMessage(_ => CoachMessages.Errors.InvalidRole(AppLanguage.Current));
        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage(_ => CommonMessages.Errors.FullNameRequired(AppLanguage.Current))
            .MaximumLength(200).WithMessage(_ => CommonMessages.Errors.FullNameTooLong(AppLanguage.Current));
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage(_ => CoachMessages.Errors.UsernameRequired(AppLanguage.Current))
            .MaximumLength(100).Matches(@"^[A-Za-z0-9._-]+$").WithMessage(_ => CoachMessages.Errors.UsernameRequired(AppLanguage.Current));
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(_ => CommonMessages.Errors.EmailInvalid(AppLanguage.Current))
            .EmailAddress().WithMessage(_ => CommonMessages.Errors.EmailInvalid(AppLanguage.Current))
            .MaximumLength(256).WithMessage(_ => CommonMessages.Errors.EmailTooLong(AppLanguage.Current));
        RuleFor(x => x.NationalId)
            .NotEmpty().Matches(@"^\d{14}$").WithMessage(_ => CoachMessages.Errors.NationalIdInvalid(AppLanguage.Current));
        RuleFor(x => x.GenderId)
            .NotEqual(Guid.Empty).WithMessage(_ => CoachMessages.Errors.GenderRequired(AppLanguage.Current));
        RuleFor(x => x.Dob)
            .NotEqual(default(DateOnly)).WithMessage(_ => CoachMessages.Errors.DobRequired(AppLanguage.Current))
            .LessThan(_ => DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage(_ => CoachMessages.Errors.DobInPast(AppLanguage.Current));
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage(_ => CoachMessages.Errors.PhoneRequired(AppLanguage.Current))
            .Matches(UserValidationRules.PhonePattern).WithMessage(_ => UserMessages.Errors.PhoneInvalid(AppLanguage.Current));
        RuleFor(x => x.NameAr).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.NameAr));
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~CreateCoachRequestValidatorTests" -v minimal --nologo`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit** — SKIP.

---

### Task 3: CoachProfileRepository — national-id check + bool save

**Files:**
- Modify: `.../Domain/Repositories/ICoachProfileRepository.cs`
- Modify: `.../Infrastructure/Repositories/CoachProfileRepository.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/CoachProfileRepositoryTests.cs`

**Interfaces:**
- Produces: `ICoachProfileRepository.NationalIdExistsAsync(string) → Task<bool>`; `SaveChangesAsync` changed to `Task<bool>` (false = 23505).

- [ ] **Step 1: Write the failing test (RED)**

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Kheprx.BaseBackend.Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Repositories;

public class CoachProfileRepositoryTests
{
    private static IdentityDbContext NewDb() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task NationalIdExists_checks_both_profile_tables()
    {
        await using var db = NewDb();
        var repo = new CoachProfileRepository(db);
        Assert.False(await repo.NationalIdExistsAsync("29001011234567"));

        await repo.AddCaptainAsync(new CaptainProfile(Guid.NewGuid(), "29001011234567"));
        await repo.AddHeadCoachAsync(new HeadCoachProfile(Guid.NewGuid(), "28502021234567"));
        var saved = await repo.SaveChangesAsync();

        Assert.True(saved);
        Assert.True(await repo.NationalIdExistsAsync("29001011234567")); // captain table
        Assert.True(await repo.NationalIdExistsAsync("28502021234567")); // head_coach table
        Assert.False(await repo.NationalIdExistsAsync("00000000000000"));
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~CoachProfileRepositoryTests" -v minimal --nologo`
Expected: FAIL — `NationalIdExistsAsync` missing; `SaveChangesAsync` returns `Task` not `Task<bool>` (compile error on `Assert.True(saved)`).

- [ ] **Step 3: Update the interface + impl**

In `ICoachProfileRepository.cs`: add `Task<bool> NationalIdExistsAsync(string nationalId, CancellationToken ct = default);` and change `Task SaveChangesAsync(...)` → `Task<bool> SaveChangesAsync(CancellationToken ct = default);`.

In `CoachProfileRepository.cs` (add `using Npgsql;`):
```csharp
    public async Task<bool> NationalIdExistsAsync(string nationalId, CancellationToken ct = default)
        => await _db.CaptainProfiles.AsNoTracking().AnyAsync(p => p.NationalId == nationalId, ct)
        || await _db.HeadCoachProfiles.AsNoTracking().AnyAsync(p => p.NationalId == nationalId, ct);

    public async Task<bool> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Unique-index race (national_id / username / email / user_id). Other failures propagate (→ 500).
            return false;
        }
    }
```
> Confirm no other caller relied on the old `Task` return of `SaveChangesAsync` (AuthService only calls `GetNationalIdAsync`). If the build flags an unused-await or caller mismatch, fix that caller.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~CoachProfileRepositoryTests" -v minimal --nologo`
Expected: PASS.

- [ ] **Step 5: Commit** — SKIP.

---

### Task 4: CoachService.CreateAsync

**Files:**
- Create: `.../Application/Services/Interfaces/ICoachService.cs`
- Create: `.../Application/Services/CoachService.cs`
- Modify: `.../Infrastructure/Extensions/IdentityModuleExtensions.cs` (register `ICoachService`)
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/CoachServiceTests.cs`

**Interfaces:**
- Consumes: `IUserRepository`, `IRoleRepository`, `ICoachProfileRepository`, `IGenderRepository`, `IPasswordHasher`, `IOptions<AccountCreationOptions>`; DTOs (Task 2).
- Produces: `ICoachService.CreateAsync(CreateCoachRequest, ct) → Task<CreatedCoachDto?>` (null on conflict).

- [ ] **Step 1: Write the failing test (RED)**

```csharp
using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Options;
using Kheprx.BaseBackend.Identity.Application.Services;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Services;

public class CoachServiceTests
{
    private static CreateCoachRequest Req(string role = "captain") => new(
        role, "Dave Coach", "dave.coach", "dave@oasis.com", "29001011234567",
        Guid.NewGuid(), new DateOnly(1990, 1, 1), "01000000000", null);

    private static (CoachService svc, Mock<IUserRepository> users, Mock<ICoachProfileRepository> coaches) Build(
        bool usernameTaken = false, bool emailTaken = false, bool nidTaken = false,
        bool genderExists = true, bool saveOk = true)
    {
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(usernameTaken ? new AppUser("x", "X", Guid.NewGuid()) : null);
        users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(emailTaken ? new AppUser("y", "Y", Guid.NewGuid()) : null);

        var roles = new Mock<IRoleRepository>();
        roles.Setup(r => r.GetByCodeAsync("captain", It.IsAny<CancellationToken>())).ReturnsAsync(new Role("captain", "Captain"));
        roles.Setup(r => r.GetByCodeAsync("head_coach", It.IsAny<CancellationToken>())).ReturnsAsync(new Role("head_coach", "Head Coach"));

        var coaches = new Mock<ICoachProfileRepository>();
        coaches.Setup(c => c.NationalIdExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(nidTaken);
        coaches.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(saveOk);

        var genders = new Mock<IGenderRepository>();
        genders.Setup(g => g.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(genderExists);

        var hasher = new Mock<IPasswordHasher>(); hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("HASH");
        var opts = Options.Create(new AccountCreationOptions { GenericPassword = "Oasis2026!" });

        var svc = new CoachService(users.Object, roles.Object, coaches.Object, genders.Object, hasher.Object, opts);
        return (svc, users, coaches);
    }

    [Fact]
    public async Task Create_captain_adds_captain_profile_and_returns_dto()
    {
        var (svc, users, coaches) = Build();
        var result = await svc.CreateAsync(Req("captain"));

        Assert.NotNull(result);
        Assert.Equal("captain", result!.Role);
        Assert.Equal("Oasis2026!", result.TemporaryPassword);
        users.Verify(u => u.AddAsync(It.Is<AppUser>(a => a.Username == "dave.coach" && a.IsFirstLogin), It.IsAny<CancellationToken>()), Times.Once);
        coaches.Verify(c => c.AddCaptainAsync(It.IsAny<CaptainProfile>(), It.IsAny<CancellationToken>()), Times.Once);
        coaches.Verify(c => c.AddHeadCoachAsync(It.IsAny<HeadCoachProfile>(), It.IsAny<CancellationToken>()), Times.Never);
        coaches.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_head_coach_adds_head_coach_profile()
    {
        var (svc, _, coaches) = Build();
        var result = await svc.CreateAsync(Req("head_coach"));
        Assert.Equal("head_coach", result!.Role);
        coaches.Verify(c => c.AddHeadCoachAsync(It.IsAny<HeadCoachProfile>(), It.IsAny<CancellationToken>()), Times.Once);
        coaches.Verify(c => c.AddCaptainAsync(It.IsAny<CaptainProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact] public async Task Returns_null_when_username_taken() { var (s,_,_) = Build(usernameTaken: true); Assert.Null(await s.CreateAsync(Req())); }
    [Fact] public async Task Returns_null_when_email_taken() { var (s,_,_) = Build(emailTaken: true); Assert.Null(await s.CreateAsync(Req())); }
    [Fact] public async Task Returns_null_when_national_id_taken() { var (s,_,_) = Build(nidTaken: true); Assert.Null(await s.CreateAsync(Req())); }
    [Fact] public async Task Returns_null_on_save_conflict() { var (s,_,_) = Build(saveOk: false); Assert.Null(await s.CreateAsync(Req())); }
    [Fact] public async Task Throws_when_gender_unknown() { var (s,_,_) = Build(genderExists: false); await Assert.ThrowsAnyAsync<Exception>(() => s.CreateAsync(Req())); }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~CoachServiceTests" -v minimal --nologo`
Expected: FAIL — `CoachService`/`ICoachService` don't exist (compile error).

- [ ] **Step 3: Implement the service**

`ICoachService.cs`:
```csharp
using Kheprx.BaseBackend.Identity.Application.DTOs;

namespace Kheprx.BaseBackend.Identity.Application.Services.Interfaces;

public interface ICoachService
{
    Task<CreatedCoachDto?> CreateAsync(CreateCoachRequest request, CancellationToken ct = default);
}
```

`CoachService.cs`:
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

internal sealed class CoachService : ICoachService
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly ICoachProfileRepository _coaches;
    private readonly IGenderRepository _genders;
    private readonly IPasswordHasher _hasher;
    private readonly AccountCreationOptions _options;

    public CoachService(
        IUserRepository users, IRoleRepository roles, ICoachProfileRepository coaches,
        IGenderRepository genders, IPasswordHasher hasher, IOptions<AccountCreationOptions> options)
    {
        _users = users;
        _roles = roles;
        _coaches = coaches;
        _genders = genders;
        _hasher = hasher;
        _options = options.Value;
    }

    public async Task<CreatedCoachDto?> CreateAsync(CreateCoachRequest request, CancellationToken ct = default)
    {
        var role = await _roles.GetByCodeAsync(request.Role, ct)
            ?? throw new InvalidUserException($"Unknown role '{request.Role}'.");

        if (await _users.GetByUsernameAsync(request.Username, ct) is not null) return null;

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _users.GetByEmailAsync(email, ct) is not null) return null;

        var nationalId = request.NationalId.Trim();
        if (await _coaches.NationalIdExistsAsync(nationalId, ct)) return null;

        if (!await _genders.ExistsAsync(request.GenderId, ct)) throw new InvalidUserException("Unknown gender.");

        var passwordHash = _hasher.Hash(_options.GenericPassword);
        var user = new AppUser(
            request.Username, request.NameEn, role.Id,
            nameAr: request.NameAr, email: email, passwordHash: passwordHash,
            genderId: request.GenderId, dob: request.Dob, phone: request.Phone, isFirstLogin: true);
        await _users.AddAsync(user, ct);

        if (role.Code == "captain")
            await _coaches.AddCaptainAsync(new CaptainProfile(user.Id, nationalId), ct);
        else
            await _coaches.AddHeadCoachAsync(new HeadCoachProfile(user.Id, nationalId), ct);

        if (!await _coaches.SaveChangesAsync(ct))
            return null; // unique-index race — surface as a conflict (controller → 409)

        return new CreatedCoachDto(user.Id, user.Username, user.NameEn, role.Code, _options.GenericPassword);
    }
}
```

In `IdentityModuleExtensions.AddIdentityModule`, add: `services.AddScoped<ICoachService, CoachService>();`

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Kheprx.BaseBackend.Identity.UnitTests.csproj --filter "FullyQualifiedName~CoachServiceTests" -v minimal --nologo`
Expected: PASS (7 tests).

- [ ] **Step 5: Commit** — SKIP.

---

### Task 5: POST /api/coaches endpoint + full backend suite

**Files:**
- Create: `backend/Kheprx.BaseBackend.Api/Controllers/CoachesController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/CoachesControllerTests.cs`

**Interfaces:**
- Consumes: `ICoachService.CreateAsync`, `CreateCoachRequest`, `CreatedCoachDto`, `CoachMessages`.
- Produces: `POST /api/coaches` → 201 / 409.

- [ ] **Step 1: Write the failing test (RED)**

```csharp
using Kheprx.BaseBackend.Api.Controllers;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class CoachesControllerTests
{
    private static CreateCoachRequest Req() => new(
        "captain", "Dave", "dave.coach", "dave@oasis.com", "29001011234567",
        Guid.NewGuid(), new DateOnly(1990, 1, 1), "01000000000", null);

    [Fact]
    public async Task Create_returns_201_with_created_coach()
    {
        var svc = new Mock<ICoachService>();
        var created = new CreatedCoachDto(Guid.NewGuid(), "dave.coach", "Dave", "captain", "Oasis2026!");
        svc.Setup(s => s.CreateAsync(It.IsAny<CreateCoachRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(created);

        var result = await new CoachesController(svc.Object).Create(Req(), CancellationToken.None);

        var ok = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, ok.StatusCode);
        var body = Assert.IsType<ApiResponse<CreatedCoachDto>>(ok.Value);
        Assert.Equal("captain", body.Data!.Role);
    }

    [Fact]
    public async Task Create_returns_409_when_service_returns_null()
    {
        var svc = new Mock<ICoachService>();
        svc.Setup(s => s.CreateAsync(It.IsAny<CreateCoachRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((CreatedCoachDto?)null);

        var result = await new CoachesController(svc.Object).Create(Req(), CancellationToken.None);

        var conflict = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests/Kheprx.BaseBackend.Api.UnitTests.csproj --filter "FullyQualifiedName~CoachesControllerTests" -v minimal --nologo`
Expected: FAIL — `CoachesController` doesn't exist.

- [ ] **Step 3: Add the controller**

`CoachesController.cs`:
```csharp
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Authorize(Roles = "head_coach")]
public sealed class CoachesController : BaseApiController
{
    private readonly ICoachService _service;

    public CoachesController(ICoachService service)
    {
        _service = service;
    }

    /// <summary>Registers a captain or head coach (app_user + profile). Head Coach only.</summary>
    /// <response code="201">Account created.</response>
    /// <response code="409">Username, email, or national ID already in use.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreatedCoachDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CreatedCoachDto>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CreatedCoachDto>>> Create(CreateCoachRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, ct);
        if (created is null)
        {
            var conflict = ApiResponse<CreatedCoachDto>.Failure(
                CoachMessages.Errors.NationalIdTaken(AppLanguage.Current), "conflict");
            return StatusCode(StatusCodes.Status409Conflict, conflict);
        }

        var body = ApiResponse<CreatedCoachDto>.Success(CoachMessages.Success.CoachCreated(AppLanguage.Current), created);
        return StatusCode(StatusCodes.Status201Created, body);
    }
}
```

- [ ] **Step 4: Run the focused test, then the full backend suite**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests/Kheprx.BaseBackend.Api.UnitTests.csproj --filter "FullyQualifiedName~CoachesControllerTests" -v minimal --nologo`
Expected: PASS (2 tests).
Then: `dotnet test backend/Kheprx.BaseBackend.sln -v minimal --nologo`
Expected: all green (incl. the renamed-options swimmer tests + architecture tests).

- [ ] **Step 5: Commit** — SKIP.

---

### Task 6: Frontend coaches data slice

**Files:**
- Create: `frontend/src/app/features/coaches/data/dto/create-coach.dto.ts`
- Create: `frontend/src/app/features/coaches/domain/model/coach.ts`
- Create: `frontend/src/app/features/coaches/domain/repositories/coach.repository.ts`
- Create: `frontend/src/app/features/coaches/data/repositories/coach.repository.impl.ts`
- Create: `frontend/src/app/features/coaches/domain/usecases/create-coach.use-case.ts`
- Create: `frontend/src/app/features/coaches/data/coach.providers.ts`
- Create: `frontend/src/app/features/coaches/index.ts`
- Modify: `frontend/src/app/app.config.ts`
- Test: `frontend/src/app/features/coaches/testing/data/repositories/coach.repository.impl.spec.ts`, `frontend/src/app/features/coaches/testing/domain/usecases/create-coach.use-case.spec.ts`

**Interfaces:**
- Produces: `CreateCoachDtoRq`, `CreatedCoachDtoRs`, `CreatedCoachItemDtoRs`, `isCreatedCoachDtoRsValid`; `CreatedCoach`; `ICoachRepository.create()` + `COACH_REPOSITORY`; `CoachRepositoryImpl`; `CreateCoachUseCase`; `COACH_PROVIDERS`.

- [ ] **Step 1: Write the failing tests (RED)**

`create-coach.use-case.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { CreateCoachUseCase } from '@features/coaches/domain/usecases/create-coach.use-case';
import { COACH_REPOSITORY, ICoachRepository } from '@features/coaches/domain/repositories/coach.repository';

const OK = { data: { id: 'x', username: 'dave.coach', nameEn: 'Dave', role: 'captain', temporaryPassword: 'Oasis2026!' } };
const rq = { role: 'captain', nameEn: 'Dave', username: 'dave.coach', email: 'd@o.com', nationalId: '29001011234567', genderId: 'g1', dob: '1990-01-01', phone: '01000000000' };

function build(repo: ICoachRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: COACH_REPOSITORY, useValue: repo }] });
  return TestBed.inject(CreateCoachUseCase);
}

describe('CreateCoachUseCase', () => {
  it('maps the created coach', async () => {
    const uc = build({ create: async () => OK } as unknown as ICoachRepository);
    const r = await uc.run(rq as never);
    expect(r.ok).toBe(true);
    if (r.ok) { expect(r.data.role).toBe('captain'); expect(r.data.temporaryPassword).toBe('Oasis2026!'); }
  });

  it('fails validation on malformed response', async () => {
    const uc = build({ create: async () => ({ data: { id: 'x' } }) } as unknown as ICoachRepository);
    const r = await uc.run(rq as never);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
```

`coach.repository.impl.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { CoachRepositoryImpl } from '@features/coaches/data/repositories/coach.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('CoachRepositoryImpl', () => {
  const http = { post: jest.fn() } as unknown as HttpClientService;
  let repo: CoachRepositoryImpl;
  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({ providers: [CoachRepositoryImpl, { provide: HttpClientService, useValue: http }] });
    repo = TestBed.inject(CoachRepositoryImpl);
  });
  it('create POSTs /api/coaches with the body', async () => {
    (http.post as jest.Mock).mockResolvedValue({ data: {} });
    const rq = { role: 'captain', nameEn: 'Dave', username: 'dave', email: 'd@o.com', nationalId: '29001011234567', genderId: 'g1', dob: '1990-01-01', phone: '01000000000' };
    await repo.create(rq as never);
    expect(http.post).toHaveBeenCalledWith('/api/coaches', { body: rq });
  });
});
```

- [ ] **Step 2: Run to verify they fail**

Run: `cd frontend && npm test -- "create-coach.use-case|coach.repository.impl"`
Expected: FAIL — modules don't exist.

- [ ] **Step 3: Implement the slice + wire app.config**

`create-coach.dto.ts`:
```ts
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface CreateCoachDtoRq {
  role: 'captain' | 'head_coach';
  nameEn: string;
  username: string;
  email: string;
  nationalId: string;
  genderId: string;
  dob: string; // yyyy-mm-dd
  phone: string;
  nameAr?: string;
}

export interface CreatedCoachDtoRs {
  id: string;
  username: string;
  nameEn: string;
  role: string;
  temporaryPassword: string;
}

export interface CreatedCoachItemDtoRs extends BaseResponseRs<CreatedCoachDtoRs> {}

export function isCreatedCoachDtoRsValid(dto: unknown): dto is CreatedCoachDtoRs {
  const d = dto as CreatedCoachDtoRs;
  return !!d && typeof d.id === 'string' && typeof d.username === 'string'
    && typeof d.nameEn === 'string' && typeof d.role === 'string'
    && typeof d.temporaryPassword === 'string';
}
```

`coach.ts`:
```ts
export interface CreatedCoach {
  id: string;
  username: string;
  nameEn: string;
  role: string;
  temporaryPassword: string;
}
```

`coach.repository.ts`:
```ts
import { InjectionToken } from '@angular/core';
import { CreateCoachDtoRq, CreatedCoachItemDtoRs } from '@features/coaches/data/dto/create-coach.dto';

export interface ICoachRepository {
  create(rq: CreateCoachDtoRq): Promise<CreatedCoachItemDtoRs>;
}

export const COACH_REPOSITORY = new InjectionToken<ICoachRepository>('COACH_REPOSITORY');
```

`coach.repository.impl.ts`:
```ts
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { ICoachRepository } from '@features/coaches/domain/repositories/coach.repository';
import { CreateCoachDtoRq, CreatedCoachItemDtoRs } from '@features/coaches/data/dto/create-coach.dto';

@Injectable({ providedIn: 'root' })
export class CoachRepositoryImpl implements ICoachRepository {
  private readonly http = inject(HttpClientService);
  create(rq: CreateCoachDtoRq): Promise<CreatedCoachItemDtoRs> {
    return this.http.post<CreatedCoachItemDtoRs>('/api/coaches', { body: rq });
  }
}
```

`create-coach.use-case.ts`:
```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { COACH_REPOSITORY } from '@features/coaches/domain/repositories/coach.repository';
import { CreateCoachDtoRq, isCreatedCoachDtoRsValid } from '@features/coaches/data/dto/create-coach.dto';
import { CreatedCoach } from '@features/coaches/domain/model/coach';

@Injectable({ providedIn: 'root' })
export class CreateCoachUseCase extends UseCase<CreateCoachDtoRq, CreatedCoach> {
  private readonly repo = inject(COACH_REPOSITORY);
  constructor() { super('CreateCoach'); }
  protected async execute(input: CreateCoachDtoRq): Promise<CreatedCoach> {
    const res = await this.repo.create(input);
    if (!isCreatedCoachDtoRsValid(res.data)) throw new AppError('Invalid created coach received', 'validation');
    return { ...res.data };
  }
}
```

`coach.providers.ts`:
```ts
import { Provider } from '@angular/core';
import { COACH_REPOSITORY } from '@features/coaches/domain/repositories/coach.repository';
import { CoachRepositoryImpl } from '@features/coaches/data/repositories/coach.repository.impl';

export const COACH_PROVIDERS: Provider[] = [
  { provide: COACH_REPOSITORY, useClass: CoachRepositoryImpl },
];
```

`index.ts`:
```ts
export * from '@features/coaches/data/coach.providers';
```

In `app.config.ts`: add `import { COACH_PROVIDERS } from '@features/coaches/data/coach.providers';` and spread `...COACH_PROVIDERS,` right after `...REFERENCE_PROVIDERS,`.

- [ ] **Step 4: Run to verify they pass**

Run: `cd frontend && npm test -- "create-coach.use-case|coach.repository.impl"`
Expected: PASS.

- [ ] **Step 5: Commit** — SKIP.

---

### Task 7: Register Captain form + viewmodel + page wiring + i18n

**Files:**
- Create: `frontend/src/app/features/captain-panel/presentation/pages/account-creation/register-coach.viewmodel.ts`
- Create: `frontend/src/app/features/captain-panel/presentation/pages/account-creation/register-coach-form.component.ts` (+ `.html`)
- Modify: `frontend/src/app/features/captain-panel/presentation/pages/account-creation/account-creation.page.ts` (+ `.html`)
- Modify: `frontend/src/app/features/captain-panel/index.ts`
- Modify: `frontend/src/app/app.routes.ts` (add `RegisterCoachViewModel` provider)
- Modify: `frontend/src/app/core/i18n/en.json`, `ar.json`
- Test: `frontend/src/app/features/captain-panel/testing/presentation/pages/account-creation/register-coach.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `LoadGendersUseCase` (`@features/reference`), `CreateCoachUseCase` (`@features/coaches`), `NotificationService`, `TranslateService`, `LanguageStore`, Phase-2 shared controls.
- Produces: `RegisterCoachViewModel`, `RegisterCoachFormComponent` — exported from `@features/captain-panel`.

- [ ] **Step 1: Write the failing viewmodel spec (RED)**

```ts
import { TestBed } from '@angular/core/testing';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';
import { RegisterCoachViewModel } from '@features/captain-panel/presentation/pages/account-creation/register-coach.viewmodel';
import { LoadGendersUseCase } from '@features/reference';
import { CreateCoachUseCase } from '@features/coaches/domain/usecases/create-coach.use-case';
import { NotificationService } from '@core/ui/notification.service';

const genders = [{ id: 'g1', code: 'male', nameEn: 'Male', nameAr: 'ذكر' }];

function build(create = { run: jest.fn() }) {
  const notify = { success: jest.fn(), error: jest.fn() } as unknown as NotificationService;
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      RegisterCoachViewModel,
      { provide: LoadGendersUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(genders)) } },
      { provide: CreateCoachUseCase, useValue: create },
      { provide: NotificationService, useValue: notify },
    ],
  });
  return { vm: TestBed.inject(RegisterCoachViewModel), notify };
}

function fill(vm: RegisterCoachViewModel) {
  vm.nameEn.set('Dave'); vm.username.set('dave'); vm.email.set('d@o.com');
  vm.nationalId.set('29001011234567'); vm.genderId.set('g1'); vm.dob.set('1990-01-01'); vm.phone.set('01000000000');
}

describe('RegisterCoachViewModel', () => {
  it('loads genders', async () => {
    const { vm } = build();
    await Promise.resolve(); await Promise.resolve();
    expect(vm.genders().length).toBe(1);
  });

  it('blocks submit until required fields (incl. 14-digit national id) are set', () => {
    const { vm } = build();
    expect(vm.canSubmit()).toBe(false);
    fill(vm);
    expect(vm.canSubmit()).toBe(true);
    vm.nationalId.set('123'); // invalid
    expect(vm.canSubmit()).toBe(false);
  });

  it('creates and exposes credentials on success', async () => {
    const created = { id: 'i', username: 'dave', nameEn: 'Dave', role: 'captain', temporaryPassword: 'Oasis2026!' };
    const create = { run: jest.fn().mockResolvedValue(ok(created)) };
    const { vm, notify } = build(create);
    fill(vm);
    await vm.submit();
    expect(create.run).toHaveBeenCalled();
    expect(vm.created()?.temporaryPassword).toBe('Oasis2026!');
    expect(notify.success).toHaveBeenCalled();
  });

  it('surfaces a conflict', async () => {
    const create = { run: jest.fn().mockResolvedValue(fail(new AppError('taken', 'http', 409))) };
    const { vm, notify } = build(create);
    fill(vm);
    await vm.submit();
    expect(vm.created()).toBeNull();
    expect(notify.error).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd frontend && npm test -- register-coach.viewmodel`
Expected: FAIL — viewmodel doesn't exist.

- [ ] **Step 3: Implement viewmodel, form, page wiring, exports, route, i18n**

`register-coach.viewmodel.ts`:
```ts
import { Injectable, computed, inject, signal } from '@angular/core';
import { LoadGendersUseCase } from '@features/reference';
import { LookupItem } from '@features/reference/domain/model/reference';
import { CreateCoachUseCase } from '@features/coaches/domain/usecases/create-coach.use-case';
import { CreatedCoach } from '@features/coaches/domain/model/coach';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';

@Injectable()
export class RegisterCoachViewModel {
  private readonly loadGenders = inject(LoadGendersUseCase);
  private readonly createCoach = inject(CreateCoachUseCase);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  readonly genders = signal<LookupItem[]>([]);

  readonly role = signal<'captain' | 'head_coach'>('captain');
  readonly nameEn = signal('');
  readonly nameAr = signal('');
  readonly username = signal('');
  readonly email = signal('');
  readonly phone = signal('');
  readonly nationalId = signal('');
  readonly genderId = signal('');
  readonly dob = signal('');

  readonly loading = signal(false);
  readonly created = signal<CreatedCoach | null>(null);

  readonly canSubmit = computed(() =>
    this.nameEn().trim().length > 0 &&
    this.username().trim().length > 0 &&
    this.email().trim().length > 0 &&
    /^\d{14}$/.test(this.nationalId().trim()) &&
    this.genderId().length > 0 &&
    this.dob().length > 0 &&
    this.phone().trim().length > 0);

  constructor() { void this.loadGendersLookup(); }

  private async loadGendersLookup(): Promise<void> {
    const g = await this.loadGenders.run();
    if (g.ok) this.genders.set(g.data);
  }

  async submit(): Promise<void> {
    if (!this.canSubmit() || this.loading()) return;
    this.loading.set(true);
    const r = await this.createCoach.run({
      role: this.role(),
      nameEn: this.nameEn().trim(),
      username: this.username().trim(),
      email: this.email().trim(),
      nationalId: this.nationalId().trim(),
      genderId: this.genderId(),
      dob: this.dob(),
      phone: this.phone().trim(),
      nameAr: this.nameAr().trim() || undefined,
    });
    this.loading.set(false);
    if (r.ok) {
      this.created.set(r.data);
      this.notify.success(this.i18n.t('accountCreation.success'));
    } else {
      const key = r.error.status === 409 ? 'accountCreation.errors.taken' : 'accountCreation.errors.failed';
      this.notify.error(this.i18n.t(key));
    }
  }

  reset(): void {
    this.created.set(null);
    for (const s of [this.nameEn, this.nameAr, this.username, this.email, this.phone, this.nationalId, this.genderId, this.dob]) s.set('');
    this.role.set('captain');
  }
}
```

`register-coach-form.component.ts`:
```ts
import { Component, inject } from '@angular/core';
import { TranslatePipe } from '@core/i18n';
import { TextFieldComponent } from '@core/ui/components/text-field.component';
import { SelectFieldComponent } from '@core/ui/components/select-field.component';
import { DateFieldComponent } from '@core/ui/components/date-field.component';
import { RegisterCoachViewModel } from './register-coach.viewmodel';

@Component({
  selector: 'app-register-coach-form',
  standalone: true,
  imports: [TranslatePipe, TextFieldComponent, SelectFieldComponent, DateFieldComponent],
  templateUrl: './register-coach-form.component.html',
})
export class RegisterCoachFormComponent {
  protected readonly vm = inject(RegisterCoachViewModel);
}
```

`register-coach-form.component.html` (Captain Type = plain 2-option select bound to `role`; Gender = `app-select-field` from lookups; credentials panel on success):
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
    <app-text-field [label]="'accountCreation.fields.nationalId' | translate" [placeholder]="'accountCreation.placeholders.nationalId' | translate" [value]="vm.nationalId()" (valueChange)="vm.nationalId.set($event)"></app-text-field>
    <app-text-field [label]="'accountCreation.fields.email' | translate" [value]="vm.email()" (valueChange)="vm.email.set($event)"></app-text-field>
    <app-text-field [label]="'accountCreation.fields.phone' | translate" [value]="vm.phone()" (valueChange)="vm.phone.set($event)"></app-text-field>
    <app-select-field [label]="'accountCreation.fields.gender' | translate" [placeholder]="'accountCreation.placeholders.selectOne' | translate" [options]="vm.genders()" [value]="vm.genderId()" (valueChange)="vm.genderId.set($event)"></app-select-field>
    <app-date-field [label]="'accountCreation.fields.dob' | translate" [value]="vm.dob()" (valueChange)="vm.dob.set($event)"></app-date-field>
    <label class="flex flex-col gap-1">
      <span class="text-sm font-bold text-ink">{{ 'accountCreation.fields.captainType' | translate }}</span>
      <select class="h-10 rounded-md border border-input bg-card px-3 text-sm text-ink focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
        [value]="vm.role()" (change)="vm.role.set($any($event.target).value)">
        <option value="captain">{{ 'roles.captain' | translate }}</option>
        <option value="head_coach">{{ 'roles.head_coach' | translate }}</option>
      </select>
    </label>
    <div class="md:col-span-2 rounded-xl border border-info/30 bg-info-bg p-4">
      <h4 class="font-heading text-ink">{{ 'accountCreation.permissions.title' | translate }}</h4>
      <div class="mt-2 flex flex-wrap gap-2 text-xs">
        <span class="rounded-full bg-card px-3 py-1">{{ 'accountCreation.permissions.attendance' | translate }}</span>
        <span class="rounded-full bg-card px-3 py-1">{{ 'accountCreation.permissions.swimmerData' | translate }}</span>
        <span class="rounded-full bg-card px-3 py-1">{{ 'accountCreation.permissions.testReadings' | translate }}</span>
        <span class="rounded-full bg-card px-3 py-1">{{ 'accountCreation.permissions.health' | translate }}</span>
        <span class="rounded-full bg-card px-3 py-1">{{ 'accountCreation.permissions.records' | translate }}</span>
      </div>
    </div>
    <div class="md:col-span-2">
      <button type="submit" class="rounded-md bg-primary px-5 py-2.5 text-sm font-semibold text-white disabled:opacity-50" [disabled]="!vm.canSubmit() || vm.loading()">
        {{ 'accountCreation.submit' | translate }}
      </button>
    </div>
  </form>
}
```
> If `info-bg`/`info` tokens don't exist, use the warning tokens the swimmer form used (`border-warning/30 bg-warning-bg`).

`account-creation.page.ts` — add the coach form to imports:
```ts
import { RegisterCoachFormComponent } from './register-coach-form.component';
// ...
  imports: [TranslatePipe, RegisterSwimmerFormComponent, RegisterCoachFormComponent],
```
`account-creation.page.html` — replace the captain `@else` branch:
```html
  @if (tab() === 'swimmer') {
    <app-register-swimmer-form></app-register-swimmer-form>
  } @else {
    <app-register-coach-form></app-register-coach-form>
  }
```

`captain-panel/index.ts` — add:
```ts
export { RegisterCoachViewModel } from './presentation/pages/account-creation/register-coach.viewmodel';
```

`app.routes.ts` — add `RegisterCoachViewModel` to the `captain-panel/account-creation` route `providers` and its import:
```ts
import { RegisterSwimmerViewModel, RegisterCoachViewModel } from '@features/captain-panel';
// ...
        providers: [RegisterSwimmerViewModel, RegisterCoachViewModel],
```

`en.json` — add under `accountCreation`: `fields.nationalId` = "National ID", `fields.captainType` = "Captain Type"; `placeholders.nationalId` = "14-digit ID"; `permissions` = `{ "title": "Captain Permissions", "attendance": "Attendance", "swimmerData": "Swimmer Data", "testReadings": "Test Readings", "health": "Health", "records": "Records" }`; and `errors.taken` = "That username, email, or national ID is already in use." Add the same keys to `ar.json` with Arabic values (`nationalId` "الرقم القومي", `captainType` "نوع الكابتن", `permissions.title` "صلاحيات الكابتن", etc.).

- [ ] **Step 4: Run to verify it passes**

Run: `cd frontend && npm test -- register-coach.viewmodel`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit** — SKIP.

---

### Task 8: Full-suite verification

**Files:** none.

- [ ] **Step 1: Full frontend suite**

Run: `cd frontend && npm test`
Expected: all green (new coach specs + existing; the `app.routes` + account-creation page compile with the new exports). If unrelated pre-existing specs fail, report them — don't fix.

- [ ] **Step 2: Full backend suite** (dev server stopped)

Run: `dotnet test backend/Kheprx.BaseBackend.sln -v minimal --nologo`
Expected: all green.

- [ ] **Step 3: Smoke (optional)**

`dotnet run --project backend/Kheprx.BaseBackend.Api` → with a Head-Coach bearer token, `POST /api/coaches` `{role:"captain", …, nationalId:"29001011234599"}` returns 201 `{ username, role:"captain", temporaryPassword }`; the new captain can then log in by email + that password and hit forced first-login; a duplicate national ID / username / email returns 409; a Captain token gets 403.

- [ ] **Step 4: Scope guard** — confirm no Assigned Clubs, no permissions persistence, no coach list/edit/delete, no swimmer/login changes were introduced.

- [ ] **Step 5: Commit** — SKIP.

---

## Spec coverage check

- Shared `AccountCreationOptions` (rename) used by swimmer + coach → Task 1. ✓
- `CreateCoachRequest`/`CreatedCoachDto` + validator (role∈{captain,head_coach}, 14-digit NID, required set) → Task 2. ✓
- `NationalIdExistsAsync` across both tables + `SaveChangesAsync`→bool (23505) → Task 3. ✓
- `CoachService.CreateAsync` (atomic app_user + captain/head_coach profile, uniqueness, generic password, is_first_login) → Task 4. ✓
- `POST /api/coaches` head-coach-only, 201/409 → Task 5. ✓
- Frontend `features/coaches` slice → Task 6. ✓
- Register Captain form + viewmodel filling the tab, Captain Type switch, gender lookup, credentials panel, permissions display-only, i18n → Task 7. ✓
- No migration; non-goals (Assigned Clubs, permissions persistence, list/edit, login changes) absent → Task 8 scope guard. ✓
