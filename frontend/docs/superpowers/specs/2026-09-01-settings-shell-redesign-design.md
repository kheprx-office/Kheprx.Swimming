# Settings & Shell Redesign — Design

**Date:** 2026-09-01
**Status:** Approved for planning
**Scope:** Restyle the Angular app's shared shell (sidebar + header) and the Settings (`/account`) page to match a provided reference design, and extract a reusable `ChangePasswordForm` shared by the Settings → Security section and the forced-first-login `/change-password` page.

## Reference assets (external, read-only)

- **Instruction spec:** `C:\Users\envnt\Desktop\MCP\execute.txt`
- **Target screenshots:** `C:\Users\envnt\Desktop\MCP\ui 1.png`, `ui 2.png`, `ui 3.png` (Settings page: sidebar, header, Profile, Preferences, Security, About). `our code 1.png` shows the current state.
- **Reference implementation (React/Vite/Tailwind):** `C:\Users\envnt\Desktop\4dba8937-ce3f-44bd-b093-515f10a5ea2b\src` — notably `components/Sidebar.tsx`, `components/Topbar.tsx`, `components/LanguageSwitcher.tsx`, `pages/SettingsPage.tsx`. Use for structure/spacing/styling intent; **do not** copy React code — reimplement in the existing Angular/Tailwind idioms.

The implementer should compare against the screenshots while working (per `execute.txt`).

## Goal

The current shell has a dark (`bg-ink`) RTL sidebar with disabled "coming soon" items and a plain Sign-out row; the header has a disabled search, an `AR/EN` text toggle, and a circular avatar. The Settings page already has Profile/Preferences/Security/About sections but in the current style, and its Security section duplicates the password-change logic that also lives on `/change-password`. The redesign matches the reference: a **teal** sidebar with grouped nav + Active-club card + bottom user card, a refined header (breadcrumb, styled search, `EN | عربي`, theme toggle), a cream dotted page background with serif card headings, and a single reusable password form.

## Decisions (agreed)

1. **Real data, our brand.** Adopt the reference's teal layout/styling, but keep the app's existing brand text and wire the sidebar user-card + Profile section to the **real logged-in session data** (`AuthSessionStore` / `AccountViewModel`). "Active club" has no backend yet → static placeholder.
2. **Keep the extra Profile fields** (phone / age / gender / national ID) below name/email/role — they are live data.
3. **Search input is styled but inert** — no search feature exists; render the reference placeholder, do not fake results.
4. **Dark mode kept coherent** — light theme gets the teal-sidebar/cream look; dark theme keeps a dark-teal variant. `ThemeStore` light/dark toggle is unchanged.
5. **Decompose** the Settings page into standalone section components (per `execute.txt` §10).
6. **Extract a reusable `ChangePasswordForm`**; `/change-password` (forced first-login) keeps its route/guard and reuses the form.

## Non-goals

- No search backend, no results, no autocomplete.
- No "club" backend/entity — Active club is a placeholder string.
- No new routes or pages (only decomposition of the existing `/account` page).
- No change to auth/session logic, guards, or the `changePassword` API contract.

## Components & files

### 1. Theme tokens
- **`src/styles.scss`**: add `--c-sidebar` (deep-ocean teal) and `--c-sidebar-ink` (light text on teal) to both `:root` (light) and `.dark`. Suggested light `--c-sidebar` ≈ `#0B4A58` (matches the existing `--c-primary-hover` deep teal); dark ≈ a darker teal (e.g. `#0A3A44`). Final values chosen by comparing the screenshots.
- **`tailwind.config.js`**: map `sidebar: 'rgb(var(--c-sidebar) / <alpha-value>)'` and `'sidebar-ink': 'rgb(var(--c-sidebar-ink) / <alpha-value>)'` alongside the existing color tokens.
- Reuse existing: `canvas` (warm sand cream), `surface` (cards), `primary`/`primary-hover` (teal accents/buttons), `font-heading` (Fraunces serif), the `.grain` dotted overlay, and `shadow-warm`/`shadow-warm-lg`.

### 2. Sidebar (`src/app/layout/layout.component.ts` + `.html`)
- Background `bg-sidebar text-sidebar-ink` (replaces `bg-ink text-surface`).
- **Brand block:** waves icon + `shell.brand` + new subtitle `shell.brandSubtitle` ("International Swimming" / "السباحة الدولية").
- **Active-club card:** inset rounded card — label `shell.activeClub.label` + value `shell.activeClub.value` (static placeholder). Exposed on the component as an `activeClub` constant/signal (not hardcoded in the template) so it can later bind to session/club data.
- **Grouped nav:** restructure `allItems` into three groups with translated headers:
  - `shell.nav.groups.overview` → Dashboard
  - `shell.nav.groups.coaching` → Swimmers, Attendance, Championships
  - `shell.nav.groups.administration` → Captain Panel (`roles: ['captain']`), Settings (`route: '/account'`)
  Preserve the existing role-filtering and "coming soon" disabled state for route-less items. Selected item (active route) = white bg, rounded, teal (`text-primary`) label/icon.
- **Bottom user card:** initials avatar (`userInitial`) + `currentUserName` + role label (`roleLabel`) + a logout icon button (`signOut()`) on the right. Replaces the standalone Sign-out row. Keep the desktop collapse toggle (may move into the footer area).

### 3. Header (`layout.component.html`)
- **Left:** breadcrumb `<Active club> › <group>` + current page label, derived from the active nav item (add a small helper mapping the active route to its group header + label). 
- **Search:** styled input with placeholder `shell.searchPlaceholder` (updated to "Search swimmers, events…" / Arabic). Rendered but **inert** (no binding/handler). Remove the "(coming soon)" text.
- **Right:** segmented `EN | عربي` control (reuse the login page's switcher markup/classes) calling `LanguageStore`; theme toggle (existing). **Remove** the circular avatar button (user now lives in the sidebar card).

### 4. Settings page decomposition (`src/app/features/auth/presentation/pages/account/`)
Split `account.page.html` into standalone child components under `.../account/sections/` (naming may follow project conventions), composed by `AccountPage`:
- `profile-section` — avatar + name/email + **Role pill** (outlined, icon), with the extra fields (phone/age/gender/national ID) below. Inputs fed from `AccountViewModel`/`AuthSessionStore`.
- `preferences-section` — Language row (segmented `EN | عربي`) + Theme row (toggle), driven by `LanguageStore`/`ThemeStore`.
- `security-section` — wraps the reusable `ChangePasswordForm` (small-Save variant) + inline success message.
- `about-section` — app name (`shell.brand`), version, description (existing `profile.about.*` keys).
- Page styling: serif (`font-heading`) card headings, `bg-surface` cards with `border-border` + `shadow-warm` + large radius, cream dotted background (inherited from the shell's `.grain` main), a centered content column (~`max-w-3xl`/`4xl`), Security inputs ~50–55% card width with a small left-aligned teal `Save`.

### 5. Reusable `ChangePasswordForm` (`src/app/features/auth/presentation/components/change-password-form/`)
- Standalone component owning the three password fields, the confirm-match validation (currently in `ChangePasswordViewModel`), the call to `AuthSessionStore.changePassword(current, new)`, and its own `loading`/`error`/`success` state. Preserves i18n messages (`changePassword.mismatch`, `profile.security.success`, server messages via `toUserMessage`).
- **Inputs:** `variant: 'full' | 'compact'` (full-width primary button for the forced page vs small teal `Save` for Settings). **Output:** `(succeeded)` for the parent to react.
- **`/change-password` page** (`change-password.page.*`): replace the inline form markup with `<change-password-form variant="full" (succeeded)="onDone()">`; keep navigating to `/account` on success. The forced-first-login route + guard are untouched. `ChangePasswordViewModel` is reduced to (or replaced by) the form component — no behavior change.
- **`security-section`**: use `<change-password-form variant="compact">` + show the inline success line. Remove the duplicated password fields/logic from `AccountViewModel` (`currentPassword`/`newPassword`/`confirmPassword`/`changePassword`/`pwError`/`pwSuccess`).

### 6. i18n (`src/app/core/i18n/en.json` + `ar.json`)
Add EN + AR keys: `shell.brandSubtitle`, `shell.activeClub.label`, `shell.activeClub.value`, `shell.nav.groups.overview|coaching|administration`, and update `shell.searchPlaceholder`. Reuse existing `roles.*`, `profile.*`, and `changePassword.*` keys.

## Data flow

```
AuthSessionStore (session) ─┬─> sidebar user-card (initials, name, role)
                            └─> AccountViewModel.user() ─> profile-section (name/email/role/phone/age/gender/nationalId)
LanguageStore / ThemeStore ─> header controls + preferences-section
ChangePasswordForm ─> AuthSessionStore.changePassword() ─> (succeeded) ─> parent (navigate | inline success)
```

## Error handling

- `ChangePasswordForm`: confirm-mismatch → inline error (no API call); API failure → `toUserMessage(error)` inline; success → success state + `(succeeded)` emit. Identical behavior to today, just relocated.
- Inert search and placeholder Active-club value never error.

## Testing

- **Keep green:** existing `login`, `account`, `change-password`, and store specs (adjust any that referenced the moved `AccountViewModel`/`ChangePasswordViewModel` password members).
- **New:** `ChangePasswordForm` spec — confirm-mismatch sets the error and does NOT call `changePassword`; matching passwords call `changePassword` and emit `succeeded` on success; server error surfaces via `error`.
- **Build:** `npx ng build --configuration development` type-checks the template-heavy shell/sections; full `npx jest` passes.

## Files

**New:** `sections/profile-section`, `sections/preferences-section`, `sections/security-section`, `sections/about-section` (`.ts` + `.html` each), `components/change-password-form/change-password-form.*`, and a `ChangePasswordForm` spec under the auth `testing/` mirror.
**Modified:** `src/styles.scss`, `tailwind.config.js`, `layout.component.ts` + `.html`, `account.page.ts` + `.html`, `account.viewmodel.ts` (drop password members), `change-password.page.ts` + `.html`, `change-password.viewmodel.ts` (thin/removed), `en.json`, `ar.json`; update affected specs.

## Risks

- **Shared shell blast radius** — sidebar/header changes affect every authenticated page; verify Dashboard and other routes still render (visual check + build).
- **Forced first-login** — must not break: `/change-password` route/guard preserved; the form's forced-variant still navigates on success.
- **RTL/LTR** — the shell is a `dir`-aware flex row: the sidebar renders on the **left in LTR (English)** and the right in RTL (Arabic), because it's the first flex child. The reference screenshots are LTR (sidebar left). Keep `dir`-aware/logical spacing so both directions stay correct; don't hardcode physical left/right that breaks Arabic.
- **Token change** — introducing `--c-sidebar` must be defined in both `:root` and `.dark` or dark mode breaks.
