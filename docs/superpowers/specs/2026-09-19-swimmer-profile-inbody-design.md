# Swimmer Profile — InBody tab — Design

- **Date:** 2026-09-19
- **Status:** Approved (design); implementation pending
- **Branch:** `feat/swimmer-profile-guardian` (stacked on the committed Guardian `7b122ed` + Physiological `e212610`; no commits until instructed)
- **Sibling / prior art:** `2026-09-19-swimmer-profile-physiological-design.md`, `2026-09-19-swimmer-exam-history-edit-design.md`, `2026-09-18-health-monitoring-design.md`

## Context & goal

The Swimmer Profile page (`swimmers/:id`) ships three tabs — **Identity & Vitals**, **Guardian**, **Physiological**. This feature builds the **fourth tab, InBody** — a body-composition reading history sourced from `health.inbody_reading`, with a coach-editable full CRUD and a per-metric trend table.

Reference: design mockup (Desktop `mcp/2.png`) and the React design prototype `Desktop/4dba8937-…/src/pages/SwimmerProfile.tsx` (inbody block ~L861–1040). The mockup shows: a **reading selector** ("N readings on record — pick one to view"), coach **Add reading** + **Edit** (+ **Delete**, added per the scope decision), the selected reading rendered as bars, and a **Measurement History** table (metrics × reading-dates with a per-metric "Change" trend column).

## Key architectural decision — Health module, not Identity

Unlike the prior three tabs (which folded into the **Identity** module's `SwimmerService`/`SwimmersController`), `inbody_reading` is a **`health`-schema** table, and a full **Health module** already exists with the exact analog: `HealthReading` (swimmer-scoped, `recorded_by` via `CurrentUserId()`, its own `HealthDbContext` + migrations + `HealthReadingsController`). Folding InBody into Identity would force a backwards Identity→Health dependency. Therefore **InBody lives in the Health module**, exposed via a new `InBodyReadingsController` at `/api/swimmers/{id}/inbody-readings` (route consistent with the profile page; module placement follows the schema).

Per the established Health-module pattern (`HealthReadingService`, `ObservationService`), the Health module does **not** cross-check swimmer existence against Identity — it relies on the DB FK. This feature follows suit: no swimmer-exists 404; instead a **reading-ownership guard** on edit/delete (`reading.SwimmerId == route id`), mirroring the exam-history CRUD.

## Scope

**In scope**
- New `health.inbody_reading` table + EF migration on `HealthDbContext` (not auto-applied).
- Full CRUD for a swimmer's readings: GET list, POST add, PUT edit, DELETE.
- Frontend: the InBody tab in the `swimmer-profile` slice — reading selector, coach Add/Edit/Delete, selected-reading bars, and the Measurement History table with a per-metric "Change" (latest-vs-previous) trend column, computed client-side.

**Out of scope**
- The remaining five tabs (records, health monitoring, attendance, championships, feedback).
- Charts/graphs beyond the history table.
- Any InBody reference/catalog table, or capturing readings during registration.
- Cross-module swimmer-existence validation (follows the Health-module norm).

## Locked decisions

1. **Health module placement** — new `InBodyReading` entity/config/repository/service + `InBodyReadingsController`, DbSet on `HealthDbContext`, migration on `HealthDbContext`.
2. **Full CRUD + trend table** — GET list / POST / PUT / DELETE; the FE renders the reading selector, Add/Edit/Delete (coach), the selected reading as bars, and the Measurement History table with the "Change" column.
3. **`recorded_by` = `CurrentUserId()`** (JWT `sub`, via `BaseApiController.CurrentUserId()`); set on create, preserved on edit.
4. **`reading_date` is user-provided** (date field in the Add/Edit form, required); `created_at` is server-set (`DateTime.UtcNow`).
5. **Ownership guard** on PUT/DELETE (`reading.SwimmerId == route id`) → 404 when the reading is missing or belongs to another swimmer. No swimmer-exists check (Health-module norm).
6. **"Change" column** = latest-vs-previous reading per metric, computed client-side from the readings list. No backend trend computation.
7. **No commits** until the user says so; stacked on the current branch.

## Data model & persistence

One new table in the **Health module** (`HealthDbContext`, default schema `health`).

### `health.inbody_reading`
| column | type | notes |
|---|---|---|
| `id` | uuid | PK |
| `swimmer_id` | uuid | FK → `identity.swimmer_profile.id`, NOT NULL |
| `reading_date` | date | NOT NULL; user-provided |
| `height_cm` | numeric(5,1) | NOT NULL |
| `weight_kg` | numeric(5,1) | NOT NULL |
| `fat_pct` | numeric(4,1) | NOT NULL |
| `muscle_pct` | numeric(4,1) | NOT NULL |
| `bone_density` | numeric(4,2) | NOT NULL |
| `body_density` | numeric(4,2) | NOT NULL |
| `recorded_by` | uuid | FK → `identity.app_user.id`, NOT NULL |
| `created_at` | timestamptz | NOT NULL; server-set |

- Index on `(swimmer_id, reading_date)` for the per-swimmer history query (order by `reading_date` desc, `created_at` desc tiebreak).
- FK delete behaviour mirrors `HealthReadingConfiguration` (the swimmer FK; check its `OnDelete` and match it).
- CLR types: `reading_date`→`DateOnly`, `created_at`→`DateTime`, metrics→`decimal` with `HasPrecision(5,1)` / `HasPrecision(4,1)` / `HasPrecision(4,2)` respectively.

### Migration
- One EF migration `CreateInBodyReadingTable` on `HealthDbContext` (mirrors `20260918134550_CreateHealthReadingTable`).
- **Created but NOT auto-applied.** Run `dotnet ef database update` on the Health module when ready.

## Backend design (Health module + API)

### Domain
- `Domain/Entities/InBodyReading.cs` — entity (`Id`, `SwimmerId`, `ReadingDate`, `HeightCm`, `WeightKg`, `FatPct`, `MusclePct`, `BoneDensity`, `BodyDensity`, `RecordedBy`, `CreatedAt`). Create ctor `(swimmerId, readingDate, heightCm, weightKg, fatPct, musclePct, boneDensity, bodyDensity, recordedBy)` sets `Id = Guid.NewGuid()`, `CreatedAt = DateTime.UtcNow`. `Update(readingDate, heightCm, weightKg, fatPct, musclePct, boneDensity, bodyDensity)` mutates reading_date + 6 metrics; leaves `RecordedBy`/`CreatedAt`. Mirrors `HealthReading.cs` + `MedicalExam.cs`.
- `Domain/Repositories/IInBodyReadingRepository.cs` — `ListBySwimmerAsync(swimmerId, ct)` → `IReadOnlyList<InBodyReading>` (or a read-model row), `AddAsync(reading, ct)`, `GetTrackedAsync(readingId, ct)`, `Remove(reading)`, `SaveChangesAsync(ct)`. Mirrors `IHealthReadingRepository`.

### Infrastructure
- `Configurations/InBodyReadingConfiguration.cs` → `ToTable("inbody_reading", "health")` (schema is the DbContext default; the table name suffices), `HasPrecision` per column, FKs to `swimmer_profile` and `app_user`, index on `(SwimmerId, ReadingDate)`.
- Register `DbSet<InBodyReading> InBodyReadings` on `HealthDbContext`.
- `Repositories/InBodyReadingRepository.cs` — implements the repo (list ordered by `reading_date` desc, `created_at` desc).
- Register the repository + service in `HealthModuleExtensions.cs` (mirror `HealthReadingRepository`/`HealthReadingService` registration).

### Application
- **DTOs** — `InBodyReadingDtos.cs`:
  - Read: `InBodyReadingDto(Guid Id, DateOnly ReadingDate, decimal HeightCm, decimal WeightKg, decimal FatPct, decimal MusclePct, decimal BoneDensity, decimal BodyDensity, Guid RecordedBy)`.
  - Write: `CreateInBodyReadingRequest(DateOnly ReadingDate, decimal HeightCm, decimal WeightKg, decimal FatPct, decimal MusclePct, decimal BoneDensity, decimal BodyDensity)` (no swimmer_id — it comes from the route; no recorded_by — from `CurrentUserId()`).
- **Validation** — `CreateInBodyReadingRequestValidator` (FluentValidation): `ReadingDate` not default; `HeightCm`, `WeightKg` `> 0` and `≤ 999.9`; `FatPct`, `MusclePct` `≥ 0` and `≤ 100`; `BoneDensity`, `BodyDensity` `> 0` and `≤ 99.99` (fits `numeric(4,2)`). Localized messages in the Health module's `Resources`.
- **Service** — `IInBodyReadingService` + `InBodyReadingService`:
  - `ListAsync(Guid swimmerId, ct)` → `IReadOnlyList<InBodyReadingDto>` (empty when none).
  - `CreateAsync(Guid swimmerId, CreateInBodyReadingRequest req, Guid recordedBy, ct)` → `InBodyReadingDto`.
  - `UpdateAsync(Guid swimmerId, Guid readingId, CreateInBodyReadingRequest req, ct)` → `InBodyReadingDto?` (null ⇒ reading missing or `SwimmerId != swimmerId` ⇒ 404).
  - `DeleteAsync(Guid swimmerId, Guid readingId, ct)` → `bool` (false ⇒ missing/foreign ⇒ 404).
- **Messages** — add InBody success/error messages in the Health `Resources` (e.g. `InBodyReadingMessages`): `Listed`, `Created`, `Updated`, `Deleted`, `NotFound`, invalid-value.

### Controller (`InBodyReadingsController`)
New controller in the Api project (mirrors `HealthReadingsController` for module wiring + `SwimmersController` exam-history for the swimmer-scoped CRUD shape). Uses `ApiResponse<T>`, `AppLanguage.Current`, `CurrentUserId()`.

| Method | Route | Auth | Responses |
|---|---|---|---|
| GET | `api/swimmers/{id:guid}/inbody-readings` | `[Authorize]` | 200 `IReadOnlyList<InBodyReadingDto>` |
| POST | `api/swimmers/{id:guid}/inbody-readings` | `head_coach,captain` | 201 `InBodyReadingDto` / 400 |
| PUT | `api/swimmers/{id:guid}/inbody-readings/{readingId:guid}` | `head_coach,captain` | 200 / 404 / 400 |
| DELETE | `api/swimmers/{id:guid}/inbody-readings/{readingId:guid}` | `head_coach,captain` | 200 / 404 |

## Frontend design (`features/swimmer-profile`)

### Tab switching
- Extend `enabledTabs` (add `inbody`), the `activeTab` union (`… | 'inbody'`), and `setTab` to lazy-load the readings on first InBody open. The `swimmerProfile.tabs.inbody` label already exists.

### InBody domain (isolated in the slice)
- `domain/model/inbody-reading.ts` — `InBodyReading { id; readingDate; heightCm; weightKg; fatPct; musclePct; boneDensity; bodyDensity }` (numbers; `readingDate` string).
- `domain/usecases/`: `list-inbody-readings`, `create-inbody-reading`, `update-inbody-reading`, `delete-inbody-reading`.
- Extend the existing `ISwimmerProfileRepository` (FE) + impl with `getInBodyReadings(id)`, `createInBodyReading(id, rq)`, `updateInBodyReading(id, readingId, rq)`, `deleteInBodyReading(id, readingId)` (routes already match its `/api/swimmers/{id}/…` base). New `data/dto/` InBody DTO + mapper.

### ViewModel (`swimmer-profile.viewmodel.ts`)
Mirror the exam-history edit pattern:
- State: `inbodyReadings` signal (list), `selectedInBodyId`, `editingInBodyId` (null = adding), draft signals (`ibDate`, `ibHeight`, `ibWeight`, `ibFat`, `ibMuscle`, `ibBone`, `ibBody`), `addingInBody`/`savingInBody`/`loadingInBody`/`confirmingInBodyDelete`/`deletingInBody`, `inbodyLoaded` flag.
- `loadInBody()` — **lazy**, on first InBody tab open; sets list + selected = latest.
- `selectedInBodyReading` computed (by `selectedInBodyId`, default latest).
- `startAddInBody()` / `startEditInBody()` (seed draft from the selected reading) / `cancelEditInBody()`.
- `canSaveInBody` computed — `ibDate` set and all six metrics parse to numbers within their ranges.
- `saveInBody()` — create or update per `editingInBodyId`; success toast, reload, select the saved reading.
- `askDeleteInBody()` / `confirmDeleteInBody()` — delete + confirm, toast, reload.
- **`inbodyHistory` computed** — the Measurement History matrix: for each metric row, the value at each reading (columns = readings, oldest→newest) plus a **Change** = latest − previous (with sign/unit), driving the trend column.
- `canEdit` gates Add/Edit/Delete to `head_coach`/`captain`.

### InBody section UI
- New markup in `swimmer-profile.page.html` under `activeTab==='inbody'`: reading selector (dropdown, latest flagged), coach Add-form (date + 6 metric fields) / Edit / Delete (+ inline delete confirm), the selected reading rendered as labeled bars (height/weight/fat/muscle/bone/body), and the Measurement History table (metric rows × reading-date columns + the Change column). Reuses existing card/field/table styling.

### i18n
Add `swimmerProfile.inbody.*` keys (EN + AR): section title, metric labels (`height`, `weight`, `fatPercent`, `musclePercent`, `boneDensity`, `bodyDensity`) + units, `reading`, `addReading`, `editReading`, `delete`/confirm, `measurementHistory`, `change`, `latest`, `noReadings`, and toasts (`readingSaved`, `readingDeleted`, reuse `saveFailed`/`deleteFailed`). `swimmerProfile.tabs.inbody` already exists.

## Validation, auth & errors

- **Values**: `reading_date` required; `height_cm`/`weight_kg` `>0` & `≤999.9`; `fat_pct`/`muscle_pct` `≥0` & `≤100`; `bone_density`/`body_density` `>0` & `≤99.99`. Client + server.
- **Auth**: GET `[Authorize]` (any authenticated); POST/PUT/DELETE `head_coach`/`captain`.
- **Not found**: PUT/DELETE of a missing or cross-swimmer reading ⇒ 404 (ownership guard `reading.SwimmerId == route id`). GET of a swimmer with no readings ⇒ 200 empty list.
- **No cross-swimmer leakage**: reads/writes are scoped by the route `swimmer_id`.

## Testing

**Backend (Health unit tests, `Kheprx.BaseBackend.Health.UnitTests`)**
- Entity: ctor sets id/created_at; `Update` mutates the seven fields, preserves recorded_by/created_at.
- Repository (in-memory `HealthDbContext`): list ordered newest-first; add; get-tracked; remove.
- Service: list maps rows; create sets recorded_by; update applies + ownership guard (foreign/missing ⇒ null); delete ownership guard (⇒ false); validation ranges.
- Controller/API: auth (200 authed GET; 403 non-coach write; 401 anonymous), 200/201/404/400 shapes, `CurrentUserId()` flows to recorded_by.

**Frontend (jest)**
- Mapper (DTO ↔ model), repository impl (four URLs/payloads incl. `{readingId}`), four use cases, viewmodel (lazy load once, add/edit/delete flows, `canSave` range rules, **`inbodyHistory` matrix + Change deltas**, selector selection, tab switch renders the section).

**Gates**
- Backend + frontend builds green; existing suites still pass; whole-branch review before any merge.

## Anchor files (patterns to mirror)

- Health-module entity/config/repo/service/controller: `…/Health.Domain/Entities/HealthReading.cs`, `…/Health.Infrastructure/Configurations/HealthReadingConfiguration.cs`, `…/Health.Infrastructure/Repositories/HealthReadingRepository.cs` + `…/Domain/Repositories/IHealthReadingRepository.cs`, `…/Health.Application/Services/HealthReadingService.cs` + `Interfaces/IHealthReadingService.cs`, `…/Health.Application/DTOs/HealthReadingDtos.cs`, `backend/Kheprx.BaseBackend.Api/Controllers/HealthReadingsController.cs`
- DbContext / DI / migration precedent: `…/Health.Infrastructure/Data/HealthDbContext.cs`, `…/Health.Infrastructure/Extensions/HealthModuleExtensions.cs`, migration `20260918134550_CreateHealthReadingTable`
- Current user: `backend/Kheprx.BaseBackend.Api/Controllers/BaseApiController.cs` (`CurrentUserId()`)
- Swimmer-scoped CRUD + ownership guard: `SwimmersController.cs` exam-history endpoints (`…/medical-exams/{examId}` PUT/DELETE)
- FE slice: `frontend/src/app/features/swimmer-profile/…` (guardian/physiological domain/data/usecases/viewmodel/page), `swimmer-profile.repository.{ts,impl.ts}`
- DB diagram: `docs/references/swimming-database-diagram.html` (`health.inbody_reading` ~L250)

## Follow-ons (not now)

- InBody charts/graphs; the remaining profile tabs; a swimmer self-view.
