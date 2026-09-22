# Swimmer Profile — Feedback tab — Design

- **Date:** 2026-09-22
- **Status:** Approved (design decisions); implementation pending
- **Branch:** on local `main`; **no commits until the user says so**
- **Sibling / prior art:** `2026-09-19-swimmer-profile-inbody-design.md` (full-CRUD nested resource), `2026-09-20-swimmer-profile-records-tab-design.md` (client-side category resolution), `2026-09-02-reference-lookups-design.md` (reference lookup pattern)

## Context & goal

The Swimmer Profile page (`swimmers/:id`, titled "My Profile" in the mock) ships six tabs — **Identity & Vitals**, **Guardian**, **Physiological**, **InBody**, **Records**, **Health Monitoring**. This feature builds the **seventh tab, Feedback** — a **full-CRUD** view over a swimmer's coach performance evaluations (`health.feedback_entry`): coaches add rated, categorized, commented feedback; everyone with profile access reads the history, filterable by date range.

The `feedback` tab already exists as a **placeholder** in the Angular tab strip (`swimmer-profile.page.ts:35`) but is not in `enabledTabs`. This feature enables and wires it. It is the existing `swimmers/:id` page — **not** a new swimmer-facing "My Profile" route ("My Profile" is only the mock's header label).

Reference: design mock `Desktop/mcp/3.png` and the React prototype `Desktop/4dba8937-…/src/pages/SwimmerProfile.tsx` (feedback block ~L1514–1672, `FeedbackCard` ~L1934). The prototype also renders a coaches-only **"Performance Insights"** sparkline panel (~L1676+) — **that panel is decoration with no backing data and is out of scope** (same call the Records spec made about prototype trend charts).

## Key architectural decisions

### 1. Two new tables across two modules (neither exists yet)

Unlike Records/Health-Monitoring (which reused existing tables), this feature creates **both** backing tables, each in its owning module — mirroring how `reference.observation_category` (Identity) + `health.observation` (Health) were split:

- **`reference.feedback_category`** → **Identity** module, `reference` schema. Mirrors `ObservationCategory` exactly (entity, EF configuration, repository, migration, seed). Exposed via `ReferenceController` `GET /api/reference/feedback-categories` → `IReferenceService.GetFeedbackCategoriesAsync` → `CodedLookupDto`.
- **`health.feedback_entry`** → **Health** module, `health` schema. Mirrors the InBody slice (entity, configuration, repository, service, nested controller, DTOs, validators).

Two migrations on two `DbContext`s: an Identity migration for `feedback_category` and a Health migration for `feedback_entry`. (Consistent with prior rounds where Identity + Health each got their own migration.)

### 2. Nested routes on the Health module — matching InBody

Feedback is a brand-new full-CRUD resource with no prior create endpoint, so it follows the **InBody precedent** (`/api/swimmers/{id}/inbody-readings`) rather than the flat `ObservationsController` convention:

- `GET /api/swimmers/{id}/feedback-entries` — list, newest-first. `[Authorize]` (any authenticated).
- `POST /api/swimmers/{id}/feedback-entries` — create. `head_coach,captain`.
- `PUT /api/swimmers/{id}/feedback-entries/{entryId}` — edit. `head_coach,captain`.
- `DELETE /api/swimmers/{id}/feedback-entries/{entryId}` — delete. `head_coach,captain`.

`author_id = CurrentUserId()` at create (from JWT `sub`). Ownership guard: PUT/DELETE that reference an `entryId` whose `swimmer_id` ≠ route `id` return **404** (mirrors InBody). **Any** head_coach/captain may edit/delete any entry (no author-only restriction — same as the sibling tabs).

### 3. Category resolution stays client-side

`FeedbackEntry` lives in `HealthDbContext`; `FeedbackCategory` lives in Identity's `reference` schema (loose Guids, no cross-module FK). The Health read returns `category_id` only — it does **not** join across modules. The frontend fetches categories once via `GET /api/reference/feedback-categories` and maps `categoryId → name` for the category pill and the add/edit category chips (exactly how Records resolves observation categories).

### 4. Author name enriched at read, in the API layer

The mock shows each card's author ("Coach Omar"). `author_id` is an Identity `app_user`; the JWT carries no name claim and `GET /api/users` is admin-only — so the name must be resolved deliberately. **Decision: enrich at read in the API layer**, where both modules are already referenced:

- The Health `IFeedbackService.ListAsync` returns rows carrying `AuthorId` (and empty author-name fields).
- A new lean Identity read method resolves a batch of ids → display names, e.g. `IUserService.GetDisplayNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken) → IReadOnlyDictionary<Guid, (string NameEn, string? NameAr)>`.
- The `FeedbackEntriesController` (API project) injects `IFeedbackService` **and** `IUserService`, and populates `AuthorNameEn`/`AuthorNameAr` on the list results before returning.

This keeps the DB schema faithful to the diagram (`author_id` FK/loose-Guid, no denormalized name column), keeps the Health module free of Identity, and always shows the current name. The exact composition seam (thin controller vs. a small API-layer composer) is left to the implementation plan; the DTO shape and the new Identity method are fixed here.

Per the established Health-module pattern, the service does **not** cross-check swimmer existence against Identity.

## Scope

**In scope**
- Backend: `reference.feedback_category` lookup (Identity) — entity, config, repo, migration, seed, reference-service method, `GET /api/reference/feedback-categories`.
- Backend: `health.feedback_entry` full CRUD (Health) — entity, config, repo, `FeedbackService`, `FeedbackEntriesController` (nested GET/POST/PUT/DELETE), DTOs, request validator.
- Backend: author-name enrichment — new Identity batch name-lookup method, composed into the feedback list read in the API layer.
- Frontend: the Feedback tab in the `swimmer-profile` slice — coach add form (rating stars 1–5, category chips, comment), history list with **From/To** date filter, per-card **Edit** (inline form) and **Remove** (confirm step) for coaches. Category names + chips resolved client-side.
- i18n (en/ar) for feedback content strings.
- Tests mirroring sibling coverage (see Testing).

**Out of scope**
- The "Performance Insights" sparkline panel (prototype-only; no backing data).
- A separate swimmer-facing "My Profile" route/page (this is the existing `swimmers/:id` page).
- Author-only edit/delete restriction (any head_coach/captain may edit/delete).
- The remaining two tabs (attendance, championships).
- Cross-module swimmer-existence validation (follows the Health-module norm).

## Locked decisions

1. **Full CRUD** — coach add + edit + delete; everyone with profile access reads.
2. **Nested routes on Health**, matching InBody (see decision 2). Auth: GET any-auth; POST/PUT/DELETE `head_coach,captain`.
3. **`author_id = CurrentUserId()`** at create; **immutable** on edit. Ownership-guard 404 on PUT/DELETE.
4. **`entry_date` is server-set** to today (`DateOnly`, `DateTime.UtcNow.Date`) at create, **immutable** — the add form has no date field. (Flag: change to a user-provided date only if a date picker is wanted.)
5. **Editable fields** = `rating`, `category_id`, `comment`. `author_id`, `entry_date`, `swimmer_id` immutable.
6. **Validation** — `rating` integer 1–5; `comment` required (non-empty, trimmed; max 1000 chars); `category_id` required and must be a seeded category; `entry_date` not needed in the request (server-set).
7. **Ordering** — newest-first (`entry_date` desc, tiebreak by `id`).
8. **Category names resolved client-side** via `GET /api/reference/feedback-categories`; seeded categories: `technique`, `endurance`, `attitude`, `punctuality`, `other`.
9. **Author name enriched at read** in the API layer (decision 4).
10. **No commits** until the user says so.

## Data model & persistence

Two new tables. Loose Guids (no physical cross-module FKs), matching the codebase norm.

### `reference.feedback_category` (new — Identity module, `reference` schema)
Mirrors `reference.observation_category`.

| column | type | notes |
|---|---|---|
| `id` | uuid | PK |
| `code` | varchar(50) | **unique**; `technique`, `endurance`, `attitude`, `punctuality`, `other` |
| `name_en` | varchar(100) | NOT NULL |
| `name_ar` | varchar(100) | nullable |

- Migration on `IdentityDbContext` creating the table + unique index on `code` (copy `CreateObservationCategoryTable`).
- Seeded in `IdentitySeeder` (5 rows) alongside the other reference lookups.

### `health.feedback_entry` (new — Health module, `health` schema)
Mirrors `health.inbody_reading` structurally.

| column | type | notes |
|---|---|---|
| `id` | uuid | PK |
| `swimmer_id` | uuid | NOT NULL; loose Guid (no FK) |
| `rating` | smallint | NOT NULL; 1–5 (validated in Application) |
| `category_id` | uuid | NOT NULL; loose Guid → `reference.feedback_category.id` |
| `comment` | varchar(1000) | NOT NULL |
| `author_id` | uuid | NOT NULL; `CurrentUserId()` at create; **immutable** |
| `entry_date` | date | NOT NULL; server-set at create (`UtcNow.Date`); **immutable** |

- Migration on `HealthDbContext` creating the table. Index on `swimmer_id` (mirror how `InBodyReadingConfiguration` indexes today) to support the swimmer-scoped list.
- Read query: filter by `swimmer_id`, order `entry_date` desc, tiebreak `id`.

## Backend design

### Identity module — `feedback_category` lookup
- `FeedbackCategory` domain entity (`id`, `code`, `name_en`, `name_ar`) — copy `ObservationCategory`.
- `FeedbackCategoryConfiguration`, `IFeedbackCategoryRepository` + `FeedbackCategoryRepository` — copy the observation-category equivalents.
- `IReferenceService.GetFeedbackCategoriesAsync` + implementation returning `IReadOnlyList<CodedLookupDto>`; DI registration in `IdentityModuleExtensions`.
- `ReferenceController` `GET /api/reference/feedback-categories` (copy the `observation-categories` action) + `ReferenceMessages.Success.FeedbackCategoriesListed`.
- Seed 5 rows in `IdentitySeeder`.

### Identity module — author-name resolution
- New method on `IUserService` (+ `UserService` impl): `GetDisplayNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken)` → dictionary `id → (NameEn, NameAr?)`, backed by a single `app_user` query (`WHERE id = ANY(ids)`), returning only ids that exist. No new endpoint (used internally by the API-layer composition).

### Health module — `feedback_entry` slice (mirror InBody)
- `FeedbackEntry` domain entity with a factory/guarded constructor enforcing invariants (rating 1–5, non-empty comment, non-empty author/category/swimmer ids) and an `Update(rating, categoryId, comment)` method leaving `author_id`/`entry_date`/`swimmer_id` untouched.
- `FeedbackEntryConfiguration` (schema `health`, `swimmer_id` index), `IFeedbackEntryRepository` + `FeedbackEntryRepository`.
- DTOs:
  - `FeedbackEntryDto(Id, SwimmerId, Rating, CategoryId, Comment, AuthorId, AuthorNameEn, AuthorNameAr, EntryDate)` — `AuthorName*` populated by the API layer, empty from the service.
  - `CreateFeedbackEntryRequest(Rating, CategoryId, Comment)` — reused for POST and PUT (matches how InBody reuses its create request for update).
- `IFeedbackService` + `FeedbackService`:
  - `ListAsync(swimmerId, ct)` → newest-first `FeedbackEntryDto` (author names empty).
  - `CreateAsync(swimmerId, request, authorId, ct)` → `FeedbackEntryDto`.
  - `UpdateAsync(swimmerId, entryId, request, ct)` → `FeedbackEntryDto?` (null when not found / not owned by swimmer).
  - `DeleteAsync(swimmerId, entryId, ct)` → bool.
- `FeedbackMessages` (Success: Listed/Created/Updated/Deleted; Errors: NotFound) with en/ar, per module convention.
- `CreateFeedbackEntryRequestValidator` (FluentValidation): `Rating` in 1..5; `Comment` not empty, ≤1000; `CategoryId` not empty. (Category-exists is enforced by referential intent + seed; follow how sibling validators scope this.)
- DI registration in the Health module extensions.

### API — `FeedbackEntriesController`
- Route `api/swimmers/{id:guid}/feedback-entries`, extends `BaseApiController`; injects `IFeedbackService` + `IUserService`.
- GET: list via `IFeedbackService.ListAsync`, collect `AuthorId`s, resolve via `IUserService.GetDisplayNamesAsync`, project names onto each DTO, wrap in `ApiResponse`.
- POST: `CreateAsync(id, request, CurrentUserId(), ct)` → 201; enrich the single returned DTO's author name (the current user).
- PUT/DELETE: `{entryId:guid}`, `head_coach,captain`, 404 on null/false via `FeedbackMessages.Errors.NotFound`.

## Frontend design (`swimmer-profile` slice)

Mirror the InBody + Records file set.

- **DTO / mapper / model:** `data/dto/feedback-entry.dto.ts` (`FeedbackEntryDto`), `data/dto/feedback-entry.mapper.ts`, `domain/model/feedback-entry.ts` (`FeedbackEntry`: id, swimmerId, rating, categoryId, comment, authorId, authorNameEn, authorNameAr, entryDate).
- **Use-cases:** `list-feedback-entries`, `create-feedback-entry`, `update-feedback-entry`, `delete-feedback-entry` (+ a `load-feedback-categories` use-case mirroring `load-observation-categories`, or reuse the existing reference-loading pattern).
- **Repository:** add `listFeedbackEntries` / `createFeedbackEntry` / `updateFeedbackEntry` / `deleteFeedbackEntry` (+ `listFeedbackCategories`) to `swimmer-profile.repository` (interface) and `swimmer-profile.repository.impl` (HTTP), targeting the routes above.
- **ViewModel:** feedback signals — list, categories, add-form state (rating, categoryId, comment), From/To filter, edit state, loading/empty. Load on tab activation; `visibleFeedback` computed from the From/To filter (client-side, like the mock). CRUD actions call the use-cases and refresh.
- **Page/template:** add `'feedback'` to `enabledTabs`; render the feedback section from the mock —
  - Coach add form (gated to coaches): rating stars (1–5), category chips, comment textarea, Save (disabled until rating + comment present).
  - Feedback History header with count + From/To date inputs + Clear.
  - Card per entry: rating stars, category pill (name via categories lookup), `entry_date` (top-right), comment, author name; coach Edit (inline form reusing the add fields) + Remove (confirm step).
  - Empty states: "No feedback yet" / "No feedback in this date range".
- **i18n:** `swimmerProfile.feedback.*` keys in `en.json` + `ar.json` (`swimmerProfile.tabs.feedback` already referenced by the tab strip — add its label if missing). Category display names come from the seeded lookup, not i18n.

## Testing

Mirror the coverage of the sibling tabs (backend xUnit, frontend Vitest/Jasmine per the repo's setup):

- **Identity:** `FeedbackCategory` entity test; feedback-category repository test; `ReferenceService.GetFeedbackCategoriesAsync` test; `ReferenceController` feedback-categories action test; `IUserService.GetDisplayNamesAsync` test (subset resolution, missing ids).
- **Health:** `FeedbackEntry` entity tests (invariants, `Update` immutability of author/date/swimmer); `FeedbackEntryRepository` tests (list newest-first, ownership scoping); `FeedbackService` tests (list/create/update/delete, not-found paths); `CreateFeedbackEntryRequestValidator` tests (rating bounds, comment required/length, category required).
- **API:** `FeedbackEntriesController` tests — list enriches author names, create sets author = current user + 201, update/delete 404 on foreign/absent entry, role gating.
- **Frontend:** mapper spec; the four use-case specs; repository-impl spec (routes/payloads); viewmodel spec (load, filter, add/edit/delete, empty states).

## Open questions / assumptions to confirm at spec review

- `entry_date` server-set to today (no date picker). Change only if a user-chosen date is wanted.
- `comment` max length 1000 (arbitrary but generous). Adjust if there's a house standard.
- Enrichment seam (thin controller vs. small API-layer composer) deferred to the plan; DTO + Identity method are fixed.
