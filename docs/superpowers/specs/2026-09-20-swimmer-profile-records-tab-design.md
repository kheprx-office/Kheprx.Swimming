# Swimmer Profile — Records tab — Design

- **Date:** 2026-09-20
- **Status:** Approved (design); implementation pending
- **Branch:** on local `main` (tabs 1–4 merged via `a5c0f18`); no commits until instructed
- **Sibling / prior art:** `2026-09-19-swimmer-profile-inbody-design.md`, `2026-09-18-swimmer-data-fields-design.md`, `2026-09-02-reference-lookups-design.md`

## Context & goal

The Swimmer Profile page (`swimmers/:id`) ships four tabs — **Identity & Vitals**, **Guardian**, **Physiological**, **InBody**. This feature builds the **fifth tab, Records** — a read/edit/delete view over the swimmer's **`health.observation`** rows (the "Swimmer Data Fields" the Captain Panel creates), grouped by category.

Reference: design mockup (Desktop `mcp/2.png`) and the React design prototype `Desktop/4dba8937-…/src/pages/SwimmerProfile.tsx` (records block ~L193–309). The mock renders records in grouped sections (Body composition, Medical flags) with per-row range/status pills and trend charts. **Those pills/ranges/trends are prototype decoration with no backing data** — `health.observation` stores only field label, value, category, and recorded date. This feature renders the **real fields only**; range/status/trend are explicitly out of scope (they belong to the separate Medical-Tests / Health-Monitoring concept).

The Records tab is **read + edit + delete only**. Adding records continues to happen in the Captain Panel → Swimmer Data Fields flow (`POST /api/observations`), which already exists and is unchanged.

## Key architectural decision — extend the existing flat `ObservationsController`

`health.observation` already has a committed create slice: `ObservationsController` (`POST /api/observations`, swimmer id in the body), `IObservationService`/`ObservationService`, `IObservationRepository`/`ObservationRepository` (Health module), and `ObservationDto`. This feature **extends that same controller and services** with the read/update/delete methods rather than introducing a new nested `/api/swimmers/{id}/observations` resource.

- **Why flat, not nested:** the create endpoint already established `/api/observations` as the observation resource. Adding GET/PUT/DELETE to the same controller keeps observations cohesive and avoids a second convention for one table. (InBody uses nested `/api/swimmers/{id}/inbody-readings`; that stays as-is — observations simply follow their own already-set precedent.)
- **Category resolution stays client-side:** `Observation` lives in `HealthDbContext` while `ObservationCategory` lives in Identity's `reference` schema (loose Guids, no FK). The backend GET returns `ObservationDto` (with `CategoryId`) only — it does **not** join across modules. The frontend already fetches categories via `GET /api/reference/observation-categories` (`LoadObservationCategoriesUseCase`) and maps `CategoryId → name` for grouping + the edit dropdown.

Per the established Health-module pattern, the service does **not** cross-check swimmer existence against Identity.

## Scope

**In scope**
- Backend read/update/delete for a swimmer's observations: list-by-swimmer (GET), edit (PUT), delete (DELETE), added to the existing `ObservationsController` + observation service/repository.
- Frontend: the Records tab in the `swimmer-profile` slice — observations grouped by category (category header + count), each row = field label · value · recorded date, with coach **Edit** (inline form) and **Remove** (confirm step). Category names resolved client-side from the reference lookup.

**Out of scope**
- Adding records from this tab (stays in Captain Panel → Swimmer Data Fields; `POST` unchanged).
- Range/status pills, "In range / Out of range" evaluation, trend charts (prototype-only; no backing data — belongs to Health Monitoring / Medical Tests).
- From/To date-range filtering (deferred; v1 shows all, newest-first).
- The remaining four tabs (health monitoring, attendance, championships, feedback).
- Any schema change or new migration (`health.observation` already exists on Aiven).
- Cross-module swimmer-existence validation (follows the Health-module norm).

## Locked decisions

1. **All categories, grouped by category** — every `health.observation` row for the swimmer is shown, grouped by `categoryId` (all 7 seeded categories: allergy, surgery, chronic, autoimmune, composition, flag, other). Groups with no rows are hidden; a global empty-state shows when the swimmer has no records.
2. **Real fields only** — each row renders field label · value · recorded date. No range/status/trend.
3. **Read + edit + delete only** — no Add button on this tab.
4. **Edit = label + value + category** — all three editable; `ObservedDate` and `RecordedBy` immutable on edit.
5. **Ordering** — newest-first (`ObservedDate` desc, tiebreak by `Id`) within each group.
6. **Flat routes on `ObservationsController`** — `GET /api/observations?swimmerId={id}`, `PUT /api/observations/{id}`, `DELETE /api/observations/{id}`.
7. **Auth** — GET `[Authorize]` (any authenticated); PUT/DELETE `head_coach,captain` (matches the existing `POST`).
8. **Category names resolved client-side** via the existing `GET /api/reference/observation-categories`.
9. **No commits** until the user says so.

## Data model & persistence

No schema change. Existing table (Health module, `HealthDbContext`, schema `health`).

### `health.observation` (existing — for reference)
| column | type | notes |
|---|---|---|
| `id` | uuid | PK |
| `swimmer_id` | uuid | NOT NULL; loose Guid (no FK) |
| `category_id` | uuid | NOT NULL; loose Guid → `reference.observation_category.id` |
| `field_label` | varchar(200) | NOT NULL |
| `value` | varchar(500) | NOT NULL |
| `observed_date` | timestamptz | NOT NULL; server-set at create (`DateTime.UtcNow`); **immutable** |
| `recorded_by` | uuid | NOT NULL; `CurrentUserId()` at create; **immutable** |

- Read query: filter by `swimmer_id`, order `observed_date` desc. No new index assumed necessary for current volumes (call it out if the swimmer-scoped query needs one; mirror how `ObservationConfiguration` indexes today).

## Backend design (Health module + API)

### Domain
- `Domain/Entities/Observation.cs` — add an `Update(categoryId, fieldLabel, value)` method that mutates those three fields and leaves `SwimmerId`, `ObservedDate`, `RecordedBy` untouched (mirrors `InBodyReading.Update` / `MedicalExam.Update`).
- `Domain/Repositories/IObservationRepository.cs` — add:
  - `ListBySwimmerAsync(Guid swimmerId, CancellationToken ct)` → `IReadOnlyList<Observation>` (newest-first).
  - `GetTrackedAsync(Guid id, CancellationToken ct)` → `Observation?`.
  - `Remove(Observation observation)`.
  - (`AddAsync` / `SaveChangesAsync` already exist.)

### Infrastructure
- `Repositories/ObservationRepository.cs` — implement the three new methods on `HealthDbContext.Observations` (list ordered by `ObservedDate` desc; tracked get; remove). Mirror `InBodyReadingRepository` / `HealthReadingRepository`.
- No configuration change unless a `(swimmer_id, observed_date)` index is warranted.

### Application
- **DTOs** (`ObservationDtos.cs`) — add write contract:
  - `UpdateObservationRequest(Guid CategoryId, string FieldLabel, string Value)` (no swimmer_id — the row already has it; no recorded_by/observed_date — immutable).
  - Reuse the existing `ObservationDto(Id, SwimmerId, CategoryId, FieldLabel, Value, ObservedDate, RecordedBy)` for all responses.
- **Validation** — `UpdateObservationRequestValidator` (mirror the create validator): `CategoryId` not empty; `FieldLabel` required, ≤200; `Value` required, ≤500. Localized messages in the Health module `Resources`.
- **Service** (`IObservationService` / `ObservationService`) — add:
  - `ListBySwimmerAsync(Guid swimmerId, CancellationToken ct)` → `IReadOnlyList<ObservationDto>` (empty when none).
  - `UpdateAsync(Guid id, UpdateObservationRequest req, CancellationToken ct)` → `ObservationDto?` (null ⇒ row missing ⇒ 404).
  - `DeleteAsync(Guid id, CancellationToken ct)` → `bool` (false ⇒ missing ⇒ 404).
- **Messages** — add `Listed`, `Updated`, `Deleted`, `NotFound` to the observation messages resource (alongside the existing `Added`).

### Controller (`ObservationsController` — extend)
Add to the existing controller (`BaseApiController`, `ApiResponse<T>`, `AppLanguage.Current`, `CurrentUserId()`). `POST` unchanged.

| Method | Route | Auth | Responses |
|---|---|---|---|
| GET | `api/observations?swimmerId={id:guid}` | `[Authorize]` | 200 `IReadOnlyList<ObservationDto>` |
| PUT | `api/observations/{id:guid}` | `head_coach,captain` | 200 `ObservationDto` / 404 / 400 |
| DELETE | `api/observations/{id:guid}` | `head_coach,captain` | 200 / 404 |

- No ownership guard beyond "row exists" — observations are addressed by their globally-unique `id`; the flat route has no swimmer segment to cross-check. The frontend supplies `swimmerId` only on the GET filter. (If tighter scoping is later wanted, add an optional `swimmerId` check on PUT/DELETE — not in v1.)

## Frontend design (`features/swimmer-profile`)

### Tab switching
- Add `'records'` to `enabledTabs` in `swimmer-profile.page.ts`, extend the `activeTab` union with `'records'`, and lazy-load on first Records open in `setTab`. The `swimmerProfile.tabs.records` label already exists (EN "Records" / AR "السجلات").

### Records domain (isolated in the slice)
- `domain/model/record-entry.ts` — `RecordEntry { id; swimmerId; categoryId; fieldLabel; value; observedDate }` (`observedDate` string).
- `domain/usecases/`: `list-records`, `update-record`, `delete-record`.
- Extend the FE `ISwimmerProfileRepository` + impl with `listRecords(swimmerId)` → `GET /api/observations?swimmerId={id}`, `updateRecord(id, { categoryId, fieldLabel, value })` → `PUT /api/observations/{id}`, `deleteRecord(id)` → `DELETE /api/observations/{id}`. New `data/dto/` observation DTO + mapper.
- **Categories** — reuse the existing `LoadObservationCategoriesUseCase` (reference feature) to fetch `{ id, code, nameEn, nameAr }[]`; no new category plumbing.

### ViewModel (`swimmer-profile.viewmodel.ts`)
Mirror the InBody edit pattern:
- State: `records` signal (list), `categories` signal (lookup), `editingRecordId` (null = not editing), draft signals (`recCategoryId`, `recLabel`, `recValue`), `savingRecord` / `loadingRecords` / `confirmingRecordDelete` / `deletingRecord`, `recordsLoaded` flag.
- `loadRecords()` — **lazy**, on first Records tab open; loads observations + categories together.
- **`recordGroups` computed** — group `records` by `categoryId`, resolve the category display name by current language (`nameAr ?? nameEn` in AR, else `nameEn`), drop empty groups, sort rows newest-first. Unknown/missing category id falls back to an "Other/Uncategorized" label rather than dropping the row.
- `startEditRecord(id)` (seed draft from the row) / `cancelEditRecord()`.
- `canSaveRecord` computed — `recCategoryId` set, `recLabel` non-empty (≤200), `recValue` non-empty (≤500).
- `saveRecord()` — PUT; success toast, reload, exit edit mode.
- `askDeleteRecord(id)` / `confirmDeleteRecord()` — delete + confirm, toast, reload.
- `canEdit` gates Edit/Remove to `head_coach`/`captain` (reuse the existing role gate the other tabs use).

### Records section UI
- New markup in `swimmer-profile.page.html` under `activeTab==='records'`: for each non-empty group, a category header with a row count, then rows of field label · value · recorded date, each with **Edit** and **Remove** (coach). Inline **Edit** form = category `<select>` (from `categories`) + label field + value field + Save/Cancel. **Remove** shows an inline confirm (mirror InBody delete-confirm). Global empty-state when the swimmer has no records. Reuses existing card/field/table styling — no range/status/trend elements.

### i18n
Add `swimmerProfile.records.*` keys (EN + AR): section intro, `field`/`value`/`date` column labels, `edit`, `remove`, `save`, `cancel`, delete-confirm text, `noRecords` empty-state, `uncategorized` fallback, and toasts (`recordSaved`, `recordRemoved`; reuse `saveFailed`/`deleteFailed` if present). `swimmerProfile.tabs.records` already exists.

## Validation, auth & errors

- **Values**: `CategoryId` required; `FieldLabel` required & ≤200; `Value` required & ≤500. Client + server.
- **Auth**: GET `[Authorize]` (any authenticated); PUT/DELETE `head_coach`/`captain`.
- **Not found**: PUT/DELETE of a missing `id` ⇒ 404 → FE toast + reload. GET of a swimmer with no observations ⇒ 200 empty list → empty-state.
- **Concurrency**: a row deleted elsewhere then edited/removed ⇒ 404 handled gracefully (toast + refresh).

## Testing

**Backend (Health unit tests, `Kheprx.BaseBackend.Health.UnitTests`)**
- Entity: `Update` mutates category/label/value, preserves swimmer_id/observed_date/recorded_by.
- Repository (in-memory `HealthDbContext`): list-by-swimmer newest-first and scoped to the swimmer; get-tracked; remove.
- Service: list maps rows; update applies + returns DTO; update-missing ⇒ null; delete existing ⇒ true; delete-missing ⇒ false; validation ranges.
- Controller/API: auth (200 authed GET; 403 non-coach write; 401 anonymous), 200/404/400 shapes, GET filters by `swimmerId`.

**Frontend (jest, following the InBody slice's test conventions)**
- Mapper (DTO ↔ model), repository impl (three URLs/payloads), three use cases, viewmodel (lazy load once, **`recordGroups` grouping + newest-first + empty-group drop + uncategorized fallback**, edit/save/delete flows, `canSaveRecord` rules, tab switch renders the section).

**Gates**
- Backend + frontend builds green; existing suites still pass; whole-branch review before any merge. (`dotnet test` requires the running API stopped — DLL lock.)

## Anchor files (patterns to mirror)

- Observation create slice (extend these): `…/Health.Domain/Entities/Observation.cs` + `…/Domain/Repositories/IObservationRepository.cs`, `…/Health.Infrastructure/Repositories/ObservationRepository.cs`, `…/Health.Application/Services/ObservationService.cs` + `Interfaces/IObservationService.cs`, `…/Health.Application/DTOs/ObservationDtos.cs`, `backend/Kheprx.BaseBackend.Api/Controllers/ObservationsController.cs`
- CRUD + service shape to mirror: `InBodyReadingService.cs` / `InBodyReadingRepository.cs` (list/get-tracked/update/delete)
- Current user: `backend/Kheprx.BaseBackend.Api/Controllers/BaseApiController.cs` (`CurrentUserId()`)
- Categories endpoint (reuse): `backend/Kheprx.BaseBackend.Api/Controllers/ReferenceController.cs` (`GET /api/reference/observation-categories`), FE `LoadObservationCategoriesUseCase` + reference repository
- FE slice (mirror the InBody tab): `frontend/src/app/features/swimmer-profile/…` (inbody domain/data/usecases/viewmodel/page), `swimmer-profile.repository.{ts,impl.ts}`
- i18n: `frontend/src/app/core/i18n/{en,ar}.json` (`swimmerProfile.tabs.records` already present)
- DB diagram: `docs/references/swimming-database-diagram.html` (`health.observation`)

## Follow-ons (not now)

- From/To date-range filtering on the Records tab.
- Any range/status evaluation or trend visualisation (Health Monitoring / Medical Tests territory).
- The remaining profile tabs; a swimmer self-view (`/my-profile`).
