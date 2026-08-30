# النور كهرباء — لوحة التحكم

**النور كهرباء** is an RTL Arabic Angular v20 dashboard for an electrical business. The flow is: **login → home hub → إدارة المستخدمين**. All screens are right-to-left Arabic. Only the Home hub and إدارة المستخدمين (+ auth pages) are currently active; other sidebar / home-tile items are inert placeholders.

Built on Clean Architecture + MVVM with feature-first vertical slices, a full auth stack, and Tailwind CSS wired to SCSS design tokens.

## Getting started

```sh
npm install
npm start      # ng serve — dev server at http://localhost:4200
npx jest       # run the test suite
npm run build  # production build
```

## Offline by default

The app runs **completely offline** on mock auth and mock users immediately after `npm install && npm start` — no backend required.

- **Mock auth:** `MockAuthDataSource` accepts four seeded accounts:

| Email | Password | Role |
|---|---|---|
| `admin@kheprx.local` | `ChangeMe123!` | `admin` (forced password change on first login) |
| `manager@kheprx.local` | `ChangeMe123!` | `manager` (forced password change on first login) |
| `moqawel@kheprx.local` | `ChangeMe123!` | `moqawel` (forced password change on first login) |
| `worker@kheprx.local` | `ChangeMe123!` | `worker` (forced password change on first login) |

- **Mock users:** إدارة المستخدمين runs on the swap-ready **`USERS_DATA_SOURCE`** seam, currently bound to `MockUsersDataSource` (in-memory). Swap to `HttpUsersDataSource` when the real `/api/users` endpoint is ready — no repository or use-case changes needed.

## Going live — swaps

To connect to a real backend, make the following changes in `src/app/`:

**1. Auth data source** (`core/auth/auth.providers.ts`):
```ts
// replace:
{ provide: AUTH_DATA_SOURCE, useClass: MockAuthDataSource }
// with:
{ provide: AUTH_DATA_SOURCE, useClass: HttpAuthDataSource }
```

`HttpAuthDataSource` targets the Identity backend at `/api/auth/{login,refresh,logout,me,change-password}`.

**2. Users data source** (`features/users/users.providers.ts` or equivalent):
```ts
// replace:
{ provide: USERS_DATA_SOURCE, useClass: MockUsersDataSource }
// with:
{ provide: USERS_DATA_SOURCE, useClass: HttpUsersDataSource }
```

`HttpUsersDataSource` will target `/api/users` once that endpoint exists.

**3. Set the base URL** (`core/config/env.ts`):
```ts
baseUrl: isDev ? 'http://localhost:5080' : 'https://api.example.com'
```
`http://localhost:5080` and `https://localhost:7080` (needs a trusted dev cert) both work for local development.

The backend already allows CORS from `http://localhost:4200`. Seed accounts are
`{role}@kheprx.local` (roles: `admin`, `manager`, `moqawel`, `worker`) with password
`ChangeMe123!`; every seed account is forced to change its password on first sign-in.

**Forced password change (AD-007) — go-live caveat:** on a login that returns `mustChangePassword: true`, `LoginViewModel` redirects to `/change-password`, but the frontend does **not** hard-gate other routes on the flag, and it does not re-surface after a reload (the `GET /api/auth/me` `CurrentUserDto` carries no `mustChangePassword`). A user can navigate away or reload and reach `/account`. Enforcement of the first-login password change must therefore be guaranteed by the **backend**; the frontend redirect is a convenience, not a gate.

No repository or use-case code changes are needed. See the **Go-live caveats** section in [`docs/AUTH.html`](docs/AUTH.html) for known gaps to address before shipping with `HttpAuthDataSource`.

## Architecture

Visual architecture docs (open in a browser): [`docs/index.html`](docs/index.html)
— the documentation hub. The numbered learning path covers Architecture Overview, Project
Libraries, Core Files, Feature Anatomy, Data Flow, Composition Root, Error Handling,
Validation, Add a Feature, Testing, HTTP Client, Data Source Seam, Auth, and Styling.

## Layout

```
src/app/
  core/
    auth/          ← auth types, data sources, use cases, session store
    config/        ← env.ts (base URL, timeouts, retry config)
    datasource/
      api/         ← ApiDataSource seam + MockApiDataSource
      keyvalue/    ← KeyValueStore, StorageKeys
    domain/        ← Result<T>, AppError, UseCase base
    guards/        ← authGuard, roleGuard
    logging/       ← Logger
    network/       ← HttpClientService, http-error, authInterceptor
    crypto/        ← AES-256-GCM CryptoService
    ui/            ← theme.scss / theme.ts, NavButton, TextField, NotificationHost
  features/<group>/<feature>/{data,domain,presentation}   ← vertical slices
```
