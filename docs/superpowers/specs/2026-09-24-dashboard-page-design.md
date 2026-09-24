# Dashboard Page — Design

**Date:** 2026-09-24
**Status:** Approved design; implementation pending
**Branch:** feat/championships (current)

## Purpose

Replace the current `/home` launcher (a grid of navigation cards) with a **data
dashboard**: a coach/captain-facing overview landing page that surfaces
club-wide stats at a glance. Design reference is the React prototype
`Dashboard.tsx` (design-only, per project convention); the target is the Angular
app in `frontend/`.

The sidebar's "Dashboard" nav item already points to `/home`, so replacing the
page needs no nav change — the launcher's job (navigation) is already covered by
the sidebar.

## Intended outcome

A logged-in user landing on `/home` sees, in one read-only page:

- **Total swimmers** with a **"+N this month"** new-registration delta.
- **Attendance ring** — club-wide attendance % for the **current calendar month**.
- **Weekly attendance** — present/absent bars for the **last 7 recorded session days**.
- **Stroke split** — swimmers grouped by stroke specialization.

All in the app's existing Arabic-first RTL design system, with no database changes.

## Scope decisions (confirmed with product owner)

1. **Placement:** the data dashboard **replaces** `/home`. The launcher-card grid
   retires (navigation already lives in the sidebar).
2. **Widgets:** all four of the above are in scope.
3. **Attendance window:** ring = **current calendar month**; chart = **last 7
   recorded session days**.
4. **Backend shape:** **one composite endpoint** (`GET /api/dashboard/summary`),
   composed at the API layer — not granular per-widget endpoints, and not
   client-side aggregation.
5. **No charting library:** the app has none (Tailwind + lucide only). The ring is
   hand-rolled SVG; the weekly chart is lightweight CSS/SVG bars.
6. **No database changes:** pure read-side aggregation over existing data.

## Data reality (grounding)

Confirmed against the current codebase:

| Metric | Source | Status |
| --- | --- | --- |
| Swimmer count | `ISwimmerService.GetCountAsync()` | ✅ exists |
| New this month | `SwimmerProfile.CreatedAt` (exists) | new count method |
| Month attendance rate | `IAttendanceService.GetMonthStatusCountsAsync(year, month)` | ✅ reuse, aggregate across all swimmers |
| Last-7-days present/absent | attendance records per date | new query |
| Stroke split | `SwimmerSpecialization` (many-to-many, **no primary stroke**) + `reference.stroke` | new query |

Attendance statuses (`reference.attendance_status`) are `present`, `late`,
`absent`, `excused`. The canonical rate formula, already used in
`AttendanceRecordsController.BuildSessionAsync`, is:

```
attended = present + late
denom    = present + late + absent      // excused excluded from both
rate     = round(attended * 100 / denom)   // null when denom == 0
```

The dashboard reuses this exact convention for both the ring and the daily chart
(daily `Present` = present+late, `Absent` = absent).

## Backend design (Approach A — composite endpoint)

### Endpoint

`GET /api/dashboard/summary` — `[Authorize]` (any authenticated role, matching the
`/home` audience). New `DashboardController : BaseApiController` in
`Kheprx.BaseBackend.Api/Controllers`, composing across the Identity and Attendance
modules — the same API-layer composition pattern `AttendanceRecordsController`
uses (it already stitches Identity roster + Attendance records + reference
statuses).

### Response DTO

```csharp
public sealed record DashboardSummaryDto(
    int  SwimmerCount,
    int  NewThisMonth,
    int? MonthAttendanceRatePct,                     // null if no records this month
    IReadOnlyList<DailyAttendanceDto> Last7Days,     // oldest -> newest
    IReadOnlyList<StrokeSplitItemDto> StrokeSplit);

public sealed record DailyAttendanceDto(DateOnly Date, int Present, int Absent);

public sealed record StrokeSplitItemDto(
    Guid StrokeId, string Code, string NameEn, string? NameAr, int Count);
```

DTO location: the composite `DashboardSummaryDto` (and its `DailyAttendanceDto` /
`StrokeSplitItemDto` members) live in the **API project's `Models` folder**
(`Kheprx.BaseBackend.Api/Models/DashboardDtos.cs`). That folder exists precisely
for API-layer models, and the dashboard is a cross-module API-layer composition
with no owning domain module — so it does not belong in any single module's
Application layer. The **inputs** the controller composes from (the new
stroke-split and recent-daily-count query results, and the new-this-month count)
return simple types or small DTOs in their **own** modules' Application/Domain
layers; the controller maps those into the API-layer `DashboardSummaryDto`.

### How each field is produced

- **SwimmerCount** — existing `ISwimmerService.GetCountAsync()`.
- **NewThisMonth** — new `ISwimmerService` method (e.g. `GetNewThisMonthCountAsync`)
  counting `SwimmerProfile.CreatedAt >= firstOfMonthUtc`. "This month" = current
  calendar month in UTC (consistent with how `CreatedAt` is stamped
  `DateTime.UtcNow`).
- **MonthAttendanceRatePct** — reuse `IAttendanceService.GetMonthStatusCountsAsync(
  now.Year, now.Month)`, which returns per-swimmer per-status counts. Sum across
  all swimmers to get club totals, then apply the canonical formula. No new
  attendance query needed for the ring.
- **Last7Days** — one **new Attendance query**: return per-date status counts for
  the 7 most recent **distinct** session dates that have any record. Map to
  `DailyAttendanceDto` with `Present = present+late`, `Absent = absent`, ordered
  oldest→newest for left-to-right (RTL: start→end) rendering.
- **StrokeSplit** — one **new Identity query** grouping `SwimmerSpecialization` by
  `StrokeId`, joined to `reference.stroke` for `Code`/`NameEn`/`NameAr`. Every
  stroke that has ≥1 swimmer appears; a swimmer specializing in N strokes counts
  in N rows (see caveat).

Status GUID→code resolution (`present`/`late`/`absent`) via the existing
`IReferenceService.GetAttendanceStatusesAsync`, exactly as the attendance
controller does.

### New backend surface summary

- 1 new controller (`DashboardController`) + 1 composite DTO file.
- 1 new `ISwimmerService` count method + its repository query.
- 1 new stroke-split query (Identity repository/service).
- 1 new recent-daily-counts query (Attendance repository/service).
- Everything else is composition and reuse.

## Frontend design

### Feature structure

New `features/dashboard/`, mirroring the existing feature layout (swimmers,
attendance, championships):

```
features/dashboard/
  domain/
    models/dashboard-summary.ts             # DashboardSummary, DailyAttendance, StrokeSplitItem
    repositories/dashboard.repository.ts     # abstract repository
  data/
    repositories/dashboard.repository.impl.ts  # HTTP GET /api/dashboard/summary via core datasource
  presentation/pages/dashboard/
    dashboard.page.ts
    dashboard.page.html
    dashboard.viewmodel.ts                   # signals: summary(), loading(), error()
  testing/
    data/repositories/dashboard.repository.impl.spec.ts
    presentation/pages/dashboard/dashboard.viewmodel.spec.ts
  index.ts                                   # barrel
```

### Data flow

`DashboardPage` → `DashboardViewModel` (provided via the route, like
`SwimmerProfileViewModel`) → `DashboardRepository` → core HTTP datasource. The
ViewModel loads once on init and exposes `summary` / `loading` / `error` signals.
Read-only page — no writes.

### UI / layout

Adapt the mockup to the **app's real design system** (not the React prototype's
Tailwind classes). The current `/home` establishes the vocabulary reused here:
`dir="rtl"` root, `<app-decor-background>`, `gradient-text`,
`bg-surface`/`border-border`/`text-ink`/`text-text-secondary`, `rounded-2xl`,
`shadow-sm`, `animate-fade-in`, lucide icons, `TranslatePipe`. No new colors, no
new dependencies.

Three stacked regions, RTL-first:

1. **Hero band** — reuses the existing home greeting (`مرحباً بعودتك` + user name
   via `gradient-text`) plus a short status line. On the far side, the
   **attendance ring**: hand-rolled SVG donut showing `MonthAttendanceRatePct%`
   with an "attendance this month" caption. A compact stat block shows the
   **swimmer count** with the **`+N this month`** delta (lucide `TrendingUp`).
   Two-column on desktop, stacked on mobile.
2. **Weekly attendance** (wider card) — "الحضور الأسبوعي": the last 7 recorded
   session days as CSS/SVG present-vs-absent bars, each with a date label, plus a
   small Present/Absent legend. Pure markup, no chart lib.
3. **Stroke split** (narrower card) — "توزيع السباحين حسب السباحة": total
   registered swimmers as the header number, then one row per stroke (name via
   `NameAr`/`NameEn`, count, proportion bar), and a footnote.

### Stroke-overlap handling (honesty note)

`SwimmerSpecialization` is many-to-many with **no primary stroke**, so a swimmer
with two strokes counts in both bars — bars can sum to more than the roster. The
mockup's "sums-to-total" look is therefore not honest; instead:

- The header shows the **real total registered swimmers** (`SwimmerCount`).
- Each stroke row's proportion bar width is computed **relative to the largest
  stroke count**, not as a share of a total — so we never imply the rows add up
  to the roster.
- Footnote: *"Swimmers may specialize in more than one stroke."* (Arabic
  equivalent.)
- Swimmers with no stroke simply don't appear in any row.

### Empty / partial states

- No attendance records this month → ring shows "—" with a "no sessions yet"
  subtitle (`MonthAttendanceRatePct` is null).
- Fewer than 7 recorded days → chart renders however many exist.
- Zero swimmers → count 0, empty stroke list with an empty-state message.

### i18n

New `dashboard.*` keys in both `core/i18n/en.json` and `ar.json`. Day and number
formatting follows current app conventions. Arabic is the default.

## Routing & navigation change

- `app.routes.ts`: the `home` route swaps `loadComponent` from `@features/home`
  → `@features/dashboard` (`DashboardPage`), with `DashboardViewModel` added to
  `providers`. Path stays `/home`; `firstLoginGuard` stays; the `'' → 'home'`
  redirect is untouched.
- **Sidebar:** no change — "Dashboard" already points to `/home`.
- **Retire the launcher:** delete `features/home` (page, template, spec, barrel).
  Its only consumer is the one route line. The greeting-header markup is lifted
  into the dashboard hero, so nothing visual is lost. Verify there are no other
  imports of `@features/home` before deleting.

## Testing

### Backend

`DashboardController` unit tests with mocked services (mirroring
`AttendanceRecordsControllerSessionTests`):

- Correct composition of all four numbers from mocked service outputs.
- Month-rate formula matches `(present+late)/(present+late+absent)`, excused
  excluded.
- **Null rate when there are no records this month.**
- `Last7Days` ordering (oldest→newest) and present/absent mapping.
- Stroke-split grouping and name resolution.

Plus targeted tests for the two new query methods, following the existing
repository-spec pattern.

### Frontend (Jest)

- `dashboard.repository.impl.spec` — HTTP response → domain-model mapping.
- `dashboard.viewmodel.spec` — loading / success / error / empty-state signals.
- Remove `home.page.spec` along with the retired feature.

## Database impact

**None.** No new tables, no columns, no migration, no Aiven seed. Pure read-side
aggregation over existing data — a significant de-risker compared to recent
feature work.

## Out of scope / YAGNI

- No charting library.
- No per-widget granular endpoints.
- No configurable date ranges / filters on the dashboard (fixed windows as
  specified).
- No primary-stroke concept added to the data model.
- No month-over-month attendance trend beyond the last-7-days chart.
- No role-specific dashboard variants — all authenticated roles see the same page.
