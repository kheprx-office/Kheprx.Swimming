# Championship detail — Competition Days tab — Design

- **Date:** 2026-09-23
- **Status:** Approved (design decisions); implementation pending
- **Branch:** `feat/championships`; **no commits until the user says so** (spec included)
- **Sibling / prior art:**
  - `2026-09-23-championship-enrollment-design.md` — the detail page + **Enrollment** tab this builds the **2nd tab** onto. Establishes the tabbed `/championships/:id` page, `ChampionshipDetailViewModel`, the `championship_enrollment` slice, and the **batch load + atomic replace** save pattern that this tab reuses for the whole schedule tree.
  - `2026-09-23-championships-list-design.md` — the list page + the `Championships` module (`competition_event`, `IChampionshipService`, `ChampionshipsController`, the frontend `features/championships` slice) this extends.
  - `2026-09-22-swimmer-profile-feedback-design.md` — the closest prior art for **adding a new `reference` lookup end-to-end** (entity → EF config → repository → `IReferenceService` method → `ReferenceController` action → frontend `LoadXUseCase`), the exact shape `reference.distance` follows.

## Context & goal

The championship detail page (`/championships/:id`) exists with a four-tab strip — **Enrollment · Competition Days · Finished races · Results** — where only **Enrollment** is enabled. This task implements the **2nd tab, Competition Days**: for a chosen event, head_coach / captain build the day-by-day race schedule and assign enrolled swimmers to each race.

A Competition Days schedule is a three-level tree:

```
Competition Day  (label + date)
  └─ Race         (distance + stroke + scheduled time)
       └─ assigned swimmers   (from the event's enrolled swimmers only)
```

Four new tables back it: `reference.distance` (new lookup) and the three `championships` tables `competition_day`, `race_session`, `race_assignment`.

### Design references

- Mocks: `Desktop/mcp/2.png` (event header + tab strip + start of Competition Days: the **Days · Total Races · Total Entries** totals strip, then Day cards) and `Desktop/mcp/3.png` (day cards with per-race rows: distance/stroke/time, a "N swimmers" count, a status pill, and the **Choose participants** chip row).
- React prototype: `Desktop/4dba8937-…/src/pages/ChampionshipPage.tsx` — the **`eventSubView === 'days'`** block (~L1087–1482): totals strip, `addDay`/`removeDay`/`updateDay`, `addSession`/`updateSession`/`removeSession`, `toggleSessionSwimmer`, and the empty-state. Everything in that file about **Finished races** (`eventSubView === 'finished'`), **Results** (`resultRaces`), and race **results/times** is **out of scope**.
- DB diagram: `docs/references/swimming-database-diagram.html` — `reference.distance` (L138–143), `championships.competition_day` (L337–342), `championships.race_session` (L343–348), `championships.race_assignment` (L349–353).

### Scope decisions (confirmed with the user)

1. **Save semantics — batch load + single Save.** `GET .../schedule` returns the whole tree; the tab edits it locally; one **Save** button `PUT`s the entire tree, which the server **atomically replaces** (delete the event's days/races/assignments, re-insert). Direct analogue of the Enrollment `PUT`. Row ids are **regenerated every save** (nothing else references them yet); the client adopts the returned tree as its new baseline.
2. **Participants — enrolled swimmers only.** A race's participant chips are exactly the event's enrolled swimmers (Enrollment tab). You must enroll a swimmer before assigning them. The server **validates** that every assigned `swimmerId` is enrolled and rejects the `PUT` otherwise.
3. **Editing gated to `head_coach` / `captain`** (`canManage`), matching Enrollment. The tab renders read-only for other authenticated roles.
4. **Out of scope:** `race_result`, results/time entry, the Finished races & Results tabs, and any swimmer-facing view. The per-race **status pill** shown in the mock is a **client-side, time-derived** label only (see §Frontend), never persisted.
5. **Seed target:** the live Aiven DB **`Swimming_Production`** (needs the DB password at run time; migrations applied there first).
6. **Distance seed list:** the diagram's canonical codes — `50m, 100m, 200m, 400m, 800m, 1000m, 1500m, 5000m, 7000m, 7500m, 10000m`.
7. **Day label:** `LabelEn` is an **editable text field** defaulting to `Day N`; `LabelAr` optional. (More flexible than the prototype's auto-only label, and it uses the real columns.)

## Data model (four new tables)

EF convention here (confirmed across the module): **table + schema are snake_case via `ToTable(name, schema)`; columns default to PascalCase property names** (so SQL quotes them `"Id"`, `"LabelEn"`, …). Cross-module ids are **loose Guids** — no EF navigation, and because the modules use separate `DbContext`s, **no DB-level FK is emitted** (same as `competition_event.StatusId`). The one invariant enforced in the DB is `race_assignment`'s unique index.

### `reference.distance` (Identity module, `reference` schema — mirrors `reference.stroke`)

| Column   | Type            | Notes                                             |
|----------|-----------------|---------------------------------------------------|
| `Id`     | uuid (PK)       |                                                   |
| `Code`   | varchar(50), U  | `50m`, `100m`, … (unique index, like `stroke.Code`) |
| `NameEn` | varchar(100)    |                                                   |
| `NameAr` | varchar(100)    | nullable                                          |
| `Meters` | int             | 50, 100, …                                        |

### `championships.competition_day`

| Column     | Type      | Notes                                                    |
|------------|-----------|----------------------------------------------------------|
| `Id`       | uuid (PK) |                                                          |
| `EventId`  | uuid      | loose → `championships.competition_event`                |
| `LabelEn`  | varchar   | required                                                 |
| `LabelAr`  | varchar   | nullable                                                 |
| `DayDate`  | date      | required (`DateOnly`)                                     |

### `championships.race_session`

| Column          | Type      | Notes                                    |
|-----------------|-----------|------------------------------------------|
| `Id`            | uuid (PK) |                                          |
| `DayId`         | uuid      | loose → `championships.competition_day`  |
| `StrokeId`      | uuid      | loose → `reference.stroke`               |
| `DistanceId`    | uuid      | loose → `reference.distance`             |
| `ScheduledTime` | time      | nullable (`TimeOnly?`)                   |

### `championships.race_assignment`

| Column           | Type      | Notes                                        |
|------------------|-----------|----------------------------------------------|
| `Id`             | uuid (PK) |                                              |
| `RaceSessionId`  | uuid      | loose → `championships.race_session`         |
| `SwimmerId`      | uuid      | loose → `identity.swimmer_profile`           |

- **Unique index on `(RaceSessionId, SwimmerId)`** — "one assignment per swimmer per race" (the diagram's `U`).

## Key architectural decisions

### 1. `reference.distance` added to the Identity reference set

New `Distance` entity (`Id`, `Code`, `NameEn`, `NameAr?`, `Meters`; private EF ctor + public ctor) in `Identity.Domain.Entities`, `DistanceConfiguration` (`ToTable("distance","reference")`, unique `Code`), `DbSet<Distance>` on `IdentityDbContext`, `IDistanceRepository`/`DistanceRepository` (`GetAllAsync`, `ExistsAsync`), `IReferenceService.GetDistancesAsync` → `CodedLookupDto` (`Meters` not exposed — the tab only needs id/code/name, matching `strokes`), registered in `AddIdentityModule`, migration `AddDistanceReference` on `IdentityDbContext`. `ReferenceController` gains `GET /api/reference/distances`.

### 2. Schedule is one aggregate — one repo, one context, atomic replace

No new module — extend `backend/src/Modules/Championships/`.

- **Domain:** three entities `CompetitionDay` (`Id`, `EventId`, `LabelEn`, `LabelAr?`, `DayDate`), `RaceSession` (`Id`, `DayId`, `StrokeId`, `DistanceId`, `ScheduledTime?`), `RaceAssignment` (`Id`, `RaceSessionId`, `SwimmerId`), each with a private EF ctor + a public ctor. New `ICompetitionScheduleRepository`:
  - `Task<CompetitionScheduleData> GetAsync(Guid eventId, CancellationToken)` — the event's days + their sessions + their assignment swimmer-ids as plain lists (an **empty** `CompetitionScheduleData` when the event has no days); the service composes them into the tree. Event existence (404 vs. empty) is decided by the service, not here.
  - `Task ReplaceAsync(Guid eventId, CompetitionScheduleData tree, CancellationToken)` — in **one `SaveChanges`**: delete all `race_assignment` for the event's sessions, all `race_session` for the event's days, all `competition_day` for the event; then insert the incoming tree with fresh Guids. Atomic.
- **Application:** extend `IChampionshipService` / `ChampionshipService`:
  - `Task<ScheduleDto?> GetScheduleAsync(Guid eventId, CancellationToken)` — checks the event exists via `ICompetitionEventRepository.GetByIdAsync` (mirrors `GetEnrolledSwimmerIdsAsync`); returns `null` when it doesn't (→ 404), otherwise the tree DTO from the repo (empty `days: []` when none).
  - `Task<SetScheduleResult> SetScheduleAsync(Guid eventId, ScheduleDto tree, CancellationToken)` — a small result enum/record capturing `NotFound` (event missing), `Invalid` (validation failed — e.g. an assigned swimmer isn't enrolled, or a day/race is missing required fields), or `Ok(ScheduleDto saved)`. On `Ok` it replaces and returns the re-read tree (new ids). Validation of the **enrolled subset** reuses `IChampionshipEnrollmentRepository.ListSwimmerIdsAsync`.
  - DTOs (`CompetitionScheduleDtos.cs`): `ScheduleDto(IReadOnlyList<ScheduleDayDto> Days)`, `ScheduleDayDto(Guid Id, string LabelEn, string? LabelAr, DateOnly DayDate, IReadOnlyList<ScheduleRaceDto> Races)`, `ScheduleRaceDto(Guid Id, Guid StrokeId, Guid DistanceId, TimeOnly? ScheduledTime, IReadOnlyList<Guid> SwimmerIds)`, and request `SetScheduleRequest(IReadOnlyList<ScheduleDayDto> Days)` (client-sent ids ignored on insert).
- **Infrastructure:** three EF configs; add `DbSet<CompetitionDay> Days`, `DbSet<RaceSession> RaceSessions`, `DbSet<RaceAssignment> RaceAssignments` to `ChampionshipsDbContext`; `CompetitionScheduleRepository`; register `ICompetitionScheduleRepository` in `AddChampionshipsModule`; migration `CreateCompetitionScheduleTables` (all three tables + the unique index). Picked up by the existing `ApplyChampionshipsMigrationsAsync` — no `Program.cs` change.

### 3. Strokes/distances resolved client-side; assignment is ids-only over the wire

The schedule DTO carries `strokeId`/`distanceId`/`swimmerIds` — no enrichment. The tab loads `reference.strokes`, `reference.distances`, the event's **enrollments** (ids) and the **roster** (`GET /api/swimmers`) and resolves labels/names client-side, exactly as Enrollment resolves swimmer names from the roster. Keeps the schedule endpoints thin and the Championships module free of any Identity dependency.

## API contracts

```
GET /api/reference/distances                                  [Authorize]
→ ApiResponse<IReadOnlyList<CodedLookupDto>>   (id, code, nameEn, nameAr)

GET /api/championships/{eventId}/schedule                     [Authorize]
→ ApiResponse<ScheduleDto>                     ({ days:[ { id,labelEn,labelAr,dayDate, races:[ { id,strokeId,distanceId,scheduledTime, swimmerIds:[] } ] } ] })
  404 when the event does not exist            (empty schedule → data:{ days:[] })

PUT /api/championships/{eventId}/schedule   [Authorize(Roles="head_coach,captain")]
  body: { days: [ { labelEn, labelAr?, dayDate, races:[ { strokeId, distanceId, scheduledTime?, swimmerIds:[] } ] } ] }
→ ApiResponse<ScheduleDto>                     (200, the persisted tree with fresh ids)
  404 when the event does not exist
  422/400 (ApiResponse error) when validation fails — a race references a non-enrolled swimmer, or a day/race is missing required fields
  403 for authenticated non-managers
```

`dayDate` is an ISO `date` (`yyyy-MM-dd`); `scheduledTime` is `HH:mm` or null. A `PUT` with `days: []` clears the whole schedule.

## Frontend design

Extend `features/championships` with the same clean-architecture layering; add the reference bits to `features/reference`.

- **Reference feature:** add `getDistances(): Promise<CodedLookupListDtoRs>` (`GET /api/reference/distances`) to the reference repo port + impl, and `LoadDistancesUseCase` (validates + maps to `LookupItem[]`) next to `LoadStrokesUseCase`.
- **Data / DTOs** (`data/dto/schedule.dto.ts`): `ScheduleDtoRs extends BaseResponseRs<ScheduleData>`; `ScheduleData { days: ScheduleDayData[] }` with `ScheduleDayData { id, labelEn, labelAr, dayDate, races: ScheduleRaceData[] }` and `ScheduleRaceData { id, strokeId, distanceId, scheduledTime, swimmerIds }`; `SetScheduleRq { days: [...] }`; a structural validator; mapper to/from the domain model.
- **Repository** (`championships.repository.ts` + impl): add `getSchedule(eventId): Promise<ScheduleDtoRs>` and `setSchedule(eventId, rq): Promise<ScheduleDtoRs>`.
- **Use-cases** (`domain/usecases/`): `load-schedule.use-case.ts` (validate + map), `save-schedule.use-case.ts` (map working tree → `SetScheduleRq`, `PUT`, return the mapped saved tree). Reuse `ListSwimmersUseCase` (roster) and the existing `LoadEnrollmentsUseCase` for the enrolled-id set; reuse `LoadStrokesUseCase` + new `LoadDistancesUseCase`.
- **Domain model** (`domain/model/competition-schedule.ts`): editor types `ScheduleDay { key; labelEn; labelAr; dayDate; races: ScheduleRace[] }`, `ScheduleRace { key; strokeId; distanceId; scheduledTime; swimmerIds: Set<string> }`. Client-only `key`s (stable while editing) map to server ids on load and are discarded on save.
- **Presentation** — a **dedicated `CompetitionDaysViewModel`** (`@Injectable`, provided on the `/championships/:id` route alongside `ChampionshipDetailViewModel`), so schedule state stays isolated from enrollment state:
  - **Lazy load** on first activation of the `days` tab: schedule + strokes + distances + enrollments + roster. Signals: `loading`, `error`, `loaded`, `saving`, `strokes`, `distances`, `enrolledSwimmers` (roster ∩ enrolled, `SwimmerListItem[]`), `days` (the editable tree), a `baseline` snapshot (for `dirty`), plus toast state.
  - **Computeds:** `canManage` (head_coach/captain), `dayCount()`, `raceCount()`, `entryCount()` (the totals strip), `dirty()` (working tree ≠ baseline).
  - **Methods (all edits guarded by `canManage`):** `addDay()` (default label `Day {n}`, date defaulting to the event start), `removeDay(key)`, `updateDay(key, patch)`; `addRace(dayKey)` (default distance/stroke = first lookup), `removeRace`, `updateRace(dayKey, raceKey, patch)`; `toggleSwimmer(dayKey, raceKey, swimmerId)`; `save()` → `PUT` → on `Ok` adopt returned tree as baseline + success toast; on validation/other failure → error toast, working tree left intact.
  - **Template:** a section in `championship-detail.page.html` gated by `vm.activeTab() === 'days'`, styled from the mock — totals strip (Days · Total Races · Total Entries), the empty-state ("Add competition days to structure this event"), one card per day (editable label + `input[type=date]` bounded by the event range, **Add Race** / remove-day), each race row with **distance** + **stroke** `select`s + `input[type=time]`, a "N swimmers" badge, the **client-derived status pill** (`Scheduled HH:mm` → once the start passes, `Awaiting results`; never "recorded" — results are a later tab), remove-race, and the **Choose participants** chip row (enrolled swimmers only; empty → "Enroll swimmers first" hint). A single **Save** button (enabled only when `canManage() && dirty() && !saving()`) + saved/failed toast. Non-managers see it read-only.
- **Tab enablement:** add `'days'` to `enabledTabs` in `championship-detail.page.ts`; wire the tab so first activation triggers `CompetitionDaysViewModel.load(eventId)`.
- **i18n:** add a `championships.days.*` block (tab already labelled) — totals labels, add day/race, day/race field labels (distance, stroke, time), choose participants, enroll-first hint, status pill (`scheduled`, `awaitingResults`), save, saved/failed toasts, empty-state — in `en.json` / `ar.json`. Labels/names render EN or AR by the active `LanguageStore`.

## Error handling

- Schedule load: event 404 → `error` state with retry; empty schedule is a normal `loaded` state with zero days.
- Save failure: validation error (non-enrolled swimmer / missing field) and generic failure both surface an error toast (`championships.days.saveFailed`); the working tree is preserved for retry.
- Auth: route behind the global auth guard; `PUT` role-gated server-side (403); UI hides/disables editing for non-managers.

## Testing

**Backend (xUnit)**
- `CompetitionScheduleRepository`: `ReplaceAsync` deletes the event's prior days/sessions/assignments and inserts the new tree (nested), in one save; `GetAsync` returns only the target event's rows.
- `ChampionshipService`: `GetScheduleAsync` composes the tree / returns null when the event is missing; `SetScheduleAsync` → `NotFound` (missing event), `Invalid` (a race swimmer isn't enrolled; a day/race missing required fields), `Ok` (replaces + returns re-read tree).
- `ReferenceService`: `GetDistancesAsync` maps rows → `CodedLookupDto`.
- Controllers: `GET /schedule` 200 / 404; `PUT /schedule` 200 / 404 / **422 (non-enrolled)** / **403** (non-manager); `GET /reference/distances` 200.

**Frontend (Jest — not Karma)**
- DTO validator (schedule tree; reject malformed) + mapper round-trip.
- Repository impl: `getSchedule`/`setSchedule` hit the right URLs and unwrap `ApiResponse`; `getDistances`.
- Use-cases: `load-schedule` (valid map + invalid payload → `AppError`), `save-schedule` (maps working tree → request, adopts saved tree), `load-distances`.
- `CompetitionDaysViewModel`: add/remove day & race; `updateDay`/`updateRace`; `toggleSwimmer` (no-op for non-managers); `dayCount`/`raceCount`/`entryCount`; `dirty`; `save` success (baseline reset, `dirty` false, toast) and failure (error toast, tree intact); participant list = roster ∩ enrolled.

## Out of scope (this pass)

`race_result` and any results/time entry; the Finished races & Results tabs; persisted race status; swimmer-facing schedule view; stable/persisted schedule row ids across saves; audit columns (`created_by`, timestamps) on the schedule tables; editing the event itself (covered by the list design).

## Migrations / data

- **Two new migrations:** `AddDistanceReference` (Identity — `reference.distance` + unique `Code`) and `CreateCompetitionScheduleTables` (Championships — `competition_day`, `race_session`, `race_assignment` + unique `(RaceSessionId, SwimmerId)`).
- **Apply to Aiven `Swimming_Production` first**, then seed. Per convention: `dotnet ef database update --connection "…Swimming_Production…" --context IdentityDbContext` and `--context ChampionshipsDbContext`, with the running API **stopped** (DLL lock); the design-time factory hardcodes local, so `--connection` is required. Needs the Aiven password at run time. Verify `reference.stroke` is populated on Aiven before seeding races.
- **`scripts/seed-distances-aiven.sql`** (idempotent): the canonical distances with **fixed UUIDs** (e.g. `44444444-…-44dd00NN`), `ON CONFLICT ("Code") DO NOTHING`.
- **`scripts/seed-competition-schedule-aiven.sql`** (idempotent, PascalCase quoted columns): build a schedule for the 2-day demo event **`…3301` (National Junior)** to match the prototype — **Day 1 (Heats, 2023-11-15)** and **Day 2 (Finals, 2023-11-16)** with fixed day ids; a few `race_session`s resolving `StrokeId`/`DistanceId` **by `Code`** (e.g. 50m Freestyle, 100m Backstroke); `race_assignment`s selecting from that event's **enrolled** swimmers (`…3301` has 4, seeded by `seed-championships-enrollment-aiven.sql`). Fixed ids for days/races so assignments reference them deterministically; `ON CONFLICT DO NOTHING`. Guarded so it inserts nothing when the event, strokes, distances, or enrollments are absent. Purpose: after implementation the Competition Days tab shows a populated 2-day schedule on `…3301`, and empty schedules on `…3302`/`…3303`, confirming the read path end-to-end.
```
-- verify:
--   SELECT d."LabelEn", count(rs.*) AS races
--   FROM championships.competition_day d
--   LEFT JOIN championships.race_session rs ON rs."DayId" = d."Id"
--   WHERE d."EventId" = '33333333-3333-3333-3333-333333333301'
--   GROUP BY d."LabelEn" ORDER BY d."LabelEn";
```
