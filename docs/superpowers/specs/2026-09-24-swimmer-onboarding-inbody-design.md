# Swimmer First-Login Onboarding — Step 4 (InBody)

**Date:** 2026-09-24
**Status:** Approved design (spec)
**Scope:** The **fourth step** of the swimmer first-login onboarding wizard. Builds on Steps 1–3 (all implemented, **staged but not committed** on `feat/championships`). Step 3 (Physiological) currently *finishes* onboarding; this slice makes the wizard a **four-step flow**: Step 3 "Next" now **advances** to Step 4, and **Step 4 — InBody — becomes the finish line** (it saves one body-composition reading and clears `IsFirstLogin`, landing the swimmer on the dashboard). Step 5 (Done) remains a rendered placeholder. Step 4 writes one `health.inbody_reading` row (**Health** module — so completion spans two modules, like Step 2). **No new tables; no DB migration; no seed changes.**

> **Depends on the uncommitted Steps 1–3.** Step 4 modifies the same onboarding files (viewmodel, page, controller, `SwimmerService`) on top of the staged Step 1–3 changes. See `2026-09-24-swimmer-onboarding-physiological-design.md`.

## 1. Summary

Today (post Step 3) the wizard's Step 3 "Next Step" saves a `body_measurement` **and** clears `IsFirstLogin`. This slice moves the finish line to Step 4:

- **Step 3 "Next Step"** → **advance** to Step 4 (client-side; its physiological endpoint no longer completes).
- **Step 4 "Submit"** → submit **everything** (deferred chain: identity-vitals → guardian-medical → physiological → inbody), complete onboarding, navigate `/home`.

Step 4 collects the **seven** InBody measurements on one screen, matching the Captain/profile InBody section exactly (per the approved screenshot + the attached captain-section image):

- **Height (cm)**, **Weight (kg)**, **Fat Percentage (%)**, **Muscle Percentage (%)**, **Water Percentage (%)**, **Bone Density**, **Body Density** → one `health.inbody_reading` row.

`ReadingDate` is **server-set to today** (no date field on screen); `recorded_by` = the swimmer. Because `health.inbody_reading` is a **Health** module table and clearing `IsFirstLogin` is an **Identity** concern, completion is orchestrated at the **API/controller composition root** (Approach 1, as in Step 2): save the reading via the Health `IInBodyReadingService`, then call `ISwimmerService.CompleteOnboardingAsync` (Identity) **last**. This reuses the `CompleteOnboardingAsync` method retained since Step 2.

**Design references:**
- UI prototype (visual reference only): `C:\Users\envnt\Desktop\4dba8937-ce3f-44bd-b093-515f10a5ea2b\src\pages\SwimmerRegistration.tsx` (STAGE 4 block — note: prototype shows 6 fields; the DB/captain section and this design use **7**, adding Water%).
- Approved screenshot: `C:\Users\envnt\Desktop\mcp\2.png` (Step 4 = InBody active; "Submit" button) + the attached captain InBody-section image (7 rows incl. Water Percentage).
- Reused table: `health.inbody_reading` (see `2026-09-19-swimmer-profile-inbody-design.md` + the Water% addition) — already implemented and migrated.
- Reused code: `InBodyReading` entity, `IInBodyReadingService.CreateAsync`, `CreateInBodyReadingRequest`, the InBody numeric bounds (`CreateInBodyReadingRequestValidator`), `InBodyReadingMessages.Errors.ValueInvalid`, `AppUser.CompleteFirstLogin()`, `ISwimmerService.CompleteOnboardingAsync` (retained since Step 2).

## 2. Decisions (locked)

1. **Step 4 is the finish line.** Step 3 "Next Step" advances to Step 4 and no longer clears `IsFirstLogin`; Step 4 "Submit" clears the flag and lands on `/home`. Step 5 stays a disabled placeholder.
2. **Deferred (all-or-nothing) save, extended to four calls.** All writes happen at the **final Step 4 submit**, in order: (1) identity-vitals, (2) guardian-medical, (3) physiological, (4) inbody (+ clear first-login). Steps 1–3 "Next" are client-side only.
3. **Seven fields, matching the Captain/profile InBody section.** Height, Weight, Fat%, Muscle%, **Water%**, Bone Density, Body Density — all entered by the swimmer. No placeholder defaulting.
4. **Server-set `ReadingDate` = today.** No date field on screen (consistent with the exam/body-measurement writes). `recorded_by` = the swimmer's own user id.
5. **All seven required, using the existing InBody bounds.** Height/Weight `> 0` and `<= 999.9`; Fat/Muscle/Water `0–100` inclusive; Bone/Body `> 0` and `<= 99.99`. Because Fat/Muscle/Water accept 0, the frontend additionally requires each field to be **non-empty** (a blank must not silently pass as 0).
6. **API-layer orchestration (Approach 1).** One new endpoint; the controller injects the Health `IInBodyReadingService` alongside `ISwimmerService`. Order: save the reading (Health) → `CompleteOnboardingAsync` (Identity) **last**. Not transactional across contexts; mitigated by ordering + first-login cleared last + the frontend latches on the three prior steps.
7. **Self-service, JWT-scoped.** The Step 4 endpoint derives the swimmer from `CurrentUserId()`; it never accepts a swimmer id in the path. Gated to role `swimmer`.
8. **Retry safety via latches.** The frontend latches identity-vitals, guardian-medical, **and physiological** success so a retry after an inbody failure re-runs only the inbody (finish) call. The inbody call itself is not latched (it is last); a rare duplicate reading is possible only if `CompleteOnboardingAsync` fails after the reading saved — the same accepted trade-off as Step 2.
9. **No new tables, no DB migration, no seed changes.** `health.inbody_reading` already exists on all targets.

## 3. Data model

**No schema changes.** Reused table and its columns:

- `health.inbody_reading` — `id`, `swimmer_id` (→ `swimmer_profile.id`), `reading_date` (DateOnly, **server-set today** in onboarding), `height_cm`, `weight_kg`, `fat_pct`, `muscle_pct`, `water_pct`, `bone_density`, `body_density` (all `decimal`, non-null), `recorded_by` (→ `identity.app_user.id`), `created_at` (server-set in the entity ctor).
- `identity.app_user` — `is_first_login` (cleared on completion).

No entity-method additions: reuses the existing `InBodyReading` ctor and `AppUser.CompleteFirstLogin()`.

## 4. Backend

All paths under `backend/src/Modules/Identity/`, `backend/src/Modules/Health/`, and `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs`.

### 4.1 Change to Step 3 (make physiological non-completing)

In `SwimmerService.CompleteOnboardingPhysiologicalAsync`, **remove** the user lookup (`_users.GetByIdAsync` + its null guard) and the `user.CompleteFirstLogin()` call. It now resolves the profile, adds the `body_measurement`, and saves — returning `false` only when the profile is missing. (Parallel to how Step 2's guardian-medical stopped completing when Step 3 became the finish line.)

Resulting method:
```csharp
public async Task<bool> CompleteOnboardingPhysiologicalAsync(
    Guid userId, CompletePhysiologicalRequest req, CancellationToken ct = default)
{
    var profile = await _swimmers.GetByUserIdTrackedAsync(userId, ct);
    if (profile is null) return false;

    var measurement = new BodyMeasurement(profile.Id,
        req.ArmCm, req.ArmCm, req.LegCm, req.LegCm, req.TorsoCm, req.BustDiameterCm, req.WaistDiameterCm);
    await _swimmers.AddBodyMeasurementAsync(measurement, ct);
    await _swimmers.SaveChangesAsync(ct);
    return true;
}
```

### 4.2 Identity service addition (resolve swimmer id from JWT)

- `ISwimmerService` / `SwimmerService`: `Task<Guid?> GetSwimmerIdByUserAsync(Guid userId, CancellationToken ct = default)` — `GetByUserIdTrackedAsync(userId)`; returns `profile.Id` or `null`. Used by the Step-4 controller to get the `swimmerId` the Health service needs. (`CompleteOnboardingAsync`, retained since Step 2, is reused unchanged for the flag clear.)

### 4.3 InBody onboarding request DTO + validator (Health module)

- `CompleteInBodyRequest(decimal HeightCm, decimal WeightKg, decimal FatPct, decimal MusclePct, decimal WaterPct, decimal BoneDensity, decimal BodyDensity)` — the seven measurements (no date). Co-located in `InBodyReadingDtos.cs`.
- `CompleteInBodyRequestValidator` (Health Validators) — same metric bounds as `CreateInBodyReadingRequestValidator` minus the date rule: Height/Weight `GreaterThan(0m).LessThanOrEqualTo(999.9m)`; Fat/Muscle/Water `InclusiveBetween(0m, 100m)`; Bone/Body `GreaterThan(0m).LessThanOrEqualTo(99.99m)`; message `InBodyReadingMessages.Errors.ValueInvalid`.

### 4.4 Controller (`SwimmersController`) — new endpoint + orchestration (Approach 1)

Inject `IInBodyReadingService` (Health) alongside the existing `ISwimmerService` + `IObservationService`.

`POST("me/onboarding/inbody")` `[Authorize(Roles="swimmer")]`:
1. `var userId = CurrentUserId();`
2. `var swimmerId = await _service.GetSwimmerIdByUserAsync(userId, ct);` — `null` → `404` (`SwimmerMessages.Errors.ProfileNotFound`).
3. `var reading = new CreateInBodyReadingRequest(DateOnly.FromDateTime(DateTime.UtcNow), request.HeightCm, request.WeightKg, request.FatPct, request.MusclePct, request.WaterPct, request.BoneDensity, request.BodyDensity);`
4. `await _inbody.CreateAsync(swimmerId.Value, reading, userId, ct);` (Health save; `recordedBy` = the swimmer).
5. `await _service.CompleteOnboardingAsync(userId, ct);` — clears `IsFirstLogin` **last** (commit point).
6. `200` `ApiResponse<OnboardingStepResultDto>` (`SwimmerMessages.Success.OnboardingCompleted`) with `new OnboardingStepResultDto(false)`.

Validation failures → `400` (FluentValidation pipeline). Route note: `me/onboarding/inbody` is a literal-segment route; no collision with `GET {id:guid}` (same precedent as Steps 1–3).

## 5. Frontend (Angular)

Extend the existing `features/swimmer-onboarding/` slice.

### 5.1 Domain / data additions

- **domain/model/onboarding.ts** — `InBodySubmission { heightCm; weightKg; fatPct; musclePct; waterPct; boneDensity; bodyDensity }` (numbers).
- **data/dto/complete-inbody.dto.ts** — `CompleteInBodyDtoRq { heightCm; weightKg; fatPct; musclePct; waterPct; boneDensity; bodyDensity }` and `CompleteInBodyItemDtoRs extends BaseResponseRs<OnboardingResultDtoRs>` (import `OnboardingResultDtoRs` from `complete-identity-vitals.dto.ts`).
- **domain/repositories/swimmer-onboarding.repository.ts** + **impl** — `completeInBody(rq): Promise<CompleteInBodyItemDtoRs>` → `POST /api/swimmers/me/onboarding/inbody`.
- **domain/usecases/complete-inbody.use-case.ts** — `CompleteInBodyUseCase extends UseCase<InBodySubmission, void>` (mirror `CompletePhysiologicalUseCase`).

### 5.2 Viewmodel (`onboarding.viewmodel.ts`)

- `currentStep` widened to `signal<1 | 2 | 3 | 4>(1)`.
- New latch `private readonly physiologicalSaved = signal(false)` alongside `identitySaved` and `guardianSaved`.
- Step 4 signals: `heightCm`, `weightKg`, `fatPct`, `musclePct`, `waterPct`, `boneDensity`, `bodyDensity` (string signals, parsed on submit).
- `canSubmitStep4` computed: each of the seven is **non-empty** and its parsed number is within bounds — Height/Weight `> 0 && <= 999.9`; Fat/Muscle/Water `>= 0 && <= 100`; Bone/Body `> 0 && <= 99.99`.
- `goToStep4(): void { if (this.canSubmitStep3()) this.currentStep.set(4); }` and `backToStep3(): void { this.currentStep.set(3); }`.
- `submit()` restructured to the four-call deferred chain (guard on `canSubmitStep4` + the existing empty-`categoryId` guard):
  1. if `!identitySaved()` → `completeUc.run(step1Payload)`; on failure error+return; else latch.
  2. if `!guardianSaved()` → `completeGuardianMedicalUc.run(step2Payload)`; on failure error+return; else latch.
  3. if `!physiologicalSaved()` → `completePhysiologicalUc.run(step3Payload)`; on failure error+return; else latch.
  4. `completeInBodyUc.run({ heightCm, weightKg, fatPct, musclePct, waterPct, boneDensity, bodyDensity })`; on success → `auth.markOnboardingComplete()` + success toast + navigate `/home`; on failure → error toast, stay.
  - Completion now happens **after step 4** (inbody), not after physiological.

### 5.3 Page (`onboarding-wizard.page.html`)

- Step 3 "Next Step" now calls `vm.goToStep4()` (gated `!vm.canSubmitStep3()`), not `vm.submit()`.
- New Step 4 card `@if (vm.currentStep() === 4)` — a two-column 7-field grid matching the captain InBody layout (Height, Weight, Fat %, Muscle %, Water %, Bone Density, Body Density; `type="number"`, `step="any"`, `inputmode="decimal"`), each two-way bound to its signal. Footer: **Back** (`vm.backToStep3()`) + **Submit** (`vm.submit()`, disabled by `!vm.canSubmitStep4() || vm.submitting()`).
- Card title switches to `onboarding.steps.inbody` when `currentStep() === 4`.
- Stepper active/done state already follows `currentStep()` generically — no change.

### 5.4 i18n

Add to the `onboarding.*` block (en + ar): `fatPercentage` ("Fat Percentage (%)"), `musclePercentage` ("Muscle Percentage (%)"), `waterPercentage` ("Water Percentage (%)"), `boneDensity` ("Bone Density"), `bodyDensity` ("Body Density"), and `submit` ("Submit" — the finish button). `height` ("Height (cm)") and `weight` ("Weight (kg)") already exist (Step 1) and are reused. `steps.inbody` ("InBody") and `back` already exist.

## 6. Data flow

**Step 3 → Step 4:** swimmer fills Step 3 → "Next Step" (`canSubmitStep3`) → `currentStep = 4` (no network).

**Finish (Step 4 "Submit"):**
1. `POST …/identity-vitals` (if not latched) → identity + exam.
2. `POST …/guardian-medical` (if not latched) → guardians + observations.
3. `POST …/physiological` (if not latched) → body measurement.
4. `POST …/inbody` → controller: resolve swimmerId → `IInBodyReadingService.CreateAsync` (reading, date=today, recordedBy=swimmer) → `CompleteOnboardingAsync` (clears first-login) → `200 { mustChangePassword: false }`.
5. Frontend: `markOnboardingComplete()` → navigate `/home`.

## 7. Authorization

- `POST /api/swimmers/me/onboarding/inbody` → `[Authorize(Roles="swimmer")]`; swimmer resolved from `CurrentUserId()`. No path id. `recorded_by` = the swimmer's own user id.
- `onboardingGuard` (from Step 1) still gates `/onboarding`; unchanged.
- No coach/captain-facing endpoint changes.

## 8. Error handling / edge cases

- **User not a swimmer / no profile** → `404` from `GetSwimmerIdByUserAsync` (defensive; the guard already redirects).
- **Validation** (a value out of its bound, or unparseable/blank) → `400` server-side and blocked by `canSubmitStep4` client-side; the form stays intact with an error toast; nothing persisted.
- **Cross-context partial failure** — not atomic across Health/Identity. Ordering makes it safe: a failure before `CompleteOnboardingAsync` leaves `IsFirstLogin` set, so the swimmer re-enters and retries. The three prior steps are latched (no duplicate exam/guardians/observations/measurement). A rare **duplicate InBody reading** is possible only if `CompleteOnboardingAsync` fails after the reading saved — accepted (documented), the same trade-off as Step 2's duplicate observation.
- **Abandon at Step 4, re-enter** — deferred save means nothing was written that attempt; a clean restart.
- **Already onboarded** (`IsFirstLogin` false) → `onboardingGuard` redirects to `/home`.

## 9. Testing

**Backend**
- `Identity.UnitTests`: `SwimmerService.GetSwimmerIdByUserAsync` (returns `profile.Id`; `null` when no profile); `SwimmerService.CompleteOnboardingPhysiologicalAsync` **regression** (no longer clears `IsFirstLogin`; still adds the measurement + saves; the flag stays true).
- `Health.UnitTests`: `CompleteInBodyRequestValidator` (accepts a valid request; rejects Height ≤0 and >999.9; accepts Fat/Muscle/Water = 0 and = 100; rejects >100; rejects Bone/Body ≤0 and >99.99).
- `Api.UnitTests`: `SwimmersController` `POST me/onboarding/inbody` — `200` (asserts `IInBodyReadingService.CreateAsync` called with the seven values + `recordedBy = CurrentUserId`, then `CompleteOnboardingAsync` called **after** it), `404` (no profile → `CreateAsync` and `CompleteOnboardingAsync` never called), role-gating. Reuse the `Create(...)` test harness; extend it to also inject a mock `IInBodyReadingService`.
- `ArchitectureTests` stay green (orchestration in the API layer only; no Identity↔Health application dependency).

**Frontend**
- `complete-inbody.use-case.spec.ts` — posts the submission (all 7 fields); fails when the repo throws.
- `onboarding.viewmodel.spec.ts` — step navigation to 4 (`goToStep4` gated by `canSubmitStep3`; `backToStep3`); `canSubmitStep4` gating (all seven non-empty + in-bounds; a blank Water% is rejected even though 0 is a valid value; >100 Fat rejected); `submit()` runs identity → guardian → physiological → inbody in order, completes (markOnboardingComplete + navigate `/home`) only **after** inbody; a physiological failure stops before inbody and does not complete; the three latches prevent re-calling identity/guardian/physiological on a retry after an inbody failure.
- Keep the full suite green (backend + frontend) and the Angular AOT build clean.

## 10. Out of scope (deferred)

- **Step 5** (Done screen) — still rendered disabled.
- Letting the swimmer pick the InBody `ReadingDate` in onboarding (server-set to today; the coach edits the reading later via the profile InBody tab).
- Any coach-facing change (roster, profile, existing `/api/swimmers/{id}/…` endpoints).
- Showing prior InBody readings/trend in the wizard.
