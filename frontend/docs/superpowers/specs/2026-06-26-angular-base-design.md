# Design: Angular Frontend Base Architecture — `Base Frontend`

**Date:** 2026-06-26
**Status:** Approved

## Goal

Create a clean, reusable Angular **base architecture** that is the web counterpart of the
existing React Native mobile base (`C:\Users\envnt\Desktop\BaseProject`). It ports the same
**Clean Architecture + MVVM, feature-first** patterns and naming to idiomatic, modern Angular —
not the framework specifics. A developer who knows the mobile base should recognize this one
immediately.

The architecture documentation is a first-class deliverable: the base ships its own living
architecture guide, mirroring the role the docs play in the mobile repo.

## Scope

A **full reference port**: all cross-cutting core infrastructure **plus** web-appropriate
worked-example features, in the same heavily-commented teaching style as the mobile base.

**In scope (worked-example features):**

- `home` — landing page with navigation into each feature.
- `api/fetch-posts` — the network vertical slice (DTO + validator + repository + use case +
  viewmodel + page).
- `offline-storage` — the local-persistence vertical slice (save/load a profile via the
  KeyValueStore).
- `design/profile-form` — an Angular reactive form with validation → summary page.
- `design/design-hub` — a UI/design showcase: themed components, a responsive sidebar/drawer
  navigation (the web equivalent of the mobile slide-menu + bottom-nav demos), and an Angular
  animations demo.

**Out of scope (YAGNI):**

- Global state management libraries (NgRx, etc.) — local signals are sufficient, matching the
  mobile base's use of local component state.
- SSR / Angular Universal, PWA, i18n — added later if a real need appears.
- Authentication, real backend contracts — the API demo uses `https://dummyjson.com`, as mobile does.
- A monorepo / Nx libraries — single application project.

## Foundation & Tooling

- **Framework:** Angular **v20** (latest stable), **standalone** components only (no NgModules),
  bootstrapped via `bootstrapApplication`.
- **State:** Angular **Signals** for presentation state. **Promise + `Result<T>`** through the
  domain layers (repositories and use cases are `async`/Promise).
- **Language:** TypeScript (strict).
- **Styling:** SCSS, with design tokens exposed as CSS custom properties.
- **Routing:** Angular Router, lazy `loadComponent`, feature-scoped route `providers` for DI.
- **HTTP:** Angular `HttpClient`, wrapped at the `http-client` boundary to preserve the mobile
  retry/timeout/backoff + `AppError` contract and return Promises.
- **Persistence:** `localStorage`, wrapped behind the typed async `KeyValueStore` API.
- **Crypto:** Web Crypto (`SubtleCrypto`) AES-256-GCM, with a non-extractable `CryptoKey` stored
  in IndexedDB.
- **Testing:** Jest via `jest-preset-angular`.
- **Path aliases:** `@core/*`, `@features/*`.

## Location

- Target directory: `C:\Users\envnt\Desktop\Base Frontend` (currently empty except for `docs/`).
- The Angular project is initialized **in this folder**, preserving the existing `docs/`.
- Project name: `base-frontend` (kebab-case, Angular-valid).

## Architecture Overview

**Pattern:** Clean Architecture + MVVM, feature-first — identical to the mobile base. One-way
dependency: presentation → domain → data → core infra. The domain layer is framework-agnostic and
ports over almost verbatim.

### Composition root mapping

| Mobile (React Native)            | Angular                                                        |
| -------------------------------- | ------------------------------------------------------------- |
| `index.js`                       | `src/main.ts` (`bootstrapApplication`)                        |
| `App.tsx`                        | `app.component.ts` (root shell, `<router-outlet>`)            |
| `providers/RootProviders.tsx`    | `app.config.ts` (`provideRouter`, `provideHttpClient`, etc.)  |
| `RootNavigator.tsx` + `types.ts` | `app.routes.ts` (typed route table, lazy `loadComponent`)     |

### Folder structure

```
src/
  main.ts, index.html, styles.scss
  app/
    app.component.ts            # root shell (<router-outlet>)
    app.config.ts              # providers (composition root)
    app.routes.ts              # route table
    core/
      config/env.ts
      crypto/                  # crypto.service.ts, types.ts (DI seams),
                              # web-crypto-key-store.ts (KeyProvider → IndexedDB), index.ts
      datasource/keyvalue/    # key-value.store.ts (localStorage), storage-keys.ts
      domain/
        errors/app-error.ts
        result/result.ts
        usecase/use-case.ts
      logging/logger.ts
      network/api/            # http-client.ts (wraps HttpClient), base-response-rs.ts
      ui/
        components/           # nav-button, text-field, notification, ... (standalone)
        theme/               # theme.ts (tokens) + theme.scss (CSS variables)
    features/
      home/                  # presentation/pages + index.ts
      api/fetch-posts/       # data/ domain/ presentation/ + index.ts
      offline-storage/       # data/ domain/ presentation/ + index.ts
      design/
        profile-form/        # reactive form → summary
        design-hub/          # sidebar/drawer nav, components, animations
tsconfig.json                # paths: @core/*, @features/*
jest.config.js, setup-jest.ts
docs/                        # architecture docs + superpowers specs/plans
```

### Per-feature vertical slice

Same taxonomy and concept names as mobile; Angular kebab-case filenames.

```
features/api/fetch-posts/
  data/dto/post-dto-rs.ts                       # PostDtoRs + isPostDtoRsValid (DtoRq added when a body exists)
  data/repositories/post.repository.ts          # PostRepositoryImpl implements the port
  domain/model/post.ts                          # Post + toPost mapper
  domain/repositories/post.repository.port.ts   # IPostRepository interface + InjectionToken (the port)
  domain/usecases/get-posts.use-case.ts         # GetPostsUseCase extends UseCase<void, Post[]>
  presentation/pages/api.page.ts                # ApiPage standalone component (the "screen")
  presentation/viewmodels/fetch-posts.viewmodel.ts  # @Injectable facade, signals(loading/posts/error)
  index.ts                                       # public barrel
```

### Naming conventions

- **Filenames:** Angular kebab-case with a type suffix (`get-posts.use-case.ts`,
  `post.repository.ts`, `api.page.ts`).
- **Class/type names:** aligned with the mobile base (`GetPostsUseCase`, `PostRepositoryImpl`,
  `Post`, `PostDtoRs`).
- **DTOs:** response DTOs suffixed `…DtoRs`, request DTOs `…DtoRq` (created only for endpoints
  that carry a request body).
- **Ports:** `I…Repository` interface paired with a `…_REPOSITORY` `InjectionToken`.
- **ViewModels:** `…ViewModel` `@Injectable` facade services exposing signals.

### Deliberate Angular adaptations

1. **Ports as DI tokens.** TypeScript interfaces do not exist at runtime, so each `I…Repository`
   port is an interface **plus** an `InjectionToken<I…Repository>`. The implementation is bound in
   the feature's route-level `providers`; the use case / viewmodel receives it via `inject()`. This
   is the faithful Angular form of the mobile constructor-injection-with-default-singleton pattern.
2. **Kebab-case filenames** per Angular convention, while class/type names stay identical to the
   mobile base so the two repos read as siblings.

## Data Flow

One direction, with signals at the presentation edge:

```
Page (component)  →  ViewModel (facade service, signals)  →  UseCase.run()  →  Repository (port)
                                                                                    ↓
   AppError ← Result<T> ←────────────────────────────────  http-client / KeyValueStore / Crypto
```

Walkthrough (`fetch-posts`):

1. **Page** (`api.page.ts`) injects its **ViewModel** and renders from its signals
   (`@if (vm.loading())`, `@for (p of vm.posts())`). Calls `vm.run()` on a click.
2. **ViewModel** (`fetch-posts.viewmodel.ts`, `@Injectable`) holds `loading`, `posts`, `error`
   signals, `inject()`s the use case, sets `loading=true`, `await`s `useCase.run()`, switches on the
   `Result` to update signals. No try/catch — the base already normalized errors.
3. **UseCase** (`get-posts.use-case.ts`) `inject()`s the repository via its port token, implements
   `execute()` (fetch → validate DTOs → map to `Post[]`), throws `AppError` on a bad payload. Base
   `run()` wraps it into a `Result`.
4. **Repository** (`post.repository.ts`) calls `httpClient.request<…>()`, synthesizes the
   `BaseResponseRs` envelope, returns the DTO. Bound to its port token in the route `providers`.

### DI wiring (route level)

Each feature is self-contained and lazy-loaded; its wiring lives in one place and is swappable for
tests/fakes — the Angular-native equivalent of the mobile ViewModel's
`useMemo(() => new UseCase(new RepoImpl()))`.

```ts
// app.routes.ts (excerpt)
{
  path: 'api',
  loadComponent: () => import('@features/api/fetch-posts').then(m => m.ApiPage),
  providers: [
    { provide: POST_REPOSITORY, useClass: PostRepositoryImpl },
    GetPostsUseCase,
    FetchPostsViewModel,
  ],
}
```

## Error Handling

Ported verbatim from mobile — the least-changed part of the architecture.

- **`Result<T>`** = `{ ok: true; data: T } | { ok: false; error: AppError }`, with `ok()` / `fail()`
  helpers.
- **`AppError`** extends `Error` with `kind` + optional `status`. Kind union (unchanged):
  `'network' | 'http' | 'validation' | 'storage' | 'crypto' | 'unknown'`.
- **`UseCase<I, O>`** abstract base: subclass implements `execute()` (happy path, throws `AppError`);
  base `run()` owns the single try/catch, normalizes any non-`AppError` into
  `AppError('Unexpected error', 'unknown')`, logs via a named logger, and returns a `Result`.
- **Boundary mapping** — each infra layer throws a typed `AppError` so `kind` survives normalization:
  - `http-client.ts`: transport failure/timeout → `network`; non-2xx → `http` (+status).
  - `key-value.store.ts`: any storage failure or corrupt-object parse → `storage`.
  - `crypto.service.ts`: any SubtleCrypto failure → `crypto`.
  - use cases: failed DTO validation → `validation`.
- **Presentation feedback:** an injectable `NotificationService` (toast/snackbar) in `core/ui`
  replaces the mobile `Alert.alert`, so pages stay declarative.

## Web-Specific Primitives

Each keeps the same public contract and DI seams as mobile.

### `http-client.ts`

Wraps Angular `HttpClient`, exposes `request<T>(path): Promise<T>`.

- Base URL / timeout / retries / backoff read from `env` (same config shape as mobile).
- `firstValueFrom(http.get<T>(url).pipe(timeout(timeoutMs), retry({ count, delay: backoff })))`,
  retrying transport failures only — never HTTP errors.
- Maps `HttpErrorResponse`: `status === 0` / timeout → `AppError('network')`; non-2xx →
  `AppError('http', status)`. Returns a Promise so `UseCase` stays Promise-based.

### `key-value.store.ts`

Typed `KeyValueStore` over `localStorage`, same async API as mobile
(`setString/getString/setInt/getInt/setDouble/getDouble/setObject/getObject/remove/clear`).

- `guard()` wraps every op, mapping failures to `AppError('storage')`.
- Same intentional asymmetry: a corrupt **object** throws `storage`; a corrupt **primitive** falls
  back to the caller's default.
- Synchronous `localStorage` calls are wrapped in resolved Promises to preserve the contract.
- `storage-keys.ts` registry unchanged. Exported as a shared singleton **and** `@Injectable` for DI
  and tests.

### `crypto.service.ts`

AES-256-GCM via Web Crypto (`SubtleCrypto`), same `CryptoLike` / `KeyProvider` DI seams as mobile.

- `encrypt(plain): Promise<string>` / `decrypt(blob): Promise<string>`, Base64 framing of
  `iv (12 bytes) ‖ ciphertext+tag`. SubtleCrypto appends the 16-byte GCM auth tag to the
  ciphertext, so the service keeps the 12-byte IV prefix and treats the remainder as
  ciphertext+tag; the framing difference vs. the mobile `iv ‖ ciphertext ‖ tag` split is documented
  inline.
- `web-crypto-key-store.ts` is the production `KeyProvider`: get-or-create a **non-extractable**
  `CryptoKey` stored in IndexedDB under a fixed key name (the Keychain equivalent). Raw key bytes
  never leave the browser.
- Seams stay injectable so unit tests pass Node's `webcrypto` + an in-memory key provider — no
  browser required.

## Testing

- **Runner:** Jest via `jest-preset-angular` (`jest.config.js` + `setup-jest.ts`), `npm test`. Path
  aliases mapped in the Jest config to match `tsconfig`.
- **Framework-agnostic core (highest value, ported from mobile):**
  - `result`, `app-error`, `use-case` (success + each error kind normalized).
  - `crypto.service` (round-trip + tamper → `crypto`, using Node `webcrypto` + a fake key provider).
  - `key-value.store` (round-trip, missing key, corrupt object → `storage`, corrupt primitive →
    fallback).
  - `http-client` (network retry/backoff, HTTP error mapping, timeout) with
    `HttpClientTestingModule`.
- **Feature slices:** one use-case test per feature (validation + mapping) and a ViewModel test
  (`Result` → signal transitions) using a fake repository bound to the port token.
- **Components:** light smoke tests (renders; click triggers `vm.run()`) — pragmatic depth matching
  the mobile base.

## Documentation Plan

- **`docs/superpowers/specs/2026-06-26-angular-base-design.md`** — this approved design, committed.
- **`docs/architecture/README.md`** — the living architecture guide: layer diagram, data-flow
  walkthrough, the mobile↔Angular mapping table, naming conventions, and a step-by-step "how to add
  a new feature."
- **`README.md`** — quickstart (install, `npm start`, `npm test`, build) + a short architecture
  overview linking to the guide.
- **`docs/superpowers/plans/`** — the implementation plan (written next by the writing-plans skill).

## Verification

- Project scaffolds in `C:\Users\envnt\Desktop\Base Frontend` with the structure above; `docs/`
  survives.
- `npm install` completes; `npm start` serves the app; `npm run build` succeeds.
- `npm test` passes the core and feature tests described above.
- Each demo feature is reachable from `home` and works end-to-end (API fetch shows a result;
  profile saves/loads; reactive form → summary; design hub renders with working sidebar nav and
  animation demo).

## Success Criteria

- The Angular base mirrors the mobile base's Clean Architecture + MVVM structure, taxonomy, and
  concept names, with the two approved Angular adaptations (DI-token ports, kebab-case filenames).
- `core/` provides `Result`, `AppError`, `UseCase`, `Logger`, `env`, `http-client`,
  `KeyValueStore`, `CryptoService`, theme, and shared UI — each preserving the mobile contract.
- All four worked-example feature groups are implemented and pass their tests.
- The architecture guide and README are present, accurate, and committed.
