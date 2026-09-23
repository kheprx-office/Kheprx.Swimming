# Championships List page — Design

- **Date:** 2026-09-23
- **Status:** Approved (design decisions); implementation pending
- **Branch:** on local `main`; **no commits until the user says so**
- **Sibling / prior art:** `2026-09-23-swimmer-profile-attendance-design.md` & `2026-09-23-attendance-entry-page-design.md` (the `Attendance` module — the closest structural template for a brand-new read module + a `reference.*` lookup); `2026-09-22-swimmer-profile-feedback-design.md` (cross-module composition + name/lookup enrichment at the API layer)

## Context & goal

Add the first slice of the **Championships** area: a **read-only list** of championship events. The page already exists as a **placeholder** — `frontend/src/app/features/championships/presentation/pages/championships/championships.page.ts` renders through the app shell so the "Championships" nav item is navigable ("Feature content is intentionally not built yet"). The sidebar item (`layout.component.ts:106`), the `/championships` route (`app.routes.ts:36`), and the `shell.nav.championships` i18n keys (EN/AR) **already exist**. This task builds out the list body and everything behind it.

There is **no `Championships` backend module and no `championships.*` / `reference.competition_status` tables yet.** This task creates them, read-only, following the Attendance module recipe.

### Design references

- Mock: `Desktop/mcp/2.png` — the Championships list: a card with a **From date / To date** filter row + a right-aligned count, then a table with columns **Championship** (trophy icon + name), **Date** (range), **Location**, **Enrolled**, **Status** (Upcoming/Completed badge), plus a "Create Championship" button top-right.
- React prototype: `Desktop/4dba8937-…/src/pages/ChampionshipPage.tsx`, specifically the **grid/list block (~L802–998)**: the period (From/To) filter, the count line, and the events table. The rest of that file (event detail, competition days, race sessions, finished races, results, enrollment) is **out of scope** for this task.
- DB diagram: `docs/references/swimming-database-diagram.html` — `championships.competition_event` and `reference.competition_status`.

### Scope decisions (confirmed with the user)

1. **No `Enrolled` column** and **no `championships.championship_enrollment` table** this pass — deferred until the enrollment feature is built.
2. **No "Create Championship" button** — read-only list only.
3. **Seed target:** the live Aiven DB **`Swimming_Production`** (needs the DB password at run time).
4. **No role-based visibility filter** — every authenticated user sees all events (the prototype's swimmer-only-sees-enrolled filter is out of scope with enrollment).

## Data model (two new tables)

Both clone existing patterns exactly. EF convention here: **table + schema are snake_case via `ToTable(...)`; columns default to PascalCase property names** (confirmed by `AttendanceRecordConfiguration` and the attendance seed's `"Id"`/`"Code"` quoting).

### `reference.competition_status` (lookup — clone of `reference.attendance_status`)

| Column   | Type          | Notes                          |
|----------|---------------|--------------------------------|
| `Id`     | uuid (PK)     |                                |
| `Code`   | varchar(50)   | `upcoming` \| `completed`; unique index |
| `NameEn` | varchar(100)  | required                       |
| `NameAr` | varchar(100)  | nullable                       |

Lives in the **Identity** module (like `attendance_status`). Seeded in code via a new `IdentitySeeder.EnsureCompetitionStatuses` (upcoming/completed) so it self-populates at startup, matching every other reference lookup.

### `championships.competition_event`

| Column       | Type        | Notes                                             |
|--------------|-------------|---------------------------------------------------|
| `Id`         | uuid (PK)   |                                                   |
| `NameEn`     | varchar     | required                                          |
| `NameAr`     | varchar     | nullable                                          |
| `StartDate`  | date        | required (`DateOnly`)                             |
| `EndDate`    | date        | required (`DateOnly`)                             |
| `LocationEn` | varchar     | required                                          |
| `LocationAr` | varchar     | nullable                                          |
| `StatusId`   | uuid        | loose Guid → `reference.competition_status` (no cross-module FK) |
| `CreatedBy`  | uuid        | loose Guid → `identity.app_user`                 |

Like `attendance_record`, cross-module references are **loose Guids** — no EF navigation across module boundaries. `StatusId` is resolved to code/name at the API layer (see below).

## Key architectural decisions

### 1. New read-only `Championships` module (mirrors `Attendance`)

A new bounded-context module under `backend/src/Modules/Championships/` with the standard four projects — `Domain`, `Application`, `Infrastructure`, `Contracts` — plus its own `ChampionshipsDbContext` and migration, exactly like the Attendance module. Read-only for v1: the entity has a private EF constructor and no mutating methods (no create/update path yet).

- **Domain:** `CompetitionEvent` entity; `ICompetitionEventRepository.ListAsync(ct)` → all events ordered by `StartDate` **descending** (newest first).
- **Application:** `CompetitionEventDto`, `IChampionshipService.ListAsync(ct)`, `ChampionshipMessages` (EN/AR success strings, like `AttendanceMessages`).
- **Infrastructure:** `ChampionshipsDbContext` (+ design-time `ChampionshipsDbContextFactory`), `CompetitionEventConfiguration` (`ToTable("competition_event","championships")`), migration `CreateCompetitionEventTable`, `CompetitionEventRepository`, `AddChampionshipsModule(IServiceCollection, IConfiguration)` extension (registers `DbContext` on the `Postgres` connection string + repo + service).
- **Contracts:** empty shell project to match siblings.

### 2. Status resolved at the API layer (no cross-module dependency)

The list needs each event's status **code + names** for the badge. Following the exact seam already used in `AttendanceRecordsController` (which composes Identity's `IUserService` for recorder names), the **controller** — which references both modules — loads `reference.competition_status` via Identity's `IReferenceService.GetCompetitionStatusesAsync(ct)` and enriches each `CompetitionEventDto` with `StatusCode` / `StatusNameEn` / `StatusNameAr`. The Championships module gains **no** reference to Identity; `StatusId` stays a loose Guid.

`IReferenceService.GetCompetitionStatusesAsync` + its `ReferenceService` impl + repository (`ICompetitionStatusRepository` / `CompetitionStatusRepository`) + config are added in Identity, cloning the `AttendanceStatuses` members line-for-line.

### 3. `GET /api/championships` returns the whole list in one call

One endpoint, `[Authorize]` (any authenticated user). No paging/sorting params in v1 — the list is small and the client owns the date filter. Response items carry the enriched status fields so the frontend needs no second call to render badges.

### 4. `/api/reference/competition-statuses` added for pattern-completeness

A `GET /api/reference/competition-statuses` action is added to `ReferenceController` (clone of `AttendanceStatuses`), for consistency with every other lookup and to be ready for the future Create form. **The list page does not call it** (status names arrive enriched on the event rows); it is included so the reference surface stays uniform.

### 5. Date filter + range formatting are client concerns

The **From/To period filter** and the **count** are computed on the client from the full list (identical to the prototype's `periodEvents`): an event is shown when its run **overlaps** the chosen `[from, to]` window (`end >= from && start <= to`); empty dates show all. Date-**range formatting** (single-day collapses to one date; same-month ranges collapse the month) is a presentation helper, ported from the prototype's `formatRange`.

## API contracts

```
GET /api/championships                                   [Authorize]
→ ApiResponse<IReadOnlyList<CompetitionEventDto>>

CompetitionEventDto {
  id: Guid,
  nameEn: string, nameAr: string?,
  startDate: DateOnly, endDate: DateOnly,
  locationEn: string, locationAr: string?,
  statusId: Guid,
  statusCode: string,        // "upcoming" | "completed" — resolved at API layer
  statusNameEn: string,
  statusNameAr: string?
}

GET /api/reference/competition-statuses                  [Authorize]
→ ApiResponse<IReadOnlyList<CodedLookupDto>>             // { id, code, nameEn, nameAr }
```

Empty table → `data: []`. Both endpoints are read-only; no request bodies, no validation beyond auth.

## Backend wiring

- `Program.cs`: add `builder.Services.AddChampionshipsModule(builder.Configuration);` and `await app.ApplyChampionshipsMigrationsAsync();`.
- `MigrationExtensions.cs`: add `ApplyChampionshipsMigrationsAsync` (clone of `ApplyAttendanceMigrationsAsync`).
- Identity: register `ICompetitionStatusRepository`; add `CompetitionStatuses` `DbSet` + `EnsureCompetitionStatuses` to the seeder.

## Frontend design

Fill in `features/championships` using the same clean-architecture layering as `features/attendance`.

- **Domain:** `domain/model/championship.ts` (view model: `id, nameEn, nameAr, startDate, endDate, locationEn, locationAr, statusId, statusCode, statusNameEn, statusNameAr`); `domain/repositories/championships.repository.ts` (injection token + interface `getChampionships()`); `domain/usecases/load-championships.use-case.ts` (validates the DTO list, maps to the model).
- **Data:** `data/dto/competition-event.dto.ts` (`CompetitionEventDtoRs` + `isCompetitionEventListValid`), `data/dto/competition-event.mapper.ts`, `data/repositories/championships.repository.impl.ts` (HTTP `GET /api/championships`), `data/championships.providers.ts` (binds token → impl).
- **Presentation:** `ChampionshipsViewModel` (`@Injectable`, provided on the route like `AttendanceEntryViewModel`) with signals `loading`, `error`, `all` (loaded events), `filterFrom`, `filterTo`, and computeds `filtered` (overlap filter) + `count`. `championships.page.ts` calls `vm.load()` on init; `.html` renders:
  - a card with the **From date / To date** inputs (bound to the filter signals) + a "Show all" reset when either is set + a right-aligned "`N` championships" count;
  - a table: **Championship** (trophy chip + name EN/AR), **Date** (`formatRange`), **Location** (EN/AR), **Status** badge — color by `statusCode` (`upcoming` → info/blue, `completed` → success/green);
  - loading, empty ("No championships in this period"), and error states, matching the mock's styling.
- **i18n:** a new `championships.*` block in `en.json` / `ar.json` (page title, subtitle, column headers, From/To labels, count noun singular/plural, status labels, empty & error text). `shell.nav.championships` already exists.
- Names/locations render EN or AR by the active `LanguageStore`, like every other page.

## Error handling

- List load failure → `error` signal true → error state in the page body with a retry affordance; no toast needed for a read-only page.
- Empty DB → empty state.
- Auth: unauthenticated requests are already handled by the global auth guard/route guard; the endpoint is `[Authorize]`.

## Testing

**Backend** (xUnit — light, the service is a thin passthrough)
- Repository: `ListAsync` returns events ordered by `StartDate` desc.
- Controller/enrichment: each row's `StatusId` resolves to the correct `statusCode`/names; an unknown `StatusId` degrades gracefully (empty names, no throw).

**Frontend (Jest — not Karma)**
- Mapper: DTO → model field mapping (incl. null `nameAr`/`locationAr`).
- Repository impl: calls `GET /api/championships`, unwraps `ApiResponse`.
- Use case: rejects an invalid payload (`validation` `AppError`), maps a valid list.
- ViewModel: overlap date filter (event straddling / outside the window), `count`, "show all" reset, `loading`/`error` transitions.
- `formatRange` helper: single-day, same-month range, cross-month range.

## Out of scope (v1)

Enrolled count + `championship_enrollment`; Create/Edit/Delete championships; event detail, competition days, race sessions, finished races, results, enrollment; role-based visibility (swimmer-only-enrolled); paging/server sorting/search.

## Migrations / data

- **Two new migrations:** Identity `CreateCompetitionStatusTable` (`reference.competition_status`) and Championships `CreateCompetitionEventTable` (`championships.competition_event`).
- **Apply to Aiven `Swimming_Production`** via `dotnet ef database update --connection "…Swimming_Production…" --context {Identity,Championships}DbContext` (the running API must be **stopped** first — DLL lock; design-time factories hardcode local, so `--connection` is required). Needs the Aiven password at run time.
- **`scripts/seed-championships-aiven.sql`** (idempotent, PascalCase quoted columns like `seed-attendance-aiven.sql`): inserts the 2 fixed-id `competition_status` rows (`upcoming`, `completed`) `ON CONFLICT ("Code") DO NOTHING`, and the **3 demo events** from the mock — National Junior Championship (upcoming, 2023-11-15…16, Cairo Olympic Pool), Regional Sprint Meet (completed, 2023-10-20, Alexandria Sports Center), Winter Open Championship (upcoming, 2024-01-20…21, Giza Aquatic Center) — with `CreatedBy` = a head-coach `app_user`, `ON CONFLICT ("Id") DO NOTHING`. Reference statuses are also seeded in code by `EnsureCompetitionStatuses`; the SQL covers Aiven directly and gives the events something to reference.
