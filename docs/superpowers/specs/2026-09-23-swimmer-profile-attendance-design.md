# Swimmer Profile — Attendance tab (read-only) — Design

- **Date:** 2026-09-23
- **Status:** Approved (design decisions); implementation pending
- **Branch:** on local `main`; **no commits until the user says so**
- **Sibling / prior art:** `2026-09-20-swimmer-profile-records-tab-design.md` (flat read route + client-side reference resolution), `2026-09-22-swimmer-profile-feedback-design.md` (author-name enrichment at read; cross-module reference lookup), `2026-09-02-reference-lookups-design.md` (reference lookup pattern)

## Context & goal

The Swimmer Profile page (`swimmers/:id`, titled **"My Profile"** in the mock) ships seven tabs — **Identity & Vitals**, **Guardian**, **Physiological**, **InBody**, **Records**, **Health Monitoring**, **Feedback**. This feature builds the **eighth tab, Attendance** — a **read-only** calendar view of a swimmer's training attendance (`attendance.attendance_record`): a monthly grid with each day colored by status, an attendance-rate metric, and a per-day detail card showing the coach's note.

The `attendance` tab already exists as a **placeholder** in the Angular tab strip (`swimmer-profile.page.ts:33`) but is not in `enabledTabs`. This feature enables and wires it. It is the existing `swimmers/:id` page — **not** a new swimmer-facing "My Profile" route ("My Profile" is only the mock's header label; confirmed with the user).

Reference: design mocks `Desktop/mcp/2.png` and `Desktop/mcp/3.png` (My Profile → Attendance tab), and the React prototype `Desktop/4dba8937-…/src/pages/AttendancePage.tsx` — specifically its **swimmer view** `SwimmerAttendanceView` (~L429–708): summary stat cards, a month grid colored by status, and a per-day/feedback-history read-out. The prototype's **coach view** `CoachAttendanceView` (the "log today's session" write path, ~L37–425) is **out of scope** — this task is read-only display + seed only (confirmed with the user).

The prototype swimmer view also renders four **summary stat cards** (Sessions Attended, Attendance Rate, Late Arrivals, Current Streak) with hardcoded values. The mock (`2.png`) shows only the **big attendance-rate figure** above the month picker. We build the **rate metric** (computed) and treat the extra streak/late-count cards as **decoration, out of scope** (same call prior tabs made about prototype-only trend panels).

## Key architectural decisions

### 1. New `Attendance` module (its own bounded context) — user's explicit choice

Unlike prior rounds (which folded new tables into Identity/Health), `attendance.attendance_record` gets a **dedicated `Attendance` module**, mirroring the Health/Identity 4-project layout:

```
src/Modules/Attendance/
  Kheprx.BaseBackend.Attendance.Domain          (entity + repository interface)
  Kheprx.BaseBackend.Attendance.Application      (service + DTOs + messages)
  Kheprx.BaseBackend.Attendance.Contracts        (mirrors the empty/contracts sibling projects)
  Kheprx.BaseBackend.Attendance.Infrastructure   (AttendanceDbContext + factory + configuration + repository + module extension)
```

- `AttendanceDbContext` calls `HasDefaultSchema("attendance")` and maps only `attendance_record`.
- `AttendanceDbContextFactory` (design-time) hardcodes the **local** dev connection, exactly like `HealthDbContextFactory` — Aiven is targeted by passing `--connection` to `dotnet ef` (per the established migration workflow).
- One EF migration on `AttendanceDbContext` creating the `attendance` schema + `attendance_record` table.
- `AttendanceModuleExtensions.AddAttendanceModule(configuration)` registers the `DbContext` (on `ConnectionStrings:Postgres`) + repository + service; called from `Program.cs` alongside `AddIdentityModule` / `AddHealthModule`.
- New projects added to `Kheprx.BaseBackend.sln`; API project references the new Application/Infrastructure projects.

This keeps attendance isolated as its own context and leaves a clean home for the future coach-logging write path.

### 2. `reference.attendance_status` lives in the Identity module

Every reference lookup lives in Identity's `reference` schema (blood_type, gender, stroke, guardian_relation, observation_category, feedback_category). `attendance_status` follows suit — **not** in the new Attendance module:

- `AttendanceStatus` entity (`id`, `code`, `name_en`, `name_ar`) + configuration `ToTable("attendance_status", "reference")` — copy `FeedbackCategory`/`ObservationCategory`.
- Table created + **seeded via migration `HasData`** on `IdentityDbContext` with **four fixed GUIDs** for codes `present | late | absent | excused` (EN + AR). Using `HasData` (like `AddReferenceLookups`) means the rows land on Aiven when the Identity migration is applied — no separate seeder run needed for statuses.
- Exposed via `ReferenceController` `GET /api/reference/attendance-statuses` → `IReferenceService.GetAttendanceStatusesAsync` → `IReadOnlyList<CodedLookupDto>` (copy the `feedback-categories` action).

### 3. Read-only flat route (matches Records / Health-Monitoring)

Attendance is read-only in this task, so it follows the **flat read convention** (`GET /api/health-readings?swimmerId=…`) rather than InBody/Feedback's nested routes:

- `GET /api/attendance-records?swimmerId={id}` — list a swimmer's records, newest-first. `[Authorize]` (any authenticated). No POST/PUT/DELETE this task.

### 4. Status resolution stays client-side

`attendance_record.status_id` is a **loose Guid** → `reference.attendance_status` (no cross-module FK; the Attendance module has no reference to Identity). The read returns `status_id` only. The frontend fetches statuses once via `GET /api/reference/attendance-statuses` and maps `statusId → { code, nameEn, nameAr }` — the **`code` drives the calendar cell color** (present=blue, late=amber, absent=red, excused=blue-info per the mock legend), the name drives the pill/legend text. Exactly how Records resolves observation categories and Feedback resolves feedback categories.

### 5. Recorder name enriched at read, in the API layer

The mock's per-day detail card shows the note's author ("Coach Layla"). `recorded_by` is an Identity `app_user`; the JWT carries no name claim. **Decision: enrich at read in the API layer**, reusing the exact seam built for Feedback:

- `IAttendanceService.ListBySwimmerAsync` returns rows carrying `RecordedBy` (name fields empty).
- The API-layer `AttendanceRecordsController` injects `IAttendanceService` **and** `IUserService`, collects `RecordedBy` ids, resolves them via the **existing** `IUserService.GetDisplayNamesAsync(ids, ct)` (built for Feedback), and populates `RecordedByNameEn`/`RecordedByNameAr` on each DTO before returning.

Keeps the Attendance module free of Identity and always shows the current name. The Attendance service does **not** cross-check swimmer existence against Identity (follows the module norm).

### 6. Calendar model computed client-side

The API returns a flat record list; the viewmodel derives the calendar:

- **Month picker** lists the distinct months present in the data, most-recent first; **defaults to the latest month with data** (confirmed with the user). Empty state when the swimmer has no records.
- **Grid** = a Sun→Sat, 6-week matrix for the selected month; each day that has a record is colored by its status `code`; days without a record are blank.
- **Rate** = `(present + late) / (recorded sessions in the selected month)` as a rounded percentage (confirmed: **per selected month**). Shown as the big figure above the picker.
- **Per-day detail** — clicking a day with a record opens a card: the localized date, a status pill, the coach note (`coach_note_en`/`coach_note_ar` by language, may be null), and the recorder's name.

## Scope

**In scope**
- Backend: `reference.attendance_status` lookup (Identity) — entity, config, repo, migration **with `HasData` seed** (4 statuses, fixed GUIDs), reference-service method, `GET /api/reference/attendance-statuses`.
- Backend: new **`Attendance` module** — 4 projects, `AttendanceDbContext` + design-time factory, `AttendanceRecord` entity, configuration, repository, `AttendanceService`, DTOs, messages, module extension, migration creating `attendance.attendance_record`, `Program.cs` + `.sln` + API-project wiring.
- Backend: read endpoint `GET /api/attendance-records?swimmerId={id}` with recorder-name enrichment (reusing `IUserService.GetDisplayNamesAsync`).
- Frontend: the **Attendance tab** in the `swimmer-profile` slice — rate figure + legend, month `<select>`, colored calendar grid, per-day detail card; statuses resolved client-side.
- i18n (en/ar) for attendance content strings (`tabs.attendance` label already exists).
- Aiven seed data for **all existing swimmers** (see Seeding).
- Tests mirroring sibling coverage (see Testing).

**Out of scope**
- The coach "log today's session" **write path** (`CoachAttendanceView`) — no create/edit/delete UI or endpoints this task.
- The prototype's streak / late-count / sessions-attended stat cards (decoration; only the computed rate is built).
- A separate swimmer-facing "My Profile" route/page (this is the existing `swimmers/:id` page).
- The remaining `championships` tab.
- Cross-module swimmer-existence validation (follows the module norm).

## Locked decisions

1. **Read-only** — list + calendar display only; no create/edit/delete.
2. **New `Attendance` module** for `attendance_record` (decision 1); **`reference.attendance_status` in Identity** (decision 2).
3. **Flat read route** `GET /api/attendance-records?swimmerId={id}`, `[Authorize]` any authenticated (decision 3).
4. **Status resolved client-side** via `GET /api/reference/attendance-statuses`; codes `present | late | absent | excused` (decision 4).
5. **Recorder name enriched at read** in the API layer, reusing `IUserService.GetDisplayNamesAsync` (decision 5).
6. **Rate per selected month**; **month picker defaults to latest month with data** (decision 6).
7. **Four statuses** seeded (`present`, `late`, `absent`, `excused`) even though the mock legend shows three; the calendar colors all four (late=amber).
8. **Seed all existing swimmers** on Aiven (see Seeding).
9. **No commits** until the user says so.

## Data model & persistence

Two new tables. Loose Guids (no physical cross-module FKs), matching the codebase norm.

### `reference.attendance_status` (new — Identity module, `reference` schema)
Mirrors `reference.feedback_category`.

| column | type | notes |
|---|---|---|
| `id` | uuid | PK |
| `code` | varchar(50) | **unique**; `present`, `late`, `absent`, `excused` |
| `name_en` | varchar(100) | NOT NULL |
| `name_ar` | varchar(100) | nullable |

- Migration on `IdentityDbContext` creating the table + unique index on `code` (copy `CreateObservationCategoryTable`/feedback-category migration), **plus `HasData` seeding the 4 rows with fixed GUIDs** (so they land on Aiven with the migration).

### `attendance.attendance_record` (new — Attendance module, `attendance` schema)

| column | type | notes |
|---|---|---|
| `id` | uuid | PK |
| `swimmer_id` | uuid | NOT NULL; loose Guid (no FK) |
| `session_date` | date | NOT NULL |
| `status_id` | uuid | NOT NULL; loose Guid → `reference.attendance_status.id` |
| `recorded_by` | uuid | NOT NULL; loose Guid → `identity.app_user.id` |
| `coach_note_en` | text | nullable |
| `coach_note_ar` | text | nullable |
| — | — | **unique `(swimmer_id, session_date)`** — one attendance per swimmer per training day |

- Migration on `AttendanceDbContext` creating the `attendance` schema + table + the unique composite index. Additional index on `swimmer_id` to support the swimmer-scoped list.
- Read query: filter by `swimmer_id`, order `session_date` desc, tiebreak `id`.

## Backend design

### Identity module — `attendance_status` lookup
- `AttendanceStatus` domain entity (`id`, `code`, `name_en`, `name_ar`) — copy `FeedbackCategory`.
- `AttendanceStatusConfiguration` (`ToTable("attendance_status","reference")`, unique `code`), `IAttendanceStatusRepository` + `AttendanceStatusRepository` — copy the feedback-category equivalents.
- `IReferenceService.GetAttendanceStatusesAsync` + impl returning `IReadOnlyList<CodedLookupDto>`; DI registration in `IdentityModuleExtensions`.
- `ReferenceController` `GET /api/reference/attendance-statuses` (copy the `feedback-categories` action) + `ReferenceMessages.Success.AttendanceStatusesListed` (en/ar).
- Migration on `IdentityDbContext` with `HasData` for the 4 statuses (fixed GUIDs). (No `IdentitySeeder` change needed — `HasData` covers it.)

### Attendance module — `attendance_record` slice
- `AttendanceRecord` domain entity with a guarded constructor enforcing invariants (non-empty swimmer/status/recorded-by ids, `session_date` set). No `Update` method (read-only feature); a factory constructor is enough for seeding/tests.
- `AttendanceRecordConfiguration` (schema `attendance`, unique `(swimmer_id, session_date)`, `swimmer_id` index).
- `AttendanceDbContext` (+ design-time factory) mapping `AttendanceRecord` only; `AttendanceModuleExtensions.AddAttendanceModule`.
- `IAttendanceRecordRepository` + `AttendanceRecordRepository`: `ListBySwimmerAsync(swimmerId, ct)` → newest-first entities.
- DTO: `AttendanceRecordDto(Id, SwimmerId, SessionDate, StatusId, CoachNoteEn, CoachNoteAr, RecordedBy, RecordedByNameEn, RecordedByNameAr)` — `RecordedByName*` populated by the API layer, empty from the service.
- `IAttendanceService` + `AttendanceService`: `ListBySwimmerAsync(swimmerId, ct)` → newest-first `AttendanceRecordDto` (recorder names empty).
- `AttendanceMessages` (Success: Listed) with en/ar, per module convention.

### API — `AttendanceRecordsController`
- Route `api/attendance-records`, extends `BaseApiController`; injects `IAttendanceService` + `IUserService`.
- GET `?swimmerId={id}`: list via `IAttendanceService.ListBySwimmerAsync`, collect `RecordedBy` ids, resolve via `IUserService.GetDisplayNamesAsync`, project names onto each DTO, wrap in `ApiResponse`.

## Frontend design (`swimmer-profile` slice)

Mirror the Records + Feedback file set.

- **DTO / mapper / model:** `data/dto/attendance-record.dto.ts` (`AttendanceRecordDto`), `data/dto/attendance-record.mapper.ts`, `domain/model/attendance-record.ts` (`AttendanceRecord`: id, swimmerId, sessionDate, statusId, coachNoteEn, coachNoteAr, recordedBy, recordedByNameEn, recordedByNameAr).
- **Use-cases:** `list-attendance-records` (+ a `load-attendance-statuses` use-case mirroring the observation/feedback category loaders, or reuse the existing reference-loading pattern).
- **Repository:** add `listAttendanceRecords` (+ `listAttendanceStatuses`) to `swimmer-profile.repository` (interface) and `swimmer-profile.repository.impl` (HTTP), targeting the routes above.
- **ViewModel:** attendance signals — records, statuses (id→{code,nameEn,nameAr}), selected month, selected day, loading/empty. Load on tab activation. Computed: `monthOptions` (distinct months desc), `calendarWeeks` (6×7 matrix for selected month with per-day status code), `attendanceRate` (per selected month), `selectedDayDetail`.
- **Page/template:** add `'attendance'` to `enabledTabs`; render the attendance section from the mock —
  - Rate figure + legend (Present/Late/Absent/Excused, colored dots).
  - Month `<select>` (label "Monthly Attendance") bound to selected month.
  - Calendar grid: weekday header row, 6×7 day cells colored by status code, selected-day ring, note-dot indicator on days that have a coach note.
  - Per-day detail card (below the grid): date, status pill, coach note (by language), recorder name; empty text when the day has no note.
  - Empty state when the swimmer has no attendance records at all.
- **i18n:** `swimmerProfile.attendance.*` keys in `en.json` + `ar.json` (rate label, monthly-attendance label, legend labels, empty states, "no note for this day"). Status display names come from the seeded lookup, not i18n. `swimmerProfile.tabs.attendance` already exists.

## Aiven seeding

Two parts, both applied to Aiven (host `kheprx-service-kheprx.b.aivencloud.com:14647`, db `defaultdb`, user `avnadmin`, SSL require).

1. **Statuses** — seeded by the Identity migration's `HasData`; applied to Aiven with:
   `dotnet ef database update --context IdentityDbContext --connection "<aiven>"` (API stopped to release the DLL lock).
   The Attendance migration is applied the same way with `--context AttendanceDbContext`.
2. **Records (all existing swimmers)** — a one-off idempotent **SQL script** `scripts/seed-attendance-aiven.sql`, run against Aiven with `psql`. For every row in `identity.swimmer_profile`, it inserts ~6 weeks of **weekday** sessions using a `generate_series` of dates cross-joined to the swimmer roster:
   - status chosen deterministically from `(swimmer_id, session_date)` so most days are `present`, with a scattering of `absent`/`excused`/`late`, referencing the 4 fixed status GUIDs;
   - a couple of dated rows carry EN/AR `coach_note` text (so the detail card is populated, matching the mock);
   - `recorded_by = (SELECT id FROM identity.app_user WHERE role_code = 'head_coach' LIMIT 1)` (fallback: any `app_user`);
   - `ON CONFLICT (swimmer_id, session_date) DO NOTHING` for idempotency and to respect the unique constraint.

**Dependency to flag now:** both steps need the **Aiven password**, which is not stored in the repo/memory. Either the user provides it for this session, or the user runs the two commands themselves via `!` (exact `dotnet ef` + `psql` commands will be handed over). `dotnet ef` also requires the running API stopped (DLL lock).

## Testing

Mirror the coverage of the sibling tabs (backend xUnit, frontend per the repo's setup):

- **Identity:** `AttendanceStatus` entity test; attendance-status repository test; `ReferenceService.GetAttendanceStatusesAsync` test; `ReferenceController` attendance-statuses action test.
- **Attendance module:** `AttendanceRecord` entity test (invariants); `AttendanceRecordRepository` test (list newest-first, swimmer scoping); `AttendanceService.ListBySwimmerAsync` test.
- **API:** `AttendanceRecordsController` test — list enriches recorder names, filters by `swimmerId`, auth gating.
- **Frontend:** mapper spec; `list-attendance-records` (+ statuses loader) use-case spec; repository-impl spec (route/params); viewmodel spec (month options + default-to-latest, calendar-matrix construction, rate calc, selected-day detail, empty state).

## Open questions / assumptions to confirm at spec review

- **Rate definition** = `(present + late) / recorded sessions in the selected month`. Confirm whether `late` should count toward "attended" (assumed yes) and whether `excused` is excluded from the denominator (assumed **included** in denominator, not counted as attended).
- **Seed volume** = ~6 weeks of weekday sessions per swimmer. Adjust the window if a fuller/longer history is wanted.
- **`recorded_by` for seed** picks a `head_coach` app_user. Confirm that's the desired "author" for the seeded coach notes.
- Recorder-name enrichment seam (thin controller vs. small API-layer composer) deferred to the plan; the DTO shape + reuse of `IUserService.GetDisplayNamesAsync` are fixed here.
