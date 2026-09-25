# Swimmer Self-Service Portal — Design

- **Date:** 2026-09-25
- **Status:** Approved design — ready for implementation planning
- **Branch context:** `feat/championships`
- **Type:** Architectural (role-based navigation restructure + new self-service endpoint + backend authorization hardening)

## Context & Intent

Today the app is coach-facing. A swimmer who finishes the first-login onboarding
wizard lands on `/home` (the coach-oriented dashboard) and sees the full coaching
sidebar (Dashboard, Swimmers, Attendance, Championships, Settings) — none of which
is appropriate for a swimmer. The `user-role.ts` comment already anticipates the
gap: *"swimmers self-serve."*

**What the user asked for (verbatim intent):** after onboarding, a swimmer's menu
should show just their own **profile ("Swimmer")** and **Settings**; the profile
should present the swimmer's own data in the same **tabbed layout as the captain
view**, and every tab should be **display-only (no editing)**.

**Agreed decisions (from clarifying questions):**

1. **Data access:** *backend-enforced self-only* — a swimmer may read only their own
   record, enforced server-side (not just hidden in the UI).
2. **Swimmer menu:** *My Profile + Settings only* — all coach items hidden.
3. **Landing:** swimmer lands on *My Profile* after onboarding and on future logins.
4. **Tabs:** *all 9*, exactly like the captain view, read-only.

### Success criteria

- A swimmer logging in (or completing onboarding) lands on `/my-profile` and sees a
  sidebar with only **My Profile** and **Settings**.
- `/my-profile` shows the swimmer's own data across all 9 tabs (Identity & Vitals,
  Guardian, Physiological, InBody, Records, Health Monitoring, Attendance,
  Championships, Feedback), fully read-only.
- A swimmer cannot read another swimmer's data — via the UI *or* the API (any attempt
  with a foreign id returns 403).
- Coaches (`head_coach`, `captain`) are unaffected: same menu, same editable profile
  at `/swimmers/:id`, same access to every swimmer.

## Current-State Facts (verified during exploration)

- **Roles:** `head_coach | captain | swimmer` (`core/domain/roles/user-role.ts`).
- **Menu:** `layout.component.ts` builds a hardcoded `allGroups`; items already support
  per-item `roles?: UserRole[]` filtering, but currently only *Captain Panel* is gated.
  So a swimmer currently sees every non-captain item.
- **Settings already exists** as `/account` (`AccountPage`), shown to all roles.
- **Profile page is already read-only for swimmers:** `SwimmerProfileViewModel.canEdit()`
  = `role === 'head_coach' || role === 'captain'`. Every edit/add/delete affordance in
  `swimmer-profile.page.html` is gated on `vm.canEdit()`. No read-only work is needed —
  only reliance + tests.
- **All 9 tabs are enabled** in `swimmer-profile.page.ts` (`enabledTabs`).
- **Backend reads are any-authenticated; writes are coach-restricted.** Every GET the
  profile page uses is `[Authorize]` (any logged-in user); POST/PUT/DELETE are
  `[Authorize(Roles = "head_coach,captain")]`. This means a swimmer can already *read*
  the data, but also that **any authenticated user can read any swimmer by id (IDOR)** —
  which decision (1) closes.
- **`CurrentUserId()` and `User` (role claims) are available in controllers**, and
  `ISwimmerService.GetSwimmerIdByUserAsync(userId)` resolves a user's own swimmer id
  (already used by the onboarding endpoints).
- **Landing is already role-mapped:** `login.viewmodel.ts` has
  `LANDING_ROUTE_BY_ROLE` (currently all three roles → `/home`) and navigates there
  post-login. Onboarding completion (`onboarding.viewmodel.ts` `submit()`) navigates to
  `/home`. `firstLoginGuard` sends a first-login swimmer to `/onboarding`.

## Architecture Overview

The feature is a thin composition over existing pieces:

- **Reuse** the existing `SwimmerProfilePage` + `SwimmerProfileViewModel` unchanged for
  the read-only swimmer view — read-only already falls out of `canEdit()`.
- **Add one backend endpoint** (`GET /api/swimmers/me`) so the frontend can resolve the
  caller's own swimmer id without knowing it.
- **Add a backend authorization guard** that limits swimmers to their own record on the
  swimmer-scoped read endpoints (closes the IDOR).
- **Restructure the sidebar** by role and add role-based route guards + landing.

## Component Design

### 1. Backend — `GET /api/swimmers/me`

Resolves the calling swimmer's own profile id so the frontend can drive the existing
id-based tab loads.

- **Route:** `GET /api/swimmers/me`
- **Auth:** `[Authorize(Roles = "swimmer")]`
- **Handler:** `SwimmersController` → `_service.GetSwimmerIdByUserAsync(CurrentUserId())`.
- **Response 200:** `ApiResponse<MySwimmerRefDto>` where
  `public sealed record MySwimmerRefDto(Guid SwimmerId);`
- **Response 404:** when the user has no swimmer profile (`GetSwimmerIdByUserAsync` → null),
  `ApiResponse<MySwimmerRefDto>.Failure(..., "not_found")`.

Returning just the id (not the full profile) keeps the existing profile-load flow
untouched: the page resolves the id, then runs the normal `GET /api/swimmers/{id}` +
per-tab sequence.

### 2. Backend — self-only enforcement (Approach 1: shared guard)

**New API-layer service** `ISwimmerSelfAccessGuard` (impl `SwimmerSelfAccessGuard`),
placed in the API composition root (it may depend on Identity's `ISwimmerService`):

```csharp
public interface ISwimmerSelfAccessGuard
{
    // True if the caller may read the target swimmer's data.
    // Coaches (head_coach/captain) → always true.
    // Swimmers → true only when targetSwimmerId == their own swimmer id.
    Task<bool> CanReadAsync(ClaimsPrincipal user, Guid targetSwimmerId, CancellationToken ct);
}
```

Logic:

```csharp
if (!user.IsInRole("swimmer")) return true;              // coaches unrestricted
var mine = await _swimmers.GetSwimmerIdByUserAsync(userId, ct);
return mine is { } id && id == targetSwimmerId;
```

**Applied to every swimmer-scoped read endpoint.** At the top of each handler:

```csharp
if (!await _access.CanReadAsync(User, targetSwimmerId, ct))
    return StatusCode(StatusCodes.Status403Forbidden,
        ApiResponse<T>.Failure(SwimmerMessages.Errors.Forbidden(AppLanguage.Current), "forbidden"));
```

A new localized message key `SwimmerMessages.Errors.Forbidden` (EN/AR) is added for the
403 body, following the existing `SwimmerMessages` pattern.

Endpoints to guard (target id from route `{id}` or query `swimmerId`):

| Controller | Endpoint | Id source |
|---|---|---|
| `SwimmersController` | `GET /api/swimmers/{id}` (profile) | route |
| `SwimmersController` | `GET /api/swimmers/{id}/medical-exams` | route |
| `SwimmersController` | `GET /api/swimmers/{id}/guardians` | route |
| `SwimmersController` | `GET /api/swimmers/{id}/body-measurement` | route |
| `InBodyReadingsController` | `GET /api/swimmers/{id}/inbody-readings` | route |
| `FeedbackEntriesController` | `GET /api/swimmers/{id}/feedback-entries` | route |
| `ObservationsController` | `GET /api/observations?swimmerId=` | query |
| `HealthReadingsController` | `GET /api/health-readings?swimmerId=` | query |
| `AttendanceRecordsController` | `GET /api/attendance-records?swimmerId=` (flat list) | query |
| `ChampionshipsController` | swimmer championship-history GET (backing `LoadSwimmerChampionshipHistoryUseCase`) | route |

Not guarded (not swimmer-specific, stay any-authenticated): reference lookups
(`ReferenceController` — blood types, fitness assessments, observation/feedback
categories, attendance statuses, distances, strokes). The roster
`GET /api/swimmers` (list) and coach-only writes are out of a swimmer's reach at the
route level (see §5) and unchanged here.

**Efficiency note:** each guarded swimmer request costs one indexed
`GetSwimmerIdByUserAsync` lookup. A profile load is ~10 requests → ~10 lookups, which is
negligible. Adding a `swimmer_id` JWT claim to avoid the lookups is a documented future
optimization, out of scope here.

### 3. Frontend — `/my-profile` route + own-id resolution

- **Route:** `/my-profile` inside the shell, `canActivate: [authGuard, firstLoginGuard, roleGuard('swimmer')]`, provides `SwimmerProfileViewModel`, loads the existing `SwimmerProfilePage`.
- **Own-id resolution:** new vertical slice mirroring existing patterns:
  - DTO `MySwimmerRefDtoRs { swimmerId: string }`
  - `ISwimmerOnboardingRepository`/a profile repository method `getMySwimmerId()` → `GET /api/swimmers/me` (place alongside the existing swimmer-profile data layer)
  - `GetMySwimmerIdUseCase` returning the id (or a failure)
- **Page change (`swimmer-profile.page.ts`):** `ngOnInit` currently reads
  `route.snapshot.paramMap.get('id')`. Change to: if an `:id` param is present (coach
  route) use it; otherwise (the `/my-profile` route) resolve via `GetMySwimmerIdUseCase`,
  then call `vm.load(id)`. Everything downstream (all tab loads, read-only rendering) is
  unchanged.
- **Read-only:** automatic — `canEdit()` is false for `swimmer`.

### 4. Frontend — menu role-gating (`layout.component.ts`)

Update `allGroups`:

- Overview → Dashboard: add `roles: ['head_coach', 'captain']`.
- Coaching group (Swimmers, Attendance, Championships): add `roles: ['head_coach', 'captain']` to each item.
- Add **My Profile** item: `{ label: 'shell.nav.myProfile', icon: LucideUser, route: '/my-profile', roles: ['swimmer'] }` (import `LucideUser` from `@lucide/angular`; placed in the Overview group).
- Captain Panel: unchanged (`roles: ['head_coach', 'captain']`).
- Settings (`/account`): unchanged (no `roles` → visible to all).

The existing `navGroups` computed already filters items by role and drops empty groups,
so a swimmer sees exactly **My Profile** (Overview) and **Settings** (Administration).

- **i18n:** add `shell.nav.myProfile` to `en.json` and `ar.json`.

### 5. Frontend — landing & route guards

- **Login landing:** `LANDING_ROUTE_BY_ROLE.swimmer = '/my-profile'` (coaches stay `/home`).
- **Onboarding completion:** `onboarding.viewmodel.ts` `submit()` success navigates to
  `/my-profile` (was `/home`). (Only swimmers complete onboarding.)
- **`/home` redirect for swimmers:** add a guard on the `home` route that redirects
  `swimmer` → `/my-profile` and lets coaches through. This covers the `'' → home`
  default and the `roleGuard`→`/`→`home` fallback without creating a redirect loop
  (a swimmer never "rests" on `/home`).
- **Hard-gate coach routes:** add `roleGuard('head_coach', 'captain')` to `swimmers`,
  `swimmers/:id`, `attendance`, `championships`, `championships/:id` so a swimmer cannot
  reach them by typing the URL. `captain-panel*` is already gated. `/my-profile`,
  `/account`, and `/change-password` remain swimmer-accessible. A swimmer blocked by
  `roleGuard` goes to `/` → `home` → (home guard) → `/my-profile`.

### 6. Read-only (no code change)

`canEdit()` already gates every edit/add/delete control across all tabs. The design
relies on this and adds tests asserting a swimmer sees no edit affordances; no template
or viewmodel changes are required for read-only.

## Data Flow — swimmer opens their profile

1. Swimmer logs in → `login.viewmodel` navigates to `/my-profile`
   (or onboarding completion navigates there).
2. `roleGuard('swimmer')` + `firstLoginGuard` admit the route.
3. `SwimmerProfilePage.ngOnInit`: no `:id` param → `GetMySwimmerIdUseCase` →
   `GET /api/swimmers/me` → `{ swimmerId }`.
4. `vm.load(swimmerId)` runs the existing sequence: `GET /api/swimmers/{id}` + all tab
   GETs. Each guarded endpoint calls `CanReadAsync(User, id)`; since `id` is the
   swimmer's own, all pass.
5. Page renders all 9 tabs read-only (`canEdit() === false`).
6. If the same swimmer somehow requests a foreign id (devtools/URL), every guarded
   endpoint returns 403 and the page surfaces its error/empty state.

## Security Considerations

- Decision (1) closes an existing IDOR: before this change, any authenticated user
  (including a swimmer) could read any swimmer by id. After, swimmers are confined to
  their own record server-side; coaches are unchanged.
- Enforcement lives on the **server** (the guard), not only in the UI, so it holds
  regardless of client behavior.
- The guard is fail-closed for swimmers: if their own id cannot be resolved, access is
  denied.

## Testing Strategy (TDD)

**Backend (xUnit):**
- `GET /api/swimmers/me`: returns the caller's swimmer id; 404 when the user is not a
  swimmer.
- `SwimmerSelfAccessGuard.CanReadAsync`: coach → true for any id; swimmer → true for own
  id, false for a foreign id; swimmer with no profile → false.
- Representative controller tests: a guarded read returns 403 for a swimmer requesting a
  foreign id, 200 for their own, 200 for a coach requesting any id (cover at least one
  route-id and one query-id endpoint).

**Frontend (Jest):**
- `layout.component`: `navGroups` for `swimmer` = [My Profile, Settings]; for
  `head_coach`/`captain` = full coach menu (unchanged).
- `GetMySwimmerIdUseCase` maps `GET /api/swimmers/me` → id; fails on malformed response.
- `SwimmerProfilePage`/viewmodel: with no route id, resolves via `/me` then loads;
  `canEdit()` is false for a swimmer.
- Landing: `login.viewmodel` navigates a swimmer to `/my-profile`; onboarding `submit()`
  navigates to `/my-profile`; the home guard redirects a swimmer to `/my-profile`;
  `roleGuard` blocks a swimmer from a coach route.

## Files Touched (indicative)

**Backend:**
- `Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs` — new `GET me`; guard on profile/exams/guardians/body-measurement reads.
- `Kheprx.BaseBackend.Api/Controllers/{InBodyReadings,FeedbackEntries,Observations,HealthReadings,AttendanceRecords,Championships}Controller.cs` — guard on the swimmer-scoped read(s).
- `Kheprx.BaseBackend.Api/Security/ISwimmerSelfAccessGuard.cs` + `SwimmerSelfAccessGuard.cs` (new) + DI registration.
- `Kheprx.BaseBackend.Identity.Application/DTOs/SwimmerDtos.cs` — `MySwimmerRefDto`.
- Backend test projects — guard + endpoint tests.

**Frontend:**
- `app.routes.ts` — `/my-profile` route; `roleGuard` on coach routes; home guard.
- `features/auth/presentation/auth.guard.ts` (or a new small guard) — swimmer `/home`→`/my-profile` redirect.
- `features/auth/presentation/pages/login/login.viewmodel.ts` — `LANDING_ROUTE_BY_ROLE.swimmer`.
- `features/swimmer-onboarding/.../onboarding.viewmodel.ts` — post-submit navigation → `/my-profile`.
- `layout/layout.component.ts` — role-gated `allGroups` + My Profile item.
- `features/swimmer-profile/.../swimmer-profile.page.ts` — resolve id via `/me` when no route param.
- New data slice: `MySwimmerRef` DTO + repository method + `GetMySwimmerIdUseCase` (+ specs).
- `core/i18n/en.json`, `ar.json` — `shell.nav.myProfile`.

## Out of Scope / Future

- A swimmer-specific dashboard (they land on My Profile instead).
- Any editing by swimmers (explicitly read-only).
- `swimmer_id` JWT claim optimization for the guard.
- Hardening the `GET /api/swimmers` roster list beyond route-level role-gating.
