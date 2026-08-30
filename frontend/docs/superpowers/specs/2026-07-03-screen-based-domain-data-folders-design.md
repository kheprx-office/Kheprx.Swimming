# Screen-based folders for `dto/` · `usecases/` · `model/`

**Date:** 2026-07-03
**Status:** Design approved — pending spec review
**Scope:** `frontend/` — the `auth` and `user-management` features (home has no domain/data layer)
**Builds on:** `2026-07-03-screen-folders-testing-convention-design.md` (presentation screen-folders + centralized `testing/`)

## Goal

Extend the screen-folder organizing principle from the presentation layer into the domain/data layer, and break up the monolithic DTO files:

1. Within `data/dto/`, `domain/usecases/`, `domain/model/`, group artifacts into a subfolder per **screen**, plus a **`shared/`** subfolder for artifacts with no single screen owner.
2. Split each `*.dto.ts` monolith (auth.dto.ts has 7 interfaces + 2 validators; user.dto.ts has 6 + 1) into small, per-operation files that are easy to read.
3. Mirror the new structure into `testing/` (per the established convention).
4. Update the architecture docs.

Structure-only refactor: no runtime, DI, routing, or test behavior changes. File splits/moves + import-path rewrites.

## Decisions (locked)

| Decision | Choice |
|---|---|
| Organizing principle | **By screen** (as originally asked), with a **`shared/`** bucket for screen-less / cross-screen artifacts. |
| user-management (single screen) | **Same convention** — a `user-management/` subfolder in each layer; DTO monolith split inside it. |
| auth model | **Strict by-screen split** — `LoginInput` → `model/login/`, `ChangePasswordInput` → `model/change-password/`, the rest → `model/shared/`. |
| `testing/` | Mirrors the new source layout down to the screen/`shared` folder (established rule); the auth DTO spec splits to follow its source. |
| Committing | Hold all commits until the user authorizes (same gate as the prior refactor). |

## Rules

**Assignment rule.** An artifact lives in the folder of the screen whose user action triggers its operation. Artifacts triggered by shared infrastructure (the `AuthSessionStore` rehydrate, the HTTP interceptor) or used by 2+ screens go in `shared/`.

**DTO split rule.** A resource's response DTOs (+ its validator) sit together in one file; each request DTO gets its own file. Files are placed by the assignment rule.

## Why auth lands mostly in `shared/`

Auth is **session-centric**, not screen-centric. Its use-cases are owned by the root `AuthSessionStore` (login/logout/change-password/load-current-user) and the interceptor (refresh-token); its session model and session/current-user DTOs are produced/consumed across screens. So the screen folders hold only what a single screen exclusively triggers, and the session machinery lives in `shared/`. This is the honest shape of the feature, not an accident of the convention.

## Target trees

### auth (screens: login, change-password, account)

```
data/dto/
  login/login.dto.ts                       LoginDtoRq
  change-password/change-password.dto.ts   ChangePasswordDtoRq
  shared/session.dto.ts                    SessionDtoRs, SessionItemDtoRs, isSessionDtoRsValid
  shared/current-user.dto.ts               CurrentUserDtoRs, CurrentUserItemDtoRs, isCurrentUserDtoRsValid
  shared/refresh.dto.ts                    RefreshDtoRq
domain/usecases/
  login/login.use-case.ts
  change-password/change-password.use-case.ts
  account/logout.use-case.ts
  shared/load-current-user.use-case.ts
  shared/refresh-token.use-case.ts
domain/model/
  login/login.ts                           LoginInput
  change-password/change-password.ts       ChangePasswordInput
  shared/auth.ts                           AuthSession, AuthTokens, AuthPrincipal, toAuthSession, toAuthTokens, toAuthPrincipal
testing/                                    (mirrors the above)
  data/dto/shared/session.dto.spec.ts      (was part of auth.dto.spec.ts — isSessionDtoRsValid tests)
  data/dto/shared/current-user.dto.spec.ts (was part of auth.dto.spec.ts — isCurrentUserDtoRsValid tests)
  domain/model/shared/auth.spec.ts
  domain/usecases/login/login.use-case.spec.ts
  domain/usecases/change-password/change-password.use-case.spec.ts
  domain/usecases/account/logout.use-case.spec.ts
  domain/usecases/shared/load-current-user.use-case.spec.ts
  domain/usecases/shared/refresh-token.use-case.spec.ts
```

Files deleted after the split: `data/dto/auth.dto.ts`, `domain/model/auth.ts`, `testing/data/dto/auth.dto.spec.ts`.
`account/` exists only under `usecases/` (logout has no request/response DTO and no dedicated model type).

**Import wiring inside the split files:**
- `shared/session.dto.ts` and `shared/current-user.dto.ts` each import `BaseResponseRs` (`@core/network/api/base-response-rs`) and `ROLE_LABELS` (`@core/domain/roles`) for their validators. The three request DTO files are plain interfaces (no imports).
- `model/shared/auth.ts` imports `UserRole` (`@core/domain/roles`) and `SessionDtoRs`/`CurrentUserDtoRs` from the new `dto/shared/*` files (mappers consume them).

### user-management (single screen)

```
data/dto/user-management/
  user.dto.ts             UserDtoRs, UsersDtoRs, UserItemDtoRs, isUserDtoRsValid
  create-user.dto.ts      CreateUserDtoRq
  update-user.dto.ts      UpdateUserDtoRq
  set-user-status.dto.ts  SetUserStatusDtoRq
domain/usecases/user-management/
  list-users.use-case.ts · create-user.use-case.ts · update-user.use-case.ts · set-user-status.use-case.ts
domain/model/user-management/
  user.ts                 (moved whole: User, UserStatus, CreateUserInput, UpdateUserInput, toUser)
testing/                  (mirrors the above)
  data/dto/user-management/user.dto.spec.ts
  domain/model/user-management/user.spec.ts
  domain/usecases/user-management/users.use-cases.spec.ts
```
Files deleted: the flat `data/dto/user.dto.ts`, `domain/model/user.ts` (relocated into `user-management/`). No `shared/` — everything belongs to the one screen.

## Blast radius (all mechanical import-path updates)

**Unchanged** (verified): `auth.providers.ts` (binds `AUTH_REPOSITORY → AuthRepositoryImpl` only; use-cases are `@Injectable({providedIn:'root'})` so they self-register), the feature `index.ts` barrels, `app.routes.ts`, the guards, the stores' public APIs, and all tsconfig/jest config.

**Consumers whose import paths change** (files stay put; only their `import` lines repoint to the split/relocated targets):

*auth:*
- `data/token-store.ts` — `AuthTokens` → `model/shared/auth`
- `presentation/auth-session.store.ts` — 4 use-cases + `AuthSession`
- `data/auth.interceptor.ts` — `RefreshTokenUseCase` → `usecases/shared/refresh-token.use-case`
- `data/repositories/auth.repository.impl.ts` and `domain/repositories/auth.repository.ts` — DTO imports now span `dto/login`, `dto/change-password`, `dto/shared/*`
- the 5 use-cases — repoint their own model/dto imports (some now two lines: e.g. `login.use-case` imports `AuthSession,toAuthSession` from `model/shared/auth` and `LoginInput` from `model/login/login`). `logout.use-case` has no model/dto imports → moves clean.
- specs: `auth.dto.spec` (split), `auth.spec` (model), all 5 use-case specs, `auth-session.store.spec`, `auth.interceptor.spec`, both viewmodel specs, `auth.repository.impl.spec`.

*user-management:*
- `presentation/pages/user-management/users.viewmodel.ts` — 4 use-cases + `User`/`UserStatus`
- `data/repositories/users.repository.impl.ts` and `domain/repositories/users.repository.ts` — DTO imports → `dto/user-management/*`
- the 4 use-cases + `model/user-management/user.ts` — repoint model/dto imports
- specs: `user.dto.spec`, `user.spec` (model), `users.use-cases.spec`, `users.repository.impl.spec`, `users.viewmodel.spec`.

## Verification
1. `npm test` — green, **117 tests still passing** (no assertion changes). **Suite count goes 33 → 34**: the auth DTO spec splits into `session.dto.spec.ts` + `current-user.dto.spec.ts`, so one file becomes two (same total `it()` blocks). Every other spec moves 1:1.
2. `npm run build` — succeeds.
3. Grep check: no import references remain to the old paths `data/dto/auth.dto`, `data/dto/user.dto`, `domain/model/auth`, `domain/model/user`, or flat `domain/usecases/<name>.use-case` for the moved files.

## Docs to update
`FEATURE_ANATOMY.html`, `ADD_A_FEATURE.html`, `AUTH.html`, and the data-flow docs (`DATA_FLOW.html`, `DATA_SOURCE_SEAM.html`) — show the screen-folder + `shared/` layout for `dto`/`usecases`/`model` and the per-operation DTO split. Sweep the remaining `docs/*.html` for stale `dto/auth.dto` / `model/auth` / flat-usecase references.

## Out of scope
- Presentation layer (already screen-foldered).
- Any change to DTO/model/use-case logic, DI wiring, routes, or public barrel surface.
- `docs/architecture/README.md` (the separate API_FLOW docs effort).

## Constraints
- **No commits** until the user explicitly authorizes.
