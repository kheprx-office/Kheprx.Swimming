# Swimmer Onboarding — Per-Step Persistence (Idempotent) — Design

- **Date:** 2026-09-25
- **Status:** Approved design — pending spec review, then writing-plans
- **Branch:** feat/championships (all session work STAGED, NOT committed per user directive)
- **Type:** Architectural (reverses the deliberate deferred-latched submit; changes persistence timing + adds idempotency across 4 write paths + frontend flow rewrite)

## Context & Intent

The onboarding wizard currently **defers all four steps' saves to the final Submit** (a latched chain in `onboarding.viewmodel.ts submit()`). Consequences the user hit:
- A bad value (e.g., future exam date, out-of-range vitals) isn't caught until the very end, and the whole thing 400s on the first deferred call (`identity-vitals`) with a **generic** "Could not save your details" toast — the swimmer can't tell what's wrong.
- `canSubmitStep1` only checks required/`>0`, not the backend's full rules (DoB-in-past, exam-date≤today, phone format, hemoglobin<30 / height<300 / weight<500), so the wizard lets invalid data advance.

**User's chosen solution:** save **each step to the DB when its "Next" is clicked**, using the **server as the validation gate** — a 400 keeps the swimmer on the step and shows the **specific** server message; success advances. To make Back→edit→Next (and full re-runs on resume) safe, the step endpoints must be **idempotent** (update-or-insert the single onboarding row, never duplicate).

### Locked decisions (from clarifying questions)
1. **Per-step save**, made **idempotent** (update-or-insert), not the deferred end-submit.
2. **Server is the validation gate** — no client-side rule duplication; minimal `canSubmitStepN` only enables the button.
3. **Only Step 1 prefills** (the captain fills Step 1; steps 2–4 are swimmer-entered). Steps 2–4 are re-entered on resume and idempotently overwritten — no prefill for them.
4. **Completion (`IsFirstLogin` clear) stays at the final InBody step** — the single commit point.
5. **Idempotency mechanism: "update the latest row if it exists, else insert"** (recommended over uniform delete-then-insert). Safe because onboarding is first-login, so the swimmer has at most one of each onboarding row.

### Success criteria
- Clicking "Next" on a step persists that step; an invalid value returns 400 and the swimmer **stays on the step and sees the exact reason** (e.g., "Date of birth must be in the past").
- Re-submitting any step (Back→edit→Next, or a full resume re-run) **never creates duplicate** exams / observations / body-measurements / InBody readings.
- The final InBody step still completes onboarding (clears first-login) and lands the swimmer on `/my-profile`.

## Current-State Facts (verified)

Four onboarding write endpoints, all in `SwimmersController` (`[Authorize(Roles="swimmer")]`, resolved from the JWT via `CurrentUserId()`):

| Step | Route | Service / orchestration | INSERT point (duplicate risk) |
|---|---|---|---|
| 1 | `POST me/onboarding/identity-vitals` | `SwimmerService.CompleteIdentityVitalsAsync` | identity `UpdateProfile` (idempotent) **+ `AddExamAsync` (INSERT exam)** |
| 2 | `POST me/onboarding/guardian-medical` | controller orchestrates `UpsertOnboardingGuardiansAsync` (upsert) **+ `_observations.CreateAsync` per Yes item (INSERT, "insert-only")** | observations |
| 3 | `POST me/onboarding/physiological` | `SwimmerService.CompleteOnboardingPhysiologicalAsync` | **`AddBodyMeasurementAsync` (INSERT)** |
| 4 | `POST me/onboarding/inbody` | controller: `_inbody.CreateAsync` **(INSERT reading)** then `_service.CompleteOnboardingAsync` (clears first-login) | reading |

- Only the InBody step calls `CompleteOnboardingAsync` (line ~433) — the commit point. The XML-doc `<summary>` comments on identity-vitals/physiological still say "clearing first-login"/"finish line" — **stale**, to fix.
- Frontend `onboarding.viewmodel.ts`: `submit()` (Step 4) runs the deferred chain with `identitySaved`/`guardianSaved`/`physiologicalSaved` latches; `goToStep2/3/4` just navigate when `canSubmitStepN`. Error path shows generic `onboarding.saveFailed`.
- Error detail is available: the backend `ValidationFilter` returns `ApiResponse.Failure("Validation failed", <joined field messages>)`; `toAppError` puts the joined messages into `AppError.code`. So the specific text is already reachable client-side.

## Design

### 1. Frontend — per-step save-then-advance (`onboarding.viewmodel.ts`, `onboarding-wizard.page.html`)
- Convert each step transition into an async action that POSTs that step, then advances on success:
  - Step 1 "Next" → `completeIdentityVitals` → advance to 2
  - Step 2 "Next" → `completeGuardianMedical` → advance to 3
  - Step 3 "Next" → `completePhysiological` → advance to 4
  - Step 4 "Submit" → `completeInBody` (completes) → `markOnboardingComplete()` + navigate `/my-profile`
- On failure: **stay on the step**; if `error.status === 400 && error.code` → show `error.code` (specific); else generic. Add a `describeError(err)` helper and a per-step error signal (inline message) and/or the existing toast.
- Remove the deferred chain and the `identitySaved/guardianSaved/physiologicalSaved` latches from `submit()`; replace with per-step handlers (the current `goToStep2/3/4` become async submit-then-advance).
- Keep `canSubmitStepN` for button-enable only (required-field presence). A per-step `submitting` signal disables the button during the request.

### 2. Backend — idempotency ("update latest else insert")
Onboarding is first-login → the swimmer has at most one of each onboarding row → "latest" is always the onboarding row.
- **identity-vitals** (`CompleteIdentityVitalsAsync`): keep the identity `UpdateProfile`; then **if the swimmer has an exam, update the latest; else insert**. (`GetLatestExamAsync` + `UpdateExamAsync` already exist for the profile edit path.)
- **guardian-medical** (controller orchestration): keep guardians upsert; **replace** the swimmer's medical observations — remove existing, insert the current "Yes" set — instead of insert-only. (Needs a delete-by-swimmer for observations, or list-then-delete; `ObservationService` has `DeleteAsync(id)` and list-by-swimmer — confirm/add a bulk path in planning.)
- **physiological** (`CompleteOnboardingPhysiologicalAsync`): **update the latest body-measurement if one exists, else insert**. Body-measurement currently only inserts (profile "edit = new dated row"), so **add an "update latest measurement" capability** (repo/service method) — confirm exact shape in planning.
- **inbody** (controller): **update the latest InBody reading if one exists, else insert**, then `CompleteOnboardingAsync` (unchanged). (`InBodyReadingService` has create/update/list — reuse.)
- Guardians are already upsert; no change there.

**Alternative considered (not chosen):** uniform "delete the swimmer's onboarding rows, then insert" per step — simpler (no update methods, esp. avoids adding a body-measurement update) but recreates rows with new ids each submit. Chosen "update-latest-or-insert" preserves row identity. If planning finds the body-measurement update disproportionately costly, fall back to delete-then-insert for that one entity and note it.

### 3. Validation & errors (server as gate)
- No client-side rule duplication. The backend validators (`CompleteIdentityVitalsRequestValidator`, etc.) are the gate; a 400 surfaces the specific message and keeps the swimmer on the step (§1).
- Replace generic `onboarding.saveFailed` with the specific message via `describeError`.

### 4. Resume & completion
- Only Step 1 prefills (unchanged; the captain fills it). Steps 2–4 re-entered on resume; idempotency (§2) overwrites — no duplicates.
- `IsFirstLogin` clears only at the InBody step (unchanged commit point). Abandoning mid-wizard leaves first-login true → next login resumes at Step 1.

### 5. Cleanup
- Fix stale XML-doc comments on identity-vitals/physiological endpoints (only InBody completes).

### 6. Testing (TDD)
**Backend idempotency (the crux):**
- identity-vitals called twice for the same swimmer → exactly **one** exam (updated, not a second row); identity reflects the latest values.
- guardian-medical called twice → guardians upserted once; medical observations **replaced** (the second call's Yes-set is the only set), not duplicated.
- physiological called twice → exactly **one** body-measurement.
- inbody called twice → exactly **one** reading; `CompleteOnboardingAsync` clears first-login.
- Each still returns 404 when not a swimmer; 400 (validator) unchanged.

**Frontend (viewmodel):**
- Step 1/2/3 "Next": on use-case ok → currentStep advances; on 400 with `error.code` → stays on step + surfaces `error.code`; other error → generic. Step 4 submit: on ok → `markOnboardingComplete` + navigate `/my-profile`; on failure → stays + specific message.
- Removed latches: re-clicking a step re-POSTs (idempotent server handles dedup).

## Files Touched (indicative)
**Backend:** `SwimmersController.cs` (guardian-medical + inbody orchestration → replace/update-or-insert; stale comments), `SwimmerService.cs` (`CompleteIdentityVitalsAsync`, `CompleteOnboardingPhysiologicalAsync`), body-measurement repo/service (+update-latest), `ObservationService`/repo (+replace/delete-by-swimmer), `InBodyReadingService` (get-latest+update reuse); + backend tests.
**Frontend:** `onboarding.viewmodel.ts` (per-step handlers, `describeError`, drop latches), `onboarding-wizard.page.html` (button wiring + inline per-step error), i18n if new strings; + `onboarding.viewmodel.spec.ts`.

## Out of Scope
- Prefill for steps 2–4 on resume (explicitly excluded).
- Client-side validation parity (server is the gate).
- Step 2 guardian phone/national-id client format checks.
- Any DB migration (uses existing tables).
