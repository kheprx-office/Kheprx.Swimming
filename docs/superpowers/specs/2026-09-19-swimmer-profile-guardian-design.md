# Swimmer Profile — Guardian tab — Design

- **Date:** 2026-09-19
- **Status:** Approved (design); implementation pending
- **Branch:** `feat/swimmer-profile-guardian` (base `main`)
- **Sibling / prior art:** `2026-09-19-swimmer-profile-identity-vitals-design.md`, `2026-09-19-swimmer-exam-history-edit-design.md`

## Context & goal

The Swimmer Profile page (`swimmers/:id`) currently ships only its first tab, **Identity & Vitals**. The tab strip renders all nine tabs but they are decorative (non-clickable `<span>`s) and only the first has content.

This feature builds the **second tab, Guardian** — a "Guardian Details" card showing a swimmer's **father** and **mother** (full name, national ID, phone), with a coach-only edit form. Reference: design mockup (Desktop `mcp/2.png`) and the React design prototype `Desktop/4dba8937-…/src/pages/SwimmerProfile.tsx` (guardian block ~L742–785).

Guardians are **not** captured at registration, so every existing swimmer starts with **zero** guardian rows. This tab is therefore the first place guardian data is created — it is create-or-edit, not read/edit of pre-existing data.

## Scope

**In scope**
- New `athlete.guardian` table + `reference.guardian_relation` lookup (seeded `father`/`mother`), in the Identity module.
- Read + write endpoints for a swimmer's guardians.
- Frontend: make the tab strip switch between the two built tabs; build the Guardian section (view + edit) in the `swimmer-profile` slice.

**Out of scope**
- The other seven tabs (physiological, inbody, records, health monitoring, attendance, championships, feedback) — they stay disabled.
- Capturing guardians during swimmer registration.
- Generic multi-guardian lists, delete of a guardian, or relation types beyond father/mother.
- A `guardian-relations` reference endpoint (the UI is fixed father/mother; the server resolves relation IDs internally).

## Locked decisions

1. **Two fixed slots** — the UI shows exactly one **Father** card and one **Mother** card (matches the mockup). Storage is still generic rows in `athlete.guardian` keyed by relation.
2. **Both slots required to save** — a save requires father **and** mother each fully filled (name + national ID + phone). Save is disabled client-side until all six fields are valid, and re-validated server-side.
3. **Switch built tabs only** — the tab strip becomes clickable for `identityVitals` and `guardian`; the other seven tabs keep today's disabled/greyed styling.
4. **Backend shape = Approach A** — a dedicated guardian read/write pair with a single atomic upsert of both rows. No per-guardian CRUD; guardians are not folded into the profile GET.

## Data model & persistence

Two new tables in the **Identity module** (single `IdentityDbContext`, real FKs), using the diagram's Postgres schemas.

### `athlete.guardian`
| column | type | notes |
|---|---|---|
| `id` | uuid | PK |
| `swimmer_id` | uuid | FK → `identity.swimmer_profile.id`, NOT NULL |
| `relation_id` | uuid | FK → `reference.guardian_relation.id`, NOT NULL |
| `name` | varchar | NOT NULL |
| `national_id` | varchar(14) | NOT NULL |
| `phone` | varchar | NOT NULL |

- **Unique index on `(swimmer_id, relation_id)`** — at most one father and one mother per swimmer. Enforces the two-slot model and makes the upsert deterministic.
- FK delete behaviour mirrors the existing `medical_exam` config (restrict; guardians are not cascade-created).

### `reference.guardian_relation`
| column | type | notes |
|---|---|---|
| `id` | uuid | PK |
| `code` | varchar | `father` \| `mother`; unique index (mirror `fitness_assessment`) |
| `name_en` | varchar | NOT NULL |
| `name_ar` | varchar | nullable |

- **Seeded** idempotently at startup via `IdentitySeeder`, adding an `EnsureGuardianRelations` method beside `EnsureFitnessAssessments` (`IdentitySeeder.cs:118`). Rows: `("father","Father","الأب")`, `("mother","Mother","الأم")`.

### Migration
- One EF migration `AddGuardianAndGuardianRelation` on the Identity DbContext (mirrors `20260918224209_AddFitnessAssessmentAndMedicalExam`).
- **Created but NOT auto-applied.** Run `dotnet ef database update` on the Identity module when ready (same handling as the last tab).

## Backend design (Identity module + API)

### Domain
- `Domain/Entities/Guardian.cs` — entity (`Id`, `SwimmerId`, `RelationId`, `Name`, `NationalId`, `Phone`) with an update method (mutate name/nationalId/phone), mirroring existing entity style.
- `Domain/Entities/GuardianRelation.cs` — lookup entity mirroring `FitnessAssessment.cs` (`Code`, `NameEn`, `NameAr`).
- `Domain/ReadModels/GuardianRow.cs` — read projection (`RelationCode`, `Name`, `NationalId`, `Phone`), mirroring `MedicalExamRow.cs`.
- **Guardian persistence folds into the existing `ISwimmerProfileRepository`** (mirroring how `medical_exam` is handled — exams live on this same repo, not a standalone one). New methods: `ListGuardiansAsync(swimmerId)` → `GuardianRow`s, `GetGuardianRelationIdByCodeAsync(code)`, `GetGuardianTrackedAsync(swimmerId, relationId)`, `AddGuardianAsync(guardian)`. Implemented in `Infrastructure/Repositories/SwimmerProfileRepository.cs`.

### Infrastructure
- `Configurations/GuardianConfiguration.cs` → `ToTable("guardian","athlete")`, FKs, `national_id` `HasMaxLength(14).IsRequired()`, unique index on `(SwimmerId, RelationId)`.
- `Configurations/GuardianRelationConfiguration.cs` → `ToTable("guardian_relation","reference")`, unique `Code` index (mirror `FitnessAssessmentConfiguration.cs`).
- Register both `DbSet`s on `IdentityDbContext`; add `EnsureGuardianRelations` to `IdentitySeeder`.
- Repository implementation(s) in `Infrastructure/Repositories/`.

### Application
- **DTOs** — new `GuardianDtos.cs`:
  - `GuardianDto(Guid Id, string RelationCode, string Name, string NationalId, string Phone)`
  - Read: `SwimmerGuardiansDto(GuardianDto? Father, GuardianDto? Mother)` — either may be null.
  - Write: `UpsertGuardiansRequest(GuardianInputDto Father, GuardianInputDto Mother)` where `GuardianInputDto(string Name, string NationalId, string Phone)`.
- **Validation** — FluentValidation validator (mirror `UserRequestValidators.cs:113`): each of Father/Mother requires `Name` non-empty, `NationalId` `Matches(@"^\d{14}$")`, `Phone` non-empty. Reuse `CoachMessages.Errors.NationalIdInvalid` or add guardian-specific messages.
- **Service** — extend `ISwimmerService` (guardians live on the same service/controller/repository as exams, for consistency):
  - `GetGuardiansAsync(Guid id, ct)` → `SwimmerGuardiansDto?`; returns `null` when no swimmer with that id exists (→ 404). Returns a DTO with `null` Father/Mother when the swimmer exists but has no rows.
  - `UpsertGuardiansAsync(Guid id, UpsertGuardiansRequest req, ct)` → `bool` (false ⇒ swimmer not found ⇒ 404). Resolves the father/mother `relation_id`s from the lookup, then in one transaction upserts both rows (insert when absent, update the row matched by `swimmer_id`+`relation_id`).
- **Messages** — add `GuardiansRetrieved` / `GuardiansSaved` to `SwimmerMessages`; reuse `ProfileNotFound`.

### Controller (`SwimmersController`)
Mirrors the existing exam endpoints exactly (`ApiResponse<T>`, `AppLanguage.Current`, `StatusCode(...)`).

| Method | Route | Auth | Responses |
|---|---|---|---|
| `GET` | `api/swimmers/{id:guid}/guardians` | `[Authorize]` | 200 `SwimmerGuardiansDto` / 404 |
| `PUT` | `api/swimmers/{id:guid}/guardians` | `[Authorize(Roles="head_coach,captain")]` | 200 / 404 / 400 (validation) |

## Frontend design (`features/swimmer-profile`)

### Tab switching
- Add an `activeTab` signal (default `'identityVitals'`) — on the page or viewmodel.
- In `swimmer-profile.page.ts`, the two built tabs (`identityVitals`, `guardian`) render as clickable buttons that set `activeTab`; the other seven keep today's disabled span styling (`opacity-50`, `aria-disabled`).
- The content area (`swimmer-profile.page.html`) renders the existing Identity + Vitals sections when `activeTab==='identityVitals'`, and the new Guardian section when `activeTab==='guardian'`.

### Guardian domain (isolated in the slice)
- `domain/model/swimmer-guardians.ts` — `Guardian { id; relationCode; name; nationalId; phone }`, `SwimmerGuardians { father: Guardian|null; mother: Guardian|null }`.
- `domain/usecases/get-swimmer-guardians.use-case.ts`, `upsert-swimmer-guardians.use-case.ts`.
- `domain/repositories/…` interface method(s); `data/repositories/swimmer-profile.repository.impl.ts` gains the HTTP calls; `data/dto/` guardian DTO + mapper.

### ViewModel (`swimmer-profile.viewmodel.ts`)
Mirror the Identity/Vitals edit pattern:
- State: `guardians` signal; per-field edit signals (`gFatherName`, `gFatherNationalId`, `gFatherPhone`, `gMotherName`, `gMotherNationalId`, `gMotherPhone`); `editingGuardians`, `savingGuardians`, `loadingGuardians`, `guardiansLoaded` flags.
- `loadGuardians()` — **lazy**, called the first time the Guardian tab is opened (guard on `guardiansLoaded`).
- `startEditGuardians()` — seed the six signals from the loaded data (empty strings when a slot is null).
- `canSaveGuardians` computed — all six fields non-empty and both national IDs match `^\d{14}$`.
- `saveGuardians()` — calls the upsert use case, success toast, reload guardians, exit edit; failure toast. Reuses `NotificationService` + `TranslateService`.
- `canEdit` (existing) gates the Edit button to `head_coach`/`captain`.

### Guardian section UI
- New markup in `swimmer-profile.page.html` (or a small partial), matching the mockup: a "Guardian Details" card, header with a coach-only Edit button; two columns (**Father Info** / **Mother Info**), each showing Full Name / National ID / Phone, or a "no record" placeholder when the slot is null.
- Edit mode: a six-field form (`app-text-field`), Save disabled until `canSaveGuardians`, Cancel resets. Reuses existing card/border/`dl` styling from the Identity section.

### i18n
Add `swimmerProfile.guardian.*` keys to the EN and AR bundles: section title (`guardianDetails`), `fatherInfo`, `motherInfo`, `fullName`, `nationalId`, `phone`, `noRecord`, and toasts (`guardiansSaved`, reuse `saveFailed`). The `swimmerProfile.tabs.guardian` label already exists.

## Validation, auth & errors

- **National ID**: exactly 14 digits (`^\d{14}$`), client + server. **Phone**: non-empty (loose, matching existing handling). **Name**: non-empty.
- **Both slots required**: client disables Save until valid; server returns 400 (`ApiResponse.Failure`) on invalid input.
- **Auth**: read `[Authorize]` (any authenticated user); write `head_coach`/`captain`.
- **Not found**: unknown swimmer id ⇒ 404 on both endpoints.
- **No cross-swimmer leakage**: guardians are always queried/written scoped by `swimmer_id` from the route.

## Testing

**Backend**
- Service: swimmer-not-found ⇒ null/false; get with no rows ⇒ both slots null; get with rows ⇒ correct father/mother mapping; upsert inserts both when absent; upsert updates existing rows in place (no duplicate); relation-code → relation-id resolution; unique `(swimmer_id, relation_id)` respected.
- API: role auth (200 for authed GET; 403 for non-coach PUT; 401 anonymous); 200/404/400 response shapes. Mirror the existing Identity + API suites.

**Frontend (jest)**
- Mapper (DTO ↔ model, null slots), repository impl (URLs/payloads), get + upsert use cases, viewmodel (lazy load once, edit seeds fields, canSave validation incl. 14-digit rule, save success/failure, tab switch renders the right section).

**Gates**
- Backend + frontend builds green; existing suites still pass; whole-branch review before merge.

## Anchor files (patterns to mirror)

- Controller/endpoints: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs`
- DTOs: `…/Identity.Application/DTOs/SwimmerDtos.cs`
- Validation: `…/Identity.Application/Validators/UserRequestValidators.cs` (national_id `^\d{14}$`)
- Lookup entity/config/seed: `…/Identity.Domain/Entities/FitnessAssessment.cs`, `…/Infrastructure/Configurations/FitnessAssessmentConfiguration.cs`, `…/Infrastructure/Data/IdentitySeeder.cs` (`EnsureFitnessAssessments`), migration `20260918224209_AddFitnessAssessmentAndMedicalExam`
- Related entity/config: `…/Identity.Domain/Entities/MedicalExam.cs`, `…/Infrastructure/Configurations/MedicalExamConfiguration.cs`, read model `…/Domain/ReadModels/MedicalExamRow.cs`
- DbContext: `…/Infrastructure/Data/IdentityDbContext.cs`
- FE page/viewmodel: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.{ts,html}`, `…/swimmer-profile.viewmodel.ts`
- DB diagram: `docs/references/swimming-database-diagram.html` (`athlete.guardian` ~L214, `reference.guardian_relation` ~L174)

## Follow-ons (not now)

- Capture guardians at registration.
- Remaining seven profile tabs.
- Optional `guardian-relations` reference endpoint if a generic relation picker is ever needed.
