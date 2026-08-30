# Swimming Identity — Generic User + Role Inheritance (design)

**Date:** 2026-08-30
**Status:** Approved (design), pending spec review
**Repo:** `C:\Users\envnt\Desktop\Kheprx.Swmming`
**Amends:** `docs/superpowers/specs/2026-08-30-swimming-database-design.md` (§5 Identity, §6 Swimmers, §10 enums, §11 relationships)
**Pattern reference:** Electric `identity.Users` + `Managers`/`Moqaweleen`/`Workers` (class-table / table-per-type inheritance)

## 1. Goal & scope

Restructure the swimming **identity model** into a generic base user + per-role subtypes ("inherit by
user type"), matching Electric's class-table inheritance. Today `app_user` is login-centric and the
`swimmer` table separately duplicates person attributes; this consolidates shared person attributes
onto a base `app_user` and makes `swimmer` and `captain` thin 1-1 subtypes.

**Scope: DB model only** — this updates the design spec + the `.mmd` diagram + the Database Explorer.
No backend/EF code. Approach chosen: **C (pragmatic table-per-type)** — subtype tables only for roles
that add fields (`swimmer`, `captain`); `head_coach` is a plain `app_user` row.

## 2. Decisions (locked)

| Axis | Decision |
|------|----------|
| Deliverable | Update spec + regenerate `.mmd` + Explorer; no backend code |
| Inheritance style | Class-table (table-per-type): base + 1-1 subtype tables via unique `user_id` |
| Subtypes | `swimmer`, `captain`. `head_coach` = `app_user` row with `role=headCoach` (no table) |
| `captain_type` | Kept (enum head \| assistant) |
| `blood_type` | Stays on `swimmer` (medical attr) |
| `password_hash` | Added on base `app_user` (nullable; login optional, Electric-style) |
| Placement | `captain` in the **identity** schema; `swimmer` stays in **swimmers** (module anchor) |

## 3. Base table — `app_user` (identity schema)

Absorbs the person attributes previously duplicated on `swimmer`.

| column | type | key | null | note |
|---|---|---|---|---|
| id | uuid | PK | no | |
| username | varchar | U | yes | login handle; null for login-less persons |
| email | varchar | U | yes | optional |
| password_hash | text | | yes | null when no login (auth also via Identity module) |
| name_en | varchar | | no | person name (shared by all roles) |
| name_ar | varchar | | yes | |
| national_id | varchar(14) | U | no | every person has one |
| gender | `gender` enum | | yes | male \| female |
| dob | date | | yes | |
| phone | varchar | | yes | |
| avatar_initials | varchar(4) | | yes | |
| role | `user_role` enum | | no | headCoach \| captain \| swimmer |
| is_first_login | boolean | | no | default true |
| active_club_id | uuid | FK → identity.club.id | yes | captain's current club |
| created_at | timestamptz | | no | |

Removed vs the base spec's `app_user`: `display_name` (→ `name_en`) and `swimmer_id` (FK inverts — see §4).

## 4. Subtype — `swimmer` (swimmers schema, thinned)

| column | type | key | null | note |
|---|---|---|---|---|
| id | uuid | PK | no | swimmer-related tables keep FK'ing this |
| user_id | uuid | FK,U → identity.app_user.id | no | **1-1 to base user; person attrs live on app_user** |
| uid | varchar | U | no | `SW-2026-…` |
| club_id | uuid | FK → identity.club.id | no | training club |
| championship_club_id | uuid | FK → identity.club.id | yes | |
| blood_type | varchar(3) | | yes | |
| created_at | timestamptz | | no | |
| updated_at | timestamptz | | no | |

**Moved up to `app_user`:** `name_en`, `name_ar`, `national_id`, `gender`, `dob`, `phone`, `avatar_initials`.
**Unchanged:** every FK that targets `swimmer.id` — `guardian`, `medical_exam`, `body_measurement`,
`swimmer_specialization`, `inbody_reading`, `health_reading`, `observation`, `feedback_entry`,
`attendance_record`, `championship_enrollment`, `race_assignment`, `race_result` all still reference
`swimmers.swimmer.id`.

## 5. Subtype — `captain` (identity schema, new)

| column | type | key | null | note |
|---|---|---|---|---|
| id | uuid | PK | no | |
| user_id | uuid | FK,U → identity.app_user.id | no | 1-1 to base user |
| captain_type | `captain_type` enum | | yes | head \| assistant |
| created_at | timestamptz | | no | |

## 6. Changed table — `captain_club` (identity schema)

Re-points from the base user to the `captain` subtype (mirrors Electric `Projects.ManagerId → Managers.Id`).

| column | type | key | null | note |
|---|---|---|---|---|
| id | uuid | PK | no | |
| captain_id | uuid | FK,U → identity.captain.id | no | **was `user_id` → app_user** |
| club_id | uuid | FK,U → identity.club.id | no | |
| — | | | | UK(captain_id, club_id) |

## 7. Enums (§10 delta)

- **Add** `captain_type` (head \| assistant).
- Unchanged: `user_role`, `gender` (now used on `app_user`), and all others.

## 8. Relationship changes (§11 delta)

- **Keep (FK inverted):** `app_user 1-1 swimmer` — now via `swimmer.user_id`.
- **Add:** `app_user 1-1 captain` — via `captain.user_id`.
- **Change:** `captain_club` parent — was `app_user 1-N captain_club`, now **`captain 1-N captain_club`** (via `captain_id`). `club 1-N captain_club` unchanged.
- **Unchanged:** `club 1-N app_user` (active club); `club 1-N swimmer` (trains / represents); all authorship
  edges (`recorded_by`/`created_by`/`author_id`/`coach_id` → `app_user.id`, shown as FK columns, lines omitted).

Net effect: **24 tables** (was 23; `captain` added) and **31 relationships** (was 30; `app_user→captain` added,
`app_user→captain_club` replaced by `captain→captain_club`).

## 9. Diagram / Explorer impact (implementation targets)

The implementation updates all three artifacts to match this spec:
- **Base spec** `2026-08-30-swimming-database-design.md`: amend §5 (`app_user`, `captain_club`), §6 (`swimmer`),
  add `captain` + `captain_type`, update §11 counts.
- **`docs/references/swimming-database-diagram.mmd`**: update `app_user`, `swimmer`, `captain_club`; add
  `captain`; update the relationship lines (`swimmer.user_id`, `app_user→captain`, `captain→captain_club`).
- **`docs/references/swimming-database-diagram.html`** (Explorer): update the `SCHEMA` data (`app_user` cols,
  thinned `swimmer` with `user_id`, new `captain`, re-pointed `captain_club`, `captain_type` enum note) and
  `RELATIONSHIPS`. The app JS stays verbatim; only the `<script id="appdata">` data changes.

## 10. Assumptions

- PK type `uuid`, matching the rest of the schema.
- `national_id` is NOT NULL on the base (every person has one, per Electric); revisit if login-only accounts
  without an NID are needed.
- Authorship/coach references stay on the base `app_user` (any user can author); if a coach must be a captain
  specifically, those edges could later re-point to `captain` — out of scope here.
- No data-migration concern: the database is design-only (no deployed swimming tables yet).
