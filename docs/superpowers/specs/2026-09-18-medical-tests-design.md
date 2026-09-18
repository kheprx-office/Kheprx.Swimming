# Medical Tests Catalog — Design

**Date:** 2026-09-18
**Status:** Draft for review
**Type:** Full-stack feature slice — **new `Health` backend module** + Angular Captain-Panel sub-page

## Goal

Let a Head Coach manage a **Medical Test catalog** from the Captain Panel: add a
test (name, unit, normal bounds), see the list of tests, and delete a test. This is
the first feature of the `health` schema and stands up the codebase's first second
backend module. It matches the provided React/Magic-Patterns reference
(`mcp/1.png`, `mcp/2.png`) and the `health.medical_test` table in the database
diagram.

## Decisions (agreed during brainstorming)

1. **Catalog CRUD only** — add / list / delete. The mockup's **"Flagged Readings"**
   column is **dropped**: it depends on `health_reading` data and a separate Health
   Monitoring / "Log a reading" feature that does not exist yet. It returns with that
   feature (its own spec).
2. **New `Health` backend module.** `medical_test` belongs to the `health` schema.
   Rather than fold it into `Identity` (the pragmatic choice used for `athlete` /
   `reference` tables in earlier phases), we stand up a real `Health` module — the
   codebase's **first** second module and second `DbContext`. This is net-new
   infrastructure (see Backend below).
3. **`created_by` is a loose `Guid`, not a cross-module FK.** `medical_test.created_by`
   conceptually points at `identity.app_user.id`, but with a separate `HealthDbContext`
   we store it as a plain `Guid` (the head coach's id from the JWT) with **no EF
   navigation and no DB-level FK constraint** to Identity's table. Modules reference
   each other by id only — the modular-monolith norm. A raw cross-schema SQL FK would
   recreate exactly the coupling the module boundary exists to avoid, so we skip it.
4. **Both names required.** `name_en` **and** `name_ar` are required at the
   application level (validator **and** form). The reference diagram marks `name_ar`
   nullable; since we own the new migration we make the column **`NOT NULL`** to match
   the "both required" rule.
5. **Head-Coach only.** The DB note calls `medical_test` "headCoach-managed" and the
   React reference gates management on `role === 'headCoach'`. The API is
   `[Authorize(Roles = "head_coach")]`; the route uses `roleGuard('head_coach')`; the
   Captain-Panel card is shown only to head coaches. (Narrower than the panel's own
   `head_coach,captain` guard.)
6. **No edit action.** The mockup offers add + delete only. Editing an existing test
   is out of scope (YAGNI); a test is deleted and re-added if wrong.
7. **Hard delete.** Safe today because no `health_reading` rows reference a test yet.
   When readings exist, a guard (block-or-cascade) is added with that feature.
8. **Free-text `unit`.** A plain string (`varchar`), matching the diagram.

## Non-goals

- No `health_reading` / "Flagged Readings" count / Health Monitoring page.
- No edit of existing tests.
- No name-uniqueness constraint (the diagram has none).
- No cross-module DB FK from `health.medical_test` to `identity.app_user`.
- No pagination / search on the catalog (small, head-coach-authored list).
- No captain (view-only) access — head coach only for now.

---

## Backend — new `Health` module

The module mirrors `Identity`'s project layout. There is only one module today
(`Identity`, one `IdentityDbContext`, migrated at startup by
`ApplyIdentityMigrationsAsync`), so all module wiring below is new.

### Projects (mirror `src/Modules/Identity/…`)

`src/Modules/Health/`:
- `Kheprx.BaseBackend.Health.Domain`
- `Kheprx.BaseBackend.Health.Application`
- `Kheprx.BaseBackend.Health.Contracts` — created for layout symmetry; minimal/empty
  now unless a cross-module contract surfaces.
- `Kheprx.BaseBackend.Health.Infrastructure`

They reference `SharedKernel` and reuse the Api's cross-cutting pipeline
(FluentValidation auto-validation, `ApiResponse<T>`, JWT auth, error middleware,
localization via `AppLanguage.Current`) unchanged — same as `Identity`.

### Domain

**`Entities/MedicalTest.cs`**

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | PK, set in ctor |
| `NameEn` | `string` | required |
| `NameAr` | `string` | required (app rule; column `NOT NULL`) |
| `Unit` | `string` | required |
| `LowerBound` | `decimal` | `numeric(8,2)` |
| `UpperBound` | `decimal` | `numeric(8,2)` |
| `CreatedBy` | `Guid` | head coach id; **no FK/navigation** |
| `CreatedAt` | `DateTime` | UTC, set in ctor |

Ctor: `MedicalTest(string nameEn, string nameAr, string unit, decimal lowerBound,
decimal upperBound, Guid createdBy)` — assigns `Id`, sets `CreatedAt`.

**`Repositories/IMedicalTestRepository.cs`**
- `Task<IReadOnlyList<MedicalTest>> ListAsync(CancellationToken ct = default)` — ordered by `NameEn`.
- `Task AddAsync(MedicalTest test, CancellationToken ct = default)`
- `Task<MedicalTest?> GetByIdAsync(Guid id, CancellationToken ct = default)`
- `Task RemoveAsync(MedicalTest test, CancellationToken ct = default)`
- `Task SaveChangesAsync(CancellationToken ct = default)`

### Infrastructure

- **`Data/HealthDbContext.cs`** — `DbSet<MedicalTest> MedicalTests`; applies configs
  from the assembly in `OnModelCreating`.
- **`Data/HealthDbContextFactory.cs`** — design-time factory for `dotnet ef`
  (mirrors `IdentityDbContextFactory`; reads the `Postgres` connection string).
- **`Configurations/MedicalTestConfiguration.cs`** — `ToTable("medical_test","health")`;
  `NameEn`/`NameAr` `HasMaxLength(200)` required; `Unit` `HasMaxLength(50)` required;
  `LowerBound`/`UpperBound` `HasColumnType("numeric(8,2)")` required; `CreatedBy`
  required `uuid` (**no FK/navigation**); `CreatedAt` required.
- **`Repositories/MedicalTestRepository.cs`** — EF impl on `HealthDbContext`:
  `ListAsync` = `AsNoTracking().OrderBy(t => t.NameEn).ToListAsync()`; `AddAsync`;
  `GetByIdAsync`; `RemoveAsync`; `SaveChangesAsync`.
- **`Extensions/HealthModuleExtensions.cs`** — `AddHealthModule(services, configuration)`:
  `AddDbContext<HealthDbContext>(o => o.UseNpgsql(configuration.GetConnectionString("Postgres")))`;
  `AddScoped<IMedicalTestRepository, MedicalTestRepository>()`;
  `AddScoped<IMedicalTestService, MedicalTestService>()`.
- **`Data/HealthSeeder.cs`** — `SeedAsync(HealthDbContext db)`, idempotent (guard on
  `MedicalTests.AnyAsync`). Seeds the mockup's three demo tests:
  Hemoglobin `g/dL` 11–17.5; Glucose (FBS) `mg/dL` 70–110; Uric Acid `mg/dL` 1–7
  (Arabic names filled in). `CreatedBy` = a documented seed-constant `Guid` — valid
  because there is no FK; this is regenerable demo data.

### Application

- **`DTOs/MedicalTestDtos.cs`**
  ```csharp
  public sealed record CreateMedicalTestRequest(
      string NameEn, string NameAr, string Unit,
      decimal LowerBound, decimal UpperBound);

  public sealed record MedicalTestDto(
      Guid Id, string NameEn, string NameAr, string Unit,
      decimal LowerBound, decimal UpperBound, DateTime CreatedAt);
  ```
- **`Validators/CreateMedicalTestRequestValidator.cs`** (FluentValidation, localized via
  `AppLanguage.Current`, mirroring `UserRequestValidators` / `CreateSwimmerRequestValidator`):
  - `NameEn` NotEmpty, MaxLength 200.
  - `NameAr` NotEmpty, MaxLength 200.
  - `Unit` NotEmpty, MaxLength 50.
  - `UpperBound` `GreaterThan(x => x.LowerBound)` — the mockup's "upper must exceed lower".
- **`Services/Interfaces/IMedicalTestService.cs`** / **`Services/MedicalTestService.cs`**
  - `Task<IReadOnlyList<MedicalTestDto>> ListAsync(CancellationToken ct = default)` — repo list → dto.
  - `Task<MedicalTestDto> CreateAsync(CreateMedicalTestRequest request, Guid createdBy, CancellationToken ct = default)`
    — `new MedicalTest(...)` → `AddAsync` → `SaveChangesAsync` → dto.
  - `Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)` — `GetByIdAsync`;
    `null` → `false` (controller → 404); else `RemoveAsync` + `SaveChangesAsync` → `true`.
- **`MedicalTestMessages.cs`** — localized EN + AR: `MedicalTestsListed`,
  `MedicalTestCreated`, `MedicalTestDeleted`, `MedicalTestNotFound`.

### API (`Kheprx.BaseBackend.Api`)

- **`Controllers/MedicalTestsController.cs`** — `[ApiController]`,
  `[Route("api/medical-tests")]`, `[Authorize(Roles = "head_coach")]`:
  - `GET` → 200 `ApiResponse<IReadOnlyList<MedicalTestDto>>` (`MedicalTestsListed`).
  - `POST` (body `CreateMedicalTestRequest`) → 201 `ApiResponse<MedicalTestDto>`
    (`MedicalTestCreated`); `createdBy = CurrentUserId` (the existing controller helper).
    Invalid body → 400 via the FluentValidation pipeline.
  - `DELETE /{id}` → 200 `ApiResponse` success (`MedicalTestDeleted`), or 404
    `ApiResponse.Failure` (`MedicalTestNotFound`) when the service returns `false`.
  Follows the `SwimmersController` / `UsersController` envelope conventions.
- **`Program.cs`** — add `builder.Services.AddHealthModule(builder.Configuration);`
  and `await app.ApplyHealthMigrationsAsync();` (after the identity one).
- **`Extensions/Data/MigrationExtensions.cs`** — add `ApplyHealthMigrationsAsync`:
  resolve `HealthDbContext`, `await db.Database.MigrateAsync()`, then
  `await HealthSeeder.SeedAsync(db)`.
- Api `.csproj` references `Kheprx.BaseBackend.Health.Infrastructure` (+ transitive
  layers), mirroring the Identity reference.

### Migration

- `dotnet ef migrations add CreateMedicalTestTable --context HealthDbContext`
  `--project src/Modules/Health/…Health.Infrastructure --startup-project Kheprx.BaseBackend.Api`.
- **Safety pre-check:** `dotnet ef migrations has-pending-model-changes --context HealthDbContext`.
  Confirm the generated `Up` creates **only** schema `health` + table `medical_test`
  (and its columns). If anything else appears, **stop and consult**.
- Auto-applied at startup by `ApplyHealthMigrationsAsync`, then `HealthSeeder` runs.
- **Dev API server must be stopped** before `dotnet ef` / build / test — the running
  server locks the build-output DLLs. (See memory: *dotnet test dev-server lock*.)

### Testing (backend)

New `tests/Kheprx.BaseBackend.Health.UnitTests` (mirrors `Identity.UnitTests`; xUnit + Moq):
- `MedicalTest` entity — ctor assigns `Id` + `CreatedAt`, stores fields incl. `CreatedBy`.
- `CreateMedicalTestRequestValidator` — each required field; `MaxLength`; `UpperBound > LowerBound`
  (equal and inverted both fail).
- `MedicalTestService` — `CreateAsync` builds the entity with `createdBy` and returns
  the dto (mocked repo); `ListAsync` maps rows → dtos; `DeleteAsync` → `false` when
  missing, `true` + `RemoveAsync` called when present.
- `MedicalTestsController` — `GET` 200 list; `POST` 201; `DELETE` 200 vs 404.
- Confirm `Kheprx.BaseBackend.ArchitectureTests` layering rules cover the new module
  (Domain has no Infrastructure dependency, etc.); extend the test's module list if it
  enumerates modules explicitly.

---

## Frontend (Angular)

### New data/domain slice — `features/medical-tests/`

Mirrors the `swimmers/` slice (data → domain), consumed by a Captain-Panel page (the
same split used by `account-creation`, whose page lives under `captain-panel` while
its data/domain live in `swimmers`/`coaches`).

- **`data/dto/medical-test.dto.ts`**
  - `MedicalTestDtoRs { id; nameEn; nameAr; unit; lowerBound; upperBound; createdAt }`.
  - `MedicalTestListDtoRs extends BaseResponseRs<MedicalTestDtoRs[]>`.
  - `MedicalTestItemDtoRs extends BaseResponseRs<MedicalTestDtoRs>`.
  - `CreateMedicalTestDtoRq { nameEn; nameAr; unit; lowerBound; upperBound }`.
  - `isMedicalTestDtoRsValid(...)` guard (consistent with `swimmer-*.dto.ts`).
- **`domain/model/medical-test.ts`** — `MedicalTest { id; nameEn; nameAr; unit; lowerBound; upperBound; createdAt }`.
- **`domain/repositories/medical-test.repository.ts`** — `IMedicalTestRepository`:
  `list(): Promise<MedicalTestListDtoRs>`, `create(rq): Promise<MedicalTestItemDtoRs>`,
  `delete(id: string): Promise<BaseResponseRs<unknown>>`.
- **`data/repositories/medical-test.repository.impl.ts`** —
  `list()` → `http.get('/api/medical-tests')`; `create(rq)` → `http.post('/api/medical-tests', { body: rq })`;
  `delete(id)` → `http.delete('/api/medical-tests/' + id)`.
- **`data/medical-test.providers.ts`** — DI provider binding interface → impl (like `swimmer.providers.ts`).
- **`domain/usecases/`**
  - `list-medical-tests.use-case.ts` — repo.list, validate `res.data`, map DTO[] → `MedicalTest[]`.
  - `create-medical-test.use-case.ts` — `UseCase<CreateMedicalTestDtoRq, MedicalTest>`,
    validate `res.data` (`AppError(...,'validation')`), map → `MedicalTest`.
  - `delete-medical-test.use-case.ts` — `UseCase<string, void>` calling `repo.delete(id)`.
- **`index.ts`** barrel.

### Page — `features/captain-panel/presentation/pages/medical-tests/`

- **`medical-tests.page.ts` / `.html`** + **`medical-tests.viewmodel.ts`**
  (`@Injectable()`, route-provided, signals — no Reactive Forms, matching
  `register-swimmer.viewmodel.ts`):
  - Inject `ListMedicalTestsUseCase`, `CreateMedicalTestUseCase`,
    `DeleteMedicalTestUseCase`, `NotificationService`, `TranslateService`.
  - State signals: `tests`, `loading`, `error`; form signals `nameEn`, `nameAr`,
    `unit`, `lowerBound`, `upperBound`; `submitting`.
  - `canSubmit = computed(...)` — names + unit non-empty, `lowerBound`/`upperBound`
    parse as numbers, **`upperBound > lowerBound`**.
  - constructor `load()` → list into `tests`.
  - `submit()` → `CreateMedicalTestUseCase` → on success **prepend** the new row,
    reset the form, `NotificationService.success`; on failure surface via
    `NotificationService.error`.
  - `remove(id)` → `DeleteMedicalTestUseCase` → on success drop the row + toast; on
    404 refresh the list + info toast.
- **Layout** (mockup 2; built with the app's own Angular/Tailwind design tokens —
  **not** a verbatim React port):
  - Header — title + description (i18n).
  - **"Add a test"** panel — Test Name (EN), Test Name (AR), Unit, Lower Bound,
    Upper Bound, **Add Test** button, helper: "Names and bounds are required; upper
    must exceed lower."
  - **Table** — columns: Test (name by active language) · Unit · Lower · Upper ·
    delete-✕. **No "Flagged Readings" column** (deferred).
  - Loading skeleton, empty state, error state; RTL + name-by-language
    (`LanguageStore.lang()` → `nameAr` when `ar`). Both names always present, so the
    language switch never shows a blank name.
- **Reuses the existing `TextFieldComponent`** for text and numeric inputs — no new
  shared form controls needed.

### Hub + routing

- **`captain-panel.page.*`** — add the **"Medical Tests"** card (mockup 1), rendered
  only when the current role is `head_coach`, linking to `captain-panel/medical-tests`.
- **`app.routes.ts`** — child route `captain-panel/medical-tests`, guard
  `roleGuard('head_coach')` (narrower than the panel's `head_coach,captain`), lazy-loads
  `MedicalTestsPage`; route `providers` supply the three usecases +
  `provideMedicalTestRepository()`.

### i18n

New `medicalTests` namespace in `en.json` + `ar.json`: page title/description, every
field label + placeholder, the helper text, the Add Test button, table headers, the
empty/loading/error text, and the success/error toast messages.

---

## Data flow

```
Medical Tests page (captain-panel/medical-tests · roleGuard head_coach)
  load   → ListMedicalTestsUseCase → GET /api/medical-tests → tests signal
  submit → CreateMedicalTestUseCase{ nameEn, nameAr, unit, lowerBound, upperBound }
         → MedicalTestRepository.create → POST /api/medical-tests  [Authorize head_coach]
           → MedicalTestsController.Create → MedicalTestService.CreateAsync(req, CurrentUserId)
             → new MedicalTest(...) → AddAsync → SaveChangesAsync
           → 201 ApiResponse<MedicalTestDto>
         → prepend row + reset form + success toast
  remove → DeleteMedicalTestUseCase → DELETE /api/medical-tests/{id}
           → MedicalTestService.DeleteAsync → 200 (deleted) | 404 (not found)
         → drop row / info toast
```

## Error handling

- **Validation both sides:** client `canSubmit` gate + server 400 FluentValidation
  pipeline; `upper > lower` enforced in both.
- **Delete 404** (row already gone): refresh the list + info toast — no hard error.
- **Transport / malformed response:** usecases return `Result` / `AppError`; the VM
  maps to `NotificationService.error` + the error state (existing `toUserMessage`
  pattern).
- **Auth:** non-head-coach never sees the card and is blocked by `roleGuard` /
  `[Authorize(Roles="head_coach")]` (403).

## Testing

**Backend** — see the Backend › Testing subsection (entity, validator, service,
controller, architecture rules).

**Frontend (Jest + TestBed):**
- Usecase specs — `list` (maps + validates; malformed → `validation`), `create`
  (maps; malformed → `validation`), `delete`.
- `medical-test.repository.impl` spec — correct URL / verb / body for list, create, delete.
- `medical-tests.viewmodel` spec — `load()` populates `tests`; `canSubmit` gates on
  the required set incl. `upper > lower`; `submit()` success prepends + toasts;
  `remove()` drops the row.
- `medical-tests.page` render spec — table rows, empty, loading, error states; form present.
- `captain-panel.page` spec — the Medical Tests card is shown for `head_coach` and navigates.

## Files

**Backend — new**
- `src/Modules/Health/…Health.Domain/Entities/MedicalTest.cs`,
  `…Health.Domain/Repositories/IMedicalTestRepository.cs`
- `…Health.Application/DTOs/MedicalTestDtos.cs`,
  `…Health.Application/Validators/CreateMedicalTestRequestValidator.cs`,
  `…Health.Application/Services/Interfaces/IMedicalTestService.cs`,
  `…Health.Application/Services/MedicalTestService.cs`,
  `…Health.Application/MedicalTestMessages.cs`
- `…Health.Contracts/` (project shell)
- `…Health.Infrastructure/Data/HealthDbContext.cs`,
  `…Health.Infrastructure/Data/HealthDbContextFactory.cs`,
  `…Health.Infrastructure/Data/HealthSeeder.cs`,
  `…Health.Infrastructure/Configurations/MedicalTestConfiguration.cs`,
  `…Health.Infrastructure/Repositories/MedicalTestRepository.cs`,
  `…Health.Infrastructure/Extensions/HealthModuleExtensions.cs`,
  `…Health.Infrastructure/Migrations/*_CreateMedicalTestTable.*`
- `Kheprx.BaseBackend.Api/Controllers/MedicalTestsController.cs`
- `tests/Kheprx.BaseBackend.Health.UnitTests/…` (entity, validator, service, controller)
- 4 new `.csproj` (+ added to the solution)

**Backend — edit**
- `Kheprx.BaseBackend.Api/Program.cs` (register module + apply migrations)
- `Kheprx.BaseBackend.Api/Extensions/Data/MigrationExtensions.cs` (`ApplyHealthMigrationsAsync`)
- `Kheprx.BaseBackend.Api/Kheprx.BaseBackend.Api.csproj` (project reference)
- `Kheprx.BaseBackend.ArchitectureTests` (module coverage, if enumerated)

**Frontend — new**
- `features/medical-tests/data/dto/medical-test.dto.ts`,
  `…/data/repositories/medical-test.repository.impl.ts`,
  `…/data/medical-test.providers.ts`,
  `…/domain/model/medical-test.ts`,
  `…/domain/repositories/medical-test.repository.ts`,
  `…/domain/usecases/list-medical-tests.use-case.ts`,
  `…/domain/usecases/create-medical-test.use-case.ts`,
  `…/domain/usecases/delete-medical-test.use-case.ts`,
  `…/index.ts`
- `features/captain-panel/presentation/pages/medical-tests/medical-tests.page.ts` + `.html`,
  `…/medical-tests.viewmodel.ts`
- Matching `testing/` specs for the above.

**Frontend — edit**
- `app.routes.ts` (child route + guard + providers)
- `features/captain-panel/presentation/pages/captain-panel/captain-panel.page.*` (Medical Tests card)
- `features/captain-panel/index.ts` (export the page if the barrel lists pages)
- `core/i18n/…/en.json`, `ar.json` (`medicalTests` namespace)

## Risks / notes

- **First second module.** This establishes the second `DbContext` + migration
  pipeline + module registration. Follow the Identity project layout exactly so the
  pattern is reusable for future `health` tables (`health_reading`, `inbody_reading`,
  `observation`, `feedback_entry`).
- **`created_by` without a DB FK** is deliberate (module decoupling). An orphan id is
  possible only if a head-coach user is deleted; acceptable — the column is
  provenance metadata, not a relational constraint.
- **Role-claim authz** — `[Authorize(Roles = "head_coach")]` assumes the JWT carries
  the role code in the role claim (as existing `[Authorize(Roles="…")]` usages imply).
  Verify during implementation.
- **`name_ar` NOT NULL** diverges from the diagram's nullable marking — intentional,
  to enforce "both required". Note it against the diagram if the diagram is kept
  authoritative.
- **Deferred "Flagged Readings"** changes the page from the mockup (one fewer column).
  It returns with the Health Monitoring feature, at which point `medical_test` delete
  also needs a readings guard.
- **Dev-server DLL lock** — stop the API before `dotnet ef` / build / test.
</content>
</invoke>
