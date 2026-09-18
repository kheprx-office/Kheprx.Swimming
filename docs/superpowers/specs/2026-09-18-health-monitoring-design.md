# Health Monitoring — Log a Test Reading

**Date:** 2026-09-18
**Status:** Approved design (spec)
**Scope:** Write-path only — log a `health.health_reading`. No read/detail-page work this iteration.

## 1. Summary

Wire up the existing **Captain Panel → Health Monitoring** card (currently a disabled
"coming soon" tile) to a working **"Log a test reading"** form. A Head Coach or Captain
selects a swimmer, selects a medical test (whose normal range + unit are shown), enters a
value, and logs it. The server records the reading, stamping `reading_date` with the
creation moment and `recorded_by` with the current user.

Viewing readings (the "Health Monitoring tab on the swimmer's detail page" mentioned in the
design mockup) is **explicitly out of scope** — no swimmer-detail page exists yet; that is a
separate future feature.

**Design references:**
- UI prototype (visual reference only): `C:\Users\envnt\Desktop\4dba8937-ce3f-44bd-b093-515f10a5ea2b\`
- Approved screenshots: `C:\Users\envnt\Desktop\mcp\1.png` (Captain Panel card), `2.png` (form)
- DB table: `docs/references/swimming-database-diagram.html` → `health.health_reading`

This feature mirrors the just-built **Medical Tests** feature (commit `45744de`) layer for
layer; follow those files as the template.

## 2. Decisions (locked)

1. **Scope = form/write-path only.** POST endpoint + Captain Panel form. No GET/read
   endpoint, no swimmer-detail tab.
2. **Roles = Head Coach + Captain.** Both may log readings. This requires the medical-test
   catalog list to be readable by captains (see §5 auth change).
3. **`reading_date` = created moment.** Stored as **`timestamptz`** set to `DateTime.UtcNow`
   at creation (a true created-at, matching `medical_test.created_at`). No date input on the
   form. The DB diagram doc is updated from `date` → `timestamptz`.
4. **Derived status is included.** The POST response DTO carries an unstored, derived
   `status` (`normal` when `lower_bound ≤ value ≤ upper_bound`, else `out`) computed against
   the selected test's bounds, so the success toast can say "logged — within/outside normal
   range." It is **not** persisted (the `health_reading` table has no status column;
   severity is derived per the table note).

## 3. Data model — `health.health_reading`

New `HealthReading` entity + EF configuration + migration, mirroring `MedicalTest`.

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` PK | `Guid.NewGuid()` in ctor |
| `swimmer_id` | `uuid` | **loose Guid**, no cross-module FK (matches the `created_by`/`recorded_by` convention — health module does not FK into the identity module) |
| `medical_test_id` | `uuid` | **real FK** → `health.medical_test.id` (same schema/module) |
| `value` | `numeric(8,2)` | required |
| `reading_date` | `timestamptz` | set to `DateTime.UtcNow` at creation = the created moment |
| `recorded_by` | `uuid` | loose Guid = `CurrentUserId()` |

Notes:
- Default schema is `health` (set on `HealthDbContext`).
- The `medical_test_id` FK stays inside the `health` schema, so it is a genuine EF relationship
  (unlike `swimmer_id`/`recorded_by`, which follow the "loose Guid — no cross-module FK"
  comment already present in `MedicalTestConfiguration`).

**Doc update:** in `docs/references/swimming-database-diagram.html`, change the
`health_reading` `reading_date` column from `{c:"reading_date",t:"date",...}` to
`t:"timestamptz"`.

## 4. Backend layers (Health module)

Paths under `backend/src/Modules/Health/`.

**Domain** (`...Health.Domain`)
- `Entities/HealthReading.cs` — sealed entity. Private EF ctor + public ctor
  `HealthReading(Guid swimmerId, Guid medicalTestId, decimal value, Guid recordedBy)` that
  sets `Id = Guid.NewGuid()`, `ReadingDate = DateTime.UtcNow`, and assigns the rest. All
  setters `private`.
- `Repositories/IHealthReadingRepository.cs` — `AddAsync`, `SaveChangesAsync`
  (mirror `IMedicalTestRepository`; no list/get needed this iteration beyond what the service
  requires).

**Application** (`...Health.Application`)
- `DTOs/HealthReadingDtos.cs`:
  - `CreateHealthReadingRequest(Guid SwimmerId, Guid MedicalTestId, decimal Value)`
  - `HealthReadingDto(Guid Id, Guid SwimmerId, Guid MedicalTestId, decimal Value, DateTime ReadingDate, Guid RecordedBy, string Status)`
- `Services/Interfaces/IHealthReadingService.cs` — `CreateAsync(CreateHealthReadingRequest, Guid recordedBy, CancellationToken)`.
- `Services/HealthReadingService.cs` — loads the referenced `MedicalTest` via
  `IMedicalTestRepository.GetByIdAsync`; if missing, signals not-found (controller returns
  `404`/`400`). Otherwise constructs the `HealthReading`, persists it, derives `status` from
  the test bounds, returns the DTO.
- `Validators/CreateHealthReadingRequestValidator.cs` — `SwimmerId`/`MedicalTestId`
  not empty, `Value` provided (mirror `CreateMedicalTestRequestValidator`).
- `Resources/HealthReadingMessages.cs` — localized success/error messages
  (mirror `MedicalTestMessages`).

**Infrastructure** (`...Health.Infrastructure`)
- `Configurations/HealthReadingConfiguration.cs` — `ToTable("health_reading", "health")`,
  key, `numeric(8,2)` on `Value`, `timestamptz` on `ReadingDate`, real FK on
  `MedicalTestId` → `MedicalTest`, loose `SwimmerId`/`RecordedBy`.
- `Data/HealthDbContext.cs` — add `DbSet<HealthReading> HealthReadings`.
- `Repositories/HealthReadingRepository.cs` — implements the repo interface.
- `Migrations/<timestamp>_CreateHealthReadingTable.cs` — generated via EF tools
  (stop the dev server first — DLL lock, see the `dotnet-test-dev-server-lock` note).
- `Extensions/HealthModuleExtensions.cs` — register
  `IHealthReadingRepository`/`IHealthReadingService`.

**Api** (`backend/Kheprx.BaseBackend.Api/Controllers`)
- `HealthReadingsController.cs` — `[Route("api/health-readings")]`,
  `[Authorize(Roles = "head_coach,captain")]`, `POST` only. Calls
  `service.CreateAsync(request, CurrentUserId(), ct)`, returns `201` with
  `ApiResponse<HealthReadingDto>`. Unknown test → `404`/`400` failure envelope.

**Auth change (existing controller):** `MedicalTestsController` currently carries
`[Authorize(Roles = "head_coach")]` at the controller level. Move the gate to the actions:
- `List` → `[Authorize(Roles = "head_coach,captain")]`
- `Create` / `Delete` → `[Authorize(Roles = "head_coach")]`

This lets a Captain load the test catalog inside the Health Monitoring form without gaining
create/delete rights.

## 5. Frontend (Angular)

**New feature slice** `frontend/src/app/features/health-readings/` (clean-architecture,
mirror `features/medical-tests`):
- `domain/model/health-reading.ts` — `HealthReading` (+ `CreateHealthReadingInput`).
- `domain/repositories/health-reading.repository.ts` — interface + injection token.
- `domain/usecases/create-health-reading.use-case.ts` — `CreateHealthReadingUseCase`.
- `data/dto/health-reading.dto.ts` — request/response DTO types + a validation guard.
- `data/repositories/health-reading.repository.impl.ts` — `POST /api/health-readings`.
- `data/health-reading.providers.ts` — bind token → impl.
- `index.ts` barrel + `testing/` specs.

**Captain-panel page** `features/captain-panel/presentation/pages/health-monitoring/`:
- `health-monitoring.page.ts` / `.html` — the "Log a test reading" form.
- `health-monitoring.viewmodel.ts` — **reuses** `ListSwimmersUseCase` (from
  `features/swimmers`) and `ListMedicalTestsUseCase` (from `features/medical-tests`) to
  populate the two dropdowns; holds `selectedSwimmerId`, `selectedTestId`, `value` signals;
  `canSubmit` computed (swimmer + test selected, value is a finite number); `submit()` calls
  `CreateHealthReadingUseCase`, shows a success toast reflecting `status`, resets the form;
  error toast on failure; submit disabled while in flight. Mirror `MedicalTestsViewModel`.
- Export the page + viewmodel from `features/captain-panel/index.ts`.

**Routing** (`frontend/src/app/app.routes.ts`): add
```
{
  path: 'captain-panel/health-monitoring',
  canActivate: [firstLoginGuard, roleGuard('head_coach', 'captain')],
  loadComponent: () => import('@features/captain-panel').then((m) => m.HealthMonitoringPage),
  providers: [HealthMonitoringViewModel],
}
```

**Card wiring** (`captain-panel.page.ts`): set the existing `healthMonitoring` card's
`route: '/captain-panel/health-monitoring'`. The card stays visible to both head_coach and
captain (no filter change — the `/captain-panel` route is already guarded to those roles).

**i18n** (`core/i18n/en.json` + `ar.json`): `captainPanel.cards.healthMonitoring.*` already
exist. Add a `healthMonitoring.*` block for the page: title/subtitle, field labels (Select
Swimmer, Test, Value), the "normal range" helper, the Log button, and toasts
(`created` with within/outside variants, `createFailed`).

**Providers registration:** add the health-readings data providers wherever feature data
providers are registered (follow how `medical-test.providers` / `swimmer.providers` are
wired into the app config).

## 6. Data flow

1. Page loads → viewmodel fires `ListSwimmersUseCase` (`GET /api/swimmers`) and
   `ListMedicalTestsUseCase` (`GET /api/medical-tests`).
2. User picks a swimmer + a test; the selected test's `lowerBound`/`upperBound`/`unit`
   render as the "normal range" helper next to the value input.
3. User enters a value → `canSubmit` enables Log.
4. Submit → `CreateHealthReadingUseCase` → `POST /api/health-readings` `{ swimmerId, medicalTestId, value }`.
5. Server validates, loads the test, stores the reading (`reading_date = UtcNow`,
   `recorded_by = current user`), derives `status`, returns `201 + HealthReadingDto`.
6. Viewmodel toasts success (within/outside normal range) and resets the form.

## 7. Error handling

- Unknown `medicalTestId` → `404`/`400` failure envelope; frontend shows an error toast,
  form intact.
- Validation failures (empty ids, non-numeric value) → `400` via the existing validation
  filter + `ApiResponse` failure shape.
- Submit is disabled while a request is in flight.

## 8. Testing

**Backend**
- `Health.UnitTests`: `HealthReadingService` — `CreateAsync` sets `ReadingDate ≈ UtcNow` and
  `RecordedBy`, derives `status` correctly at/inside/outside bounds, and the test-not-found
  path. Validator tests for `CreateHealthReadingRequestValidator`.
- `Api.UnitTests`: `HealthReadingsController` returns `201` on success; role authorization
  present.
- `ArchitectureTests` must stay green (layer-dependency rules).

**Frontend**
- `create-health-reading.use-case.spec.ts`
- `health-reading.repository.impl.spec.ts`
- `health-monitoring.viewmodel.spec.ts` — dropdowns load, `canSubmit` gating, submit success
  (toast + reset) and failure (error toast).

## 9. Out of scope (deferred)

- Any read/list endpoint for readings (`GET .../health-readings`).
- Swimmer-detail page and its "Health Monitoring" tab.
- Editing/deleting readings.
- Persisting a status/severity column.
