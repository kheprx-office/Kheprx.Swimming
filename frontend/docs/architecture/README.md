# Architecture Guide — Base Frontend

> Living document. Last updated: 2026-07-02.

---

## Visual architecture docs (HTML)

Rich, standalone pages that present this architecture (siblings of the mobile/backend bases' docs):

- [Documentation Hub](../index.html) — start here (numbered learning path)
- [Architecture Overview](../ARCHITECTURE_OVERVIEW.html)
- [Project Libraries](../PROJECT_LIBRARIES.html)
- [Core Files](../CORE_FILES.html)
- [Feature Anatomy](../FEATURE_ANATOMY.html)
- [Data Flow](../DATA_FLOW.html)
- [Composition Root & DI](../COMPOSITION_ROOT.html)
- [Error Handling & Result](../ERROR_HANDLING.html)
- [Validation & Forms](../VALIDATION.html)
- [Add a Feature](../ADD_A_FEATURE.html)
- [Testing](../TESTING.html)

---

## 1. Overview

Base Frontend is an Angular v20 web application built on **Clean Architecture + MVVM**
with a **feature-first** layout. It is the direct web counterpart of the React Native mobile
base: the patterns, naming conventions, and cross-cutting primitives (`Result`, `AppError`,
`UseCase`, `Logger`, `env`, `KeyValueStore`, `CryptoService`) are ported nearly verbatim so
that a developer familiar with one repo immediately recognises the other.

Dependencies flow in one direction only:

```
presentation  →  domain  →  data  →  core
```

- **`core/`** provides pure, framework-agnostic (or lightly Angular-wrapped) infrastructure.
  Nothing in `core/` imports from `features/`.
- **`domain/`** (per feature) holds models, use cases, and repository ports (interfaces). It
  depends only on `core/`. It is free of HTTP, storage, and UI concerns — but it DOES use
  Angular DI primitives (`inject`, `InjectionToken`, `@Injectable`) because those are part
  of the approved Angular adaptation (see "Two deliberate Angular adaptations" below). No
  transport or rendering code appears here.
- **`data/`** (per feature) holds DTOs, validators, and the repository implementation that
  talks to infra (HTTP, `localStorage`, crypto). It imports from `domain/` and `core/`,
  never from `presentation/`.
- **`presentation/`** (per feature) holds the page component and its ViewModel. It imports
  from `domain/` (use case + models) and `core/ui`, never from `data/`.

Two deliberate Angular adaptations over the mobile base:

1. **Ports as DI tokens.** TypeScript interfaces are erased at runtime, so each
   `I…Repository` interface is paired with an `InjectionToken<I…Repository>`. The concrete
   implementation is bound globally in `app.config.ts`; use cases and viewmodels receive it
   via `inject()`. This is the idiomatic Angular form of the mobile constructor-injection-with-default pattern.
2. **Kebab-case filenames** per Angular convention (`list-users.use-case.ts`), while class and
   type names remain identical to the mobile base (`ListUsersUseCase`, `UsersRepositoryImpl`) so
   the two repos read as siblings.

---

## 2. Layer Diagram

One-directional data flow, with Angular Signals at the presentation edge and Promises through
the domain and data layers:

```
Page (component)
  │  calls vm.load()
  ▼
ViewModel (@Injectable, signals)
  │  loading/users signals — rendered by the Page
  │  awaits useCase.run()
  ▼
UseCase.run()         ← abstract base; owns the single try/catch
  │  calls this.execute() (subclass)
  │  returns Result<T>
  ▼
Repository (port — InjectionToken)
  │  concrete impl injected globally; returns Promise<DtoRs> (envelope — no mapping)
  ▼
HttpClientService
  │  throws AppError on failure
  ▼
Result<T>  ←  { ok: true; data: T }  |  { ok: false; error: AppError }
```

**Error path:** Each infrastructure layer throws a typed `AppError` (kind: `network`, `http`,
`validation`, `storage`, `crypto`, or `unknown`). `UseCase.run()` catches it and returns
`fail(error)`. The ViewModel reads `result.ok` and sets the appropriate signal. No `try/catch`
appears above `UseCase.run()`.

**Walkthrough (`user-management`):**

1. `UserManagementPage` injects `UsersViewModel` and renders from its signals (`vm.loading()`,
   `vm.users()`). Page initialisation calls `vm.load()`.
2. `UsersViewModel` sets `loading = true`, calls `listUsersUseCase.run()`, then switches on
   the `Result` to update `users` (on success) or call `notify.error(r.error.message)` via
   `NotificationService` (on failure) — no `error` signal, `users` list is not cleared.
   No try/catch — the base already normalised errors.
3. `ListUsersUseCase` injects the repository via `inject(USERS_REPOSITORY)`, calls
   `repo.list()`, validates each DTO (`isUserDtoRsValid`), maps to `User[]` via `toUser`, and
   returns it. Throws `AppError('Invalid user data received', 'validation')` on a bad payload.
4. `UsersRepositoryImpl` calls `http.get<UsersDtoRs>('/api/users')` and returns the envelope
   as-is — fetch-only, no unwrapping or mapping. It is bound to `USERS_REPOSITORY` via
   `USERS_PROVIDERS` spread into `app.config.ts`.

### Two repository-port patterns

This codebase intentionally supports two distinct patterns, mirroring the mobile base:

**1. Remote / untrusted data** (example: `user-management`)
- The port returns the transport **DTO**: `Promise<UsersDtoRs>` (a `BaseResponseRs` envelope)
- The **use case** validates each DTO (`isUserDtoRsValid`) and maps to the domain model
  (`toUser`). Validation lives at the use-case boundary because HTTP data is untrusted.
- Copy this pattern when the data source is an external API.

**2. Local / trusted data** (no feature currently uses this pattern)
- The port returns the **domain model** directly: `Promise<MyModel | null>`
- The repository implementation serialises/deserialises via `KeyValueStore`. No DTO layer or
  separate validation step is needed because the data was written by the same app.
- Copy this pattern when the data source is `localStorage`, IndexedDB, or another local store.

---

## 3. Folder Structure

```
src/
  main.ts                          # bootstrapApplication — entry point
  index.html
  styles.scss                      # global resets; imports theme.scss
  app/
    app.ts                         # class App — root shell (<router-outlet>)
    app.config.ts                  # appConfig — composition root (...AUTH_PROVIDERS, ...USERS_PROVIDERS)
    app.routes.ts                  # routes — route table (lazy loadComponent)
    layout/
      layout.component.ts          # LayoutComponent — app shell with sidebar nav + <router-outlet>
      layout.component.spec.ts
    core/
      config/
        env.ts                     # API base URL, timeout, retries, backoff
      crypto/
        crypto.service.ts          # AES-256-GCM encrypt/decrypt
        crypto.service.spec.ts
        types.ts                   # CryptoLike / KeyProvider DI seams
        web-crypto-key-store.ts    # IndexedDB-backed non-extractable CryptoKey
        index.ts
      datasource/keyvalue/
        key-value.store.ts         # typed localStorage wrapper (async API)
        key-value.store.spec.ts
        storage-keys.ts            # key name registry
      domain/
        errors/app-error.ts        # AppError + AppErrorKind
        errors/app-error.spec.ts
        result/result.ts           # Result<T>, ok(), fail()
        result/result.spec.ts
        roles/user-role.ts         # UserRole = 'admin'|'manager'|'moqawel'|'worker'
        roles/role-labels.ts       # ROLE_LABELS record
        roles/index.ts
        usecase/use-case.ts        # abstract UseCase<I,O> base
        usecase/use-case.spec.ts
      logging/
        logger.ts                  # createLogger(tag, name) → Logger
        logger.spec.ts
      network/api/
        base-response-rs.ts        # BaseResponseRs<T> envelope
        http-client.ts             # HttpClientService wraps Angular HttpClient
        http-client.spec.ts
        http-error.ts              # HttpErrorKind helpers
      ui/
        components/
          decor-background.component.ts
          decor-background.component.spec.ts
          nav-button.component.ts
          nav-button.component.spec.ts
          text-field.component.ts
          text-field.component.spec.ts
          notification-host.component.ts
        notification.service.ts    # toast service (replaces mobile Alert.alert)
        notification.service.spec.ts
        theme/
          theme.ts                 # design tokens (TypeScript constants)
          theme.scss               # CSS custom properties
    features/
      auth/
        data/
          dto/auth.dto.ts          # SessionDtoRs/CurrentUserDtoRs + envelopes + DtoRq types + validators
          repositories/auth.repository.impl.ts   # AuthRepositoryImpl — fetch-only
          auth.interceptor.ts      # Bearer token attachment; 401→refresh→retry
          auth.providers.ts        # AUTH_PROVIDERS: AUTH_REPOSITORY → AuthRepositoryImpl
          token-store.ts           # TokenStore — access/refresh tokens in KeyValueStore
        domain/
          model/auth.ts            # AuthSession/AuthTokens/AuthPrincipal + toAuth* mappers
          repositories/auth.repository.ts   # IAuthRepository + AUTH_REPOSITORY token
          usecases/login.use-case.ts
          usecases/logout.use-case.ts
          usecases/refresh-token.use-case.ts
          usecases/change-password.use-case.ts
          usecases/load-current-user.use-case.ts
        presentation/
          auth-session.store.ts    # AuthSessionStore — session signals; rehydrates via LoadCurrentUserUseCase
          auth.guard.ts            # authGuard + roleGuard(role)
          pages/login.page.ts
          pages/account.page.ts
          pages/change-password.page.ts
          viewmodels/login.viewmodel.ts
          viewmodels/change-password.viewmodel.ts
        index.ts
      home/
        presentation/pages/
          home.page.ts
          home.page.spec.ts
        index.ts
      user-management/
        data/
          dto/user.dto.ts          # UserDtoRs + envelopes (UserItemDtoRs/UsersDtoRs) + DtoRq types + isUserDtoRsValid
          repositories/users.repository.impl.ts   # UsersRepositoryImpl — fetch-only
          users.providers.ts       # USERS_PROVIDERS: USERS_REPOSITORY → UsersRepositoryImpl
        domain/
          model/user.ts            # User (UserRole/UserStatus) + toUser mapper
          repositories/users.repository.ts   # IUsersRepository + USERS_REPOSITORY token
          usecases/list-users.use-case.ts
          usecases/create-user.use-case.ts
          usecases/update-user.use-case.ts
          usecases/set-user-status.use-case.ts
        presentation/
          pages/user-management.page.ts
          viewmodels/users.viewmodel.ts
        index.ts
tsconfig.json                      # paths: @core/*, @features/*
jest.config.js
setup-jest.ts
docs/
  architecture/README.md           # this file
  superpowers/specs/               # approved design specs
  superpowers/plans/               # implementation plans
```

---

## 4. Mobile ↔ Angular Mapping

| Mobile (React Native)              | Angular                                                       |
| ---------------------------------- | ------------------------------------------------------------- |
| `index.js`                         | `src/main.ts` (`bootstrapApplication`)                        |
| `App.tsx`                          | `src/app/app.ts` (class `App`, root shell, `<router-outlet>`) |
| `providers/RootProviders.tsx`      | `src/app/app.config.ts` (`appConfig` — `provideRouter`, `provideHttpClient`, etc.) |
| `RootNavigator.tsx` + `types.ts`   | `src/app/app.routes.ts` (typed `Routes` array, lazy `loadComponent`) |

Both projects share the same feature taxonomy and naming. The only structural differences are:

- Ports (repository interfaces) become `InjectionToken`s to survive the TypeScript erasure.
- Filenames are kebab-case per Angular convention; class names are unchanged.
- The ViewModel is an `@Injectable` service (not a React hook), but it exposes the same
  surface: `loading`, `users`/`data` signals and a `load()` method. (In `UsersViewModel`,
  failures are routed through `NotificationService` rather than an `error` signal.)

---

## 5. Naming Conventions

### Files

Angular kebab-case with a type suffix:

| Layer            | Filename pattern                              | Example                               |
| ---------------- | --------------------------------------------- | ------------------------------------- |
| DTO              | `<entity>.dto.ts`                             | `user.dto.ts`                         |
| Domain model     | `<entity>.ts`                                 | `user.ts`                             |
| Port (interface) | `domain/repositories/<entity>.repository.ts`  | `users.repository.ts`                 |
| Repository impl  | `data/repositories/<entity>.repository.impl.ts` | `users.repository.impl.ts`          |
| Providers        | `data/<feature>.providers.ts`                 | `users.providers.ts`                  |
| Use case         | `<verb>-<entity>.use-case.ts`                 | `list-users.use-case.ts`              |
| ViewModel        | `<entity>.viewmodel.ts`                       | `users.viewmodel.ts`                  |
| Page component   | `<name>.page.ts`                              | `user-management.page.ts`             |
| Public barrel    | `index.ts`                                    | `features/user-management/index.ts`   |

### Classes and types

Class and type names mirror the mobile base exactly so the two repos read as siblings:

| Concept           | Pattern               | Example                       |
| ----------------- | --------------------- | ----------------------------- |
| Response DTO      | `…DtoRs`              | `UserDtoRs`, `UsersDtoRs`     |
| Request DTO       | `…DtoRq`              | `CreateUserDtoRq`             |
| Domain model      | PascalCase noun       | `User`, `AuthSession`         |
| Port interface    | `I…Repository`        | `IUsersRepository`            |
| DI token          | `…_REPOSITORY`        | `USERS_REPOSITORY`            |
| Repository impl   | `…RepositoryImpl`     | `UsersRepositoryImpl`         |
| Providers array   | `…_PROVIDERS`         | `USERS_PROVIDERS`             |
| Use case          | `…UseCase`            | `ListUsersUseCase`            |
| ViewModel         | `…ViewModel`          | `UsersViewModel`              |
| Page component    | `…Page`               | `UserManagementPage`          |

### Rule of thumb

- DTOs live in `data/dto/` and are never imported by `presentation/`.
- The port interface + DI token live together in `domain/repositories/<entity>.repository.ts`.
- The concrete implementation lives in `data/repositories/<entity>.repository.impl.ts`.
- The providers array lives in `data/<feature>.providers.ts` and is spread into `app.config.ts`.
- Only the `index.ts` barrel is imported from outside the feature folder.

---

## 6. How to Add a New Feature

The steps below follow the `user-management` slice exactly. Substitute your entity name
wherever `User`/`user` appears.

### 1. Create the folder scaffold

```
src/app/features/<feature>/
  data/
    dto/
    repositories/
  domain/
    model/
    repositories/
    usecases/
  presentation/
    pages/
    viewmodels/
  index.ts
```

### 2. Add the response DTO + validator (`data/dto/<entity>.dto.ts`)

```ts
import type { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface MyEntityDtoRs { id: string; name: string; /* ... */ }
export interface MyEntityItemDtoRs extends BaseResponseRs<MyEntityDtoRs> {}
export interface MyEntitiesDtoRs extends BaseResponseRs<MyEntityDtoRs[]> {}

export function isMyEntityDtoRsValid(dto: MyEntityDtoRs): boolean {
  return typeof dto.id === 'string' && dto.id.length > 0 &&
         typeof dto.name === 'string';
}
```

Add `…DtoRq` types only if the endpoint takes a request body.

### 3. Add the domain model + mapper (`domain/model/<entity>.ts`)

```ts
export interface MyEntity { id: string; name: string; }
export const toMyEntity = (dto: MyEntityDtoRs): MyEntity => ({ id: dto.id, name: dto.name });
```

### 4. Add the port interface + DI token (`domain/repositories/<entity>.repository.ts`)

```ts
export interface IMyEntityRepository {
  getEntities(): Promise<MyEntitiesDtoRs>;
}
export const MY_ENTITY_REPOSITORY = new InjectionToken<IMyEntityRepository>('MY_ENTITY_REPOSITORY');
```

### 5. Add the repository implementation (`data/repositories/<entity>.repository.impl.ts`)

The implementation is **fetch-only**: it returns the raw `BaseResponseRs` envelope. No
unwrapping or mapping. It is `providedIn: 'root'` so the DI token binding in the providers
file (step 6) is the only wiring needed.

```ts
@Injectable({ providedIn: 'root' })
export class MyEntityRepositoryImpl implements IMyEntityRepository {
  private readonly http = inject(HttpClientService);
  getEntities(): Promise<MyEntitiesDtoRs> { return this.http.get<MyEntitiesDtoRs>('/my-entities'); }
}
```

### 6. Add the providers file and register globally (`data/<feature>.providers.ts`)

```ts
// data/<feature>.providers.ts
export const MY_FEATURE_PROVIDERS: Provider[] = [
  { provide: MY_ENTITY_REPOSITORY, useClass: MyEntityRepositoryImpl },
];
```

Then spread it into `app.config.ts` alongside the existing feature providers:

```ts
// app.config.ts (excerpt)
providers: [
  provideRouter(routes),
  provideHttpClient(withInterceptors([authInterceptor])),
  provideAnimations(),
  ...AUTH_PROVIDERS,
  ...USERS_PROVIDERS,
  ...MY_FEATURE_PROVIDERS,   // ← add here
],
```

### 7. Add the use case (`domain/usecases/<verb>-<entity>.use-case.ts`), extending `UseCase`

```ts
@Injectable({ providedIn: 'root' })
export class GetMyEntitiesUseCase extends UseCase<void, MyEntity[]> {
  private readonly repo = inject(MY_ENTITY_REPOSITORY);
  constructor() { super('GetMyEntities'); }

  protected async execute(): Promise<MyEntity[]> {
    const res = await this.repo.getEntities();
    if (!res.data.every(isMyEntityDtoRsValid)) {
      throw new AppError('Invalid data received', 'validation');
    }
    return res.data.map(toMyEntity);
  }
}
```

### 8. Add the ViewModel (`presentation/viewmodels/<entity>.viewmodel.ts`)

```ts
@Injectable()
export class MyFeatureViewModel {
  private readonly useCase = inject(GetMyEntitiesUseCase);
  private readonly notify  = inject(NotificationService);

  readonly loading = signal(false);
  readonly items   = signal<MyEntity[]>([]);

  async loadItems(): Promise<void> {
    this.loading.set(true);
    const result = await this.useCase.run();
    this.loading.set(false);
    if (result.ok) this.items.set(result.data);
    else this.notify.error(result.error.message);   // toast via NotificationService
  }
}
```

Important: on failure, **surface the error via `NotificationService.error(...)`** — do not
add an `error` signal and do not clear the data signal. Stale data remains visible until
the next successful load.

### 9. Add the page component (`presentation/pages/<name>.page.ts`)

```ts
@Component({ selector: 'app-my-feature-page', standalone: true, /* ... */ })
export class MyFeaturePage {
  readonly vm = inject(MyFeatureViewModel);

  onLoad(): void { void this.vm.loadItems(); }
}
```

### 10. Export from the barrel (`index.ts`)

```ts
export { MyFeaturePage } from './presentation/pages/my-feature.page';
export { MyFeatureViewModel } from './presentation/viewmodels/my-feature.viewmodel';
export { GetMyEntitiesUseCase } from './domain/usecases/get-my-entities.use-case';
export { MY_ENTITY_REPOSITORY } from './domain/repositories/my-entity.repository';
export type { IMyEntityRepository } from './domain/repositories/my-entity.repository';
export { MyEntityRepositoryImpl } from './data/repositories/my-entity.repository.impl';
export { MY_FEATURE_PROVIDERS } from './data/my-feature.providers';
```

### 11. Wire the route (`app.routes.ts`)

```ts
{
  path: 'my-feature',
  loadComponent: () => import('@features/my-feature').then(m => m.MyFeaturePage),
  providers: [MyFeatureViewModel],   // ViewModel only — impl + use case are providedIn:'root'
}
```

The ViewModel is route-scoped so each route activation gets a fresh instance with empty
signals. The repository impl and use cases are already `providedIn: 'root'`; the token→impl
binding is handled by the global `...MY_FEATURE_PROVIDERS` in `app.config.ts`.

### 12. Add tests

**Use-case test** — bind a fake repository to the port token and assert on the `Result`:

```ts
TestBed.configureTestingModule({
  providers: [
    { provide: MY_ENTITY_REPOSITORY, useValue: fakeRepo },
  ],
});
const uc = TestBed.inject(GetMyEntitiesUseCase);
const r = await uc.run();
expect(r.ok).toBe(true);
```

**ViewModel test** — stub the use case's `run` method and assert on signal transitions:

```ts
TestBed.configureTestingModule({
  providers: [
    MyFeatureViewModel,
    { provide: GetMyEntitiesUseCase, useValue: { run: async () => ok([/* ... */]) } },
  ],
});
const vm = TestBed.inject(MyFeatureViewModel);
await vm.loadItems();
expect(vm.loading()).toBe(false);
expect(vm.items().length).toBeGreaterThan(0);
```

See `users.use-cases.spec.ts` and `users.viewmodel.spec.ts` for full examples.

---

## 7. Testing

Run the full suite:

```sh
npm test
```

Jest is configured via `jest-preset-angular` (`jest.config.js` + `setup-jest.ts`). Path
aliases (`@core/*`, `@features/*`) are mapped in the Jest config to match `tsconfig.json`.

### What each layer covers

| Layer / file                                          | What the tests verify                                                                              |
| ----------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| `core/domain/result/result.spec.ts`                   | `ok()` and `fail()` constructors; type narrowing via `.ok`.                                        |
| `core/domain/usecase/use-case.spec.ts`                | Success path returns `ok(data)`; thrown `AppError` returns `fail(error)` with correct kind; non-`AppError` normalised to `'unknown'`. |
| `core/domain/errors/app-error.spec.ts`                | `AppError` carries kind, status, and code; is a real `Error` instance.                            |
| `core/logging/logger.spec.ts`                         | Logger does not throw; levels propagate correctly.                                                 |
| `core/network/api/http-client.spec.ts`                | Transport failure → `AppError('network')`; non-2xx → `AppError('http', status)`; timeout maps to `network`; retry count is respected. Uses `HttpClientTestingModule`. |
| `core/datasource/keyvalue/key-value.store.spec.ts`    | Round-trip set/get; missing key returns `null` / fallback; corrupt object throws `AppError('storage')`; corrupt primitive falls back silently. |
| `core/crypto/crypto.service.spec.ts`                  | Encrypt → decrypt round-trip; tampered ciphertext throws `AppError('crypto')`. Uses Node's `webcrypto` and an in-memory `KeyProvider`. |
| `core/ui/components/nav-button.component.spec.ts`     | Renders label; emits `press` on click; disabled state prevents emit.                               |
| `core/ui/components/text-field.component.spec.ts`     | Renders label; updates bound signal on input.                                                      |
| `core/ui/components/decor-background.component.spec.ts` | Defaults to the `page` variant; switches to the `auth` variant when set.                        |
| `core/ui/notification.service.spec.ts`                | `success()` / `error()` enqueue the correct notification type.                                     |
| `app.routes.spec.ts`                                  | Login route lives outside the guarded shell; shell contains `home`, `account`, `change-password`; `user-management` is a role-guarded child with a ViewModel provider. |
| `auth/data/dto/auth.dto.spec.ts`                      | `isSessionDtoRsValid` / `isCurrentUserDtoRsValid` accept well-formed DTOs; reject empty tokens and unknown roles. |
| `auth/data/token-store.spec.ts`                       | Saves, reads, and clears access + refresh tokens via `KeyValueStore`.                              |
| `auth/data/repositories/auth.repository.impl.spec.ts` | Each method delegates to `HttpClientService` and returns the raw envelope.                        |
| `auth/data/auth.interceptor.spec.ts`                  | Attaches Bearer token; 401 triggers refresh-then-retry; clears tokens and redirects on refresh failure; bypasses `/api/auth/{refresh,login,logout}` endpoints. |
| `auth/domain/model/auth.spec.ts`                      | `toAuthSession` / `toAuthTokens` / `toAuthPrincipal` map DTOs to domain models correctly.        |
| `auth/domain/usecases/login.use-case.spec.ts`         | Valid session DTO → persists tokens, returns `AuthSession`; invalid DTO → `fail('validation')`.  |
| `auth/domain/usecases/logout.use-case.spec.ts`        | Calls repository `logout` and clears `TokenStore`.                                                |
| `auth/domain/usecases/refresh-token.use-case.spec.ts` | Valid refresh DTO → rotates tokens in `TokenStore`; invalid DTO → `fail('validation')`.          |
| `auth/domain/usecases/change-password.use-case.spec.ts` | Valid response → rotates tokens; invalid DTO → `fail('validation')`.                           |
| `auth/domain/usecases/load-current-user.use-case.spec.ts` | Valid DTO → returns `AuthPrincipal`; invalid DTO → `fail('validation')`.                    |
| `auth/presentation/auth-session.store.spec.ts`        | `signIn` / `signOut` / `changePassword` drive the session signals; rehydrates on init via `LoadCurrentUserUseCase`. |
| `auth/presentation/auth.guard.spec.ts`                | `authGuard` allows authenticated users, redirects others to `/login`; `roleGuard('admin')` allows only the matching role. |
| `auth/presentation/viewmodels/login.viewmodel.spec.ts` | Navigates to `/home` on success; to `/change-password` when `mustChangePassword`; sets error on failure. |
| `auth/presentation/viewmodels/change-password.viewmodel.spec.ts` | Blocks submit when confirmation does not match; navigates to `/home` on success; sets error on failure. |
| `home/presentation/pages/home.page.spec.ts`           | Home page renders navigation links.                                                                |
| `user-management/data/dto/user.dto.spec.ts`           | `isUserDtoRsValid` accepts well-formed DTOs; rejects unknown role, unknown status, empty id, and roles that collide with object prototype properties. |
| `user-management/data/repositories/users.repository.impl.spec.ts` | `list`, `create`, `update`, `setStatus` delegate to `HttpClientService` and return the raw envelope. |
| `user-management/domain/model/user.spec.ts`           | `toUser` maps a `UserDtoRs` to a domain `User`.                                                   |
| `user-management/domain/usecases/users.use-cases.spec.ts` | `ListUsersUseCase` validates + maps DTOs; invalid DTO → `fail('validation')`; same pattern verified for `CreateUserUseCase`, `UpdateUserUseCase`, `SetUserStatusUseCase`. |
| `user-management/presentation/viewmodels/users.viewmodel.spec.ts` | `load()` populates the `users` signal; CRUD methods call the correct use case and show success / error notifications. |
| `layout/layout.component.spec.ts`                     | Shows the user-management nav link for `admin`; hides it for non-admin roles.                     |

Component-level tests are light smoke tests — confirming render and basic interaction — which
matches the pragmatic depth of the mobile base.

---

## 8. Links

The documents below are the canonical references for this project's architecture decisions and design system.

- **Design spec:** [`../superpowers/specs/2026-06-26-angular-base-design.md`](../superpowers/specs/2026-06-26-angular-base-design.md) — the approved architecture decision record; source of truth for all patterns documented here.
