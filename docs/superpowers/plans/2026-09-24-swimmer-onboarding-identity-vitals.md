# Swimmer First-Login Onboarding — Step 1 (Identity & Vitals) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a swimmer signs in for the first time, route them into a full-screen onboarding wizard whose first step ("Identity & Vitals") lets them complete their own profile; completing it clears `IsFirstLogin` and lands them on the dashboard.

**Architecture:** A single atomic self-service backend endpoint (`GET`/`POST /api/swimmers/me/onboarding/identity-vitals`, role `swimmer`, swimmer resolved from the JWT) updates `app_user` + `swimmer_profile`, inserts a new `athlete.medical_exam`, and clears `IsFirstLogin` in one `SaveChanges`. A new Angular `features/swimmer-onboarding` slice hosts the 5-step wizard (only Step 1 functional); role-aware routing sends first-login swimmers to `/onboarding` while leaving the coach `/change-password` flow untouched. No new tables, no DB migration.

**Tech Stack:** Backend — .NET / ASP.NET Core, EF Core (Npgsql), FluentValidation, xUnit + Moq. Frontend — Angular (standalone components, signals), Jest.

**Spec:** `docs/superpowers/specs/2026-09-24-swimmer-onboarding-identity-vitals-design.md`

## Global Constraints

- **Do NOT run `git commit` or `git add` — the user commits manually (explicit directive).** Where a TDD cycle would normally end in a commit, stop after the tests pass and report the task complete. (Steps below say "Checkpoint" instead of "Commit".)
- **Stop the running dev API before any `dotnet` build/test** — a running backend locks the DLLs and the build fails. (See memory `dotnet-test-dev-server-lock`.)
- **No new tables and no EF migration** in this feature — only C# entity methods and one repository query.
- **Self-service endpoints derive the swimmer from `CurrentUserId()` (JWT `sub`), never from a path/body id.** They are gated `[Authorize(Roles = "swimmer")]`.
- **`Full Name` maps to `name_en` only; `name_ar` is preserved** (send back the value loaded from prefill; never blank it).
- **Email + phone are preserved** on the identity update (pass the current `user.Email` / `user.Phone` through `UpdateProfile`).
- **Reference-id validation throws `InvalidUserException("Unknown …")`** (the existing global filter maps it to `400`); missing-swimmer returns `null` → controller `404`.
- Backend test projects: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests`, `backend/tests/Kheprx.BaseBackend.Api.UnitTests`. Frontend tests: Jest, run from `frontend/` (`npx jest <path>`).
- Frontend layering to mirror exactly (from `features/swimmer-profile`): `data/dto` (`…DtoRq`/`…DtoRs` + `is…Valid` guards) → `data/repositories/*.impl.ts` (`@Injectable({providedIn:'root'})`, returns the raw `…ItemDtoRs` envelope via `HttpClientService`) → `domain/repositories/*.repository.ts` (interface + `InjectionToken`) → `domain/usecases/*.use-case.ts` (`@Injectable({providedIn:'root'})`, `extends UseCase<I,O>`, injects the token, validates `res.data`, throws `AppError`) → `data/*.providers.ts` → `index.ts` barrel → registered in `app.config.ts`.

## Review Focus

- **Swimmer logs out mid-onboarding, then logs back in** — must return to the wizard, because `IsFirstLogin` clears only on completion. Pinned by Task 5 (service does not clear the flag before `SaveChanges` of a completed step) and Task 12 (`onboardingGuard` re-admits a `swimmer` + `mustChangePassword` user).
- **A coach or captain opens `/onboarding` directly** — must be redirected, not shown the swimmer wizard. Pinned by Task 12 (`onboardingGuard` non-swimmer → `/home`).
- **An already-onboarded swimmer opens `/onboarding`** — must be redirected to `/home`. Pinned by Task 12 (`onboardingGuard` `mustChangePassword() === false` → `/home`).
- **Blood Type omitted (it's optional)** — the exam must still save with `bloodTypeId = null`. Pinned by Task 5 (service test with null blood type) and Task 4 (validator does not require it).
- **Future DOB, future exam date, or non-positive height/weight/hemoglobin** — must be rejected with `400`, not persisted. Pinned by Task 4 (`CompleteIdentityVitalsRequestValidator` tests).

---

## BACKEND

### Task 1: `AppUser.CompleteFirstLogin()` entity method

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/AppUser.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/AppUserTests.cs`

**Interfaces:**
- Produces: `void AppUser.CompleteFirstLogin()` — sets `IsFirstLogin = false`; does not touch the password.

- [ ] **Step 1: Write the failing test** — add to `AppUserTests`:

```csharp
[Fact]
public void CompleteFirstLogin_clears_flag_without_touching_password()
{
    var u = New(firstLogin: true);
    u.CompleteFirstLogin();
    Assert.False(u.IsFirstLogin);
    Assert.Equal("hash", u.PasswordHash); // unchanged (New() sets passwordHash: "hash")
}

[Fact]
public void CompleteFirstLogin_is_idempotent()
{
    var u = New(firstLogin: true);
    u.CompleteFirstLogin();
    u.CompleteFirstLogin();
    Assert.False(u.IsFirstLogin);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~AppUserTests`
Expected: FAIL — `AppUser` does not contain a definition for `CompleteFirstLogin`.

- [ ] **Step 3: Write minimal implementation** — add to `AppUser.cs` (below `SetPassword`):

```csharp
    // Clears the forced-first-login flag WITHOUT changing the password. Used by the
    // swimmer onboarding wizard, which completes first-login by finishing the wizard
    // rather than by a password change (unlike SetPassword). Idempotent.
    public void CompleteFirstLogin()
    {
        IsFirstLogin = false;
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~AppUserTests`
Expected: PASS.

- [ ] **Step 5: Checkpoint** — tests green; do NOT commit (user directive).

---

### Task 2: `SwimmerProfile.SetTrainingClub(Guid)` entity method

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/SwimmerProfile.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/SwimmerProfileTests.cs` (create if absent)

**Interfaces:**
- Produces: `void SwimmerProfile.SetTrainingClub(Guid trainingClubId)` — sets `TrainingClubId`, bumps `UpdatedAt`; throws `InvalidUserException` on `Guid.Empty`.

- [ ] **Step 1: Write the failing test** — create `SwimmerProfileTests.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Exceptions;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public sealed class SwimmerProfileTests
{
    [Fact]
    public void SetTrainingClub_updates_id()
    {
        var p = new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid());
        var newClub = Guid.NewGuid();
        p.SetTrainingClub(newClub);
        Assert.Equal(newClub, p.TrainingClubId);
    }

    [Fact]
    public void SetTrainingClub_rejects_empty()
    {
        var p = new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid());
        Assert.Throws<InvalidUserException>(() => p.SetTrainingClub(Guid.Empty));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~SwimmerProfileTests`
Expected: FAIL — no `SetTrainingClub`.

- [ ] **Step 3: Write minimal implementation** — add to `SwimmerProfile.cs` (add `using Kheprx.BaseBackend.Identity.Domain.Exceptions;` at the top):

```csharp
    public void SetTrainingClub(Guid trainingClubId)
    {
        if (trainingClubId == Guid.Empty) throw new InvalidUserException("Training club is required.");
        TrainingClubId = trainingClubId;
        UpdatedAt = DateTime.UtcNow;
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~SwimmerProfileTests`
Expected: PASS.

- [ ] **Step 5: Checkpoint** — tests green; do NOT commit.

---

### Task 3: `GetByUserIdTrackedAsync` on the swimmer-profile repository

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/ISwimmerProfileRepository.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/SwimmerProfileRepository.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/SwimmerProfileRepositoryTests.cs`

**Interfaces:**
- Produces: `Task<SwimmerProfile?> ISwimmerProfileRepository.GetByUserIdTrackedAsync(Guid userId, CancellationToken ct = default)` — tracked profile for the given `app_user` id, or `null`.

- [ ] **Step 1: Write the failing test** — add to `SwimmerProfileRepositoryTests`:

```csharp
[Fact]
public async Task GetByUserIdTracked_returns_profile_for_user_and_null_otherwise()
{
    await using var db = NewDb();
    var repo = new SwimmerProfileRepository(db);
    var userId = Guid.NewGuid();
    await repo.AddAsync(new SwimmerProfile(userId, "SW-0007", Guid.NewGuid()));
    await repo.SaveChangesAsync();

    var found = await repo.GetByUserIdTrackedAsync(userId);
    Assert.NotNull(found);
    Assert.Equal("SW-0007", found!.Uid);

    Assert.Null(await repo.GetByUserIdTrackedAsync(Guid.NewGuid()));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~SwimmerProfileRepositoryTests`
Expected: FAIL — no `GetByUserIdTrackedAsync`.

- [ ] **Step 3: Write minimal implementation**

Add to `ISwimmerProfileRepository.cs` (next to `GetByIdTrackedAsync`):

```csharp
    Task<SwimmerProfile?> GetByUserIdTrackedAsync(Guid userId, CancellationToken ct = default);
```

Add to `SwimmerProfileRepository.cs` (next to `GetByIdTrackedAsync`):

```csharp
    public Task<SwimmerProfile?> GetByUserIdTrackedAsync(Guid userId, CancellationToken ct = default)
        => _db.SwimmerProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~SwimmerProfileRepositoryTests`
Expected: PASS.

- [ ] **Step 5: Checkpoint** — tests green; do NOT commit.

---

### Task 4: Onboarding DTOs, messages, and request validator

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/SwimmerDtos.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Resources/SwimmerMessages.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Validators/UserRequestValidators.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Validators/OnboardingValidatorTests.cs` (create)

**Interfaces:**
- Produces:
  - `record OnboardingPrefillDto(string Uid, string NameEn, string? NameAr, Guid? GenderId, DateOnly? Dob, Guid? TrainingClubId)`
  - `record CompleteIdentityVitalsRequest(string NameEn, string? NameAr, Guid GenderId, DateOnly Dob, Guid TrainingClubId, DateOnly ExamDate, Guid? BloodTypeId, decimal Hemoglobin, decimal HeightCm, decimal WeightKg, Guid InternalMedId, Guid HeartAssessId, Guid SpineAssessId)`
  - `record OnboardingStepResultDto(bool MustChangePassword)`
  - `SwimmerMessages.Success.OnboardingPrefillRetrieved(string)`, `SwimmerMessages.Success.OnboardingCompleted(string)`
  - `CompleteIdentityVitalsRequestValidator`

- [ ] **Step 1: Add the DTOs** — append to `SwimmerDtos.cs`:

```csharp
/// <summary>Prefill for the swimmer first-login wizard, Step 1 (GET /api/swimmers/me/onboarding/identity-vitals).</summary>
public sealed record OnboardingPrefillDto(
    string Uid, string NameEn, string? NameAr, Guid? GenderId, DateOnly? Dob, Guid? TrainingClubId);

/// <summary>Step 1 submission (POST /api/swimmers/me/onboarding/identity-vitals): identity edits + a new medical exam.</summary>
public sealed record CompleteIdentityVitalsRequest(
    string NameEn, string? NameAr, Guid GenderId, DateOnly Dob, Guid TrainingClubId,
    DateOnly ExamDate, Guid? BloodTypeId, decimal Hemoglobin, decimal HeightCm, decimal WeightKg,
    Guid InternalMedId, Guid HeartAssessId, Guid SpineAssessId);

/// <summary>Result of completing an onboarding step: the refreshed first-login flag (false once done).</summary>
public sealed record OnboardingStepResultDto(bool MustChangePassword);
```

- [ ] **Step 2: Add the messages** — add to `SwimmerMessages.Success`:

```csharp
        public static string OnboardingPrefillRetrieved(string lang) => lang switch { "ar" => "بيانات البدء", _ => "Onboarding prefill" };
        public static string OnboardingCompleted(string lang) => lang switch { "ar" => "تم إكمال البيانات", _ => "Onboarding completed" };
```

- [ ] **Step 3: Write the failing validator test** — create `OnboardingValidatorTests.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Validators;

public sealed class OnboardingValidatorTests
{
    private static CompleteIdentityVitalsRequest Valid() => new(
        "Ahmed Ali", null, Guid.NewGuid(), new DateOnly(2010, 1, 1), Guid.NewGuid(),
        new DateOnly(2026, 1, 1), null, 14.5m, 175m, 68m,
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private static readonly CompleteIdentityVitalsRequestValidator V = new();

    [Fact] public void Accepts_a_valid_request() => Assert.True(V.Validate(Valid()).IsValid);

    [Fact] public void Rejects_blank_name() => Assert.False(V.Validate(Valid() with { NameEn = "" }).IsValid);

    [Fact] public void Rejects_empty_gender() => Assert.False(V.Validate(Valid() with { GenderId = Guid.Empty }).IsValid);

    [Fact] public void Rejects_empty_training_club() => Assert.False(V.Validate(Valid() with { TrainingClubId = Guid.Empty }).IsValid);

    [Fact] public void Rejects_future_dob() => Assert.False(V.Validate(Valid() with { Dob = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1) }).IsValid);

    [Fact] public void Rejects_future_exam_date() => Assert.False(V.Validate(Valid() with { ExamDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1) }).IsValid);

    [Fact] public void Rejects_empty_assessment() => Assert.False(V.Validate(Valid() with { InternalMedId = Guid.Empty }).IsValid);

    [Fact] public void Rejects_non_positive_height() => Assert.False(V.Validate(Valid() with { HeightCm = 0m }).IsValid);

    [Fact] public void Allows_null_blood_type() => Assert.True(V.Validate(Valid() with { BloodTypeId = null }).IsValid);
}
```

- [ ] **Step 4: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~OnboardingValidatorTests`
Expected: FAIL — `CompleteIdentityVitalsRequestValidator` does not exist.

- [ ] **Step 5: Add the validator** — append to `UserRequestValidators.cs` (mirrors `CreateMedicalExamRequestValidator`; the file already imports FluentValidation, `SwimmerMessages`, and `AppLanguage`):

```csharp
public sealed class CompleteIdentityVitalsRequestValidator : AbstractValidator<CompleteIdentityVitalsRequest>
{
    public CompleteIdentityVitalsRequestValidator()
    {
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.GenderId).NotEqual(Guid.Empty).WithMessage(_ => SwimmerMessages.Errors.GenderRequired(AppLanguage.Current));
        RuleFor(x => x.TrainingClubId).NotEqual(Guid.Empty).WithMessage(_ => SwimmerMessages.Errors.TrainingClubRequired(AppLanguage.Current));
        RuleFor(x => x.Dob)
            .NotEqual(default(DateOnly)).WithMessage(_ => SwimmerMessages.Errors.DobRequired(AppLanguage.Current))
            .LessThan(_ => DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage(_ => SwimmerMessages.Errors.DobInPast(AppLanguage.Current));
        RuleFor(x => x.ExamDate)
            .NotEqual(default(DateOnly)).WithMessage(_ => SwimmerMessages.Errors.ExamDateInvalid(AppLanguage.Current))
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage(_ => SwimmerMessages.Errors.ExamDateInvalid(AppLanguage.Current));
        RuleFor(x => x.InternalMedId).NotEqual(Guid.Empty).WithMessage(_ => SwimmerMessages.Errors.AssessmentRequired(AppLanguage.Current));
        RuleFor(x => x.HeartAssessId).NotEqual(Guid.Empty).WithMessage(_ => SwimmerMessages.Errors.AssessmentRequired(AppLanguage.Current));
        RuleFor(x => x.SpineAssessId).NotEqual(Guid.Empty).WithMessage(_ => SwimmerMessages.Errors.AssessmentRequired(AppLanguage.Current));
        RuleFor(x => x.Hemoglobin).GreaterThan(0m).LessThan(30m).WithMessage(_ => SwimmerMessages.Errors.MeasurementInvalid(AppLanguage.Current));
        RuleFor(x => x.HeightCm).GreaterThan(0m).LessThan(300m).WithMessage(_ => SwimmerMessages.Errors.MeasurementInvalid(AppLanguage.Current));
        RuleFor(x => x.WeightKg).GreaterThan(0m).LessThan(500m).WithMessage(_ => SwimmerMessages.Errors.MeasurementInvalid(AppLanguage.Current));
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~OnboardingValidatorTests`
Expected: PASS.

- [ ] **Step 7: Checkpoint** — tests green; do NOT commit.

---

### Task 5: `SwimmerService` onboarding methods (prefill + atomic complete)

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/ISwimmerService.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs`

**Interfaces:**
- Consumes: `AppUser.CompleteFirstLogin()` (Task 1), `SwimmerProfile.SetTrainingClub` (Task 2), `ISwimmerProfileRepository.GetByUserIdTrackedAsync` (Task 3), the DTOs (Task 4). Existing: `MedicalExam` ctor, `_swimmers.AddExamAsync`, `_swimmers.SaveChangesAsync`, `_users.GetByIdAsync`, `_genders/_clubs/_fitness/_bloodTypes.ExistsAsync`.
- Produces:
  - `Task<OnboardingPrefillDto?> ISwimmerService.GetOnboardingPrefillAsync(Guid userId, CancellationToken ct = default)`
  - `Task<OnboardingStepResultDto?> ISwimmerService.CompleteIdentityVitalsAsync(Guid userId, CompleteIdentityVitalsRequest request, CancellationToken ct = default)`

- [ ] **Step 1: Declare on the interface** — add to `ISwimmerService.cs`:

```csharp
    Task<OnboardingPrefillDto?> GetOnboardingPrefillAsync(Guid userId, CancellationToken ct = default);
    Task<OnboardingStepResultDto?> CompleteIdentityVitalsAsync(Guid userId, CompleteIdentityVitalsRequest request, CancellationToken ct = default);
```

- [ ] **Step 2: Write the failing tests** — add to `SwimmerServiceTests` (the file already has the `Build(...)` factory, `ExamReq()` helper, and `using` block shown here):

```csharp
[Fact]
public async Task GetOnboardingPrefill_returns_null_when_not_a_swimmer()
{
    var (svc, swimmers, _) = Build();
    swimmers.Setup(r => r.GetByUserIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SwimmerProfile?)null);
    Assert.Null(await svc.GetOnboardingPrefillAsync(Guid.NewGuid()));
}

[Fact]
public async Task GetOnboardingPrefill_maps_identity_ids()
{
    var (svc, swimmers, users) = Build();
    var userId = Guid.NewGuid();
    var clubId = Guid.NewGuid();
    var profile = new SwimmerProfile(userId, "SW-0009", clubId);
    swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
    var genderId = Guid.NewGuid();
    users.Setup(u => u.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>()))
         .ReturnsAsync(new AppUser("s.swimmer", "Sam", Guid.NewGuid(), nameAr: "سام", genderId: genderId, dob: new DateOnly(2011, 3, 4)));

    var dto = await svc.GetOnboardingPrefillAsync(userId);

    Assert.NotNull(dto);
    Assert.Equal("SW-0009", dto!.Uid);
    Assert.Equal("Sam", dto.NameEn);
    Assert.Equal("سام", dto.NameAr);
    Assert.Equal(genderId, dto.GenderId);
    Assert.Equal(clubId, dto.TrainingClubId);
}

[Fact]
public async Task CompleteIdentityVitals_returns_null_when_not_a_swimmer()
{
    var (svc, swimmers, _) = Build();
    swimmers.Setup(r => r.GetByUserIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SwimmerProfile?)null);
    Assert.Null(await svc.CompleteIdentityVitalsAsync(Guid.NewGuid(), CompleteReq()));
}

[Fact]
public async Task CompleteIdentityVitals_updates_identity_inserts_exam_and_clears_first_login()
{
    var (svc, swimmers, users) = Build();
    var userId = Guid.NewGuid();
    var profile = new SwimmerProfile(userId, "SW-0001", Guid.NewGuid());
    swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
    var user = new AppUser("s.swimmer", "Old Name", Guid.NewGuid(), email: "s@x.io", genderId: Guid.NewGuid(), dob: new DateOnly(2009, 1, 1), isFirstLogin: true);
    users.Setup(u => u.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
    var newClub = Guid.NewGuid();

    var result = await svc.CompleteIdentityVitalsAsync(userId, CompleteReq() with { NameEn = "New Name", TrainingClubId = newClub });

    Assert.NotNull(result);
    Assert.False(result!.MustChangePassword);
    Assert.Equal("New Name", user.NameEn);
    Assert.Equal("s@x.io", user.Email);          // preserved
    Assert.False(user.IsFirstLogin);             // cleared
    Assert.Equal(newClub, profile.TrainingClubId);
    swimmers.Verify(r => r.AddExamAsync(It.IsAny<MedicalExam>(), It.IsAny<CancellationToken>()), Times.Once);
    swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
}

[Fact]
public async Task CompleteIdentityVitals_accepts_null_blood_type()
{
    var (svc, swimmers, users) = Build();
    var userId = Guid.NewGuid();
    swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SwimmerProfile(userId, "SW-0001", Guid.NewGuid()));
    users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(new AppUser("s", "S", Guid.NewGuid(), email: "s@x.io"));
    var result = await svc.CompleteIdentityVitalsAsync(userId, CompleteReq() with { BloodTypeId = null });
    Assert.NotNull(result);
}

[Fact]
public async Task CompleteIdentityVitals_throws_when_reference_unknown()
{
    var (svc, swimmers, users) = Build(refsExist: false);
    var userId = Guid.NewGuid();
    swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SwimmerProfile(userId, "SW-0001", Guid.NewGuid()));
    users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(new AppUser("s", "S", Guid.NewGuid(), email: "s@x.io"));
    await Assert.ThrowsAnyAsync<Exception>(() => svc.CompleteIdentityVitalsAsync(userId, CompleteReq()));
}

private static CompleteIdentityVitalsRequest CompleteReq() => new(
    "Ahmed Ali", null, Guid.NewGuid(), new DateOnly(2010, 1, 1), Guid.NewGuid(),
    new DateOnly(2026, 1, 1), null, 14.5m, 175m, 68m,
    Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~SwimmerServiceTests`
Expected: FAIL — methods not defined (won't compile).

- [ ] **Step 4: Implement the service methods** — add to `SwimmerService.cs` (uses `InvalidUserException` from `Kheprx.BaseBackend.Identity.Domain.Exceptions`, already referenced elsewhere in the file):

```csharp
    public async Task<OnboardingPrefillDto?> GetOnboardingPrefillAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await _swimmers.GetByUserIdTrackedAsync(userId, ct);
        if (profile is null) return null;
        var user = await _users.GetByIdAsync(profile.UserId, ct);
        if (user is null) return null;
        return new OnboardingPrefillDto(profile.Uid, user.NameEn, user.NameAr, user.GenderId, user.Dob, profile.TrainingClubId);
    }

    public async Task<OnboardingStepResultDto?> CompleteIdentityVitalsAsync(
        Guid userId, CompleteIdentityVitalsRequest request, CancellationToken ct = default)
    {
        var profile = await _swimmers.GetByUserIdTrackedAsync(userId, ct);
        if (profile is null) return null;
        var user = await _users.GetByIdAsync(profile.UserId, ct);
        if (user is null) return null;

        // Reference-existence guards (same throw-on-unknown convention as CreateAsync/CreateExamAsync).
        if (!await _genders.ExistsAsync(request.GenderId, ct)) throw new InvalidUserException("Unknown gender.");
        if (!await _clubs.ExistsAsync(request.TrainingClubId, ct)) throw new InvalidUserException("Unknown training club.");
        if (!await _fitness.ExistsAsync(request.InternalMedId, ct)) throw new InvalidUserException("Unknown internal medicine assessment.");
        if (!await _fitness.ExistsAsync(request.HeartAssessId, ct)) throw new InvalidUserException("Unknown heart assessment.");
        if (!await _fitness.ExistsAsync(request.SpineAssessId, ct)) throw new InvalidUserException("Unknown spine assessment.");
        if (request.BloodTypeId is { } bt && !await _bloodTypes.ExistsAsync(bt, ct)) throw new InvalidUserException("Unknown blood type.");

        // Identity: name_en (+ name_ar preserved as sent), gender, dob. Email + phone preserved.
        user.UpdateProfile(request.NameEn, request.NameAr, user.Email, request.GenderId, request.Dob, user.Phone);
        profile.SetTrainingClub(request.TrainingClubId);

        // Vitals: a new dated medical exam row.
        var exam = new MedicalExam(profile.Id, request.ExamDate, request.InternalMedId, request.HeartAssessId,
            request.SpineAssessId, request.BloodTypeId, request.Hemoglobin, request.HeightCm, request.WeightKg);
        await _swimmers.AddExamAsync(exam, ct);

        // Completing Step 1 completes onboarding for now → clear the forced-first-login flag.
        user.CompleteFirstLogin();

        // Single SaveChanges over the shared IdentityDbContext → identity + exam + flag persist atomically.
        await _swimmers.SaveChangesAsync(ct);
        return new OnboardingStepResultDto(false);
    }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~SwimmerServiceTests`
Expected: PASS.

- [ ] **Step 6: Checkpoint** — tests green; do NOT commit.

---

### Task 6: `SwimmersController` self-service onboarding endpoints

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs`

**Interfaces:**
- Consumes: `ISwimmerService.GetOnboardingPrefillAsync`, `ISwimmerService.CompleteIdentityVitalsAsync` (Task 5); `CurrentUserId()` from `BaseApiController`.
- Produces: HTTP `GET`/`POST /api/swimmers/me/onboarding/identity-vitals` (role `swimmer`).

- [ ] **Step 1: Write the failing tests** — add to `SwimmersControllerTests` (note the `DefaultHttpContext` so `CurrentUserId()` resolves to `Guid.Empty`, mirroring `InBodyReadingsControllerTests`):

```csharp
private static SwimmersController OnboardingController(ISwimmerService svc)
    => new(svc) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

[Fact]
public async Task GetOnboardingPrefill_returns_200_with_dto()
{
    var svc = new Mock<ISwimmerService>();
    svc.Setup(s => s.GetOnboardingPrefillAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
       .ReturnsAsync(new OnboardingPrefillDto("SW-0001", "Sam", null, Guid.NewGuid(), new DateOnly(2010, 1, 1), Guid.NewGuid()));

    var result = await OnboardingController(svc.Object).GetOnboardingPrefill(CancellationToken.None);

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var body = Assert.IsType<ApiResponse<OnboardingPrefillDto>>(ok.Value);
    Assert.True(body.SuccessStatus);
    Assert.Equal("SW-0001", body.Data!.Uid);
}

[Fact]
public async Task GetOnboardingPrefill_returns_404_when_not_a_swimmer()
{
    var svc = new Mock<ISwimmerService>();
    svc.Setup(s => s.GetOnboardingPrefillAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
       .ReturnsAsync((OnboardingPrefillDto?)null);

    var result = await OnboardingController(svc.Object).GetOnboardingPrefill(CancellationToken.None);

    Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(result.Result).StatusCode);
}

[Fact]
public async Task CompleteOnboarding_returns_200_when_ok_404_when_null()
{
    var req = new CompleteIdentityVitalsRequest("Sam", null, Guid.NewGuid(), new DateOnly(2010, 1, 1), Guid.NewGuid(),
        new DateOnly(2026, 1, 1), null, 14.5m, 175m, 68m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    var okSvc = new Mock<ISwimmerService>();
    okSvc.Setup(s => s.CompleteIdentityVitalsAsync(It.IsAny<Guid>(), It.IsAny<CompleteIdentityVitalsRequest>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(new OnboardingStepResultDto(false));
    var okResult = await OnboardingController(okSvc.Object).CompleteOnboardingIdentityVitals(req, CancellationToken.None);
    var ok = Assert.IsType<OkObjectResult>(okResult.Result);
    Assert.False(Assert.IsType<ApiResponse<OnboardingStepResultDto>>(ok.Value).Data!.MustChangePassword);

    var nfSvc = new Mock<ISwimmerService>();
    nfSvc.Setup(s => s.CompleteIdentityVitalsAsync(It.IsAny<Guid>(), It.IsAny<CompleteIdentityVitalsRequest>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync((OnboardingStepResultDto?)null);
    var nfResult = await OnboardingController(nfSvc.Object).CompleteOnboardingIdentityVitals(req, CancellationToken.None);
    Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(nfResult.Result).StatusCode);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter FullyQualifiedName~SwimmersControllerTests`
Expected: FAIL — controller actions not defined (won't compile).

- [ ] **Step 3: Add the endpoints** — add a new region inside `SwimmersController`:

```csharp
    #region Onboarding (self-service) — GET/POST api/swimmers/me/onboarding/identity-vitals

    /// <summary>Prefill for the swimmer's own first-login wizard, Step 1. Swimmer only; resolved from the JWT.</summary>
    /// <response code="200">The prefill.</response>
    /// <response code="404">The caller is not a swimmer / has no profile.</response>
    [HttpGet("me/onboarding/identity-vitals")]
    [Authorize(Roles = "swimmer")]
    [ProducesResponseType(typeof(ApiResponse<OnboardingPrefillDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OnboardingPrefillDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OnboardingPrefillDto>>> GetOnboardingPrefill(CancellationToken ct)
    {
        var dto = await _service.GetOnboardingPrefillAsync(CurrentUserId(), ct);
        if (dto is null)
        {
            var nf = ApiResponse<OnboardingPrefillDto>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<OnboardingPrefillDto>.Success(SwimmerMessages.Success.OnboardingPrefillRetrieved(AppLanguage.Current), dto));
    }

    /// <summary>Completes the swimmer's own first-login Step 1 (identity + first medical exam), clearing first-login. Swimmer only.</summary>
    /// <response code="200">Completed; returns the refreshed first-login flag (false).</response>
    /// <response code="404">The caller is not a swimmer / has no profile.</response>
    [HttpPost("me/onboarding/identity-vitals")]
    [Authorize(Roles = "swimmer")]
    [ProducesResponseType(typeof(ApiResponse<OnboardingStepResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OnboardingStepResultDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OnboardingStepResultDto>>> CompleteOnboardingIdentityVitals(
        CompleteIdentityVitalsRequest request, CancellationToken ct)
    {
        var result = await _service.CompleteIdentityVitalsAsync(CurrentUserId(), request, ct);
        if (result is null)
        {
            var nf = ApiResponse<OnboardingStepResultDto>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<OnboardingStepResultDto>.Success(SwimmerMessages.Success.OnboardingCompleted(AppLanguage.Current), result));
    }

    #endregion
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter FullyQualifiedName~SwimmersControllerTests`
Expected: PASS.

- [ ] **Step 5: Run the whole backend suite** (guards ArchitectureTests + regressions)

Run: `dotnet test backend` (ensure the dev API is stopped first).
Expected: PASS.

- [ ] **Step 6: Checkpoint** — backend green; do NOT commit.

---

## FRONTEND

> All frontend tasks: Jest, run from `frontend/`. Aliases: `@features/*`, `@core/*`.

### Task 7: `swimmer-onboarding` data layer (DTOs, model, repository, wiring)

**Files:**
- Create: `frontend/src/app/features/swimmer-onboarding/domain/model/onboarding.ts`
- Create: `frontend/src/app/features/swimmer-onboarding/data/dto/onboarding-prefill.dto.ts`
- Create: `frontend/src/app/features/swimmer-onboarding/data/dto/complete-identity-vitals.dto.ts`
- Create: `frontend/src/app/features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository.ts`
- Create: `frontend/src/app/features/swimmer-onboarding/data/repositories/swimmer-onboarding.repository.impl.ts`
- Create: `frontend/src/app/features/swimmer-onboarding/data/swimmer-onboarding.providers.ts`
- Create: `frontend/src/app/features/swimmer-onboarding/index.ts`
- Modify: `frontend/src/app/app.config.ts`
- Test: `frontend/src/app/features/swimmer-onboarding/testing/data/repositories/swimmer-onboarding.repository.impl.spec.ts`

**Interfaces:**
- Produces:
  - Model `OnboardingPrefill { uid; nameEn; nameAr: string | null; genderId: string | null; dob: string | null; trainingClubId: string | null }`
  - Model `IdentityVitalsSubmission { nameEn; nameAr: string | null; genderId; dob; trainingClubId; examDate; bloodTypeId: string | null; hemoglobin; heightCm; weightKg; internalMedId; heartAssessId; spineAssessId }`
  - Model `OnboardingResult { mustChangePassword: boolean }`
  - `ISwimmerOnboardingRepository { getPrefill(): Promise<OnboardingPrefillItemDtoRs>; completeIdentityVitals(rq): Promise<CompleteIdentityVitalsItemDtoRs> }` + token `SWIMMER_ONBOARDING_REPOSITORY`
  - `SWIMMER_ONBOARDING_PROVIDERS`

- [ ] **Step 1: Create the domain model** — `onboarding.ts`:

```typescript
// onboarding.ts — swimmer first-login onboarding domain models.
export interface OnboardingPrefill {
  uid: string;
  nameEn: string;
  nameAr: string | null;
  genderId: string | null;
  dob: string | null;
  trainingClubId: string | null;
}

export interface IdentityVitalsSubmission {
  nameEn: string;
  nameAr: string | null;
  genderId: string;
  dob: string;
  trainingClubId: string;
  examDate: string;
  bloodTypeId: string | null;
  hemoglobin: number;
  heightCm: number;
  weightKg: number;
  internalMedId: string;
  heartAssessId: string;
  spineAssessId: string;
}

export interface OnboardingResult { mustChangePassword: boolean; }
```

- [ ] **Step 2: Create the DTOs** — `onboarding-prefill.dto.ts`:

```typescript
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface OnboardingPrefillDtoRs {
  uid: string;
  nameEn: string;
  nameAr: string | null;
  genderId: string | null;
  dob: string | null;
  trainingClubId: string | null;
}
export interface OnboardingPrefillItemDtoRs extends BaseResponseRs<OnboardingPrefillDtoRs> {}

export function isOnboardingPrefillValid(dto: unknown): dto is OnboardingPrefillDtoRs {
  const d = dto as OnboardingPrefillDtoRs;
  return !!d && typeof d.uid === 'string' && typeof d.nameEn === 'string';
}
```

`complete-identity-vitals.dto.ts`:

```typescript
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface CompleteIdentityVitalsDtoRq {
  nameEn: string;
  nameAr: string | null;
  genderId: string;
  dob: string;
  trainingClubId: string;
  examDate: string;
  bloodTypeId: string | null;
  hemoglobin: number;
  heightCm: number;
  weightKg: number;
  internalMedId: string;
  heartAssessId: string;
  spineAssessId: string;
}
export interface OnboardingResultDtoRs { mustChangePassword: boolean; }
export interface CompleteIdentityVitalsItemDtoRs extends BaseResponseRs<OnboardingResultDtoRs> {}
```

- [ ] **Step 3: Create the repository interface + token** — `swimmer-onboarding.repository.ts`:

```typescript
import { InjectionToken } from '@angular/core';
import { OnboardingPrefillItemDtoRs } from '@features/swimmer-onboarding/data/dto/onboarding-prefill.dto';
import { CompleteIdentityVitalsDtoRq, CompleteIdentityVitalsItemDtoRs } from '@features/swimmer-onboarding/data/dto/complete-identity-vitals.dto';

export interface ISwimmerOnboardingRepository {
  getPrefill(): Promise<OnboardingPrefillItemDtoRs>;
  completeIdentityVitals(rq: CompleteIdentityVitalsDtoRq): Promise<CompleteIdentityVitalsItemDtoRs>;
}

export const SWIMMER_ONBOARDING_REPOSITORY = new InjectionToken<ISwimmerOnboardingRepository>('SWIMMER_ONBOARDING_REPOSITORY');
```

- [ ] **Step 4: Create the repository impl** — `swimmer-onboarding.repository.impl.ts`:

```typescript
// swimmer-onboarding.repository.impl.ts — self-service onboarding read + write (JWT-scoped `me` endpoints).
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { ISwimmerOnboardingRepository } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { OnboardingPrefillItemDtoRs } from '@features/swimmer-onboarding/data/dto/onboarding-prefill.dto';
import { CompleteIdentityVitalsDtoRq, CompleteIdentityVitalsItemDtoRs } from '@features/swimmer-onboarding/data/dto/complete-identity-vitals.dto';

@Injectable({ providedIn: 'root' })
export class SwimmerOnboardingRepositoryImpl implements ISwimmerOnboardingRepository {
  private readonly http = inject(HttpClientService);

  getPrefill(): Promise<OnboardingPrefillItemDtoRs> {
    return this.http.get<OnboardingPrefillItemDtoRs>('/api/swimmers/me/onboarding/identity-vitals');
  }
  completeIdentityVitals(rq: CompleteIdentityVitalsDtoRq): Promise<CompleteIdentityVitalsItemDtoRs> {
    return this.http.post<CompleteIdentityVitalsItemDtoRs>('/api/swimmers/me/onboarding/identity-vitals', { body: rq });
  }
}
```

- [ ] **Step 5: Create providers + barrel**

`swimmer-onboarding.providers.ts`:

```typescript
import { Provider } from '@angular/core';
import { SWIMMER_ONBOARDING_REPOSITORY } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { SwimmerOnboardingRepositoryImpl } from '@features/swimmer-onboarding/data/repositories/swimmer-onboarding.repository.impl';

export const SWIMMER_ONBOARDING_PROVIDERS: Provider[] = [
  { provide: SWIMMER_ONBOARDING_REPOSITORY, useClass: SwimmerOnboardingRepositoryImpl },
];
```

`index.ts` (page + viewmodel added in Task 15; add those exports then):

```typescript
export { SWIMMER_ONBOARDING_PROVIDERS } from './data/swimmer-onboarding.providers';
```

- [ ] **Step 6: Register in `app.config.ts`** — add the import and spread `...SWIMMER_ONBOARDING_PROVIDERS,` into the `providers` array (next to `...SWIMMER_PROFILE_PROVIDERS,`):

```typescript
import { SWIMMER_ONBOARDING_PROVIDERS } from '@features/swimmer-onboarding/data/swimmer-onboarding.providers';
// ...
    ...SWIMMER_PROFILE_PROVIDERS,
    ...SWIMMER_ONBOARDING_PROVIDERS,
```

- [ ] **Step 7: Write the failing repo spec** — `swimmer-onboarding.repository.impl.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { SwimmerOnboardingRepositoryImpl } from '@features/swimmer-onboarding/data/repositories/swimmer-onboarding.repository.impl';
import { HttpClientService } from '@core/network/api/http-client';

describe('SwimmerOnboardingRepositoryImpl', () => {
  const http = { get: jest.fn(), post: jest.fn() } as unknown as HttpClientService;
  let repo: SwimmerOnboardingRepositoryImpl;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({ providers: [SwimmerOnboardingRepositoryImpl, { provide: HttpClientService, useValue: http }] });
    repo = TestBed.inject(SwimmerOnboardingRepositoryImpl);
  });

  it('getPrefill GETs the me onboarding endpoint', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: {} });
    await repo.getPrefill();
    expect(http.get).toHaveBeenCalledWith('/api/swimmers/me/onboarding/identity-vitals');
  });

  it('completeIdentityVitals POSTs the me onboarding endpoint with the body', async () => {
    (http.post as jest.Mock).mockResolvedValue({ data: { mustChangePassword: false } });
    const rq = { nameEn: 'Sam', nameAr: null, genderId: 'g1', dob: '2010-01-01', trainingClubId: 'c1', examDate: '2026-01-01', bloodTypeId: null, hemoglobin: 14.5, heightCm: 175, weightKg: 68, internalMedId: 'f1', heartAssessId: 'f1', spineAssessId: 'f1' };
    await repo.completeIdentityVitals(rq);
    expect(http.post).toHaveBeenCalledWith('/api/swimmers/me/onboarding/identity-vitals', { body: rq });
  });
});
```

- [ ] **Step 8: Run the spec**

Run (from `frontend/`): `npx jest swimmer-onboarding/testing/data/repositories`
Expected: PASS.

- [ ] **Step 9: Checkpoint** — spec green; do NOT commit.

---

### Task 8: `GetOnboardingPrefillUseCase`

**Files:**
- Create: `frontend/src/app/features/swimmer-onboarding/domain/usecases/get-onboarding-prefill.use-case.ts`
- Test: `frontend/src/app/features/swimmer-onboarding/testing/domain/usecases/get-onboarding-prefill.use-case.spec.ts`

**Interfaces:**
- Consumes: `SWIMMER_ONBOARDING_REPOSITORY`, `isOnboardingPrefillValid`, `OnboardingPrefill` (Task 7).
- Produces: `GetOnboardingPrefillUseCase extends UseCase<void, OnboardingPrefill>`.

- [ ] **Step 1: Write the failing test** — `get-onboarding-prefill.use-case.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { GetOnboardingPrefillUseCase } from '@features/swimmer-onboarding/domain/usecases/get-onboarding-prefill.use-case';
import { SWIMMER_ONBOARDING_REPOSITORY, ISwimmerOnboardingRepository } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';

function build(repo: Partial<ISwimmerOnboardingRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_ONBOARDING_REPOSITORY, useValue: repo }] });
  return TestBed.inject(GetOnboardingPrefillUseCase);
}

describe('GetOnboardingPrefillUseCase', () => {
  it('maps the prefill DTO to the domain model', async () => {
    const repo = { getPrefill: async () => ({ data: { uid: 'SW-1', nameEn: 'Sam', nameAr: 'سام', genderId: 'g1', dob: '2010-01-01', trainingClubId: 'c1' } }) };
    const r = await build(repo as ISwimmerOnboardingRepository).run();
    expect(r.ok).toBe(true);
    if (r.ok) { expect(r.data.nameEn).toBe('Sam'); expect(r.data.trainingClubId).toBe('c1'); }
  });

  it('fails validation when the prefill is malformed', async () => {
    const repo = { getPrefill: async () => ({ data: { uid: 5 } }) };
    const r = await build(repo as unknown as ISwimmerOnboardingRepository).run();
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run (from `frontend/`): `npx jest get-onboarding-prefill`
Expected: FAIL — use-case not found.

- [ ] **Step 3: Implement** — `get-onboarding-prefill.use-case.ts`:

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_ONBOARDING_REPOSITORY } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { isOnboardingPrefillValid } from '@features/swimmer-onboarding/data/dto/onboarding-prefill.dto';
import { OnboardingPrefill } from '@features/swimmer-onboarding/domain/model/onboarding';

@Injectable({ providedIn: 'root' })
export class GetOnboardingPrefillUseCase extends UseCase<void, OnboardingPrefill> {
  private readonly repo = inject(SWIMMER_ONBOARDING_REPOSITORY);
  constructor() { super('GetOnboardingPrefill'); }

  protected async execute(): Promise<OnboardingPrefill> {
    const res = await this.repo.getPrefill();
    if (!isOnboardingPrefillValid(res.data)) throw new AppError('Invalid onboarding prefill received', 'validation');
    const d = res.data;
    return { uid: d.uid, nameEn: d.nameEn, nameAr: d.nameAr ?? null, genderId: d.genderId ?? null, dob: d.dob ?? null, trainingClubId: d.trainingClubId ?? null };
  }
}
```

- [ ] **Step 4: Run to verify it passes**

Run (from `frontend/`): `npx jest get-onboarding-prefill`
Expected: PASS.

- [ ] **Step 5: Checkpoint** — spec green; do NOT commit.

---

### Task 9: `CompleteIdentityVitalsUseCase`

**Files:**
- Create: `frontend/src/app/features/swimmer-onboarding/domain/usecases/complete-identity-vitals.use-case.ts`
- Test: `frontend/src/app/features/swimmer-onboarding/testing/domain/usecases/complete-identity-vitals.use-case.spec.ts`

**Interfaces:**
- Consumes: `SWIMMER_ONBOARDING_REPOSITORY`, `IdentityVitalsSubmission` (Task 7).
- Produces: `CompleteIdentityVitalsUseCase extends UseCase<IdentityVitalsSubmission, void>`.

- [ ] **Step 1: Write the failing test** — `complete-identity-vitals.use-case.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { CompleteIdentityVitalsUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-identity-vitals.use-case';
import { SWIMMER_ONBOARDING_REPOSITORY, ISwimmerOnboardingRepository } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { IdentityVitalsSubmission } from '@features/swimmer-onboarding/domain/model/onboarding';

const SUBMISSION: IdentityVitalsSubmission = {
  nameEn: 'Sam', nameAr: null, genderId: 'g1', dob: '2010-01-01', trainingClubId: 'c1',
  examDate: '2026-01-01', bloodTypeId: null, hemoglobin: 14.5, heightCm: 175, weightKg: 68,
  internalMedId: 'f1', heartAssessId: 'f1', spineAssessId: 'f1',
};

function build(repo: Partial<ISwimmerOnboardingRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_ONBOARDING_REPOSITORY, useValue: repo }] });
  return TestBed.inject(CompleteIdentityVitalsUseCase);
}

describe('CompleteIdentityVitalsUseCase', () => {
  it('posts the submission and succeeds', async () => {
    const post = jest.fn().mockResolvedValue({ data: { mustChangePassword: false } });
    const uc = build({ completeIdentityVitals: post } as unknown as ISwimmerOnboardingRepository);
    const r = await uc.run(SUBMISSION);
    expect(r.ok).toBe(true);
    expect(post).toHaveBeenCalledWith(expect.objectContaining({ nameEn: 'Sam', bloodTypeId: null, hemoglobin: 14.5 }));
  });

  it('fails when the repository throws', async () => {
    const uc = build({ completeIdentityVitals: async () => { throw new Error('boom'); } } as unknown as ISwimmerOnboardingRepository);
    const r = await uc.run(SUBMISSION);
    expect(r.ok).toBe(false);
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run (from `frontend/`): `npx jest complete-identity-vitals`
Expected: FAIL — use-case not found.

- [ ] **Step 3: Implement** — `complete-identity-vitals.use-case.ts`:

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_ONBOARDING_REPOSITORY } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { IdentityVitalsSubmission } from '@features/swimmer-onboarding/domain/model/onboarding';

@Injectable({ providedIn: 'root' })
export class CompleteIdentityVitalsUseCase extends UseCase<IdentityVitalsSubmission, void> {
  private readonly repo = inject(SWIMMER_ONBOARDING_REPOSITORY);
  constructor() { super('CompleteIdentityVitals'); }

  protected async execute(input: IdentityVitalsSubmission): Promise<void> {
    await this.repo.completeIdentityVitals({
      nameEn: input.nameEn,
      nameAr: input.nameAr ?? null,
      genderId: input.genderId,
      dob: input.dob,
      trainingClubId: input.trainingClubId,
      examDate: input.examDate,
      bloodTypeId: input.bloodTypeId ?? null,
      hemoglobin: input.hemoglobin,
      heightCm: input.heightCm,
      weightKg: input.weightKg,
      internalMedId: input.internalMedId,
      heartAssessId: input.heartAssessId,
      spineAssessId: input.spineAssessId,
    });
  }
}
```

- [ ] **Step 4: Run to verify it passes**

Run (from `frontend/`): `npx jest complete-identity-vitals`
Expected: PASS.

- [ ] **Step 5: Checkpoint** — spec green; do NOT commit.

---

### Task 10: Add the `swimmer` role to the frontend role set

**Files:**
- Modify: `frontend/src/app/core/domain/roles/user-role.ts`
- Modify: `frontend/src/app/core/domain/roles/role-labels.ts`
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`
- Test: `frontend/src/app/core/domain/roles/testing/role-labels.spec.ts`

**Interfaces:**
- Produces: `type UserRole = 'head_coach' | 'captain' | 'swimmer'`; `ROLE_LABELS.swimmer`; i18n key `roles.swimmer`.

- [ ] **Step 1: Update the failing test** — set `role-labels.spec.ts` to expect `swimmer`:

```typescript
import { ROLE_LABELS } from '@core/domain/roles/role-labels';

describe('ROLE_LABELS', () => {
  it('maps each role to its i18n key', () => {
    expect(ROLE_LABELS.head_coach).toBe('roles.head_coach');
    expect(ROLE_LABELS.captain).toBe('roles.captain');
    expect(ROLE_LABELS.swimmer).toBe('roles.swimmer');
    expect(Object.keys(ROLE_LABELS).sort()).toEqual(['captain', 'head_coach', 'swimmer']);
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run (from `frontend/`): `npx jest role-labels`
Expected: FAIL — `ROLE_LABELS.swimmer` undefined; type error on the key set.

- [ ] **Step 3: Implement**

`user-role.ts`:

```typescript
// UserRole is the swimming role set. Head coach has broader access than captain; swimmers self-serve.
export type UserRole = 'head_coach' | 'captain' | 'swimmer';
```

`role-labels.ts` — add the entry:

```typescript
  swimmer: 'roles.swimmer',
```

`en.json` — extend the `roles` block: `"roles": { "head_coach": "Head Coach", "captain": "Captain", "swimmer": "Swimmer" },`

`ar.json` — extend the `roles` block: add `"swimmer": "سبّاح"` alongside the existing `head_coach`/`captain` Arabic labels.

- [ ] **Step 4: Run to verify it passes**

Run (from `frontend/`): `npx jest role-labels`
Expected: PASS.

- [ ] **Step 5: Checkpoint** — spec green; do NOT commit.

---

### Task 11: `AuthSessionStore.markOnboardingComplete()`

**Files:**
- Modify: `frontend/src/app/features/auth/presentation/auth-session.store.ts`
- Test: `frontend/src/app/features/auth/testing/presentation/auth-session.store.spec.ts`

**Interfaces:**
- Produces: `void AuthSessionStore.markOnboardingComplete()` — patches the current session so `mustChangePassword()` becomes `false` (no-op when there's no session).

- [ ] **Step 1: Write the failing test** — add to `auth-session.store.spec.ts`:

```typescript
it('markOnboardingComplete flips mustChangePassword to false', async () => {
  login.run.mockResolvedValue(ok({ ...session, principal: { role: 'swimmer', userId: 'USR-SWIM' }, mustChangePassword: true }));
  const store = make();
  await store.signIn('swimmer@example.com', 'Oasis2026!', 'swimmer');
  expect(store.mustChangePassword()).toBe(true);
  store.markOnboardingComplete();
  expect(store.mustChangePassword()).toBe(false);
  expect(store.isAuthenticated()).toBe(true);
});
```

- [ ] **Step 2: Run to verify it fails**

Run (from `frontend/`): `npx jest auth-session.store`
Expected: FAIL — `markOnboardingComplete` is not a function.

- [ ] **Step 3: Implement** — add to `AuthSessionStore` (after `changePassword`):

```typescript
  /** Marks first-login onboarding complete locally: the swimmer finished the wizard, so the
   *  backend cleared IsFirstLogin. Patches the session flag so firstLoginGuard/onboardingGuard
   *  stop pinning the user to /onboarding — no re-login or token reissue needed. */
  markOnboardingComplete(): void {
    const s = this._session();
    if (s) this._session.set({ ...s, mustChangePassword: false });
  }
```

- [ ] **Step 4: Run to verify it passes**

Run (from `frontend/`): `npx jest auth-session.store`
Expected: PASS.

- [ ] **Step 5: Checkpoint** — spec green; do NOT commit.

---

### Task 12: `onboardingGuard` + role-aware `firstLoginGuard`

**Files:**
- Modify: `frontend/src/app/features/auth/presentation/auth.guard.ts`
- Test: `frontend/src/app/features/auth/testing/presentation/auth.guard.spec.ts`

**Interfaces:**
- Consumes: `AuthSessionStore.isAuthenticated()`, `.role()`, `.mustChangePassword()`.
- Produces: `onboardingGuard: CanActivateFn`; updated `firstLoginGuard` (swimmer → `/onboarding`, others → `/change-password`).

- [ ] **Step 1: Write the failing tests** — add to `auth.guard.spec.ts` (import `onboardingGuard`):

```typescript
describe('onboardingGuard', () => {
  it('allows a first-login swimmer', () => {
    const { injector } = setup({ isAuthenticated: () => true, role: () => 'swimmer', mustChangePassword: () => true } as Partial<AuthSessionStore>);
    expect(run(injector, onboardingGuard)).toBe(true);
  });
  it('redirects an already-onboarded swimmer to /home', () => {
    const { injector, router } = setup({ isAuthenticated: () => true, role: () => 'swimmer', mustChangePassword: () => false } as Partial<AuthSessionStore>);
    expect(run(injector, onboardingGuard)).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/home']);
  });
  it('redirects a non-swimmer to /home', () => {
    const { injector, router } = setup({ isAuthenticated: () => true, role: () => 'head_coach', mustChangePassword: () => true } as Partial<AuthSessionStore>);
    expect(run(injector, onboardingGuard)).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/home']);
  });
  it('redirects an anonymous user to /login', () => {
    const { injector, router } = setup({ isAuthenticated: () => false } as Partial<AuthSessionStore>);
    expect(run(injector, onboardingGuard)).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });
});

describe('firstLoginGuard (role-aware)', () => {
  it('pins a first-login swimmer to /onboarding', () => {
    const { injector, router } = setup({ mustChangePassword: () => true, role: () => 'swimmer' } as Partial<AuthSessionStore>);
    expect(run(injector, firstLoginGuard)).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/onboarding']);
  });
});
```

(The existing `firstLoginGuard` tests keep passing: their mock has `role` undefined → not swimmer → `/change-password`.)

- [ ] **Step 2: Run to verify it fails**

Run (from `frontend/`): `npx jest auth.guard`
Expected: FAIL — `onboardingGuard` not exported; swimmer case not handled.

- [ ] **Step 3: Implement** — in `auth.guard.ts`, replace `firstLoginGuard` and add `onboardingGuard`:

```typescript
// firstLoginGuard: an authenticated user still flagged mustChangePassword is pinned to their
// first-login destination until it clears — swimmers to /onboarding (finish the wizard),
// everyone else to /change-password. Applied to shell children EXCEPT those two routes.
export const firstLoginGuard: CanActivateFn = () => {
  const auth = inject(AuthSessionStore);
  const router = inject(Router);
  if (auth.mustChangePassword()) {
    router.navigate([auth.role() === 'swimmer' ? '/onboarding' : '/change-password']);
    return false;
  }
  return true;
};

// onboardingGuard: the /onboarding wizard is only for a first-login swimmer. Anonymous → /login;
// a non-swimmer or an already-onboarded swimmer → /home.
export const onboardingGuard: CanActivateFn = () => {
  const auth = inject(AuthSessionStore);
  const router = inject(Router);
  if (!auth.isAuthenticated()) { router.navigate(['/login']); return false; }
  if (auth.role() !== 'swimmer' || !auth.mustChangePassword()) { router.navigate(['/home']); return false; }
  return true;
};
```

- [ ] **Step 4: Run to verify it passes**

Run (from `frontend/`): `npx jest auth.guard`
Expected: PASS (new + existing).

- [ ] **Step 5: Checkpoint** — spec green; do NOT commit.

---

### Task 13: Role-aware post-login navigation

**Files:**
- Modify: `frontend/src/app/features/auth/presentation/pages/login/login.viewmodel.ts`
- Test: `frontend/src/app/features/auth/testing/presentation/pages/login/login.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `AuthSessionStore.role()` (now includes `swimmer`), the role-aware routing.
- Produces: on `mustChangePassword`, swimmer → `/onboarding`, others → `/change-password`; `LANDING_ROUTE_BY_ROLE.swimmer = '/home'`.

- [ ] **Step 1: Write the failing test** — add to `login.viewmodel.spec.ts`:

```typescript
it('routes a first-login swimmer to /onboarding', async () => {
  const swimmerForced: AuthSession = { tokens: { accessToken: 'a', refreshToken: 'r' }, principal: { role: 'swimmer', userId: 'USR-SWIM' }, mustChangePassword: true };
  (auth.signIn as jest.Mock).mockResolvedValue(ok(swimmerForced));
  (auth.role as jest.Mock).mockReturnValue('swimmer');
  await vm.submit();
  expect(router.navigate).toHaveBeenCalledWith(['/onboarding']);
});
```

- [ ] **Step 2: Run to verify it fails**

Run (from `frontend/`): `npx jest login.viewmodel`
Expected: FAIL — currently routes to `/change-password` for any forced login.

- [ ] **Step 3: Implement** — in `login.viewmodel.ts`:

Extend the landing map:

```typescript
const LANDING_ROUTE_BY_ROLE: Record<UserRole, string> = {
  head_coach: '/home',
  captain: '/home',
  swimmer: '/home',
};
```

Make the forced-login branch role-aware inside `submit()`:

```typescript
    if (r.ok) {
      if (r.data.mustChangePassword) {
        void this.router.navigate([this.auth.role() === 'swimmer' ? '/onboarding' : '/change-password']);
      } else {
        const role = this.auth.role();
        void this.router.navigate([role ? LANDING_ROUTE_BY_ROLE[role] : '/home']);
      }
    } else {
```

- [ ] **Step 4: Run to verify it passes**

Run (from `frontend/`): `npx jest login.viewmodel`
Expected: PASS (new + existing, including the captain/coach `/change-password` case).

- [ ] **Step 5: Checkpoint** — spec green; do NOT commit.

---

### Task 14: `OnboardingViewModel`

**Files:**
- Create: `frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding.viewmodel.ts`
- Test: `frontend/src/app/features/swimmer-onboarding/testing/presentation/pages/onboarding/onboarding.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `GetOnboardingPrefillUseCase` (Task 8), `CompleteIdentityVitalsUseCase` (Task 9), reference use-cases, `AuthSessionStore.markOnboardingComplete()` (Task 11), `NotificationService`, `TranslateService`, `Router`.
- Produces: `OnboardingViewModel` (`@Injectable()`, provided in the route) with signals `loading/error/submitting`, lookup signals, draft field signals, `canSubmit`, `load()`, `submit()`.

- [ ] **Step 1: Write the failing test** — `onboarding.viewmodel.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { OnboardingViewModel } from '@features/swimmer-onboarding/presentation/pages/onboarding/onboarding.viewmodel';
import { GetOnboardingPrefillUseCase } from '@features/swimmer-onboarding/domain/usecases/get-onboarding-prefill.use-case';
import { CompleteIdentityVitalsUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-identity-vitals.use-case';
import { LoadClubsUseCase, LoadGendersUseCase, LoadBloodTypesUseCase } from '@features/reference';
import { LoadFitnessAssessmentsUseCase } from '@features/reference/domain/usecases/load-fitness-assessments.use-case';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';

const PREFILL = { uid: 'SW-1', nameEn: 'Sam', nameAr: 'سام', genderId: 'g1', dob: '2010-01-01', trainingClubId: 'c1' };
const LOOKUP = [{ id: 'x', nameEn: 'X', nameAr: null }];

function build(overrides: { prefill?: unknown; complete?: unknown } = {}) {
  const getPrefill = { run: jest.fn().mockResolvedValue(ok(PREFILL)) };
  const complete = { run: jest.fn().mockResolvedValue(overrides.complete ?? ok(undefined)) };
  if (overrides.prefill) getPrefill.run.mockResolvedValue(overrides.prefill);
  const lookups = { run: jest.fn().mockResolvedValue(ok(LOOKUP)) };
  const auth = { markOnboardingComplete: jest.fn() } as unknown as AuthSessionStore;
  const router = { navigate: jest.fn() } as unknown as Router;
  const notify = { success: jest.fn(), error: jest.fn() } as unknown as NotificationService;
  const i18n = { t: (k: string) => k } as unknown as TranslateService;
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      OnboardingViewModel,
      { provide: GetOnboardingPrefillUseCase, useValue: getPrefill },
      { provide: CompleteIdentityVitalsUseCase, useValue: complete },
      { provide: LoadClubsUseCase, useValue: lookups },
      { provide: LoadGendersUseCase, useValue: lookups },
      { provide: LoadBloodTypesUseCase, useValue: lookups },
      { provide: LoadFitnessAssessmentsUseCase, useValue: lookups },
      { provide: AuthSessionStore, useValue: auth },
      { provide: Router, useValue: router },
      { provide: NotificationService, useValue: notify },
      { provide: TranslateService, useValue: i18n },
    ],
  });
  return { vm: TestBed.inject(OnboardingViewModel), complete, auth, router, notify };
}

function fillValid(vm: OnboardingViewModel) {
  vm.examDate.set('2026-01-01'); vm.internalMedId.set('f1'); vm.heartAssessId.set('f1'); vm.spineAssessId.set('f1');
  vm.hemoglobin.set('14.5'); vm.heightCm.set('175'); vm.weightKg.set('68');
}

describe('OnboardingViewModel', () => {
  it('load() seeds identity drafts and lookups', async () => {
    const { vm } = build();
    await vm.load();
    expect(vm.nameEn()).toBe('Sam');
    expect(vm.genderId()).toBe('g1');
    expect(vm.trainingClubId()).toBe('c1');
    expect(vm.genders().length).toBe(1);
  });

  it('canSubmit is false until required fields are set', async () => {
    const { vm } = build();
    await vm.load();
    expect(vm.canSubmit()).toBe(false);
    fillValid(vm);
    expect(vm.canSubmit()).toBe(true);
  });

  it('submit() success marks onboarding complete and navigates to /home', async () => {
    const { vm, complete, auth, router, notify } = build();
    await vm.load();
    fillValid(vm);
    await vm.submit();
    expect(complete.run).toHaveBeenCalledWith(expect.objectContaining({ nameEn: 'Sam', nameAr: 'سام', bloodTypeId: null, hemoglobin: 14.5 }));
    expect(auth.markOnboardingComplete).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/home']);
    expect(notify.success).toHaveBeenCalled();
  });

  it('submit() failure toasts an error and does not navigate', async () => {
    const { vm, auth, router, notify } = build({ complete: fail(new AppError('nope', 'network')) });
    await vm.load();
    fillValid(vm);
    await vm.submit();
    expect(auth.markOnboardingComplete).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
    expect(notify.error).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run (from `frontend/`): `npx jest onboarding.viewmodel`
Expected: FAIL — viewmodel not found.

- [ ] **Step 3: Implement** — `onboarding.viewmodel.ts`:

```typescript
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { GetOnboardingPrefillUseCase } from '@features/swimmer-onboarding/domain/usecases/get-onboarding-prefill.use-case';
import { CompleteIdentityVitalsUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-identity-vitals.use-case';
import { LoadClubsUseCase, LoadGendersUseCase, LoadBloodTypesUseCase } from '@features/reference';
import { LoadFitnessAssessmentsUseCase } from '@features/reference/domain/usecases/load-fitness-assessments.use-case';
import { LookupItem } from '@features/reference/domain/model/reference';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';

@Injectable()
export class OnboardingViewModel {
  private readonly getPrefillUc = inject(GetOnboardingPrefillUseCase);
  private readonly completeUc = inject(CompleteIdentityVitalsUseCase);
  private readonly loadClubs = inject(LoadClubsUseCase);
  private readonly loadGenders = inject(LoadGendersUseCase);
  private readonly loadBloodTypes = inject(LoadBloodTypesUseCase);
  private readonly loadFitness = inject(LoadFitnessAssessmentsUseCase);
  private readonly auth = inject(AuthSessionStore);
  private readonly router = inject(Router);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  readonly loading = signal(false);
  readonly error = signal(false);
  readonly submitting = signal(false);

  readonly clubs = signal<LookupItem[]>([]);
  readonly genders = signal<LookupItem[]>([]);
  readonly bloodTypes = signal<LookupItem[]>([]);
  readonly fitnessAssessments = signal<LookupItem[]>([]);

  // Identity (pre-filled, editable).
  readonly nameEn = signal('');
  readonly genderId = signal('');
  readonly dob = signal('');
  readonly trainingClubId = signal('');
  // Exam + vitals (new).
  readonly examDate = signal('');
  readonly bloodTypeId = signal('');
  readonly hemoglobin = signal('');
  readonly heightCm = signal('');
  readonly weightKg = signal('');
  readonly internalMedId = signal('');
  readonly heartAssessId = signal('');
  readonly spineAssessId = signal('');

  // name_ar is not shown (single "Full Name" field) — preserved from prefill and sent back untouched.
  private nameArOriginal: string | null = null;

  readonly canSubmit = computed(() =>
    this.nameEn().trim().length > 0 &&
    this.genderId().length > 0 &&
    this.dob().length > 0 &&
    this.trainingClubId().length > 0 &&
    this.examDate().length > 0 &&
    this.internalMedId().length > 0 &&
    this.heartAssessId().length > 0 &&
    this.spineAssessId().length > 0 &&
    Number(this.hemoglobin()) > 0 &&
    Number(this.heightCm()) > 0 &&
    Number(this.weightKg()) > 0);

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(false);
    const [prefill, clubs, genders, blood, fitness] = await Promise.all([
      this.getPrefillUc.run(),
      this.loadClubs.run(),
      this.loadGenders.run(),
      this.loadBloodTypes.run(),
      this.loadFitness.run(),
    ]);
    if (clubs.ok) this.clubs.set(clubs.data);
    if (genders.ok) this.genders.set(genders.data);
    if (blood.ok) this.bloodTypes.set(blood.data);
    if (fitness.ok) this.fitnessAssessments.set(fitness.data);
    if (prefill.ok) {
      this.nameEn.set(prefill.data.nameEn);
      this.nameArOriginal = prefill.data.nameAr;
      this.genderId.set(prefill.data.genderId ?? '');
      this.dob.set(prefill.data.dob ?? '');
      this.trainingClubId.set(prefill.data.trainingClubId ?? '');
    } else {
      this.error.set(true);
    }
    this.loading.set(false);
  }

  async submit(): Promise<void> {
    if (!this.canSubmit() || this.submitting()) return;
    this.submitting.set(true);
    const r = await this.completeUc.run({
      nameEn: this.nameEn().trim(),
      nameAr: this.nameArOriginal,
      genderId: this.genderId(),
      dob: this.dob(),
      trainingClubId: this.trainingClubId(),
      examDate: this.examDate(),
      bloodTypeId: this.bloodTypeId() || null,
      hemoglobin: Number(this.hemoglobin()),
      heightCm: Number(this.heightCm()),
      weightKg: Number(this.weightKg()),
      internalMedId: this.internalMedId(),
      heartAssessId: this.heartAssessId(),
      spineAssessId: this.spineAssessId(),
    });
    this.submitting.set(false);
    if (r.ok) {
      this.auth.markOnboardingComplete();
      this.notify.success(this.i18n.t('onboarding.completed'));
      void this.router.navigate(['/home']);
    } else {
      this.notify.error(this.i18n.t('onboarding.saveFailed'));
    }
  }
}
```

- [ ] **Step 4: Run to verify it passes**

Run (from `frontend/`): `npx jest onboarding.viewmodel`
Expected: PASS.

- [ ] **Step 5: Checkpoint** — spec green; do NOT commit.

---

### Task 15: `OnboardingWizardPage`, i18n block, and route

**Files:**
- Create: `frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding-wizard.page.ts`
- Create: `frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding-wizard.page.html`
- Modify: `frontend/src/app/features/swimmer-onboarding/index.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`

**Interfaces:**
- Consumes: `OnboardingViewModel` (Task 14), shared UI controls (`app-text-field`, `app-select-field`, `app-date-field`), `TranslatePipe`, `onboardingGuard` (Task 12).
- Produces: standalone `OnboardingWizardPage`; route `path: 'onboarding'`.

- [ ] **Step 1: Add the i18n block** — add an `onboarding` block to `en.json`:

```json
  "onboarding": {
    "title": "Join Oasis Academy",
    "subtitle": "Let's get you set up for the pool",
    "steps": { "identity": "Identity & Vitals", "guardian": "Guardian & Medical", "physiological": "Physiological", "inbody": "InBody", "done": "Done" },
    "sectionTitle": "Identity & Vitals",
    "fullName": "Full Name",
    "sport": "Sport",
    "sportLocked": "Swimming (Locked)",
    "trainingClub": "Training Club",
    "gender": "Gender",
    "dob": "Date of Birth",
    "lastExamDate": "Last Examination Date",
    "internalMed": "Internal Medicine Result",
    "heart": "Heart Assessment",
    "spine": "Spine Assessment",
    "bloodType": "Blood Type",
    "hemoglobin": "Hemoglobin (g/dL)",
    "height": "Height (cm)",
    "weight": "Weight (kg)",
    "selectPlaceholder": "Select…",
    "next": "Next Step",
    "completed": "Welcome aboard! Your profile is set up.",
    "saveFailed": "Could not save your details. Please try again.",
    "loadFailed": "Could not load your details. Please try again."
  },
```

Add the mirrored Arabic block to `ar.json` (same keys; Arabic values, e.g. `"title": "انضم إلى أكاديمية الواحة"`, `"next": "الخطوة التالية"`, `"sectionTitle": "الهوية والمؤشرات الحيوية"`, etc.).

- [ ] **Step 2: Create the page component** — `onboarding-wizard.page.ts`:

```typescript
import { Component, OnInit, inject } from '@angular/core';
import { TextFieldComponent } from '@core/ui/components/text-field.component';
import { SelectFieldComponent } from '@core/ui/components/select-field.component';
import { DateFieldComponent } from '@core/ui/components/date-field.component';
import { TranslatePipe, LanguageStore, type Lang } from '@core/i18n';
import { OnboardingViewModel } from './onboarding.viewmodel';

@Component({
  selector: 'app-onboarding-wizard-page',
  standalone: true,
  imports: [TextFieldComponent, SelectFieldComponent, DateFieldComponent, TranslatePipe],
  templateUrl: './onboarding-wizard.page.html',
})
export class OnboardingWizardPage implements OnInit {
  protected readonly vm = inject(OnboardingViewModel);
  private readonly language = inject(LanguageStore);
  protected readonly lang = this.language.lang;

  ngOnInit(): void { void this.vm.load(); }
  setLang(lang: Lang): void { this.language.set(lang); }
}
```

- [ ] **Step 3: Create the template** — `onboarding-wizard.page.html` (full-screen; 5-step strip with only Step 1 active; two-way bindings to the viewmodel signals via the shared controls' `value` model):

```html
<div class="min-h-screen bg-background p-4 md:p-8 flex flex-col items-center">
  <div class="w-full max-w-4xl">
    <header class="mb-8 text-center">
      <h1 class="text-3xl font-bold text-ink">{{ 'onboarding.title' | translate }}</h1>
      <p class="text-muted">{{ 'onboarding.subtitle' | translate }}</p>
    </header>

    <!-- Step strip: only Identity & Vitals is active this pass -->
    <ol class="mb-8 flex items-center justify-between text-sm">
      <li class="font-bold text-primary">1. {{ 'onboarding.steps.identity' | translate }}</li>
      <li class="text-muted" aria-disabled="true">2. {{ 'onboarding.steps.guardian' | translate }}</li>
      <li class="text-muted" aria-disabled="true">3. {{ 'onboarding.steps.physiological' | translate }}</li>
      <li class="text-muted" aria-disabled="true">4. {{ 'onboarding.steps.inbody' | translate }}</li>
      <li class="text-muted" aria-disabled="true">5. {{ 'onboarding.steps.done' | translate }}</li>
    </ol>

    <section class="rounded-2xl border border-border bg-card p-6 md:p-8">
      <h2 class="mb-6 text-2xl font-bold text-ink">{{ 'onboarding.sectionTitle' | translate }}</h2>

      @if (vm.error()) {
        <p class="text-red-600">{{ 'onboarding.loadFailed' | translate }}</p>
      }

      <!-- Two-way binding to plain signals uses the explicit [value]/(valueChange) pair
           (the shared controls expose a model('value'); banana-box [(value)] would need a
           WritableSignal target and does NOT work against `vm.x()` calls). -->
      <div class="grid grid-cols-1 gap-6 md:grid-cols-3">
        <app-text-field [label]="'onboarding.fullName' | translate" [value]="vm.nameEn()" (valueChange)="vm.nameEn.set($event)"></app-text-field>

        <!-- Sport is locked (single-sport app): a read-only display, not an editable field. -->
        <label class="flex flex-col gap-1">
          <span class="text-sm font-bold text-ink">{{ 'onboarding.sport' | translate }}</span>
          <div class="flex h-10 items-center rounded-md border border-input bg-muted px-3 text-sm text-muted">{{ 'onboarding.sportLocked' | translate }}</div>
        </label>

        <app-select-field [label]="'onboarding.trainingClub' | translate" [placeholder]="'onboarding.selectPlaceholder' | translate" [options]="vm.clubs()" [value]="vm.trainingClubId()" (valueChange)="vm.trainingClubId.set($event)"></app-select-field>

        <app-select-field [label]="'onboarding.gender' | translate" [placeholder]="'onboarding.selectPlaceholder' | translate" [options]="vm.genders()" [value]="vm.genderId()" (valueChange)="vm.genderId.set($event)"></app-select-field>
        <app-date-field [label]="'onboarding.dob' | translate" [value]="vm.dob()" (valueChange)="vm.dob.set($event)"></app-date-field>
        <app-date-field [label]="'onboarding.lastExamDate' | translate" [value]="vm.examDate()" (valueChange)="vm.examDate.set($event)"></app-date-field>

        <app-select-field [label]="'onboarding.internalMed' | translate" [placeholder]="'onboarding.selectPlaceholder' | translate" [options]="vm.fitnessAssessments()" [value]="vm.internalMedId()" (valueChange)="vm.internalMedId.set($event)"></app-select-field>
        <app-select-field [label]="'onboarding.heart' | translate" [placeholder]="'onboarding.selectPlaceholder' | translate" [options]="vm.fitnessAssessments()" [value]="vm.heartAssessId()" (valueChange)="vm.heartAssessId.set($event)"></app-select-field>
        <app-select-field [label]="'onboarding.spine' | translate" [placeholder]="'onboarding.selectPlaceholder' | translate" [options]="vm.fitnessAssessments()" [value]="vm.spineAssessId()" (valueChange)="vm.spineAssessId.set($event)"></app-select-field>

        <app-select-field [label]="'onboarding.bloodType' | translate" [placeholder]="'onboarding.selectPlaceholder' | translate" [options]="vm.bloodTypes()" [value]="vm.bloodTypeId()" (valueChange)="vm.bloodTypeId.set($event)"></app-select-field>
        <app-text-field [label]="'onboarding.hemoglobin' | translate" [value]="vm.hemoglobin()" (valueChange)="vm.hemoglobin.set($event)"></app-text-field>
        <app-text-field [label]="'onboarding.height' | translate" [value]="vm.heightCm()" (valueChange)="vm.heightCm.set($event)"></app-text-field>
        <app-text-field [label]="'onboarding.weight' | translate" [value]="vm.weightKg()" (valueChange)="vm.weightKg.set($event)"></app-text-field>
      </div>

      <div class="mt-8 flex justify-end">
        <button
          type="button"
          class="rounded-md bg-primary px-5 py-2 text-primary-foreground disabled:opacity-50"
          [disabled]="!vm.canSubmit() || vm.submitting()"
          (click)="vm.submit()">
          {{ 'onboarding.next' | translate }}
        </button>
      </div>
    </section>
  </div>
</div>
```

- [ ] **Step 4: Export from the barrel** — add to `frontend/src/app/features/swimmer-onboarding/index.ts`:

```typescript
export { OnboardingWizardPage } from './presentation/pages/onboarding/onboarding-wizard.page';
export { OnboardingViewModel } from './presentation/pages/onboarding/onboarding.viewmodel';
```

- [ ] **Step 5: Register the route** — in `app.routes.ts`, import the guard + viewmodel and add a **top-level** route (sibling of `login`, before the `''` layout route):

```typescript
import { authGuard, firstLoginGuard, roleGuard, onboardingGuard } from '@features/auth/presentation/auth.guard';
import { OnboardingViewModel } from '@features/swimmer-onboarding';
// ...
  {
    path: 'onboarding',
    canActivate: [authGuard, onboardingGuard],
    loadComponent: () => import('@features/swimmer-onboarding').then((m) => m.OnboardingWizardPage),
    providers: [OnboardingViewModel],
  },
```

- [ ] **Step 6: Type-check + run the full frontend suite**

Run (from `frontend/`): `npx tsc -p tsconfig.app.json --noEmit` then `npx jest`
Expected: no type errors; all specs PASS (including `app.routes.spec.ts`).

- [ ] **Step 7: Manual smoke (optional, requires the app running).** Sign in as a first-login swimmer (email + password, role Swimmer) → lands on `/onboarding` → the step strip shows Identity & Vitals active → fields pre-filled → complete → lands on `/home`; signing in again goes straight to `/home` (onboarding no longer forced).

- [ ] **Step 8: Checkpoint** — frontend green; do NOT commit.

---

## Done-When

- Backend: `dotnet test backend` all green (Identity + Api unit tests, ArchitectureTests).
- Frontend: `npx jest` all green; `npx tsc … --noEmit` clean.
- A first-login swimmer signs in → onboarding wizard → completes Step 1 → dashboard; `IsFirstLogin` cleared; re-login skips onboarding. Coaches/captains still hit `/change-password` on their first login.
- Nothing committed (user commits manually).
