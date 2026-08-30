# Auth Frontend — Phase 1 Wiring (design)

**Date:** 2026-07-01
**Feature:** Auth (identity module)
**Source plan:** `docs/frontend/auth-feature-flows.html` (data: `docs/frontend/_data/auth.flows.js`)
**Decisions referenced:** AD-001 (token storage), AD-007 (forced first-login change), AD-008 (4-role model), AD-009 (logout-everywhere)
**Scope decision:** Phase 1 only. Admin→Users (P2, `FE-AUTH-USR`) is explicitly out of scope.
**Transport decision:** `MockAuthDataSource` stays the default binding; `HttpAuthDataSource` is made fully correct so going live is a one-line provider swap + base-URL change.

---

## 1. Goal

Close the Phase-1 gaps in the auth feature so the Angular frontend matches the as-built
Identity backend contract, while keeping the app runnable fully offline on mock auth.

The gaps (from `auth.flows.js` `gapRegister`, P1 only):

| Gap | Summary |
|---|---|
| `FE-AUTH-BE` / `FE-AUTH-WIRE` | `HttpAuthDataSource` must match the real `/api/auth/*` contract; Mock→Http is a 1-line swap. |
| `FE-AUTH-ROLE` (AD-008) | Extend `UserRole` from `'admin' \| 'user'` to `'admin' \| 'manager' \| 'moqawel' \| 'worker'`. |
| `FE-AUTH-WIRE` (AD-009) | Logout signs out everywhere (server revoke-all), best-effort. |
| `FE-AUTH-WIRE` (AD-007) | Login surfaces `mustChangePassword` → redirect to `/change-password`. |
| `FE-AUTH-PWD` (AD-007) | Build the change-password route, use-case, page, and view-model. |
| `FE-AUTH-ME` | `rehydrate()` calls `GET /api/auth/me` instead of parsing the token shape. |

Out of scope (P2): `FE-AUTH-USR` — Admin→Users list/create. `AdminPage` stays the current stub.

---

## 2. As-built backend contract (confirmed, source of truth)

All routes are under `api/[controller]` → `/api/auth/*`. JSON is **camelCase**. Every response is
wrapped in `ApiResponse<T>` which serializes as `{ successStatus, message, error, data }`.
Backend dev URL: `https://localhost:7080` (also `http://localhost:5080`). CORS already allows
`http://localhost:4200`, any header/method, no credentials (Bearer tokens).

| Method / path | Auth | Request body | `data` payload (`SessionDto` unless noted) |
|---|---|---|---|
| `POST /api/auth/login` | anon | `{ email, password }` | `SessionDto` |
| `POST /api/auth/refresh` | anon | `{ refreshToken }` | `SessionDto` (full session) |
| `POST /api/auth/logout` | Bearer | *none* (AD-009) | `null` |
| `GET /api/auth/me` | Bearer | — | `CurrentUserDto` |
| `POST /api/auth/change-password` | Bearer | `{ currentPassword, newPassword }` | `SessionDto` (rotated) |

- `SessionDto` = `{ accessToken, refreshToken, role, userId, mustChangePassword }` — **flat**, `role` is
  a string code (`admin`/`manager`/`moqawel`/`worker`), `userId` is a GUID string.
- `CurrentUserDto` = `{ userId, email, fullName, role }` (no `mustChangePassword`).
- Seed users: `{role}@kheprx.local`, password `ChangeMe123!` (config-overridable), all start
  `mustChangePassword: true`.

### Mismatches to fix in `HttpAuthDataSource` (current state)
1. Posts to `/auth/*` — missing the `/api` prefix.
2. Points at `dummyjson.com` — needs the backend base URL when live.
3. Does not unwrap the `ApiResponse<T>` envelope (reads the session directly).
4. Expects a nested `AuthSession`; backend returns a flat `SessionDto`.

---

## 3. Domain model & port changes (`core/auth`)

### `auth.types.ts`
```ts
export type UserRole = 'admin' | 'manager' | 'moqawel' | 'worker';
export interface AuthTokens { accessToken: string; refreshToken: string; }
export interface AuthPrincipal { role: UserRole; userId: string; }
export interface AuthSession { tokens: AuthTokens; principal: AuthPrincipal; mustChangePassword: boolean; }
```
`mustChangePassword` lives on `AuthSession` (mirrors `SessionDto`), not on `AuthPrincipal` — it is a
session-lifecycle fact, not an identity attribute. Rejected alternative: a standalone store signal
detached from the login result.

### `auth-data-source.ts` (port) — grows 2 → 5 methods
```ts
login(email: string, password: string): Promise<AuthSession>;
refresh(refreshToken: string): Promise<AuthTokens>;
logout(): Promise<void>;                                             // AD-009
me(): Promise<AuthPrincipal>;                                        // FE-AUTH-ME
changePassword(currentPassword: string, newPassword: string): Promise<AuthSession>; // AD-007
```
Both `MockAuthDataSource` and `HttpAuthDataSource` implement all five. `IAuthRepository` /
`AuthRepository` gain the matching `logout()`, `me()`, and `changePassword()` pass-throughs.

---

## 4. Data sources

### `http-auth.data-source.ts` (rewritten to match the contract)
- Paths: `/api/auth/login`, `/api/auth/refresh`, `/api/auth/logout`, `/api/auth/me`, `/api/auth/change-password`.
- Every call types the response as `BaseResponseRs<T>` and reads `.data` (envelope unwrap).
- Private `toSession(dto: SessionDto): AuthSession` maps flat → nested and copies `mustChangePassword`.
- `refresh()` reads `.data` and returns just `{ accessToken, refreshToken }` (the port returns `AuthTokens`).
- `logout()` POSTs with **no body**; the Bearer header is attached by `authInterceptor`.
- `me()` GETs `/api/auth/me`, maps `CurrentUserDto` → `AuthPrincipal` (`{ role, userId }`).
- `changePassword()` POSTs `{ currentPassword, newPassword }` and returns the rotated session.
- A local `interface SessionDto`/`CurrentUserDto` (camelCase) documents the wire shape.

### `mock-auth.data-source.ts` (extended to the same port; still the default)
- Demo seeds become the 4 roles: `admin/manager/moqawel/worker@example.com`, password `<role>123`.
- One seed (e.g. `moqawel@example.com`) starts `mustChangePassword: true` to exercise the forced-change
  flow offline; the rest are `false`.
- `login()` returns `AuthSession` including `mustChangePassword` from the seed.
- `refresh()` unchanged in spirit (mints new tokens from the mock refresh token shape).
- `logout()` is a no-op (`await Promise.resolve()`).
- `me()` reconstructs the principal by parsing the mock access token (`mock-access.<role>.<userId>`).
- `changePassword()` validates the current password against the seed, mints a fresh session with
  `mustChangePassword: false`.

---

## 5. Use-cases & session store

### `logout.use-case.ts`
```
try { await repo.logout(); } catch { /* best-effort server revoke */ }
await tokenStore.clear();   // local clear always succeeds (flow errorNote)
```
Order matters: revoke first (Bearer still present), then clear.

### `change-password.use-case.ts` (new)
`ChangePasswordUseCase<{ currentPassword; newPassword }, AuthSession>`:
`repo.changePassword()` → `TokenStore.save(session.tokens)` → return the rotated `AuthSession`.

### `refresh-token.use-case.ts`
Unchanged logic (still returns `AuthTokens`).

### `auth-session.store.ts`
- New `mustChangePassword` computed signal off `_session()`.
- New `changePassword(currentPassword, newPassword)` method: runs the use-case, on success
  `_session.set(rotatedSession)` (flag now cleared).
- `rehydrate()` rewritten: the store injects `AUTH_REPOSITORY` directly and, if tokens exist, builds the
  principal via `repo.me()` instead of string-splitting the token (no `MeUseCase` needed — `me()` has no
  token side effect). Real tokens are JWTs; the old `.split('.')` no longer yields role/userId.
  `mustChangePassword` defaults to `false` on rehydrate (the flag only matters right after an actual
  login/change). On `me()` failure, `authInterceptor` handles the 401→refresh→retry or clear+redirect;
  the store leaves `_session` null.

---

## 6. Change-password feature (`features/auth`) — FE-AUTH-PWD

- **`change-password.viewmodel.ts`** (new, route-scoped): `currentPassword`, `newPassword`, `confirm`,
  `loading`, `error` signals; `submit()` validates confirm==new locally, calls
  `AuthSessionStore.changePassword()`, on success navigates to `/account`, on failure sets `error`.
- **`change-password.page.ts`** (new): styled to match `login.page.ts` (three password inputs, error
  banner, submit button, loading label).
- **Route** `/change-password` under `authGuard`, with `ChangePasswordViewModel` in route `providers`.
- **`index.ts`** exports `ChangePasswordPage` + `ChangePasswordViewModel`.
- **`login.viewmodel.ts`**: `AuthSessionStore.signIn()` already returns `Result<AuthSession>`, so after a
  successful sign-in the view-model branches on `r.data` —
  `r.data.mustChangePassword ? router.navigate(['/change-password']) : router.navigate(['/account'])`.
  No store API change needed for the branch.
- **`account.page.ts`**: add a "Change password" link to `/change-password`.

---

## 7. Roles, tests & docs

### Role-union ripple (old `'user'` → new union)
Files touched: `auth.types.ts`, `mock-auth.data-source.ts` (+ spec), `auth-session.store.spec.ts`,
`login.viewmodel.spec.ts`, `login.use-case.spec.ts`, `auth.repository.spec.ts`, `auth.guard.spec.ts`.
`roleGuard('admin')` on `/admin` and `p.role === 'admin'` on the account page remain valid.

### New/updated tests
- `mock-auth.data-source.spec.ts`: `login` returns `mustChangePassword`; `logout`, `me`, `changePassword`.
- `change-password.use-case.spec.ts` (new): saves rotated tokens, returns session.
- `change-password.viewmodel.spec.ts` (new): confirm-mismatch guard, success navigation, error surface.
- `login.viewmodel.spec.ts`: forced-change branch (`mustChangePassword` → `/change-password`).
- `auth-session.store.spec.ts`: `rehydrate` via `me()`; `changePassword` updates `_session`.
- `logout.use-case.spec.ts` (new or extended): server-revoke best-effort + always clears local.

### Docs
- `login.page.ts` demo hint updated for the 4-role seeds.
- `README.md` "Going live" section: swap `MockAuthDataSource → HttpAuthDataSource` in
  `auth.providers.ts`; set `env.api.baseUrl` to `https://localhost:7080`; note CORS already allows
  `:4200`; note backend seed creds (`{role}@kheprx.local` / `ChangeMe123!`, forced change on first login).
- `env.ts`: dev comment points at the backend URL. **Base URL default is NOT changed** (shared with the
  posts feature, which stays on mock); the swap is documented, not applied.

---

## 8. Non-goals / explicitly deferred

- `GET/POST /api/users`, `ListUsersUseCase`, `CreateUserUseCase`, `UsersViewModel`, users repository,
  and the Admin→Users UI — all P2 (`FE-AUTH-USR`). `AdminPage` remains the static stub.
- Changing the default `AUTH_DATA_SOURCE` binding to Http. It stays Mock; Http is made swap-ready only.
- Changing the shared `env.api.baseUrl` default or the API (posts) data source.

---

## 9. Verification gate

- `npx jest` — all specs green (existing + new).
- `npm run build` — production build clean.
- Manual (optional, offline): login as a `mustChangePassword` mock user → redirected to
  `/change-password` → change → land on `/account`; sign out → `/login`.
