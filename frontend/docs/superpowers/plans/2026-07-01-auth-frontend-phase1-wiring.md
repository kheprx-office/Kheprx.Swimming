# Auth Frontend — Phase 1 Wiring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the Angular auth feature to the as-built Identity backend contract (4-role model, forced first-login change-password, logout-everywhere, `/me` rehydrate) while keeping `MockAuthDataSource` the default so the app still runs fully offline.

**Architecture:** Clean Architecture + MVVM, feature-first. Auth flows page → view-model → `AuthSessionStore` (signals) → use-cases → `AuthRepository` → `AUTH_DATA_SOURCE` seam (Mock default / Http swap-ready). Tokens persist via `TokenStore`; `authInterceptor` attaches the Bearer token and refreshes once on 401. This plan widens the auth domain types, grows the transport port to five methods, and adds the change-password vertical slice.

**Tech Stack:** Angular v20 (standalone components, signals), TypeScript 5.9 (`strict`), Jest 30 + `jest-preset-angular`, Tailwind 3.4.

## Global Constraints

- **Scope:** Phase 1 only. Do **not** build `/api/users`, `ListUsersUseCase`, `CreateUserUseCase`, `UsersViewModel`, a users repository, or Admin→Users UI. `AdminPage` stays the current static stub.
- **Default transport:** `AUTH_DATA_SOURCE` stays bound to `MockAuthDataSource`. Do **not** change the default provider in `auth.providers.ts` or `app.config.ts`.
- **Base URL:** Do **not** change the `env.api.baseUrl` default value (shared with the posts feature). Going live is documented, not applied.
- **Roles:** `UserRole` is exactly `'admin' | 'manager' | 'moqawel' | 'worker'` (AD-008). No `'user'` role anywhere.
- **Backend contract (camelCase JSON, wrapped in `ApiResponse<T>` → `{ successStatus, message, error, data }`):**
  - `POST /api/auth/login` (anon) body `{ email, password }` → `data: SessionDto`
  - `POST /api/auth/refresh` (anon) body `{ refreshToken }` → `data: SessionDto`
  - `POST /api/auth/logout` (Bearer, **no body**) → `data: null`
  - `GET /api/auth/me` (Bearer) → `data: CurrentUserDto`
  - `POST /api/auth/change-password` (Bearer) body `{ currentPassword, newPassword }` → `data: SessionDto` (rotated)
  - `SessionDto = { accessToken, refreshToken, role, userId, mustChangePassword }` — **flat**.
  - `CurrentUserDto = { userId, email, fullName, role }`.
- **TDD:** every task writes the failing test first, watches it fail, implements minimally, watches it pass, commits.
- **Commit trailer:** every commit message ends with a blank line then:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`
- **Run commands from** the `frontend/` directory. Single-file test run: `npx jest <path>`. Full suite: `npx jest`. Build: `npm run build`.

---

## File Map

| File | Task | Responsibility |
|---|---|---|
| `src/app/core/auth/auth.types.ts` | 1 | `UserRole` union (4 roles) + `AuthSession.mustChangePassword`. |
| `src/app/core/auth/mock-auth.data-source.ts` | 1, 2 | Offline seed auth; 4-role seeds + `logout/me/changePassword`. |
| `src/app/core/auth/auth-session.store.ts` | 1, 4, 5 | Session signals; `mustChangePassword` signal; `/me` rehydrate; `changePassword`. |
| `src/app/core/auth/auth-data-source.ts` | 2 | Port: `login/refresh/logout/me/changePassword`. |
| `src/app/core/auth/auth.repository.interface.ts` | 2 | `IAuthRepository` (5 methods). |
| `src/app/core/auth/auth.repository.ts` | 2 | Pass-through repository. |
| `src/app/core/auth/http-auth.data-source.ts` | 2 | Real `/api/auth/*` transport; envelope unwrap + flat→nested mapping. |
| `src/app/core/auth/usecases/logout.use-case.ts` | 3 | Best-effort server revoke (AD-009) then local clear. |
| `src/app/core/auth/usecases/change-password.use-case.ts` | 5 | Change password → save rotated tokens → return session. |
| `src/app/features/auth/presentation/viewmodels/change-password.viewmodel.ts` | 6 | Change-password form facade. |
| `src/app/features/auth/presentation/pages/change-password.page.ts` | 6 | Change-password page. |
| `src/app/features/auth/presentation/viewmodels/login.viewmodel.ts` | 7 | Forced-change redirect branch. |
| `src/app/app.routes.ts` | 6 | `/change-password` route. |
| `src/app/features/auth/index.ts` | 6 | Export change-password page + view-model. |
| `src/app/features/auth/presentation/pages/account.page.ts` | 8 | "Change password" link. |
| `src/app/features/auth/presentation/pages/login.page.ts` | 8 | 4-role demo hint. |
| `README.md`, `src/app/core/config/env.ts` | 8 | Going-live docs. |

Spec files updated alongside their subjects (Tasks 1, 2, 3, 4, 5, 6, 7).

---

## Task 1: Widen the auth domain types (4 roles + `mustChangePassword`)

Mechanical contract-widening: extend `UserRole` (AD-008) and add the `mustChangePassword` flag to `AuthSession` (AD-007), then update every construction site and spec literal so the existing suite stays green. No new behavior.

**Files:**
- Modify: `src/app/core/auth/auth.types.ts`
- Modify: `src/app/core/auth/mock-auth.data-source.ts`
- Modify: `src/app/core/auth/auth-session.store.ts`
- Test (modify): `src/app/core/auth/mock-auth.data-source.spec.ts`, `src/app/core/auth/auth-session.store.spec.ts`, `src/app/features/auth/presentation/viewmodels/login.viewmodel.spec.ts`, `src/app/core/auth/auth.repository.spec.ts`, `src/app/core/auth/usecases/login.use-case.spec.ts`, `src/app/core/auth/usecases/refresh-token.use-case.spec.ts`, `src/app/core/guards/auth.guard.spec.ts`

**Interfaces:**
- Produces: `UserRole = 'admin' | 'manager' | 'moqawel' | 'worker'`; `AuthSession = { tokens: AuthTokens; principal: AuthPrincipal; mustChangePassword: boolean }`; `AuthSessionStore.mustChangePassword: Signal<boolean>`.

- [ ] **Step 1: Update the failing specs first (role rename + `mustChangePassword` on every `AuthSession` literal).**

In `src/app/core/auth/mock-auth.data-source.spec.ts` replace the file body with:

```ts
import { MockAuthDataSource } from '@core/auth/mock-auth.data-source';

describe('MockAuthDataSource', () => {
  const ds = new MockAuthDataSource();

  it('logs in the admin demo user and mints role-stamped tokens', async () => {
    const s = await ds.login('admin@example.com', 'admin123');
    expect(s.principal).toEqual({ role: 'admin', userId: 'USR-ADMIN' });
    expect(s.tokens.accessToken).toBe('mock-access.admin.USR-ADMIN');
    expect(s.mustChangePassword).toBe(false);
  });

  it('logs in the manager demo user', async () => {
    const s = await ds.login('manager@example.com', 'manager123');
    expect(s.principal.role).toBe('manager');
  });

  it('surfaces mustChangePassword for the forced-change seed (moqawel)', async () => {
    const s = await ds.login('moqawel@example.com', 'moqawel123');
    expect(s.mustChangePassword).toBe(true);
  });

  it('rejects bad credentials with kind=auth, status 401', async () => {
    await expect(ds.login('admin@example.com', 'wrong')).rejects.toMatchObject({ kind: 'auth', status: 401 });
  });

  it('refreshes a mock refresh token', async () => {
    const t = await ds.refresh('mock-refresh.worker.USR-WORKER');
    expect(t.accessToken).toBe('mock-access.worker.USR-WORKER');
  });
});
```

In `src/app/core/auth/auth-session.store.spec.ts` update the shared `session` literal (line ~12) to add the flag:

```ts
const session: AuthSession = {
  tokens: { accessToken: 'mock-access.admin.USR-ADMIN', refreshToken: 'mock-refresh.admin.USR-ADMIN' },
  principal: { role: 'admin', userId: 'USR-ADMIN' },
  mustChangePassword: false,
};
```

In `src/app/features/auth/presentation/viewmodels/login.viewmodel.spec.ts` update the `session` literal (line ~9):

```ts
const session: AuthSession = { tokens: { accessToken: 'a', refreshToken: 'r' }, principal: { role: 'admin', userId: 'USR-ADMIN' }, mustChangePassword: false };
```

In `src/app/core/auth/auth.repository.spec.ts` update the `session` literal (line ~6):

```ts
const session: AuthSession = { tokens: { accessToken: 'a', refreshToken: 'r' }, principal: { role: 'admin', userId: 'USR-ADMIN' }, mustChangePassword: false };
```

In `src/app/core/auth/usecases/login.use-case.spec.ts` add the flag to the `fakeRepo.login` return (line ~11):

```ts
      ? { tokens: { accessToken: 'a', refreshToken: 'r' }, principal: { role: 'admin', userId: 'USR-ADMIN' }, mustChangePassword: false }
```

In `src/app/core/auth/usecases/refresh-token.use-case.spec.ts` add the flag to the `fakeRepo.login` return (line ~8):

```ts
  login: async () => ({ tokens: { accessToken: 'a', refreshToken: 'r' }, principal: { role: 'admin', userId: 'USR-ADMIN' }, mustChangePassword: false }),
```

In `src/app/core/guards/auth.guard.spec.ts` change the non-matching role (line ~38) from `'user'` to `'worker'`:

```ts
    const { injector, router } = setup({ isAuthenticated: () => true, role: () => 'worker' } as Partial<AuthSessionStore>);
```

- [ ] **Step 2: Run the changed specs to verify they fail (types not widened yet).**

Run: `npx jest src/app/core/auth/mock-auth.data-source.spec.ts`
Expected: FAIL — TypeScript errors (`mustChangePassword` missing on the return type; `'moqawel'` not assignable to `'admin' | 'user'`).

- [ ] **Step 3: Widen the domain types.**

Replace `src/app/core/auth/auth.types.ts` with:

```ts
// Auth domain primitives. UserRole is the Kheprx Electric role set (AD-008).
export type UserRole = 'admin' | 'manager' | 'moqawel' | 'worker';

export interface AuthTokens { accessToken: string; refreshToken: string; }
export interface AuthPrincipal { role: UserRole; userId: string; }
// mustChangePassword is a session-lifecycle fact (AD-007 forced first-login change),
// surfaced by login/refresh/change-password; not part of the identity principal.
export interface AuthSession { tokens: AuthTokens; principal: AuthPrincipal; mustChangePassword: boolean; }
```

- [ ] **Step 4: Update the mock seeds to the 4 roles + forced-change flag.**

In `src/app/core/auth/mock-auth.data-source.ts` replace the `DemoUser` interface, the `USERS` array, and the `login` return:

```ts
interface DemoUser { email: string; password: string; role: UserRole; userId: string; mustChangePassword: boolean; }

const USERS: DemoUser[] = [
  { email: 'admin@example.com',   password: 'admin123',   role: 'admin',   userId: 'USR-ADMIN',   mustChangePassword: false },
  { email: 'manager@example.com', password: 'manager123', role: 'manager', userId: 'USR-MANAGER', mustChangePassword: false },
  { email: 'moqawel@example.com', password: 'moqawel123', role: 'moqawel', userId: 'USR-MOQAWEL', mustChangePassword: true  },
  { email: 'worker@example.com',  password: 'worker123',  role: 'worker',  userId: 'USR-WORKER',  mustChangePassword: false },
];
```

And change the `login` return line to include the flag:

```ts
    return { tokens: this.mint(user.role, user.userId), principal: { role: user.role, userId: user.userId }, mustChangePassword: user.mustChangePassword };
```

(Leave `refresh` and `mint` unchanged.)

- [ ] **Step 5: Keep the store compiling — add the `mustChangePassword` signal and fix the rehydrate literal.**

In `src/app/core/auth/auth-session.store.ts` add the computed signal after `role` (line ~22):

```ts
  readonly mustChangePassword = computed<boolean>(() => this._session()?.mustChangePassword ?? false);
```

And in `rehydrate()`, add the flag to the constructed session (the token-parse rehydrate stays until Task 4):

```ts
    this._session.set({ tokens: { accessToken, refreshToken }, principal: { role: role as UserRole, userId }, mustChangePassword: false });
```

- [ ] **Step 6: Run the auth suite to verify green.**

Run: `npx jest src/app/core/auth src/app/core/guards src/app/features/auth`
Expected: PASS (all auth/guard/feature specs green).

- [ ] **Step 7: Full build to confirm no type breakage elsewhere.**

Run: `npm run build`
Expected: build succeeds.

- [ ] **Step 8: Commit.**

```bash
git add src/app/core/auth/auth.types.ts src/app/core/auth/mock-auth.data-source.ts src/app/core/auth/auth-session.store.ts src/app/core/auth/mock-auth.data-source.spec.ts src/app/core/auth/auth-session.store.spec.ts src/app/features/auth/presentation/viewmodels/login.viewmodel.spec.ts src/app/core/auth/auth.repository.spec.ts src/app/core/auth/usecases/login.use-case.spec.ts src/app/core/auth/usecases/refresh-token.use-case.spec.ts src/app/core/guards/auth.guard.spec.ts
git commit -m "feat(auth): widen UserRole to 4 roles + add mustChangePassword (AD-007/008)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: Complete the transport seam (port, repository, Mock, Http)

Grow the `AuthDataSource` port and `IAuthRepository` from 2 to 5 methods, implement `logout/me/changePassword` in `MockAuthDataSource` (which now injects `TokenStore` to simulate the Bearer-scoped `/me` and change-password), and rewrite `HttpAuthDataSource` to the real `/api/auth/*` contract (envelope unwrap + flat→nested mapping). These files change together because TypeScript requires both data-source implementations to satisfy the widened port in one commit.

**Files:**
- Modify: `src/app/core/auth/auth-data-source.ts`
- Modify: `src/app/core/auth/auth.repository.interface.ts`
- Modify: `src/app/core/auth/auth.repository.ts`
- Modify: `src/app/core/auth/mock-auth.data-source.ts`
- Modify: `src/app/core/auth/http-auth.data-source.ts`
- Test (create): `src/app/core/auth/http-auth.data-source.spec.ts`
- Test (modify): `src/app/core/auth/mock-auth.data-source.spec.ts`, `src/app/core/auth/auth.repository.spec.ts`, `src/app/core/auth/usecases/login.use-case.spec.ts`, `src/app/core/auth/usecases/refresh-token.use-case.spec.ts`

**Interfaces:**
- Consumes: `AuthSession`, `AuthTokens`, `AuthPrincipal`, `UserRole` (Task 1); `HttpClientService.post/get`; `BaseResponseRs<T>` (`{ data: T; success?; message? }`).
- Produces (on port + `IAuthRepository`, both implemented by Mock and Http):
  - `login(email, password): Promise<AuthSession>`
  - `refresh(refreshToken): Promise<AuthTokens>`
  - `logout(): Promise<void>`
  - `me(): Promise<AuthPrincipal>`
  - `changePassword(currentPassword, newPassword): Promise<AuthSession>`

- [ ] **Step 1: Write the failing Http data-source spec.**

Create `src/app/core/auth/http-auth.data-source.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { HttpAuthDataSource } from '@core/auth/http-auth.data-source';
import { HttpClientService } from '@core/network/api/http-client';

describe('HttpAuthDataSource', () => {
  const http = { post: jest.fn(), get: jest.fn() } as unknown as HttpClientService;
  let ds: HttpAuthDataSource;

  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [HttpAuthDataSource, { provide: HttpClientService, useValue: http }],
    });
    ds = TestBed.inject(HttpAuthDataSource);
  });

  it('login unwraps ApiResponse and maps flat SessionDto to a nested AuthSession', async () => {
    (http.post as jest.Mock).mockResolvedValue({
      data: { accessToken: 'jwt', refreshToken: 'r1', role: 'manager', userId: 'u-1', mustChangePassword: true },
    });
    const s = await ds.login('manager@kheprx.local', 'x');
    expect(http.post).toHaveBeenCalledWith('/api/auth/login', { body: { email: 'manager@kheprx.local', password: 'x' } });
    expect(s).toEqual({
      tokens: { accessToken: 'jwt', refreshToken: 'r1' },
      principal: { role: 'manager', userId: 'u-1' },
      mustChangePassword: true,
    });
  });

  it('refresh returns only the rotated tokens from the envelope', async () => {
    (http.post as jest.Mock).mockResolvedValue({
      data: { accessToken: 'jwt2', refreshToken: 'r2', role: 'admin', userId: 'u', mustChangePassword: false },
    });
    await expect(ds.refresh('r1')).resolves.toEqual({ accessToken: 'jwt2', refreshToken: 'r2' });
    expect(http.post).toHaveBeenCalledWith('/api/auth/refresh', { body: { refreshToken: 'r1' } });
  });

  it('logout POSTs to /api/auth/logout with no body', async () => {
    (http.post as jest.Mock).mockResolvedValue({ data: null });
    await ds.logout();
    expect(http.post).toHaveBeenCalledWith('/api/auth/logout', {});
  });

  it('me maps CurrentUserDto to a principal', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: { userId: 'u-9', email: 'a@b.c', fullName: 'A', role: 'worker' } });
    await expect(ds.me()).resolves.toEqual({ role: 'worker', userId: 'u-9' });
    expect(http.get).toHaveBeenCalledWith('/api/auth/me');
  });

  it('changePassword maps the rotated session', async () => {
    (http.post as jest.Mock).mockResolvedValue({
      data: { accessToken: 'n', refreshToken: 'nr', role: 'admin', userId: 'u', mustChangePassword: false },
    });
    const s = await ds.changePassword('old', 'new');
    expect(http.post).toHaveBeenCalledWith('/api/auth/change-password', { body: { currentPassword: 'old', newPassword: 'new' } });
    expect(s.mustChangePassword).toBe(false);
    expect(s.tokens.accessToken).toBe('n');
  });
});
```

- [ ] **Step 2: Run it to verify it fails.**

Run: `npx jest src/app/core/auth/http-auth.data-source.spec.ts`
Expected: FAIL — current `HttpAuthDataSource` posts to `/auth/login`, doesn't unwrap `.data`, and lacks `logout/me/changePassword`.

- [ ] **Step 3: Extend the port and repository interface.**

Replace `src/app/core/auth/auth-data-source.ts` with:

```ts
import { InjectionToken } from '@angular/core';
import { AuthSession, AuthTokens, AuthPrincipal } from '@core/auth/auth.types';

// AuthDataSource: the swappable auth transport. MockAuthDataSource validates against
// seed users (default); HttpAuthDataSource talks to the Identity backend. Swap the
// binding in AUTH_PROVIDERS to go live.
export interface AuthDataSource {
  login(email: string, password: string): Promise<AuthSession>;
  refresh(refreshToken: string): Promise<AuthTokens>;
  logout(): Promise<void>;                                                     // AD-009 server revoke-all
  me(): Promise<AuthPrincipal>;                                                // rehydrate via GET /me
  changePassword(currentPassword: string, newPassword: string): Promise<AuthSession>; // AD-007
}

export const AUTH_DATA_SOURCE = new InjectionToken<AuthDataSource>('AUTH_DATA_SOURCE');
```

Replace `src/app/core/auth/auth.repository.interface.ts` with:

```ts
import { AuthSession, AuthTokens, AuthPrincipal } from '@core/auth/auth.types';

export interface IAuthRepository {
  login(email: string, password: string): Promise<AuthSession>;
  refresh(refreshToken: string): Promise<AuthTokens>;
  logout(): Promise<void>;
  me(): Promise<AuthPrincipal>;
  changePassword(currentPassword: string, newPassword: string): Promise<AuthSession>;
}
```

- [ ] **Step 4: Add the pass-throughs to the repository.**

Replace `src/app/core/auth/auth.repository.ts` with:

```ts
import { Injectable, inject } from '@angular/core';
import { IAuthRepository } from '@core/auth/auth.repository.interface';
import { AUTH_DATA_SOURCE } from '@core/auth/auth-data-source';
import { AuthSession, AuthTokens, AuthPrincipal } from '@core/auth/auth.types';

@Injectable({ providedIn: 'root' })
export class AuthRepository implements IAuthRepository {
  private readonly api = inject(AUTH_DATA_SOURCE);
  login(email: string, password: string): Promise<AuthSession> { return this.api.login(email, password); }
  refresh(refreshToken: string): Promise<AuthTokens> { return this.api.refresh(refreshToken); }
  logout(): Promise<void> { return this.api.logout(); }
  me(): Promise<AuthPrincipal> { return this.api.me(); }
  changePassword(currentPassword: string, newPassword: string): Promise<AuthSession> {
    return this.api.changePassword(currentPassword, newPassword);
  }
}
```

- [ ] **Step 5: Implement the three new methods in the mock (inject `TokenStore`).**

Replace `src/app/core/auth/mock-auth.data-source.ts` with:

```ts
// MockAuthDataSource: seed-backed auth for offline/demo use. Validates against four
// role demo users and mints deterministic mock tokens. me()/changePassword() read the
// stored mock token to simulate the backend's Bearer-scoped endpoints. Swap for
// HttpAuthDataSource at integration time.
import { Injectable, inject } from '@angular/core';
import { AppError } from '@core/domain/errors/app-error';
import { AuthDataSource } from '@core/auth/auth-data-source';
import { AuthSession, AuthTokens, AuthPrincipal, UserRole } from '@core/auth/auth.types';
import { TokenStore } from '@core/auth/token-store';

interface DemoUser { email: string; password: string; role: UserRole; userId: string; mustChangePassword: boolean; }

const USERS: DemoUser[] = [
  { email: 'admin@example.com',   password: 'admin123',   role: 'admin',   userId: 'USR-ADMIN',   mustChangePassword: false },
  { email: 'manager@example.com', password: 'manager123', role: 'manager', userId: 'USR-MANAGER', mustChangePassword: false },
  { email: 'moqawel@example.com', password: 'moqawel123', role: 'moqawel', userId: 'USR-MOQAWEL', mustChangePassword: true  },
  { email: 'worker@example.com',  password: 'worker123',  role: 'worker',  userId: 'USR-WORKER',  mustChangePassword: false },
];

@Injectable({ providedIn: 'root' })
export class MockAuthDataSource implements AuthDataSource {
  private readonly tokens = inject(TokenStore);

  async login(email: string, password: string): Promise<AuthSession> {
    await Promise.resolve();
    const e = email.trim().toLowerCase();
    const user = USERS.find((u) => u.email === e && u.password === password);
    if (!user) throw new AppError('Invalid email or password', 'auth', 401);
    return { tokens: this.mint(user.role, user.userId), principal: { role: user.role, userId: user.userId }, mustChangePassword: user.mustChangePassword };
  }

  async refresh(refreshToken: string): Promise<AuthTokens> {
    await Promise.resolve();
    if (!refreshToken.startsWith('mock-refresh.')) throw new AppError('Invalid refresh token', 'auth', 401);
    const [, role, userId] = refreshToken.split('.');
    return this.mint(role as UserRole, userId);
  }

  async logout(): Promise<void> { await Promise.resolve(); } // no server session to revoke offline

  async me(): Promise<AuthPrincipal> {
    const access = await this.tokens.getAccess();
    if (!access || !access.startsWith('mock-access.')) throw new AppError('Not authenticated', 'auth', 401);
    const [, role, userId] = access.split('.');
    return { role: role as UserRole, userId };
  }

  async changePassword(currentPassword: string, newPassword: string): Promise<AuthSession> {
    const access = await this.tokens.getAccess();
    const [, , userId] = (access ?? '').split('.');
    const user = USERS.find((u) => u.userId === userId);
    if (!user || user.password !== currentPassword) throw new AppError('Current password is incorrect', 'auth', 400);
    void newPassword; // mock does not persist the new password
    return { tokens: this.mint(user.role, user.userId), principal: { role: user.role, userId: user.userId }, mustChangePassword: false };
  }

  private mint(role: UserRole, userId: string): AuthTokens {
    return { accessToken: `mock-access.${role}.${userId}`, refreshToken: `mock-refresh.${role}.${userId}` };
  }
}
```

- [ ] **Step 6: Rewrite the Http data source to the real contract.**

Replace `src/app/core/auth/http-auth.data-source.ts` with:

```ts
// HttpAuthDataSource: the real auth transport against the Identity backend. Bind it in
// place of MockAuthDataSource (and set env.api.baseUrl to the backend) to go live.
// All responses are wrapped in ApiResponse<T> ({ data, ... }); SessionDto is flat and
// is mapped to the nested AuthSession here.
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { BaseResponseRs } from '@core/network/api/base-response-rs';
import { AuthDataSource } from '@core/auth/auth-data-source';
import { AuthSession, AuthTokens, AuthPrincipal, UserRole } from '@core/auth/auth.types';

interface SessionDto { accessToken: string; refreshToken: string; role: string; userId: string; mustChangePassword: boolean; }
interface CurrentUserDto { userId: string; email: string; fullName: string; role: string; }

@Injectable({ providedIn: 'root' })
export class HttpAuthDataSource implements AuthDataSource {
  private readonly http = inject(HttpClientService);

  async login(email: string, password: string): Promise<AuthSession> {
    const res = await this.http.post<BaseResponseRs<SessionDto>>('/api/auth/login', { body: { email, password } });
    return this.toSession(res.data);
  }

  async refresh(refreshToken: string): Promise<AuthTokens> {
    const res = await this.http.post<BaseResponseRs<SessionDto>>('/api/auth/refresh', { body: { refreshToken } });
    return { accessToken: res.data.accessToken, refreshToken: res.data.refreshToken };
  }

  async logout(): Promise<void> {
    await this.http.post<BaseResponseRs<unknown>>('/api/auth/logout', {}); // Bearer attached by authInterceptor; no body (AD-009)
  }

  async me(): Promise<AuthPrincipal> {
    const res = await this.http.get<BaseResponseRs<CurrentUserDto>>('/api/auth/me');
    return { role: res.data.role as UserRole, userId: res.data.userId };
  }

  async changePassword(currentPassword: string, newPassword: string): Promise<AuthSession> {
    const res = await this.http.post<BaseResponseRs<SessionDto>>('/api/auth/change-password', { body: { currentPassword, newPassword } });
    return this.toSession(res.data);
  }

  private toSession(dto: SessionDto): AuthSession {
    return {
      tokens: { accessToken: dto.accessToken, refreshToken: dto.refreshToken },
      principal: { role: dto.role as UserRole, userId: dto.userId },
      mustChangePassword: dto.mustChangePassword,
    };
  }
}
```

- [ ] **Step 7: Fix the use-case + repository spec fakes to satisfy the widened `IAuthRepository`/`AuthDataSource`.**

In `src/app/core/auth/usecases/login.use-case.spec.ts` replace `fakeRepo` (add the 3 methods):

```ts
const fakeRepo: IAuthRepository = {
  login: async (email) =>
    email === 'admin@example.com'
      ? { tokens: { accessToken: 'a', refreshToken: 'r' }, principal: { role: 'admin', userId: 'USR-ADMIN' }, mustChangePassword: false }
      : Promise.reject(new AppError('bad', 'auth', 401)),
  refresh: async () => ({ accessToken: 'a2', refreshToken: 'r2' }),
  logout: async () => undefined,
  me: async () => ({ role: 'admin', userId: 'USR-ADMIN' }),
  changePassword: async () => ({ tokens: { accessToken: 'a', refreshToken: 'r' }, principal: { role: 'admin', userId: 'USR-ADMIN' }, mustChangePassword: false }),
};
```

In `src/app/core/auth/usecases/refresh-token.use-case.spec.ts` replace `fakeRepo`:

```ts
const fakeRepo: IAuthRepository = {
  login: async () => ({ tokens: { accessToken: 'a', refreshToken: 'r' }, principal: { role: 'admin', userId: 'USR-ADMIN' }, mustChangePassword: false }),
  refresh: async () => ({ accessToken: 'a2', refreshToken: 'r2' }),
  logout: async () => undefined,
  me: async () => ({ role: 'admin', userId: 'USR-ADMIN' }),
  changePassword: async () => ({ tokens: { accessToken: 'a', refreshToken: 'r' }, principal: { role: 'admin', userId: 'USR-ADMIN' }, mustChangePassword: false }),
};
```

In `src/app/core/auth/auth.repository.spec.ts` replace the fake data source in the test (line ~10) so it satisfies `AuthDataSource`:

```ts
    const api = {
      login: async () => session,
      refresh: async () => session.tokens,
      logout: async () => undefined,
      me: async () => session.principal,
      changePassword: async () => session,
    } as AuthDataSource;
```

- [ ] **Step 8: Convert the mock spec to TestBed (mock now injects `TokenStore`) and add the new-method cases.**

Replace `src/app/core/auth/mock-auth.data-source.spec.ts` with:

```ts
import { TestBed } from '@angular/core/testing';
import { MockAuthDataSource } from '@core/auth/mock-auth.data-source';
import { TokenStore } from '@core/auth/token-store';
import { KeyValueStore } from '@core/datasource/keyvalue/key-value.store';

describe('MockAuthDataSource', () => {
  let ds: MockAuthDataSource;
  let tokens: TokenStore;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [MockAuthDataSource, TokenStore, KeyValueStore] });
    ds = TestBed.inject(MockAuthDataSource);
    tokens = TestBed.inject(TokenStore);
  });

  it('logs in the admin demo user and mints role-stamped tokens', async () => {
    const s = await ds.login('admin@example.com', 'admin123');
    expect(s.principal).toEqual({ role: 'admin', userId: 'USR-ADMIN' });
    expect(s.tokens.accessToken).toBe('mock-access.admin.USR-ADMIN');
    expect(s.mustChangePassword).toBe(false);
  });

  it('surfaces mustChangePassword for the forced-change seed (moqawel)', async () => {
    const s = await ds.login('moqawel@example.com', 'moqawel123');
    expect(s.mustChangePassword).toBe(true);
  });

  it('rejects bad credentials with kind=auth, status 401', async () => {
    await expect(ds.login('admin@example.com', 'wrong')).rejects.toMatchObject({ kind: 'auth', status: 401 });
  });

  it('refreshes a mock refresh token', async () => {
    const t = await ds.refresh('mock-refresh.worker.USR-WORKER');
    expect(t.accessToken).toBe('mock-access.worker.USR-WORKER');
  });

  it('logout resolves without throwing', async () => {
    await expect(ds.logout()).resolves.toBeUndefined();
  });

  it('me reconstructs the principal from the stored mock token', async () => {
    await tokens.save({ accessToken: 'mock-access.manager.USR-MANAGER', refreshToken: 'mock-refresh.manager.USR-MANAGER' });
    await expect(ds.me()).resolves.toEqual({ role: 'manager', userId: 'USR-MANAGER' });
  });

  it('me rejects when no token is stored', async () => {
    await expect(ds.me()).rejects.toMatchObject({ kind: 'auth', status: 401 });
  });

  it('changePassword clears the flag when the current password matches', async () => {
    await ds.login('moqawel@example.com', 'moqawel123');
    await tokens.save({ accessToken: 'mock-access.moqawel.USR-MOQAWEL', refreshToken: 'mock-refresh.moqawel.USR-MOQAWEL' });
    const s = await ds.changePassword('moqawel123', 'newpass123');
    expect(s.mustChangePassword).toBe(false);
    expect(s.principal.role).toBe('moqawel');
  });

  it('changePassword rejects a wrong current password with status 400', async () => {
    await tokens.save({ accessToken: 'mock-access.moqawel.USR-MOQAWEL', refreshToken: 'mock-refresh.moqawel.USR-MOQAWEL' });
    await expect(ds.changePassword('wrong', 'newpass123')).rejects.toMatchObject({ kind: 'auth', status: 400 });
  });
});
```

- [ ] **Step 9: Run the auth suite to verify green.**

Run: `npx jest src/app/core/auth`
Expected: PASS — including the new `http-auth.data-source.spec.ts` and the extended mock spec.

- [ ] **Step 10: Commit.**

```bash
git add src/app/core/auth/auth-data-source.ts src/app/core/auth/auth.repository.interface.ts src/app/core/auth/auth.repository.ts src/app/core/auth/mock-auth.data-source.ts src/app/core/auth/http-auth.data-source.ts src/app/core/auth/http-auth.data-source.spec.ts src/app/core/auth/mock-auth.data-source.spec.ts src/app/core/auth/auth.repository.spec.ts src/app/core/auth/usecases/login.use-case.spec.ts src/app/core/auth/usecases/refresh-token.use-case.spec.ts
git commit -m "feat(auth): grow transport seam to 5 methods; Http matches /api/auth/* contract

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: Logout signs out everywhere (AD-009)

Wire `LogoutUseCase` to call the server revoke-all best-effort, then always clear local tokens. Because `LogoutUseCase` now injects `AUTH_REPOSITORY`, the `AuthSessionStore` spec (which uses the real `LogoutUseCase`) must provide a fake `AUTH_DATA_SOURCE` so the repository resolves.

**Files:**
- Modify: `src/app/core/auth/usecases/logout.use-case.ts`
- Test (create): `src/app/core/auth/usecases/logout.use-case.spec.ts`
- Test (modify): `src/app/core/auth/auth-session.store.spec.ts`

**Interfaces:**
- Consumes: `AUTH_REPOSITORY.logout()` (Task 2), `TokenStore.clear()`.
- Produces: `LogoutUseCase.run()` calls `repo.logout()` (best-effort) then `TokenStore.clear()`.

- [ ] **Step 1: Write the failing use-case spec.**

Create `src/app/core/auth/usecases/logout.use-case.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { LogoutUseCase } from '@core/auth/usecases/logout.use-case';
import { AUTH_REPOSITORY, IAuthRepository } from '@core/auth/auth.repository.port';
import { TokenStore } from '@core/auth/token-store';
import { KeyValueStore } from '@core/datasource/keyvalue/key-value.store';

function makeRepo(overrides: Partial<IAuthRepository> = {}): IAuthRepository {
  return {
    login: jest.fn(),
    refresh: jest.fn(),
    logout: jest.fn().mockResolvedValue(undefined),
    me: jest.fn(),
    changePassword: jest.fn(),
    ...overrides,
  } as unknown as IAuthRepository;
}

describe('LogoutUseCase', () => {
  let tokens: TokenStore;
  const build = (repo: IAuthRepository) => {
    TestBed.resetTestingModule();
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [LogoutUseCase, TokenStore, KeyValueStore, { provide: AUTH_REPOSITORY, useValue: repo }],
    });
    tokens = TestBed.inject(TokenStore);
    return TestBed.inject(LogoutUseCase);
  };

  it('calls the server revoke then clears local tokens', async () => {
    const repo = makeRepo();
    const uc = build(repo);
    await tokens.save({ accessToken: 'a', refreshToken: 'r' });
    const r = await uc.run();
    expect(r.ok).toBe(true);
    expect(repo.logout).toHaveBeenCalled();
    expect(await tokens.getAccess()).toBeNull();
  });

  it('still clears local tokens when the server revoke fails', async () => {
    const repo = makeRepo({ logout: jest.fn().mockRejectedValue(new Error('network')) });
    const uc = build(repo);
    await tokens.save({ accessToken: 'a', refreshToken: 'r' });
    const r = await uc.run();
    expect(r.ok).toBe(true);
    expect(await tokens.getAccess()).toBeNull();
  });
});
```

- [ ] **Step 2: Run it to verify it fails.**

Run: `npx jest src/app/core/auth/usecases/logout.use-case.spec.ts`
Expected: FAIL — current `LogoutUseCase` never calls a repository (`repo.logout` not called).

- [ ] **Step 3: Implement the best-effort server revoke.**

Replace `src/app/core/auth/usecases/logout.use-case.ts` with:

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AUTH_REPOSITORY } from '@core/auth/auth.repository.port';
import { TokenStore } from '@core/auth/token-store';

@Injectable({ providedIn: 'root' })
export class LogoutUseCase extends UseCase<void, void> {
  private readonly repo = inject(AUTH_REPOSITORY);
  private readonly store = inject(TokenStore);
  constructor() { super('Logout'); }
  protected async execute(): Promise<void> {
    // AD-009: revoke the server session (best-effort). The local clear must always
    // succeed, so a failed revoke is swallowed.
    try { await this.repo.logout(); } catch { /* best-effort */ }
    await this.store.clear();
  }
}
```

- [ ] **Step 4: Keep the store spec green — provide a fake `AUTH_DATA_SOURCE`.**

In `src/app/core/auth/auth-session.store.spec.ts`, add these imports:

```ts
import { AUTH_DATA_SOURCE, AuthDataSource } from '@core/auth/auth-data-source';
```

Add a fake data source above `describe` (below the `session` literal):

```ts
const dataSource = {
  login: jest.fn(),
  refresh: jest.fn(),
  logout: jest.fn().mockResolvedValue(undefined),
  me: jest.fn(),
  changePassword: jest.fn(),
} as unknown as AuthDataSource;
```

Add `{ provide: AUTH_DATA_SOURCE, useValue: dataSource }` to **both** `TestBed.configureTestingModule` provider arrays in this file (the `beforeEach` one and the one inside the rehydrate test). Also add `jest.clearAllMocks()` is already in `beforeEach`; keep it.

- [ ] **Step 5: Run the store + logout specs to verify green.**

Run: `npx jest src/app/core/auth/auth-session.store.spec.ts src/app/core/auth/usecases/logout.use-case.spec.ts`
Expected: PASS.

- [ ] **Step 6: Commit.**

```bash
git add src/app/core/auth/usecases/logout.use-case.ts src/app/core/auth/usecases/logout.use-case.spec.ts src/app/core/auth/auth-session.store.spec.ts
git commit -m "feat(auth): logout revokes server session best-effort then clears local (AD-009)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: Rehydrate via GET /me (FE-AUTH-ME)

Replace the token-parsing rehydrate with a call to `repo.me()`, so a page reload reconstructs the principal from the backend (real JWTs no longer encode role/userId in a splittable shape). The `AuthSessionStore` gains an `AUTH_REPOSITORY` dependency.

**Files:**
- Modify: `src/app/core/auth/auth-session.store.ts`
- Test (modify): `src/app/core/auth/auth-session.store.spec.ts`

**Interfaces:**
- Consumes: `AUTH_REPOSITORY.me(): Promise<AuthPrincipal>` (Task 2).
- Produces: `AuthSessionStore` constructor rehydrates via `repo.me()`; on failure leaves `_session` null.

- [ ] **Step 1: Rewrite the rehydrate spec to assert the `/me` path.**

In `src/app/core/auth/auth-session.store.spec.ts` replace the existing `rehydrates session from stored tokens` test with:

```ts
  it('rehydrates the session by calling GET /me', async () => {
    TestBed.resetTestingModule();
    localStorage.setItem(StorageKeys.accessToken, 'jwt-access');
    localStorage.setItem(StorageKeys.refreshToken, 'jwt-refresh');
    (dataSource.me as jest.Mock).mockResolvedValue({ role: 'admin', userId: 'USR-ADMIN' });
    TestBed.configureTestingModule({
      providers: [
        AuthSessionStore, LogoutUseCase, TokenStore, KeyValueStore,
        { provide: LoginUseCase, useValue: login },
        { provide: AUTH_DATA_SOURCE, useValue: dataSource },
      ],
    });
    const freshStore = TestBed.inject(AuthSessionStore);
    await Promise.resolve();
    await Promise.resolve();
    expect(dataSource.me).toHaveBeenCalled();
    expect(freshStore.isAuthenticated()).toBe(true);
    expect(freshStore.role()).toBe('admin');
    expect(freshStore.principal()?.userId).toBe('USR-ADMIN');
  });

  it('stays unauthenticated when /me rejects', async () => {
    TestBed.resetTestingModule();
    localStorage.setItem(StorageKeys.accessToken, 'jwt-access');
    localStorage.setItem(StorageKeys.refreshToken, 'jwt-refresh');
    (dataSource.me as jest.Mock).mockRejectedValue(new Error('401'));
    TestBed.configureTestingModule({
      providers: [
        AuthSessionStore, LogoutUseCase, TokenStore, KeyValueStore,
        { provide: LoginUseCase, useValue: login },
        { provide: AUTH_DATA_SOURCE, useValue: dataSource },
      ],
    });
    const freshStore = TestBed.inject(AuthSessionStore);
    await Promise.resolve();
    await Promise.resolve();
    expect(freshStore.isAuthenticated()).toBe(false);
  });
```

- [ ] **Step 2: Run it to verify it fails.**

Run: `npx jest src/app/core/auth/auth-session.store.spec.ts -t "GET /me"`
Expected: FAIL — current rehydrate parses the token and never calls `dataSource.me`.

- [ ] **Step 3: Rewrite rehydrate to call `repo.me()`.**

In `src/app/core/auth/auth-session.store.ts`:

Add the import:

```ts
import { AUTH_REPOSITORY } from '@core/auth/auth.repository.port';
```

Add the injected repository (next to the other `inject(...)` fields):

```ts
  private readonly repo = inject(AUTH_REPOSITORY);
```

Replace the `rehydrate()` method with:

```ts
  private async rehydrate(): Promise<void> {
    const accessToken = await this.tokens.getAccess();
    const refreshToken = await this.tokens.getRefresh();
    if (!accessToken || !refreshToken) return;
    try {
      const principal = await this.repo.me();
      this._session.set({ tokens: { accessToken, refreshToken }, principal, mustChangePassword: false });
    } catch {
      // Token invalid/expired: authInterceptor handles the 401→refresh→retry path, or
      // the user must re-login. Leave the session null here.
    }
  }
```

`UserRole` may now be an unused import in this file — if the compiler flags it, remove `UserRole` from the `auth.types` import. (It is still used by the `mustChangePassword`/`role` computed signals via `AuthSession`, so only remove it if the build complains.)

- [ ] **Step 4: Run the store spec to verify green.**

Run: `npx jest src/app/core/auth/auth-session.store.spec.ts`
Expected: PASS.

- [ ] **Step 5: Commit.**

```bash
git add src/app/core/auth/auth-session.store.ts src/app/core/auth/auth-session.store.spec.ts
git commit -m "feat(auth): rehydrate session via GET /api/auth/me (FE-AUTH-ME)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: Change-password domain (use-case + store method)

Add `ChangePasswordUseCase` (saves the rotated tokens and returns the fresh session) and an `AuthSessionStore.changePassword()` method that updates `_session` on success.

**Files:**
- Create: `src/app/core/auth/usecases/change-password.use-case.ts`
- Modify: `src/app/core/auth/auth-session.store.ts`
- Test (create): `src/app/core/auth/usecases/change-password.use-case.spec.ts`
- Test (modify): `src/app/core/auth/auth-session.store.spec.ts`

**Interfaces:**
- Consumes: `AUTH_REPOSITORY.changePassword(current, new): Promise<AuthSession>` (Task 2), `TokenStore.save`.
- Produces:
  - `ChangePasswordUseCase.run({ currentPassword, newPassword }): Promise<Result<AuthSession>>`
  - `AuthSessionStore.changePassword(currentPassword, newPassword): Promise<Result<AuthSession>>`

- [ ] **Step 1: Write the failing use-case spec.**

Create `src/app/core/auth/usecases/change-password.use-case.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { ChangePasswordUseCase } from '@core/auth/usecases/change-password.use-case';
import { AUTH_REPOSITORY, IAuthRepository } from '@core/auth/auth.repository.port';
import { TokenStore } from '@core/auth/token-store';
import { KeyValueStore } from '@core/datasource/keyvalue/key-value.store';
import { AppError } from '@core/domain/errors/app-error';

const rotated = { tokens: { accessToken: 'new-a', refreshToken: 'new-r' }, principal: { role: 'moqawel' as const, userId: 'USR-MOQAWEL' }, mustChangePassword: false };

function makeRepo(overrides: Partial<IAuthRepository> = {}): IAuthRepository {
  return {
    login: jest.fn(), refresh: jest.fn(), logout: jest.fn(), me: jest.fn(),
    changePassword: jest.fn().mockResolvedValue(rotated),
    ...overrides,
  } as unknown as IAuthRepository;
}

describe('ChangePasswordUseCase', () => {
  let tokens: TokenStore;
  const build = (repo: IAuthRepository) => {
    TestBed.resetTestingModule();
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [ChangePasswordUseCase, TokenStore, KeyValueStore, { provide: AUTH_REPOSITORY, useValue: repo }],
    });
    tokens = TestBed.inject(TokenStore);
    return TestBed.inject(ChangePasswordUseCase);
  };

  it('saves the rotated tokens and returns the fresh session', async () => {
    const uc = build(makeRepo());
    const r = await uc.run({ currentPassword: 'old', newPassword: 'new12345' });
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.data.mustChangePassword).toBe(false);
    expect(await tokens.getAccess()).toBe('new-a');
  });

  it('returns fail(auth) when the current password is wrong', async () => {
    const uc = build(makeRepo({ changePassword: jest.fn().mockRejectedValue(new AppError('Current password is incorrect', 'auth', 400)) }));
    const r = await uc.run({ currentPassword: 'wrong', newPassword: 'new12345' });
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('auth');
  });
});
```

- [ ] **Step 2: Run it to verify it fails.**

Run: `npx jest src/app/core/auth/usecases/change-password.use-case.spec.ts`
Expected: FAIL — `ChangePasswordUseCase` does not exist.

- [ ] **Step 3: Implement the use-case.**

Create `src/app/core/auth/usecases/change-password.use-case.ts`:

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AUTH_REPOSITORY } from '@core/auth/auth.repository.port';
import { TokenStore } from '@core/auth/token-store';
import { AuthSession } from '@core/auth/auth.types';

export interface ChangePasswordInput { currentPassword: string; newPassword: string; }

@Injectable({ providedIn: 'root' })
export class ChangePasswordUseCase extends UseCase<ChangePasswordInput, AuthSession> {
  private readonly repo = inject(AUTH_REPOSITORY);
  private readonly tokens = inject(TokenStore);
  constructor() { super('ChangePassword'); }
  protected async execute({ currentPassword, newPassword }: ChangePasswordInput): Promise<AuthSession> {
    const session = await this.repo.changePassword(currentPassword, newPassword);
    await this.tokens.save(session.tokens); // backend rotates tokens on change (AD-007)
    return session;
  }
}
```

- [ ] **Step 4: Run the use-case spec to verify green.**

Run: `npx jest src/app/core/auth/usecases/change-password.use-case.spec.ts`
Expected: PASS.

- [ ] **Step 5: Write the failing store test for `changePassword()`.**

In `src/app/core/auth/auth-session.store.spec.ts` add these imports:

```ts
import { ChangePasswordUseCase } from '@core/auth/usecases/change-password.use-case';
```

Add `ChangePasswordUseCase` to the `beforeEach` provider array (real use-case; it resolves through the fake `AUTH_DATA_SOURCE`). Then add this test inside the `describe`:

```ts
  it('changePassword updates the session with the rotated result', async () => {
    (dataSource.changePassword as jest.Mock).mockResolvedValue({
      tokens: { accessToken: 'na', refreshToken: 'nr' },
      principal: { role: 'admin', userId: 'USR-ADMIN' },
      mustChangePassword: false,
    });
    const r = await store.changePassword('old', 'new12345');
    expect(r.ok).toBe(true);
    expect(store.isAuthenticated()).toBe(true);
    expect(store.mustChangePassword()).toBe(false);
    expect(store.session()?.tokens.accessToken).toBe('na');
  });
```

- [ ] **Step 6: Run it to verify it fails.**

Run: `npx jest src/app/core/auth/auth-session.store.spec.ts -t "changePassword updates"`
Expected: FAIL — `store.changePassword` is not a function.

- [ ] **Step 7: Add `changePassword()` to the store.**

In `src/app/core/auth/auth-session.store.ts`:

Add imports:

```ts
import { ChangePasswordUseCase } from '@core/auth/usecases/change-password.use-case';
import { Result } from '@core/domain/result/result';
```

(`Result` is already imported — keep a single import.) Add the injected use-case:

```ts
  private readonly changePasswordUseCase = inject(ChangePasswordUseCase);
```

Add the method (after `signOut`):

```ts
  async changePassword(currentPassword: string, newPassword: string): Promise<Result<AuthSession>> {
    const r = await this.changePasswordUseCase.run({ currentPassword, newPassword });
    if (r.ok) this._session.set(r.data);
    return r;
  }
```

- [ ] **Step 8: Run the store spec to verify green.**

Run: `npx jest src/app/core/auth/auth-session.store.spec.ts`
Expected: PASS.

- [ ] **Step 9: Commit.**

```bash
git add src/app/core/auth/usecases/change-password.use-case.ts src/app/core/auth/usecases/change-password.use-case.spec.ts src/app/core/auth/auth-session.store.ts src/app/core/auth/auth-session.store.spec.ts
git commit -m "feat(auth): ChangePasswordUseCase + store.changePassword (AD-007)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: Change-password page, view-model, and route (FE-AUTH-PWD)

Build the presentation slice: a route-scoped `ChangePasswordViewModel` (with a local confirm-match guard), a `ChangePasswordPage` styled like the login page, the `/change-password` route under `authGuard`, and the feature-barrel exports.

**Files:**
- Create: `src/app/features/auth/presentation/viewmodels/change-password.viewmodel.ts`
- Create: `src/app/features/auth/presentation/pages/change-password.page.ts`
- Modify: `src/app/app.routes.ts`
- Modify: `src/app/features/auth/index.ts`
- Test (create): `src/app/features/auth/presentation/viewmodels/change-password.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `AuthSessionStore.changePassword(current, new): Promise<Result<AuthSession>>` (Task 5), `Router`.
- Produces: `ChangePasswordViewModel` (signals `currentPassword`, `newPassword`, `confirmPassword`, `loading`, `error`; `submit()`); `ChangePasswordPage`; route `/change-password`.

- [ ] **Step 1: Write the failing view-model spec.**

Create `src/app/features/auth/presentation/viewmodels/change-password.viewmodel.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { ChangePasswordViewModel } from './change-password.viewmodel';
import { AuthSessionStore } from '@core/auth/auth-session.store';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';
import { AuthSession } from '@core/auth/auth.types';

const session: AuthSession = { tokens: { accessToken: 'a', refreshToken: 'r' }, principal: { role: 'moqawel', userId: 'USR-MOQAWEL' }, mustChangePassword: false };

describe('ChangePasswordViewModel', () => {
  const auth = { changePassword: jest.fn() } as unknown as AuthSessionStore;
  const router = { navigate: jest.fn() } as unknown as Router;
  let vm: ChangePasswordViewModel;
  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [ChangePasswordViewModel, { provide: AuthSessionStore, useValue: auth }, { provide: Router, useValue: router }],
    });
    vm = TestBed.inject(ChangePasswordViewModel);
  });

  it('blocks submit and shows an error when confirmation does not match', async () => {
    vm.currentPassword.set('old'); vm.newPassword.set('new12345'); vm.confirmPassword.set('different');
    await vm.submit();
    expect(auth.changePassword).not.toHaveBeenCalled();
    expect(vm.error()).toBe('New password and confirmation do not match.');
  });

  it('navigates to /account on success', async () => {
    (auth.changePassword as jest.Mock).mockResolvedValue(ok(session));
    vm.currentPassword.set('old'); vm.newPassword.set('new12345'); vm.confirmPassword.set('new12345');
    await vm.submit();
    expect(auth.changePassword).toHaveBeenCalledWith('old', 'new12345');
    expect(router.navigate).toHaveBeenCalledWith(['/account']);
    expect(vm.error()).toBeNull();
  });

  it('surfaces the error message on failure', async () => {
    (auth.changePassword as jest.Mock).mockResolvedValue(fail(new AppError('Current password is incorrect', 'auth', 400)));
    vm.currentPassword.set('wrong'); vm.newPassword.set('new12345'); vm.confirmPassword.set('new12345');
    await vm.submit();
    expect(vm.error()).toBe('Current password is incorrect');
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run it to verify it fails.**

Run: `npx jest src/app/features/auth/presentation/viewmodels/change-password.viewmodel.spec.ts`
Expected: FAIL — `ChangePasswordViewModel` does not exist.

- [ ] **Step 3: Implement the view-model.**

Create `src/app/features/auth/presentation/viewmodels/change-password.viewmodel.ts`:

```ts
// ChangePasswordViewModel: presentation facade for the change-password form. Guards
// the confirm-match locally, then drives AuthSessionStore.changePassword().
import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthSessionStore } from '@core/auth/auth-session.store';
import { createLogger } from '@core/logging/logger';

@Injectable()
export class ChangePasswordViewModel {
  private readonly auth = inject(AuthSessionStore);
  private readonly router = inject(Router);
  private readonly log = createLogger('viewmodel', 'ChangePasswordViewModel');

  readonly currentPassword = signal('');
  readonly newPassword = signal('');
  readonly confirmPassword = signal('');
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  async submit(): Promise<void> {
    this.log.debug('submit()');
    this.error.set(null);
    if (this.newPassword() !== this.confirmPassword()) {
      this.error.set('New password and confirmation do not match.');
      return;
    }
    this.loading.set(true);
    const r = await this.auth.changePassword(this.currentPassword(), this.newPassword());
    this.loading.set(false);
    if (r.ok) {
      void this.router.navigate(['/account']);
    } else {
      this.error.set(r.error.message);
    }
  }
}
```

- [ ] **Step 4: Run the view-model spec to verify green.**

Run: `npx jest src/app/features/auth/presentation/viewmodels/change-password.viewmodel.spec.ts`
Expected: PASS.

- [ ] **Step 5: Implement the page.**

Create `src/app/features/auth/presentation/pages/change-password.page.ts`:

```ts
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChangePasswordViewModel } from '../viewmodels/change-password.viewmodel';

@Component({
  selector: 'app-change-password-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section style="max-width: 360px; margin: 48px auto; display: grid; gap: var(--space-md);">
      <h1>Change password</h1>
      <input [ngModel]="vm.currentPassword()" (ngModelChange)="vm.currentPassword.set($event)" type="password" placeholder="Current password" autocomplete="current-password" />
      <input [ngModel]="vm.newPassword()" (ngModelChange)="vm.newPassword.set($event)" type="password" placeholder="New password" autocomplete="new-password" />
      <input [ngModel]="vm.confirmPassword()" (ngModelChange)="vm.confirmPassword.set($event)" type="password" placeholder="Confirm new password" autocomplete="new-password" />
      @if (vm.error(); as e) { <p style="color: var(--color-error);">{{ e }}</p> }
      <button (click)="vm.submit()" [disabled]="vm.loading()">
        {{ vm.loading() ? 'Saving…' : 'Change password' }}
      </button>
    </section>
  `,
})
export class ChangePasswordPage {
  protected readonly vm = inject(ChangePasswordViewModel);
}
```

- [ ] **Step 6: Export from the feature barrel.**

Replace `src/app/features/auth/index.ts` with:

```ts
export { LoginPage } from './presentation/pages/login.page';
export { AccountPage } from './presentation/pages/account.page';
export { AdminPage } from './presentation/pages/admin.page';
export { ChangePasswordPage } from './presentation/pages/change-password.page';
export { LoginViewModel } from './presentation/viewmodels/login.viewmodel';
export { ChangePasswordViewModel } from './presentation/viewmodels/change-password.viewmodel';
```

- [ ] **Step 7: Add the route.**

In `src/app/app.routes.ts`, add the `ChangePasswordViewModel` to the existing `@features/auth` import:

```ts
import { LoginViewModel, ChangePasswordViewModel } from '@features/auth';
```

Add this route object immediately after the `account` route (before the `admin` route):

```ts
  {
    path: 'change-password',
    canActivate: [authGuard],
    loadComponent: () => import('@features/auth').then((m) => m.ChangePasswordPage),
    providers: [ChangePasswordViewModel],
  },
```

- [ ] **Step 8: Build to verify the route + lazy component compile.**

Run: `npm run build`
Expected: build succeeds (the new lazy chunk for `ChangePasswordPage` appears).

- [ ] **Step 9: Commit.**

```bash
git add src/app/features/auth/presentation/viewmodels/change-password.viewmodel.ts src/app/features/auth/presentation/viewmodels/change-password.viewmodel.spec.ts src/app/features/auth/presentation/pages/change-password.page.ts src/app/features/auth/index.ts src/app/app.routes.ts
git commit -m "feat(auth): change-password page, view-model, and /change-password route (FE-AUTH-PWD)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: Forced first-login change redirect (AD-007)

After a successful login, route to `/change-password` when the session carries `mustChangePassword`, otherwise to `/account`.

**Files:**
- Modify: `src/app/features/auth/presentation/viewmodels/login.viewmodel.ts`
- Test (modify): `src/app/features/auth/presentation/viewmodels/login.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `AuthSessionStore.signIn(): Promise<Result<AuthSession>>` (`r.data.mustChangePassword`).
- Produces: login redirect branches on `mustChangePassword`.

- [ ] **Step 1: Add the failing forced-change test.**

In `src/app/features/auth/presentation/viewmodels/login.viewmodel.spec.ts` add a second session literal and a test. After the existing `session` constant add:

```ts
const forcedSession: AuthSession = { tokens: { accessToken: 'a', refreshToken: 'r' }, principal: { role: 'moqawel', userId: 'USR-MOQAWEL' }, mustChangePassword: true };
```

Add this test inside the `describe`:

```ts
  it('redirects to /change-password when mustChangePassword is set', async () => {
    (auth.signIn as jest.Mock).mockResolvedValue(ok(forcedSession));
    vm.email.set('moqawel@example.com'); vm.password.set('moqawel123');
    await vm.submit();
    expect(router.navigate).toHaveBeenCalledWith(['/change-password']);
  });
```

- [ ] **Step 2: Run it to verify it fails.**

Run: `npx jest src/app/features/auth/presentation/viewmodels/login.viewmodel.spec.ts -t "mustChangePassword"`
Expected: FAIL — current view-model always navigates to `/account`.

- [ ] **Step 3: Branch on `mustChangePassword`.**

In `src/app/features/auth/presentation/viewmodels/login.viewmodel.ts` replace the success branch of `submit()`:

```ts
    if (r.ok) {
      void this.router.navigate([r.data.mustChangePassword ? '/change-password' : '/account']);
    } else {
      this.error.set(r.error.message);
    }
```

- [ ] **Step 4: Run the login view-model spec to verify green.**

Run: `npx jest src/app/features/auth/presentation/viewmodels/login.viewmodel.spec.ts`
Expected: PASS (both the `/account` and `/change-password` branches).

- [ ] **Step 5: Commit.**

```bash
git add src/app/features/auth/presentation/viewmodels/login.viewmodel.ts src/app/features/auth/presentation/viewmodels/login.viewmodel.spec.ts
git commit -m "feat(auth): redirect to /change-password on forced first-login change (AD-007)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 8: Wire-up polish + going-live docs

Add the account "Change password" link, refresh the login demo hint for the 4 roles, and document the one-line go-live swap in `README.md` + `env.ts`. No new tests; ends with the full-suite + build verification gate.

**Files:**
- Modify: `src/app/features/auth/presentation/pages/account.page.ts`
- Modify: `src/app/features/auth/presentation/pages/login.page.ts`
- Modify: `README.md`
- Modify: `src/app/core/config/env.ts`

- [ ] **Step 1: Add a "Change password" link to the account page.**

In `src/app/features/auth/presentation/pages/account.page.ts` add a link inside the `@if (auth.principal(); as p)` block, after the admin link line:

```html
        <a routerLink="/change-password">Change password</a>
```

(The component already imports `RouterLink`, so no import change is needed.)

- [ ] **Step 2: Update the login demo hint for the 4 roles.**

In `src/app/features/auth/presentation/pages/login.page.ts` replace the demo-users `<p>` with:

```html
      <p style="color: var(--color-muted);">
        Demo users: admin / manager / moqawel / worker &#64;example.com — password
        <code>&lt;role&gt;123</code> (e.g. admin123). The moqawel user is forced to change
        its password on first sign-in.
      </p>
```

- [ ] **Step 3: Document the go-live swap in `env.ts`.**

In `src/app/core/config/env.ts` replace the dev `baseUrl` comment line so it points at the backend without changing the value:

```ts
    baseUrl: isDev
      ? 'https://dummyjson.com' // TODO(dev): posts demo API. For live auth, set to the Identity backend, e.g. 'https://localhost:7080'
      : 'https://api.example.com', // TODO(prod): production API
```

- [ ] **Step 4: Update the README "Going live" section.**

In `README.md`, in the "Going live — two swaps" section, replace the auth swap + base-URL blocks so they reflect the real Identity backend. Replace the block under **2. Auth data source** and **3. Set the base URL** with:

````markdown
**2. Auth data source** (`core/auth/auth.providers.ts`):
```ts
// replace:
{ provide: AUTH_DATA_SOURCE, useClass: MockAuthDataSource }
// with:
{ provide: AUTH_DATA_SOURCE, useClass: HttpAuthDataSource }
```

`HttpAuthDataSource` targets the Identity backend at `/api/auth/{login,refresh,logout,me,change-password}`.

**3. Set the base URL** (`core/config/env.ts`):
```ts
baseUrl: isDev ? 'https://localhost:7080' : 'https://api.example.com'
```

The backend already allows CORS from `http://localhost:4200`. Seed accounts are
`{role}@kheprx.local` (roles: `admin`, `manager`, `moqawel`, `worker`) with password
`ChangeMe123!`; every seed account is forced to change its password on first sign-in.
````

Also update the offline demo-users table in the README to the four roles:

```markdown
| Email | Password | Role |
|---|---|---|
| `admin@example.com` | `admin123` | `admin` |
| `manager@example.com` | `manager123` | `manager` |
| `moqawel@example.com` | `moqawel123` | `moqawel` (forced change on first login) |
| `worker@example.com` | `worker123` | `worker` |
```

- [ ] **Step 5: Run the full suite.**

Run: `npx jest`
Expected: PASS — all specs green.

- [ ] **Step 6: Production build.**

Run: `npm run build`
Expected: build succeeds, no type errors.

- [ ] **Step 7: Commit.**

```bash
git add src/app/features/auth/presentation/pages/account.page.ts src/app/features/auth/presentation/pages/login.page.ts src/app/core/config/env.ts README.md
git commit -m "docs(auth): account change-password link, 4-role demo hint, go-live docs

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Final Verification

After Task 8:
- [ ] `npx jest` — entire suite green (existing specs + new: http-auth, logout use-case, change-password use-case, change-password view-model, extended mock/store/login specs).
- [ ] `npm run build` — clean production build.
- [ ] Manual smoke (optional, offline on Mock): sign in as `moqawel@example.com` / `moqawel123` → redirected to `/change-password` → submit matching new passwords → land on `/account` → "Sign out" → `/login`.
- [ ] Confirm the default provider is still `MockAuthDataSource` (`app.config.ts` / `auth.providers.ts` unchanged) and `env.api.baseUrl` default is unchanged.

## Notes on scope (do not implement)

- `GET/POST /api/users`, users repository, `ListUsersUseCase`, `CreateUserUseCase`, `UsersViewModel`, Admin→Users UI — Phase 2 (`FE-AUTH-USR`). `AdminPage` remains the static stub.
- No change to `HttpApiDataSource`/posts wiring or the shared `env.api.baseUrl` default.
