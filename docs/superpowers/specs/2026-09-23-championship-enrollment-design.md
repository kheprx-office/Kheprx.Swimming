# Championship Detail page + Enrollment tab — Design

- **Date:** 2026-09-23
- **Status:** Approved (design decisions); implementation pending
- **Branch:** `feat/championships`; **no commits until the user says so**
- **Sibling / prior art:**
  - `2026-09-23-championships-list-design.md` — the list page + the `Championships` module this builds on (created the `competition_event` table, `IChampionshipService`, `ChampionshipsController`, the frontend `features/championships` slice).
  - `2026-09-19-swimmer-profile-identity-vitals-design.md` — the closest template for a **routed, tabbed detail page** (`/swimmers/:id`) with a full tab strip where only some tabs are enabled.
  - `2026-09-19-swimmer-profile-guardian-design.md` — the **atomic upsert** pattern (`GET` current state + `PUT` the whole set) that Enrollment's Save mirrors.

## Context & goal

The `Championships` list page (`/championships`) is built and read-only. This task adds the **event detail page** reached by clicking a list row, and implements its **first tab — Enrollment**: picking which swimmers are entered in a championship event.

Detail navigation uses a **dedicated route `/championships/:id`** (a tabbed detail page, matching `/swimmers/:id`), *not* the in-page master/detail of the React prototype. The detail page shows the **full four-tab strip** (Enrollment · Competition Days · Finished races · Results) with **only Enrollment enabled**; the other three are rendered visibly disabled, exactly like the `swimmer-profile` tab strip.

The one new table this pass is `championships.championship_enrollment`.

### Design references

- Mocks: `Desktop/mcp/2.png` (event detail header + tab strip + start of Swimmer Enrollment) and `Desktop/mcp/3.png` (the full Swimmer Enrollment checklist: `N / total` counter, Save button, search box, one row per swimmer with checkbox · avatar initials · name · `club • age yrs`).
- React prototype: `Desktop/4dba8937-…/src/pages/ChampionshipPage.tsx` — the **event-detail block** (tab strip `SegmentedControl` ~L1026–1059) and the **Enrollment view** (`eventSubView === 'enrollment'` ~L1809–1918). Everything else in that file (competition days, race sessions, finished races, results) is **out of scope**.
- DB diagram: `docs/references/swimming-database-diagram.html` — `championships.championship_enrollment`.

### Scope decisions (confirmed with the user)

1. **Navigation:** dedicated route `/championships/:id` with a tab bar (matches `/swimmers/:id`). Not in-page master/detail.
2. **Tab strip now:** render the full 4-tab strip; **Enrollment enabled, the other three disabled** ("coming soon" affordance via disabled styling).
3. **Enrollment only** this pass — Competition Days / Finished races / Results are not built.
4. **Save semantics:** batch **atomic replace** of the event's enrollment set (like the guardians upsert), not per-swimmer instant toggle.
5. **Editing gated to `head_coach` / `captain`** (`canManage`), matching the list's Create button.
6. **Seed target:** the live Aiven DB **`Swimming_Production`** (needs the DB password at run time).

## Data model (one new table)

Exactly the diagram's columns — **nothing extra** (no `created_by`, no timestamp). EF convention here: **table + schema snake_case via `ToTable(...)`; columns default to PascalCase property names** (confirmed by `CompetitionEventConfiguration` and the seed's `"Id"` quoting).

### `championships.championship_enrollment`

| Column       | Type      | Notes                                                              |
|--------------|-----------|-------------------------------------------------------------------|
| `Id`         | uuid (PK) |                                                                   |
| `EventId`    | uuid      | loose Guid → `championships.competition_event` (no cross-context FK) |
| `SwimmerId`  | uuid      | loose Guid → `identity.swimmer_profile` (no cross-context FK)      |

- **Unique index on `(EventId, SwimmerId)`** — "one enrollment per swimmer per event" (the diagram's `U` constraint).
- Cross-module ids are **loose Guids**, matching `CompetitionEvent.StatusId`/`CreatedBy`. No EF navigation, and — since the modules use separate `DbContext`s — **no DB-level FK is emitted** (same as the existing `competition_event` loose Guids). The frontend only ever sends swimmer ids from the roster, so orphan rows are not a practical concern; the unique index is the one invariant enforced in the DB.

## Key architectural decisions

### 1. `championship_enrollment` slice inside the existing Championships module

No new module — extend `backend/src/Modules/Championships/`:

- **Domain:** `ChampionshipEnrollment` entity (`Id`, `EventId`, `SwimmerId`; private EF ctor + `public ChampionshipEnrollment(Guid eventId, Guid swimmerId)`). New `IChampionshipEnrollmentRepository`:
  - `Task<IReadOnlyList<Guid>> ListSwimmerIdsAsync(Guid eventId, CancellationToken)` — enrolled swimmer ids for one event.
  - `Task ReplaceAsync(Guid eventId, IReadOnlyList<Guid> swimmerIds, CancellationToken)` — **delete all rows for the event, insert the new set** in one `SaveChanges` (atomic replace). De-dupes the incoming ids.
  - Extend `ICompetitionEventRepository` with `Task<CompetitionEvent?> GetByIdAsync(Guid id, CancellationToken)` (used for the detail header **and** to detect a missing event for 404s).
- **Application:** extend `IChampionshipService` / `ChampionshipService`:
  - `Task<CompetitionEventDto?> GetByIdAsync(Guid id, CancellationToken)` — returns the event (status fields empty, resolved in the API layer like `ListAsync`), or `null`.
  - `Task<IReadOnlyList<Guid>?> GetEnrolledSwimmerIdsAsync(Guid eventId, CancellationToken)` — `null` when the event does not exist (→ 404), otherwise the id list.
  - `Task<bool> SetEnrollmentsAsync(Guid eventId, IReadOnlyList<Guid> swimmerIds, CancellationToken)` — `false` when the event does not exist, otherwise replaces and returns `true`.
  - New request DTO `SetEnrollmentsRequest(IReadOnlyList<Guid> SwimmerIds)`.
- **Infrastructure:** `ChampionshipEnrollment` config (`ToTable("championship_enrollment","championships")`, key `Id`, unique index `(EventId, SwimmerId)`); add `DbSet<ChampionshipEnrollment> Enrollments` to `ChampionshipsDbContext`; `ChampionshipEnrollmentRepository`; register `IChampionshipEnrollmentRepository` in `AddChampionshipsModule`; migration `CreateChampionshipEnrollmentTable`.

### 2. Status resolved at the API layer (unchanged seam)

`GET /api/championships/{id}` enriches the single event with `StatusCode`/`StatusNameEn`/`StatusNameAr` the same way `List` does — the controller loads `reference.competition_status` via Identity's `IReferenceService.GetCompetitionStatusesAsync` and maps by id. The Championships module still takes **no** dependency on Identity.

### 3. Enrollment is ids-only over the wire

`GET .../enrollments` returns just the enrolled **swimmer ids**. The frontend already loads the full roster (`GET /api/swimmers`) and only needs the enrolled set to tick checkboxes and compute the counter — so no swimmer enrichment on the enrollment endpoints.

### 4. `PUT` replaces the whole set (matches the Save-button UX)

The Enrollment tab is a checklist with a single **Save**. `PUT .../enrollments` takes the full desired set and atomically replaces the stored set — the direct analogue of the guardians `PUT`. This keeps the client simple (send working set) and the server idempotent.

## API contracts

```
GET /api/championships/{id}                                   [Authorize]
→ ApiResponse<CompetitionEventDto>            (same shape as the list rows; status resolved)
  404 when no event with that id

GET /api/championships/{eventId}/enrollments                 [Authorize]
→ ApiResponse<IReadOnlyList<Guid>>            (enrolled swimmer ids)
  404 when the event does not exist

PUT /api/championships/{eventId}/enrollments   [Authorize(Roles="head_coach,captain")]
  body: { swimmerIds: Guid[] }
→ ApiResponse<object>                          (200, replaced)
  404 when the event does not exist
  403 for authenticated non-managers
```

`CompetitionEventDto` is the existing record from the list design (`id, nameEn, nameAr, startDate, endDate, locationEn, locationAr, statusId, statusCode, statusNameEn, statusNameAr`). Empty enrollment → `data: []`. The `PUT` body may be empty (`swimmerIds: []`) — that clears the event's enrollment.

## Backend wiring

- `ChampionshipsController`: add the three actions above; reuse the existing `_service` + `_reference` fields (status enrichment for GET-by-id).
- `AddChampionshipsModule`: register `IChampionshipEnrollmentRepository → ChampionshipEnrollmentRepository`.
- No `Program.cs` change beyond the existing `ApplyChampionshipsMigrationsAsync` (the new migration is picked up by the same context).

## Frontend design

Extend `features/championships` with the same clean-architecture layering already there.

- **Data / DTOs** (`data/dto/`): add `enrollment.dto.ts` — `SetEnrollmentsRq { swimmerIds: string[] }` and `EnrollmentIdsDtoRs extends BaseResponseRs<string[]>` (+ a small array-of-strings validator). Reuse the existing `CompetitionEventItemDtoRs` for GET-by-id.
- **Repository** (`domain/repositories/championships.repository.ts` + `data/repositories/championships.repository.impl.ts`): add `getChampionship(id): Promise<CompetitionEventItemDtoRs>` (`GET /api/championships/{id}`), `getEnrollments(eventId): Promise<EnrollmentIdsDtoRs>` (`GET .../enrollments`), `setEnrollments(eventId, rq): Promise<...>` (`PUT .../enrollments`).
- **Use-cases** (`domain/usecases/`): `load-championship.use-case.ts` (validates + maps via existing `toChampionship`), `load-enrollments.use-case.ts` (validates the id array), `save-enrollments.use-case.ts`. Reuse **`ListSwimmersUseCase`** from `@features/swimmers` for the roster (it is `providedIn: 'root'`; imported by deep path like the existing `@features/auth/...` imports — no change to the swimmers barrel required).
- **Presentation** (`presentation/pages/championship-detail/`):
  - `championship-detail.viewmodel.ts` (`@Injectable`, provided on the route) — signals: `loading`, `error`, `notFound`, `championship`, `roster` (`SwimmerListItem[]`), `working` (a `Set<string>` of enrolled swimmer ids, seeded from the server), `baseline` (server set, for dirty check), `search`, `saving`, `activeTab`. Computeds: `canManage` (role head_coach/captain), `filtered()` (roster filtered by name EN/AR), `enrolledCount()`, `total()`, `dirty()`. Methods: `load(id)`, `setTab`, `toggle(swimmerId)` (guarded by `canManage`), `setSearch`, `save()` (calls `setEnrollments` with the working set; on success → success toast + reset baseline; on failure → error toast). Small header helpers `name()/location()` (EN/AR by `LanguageStore`) live on this VM; the date range reuses the standalone `formatDateRange` helper already in the feature.
  - `championship-detail.page.ts` — reads `:id` from `ActivatedRoute`, calls `vm.load(id)` on init; declares `tabs` (`enrollment | days | finished | results`) + `enabledTabs = new Set(['enrollment'])` + `isEnabled()`, mirroring `swimmer-profile.page.ts`.
  - `championship-detail.page.html` — identity strip (event name EN/AR · date range · location) + a **Back** link to `/championships`; the tab strip (disabled styling for the three unbuilt tabs); the Enrollment panel: header with `enrolledCount() / total()` counter + Save button (disabled unless `canManage() && dirty() && !saving()`), search input, and the swimmer checklist. Loading / notFound / error states like `swimmer-profile.page.html`. Non-managers see the checklist read-only (checkboxes + Save disabled).
- **List → detail navigation**: make each `/championships` table row navigate to `/championships/{id}` — add `RouterLink`/`Router` to `championships.page.ts` and a row `(click)`/`routerLink` + keyboard affordance (like the prototype's focusable rows).
- **Routing** (`app.routes.ts`): add `{ path: 'championships/:id', canActivate: [firstLoginGuard], loadComponent: … ChampionshipDetailPage, providers: [ChampionshipDetailViewModel] }`.
- **Barrel** (`features/championships/index.ts`): export `ChampionshipDetailPage` + `ChampionshipDetailViewModel`.
- **i18n**: add a `championships.detail.*` block (back, tab labels for enrollment/days/finished/results, "coming soon") and `championships.enrollment.*` (title, `N / total`, search placeholder, save, saved toast, saveFailed, empty roster) in `en.json` / `ar.json`. Names/locations render EN or AR by the active `LanguageStore`.

## Error handling

- Detail load: event 404 → `notFound` state; other failures → `error` state (retry affordance), matching `swimmer-profile`.
- Save failure → error toast (`championships.enrollment.saveFailed`); working set is left intact so the user can retry.
- Auth: route is behind the global auth guard; `PUT` is role-gated server-side (403) and the UI hides/disables editing for non-managers.

## Testing

**Backend (xUnit)**
- `ChampionshipEnrollmentRepository`: `ReplaceAsync` deletes prior rows + inserts the new set (and de-dupes); `ListSwimmerIdsAsync` returns the event's ids only.
- `ChampionshipService`: `GetByIdAsync` maps / returns null; `GetEnrolledSwimmerIdsAsync` returns null when the event is missing; `SetEnrollmentsAsync` returns false when missing, replaces otherwise.
- `ChampionshipsController`: GET-by-id 200 (status enriched) / 404; GET enrollments 200 / 404; PUT enrollments 200 / 404 / **403** for a non-manager role.

**Frontend (Jest — not Karma)**
- DTO validators (enrollment id array; reject malformed).
- Repository impl: calls the three endpoints and unwraps `ApiResponse`.
- Use-cases: `load-championship` (valid map + invalid payload → `AppError`), `load-enrollments`, `save-enrollments`.
- Detail ViewModel: seeds `working` from the server set; `toggle` adds/removes (and is a no-op for non-managers); `enrolledCount`/`total`/`dirty`; `filtered` search (EN + AR); `save` success (toast + baseline reset, `dirty` false) and failure (error toast); `notFound`/`error` transitions.
- Page: renders the four tabs with only Enrollment enabled (others `disabled`); list-page row navigates to `/championships/{id}`.

## Out of scope (this pass)

Competition Days, race sessions, Finished races, Results tabs; per-race swimmer assignment; race results/times; swimmer-facing view of their own enrollment/results; `created_by`/audit columns on enrollment; any Create/Edit/Delete of the event itself (already covered by the list design).

## Migrations / data

- **One new migration:** Championships `CreateChampionshipEnrollmentTable` (`championships.championship_enrollment` + unique `(EventId, SwimmerId)`).
- **Apply to Aiven `Swimming_Production`** via `dotnet ef database update --connection "…Swimming_Production…" --context ChampionshipsDbContext` — the running API must be **stopped** first (DLL lock); the design-time factory hardcodes local, so `--connection` is required. Needs the Aiven password at run time.
- **`scripts/seed-championships-enrollment-aiven.sql`** (idempotent, PascalCase quoted columns like the sibling seeds): for the fixed demo event ids from `seed-championships-aiven.sql` — `…3301` (National Junior), `…3302` (Regional Sprint), `…3303` (Winter Open) — enroll a handful of **real** `identity.swimmer_profile` rows selected by `ORDER BY "Id" LIMIT n` (swimmer ids are not fixed). Matches the mock: event 3301 → 4 swimmers, 3302 → 4 swimmers, 3303 → 0. Uses `"Id" = gen_random_uuid()` and `ON CONFLICT ("EventId","SwimmerId") DO NOTHING`. Guarded so it inserts nothing if the events or swimmers are absent. Purpose: after implementation, the Enrollment tab shows pre-ticked swimmers on 3301/3302 and an empty set on 3303, confirming the read path end-to-end.
```
-- verify:
--   SELECT e."NameEn", count(en.*) FROM championships.competition_event e
--     LEFT JOIN championships.championship_enrollment en ON en."EventId" = e."Id"
--     GROUP BY e."NameEn" ORDER BY e."NameEn";
```
