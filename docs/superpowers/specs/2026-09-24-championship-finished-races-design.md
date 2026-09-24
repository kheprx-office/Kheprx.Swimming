# Championship detail — Finished Races + Results tabs — Design

- **Date:** 2026-09-24
- **Status:** Approved (design decisions); implementation pending
- **Branch:** `feat/championships`; **no commits until the user says so** (spec included)
- **Sibling / prior art:**
  - `2026-09-23-competition-days-design.md` — the **2nd tab** (Competition Days) this builds directly on. Establishes the `competition_day → race_session → race_assignment` tree, the `ChampionshipsController` `…/schedule` endpoints, `CompetitionDaysViewModel`, the client-derived race status pill (`raceStatus`), and the **atomic replace** save pattern this reuses for per-race results. **Finished Races reads `race_session` + `race_assignment` and writes `race_result`.**
  - `2026-09-23-championship-enrollment-design.md` — the detail page + Enrollment tab; the tabbed `/championships/:id` page, `ChampionshipDetailViewModel`, the batch load + atomic replace pattern.
  - `2026-09-23-championships-list-design.md` — the list page + the `Championships` module.

## Context & goal

The championship detail page (`/championships/:id`) has a four-tab strip — **Enrollment · Competition Days · Finished races · Results** — with **Enrollment** and **Competition Days** live and **Finished** / **Results** still placeholders. This task implements the **3rd tab (Finished races)** and the **4th tab (Results)**:

- **Finished races** (write path) — races built in Competition Days whose scheduled start has passed but have **no recorded times yet** appear as cards. A head_coach / captain expands one, types each **assigned** swimmer's finish time, and saves — writing `championships.race_result`. The race then leaves Finished and appears under Results.
- **Results** (read path) — races that have recorded times, grouped by race, each swimmer's time sorted to a rank with a personal-best badge.

Both tabs are backed by **one new table**, `championships.race_result`, plus one read endpoint and one per-race write endpoint on the existing `ChampionshipsController`.

### Design references

- Mocks: `Desktop/mcp/2.png` and `Desktop/mcp/3.png` — the event header + tab strip with **Finished races** selected: a helper line, then one card per finished race (race name, day label · scheduled time · N swimmers, an **Enter results** toggle) expanding to a per-swimmer time input row and a **Save results** button. `Desktop/mcp/1.png` is the list page (already built).
- React prototype: `Desktop/4dba8937-…/src/pages/ChampionshipPage.tsx`:
  - `finishedRaces` derivation (~L619–630): sessions where `hasStarted(day.date, s.time)` **and** `(s.results?.length || 0) === 0`.
  - `recordedRaces` / `resultRaces` derivation (~L631–687): sessions with `results.length > 0`, grouped per race with rank + PB.
  - `eventSubView === 'finished'` block (~L1484 onward): finished-race cards, the **Enter results** expander, per-swimmer time inputs, `commitResults`, `Save results`.
  - `hasStarted` (~L592–598) — the same "scheduled start passed" rule already ported to the frontend as `raceStatus`.
  - Out of scope from that file: the mock's hard-coded `event.results`/points/`isPB` values (ours come from `race_result`), and the swimmer-facing view.
- DB diagram: `docs/references/swimming-database-diagram.html` — `championships.race_result` (L354–362): `id, race_session_id, swimmer_id, time_ms int, points int, is_personal_best bool, recorded_by → app_user, unique(race_session_id, swimmer_id)`.

### Scope decisions (confirmed with the user)

1. **Both tabs this pass.** Finished races (enter) **and** Results (view). Seeding `race_result` rows would otherwise be invisible — the Finished tab hides races that already have results — so the read-side Results tab is needed to verify the seed.
2. **"Finished" rule — scheduled start passed.** A race appears in Finished once its `day_date` + `scheduled_time` is in the past **and** it has zero results, exactly like the prototype. Reuses the existing client-derived `raceStatus` helper (venue local wall-clock, matching Competition Days). A race with no `scheduled_time` counts as started once its day date passes. **No per-race "finished" flag is persisted** — it is derived from schedule + results.
3. **Result fields — time only; PB auto; points = 0.** The coach enters only the finish time (`mm:ss.SS` in the UI → stored as `TimeMs` int). `IsPersonalBest` is **computed server-side** by comparing the new time against the swimmer's prior results for the **same distance + stroke** (career-wide, all within the Championships context). `Points` is stored as **0** — no scoring table this pass. Results shows time + client-derived rank + PB badge.
4. **Per-race save.** One "Save results" button per finished-race card. Each save is an **atomic replace** of that one `race_session`'s results (delete the session's rows, insert the entered ones), mirroring the schedule's `ReplaceAsync`. An empty `entries` list clears the race's results (moving it back to Finished).
5. **Editing gated to `head_coach` / `captain`** (`canManage`), matching Enrollment and Competition Days. Other authenticated roles read Results and see Finished read-only.
6. **Seed target:** the live Aiven DB **`Swimming_Production`** (migration applied there first; needs the DB password at run time).

## Data model (one new table)

EF convention here (confirmed across the module): **table + schema snake_case via `ToTable(name, schema)`; columns default to PascalCase property names** (SQL quotes them `"Id"`, `"TimeMs"`, …). Cross-module ids are **loose Guids** — no EF navigation, no DB-level FK (separate `DbContext`s). The one DB invariant is the unique index.

### `championships.race_result`

| Column           | Type      | Notes                                                    |
|------------------|-----------|----------------------------------------------------------|
| `Id`             | uuid (PK) |                                                          |
| `RaceSessionId`  | uuid      | loose → `championships.race_session`                     |
| `SwimmerId`      | uuid      | loose → `identity.swimmer_profile`                       |
| `TimeMs`         | int       | finish time in milliseconds, > 0                         |
| `Points`         | int       | always `0` this pass (no scoring table)                  |
| `IsPersonalBest` | bool      | computed server-side on save                             |
| `RecordedBy`     | uuid      | loose → `identity.app_user` (= `CurrentUserId`)          |

- **Unique index on `(RaceSessionId, SwimmerId)`** — "one result per swimmer per race" (the diagram's `U`).

## Key architectural decisions

### 1. `race_result` is one aggregate scoped per race session — one repo, one context, atomic per-session replace

No new module — extend `backend/src/Modules/Championships/`.

- **Domain:** new entity `RaceResult` (`Id`, `RaceSessionId`, `SwimmerId`, `TimeMs`, `Points`, `IsPersonalBest`, `RecordedBy`) with a private EF ctor + a public ctor. New `IRaceResultRepository`:
  - `Task<IReadOnlyList<RaceResultRow>> GetByEventAsync(Guid eventId, CancellationToken)` — all `race_result` rows for sessions under the event's days (`race_result` → `race_session` → `competition_day` where `EventId = eventId`), as flat rows. Empty list when none.
  - `Task ReplaceForSessionAsync(Guid raceSessionId, IReadOnlyList<RaceResultInput> rows, CancellationToken)` — in **one `SaveChanges`**: delete all `race_result` for the session, insert the incoming rows with fresh Guids. Atomic. (An empty list clears the session.)
  - `Task<IReadOnlyDictionary<Guid, int>> GetBestTimesAsync(Guid distanceId, Guid strokeId, Guid excludeSessionId, IReadOnlyList<Guid> swimmerIds, CancellationToken)` — each swimmer's minimum prior `TimeMs` across all `race_session`s with the same `DistanceId` + `StrokeId`, excluding `excludeSessionId`. All within `ChampionshipsDbContext` (distance/stroke live on `race_session`). Used for the PB flag; swimmers with no prior time are absent from the map.
  - Domain row/input records in `RaceResultData.cs`: `RaceResultRow(Guid Id, Guid RaceSessionId, Guid SwimmerId, int TimeMs, int Points, bool IsPersonalBest, Guid RecordedBy)`, `RaceResultInput(Guid SwimmerId, int TimeMs, int Points, bool IsPersonalBest, Guid RecordedBy)`.
- **Application:** extend `IChampionshipService` / `ChampionshipService`:
  - `Task<ResultsDto?> GetResultsAsync(Guid eventId, CancellationToken)` — checks the event exists via `ICompetitionEventRepository.GetByIdAsync` (mirrors `GetScheduleAsync`); returns `null` when it doesn't (→ 404), otherwise the event's results (empty `results: []` when none).
  - `Task<SetRaceResultsResult> SetRaceResultsAsync(Guid eventId, Guid raceSessionId, IReadOnlyList<SetRaceResultsEntry> entries, Guid recordedBy, CancellationToken)` — a result record capturing `NotFound` (event or session missing / session not under this event), `Invalid` (an entered swimmer isn't **assigned** to the race, a `timeMs ≤ 0`, or a duplicate swimmer in the batch), or `Ok(ResultsDto saved)`. On `Ok`:
    1. resolve the session's `DistanceId` + `StrokeId`,
    2. fetch prior best times via `GetBestTimesAsync`,
    3. build `RaceResultInput`s with `IsPersonalBest = prior best absent || timeMs < prior best`, `Points = 0`, `RecordedBy = recordedBy`,
    4. `ReplaceForSessionAsync`, then re-read and return the event's results.
  - Session validation (exists, belongs to the event, assigned swimmers) reads through the schedule repo / a small session lookup — reuse `ICompetitionScheduleRepository.GetAsync(eventId)` to get the event's sessions + assignments in one call, then validate against it (keeps a single source of truth for the event's schedule; no new "get one session" repo method needed).
  - DTOs (`RaceResultDtos.cs`): `RaceResultDto(Guid RaceSessionId, Guid SwimmerId, int TimeMs, int Points, bool IsPersonalBest)`, `ResultsDto(IReadOnlyList<RaceResultDto> Results)`; request `SetRaceResultsEntry(Guid SwimmerId, int TimeMs)`, `SetRaceResultsRequest(IReadOnlyList<SetRaceResultsEntry> Entries)`; and `SetRaceResultsResult(SetRaceResultsOutcome Outcome, ResultsDto? Saved, string? Error)` with `enum SetRaceResultsOutcome { Ok, NotFound, Invalid }`.
- **Infrastructure:** `RaceResultConfiguration` (`ToTable("race_result","championships")`, unique `(RaceSessionId, SwimmerId)`); add `DbSet<RaceResult> RaceResults` to `ChampionshipsDbContext`; `RaceResultRepository`; register `IRaceResultRepository` in `AddChampionshipsModule`; migration `CreateRaceResultTable` (table + unique index). Picked up by the existing `ApplyChampionshipsMigrationsAsync` — no `Program.cs` change.

### 2. Both tabs are client-composed from schedule + results

The Finished and Results tabs are two views over the same two data sources — the **schedule tree** (`GET …/schedule`, already built) and the **event's results** (`GET …/results`, new). The frontend loads both plus roster/strokes/distances and derives:
- **Finished** = for each day→race, `raceStatus(day.dayDate, race.scheduledTime) === 'awaitingResults'` **and** no result row has that `raceSessionId`.
- **Results** = races that have ≥1 result row, entries sorted by `timeMs` → rank.

Race name (distance + stroke), swimmer names, and day labels are resolved **client-side** from the reference lookups + roster + schedule — exactly as the Days tab resolves them. The results endpoints stay thin (ids + numbers only), and the Championships module keeps no Identity dependency.

**Important — the schedule must be read *with* its `race_session` ids here.** The existing `LoadScheduleUseCase` deliberately maps the schedule DTO to a **key-less** `ScheduleDayData[]` (its mapper `toScheduleDataList` drops the server `id`s, because the Days editor regenerates ids on every save). Finished/Results instead need the real `race_session` id to (a) match a race against its `race_result` rows and (b) target the `PUT …/races/{raceSessionId}/results`. So this feature adds a **separate id-preserving read** over the same `GET …/schedule` response (the DTO already carries `id` on every day and race) — it does **not** reuse `LoadScheduleUseCase`, and it does **not** touch the Days flow.

### 3. Times cross the wire as integer milliseconds

The coach types `mm:ss.SS` (or `ss.SS`); the client parses to `TimeMs` (int) before `PUT`, and formats `TimeMs` back to `mm:ss.SS` for display. Keeps the wire and DB in one canonical unit and avoids server-side string parsing. A small `race-time.ts` util owns both conversions (unit-tested for round-trip + malformed input).

## API contracts

```
GET /api/championships/{eventId}/results                              [Authorize]
→ ApiResponse<ResultsDto>   ({ results:[ { raceSessionId, swimmerId, timeMs, points, isPersonalBest } ] })
  404 when the event does not exist            (no results → data:{ results:[] })

PUT /api/championships/{eventId}/races/{raceSessionId}/results  [Authorize(Roles="head_coach,captain")]
  body: { entries:[ { swimmerId, timeMs } ] }   (empty entries clears the race's results)
→ ApiResponse<ResultsDto>   (200, the event's re-read results with fresh ids + computed PB)
  404 when the event or session is unknown, or the session is not part of this event
  400 (ApiResponse error) when an entered swimmer is not assigned to the race, a timeMs ≤ 0, or a swimmer is repeated
  403 for authenticated non-managers
```

`timeMs` is a positive integer (milliseconds). `recorded_by` is taken from the authenticated user, never the body. `points` is always `0` in stored rows this pass.

## Frontend design

Extend `features/championships` with the same clean-architecture layering.

- **Data / DTOs** (`data/dto/race-result.dto.ts`): `ResultsDtoRs extends BaseResponseRs<ResultsData>`; `ResultsData { results: RaceResultData[] }` with `RaceResultData { raceSessionId, swimmerId, timeMs, points, isPersonalBest }`; `SetRaceResultsRq { entries: { swimmerId, timeMs }[] }`; a structural validator; mapper to the domain model.
- **Repository** (`championships.repository.ts` + impl): add `getResults(eventId): Promise<ResultsDtoRs>` and `setRaceResults(eventId, raceSessionId, rq): Promise<ResultsDtoRs>`. The existing `getSchedule(eventId): Promise<ScheduleDtoRs>` is reused as-is (its DTO already carries day/race `id`s).
- **Use-cases** (`domain/usecases/`): `load-results.use-case.ts` (validate + map), `save-race-results.use-case.ts` (map inputs → `SetRaceResultsRq`, `PUT`, return the mapped saved results), and `load-race-schedule.use-case.ts` — calls the existing `repo.getSchedule`, validates via the existing `isScheduleDtoValid`, and maps to an **id-preserving** tree (a new `toRaceScheduleList` mapper alongside `toScheduleDataList`). Reuse `ListSwimmersUseCase`, `LoadStrokesUseCase`, `LoadDistancesUseCase`. **Not** `LoadScheduleUseCase` (drops ids — see §2).
- **Domain model** (`domain/model/race-result.ts`): wire type `RaceResultData` (above); an **id-carrying** schedule read model `RaceScheduleDay { id; labelEn; labelAr; dayDate; races: RaceScheduleRace[] }`, `RaceScheduleRace { id; strokeId; distanceId; scheduledTime; swimmerIds }`; and view types `FinishedRaceCard { raceSessionId; raceName; dayLabel; scheduledTime; swimmers: { id; name }[] }`, `ResultsRaceCard { raceSessionId; raceName; dayLabel; entries: { swimmerName; timeMs; rank; isPersonalBest }[] }`. Plus `race-time.ts` (`parseTimeToMs`, `formatMsToTime`).
- **Presentation** — a dedicated `RaceResultsViewModel` (`@Injectable`, provided on the `/championships/:id` route alongside the other two view-models), serving **both** tabs from one load:
  - **Lazy load** on first activation of the `finished` **or** `results` tab: schedule + results + roster + strokes + distances. Signals: `loading`, `loaded`, `error`, `saving`, `strokes`, `distances`, `roster`, `schedule` (the day→race tree with ids), `results` (flat rows), `openRaceId`, `inputs` (`Record<sessionId, Record<swimmerId, string>>`), plus toast via `NotificationService`.
  - **Computeds:** `canManage` (head_coach/captain); `finishedRaces(): FinishedRaceCard[]`; `resultRaces(): ResultsRaceCard[]` (entries sorted by `timeMs`, `rank = index+1`); `finishedCount()` (for the tab badge).
  - **Methods (entry guarded by `canManage`):** `openResults(sessionId)` / `closeResults()` (toggle the expander, seed `inputs` from any current values); `setTime(sessionId, swimmerId, value)`; `saveResults(sessionId)` → parse each non-blank input via `parseTimeToMs`, build `entries`, call `save-race-results`; on `Ok` adopt the returned results, collapse the card, success toast; on failure error toast, inputs left intact.
  - **Display helpers:** `raceName(session)` (distance + stroke labels, locale-aware), `swimmerName`, `dayLabel`, `formatTime(ms)`.
  - **Template:** two sections in `championship-detail.page.html`, gated by `vm.activeTab() === 'finished'` / `=== 'results'`, styled from the mocks:
    - *Finished:* helper line; empty state ("No finished races — races appear here once their start time has passed"); one card per finished race (race name; day label · scheduled time · N swimmers; **Enter results** toggle → per-assigned-swimmer time input row + **Save results**, enabled only for `canManage`). Non-managers see cards read-only (no expander).
    - *Results:* empty state ("No results recorded yet"); one card per results race (race name + day label; rows of rank · swimmer · time · PB badge).
- **Tab enablement:** add `'finished'` and `'results'` to `enabledTabs` in `championship-detail.page.ts`; `onTab` triggers `raceResultsVm.ensureLoaded(eventId)` for either tab (load once; subsequent activations are no-ops, matching `CompetitionDaysViewModel.ensureLoaded`). The Finished tab label shows the `finishedCount()` badge.
- **i18n:** add `championships.finished.*` (tab already labelled, helper line, empty state, enter/save results, time column, swimmers count, saved/failed toasts) and `championships.results.*` (empty state, rank/swimmer/time/PB labels) to `en.json` / `ar.json`. Labels render EN or AR by the active `LanguageStore`.

## Error handling

- Load: event 404 → `error` state with retry; no results is a normal `loaded` state (empty Results, Finished derived from schedule).
- Save failure: validation (non-assigned swimmer / bad time) and generic failure both surface an error toast (`championships.finished.saveFailed`); the open card and its inputs are preserved for retry.
- Auth: route behind the global auth guard; `PUT` role-gated server-side (403); UI hides the entry expander for non-managers.

## Testing

**Backend (xUnit)**
- `RaceResultRepository`: `GetByEventAsync` returns only the target event's rows (via day→session join); `ReplaceForSessionAsync` deletes the session's prior rows and inserts the new set in one save; `GetBestTimesAsync` returns each swimmer's min prior time for the same distance+stroke, excluding the current session, omitting swimmers with none.
- `ChampionshipService`: `GetResultsAsync` returns rows / null when the event is missing; `SetRaceResultsAsync` → `NotFound` (missing event or session, or session not under the event), `Invalid` (swimmer not assigned to the race / `timeMs ≤ 0` / duplicate swimmer), `Ok` (replaces, `Points=0`, `RecordedBy` set, and **`IsPersonalBest` true only when new time beats the prior best or none exists**).
- Controller: `GET /results` 200 / 404; `PUT …/races/{id}/results` 200 / 404 / **400 (non-assigned)** / **403** (non-manager).

**Frontend (Jest — not Karma)**
- `race-time.ts`: `parseTimeToMs` (`mm:ss.SS`, `ss.SS`, malformed → null), `formatMsToTime`, round-trip.
- DTO validator (results payload; reject malformed) + mapper.
- Repository impl: `getResults`/`setRaceResults` hit the right URLs and unwrap `ApiResponse`.
- Use-cases: `load-results` (valid map + invalid payload → `AppError`), `save-race-results` (maps inputs → request, adopts saved results), `load-race-schedule` (**preserves day/race `id`s** through `toRaceScheduleList`; invalid payload → `AppError`).
- `RaceResultsViewModel`: `finishedRaces` (start-passed + no results, assigned swimmers only), `resultRaces` (rank order + PB badge), `finishedCount`; `openResults`/`setTime`; `saveResults` success (adopt results, card collapses, race leaves Finished, toast) and failure (error toast, inputs intact); entry is a no-op for non-managers.

## Out of scope (this pass)

- Scoring / `Points` computation (stored as 0); any FINA-points table.
- Swimmer-facing results view (the prototype's swimmer role).
- Editing an already-recorded race from the Results tab (results are edited by re-opening the race while it is still Finished, i.e. before results exist; changing recorded times is a follow-up).
- Persisting a per-race "finished"/status flag (derived, not stored).
- Making Competition Days save preserve `race_session` ids — see Known limitations.

## Known limitations / follow-ups

- **Schedule re-save orphans results.** Competition Days save regenerates `race_session` ids (delete+insert). Once `race_result` rows exist for a session, re-saving that event's schedule leaves the results pointing at deleted session ids (orphaned; they vanish from both tabs). It does **not** affect the seed/verify flow (we never re-save the seeded schedule). **Follow-up:** change the schedule `ReplaceAsync` to preserve `race_session` ids (upsert-by-id) before results entry is used heavily in production. Flagged to the user and deferred out of this task.

## Migrations / data

- **One new migration:** `CreateRaceResultTable` (Championships — `race_result` + unique `(RaceSessionId, SwimmerId)`).
- **Apply to Aiven `Swimming_Production` first**, then seed. Per convention: `dotnet ef database update --connection "…Swimming_Production…" --context ChampionshipsDbContext`, with the running API **stopped** (DLL lock); the design-time factory hardcodes local, so `--connection` is required. Needs the Aiven password at run time.
- **`scripts/seed-race-results-aiven.sql`** (idempotent, PascalCase-quoted columns): builds on `seed-competition-schedule-aiven.sql` (already applied to Aiven), which created event `…3301`'s schedule with fixed ids:
  - Race `66666666-…-666666660001` (**50m Freestyle heats**, Day 1) has 2 assigned swimmers → seed a `race_result` for **each of its assigned swimmers** (select from `race_assignment` where `RaceSessionId = …660001`, guaranteeing valid assigned pairs), with sample times (e.g. `24560`, `25890` ms), `Points = 0`, `IsPersonalBest = true`, fixed result ids, `RecordedBy` = a seeded head-coach `app_user`. → **Results tab** shows this race.
  - Race `66666666-…-666666660002` (**100m Backstroke heats**, Day 1) has 2 assigned swimmers and no results → stays in the **Finished tab**.
  - `RecordedBy` resolved via subquery: an `app_user` whose role is `head_coach` (join `reference.role`), so no id is hard-coded.
  - `ON CONFLICT ("RaceSessionId","SwimmerId") DO NOTHING`; guarded so it inserts nothing when the session or its assignments are absent.
  - Purpose: after implementation the **Results tab** shows the 50m Freestyle with two recorded times (one PB badge), and the **Finished tab** shows the 100m Backstroke awaiting entry — confirming both the read and write paths on `…3301`.

```
-- verify:
--   SELECT rs."Id" AS session, count(rr.*) AS results
--   FROM championships.race_session rs
--   JOIN championships.competition_day d ON d."Id" = rs."DayId"
--   LEFT JOIN championships.race_result rr ON rr."RaceSessionId" = rs."Id"
--   WHERE d."EventId" = '33333333-3333-3333-3333-333333333301'
--   GROUP BY rs."Id" ORDER BY rs."Id";
```
