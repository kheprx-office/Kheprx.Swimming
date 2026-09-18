# Swimming — Login + Coach/Captain Profile Design

**Date:** 2026-08-31
**Status:** Draft (design), pending user review
**Target directory:** `C:\Users\envnt\Desktop\Kheprx.Swmming`
**Design references:** `C:\Users\envnt\Desktop\MCP\1.png` (login), `3.png`/`4.png` (settings/profile),
`5.png` (Aiven connection), `6.png` (pgAdmin — `Swimming_Production`), `7.png` / `docs/references/swimming-database-diagram.*` (DB design).
Ignored mockup: `2.png` (club picker).

## 1. Goal

Deliver the first real feature slice on the swimming scaffold: a **restyled login screen**
and a **role-aware profile/settings screen** for the **head-coach** and **captain (coach)**
roles, wired to the real **`Swimming_Production`** PostgreSQL database on Aiven.

This reshapes the scaffold's still-Electric-shaped `Identity` module to the swimming schema,
creates that schema in the (currently empty) database via one fresh EF migration, seeds demo
users, and introduces the frontend infrastructure the mockups require (bilingual EN/AR with a
live toggle + LTR↔RTL flip, and a light/dark theme).

## 2. Locked decisions

| Question | Decision |
|----------|----------|
| DB state | **Empty — create it.** Write EF migrations to build the swimming identity schema in `Swimming_Production`; seed demo users. |
| "coach" role mapping | **`coach` = `captain_profile`.** Two roles: `head_coach` and `captain`. **Head coach has broader access than captain.** |
| Screen scope | **Login + profile only**, inside a **minimal shell** (other nav items shown disabled). |
| Login mechanism | **Real email + password.** Honor `is_first_login` → forced change-password. No demo role picker. |
| Language | **Full working EN/AR toggle now**, with live LTR↔RTL flip. |
| i18n mechanism | Lightweight **signal-based translation service** in `core/` (no new dependency). |
| Backend reshape | **In-place** reshape of `Identity` + delete orphaned Electric entities + **one fresh migration**. |
| Profile composition | **One Settings page** (Profile + Preferences + inline Security + About); keep standalone change-password for the forced flow. |
| Theme | CSS-variable theming recolored to Oasis teal/cream; `ThemeStore` toggles `light`/`dark`. |
| Secrets | Aiven connection string + JWT key via `dotnet user-secrets` / env vars — never committed. |
| `user-management` collateral | **Adapt-to-compile** (minimal), no UI/feature change. |

## 3. Scope

**In scope**
- Login screen restyled to `1.png` (English + Arabic, real email + password).
- Profile/Settings screen (`3.png`/`4.png`) for head-coach and captain — Profile, Preferences
  (language + theme), inline Security (change password), About.
- Minimal app shell (sidebar + top bar); non-implemented nav items shown disabled.
- Backend `Identity` reshape to the swimming schema; fresh migration in `Swimming_Production`;
  seed one head-coach + one captain user (+ profiles).
- Bilingual EN/AR with live toggle + direction flip; light/dark theme.

**Non-goals**
- Club-picker screen (`2.png`).
- Dashboard / Swimmers / Attendance / Championships pages and their tables (swimmer, guardian,
  health, championship, attendance, reference lookups beyond role/gender).
- `user-management` UI redesign (kept compiling only).

## 4. Backend — Identity reshape, DB, endpoints

### 4.1 Entities (`Identity.Domain/Entities`)

- **`AppUser`** (replaces `User`) → table `identity.app_user`
  - `Id` (uuid PK), `Username` (varchar, NN, unique), `Email` (varchar, unique, nullable),
    `PasswordHash` (text, nullable — "null when no login"), `NameEn` (varchar, NN), `NameAr`
    (varchar, nullable), `GenderId` (uuid FK→`reference.gender`, nullable), `Dob` (date, nullable),
    `Phone` (varchar, nullable), `RoleId` (uuid FK→`reference.role`, NN), `IsFirstLogin` (bool,
    default true), `CreatedAt` (timestamptz).
  - Methods: constructor; `SetPassword(hash)` clears `IsFirstLogin`. `Age` is **derived** from `Dob`.
  - **Removed from old `User`:** `IsActive`, `LastLoginAt`, `Code`, `Nid` (national_id moves to the
    profile subtypes). Login no longer checks `IsActive` and no longer records last-login (no such
    columns in the swimming schema).
- **`Role`** → `reference.role`: `Id`, `Code`, `NameEn`, `NameAr`. (Drops `SortOrder`/`IsActive`/`CreatedAt`.)
- **`Gender`** (new) → `reference.gender`: `Id`, `Code`, `NameEn`, `NameAr`.
- **`HeadCoachProfile`** (new) → `identity.head_coach_profile`: `Id`, `UserId` (FK + unique),
  `NationalId` (varchar(14), unique), `CreatedAt`.
- **`CaptainProfile`** (new) → `identity.captain_profile`: same shape as `HeadCoachProfile`.
- **`RefreshToken`** — kept unchanged (auth plumbing; stays in `identity`, intentionally not in the
  domain ER diagram).
- **Deleted** (Electric-only, currently orphaned — no controller references them): `Manager`,
  `Moqawel`, `Worker` and their configurations, repositories, read-models, and contracts DTOs
  (`ManagerNameDto`, `MoqawelPersonDto`, `PersonOptionDto`, `WorkerDetailPersonDto`, `WorkerPersonDto`).

### 4.2 Infrastructure

- **`IdentityDbContext`**: default schema `identity`; `Role` and `Gender` mapped to schema `reference`.
  DbSets: `AppUsers`, `Roles`, `Genders`, `HeadCoachProfiles`, `CaptainProfiles`, `RefreshTokens`.
  Remove the `Manager`/`Moqawel`/`Worker` DbSets.
- **Configurations**: `AppUserConfiguration`, `RoleConfiguration` (→`reference`), `GenderConfiguration`
  (→`reference`), `HeadCoachProfileConfiguration`, `CaptainProfileConfiguration`, keep
  `RefreshTokenConfiguration`. Delete the three Electric configs.
- **Migrations**: delete the five existing Electric migrations (DB is empty — nothing to preserve) and
  generate one fresh **`InitialSwimmingIdentity`** that creates schemas `identity` + `reference` and the
  six tables (`role`, `gender`, `app_user`, `head_coach_profile`, `captain_profile`, `refresh_token`).

### 4.3 Seeding

Idempotent **startup seeder** (not `HasData` — PBKDF2 salts are random, so hashes can't be baked into a
migration). On startup, if absent, insert:
- `reference.role`: `head_coach`, `captain` (with `name_en`/`name_ar`).
- `reference.gender`: `male`, `female`.
- `identity.app_user`: one **head-coach** user and one **captain** user, each with a matching profile
  row (national_id) and a hashed known dev password. At least one is seeded with `is_first_login = true`
  to exercise the forced-change flow.

Dev credentials (documented, not real secrets) are recorded in `docs/STARTER.md`:
- `headcoach@kheprx.local` / `Passw0rd!` (`is_first_login = true`)
- `captain@kheprx.local` / `Passw0rd!` (`is_first_login = false`)

(Additional roles — `admin`, `swimmer` — are deliberately out of scope and added by later features.)

### 4.4 Application / API

- `AuthService.LoginAsync`: find by email → require the user exists, has a `PasswordHash`, and the hash
  verifies; otherwise one identical `401`. Issue access JWT (role claim) + refresh token; return
  `IsFirstLogin`. (Drops the `IsActive` check and `RecordLogin`.)
- `GetCurrentUserAsync` → **profile DTO** enriched with `NameEn`, `NameAr`, `Email`, role code, `Phone`,
  gender label, `Dob`/age, and `NationalId` (joined from whichever profile subtype the user's role maps to).
- `ChangePasswordAsync`: unchanged flow; `SetPassword` clears `IsFirstLogin`; all sessions revoked then a
  fresh pair reissued to the caller.
- DTOs updated: profile DTO gains `NameEn`/`NameAr`/`NationalId`; `SessionDto` keeps its shape (its
  `MustChangePassword` field now maps to `is_first_login`).
- Endpoints unchanged in shape: `POST /api/auth/login`, `/refresh`, `/logout`, `/change-password`;
  `GET /api/auth/me`.

### 4.5 Config / secrets

- Real Aiven connection string + JWT signing key supplied via **`dotnet user-secrets`** or environment
  variables (`ConnectionStrings__Postgres`, `Jwt__SigningKey`) — **never committed**.
- Target database: **`Swimming_Production`** on `kheprx-service-kheprx.b.aivencloud.com:14647`,
  `SSL Mode=Require` (trust server certificate for dev, or the Aiven CA cert).
- `appsettings.json` keeps only a localhost placeholder connection string and the existing dev JWT key
  (dev-only). Real/prod values come from secrets.

### 4.6 `user-management` collateral (adapt-to-compile)

Reshaping `User`→`AppUser` breaks the out-of-scope `user-management` service, DTOs, validators, and tests
(they reference `FullName`/`Nid`). Adapt them **minimally** to the new `AppUser` fields
(`NameEn`/`NameAr`, `RoleId`, `GenderId`, `Dob`, `Phone`, `Email`, `Username`), **drop** national_id
handling (national_id now lives on the profile subtype, managed later), and keep tests green. **No UI or
feature change.** This applies to both the backend service/DTOs and the frontend `user-management`
DTOs/tests.

## 5. Frontend — i18n + theme infrastructure

### 5.1 i18n (`core/`)

- **`LanguageStore`** — a signal service holding `lang` (`'en' | 'ar'`), persisted to `localStorage`,
  that sets `<html lang>` and `<html dir>` (`ltr`/`rtl`) whenever it changes.
- **`TranslateService`** — resolves `t('key.path')` reactively from per-feature JSON dictionaries
  (`en.json`/`ar.json` for `auth`, `profile`, `shell`), with a missing-key fallback (returns the key).
  A `translate` pipe or `t()` helper is used in templates.
- All login/profile/shell strings move from hardcoded Arabic into the dictionaries; the toggle
  **re-renders live and flips direction** with no reload. Display of `name_en` vs `name_ar` follows the
  active language.

### 5.2 Theme (`core/`)

- **`ThemeStore`** — a signal (`'light' | 'dark'`), persisted to `localStorage`, toggling a class on `<html>`.
- Recolor the Tailwind design tokens (`tailwind.config.js` + CSS variables) from Electric navy/accent to
  the **Oasis teal/cream** palette, with light/dark variants. Existing token names (`bg-canvas`,
  `text-ink`, `bg-primary`, `bg-accent`, `border-border`, `glass-panel`, `gradient-text`) are reused so
  component markup changes stay minimal.

Both stores live in `core` (no `features` import) and are consumed by the shell + Settings Preferences controls.

## 6. Frontend — screens + routing

### 6.1 Login (`features/auth/.../login`)

Restyle to `1.png`: two-panel layout — left teal hero panel with the tagline
"Every swimmer, every session, every result — in one place." plus a stat; right sign-in card
("Welcome…", email, password, Sign In) with a small EN/AR toggle. **Removes** the Electric
demo-credentials box and the "Select Role (Demo)" dropdown. Strings come from the i18n dictionaries;
layout is direction-aware. On success: if `isFirstLogin`, route to `/change-password` (forced); else
`/home`. The existing login viewmodel already performs most of this and is adapted rather than rewritten.

Brand name defaults to **"International Swimming Academy"** (from the mockup's About panel), held as a
single configurable string; the mockup's "Oasis Academy" is demo data and is not used.

### 6.2 Settings/Profile (`/account`, labelled "Settings")

Merge the account page into one Settings page per `3.png`+`4.png`:
- **Profile card** (read-only): avatar initials, name (en/ar by language), email, role badge, plus
  phone / gender / dob (age) / national_id rows. The current account page already renders name/email/
  phone/gender/age/role; keep those and add name display + national_id.
- **Preferences**: Language (EN/AR) + Theme (light/dark), wired to `LanguageStore`/`ThemeStore`.
- **Security**: inline change-password (current / new / confirm + Save), reusing the change-password use-case.
- **About**: app name, version, description (static).

The same screen serves head-coach and captain; only the role badge differs. The standalone
`/change-password` page remains for the forced first-login flow.

### 6.3 Minimal shell (`layout.component`)

Rebrand from Electric to swimming/teal. The data-driven `navItems` keep **Settings enabled**; Dashboard,
Swimmers, Attendance, Championships, and Captain Panel are **shown but disabled** ("coming soon"), matching
the mockup sidebar. The role filter is wired (so head-coach vs captain can diverge later) but is cosmetic
now. The top-bar profile button routes to Settings. The shell flips LTR/RTL with the language.

### 6.4 Routing (`app.routes.ts`)

- `/login` — public.
- Authenticated shell children: `/home` (existing minimal landing), `/account` (Settings),
  `/change-password`, `/user-management` (kept, admin-guarded, unchanged).
- A guard enforces `is_first_login`: an authenticated user with `is_first_login = true` cannot leave
  `/change-password` until they change their password.
- Default `''` → `/home`.

## 7. Contracts regeneration

Regenerate `contracts/generated/frontend-api-client` from the trimmed API so it exposes the auth/identity
endpoints with the new profile DTO shape (`name_en`/`name_ar`/`national_id`/`gender`/`dob`). If the
generator cannot run in this environment, hand-update the affected frontend TS DTOs (`login`,
`current-user`, `session`, `change-password`) and document the manual step (per the scaffold's STARTER
fallback).

## 8. Testing (TDD during implementation)

- **Backend**: AuthService unit tests — login success/failure, `is_first_login` flag, change-password
  clears the flag, profile enrichment with national_id. Adapt `Identity.UnitTests` and the
  `user-management` unit tests to the new `AppUser` shape. Architecture tests (module boundaries) remain.
- **Frontend** (Jest, existing `testing/` mirror convention): `LanguageStore` (persistence + dir/lang),
  `TranslateService` (key resolution + missing-key fallback), `ThemeStore`; login viewmodel
  (route-by-first-login, error handling); Settings viewmodel (load profile, preference toggles, inline
  change-password).
- **Integration (manual)**: migration applies to `Swimming_Production`; seeder creates roles/genders and
  the two users; real end-to-end login; forced-change flow; language + theme toggles.

## 9. File-level change map (orientation for the plan)

**Backend**
- `Identity.Domain/Entities/`: add `AppUser` (replace `User`), `Gender`, `HeadCoachProfile`,
  `CaptainProfile`; edit `Role`; delete `Manager`, `Moqawel`, `Worker`.
- `Identity.Domain/Repositories`, `ReadModels`: delete Manager/Moqawel/Worker artifacts; adjust
  `IUserRepository`/`IUserProfileRepository` for the profile join.
- `Identity.Infrastructure/Data/IdentityDbContext.cs`, `Configurations/`, `Migrations/`: as §4.2.
- `Identity.Application/Services/AuthService.cs`, `DTOs/AuthDtos.cs`, `Services/UserService.cs`,
  `RoleService.cs`, `Validators/`: as §4.4 + §4.6.
- `Identity.Contracts/`: delete Electric person DTOs.
- `Kheprx.BaseBackend.Api`: startup seeder; `appsettings.json` connection placeholder; user-secrets docs.
- Tests: `Identity.UnitTests`, `Api.UnitTests`, `ArchitectureTests` adapted.

**Frontend**
- `core/`: add `LanguageStore`, `TranslateService` + dictionaries, `ThemeStore`.
- `tailwind.config.js` + global styles: teal/cream palette + light/dark variables.
- `features/auth/.../login`: restyle + i18n.
- `features/auth/.../account`: rebuild as Settings page (Profile + Preferences + inline Security + About).
- `layout/layout.component.*`: rebrand, nav config, direction, toggles.
- `app.routes.ts`: first-login guard, labels.
- `features/auth/data/dto/*`, `features/user-management/*`: DTO updates (adapt-to-compile).
- `testing/` mirrors for the above.

## 10. Verification (evidence before "done")

- `dotnet build backend/Kheprx.BaseBackend.sln` → succeeds, 0 errors.
- EF migration applies to `Swimming_Production`; seeder creates the two roles, two genders, and two users
  with profiles.
- `cd frontend && npm run build` → succeeds; `npm test` → green.
- End-to-end: log in as the seeded captain → land on `/home`; log in as the head-coach (`is_first_login`)
  → forced to `/change-password`, then reach Settings.
- Language toggle switches every login/Settings/shell string and flips LTR↔RTL; theme toggle switches
  light/dark; both persist across reload.

## 11. Assumptions & risks

**Assumptions**
- Target DB is `Swimming_Production`; Aiven credentials are provided via user-secrets at implementation time.
- Brand name is "International Swimming Academy" (configurable).
- `refresh_token` is added to the `identity` schema (auth plumbing, not domain data).
- Roles are limited to `head_coach` + `captain` for now.
- The `/account` route path is kept (labelled "Settings").

**Risks**
- The i18n retrofit touches every existing string (login/shell/account) — the largest single effort.
- Aiven SSL/connectivity and CA-cert handling.
- Recoloring Tailwind tokens can affect other scaffold pages (e.g., `home`) — spot-check after the palette change.
