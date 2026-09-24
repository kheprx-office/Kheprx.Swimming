# Swimmer First-Login Onboarding — Step 1 (Identity & Vitals)

**Date:** 2026-09-24
**Status:** Approved design (spec)
**Scope:** The **swimmer first-login flow**. When a swimmer signs in for the first time, they are routed into a full-screen **onboarding wizard** with a 5-step strip (① Identity & Vitals → ② Guardian & Medical → ③ Physiological → ④ InBody → ⑤ Done). **Only Step 1 — Identity & Vitals — is built** this pass; steps 2–5 are rendered as disabled placeholders. Completing Step 1 **clears `IsFirstLogin`** and lands the swimmer on the dashboard. Includes the **minimal swimmer-login wiring** required to reach the flow. **No password-change step for swimmers.** No new tables; no DB migration.

## 1. Summary

Today a first-login user (flagged `IsFirstLogin`) is forced to `/change-password`; that flag is cleared only by `AuthService.ChangePasswordAsync` (via `AppUser.SetPassword`). This design adds a **swimmer-specific** first-login path: a first-login **swimmer** is sent to a new `/onboarding` wizard instead of `/change-password` (the coach/captain change-password flow is untouched).

Step 1 collects the swimmer's own **Identity & Vitals** on one screen. Fields already known from account creation are **pre-filled and editable**; the genuinely new data (a medical exam + vitals) is entered fresh:

- **Identity (pre-filled, editable)** — Full Name (`app_user.name_en`; `name_ar` preserved), Gender (`app_user.gender_id`), Date of Birth (`app_user.dob`), Training Club (`swimmer_profile.training_club_id`).
- **Sport** — locked display ("Swimming"); single-sport app, not stored.
- **Medical exam + vitals (new)** — Last Examination Date, Internal Medicine / Heart / Spine assessments, Blood Type (optional), Hemoglobin, Height, Weight → a new `athlete.medical_exam` row.

On submit, a **single atomic self-service endpoint** updates identity, inserts the exam, and clears `IsFirstLogin` — resolving the swimmer from the **JWT** (never a path id). The frontend flips its session flag and navigates to `/home`.

**Design references:**
- UI prototype (visual reference only): `C:\Users\envnt\Desktop\4dba8937-ce3f-44bd-b093-515f10a5ea2b\` (`src/pages/RegistrationWizard.tsx`, `src/components/StepIndicator.tsx`)
- Approved screenshots: `C:\Users\envnt\Desktop\mcp\1.png` (login with "Swimmer (First Login)" role), `2.png` (5-step wizard, Step 1 = Identity & Vitals active)
- Reused tables: `identity.app_user`, `identity.swimmer_profile`, `athlete.medical_exam`, `reference.club` / `gender` / `blood_type` / `fitness_assessment` (all already implemented — see `docs/superpowers/specs/2026-09-19-swimmer-profile-identity-vitals-design.md`)

## 2. Decisions (locked)

1. **Wizard only — no password change for swimmers.** The swimmer never sees `/change-password`; `IsFirstLogin` is cleared by completing onboarding.
2. **Step 1 completes onboarding (for now).** The 5-step strip is rendered for visual fidelity, but only Step 1 is functional. Its primary button ("Next Step") acts as **Finish** for this slice: on success `IsFirstLogin` clears and the swimmer lands on `/home`. Steps 2–5 are wired in later slices.
3. **Pre-fill, editable.** Fields set by the captain at account creation (Full Name, Gender, DOB, Training Club) are pre-filled and editable; identity edits persist.
4. **No National ID.** Follows the earlier locked removal; not in the schema, not shown, not sent.
5. **No Championship Club** in Step 1 (Training Club only). `swimmer_profile.RepresentChampionshipClubId` is left unchanged.
6. **Sport is a locked display** ("Swimming"); not stored.
7. **Full Name maps to `name_en` only.** `name_ar` is preserved unchanged (the screenshot shows a single "Full Name" field).
8. **Vitals write = a new dated `medical_exam` row** (append-only history), reusing the existing `MedicalExam` entity + `AddExamAsync`. A brand-new swimmer has no prior exam, so the medical/vitals inputs start blank.
9. **Self-service, JWT-scoped.** Step 1 read/write endpoints derive the swimmer from `CurrentUserId()`; they never accept a swimmer id in the path. Gated to role `swimmer`.
10. **No new tables, no DB migration.** Only C# entity methods (`AppUser.CompleteFirstLogin`, `SwimmerProfile.SetTrainingClub`) and a repository lookup are added.
11. **Minimal email-based swimmer-login wiring.** `'swimmer'` is added to the frontend role set (type, labels, landing). Login stays email-based (username login is out of scope); a swimmer must have an email to sign in.
12. **Full-screen wizard route.** `/onboarding` is a top-level route (sibling of `/login`), outside the app shell/sidebar — matching the mockup.

## 3. Data model

**No schema changes.** Reused tables and their relevant columns:

- `identity.app_user` — `name_en`, `name_ar?`, `gender_id`, `dob?`, `email` (preserved), `is_first_login`.
- `identity.swimmer_profile` — `user_id`, `uid`, `training_club_id`, `represent_championship_club_id?` (untouched).
- `athlete.medical_exam` — `swimmer_id`, `exam_date`, `internal_med_id`, `heart_assess_id`, `spine_assess_id`, `blood_type_id?`, `hemoglobin`, `height_cm`, `weight_kg`, `created_at` (see the identity-vitals spec §3.2).
- Reference: `reference.club`, `reference.gender`, `reference.blood_type`, `reference.fitness_assessment`.

### 3.1 Entity methods (new)

- **`AppUser.CompleteFirstLogin()`** — sets `IsFirstLogin = false` **without touching the password** (mirrors the flag-clearing side of `SetPassword`, but password-free). Idempotent (safe if already false).
- **`SwimmerProfile.SetTrainingClub(Guid trainingClubId)`** — sets `TrainingClubId` and bumps `UpdatedAt`. Guards against `Guid.Empty`.
- `AppUser.UpdateProfile(nameEn, nameAr, email, genderId, dob, phone)` — **reused** (already exists); email + phone are passed through unchanged from the current user.

## 4. Backend — self-service onboarding (Identity module)

All paths under `backend/src/Modules/Identity/` + `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs`.

### 4.1 Repository additions (`ISwimmerProfileRepository` + `SwimmerProfileRepository`)
- `Task<SwimmerProfile?> GetByUserIdTrackedAsync(Guid userId, CancellationToken)` — tracked `swimmer_profile` for the JWT user; `null` when the user is not a swimmer / has no profile.

(`GetByIdTrackedAsync`, `AddExamAsync`, and the existing `SaveChangesAsync` are reused.)

### 4.2 DTOs (`SwimmerDtos.cs` additions)
- `OnboardingPrefillDto(string Uid, string NameEn, string? NameAr, Guid? GenderId, DateOnly? Dob, Guid? TrainingClubId)` — ids so the wizard can preselect dropdowns.
- `CompleteIdentityVitalsRequest(string NameEn, string? NameAr, Guid GenderId, DateOnly Dob, Guid TrainingClubId, DateOnly ExamDate, Guid? BloodTypeId, decimal Hemoglobin, decimal HeightCm, decimal WeightKg, Guid InternalMedId, Guid HeartAssessId, Guid SpineAssessId)`.
- `OnboardingStepResultDto(bool MustChangePassword)` — returns `false` after a successful completion.

### 4.3 Validator
`CompleteIdentityVitalsRequestValidator` — `NameEn` not empty (max 200); `GenderId` / `TrainingClubId` / `InternalMedId` / `HeartAssessId` / `SpineAssessId` not empty; `Dob` and `ExamDate` not in the future; `Hemoglobin` / `HeightCm` / `WeightKg` > 0 and within sane bounds (reuse the numeric bounds from `CreateMedicalExamRequestValidator`).

### 4.4 Service (`SwimmerService`)
- `Task<OnboardingPrefillDto?> GetOnboardingPrefillAsync(Guid userId, CancellationToken)` — `GetByUserIdTrackedAsync(userId)`; if `null` return `null` (→ 404). Load the `app_user` (`_users.GetByIdAsync`); map name/gender/dob + `swimmer_profile.TrainingClubId` + `Uid`.
- `Task<OnboardingStepResultDto?> CompleteIdentityVitalsAsync(Guid userId, CompleteIdentityVitalsRequest req, CancellationToken)` — atomic:
  1. `GetByUserIdTrackedAsync(userId)` → profile; `null` → `null` (404).
  2. `_users.GetByIdAsync(profile.UserId)` → user; `null` → `null` (404).
  3. Validate reference ids exist, reusing the guards already used by `SwimmerService.CreateAsync` (training club, gender) and `CreateExamAsync` (the three `fitness_assessment` ids via `IFitnessAssessmentRepository.ExistsAsync`; `BloodTypeId`, if provided, via `IBloodTypeRepository.ExistsAsync`). Add any missing `ExistsAsync` mirroring the blood-type precedent. Unknown ids → surfaced as 400 (consistent with `CreateAsync` / `CreateExamAsync`).
  4. `user.UpdateProfile(req.NameEn, req.NameAr, user.Email, req.GenderId, req.Dob, user.Phone)` (email + phone preserved).
  5. `profile.SetTrainingClub(req.TrainingClubId)`.
  6. Construct `MedicalExam(profile.Id, req.ExamDate, req.InternalMedId, req.HeartAssessId, req.SpineAssessId, req.BloodTypeId, req.Hemoglobin, req.HeightCm, req.WeightKg)`; `AddExamAsync`.
  7. `user.CompleteFirstLogin()`.
  8. One `SaveChangesAsync` (single `IdentityDbContext` → one transaction). Return `new OnboardingStepResultDto(false)`.

### 4.5 Controller (`SwimmersController`)
- `GET("me/onboarding/identity-vitals")` `[Authorize(Roles="swimmer")]` → `GetOnboardingPrefillAsync(CurrentUserId())`; `200` `ApiResponse<OnboardingPrefillDto>` or `404`.
- `POST("me/onboarding/identity-vitals")` `[Authorize(Roles="swimmer")]` → `CompleteIdentityVitalsAsync(CurrentUserId(), req)`; `200` `ApiResponse<OnboardingStepResultDto>` (`SwimmerMessages.Success.OnboardingCompleted`) or `404`; unknown reference ids → `400`.

Route note: these `me/onboarding/...` routes must be declared so they don't collide with the existing `GET {id:guid}`; the literal `me` segment takes precedence over the `:guid` constraint.

## 5. Frontend (Angular)

### 5.1 New feature slice `features/swimmer-onboarding/`
Mirror the layering of existing slices (`swimmer-profile`, `health-readings`).

- **domain/model/onboarding.ts** —
  - `OnboardingPrefill { uid; nameEn; nameAr; genderId; dob; trainingClubId }` (ids nullable).
  - `IdentityVitalsSubmission { nameEn; nameAr?; genderId; dob; trainingClubId; examDate; bloodTypeId?; hemoglobin; heightCm; weightKg; internalMedId; heartAssessId; spineAssessId }`.
- **domain/repositories/swimmer-onboarding.repository.ts** — interface + DI token: `getPrefill()`, `completeIdentityVitals(body)`.
- **domain/usecases/** — `get-onboarding-prefill`, `complete-identity-vitals` (thin `run(...)` returning the `Result` envelope).
- **data/dto/** — `onboarding-prefill.dto.ts` (`…DtoRs`), `complete-identity-vitals.dto.ts` (`…DtoRq` / `…DtoRs`), each with a validation guard like existing DTOs.
- **data/repositories/swimmer-onboarding.repository.impl.ts** — `GET`/`POST /api/swimmers/me/onboarding/identity-vitals` via `HttpClientService`.
- **data/swimmer-onboarding.providers.ts**; **index.ts** exports (page + viewmodel + providers).

### 5.2 Page `presentation/pages/onboarding/`
- **onboarding-wizard.page.ts / .html** — full-screen, centered, no sidebar. Header ("Join Oasis Academy" / localized), a **5-step strip** (Identity & Vitals selected; Guardian & Medical / Physiological / InBody / Done disabled, `aria-disabled`, no navigation), and the Step-1 form card.
  - *Step-1 form:* `TextFieldComponent` (Full Name), a locked/read-only Sport field ("Swimming"), `SelectFieldComponent` (Training Club, Gender, Blood Type[optional], Internal Medicine, Heart, Spine), `DateFieldComponent` (Date of Birth, Last Examination Date — defaults to today), number inputs (Hemoglobin, Height, Weight). Identity fields pre-filled from the prefill read. Primary button "Next Step" (= Finish); disabled while submitting or invalid.
- **onboarding.viewmodel.ts** — signal-based, like `swimmer-profile.viewmodel`:
  - state: `prefill`, `loading`, `error`, `submitting`; draft field signals; lookup signals `clubs`, `genders`, `bloodTypes`, `fitnessAssessments`.
  - `load()` — fetch prefill + reference lookups (reuse `LoadClubs/LoadGenders/LoadBloodTypes/LoadFitnessAssessments` use-cases), seed the identity drafts.
  - `canSubmit` computed (required fields present, numbers > 0).
  - `submit()` — `CompleteIdentityVitalsUseCase`; on success call `auth.markOnboardingComplete()` then navigate `/home`; toast success/failure via `NotificationService` + i18n; keep the form on failure.

### 5.3 Routing + guard
- **app.routes.ts:** add a **top-level** route (sibling of `login`):
  `{ path: 'onboarding', canActivate: [authGuard, onboardingGuard], loadComponent: () => import('@features/swimmer-onboarding').then(m => m.OnboardingWizardPage), providers: [OnboardingViewModel] }`.
- **auth.guard.ts:**
  - New `onboardingGuard` — authenticated **swimmer** with `mustChangePassword() === true`; otherwise redirect to `/home` (already onboarded or wrong role) or `/login` (anonymous).
  - `firstLoginGuard` becomes **role-aware**: a `mustChangePassword` **swimmer** is pinned to `/onboarding`; any other role stays pinned to `/change-password` (unchanged behavior).

### 5.4 Swimmer-login wiring (minimal)
- **`core/domain/roles/user-role.ts`:** `export type UserRole = 'head_coach' | 'captain' | 'swimmer';`
- **`core/domain/roles/role-labels.ts`:** add `swimmer: 'roles.swimmer'` (+ the existing role-labels spec expectation updated).
- **`login.viewmodel.ts`:** add `swimmer: '/home'` to `LANDING_ROUTE_BY_ROLE`; make post-login navigation role-aware — on `mustChangePassword`, route `swimmer → /onboarding`, else `/change-password`.
- **`auth-session.store.ts`:** add `markOnboardingComplete()` — patches `_session` to `{ ...current, mustChangePassword: false }` so guards pass without a re-login. (Optional: also re-run `LoadCurrentUserUseCase`.)

### 5.5 i18n
Add an `onboarding.*` block to `en.json` + `ar.json`: page title/subtitle, the 5 step labels, section/field labels (Full Name, Sport, Training Club, Gender, Date of Birth, Last Examination Date, Internal Medicine Result, Heart Assessment, Spine Assessment, Blood Type, Hemoglobin, Height, Weight), the "Next Step"/Finish button, and toasts (completed, load failed, save failed). Add `roles.swimmer`. Dropdown option labels come from reference data (`nameEn`/`nameAr`).

## 6. Data flow

**Login (swimmer, first time):** email + password + role `swimmer` → `POST /api/auth/login` → `SessionDto { MustChangePassword: true, Role: 'swimmer' }` → login viewmodel routes to `/onboarding`.

**Enter wizard:** `onboardingGuard` allows (swimmer + first-login) → viewmodel `load()` → `GET …/me/onboarding/identity-vitals` (prefill) + reference lookups → Step-1 form pre-filled.

**Complete Step 1:** submit → `CompleteIdentityVitalsUseCase` (`POST …/me/onboarding/identity-vitals`) → server updates `app_user` + `swimmer_profile`, inserts `medical_exam`, clears `IsFirstLogin`, one transaction → `200 { mustChangePassword: false }` → store `markOnboardingComplete()` → navigate `/home` (dashboard).

## 7. Authorization

- `GET`/`POST /api/swimmers/me/onboarding/identity-vitals` → `[Authorize(Roles="swimmer")]`; the swimmer is resolved from `CurrentUserId()`, so a swimmer can only read/write their own record. No path id is accepted.
- `onboardingGuard` blocks non-swimmers and already-onboarded swimmers from `/onboarding`.
- Coach/captain first-login (`/change-password`) is unchanged.

## 8. Error handling / edge cases

- **User is not a swimmer / has no profile** → API `404`; the guard would already have redirected, so this is defensive.
- **Already onboarded** (`IsFirstLogin` false) → `onboardingGuard` redirects to `/home`; the completion endpoint is idempotent on the flag.
- **Unknown reference id** (club/gender/blood type/assessment) → `400` via the validation path; the form stays intact with an error toast.
- **Nullable blood type** — optional dropdown; `null` round-trips.
- **Emailless swimmer** — cannot sign in (email-only login); out of scope this slice (noted in §10).
- **Partial failure** — the write is a single `SaveChanges`, so identity/exam/flag either all persist or none do.
- `loading` / `error` / `submitting` signals gate the UI; the submit button is disabled in flight.

## 9. Testing

**Backend**
- `Identity.UnitTests`: `AppUser.CompleteFirstLogin` (clears flag, leaves password/other fields intact, idempotent); `SwimmerProfile.SetTrainingClub` (sets id, bumps `UpdatedAt`, rejects empty); `SwimmerProfileRepository.GetByUserIdTrackedAsync` (found / not-a-swimmer null) on InMemory; `SwimmerService.GetOnboardingPrefillAsync` (maps ids; null when no profile); `SwimmerService.CompleteIdentityVitalsAsync` (persists identity + exam + clears flag in one save; unknown ref id rejected; not-found → null); `CompleteIdentityVitalsRequestValidator`.
- `Api.UnitTests`: `SwimmersController` `GET me/onboarding/identity-vitals` 200/404 and `POST` 200/404/400; role-gating (`swimmer` only). Extend `SwimmersControllerTests` with `CurrentUserId` HttpContext plumbing (mirror `InBodyReadingsControllerTests`).
- `ArchitectureTests` stay green.

**Frontend**
- `get-onboarding-prefill.use-case.spec.ts`, `complete-identity-vitals.use-case.spec.ts`, `swimmer-onboarding.repository.impl.spec.ts`.
- `onboarding.viewmodel.spec.ts` — `load()` seeds identity drafts + lookups; `canSubmit` gating; `submit()` success (store flag flipped + navigate `/home`) and failure (error toast, form kept).
- `auth.guard.spec.ts` — `onboardingGuard` (allow swimmer+first-login; redirect otherwise) and role-aware `firstLoginGuard` (swimmer → `/onboarding`, others → `/change-password`).
- `login.viewmodel.spec.ts` — role-aware first-login navigation (swimmer → `/onboarding`).
- `auth-session.store.spec.ts` — `markOnboardingComplete()` flips `mustChangePassword`.
- `role-labels.spec.ts` — updated to include `swimmer`.

## 10. Out of scope (deferred)

- **Steps 2–5** (Guardian & Medical, Physiological, InBody, Done) — rendered disabled only.
- **Username login** for emailless swimmers (login stays email-based).
- **Championship Club** and **National ID** in Step 1.
- Any **coach-facing** change (existing `/api/swimmers/{id}/…` endpoints, roster, profile).
- Editing/deleting past exams or showing exam history in the wizard.
- Persisting wizard progress across sessions (with only Step 1, completion is atomic; no partial state to resume).
