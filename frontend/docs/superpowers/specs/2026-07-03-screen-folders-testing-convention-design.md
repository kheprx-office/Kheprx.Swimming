# Screen-folder + centralized `testing/` convention

**Date:** 2026-07-03
**Status:** Design approved — pending spec review
**Scope:** `frontend/` — all three features (`auth`, `user-management`, `home`)

## Goal

Reorganize each feature's presentation layer and test files for legibility:

1. **Screen folders** — every page lives in a folder named after the screen, bundling the
   page component, its template, and its viewmodel so the page↔viewmodel association is
   obvious from the folder.
2. **Centralized `testing/`** — every `.spec.ts` moves out of the source tree into a per-feature
   `testing/` folder that mirrors the source structure, so all tests for a feature sit in one place.

This is a **structure-only** change: no test behavior, routing behavior, or runtime behavior
changes. It is a file-move + import-rewrite + template-extraction operation.

## Decisions (locked)

| Decision | Choice |
|---|---|
| Where do viewmodel `.spec.ts` files go? | **All specs in `testing/`** — no `.spec.ts` lives outside `testing/`. |
| Inline-template pages (`account`, `change-password`) | **Extract** inline `template:` to real `.html` + switch to `templateUrl`, so every screen is uniformly `page.ts` + `page.html`. |
| `testing/presentation/` viewmodel-spec depth | **Mirror the screen subfolder** — `testing/` mirrors source exactly, down to the per-screen folder. |
| Architecture HTML docs | **Update as part of this work.** |
| Committing | **Do not commit** until the user explicitly says so (respects the active API_FLOW pilot gate). |

## The two rules

**Rule A — Screen folders.** Each page becomes `presentation/pages/<screen>/`, containing:
- `<screen>.page.ts`
- `<screen>.page.html` (extracted if it was an inline template)
- `<screen>.viewmodel.ts` (only if the screen has a viewmodel)

Presentation-layer files that are **not** screens (e.g. `auth-session.store.ts`, `auth.guard.ts`)
stay at the `presentation/` root.

**Rule B — Central `testing/`.** No `.spec.ts` lives under `data/`, `domain/`, or `presentation/`.
Every spec moves into `<feature>/testing/`, mirroring its original source path:
- `data/dto/x.dto.spec.ts` → `testing/data/dto/x.dto.spec.ts`
- `domain/usecases/x.use-case.spec.ts` → `testing/domain/usecases/x.use-case.spec.ts`
- `presentation/pages/login/login.viewmodel.spec.ts` → `testing/presentation/pages/login/login.viewmodel.spec.ts`
- `presentation/auth-session.store.spec.ts` → `testing/presentation/auth-session.store.spec.ts`

Specs import their subject-under-test via the `@features`/`@core` **path aliases** (never fragile
relative paths), since they now sit structurally far from source.

## Target trees

### auth (richest feature)

```
features/auth/
├── data/                              (source unchanged in place)
│   ├── dto/auth.dto.ts
│   ├── repositories/auth.repository.impl.ts
│   ├── auth.interceptor.ts
│   ├── auth.providers.ts
│   └── token-store.ts
├── domain/                            (source unchanged in place)
│   ├── model/auth.ts
│   ├── repositories/auth.repository.ts
│   └── usecases/{change-password,load-current-user,login,logout,refresh-token}.use-case.ts
├── presentation/
│   ├── pages/
│   │   ├── login/
│   │   │   ├── login.page.ts
│   │   │   ├── login.page.html
│   │   │   └── login.viewmodel.ts
│   │   ├── change-password/
│   │   │   ├── change-password.page.ts
│   │   │   ├── change-password.page.html      ← extracted from inline template
│   │   │   └── change-password.viewmodel.ts
│   │   └── account/
│   │       ├── account.page.ts
│   │       └── account.page.html              ← extracted; no viewmodel
│   ├── auth-session.store.ts                  ← stays (not a screen)
│   └── auth.guard.ts                          ← stays (not a screen)
├── testing/
│   ├── data/
│   │   ├── dto/auth.dto.spec.ts
│   │   ├── repositories/auth.repository.impl.spec.ts
│   │   ├── auth.interceptor.spec.ts
│   │   └── token-store.spec.ts
│   ├── domain/
│   │   ├── model/auth.spec.ts
│   │   └── usecases/{change-password,load-current-user,login,logout,refresh-token}.use-case.spec.ts
│   └── presentation/
│       ├── pages/
│       │   ├── login/login.viewmodel.spec.ts
│       │   └── change-password/change-password.viewmodel.spec.ts
│       ├── auth-session.store.spec.ts
│       └── auth.guard.spec.ts
└── index.ts                                    ← export paths updated
```

### user-management

```
features/user-management/
├── data/{dto/user.dto.ts, repositories/users.repository.impl.ts, users.providers.ts}
├── domain/{model/user.ts, repositories/users.repository.ts,
│           usecases/{list-users,create-user,update-user,set-user-status}.use-case.ts}
├── presentation/
│   └── pages/
│       └── user-management/
│           ├── user-management.page.ts
│           ├── user-management.page.html       (already external — just moves)
│           └── users.viewmodel.ts
├── testing/
│   ├── data/
│   │   ├── dto/user.dto.spec.ts
│   │   └── repositories/users.repository.impl.spec.ts
│   ├── domain/
│   │   └── usecases/users.use-cases.spec.ts    (single combined spec — moves as-is)
│   └── presentation/
│       └── pages/user-management/users.viewmodel.spec.ts
└── index.ts                                     ← export paths updated
```

### home (no viewmodel, presentation-only)

```
features/home/
├── presentation/
│   └── pages/
│       └── home/
│           ├── home.page.ts
│           └── home.page.html                  (already external — just moves)
├── testing/
│   └── presentation/pages/home/home.page.spec.ts   (page spec, not a viewmodel)
└── index.ts                                     ← export path updated
```

## Edits required (blast radius)

### What does NOT change
- **`src/app/app.routes.ts`** — loads every page/viewmodel through feature barrels
  (`@features/auth`, `@features/home`, `@features/user-management`). Unaffected by moves.
- **`auth.guard.ts` import in routes** (`@features/auth/presentation/auth.guard`) — guard stays put.
- **`auth-session.store.ts` imports** across the app — store stays put.
- **Jest discovery** — `**/*.spec.ts` glob still finds specs under `testing/`.
- Test file **contents/assertions** — unchanged except import paths.

### 1. Move pages into screen folders
Move `<screen>.page.ts` (+ existing `.html`) into `presentation/pages/<screen>/`.

### 2. Extract inline templates (auth only)
- `account.page.ts`: replace `template: \`…\`` → `templateUrl: './account.page.html'`; write the
  markup to `account/account.page.html`.
- `change-password.page.ts`: same, → `change-password/change-password.page.html`.
- `login.page.ts` already uses `templateUrl: './login.page.html'` — the relative ref stays valid
  since the html moves alongside it.

### 3. Move viewmodels next to their pages + fix page imports
Move each viewmodel from `presentation/viewmodels/` into its screen folder, then update the page's
import from relative-parent to relative-same-folder:
- `login.page.ts`: `../viewmodels/login.viewmodel` → `./login.viewmodel`
- `change-password.page.ts`: `../viewmodels/change-password.viewmodel` → `./change-password.viewmodel`
- `user-management.page.ts`: `../viewmodels/users.viewmodel` → `./users.viewmodel`

Delete the now-empty `presentation/viewmodels/` folders.

### 4. Move specs into `testing/` + switch to alias imports
For every `.spec.ts`, move to the mirrored `testing/` path and rewrite any **relative** import of the
subject-under-test to its `@features/…` alias. Cross-module imports (already `@core/…` / `@features/…`)
stay as-is. Example — `login.viewmodel.spec.ts`:
- `import { LoginViewModel } from './login.viewmodel'`
  → `from '@features/auth/presentation/pages/login/login.viewmodel'`

Full spec inventory to move:
- **auth:** `auth.dto.spec`, `auth.repository.impl.spec`, `auth.interceptor.spec`, `token-store.spec`,
  `auth.spec` (model), 5× `*.use-case.spec`, `login.viewmodel.spec`, `change-password.viewmodel.spec`,
  `auth-session.store.spec`, `auth.guard.spec`.
- **user-management:** `user.dto.spec`, `users.repository.impl.spec`, `users.use-cases.spec`,
  `users.viewmodel.spec`.
- **home:** `home.page.spec`.

### 5. Update `index.ts` barrels
- **auth/index.ts** (5 lines):
  - `./presentation/pages/login.page` → `./presentation/pages/login/login.page`
  - `./presentation/pages/account.page` → `./presentation/pages/account/account.page`
  - `./presentation/pages/change-password.page` → `./presentation/pages/change-password/change-password.page`
  - `./presentation/viewmodels/login.viewmodel` → `./presentation/pages/login/login.viewmodel`
  - `./presentation/viewmodels/change-password.viewmodel` → `./presentation/pages/change-password/change-password.viewmodel`
- **user-management/index.ts** (2 lines):
  - `./presentation/pages/user-management.page` → `./presentation/pages/user-management/user-management.page`
  - `./presentation/viewmodels/users.viewmodel` → `./presentation/pages/user-management/users.viewmodel`
- **home/index.ts** (1 line):
  - `./presentation/pages/home.page` → `./presentation/pages/home/home.page`

### 6. Update architecture docs
- **`FEATURE_ANATOMY.html`** — replace the feature/presentation layout description with the
  screen-folder + `testing/` structure.
- **`TESTING.html`** — document the centralized `testing/` convention (mirror-source, alias imports).
- **`ADD_A_FEATURE.html`** — update the step-by-step so new screens/specs land in the new locations.
- Sweep the remaining `docs/*.html` for stale path strings (`presentation/pages/<x>.page`,
  `presentation/viewmodels`, co-located `.spec.ts`) and fix any hits.

## Verification
1. `npx jest` (project test script) — **green, identical test count** to before.
2. `npx tsc -p tsconfig.app.json --noEmit` and `npx tsc -p tsconfig.spec.json --noEmit` — **no errors**
   (catches broken imports/aliases and template-extraction typos). `ng build` as a final backstop.
3. Manual sanity: confirm `presentation/viewmodels/` dirs are gone and no `.spec.ts` remains outside
   any `testing/` folder.

## Out of scope
- Any change to test assertions/behavior.
- The API_FLOW `data`/`domain` restructure itself (separate, in-flight pilot).
- Route paths, URLs, component selectors, public API surface — all unchanged.

## Constraints
- **No commits** until the user explicitly authorizes (active API_FLOW pilot gate).
