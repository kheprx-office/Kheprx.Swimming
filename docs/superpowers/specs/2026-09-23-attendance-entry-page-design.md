# Attendance Entry page (coach write path) — Design

- **Date:** 2026-09-23
- **Status:** Approved (design decisions); implementation pending
- **Branch:** on local `main`; **no commits until the user says so**
- **Sibling / prior art:** `2026-09-23-swimmer-profile-attendance-design.md` (the read-only sibling — created the `Attendance` module, `attendance_record`, `reference.attendance_status`, recorder-name enrichment), `2026-09-22-swimmer-profile-feedback-design.md` (cross-module composition at the API layer), `2026-09-13-swimmers-roster-design.md` (`ISwimmerService.ListAsync` roster source)

## Context & goal

The app already has a **read-only** Attendance surface (the swimmer-profile Attendance tab: a monthly calendar of one swimmer's records). This feature builds the **complementary write path**: a standalone **Attendance Entry page** where a coach marks attendance for the **whole roster** on a chosen date.

The page already exists as a **placeholder** — `frontend/src/app/features/attendance/presentation/pages/attendance/attendance.page.ts` renders through the app shell so the "Attendance" nav item is navigable, with a comment noting "Feature content is intentionally not built yet." This task builds it out.

The backend `Attendance` module exists but is **read-only**: `AttendanceRecord` entity (`Id, SwimmerId, SessionDate, StatusId, RecordedBy, CoachNoteEn, CoachNoteAr`), a **unique index on `(SwimmerId, SessionDate)`**, `GET /api/attendance-records?swimmerId=…`, and `GET /api/reference/attendance-statuses`. There is **no write path** yet. This task adds it.

### Design references

- Mocks: `Desktop/mcp/2.png`, `Desktop/mcp/3.png` — the coach Attendance page.
- React prototype: `Desktop/4dba8937-…/src/pages/AttendancePage.tsx`, specifically **`CoachAttendanceView`** (~L37–425): a flat roster where each swimmer has a Present/Late/Absent/Excused toggle, a "General feedback" textarea, a colored "% this month" figure, "Mark all present", and a single "Save Session" bar.
- **Out of scope from the prototype:** the **Specialization dropdown and "Freestyle squad" grouping are struck out in `2.png`** — the roster is **all swimmers**, no squad/specialization concept. The prototype's *required-feedback gating* is also dropped (see decision 6). The `SwimmerAttendanceView` (~L429–708) is the already-built read-only tab, not this task.

## Key architectural decisions

### 1. "Session" endpoints on the existing `AttendanceRecordsController` (Approach A)

Model a **session = one date's roster**. Two new actions on the existing controller:

- **`GET /api/attendance-records/session?date=YYYY-MM-DD`** — `[Authorize]` (any authenticated user). Returns the whole page in one call: every swimmer, their record for that date (if any), and their attendance rate for that date's month.
- **`PUT /api/attendance-records/session`** — `[Authorize(Roles = "head_coach,captain")]`. Bulk **upsert** of the roster's entries for `date`; returns the re-loaded session.

Rejected alternatives: thin per-endpoint composition on the client (3+ calls, client owns the merge, the month-rate aggregate still wants the server) and per-swimmer POST (no atomicity, poor fit for one Save button).

### 2. Composition at the API layer (no new cross-module dependency)

The session view joins data from two modules. Following the **exact seam already in `AttendanceRecordsController`** (it composes Identity's `IUserService` for recorder names), composition stays in the **controller**:

- **Roster** ← Identity `ISwimmerService.ListAsync(search: null, ct)` → `IReadOnlyList<SwimmerListItemDto>` (`Id, Uid, NameEn, NameAr, ClubNameEn, ClubNameAr, GenderCode, Age`).
- **Records for the date** + **month rates** ← the Attendance service.

The Attendance module gains **no** reference to Identity — the controller (API project, which references both modules) does the merge. `status_id` stays a loose Guid → `reference.attendance_status`; the frontend resolves it client-side, exactly as the read-only tab does.

### 3. Upsert keyed on the existing `(SwimmerId, SessionDate)` unique index

Save is an **upsert**: for each entry, update the existing `(swimmerId, date)` record or insert a new one. Every affected row is re-stamped `RecordedBy = CurrentUserId()`. **No delete** in v1 — every roster row always carries a status (default Present, decision 5), so there is nothing to remove.

- **Domain:** add `AttendanceRecord.Update(Guid statusId, Guid recordedBy, string? noteEn, string? noteAr)` (same trim/null-collapse rules as the constructor) so existing rows can be re-marked.
- **Repository:** `ListByDateAsync(DateOnly date)`, `ListByMonthAsync(int year, int month)`, `SaveSessionAsync(IEnumerable<AttendanceRecord> toInsert, IEnumerable<AttendanceRecord> toUpdate)` (single `SaveChangesAsync`).

### 4. Month rate — `(present + late) / (present + late + absent)`, excused excluded

Per confirmed decision: **excused days count neither for nor against** the rate.

- `rate = (present + late) / (present + late + absent)`, as an integer **percent 0–100**.
- A swimmer with **no ratable records** that month (only excused, or none) → `monthRatePct = null`, rendered as "—".
- The month is the **selected date's** month (so changing the date can shift the figure).
- **Bucketing (explicit, no Identity dependency):** the Attendance service returns raw **per-swimmer counts grouped by `statusId`** for the month (`Dictionary<Guid swimmerId, Dictionary<Guid statusId, int>>`). The **controller** — which already holds the `reference.attendance_status` lookup (code→id) from Identity — maps the four codes to ids and computes `monthRatePct` per the formula. This keeps `code` semantics in the API layer and leaves the Attendance module free of any Identity/reference reference.

### 5. Default-Present is a client concern; server returns `null` for unrecorded swimmers

The session GET returns `statusId: null, hasRecord: false` for any swimmer with no record on that date. The **frontend** defaults that row's toggle to **Present** (resolved from the statuses list by `code === 'present'`) and flags it unsaved. On Save, **every** row is sent (including untouched Present defaults), so a first save for a fresh date writes a Present record for the whole roster unless the coach changed it. "Mark all present" re-applies Present to all rows. This keeps the server from inventing records and keeps default-Present a UI decision.

### 6. Feedback is optional; single textarea maps to the note column by UI language

- **Optional:** no save gating (the prototype's "N swimmers still need feedback" block is dropped). Save is enabled whenever the roster is **dirty**.
- **Language mapping (confirmed):** the single "General feedback" textarea maps to `CoachNoteEn` / `CoachNoteAr` **by current UI language** (`AppLanguage.Current`). Save routes the text to the matching column; the session GET returns `coachNote` **resolved to the current language**, falling back to the other column when the current is empty. Known limitation (acceptable v1): editing a note under a different UI language than it was written can leave the other column stale.

## API contracts

```
GET /api/attendance-records/session?date=YYYY-MM-DD        [Authorize]
→ ApiResponse<AttendanceSessionDto>

AttendanceSessionDto {
  date: DateOnly,
  rows: SwimmerSessionRowDto[]
}
SwimmerSessionRowDto {
  swimmerId, uid, nameEn, nameAr, clubNameEn, clubNameAr, genderCode,
  statusId: Guid?,        // null when no record for this date
  coachNote: string?,     // resolved to current UI language
  monthRatePct: int?,     // 0–100, null when no ratable records
  hasRecord: bool
}

PUT /api/attendance-records/session                        [Authorize(Roles="head_coach,captain")]
body SaveSessionRequest {
  date: DateOnly,
  entries: SaveEntry[]     // SaveEntry { swimmerId: Guid, statusId: Guid, coachNote: string? }
}
→ ApiResponse<AttendanceSessionDto>   // re-loaded session (refreshed rates)
```

Validation: malformed `date` → 400. Any `statusId` not in `reference.attendance_status` → 400. Duplicate `swimmerId` in `entries` → 400. Empty roster → `rows: []`.

## Frontend design

Build out `attendance.page.ts` / `.html` (feature: `features/attendance`), plus a data-access service.

- **Data access:** `AttendanceEntryService` (Angular service) with `getSession(date)` and `saveSession(payload)` hitting the two endpoints, plus reuse of the existing attendance-statuses fetch.
- **State (signals):** `selectedDate` (defaults to today), `statuses`, `rows` (working copy), `loading`, `saving`, `dirty` (derived from row edits).
- **Load flow:** on init and on date change → `getSession(date)`; map each row, defaulting `statusId === null` to the Present status id and marking it unsaved.
- **Row UI (per `2.png`/`3.png`):** left color rail by status; avatar initials; name (EN/AR by UI language); colored "% this month" (`< 70` red, `< 85` amber, else green; "—" when null); collapsible body with the Present/Late/Absent/Excused toggle and the optional "General feedback" textarea.
- **Header:** a **date picker** (bound to `selectedDate`) + "Mark all present" + a `present+late / total` counter.
- **Save bar:** single "Save Session" → `PUT`; enabled when `dirty`; on success, refresh from the returned session and clear `dirty`. Non-coach users (no `head_coach`/`captain` role) get a 403 → show a friendly "not permitted" message (page is still viewable).
- **i18n:** add feature keys under the existing `attendance` namespace (EN + AR); `shell.nav.attendance` already exists.
- **Colors** reuse the read-only tab's status→color mapping (`present`, `late=amber`, `absent=red`, `excused=blue-info`) for consistency.

## Error handling

- Invalid `date` / unknown `statusId` / duplicate swimmer → 400 with a localized message.
- Save wrapped in one `SaveChangesAsync`; on failure return a localized 500-style `ApiResponse.Failure`; the client keeps the working copy and surfaces a retry toast.
- Empty roster → empty state in the page body.

## Testing

**Backend**
- Domain: `AttendanceRecord.Update` sets fields, trims/nulls blank notes.
- Repository: `SaveSessionAsync` insert path, update path, and that the `(SwimmerId, SessionDate)` unique index is respected (no duplicate for same swimmer+date); `ListByDateAsync` / `ListByMonthAsync` filter correctly.
- Service: month-rate buckets — present+late over present+late+absent, excused excluded from both; null when no ratable records.
- Controller: session compose (roster ∪ records ∪ rates, unrecorded → null/`hasRecord:false`); save round-trip; **auth** — `PUT` returns 403 for a non-coach, 200 for `head_coach`/`captain`; validation 400s.

**Frontend (Jest — not Karma)**
- Default-Present mapping for `statusId: null` rows.
- "Mark all present" sets every row to Present and marks dirty.
- Dirty tracking enables/disables Save; feedback edits don't gate save.
- Save posts every row (including untouched defaults) and refreshes from the response.
- Month-rate color thresholds and "—" for null.

## Out of scope (v1)

- Deleting attendance records; specialization/squad grouping; the required-feedback gate; recorder-name display on the entry page; streak / late-count summary cards; editing notes in both languages simultaneously.

## Migrations / data

No schema changes — `attendance_record` and `reference.attendance_status` already exist (created by the read-only sibling). No new migration. Targets the live Aiven DB **`Swimming_Production`** per the standing DB note (already holds these tables).
