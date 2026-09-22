# Swimmer Profile — Health Monitoring tab — Design

- **Date:** 2026-09-20
- **Status:** Approved (design); implementation pending
- **Branch:** on local `main` (tabs 1–5 present; Records tab committed `d9b904c`); no commits until instructed
- **Sibling / prior art:** `2026-09-20-swimmer-profile-records-tab-design.md`, `2026-09-19-swimmer-profile-inbody-design.md`, `2026-09-18-health-monitoring-design.md`, `2026-09-18-medical-tests-design.md`

## Context & goal

The Swimmer Profile page (`swimmers/:id`) ships five tabs — **Identity & Vitals**, **Guardian**, **Physiological**, **InBody**, **Records**. This feature builds the **sixth tab, Health Monitoring** — a read/edit/delete view over the swimmer's **`health.health_reading`** rows: every logged medical-test result, each measured against that test's normal bounds.

Reference: design mockup (Desktop `mcp/2.png`) and the React design prototype `Desktop/4dba8937-…/src/pages/SwimmerProfile.tsx` (health block: `HEALTH_GROUPS` ~L212–249, render ~L1208–1252). The mock renders one **"Test results"** card (prototype-labelled "Bloodwork") with a From/To date filter and rows of *test name · value+unit · lower–upper range · status pill (In range / Watch) · date*, plus a coach **Edit** affordance.

Unlike the Records tab (where range/status were prototype decoration with no backing data), **here the range and status are real**: `health.medical_test` stores `unit`, `lower_bound`, `upper_bound`, and the status is derived by the backend from the value vs. those bounds (the create slice already does this).

The Health Monitoring tab is **read + edit + delete only**. Logging readings continues to happen in the **Captain Panel → Health Monitoring** flow (`POST /api/health-readings`), which already exists and is unchanged.

## Key architectural decision — extend the existing flat `HealthReadingsController`

`health.health_reading` already has a committed create slice (`8e09f97`): `HealthReadingsController` (`POST /api/health-readings`, swimmer id in the body), `IHealthReadingService`/`HealthReadingService` (with `DeriveStatus`), `IHealthReadingRepository`/`HealthReadingRepository`, and the `HealthReadingDto`/`CreateHealthReadingRequest` DTOs. This feature **extends that same controller and services** with read/update/delete — exactly as the Records tab extended `ObservationsController`.

- **Why flat, not nested:** the create endpoint already established `/api/health-readings` as the resource. Adding GET/PUT/DELETE to the same controller keeps readings cohesive and follows the Records-tab precedent. (InBody uses nested `/api/swimmers/{id}/inbody-readings`; that stays as-is.)
- **Test enrichment stays a service-side lookup, not a DB join projection:** the list must show each test's name/unit/bounds. `HealthReading` and `MedicalTest` both live in `HealthDbContext`, but rather than add a cross-type join projection to the repo, the service loads the (small, head-coach-managed) test catalog via the existing `IMedicalTestRepository.ListAsync()`, builds an id→test map, and enriches + derives status in memory. This mirrors how the Records tab resolves categories via a separate lookup and reuses an endpoint that already exists.

Per the established Health-module pattern, the service does **not** cross-check swimmer existence against Identity.

## Scope

**In scope**
- Backend read/update/delete for a swimmer's health readings: list-by-swimmer (GET, enriched + status), edit value (PUT), delete (DELETE), added to the existing `HealthReadingsController` + health-reading service/repository.
- Frontend: the Health Monitoring tab in the `swimmer-profile` slice — one "Test results" card listing every reading (test name · value+unit · lower–upper range · status pill · date), newest-first, with a client-side From/To date filter and coach **Edit** (inline value form) + **Remove** (confirm step).

**Out of scope**
- Adding readings from this tab (stays in Captain Panel → Health Monitoring; `POST` unchanged).
- Editing which **test** a reading belongs to (edit is **value-only** — see locked decisions). No medical-test catalog is pulled into the profile page.
- Grouping by category (`medical_test` has no category column) — single flat card.
- Server-side date-range filtering (From/To is client-side, mirroring the prototype).
- A third "critical" status band — status is binary (normal/out) mapped to In range / Watch.
- The remaining tabs (attendance, championships, feedback); a swimmer self-view (`/my-profile`).
- Any schema change or new migration (`health.health_reading` already exists on Aiven).
- Cross-module swimmer-existence validation (follows the Health-module norm).

## Locked decisions

1. **Single flat card, all readings** — every `health.health_reading` row for the swimmer, one "Test results" card. Global empty-state when the swimmer has no readings.
2. **Enriched rows** — each row = test name (localized) · value + unit · `lower – upper` range · status pill · reading date. Range/unit/name come from `medical_test`; status is server-derived.
3. **Status is binary, server-derived** — `DeriveStatus`: `value ∈ [lower, upper] ⇒ "normal"`, else `"out"`. UI maps `normal → "In range"` (green) and `out → "Watch"` (amber). No third band.
4. **Read + edit + delete only** — no Add button on this tab.
5. **Edit = value only** — `Value` editable; the reading's `MedicalTestId`, `ReadingDate` (created-moment), and `RecordedBy` are immutable. Status re-derives from the new value on save.
6. **Ordering** — newest-first (`ReadingDate` desc, tiebreak by `Id`).
7. **Date filter is client-side** — From/To narrows the rendered rows by `readingDate`; the GET returns all readings for the swimmer.
8. **Flat routes on `HealthReadingsController`** — `GET /api/health-readings?swimmerId={id}`, `PUT /api/health-readings/{id}`, `DELETE /api/health-readings/{id}`.
9. **Auth** — GET `[Authorize]` (any authenticated); PUT/DELETE `head_coach,captain` (matches the existing `POST`). The class-level `[Authorize(Roles=…)]` moves to the write actions; class becomes `[Authorize]` (mirrors `ObservationsController`).
10. **No ownership guard** beyond "row exists" — readings addressed by globally-unique `id`; the flat route has no swimmer segment to cross-check (matches Records tab).
11. **No commits** until the user says so.

## Data model & persistence

No schema change. Existing tables (Health module, `HealthDbContext`, schema `health`).

### `health.health_reading` (existing — for reference)
| column | type | notes |
|---|---|---|
| `id` | uuid | PK |
| `swimmer_id` | uuid | NOT NULL; loose Guid (no FK) |
| `medical_test_id` | uuid | NOT NULL; → `health.medical_test.id` |
| `value` | numeric(8,2) | NOT NULL; **editable** |
| `reading_date` | timestamptz | NOT NULL; server-set at create (`DateTime.UtcNow`); **immutable** |
| `recorded_by` | uuid | NOT NULL; `CurrentUserId()` at create; **immutable** |

### `health.medical_test` (existing — read for enrichment)
| column | type | notes |
|---|---|---|
| `id` | uuid | PK |
| `name_en` | varchar | NOT NULL |
| `name_ar` | varchar | nullable |
| `unit` | varchar | NOT NULL |
| `lower_bound` | numeric(8,2) | NOT NULL |
| `upper_bound` | numeric(8,2) | NOT NULL |

- Read query: filter `health_reading` by `swimmer_id`, order `reading_date` desc. Test catalog fetched once via `IMedicalTestRepository.ListAsync()` and mapped by id. No new index assumed necessary (mirror how `HealthReadingConfiguration` indexes today; call it out if a `(swimmer_id, reading_date)` index is warranted).

## Backend design (Health module + API)

### Domain
- `Domain/Entities/HealthReading.cs` — add an `Update(decimal value)` method that mutates `Value` only, leaving `SwimmerId`, `MedicalTestId`, `ReadingDate`, `RecordedBy` untouched (mirrors `InBodyReading.Update` / `Observation.Update`).
- `Domain/Repositories/IHealthReadingRepository.cs` — add:
  - `ListBySwimmerAsync(Guid swimmerId, CancellationToken ct)` → `IReadOnlyList<HealthReading>` (newest-first).
  - `GetTrackedAsync(Guid id, CancellationToken ct)` → `HealthReading?`.
  - `Remove(HealthReading reading)`.
  - (`AddAsync` / `SaveChangesAsync` already exist.)

### Infrastructure
- `Repositories/HealthReadingRepository.cs` — implement the three new methods on `HealthDbContext.HealthReadings` (list ordered by `ReadingDate` desc, tiebreak `Id`; tracked get; remove). Mirror `ObservationRepository` / `InBodyReadingRepository`.
- No configuration change unless a `(swimmer_id, reading_date)` index is warranted.

### Application
- **DTOs** (`HealthReadingDtos.cs`) — add:
  - `UpdateHealthReadingRequest(decimal Value)` — value only (no swimmer_id / test_id / date / recorded_by; all immutable on edit).
  - `HealthReadingListItemDto(Guid Id, Guid MedicalTestId, string TestNameEn, string? TestNameAr, string Unit, decimal Value, decimal LowerBound, decimal UpperBound, DateTime ReadingDate, string Status)` — the enriched read row, returned by both GET (list) and PUT (single, so the UI re-renders the edited row with re-derived status).
  - Keep the existing `HealthReadingDto` / `CreateHealthReadingRequest` for the create slice, unchanged.
- **Validation** — `UpdateHealthReadingRequestValidator` (mirror `CreateHealthReadingRequestValidator`'s value rule). Localized messages in the Health module `Resources`.
- **Service** (`IHealthReadingService` / `HealthReadingService`) — add:
  - `ListBySwimmerAsync(Guid swimmerId, CancellationToken ct)` → `IReadOnlyList<HealthReadingListItemDto>`: load readings (newest-first) + `_tests.ListAsync()`, map by id, enrich each row, derive status via the existing `DeriveStatus`. A reading whose test id is missing from the catalog falls back to a safe label (e.g. unit `""`, name from a placeholder) rather than throwing — call it out; not expected in practice.
  - `UpdateAsync(Guid id, UpdateHealthReadingRequest req, CancellationToken ct)` → `HealthReadingListItemDto?` (null ⇒ row missing ⇒ 404); loads the test to re-derive status.
  - `DeleteAsync(Guid id, CancellationToken ct)` → `bool` (false ⇒ missing ⇒ 404).
  - `DeriveStatus` already exists — reuse.
- **Messages** — add `Listed`, `Updated`, `Deleted`, `NotFound` to `HealthReadingMessages` (alongside the existing `Logged` / `Errors.TestNotFound`).

### Controller (`HealthReadingsController` — extend)
Add to the existing controller (`BaseApiController`, `ApiResponse<T>`, `AppLanguage.Current`, `CurrentUserId()`). `POST` unchanged. Move the role restriction off the class onto the write actions.

| Method | Route | Auth | Responses |
|---|---|---|---|
| GET | `api/health-readings?swimmerId={id:guid}` | `[Authorize]` | 200 `IReadOnlyList<HealthReadingListItemDto>` |
| POST | `api/health-readings` | `head_coach,captain` | 201 / 404 (existing) |
| PUT | `api/health-readings/{id:guid}` | `head_coach,captain` | 200 `HealthReadingListItemDto` / 404 / 400 |
| DELETE | `api/health-readings/{id:guid}` | `head_coach,captain` | 200 / 404 |

- No ownership guard beyond "row exists"; the frontend supplies `swimmerId` only on the GET filter.

## Frontend design (`features/swimmer-profile` + `features/health-readings`)

### Tab switching
- Add `'healthMonitoring'` to `enabledTabs` in `swimmer-profile.page.ts`, extend the `activeTab` union + `setTab` param type with `'healthMonitoring'`, and lazy-load on first open in `setTab`. The `swimmerProfile.tabs.healthMonitoring` label already exists (EN "Health Monitoring" / AR "المتابعة الصحية").

### Health-reading domain/data (extend the existing `health-readings` slice)
The `health-readings` feature already has a create slice (model, DTO, repository, `create` use-case). Add:
- `domain/model/health-reading-list-item.ts` — `HealthReadingListItem { id; medicalTestId; testNameEn; testNameAr; unit; value; lowerBound; upperBound; readingDate; status }`.
- `data/dto/` — list-item response DTO + mapper; `UpdateHealthReadingDtoRq { value }`.
- Extend `IHealthReadingRepository` (FE) + impl with:
  - `listBySwimmer(swimmerId)` → `GET /api/health-readings?swimmerId={id}`
  - `update(id, { value })` → `PUT /api/health-readings/{id}`
  - `delete(id)` → `DELETE /api/health-readings/{id}`
- `domain/usecases/`: `list-health-readings`, `update-health-reading`, `delete-health-reading` (mirror the InBody use-cases).

### ViewModel (`swimmer-profile.viewmodel.ts`)
Mirror the Records/InBody edit patterns:
- State: `healthReadings` signal (list of `HealthReadingListItem`), `hmFrom` / `hmTo` date-filter signals, `editingHealthReadingId` (null = not editing), draft `hrValue` signal, `savingHealthReading` / `loadingHealthReadings` / `confirmingHealthReadingDeleteId` / `deletingHealthReading`, `healthReadingsLoaded` flag. Reset these in `load()` alongside the other per-swimmer tab resets.
- `loadHealthReadings()` — **lazy**, on first Health Monitoring tab open.
- **`healthReadingRows` computed** — apply the From/To filter to `healthReadings` (inclusive; empty bound = open-ended), keep newest-first. Localize the test name (`testNameAr ?? testNameEn` in AR, else `testNameEn`). Format the range as `lower – upper` and value as `value unit`.
- `startEditHealthReading(id)` (seed `hrValue` from the row) / `cancelEditHealthReading()`.
- `canSaveHealthReading` computed — `hrValue` non-empty, finite, and > 0 (mirror the create validator's value rule).
- `saveHealthReading()` — PUT; success toast, reload, exit edit mode.
- `askDeleteHealthReading(id)` / `confirmDeleteHealthReading()` — delete + inline confirm, toast, reload.
- `canEdit` (existing `head_coach`/`captain` gate) gates Edit/Remove.

### Health Monitoring section UI (`swimmer-profile.page.html`)
New markup under `activeTab==='healthMonitoring'`: a "Test results" heading + subtitle, a From/To date filter (two `<input type="date">`), then one card listing rows of *test name · value+unit · `lower – upper` · status pill · date*. Status pill: green "In range" for `normal`, amber "Watch" for `out`. Each row shows **Edit** (inline value field + Save/Cancel) and **Remove** (inline confirm, mirror the Records/InBody delete-confirm) for coaches. Global empty-state when the swimmer has no readings (and a "no readings in range" note when the filter excludes all). Reuse existing card/field/table styling.

### i18n
Add `swimmerProfile.healthMonitoring.*` keys (EN + AR): `title` ("Test results"), `subtitle`, `from`/`to` labels, column labels (`test`/`value`/`range`/`status`/`date`), status labels `inRange`/`watch`, `edit`/`remove`/`save`/`cancel`, delete-confirm text, `noReadings` empty-state, `noneInRange` filtered-empty note, and toasts (`readingUpdated`, `readingRemoved`; reuse `saveFailed`/`deleteFailed`). `swimmerProfile.tabs.healthMonitoring` already exists. (Keep these separate from the existing top-level `healthMonitoring.*` Captain Panel keys.)

## Validation, auth & errors

- **Value**: required, finite, > 0 (client + server; mirror the create validator).
- **Auth**: GET `[Authorize]` (any authenticated); PUT/DELETE `head_coach`/`captain`.
- **Not found**: PUT/DELETE of a missing `id` ⇒ 404 → FE toast + reload. GET of a swimmer with no readings ⇒ 200 empty list → empty-state.
- **Missing test in catalog** (a reading referencing a deleted test): service falls back to a safe row rather than 500; UI still renders value/date/status-unknown. Not expected in practice.
- **Concurrency**: a row deleted elsewhere then edited/removed ⇒ 404 handled gracefully (toast + refresh).

## Testing

**Backend (Health unit tests, `Kheprx.BaseBackend.Health.UnitTests`)**
- Entity: `Update` mutates `Value` only, preserves swimmer_id/test_id/reading_date/recorded_by.
- Repository (in-memory `HealthDbContext`): list-by-swimmer newest-first and scoped to the swimmer; get-tracked; remove.
- Service: list enriches from the test catalog + derives status (in-range ⇒ normal, out-of-range low/high ⇒ out); update applies value + re-derives status + returns DTO; update-missing ⇒ null; delete existing ⇒ true; delete-missing ⇒ false; validation (value > 0).
- Controller/API: auth (200 authed GET; 403 non-coach write; 401 anonymous), 200/404/400 shapes, GET filters by `swimmerId`. (Mirror `InBodyReadingsControllerTests` / `HealthReadingsControllerTests`.)

**Frontend (jest, following the InBody/Records slice conventions)**
- Mapper (DTO ↔ model), repository impl (three URLs/payloads), three use cases, viewmodel (lazy load once, **`healthReadingRows` filter + newest-first + localization + value/range formatting**, edit/save/delete flows, `canSaveHealthReading` rules, status→pill mapping, tab switch renders the section).

**Gates**
- Backend + frontend builds green; existing suites still pass; whole-branch review before any merge. (`dotnet test` requires the running API stopped — DLL lock.)

## Anchor files (patterns to mirror)

- Health-reading create slice (extend these): `…/Health.Domain/Entities/HealthReading.cs` + `…/Domain/Repositories/IHealthReadingRepository.cs`, `…/Health.Infrastructure/Repositories/HealthReadingRepository.cs`, `…/Health.Application/Services/HealthReadingService.cs` + `Interfaces/IHealthReadingService.cs`, `…/Health.Application/DTOs/HealthReadingDtos.cs`, `…/Health.Application/Resources/HealthReadingMessages.cs`, `…/Health.Application/Validators/CreateHealthReadingRequestValidator.cs`, `backend/Kheprx.BaseBackend.Api/Controllers/HealthReadingsController.cs`
- Read/edit/delete CRUD shape to mirror: `ObservationsController.cs` + `ObservationService.cs` (list/get-tracked/update/delete, flat routes, 404), `InBodyReadingsController.cs`
- Test catalog lookup (reuse): `IMedicalTestRepository.ListAsync()`
- Current user: `backend/Kheprx.BaseBackend.Api/Controllers/BaseApiController.cs` (`CurrentUserId()`)
- FE slice (mirror the Records/InBody tabs): `frontend/src/app/features/health-readings/…` (existing create slice — extend), `frontend/src/app/features/swimmer-profile/…` (viewmodel/page + records/inbody domain/data/usecases)
- i18n: `frontend/src/app/core/i18n/{en,ar}.json` (`swimmerProfile.tabs.healthMonitoring` already present)
- DB diagram: `docs/references/swimming-database-diagram.html` (`health.health_reading`, `health.medical_test`)

## Follow-ons (not now)

- Server-side date-range filtering / pagination if reading volumes grow.
- Re-pointing a reading to a different test from this tab (needs a test dropdown + medical-tests list endpoint).
- Trend visualisation per test (series over time).
- The remaining profile tabs; a swimmer self-view (`/my-profile`).
