# Swimmer profile — Championships tab — Design

- **Date:** 2026-09-24
- **Status:** Approved (design decisions); implementation pending
- **Branch:** `feat/championships`; **no commits until the user says so** (spec included)
- **Sibling / prior art:**
  - `2026-09-24-championship-finished-races-design.md` + `2026-09-23-competition-days-design.md` — establish `championships.race_result`, `race_session`, `competition_day`, `competition_event`, and the client-side stroke/distance name resolution this tab reuses.
  - The 8 existing swimmer-profile tabs (Identity & Vitals, Guardian, Physiological, InBody, Records, Health Monitoring, Attendance, Feedback) — establish the profile page's **lazy per-tab load** pattern (`SwimmerProfileViewModel.setTab` + per-tab `loaded` guard + per-tab signals) and the `/api/swimmers/{id}/…` read convention.

## Context & goal

The swimmer profile page (`SwimmerProfilePage`, route `/swimmers/:id`) has a nine-tab strip. The **Championships** tab is already present in the strip (`swimmer-profile.page.ts` `tabs`) but **disabled** — it is not in `enabledTabs`, so it renders greyed and shows nothing. This task **enables** the Championships tab and wires its content + data, following the same slice pattern as the other eight tabs.

The tab is a **read-only** view of the swimmer's championship participation: a dropdown of every championship the swimmer **joined** (enrolled in), and — for the selected one — a card (trophy, name, "dates · venue", "N races") over a **Race · Day · Time** table of that swimmer's results in that championship, with a personal-best badge on the time.

### Design references

- Mock: `Desktop/mcp/2.png` — "My Profile" page, **Championships** tab selected: heading "Championship History" / "Every championship entered, with its races and results", a **Championship** `<select>` (label · dates) top-right, then a card — trophy icon, championship name, `dates · venue`, `N races` badge — over a table with columns **Race · Day · Time · Rank**. (`Desktop/mcp/1.png` is the swimmers list, already built.)
- React prototype: `Desktop/4dba8937-…/src/pages/SwimmerProfile.tsx`:
  - `ChampionshipEntry` shape (~L29–44): `{ id, name, nameAr, dates, location, locationAr, races: [{ race, day, time, points, rank, isPB }] }`.
  - `CHAMPIONSHIP_HISTORY` sample (~L46–127): championships ordered newest-first; each with a few races.
  - `activeTab === 'championships'` block (~L1389–1511): the heading, the `champ-select` dropdown, and the selected-championship card + table.
  - **Deviations from the prototype (confirmed with the user):**
    - **Rank column removed.** The prototype's `rank` (and medal icons) are dropped — the app only stores this club's swimmers' times, so a derived rank would not reflect the true meet placing. Table columns are **Race · Day · Time** only.
    - **Points not shown.** `race_result.Points` exists but is not displayed (no scoring this pass).
    - Prototype data is hard-coded; ours comes from `championship_enrollment` + `race_result`.

### Scope decisions (confirmed with the user)

1. **Enable the existing tab.** Add `'championships'` to `enabledTabs`; no new tab entry or route.
2. **Dropdown = enrolled championships.** Lists every championship the swimmer is **enrolled in** (`championship_enrollment`), ordered by event start date **newest-first**. A championship the swimmer joined but has **no recorded results** yet still appears; selecting it shows the card with a **"No results recorded yet"** empty table (`races: []`).
3. **No rank, no points.** Table is **Race · Day · Time (+ PB badge)**. No schema change.
4. **Read-only.** No add/edit/delete. The tab is visible to any role that can open the profile (same as the other read tabs); no auth change.
5. **Client-side name resolution.** The endpoint returns `distanceId` + `strokeId`; the client builds `"50m Freestyle"` via the existing reference lookups (`LoadDistancesUseCase` + `LoadStrokesUseCase`), exactly as the Competition Days / Results tabs do. The Championships module does **not** read the Identity `reference` schema.
6. **No migration, no seed.** All tables already exist and are populated on Aiven `Swimming_Production` (Competition Days + Finished Races work). This is a pure read feature over existing data.

## Data — no new tables

Reads existing tables only:

- `championships.championship_enrollment` — which swimmers joined which event (`EventId`, `SwimmerId`).
- `championships.competition_event` — `NameEn/NameAr`, `StartDate`, `EndDate`, `LocationEn/LocationAr` (the venue).
- `championships.race_result` — `RaceSessionId`, `SwimmerId`, `TimeMs`, `IsPersonalBest` (Points ignored).
- `championships.race_session` — `DayId`, `DistanceId`, `StrokeId`, `ScheduledTime`.
- `championships.competition_day` — `EventId`, `LabelEn/LabelAr`, `DayDate`.

All ids are **loose Guids** across modules (no EF navigation / DB FK), consistent with the module.

## Backend (Championships module) — one read endpoint

### Endpoint

`GET /api/championships/swimmer/{swimmerId:guid}/history` on the existing `ChampionshipsController` →
`ApiResponse<IReadOnlyList<ChampionshipSwimmerHistoryDto>>`.

```
ChampionshipSwimmerHistoryDto(
  Guid EventId, string NameEn, string? NameAr,
  DateOnly StartDate, DateOnly EndDate,
  string LocationEn, string? LocationAr,
  IReadOnlyList<ChampionshipSwimmerRaceDto> Races)

ChampionshipSwimmerRaceDto(
  string DayLabelEn, string? DayLabelAr,
  Guid DistanceId, Guid StrokeId,
  int TimeMs, bool IsPersonalBest)
```

- Events ordered **newest-first** by `StartDate` (tie-break `EndDate` desc, then `NameEn`).
- Within an event, races ordered by `DayDate`, then `ScheduledTime` (nulls last).
- Enrolled events with no results → `Races: []`.
- Always `200` with a (possibly empty) list — an unknown/absent swimmer simply yields `[]` (no ownership guard; matches the other read tabs' loose behaviour). No `404`.

### Application

Extend `IChampionshipService` / `ChampionshipService`:

- `Task<IReadOnlyList<ChampionshipSwimmerHistoryDto>> GetSwimmerHistoryAsync(Guid swimmerId, CancellationToken ct)`:
  1. `enrolledEventIds = enrollmentRepo.ListEventIdsBySwimmerAsync(swimmerId)`.
  2. `events = eventRepo.ListByIdsAsync(enrolledEventIds)` (name / dates / venue).
  3. `lines = raceResultRepo.GetSwimmerRaceLinesAsync(swimmerId)` — the swimmer's `race_result` rows joined to `race_session` + `competition_day`, as flat rows carrying `EventId`, `DayLabelEn/Ar`, `DayDate`, `ScheduledTime`, `DistanceId`, `StrokeId`, `TimeMs`, `IsPersonalBest`.
  4. Group `lines` by `EventId`; for each enrolled event (newest-first) attach its ordered races (empty when none); map to DTO.

### Repository additions

- `IChampionshipEnrollmentRepository.ListEventIdsBySwimmerAsync(Guid swimmerId, CancellationToken)` → distinct `EventId`s where `SwimmerId = swimmerId`.
- `ICompetitionEventRepository.ListByIdsAsync(IReadOnlyList<Guid> eventIds, CancellationToken)` → `IReadOnlyList<CompetitionEvent>` — the events for those ids in one query (a batched sibling of the existing `GetByIdAsync`, returning the same `CompetitionEvent` entity).
- `IRaceResultRepository.GetSwimmerRaceLinesAsync(Guid swimmerId, CancellationToken)` → flat join rows (new `SwimmerRaceLineRow` record in `RaceResultData.cs`). One query: `race_result` → `race_session` → `competition_day`, filtered by `SwimmerId`.

No new entity, no migration.

## Frontend

### Championships feature — new read use case

- `LoadSwimmerChampionshipHistoryUseCase.run(swimmerId)` → `Result<SwimmerChampionshipHistory[]>`.
- Repository method on the existing championships repository (`GET /api/championships/swimmer/{swimmerId}/history`) + DTO + mapper. Domain model:
  ```
  SwimmerChampionshipHistory { eventId, nameEn, nameAr, startDate, endDate, locationEn, locationAr, races: SwimmerChampionshipRace[] }
  SwimmerChampionshipRace   { dayLabelEn, dayLabelAr, distanceId, strokeId, timeMs, isPersonalBest }
  ```

### Profile page wiring

- `swimmer-profile.page.ts`: add `'championships'` to `enabledTabs`.
- `SwimmerProfileViewModel`:
  - Add `'championships'` to the `activeTab` / `setTab` union.
  - Inject `LoadSwimmerChampionshipHistoryUseCase`, `LoadDistancesUseCase`, `LoadStrokesUseCase`.
  - Signals: `championshipHistory`, `distances`, `strokes`, `selectedChampId`, `loadingChampionships`; guard `championshipsLoaded`.
  - `loadChampionships()` (called from `setTab('championships')` when not loaded): `Promise.all` the history + distances + strokes; on success set signals and default `selectedChampId` to the first event; on history failure clear the guard so re-entry retries (same pattern as the other tabs).
  - `selectedChampionship` computed (find by `selectedChampId`, fallback first).
  - `raceName(distanceId, strokeId)` helper — `"<distance> <stroke>"` via the lookups, same as `RaceResultsViewModel.raceName`.
  - `formatChampTime(ms)` via existing `formatMsToTime`.
  - Reset all championships state in `load()` (per-swimmer reset, like the other tabs).
- `swimmer-profile.page.html`: the `@if (vm.activeTab() === 'championships')` section — heading + subtitle, the championship `<select>` (option label `name · dates`), and the selected-championship card (trophy, name, `dates · venue`, `N races`) over the **Race · Day · Time (+PB)** table, plus the empty-state row when `races.length === 0`.

### i18n (en + ar)

- `swimmerProfile.tabs.championships` — reuse if present, else add ("Championships" / "البطولات").
- New `swimmerProfile.championships.*`: `title`, `subtitle`, `select`, `raceCol`, `dayCol`, `timeCol`, `races` (count noun), `empty` ("No results recorded yet."), `pb`.

## Testing

- **Backend:** `ChampionshipService.GetSwimmerHistoryAsync` unit tests (fake repos): enrolled-with-results maps + orders newest-first and by day/time; joined-with-no-results yields `Races: []`; unknown swimmer → `[]`. Controller test: `200` + shape. Repository tests for the three new query methods (in-memory / provider per existing repo test style).
- **Frontend:** DTO↔model mapper spec; `LoadSwimmerChampionshipHistoryUseCase` spec; `SwimmerProfileViewModel` spec — `setTab('championships')` loads history + lookups once (guard), `selectedChampionship` tracks the dropdown, race-name resolution, empty-state when a championship has no races; page render smoke check. Uses Jest, matching the existing profile-tab specs.

## Out of scope

- Rank / finishing place, and any stored "official place" field.
- Points display / scoring.
- Any write, edit, or delete on this tab.
- A separate swimmer self-service "My Profile" page or auth changes — this stays the existing coach-facing `/swimmers/:id` profile.
- Migrations / seeds — all data already exists on Aiven `Swimming_Production`.
