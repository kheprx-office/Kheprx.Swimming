# Electric Website Frontend — Design System, Shell, Login, Home, User Management (design)

**Date:** 2026-07-01
**Feature:** Electric website frontend foundation (النور كهرباء)
**Reference design:** `docs/references/electric-website-angular` (RTL Arabic ERP)
**Builds on:** the completed Phase-1 auth wiring (`docs/superpowers/specs/2026-07-01-auth-frontend-phase1-wiring-design.md`) — login/refresh/logout/me/change-password, `AuthSessionStore`, `authGuard`/`roleGuard`, roles `admin|manager|moqawel|worker`.

## Goal

Replace the demo scaffold UI with the real **النور كهرباء** website, styled to match the reference
design (`electric-website-angular`), implemented in our Clean Architecture + MVVM, feature-first
structure. Deliver three live screens — **Login**, **Home** (hub), **إدارة المستخدمين** (user
management) — inside an RTL sidebar app shell, plus restyled change-password/account pages.

## Decisions (locked)

- **Users data source:** Mock seam, swap-ready (`MockUsersDataSource` default; `HttpUsersDataSource`
  built-not-wired against `/api/users`). No backend work — `/api/users` does not exist yet.
- **Layout shell:** the reference's full RTL collapsible dark sidebar + top header. Non-implemented
  sections render **inert** (no navigation); only Home, إدارة المستخدمين (admin), change-password,
  account, and sign-out are live.
- **Brand:** keep **النور كهرباء** ("نظام إدارة مقاولات الكهرباء").
- **Global RTL Arabic** (`index.html` `lang="ar" dir="rtl"`).
- **Auth transport:** stays **Mock-default, Http-swap-ready** (the Http path is verified against the
  live backend). Roles: `admin` is the top role (the reference's `owner` equivalent).
- **Design system:** replace the minimal CSS-variable Tailwind palette with the reference's palette
  directly in `tailwind.config.js` (1:1 fidelity). Verified safe — no surviving `core` component uses
  the old token color classes.

## Removals (delete entirely: folders, routes, barrels, providers)

- `features/home` (demo), `features/api/fetch-posts`, `features/offline-storage`,
  `features/design/profile-form`, `features/design/design-hub` (Animations).
- Their routes in `app.routes.ts`, exports, and the `API_DATA_SOURCE`/`MockApiDataSource` provider in
  `app.config.ts` (only the posts feature used it).
- The old `/admin` stub route + `AdminPage` (replaced by `/user-management`).
- Keep all `core/*` (auth, network, guards, notification, keyvalue, crypto, logging, domain, ui/theme).
  `core/datasource/api/*` (posts seam) is removed with the posts feature.

---

## 1. Design system

### `index.html`
`<html lang="ar" dir="rtl">`, `<title>النور كهرباء</title>`.

### `tailwind.config.js` — replace `theme.extend` colors/fonts/radii with the reference set
```js
colors: {
  ink: { DEFAULT: '#0F172A', hover: '#1E293B' },
  canvas: '#F8FAFC', surface: '#FFFFFF',
  border: { DEFAULT: '#E2E8F0' },
  primary: { DEFAULT: '#1E3A8A', hover: '#0EA5E9', light: '#0EA5E9' },
  sapphire: '#1E3A8A', sky: { glow: '#0EA5E9' },
  accent: { DEFAULT: '#F59E0B', light: '#FBBF24' }, gold: '#FBBF24',
  text: { primary: '#0F172A', secondary: '#475569', muted: '#94A3B8' },
  success: { DEFAULT: '#10B981', bg: '#ECFDF5' },
  warning: { DEFAULT: '#F59E0B', bg: '#FEF3C7' },
  danger:  { DEFAULT: '#E11D48', bg: '#FFF1F2' },
},
fontFamily: { sans: ['Cairo','Tajawal','sans-serif'], display: ['Cairo','sans-serif'], en: ['Inter','sans-serif'] },
borderRadius: { '2xl': '1rem', '3xl': '1.5rem' },
keyframes/animation: float, float-slow, rotate-slow (per reference),
```

### `styles.scss`
- Google Fonts import (Cairo/Tajawal/Inter).
- `body` → `bg-canvas text-text-primary font-sans`.
- Port the reference's global utility CSS verbatim: `tabular-data`, custom scrollbar, all keyframes/
  animation utilities (`fadeIn`, `slideUp`, `slideInRight/Left`, `scaleIn`, `pulseSoft`, `shimmer`,
  `float*`, `rotate-slow`, `blob*`, `shine`, `countPop`, `shake`), `stagger-*`, `gradient-text[-gold]`,
  `glass-panel[-strong]`, `gradient-border`, `bg-grid-dots`/`bg-grid-lines`, `card-hover`/`card-glow`,
  `modal-backdrop`/`modal-panel`, `skeleton`, reduced-motion block, and the global `button, a` transition.
- Keep `theme.scss` (vestigial CSS vars; harmless — `NotificationHostComponent` may still read them).

### `DecorBackgroundComponent` (port to `core/ui/components/decor-background.component.ts`)
Standalone, `variant = input<'page'|'auth'>('page')`, `className = input<string>('')`; template = the
reference's animated blobs/shapes for each variant. Reused by login, home, user-management.

### Toasts
Reuse our existing `NotificationService` + `NotificationHostComponent`. Restyle the host lightly to the
reference toast look (surface card, success/danger accents, slide-in). No new toast service.

### Icons
Use our `@lucide/angular` (`LucideAngularModule`, `<lucide-icon [img]="X" [size]="n" />`) — API matches
the reference's usage; icons imported from `@lucide/angular`.

---

## 2. App shell + routing

### `LayoutComponent` — `src/app/core/ui/layout/layout.component.ts` (+ `.html`)
Reference's RTL shell: collapsible dark sidebar (`bg-ink`) + top header + `<router-outlet />`. Standalone,
imports `RouterOutlet`, `RouterLink`, `LucideAngularModule`. Injects `AuthSessionStore`.

- **Sidebar nav** (`NavItem { id, label, icon, active: boolean, roles?: UserRole[] }`): render the
  reference nav set for fidelity. **Active** items: `الرئيسية`→`/home`, `إدارة المستخدمين`→
  `/user-management` (admin only), `الإعدادات`→`/account`. **Inert** items (no `routerLink`, disabled
  styling, `title="قريباً"`): `الملخص`, `المشاريع`, `المهام`, `المخازن`, `العمالة`, `الحسابات`,
  `التوريدات`, `توزيع الدفعات`. Active-route highlight via `router.url`.
- **Header:** mobile menu toggle, an **inert placeholder search** (visual only, `disabled`), profile
  avatar (initial from `principal`) linking to `/account`.
- **Footer:** sign out (`AuthSessionStore.signOut()` → `/login`) + collapse toggle.
- `isSidebarOpen`/`isMobileMenuOpen` signals; mobile overlay; role-filtered nav via
  `AuthSessionStore.role()`.

### Role labels — `src/app/core/auth/role-labels.ts`
`ROLE_LABELS: Record<UserRole,string> = { admin:'مدير النظام', manager:'مدير المشروع', moqawel:'مقاول', worker:'عامل' }`.

### Routes (`app.routes.ts`)
```
/login                        → LoginPage (no shell)
'' (LayoutComponent, canActivate: [authGuard]) children:
  /home                       → HomePage
  /user-management            → UserManagementPage   (canActivate: [roleGuard('admin')], route providers: users seam + use-cases + UsersViewModel)
  /change-password            → ChangePasswordPage    (providers: ChangePasswordViewModel)
  /account                    → AccountPage
  '' pathMatch full           → redirectTo 'home'
/** → redirectTo ''
```
`LoginViewModel` redirect after sign-in: `mustChangePassword ? '/change-password' : '/home'` (update the
existing `'/account'` target). Remove all demo routes and the `/admin` route.

---

## 3. Login / change-password / account (restyle)

- **`LoginPage`** template → reference's dark glass card + `<app-decor-background variant="auth" />`,
  brand logo (gold `Zap`), title "النور كهرباء" / "نظام إدارة مقاولات الكهرباء", email + password
  (`dir="ltr"` inputs), error alert with **shake** on failure, gold submit with `Loader2` spinner.
  Keeps the real wiring. `LoginViewModel` gains `readonly shakeKey = signal(0)`; on `signIn` failure,
  `shakeKey.update(k => k+1)` (drives `[class.animate-shake]`). Demo-creds hint shows the real seeded
  backend accounts (`{role}@kheprx.local` / `ChangeMe123!`).
- **`ChangePasswordPage`** → reference surface/glass card look inside the shell; same VM/logic.
- **`AccountPage`** → restyled profile/settings page: greeting + principal (name/role via `ROLE_LABELS`)
  + "تغيير كلمة المرور" link (`/change-password`) + sign out.

---

## 4. Home hub — `features/home` (new)

`HomePage` (standalone) = reference home: `<app-decor-background />` + gradient greeting from
`AuthSessionStore.principal()` + a responsive card grid. `HomeCard { label, desc, icon, color, route?, adminOnly? }`.
Only **إدارة المستخدمين** has a `route` (`/user-management`, `adminOnly`) and navigates; all other cards
(المشاريع، العمالة، المهام، المخزون، الحسابات، التوريدات، الإعدادات) render with the reference styling
but are **inert** (`disabled`, `cursor-not-allowed`, `title="قريباً"`, no click). Admin-only cards hidden
for non-admins.

---

## 5. إدارة المستخدمين — mock users seam (`features/user-management` + `core`)

### Domain
```ts
type UserStatus = 'active' | 'disabled';
interface ManagedUser { id: string; fullName: string; email: string; role: UserRole; status: UserStatus; mustChangePassword: boolean; }
interface CreateUserInput { fullName: string; email: string; role: UserRole; password: string; }
interface UpdateUserInput { fullName: string; email: string; status: UserStatus; password?: string; }
```

### Seam — `core/users` (mirrors the auth seam)
- Port `USERS_DATA_SOURCE` (InjectionToken): `list(): Promise<ManagedUser[]>`, `create(CreateUserInput): Promise<ManagedUser>`,
  `update(id, UpdateUserInput): Promise<ManagedUser>`, `setStatus(id, UserStatus): Promise<ManagedUser>`.
- **`MockUsersDataSource`** (default): in-memory list seeded with the 4 backend accounts
  (`{role}@kheprx.local`, `fullName="{role} (seed)"`, `status:'active'`, `mustChangePassword:true`);
  `create` assigns a generated id (internal incrementing counter, e.g. `USR-{n}` — deterministic and
  jsdom-safe, no `crypto.randomUUID`) + `mustChangePassword:true`, and rejects a duplicate email with
  `AppError('البريد مستخدم بالفعل','validation',409)` (`AppErrorKind` has no `conflict`; validation+409
  conveys it); `update`/`setStatus` mutate the entry.
- **`HttpUsersDataSource`** (built-not-wired): `GET/POST/PATCH /api/users` via `HttpClientService`,
  unwrapping `BaseResponseRs<T>` — same envelope pattern as auth. Not bound by default.
- `USERS_PROVIDERS` binds `USERS_DATA_SOURCE → MockUsersDataSource` + `USERS_REPOSITORY → UsersRepository`.

### Repository + use-cases (`core/users`)
`IUsersRepository`/`UsersRepository` pass-through; `ListUsersUseCase`, `CreateUserUseCase`,
`UpdateUserUseCase`, `SetUserStatusUseCase` (extend `UseCase<I,O>`; return `Result<...>`).

### Presentation (`features/user-management/presentation`)
- **`UsersViewModel`** (route-scoped): signals `users`, `loading`, `error`, `search`, `filterRole:''|UserRole`,
  `filterStatus:''|UserStatus`, modal state (`modalOpen`, `editingId`, `formFullName/Email/Role/Password/Status`);
  `filtered` computed (search on name/email + role + status); `load()` (ListUsers), `openAdd()/openEdit(u)`,
  `save()` (validates name+email; create vs update; success/error via `NotificationService`; reloads list),
  `toggleStatus(u)`.
- **`UserManagementPage`** = reference layout adapted to our fields: header ("ادارة المستخدمين" + count +
  "إضافة مستخدم"), filter bar (search + role select of the 4 roles + status select), users **table**
  (الاسم / البريد / الدور / الحالة / الإجراءات[تعديل، تفعيل/تعطيل]), add/edit **modal** (الاسم، البريد،
  الدور[disabled on edit], كلمة المرور[create-only or reset], الحالة). Non-admin → "صلاحية محظورة"
  forbidden state (defensive; route is already `roleGuard('admin')`).

---

## 6. File structure

**New:** `core/ui/components/decor-background.component.ts`; `core/ui/layout/layout.component.{ts,html}`;
`core/auth/role-labels.ts`; `core/users/{users.types.ts, users-data-source.ts, mock-users.data-source.ts,
http-users.data-source.ts, users.repository.{interface,port,ts}, users.providers.ts, usecases/*.ts}`;
`features/home/{index.ts, presentation/pages/home.page.ts}`; `features/user-management/{index.ts,
presentation/pages/user-management.page.{ts,html}, presentation/viewmodels/users.viewmodel.ts}`.

**Modified:** `index.html`, `tailwind.config.js`, `styles.scss`, `app.routes.ts`, `app.config.ts`,
`app.ts` (root stays `<router-outlet/>` + toast host — shell is a routed layout), `features/auth/*`
(login/change-password/account restyle + `login.viewmodel.ts` redirect + `shakeKey`), `features/auth/index.ts`,
`core/ui/components/notification-host.component.ts` (toast restyle).

**Removed:** `features/home`(demo)/`api`/`offline-storage`/`design`; `core/datasource/api/*`;
`features/auth/presentation/pages/admin.page.ts`.

---

## 7. Testing

Jest specs (co-located): `MockUsersDataSource` (seed list, duplicate-email conflict, create/update/setStatus),
`UsersRepository` delegation, each users use-case (RED→GREEN), `UsersViewModel` (load, filter by
search/role/status, create validation + success, edit, toggle status, error surfacing), and
`LoginViewModel` `shakeKey` bump on failure (+ existing redirect tests keep passing with the `/home`
target). Component render is exercised via existing page/VM patterns. **Gate:** `npx jest` green +
`npm run build` clean.

## 8. Non-goals / preconditions

- No other reference screens (dashboard/projects/tasks/inventory/finance/procurement/etc.) — inert.
- No backend `/api/users` and no change to the Phase-1 auth logic.
- **Precondition (working tree):** the temporary run-session edits (`auth.providers.ts`→Http,
  `env.ts` baseUrl, `angular.json` serve-target fix) are uncommitted. Before implementation: revert the
  auth Http test-wiring (auth stays Mock-default), **keep** the `angular.json` fix (real bug — commit it),
  and set `env.api.baseUrl` dev value to the backend for swap-readiness.
