# Screen-based folders for `dto/` · `usecases/` · `model/` — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganize each feature's `data/dto/`, `domain/usecases/`, and `domain/model/` into screen-based subfolders (plus a `shared/` bucket), splitting the monolithic DTO files into small per-operation files.

**Architecture:** Pure structural refactor of an Angular 20 app. Split the DTO/model monoliths into new files, move use-cases into screen/`shared` folders, and repoint every `@features/...` import that referenced a moved/split symbol. No runtime, DI, routing, or test-assertion changes. Safety net: the existing Jest suite + `ng build`; each task ends green.

**Tech Stack:** Angular 20 (standalone, signals), TypeScript 5.9 (strict), Jest 30 (`jest-preset-angular`). Path aliases `@core/*`, `@features/*`. Use-cases are `@Injectable({providedIn:'root'})` (self-registered).

**Spec:** `docs/superpowers/specs/2026-07-03-screen-based-domain-data-folders-design.md`

## Global Constraints

- **Working directory = `frontend/`.** Git paths use forward slashes. The shell resets cwd to repo root between calls — prefix each command with `cd frontend && …` or use `git -C frontend …`.
- **COMMIT GATE:** do NOT run `git commit` until the user authorizes. Staging (`git add`/`git mv`/`git rm`) is allowed. Each task's commit step is GATED.
- **Keep-green:** each task ends with `npm test` and `npm run build` passing. **117 tests stay passing** (no assertion changes). Suite count: **33 → 34 after Task 1** (the auth DTO spec splits into two files); **stays 34 through Tasks 2–3**.
- **Do NOT change:** `auth.providers.ts` / `users.providers.ts` (they bind only the repository token; use-cases self-register), the feature `index.ts` barrels, `app.routes.ts`, the guards, the stores' public APIs, and all tsconfig/jest config. None of these reference the moved symbols by a path that changes.
- **Import rule:** repoint every `@features/...` import of a moved/split symbol to its new module (master table below). Cross-module imports via `@core/...` are unaffected. No relative imports are introduced.
- **File granularity:** DTO response types + their validator live together in one file; each request DTO gets its own file. Use-cases stay one file each. The auth model splits by screen; the user model moves whole.

## Master symbol → new module map

**auth** (old `@features/auth/data/dto/auth.dto` and `@features/auth/domain/model/auth` are DELETED):

| Symbol | New module |
|---|---|
| `LoginDtoRq` | `@features/auth/data/dto/login/login.dto` |
| `ChangePasswordDtoRq` | `@features/auth/data/dto/change-password/change-password.dto` |
| `RefreshDtoRq` | `@features/auth/data/dto/shared/refresh.dto` |
| `SessionDtoRs`, `SessionItemDtoRs`, `isSessionDtoRsValid` | `@features/auth/data/dto/shared/session.dto` |
| `CurrentUserDtoRs`, `CurrentUserItemDtoRs`, `isCurrentUserDtoRsValid` | `@features/auth/data/dto/shared/current-user.dto` |
| `LoginInput` | `@features/auth/domain/model/login/login` |
| `ChangePasswordInput` | `@features/auth/domain/model/change-password/change-password` |
| `AuthSession`, `AuthTokens`, `AuthPrincipal`, `toAuthSession`, `toAuthTokens`, `toAuthPrincipal` | `@features/auth/domain/model/shared/auth` |
| `LoginUseCase` | `@features/auth/domain/usecases/login/login.use-case` |
| `ChangePasswordUseCase` | `@features/auth/domain/usecases/change-password/change-password.use-case` |
| `LogoutUseCase` | `@features/auth/domain/usecases/account/logout.use-case` |
| `LoadCurrentUserUseCase` | `@features/auth/domain/usecases/shared/load-current-user.use-case` |
| `RefreshTokenUseCase` | `@features/auth/domain/usecases/shared/refresh-token.use-case` |

**user-management** (old `@features/user-management/data/dto/user.dto` and `@features/user-management/domain/model/user` are DELETED):

| Symbol | New module |
|---|---|
| `UserDtoRs`, `UsersDtoRs`, `UserItemDtoRs`, `isUserDtoRsValid` | `@features/user-management/data/dto/user-management/user.dto` |
| `CreateUserDtoRq` | `@features/user-management/data/dto/user-management/create-user.dto` |
| `UpdateUserDtoRq` | `@features/user-management/data/dto/user-management/update-user.dto` |
| `SetUserStatusDtoRq` | `@features/user-management/data/dto/user-management/set-user-status.dto` |
| `User`, `UserStatus`, `CreateUserInput`, `UpdateUserInput`, `toUser` | `@features/user-management/domain/model/user-management/user` |
| `ListUsersUseCase` | `@features/user-management/domain/usecases/user-management/list-users.use-case` |
| `CreateUserUseCase` | `@features/user-management/domain/usecases/user-management/create-user.use-case` |
| `UpdateUserUseCase` | `@features/user-management/domain/usecases/user-management/update-user.use-case` |
| `SetUserStatusUseCase` | `@features/user-management/domain/usecases/user-management/set-user-status.use-case` |

---

### Task 1: Restructure the `auth` domain/data layer

**Files:**
- Create (dto): `data/dto/login/login.dto.ts`, `data/dto/change-password/change-password.dto.ts`, `data/dto/shared/session.dto.ts`, `data/dto/shared/current-user.dto.ts`, `data/dto/shared/refresh.dto.ts`. Delete `data/dto/auth.dto.ts`.
- Create (model): `domain/model/login/login.ts`, `domain/model/change-password/change-password.ts`, `domain/model/shared/auth.ts`. Delete `domain/model/auth.ts`.
- Move (use-cases): the 5 `*.use-case.ts` into `usecases/{login,change-password,account,shared}/`.
- Modify (consumers): `data/token-store.ts`, `presentation/auth-session.store.ts`, `data/auth.interceptor.ts`, `domain/repositories/auth.repository.ts`, `data/repositories/auth.repository.impl.ts`.
- Tests: move specs into mirrored `testing/` paths; split `testing/data/dto/auth.dto.spec.ts` into two.

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces: the `@features/auth/...` module paths in the master table. Public barrel (`@features/auth`) and `AUTH_REPOSITORY` DI unchanged.

- [ ] **Step 1: Preflight — green baseline**

Run: `cd frontend && npm test`
Expected: PASS, **Test Suites: 33 passed**, **Tests: 117 passed**.
Run: `cd frontend && npm run build`
Expected: build succeeds.

- [ ] **Step 2: Create the 5 split DTO files, delete the monolith**

Create `src/app/features/auth/data/dto/shared/session.dto.ts`:
```ts
// session.dto.ts — session response DTOs (API_FLOW convention).
// SessionDtoRs fields are raw/untrusted transport — isSessionDtoRsValid guards
// them before the use cases map to domain models.
import { BaseResponseRs } from '@core/network/api/base-response-rs';
import { ROLE_LABELS } from '@core/domain/roles';

export interface SessionDtoRs {
  accessToken: string;
  refreshToken: string;
  role: string;
  userId: string;
  mustChangePassword: boolean;
}

export interface SessionItemDtoRs extends BaseResponseRs<SessionDtoRs> {}

export function isSessionDtoRsValid(dto: SessionDtoRs): boolean {
  return (
    typeof dto.accessToken === 'string' && dto.accessToken.length > 0 &&
    typeof dto.refreshToken === 'string' && dto.refreshToken.length > 0 &&
    typeof dto.userId === 'string' && dto.userId.length > 0 &&
    typeof dto.role === 'string' &&
    Object.prototype.hasOwnProperty.call(ROLE_LABELS, dto.role) &&
    typeof dto.mustChangePassword === 'boolean'
  );
}
```

Create `src/app/features/auth/data/dto/shared/current-user.dto.ts`:
```ts
// current-user.dto.ts — current-user response DTOs (API_FLOW convention).
// CurrentUserDtoRs fields are raw/untrusted transport — isCurrentUserDtoRsValid
// guards them before the use case maps to the domain model.
import { BaseResponseRs } from '@core/network/api/base-response-rs';
import { ROLE_LABELS } from '@core/domain/roles';

export interface CurrentUserDtoRs {
  userId: string;
  email: string;
  fullName: string;
  role: string;
}

export interface CurrentUserItemDtoRs extends BaseResponseRs<CurrentUserDtoRs> {}

export function isCurrentUserDtoRsValid(dto: CurrentUserDtoRs): boolean {
  return (
    typeof dto.userId === 'string' && dto.userId.length > 0 &&
    typeof dto.email === 'string' &&
    typeof dto.fullName === 'string' &&
    typeof dto.role === 'string' &&
    Object.prototype.hasOwnProperty.call(ROLE_LABELS, dto.role)
  );
}
```

Create `src/app/features/auth/data/dto/login/login.dto.ts`:
```ts
// login.dto.ts — login request DTO (API_FLOW convention).
export interface LoginDtoRq {
  email: string;
  password: string;
}
```

Create `src/app/features/auth/data/dto/change-password/change-password.dto.ts`:
```ts
// change-password.dto.ts — change-password request DTO (API_FLOW convention).
export interface ChangePasswordDtoRq {
  currentPassword: string;
  newPassword: string;
}
```

Create `src/app/features/auth/data/dto/shared/refresh.dto.ts`:
```ts
// refresh.dto.ts — token-refresh request DTO (API_FLOW convention).
export interface RefreshDtoRq {
  refreshToken: string;
}
```

Delete the monolith: `git -C frontend rm src/app/features/auth/data/dto/auth.dto.ts`

- [ ] **Step 3: Create the 3 split model files, delete the monolith**

Create `src/app/features/auth/domain/model/shared/auth.ts`:
```ts
// auth.ts — auth domain models (session vocabulary) + DTO→model mappers.
import { UserRole } from '@core/domain/roles';
import { SessionDtoRs } from '@features/auth/data/dto/shared/session.dto';
import { CurrentUserDtoRs } from '@features/auth/data/dto/shared/current-user.dto';

export interface AuthTokens { accessToken: string; refreshToken: string; }
export interface AuthPrincipal { role: UserRole; userId: string; }
// mustChangePassword is a session-lifecycle fact (AD-007 forced first-login change),
// surfaced by login/refresh/change-password; not part of the identity principal.
export interface AuthSession { tokens: AuthTokens; principal: AuthPrincipal; mustChangePassword: boolean; }

export function toAuthSession(dto: SessionDtoRs): AuthSession {
  return {
    tokens: { accessToken: dto.accessToken, refreshToken: dto.refreshToken },
    principal: { role: dto.role as UserRole, userId: dto.userId },
    mustChangePassword: dto.mustChangePassword,
  };
}

export function toAuthTokens(dto: SessionDtoRs): AuthTokens {
  return { accessToken: dto.accessToken, refreshToken: dto.refreshToken };
}

export function toAuthPrincipal(dto: CurrentUserDtoRs): AuthPrincipal {
  return { role: dto.role as UserRole, userId: dto.userId };
}
```

Create `src/app/features/auth/domain/model/login/login.ts`:
```ts
// login.ts — login use-case input model.
export interface LoginInput { email: string; password: string; }
```

Create `src/app/features/auth/domain/model/change-password/change-password.ts`:
```ts
// change-password.ts — change-password use-case input model.
export interface ChangePasswordInput { currentPassword: string; newPassword: string; }
```

Delete the monolith: `git -C frontend rm src/app/features/auth/domain/model/auth.ts`

- [ ] **Step 4: Move the 5 use-cases into folders + repoint their model/dto imports**

```bash
git -C frontend mv src/app/features/auth/domain/usecases/login.use-case.ts             src/app/features/auth/domain/usecases/login/login.use-case.ts
git -C frontend mv src/app/features/auth/domain/usecases/change-password.use-case.ts    src/app/features/auth/domain/usecases/change-password/change-password.use-case.ts
git -C frontend mv src/app/features/auth/domain/usecases/logout.use-case.ts             src/app/features/auth/domain/usecases/account/logout.use-case.ts
git -C frontend mv src/app/features/auth/domain/usecases/load-current-user.use-case.ts  src/app/features/auth/domain/usecases/shared/load-current-user.use-case.ts
git -C frontend mv src/app/features/auth/domain/usecases/refresh-token.use-case.ts      src/app/features/auth/domain/usecases/shared/refresh-token.use-case.ts
```

In `usecases/login/login.use-case.ts` replace the two import lines:
```ts
import { AuthSession, LoginInput, toAuthSession } from '@features/auth/domain/model/auth';
import { isSessionDtoRsValid } from '@features/auth/data/dto/auth.dto';
```
with:
```ts
import { AuthSession, toAuthSession } from '@features/auth/domain/model/shared/auth';
import { LoginInput } from '@features/auth/domain/model/login/login';
import { isSessionDtoRsValid } from '@features/auth/data/dto/shared/session.dto';
```

In `usecases/change-password/change-password.use-case.ts` replace:
```ts
import { AuthSession, ChangePasswordInput, toAuthSession } from '@features/auth/domain/model/auth';
import { isSessionDtoRsValid } from '@features/auth/data/dto/auth.dto';
```
with:
```ts
import { AuthSession, toAuthSession } from '@features/auth/domain/model/shared/auth';
import { ChangePasswordInput } from '@features/auth/domain/model/change-password/change-password';
import { isSessionDtoRsValid } from '@features/auth/data/dto/shared/session.dto';
```

In `usecases/shared/refresh-token.use-case.ts` replace:
```ts
import { AuthTokens, toAuthTokens } from '@features/auth/domain/model/auth';
import { isSessionDtoRsValid } from '@features/auth/data/dto/auth.dto';
```
with:
```ts
import { AuthTokens, toAuthTokens } from '@features/auth/domain/model/shared/auth';
import { isSessionDtoRsValid } from '@features/auth/data/dto/shared/session.dto';
```

In `usecases/shared/load-current-user.use-case.ts` replace:
```ts
import { AuthPrincipal, toAuthPrincipal } from '@features/auth/domain/model/auth';
import { isCurrentUserDtoRsValid } from '@features/auth/data/dto/auth.dto';
```
with:
```ts
import { AuthPrincipal, toAuthPrincipal } from '@features/auth/domain/model/shared/auth';
import { isCurrentUserDtoRsValid } from '@features/auth/data/dto/shared/current-user.dto';
```

`usecases/account/logout.use-case.ts` imports no model/dto symbols — no edit needed (it moved with the `git mv` above).

- [ ] **Step 5: Repoint the non-use-case consumers**

`data/token-store.ts` — change:
```ts
import { AuthTokens } from '@features/auth/domain/model/auth';
```
to:
```ts
import { AuthTokens } from '@features/auth/domain/model/shared/auth';
```

`presentation/auth-session.store.ts` — change the 4 use-case imports and the model import (lines 6–11) to:
```ts
import { LoginUseCase } from '@features/auth/domain/usecases/login/login.use-case';
import { LogoutUseCase } from '@features/auth/domain/usecases/account/logout.use-case';
import { ChangePasswordUseCase } from '@features/auth/domain/usecases/change-password/change-password.use-case';
import { LoadCurrentUserUseCase } from '@features/auth/domain/usecases/shared/load-current-user.use-case';
```
and (the `TokenStore` line between them stays unchanged) the model line:
```ts
import { AuthSession } from '@features/auth/domain/model/shared/auth';
```

`data/auth.interceptor.ts` — change:
```ts
import { RefreshTokenUseCase } from '@features/auth/domain/usecases/refresh-token.use-case';
```
to:
```ts
import { RefreshTokenUseCase } from '@features/auth/domain/usecases/shared/refresh-token.use-case';
```

`domain/repositories/auth.repository.ts` AND `data/repositories/auth.repository.impl.ts` — both have the same 5-symbol DTO import block:
```ts
import {
  SessionItemDtoRs,
  CurrentUserItemDtoRs,
  LoginDtoRq,
  RefreshDtoRq,
  ChangePasswordDtoRq,
} from '@features/auth/data/dto/auth.dto';
```
Replace it (in each file) with:
```ts
import { SessionItemDtoRs } from '@features/auth/data/dto/shared/session.dto';
import { CurrentUserItemDtoRs } from '@features/auth/data/dto/shared/current-user.dto';
import { LoginDtoRq } from '@features/auth/data/dto/login/login.dto';
import { RefreshDtoRq } from '@features/auth/data/dto/shared/refresh.dto';
import { ChangePasswordDtoRq } from '@features/auth/data/dto/change-password/change-password.dto';
```

- [ ] **Step 6: Move + repoint the auth specs; split the DTO spec**

Move and repoint (the use-case specs, model spec):
```bash
git -C frontend mv src/app/features/auth/testing/domain/usecases/login.use-case.spec.ts            src/app/features/auth/testing/domain/usecases/login/login.use-case.spec.ts
git -C frontend mv src/app/features/auth/testing/domain/usecases/change-password.use-case.spec.ts   src/app/features/auth/testing/domain/usecases/change-password/change-password.use-case.spec.ts
git -C frontend mv src/app/features/auth/testing/domain/usecases/logout.use-case.spec.ts            src/app/features/auth/testing/domain/usecases/account/logout.use-case.spec.ts
git -C frontend mv src/app/features/auth/testing/domain/usecases/load-current-user.use-case.spec.ts src/app/features/auth/testing/domain/usecases/shared/load-current-user.use-case.spec.ts
git -C frontend mv src/app/features/auth/testing/domain/usecases/refresh-token.use-case.spec.ts     src/app/features/auth/testing/domain/usecases/shared/refresh-token.use-case.spec.ts
git -C frontend mv src/app/features/auth/testing/domain/model/auth.spec.ts                          src/app/features/auth/testing/domain/model/shared/auth.spec.ts
```

Apply these import edits (change only the listed lines; leave all other imports/assertions untouched):

- `testing/domain/usecases/login/login.use-case.spec.ts`:
  - `@features/auth/domain/usecases/login.use-case` → `@features/auth/domain/usecases/login/login.use-case`
  - `@features/auth/data/dto/auth.dto` (imports `SessionDtoRs`) → `@features/auth/data/dto/shared/session.dto`
- `testing/domain/usecases/change-password/change-password.use-case.spec.ts`:
  - `@features/auth/domain/usecases/change-password.use-case` → `@features/auth/domain/usecases/change-password/change-password.use-case`
  - `@features/auth/data/dto/auth.dto` (`SessionDtoRs`) → `@features/auth/data/dto/shared/session.dto`
- `testing/domain/usecases/account/logout.use-case.spec.ts`:
  - `@features/auth/domain/usecases/logout.use-case` → `@features/auth/domain/usecases/account/logout.use-case`
- `testing/domain/usecases/shared/load-current-user.use-case.spec.ts`:
  - `@features/auth/domain/usecases/load-current-user.use-case` → `@features/auth/domain/usecases/shared/load-current-user.use-case`
  - `@features/auth/data/dto/auth.dto` (`CurrentUserDtoRs`) → `@features/auth/data/dto/shared/current-user.dto`
- `testing/domain/usecases/shared/refresh-token.use-case.spec.ts`:
  - `@features/auth/domain/usecases/refresh-token.use-case` → `@features/auth/domain/usecases/shared/refresh-token.use-case`
  - `@features/auth/data/dto/auth.dto` (`SessionDtoRs`) → `@features/auth/data/dto/shared/session.dto`
- `testing/domain/model/shared/auth.spec.ts` — replace its two import lines:
  ```ts
  import { toAuthSession, toAuthTokens, toAuthPrincipal, AuthSession, AuthTokens, AuthPrincipal } from '@features/auth/domain/model/auth';
  import { SessionDtoRs, CurrentUserDtoRs } from '@features/auth/data/dto/auth.dto';
  ```
  with:
  ```ts
  import { toAuthSession, toAuthTokens, toAuthPrincipal, AuthSession, AuthTokens, AuthPrincipal } from '@features/auth/domain/model/shared/auth';
  import { SessionDtoRs } from '@features/auth/data/dto/shared/session.dto';
  import { CurrentUserDtoRs } from '@features/auth/data/dto/shared/current-user.dto';
  ```

Specs that stay in place — edit imports only:
- `testing/presentation/auth-session.store.spec.ts`: repoint the 4 use-case imports (same new paths as Step 5's store edit) and `AuthSession` → `@features/auth/domain/model/shared/auth`.
- `testing/data/auth.interceptor.spec.ts`: `RefreshTokenUseCase` → `@features/auth/domain/usecases/shared/refresh-token.use-case`.
- `testing/presentation/pages/login/login.viewmodel.spec.ts`: `AuthSession` → `@features/auth/domain/model/shared/auth`.
- `testing/presentation/pages/change-password/change-password.viewmodel.spec.ts`: `AuthSession` → `@features/auth/domain/model/shared/auth`.
- `testing/data/repositories/auth.repository.impl.spec.ts`: replace `import { SessionDtoRs, CurrentUserDtoRs } from '@features/auth/data/dto/auth.dto';` with:
  ```ts
  import { SessionDtoRs } from '@features/auth/data/dto/shared/session.dto';
  import { CurrentUserDtoRs } from '@features/auth/data/dto/shared/current-user.dto';
  ```

Split the DTO spec — create two files and delete the original:

Create `src/app/features/auth/testing/data/dto/shared/session.dto.spec.ts`:
```ts
import { SessionDtoRs, isSessionDtoRsValid } from '@features/auth/data/dto/shared/session.dto';

const validSession: SessionDtoRs = {
  accessToken: 'a', refreshToken: 'r', role: 'admin', userId: 'USR-1', mustChangePassword: false,
};

describe('isSessionDtoRsValid', () => {
  it('accepts a well-formed session DTO', () => {
    expect(isSessionDtoRsValid(validSession)).toBe(true);
  });
  it('rejects an empty accessToken', () => {
    expect(isSessionDtoRsValid({ ...validSession, accessToken: '' })).toBe(false);
  });
  it('rejects an empty refreshToken', () => {
    expect(isSessionDtoRsValid({ ...validSession, refreshToken: '' })).toBe(false);
  });
  it('rejects an unknown role', () => {
    expect(isSessionDtoRsValid({ ...validSession, role: 'superadmin' })).toBe(false);
  });
  it('rejects a role that collides with an inherited object property', () => {
    expect(isSessionDtoRsValid({ ...validSession, role: 'toString' })).toBe(false);
  });
  it('rejects a non-boolean mustChangePassword', () => {
    expect(isSessionDtoRsValid({ ...validSession, mustChangePassword: 'no' as unknown as boolean })).toBe(false);
  });
});
```

Create `src/app/features/auth/testing/data/dto/shared/current-user.dto.spec.ts`:
```ts
import { CurrentUserDtoRs, isCurrentUserDtoRsValid } from '@features/auth/data/dto/shared/current-user.dto';

const validUser: CurrentUserDtoRs = {
  userId: 'USR-1', email: 'a@b.c', fullName: 'A', role: 'worker',
};

describe('isCurrentUserDtoRsValid', () => {
  it('accepts a well-formed current-user DTO', () => {
    expect(isCurrentUserDtoRsValid(validUser)).toBe(true);
  });
  it('rejects an empty userId', () => {
    expect(isCurrentUserDtoRsValid({ ...validUser, userId: '' })).toBe(false);
  });
  it('rejects an unknown role', () => {
    expect(isCurrentUserDtoRsValid({ ...validUser, role: 'superadmin' })).toBe(false);
  });
  it('rejects a role that collides with an inherited object property', () => {
    expect(isCurrentUserDtoRsValid({ ...validUser, role: 'toString' })).toBe(false);
  });
});
```

Delete the original: `git -C frontend rm src/app/features/auth/testing/data/dto/auth.dto.spec.ts`

- [ ] **Step 7: Verify green**

Run: `cd frontend && npm test`
Expected: **Tests: 117 passed**, **Test Suites: 34 passed** (the DTO spec is now two files).
Run: `cd frontend && npm run build`
Expected: build succeeds.
If a test/build fails, grep the auth tree for any lingering `data/dto/auth.dto` or `domain/model/auth'` reference and repoint it.

- [ ] **Step 8: Commit (GATED — do not run until the user authorizes)**

```bash
git -C frontend add -A -- src/app/features/auth
git commit -m "refactor(auth): screen-based dto/usecases/model folders + split DTOs"
```

---

### Task 2: Restructure the `user-management` domain/data layer

Single screen → everything lands under a `user-management/` subfolder; no `shared/`. The DTO monolith splits; the model moves whole.

**Files:**
- Create (dto): `data/dto/user-management/{user,create-user,update-user,set-user-status}.dto.ts`. Delete `data/dto/user.dto.ts`.
- Move (model): `domain/model/user.ts` → `domain/model/user-management/user.ts` (+ repoint its DTO import).
- Move (use-cases): the 4 `*.use-case.ts` → `usecases/user-management/`.
- Modify (consumers): `domain/repositories/users.repository.ts`, `data/repositories/users.repository.impl.ts`, `presentation/pages/user-management/users.viewmodel.ts`.
- Tests: move specs into mirrored `testing/user-management/` paths.

**Interfaces:**
- Consumes: nothing from Task 1 (independent feature).
- Produces: the `@features/user-management/...` module paths in the master table. Barrel + `USERS_REPOSITORY` DI unchanged.

- [ ] **Step 1: Create the 4 split DTO files, delete the monolith**

Create `src/app/features/user-management/data/dto/user-management/user.dto.ts`:
```ts
// user.dto.ts — user resource response DTOs (API_FLOW convention).
// UserDtoRs fields are raw/untrusted transport — isUserDtoRsValid guards them
// before the use case maps to the domain model.
import { BaseResponseRs } from '@core/network/api/base-response-rs';
import { ROLE_LABELS } from '@core/domain/roles';

export interface UserDtoRs {
  id: string;
  fullName: string;
  email: string;
  role: string;
  status: string;
  mustChangePassword: boolean;
}

export interface UsersDtoRs extends BaseResponseRs<UserDtoRs[]> {}
export interface UserItemDtoRs extends BaseResponseRs<UserDtoRs> {}

export function isUserDtoRsValid(dto: UserDtoRs): boolean {
  return (
    typeof dto.id === 'string' && dto.id.length > 0 &&
    typeof dto.fullName === 'string' &&
    typeof dto.email === 'string' &&
    typeof dto.role === 'string' &&
    Object.prototype.hasOwnProperty.call(ROLE_LABELS, dto.role) &&
    (dto.status === 'active' || dto.status === 'disabled') &&
    typeof dto.mustChangePassword === 'boolean'
  );
}
```

Create `src/app/features/user-management/data/dto/user-management/create-user.dto.ts`:
```ts
// create-user.dto.ts — create-user request DTO (API_FLOW convention).
export interface CreateUserDtoRq {
  fullName: string;
  email: string;
  role: string;
  password: string;
}
```

Create `src/app/features/user-management/data/dto/user-management/update-user.dto.ts`:
```ts
// update-user.dto.ts — update-user request DTO (API_FLOW convention).
export interface UpdateUserDtoRq {
  fullName: string;
  email: string;
  status: string;
  password?: string;
}
```

Create `src/app/features/user-management/data/dto/user-management/set-user-status.dto.ts`:
```ts
// set-user-status.dto.ts — set-user-status request DTO (API_FLOW convention).
export interface SetUserStatusDtoRq {
  status: string;
}
```

Delete: `git -C frontend rm src/app/features/user-management/data/dto/user.dto.ts`

- [ ] **Step 2: Move the model, repoint its DTO import**

```bash
git -C frontend mv src/app/features/user-management/domain/model/user.ts src/app/features/user-management/domain/model/user-management/user.ts
```
In the moved file, change:
```ts
import { UserDtoRs } from '@features/user-management/data/dto/user.dto';
```
to:
```ts
import { UserDtoRs } from '@features/user-management/data/dto/user-management/user.dto';
```

- [ ] **Step 3: Move the 4 use-cases, repoint their model/dto imports**

```bash
git -C frontend mv src/app/features/user-management/domain/usecases/list-users.use-case.ts      src/app/features/user-management/domain/usecases/user-management/list-users.use-case.ts
git -C frontend mv src/app/features/user-management/domain/usecases/create-user.use-case.ts     src/app/features/user-management/domain/usecases/user-management/create-user.use-case.ts
git -C frontend mv src/app/features/user-management/domain/usecases/update-user.use-case.ts     src/app/features/user-management/domain/usecases/user-management/update-user.use-case.ts
git -C frontend mv src/app/features/user-management/domain/usecases/set-user-status.use-case.ts src/app/features/user-management/domain/usecases/user-management/set-user-status.use-case.ts
```

`user-management/list-users.use-case.ts` — replace:
```ts
import { User, toUser } from '@features/user-management/domain/model/user';
import { isUserDtoRsValid } from '@features/user-management/data/dto/user.dto';
```
with:
```ts
import { User, toUser } from '@features/user-management/domain/model/user-management/user';
import { isUserDtoRsValid } from '@features/user-management/data/dto/user-management/user.dto';
```

`user-management/create-user.use-case.ts` — replace:
```ts
import { User, CreateUserInput, toUser } from '@features/user-management/domain/model/user';
import { CreateUserDtoRq, isUserDtoRsValid } from '@features/user-management/data/dto/user.dto';
```
with:
```ts
import { User, CreateUserInput, toUser } from '@features/user-management/domain/model/user-management/user';
import { CreateUserDtoRq } from '@features/user-management/data/dto/user-management/create-user.dto';
import { isUserDtoRsValid } from '@features/user-management/data/dto/user-management/user.dto';
```

`user-management/update-user.use-case.ts` — replace:
```ts
import { User, UpdateUserInput, toUser } from '@features/user-management/domain/model/user';
import { UpdateUserDtoRq, isUserDtoRsValid } from '@features/user-management/data/dto/user.dto';
```
with:
```ts
import { User, UpdateUserInput, toUser } from '@features/user-management/domain/model/user-management/user';
import { UpdateUserDtoRq } from '@features/user-management/data/dto/user-management/update-user.dto';
import { isUserDtoRsValid } from '@features/user-management/data/dto/user-management/user.dto';
```

`user-management/set-user-status.use-case.ts` — replace:
```ts
import { User, UserStatus, toUser } from '@features/user-management/domain/model/user';
import { SetUserStatusDtoRq, isUserDtoRsValid } from '@features/user-management/data/dto/user.dto';
```
with:
```ts
import { User, UserStatus, toUser } from '@features/user-management/domain/model/user-management/user';
import { SetUserStatusDtoRq } from '@features/user-management/data/dto/user-management/set-user-status.dto';
import { isUserDtoRsValid } from '@features/user-management/data/dto/user-management/user.dto';
```

- [ ] **Step 4: Repoint the non-use-case consumers**

`domain/repositories/users.repository.ts` AND `data/repositories/users.repository.impl.ts` — both have:
```ts
import {
  UsersDtoRs,
  UserItemDtoRs,
  CreateUserDtoRq,
  UpdateUserDtoRq,
  SetUserStatusDtoRq,
} from '@features/user-management/data/dto/user.dto';
```
Replace it (in each file) with:
```ts
import { UsersDtoRs, UserItemDtoRs } from '@features/user-management/data/dto/user-management/user.dto';
import { CreateUserDtoRq } from '@features/user-management/data/dto/user-management/create-user.dto';
import { UpdateUserDtoRq } from '@features/user-management/data/dto/user-management/update-user.dto';
import { SetUserStatusDtoRq } from '@features/user-management/data/dto/user-management/set-user-status.dto';
```

`presentation/pages/user-management/users.viewmodel.ts` — repoint the 4 use-case imports (lines 2–5) to the `usecases/user-management/*` paths (master table) and the model import (line 7):
```ts
import { User, UserStatus } from '@features/user-management/domain/model/user-management/user';
```

- [ ] **Step 5: Move + repoint the user-management specs**

```bash
git -C frontend mv src/app/features/user-management/testing/data/dto/user.dto.spec.ts                    src/app/features/user-management/testing/data/dto/user-management/user.dto.spec.ts
git -C frontend mv src/app/features/user-management/testing/domain/model/user.spec.ts                    src/app/features/user-management/testing/domain/model/user-management/user.spec.ts
git -C frontend mv src/app/features/user-management/testing/domain/usecases/users.use-cases.spec.ts      src/app/features/user-management/testing/domain/usecases/user-management/users.use-cases.spec.ts
```

Import edits:
- `testing/data/dto/user-management/user.dto.spec.ts`: `@features/user-management/data/dto/user.dto` → `@features/user-management/data/dto/user-management/user.dto` (imports `UserDtoRs, isUserDtoRsValid`).
- `testing/domain/model/user-management/user.spec.ts`:
  - `@features/user-management/domain/model/user` → `@features/user-management/domain/model/user-management/user` (`toUser, User`)
  - `@features/user-management/data/dto/user.dto` → `@features/user-management/data/dto/user-management/user.dto` (`UserDtoRs`)
- `testing/domain/usecases/user-management/users.use-cases.spec.ts`:
  - the 4 use-case imports → `usecases/user-management/*` (master table)
  - `@features/user-management/data/dto/user.dto` → `@features/user-management/data/dto/user-management/user.dto` (`UserDtoRs`)
  - `@features/user-management/domain/model/user` → `@features/user-management/domain/model/user-management/user` (`User`)

Specs that stay in place — edit imports only:
- `testing/data/repositories/users.repository.impl.spec.ts`: `UserDtoRs` → `@features/user-management/data/dto/user-management/user.dto`.
- `testing/presentation/pages/user-management/users.viewmodel.spec.ts`: the 4 use-case imports → `usecases/user-management/*`; `User` → `@features/user-management/domain/model/user-management/user`.

- [ ] **Step 6: Verify green**

Run: `cd frontend && npm test`
Expected: **Tests: 117 passed**, **Test Suites: 34 passed** (unchanged from Task 1 — user-management specs move 1:1, no split).
Run: `cd frontend && npm run build`
Expected: build succeeds.
If it fails, grep the user-management tree for lingering `data/dto/user.dto` or `domain/model/user'` references and repoint.

- [ ] **Step 7: Commit (GATED — do not run until the user authorizes)**

```bash
git -C frontend add -A -- src/app/features/user-management
git commit -m "refactor(users): screen-based dto/usecases/model folders + split DTOs"
```

---

### Task 3: Update the architecture docs

**Files:** Modify `frontend/docs/{FEATURE_ANATOMY,ADD_A_FEATURE,AUTH,DATA_FLOW,DATA_SOURCE_SEAM}.html`; sweep the rest.

**Interfaces:** none (documentation only).

- [ ] **Step 1: Find every stale reference**

Run from the repo root:
```bash
grep -rnE "dto/(auth|user)\.dto|domain/model/(auth|user)'|usecases/(login|logout|refresh-token|load-current-user|change-password|list-users|create-user|update-user|set-user-status)\.use-case" frontend/docs --include=*.html
```
Expected: a list of hits (primarily in `FEATURE_ANATOMY`, `ADD_A_FEATURE`, `AUTH`, `DATA_FLOW`, `DATA_SOURCE_SEAM`). This is the worklist.

- [ ] **Step 2: Update `FEATURE_ANATOMY.html` and `ADD_A_FEATURE.html`**

Update the `data/dto`, `domain/usecases`, and `domain/model` sections to show screen-based subfolders + a `shared/` bucket, and the per-operation DTO split. Reflect this canonical layout (adapt to each doc's existing HTML/tree style):
```
data/dto/
  <screen>/<operation>.dto.ts        request DTO per operation
  shared/<resource>.dto.ts           response DTOs + validator (cross-screen / screen-less)
domain/usecases/
  <screen>/<name>.use-case.ts        (or shared/ when owned by the store/interceptor/app-init)
domain/model/
  <screen>/<name>.ts                 screen-specific input types
  shared/<aggregate>.ts              feature-wide model + mappers
```
State the assignment rule (screen that triggers the operation owns it; store/interceptor/app-init/multi-screen → `shared/`) and the DTO split rule (response DTOs + validator together; each request DTO its own file). In `ADD_A_FEATURE.html`, update the "add a DTO / use-case / model" steps to create files in the new locations.

- [ ] **Step 3: Update `AUTH.html`, `DATA_FLOW.html`, `DATA_SOURCE_SEAM.html`**

`AUTH.html`: update the auth folder map to the concrete tree — `dto/{login,change-password,shared}`, `usecases/{login,change-password,account,shared}`, `model/{login,change-password,shared}` (see the spec's auth tree for the exact contents). Fix any prose naming old file paths. Read the doc's tree carefully (it is a multi-line diagram the Step-1 grep may not fully catch).
`DATA_FLOW.html` / `DATA_SOURCE_SEAM.html`: update any DTO→model flow that names `auth.dto`/`user.dto` or `model/auth`/`model/user` to the new split file paths (e.g. `dto/shared/session.dto.ts` → `model/shared/auth.ts`).

- [ ] **Step 4: Sweep remaining docs and verify clean**

Apply the same corrections to any other `docs/*.html` hit in Step 1. Re-run the Step 1 grep — expect **no output**. Also re-read each edited doc to confirm the HTML is well-formed and coherent.

- [ ] **Step 5: Commit (GATED — do not run until the user authorizes)**

```bash
git -C frontend add -A -- docs
git commit -m "docs(fe): document screen-based dto/usecases/model folders"
```

---

## Self-Review

**Spec coverage:**
- Screen folders + `shared/` in dto/usecases/model → Tasks 1 (auth) & 2 (user-management).
- DTO monolith split per operation → Task 1 Step 2, Task 2 Step 1 (exact file contents given).
- Strict auth model split → Task 1 Step 3.
- user-management single `user-management/` folder + whole-model move → Task 2 Steps 1–3.
- `testing/` mirrors + auth DTO-spec split → Task 1 Step 6, Task 2 Step 5.
- DI/barrel/router/tsconfig untouched → Global Constraints + asserted by `npm run build`.
- Docs → Task 3. ✓ All spec sections covered.

**Placeholder scan:** No "TBD"/"handle X"/"similar to Task N". New files shown in full; every repoint gives exact old→new import text or a master-table lookup with the symbol named. ✓

**Type consistency:** Module paths are defined once in the master table and reused verbatim in every step. Split-file exports (`SessionDtoRs`/`SessionItemDtoRs`/`isSessionDtoRsValid`, `CurrentUserDtoRs`/`CurrentUserItemDtoRs`/`isCurrentUserDtoRsValid`, `LoginDtoRq`, `ChangePasswordDtoRq`, `RefreshDtoRq`, model types, use-case classes) match the symbols the consumers import. Suite-count expectation (33→34 at Task 1, then steady) is stated per task. ✓
