# Swimmer Onboarding — Step 2 (Guardian & Medical) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the swimmer first-login wizard into a real two-step flow where **Step 2 (Guardian & Medical)** is the finish line — saving both parents to `athlete.guardian` and a Yes-only medical history to `health.observation`, then clearing `IsFirstLogin`.

**Architecture:** Step 1's server call stops clearing `IsFirstLogin`; both server calls now fire at the final Step 2 submit (deferred, all-or-nothing). A single new self-service endpoint `POST /api/swimmers/me/onboarding/guardian-medical` is orchestrated at the **API/controller composition root** (Approach 1): it upserts Father+Mother guardians via the Identity `ISwimmerService`, inserts one observation per Yes-category via the Health `IObservationService`, then clears `IsFirstLogin` **last** as the commit point. No schema change, no migration, no seed change.

**Tech Stack:** .NET (C#, modular monolith: Identity + Health modules, FluentValidation, EF Core, xUnit + Moq), Angular (standalone components, signals, clean-architecture feature slices, Jest).

**Spec:** `docs/superpowers/specs/2026-09-24-swimmer-onboarding-guardian-medical-design.md`

## Global Constraints

- **⚠️ Do NOT run `git commit` until the user explicitly authorizes it** (standing user directive: "don't commit anything until I tell you"). Each task below ends with a **"Stage (commit deferred)"** step: run `git add` for the task's files but **do not commit**. When the user gives the go-ahead, commit the whole feature (or per-task, their choice).
- **No new tables, no DB migration, no seed changes.** `athlete.guardian`, `reference.guardian_relation`, `health.observation`, and the four `reference.observation_category` rows (`allergy`/`surgery`/`chronic`/`autoimmune`) already exist on all targets.
- **Module boundary:** the Identity application layer must **not** depend on the Health application layer. Cross-module orchestration lives only in the API controller (composition root). `ArchitectureTests` must stay green.
- **Self-service, JWT-scoped:** the new endpoint resolves the swimmer from `CurrentUserId()` and is gated `[Authorize(Roles = "swimmer")]`. No swimmer id in the path.
- **Guardian National ID = exactly 14 digits** (`^\d{14}$`), reusing `GuardianInputDtoValidator`.
- **Medical write is insert-only** — never update/delete observations (protects Captain-Panel/Records entries).
- **Backend test command:** stop any running API first (it locks the build DLLs), then `dotnet test` from `backend/`. **Frontend test command:** `npm test` (Jest) from `frontend/`. **AOT build:** `npm run build` from `frontend/`.
- Keep the full suites green: backend (currently 475) + frontend (currently 584), plus the new tests below.

## Review Focus

- **Medical items with `value` present but a `categoryId` the swimmer never should send** (e.g. a non-medical category id) — the endpoint currently trusts the id (FK-guarded only). Covered by Task 2 validator (`CategoryId` NotEmpty) + the frontend only ever sending the four mapped ids; documented as accepted (FK rejects unknown ids). Test: Task 2 rejects an item with empty `CategoryId` (`Gm_rejects_medical_item_with_empty_category`).
- **A parent submitted with a National ID that is not 14 digits** (e.g. 13 digits, or with letters) — must 400, not persist. Test: Task 2 rejects a 13-digit father National ID (`Gm_rejects_father_national_id_not_14_digits`).
- **All four medical answers = No** (empty `Medical` list) — must still upsert guardians and complete onboarding, not error. Test: Task 4 controller test with empty `Medical` still calls `CompleteOnboardingAsync` (`CompleteGuardianMedical_with_empty_medical_still_completes`); Task 2 validator accepts an empty list (`Gm_accepts_empty_medical_list`).
- **Step 2 submit where the identity-vitals call fails** — must not proceed to guardian-medical, must not mark onboarding complete, must keep the form. Test: Task 6 viewmodel spec (identity-vitals fails → guardian-medical use-case not called, no navigate).
- **Yes toggled on with empty Details** — must block Step 2 submit (Details required when Yes). Test: Task 6 viewmodel spec `canSubmitStep2` false when a category is Yes but Details blank.

---

## Task 1: Step 1 endpoint stops clearing first-login

Make `CompleteIdentityVitalsAsync` save identity + exam **without** clearing `IsFirstLogin` (Step 2 now owns completion).

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs` (method `CompleteIdentityVitalsAsync`, ~line 294-325)
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs` (existing test ~line 570)

**Interfaces:**
- Consumes: existing `SwimmerService.CompleteIdentityVitalsAsync(Guid userId, CompleteIdentityVitalsRequest, CancellationToken)`.
- Produces: same signature/return (`OnboardingStepResultDto?`), but now leaves `AppUser.IsFirstLogin` unchanged.

- [ ] **Step 1: Update the existing test to expect first-login is NOT cleared**

In `SwimmerServiceTests.cs`, rename `CompleteIdentityVitals_updates_identity_inserts_exam_and_clears_first_login` to `CompleteIdentityVitals_updates_identity_inserts_exam_and_keeps_first_login` and change the flag assertion:

```csharp
    [Fact]
    public async Task CompleteIdentityVitals_updates_identity_inserts_exam_and_keeps_first_login()
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
        Assert.Equal("New Name", user.NameEn);
        Assert.Equal("s@x.io", user.Email);          // preserved
        Assert.True(user.IsFirstLogin);              // NOT cleared — Step 2 completes onboarding
        Assert.Equal(newClub, profile.TrainingClubId);
        swimmers.Verify(r => r.AddExamAsync(It.IsAny<MedicalExam>(), It.IsAny<CancellationToken>()), Times.Once);
        swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run (stop the API first): `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter CompleteIdentityVitals_updates_identity_inserts_exam_and_keeps_first_login`
Expected: FAIL — `Assert.True(user.IsFirstLogin)` fails because the impl still calls `CompleteFirstLogin()` (flag is false).

- [ ] **Step 3: Remove the first-login clear from the service**

In `SwimmerService.CompleteIdentityVitalsAsync`, delete the completion line and its comment (step 7):

```csharp
        // (removed) Completing Step 1 no longer completes onboarding — Step 2 (Guardian & Medical) clears the flag.
        // user.CompleteFirstLogin();
```

Leave everything else (identity update, `SetTrainingClub`, `AddExamAsync`, the single `SaveChangesAsync`, `return new OnboardingStepResultDto(false)`) unchanged.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter CompleteIdentityVitals`
Expected: PASS (all CompleteIdentityVitals tests, including the null-blood-type and reference-unknown ones).

- [ ] **Step 5: Stage (commit deferred)**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs
# DO NOT COMMIT yet (user directive)
```

---

## Task 2: DTOs + validator for the Step 2 request

Add the request DTOs and a FluentValidation validator (both parents required, Yes-items require a value).

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/SwimmerDtos.cs` (append)
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Validators/UserRequestValidators.cs` (append)
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Validators/OnboardingValidatorTests.cs` (append)

**Interfaces:**
- Consumes: existing `GuardianInputDto(string Name, string NationalId, string Phone)` and `internal GuardianInputDtoValidator` (same assembly).
- Produces:
  - `OnboardingMedicalItemDto(Guid CategoryId, string FieldLabel, string Value)`
  - `CompleteGuardianMedicalRequest(GuardianInputDto Father, GuardianInputDto Mother, IReadOnlyList<OnboardingMedicalItemDto> Medical)`
  - `CompleteGuardianMedicalRequestValidator : AbstractValidator<CompleteGuardianMedicalRequest>`

- [ ] **Step 1: Add the DTOs**

Append to `SwimmerDtos.cs`:

```csharp
/// <summary>One medical-history observation captured in onboarding Step 2 (a "Yes" answer with details).</summary>
public sealed record OnboardingMedicalItemDto(Guid CategoryId, string FieldLabel, string Value);

/// <summary>Step 2 submission (POST /api/swimmers/me/onboarding/guardian-medical): both guardians + Yes-only medical items.</summary>
public sealed record CompleteGuardianMedicalRequest(
    GuardianInputDto Father,
    GuardianInputDto Mother,
    IReadOnlyList<OnboardingMedicalItemDto> Medical);
```

- [ ] **Step 2: Write the failing validator tests**

Append to `OnboardingValidatorTests.cs` (inside the class):

```csharp
    private static CompleteGuardianMedicalRequest ValidGm() => new(
        new GuardianInputDto("Ahmed Ali", "12345678901234", "0100000000"),
        new GuardianInputDto("Sara Omar", "43210987654321", "0111111111"),
        new[] { new OnboardingMedicalItemDto(Guid.NewGuid(), "Allergies", "Peanuts") });

    private static readonly CompleteGuardianMedicalRequestValidator GV = new();

    [Fact] public void Gm_accepts_a_valid_request() => Assert.True(GV.Validate(ValidGm()).IsValid);

    [Fact] public void Gm_accepts_empty_medical_list()
        => Assert.True(GV.Validate(ValidGm() with { Medical = System.Array.Empty<OnboardingMedicalItemDto>() }).IsValid);

    [Fact] public void Gm_rejects_father_national_id_not_14_digits()
        => Assert.False(GV.Validate(ValidGm() with { Father = new GuardianInputDto("Ahmed Ali", "1234567890123", "0100000000") }).IsValid);

    [Fact] public void Gm_rejects_blank_mother_name()
        => Assert.False(GV.Validate(ValidGm() with { Mother = new GuardianInputDto("", "43210987654321", "0111111111") }).IsValid);

    [Fact] public void Gm_rejects_medical_item_with_blank_value()
        => Assert.False(GV.Validate(ValidGm() with { Medical = new[] { new OnboardingMedicalItemDto(Guid.NewGuid(), "Allergies", "") } }).IsValid);

    [Fact] public void Gm_rejects_medical_item_with_empty_category()
        => Assert.False(GV.Validate(ValidGm() with { Medical = new[] { new OnboardingMedicalItemDto(Guid.Empty, "Allergies", "Peanuts") } }).IsValid);
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter Gm_`
Expected: FAIL to compile — `CompleteGuardianMedicalRequestValidator` does not exist yet.

- [ ] **Step 4: Add the validator**

Append to `UserRequestValidators.cs` (it already has `using FluentValidation;` and the DTO/`GuardianInputDtoValidator` are in-assembly):

```csharp
public sealed class CompleteGuardianMedicalRequestValidator : AbstractValidator<CompleteGuardianMedicalRequest>
{
    public CompleteGuardianMedicalRequestValidator()
    {
        RuleFor(x => x.Father).NotNull().SetValidator(new GuardianInputDtoValidator());
        RuleFor(x => x.Mother).NotNull().SetValidator(new GuardianInputDtoValidator());
        RuleForEach(x => x.Medical).ChildRules(item =>
        {
            item.RuleFor(i => i.CategoryId).NotEmpty();
            item.RuleFor(i => i.FieldLabel).NotEmpty();
            item.RuleFor(i => i.Value).NotEmpty();
        });
    }
}
```

> Note: `GuardianInputDtoValidator` is `internal` in `GuardianRequestValidators.cs` but in the same assembly (`Identity.Application`), so it is accessible here.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "Gm_|Onboarding"`
Expected: PASS.

- [ ] **Step 6: Stage (commit deferred)**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/SwimmerDtos.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Validators/UserRequestValidators.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Validators/OnboardingValidatorTests.cs
# DO NOT COMMIT yet
```

---

## Task 3: Identity service — guardian upsert + complete-onboarding (by userId)

Add two JWT-scoped service methods: upsert both guardians (returns the swimmer id), and clear the first-login flag.

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/ISwimmerService.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs` (append)

**Interfaces:**
- Consumes: existing repo methods `ISwimmerProfileRepository.GetByUserIdTrackedAsync`, `GetGuardianRelationIdByCodeAsync`, `GetGuardianTrackedAsync`, `AddGuardianAsync`, `SaveChangesAsync`; `IUserRepository.GetByIdAsync`, `SaveChangesAsync`; `AppUser.CompleteFirstLogin()`; the private `SwimmerService.UpsertOne(Guid swimmerId, string relationCode, GuardianInputDto input, CancellationToken)`.
- Produces:
  - `Task<Guid?> UpsertOnboardingGuardiansAsync(Guid userId, GuardianInputDto father, GuardianInputDto mother, CancellationToken ct = default)` — returns `swimmerId` (profile id), or `null` when the user has no swimmer profile.
  - `Task<bool> CompleteOnboardingAsync(Guid userId, CancellationToken ct = default)` — clears `IsFirstLogin`; `false` when no profile/user.

- [ ] **Step 1: Write the failing service tests**

Append to `SwimmerServiceTests.cs`:

```csharp
    // ── Onboarding Step 2 (guardian + complete) tests ────────────────────────

    [Fact]
    public async Task UpsertOnboardingGuardians_returns_null_when_not_a_swimmer()
    {
        var (svc, swimmers, _) = Build();
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwimmerProfile?)null);
        var father = new GuardianInputDto("Ahmed", "12345678901234", "010");
        var mother = new GuardianInputDto("Sara", "43210987654321", "011");
        Assert.Null(await svc.UpsertOnboardingGuardiansAsync(Guid.NewGuid(), father, mother));
    }

    [Fact]
    public async Task UpsertOnboardingGuardians_inserts_both_and_returns_swimmer_id()
    {
        var (svc, swimmers, _) = Build();
        var userId = Guid.NewGuid();
        var profile = new SwimmerProfile(userId, "SW-0001", Guid.NewGuid());
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        var fatherRel = Guid.NewGuid(); var motherRel = Guid.NewGuid();
        swimmers.Setup(r => r.GetGuardianRelationIdByCodeAsync("father", It.IsAny<CancellationToken>())).ReturnsAsync(fatherRel);
        swimmers.Setup(r => r.GetGuardianRelationIdByCodeAsync("mother", It.IsAny<CancellationToken>())).ReturnsAsync(motherRel);
        swimmers.Setup(r => r.GetGuardianTrackedAsync(profile.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guardian?)null);

        var result = await svc.UpsertOnboardingGuardiansAsync(userId,
            new GuardianInputDto("Ahmed", "12345678901234", "010"),
            new GuardianInputDto("Sara", "43210987654321", "011"));

        Assert.Equal(profile.Id, result);
        swimmers.Verify(r => r.AddGuardianAsync(It.Is<Guardian>(g => g.RelationId == fatherRel && g.Name == "Ahmed"), It.IsAny<CancellationToken>()), Times.Once);
        swimmers.Verify(r => r.AddGuardianAsync(It.Is<Guardian>(g => g.RelationId == motherRel && g.Name == "Sara"), It.IsAny<CancellationToken>()), Times.Once);
        swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteOnboarding_clears_first_login()
    {
        var (svc, swimmers, users) = Build();
        var userId = Guid.NewGuid();
        var profile = new SwimmerProfile(userId, "SW-0001", Guid.NewGuid());
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        var user = new AppUser("s", "S", Guid.NewGuid(), email: "s@x.io", isFirstLogin: true);
        users.Setup(u => u.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        Assert.True(await svc.CompleteOnboardingAsync(userId));
        Assert.False(user.IsFirstLogin);
        users.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteOnboarding_returns_false_when_not_a_swimmer()
    {
        var (svc, swimmers, _) = Build();
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwimmerProfile?)null);
        Assert.False(await svc.CompleteOnboardingAsync(Guid.NewGuid()));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "UpsertOnboardingGuardians|CompleteOnboarding"`
Expected: FAIL to compile — the methods do not exist on `ISwimmerService`.

- [ ] **Step 3: Declare the interface methods**

Add to `ISwimmerService.cs` (near the existing onboarding methods, ~line 22):

```csharp
    Task<Guid?> UpsertOnboardingGuardiansAsync(Guid userId, GuardianInputDto father, GuardianInputDto mother, CancellationToken ct = default);
    Task<bool> CompleteOnboardingAsync(Guid userId, CancellationToken ct = default);
```

- [ ] **Step 4: Implement the service methods**

Add to `SwimmerService.cs` (after `CompleteIdentityVitalsAsync`). Reuse the existing private `UpsertOne`:

```csharp
    public async Task<Guid?> UpsertOnboardingGuardiansAsync(
        Guid userId, GuardianInputDto father, GuardianInputDto mother, CancellationToken ct = default)
    {
        var profile = await _swimmers.GetByUserIdTrackedAsync(userId, ct);
        if (profile is null) return null;

        await UpsertOne(profile.Id, "father", father, ct);
        await UpsertOne(profile.Id, "mother", mother, ct);
        await _swimmers.SaveChangesAsync(ct);
        return profile.Id;
    }

    public async Task<bool> CompleteOnboardingAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await _swimmers.GetByUserIdTrackedAsync(userId, ct);
        if (profile is null) return false;
        var user = await _users.GetByIdAsync(profile.UserId, ct);
        if (user is null) return false;

        user.CompleteFirstLogin();
        await _users.SaveChangesAsync(ct);
        return true;
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "UpsertOnboardingGuardians|CompleteOnboarding"`
Expected: PASS.

- [ ] **Step 6: Stage (commit deferred)**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/ISwimmerService.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs
# DO NOT COMMIT yet
```

---

## Task 4: Controller endpoint — orchestrate guardian + medical + complete

Add the new self-service endpoint. Inject the Health `IObservationService` alongside `ISwimmerService`; upsert guardians → insert observations → complete (last).

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs` (constructor + Onboarding region)
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs`

**Interfaces:**
- Consumes: `ISwimmerService.UpsertOnboardingGuardiansAsync`, `ISwimmerService.CompleteOnboardingAsync` (Task 3); `IObservationService.CreateAsync(CreateObservationRequest(Guid SwimmerId, Guid CategoryId, string FieldLabel, string Value), Guid recordedBy, CancellationToken)` (Health); `CompleteGuardianMedicalRequest` (Task 2); `CurrentUserId()` (base controller).
- Produces: `POST api/swimmers/me/onboarding/guardian-medical` → `ApiResponse<OnboardingStepResultDto>` (200) / 404.

- [ ] **Step 1: Update the controller test harness + write the failing endpoint tests**

In `SwimmersControllerTests.cs`, add the Health usings at the top:

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
```

Replace the `OnboardingController` helper and add a general `Create` helper (the controller constructor now needs both services):

```csharp
    private static SwimmersController Create(ISwimmerService svc, IObservationService? obs = null)
        => new(svc, obs ?? Mock.Of<IObservationService>())
           { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    private static SwimmersController OnboardingController(ISwimmerService svc, IObservationService? obs = null)
        => Create(svc, obs);
```

Then **replace every** `new SwimmersController(svc.Object)` in this file with `Create(svc.Object)` (mechanical; the constructor now takes two args). Add the endpoint tests:

```csharp
    [Fact]
    public async Task CompleteGuardianMedical_upserts_creates_observations_then_completes()
    {
        var svc = new Mock<ISwimmerService>();
        var swimmerId = Guid.NewGuid();
        svc.Setup(s => s.UpsertOnboardingGuardiansAsync(It.IsAny<Guid>(), It.IsAny<GuardianInputDto>(), It.IsAny<GuardianInputDto>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(swimmerId);
        svc.Setup(s => s.CompleteOnboardingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var obs = new Mock<IObservationService>();

        var catId = Guid.NewGuid();
        var req = new CompleteGuardianMedicalRequest(
            new GuardianInputDto("Ahmed", "12345678901234", "010"),
            new GuardianInputDto("Sara", "43210987654321", "011"),
            new[] { new OnboardingMedicalItemDto(catId, "Allergies", "Peanuts") });

        var result = await Create(svc.Object, obs.Object).CompleteOnboardingGuardianMedical(req, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<OnboardingStepResultDto>>(ok.Value);
        Assert.True(body.SuccessStatus);
        Assert.False(body.Data!.MustChangePassword);
        obs.Verify(o => o.CreateAsync(It.Is<CreateObservationRequest>(r => r.SwimmerId == swimmerId && r.CategoryId == catId && r.Value == "Peanuts"), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        svc.Verify(s => s.CompleteOnboardingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteGuardianMedical_with_empty_medical_still_completes()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.UpsertOnboardingGuardiansAsync(It.IsAny<Guid>(), It.IsAny<GuardianInputDto>(), It.IsAny<GuardianInputDto>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Guid.NewGuid());
        svc.Setup(s => s.CompleteOnboardingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var obs = new Mock<IObservationService>();

        var req = new CompleteGuardianMedicalRequest(
            new GuardianInputDto("Ahmed", "12345678901234", "010"),
            new GuardianInputDto("Sara", "43210987654321", "011"),
            System.Array.Empty<OnboardingMedicalItemDto>());

        var result = await Create(svc.Object, obs.Object).CompleteOnboardingGuardianMedical(req, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        obs.Verify(o => o.CreateAsync(It.IsAny<CreateObservationRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        svc.Verify(s => s.CompleteOnboardingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteGuardianMedical_returns_404_when_not_a_swimmer()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.UpsertOnboardingGuardiansAsync(It.IsAny<Guid>(), It.IsAny<GuardianInputDto>(), It.IsAny<GuardianInputDto>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync((Guid?)null);
        var obs = new Mock<IObservationService>();

        var req = new CompleteGuardianMedicalRequest(
            new GuardianInputDto("Ahmed", "12345678901234", "010"),
            new GuardianInputDto("Sara", "43210987654321", "011"),
            System.Array.Empty<OnboardingMedicalItemDto>());

        var result = await Create(svc.Object, obs.Object).CompleteOnboardingGuardianMedical(req, CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
        obs.Verify(o => o.CreateAsync(It.IsAny<CreateObservationRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        svc.Verify(s => s.CompleteOnboardingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter CompleteGuardianMedical`
Expected: FAIL to compile — the constructor overload and `CompleteOnboardingGuardianMedical` action do not exist yet.

- [ ] **Step 3: Update the controller constructor**

In `SwimmersController.cs`, add the Health usings:

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services.Interfaces;
```

Change the fields + constructor:

```csharp
    private readonly ISwimmerService _service;
    private readonly IObservationService _observations;

    public SwimmersController(ISwimmerService service, IObservationService observations)
    {
        _service = service;
        _observations = observations;
    }
```

- [ ] **Step 4: Add the endpoint (inside the Onboarding region, after the identity-vitals POST)**

```csharp
    /// <summary>Completes the swimmer's first-login Step 2 (guardians + medical history), clearing first-login. Swimmer only.</summary>
    /// <response code="200">Completed; returns the refreshed first-login flag (false).</response>
    /// <response code="404">The caller is not a swimmer / has no profile.</response>
    [HttpPost("me/onboarding/guardian-medical")]
    [Authorize(Roles = "swimmer")]
    [ProducesResponseType(typeof(ApiResponse<OnboardingStepResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OnboardingStepResultDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OnboardingStepResultDto>>> CompleteOnboardingGuardianMedical(
        CompleteGuardianMedicalRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId();

        // 1) Guardians (Identity). Returns the swimmer id needed for observations; null → not a swimmer.
        var swimmerId = await _service.UpsertOnboardingGuardiansAsync(userId, request.Father, request.Mother, ct);
        if (swimmerId is null)
        {
            var nf = ApiResponse<OnboardingStepResultDto>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }

        // 2) Medical history (Health) — one observation per "Yes" answer. Insert-only.
        foreach (var item in request.Medical)
            await _observations.CreateAsync(
                new CreateObservationRequest(swimmerId.Value, item.CategoryId, item.FieldLabel, item.Value), userId, ct);

        // 3) Clear first-login LAST — the commit point for onboarding.
        await _service.CompleteOnboardingAsync(userId, ct);

        return Ok(ApiResponse<OnboardingStepResultDto>.Success(
            SwimmerMessages.Success.OnboardingCompleted(AppLanguage.Current), new OnboardingStepResultDto(false)));
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests`
Expected: PASS (the new endpoint tests + all pre-existing SwimmersController tests still green after the `Create(...)` harness swap).

- [ ] **Step 6: Run the whole backend suite + architecture tests**

Run (API stopped): `dotnet test backend/`
Expected: PASS, including `ArchitectureTests` (no Identity→Health application dependency was introduced — orchestration is in the API layer only).

- [ ] **Step 7: Stage (commit deferred)**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs \
        backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs
# DO NOT COMMIT yet
```

---

## Task 5: Frontend — model, DTO, repository, use-case (Step 2 submit)

Add the domain model, request DTO, repository method, and use-case for the guardian-medical submit.

**Files:**
- Modify: `frontend/src/app/features/swimmer-onboarding/domain/model/onboarding.ts`
- Create: `frontend/src/app/features/swimmer-onboarding/data/dto/complete-guardian-medical.dto.ts`
- Modify: `frontend/src/app/features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository.ts`
- Modify: `frontend/src/app/features/swimmer-onboarding/data/repositories/swimmer-onboarding.repository.impl.ts`
- Create: `frontend/src/app/features/swimmer-onboarding/domain/usecases/complete-guardian-medical.use-case.ts`
- Test: `frontend/src/app/features/swimmer-onboarding/testing/domain/usecases/complete-guardian-medical.use-case.spec.ts`

**Interfaces:**
- Consumes: `UseCase<I,O>` base; `SWIMMER_ONBOARDING_REPOSITORY` token; `OnboardingResultDtoRs` (from `complete-identity-vitals.dto.ts`); `HttpClientService`.
- Produces:
  - Model: `GuardianInput { name; nationalId; phone }`, `OnboardingMedicalItem { categoryId; fieldLabel; value }`, `GuardianMedicalSubmission { father; mother; medical }`.
  - DTO: `CompleteGuardianMedicalDtoRq`, `CompleteGuardianMedicalItemDtoRs`.
  - Repo: `completeGuardianMedical(rq): Promise<CompleteGuardianMedicalItemDtoRs>`.
  - Use-case: `CompleteGuardianMedicalUseCase extends UseCase<GuardianMedicalSubmission, void>`.

- [ ] **Step 1: Add the domain models**

Append to `onboarding.ts`:

```typescript
export interface GuardianInput {
  name: string;
  nationalId: string;
  phone: string;
}

export interface OnboardingMedicalItem {
  categoryId: string;
  fieldLabel: string;
  value: string;
}

export interface GuardianMedicalSubmission {
  father: GuardianInput;
  mother: GuardianInput;
  medical: OnboardingMedicalItem[];
}
```

- [ ] **Step 2: Add the request DTO**

Create `data/dto/complete-guardian-medical.dto.ts`:

```typescript
import { BaseResponseRs } from '@core/network/api/base-response-rs';
import { OnboardingResultDtoRs } from './complete-identity-vitals.dto';

export interface GuardianInputDtoRq {
  name: string;
  nationalId: string;
  phone: string;
}
export interface OnboardingMedicalItemDtoRq {
  categoryId: string;
  fieldLabel: string;
  value: string;
}
export interface CompleteGuardianMedicalDtoRq {
  father: GuardianInputDtoRq;
  mother: GuardianInputDtoRq;
  medical: OnboardingMedicalItemDtoRq[];
}
export interface CompleteGuardianMedicalItemDtoRs extends BaseResponseRs<OnboardingResultDtoRs> {}
```

- [ ] **Step 3: Extend the repository interface + impl**

In `swimmer-onboarding.repository.ts`, add the import and the method:

```typescript
import { CompleteGuardianMedicalDtoRq, CompleteGuardianMedicalItemDtoRs } from '@features/swimmer-onboarding/data/dto/complete-guardian-medical.dto';
```
```typescript
  completeGuardianMedical(rq: CompleteGuardianMedicalDtoRq): Promise<CompleteGuardianMedicalItemDtoRs>;
```

In `swimmer-onboarding.repository.impl.ts`, add the import and implement:

```typescript
import { CompleteGuardianMedicalDtoRq, CompleteGuardianMedicalItemDtoRs } from '@features/swimmer-onboarding/data/dto/complete-guardian-medical.dto';
```
```typescript
  completeGuardianMedical(rq: CompleteGuardianMedicalDtoRq): Promise<CompleteGuardianMedicalItemDtoRs> {
    return this.http.post<CompleteGuardianMedicalItemDtoRs>('/api/swimmers/me/onboarding/guardian-medical', { body: rq });
  }
```

- [ ] **Step 4: Write the failing use-case spec**

Create `testing/domain/usecases/complete-guardian-medical.use-case.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { CompleteGuardianMedicalUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-guardian-medical.use-case';
import { SWIMMER_ONBOARDING_REPOSITORY, ISwimmerOnboardingRepository } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { GuardianMedicalSubmission } from '@features/swimmer-onboarding/domain/model/onboarding';

const SUBMISSION: GuardianMedicalSubmission = {
  father: { name: 'Ahmed', nationalId: '12345678901234', phone: '010' },
  mother: { name: 'Sara', nationalId: '43210987654321', phone: '011' },
  medical: [{ categoryId: 'cat-allergy', fieldLabel: 'Allergies', value: 'Peanuts' }],
};

function build(repo: Partial<ISwimmerOnboardingRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_ONBOARDING_REPOSITORY, useValue: repo }] });
  return TestBed.inject(CompleteGuardianMedicalUseCase);
}

describe('CompleteGuardianMedicalUseCase', () => {
  it('posts the submission and succeeds', async () => {
    const post = jest.fn().mockResolvedValue({ data: { mustChangePassword: false } });
    const uc = build({ completeGuardianMedical: post } as unknown as ISwimmerOnboardingRepository);
    const r = await uc.run(SUBMISSION);
    expect(r.ok).toBe(true);
    expect(post).toHaveBeenCalledWith(expect.objectContaining({
      father: expect.objectContaining({ name: 'Ahmed', nationalId: '12345678901234' }),
      medical: [{ categoryId: 'cat-allergy', fieldLabel: 'Allergies', value: 'Peanuts' }],
    }));
  });

  it('fails when the repository throws', async () => {
    const uc = build({ completeGuardianMedical: async () => { throw new Error('boom'); } } as unknown as ISwimmerOnboardingRepository);
    const r = await uc.run(SUBMISSION);
    expect(r.ok).toBe(false);
  });
});
```

- [ ] **Step 5: Run the spec to verify it fails**

Run: `npm test -- complete-guardian-medical.use-case` (from `frontend/`)
Expected: FAIL — `CompleteGuardianMedicalUseCase` does not exist yet.

- [ ] **Step 6: Implement the use-case**

Create `domain/usecases/complete-guardian-medical.use-case.ts`:

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_ONBOARDING_REPOSITORY } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { GuardianMedicalSubmission } from '@features/swimmer-onboarding/domain/model/onboarding';

@Injectable({ providedIn: 'root' })
export class CompleteGuardianMedicalUseCase extends UseCase<GuardianMedicalSubmission, void> {
  private readonly repo = inject(SWIMMER_ONBOARDING_REPOSITORY);
  constructor() { super('CompleteGuardianMedical'); }

  protected async execute(input: GuardianMedicalSubmission): Promise<void> {
    await this.repo.completeGuardianMedical({
      father: { name: input.father.name, nationalId: input.father.nationalId, phone: input.father.phone },
      mother: { name: input.mother.name, nationalId: input.mother.nationalId, phone: input.mother.phone },
      medical: input.medical.map((m) => ({ categoryId: m.categoryId, fieldLabel: m.fieldLabel, value: m.value })),
    });
  }
}
```

- [ ] **Step 7: Run the spec to verify it passes**

Run: `npm test -- complete-guardian-medical.use-case`
Expected: PASS.

- [ ] **Step 8: Stage (commit deferred)**

```bash
git add frontend/src/app/features/swimmer-onboarding/domain/model/onboarding.ts \
        frontend/src/app/features/swimmer-onboarding/data/dto/complete-guardian-medical.dto.ts \
        frontend/src/app/features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository.ts \
        frontend/src/app/features/swimmer-onboarding/data/repositories/swimmer-onboarding.repository.impl.ts \
        frontend/src/app/features/swimmer-onboarding/domain/usecases/complete-guardian-medical.use-case.ts \
        frontend/src/app/features/swimmer-onboarding/testing/domain/usecases/complete-guardian-medical.use-case.spec.ts
# DO NOT COMMIT yet
```

---

## Task 6: Frontend — viewmodel (two-step flow + Step 2 state + submit sequence)

Extend `OnboardingViewModel` with step navigation, Step 2 field state, `canSubmitStep2`, observation-category loading, and the deferred two-call submit.

**Files:**
- Modify: `frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding.viewmodel.ts`
- Test: `frontend/src/app/features/swimmer-onboarding/testing/presentation/pages/onboarding/onboarding.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `CompleteIdentityVitalsUseCase`, `CompleteGuardianMedicalUseCase` (Task 5), `LoadObservationCategoriesUseCase` (`@features/reference/domain/usecases/load-observation-categories.use-case`), `LookupItem`, `AuthSessionStore.markOnboardingComplete`, `Router`, `NotificationService`, `TranslateService`.
- Produces (new public members on `OnboardingViewModel`): `currentStep: WritableSignal<1|2>`; `goToStep2()`, `backToStep1()`; `canSubmitStep1` (renamed from `canSubmit`), `canSubmitStep2`; Step 2 signals `fatherName/fatherNationalId/fatherPhone/motherName/motherNationalId/motherPhone` and per-category `allergyYes/allergyDetails/surgeryYes/surgeryDetails/chronicYes/chronicDetails/autoimmuneYes/autoimmuneDetails`; `observationCategories: Signal<LookupItem[]>`; `submit()` now runs the two-call finish.

- [ ] **Step 1: Update the viewmodel spec (failing tests for the new behavior)**

Edit `onboarding.viewmodel.spec.ts`. Add the new use-case imports and provide them + `LoadObservationCategoriesUseCase` in `build()`; wire a `guardianMedical` mock. Replace the file body with:

```typescript
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { OnboardingViewModel } from '@features/swimmer-onboarding/presentation/pages/onboarding/onboarding.viewmodel';
import { GetOnboardingPrefillUseCase } from '@features/swimmer-onboarding/domain/usecases/get-onboarding-prefill.use-case';
import { CompleteIdentityVitalsUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-identity-vitals.use-case';
import { CompleteGuardianMedicalUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-guardian-medical.use-case';
import { LoadClubsUseCase, LoadGendersUseCase, LoadBloodTypesUseCase } from '@features/reference';
import { LoadFitnessAssessmentsUseCase } from '@features/reference/domain/usecases/load-fitness-assessments.use-case';
import { LoadObservationCategoriesUseCase } from '@features/reference/domain/usecases/load-observation-categories.use-case';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';

const PREFILL = { uid: 'SW-1', nameEn: 'Sam', nameAr: 'سام', genderId: 'g1', dob: '2010-01-01', trainingClubId: 'c1' };
const LOOKUP = [{ id: 'x', nameEn: 'X', nameAr: null }];
const CATS = [
  { id: 'cat-allergy', code: 'allergy', nameEn: 'Allergy', nameAr: null },
  { id: 'cat-surgery', code: 'surgery', nameEn: 'Surgery', nameAr: null },
  { id: 'cat-chronic', code: 'chronic', nameEn: 'Chronic', nameAr: null },
  { id: 'cat-autoimmune', code: 'autoimmune', nameEn: 'Autoimmune', nameAr: null },
];

function build(overrides: { identity?: unknown; guardianMedical?: unknown } = {}) {
  const getPrefill = { run: jest.fn().mockResolvedValue(ok(PREFILL)) };
  const identity = { run: jest.fn().mockResolvedValue(overrides.identity ?? ok(undefined)) };
  const guardianMedical = { run: jest.fn().mockResolvedValue(overrides.guardianMedical ?? ok(undefined)) };
  const lookups = { run: jest.fn().mockResolvedValue(ok(LOOKUP)) };
  const cats = { run: jest.fn().mockResolvedValue(ok(CATS)) };
  const auth = { markOnboardingComplete: jest.fn() } as unknown as AuthSessionStore;
  const router = { navigate: jest.fn() } as unknown as Router;
  const notify = { success: jest.fn(), error: jest.fn() } as unknown as NotificationService;
  const i18n = { t: (k: string) => k } as unknown as TranslateService;
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      OnboardingViewModel,
      { provide: GetOnboardingPrefillUseCase, useValue: getPrefill },
      { provide: CompleteIdentityVitalsUseCase, useValue: identity },
      { provide: CompleteGuardianMedicalUseCase, useValue: guardianMedical },
      { provide: LoadClubsUseCase, useValue: lookups },
      { provide: LoadGendersUseCase, useValue: lookups },
      { provide: LoadBloodTypesUseCase, useValue: lookups },
      { provide: LoadFitnessAssessmentsUseCase, useValue: lookups },
      { provide: LoadObservationCategoriesUseCase, useValue: cats },
      { provide: AuthSessionStore, useValue: auth },
      { provide: Router, useValue: router },
      { provide: NotificationService, useValue: notify },
      { provide: TranslateService, useValue: i18n },
    ],
  });
  return { vm: TestBed.inject(OnboardingViewModel), identity, guardianMedical, auth, router, notify };
}

function fillStep1(vm: OnboardingViewModel) {
  vm.examDate.set('2026-01-01'); vm.internalMedId.set('f1'); vm.heartAssessId.set('f1'); vm.spineAssessId.set('f1');
  vm.hemoglobin.set('14.5'); vm.heightCm.set('175'); vm.weightKg.set('68');
}

function fillStep2Guardians(vm: OnboardingViewModel) {
  vm.fatherName.set('Ahmed'); vm.fatherNationalId.set('12345678901234'); vm.fatherPhone.set('010');
  vm.motherName.set('Sara'); vm.motherNationalId.set('43210987654321'); vm.motherPhone.set('011');
}

describe('OnboardingViewModel', () => {
  it('load() seeds identity drafts, lookups, and observation categories', async () => {
    const { vm } = build();
    await vm.load();
    expect(vm.nameEn()).toBe('Sam');
    expect(vm.observationCategories().length).toBe(4);
  });

  it('goToStep2() advances only when Step 1 is valid', async () => {
    const { vm } = build();
    await vm.load();
    vm.goToStep2();
    expect(vm.currentStep()).toBe(1);   // blocked — Step 1 incomplete
    fillStep1(vm);
    vm.goToStep2();
    expect(vm.currentStep()).toBe(2);
    vm.backToStep1();
    expect(vm.currentStep()).toBe(1);
  });

  it('canSubmitStep2 requires both parents and details for every "Yes"', async () => {
    const { vm } = build();
    await vm.load();
    expect(vm.canSubmitStep2()).toBe(false);
    fillStep2Guardians(vm);
    expect(vm.canSubmitStep2()).toBe(true);
    vm.allergyYes.set(true);              // Yes with blank details → blocked
    expect(vm.canSubmitStep2()).toBe(false);
    vm.allergyDetails.set('Peanuts');
    expect(vm.canSubmitStep2()).toBe(true);
  });

  it('submit() posts identity-vitals then guardian-medical, marks complete, navigates', async () => {
    const { vm, identity, guardianMedical, auth, router } = build();
    await vm.load();
    fillStep1(vm); fillStep2Guardians(vm);
    vm.allergyYes.set(true); vm.allergyDetails.set('Peanuts');
    await vm.submit();
    expect(identity.run).toHaveBeenCalled();
    expect(guardianMedical.run).toHaveBeenCalledWith(expect.objectContaining({
      father: expect.objectContaining({ name: 'Ahmed' }),
      medical: [{ categoryId: 'cat-allergy', fieldLabel: 'Allergies', value: 'Peanuts' }],
    }));
    expect(auth.markOnboardingComplete).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/home']);
  });

  it('submit() stops and does not complete when identity-vitals fails', async () => {
    const { vm, guardianMedical, auth, router, notify } = build({ identity: fail(new AppError('nope', 'network')) });
    await vm.load();
    fillStep1(vm); fillStep2Guardians(vm);
    await vm.submit();
    expect(guardianMedical.run).not.toHaveBeenCalled();
    expect(auth.markOnboardingComplete).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
    expect(notify.error).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run the spec to verify it fails**

Run: `npm test -- onboarding.viewmodel`
Expected: FAIL — new members (`currentStep`, `goToStep2`, `canSubmitStep2`, Step 2 signals, `observationCategories`) don't exist.

- [ ] **Step 3: Implement the viewmodel changes**

Edit `onboarding.viewmodel.ts`:

Add imports:
```typescript
import { CompleteGuardianMedicalUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-guardian-medical.use-case';
import { LoadObservationCategoriesUseCase } from '@features/reference/domain/usecases/load-observation-categories.use-case';
```

Inject the new use-cases (next to the existing `completeUc`):
```typescript
  private readonly completeGuardianMedicalUc = inject(CompleteGuardianMedicalUseCase);
  private readonly loadObservationCategories = inject(LoadObservationCategoriesUseCase);
```

Add step + Step 2 state (after the existing exam/vitals signals):
```typescript
  // Wizard step (1 = Identity & Vitals, 2 = Guardian & Medical).
  readonly currentStep = signal<1 | 2>(1);

  readonly observationCategories = signal<LookupItem[]>([]);

  // Guardian (both parents required).
  readonly fatherName = signal(''); readonly fatherNationalId = signal(''); readonly fatherPhone = signal('');
  readonly motherName = signal(''); readonly motherNationalId = signal(''); readonly motherPhone = signal('');

  // Medical history — Yes/No + details per fixed category.
  readonly allergyYes = signal(false); readonly allergyDetails = signal('');
  readonly surgeryYes = signal(false); readonly surgeryDetails = signal('');
  readonly chronicYes = signal(false); readonly chronicDetails = signal('');
  readonly autoimmuneYes = signal(false); readonly autoimmuneDetails = signal('');

  // Fixed medical categories: code (matches reference.observation_category) + stored English field label.
  private readonly medicalDefs = [
    { code: 'allergy', label: 'Allergies', yes: this.allergyYes, details: this.allergyDetails },
    { code: 'surgery', label: 'Previous Surgeries', yes: this.surgeryYes, details: this.surgeryDetails },
    { code: 'chronic', label: 'Chronic Diseases', yes: this.chronicYes, details: this.chronicDetails },
    { code: 'autoimmune', label: 'Autoimmune Diseases', yes: this.autoimmuneYes, details: this.autoimmuneDetails },
  ];
```

Rename the existing `canSubmit` computed to `canSubmitStep1` (keep its body identical) and add `canSubmitStep2`:
```typescript
  readonly canSubmitStep2 = computed(() =>
    this.fatherName().trim().length > 0 && this.fatherNationalId().trim().length > 0 && this.fatherPhone().trim().length > 0 &&
    this.motherName().trim().length > 0 && this.motherNationalId().trim().length > 0 && this.motherPhone().trim().length > 0 &&
    this.medicalDefs.every((d) => !d.yes() || d.details().trim().length > 0));
```

In `load()`, add the observation-category load to the `Promise.all` and store it:
```typescript
    const [prefill, clubs, genders, blood, fitness, cats] = await Promise.all([
      this.getPrefillUc.run(),
      this.loadClubs.run(),
      this.loadGenders.run(),
      this.loadBloodTypes.run(),
      this.loadFitness.run(),
      this.loadObservationCategories.run(),
    ]);
    if (cats.ok) this.observationCategories.set(cats.data);
```
(keep the existing `if (clubs.ok) …` assignments and prefill handling.)

Add navigation helpers:
```typescript
  goToStep2(): void { if (this.canSubmitStep1()) this.currentStep.set(2); }
  backToStep1(): void { this.currentStep.set(1); }

  private buildMedical() {
    const cats = this.observationCategories();
    const idOf = (code: string) => cats.find((c) => c.code === code)?.id ?? '';
    return this.medicalDefs
      .filter((d) => d.yes() && d.details().trim().length > 0)
      .map((d) => ({ categoryId: idOf(d.code), fieldLabel: d.label, value: d.details().trim() }));
  }
```

Replace the existing `submit()` body with the deferred two-call finish:
```typescript
  async submit(): Promise<void> {
    if (!this.canSubmitStep2() || this.submitting()) return;
    this.submitting.set(true);

    const r1 = await this.completeUc.run({
      nameEn: this.nameEn().trim(), nameAr: this.nameArOriginal,
      genderId: this.genderId(), dob: this.dob(), trainingClubId: this.trainingClubId(),
      examDate: this.examDate(), bloodTypeId: this.bloodTypeId() || null,
      hemoglobin: Number(this.hemoglobin()), heightCm: Number(this.heightCm()), weightKg: Number(this.weightKg()),
      internalMedId: this.internalMedId(), heartAssessId: this.heartAssessId(), spineAssessId: this.spineAssessId(),
    });
    if (!r1.ok) { this.submitting.set(false); this.notify.error(this.i18n.t('onboarding.saveFailed')); return; }

    const r2 = await this.completeGuardianMedicalUc.run({
      father: { name: this.fatherName().trim(), nationalId: this.fatherNationalId().trim(), phone: this.fatherPhone().trim() },
      mother: { name: this.motherName().trim(), nationalId: this.motherNationalId().trim(), phone: this.motherPhone().trim() },
      medical: this.buildMedical(),
    });
    this.submitting.set(false);

    if (r2.ok) {
      this.auth.markOnboardingComplete();
      this.notify.success(this.i18n.t('onboarding.completed'));
      void this.router.navigate(['/home']);
    } else {
      this.notify.error(this.i18n.t('onboarding.saveFailed'));
    }
  }
```

- [ ] **Step 4: Run the spec to verify it passes**

Run: `npm test -- onboarding.viewmodel`
Expected: PASS.

- [ ] **Step 5: Stage (commit deferred)**

```bash
git add frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding.viewmodel.ts \
        frontend/src/app/features/swimmer-onboarding/testing/presentation/pages/onboarding/onboarding.viewmodel.spec.ts
# DO NOT COMMIT yet
```

---

## Task 7: Frontend — wizard page template (two steps) + i18n

Render Step 2's two cards, wire the stepper to `currentStep`, and add the i18n labels. Verified by the AOT build (logic is covered by the viewmodel spec).

**Files:**
- Modify: `frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding-wizard.page.ts`
- Modify: `frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding-wizard.page.html`
- Modify: `frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding-wizard.page.scss`
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`

**Interfaces:**
- Consumes: `OnboardingViewModel` members from Task 6 (`currentStep`, `canSubmitStep1`, `canSubmitStep2`, `goToStep2`, `backToStep1`, `submit`, Step 2 signals).
- Produces: no new code interface (template + i18n only).

- [ ] **Step 1: Wire the stepper to `currentStep` in the page component**

In `onboarding-wizard.page.ts`, simplify `steps` to keys only (active/done are derived in the template):

```typescript
  protected readonly steps: readonly string[] = ['identity', 'guardian', 'physiological', 'inbody', 'done'];
```

- [ ] **Step 2: Update the stepper markup**

In `onboarding-wizard.page.html`, replace the `<ol class="oa-stepper">` block so active/done follow `vm.currentStep()`:

```html
    <ol class="oa-stepper">
      @for (key of steps; track key; let i = $index; let last = $last) {
        <li class="oa-step" [class.is-last]="last">
          <div class="oa-step-col" [class.is-active]="i + 1 === vm.currentStep()" [class.is-done]="i + 1 < vm.currentStep()">
            <span class="oa-step-circle">{{ i + 1 }}</span>
            <span class="oa-step-label">{{ 'onboarding.steps.' + key | translate }}</span>
          </div>
          @if (!last) { <span class="oa-step-line" aria-hidden="true"></span> }
        </li>
      }
    </ol>
```

- [ ] **Step 3: Gate the existing Step 1 card and its button on Step 1**

In `onboarding-wizard.page.html`, wrap the existing Step-1 `<section class="oa-card">` content: keep the card title/divider, but wrap the `.oa-grid` in `@if (vm.currentStep() === 1) { … }`, and change the footer button. The Step-1 "Next" now advances instead of submitting:

```html
      @if (vm.currentStep() === 1) {
        <div class="oa-grid"><!-- existing Step 1 fields unchanged --></div>
        <hr class="oa-divider oa-divider--bottom" />
        <div class="oa-actions">
          <button type="button" class="oa-next" [disabled]="!vm.canSubmitStep1()" (click)="vm.goToStep2()">
            {{ 'onboarding.next' | translate }}
            <svg class="oa-next-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><polyline points="9 18 15 12 9 6"></polyline></svg>
          </button>
        </div>
      }
```

> Also update the card title binding to switch label per step: `{{ (vm.currentStep() === 1 ? 'onboarding.sectionTitle' : 'onboarding.steps.guardian') | translate }}`.

- [ ] **Step 4: Add the Step 2 card markup**

Add, after the Step 1 block (still inside `oa-card`):

```html
      @if (vm.currentStep() === 2) {
        <div class="oa-two-col">
          <!-- Guardian Information -->
          <div class="oa-subcard">
            <h3 class="oa-subcard-title oa-subcard-title--teal">{{ 'onboarding.guardianInfo' | translate }}</h3>
            <label class="oa-field"><span class="oa-label">{{ 'onboarding.fatherName' | translate }}</span>
              <input class="oa-input" type="text" [value]="vm.fatherName()" (input)="vm.fatherName.set($any($event.target).value)" /></label>
            <label class="oa-field"><span class="oa-label">{{ 'onboarding.fatherNationalId' | translate }}</span>
              <input class="oa-input" type="text" inputmode="numeric" [value]="vm.fatherNationalId()" (input)="vm.fatherNationalId.set($any($event.target).value)" /></label>
            <label class="oa-field"><span class="oa-label">{{ 'onboarding.fatherPhone' | translate }}</span>
              <input class="oa-input" type="tel" [value]="vm.fatherPhone()" (input)="vm.fatherPhone.set($any($event.target).value)" /></label>
            <label class="oa-field"><span class="oa-label">{{ 'onboarding.motherName' | translate }}</span>
              <input class="oa-input" type="text" [value]="vm.motherName()" (input)="vm.motherName.set($any($event.target).value)" /></label>
            <label class="oa-field"><span class="oa-label">{{ 'onboarding.motherNationalId' | translate }}</span>
              <input class="oa-input" type="text" inputmode="numeric" [value]="vm.motherNationalId()" (input)="vm.motherNationalId.set($any($event.target).value)" /></label>
            <label class="oa-field"><span class="oa-label">{{ 'onboarding.motherPhone' | translate }}</span>
              <input class="oa-input" type="tel" [value]="vm.motherPhone()" (input)="vm.motherPhone.set($any($event.target).value)" /></label>
          </div>

          <!-- Medical History -->
          <div class="oa-subcard">
            <h3 class="oa-subcard-title oa-subcard-title--coral">{{ 'onboarding.medicalHistory' | translate }}</h3>
            @for (m of medicalRows; track m.key) {
              <div class="oa-med-row">
                <div class="oa-med-head">
                  <span class="oa-label">{{ ('onboarding.' + m.key) | translate }}</span>
                  <span class="oa-yesno">
                    <label><input type="radio" [name]="m.key" [checked]="!m.yes()" (change)="m.yes.set(false)" /> {{ 'onboarding.no' | translate }}</label>
                    <label><input type="radio" [name]="m.key" [checked]="m.yes()" (change)="m.yes.set(true)" /> {{ 'onboarding.yes' | translate }}</label>
                  </span>
                </div>
                <input class="oa-input" type="text" [disabled]="!m.yes()" [value]="m.details()" (input)="m.details.set($any($event.target).value)"
                       [placeholder]="'onboarding.details' | translate" />
              </div>
            }
          </div>
        </div>

        <hr class="oa-divider oa-divider--bottom" />
        <div class="oa-actions oa-actions--split">
          <button type="button" class="oa-back" (click)="vm.backToStep1()">{{ 'onboarding.back' | translate }}</button>
          <button type="button" class="oa-next" [disabled]="!vm.canSubmitStep2() || vm.submitting()" (click)="vm.submit()">
            {{ 'onboarding.next' | translate }}
            <svg class="oa-next-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><polyline points="9 18 15 12 9 6"></polyline></svg>
          </button>
        </div>
      }
```

Add a `medicalRows` accessor to `onboarding-wizard.page.ts` so the template can iterate signals:
```typescript
  protected readonly medicalRows = [
    { key: 'allergies', yes: this.vm.allergyYes, details: this.vm.allergyDetails },
    { key: 'surgeries', yes: this.vm.surgeryYes, details: this.vm.surgeryDetails },
    { key: 'chronic', yes: this.vm.chronicYes, details: this.vm.chronicDetails },
    { key: 'autoimmune', yes: this.vm.autoimmuneYes, details: this.vm.autoimmuneDetails },
  ];
```
(`this.vm` is already injected; declare `medicalRows` after the `vm` field.)

- [ ] **Step 5: Add Step 2 styles**

Append to `onboarding-wizard.page.scss` (match the existing teal/cream tokens used in the file):
```scss
.oa-two-col { display: grid; grid-template-columns: 1fr 1fr; gap: 1.5rem; }
@media (max-width: 800px) { .oa-two-col { grid-template-columns: 1fr; } }
.oa-subcard { border: 1px solid var(--oa-border, #e5e0d8); border-radius: 12px; padding: 1.25rem; background: rgba(0,0,0,0.015); display: flex; flex-direction: column; gap: 0.9rem; }
.oa-subcard-title { font-weight: 700; margin: 0 0 0.5rem; }
.oa-subcard-title--teal { color: var(--oa-teal, #1f6f78); }
.oa-subcard-title--coral { color: #c85a3c; }
.oa-med-row { display: flex; flex-direction: column; gap: 0.5rem; padding-bottom: 0.75rem; border-bottom: 1px solid var(--oa-border, #e5e0d8); }
.oa-med-row:last-child { border-bottom: 0; }
.oa-med-head { display: flex; align-items: center; justify-content: space-between; }
.oa-yesno { display: inline-flex; gap: 1rem; font-size: 0.9rem; }
.oa-actions--split { display: flex; justify-content: space-between; align-items: center; }
.oa-back { background: none; border: 0; color: var(--oa-muted, #6b6459); cursor: pointer; font: inherit; }
.oa-back:hover { color: var(--oa-fg, #2b2b2b); }
```

- [ ] **Step 6: Add the i18n keys**

In `frontend/src/app/core/i18n/en.json`, extend the `onboarding` block (before the closing `}`):
```json
    "back": "Back",
    "guardianInfo": "Guardian Information",
    "medicalHistory": "Medical History",
    "fatherName": "Father Full Name",
    "fatherNationalId": "Father National ID",
    "fatherPhone": "Father Phone",
    "motherName": "Mother Full Name",
    "motherNationalId": "Mother National ID",
    "motherPhone": "Mother Phone",
    "allergies": "Allergies",
    "surgeries": "Previous Surgeries",
    "chronic": "Chronic Diseases",
    "autoimmune": "Autoimmune Diseases",
    "details": "Details",
    "yes": "Yes",
    "no": "No"
```
Add the matching Arabic values to `ar.json` (same keys):
```json
    "back": "رجوع",
    "guardianInfo": "بيانات ولي الأمر",
    "medicalHistory": "التاريخ الطبي",
    "fatherName": "اسم الأب",
    "fatherNationalId": "الرقم القومي للأب",
    "fatherPhone": "هاتف الأب",
    "motherName": "اسم الأم",
    "motherNationalId": "الرقم القومي للأم",
    "motherPhone": "هاتف الأم",
    "allergies": "الحساسية",
    "surgeries": "عمليات جراحية سابقة",
    "chronic": "أمراض مزمنة",
    "autoimmune": "أمراض مناعية ذاتية",
    "details": "تفاصيل",
    "yes": "نعم",
    "no": "لا"
```
(Ensure valid JSON — add a comma after the previous last key in each block.)

- [ ] **Step 7: Verify the build and full frontend suite**

Run (from `frontend/`): `npm run build` then `npm test`
Expected: AOT build clean; all Jest suites green (including the Task 6 viewmodel spec and Task 5 use-case spec).

- [ ] **Step 8: Stage (commit deferred)**

```bash
git add frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding-wizard.page.ts \
        frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding-wizard.page.html \
        frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding-wizard.page.scss \
        frontend/src/app/core/i18n/en.json frontend/src/app/core/i18n/ar.json
# DO NOT COMMIT yet
```

---

## Task 8: Full verification (both suites) + hand back to the user

**Files:** none (verification only).

- [ ] **Step 1: Backend — full suite (API stopped)**

Run: `dotnet test backend/`
Expected: all green (475 existing + the new Task 1–4 tests), `ArchitectureTests` green.

- [ ] **Step 2: Frontend — full suite + AOT build**

Run (from `frontend/`): `npm test` and `npm run build`
Expected: all Jest suites green (584 existing + new specs); AOT build clean.

- [ ] **Step 3: Manual smoke (optional, if a demo swimmer is available)**

Sign in as a first-login swimmer → Step 1 "Next" advances to Step 2 → fill both parents, toggle one medical Yes with details → "Next Step" → lands on `/home`. Verify a `guardian` row for father+mother and one `observation` row for the Yes category (recorded_by = the swimmer). Re-login should NOT force onboarding again.

- [ ] **Step 4: Report and await commit authorization**

Summarize what changed and that everything is green. **Do not commit** — ask the user for the go-ahead, then commit per their preference (single feature commit or per-task).

---

## Notes for the executor

- **Route safety:** `me/onboarding/guardian-medical` is a literal-segment route; it does not collide with `GET {id:guid}` (same precedent as Step 1).
- **Non-atomic by design:** guardians (Identity) and observations (Health) commit in separate contexts. Ordering + idempotent guardian upsert + insert-only medical + first-login-cleared-last make a mid-way failure safe to retry (see spec §8). Do not attempt a distributed transaction.
- **`field_label` is the English label** (`Allergies` / `Previous Surgeries` / `Chronic Diseases` / `Autoimmune Diseases`) — matches how the Records tab displays observations. The UI text is localized via i18n; the stored `field_label` is not.
- **Guardian prefill on re-entry is intentionally omitted** (spec §5.3) — the deferred-save model means a clean restart; guardian upsert is idempotent, so a second pass overwrites safely.
