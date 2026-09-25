# Swimmer First-Login Onboarding — Step 3 (Physiological)

**Date:** 2026-09-24
**Status:** Approved design (spec)
**Scope:** The **third step** of the swimmer first-login onboarding wizard. Builds directly on Step 2 (Guardian & Medical), which currently *finishes* onboarding. This slice makes the wizard a **three-step flow**: Step 2 "Next Step" now **advances** to Step 3 instead of completing, and **Step 3 — Physiological — becomes the finish line** (it saves a body measurement and clears `IsFirstLogin`, landing the swimmer on the dashboard). Steps 4–5 (InBody, Done) remain rendered placeholders. Step 3 writes one `athlete.body_measurement` row (Identity module — **single-module, one atomic `SaveChanges`**). **No new tables; no DB migration; no seed changes.**

> **Depends on the uncommitted Step 2 work.** Step 2 (Guardian & Medical) is implemented and **staged but not committed** on `feat/championships`. Step 3 modifies the same onboarding files (viewmodel, page, controller, `SwimmerService`) on top of Step 2's staged changes. See `2026-09-24-swimmer-onboarding-guardian-medical-design.md`.

## 1. Summary

Today (post Step 2) the wizard's Step 2 "Next Step" upserts guardians + medical observations **and** clears `IsFirstLogin`, dropping the swimmer on `/home`. This slice moves the finish line to Step 3:

- **Step 1 "Next"** → advance to Step 2 (client-side; unchanged).
- **Step 2 "Next Step"** → **advance** to Step 3 (client-side, no server call; its guardian-medical endpoint no longer completes).
- **Step 3 "Next Step"** → submit **everything** (deferred chain: identity-vitals → guardian-medical → physiological), complete onboarding, navigate `/home`.

Step 3 collects five physiological measurements on one screen, matching the approved screenshot (`2.png`):

- **Arm Length (cm)**, **Leg Length (cm)**, **Torso Length (cm)**, **Bust Diameter (cm)**, **Waist Diameter (cm)** → one `athlete.body_measurement` row.

Because `athlete.body_measurement` has **seven** non-nullable columns (arm and leg are split left/right), the five screen fields map onto seven columns: **Arm Length → both `RightArmCm` and `LeftArmCm`; Leg Length → both `RightLegCm` and `LeftLegCm`; Torso/Bust/Waist 1:1** (locked decision). A coach later refines left vs right in the profile Physiological tab. The 5→7 duplication happens **server-side**, in one place.

Physiological is entirely in the **Identity module** (`athlete.body_measurement` + `identity.app_user`, both `IdentityDbContext`), so the measurement write **and** the first-login clear commit in **one atomic `SaveChanges`** — no cross-context orchestration (unlike Step 2).

**Design references:**
- UI prototype (visual reference only): `C:\Users\envnt\Desktop\4dba8937-ce3f-44bd-b093-515f10a5ea2b\src\pages\SwimmerRegistration.tsx` (STAGE 3 block)
- Approved screenshot: `C:\Users\envnt\Desktop\mcp\2.png` (Step 3 = Physiological active: 5 fields, Back / Next Step)
- Reused table: `athlete.body_measurement` (see `2026-09-19-swimmer-profile-physiological-design.md`) — already implemented and migrated.
- Reused code: `BodyMeasurement` entity, `ISwimmerProfileRepository.AddBodyMeasurementAsync`, `AppUser.CompleteFirstLogin()`, `SwimmerService.GetByUserIdTrackedAsync`, the numeric bounds from `CreateBodyMeasurementRequestValidator` (each value `> 0` and `<= 999.9`).

## 2. Decisions (locked)

1. **Step 3 is the finish line.** Step 2 "Next Step" advances to Step 3 and no longer clears `IsFirstLogin`; Step 3 "Next Step" clears the flag and lands on `/home`. Steps 4–5 stay disabled placeholders.
2. **Deferred (all-or-nothing) save, extended.** All writes still happen at the **final Step 3 submit**, in order: (1) identity-vitals, (2) guardian-medical, (3) physiological (+ clear first-login). Steps 1 and 2 "Next" are client-side only.
3. **Five screen fields → seven columns.** Arm Length → `RightArmCm` **and** `LeftArmCm`; Leg Length → `RightLegCm` **and** `LeftLegCm`; Torso → `TorsoCm`; Bust → `BustDiameterCm`; Waist → `WaistDiameterCm`. Duplication is **server-side**.
4. **All five required.** Each value must be `> 0` and `<= 999.9` (reusing the existing measurement bounds). Consistent with the profile Physiological tab.
5. **Single atomic write.** Adding the `body_measurement` row and clearing `IsFirstLogin` happen in one `IdentityDbContext` `SaveChanges` (both are Identity-module tables).
6. **Self-service, JWT-scoped.** The Step 3 endpoint derives the swimmer from `CurrentUserId()`; it never accepts a swimmer id in the path. Gated to role `swimmer`.
7. **Retry safety via latches.** The frontend latches identity-vitals **and** guardian-medical success so a retry after a physiological failure re-runs only physiological. Physiological being atomic-with-completion means a failure saves nothing → a clean retry, no duplicate measurement.
8. **No new tables, no DB migration, no seed changes.** `athlete.body_measurement` already exists on all targets.

## 3. Data model

**No schema changes.** Reused table and its columns:

- `athlete.body_measurement` — `id`, `swimmer_id` (→ `swimmer_profile.id`), `measured_at` (DateOnly, **server-set** in the entity ctor to today UTC), `right_arm_cm`, `left_arm_cm`, `right_leg_cm`, `left_leg_cm`, `torso_cm`, `bust_diameter_cm`, `waist_diameter_cm` (all `decimal`, non-null).
- `identity.app_user` — `is_first_login` (cleared on completion).

No entity-method additions: reuses the existing `BodyMeasurement` ctor and `AppUser.CompleteFirstLogin()`.

## 4. Backend (Identity module)

All paths under `backend/src/Modules/Identity/` + `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs`.

### 4.1 Change to Step 2 (make guardian-medical non-completing)

In `SwimmersController.CompleteOnboardingGuardianMedical` (the Step-2 endpoint), **remove** the final `await _service.CompleteOnboardingAsync(userId, ct);` call. Guardian-medical now upserts guardians + inserts observations only; it no longer clears `IsFirstLogin`. (Parallel to how Step 1's `CompleteIdentityVitalsAsync` stopped completing when Step 2 became the finish line.) `CompleteOnboardingAsync` remains in the service, now called only by the Step-3 path (§4.4).

> Consequence: after the guardian-medical call, `IsFirstLogin` is still true. Because the frontend defers all calls to the final Step-3 submit (Decision 2), the flag lifetime is unchanged from the user's perspective.

### 4.2 DTOs (`SwimmerDtos.cs` additions)

- `CompletePhysiologicalRequest(decimal ArmCm, decimal LegCm, decimal TorsoCm, decimal BustDiameterCm, decimal WaistDiameterCm)` — the five screen values.
- Response reuses `OnboardingStepResultDto(bool MustChangePassword)` — returns `false` on success.

### 4.3 Validator

`CompletePhysiologicalRequestValidator` — each of the five values `> 0` and `<= 999.9`, reusing the message `SwimmerMessages.Errors.MeasurementInvalid` (mirror the private `Rule(...)` helper style of `CreateBodyMeasurementRequestValidator`).

### 4.4 Service (`ISwimmerService` / `SwimmerService`)

- `Task<bool> CompleteOnboardingPhysiologicalAsync(Guid userId, CompletePhysiologicalRequest req, CancellationToken ct = default)`:
  1. `GetByUserIdTrackedAsync(userId)` → profile; `null` → `false` (→ 404).
  2. `_users.GetByIdAsync(profile.UserId)` → user; `null` → `false`.
  3. Construct `new BodyMeasurement(profile.Id, req.ArmCm, req.ArmCm, req.LegCm, req.LegCm, req.TorsoCm, req.BustDiameterCm, req.WaistDiameterCm)` (arm → both arms, leg → both legs); `AddBodyMeasurementAsync(measurement)`.
  4. `user.CompleteFirstLogin()`.
  5. One `SaveChangesAsync` (single `IdentityDbContext` → measurement + flag persist atomically). Return `true`.

- `CompleteOnboardingAsync` (added in Step 2) is retained but no longer called by the guardian-medical endpoint; it is not called by Step 3 either (Step 3 clears the flag inline for atomicity). It may become unused — leave it in place (Step 4 may reuse the pattern); note it in the plan.

### 4.5 Controller (`SwimmersController`)

`POST("me/onboarding/physiological")` `[Authorize(Roles="swimmer")]`:
1. `var ok = await _service.CompleteOnboardingPhysiologicalAsync(CurrentUserId(), request, ct);`
2. `!ok` → `404` (`SwimmerMessages.Errors.ProfileNotFound`).
3. `200` `ApiResponse<OnboardingStepResultDto>` (`SwimmerMessages.Success.OnboardingCompleted`) with `new OnboardingStepResultDto(false)`.

Validation failures → `400` (FluentValidation pipeline). Route note: `me/onboarding/physiological` is a literal-segment route; no collision with `GET {id:guid}` (same precedent as Steps 1–2).

## 5. Frontend (Angular)

Extend the existing `features/swimmer-onboarding/` slice.

### 5.1 Domain / data additions

- **domain/model/onboarding.ts** — add `PhysiologicalSubmission { armCm; legCm; torsoCm; bustDiameterCm; waistDiameterCm }` (numbers).
- **data/dto/complete-physiological.dto.ts** — `CompletePhysiologicalDtoRq { armCm; legCm; torsoCm; bustDiameterCm; waistDiameterCm }` and `CompletePhysiologicalItemDtoRs extends BaseResponseRs<OnboardingResultDtoRs>` (import `OnboardingResultDtoRs` from `complete-identity-vitals.dto.ts`).
- **domain/repositories/swimmer-onboarding.repository.ts** + **impl** — add `completePhysiological(rq): Promise<CompletePhysiologicalItemDtoRs>` → `POST /api/swimmers/me/onboarding/physiological`.
- **domain/usecases/complete-physiological.use-case.ts** — `CompletePhysiologicalUseCase extends UseCase<PhysiologicalSubmission, void>` (mirror `CompleteGuardianMedicalUseCase`).

### 5.2 Viewmodel (`onboarding.viewmodel.ts`)

- `currentStep` widened to `signal<1 | 2 | 3>(1)`.
- New latch `private readonly guardianSaved = signal(false)` alongside the existing `identitySaved`.
- Step 3 signals: `armCm`, `legCm`, `torsoCm`, `bustDiameterCm`, `waistDiameterCm` (string signals, parsed on submit).
- `canSubmitStep3` computed: every one of the five parses to a number `> 0` and `<= 999.9`.
- `goToStep3(): void { if (this.canSubmitStep2()) this.currentStep.set(3); }` and `backToStep2(): void { this.currentStep.set(2); }`.
- `submit()` restructured to the three-call deferred chain (guard on `canSubmitStep3` + the existing empty-`categoryId` guard):
  1. if `!identitySaved()` → `completeUc.run(step1Payload)`; on failure error+return; else latch.
  2. if `!guardianSaved()` → `completeGuardianMedicalUc.run(step2Payload)`; on failure error+return; else latch.
  3. `completePhysiologicalUc.run({ armCm, legCm, torsoCm, bustDiameterCm, waistDiameterCm })`; on success → `auth.markOnboardingComplete()` + success toast + navigate `/home`; on failure → error toast, stay (first-login still set → safe retry re-runs only physiological).
  - Completion (`markOnboardingComplete` + navigate) now happens **after step 3**, not after guardian-medical.

### 5.3 Page (`onboarding-wizard.page.ts / .html / .scss`)

- Step 2 "Next Step" now calls `vm.goToStep3()` (gated `!vm.canSubmitStep2()`), not `vm.submit()`.
- New Step 3 card `@if (vm.currentStep() === 3)` — the two-column 5-field grid from the screenshot (Arm Length, Leg Length, Torso Length, Bust Diameter, Waist Diameter; `type="number"`, `inputmode="decimal"`), each two-way bound to its signal. Footer: **Back** (`vm.backToStep2()`) + **Next Step** (`vm.submit()`, disabled by `!vm.canSubmitStep3() || vm.submitting()`).
- Card title switches to `onboarding.steps.physiological` when `currentStep() === 3`.
- Stepper active/done state already follows `currentStep()` generically (from Step 2) — no change needed.

### 5.4 i18n

Add to the `onboarding.*` block (en + ar): `armLength` ("Arm Length (cm)"), `legLength` ("Leg Length (cm)"), `torsoLength` ("Torso Length (cm)"), `bustDiameter` ("Bust Diameter (cm)"), `waistDiameter` ("Waist Diameter (cm)"). `steps.physiological` ("Physiological") and `back`/`next` already exist.

## 6. Data flow

**Step 2 → Step 3:** swimmer fills Step 2 → "Next Step" (`canSubmitStep2`) → `currentStep = 3` (no network).

**Finish (Step 3 "Next Step"):**
1. `POST …/identity-vitals` (if not latched) → identity + exam saved.
2. `POST …/guardian-medical` (if not latched) → guardians + observations saved (no completion).
3. `POST …/physiological` → controller: `CompleteOnboardingPhysiologicalAsync` builds the 7-column measurement from the 5 values, adds it, `CompleteFirstLogin()`, one atomic save → `200 { mustChangePassword: false }`.
4. Frontend: `markOnboardingComplete()` → navigate `/home`.

## 7. Authorization

- `POST /api/swimmers/me/onboarding/physiological` → `[Authorize(Roles="swimmer")]`; swimmer resolved from `CurrentUserId()`. No path id.
- `onboardingGuard` (from Step 1) still gates `/onboarding`; unchanged.
- No coach/captain-facing endpoint changes.

## 8. Error handling / edge cases

- **User not a swimmer / no profile** → `404` from `CompleteOnboardingPhysiologicalAsync` (defensive; the guard already redirects).
- **Validation** (a value ≤0 or >999.9, or unparseable) → `400`; the form stays intact with an error toast; nothing persisted.
- **Retry after a failed later step** — latches on identity-vitals and guardian-medical mean a retry re-runs only the failed/last calls. Guardian upsert is idempotent, medical is insert-only. Physiological + completion is one atomic write, so a physiological failure persists nothing → a retry creates no duplicate measurement.
- **Abandon at Step 3, re-enter** — deferred save means nothing was written that attempt; a clean restart.
- **Already onboarded** (`IsFirstLogin` false) → `onboardingGuard` redirects to `/home`.

## 9. Testing

**Backend**
- `Identity.UnitTests`: `SwimmerService.CompleteOnboardingPhysiologicalAsync` (maps ArmCm→both arm columns, LegCm→both leg columns, torso/bust/waist 1:1; adds the measurement; clears `IsFirstLogin`; one `SaveChanges`; `false`/null when no profile); `CompletePhysiologicalRequestValidator` (accepts a valid request; rejects a value ≤0; rejects a value >999.9).
- `Api.UnitTests`: `SwimmersController` `POST me/onboarding/physiological` — `200` (service ok) and `404` (service false); role-gating. **Regression**: the guardian-medical endpoint no longer calls `CompleteOnboardingAsync` (assert `CompleteOnboardingAsync` is not invoked by that action).
- `ArchitectureTests` stay green.

**Frontend**
- `complete-physiological.use-case.spec.ts` — posts the submission; fails when the repo throws.
- `onboarding.viewmodel.spec.ts` — step navigation to 3 (`goToStep3` gated by `canSubmitStep2`; `backToStep2`); `canSubmitStep3` gating (all five in (0, 999.9]); `submit()` runs identity-vitals → guardian-medical → physiological in order, completes (markOnboardingComplete + navigate `/home`) only **after** physiological; a physiological failure keeps the form and does not complete; the latches prevent re-calling identity-vitals / guardian-medical on a retry.
- Keep the full suite green (backend + frontend) and the Angular AOT build clean.

## 10. Out of scope (deferred)

- **Steps 4–5** (InBody, Done screen) — still rendered disabled.
- Editing left vs right arm/leg separately in onboarding (the profile Physiological tab owns that; onboarding writes both sides equal).
- Any coach-facing change (roster, profile, existing `/api/swimmers/{id}/…` endpoints).
- Showing prior measurements in the wizard.
