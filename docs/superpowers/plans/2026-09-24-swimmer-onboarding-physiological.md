# Swimmer Onboarding — Step 3 (Physiological) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Step 3 (Physiological) to the swimmer first-login wizard as the new finish line — Step 2 stops completing, and Step 3 saves one `athlete.body_measurement` row (5 screen fields mapped to 7 columns) and clears `IsFirstLogin` in one atomic write.

**Architecture:** Single Identity module. Step 2's guardian-medical endpoint stops clearing `IsFirstLogin`; a new self-service `POST /api/swimmers/me/onboarding/physiological` takes 5 values, and `ISwimmerService.CompleteOnboardingPhysiologicalAsync` maps them onto the 7 non-null `body_measurement` columns (Arm→both arms, Leg→both legs, Torso/Bust/Waist 1:1), adds the row, and clears `IsFirstLogin` in one `IdentityDbContext` `SaveChanges`. The frontend defers all writes to the final Step-3 submit (identity-vitals → guardian-medical → physiological), latching the first two so a retry re-runs only physiological.

**Tech Stack:** .NET (C#, Identity module, FluentValidation, EF Core, xUnit + Moq), Angular (standalone components, signals, feature slices, Jest).

**Spec:** `docs/superpowers/specs/2026-09-24-swimmer-onboarding-physiological-design.md`

## Global Constraints

- **⚠️ Do NOT run `git commit`.** Standing user directive: "don't commit until I tell you." Each task ends with a **"Stage (commit deferred)"** step — `git add` only. This builds ON TOP of the Step 2 work already **staged but uncommitted** on `feat/championships` (HEAD still `0af59ce`).
- **No new tables, no DB migration, no seed changes.** `athlete.body_measurement` already exists on all targets.
- **5→7 mapping is server-side:** `Arm Length → RightArmCm AND LeftArmCm`; `Leg Length → RightLegCm AND LeftLegCm`; `Torso/Bust/Waist` 1:1. `BodyMeasurement` ctor order is `(swimmerId, rightArmCm, leftArmCm, rightLegCm, leftLegCm, torsoCm, bustDiameterCm, waistDiameterCm)`.
- **Each value `> 0` and `<= 999.9`** (reuse the existing measurement bounds + `SwimmerMessages.Errors.MeasurementInvalid`).
- **Step 3 is the finish line:** the Step-2 guardian-medical endpoint must STOP calling `CompleteOnboardingAsync`; Step 3 clears `IsFirstLogin` inline (atomic with the measurement).
- **Self-service, JWT-scoped:** the new endpoint resolves the swimmer from `CurrentUserId()`, `[Authorize(Roles = "swimmer")]`, no path id.
- **Backend tests:** stop any running API first (DLL lock), then `dotnet test` from `backend/`. **Frontend:** `npm test` (Jest) + `npm run build` from `frontend/`.
- Keep both suites green: backend (currently 488) + frontend (currently 589), plus the new tests below.

## Review Focus

- **A physiological value that is 0, negative, or > 999.9** — must 400, persist nothing. Test: Task 2 validator rejects 0 and rejects 1000; Task 6 `canSubmitStep3` false for 0 and for 1000.
- **Retry after the physiological call fails** (identity-vitals + guardian-medical already succeeded) — must NOT re-run identity-vitals or guardian-medical (no duplicate exam / observations), only physiological. Test: Task 6 viewmodel spec (physiological fails → retry calls identity 0 more times, guardian 0 more times, physiological again).
- **Guardian-medical call fails at final submit** — must stop, not call physiological, not complete. Test: Task 6 viewmodel spec (guardian-medical fails → physiological use-case not called, no navigate).
- **Guardian-medical no longer completes onboarding** — the endpoint must not clear `IsFirstLogin` anymore. Test: Task 1 controller tests assert `CompleteOnboardingAsync` is invoked `Times.Never`.
- **Step 3 reached with an empty medical categoryId** (categories failed to load) — the existing guard must still fire before any save. Test: Task 6 keeps the empty-`categoryId` guard test green.

---

## Task 1: Guardian-medical endpoint stops completing onboarding

Step 3 becomes the finish line, so the Step-2 endpoint must no longer clear `IsFirstLogin`.

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs` (`CompleteOnboardingGuardianMedical`, the `me/onboarding/guardian-medical` action)
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs`

**Interfaces:**
- Consumes: existing `ISwimmerService.UpsertOnboardingGuardiansAsync`, `IObservationService.CreateAsync`, `ISwimmerService.CompleteOnboardingAsync` (the last call is being removed from this action).
- Produces: same endpoint, same 200/404 shape, but `CompleteOnboardingAsync` is no longer invoked by it.

- [ ] **Step 1: Update the two controller tests to expect no completion**

In `SwimmersControllerTests.cs`, the Step-2 tests currently assert `CompleteOnboardingAsync` is called `Times.Once`. Flip them to `Times.Never` and rename to reflect the new behavior:

```csharp
    [Fact]
    public async Task CompleteGuardianMedical_upserts_creates_observations_and_does_not_complete()
    {
        var svc = new Mock<ISwimmerService>();
        var swimmerId = Guid.NewGuid();
        svc.Setup(s => s.UpsertOnboardingGuardiansAsync(It.IsAny<Guid>(), It.IsAny<GuardianInputDto>(), It.IsAny<GuardianInputDto>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(swimmerId);
        var obs = new Mock<IObservationService>();

        var catId = Guid.NewGuid();
        var req = new CompleteGuardianMedicalRequest(
            new GuardianInputDto("Ahmed", "12345678901234", "010"),
            new GuardianInputDto("Sara", "43210987654321", "011"),
            new[] { new OnboardingMedicalItemDto(catId, "Allergies", "Peanuts") });

        var result = await Create(svc.Object, obs.Object).CompleteOnboardingGuardianMedical(req, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        obs.Verify(o => o.CreateAsync(It.Is<CreateObservationRequest>(r => r.SwimmerId == swimmerId && r.CategoryId == catId && r.Value == "Peanuts"), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        svc.Verify(s => s.CompleteOnboardingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteGuardianMedical_with_empty_medical_does_not_complete()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.UpsertOnboardingGuardiansAsync(It.IsAny<Guid>(), It.IsAny<GuardianInputDto>(), It.IsAny<GuardianInputDto>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Guid.NewGuid());
        var obs = new Mock<IObservationService>();

        var req = new CompleteGuardianMedicalRequest(
            new GuardianInputDto("Ahmed", "12345678901234", "010"),
            new GuardianInputDto("Sara", "43210987654321", "011"),
            System.Array.Empty<OnboardingMedicalItemDto>());

        var result = await Create(svc.Object, obs.Object).CompleteOnboardingGuardianMedical(req, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        obs.Verify(o => o.CreateAsync(It.IsAny<CreateObservationRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        svc.Verify(s => s.CompleteOnboardingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
```

Rename the old `CompleteGuardianMedical_upserts_creates_observations_then_completes` and `CompleteGuardianMedical_with_empty_medical_still_completes` to the two above (replace their bodies). Leave `CompleteGuardianMedical_returns_404_when_not_a_swimmer` unchanged (it already asserts `Times.Never`).

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter CompleteGuardianMedical`
Expected: FAIL — the two renamed tests fail because the action still calls `CompleteOnboardingAsync` (Times.Once, not Never).

- [ ] **Step 3: Remove the completion call from the endpoint**

In `SwimmersController.CompleteOnboardingGuardianMedical`, delete the step-3 completion line and adjust the comment:

```csharp
        // 2) Medical history (Health) — one observation per "Yes" answer. Insert-only.
        foreach (var item in request.Medical)
            await _observations.CreateAsync(
                new CreateObservationRequest(swimmerId.Value, item.CategoryId, item.FieldLabel, item.Value), userId, ct);

        // Onboarding is NOT completed here anymore — Step 3 (Physiological) is the finish line.
        return Ok(ApiResponse<OnboardingStepResultDto>.Success(
            SwimmerMessages.Success.OnboardingCompleted(AppLanguage.Current), new OnboardingStepResultDto(false)));
```

(Delete the `// 3) Clear first-login LAST …` comment and the `await _service.CompleteOnboardingAsync(userId, ct);` line. Keep the XML `<summary>` accurate: change "clearing first-login" to "advancing to Step 3".)

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter CompleteGuardianMedical`
Expected: PASS (all three).

- [ ] **Step 5: Stage (commit deferred)**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs \
        backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs
# DO NOT COMMIT yet
```

---

## Task 2: Physiological DTO + validator

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/BodyMeasurementDtos.cs` (append)
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Validators/BodyMeasurementRequestValidators.cs` (append)
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Validators/OnboardingValidatorTests.cs` (append)

**Interfaces:**
- Produces: `CompletePhysiologicalRequest(decimal ArmCm, decimal LegCm, decimal TorsoCm, decimal BustDiameterCm, decimal WaistDiameterCm)` and `CompletePhysiologicalRequestValidator : AbstractValidator<CompletePhysiologicalRequest>`.

- [ ] **Step 1: Add the DTO**

Append to `BodyMeasurementDtos.cs`:

```csharp
/// <summary>Step 3 onboarding submission (POST /api/swimmers/me/onboarding/physiological): five screen values, mapped 5→7 server-side.</summary>
public sealed record CompletePhysiologicalRequest(
    decimal ArmCm,
    decimal LegCm,
    decimal TorsoCm,
    decimal BustDiameterCm,
    decimal WaistDiameterCm);
```

- [ ] **Step 2: Write the failing validator tests**

Append to `OnboardingValidatorTests.cs` (inside the class):

```csharp
    private static CompletePhysiologicalRequest ValidPhys() => new(32.5m, 95m, 60m, 90m, 75m);

    private static readonly CompletePhysiologicalRequestValidator PV = new();

    [Fact] public void Phys_accepts_a_valid_request() => Assert.True(PV.Validate(ValidPhys()).IsValid);

    [Fact] public void Phys_rejects_zero_arm() => Assert.False(PV.Validate(ValidPhys() with { ArmCm = 0m }).IsValid);

    [Fact] public void Phys_rejects_negative_leg() => Assert.False(PV.Validate(ValidPhys() with { LegCm = -1m }).IsValid);

    [Fact] public void Phys_rejects_over_max_torso() => Assert.False(PV.Validate(ValidPhys() with { TorsoCm = 1000m }).IsValid);

    [Fact] public void Phys_accepts_boundary_max() => Assert.True(PV.Validate(ValidPhys() with { WaistDiameterCm = 999.9m }).IsValid);
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter Phys_`
Expected: FAIL to compile — `CompletePhysiologicalRequestValidator` does not exist yet.

- [ ] **Step 4: Add the validator**

Append to `BodyMeasurementRequestValidators.cs` (it already has the `using`s for FluentValidation, `SwimmerMessages`, `AppLanguage`):

```csharp
public sealed class CompletePhysiologicalRequestValidator : AbstractValidator<CompletePhysiologicalRequest>
{
    public CompletePhysiologicalRequestValidator()
    {
        Rule(x => x.ArmCm);
        Rule(x => x.LegCm);
        Rule(x => x.TorsoCm);
        Rule(x => x.BustDiameterCm);
        Rule(x => x.WaistDiameterCm);
    }

    private void Rule(System.Linq.Expressions.Expression<System.Func<CompletePhysiologicalRequest, decimal>> selector)
        => RuleFor(selector)
            .GreaterThan(0m)
            .LessThanOrEqualTo(999.9m)
            .WithMessage(_ => SwimmerMessages.Errors.MeasurementInvalid(AppLanguage.Current));
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "Phys_|Onboarding"`
Expected: PASS.

- [ ] **Step 6: Stage (commit deferred)**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/BodyMeasurementDtos.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Validators/BodyMeasurementRequestValidators.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Validators/OnboardingValidatorTests.cs
# DO NOT COMMIT yet
```

---

## Task 3: Identity service — CompleteOnboardingPhysiologicalAsync

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/ISwimmerService.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs` (append)

**Interfaces:**
- Consumes: `ISwimmerProfileRepository.GetByUserIdTrackedAsync`, `AddBodyMeasurementAsync`, `SaveChangesAsync`; `IUserRepository.GetByIdAsync`; `AppUser.CompleteFirstLogin()`; `BodyMeasurement` ctor `(Guid swimmerId, decimal rightArmCm, decimal leftArmCm, decimal rightLegCm, decimal leftLegCm, decimal torsoCm, decimal bustDiameterCm, decimal waistDiameterCm)`; `CompletePhysiologicalRequest` (Task 2).
- Produces: `Task<bool> CompleteOnboardingPhysiologicalAsync(Guid userId, CompletePhysiologicalRequest req, CancellationToken ct = default)`.

- [ ] **Step 1: Write the failing service tests**

Append to `SwimmerServiceTests.cs`:

```csharp
    // ── Onboarding Step 3 (physiological) tests ──────────────────────────────

    [Fact]
    public async Task CompleteOnboardingPhysiological_returns_false_when_not_a_swimmer()
    {
        var (svc, swimmers, _) = Build();
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwimmerProfile?)null);
        Assert.False(await svc.CompleteOnboardingPhysiologicalAsync(Guid.NewGuid(), new CompletePhysiologicalRequest(32.5m, 95m, 60m, 90m, 75m)));
    }

    [Fact]
    public async Task CompleteOnboardingPhysiological_maps_five_to_seven_adds_measurement_and_clears_first_login()
    {
        var (svc, swimmers, users) = Build();
        var userId = Guid.NewGuid();
        var profile = new SwimmerProfile(userId, "SW-0001", Guid.NewGuid());
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        var user = new AppUser("s", "S", Guid.NewGuid(), email: "s@x.io", isFirstLogin: true);
        users.Setup(u => u.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var ok = await svc.CompleteOnboardingPhysiologicalAsync(userId, new CompletePhysiologicalRequest(32.5m, 95m, 60m, 90m, 75m));

        Assert.True(ok);
        Assert.False(user.IsFirstLogin); // cleared
        swimmers.Verify(r => r.AddBodyMeasurementAsync(It.Is<BodyMeasurement>(m =>
            m.SwimmerId == profile.Id &&
            m.RightArmCm == 32.5m && m.LeftArmCm == 32.5m &&
            m.RightLegCm == 95m && m.LeftLegCm == 95m &&
            m.TorsoCm == 60m && m.BustDiameterCm == 90m && m.WaistDiameterCm == 75m),
            It.IsAny<CancellationToken>()), Times.Once);
        swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter CompleteOnboardingPhysiological`
Expected: FAIL to compile — the method doesn't exist on `ISwimmerService`.

- [ ] **Step 3: Declare the interface method**

Add to `ISwimmerService.cs` (near the other onboarding methods):

```csharp
    Task<bool> CompleteOnboardingPhysiologicalAsync(Guid userId, CompletePhysiologicalRequest req, CancellationToken ct = default);
```

- [ ] **Step 4: Implement the service method**

Add to `SwimmerService.cs` (after `CompleteOnboardingAsync`):

```csharp
    public async Task<bool> CompleteOnboardingPhysiologicalAsync(
        Guid userId, CompletePhysiologicalRequest req, CancellationToken ct = default)
    {
        var profile = await _swimmers.GetByUserIdTrackedAsync(userId, ct);
        if (profile is null) return false;
        var user = await _users.GetByIdAsync(profile.UserId, ct);
        if (user is null) return false;

        // Five screen values → seven columns: arm → both arms, leg → both legs, torso/bust/waist 1:1.
        var measurement = new BodyMeasurement(profile.Id,
            req.ArmCm, req.ArmCm, req.LegCm, req.LegCm, req.TorsoCm, req.BustDiameterCm, req.WaistDiameterCm);
        await _swimmers.AddBodyMeasurementAsync(measurement, ct);

        user.CompleteFirstLogin();

        // One SaveChanges over IdentityDbContext → measurement + first-login clear persist atomically.
        await _swimmers.SaveChangesAsync(ct);
        return true;
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter CompleteOnboardingPhysiological`
Expected: PASS.

- [ ] **Step 6: Stage (commit deferred)**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/ISwimmerService.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs
# DO NOT COMMIT yet
```

---

## Task 4: Controller endpoint — POST me/onboarding/physiological

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs` (add action in the Onboarding region)
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs` (append)

**Interfaces:**
- Consumes: `ISwimmerService.CompleteOnboardingPhysiologicalAsync` (Task 3); `CompletePhysiologicalRequest` (Task 2); `CurrentUserId()`. No new DI (uses the existing `_service`).
- Produces: `POST api/swimmers/me/onboarding/physiological` → `ApiResponse<OnboardingStepResultDto>` (200) / 404.

- [ ] **Step 1: Write the failing controller tests**

Append to `SwimmersControllerTests.cs`:

```csharp
    [Fact]
    public async Task CompletePhysiological_returns_200_when_service_ok()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.CompleteOnboardingPhysiologicalAsync(It.IsAny<Guid>(), It.IsAny<CompletePhysiologicalRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(true);

        var req = new CompletePhysiologicalRequest(32.5m, 95m, 60m, 90m, 75m);
        var result = await Create(svc.Object).CompleteOnboardingPhysiological(req, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<OnboardingStepResultDto>>(ok.Value);
        Assert.True(body.SuccessStatus);
        Assert.False(body.Data!.MustChangePassword);
    }

    [Fact]
    public async Task CompletePhysiological_returns_404_when_service_false()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.CompleteOnboardingPhysiologicalAsync(It.IsAny<Guid>(), It.IsAny<CompletePhysiologicalRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(false);

        var req = new CompletePhysiologicalRequest(32.5m, 95m, 60m, 90m, 75m);
        var result = await Create(svc.Object).CompleteOnboardingPhysiological(req, CancellationToken.None);

        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter CompletePhysiological`
Expected: FAIL to compile — `CompleteOnboardingPhysiological` action doesn't exist.

- [ ] **Step 3: Add the endpoint (inside the Onboarding region, after the guardian-medical action)**

```csharp
    /// <summary>Completes the swimmer's first-login Step 3 (physiological measurements), clearing first-login. Swimmer only.</summary>
    /// <response code="200">Completed; returns the refreshed first-login flag (false).</response>
    /// <response code="404">The caller is not a swimmer / has no profile.</response>
    [HttpPost("me/onboarding/physiological")]
    [Authorize(Roles = "swimmer")]
    [ProducesResponseType(typeof(ApiResponse<OnboardingStepResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OnboardingStepResultDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OnboardingStepResultDto>>> CompleteOnboardingPhysiological(
        CompletePhysiologicalRequest request, CancellationToken ct)
    {
        var ok = await _service.CompleteOnboardingPhysiologicalAsync(CurrentUserId(), request, ct);
        if (!ok)
        {
            var nf = ApiResponse<OnboardingStepResultDto>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<OnboardingStepResultDto>.Success(
            SwimmerMessages.Success.OnboardingCompleted(AppLanguage.Current), new OnboardingStepResultDto(false)));
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter CompletePhysiological`
Expected: PASS.

- [ ] **Step 5: Run the whole backend suite + architecture tests**

Run (API stopped): `dotnet test backend/`
Expected: PASS (488 + the new Task 1–4 tests), `ArchitectureTests` green.

- [ ] **Step 6: Stage (commit deferred)**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs \
        backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs
# DO NOT COMMIT yet
```

---

## Task 5: Frontend — model, DTO, repository, use-case

**Files:**
- Modify: `frontend/src/app/features/swimmer-onboarding/domain/model/onboarding.ts`
- Create: `frontend/src/app/features/swimmer-onboarding/data/dto/complete-physiological.dto.ts`
- Modify: `frontend/src/app/features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository.ts`
- Modify: `frontend/src/app/features/swimmer-onboarding/data/repositories/swimmer-onboarding.repository.impl.ts`
- Create: `frontend/src/app/features/swimmer-onboarding/domain/usecases/complete-physiological.use-case.ts`
- Test: `frontend/src/app/features/swimmer-onboarding/testing/domain/usecases/complete-physiological.use-case.spec.ts`

**Interfaces:**
- Produces: model `PhysiologicalSubmission { armCm; legCm; torsoCm; bustDiameterCm; waistDiameterCm }`; DTO `CompletePhysiologicalDtoRq` + `CompletePhysiologicalItemDtoRs`; repo `completePhysiological(rq)`; `CompletePhysiologicalUseCase extends UseCase<PhysiologicalSubmission, void>`.

- [ ] **Step 1: Add the domain model**

Append to `onboarding.ts`:

```typescript
export interface PhysiologicalSubmission {
  armCm: number;
  legCm: number;
  torsoCm: number;
  bustDiameterCm: number;
  waistDiameterCm: number;
}
```

- [ ] **Step 2: Add the request DTO**

Create `data/dto/complete-physiological.dto.ts`:

```typescript
import { BaseResponseRs } from '@core/network/api/base-response-rs';
import { OnboardingResultDtoRs } from './complete-identity-vitals.dto';

export interface CompletePhysiologicalDtoRq {
  armCm: number;
  legCm: number;
  torsoCm: number;
  bustDiameterCm: number;
  waistDiameterCm: number;
}
export interface CompletePhysiologicalItemDtoRs extends BaseResponseRs<OnboardingResultDtoRs> {}
```

- [ ] **Step 3: Extend the repository interface + impl**

In `swimmer-onboarding.repository.ts`, add the import and method:

```typescript
import { CompletePhysiologicalDtoRq, CompletePhysiologicalItemDtoRs } from '@features/swimmer-onboarding/data/dto/complete-physiological.dto';
```
```typescript
  completePhysiological(rq: CompletePhysiologicalDtoRq): Promise<CompletePhysiologicalItemDtoRs>;
```

In `swimmer-onboarding.repository.impl.ts`, add the import and implement:

```typescript
import { CompletePhysiologicalDtoRq, CompletePhysiologicalItemDtoRs } from '@features/swimmer-onboarding/data/dto/complete-physiological.dto';
```
```typescript
  completePhysiological(rq: CompletePhysiologicalDtoRq): Promise<CompletePhysiologicalItemDtoRs> {
    return this.http.post<CompletePhysiologicalItemDtoRs>('/api/swimmers/me/onboarding/physiological', { body: rq });
  }
```

- [ ] **Step 4: Write the failing use-case spec**

Create `testing/domain/usecases/complete-physiological.use-case.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { CompletePhysiologicalUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-physiological.use-case';
import { SWIMMER_ONBOARDING_REPOSITORY, ISwimmerOnboardingRepository } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { PhysiologicalSubmission } from '@features/swimmer-onboarding/domain/model/onboarding';

const SUBMISSION: PhysiologicalSubmission = { armCm: 32.5, legCm: 95, torsoCm: 60, bustDiameterCm: 90, waistDiameterCm: 75 };

function build(repo: Partial<ISwimmerOnboardingRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_ONBOARDING_REPOSITORY, useValue: repo }] });
  return TestBed.inject(CompletePhysiologicalUseCase);
}

describe('CompletePhysiologicalUseCase', () => {
  it('posts the submission and succeeds', async () => {
    const post = jest.fn().mockResolvedValue({ data: { mustChangePassword: false } });
    const uc = build({ completePhysiological: post } as unknown as ISwimmerOnboardingRepository);
    const r = await uc.run(SUBMISSION);
    expect(r.ok).toBe(true);
    expect(post).toHaveBeenCalledWith({ armCm: 32.5, legCm: 95, torsoCm: 60, bustDiameterCm: 90, waistDiameterCm: 75 });
  });

  it('fails when the repository throws', async () => {
    const uc = build({ completePhysiological: async () => { throw new Error('boom'); } } as unknown as ISwimmerOnboardingRepository);
    const r = await uc.run(SUBMISSION);
    expect(r.ok).toBe(false);
  });
});
```

- [ ] **Step 5: Run the spec to verify it fails**

Run: `npm test -- complete-physiological.use-case` (from `frontend/`)
Expected: FAIL — `CompletePhysiologicalUseCase` does not exist yet.

- [ ] **Step 6: Implement the use-case**

Create `domain/usecases/complete-physiological.use-case.ts`:

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_ONBOARDING_REPOSITORY } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { PhysiologicalSubmission } from '@features/swimmer-onboarding/domain/model/onboarding';

@Injectable({ providedIn: 'root' })
export class CompletePhysiologicalUseCase extends UseCase<PhysiologicalSubmission, void> {
  private readonly repo = inject(SWIMMER_ONBOARDING_REPOSITORY);
  constructor() { super('CompletePhysiological'); }

  protected async execute(input: PhysiologicalSubmission): Promise<void> {
    await this.repo.completePhysiological({
      armCm: input.armCm,
      legCm: input.legCm,
      torsoCm: input.torsoCm,
      bustDiameterCm: input.bustDiameterCm,
      waistDiameterCm: input.waistDiameterCm,
    });
  }
}
```

- [ ] **Step 7: Run the spec to verify it passes**

Run: `npm test -- complete-physiological.use-case`
Expected: PASS.

- [ ] **Step 8: Stage (commit deferred)**

```bash
git add frontend/src/app/features/swimmer-onboarding/domain/model/onboarding.ts \
        frontend/src/app/features/swimmer-onboarding/data/dto/complete-physiological.dto.ts \
        frontend/src/app/features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository.ts \
        frontend/src/app/features/swimmer-onboarding/data/repositories/swimmer-onboarding.repository.impl.ts \
        frontend/src/app/features/swimmer-onboarding/domain/usecases/complete-physiological.use-case.ts \
        frontend/src/app/features/swimmer-onboarding/testing/domain/usecases/complete-physiological.use-case.spec.ts
# DO NOT COMMIT yet
```

---

## Task 6: Viewmodel — three-step flow + physiological submit

**Files:**
- Modify: `frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding.viewmodel.ts`
- Test: `frontend/src/app/features/swimmer-onboarding/testing/presentation/pages/onboarding/onboarding.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `CompletePhysiologicalUseCase` (Task 5); existing `CompleteIdentityVitalsUseCase`, `CompleteGuardianMedicalUseCase`, `AuthSessionStore`, `Router`, `NotificationService`, `TranslateService`.
- Produces (new members): `currentStep: WritableSignal<1|2|3>`; `goToStep3()`, `backToStep2()`; `canSubmitStep3`; Step 3 signals `armCm/legCm/torsoCm/bustDiameterCm/waistDiameterCm`; a `guardianSaved` latch; `submit()` now completes after the physiological call.

- [ ] **Step 1: Update the viewmodel spec (failing tests for the new behavior)**

Edit `onboarding.viewmodel.spec.ts`. Add the physiological use-case import + provider, a `physiological` mock in `build()`, a `fillStep3` helper, widen coverage. Replace the file with:

```typescript
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { OnboardingViewModel } from '@features/swimmer-onboarding/presentation/pages/onboarding/onboarding.viewmodel';
import { GetOnboardingPrefillUseCase } from '@features/swimmer-onboarding/domain/usecases/get-onboarding-prefill.use-case';
import { CompleteIdentityVitalsUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-identity-vitals.use-case';
import { CompleteGuardianMedicalUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-guardian-medical.use-case';
import { CompletePhysiologicalUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-physiological.use-case';
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

function build(overrides: { identity?: unknown; guardianMedical?: unknown; physiological?: unknown; cats?: unknown } = {}) {
  const getPrefill = { run: jest.fn().mockResolvedValue(ok(PREFILL)) };
  const identity = { run: jest.fn().mockResolvedValue(overrides.identity ?? ok(undefined)) };
  const guardianMedical = { run: jest.fn().mockResolvedValue(overrides.guardianMedical ?? ok(undefined)) };
  const physiological = { run: jest.fn().mockResolvedValue(overrides.physiological ?? ok(undefined)) };
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
  return { vm: TestBed.inject(OnboardingViewModel), identity, guardianMedical, physiological, auth, router, notify };
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

describe('OnboardingViewModel', () => {
  it('load() seeds identity drafts, lookups, and observation categories', async () => {
    const { vm } = build();
    await vm.load();
    expect(vm.nameEn()).toBe('Sam');
    expect(vm.observationCategories().length).toBe(4);
  });

  it('goToStep2 then goToStep3 gate on the prior step being valid', async () => {
    const { vm } = build();
    await vm.load();
    vm.goToStep3();
    expect(vm.currentStep()).toBe(1);       // can't skip
    fillStep1(vm); vm.goToStep2();
    expect(vm.currentStep()).toBe(2);
    vm.goToStep3();
    expect(vm.currentStep()).toBe(2);       // blocked — step 2 incomplete
    fillStep2(vm); vm.goToStep3();
    expect(vm.currentStep()).toBe(3);
    vm.backToStep2();
    expect(vm.currentStep()).toBe(2);
  });

  it('canSubmitStep3 requires all five in (0, 999.9]', async () => {
    const { vm } = build();
    await vm.load();
    expect(vm.canSubmitStep3()).toBe(false);
    fillStep3(vm);
    expect(vm.canSubmitStep3()).toBe(true);
    vm.armCm.set('0');
    expect(vm.canSubmitStep3()).toBe(false);
    vm.armCm.set('1000');
    expect(vm.canSubmitStep3()).toBe(false);
  });

  it('submit() posts identity, guardian-medical, then physiological, and completes after physiological', async () => {
    const { vm, identity, guardianMedical, physiological, auth, router } = build();
    await vm.load();
    fillStep1(vm); fillStep2(vm); fillStep3(vm);
    await vm.submit();
    expect(identity.run).toHaveBeenCalledTimes(1);
    expect(guardianMedical.run).toHaveBeenCalledTimes(1);
    expect(physiological.run).toHaveBeenCalledWith({ armCm: 32.5, legCm: 95, torsoCm: 60, bustDiameterCm: 90, waistDiameterCm: 75 });
    expect(auth.markOnboardingComplete).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/home']);
  });

  it('submit() stops and does not complete when guardian-medical fails', async () => {
    const { vm, physiological, auth, router, notify } = build({ guardianMedical: fail(new AppError('nope', 'network')) });
    await vm.load();
    fillStep1(vm); fillStep2(vm); fillStep3(vm);
    await vm.submit();
    expect(physiological.run).not.toHaveBeenCalled();
    expect(auth.markOnboardingComplete).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
    expect(notify.error).toHaveBeenCalled();
  });

  it('retry after a physiological failure re-runs only physiological (latches)', async () => {
    const { vm, identity, guardianMedical, physiological, auth } = build({ physiological: fail(new AppError('nope', 'network')) });
    await vm.load();
    fillStep1(vm); fillStep2(vm); fillStep3(vm);
    await vm.submit();
    expect(physiological.run).toHaveBeenCalledTimes(1);
    expect(auth.markOnboardingComplete).not.toHaveBeenCalled();
    (physiological.run as jest.Mock).mockResolvedValueOnce(ok(undefined));
    await vm.submit();
    expect(identity.run).toHaveBeenCalledTimes(1);        // NOT re-run
    expect(guardianMedical.run).toHaveBeenCalledTimes(1); // NOT re-run
    expect(physiological.run).toHaveBeenCalledTimes(2);   // re-run
    expect(auth.markOnboardingComplete).toHaveBeenCalled();
  });

  it('submit() blocks (persists nothing) when a medical "Yes" has an unresolved category id', async () => {
    const { vm, identity, physiological, notify } = build({ cats: ok([]) }); // categories failed to load
    await vm.load();
    fillStep1(vm); fillStep2(vm); fillStep3(vm);
    vm.allergyYes.set(true); vm.allergyDetails.set('Peanuts'); // a Yes that can't resolve to a category id
    await vm.submit();
    expect(identity.run).not.toHaveBeenCalled();
    expect(physiological.run).not.toHaveBeenCalled();
    expect(notify.error).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run the spec to verify it fails**

Run: `npm test -- onboarding.viewmodel`
Expected: FAIL — new members (`currentStep` widened to 3, `goToStep3`, `backToStep2`, `canSubmitStep3`, Step 3 signals, physiological in submit) don't exist.

- [ ] **Step 3: Implement the viewmodel changes**

Edit `onboarding.viewmodel.ts`:

Add the import (with the other use-case imports):
```typescript
import { CompletePhysiologicalUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-physiological.use-case';
```
Inject it (next to `completeGuardianMedicalUc`):
```typescript
  private readonly completePhysiologicalUc = inject(CompletePhysiologicalUseCase);
```
Add the guardian latch (next to `identitySaved`):
```typescript
  private readonly guardianSaved = signal(false);
```
Widen `currentStep`:
```typescript
  readonly currentStep = signal<1 | 2 | 3>(1);
```
Add Step 3 signals (after the guardian/medical signals):
```typescript
  // Physiological (Step 3) — five values, mapped 5→7 server-side.
  readonly armCm = signal(''); readonly legCm = signal(''); readonly torsoCm = signal('');
  readonly bustDiameterCm = signal(''); readonly waistDiameterCm = signal('');
```
Add `canSubmitStep3` (after `canSubmitStep2`):
```typescript
  readonly canSubmitStep3 = computed(() =>
    [this.armCm(), this.legCm(), this.torsoCm(), this.bustDiameterCm(), this.waistDiameterCm()]
      .every((s) => { const n = Number(s); return n > 0 && n <= 999.9; }));
```
Add the navigation helpers (after `backToStep1`):
```typescript
  goToStep3(): void { if (this.canSubmitStep2()) this.currentStep.set(3); }
  backToStep2(): void { this.currentStep.set(2); }
```
Replace `submit()` with the three-call chain (completion moves to after physiological; the guard changes to `canSubmitStep3`):
```typescript
  async submit(): Promise<void> {
    if (!this.canSubmitStep3() || this.submitting()) return;

    // Guard against unresolved category ids — check before persisting anything.
    const medicalItems = this.buildMedical();
    if (medicalItems.some((item) => item.categoryId === '')) {
      this.notify.error(this.i18n.t('onboarding.saveFailed'));
      return;
    }

    this.submitting.set(true);

    // 1) identity-vitals (latched — never re-run on retry).
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

    // 2) guardian-medical (latched — never re-run on retry).
    if (!this.guardianSaved()) {
      const r2 = await this.completeGuardianMedicalUc.run({
        father: { name: this.fatherName().trim(), nationalId: this.fatherNationalId().trim(), phone: this.fatherPhone().trim() },
        mother: { name: this.motherName().trim(), nationalId: this.motherNationalId().trim(), phone: this.motherPhone().trim() },
        medical: medicalItems,
      });
      if (!r2.ok) { this.submitting.set(false); this.notify.error(this.i18n.t('onboarding.saveFailed')); return; }
      this.guardianSaved.set(true);
    }

    // 3) physiological — saves the measurement AND completes onboarding (server-side, atomic).
    const r3 = await this.completePhysiologicalUc.run({
      armCm: Number(this.armCm()), legCm: Number(this.legCm()), torsoCm: Number(this.torsoCm()),
      bustDiameterCm: Number(this.bustDiameterCm()), waistDiameterCm: Number(this.waistDiameterCm()),
    });
    this.submitting.set(false);

    if (r3.ok) {
      this.auth.markOnboardingComplete();
      this.notify.success(this.i18n.t('onboarding.completed'));
      void this.router.navigate(['/home']);
    } else {
      this.notify.error(this.i18n.t('onboarding.saveFailed'));
    }
  }
```
Update the `currentStep` comment on the signal line to note 3 steps.

- [ ] **Step 4: Run the spec to verify it passes**

Run: `npm test -- onboarding.viewmodel`
Expected: PASS (7 tests).

Note: do NOT run the full AOT build here — the page template still calls `vm.submit()` on the Step-2 button and has no Step-3 card; Task 7 fixes the template. The viewmodel spec is Jest-only and does not compile the template.

- [ ] **Step 5: Stage (commit deferred)**

```bash
git add frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding.viewmodel.ts \
        frontend/src/app/features/swimmer-onboarding/testing/presentation/pages/onboarding/onboarding.viewmodel.spec.ts
# DO NOT COMMIT yet
```

---

## Task 7: Template (Step 3 card) + i18n

**Files:**
- Modify: `frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding-wizard.page.html`
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`

**Interfaces:**
- Consumes: `OnboardingViewModel` members from Task 6 (`currentStep`, `canSubmitStep2`, `canSubmitStep3`, `goToStep3`, `backToStep2`, `submit`, Step 3 signals).
- Produces: no new code interface (template + i18n only). Verified by the AOT build + full Jest suite.

- [ ] **Step 1: Point the Step-2 "Next Step" button at Step 3**

In `onboarding-wizard.page.html`, the Step-2 footer currently submits. Change it to advance:

```html
          <button type="button" class="oa-next" [disabled]="!vm.canSubmitStep2()" (click)="vm.goToStep3()">
            {{ 'onboarding.next' | translate }}
            <svg class="oa-next-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><polyline points="9 18 15 12 9 6"></polyline></svg>
          </button>
```
(Replace the existing Step-2 `[disabled]="!vm.canSubmitStep2() || vm.submitting()" (click)="vm.submit()"` on that button.)

- [ ] **Step 2: Switch the card title to three steps**

Update the card-title binding:

```html
      <h2 class="oa-card-title">{{ (vm.currentStep() === 1 ? 'onboarding.sectionTitle' : vm.currentStep() === 2 ? 'onboarding.steps.guardian' : 'onboarding.steps.physiological') | translate }}</h2>
```

- [ ] **Step 3: Add the Step 3 card**

After the Step-2 `@if (vm.currentStep() === 2) { … }` block (and before the closing of the card `<section>`), add:

```html
      @if (vm.currentStep() === 3) {
        <div class="oa-grid">
          <label class="oa-field">
            <span class="oa-label">{{ 'onboarding.armLength' | translate }}</span>
            <input class="oa-input" type="number" step="any" inputmode="decimal" [value]="vm.armCm()" (input)="vm.armCm.set($any($event.target).value)" placeholder="0.0" />
          </label>
          <label class="oa-field">
            <span class="oa-label">{{ 'onboarding.legLength' | translate }}</span>
            <input class="oa-input" type="number" step="any" inputmode="decimal" [value]="vm.legCm()" (input)="vm.legCm.set($any($event.target).value)" placeholder="0.0" />
          </label>
          <label class="oa-field">
            <span class="oa-label">{{ 'onboarding.torsoLength' | translate }}</span>
            <input class="oa-input" type="number" step="any" inputmode="decimal" [value]="vm.torsoCm()" (input)="vm.torsoCm.set($any($event.target).value)" placeholder="0.0" />
          </label>
          <label class="oa-field">
            <span class="oa-label">{{ 'onboarding.bustDiameter' | translate }}</span>
            <input class="oa-input" type="number" step="any" inputmode="decimal" [value]="vm.bustDiameterCm()" (input)="vm.bustDiameterCm.set($any($event.target).value)" placeholder="0.0" />
          </label>
          <label class="oa-field">
            <span class="oa-label">{{ 'onboarding.waistDiameter' | translate }}</span>
            <input class="oa-input" type="number" step="any" inputmode="decimal" [value]="vm.waistDiameterCm()" (input)="vm.waistDiameterCm.set($any($event.target).value)" placeholder="0.0" />
          </label>
        </div>

        <hr class="oa-divider oa-divider--bottom" />
        <div class="oa-actions oa-actions--split">
          <button type="button" class="oa-back" (click)="vm.backToStep2()">{{ 'onboarding.back' | translate }}</button>
          <button type="button" class="oa-next" [disabled]="!vm.canSubmitStep3() || vm.submitting()" (click)="vm.submit()">
            {{ 'onboarding.next' | translate }}
            <svg class="oa-next-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><polyline points="9 18 15 12 9 6"></polyline></svg>
          </button>
        </div>
      }
```

- [ ] **Step 4: Add the i18n keys**

In `frontend/src/app/core/i18n/en.json`, extend the `onboarding` block (add a comma after the previous last key):
```json
    "armLength": "Arm Length (cm)",
    "legLength": "Leg Length (cm)",
    "torsoLength": "Torso Length (cm)",
    "bustDiameter": "Bust Diameter (cm)",
    "waistDiameter": "Waist Diameter (cm)"
```
In `frontend/src/app/core/i18n/ar.json`, the matching keys:
```json
    "armLength": "طول الذراع (سم)",
    "legLength": "طول الساق (سم)",
    "torsoLength": "طول الجذع (سم)",
    "bustDiameter": "محيط الصدر (سم)",
    "waistDiameter": "محيط الخصر (سم)"
```
(Ensure valid JSON — comma after the prior last key in each block.)

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
Expected: all green (488 existing + new Task 1–4 tests), `ArchitectureTests` green.

- [ ] **Step 2: Frontend — full suite + AOT build**

Run (from `frontend/`): `npm test` and `npm run build`
Expected: all Jest suites green (589 existing + new specs); AOT build clean.

- [ ] **Step 3: Manual smoke (optional, if a demo swimmer is available)**

First-login swimmer → Step 1 Next → Step 2 fill + Next → Step 3 fill 5 fields → Next Step → lands on `/home`. Verify one `body_measurement` row with `right_arm_cm == left_arm_cm`, `right_leg_cm == left_leg_cm`, and the torso/bust/waist values; `is_first_login` cleared. Re-login should NOT force onboarding again.

- [ ] **Step 4: Report and await commit authorization**

Summarize what changed and that everything is green. **Do not commit** — ask the user for the go-ahead, then commit per their preference.

---

## Notes for the executor

- **Builds on uncommitted Step 2.** The working tree already contains Steps 1–2 staged (no commits). All diffs pile on top; `git diff 0af59ce` is the cumulative view. Do not revert Step-2 changes.
- **`CompleteOnboardingAsync` becomes unused** after Task 1 (guardian-medical no longer calls it; Step 3 clears the flag inline via `CompleteOnboardingPhysiologicalAsync`). Leave it in place — it's a public interface method (no warning), its own Step-2 service tests still cover it, and Step 4 (InBody) may reuse the pattern.
- **`CompletePhysiologicalRequest` lives in `BodyMeasurementDtos.cs`** (co-located with the other body-measurement DTOs), not `SwimmerDtos.cs` — a cohesion choice; the validator sits beside `CreateBodyMeasurementRequestValidator` in `BodyMeasurementRequestValidators.cs`.
- **Atomicity:** Step 3's measurement write + first-login clear are one `IdentityDbContext` `SaveChanges`. A failure persists nothing, so a retry creates no duplicate measurement; the two frontend latches prevent duplicate exam/observations from earlier steps.
- **Task 6 must not run the AOT build** (the template still references the old Step-2 submit until Task 7). Verify Task 6 via `npm test -- onboarding.viewmodel` only.
