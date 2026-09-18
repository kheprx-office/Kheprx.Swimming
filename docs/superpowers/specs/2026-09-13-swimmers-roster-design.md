# Swimmers Roster Page — Design

**Date:** 2026-09-13
**Status:** Approved (design); pending spec review
**Type:** Full-stack feature slice (backend list endpoint + Angular page)

## Context

The Angular app has a scaffolded `swimmers` feature wired only for **count** and
**create**; the `/swimmers` route currently renders a placeholder page. We want a
**Swimmers roster** page — a searchable, filterable list of registered swimmers —
matching the provided React reference design (`SwimmersPage.tsx`), fed by the .NET
backend.

The backend currently exposes only `GET /api/swimmers/count` and
`POST /api/swimmers`. There is **no list endpoint**, so this adds a new read path
end-to-end.

### Reference design vs. backend reality

The React reference shows an attendance % bar, a colored health badge, and a
14-digit national ID per row. Three of these have **no backend source**. Resolved
during brainstorming:

| Reference element        | Backend reality                          | Decision |
|--------------------------|------------------------------------------|----------|
| Attendance % bar + badge | No attendance data model exists at all   | **Omit entirely** (not hidden — removed) |
| 14-digit national ID     | Swimmers have no NID field; only `Username` + generated `Uid` | Show **`Uid`** (`SW-0001`) |
| Search + gender filter   | `GET /api/users?search=` pattern exists  | **Server-side search**, **client-side gender filter** |

## Goal

Replace the placeholder Swimmers page with a working roster that lists every
registered swimmer with name, UID, training club, gender, and age — searchable by
name/UID (server-side) and filterable by gender (client-side).

## Scope

### In scope
- New `GET /api/swimmers` list endpoint (backend).
- New Angular data → domain → presentation path for the roster.
- Search (server), gender filter (client), loading / empty / error states, i18n.

### Out of scope (explicit)
- Attendance % / health status — no data source.
- Swimmer **detail page** — rows are plain display rows (no navigation).
- National-ID field on the swimmer entity — using `Uid` instead.
- Role/club scoping — the endpoint returns **all** swimmers.
- Pagination — none, consistent with the rest of the app (club-sized rosters).

## Backend design

Mirrors the existing `GET /api/users?search=` list pattern
(`UsersController.List` → `UserService.ListAsync` → `UserRepository.ListAsync`,
EF Core `.AsNoTracking().Where(ILike).OrderBy().ToListAsync()`).

### Endpoint
- `SwimmersController` gains: `[HttpGet] [Authorize]`, `[FromQuery] string? search`,
  returns `ApiResponse<IReadOnlyList<SwimmerListItemDto>>`.
- **Authorization decision:** `[Authorize]` — any authenticated user may view the
  roster. (Can be tightened to `Roles = "head_coach,captain"` later; noted for
  review.)
- Success message: new `SwimmerMessages.Success.SwimmersListed`.

### DTO — `SwimmerListItemDto`
```csharp
public sealed record SwimmerListItemDto(
    Guid Id,
    string Uid,           // "SW-0001" — the row sub-text
    string NameEn,
    string? NameAr,
    string? ClubNameEn,   // training club
    string? ClubNameAr,
    string GenderCode,    // stable code for client-side filtering + label lookup
    int? Age);            // computed from Dob; null when Dob unknown
```
Both En/Ar names travel to the client; the Angular page selects by active language
(same approach the app already uses for gender labels).

### Repository — `ISwimmerProfileRepository.ListAsync(string? search, ct)`
One EF Core **projection join**:
- `SwimmerProfiles` ⋈ `Users` on `UserId`
- left-join `Clubs` on `TrainingClubId`
- left-join `Genders` on `GenderId`
- Search: case-insensitive `ILike` over `NameEn`, `NameAr`, `Uid` (escape
  `\ % _` like the users query).
- `OrderBy(NameEn)`.
- Returns a lightweight read-model row (projection record) — layer/location of this
  read-model type to follow the current codebase convention at implementation time.

### Service — `SwimmerService.ListAsync(search, ct)`
Maps rows → `SwimmerListItemDto`:
- **Age** = `today.Year - dob.Year`, minus 1 if the birthday hasn't occurred yet
  this year; `null` when `Dob` is null.
- **GenderCode** = the joined `Gender.Code` (or empty string when gender unknown).
- Club name fields passed through from the join.

## Frontend design (Angular)

### Data → domain (mirrors existing count/create wiring)
- `data/dto/swimmer-list.dto.ts` — `SwimmerListItemDtoRs` item + envelope
  `extends BaseResponseRs<...>`, plus a validity guard (consistent with
  `swimmer-count.dto.ts`).
- `domain/model/` — `SwimmerListItem` model.
- `domain/repositories/swimmer.repository.ts` — add `list(search?: string)`.
- `data/repositories/swimmer.repository.impl.ts` — `http.get('/api/swimmers', { query: { search } })`.
- `domain/usecases/list-swimmers.use-case.ts` — calls repo, maps DTO → model list.

### Presentation — replaces the placeholder `SwimmersPage`
- **State (signals):** `swimmers`, `loading`, `error`, `search` (debounced → re-calls
  the API), `genderFilter` (`'all' | 'male' | 'female'`, applied client-side).
- **Layout:**
  - Header — title + description (i18n).
  - Controls row — search input (server search, debounced ~300ms) + gender select
    (client filter) + live count of visible rows.
  - Roster list — per row: avatar initials (derived client-side), name (by
    language), `Uid` sub-text, training club name (by language), `gender · age`
    (age → "—" when null). **No attendance bar/badge. No chevron** (rows are
    non-interactive display rows).
  - Loading skeleton, empty state, error state.
- **Styling:** built with the app's own Angular/Tailwind design tokens
  (ink/card/border, etc.) to match sibling pages — **not** a verbatim port of the
  React classes. Visual result mirrors the reference minus attendance.
- i18n keys added to `core/i18n/dictionaries.ts` (En + Ar).

## Testing

- **Backend:** `SwimmerService.ListAsync` unit test — row→DTO mapping, age
  computation (incl. birthday-not-yet-passed and null Dob), search passthrough.
  Repository test if an integration harness exists.
- **Frontend:** `list-swimmers.use-case` spec + `swimmer.repository.impl` spec
  (mirroring the existing swimmer specs) + a page render test (list, empty, loading,
  gender filter).
- **Note:** stop the running backend before `dotnet test` — it locks the build
  output DLLs.

## Risks / notes
- Attendance is the reference's headline signal; omitting it changes the page's
  emphasis. Acceptable until an attendance subsystem exists (would be its own spec).
- `[Authorize]`-only visibility is intentionally broad; revisit if roster data
  should be role-scoped.
- No pagination — fine for current roster sizes; revisit if a club exceeds a few
  hundred swimmers.
