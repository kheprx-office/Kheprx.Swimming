# Swimmer Onboarding — Step 4 (InBody) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Step 4 (InBody) to the swimmer first-login wizard as the new finish line — Step 3 stops completing, and Step 4 saves one `health.inbody_reading` (7 fields, `ReadingDate` = today) and clears `IsFirstLogin`, orchestrated across the Health and Identity modules.

**Architecture:** Single new self-service endpoint `POST /api/swimmers/me/onboarding/inbody`, orchestrated at the API/controller composition root (like Step 2): resolve the swimmer from the JWT, save the reading via the Health `IInBodyReadingService`, then call `ISwimmerService.CompleteOnboardingAsync` (Identity) **last**. Step 3's physiological service method drops its first-login clear. The frontend defers all four writes to the final Step-4 submit (identity-vitals → guardian-medical → physiological → inbody), latching the first three so a retry re-runs only inbody.

**Tech Stack:** .NET (C#, Identity + Health modules, FluentValidation, EF Core, xUnit + Moq), Angular (standalone components, signals, feature slices, Jest).

**Spec:** `docs/superpowers/specs/2026-09-24-swimmer-onboarding-inbody-design.md`

## Global Constraints

- **⚠️ Do NOT run `git commit`.** Standing user directive. Each task ends with **"Stage (commit deferred)"** — `git add` only. Builds ON TOP of Steps 1–3 already **staged but uncommitted** on `feat/championships` (HEAD still `0af59ce`).
- **No new tables, no DB migration, no seed changes.** `health.inbody_reading` already exists.
- **7 InBody fields** (match the Captain/profile section): Height, Weight, Fat%, Muscle%, **Water%**, Bone Density, Body Density. `ReadingDate` = **today (server-set)**; `recorded_by` = the swimmer.
- **Bounds (reuse the InBody validator):** Height/Weight `> 0` and `<= 999.9`; Fat/Muscle/Water `0–100` inclusive; Bone/Body `> 0` and `<= 99.99`. Message `InBodyReadingMessages.Errors.ValueInvalid`.
- **Step 4 is the finish line:** Step 3's `CompleteOnboardingPhysiologicalAsync` must STOP clearing `IsFirstLogin`; Step 4 clears it via `CompleteOnboardingAsync` (Identity) **last**, after the Health reading save.
- **`CreateInBodyReadingRequest` ctor order:** `(ReadingDate, HeightCm, WeightKg, FatPct, MusclePct, WaterPct, BoneDensity, BodyDensity)`.
- **Self-service, JWT-scoped:** `[Authorize(Roles = "swimmer")]`, swimmer from `CurrentUserId()`, no path id.
- **Viewmodel signal naming:** the viewmodel already has `heightCm`/`weightKg` (Step 1 vitals). Step 4's InBody signals MUST be distinct — prefix with `ib` (`ibHeightCm`, `ibWeightKg`, `ibFatPct`, `ibMusclePct`, `ibWaterPct`, `ibBoneDensity`, `ibBodyDensity`).
- **Backend tests:** stop any running API first (DLL lock), then `dotnet test` from `backend/`. **Frontend:** `npm test` (Jest) + `npm run build` from `frontend/`.
- Keep both suites green: backend (currently 497) + frontend (currently 591), plus the new tests below.

## Review Focus

- **A Fat%/Muscle%/Water% value > 100, or Height/Weight/Bone/Body ≤ 0 or over max** — must 400, persist nothing. Test: Task 3 validator rejects Fat=101, Height=0, Bone=100; accepts Water=0 and Water=100.
- **A blank Fat%/Muscle%/Water% field** (0 is a valid value, so a blank must not silently submit as 0) — must block submit. Test: Task 6 `canSubmitStep4` false when Water% is blank, true when Water%='0'.
- **Retry after the inbody call fails** (identity/guardian/physiological already saved) — must NOT re-run them, only inbody. Test: Task 6 viewmodel spec (inbody fails → identity/guardian/physiological each called once total across two submits, inbody twice).
- **Physiological call fails at final submit** — must stop, not call inbody, not complete. Test: Task 6 viewmodel spec (physiological fails → inbody use-case not called, no navigate).
- **Physiological no longer completes onboarding** — the service must not clear `IsFirstLogin` anymore. Test: Task 1 asserts `IUserRepository.GetByIdAsync` is `Times.Never` in `CompleteOnboardingPhysiologicalAsync` and the flag stays true.

---

## Task 1: Physiological service stops completing onboarding

Step 4 becomes the finish line, so Step 3's `CompleteOnboardingPhysiologicalAsync` must no longer clear `IsFirstLogin`.

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs` (`CompleteOnboardingPhysiologicalAsync`, ~lines 351-368)
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs`

**Interfaces:**
- Consumes: `ISwimmerProfileRepository.GetByUserIdTrackedAsync`, `AddBodyMeasurementAsync`, `SaveChangesAsync`.
- Produces: same `CompleteOnboardingPhysiologicalAsync(Guid, CompletePhysiologicalRequest, CancellationToken) -> Task<bool>` signature; now saves the measurement only (no first-login clear, no user lookup).

- [ ] **Step 1: Update the existing service test to expect no completion**

In `SwimmerServiceTests.cs`, rename `CompleteOnboardingPhysiological_maps_five_to_seven_adds_measurement_and_clears_first_login` to `..._keeps_first_login` and change it to assert the flag is NOT cleared and the user is never looked up:

```csharp
    [Fact]
    public async Task CompleteOnboardingPhysiological_maps_five_to_seven_adds_measurement_and_keeps_first_login()
    {
        var (svc, swimmers, users) = Build();
        var userId = Guid.NewGuid();
        var profile = new SwimmerProfile(userId, "SW-0001", Guid.NewGuid());
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var ok = await svc.CompleteOnboardingPhysiologicalAsync(userId, new CompletePhysiologicalRequest(32.5m, 95m, 60m, 90m, 75m));

        Assert.True(ok);
        swimmers.Verify(r => r.AddBodyMeasurementAsync(It.Is<BodyMeasurement>(m =>
            m.SwimmerId == profile.Id &&
            m.RightArmCm == 32.5m && m.LeftArmCm == 32.5m &&
            m.RightLegCm == 95m && m.LeftLegCm == 95m &&
            m.TorsoCm == 60m && m.BustDiameterCm == 90m && m.WaistDiameterCm == 75m),
            It.IsAny<CancellationToken>()), Times.Once);
        swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        users.Verify(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never); // no longer clears first-login
    }
```

(The `CompleteOnboardingPhysiological_returns_false_when_not_a_swimmer` test is unchanged.)

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter CompleteOnboardingPhysiological`
Expected: FAIL — the impl still calls `_users.GetByIdAsync` + `CompleteFirstLogin()`, so the `Times.Never` verify fails.

- [ ] **Step 3: Remove the first-login clear from the service method**

Replace `CompleteOnboardingPhysiologicalAsync` body in `SwimmerService.cs` with (drop the user lookup + `CompleteFirstLogin`):

```csharp
    public async Task<bool> CompleteOnboardingPhysiologicalAsync(
        Guid userId, CompletePhysiologicalRequest req, CancellationToken ct = default)
    {
        var profile = await _swimmers.GetByUserIdTrackedAsync(userId, ct);
        if (profile is null) return false;

        // Five screen values → seven columns: arm → both arms, leg → both legs, torso/bust/waist 1:1.
        var measurement = new BodyMeasurement(profile.Id,
            req.ArmCm, req.ArmCm, req.LegCm, req.LegCm, req.TorsoCm, req.BustDiameterCm, req.WaistDiameterCm);
        await _swimmers.AddBodyMeasurementAsync(measurement, ct);

        // Onboarding is NOT completed here anymore — Step 4 (InBody) is the finish line.
        await _swimmers.SaveChangesAsync(ct);
        return true;
    }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter CompleteOnboardingPhysiological`
Expected: PASS (both physiological tests).

- [ ] **Step 5: Stage (commit deferred)**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs
# DO NOT COMMIT yet
```

---

## Task 2: Identity service — GetSwimmerIdByUserAsync

The Step-4 controller needs the swimmer (profile) id from the JWT to pass to the Health service.

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/ISwimmerService.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs` (append)

**Interfaces:**
- Consumes: `ISwimmerProfileRepository.GetByUserIdTrackedAsync`.
- Produces: `Task<Guid?> GetSwimmerIdByUserAsync(Guid userId, CancellationToken ct = default)` — returns `profile.Id` or `null`.

- [ ] **Step 1: Write the failing service tests**

Append to `SwimmerServiceTests.cs`:

```csharp
    [Fact]
    public async Task GetSwimmerIdByUser_returns_profile_id()
    {
        var (svc, swimmers, _) = Build();
        var userId = Guid.NewGuid();
        var profile = new SwimmerProfile(userId, "SW-0001", Guid.NewGuid());
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        Assert.Equal(profile.Id, await svc.GetSwimmerIdByUserAsync(userId));
    }

    [Fact]
    public async Task GetSwimmerIdByUser_returns_null_when_not_a_swimmer()
    {
        var (svc, swimmers, _) = Build();
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwimmerProfile?)null);
        Assert.Null(await svc.GetSwimmerIdByUserAsync(Guid.NewGuid()));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter GetSwimmerIdByUser`
Expected: FAIL to compile — the method doesn't exist on `ISwimmerService`.

- [ ] **Step 3: Declare the interface method**

Add to `ISwimmerService.cs` (near the other onboarding methods):

```csharp
    Task<Guid?> GetSwimmerIdByUserAsync(Guid userId, CancellationToken ct = default);
```

- [ ] **Step 4: Implement the service method**

Add to `SwimmerService.cs` (near the other onboarding methods):

```csharp
    public async Task<Guid?> GetSwimmerIdByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await _swimmers.GetByUserIdTrackedAsync(userId, ct);
        return profile?.Id;
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter GetSwimmerIdByUser`
Expected: PASS.

- [ ] **Step 6: Stage (commit deferred)**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/ISwimmerService.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs
# DO NOT COMMIT yet
```

---

## Task 3: InBody onboarding DTO + validator (Health module)

**Files:**
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/DTOs/InBodyReadingDtos.cs` (append)
- Create: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Validators/CompleteInBodyRequestValidator.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Validators/CompleteInBodyRequestValidatorTests.cs`

**Interfaces:**
- Consumes: `InBodyReadingMessages.Errors.ValueInvalid`, `AppLanguage.Current`.
- Produces: `CompleteInBodyRequest(decimal HeightCm, decimal WeightKg, decimal FatPct, decimal MusclePct, decimal WaterPct, decimal BoneDensity, decimal BodyDensity)` and `CompleteInBodyRequestValidator : AbstractValidator<CompleteInBodyRequest>`.

- [ ] **Step 1: Add the DTO**

Append to `InBodyReadingDtos.cs`:

```csharp
/// <summary>Onboarding Step 4 submission (POST /api/swimmers/me/onboarding/inbody): seven measurements; date is server-set to today.</summary>
public sealed record CompleteInBodyRequest(
    decimal HeightCm,
    decimal WeightKg,
    decimal FatPct,
    decimal MusclePct,
    decimal WaterPct,
    decimal BoneDensity,
    decimal BodyDensity);
```

- [ ] **Step 2: Write the failing validator tests**

Create `CompleteInBodyRequestValidatorTests.cs`:

```csharp
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Validators;

public sealed class CompleteInBodyRequestValidatorTests
{
    private static CompleteInBodyRequest Valid() => new(175m, 68m, 15m, 40m, 55m, 3.2m, 1.05m);
    private static readonly CompleteInBodyRequestValidator V = new();

    [Fact] public void Accepts_a_valid_request() => Assert.True(V.Validate(Valid()).IsValid);
    [Fact] public void Rejects_zero_height() => Assert.False(V.Validate(Valid() with { HeightCm = 0m }).IsValid);
    [Fact] public void Rejects_over_max_weight() => Assert.False(V.Validate(Valid() with { WeightKg = 1000m }).IsValid);
    [Fact] public void Accepts_zero_water() => Assert.True(V.Validate(Valid() with { WaterPct = 0m }).IsValid);
    [Fact] public void Accepts_hundred_muscle() => Assert.True(V.Validate(Valid() with { MusclePct = 100m }).IsValid);
    [Fact] public void Rejects_fat_over_hundred() => Assert.False(V.Validate(Valid() with { FatPct = 101m }).IsValid);
    [Fact] public void Rejects_zero_bone_density() => Assert.False(V.Validate(Valid() with { BoneDensity = 0m }).IsValid);
    [Fact] public void Rejects_over_max_body_density() => Assert.False(V.Validate(Valid() with { BodyDensity = 100m }).IsValid);
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter CompleteInBodyRequestValidator`
Expected: FAIL to compile — `CompleteInBodyRequestValidator` does not exist yet.

- [ ] **Step 4: Add the validator**

Create `CompleteInBodyRequestValidator.cs`:

```csharp
using FluentValidation;
using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Resources;
using Kheprx.BaseBackend.SharedKernel.Resources;

namespace Kheprx.BaseBackend.Health.Application.Validators;

public sealed class CompleteInBodyRequestValidator : AbstractValidator<CompleteInBodyRequest>
{
    public CompleteInBodyRequestValidator()
    {
        RuleFor(x => x.HeightCm).GreaterThan(0m).LessThanOrEqualTo(999.9m).WithMessage(_ => InBodyReadingMessages.Errors.ValueInvalid(AppLanguage.Current));
        RuleFor(x => x.WeightKg).GreaterThan(0m).LessThanOrEqualTo(999.9m).WithMessage(_ => InBodyReadingMessages.Errors.ValueInvalid(AppLanguage.Current));
        RuleFor(x => x.FatPct).InclusiveBetween(0m, 100m).WithMessage(_ => InBodyReadingMessages.Errors.ValueInvalid(AppLanguage.Current));
        RuleFor(x => x.MusclePct).InclusiveBetween(0m, 100m).WithMessage(_ => InBodyReadingMessages.Errors.ValueInvalid(AppLanguage.Current));
        RuleFor(x => x.WaterPct).InclusiveBetween(0m, 100m).WithMessage(_ => InBodyReadingMessages.Errors.ValueInvalid(AppLanguage.Current));
        RuleFor(x => x.BoneDensity).GreaterThan(0m).LessThanOrEqualTo(99.99m).WithMessage(_ => InBodyReadingMessages.Errors.ValueInvalid(AppLanguage.Current));
        RuleFor(x => x.BodyDensity).GreaterThan(0m).LessThanOrEqualTo(99.99m).WithMessage(_ => InBodyReadingMessages.Errors.ValueInvalid(AppLanguage.Current));
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter CompleteInBodyRequestValidator`
Expected: PASS.

- [ ] **Step 6: Stage (commit deferred)**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/DTOs/InBodyReadingDtos.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Validators/CompleteInBodyRequestValidator.cs \
        backend/tests/Kheprx.BaseBackend.Health.UnitTests/Validators/CompleteInBodyRequestValidatorTests.cs
# DO NOT COMMIT yet
```

---

## Task 4: Controller endpoint — orchestrate inbody + complete

Add the endpoint. Inject the Health `IInBodyReadingService`; save the reading → complete (last).

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs` (constructor + Onboarding region)
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs`

**Interfaces:**
- Consumes: `ISwimmerService.GetSwimmerIdByUserAsync` (Task 2), `ISwimmerService.CompleteOnboardingAsync` (existing); `IInBodyReadingService.CreateAsync(Guid swimmerId, CreateInBodyReadingRequest, Guid recordedBy, CancellationToken)` (Health); `CompleteInBodyRequest` (Task 3); `CreateInBodyReadingRequest` (existing); `CurrentUserId()`.
- Produces: `POST api/swimmers/me/onboarding/inbody` → `ApiResponse<OnboardingStepResultDto>` (200) / 404.

- [ ] **Step 1: Update the controller test harness + write the failing endpoint tests**

In `SwimmersControllerTests.cs`, extend the `Create` helper to also inject a mock `IInBodyReadingService` (the controller constructor gains a third param):

```csharp
    private static SwimmersController Create(ISwimmerService svc, IObservationService? obs = null, IInBodyReadingService? inbody = null)
        => new(svc, obs ?? Mock.Of<IObservationService>(), inbody ?? Mock.Of<IInBodyReadingService>())
           { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
```

(Keep the `OnboardingController` alias delegating to `Create`.) Add the endpoint tests:

```csharp
    [Fact]
    public async Task CompleteInBody_saves_reading_then_completes()
    {
        var svc = new Mock<ISwimmerService>();
        var swimmerId = Guid.NewGuid();
        svc.Setup(s => s.GetSwimmerIdByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(swimmerId);
        svc.Setup(s => s.CompleteOnboardingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var inbody = new Mock<IInBodyReadingService>();

        var req = new CompleteInBodyRequest(175m, 68m, 15m, 40m, 55m, 3.2m, 1.05m);
        var result = await Create(svc.Object, null, inbody.Object).CompleteOnboardingInBody(req, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<OnboardingStepResultDto>>(ok.Value);
        Assert.True(body.SuccessStatus);
        Assert.False(body.Data!.MustChangePassword);
        inbody.Verify(i => i.CreateAsync(swimmerId,
            It.Is<CreateInBodyReadingRequest>(r => r.HeightCm == 175m && r.WeightKg == 68m && r.FatPct == 15m && r.MusclePct == 40m && r.WaterPct == 55m && r.BoneDensity == 3.2m && r.BodyDensity == 1.05m),
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        svc.Verify(s => s.CompleteOnboardingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteInBody_returns_404_when_not_a_swimmer()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.GetSwimmerIdByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        var inbody = new Mock<IInBodyReadingService>();

        var req = new CompleteInBodyRequest(175m, 68m, 15m, 40m, 55m, 3.2m, 1.05m);
        var result = await Create(svc.Object, null, inbody.Object).CompleteOnboardingInBody(req, CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
        inbody.Verify(i => i.CreateAsync(It.IsAny<Guid>(), It.IsAny<CreateInBodyReadingRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        svc.Verify(s => s.CompleteOnboardingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter CompleteInBody`
Expected: FAIL to compile — the constructor's third param and `CompleteOnboardingInBody` action don't exist yet.

- [ ] **Step 3: Update the controller constructor**

In `SwimmersController.cs`, add the field + constructor param (the Health usings were added in Step 2):

```csharp
    private readonly ISwimmerService _service;
    private readonly IObservationService _observations;
    private readonly IInBodyReadingService _inbody;

    public SwimmersController(ISwimmerService service, IObservationService observations, IInBodyReadingService inbody)
    {
        _service = service;
        _observations = observations;
        _inbody = inbody;
    }
```

Add `using Kheprx.BaseBackend.Health.Application.Services.Interfaces;` if not already present (Step 2 added `IObservationService` from that namespace, so it is).

- [ ] **Step 4: Add the endpoint (inside the Onboarding region, after the physiological action)**

```csharp
    /// <summary>Completes the swimmer's first-login Step 4 (InBody reading), clearing first-login. Swimmer only.</summary>
    /// <response code="200">Completed; returns the refreshed first-login flag (false).</response>
    /// <response code="404">The caller is not a swimmer / has no profile.</response>
    [HttpPost("me/onboarding/inbody")]
    [Authorize(Roles = "swimmer")]
    [ProducesResponseType(typeof(ApiResponse<OnboardingStepResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OnboardingStepResultDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OnboardingStepResultDto>>> CompleteOnboardingInBody(
        CompleteInBodyRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId();

        // 1) Resolve the swimmer (Identity); null → not a swimmer.
        var swimmerId = await _service.GetSwimmerIdByUserAsync(userId, ct);
        if (swimmerId is null)
        {
            var nf = ApiResponse<OnboardingStepResultDto>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }

        // 2) Save the InBody reading (Health); date server-set to today, recorded by the swimmer.
        var reading = new CreateInBodyReadingRequest(DateOnly.FromDateTime(DateTime.UtcNow),
            request.HeightCm, request.WeightKg, request.FatPct, request.MusclePct, request.WaterPct, request.BoneDensity, request.BodyDensity);
        await _inbody.CreateAsync(swimmerId.Value, reading, userId, ct);

        // 3) Clear first-login LAST — the commit point for onboarding.
        await _service.CompleteOnboardingAsync(userId, ct);

        return Ok(ApiResponse<OnboardingStepResultDto>.Success(
            SwimmerMessages.Success.OnboardingCompleted(AppLanguage.Current), new OnboardingStepResultDto(false)));
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter CompleteInBody`
Expected: PASS.

- [ ] **Step 6: Run the whole backend suite + architecture tests**

Run (API stopped): `dotnet test backend/`
Expected: PASS (497 + the new Task 1–4 tests), `ArchitectureTests` green (orchestration in the API layer only).

- [ ] **Step 7: Stage (commit deferred)**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs \
        backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs
# DO NOT COMMIT yet
```

---

## Task 5: Frontend — model, DTO, repository, use-case

**Files:**
- Modify: `frontend/src/app/features/swimmer-onboarding/domain/model/onboarding.ts`
- Create: `frontend/src/app/features/swimmer-onboarding/data/dto/complete-inbody.dto.ts`
- Modify: `frontend/src/app/features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository.ts`
- Modify: `frontend/src/app/features/swimmer-onboarding/data/repositories/swimmer-onboarding.repository.impl.ts`
- Create: `frontend/src/app/features/swimmer-onboarding/domain/usecases/complete-inbody.use-case.ts`
- Test: `frontend/src/app/features/swimmer-onboarding/testing/domain/usecases/complete-inbody.use-case.spec.ts`

**Interfaces:**
- Produces: model `InBodySubmission { heightCm; weightKg; fatPct; musclePct; waterPct; boneDensity; bodyDensity }`; DTO `CompleteInBodyDtoRq` + `CompleteInBodyItemDtoRs`; repo `completeInBody(rq)`; `CompleteInBodyUseCase extends UseCase<InBodySubmission, void>`.

- [ ] **Step 1: Add the domain model**

Append to `onboarding.ts`:

```typescript
export interface InBodySubmission {
  heightCm: number;
  weightKg: number;
  fatPct: number;
  musclePct: number;
  waterPct: number;
  boneDensity: number;
  bodyDensity: number;
}
```

- [ ] **Step 2: Add the request DTO**

Create `data/dto/complete-inbody.dto.ts`:

```typescript
import { BaseResponseRs } from '@core/network/api/base-response-rs';
import { OnboardingResultDtoRs } from './complete-identity-vitals.dto';

export interface CompleteInBodyDtoRq {
  heightCm: number;
  weightKg: number;
  fatPct: number;
  musclePct: number;
  waterPct: number;
  boneDensity: number;
  bodyDensity: number;
}
export interface CompleteInBodyItemDtoRs extends BaseResponseRs<OnboardingResultDtoRs> {}
```

- [ ] **Step 3: Extend the repository interface + impl**

In `swimmer-onboarding.repository.ts`, add the import and method:

```typescript
import { CompleteInBodyDtoRq, CompleteInBodyItemDtoRs } from '@features/swimmer-onboarding/data/dto/complete-inbody.dto';
```
```typescript
  completeInBody(rq: CompleteInBodyDtoRq): Promise<CompleteInBodyItemDtoRs>;
```

In `swimmer-onboarding.repository.impl.ts`, add the import and implement:

```typescript
import { CompleteInBodyDtoRq, CompleteInBodyItemDtoRs } from '@features/swimmer-onboarding/data/dto/complete-inbody.dto';
```
```typescript
  completeInBody(rq: CompleteInBodyDtoRq): Promise<CompleteInBodyItemDtoRs> {
    return this.http.post<CompleteInBodyItemDtoRs>('/api/swimmers/me/onboarding/inbody', { body: rq });
  }
```

- [ ] **Step 4: Write the failing use-case spec**

Create `testing/domain/usecases/complete-inbody.use-case.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { CompleteInBodyUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-inbody.use-case';
import { SWIMMER_ONBOARDING_REPOSITORY, ISwimmerOnboardingRepository } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { InBodySubmission } from '@features/swimmer-onboarding/domain/model/onboarding';

const SUBMISSION: InBodySubmission = { heightCm: 175, weightKg: 68, fatPct: 15, musclePct: 40, waterPct: 55, boneDensity: 3.2, bodyDensity: 1.05 };

function build(repo: Partial<ISwimmerOnboardingRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_ONBOARDING_REPOSITORY, useValue: repo }] });
  return TestBed.inject(CompleteInBodyUseCase);
}

describe('CompleteInBodyUseCase', () => {
  it('posts the submission and succeeds', async () => {
    const post = jest.fn().mockResolvedValue({ data: { mustChangePassword: false } });
    const uc = build({ completeInBody: post } as unknown as ISwimmerOnboardingRepository);
    const r = await uc.run(SUBMISSION);
    expect(r.ok).toBe(true);
    expect(post).toHaveBeenCalledWith({ heightCm: 175, weightKg: 68, fatPct: 15, musclePct: 40, waterPct: 55, boneDensity: 3.2, bodyDensity: 1.05 });
  });

  it('fails when the repository throws', async () => {
    const uc = build({ completeInBody: async () => { throw new Error('boom'); } } as unknown as ISwimmerOnboardingRepository);
    const r = await uc.run(SUBMISSION);
    expect(r.ok).toBe(false);
  });
});
```

- [ ] **Step 5: Run the spec to verify it fails**

Run: `npm test -- complete-inbody.use-case` (from `frontend/`)
Expected: FAIL — `CompleteInBodyUseCase` does not exist yet.

- [ ] **Step 6: Implement the use-case**

Create `domain/usecases/complete-inbody.use-case.ts`:

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_ONBOARDING_REPOSITORY } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { InBodySubmission } from '@features/swimmer-onboarding/domain/model/onboarding';

@Injectable({ providedIn: 'root' })
export class CompleteInBodyUseCase extends UseCase<InBodySubmission, void> {
  private readonly repo = inject(SWIMMER_ONBOARDING_REPOSITORY);
  constructor() { super('CompleteInBody'); }

  protected async execute(input: InBodySubmission): Promise<void> {
    await this.repo.completeInBody({
      heightCm: input.heightCm,
      weightKg: input.weightKg,
      fatPct: input.fatPct,
      musclePct: input.musclePct,
      waterPct: input.waterPct,
      boneDensity: input.boneDensity,
      bodyDensity: input.bodyDensity,
    });
  }
}
```

- [ ] **Step 7: Run the spec to verify it passes**

Run: `npm test -- complete-inbody.use-case`
Expected: PASS.

- [ ] **Step 8: Stage (commit deferred)**

```bash
git add frontend/src/app/features/swimmer-onboarding/domain/model/onboarding.ts \
        frontend/src/app/features/swimmer-onboarding/data/dto/complete-inbody.dto.ts \
        frontend/src/app/features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository.ts \
        frontend/src/app/features/swimmer-onboarding/data/repositories/swimmer-onboarding.repository.impl.ts \
        frontend/src/app/features/swimmer-onboarding/domain/usecases/complete-inbody.use-case.ts \
        frontend/src/app/features/swimmer-onboarding/testing/domain/usecases/complete-inbody.use-case.spec.ts
# DO NOT COMMIT yet
```

---

## Task 6: Viewmodel — four-step flow + inbody submit

**Files:**
- Modify: `frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding.viewmodel.ts`
- Test: `frontend/src/app/features/swimmer-onboarding/testing/presentation/pages/onboarding/onboarding.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `CompleteInBodyUseCase` (Task 5); existing `CompleteIdentityVitalsUseCase`, `CompleteGuardianMedicalUseCase`, `CompletePhysiologicalUseCase`, `AuthSessionStore`, `Router`, `NotificationService`, `TranslateService`.
- Produces (new members): `currentStep: WritableSignal<1|2|3|4>`; `goToStep4()`, `backToStep3()`; `canSubmitStep4`; Step 4 signals `ibHeightCm/ibWeightKg/ibFatPct/ibMusclePct/ibWaterPct/ibBoneDensity/ibBodyDensity`; a `physiologicalSaved` latch; `submit()` completes after the inbody call.

- [ ] **Step 1: Update the viewmodel spec (failing tests for the new behavior)**

Edit `onboarding.viewmodel.spec.ts`. Add the inbody use-case import + provider, an `inbody` mock in `build()`, a `fillStep4` helper, and widen coverage. Replace the file with:

```typescript
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { OnboardingViewModel } from '@features/swimmer-onboarding/presentation/pages/onboarding/onboarding.viewmodel';
import { GetOnboardingPrefillUseCase } from '@features/swimmer-onboarding/domain/usecases/get-onboarding-prefill.use-case';
import { CompleteIdentityVitalsUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-identity-vitals.use-case';
import { CompleteGuardianMedicalUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-guardian-medical.use-case';
import { CompletePhysiologicalUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-physiological.use-case';
import { CompleteInBodyUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-inbody.use-case';
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

function build(overrides: { identity?: unknown; guardianMedical?: unknown; physiological?: unknown; inbody?: unknown; cats?: unknown } = {}) {
  const getPrefill = { run: jest.fn().mockResolvedValue(ok(PREFILL)) };
  const identity = { run: jest.fn().mockResolvedValue(overrides.identity ?? ok(undefined)) };
  const guardianMedical = { run: jest.fn().mockResolvedValue(overrides.guardianMedical ?? ok(undefined)) };
  const physiological = { run: jest.fn().mockResolvedValue(overrides.physiological ?? ok(undefined)) };
  const inbody = { run: jest.fn().mockResolvedValue(overrides.inbody ?? ok(undefined)) };
  const lookups = { run: jest.fn().mockResolvedValue(ok(LOOKUP)) };
  const cats = { run: jest.fn().mockResolvedValue(overrides.cats ?? ok(CATS)) };
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
      { provide: CompletePhysiologicalUseCase, useValue: physiological },
      { provide: CompleteInBodyUseCase, useValue: inbody },
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
  return { vm: TestBed.inject(OnboardingViewModel), identity, guardianMedical, physiological, inbody, auth, router, notify };
}

function fillStep1(vm: OnboardingViewModel) {
  vm.examDate.set('2026-01-01'); vm.internalMedId.set('f1'); vm.heartAssessId.set('f1'); vm.spineAssessId.set('f1');
  vm.hemoglobin.set('14.5'); vm.heightCm.set('175'); vm.weightKg.set('68');
}
function fillStep2(vm: OnboardingViewModel) {
  vm.fatherName.set('Ahmed'); vm.fatherNationalId.set('12345678901234'); vm.fatherPhone.set('010');
  vm.motherName.set('Sara'); vm.motherNationalId.set('43210987654321'); vm.motherPhone.set('011');
}
function fillStep3(vm: OnboardingViewModel) {
  vm.armCm.set('32.5'); vm.legCm.set('95'); vm.torsoCm.set('60'); vm.bustDiameterCm.set('90'); vm.waistDiameterCm.set('75');
}
function fillStep4(vm: OnboardingViewModel) {
  vm.ibHeightCm.set('175'); vm.ibWeightKg.set('68'); vm.ibFatPct.set('15'); vm.ibMusclePct.set('40');
  vm.ibWaterPct.set('55'); vm.ibBoneDensity.set('3.2'); vm.ibBodyDensity.set('1.05');
}

describe('OnboardingViewModel', () => {
  it('load() seeds identity drafts, lookups, and observation categories', async () => {
    const { vm } = build();
    await vm.load();
    expect(vm.nameEn()).toBe('Sam');
    expect(vm.observationCategories().length).toBe(4);
  });

  it('navigation gates each step on the prior being valid', async () => {
    const { vm } = build();
    await vm.load();
    fillStep1(vm); vm.goToStep2(); expect(vm.currentStep()).toBe(2);
    fillStep2(vm); vm.goToStep3(); expect(vm.currentStep()).toBe(3);
    vm.goToStep4(); expect(vm.currentStep()).toBe(3);   // blocked — step 3 incomplete
    fillStep3(vm); vm.goToStep4(); expect(vm.currentStep()).toBe(4);
    vm.backToStep3(); expect(vm.currentStep()).toBe(3);
  });

  it('canSubmitStep4 requires all seven non-empty and in-bounds (0 allowed for %, blank rejected)', async () => {
    const { vm } = build();
    await vm.load();
    expect(vm.canSubmitStep4()).toBe(false);
    fillStep4(vm);
    expect(vm.canSubmitStep4()).toBe(true);
    vm.ibWaterPct.set('');            // blank % must be rejected even though 0 is valid
    expect(vm.canSubmitStep4()).toBe(false);
    vm.ibWaterPct.set('0');           // explicit 0 is accepted
    expect(vm.canSubmitStep4()).toBe(true);
    vm.ibFatPct.set('101');           // over 100 rejected
    expect(vm.canSubmitStep4()).toBe(false);
  });

  it('submit() posts identity, guardian, physiological, then inbody, completing after inbody', async () => {
    const { vm, identity, guardianMedical, physiological, inbody, auth, router } = build();
    await vm.load();
    fillStep1(vm); fillStep2(vm); fillStep3(vm); fillStep4(vm);
    await vm.submit();
    expect(identity.run).toHaveBeenCalledTimes(1);
    expect(guardianMedical.run).toHaveBeenCalledTimes(1);
    expect(physiological.run).toHaveBeenCalledTimes(1);
    expect(inbody.run).toHaveBeenCalledWith({ heightCm: 175, weightKg: 68, fatPct: 15, musclePct: 40, waterPct: 55, boneDensity: 3.2, bodyDensity: 1.05 });
    expect(auth.markOnboardingComplete).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/home']);
  });

  it('submit() stops and does not complete when physiological fails', async () => {
    const { vm, inbody, auth, router, notify } = build({ physiological: fail(new AppError('nope', 'network')) });
    await vm.load();
    fillStep1(vm); fillStep2(vm); fillStep3(vm); fillStep4(vm);
    await vm.submit();
    expect(inbody.run).not.toHaveBeenCalled();
    expect(auth.markOnboardingComplete).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
    expect(notify.error).toHaveBeenCalled();
  });

  it('retry after an inbody failure re-runs only inbody (latches)', async () => {
    const { vm, identity, guardianMedical, physiological, inbody, auth } = build({ inbody: fail(new AppError('nope', 'network')) });
    await vm.load();
    fillStep1(vm); fillStep2(vm); fillStep3(vm); fillStep4(vm);
    await vm.submit();
    expect(inbody.run).toHaveBeenCalledTimes(1);
    expect(auth.markOnboardingComplete).not.toHaveBeenCalled();
    (inbody.run as jest.Mock).mockResolvedValueOnce(ok(undefined));
    await vm.submit();
    expect(identity.run).toHaveBeenCalledTimes(1);        // NOT re-run
    expect(guardianMedical.run).toHaveBeenCalledTimes(1); // NOT re-run
    expect(physiological.run).toHaveBeenCalledTimes(1);   // NOT re-run
    expect(inbody.run).toHaveBeenCalledTimes(2);          // re-run
    expect(auth.markOnboardingComplete).toHaveBeenCalled();
  });

  it('submit() blocks (persists nothing) when a medical "Yes" has an unresolved category id', async () => {
    const { vm, identity, inbody, notify } = build({ cats: ok([]) }); // categories failed to load
    await vm.load();
    fillStep1(vm); fillStep2(vm); fillStep3(vm); fillStep4(vm);
    vm.allergyYes.set(true); vm.allergyDetails.set('Peanuts'); // a Yes that can't resolve to a category id
    await vm.submit();
    expect(identity.run).not.toHaveBeenCalled();
    expect(inbody.run).not.toHaveBeenCalled();
    expect(notify.error).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run the spec to verify it fails**

Run: `npm test -- onboarding.viewmodel`
Expected: FAIL — new members (`currentStep` widened to 4, `goToStep4`, `backToStep3`, `canSubmitStep4`, `ib*` signals, inbody in submit) don't exist.

- [ ] **Step 3: Implement the viewmodel changes**

Edit `onboarding.viewmodel.ts`:

Add the import (with the other use-case imports):
```typescript
import { CompleteInBodyUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-inbody.use-case';
```
Inject it (next to `completePhysiologicalUc`):
```typescript
  private readonly completeInBodyUc = inject(CompleteInBodyUseCase);
```
Add the physiological latch (next to `guardianSaved`):
```typescript
  private readonly physiologicalSaved = signal(false);
```
Widen `currentStep`:
```typescript
  readonly currentStep = signal<1 | 2 | 3 | 4>(1);
```
Add Step 4 signals (after the physiological signals; `ib` prefix avoids colliding with the Step-1 `heightCm`/`weightKg`):
```typescript
  // InBody (Step 4) — seven measurements (ib-prefixed to avoid colliding with Step-1 vitals height/weight).
  readonly ibHeightCm = signal(''); readonly ibWeightKg = signal(''); readonly ibFatPct = signal(''); readonly ibMusclePct = signal('');
  readonly ibWaterPct = signal(''); readonly ibBoneDensity = signal(''); readonly ibBodyDensity = signal('');
```
Add `canSubmitStep4` (after `canSubmitStep3`):
```typescript
  readonly canSubmitStep4 = computed(() => {
    const ok = (s: string, lo: number, hi: number, inclusiveLo: boolean) => {
      if (s.trim().length === 0) return false;
      const n = Number(s);
      return (inclusiveLo ? n >= lo : n > lo) && n <= hi;
    };
    return ok(this.ibHeightCm(), 0, 999.9, false) && ok(this.ibWeightKg(), 0, 999.9, false) &&
      ok(this.ibFatPct(), 0, 100, true) && ok(this.ibMusclePct(), 0, 100, true) && ok(this.ibWaterPct(), 0, 100, true) &&
      ok(this.ibBoneDensity(), 0, 99.99, false) && ok(this.ibBodyDensity(), 0, 99.99, false);
  });
```
Add the navigation helpers (after `backToStep2`):
```typescript
  goToStep4(): void { if (this.canSubmitStep3()) this.currentStep.set(4); }
  backToStep3(): void { this.currentStep.set(3); }
```
Replace `submit()` — physiological becomes a latched middle step; inbody is the finish call (guard changes to `canSubmitStep4`):
```typescript
  async submit(): Promise<void> {
    if (!this.canSubmitStep4() || this.submitting()) return;

    // Guard against unresolved category ids — check before persisting anything.
    const medicalItems = this.buildMedical();
    if (medicalItems.some((item) => item.categoryId === '')) {
      this.notify.error(this.i18n.t('onboarding.saveFailed'));
      return;
    }

    this.submitting.set(true);

    // 1) identity-vitals (latched).
    if (!this.identitySaved()) {
      const r1 = await this.completeUc.run({
        nameEn: this.nameEn().trim(), nameAr: this.nameArOriginal,
        genderId: this.genderId(), dob: this.dob(), trainingClubId: this.trainingClubId(),
        examDate: this.examDate(), bloodTypeId: this.bloodTypeId() || null,
        hemoglobin: Number(this.hemoglobin()), heightCm: Number(this.heightCm()), weightKg: Number(this.weightKg()),
        internalMedId: this.internalMedId(), heartAssessId: this.heartAssessId(), spineAssessId: this.spineAssessId(),
      });
      if (!r1.ok) { this.submitting.set(false); this.notify.error(this.i18n.t('onboarding.saveFailed')); return; }
      this.identitySaved.set(true);
    }

    // 2) guardian-medical (latched).
    if (!this.guardianSaved()) {
      const r2 = await this.completeGuardianMedicalUc.run({
        father: { name: this.fatherName().trim(), nationalId: this.fatherNationalId().trim(), phone: this.fatherPhone().trim() },
        mother: { name: this.motherName().trim(), nationalId: this.motherNationalId().trim(), phone: this.motherPhone().trim() },
        medical: medicalItems,
      });
      if (!r2.ok) { this.submitting.set(false); this.notify.error(this.i18n.t('onboarding.saveFailed')); return; }
      this.guardianSaved.set(true);
    }

    // 3) physiological (latched — now a middle step, no longer completes).
    if (!this.physiologicalSaved()) {
      const r3 = await this.completePhysiologicalUc.run({
        armCm: Number(this.armCm()), legCm: Number(this.legCm()), torsoCm: Number(this.torsoCm()),
        bustDiameterCm: Number(this.bustDiameterCm()), waistDiameterCm: Number(this.waistDiameterCm()),
      });
      if (!r3.ok) { this.submitting.set(false); this.notify.error(this.i18n.t('onboarding.saveFailed')); return; }
      this.physiologicalSaved.set(true);
    }

    // 4) inbody — saves the reading AND completes onboarding (server-side).
    const r4 = await this.completeInBodyUc.run({
      heightCm: Number(this.ibHeightCm()), weightKg: Number(this.ibWeightKg()),
      fatPct: Number(this.ibFatPct()), musclePct: Number(this.ibMusclePct()), waterPct: Number(this.ibWaterPct()),
      boneDensity: Number(this.ibBoneDensity()), bodyDensity: Number(this.ibBodyDensity()),
    });
    this.submitting.set(false);

    if (r4.ok) {
      this.auth.markOnboardingComplete();
      this.notify.success(this.i18n.t('onboarding.completed'));
      void this.router.navigate(['/home']);
    } else {
      this.notify.error(this.i18n.t('onboarding.saveFailed'));
    }
  }
```
Update the `currentStep` comment to note four steps.

- [ ] **Step 4: Run the spec to verify it passes**

Run: `npm test -- onboarding.viewmodel`
Expected: PASS.

Note: do NOT run the AOT build here — the page template still calls the old Step-3 `vm.submit()` on the Step-3 button and has no Step-4 card; Task 7 fixes the template. The viewmodel spec is Jest-only and does not compile the template.

- [ ] **Step 5: Stage (commit deferred)**

```bash
git add frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding.viewmodel.ts \
        frontend/src/app/features/swimmer-onboarding/testing/presentation/pages/onboarding/onboarding.viewmodel.spec.ts
# DO NOT COMMIT yet
```

---

## Task 7: Template (Step 4 card) + i18n

**Files:**
- Modify: `frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding-wizard.page.html`
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`

**Interfaces:**
- Consumes: `OnboardingViewModel` members from Task 6 (`currentStep`, `canSubmitStep3`, `canSubmitStep4`, `goToStep4`, `backToStep3`, `submit`, `ib*` signals).
- Produces: no new code interface (template + i18n only). Verified by the AOT build + full Jest suite.

- [ ] **Step 1: Point the Step-3 "Next Step" button at Step 4**

In `onboarding-wizard.page.html`, the Step-3 footer currently submits (`(click)="vm.submit()"`, disabled `!vm.canSubmitStep3() || vm.submitting()`). Change it to advance:

```html
          <button type="button" class="oa-next" [disabled]="!vm.canSubmitStep3()" (click)="vm.goToStep4()">
            {{ 'onboarding.next' | translate }}
            <svg class="oa-next-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><polyline points="9 18 15 12 9 6"></polyline></svg>
          </button>
```

- [ ] **Step 2: Switch the card title to four steps**

Update the card-title binding:

```html
      <h2 class="oa-card-title">{{ (vm.currentStep() === 1 ? 'onboarding.sectionTitle' : vm.currentStep() === 2 ? 'onboarding.steps.guardian' : vm.currentStep() === 3 ? 'onboarding.steps.physiological' : 'onboarding.steps.inbody') | translate }}</h2>
```

- [ ] **Step 3: Add the Step 4 card**

After the Step-3 `@if (vm.currentStep() === 3) { … }` block (and before the card `<section>` closes), add:

```html
      @if (vm.currentStep() === 4) {
        <div class="oa-grid">
          <label class="oa-field">
            <span class="oa-label">{{ 'onboarding.height' | translate }}</span>
            <input class="oa-input" type="number" step="any" inputmode="decimal" [value]="vm.ibHeightCm()" (input)="vm.ibHeightCm.set($any($event.target).value)" placeholder="0.0" />
          </label>
          <label class="oa-field">
            <span class="oa-label">{{ 'onboarding.weight' | translate }}</span>
            <input class="oa-input" type="number" step="any" inputmode="decimal" [value]="vm.ibWeightKg()" (input)="vm.ibWeightKg.set($any($event.target).value)" placeholder="0.0" />
          </label>
          <label class="oa-field">
            <span class="oa-label">{{ 'onboarding.fatPercentage' | translate }}</span>
            <input class="oa-input" type="number" step="any" inputmode="decimal" [value]="vm.ibFatPct()" (input)="vm.ibFatPct.set($any($event.target).value)" placeholder="0.0" />
          </label>
          <label class="oa-field">
            <span class="oa-label">{{ 'onboarding.musclePercentage' | translate }}</span>
            <input class="oa-input" type="number" step="any" inputmode="decimal" [value]="vm.ibMusclePct()" (input)="vm.ibMusclePct.set($any($event.target).value)" placeholder="0.0" />
          </label>
          <label class="oa-field">
            <span class="oa-label">{{ 'onboarding.waterPercentage' | translate }}</span>
            <input class="oa-input" type="number" step="any" inputmode="decimal" [value]="vm.ibWaterPct()" (input)="vm.ibWaterPct.set($any($event.target).value)" placeholder="0.0" />
          </label>
          <label class="oa-field">
            <span class="oa-label">{{ 'onboarding.boneDensity' | translate }}</span>
            <input class="oa-input" type="number" step="any" inputmode="decimal" [value]="vm.ibBoneDensity()" (input)="vm.ibBoneDensity.set($any($event.target).value)" placeholder="0.0" />
          </label>
          <label class="oa-field">
            <span class="oa-label">{{ 'onboarding.bodyDensity' | translate }}</span>
            <input class="oa-input" type="number" step="any" inputmode="decimal" [value]="vm.ibBodyDensity()" (input)="vm.ibBodyDensity.set($any($event.target).value)" placeholder="0.0" />
          </label>
        </div>

        <hr class="oa-divider oa-divider--bottom" />
        <div class="oa-actions oa-actions--split">
          <button type="button" class="oa-back" (click)="vm.backToStep3()">{{ 'onboarding.back' | translate }}</button>
          <button type="button" class="oa-next" [disabled]="!vm.canSubmitStep4() || vm.submitting()" (click)="vm.submit()">
            {{ 'onboarding.submit' | translate }}
            <svg class="oa-next-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><polyline points="9 18 15 12 9 6"></polyline></svg>
          </button>
        </div>
      }
```

- [ ] **Step 4: Add the i18n keys**

In `frontend/src/app/core/i18n/en.json`, extend the `onboarding` block (add a comma after the previous last key):
```json
    "fatPercentage": "Fat Percentage (%)",
    "musclePercentage": "Muscle Percentage (%)",
    "waterPercentage": "Water Percentage (%)",
    "boneDensity": "Bone Density",
    "bodyDensity": "Body Density",
    "submit": "Submit"
```
In `frontend/src/app/core/i18n/ar.json`, the matching keys:
```json
    "fatPercentage": "نسبة الدهون (٪)",
    "musclePercentage": "نسبة العضلات (٪)",
    "waterPercentage": "نسبة الماء (٪)",
    "boneDensity": "كثافة العظام",
    "bodyDensity": "كثافة الجسم",
    "submit": "إرسال"
```
(Ensure valid JSON — comma after the prior last key in each block. `height`/`weight` already exist and are reused.)

- [ ] **Step 5: Verify the build and full frontend suite**

Run (from `frontend/`): `npm run build` then `npm test`
Expected: AOT build clean; all Jest suites green (including the Task 6 viewmodel spec and Task 5 use-case spec).

- [ ] **Step 6: Stage (commit deferred)**

```bash
git add frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding-wizard.page.html \
        frontend/src/app/core/i18n/en.json frontend/src/app/core/i18n/ar.json
# DO NOT COMMIT yet
```

---

## Task 8: Full verification + hand back

**Files:** none (verification only).

- [ ] **Step 1: Backend — full suite (API stopped)**

Run: `dotnet test backend/`
Expected: all green (497 existing + new Task 1–4 tests), `ArchitectureTests` green.

- [ ] **Step 2: Frontend — full suite + AOT build**

Run (from `frontend/`): `npm test` and `npm run build`
Expected: all Jest suites green (591 existing + new specs); AOT build clean.

- [ ] **Step 3: Manual smoke (optional, if a demo swimmer is available)**

First-login swimmer → Step 1 Next → Step 2 fill + Next → Step 3 fill + Next → Step 4 fill 7 InBody fields → Submit → lands on `/home`. Verify one `inbody_reading` row with `reading_date` = today, `recorded_by` = the swimmer, the seven values; `is_first_login` cleared. Re-login should NOT force onboarding again.

- [ ] **Step 4: Report and await commit authorization**

Summarize what changed and that everything is green. **Do not commit** — ask the user for the go-ahead, then commit per their preference.

---

## Notes for the executor

- **Builds on uncommitted Steps 1–3.** The working tree already contains Steps 1–3 staged (no commits). All diffs pile on top; `git diff 0af59ce` is the cumulative view. Do not revert prior-step changes.
- **`CompleteOnboardingAsync` is reused here** (retained since Step 2) — Step 4's endpoint calls it to clear first-login after the Health reading save.
- **Cross-context, not atomic.** InBody save (Health) + first-login clear (Identity) are separate `SaveChanges`. Ordering (reading first, complete last) + the three frontend latches (identity/guardian/physiological) keep retries safe; a rare duplicate InBody reading is possible only if `CompleteOnboardingAsync` fails after the reading saved — accepted, documented (same as Step 2).
- **Signal naming:** Step 4 InBody signals are `ib`-prefixed because the viewmodel already has `heightCm`/`weightKg` (Step 1 vitals). Do not reuse those.
- **Task 6 must not run the AOT build** (the template still references the old Step-3 submit until Task 7). Verify Task 6 via `npm test -- onboarding.viewmodel` only.
- **The finish button reads "Submit"** (`onboarding.submit`), per the screenshot — distinct from the "Next Step" (`onboarding.next`) middle-step buttons.
