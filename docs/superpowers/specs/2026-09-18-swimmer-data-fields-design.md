# Swimmer Data Fields — Add an Observation

**Date:** 2026-09-18
**Status:** Approved design (spec)
**Scope:** Write-path only — add a `health.observation` row via a Captain Panel form, plus the `reference.observation_category` lookup that feeds the category dropdown.

## 1. Summary

Wire the existing **Captain Panel → Swimmer Records** card (currently a disabled
"coming soon" tile) to a working **"Swimmer Data Fields"** form. A Head Coach or Captain
selects a swimmer, picks a category (allergy / surgery / …), enters a field name + value, and
saves it. The server records the observation, stamping `observed_date` with the creation
moment and `recorded_by` with the current user.

This is the direct sibling of the **Health Monitoring** feature (commit `8e09f97`) — the
observation write-path mirrors `HealthReading` layer-for-layer. It additionally implements
the **`reference.observation_category`** lookup (added to the schema doc in commit `e8f9f79`
but not yet in the backend), following the existing reference-lookup pattern
(`blood_type`/`stroke`/`gender` in the Identity module).

Viewing observations (the "Records tab on the swimmer's detail page" in the mockup) is
**out of scope** — no swimmer-detail page exists; that is a later feature.

**Design references:**
- UI prototype (visual reference only): `C:\Users\envnt\Desktop\4dba8937-ce3f-44bd-b093-515f10a5ea2b\` (`src/pages/CaptainPanel.tsx`, "Swimmer Data Fields" section)
- Approved screenshots: `C:\Users\envnt\Desktop\mcp\1.png` (Captain Panel card), `2.png` (form)
- DB tables: `docs/references/swimming-database-diagram.html` → `health.observation`, `reference.observation_category`

## 2. Decisions (locked)

1. **Category = real reference lookup.** Implement `reference.observation_category` as a
   seeded lookup (Identity module, like `blood_type`) with a read endpoint + frontend load
   use-case. `observation.category_id` references it. Matches the committed schema.
2. **`observed_date` = created moment.** Stored as **`timestamptz`** set to `DateTime.UtcNow`
   at creation (a true created-at, matching `health_reading.reading_date`). No date input on
   the form. The schema doc is updated `date` → `timestamptz`.
3. **Roles = Head Coach + Captain.** Both may add swimmer data. `POST /api/observations` is
   gated to `head_coach,captain`. The `reference` read endpoint is `[Authorize]` (any
   authenticated user), matching the other lookups.
4. **Scope = form/write-path only.** No GET/list of observations, no swimmer-detail tab.
5. **`category_id` is a loose Guid at the EF level.** `observation_category` lives in the
   Identity module; the Health module does not FK across modules (same convention as
   `swimmer_id`/`recorded_by`). The diagram still shows it as a logical FK. Because it is
   cross-module and loose, the service does **not** validate category existence (same as it
   does not validate `swimmer_id`); the frontend only submits ids from the dropdown.

## 3. Data model

### 3.1 `reference.observation_category` (Identity module, `reference` schema)
Mirror `blood_type`.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | `Guid.NewGuid()` |
| `code` | varchar(50) | unique |
| `name_en` | varchar(100) | required |
| `name_ar` | varchar(100) | nullable |

Seeded rows (code → EN / AR):

| code | name_en | name_ar |
|---|---|---|
| allergy | Allergy | حساسية |
| surgery | Surgery | جراحة |
| chronic | Chronic | مرض مزمن |
| autoimmune | Autoimmune | مناعي ذاتي |
| composition | Composition | تركيب الجسم |
| flag | Flag | ملاحظة |
| other | Other | أخرى |

### 3.2 `health.observation` (Health module, `health` schema)
Mirror `health_reading`.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | ctor `Guid.NewGuid()` |
| `swimmer_id` | uuid | loose Guid, no cross-module FK |
| `category_id` | uuid | loose Guid, no cross-module FK (→ `reference.observation_category`) |
| `field_label` | varchar(200) | required |
| `value` | varchar(500) | required |
| `observed_date` | timestamptz | ctor `DateTime.UtcNow` = created moment |
| `recorded_by` | uuid | loose Guid = current user |

**Doc updates:** in both `docs/references/swimming-database-diagram.html` and
`docs/superpowers/specs/2026-08-30-swimming-database-design.md`, change
`observation.observed_date` from `date` → `timestamptz`. In the diagram's `IMPLEMENTED`
set, add `"reference.observation_category"` and `"health.observation"` (green dots).

## 4. Backend — reference lookup (Identity module)

Paths under `backend/src/Modules/Identity/`. Mirror the `BloodType` slice exactly.

- **Domain:** `Entities/ObservationCategory.cs` (Id, Code, NameEn, NameAr?; ctor trims,
  generates Id) — copy of `BloodType`. `Repositories/IObservationCategoryRepository.cs`
  (`Task<IReadOnlyList<ObservationCategory>> GetAllAsync(CancellationToken)`).
- **Infrastructure:** `Configurations/ObservationCategoryConfiguration.cs`
  (`ToTable("observation_category","reference")`, `Code` unique, lengths as §3.1);
  `Repositories/ObservationCategoryRepository.cs`; `DbSet<ObservationCategory>` on
  `IdentityDbContext`; `IdentitySeeder.EnsureObservationCategories` seeding the 7 rows
  (idempotent, matching `EnsureBloodTypes`); a `CreateObservationCategoryTable` migration
  (Identity.Infrastructure, `reference` schema); DI registration for the repository.
- **Application:** `ReferenceService.GetObservationCategoriesAsync` → `CodedLookupDto` list
  (reuse the existing `CodedLookupDto`); add to `IReferenceService`; add
  `ReferenceMessages.Success.ObservationCategoriesListed`.
- **Api:** add `GET /api/reference/observation-categories` to the existing
  `ReferenceController` (`[Authorize]`), returning `ApiResponse<IReadOnlyList<CodedLookupDto>>`.

## 5. Backend — observation write path (Health module)

Paths under `backend/src/Modules/Health/`. Mirror the `HealthReading` slice.

- **Domain:** `Entities/Observation.cs` — sealed; private EF ctor + public
  `Observation(Guid swimmerId, Guid categoryId, string fieldLabel, string value, Guid recordedBy)`
  setting `Id = Guid.NewGuid()`, `ObservedDate = DateTime.UtcNow`, trimming the two strings.
  `Repositories/IObservationRepository.cs` (`AddAsync`, `SaveChangesAsync`).
- **Application:**
  - `DTOs/ObservationDtos.cs`: `CreateObservationRequest(Guid SwimmerId, Guid CategoryId, string FieldLabel, string Value)`; `ObservationDto(Guid Id, Guid SwimmerId, Guid CategoryId, string FieldLabel, string Value, DateTime ObservedDate, Guid RecordedBy)`.
  - `Services/Interfaces/IObservationService.cs` — `CreateAsync(CreateObservationRequest, Guid recordedBy, CancellationToken) -> Task<ObservationDto>` (no nullable — no cross-module existence check).
  - `Services/ObservationService.cs` — construct `Observation`, persist, return DTO.
  - `Validators/CreateObservationRequestValidator.cs` — `SwimmerId`, `CategoryId` not empty; `FieldLabel`, `Value` not empty (+ max lengths 200 / 500).
  - `Resources/ObservationMessages.cs` — localized success/error strings.
- **Infrastructure:** `Configurations/ObservationConfiguration.cs`
  (`ToTable("observation","health")`, all loose Guids, `field_label`/`value` lengths,
  `observed_date` required); `Repositories/ObservationRepository.cs`;
  `DbSet<Observation>` on `HealthDbContext`; `CreateObservationTable` migration; DI
  registration for repository + service in `HealthModuleExtensions`.
- **Api:** `ObservationsController` — `[Route("api/observations")]`,
  `[Authorize(Roles="head_coach,captain")]`, one `POST` returning `201` with
  `ApiResponse<ObservationDto>`.

## 6. Frontend (Angular)

- **New feature slice** `features/observations/` (mirror `features/health-readings`):
  model, DTO + validation guard, repository interface+token, repository impl
  (`POST /api/observations`), `CreateObservationUseCase`, providers (registered in
  `app.config.ts`).
- **`features/reference`:** add `getObservationCategories()` to `IReferenceRepository` +
  its impl (`GET /api/reference/observation-categories`), and a
  `LoadObservationCategoriesUseCase` (reuse `CodedLookupListDtoRs` / `isCodedLookupListValid`).
- **Captain-panel page** `presentation/pages/swimmer-data/`:
  - `swimmer-data.page.ts` / `.html` — the "Swimmer Data Fields" form.
  - `swimmer-data.viewmodel.ts` — reuses `ListSwimmersUseCase` and
    `LoadObservationCategoriesUseCase` for the two dropdowns; holds `swimmerId`,
    `categoryId`, `fieldLabel`, `value` signals; `canSubmit` (all four non-empty);
    `submit()` calls `CreateObservationUseCase`, toasts success, clears `fieldLabel` +
    `value` (keeps swimmer + category selected for rapid entry); error toast on failure;
    submit disabled while in flight.
  - Export page + viewmodel from `features/captain-panel/index.ts`.
- **Routing:** add `captain-panel/swimmer-data` (`roleGuard('head_coach','captain')`,
  provides `SwimmerDataViewModel`). Set the `swimmerRecords` card's
  `route: '/captain-panel/swimmer-data'` in `captain-panel.page.ts` (card stays visible to
  both roles).
- **i18n:** add a `swimmerData.*` block (title, description, field labels — Select Swimmer,
  Select Category, Field Name, Value — placeholders, submit, toasts) to `en.json` + `ar.json`.

## 7. Data flow

1. Page load → viewmodel fires `ListSwimmersUseCase` (`GET /api/swimmers`) and
   `LoadObservationCategoriesUseCase` (`GET /api/reference/observation-categories`).
2. User picks swimmer + category, types field name + value → `canSubmit` enables.
3. Submit → `CreateObservationUseCase` → `POST /api/observations`
   `{ swimmerId, categoryId, fieldLabel, value }`.
4. Server stores the observation (`observed_date = UtcNow`, `recorded_by = current user`),
   returns `201 + ObservationDto`.
5. Viewmodel toasts success and clears field name + value.

## 8. Error handling

- Validation failures (empty ids/labels) → `400` via the existing validation filter +
  `ApiResponse` failure shape.
- Frontend shows an error toast on failure and keeps the form intact; submit disabled while
  in flight.

## 9. Testing

**Backend**
- `Identity.UnitTests`: `ObservationCategory` entity ctor; `ObservationCategoryRepository`
  `GetAllAsync` (InMemory); `ReferenceService.GetObservationCategoriesAsync` mapping.
- `Api.UnitTests`: `ReferenceController` new endpoint returns 200 (there is a
  `ReferenceControllerTests` precedent to extend).
- `Health.UnitTests`: `Observation` entity (stamps `ObservedDate` + `RecordedBy`);
  `ObservationService.CreateAsync` (persists + returns DTO); `CreateObservationRequestValidator`.
- `ArchitectureTests` stay green.

**Frontend**
- `create-observation.use-case.spec.ts`, `observation.repository.impl.spec.ts`.
- `load-observation-categories.use-case.spec.ts`.
- `swimmer-data.viewmodel.spec.ts` — dropdowns load, `canSubmit` gating, submit success
  (toast + clears field/value) and failure (error toast).

**No `ObservationsController` unit test** — the analogous `HealthReadingsController`/
`MedicalTestsController` have none (controllers calling `CurrentUserId()` need HttpContext
plumbing these unit tests lack); the service test covers behavior. (The `ReferenceController`
endpoint IS tested, since that controller has a test harness and does not use
`CurrentUserId()`.)

## 10. Out of scope (deferred)

- Any read/list endpoint for observations.
- Swimmer-detail page and its "Records" tab.
- Editing/deleting observations.
- Managing the `observation_category` catalog from the UI (seed-only, like other lookups).
