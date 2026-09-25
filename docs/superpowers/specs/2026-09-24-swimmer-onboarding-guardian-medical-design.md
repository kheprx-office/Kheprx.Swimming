# Swimmer First-Login Onboarding — Step 2 (Guardian & Medical)

**Date:** 2026-09-24
**Status:** Approved design (spec)
**Scope:** The **second step** of the swimmer first-login onboarding wizard. Step 1 (Identity & Vitals) already exists but currently *finishes* onboarding on its "Next" click. This slice turns the wizard into a genuine **two-step flow**: Step 1 "Next" now **advances** to Step 2 instead of completing, and **Step 2 — Guardian & Medical — becomes the finish line** (it clears `IsFirstLogin` and lands the swimmer on the dashboard). Steps 3–5 (Physiological, InBody, Done) remain rendered placeholders. Step 2 writes **guardians** (`athlete.guardian`, Identity module) and a **medical history** as observations (`health.observation`, Health module). **No new tables; no DB migration; no seed changes** — both tables and the four observation categories already exist.

## 1. Summary

Today the wizard's Step 1 "Next Step" button submits identity + vitals **and** clears `IsFirstLogin`, dropping the swimmer straight on `/home` (see `2026-09-24-swimmer-onboarding-identity-vitals-design.md` §2.2). This slice moves the finish line to Step 2:

- **Step 1 "Next"** → client-side validate + **advance** to Step 2 (no server call yet).
- **Step 2 "Back"** → return to Step 1 (form state preserved in the viewmodel).
- **Step 2 "Next Step"** → submit **everything** (two sequential self-service calls), complete onboarding, navigate `/home`.

Step 2 collects, on one screen, two cards matching the approved screenshots:

- **Guardian Information** — Father and Mother, each with Full Name, National ID, Phone → two `athlete.guardian` rows (`father` / `mother` relations). **Both parents required.**
- **Medical History** — four fixed categories (Allergies, Previous Surgeries, Chronic Diseases, Autoimmune Diseases), each a **Yes/No** toggle + a **Details** text field → for every **Yes**, one `health.observation` row (`value` = the Details text). **No** writes nothing. **Insert-only.**

Because the write spans two modules (Guardian = `IdentityDbContext`, Observations = `HealthDbContext`), there is **no single atomic `SaveChanges`**. Orchestration lives at the **API/controller composition root** (Approach 1): upsert guardians (Identity) → insert observations (Health) → clear `IsFirstLogin` **last** as the commit point.

**Design references:**
- UI prototype (visual reference only): `C:\Users\envnt\Desktop\4dba8937-ce3f-44bd-b093-515f10a5ea2b\src\pages\SwimmerRegistration.tsx` (STAGE 2 block), `src/components/StepIndicator.tsx`
- Approved screenshots: `C:\Users\envnt\Desktop\mcp\2.png`, `3.png` (Step 2 = Guardian & Medical: two cards + Back / Next Step)
- Reused tables: `athlete.guardian` + `reference.guardian_relation` (see `2026-09-19-swimmer-profile-guardian-design.md`), `health.observation` + `reference.observation_category` (see `2026-09-18-swimmer-data-fields-design.md`) — all already implemented and seeded.
- Reused services: Identity guardian upsert (`SwimmerService.UpsertGuardiansAsync` / `UpsertOne`), Health `IObservationService.CreateAsync`.

## 2. Decisions (locked)

1. **Step 2 is the finish line.** Step 1 "Next" advances to Step 2 and no longer clears `IsFirstLogin`. Step 2 "Next Step" clears the flag and lands on `/home`. Steps 3–5 stay disabled placeholders.
2. **Deferred (all-or-nothing) save.** Step 1's "Next" is **client-side only** — no server call. Both writes happen at the **final Step 2 submit**, in this order: (1) identity + vitals via the existing (now non-completing) Step 1 endpoint, (2) guardian + medical via the new Step 2 endpoint. This avoids the duplicate `medical_exam` row that per-step saving would create on Back→Next navigation or re-entry, and keeps onboarding atomic-per-attempt.
3. **Both parents required.** Father **and** Mother must each be fully filled (Name + National ID + Phone). Mirrors the profile Guardian tab's both-required rule and reuses its validation.
4. **Medical: Yes → one observation with Details as value.** For each of the four categories set to **Yes**, insert one `health.observation` (`field_label` = category display label, `value` = Details text, **required when Yes**). **No** writes nothing. **Insert-only** — never deletes/updates, so it can't clobber Captain-Panel/Records entries in the same categories.
5. **Medical wire contract = observation items.** The frontend loads the observation-category lookup (existing `LoadObservationCategories` use-case), maps the four by code (`allergy`/`surgery`/`chronic`/`autoimmune`), and sends `{ categoryId, fieldLabel, value }` items. Mirrors exactly how the Captain Panel already writes observations; **no new backend code→id resolution**.
6. **API-layer orchestration (Approach 1).** One new endpoint; the controller injects both `ISwimmerService` (Identity) and `IObservationService` (Health). Module domains stay decoupled — only the composition root sees both. Not transactional across contexts; mitigated by ordering + idempotent guardian upsert + insert-only medical + first-login cleared last.
7. **Self-service, JWT-scoped.** The Step 2 endpoint derives the swimmer from `CurrentUserId()`; it never accepts a swimmer id in the path. Gated to role `swimmer`.
8. **No new tables, no DB migration, no seed changes.** `athlete.guardian`, `reference.guardian_relation`, `health.observation`, and the four `reference.observation_category` rows already exist on all targets (local + Aiven `Swimming_Production`).
9. **No National ID for the swimmer.** Unchanged from Step 1 — only the *guardians* carry a National ID (`athlete.guardian.national_id`, exactly 14 digits per the existing validator).

## 3. Data model

**No schema changes.** Reused tables and their relevant columns:

- `athlete.guardian` — `id`, `swimmer_id` (→ `swimmer_profile.id`), `relation_id` (→ `reference.guardian_relation`), `name`, `national_id` (varchar 14), `phone`.
- `reference.guardian_relation` — seeded codes `father`, `mother`.
- `health.observation` — `id`, `swimmer_id` (→ `swimmer_profile.id`), `category_id` (→ `reference.observation_category`), `field_label` (varchar), `value` (varchar), `observed_date` (timestamptz, **server-set** in the entity ctor), `recorded_by` (→ `identity.app_user.id`).
- `reference.observation_category` — seeded codes `allergy`, `surgery`, `chronic`, `autoimmune` (+ their `name_en`/`name_ar`).

No entity-method additions are required beyond the existing `Guardian` ctor/`Update`, `Observation` ctor, and `AppUser.CompleteFirstLogin()` (added in Step 1).

## 4. Backend

All paths under `backend/src/Modules/Identity/`, `backend/src/Modules/Health/` (reuse only), and `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs`.

### 4.1 Change to Step 1 (make it non-completing)

`SwimmerService.CompleteIdentityVitalsAsync` — **remove** the `user.CompleteFirstLogin()` call (step 7 of that method). It now updates identity + inserts the exam and returns `OnboardingStepResultDto(false)` **without** clearing the flag. The single `SaveChanges` and all reference guards are unchanged.

> Consequence: after Step 1's server call, `IsFirstLogin` is still true. The flag is cleared only by the Step 2 endpoint (§4.4). Because Step 1's call now happens only at the final Step 2 submit (Decision 2), the flag lifetime is effectively unchanged from the user's perspective.

### 4.2 DTOs (`SwimmerDtos.cs` additions)

- `GuardianInputDto` — **reuse** the existing type from `GuardianDtos.cs` (`Name`, `NationalId`, `Phone`).
- `OnboardingMedicalItemDto(Guid CategoryId, string FieldLabel, string Value)` — one medical observation to insert.
- `CompleteGuardianMedicalRequest(GuardianInputDto Father, GuardianInputDto Mother, IReadOnlyList<OnboardingMedicalItemDto> Medical)`.
- Response reuses `OnboardingStepResultDto(bool MustChangePassword)` — returns `false` on success (onboarding complete).

### 4.3 Validator

`CompleteGuardianMedicalRequestValidator`:
- `Father` and `Mother` each validated by **reusing `GuardianInputDtoValidator`** (the same child validator used by `UpsertGuardiansRequestValidator`): `Name` not empty, `NationalId` exactly 14 digits (`^\d{14}$`), `Phone` not empty. **Both required** — `NotNull()` on each parent, no null/blank parent.
- `Medical` — each item: `CategoryId` not empty, `FieldLabel` not empty, `Value` not empty (the "Yes requires Details" rule; the frontend only sends items for Yes-with-details). Empty list is allowed (all four = No).

### 4.4 Service additions (`ISwimmerService` / `SwimmerService`, Identity)

- `Task<Guid?> UpsertOnboardingGuardiansAsync(Guid userId, GuardianInputDto father, GuardianInputDto mother, CancellationToken)`:
  1. `GetByUserIdTrackedAsync(userId)` → profile; `null` → `null` (→ 404).
  2. `UpsertOne(profile.Id, "father", father)` and `UpsertOne(profile.Id, "mother", mother)` — **reuse** the existing private `UpsertOne` (resolves relation id by code, adds or updates the guardian).
  3. `SaveChangesAsync` (Identity).
  4. Return `profile.Id` (the `swimmerId` the caller needs for observations).
- `Task<bool> CompleteOnboardingAsync(Guid userId, CancellationToken)`:
  1. `GetByUserIdTrackedAsync(userId)` → profile; `null` → `false` (→ 404).
  2. `_users.GetByIdAsync(profile.UserId)` → user; `null` → `false`.
  3. `user.CompleteFirstLogin()`; `SaveChangesAsync` (Identity). Idempotent on the flag.

(No Identity→Health coupling: the service never touches observations.)

### 4.5 Controller (`SwimmersController`) — new endpoint + orchestration (Approach 1)

Inject `IObservationService` alongside the existing `ISwimmerService`.

`POST("me/onboarding/guardian-medical")` `[Authorize(Roles="swimmer")]`:
1. `var userId = CurrentUserId();`
2. `var swimmerId = await _service.UpsertOnboardingGuardiansAsync(userId, req.Father, req.Mother, ct);` — `null` → `404` (`SwimmerMessages.Errors.ProfileNotFound`).
3. For each `item` in `req.Medical`: `await _observations.CreateAsync(new CreateObservationRequest(swimmerId.Value, item.CategoryId, item.FieldLabel, item.Value), recordedBy: userId, ct);` (each `CreateAsync` does its own Health `SaveChanges`; `observed_date` is server-set).
4. `await _service.CompleteOnboardingAsync(userId, ct);` — clears `IsFirstLogin` **last** (commit point).
5. `200` `ApiResponse<OnboardingStepResultDto>` (`SwimmerMessages.Success.OnboardingCompleted`) with `new OnboardingStepResultDto(false)`.

Validation failures → `400` (FluentValidation pipeline, as elsewhere). Unknown `category_id` → surfaced as a DB/FK error consistent with the existing observation write path.

Route note: `me/onboarding/guardian-medical` is a literal-segment route and does not collide with `GET {id:guid}` (same precedent as Step 1's `me/onboarding/identity-vitals`).

## 5. Frontend (Angular)

Extend the existing `features/swimmer-onboarding/` slice — no new feature folder.

### 5.1 Domain / data additions

- **domain/model/onboarding.ts** — add:
  - `GuardianInput { name; nationalId; phone }`.
  - `OnboardingMedicalItem { categoryId; fieldLabel; value }`.
  - `GuardianMedicalSubmission { father: GuardianInput; mother: GuardianInput; medical: OnboardingMedicalItem[] }`.
- **domain/repositories/swimmer-onboarding.repository.ts** — add `completeGuardianMedical(body: GuardianMedicalSubmission): Promise<Result<void>>` (and, if reused for guardian prefill, `getGuardians()` — optional, see §5.3).
- **domain/usecases/** — add `complete-guardian-medical.use-case.ts` (thin `run(body)` returning the `Result` envelope).
- **data/dto/** — `complete-guardian-medical.dto.ts` (`…DtoRq`) with the request mapper; validation guard consistent with existing DTOs.
- **data/repositories/swimmer-onboarding.repository.impl.ts** — `POST /api/swimmers/me/onboarding/guardian-medical`.

### 5.2 Wizard page + viewmodel (two real steps)

- **onboarding-wizard.page.ts / .html** — introduce a `currentStep` signal (1 | 2). The step strip's active/checked state follows `currentStep` (Step 1 shows a check once past it; Step 2 becomes active). Steps 3–5 stay disabled.
  - **Step 1 content** (existing) — the "Next" button now calls `vm.goToStep2()` (guarded by the existing `canSubmit`, renamed to `canSubmitStep1`), which only advances the signal.
  - **Step 2 content** (new) — the two-card Oasis layout from the screenshots:
    - *Guardian Information*: Father Full Name / Father National ID / Father Phone / Mother Full Name / Mother National ID / Mother Phone (`TextFieldComponent` / plain inputs matching Step 1 styling).
    - *Medical History*: four rows (Allergies, Previous Surgeries, Chronic Diseases, Autoimmune Diseases), each a Yes/No radio pair (default **No**) + a Details text input (enabled/required when **Yes**).
    - Footer: **Back** (→ Step 1) and **Next Step** (= Finish).
- **onboarding.viewmodel.ts** — add:
  - Step 2 signals: `fatherName/fatherNationalId/fatherPhone`, `motherName/motherNationalId/motherPhone`; per-category `…Yes` booleans + `…Details` strings; `observationCategories` lookup signal.
  - `load()` also loads observation categories (reuse `LoadObservationCategoriesUseCase`); optionally prefill guardians (§5.3).
  - `canSubmitStep2` computed: both parents' three fields non-empty **and** every category with Yes has non-empty Details.
  - `submit()` (final) — sequential:
    1. `await completeIdentityVitalsUc.run(step1Payload)`; on failure → error toast, stay.
    2. Build `medical` items from the four toggles (only Yes-with-details), map the four `categoryId`s from `observationCategories` by code.
    3. `await completeGuardianMedicalUc.run({ father, mother, medical })`; on success → `auth.markOnboardingComplete()` + success toast + navigate `/home`; on failure → error toast, stay (first-login still set → safe retry).
  - `goToStep2()` / `backToStep1()` toggle `currentStep`.
  - Keep Step 1 and Step 2 field state clearly grouped; if the viewmodel grows past comfort, extract a small Step 2 form helper.

### 5.3 Guardian prefill (optional, safe)

On re-entry (a prior attempt saved guardians), the Guardian card **may** prefill from the existing read path. If included, add `getGuardians()` to the repository (mapping the existing `GET /api/swimmers/{id}/guardians` is coach-scoped, so prefer a `me`-scoped read only if cheap; otherwise **omit prefill** — the deferred-save model means a clean re-entry usually starts blank). Prefill is a nice-to-have, not required for correctness (guardian upsert is idempotent).

### 5.4 i18n

Add to the existing `onboarding.*` block (en + ar):
- Card titles: `guardianInfo` ("Guardian Information"), `medicalHistory` ("Medical History").
- Guardian labels: `fatherName`, `fatherNationalId`, `fatherPhone`, `motherName`, `motherNationalId`, `motherPhone`.
- Medical labels: `allergies`, `surgeries`, `chronic`, `autoimmune`, `details`, `yes`, `no`.
- Buttons: reuse `next` ("Next Step") for Step 2 finish; add `back` ("Back").
- The four medical `fieldLabel` values sent to the API use the English category label (stable, matches how Records displays them) — the *display* uses i18n; the *stored `field_label`* is the English label.

## 6. Data flow

**Step 1 → Step 2:** swimmer fills Step 1 → "Next" (`canSubmitStep1`) → `currentStep = 2` (no network).

**Finish (Step 2 "Next Step"):**
1. `POST …/me/onboarding/identity-vitals` → updates `app_user` + `swimmer_profile`, inserts `medical_exam` (flag **not** cleared).
2. `POST …/me/onboarding/guardian-medical` → controller: upsert Father+Mother `guardian` (Identity save) → insert N `observation` rows (Health saves) → `CompleteFirstLogin` (Identity save, last) → `200 { mustChangePassword: false }`.
3. Frontend: `markOnboardingComplete()` → navigate `/home`.

## 7. Authorization

- `POST /api/swimmers/me/onboarding/guardian-medical` → `[Authorize(Roles="swimmer")]`; swimmer resolved from `CurrentUserId()`. No path id accepted. `recorded_by` on each observation = the swimmer's own user id.
- `onboardingGuard` (from Step 1) still gates `/onboarding`; unchanged.
- No coach/captain-facing endpoint changes.

## 8. Error handling / edge cases

- **User not a swimmer / no profile** → `404` from `UpsertOnboardingGuardiansAsync` (defensive; the guard already redirects).
- **Validation** (missing parent field, Yes without Details) → `400`; the form stays intact with an error toast; nothing persisted.
- **Cross-context partial failure** — not atomic across Identity/Health. Ordering makes it safe: a failure before step 4 leaves `IsFirstLogin` set, so the swimmer re-enters and retries. Guardian upsert is **idempotent**; medical is **insert-only**, so a retry may create at most a rare **duplicate observation** (accepted — never clobbers Captain/Records data).
- **Abandon at Step 2, re-enter** — deferred-save means nothing was written that attempt; a clean restart with **no duplicate exam**.
- **All four medical = No** — `Medical` is an empty list; endpoint still upserts guardians and completes.
- **Already onboarded** (`IsFirstLogin` false) — `onboardingGuard` redirects to `/home`; `CompleteOnboardingAsync` is idempotent on the flag.

## 9. Testing

**Backend**
- `Identity.UnitTests`: `SwimmerService.UpsertOnboardingGuardiansAsync` (adds both guardians when none exist; updates when present; returns `swimmerId`; null when no profile); `SwimmerService.CompleteOnboardingAsync` (clears flag; idempotent; null when no profile); `SwimmerService.CompleteIdentityVitalsAsync` **regression** (no longer clears the flag; still saves identity + exam); `CompleteGuardianMedicalRequestValidator` (both parents required; Yes-item requires Value; empty medical allowed).
- `Api.UnitTests`: `SwimmersController` `POST me/onboarding/guardian-medical` — `200` (guardians upserted, observations created with `recordedBy = CurrentUserId`, `CompleteOnboarding` called **after** observations), `404` (no profile), `400` (invalid), role-gating (`swimmer` only). Mock `IObservationService` and assert call order (guardians → observations → complete). Reuse the `CurrentUserId` HttpContext plumbing added for Step 1.
- `Health.UnitTests` / `ObservationService` — unchanged (reused as-is).
- `ArchitectureTests` stay green (no Identity→Health application dependency introduced).

**Frontend**
- `complete-guardian-medical.use-case.spec.ts`, repository-impl spec for the new `POST`.
- `onboarding.viewmodel.spec.ts` — step navigation (`goToStep2` gated by `canSubmitStep1`; `backToStep1`); `canSubmitStep2` gating (both parents + Yes-requires-details); `submit()` builds medical items from toggles + maps category ids by code; success path calls identity-vitals **then** guardian-medical **then** `markOnboardingComplete` + navigate `/home`; failure of either call keeps the form and shows a toast (and does not mark complete).
- Keep the full suite green (backend + frontend) and the Angular AOT build clean.

## 10. Out of scope (deferred)

- **Steps 3–5** (Physiological, InBody, Done screen) — still rendered disabled.
- Cross-context **distributed transaction** / outbox — the ordered, idempotent write is sufficient for a first-login flow.
- **Editing** guardians/observations from the wizard beyond the initial submit (the profile Guardian/Records tabs own ongoing edits).
- Any **coach-facing** change (roster, profile, existing `/api/swimmers/{id}/…` endpoints).
- Guardian **prefill** is optional (§5.3); if it complicates the `me`-scoped read, ship without it.
