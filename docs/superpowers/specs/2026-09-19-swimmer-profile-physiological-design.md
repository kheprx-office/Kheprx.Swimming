# Swimmer Profile — Physiological tab — Design

- **Date:** 2026-09-19
- **Status:** Approved (design); implementation pending
- **Branch:** `feat/swimmer-profile-guardian` (stacked on the uncommitted Guardian work; no commits until instructed)
- **Sibling / prior art:** `2026-09-19-swimmer-profile-guardian-design.md`, `2026-09-19-swimmer-profile-identity-vitals-design.md`, `2026-09-19-swimmer-exam-history-edit-design.md`

## Context & goal

The Swimmer Profile page (`swimmers/:id`) currently ships two tabs: **Identity & Vitals** (tab 1, on main) and **Guardian** (tab 2, implemented but uncommitted on this branch). The tab strip renders all nine tabs; tabs 3–9 are disabled/greyed.

This feature builds the **third tab, Physiological** — a "Body Measurements" card showing a swimmer's latest set of dated physiological measurements (arms, legs, torso, bust, waist — all in cm), with a coach-only edit that records a new dated measurement. Reference: design mockup (Desktop `mcp/2.png`) and the React design prototype `Desktop/4dba8937-…/src/pages/SwimmerProfile.tsx` (physiological block ~L788–858).

Body measurements are **not** captured at registration, so every existing swimmer starts with **zero** measurement rows. This tab is therefore the first place measurement data is created — it is create-or-edit.

The mockup's page heading ("My Profile") and its National-ID chip are **design-reference-only**: this stays the coach-reached `swimmers/:id` page, and National ID remains removed from the UI (per the Identity & Vitals decision).

## Scope

**In scope**
- New `athlete.body_measurement` table, in the Identity module.
- Read + write endpoints for a swimmer's body measurements (read = latest snapshot; write = create a new dated measurement).
- Frontend: make the tab strip switch to a third built tab; build the Body Measurements section (view + edit) in the `swimmer-profile` slice.

**Out of scope**
- The other six tabs (inbody, records, health monitoring, attendance, championships, feedback) — they stay disabled.
- Capturing body measurements during swimmer registration.
- A history/date-dropdown UI, or edit-in-place / delete of past measurement rows (history is preserved in the DB but not surfaced this iteration).
- Re-adding National ID to the UI, or a new self-service "My Profile" route.
- Any reference/lookup table (unlike Guardian, this feature adds no `reference.*` table).

## Locked decisions

1. **Latest snapshot shown, append-only writes** — the card shows the single most-recent measurement (by `measured_at`). Coach Edit **POSTs a new dated `body_measurement`**; older rows are kept in the DB but not displayed. Mirrors how Vitals originally worked (`POST` a new dated `medical_exam`, show latest).
2. **Server-set date** — `measured_at` is set to today (server clock) on create; there is no date field in the form (the mockup shows none).
3. **All seven fields required** — a save requires all seven measurements, each a positive number that fits `numeric(5,1)`. Save is disabled client-side until valid, and re-validated server-side.
4. **Switch built tabs only** — the tab strip becomes clickable for `identityVitals`, `guardian`, and `physiological`; the other six tabs keep today's disabled/greyed styling.
5. **Backend shape** — a dedicated latest-read + create pair. No per-measurement CRUD; measurements are not folded into the profile GET. Persistence folds into the existing `ISwimmerProfileRepository`/`SwimmerService`/`SwimmersController` (mirroring how `medical_exam` and `guardian` are handled), NOT a standalone repository.
6. **No commits** — all work stays staged/uncommitted on `feat/swimmer-profile-guardian` until the user says otherwise.

## Data model & persistence

One new table in the **Identity module** (single `IdentityDbContext`, real FK), using the diagram's Postgres schema `athlete`.

### `athlete.body_measurement`
| column | type | notes |
|---|---|---|
| `id` | uuid | PK |
| `swimmer_id` | uuid | FK → `identity.swimmer_profile.id`, NOT NULL |
| `measured_at` | date | NOT NULL; server-set to today on create |
| `right_arm_cm` | numeric(5,1) | NOT NULL |
| `left_arm_cm` | numeric(5,1) | NOT NULL |
| `right_leg_cm` | numeric(5,1) | NOT NULL |
| `left_leg_cm` | numeric(5,1) | NOT NULL |
| `torso_cm` | numeric(5,1) | NOT NULL |
| `bust_diameter_cm` | numeric(5,1) | NOT NULL |
| `waist_diameter_cm` | numeric(5,1) | NOT NULL |

- **Index on `(swimmer_id, measured_at)`** — supports the latest-per-swimmer lookup (order by `measured_at` desc, then `id` desc as a stable tiebreaker for same-day rows).
- FK delete behaviour mirrors the existing `medical_exam` config: the `swimmer_id` FK uses `OnDelete(Cascade)` (as `MedicalExamConfiguration` does for its swimmer FK).
- Column values map to CLR `decimal`; configure each with `HasPrecision(5, 1)`.

### Migration
- One EF migration `AddBodyMeasurement` on the Identity DbContext (mirrors `AddGuardianAndGuardianRelation` / `AddFitnessAssessmentAndMedicalExam`).
- **Created but NOT auto-applied.** Run `dotnet ef database update` on the Identity module when ready (same handling as the prior tabs).

## Backend design (Identity module + API)

### Domain
- `Domain/Entities/BodyMeasurement.cs` — entity (`Id`, `SwimmerId`, `MeasuredAt`, `RightArmCm`, `LeftArmCm`, `RightLegCm`, `LeftLegCm`, `TorsoCm`, `BustDiameterCm`, `WaistDiameterCm`). **Append-only**: a create factory/ctor only — no update or delete method (mirrors `MedicalExam` creation before the exam-history follow-on added mutation).
- `Domain/ReadModels/BodyMeasurementRow.cs` — read projection (`Id`, `MeasuredAt`, the seven decimals), mirroring `MedicalExamRow.cs` / `GuardianRow.cs`.
- **Persistence folds into the existing `ISwimmerProfileRepository`** (mirroring how `medical_exam` and `guardian` are handled). New methods:
  - `GetLatestBodyMeasurementAsync(swimmerId, ct)` → `BodyMeasurementRow?` (latest by `measured_at` desc, `id` desc; null when the swimmer has no rows).
  - `AddBodyMeasurementAsync(measurement, ct)`.
  - Reuse the existing swimmer-exists check used by the guardian/exam paths to distinguish 404 from an empty result.

### Infrastructure
- `Configurations/BodyMeasurementConfiguration.cs` → `ToTable("body_measurement","athlete")`, FK to `swimmer_profile`, `HasPrecision(5,1)` on each of the seven value columns, all `IsRequired()`, `measured_at` required, index on `(SwimmerId, MeasuredAt)`.
- Register the `DbSet<BodyMeasurement>` on `IdentityDbContext`.
- Repository method implementations in `Infrastructure/Repositories/SwimmerProfileRepository.cs`.
- No seeder change (no lookup table).

### Application
- **DTOs** — new `BodyMeasurementDtos.cs`:
  - `BodyMeasurementDto(Guid Id, DateOnly MeasuredAt, decimal RightArmCm, decimal LeftArmCm, decimal RightLegCm, decimal LeftLegCm, decimal TorsoCm, decimal BustDiameterCm, decimal WaistDiameterCm)`.
  - Read wrapper: `SwimmerBodyMeasurementDto(BodyMeasurementDto? Latest)` — the **outer object is null only when the swimmer does not exist** (→ 404); `Latest` is null when the swimmer exists but has no measurement rows yet (→ 200 with empty).
  - Write: `CreateBodyMeasurementRequest(decimal RightArmCm, decimal LeftArmCm, decimal RightLegCm, decimal LeftLegCm, decimal TorsoCm, decimal BustDiameterCm, decimal WaistDiameterCm)` — no date (server sets today).
- **Validation** — FluentValidation validator `BodyMeasurementRequestValidators.cs` (mirror `GuardianRequestValidators.cs`): each of the seven fields `GreaterThan(0)` and `LessThanOrEqualTo(999.9m)` (fits `numeric(5,1)`, guards absurd input). Add measurement-specific messages to `SwimmerMessages` or reuse a generic invalid-value message.
- **Service** — extend `ISwimmerService` (measurements live on the same service/controller/repository as exams and guardians, for consistency):
  - `GetBodyMeasurementAsync(Guid id, ct)` → `SwimmerBodyMeasurementDto?`; returns `null` when no swimmer with that id exists (→ 404). Returns a DTO with `null` `Latest` when the swimmer exists but has no rows.
  - `AddBodyMeasurementAsync(Guid id, CreateBodyMeasurementRequest req, ct)` → `bool` (false ⇒ swimmer not found ⇒ 404). Constructs a `BodyMeasurement` with `MeasuredAt` = today (server) and inserts it.
- **Messages** — add `BodyMeasurementRetrieved` / `BodyMeasurementSaved` to `SwimmerMessages`; reuse `ProfileNotFound`.

### Controller (`SwimmersController`)
Mirrors the existing exam/guardian endpoints exactly (`ApiResponse<T>`, `AppLanguage.Current`, `StatusCode(...)`).

| Method | Route | Auth | Responses |
|---|---|---|---|
| `GET` | `api/swimmers/{id:guid}/body-measurements/latest` | `[Authorize]` | 200 `SwimmerBodyMeasurementDto` / 404 |
| `POST` | `api/swimmers/{id:guid}/body-measurements` | `[Authorize(Roles="head_coach,captain")]` | 200 / 404 / 400 (validation) |

## Frontend design (`features/swimmer-profile`)

### Tab switching
- Extend the existing `activeTab` handling (added for `identityVitals` + `guardian`) to include `physiological`.
- In `swimmer-profile.page.ts`, the three built tabs (`identityVitals`, `guardian`, `physiological`) render as clickable buttons that set `activeTab`; the other six keep today's disabled span styling (`opacity-50`, `aria-disabled`).
- The content area (`swimmer-profile.page.html`) renders the Body Measurements section when `activeTab==='physiological'`.

### Physiological domain (isolated in the slice)
- `domain/model/body-measurement.ts` — `BodyMeasurement { id; measuredAt; rightArmCm; leftArmCm; rightLegCm; leftLegCm; torsoCm; bustDiameterCm; waistDiameterCm }`.
- `domain/usecases/get-latest-body-measurement.use-case.ts`, `create-body-measurement.use-case.ts`.
- `domain/repositories/…` interface method(s); `data/repositories/swimmer-profile.repository.impl.ts` gains the HTTP calls; `data/dto/` body-measurement DTO + mapper (incl. null latest).

### ViewModel (`swimmer-profile.viewmodel.ts`)
Mirror the Guardian/Vitals edit pattern:
- State: `bodyMeasurement` signal (latest or null); seven per-field edit signals (`bmRightArm`, `bmLeftArm`, `bmRightLeg`, `bmLeftLeg`, `bmTorso`, `bmBustDiameter`, `bmWaistDiameter`); `editingBodyMeasurement`, `savingBodyMeasurement`, `loadingBodyMeasurement`, `bodyMeasurementLoaded` flags.
- `loadBodyMeasurement()` — **lazy**, called the first time the Physiological tab is opened (guard on `bodyMeasurementLoaded`).
- `startEditBodyMeasurement()` — seed the seven signals from the loaded latest (empty strings when null).
- `canSaveBodyMeasurement` computed — all seven fields parse to numbers `> 0` and `≤ 999.9`.
- `saveBodyMeasurement()` — calls the create use case, success toast, reload latest, exit edit; failure toast. Reuses `NotificationService` + `TranslateService`.
- `canEdit` (existing) gates the Edit button to `head_coach`/`captain`.

### Physiological section UI
- New markup in `swimmer-profile.page.html` (or a small partial), matching the mockup: a "Body Measurements" card, header with a coach-only Edit button; a bordered/divided list of the seven rows (Ruler icon + label + value with a `cm` unit suffix), or a "no record" placeholder when latest is null.
- Edit mode: a seven-field numeric form (`app-text-field`, numeric input), Save disabled until `canSaveBodyMeasurement`, Cancel resets. Reuses existing card/border/list styling from the sibling sections.

### i18n
Add `swimmerProfile.physiological.*` keys to the EN and AR bundles: section title (`bodyMeasurements`), the seven labels (`rightArm`, `leftArm`, `rightLeg`, `leftLeg`, `torsoLength`, `bustDiameter`, `waistDiameter`), unit (`cm`), `noRecord`, and toasts (`bodyMeasurementSaved`, reuse `saveFailed`). The `swimmerProfile.tabs.physiological` label already exists.

## Validation, auth & errors

- **Values**: each of the seven measurements is required, `> 0`, and `≤ 999.9` (one decimal place, fits `numeric(5,1)`), client + server.
- **All seven required**: client disables Save until valid; server returns 400 (`ApiResponse.Failure`) on invalid input.
- **Auth**: read `[Authorize]` (any authenticated user); write `head_coach`/`captain`.
- **Not found**: unknown swimmer id ⇒ 404 on both endpoints.
- **No cross-swimmer leakage**: measurements are always queried/written scoped by `swimmer_id` from the route.

## Testing

**Backend**
- Service: swimmer-not-found ⇒ null/false; get with no rows ⇒ `Latest` null; get with rows ⇒ correct latest mapping and **latest-by-`measured_at` ordering** (most-recent wins, `id` desc tiebreak for same-day rows); add inserts a new dated row with `measured_at` = today; validation rejects non-positive / out-of-range values.
- API: role auth (200 for authed GET; 403 for non-coach POST; 401 anonymous); 200/404/400 response shapes. Mirror the existing Identity + API suites.

**Frontend (jest)**
- Mapper (DTO ↔ model, null latest), repository impl (URLs/payloads), get-latest + create use cases, viewmodel (lazy load once, edit seeds fields from latest, `canSave` range validation incl. positive + ≤ 999.9, save success/failure, tab switch renders the right section).

**Gates**
- Backend + frontend builds green; existing suites still pass; whole-branch review before any merge.

## Anchor files (patterns to mirror)

- Controller/endpoints: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs` (guardian + medical-exam endpoints)
- DTOs: `…/Identity.Application/DTOs/GuardianDtos.cs`, `…/DTOs/SwimmerDtos.cs`
- Validation: `…/Identity.Application/Validators/GuardianRequestValidators.cs`
- Entity/config/read-model to mirror: `…/Identity.Domain/Entities/Guardian.cs` + `MedicalExam.cs`, `…/Infrastructure/Configurations/GuardianConfiguration.cs`, `…/Domain/ReadModels/GuardianRow.cs` + `MedicalExamRow.cs`
- Repository: `…/Infrastructure/Repositories/SwimmerProfileRepository.cs` (guardian + exam methods), interface `…/Domain/Repositories/ISwimmerProfileRepository.cs`
- DbContext: `…/Infrastructure/Data/IdentityDbContext.cs`
- Migration precedent: `…/Infrastructure/Migrations/20260919111701_AddGuardianAndGuardianRelation.cs`
- Service/messages: `…/Identity.Application/Services/SwimmerService.cs` + `Services/Interfaces/ISwimmerService.cs`, `…/Resources/SwimmerMessages.cs`
- FE page/viewmodel: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.{ts,html}`, `…/swimmer-profile.viewmodel.ts` (guardian tab wiring)
- DB diagram: `docs/references/swimming-database-diagram.html` (`athlete.body_measurement` ~L233)

## Follow-ons (not now)

- Surface the measurement history (date dropdown + edit/delete of past rows), mirroring the exam-history follow-on.
- Capture body measurements at registration.
- The remaining disabled profile tabs (inbody, records, health monitoring, attendance, championships, feedback).
