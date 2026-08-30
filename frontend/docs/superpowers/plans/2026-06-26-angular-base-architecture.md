# Angular Frontend Base Architecture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an Angular web base architecture that is the faithful counterpart of the React Native mobile base at `C:\Users\envnt\Desktop\BaseProject` — Clean Architecture + MVVM, feature-first, with the same `Result`/`AppError`/`UseCase`/repository-port/ViewModel taxonomy.

**Architecture:** Standalone Angular (no NgModules), Signals for presentation state, Promise + `Result<T>` through the domain. `core/` holds cross-cutting infra (errors, result, use-case base, logger, env, http client, key-value store, crypto, theme, shared UI); `features/<group>/<feature>/{data,domain,presentation}` are vertical slices wired via route-level DI. Web-specific primitives (`HttpClient` wrapper, `localStorage`, Web Crypto) preserve the mobile contracts.

**Tech Stack:** Angular v20 (standalone + signals), TypeScript (strict), SCSS, Angular Router, `HttpClient`, Web Crypto (`SubtleCrypto`) + IndexedDB, `localStorage`, Jest via `jest-preset-angular`.

## Global Constraints

- **Angular:** v20 (latest stable), standalone components only, bootstrapped via `bootstrapApplication`. No NgModules. Zone.js enabled (default).
- **State:** Signals at the presentation edge; Promise + `Result<T>` through repositories and use cases. No global state library (NgRx etc.).
- **Project name:** `base-frontend`. **Location:** `C:\Users\envnt\Desktop\Base Frontend` (in-place; `docs/` and `.git` already exist and MUST survive).
- **Path aliases:** `@core/*` → `src/app/core/*`, `@features/*` → `src/app/features/*` (in both `tsconfig.json` and `jest.config.js`).
- **Filenames:** Angular kebab-case with type suffix (`get-posts.use-case.ts`, `post.repository.ts`, `api.page.ts`). **Class/type names** stay aligned with the mobile base (`GetPostsUseCase`, `PostRepositoryImpl`, `Post`, `PostDtoRs`).
- **DTO naming:** response DTOs suffixed `…DtoRs`, request DTOs `…DtoRq` (only when an endpoint carries a body).
- **Ports:** every `I…Repository` interface is paired with a `…_REPOSITORY` `InjectionToken`; the impl is bound in the feature's route-level `providers`.
- **AppError kinds (exact union):** `'network' | 'http' | 'validation' | 'storage' | 'crypto' | 'unknown'`.
- **Comments:** match the mobile base's didactic comment style — each file opens with a short comment explaining its responsibility; non-obvious decisions get an inline note.
- **Testing:** Jest only (`npm test`). Framework-agnostic core gets thorough unit tests; feature slices get a use-case + ViewModel test; components get light smoke tests.
- **Commits:** one commit per task (or per TDD cycle within a task). End every commit message with the trailer:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

---

## File Structure

```
src/
  main.ts                                   # bootstrapApplication(AppComponent, appConfig)
  index.html, styles.scss
  app/
    app.component.ts                        # root shell: <router-outlet> + notification host
    app.config.ts                          # providers: router, http, animations
    app.routes.ts                          # route table + feature-scoped providers
    core/
      config/env.ts                        # typed env + tuning object
      crypto/
        types.ts                           # WebCryptoLike + KeyProvider seams
        crypto.service.ts                  # AES-GCM via SubtleCrypto (DI seams)
        web-crypto-key-store.ts            # production KeyProvider: non-extractable key in IndexedDB
        index.ts                           # production composition (cryptoService singleton)
      datasource/keyvalue/
        key-value.store.ts                 # typed async store over localStorage
        storage-keys.ts                    # key registry
      domain/
        errors/app-error.ts                # AppError
        result/result.ts                   # Result<T>, ok(), fail()
        usecase/use-case.ts                # UseCase<I,O> base
      logging/logger.ts                    # layer/level-aware logger
      network/api/
        base-response-rs.ts                # BaseResponseRs<T> envelope
        http-client.ts                     # HttpClientService.request<T>() → Promise
      ui/
        components/
          nav-button.component.ts          # themed button
          text-field.component.ts          # labeled input (model signal)
          notification-host.component.ts   # renders NotificationService.current
        notification.service.ts            # toast/snackbar state (signal)
        theme/theme.ts                     # design tokens (TS)
        theme/theme.scss                   # tokens as CSS variables
    features/
      home/
        presentation/pages/home.page.ts
        index.ts
      api/fetch-posts/
        data/dto/post-dto-rs.ts
        data/repositories/post.repository.ts
        domain/model/post.ts
        domain/repositories/post.repository.port.ts
        domain/usecases/get-posts.use-case.ts
        presentation/pages/api.page.ts
        presentation/viewmodels/fetch-posts.viewmodel.ts
        index.ts
      offline-storage/
        data/repositories/profile-storage.repository.ts
        domain/model/saved-profile.ts
        domain/repositories/profile-storage.repository.port.ts
        domain/usecases/save-profile.use-case.ts
        domain/usecases/load-profile.use-case.ts
        presentation/pages/offline-storage.page.ts
        presentation/pages/saving-data.page.ts
        presentation/pages/retrieved-data.page.ts
        presentation/viewmodels/save-profile.viewmodel.ts
        presentation/viewmodels/retrieve-profile.viewmodel.ts
        index.ts
      design/
        profile-form/
          presentation/profile-form.store.ts
          presentation/pages/profile-form.page.ts
          presentation/pages/profile-summary.page.ts
          index.ts
        design-hub/
          presentation/components/sidebar-nav.component.ts
          presentation/pages/design-hub.page.ts
          presentation/pages/animation-demo.page.ts
          index.ts
tsconfig.json                              # paths aliases
tsconfig.spec.json                         # jest types
jest.config.js, setup-jest.ts
package.json, angular.json
docs/                                       # specs, plans, architecture guide (pre-existing + new)
```

---

## Task 1: Scaffold the Angular project + tooling (in-place)

**Files:**
- Create: the full Angular project tree under `C:\Users\envnt\Desktop\Base Frontend`
- Create: `jest.config.js`, `setup-jest.ts`
- Modify: `package.json` (scripts + deps), `tsconfig.json` (aliases), `tsconfig.spec.json` (jest types), `angular.json` (style=scss already), `src/styles.scss`
- Delete: `src/app/app.component.spec.ts` (karma sample), `karma.conf.js` if present

**Interfaces:**
- Produces: a buildable Angular app; `npm test` runs Jest; aliases `@core/*` and `@features/*` resolve in both TS and Jest. Root component is `AppComponent` (selector `app-root`).

- [ ] **Step 1: Confirm Node/npm and record the directory contents**

Run:
```powershell
node --version; npm --version; npx --version
Get-ChildItem -Force "C:\Users\envnt\Desktop\Base Frontend" | Select-Object Name
```
Expected: Node 20.11+/22+ prints versions. The folder shows `.git` and `docs` only. If other files exist, report before continuing.

- [ ] **Step 2: Scaffold into a temp subfolder, then move files up (preserving `.git` + `docs`)**

`ng new` refuses a non-empty target, so scaffold into a temp folder and move the contents up.

Run:
```powershell
cd "C:\Users\envnt\Desktop\Base Frontend"
npx -p @angular/cli@20 ng new base-frontend --directory ".__ng_tmp" --style=scss --routing=true --ssr=false --skip-git --defaults
Get-ChildItem -Force ".__ng_tmp" | Where-Object { $_.Name -ne ".git" } | ForEach-Object { Move-Item -Path $_.FullName -Destination "." -Force }
Remove-Item -Recurse -Force ".__ng_tmp"
```
Expected: Angular files (`src/`, `package.json`, `angular.json`, `tsconfig.json`, …) now sit at the root alongside `.git` and `docs`. `.__ng_tmp` is gone.

- [ ] **Step 3: Verify the scaffold and that docs survived**

Run:
```powershell
cd "C:\Users\envnt\Desktop\Base Frontend"
@("src/main.ts","src/app/app.component.ts","angular.json","package.json","tsconfig.json") | ForEach-Object { "{0}`t{1}" -f $_, (Test-Path $_) }
Test-Path "docs/superpowers/specs/2026-06-26-angular-base-design.md"
```
Expected: every path prints `True`.

- [ ] **Step 4: Add path aliases to `tsconfig.json`**

Add `baseUrl` + `paths` to `compilerOptions` (keep the rest of the generated file):
```jsonc
// tsconfig.json — compilerOptions additions
"baseUrl": "./",
"paths": {
  "@core/*": ["src/app/core/*"],
  "@features/*": ["src/app/features/*"]
}
```

- [ ] **Step 5: Install Jest and configure it**

Run:
```powershell
cd "C:\Users\envnt\Desktop\Base Frontend"
npm install --save-dev jest @types/jest jest-preset-angular @jest/globals
```
Expected: installs without errors.

Create `jest.config.js`:
```js
// jest.config.js — Jest via jest-preset-angular (CommonJS preset).
const { createCjsPreset } = require('jest-preset-angular/presets');

module.exports = {
  ...createCjsPreset(),
  setupFilesAfterEnv: ['<rootDir>/setup-jest.ts'],
  testPathIgnorePatterns: ['<rootDir>/node_modules/', '<rootDir>/dist/'],
  moduleNameMapper: {
    '^@core/(.*)$': '<rootDir>/src/app/core/$1',
    '^@features/(.*)$': '<rootDir>/src/app/features/$1',
  },
};
```

Create `setup-jest.ts`:
```ts
// setup-jest.ts — initialise the Angular TestBed zone environment for Jest.
import { setupZoneTestEnv } from 'jest-preset-angular/setup-env/zone';

setupZoneTestEnv();
```

- [ ] **Step 6: Point `tsconfig.spec.json` at Jest types**

Replace `tsconfig.spec.json` with:
```json
{
  "extends": "./tsconfig.json",
  "compilerOptions": {
    "outDir": "./out-tsc/spec",
    "types": ["jest", "node"]
  },
  "include": ["src/**/*.spec.ts", "src/**/*.d.ts", "setup-jest.ts"]
}
```

- [ ] **Step 7: Replace the `test` script and remove the Karma sample**

In `package.json`, set scripts (keep `build`, `start`, `ng`):
```jsonc
"scripts": {
  "ng": "ng",
  "start": "ng serve",
  "build": "ng build",
  "watch": "ng build --watch --configuration development",
  "test": "jest"
}
```
Delete the Karma sample test and config if present:
```powershell
Remove-Item -Force "src/app/app.component.spec.ts" -ErrorAction SilentlyContinue
Remove-Item -Force "karma.conf.js" -ErrorAction SilentlyContinue
```

- [ ] **Step 8: Add a sanity test to validate the Jest pipeline**

Create `src/app/core/_sanity.spec.ts`:
```ts
// Temporary: proves the Jest + TS + alias pipeline works. Deleted in Step 11.
describe('jest pipeline', () => {
  it('runs TypeScript tests', () => {
    expect(1 + 1).toBe(2);
  });
});
```

- [ ] **Step 9: Run the sanity test**

Run: `npm test`
Expected: PASS — 1 test passed. If Jest fails to parse Angular/rxjs ESM, add to `jest.config.js`:
`transformIgnorePatterns: ['node_modules/(?!(?:.*\\.mjs$|@angular|rxjs|tslib))']` and re-run.

- [ ] **Step 10: Verify the app still builds**

Run: `npm run build`
Expected: build succeeds (a `dist/` is produced). If it fails, fix before continuing.

- [ ] **Step 11: Configure git identity, remove the sanity test, and commit**

Run:
```powershell
cd "C:\Users\envnt\Desktop\Base Frontend"
git config user.name "Claude"
git config user.email "bogdev@codelabsys.com"
Remove-Item -Force "src/app/core/_sanity.spec.ts"
git add -A
git commit -m "chore: scaffold Angular base-frontend with Jest, SCSS, routing, path aliases

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
Expected: commit succeeds; `node_modules/` is excluded by the generated `.gitignore`.

---

## Task 2: Core domain — `Result` + `AppError`

**Files:**
- Create: `src/app/core/domain/errors/app-error.ts`
- Create: `src/app/core/domain/result/result.ts`
- Test: `src/app/core/domain/result/result.spec.ts`

**Interfaces:**
- Produces:
  - `class AppError extends Error { readonly kind: AppErrorKind; readonly status?: number; constructor(message: string, kind: AppErrorKind, status?: number) }`
  - `type AppErrorKind = 'network'|'http'|'validation'|'storage'|'crypto'|'unknown'`
  - `type Result<T> = {ok:true; data:T} | {ok:false; error:AppError}`
  - `const ok: <T>(data:T) => Result<T>`; `const fail: (error:AppError) => Result<never>`

- [ ] **Step 1: Write the failing test**

`src/app/core/domain/result/result.spec.ts`:
```ts
import { AppError } from '@core/domain/errors/app-error';
import { ok, fail, Result } from '@core/domain/result/result';

describe('Result', () => {
  it('ok() wraps data with ok=true', () => {
    const r: Result<number> = ok(42);
    expect(r).toEqual({ ok: true, data: 42 });
  });

  it('fail() wraps an AppError with ok=false', () => {
    const err = new AppError('boom', 'network');
    const r = fail(err);
    expect(r.ok).toBe(false);
    if (!r.ok) {
      expect(r.error).toBe(err);
      expect(r.error.kind).toBe('network');
    }
  });

  it('AppError carries kind and optional status', () => {
    const err = new AppError('HTTP 404', 'http', 404);
    expect(err).toBeInstanceOf(Error);
    expect(err.name).toBe('AppError');
    expect(err.kind).toBe('http');
    expect(err.status).toBe(404);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test -- result.spec`
Expected: FAIL — cannot find module `@core/domain/errors/app-error`.

- [ ] **Step 3: Implement `AppError`**

`src/app/core/domain/errors/app-error.ts`:
```ts
// AppError: a typed error carrying the failure kind (and HTTP status when known).
export type AppErrorKind =
  | 'network'
  | 'http'
  | 'validation'
  | 'storage'
  | 'crypto'
  | 'unknown';

export class AppError extends Error {
  readonly kind: AppErrorKind;
  readonly status?: number;

  constructor(message: string, kind: AppErrorKind, status?: number) {
    super(message);
    this.name = 'AppError';
    this.kind = kind;
    this.status = status;
  }
}
```

- [ ] **Step 4: Implement `Result`**

`src/app/core/domain/result/result.ts`:
```ts
// Result: the uniform success/failure value returned by every use case via the
// UseCase base. Callers switch on `ok` instead of writing try/catch themselves.
import { AppError } from '@core/domain/errors/app-error';

export type Result<T> =
  | { ok: true; data: T }
  | { ok: false; error: AppError };

export const ok = <T>(data: T): Result<T> => ({ ok: true, data });
export const fail = (error: AppError): Result<never> => ({ ok: false, error });
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `npm test -- result.spec`
Expected: PASS — 3 tests.

- [ ] **Step 6: Commit**

```bash
git add src/app/core/domain
git commit -m "feat(core): add Result and AppError domain primitives

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: Core — `Logger` + `env`

**Files:**
- Create: `src/app/core/config/env.ts`
- Create: `src/app/core/logging/logger.ts`
- Test: `src/app/core/logging/logger.spec.ts`

**Interfaces:**
- Consumes: nothing from prior tasks.
- Produces:
  - `env` object: `{ name: 'development'|'production'; isDev: boolean; api: { baseUrl: string; timeoutMs: number; maxRetries: number; backoffMs: number[] } }`
  - `type LogLevel = 'debug'|'info'|'warn'|'error'`
  - `type LogLayer = 'page'|'viewmodel'|'usecase'|'repository'|'datasource'|'core'`
  - `type Logger = { debug; info; warn; error: (...args: unknown[]) => void }`
  - `function createLogger(layer: LogLayer, name: string): Logger`
  - `function setLogLevel(level: LogLevel): void`

- [ ] **Step 1: Write the failing test**

`src/app/core/logging/logger.spec.ts`:
```ts
import { createLogger, setLogLevel } from '@core/logging/logger';

describe('Logger', () => {
  afterEach(() => setLogLevel('debug'));

  it('emits through the matching console method with a tagged prefix', () => {
    const spy = jest.spyOn(console, 'warn').mockImplementation(() => {});
    const log = createLogger('usecase', 'GetPosts');
    log.warn('failed');
    expect(spy).toHaveBeenCalledTimes(1);
    expect(String(spy.mock.calls[0][0])).toContain('usecase');
    expect(String(spy.mock.calls[0][0])).toContain('GetPosts');
    spy.mockRestore();
  });

  it('suppresses levels below the active threshold', () => {
    const spy = jest.spyOn(console, 'log').mockImplementation(() => {});
    setLogLevel('warn');
    createLogger('core', 'x').debug('hidden');
    expect(spy).not.toHaveBeenCalled();
    spy.mockRestore();
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test -- logger.spec`
Expected: FAIL — cannot find module `@core/logging/logger`.

- [ ] **Step 3: Implement `env`**

`src/app/core/config/env.ts`:
```ts
// env: the single source of environment + tuning config. A plain typed object.
// isDevMode() is false in optimized production builds, true otherwise (incl. Jest).
import { isDevMode } from '@angular/core';

export type AppEnv = 'development' | 'production';

const isDev = isDevMode();

export const env = {
  name: (isDev ? 'development' : 'production') as AppEnv,
  isDev,
  api: {
    // Per-environment base URL. Replace with real URLs when available.
    baseUrl: isDev
      ? 'https://dummyjson.com' // TODO(dev): dev / staging API
      : 'https://api.example.com', // TODO(prod): production API
    timeoutMs: 10_000,
    maxRetries: 2, // total attempts = 1 + maxRetries
    backoffMs: [500, 1500], // delay before retry 1, retry 2
  },
} as const;
```

- [ ] **Step 4: Implement `logger`**

`src/app/core/logging/logger.ts`:
```ts
// Logger: a layer-aware, level-aware logging utility used app-wide.
// Usage: const log = createLogger('core', 'apiClient'); log.debug('→ GET', url);
import { env } from '@core/config/env';

export type LogLevel = 'debug' | 'info' | 'warn' | 'error';

// The architectural layer a log originates from — lets you trace a flow
// (page → viewmodel → usecase → repository → datasource → core).
export type LogLayer =
  | 'page'
  | 'viewmodel'
  | 'usecase'
  | 'repository'
  | 'datasource'
  | 'core';

export type Logger = {
  debug: (...args: unknown[]) => void;
  info: (...args: unknown[]) => void;
  warn: (...args: unknown[]) => void;
  error: (...args: unknown[]) => void;
};

const PREFIX = 'BOG-Web';

const LAYER_EMOJI: Record<LogLayer, string> = {
  page: '📄',
  viewmodel: '🪟',
  usecase: '🎲',
  repository: '🗄️',
  datasource: '🛰️',
  core: '⚙️',
};

const LEVEL_ORDER: Record<LogLevel, number> = {
  debug: 0,
  info: 1,
  warn: 2,
  error: 3,
};

// Route each level to the matching console method at emit time (not load time)
// so spies/wrappers see the live method.
const SINK: Record<LogLevel, (...args: unknown[]) => void> = {
  debug: (...args) => console.log(...args),
  info: (...args) => console.info(...args),
  warn: (...args) => console.warn(...args),
  error: (...args) => console.error(...args),
};

let minLevel: LogLevel = env.isDev ? 'debug' : 'warn';

export function setLogLevel(level: LogLevel): void {
  minLevel = level;
}

const enabled = (level: LogLevel): boolean =>
  LEVEL_ORDER[level] >= LEVEL_ORDER[minLevel];

export function createLogger(layer: LogLayer, name: string): Logger {
  const tag = `${PREFIX} [${LAYER_EMOJI[layer]} ${layer}] ${name}`;
  const emit =
    (level: LogLevel) =>
    (...args: unknown[]): void => {
      if (enabled(level)) {
        SINK[level](tag, ...args);
      }
    };
  return {
    debug: emit('debug'),
    info: emit('info'),
    warn: emit('warn'),
    error: emit('error'),
  };
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `npm test -- logger.spec`
Expected: PASS — 2 tests.

- [ ] **Step 6: Commit**

```bash
git add src/app/core/config src/app/core/logging
git commit -m "feat(core): add env config and layer-aware Logger

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: Core domain — `UseCase` base

**Files:**
- Create: `src/app/core/domain/usecase/use-case.ts`
- Test: `src/app/core/domain/usecase/use-case.spec.ts`

**Interfaces:**
- Consumes: `AppError` (Task 2), `Result/ok/fail` (Task 2), `createLogger/Logger` (Task 3).
- Produces:
  - `abstract class UseCase<I, O> { protected readonly log: Logger; constructor(name: string); protected abstract execute(input: I): Promise<O>; run(input: I): Promise<Result<O>> }`

- [ ] **Step 1: Write the failing test**

`src/app/core/domain/usecase/use-case.spec.ts`:
```ts
import { AppError } from '@core/domain/errors/app-error';
import { UseCase } from '@core/domain/usecase/use-case';

class Doubler extends UseCase<number, number> {
  constructor() { super('Doubler'); }
  protected async execute(input: number): Promise<number> {
    if (input < 0) throw new AppError('negative', 'validation');
    if (input === 999) throw new Error('boom'); // non-AppError
    return input * 2;
  }
}

describe('UseCase', () => {
  const uc = new Doubler();

  it('returns ok with the execute() result on the happy path', async () => {
    const r = await uc.run(21);
    expect(r).toEqual({ ok: true, data: 42 });
  });

  it('passes a thrown AppError through unchanged (kind preserved)', async () => {
    const r = await uc.run(-1);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });

  it('normalizes a non-AppError throw to kind=unknown', async () => {
    const r = await uc.run(999);
    expect(r.ok).toBe(false);
    if (!r.ok) {
      expect(r.error.kind).toBe('unknown');
      expect(r.error.message).toBe('Unexpected error');
    }
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test -- use-case.spec`
Expected: FAIL — cannot find module `@core/domain/usecase/use-case`.

- [ ] **Step 3: Implement `UseCase`**

`src/app/core/domain/usecase/use-case.ts`:
```ts
// UseCase: the base every use case extends. Subclasses implement only the happy
// path in execute(); run() owns the shared error handling once, so no use case
// repeats the same try/catch.
import { AppError } from '@core/domain/errors/app-error';
import { createLogger, Logger } from '@core/logging/logger';
import { Result, fail, ok } from '@core/domain/result/result';

export abstract class UseCase<I, O> {
  protected readonly log: Logger;

  constructor(name: string) {
    this.log = createLogger('usecase', name);
  }

  // happy path only — throw an AppError to signal failure
  protected abstract execute(input: I): Promise<O>;

  async run(input: I): Promise<Result<O>> {
    this.log.debug('run()');
    try {
      return ok(await this.execute(input));
    } catch (e) {
      const error =
        e instanceof AppError ? e : new AppError('Unexpected error', 'unknown');
      this.log.warn('failed:', error.kind, error.message);
      return fail(error);
    }
  }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm test -- use-case.spec`
Expected: PASS — 3 tests.

- [ ] **Step 5: Commit**

```bash
git add src/app/core/domain/usecase
git commit -m "feat(core): add UseCase base with Result error normalization

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: Core network — `BaseResponseRs` + `http-client`

**Files:**
- Create: `src/app/core/network/api/base-response-rs.ts`
- Create: `src/app/core/network/api/http-client.ts`
- Test: `src/app/core/network/api/http-client.spec.ts`

**Interfaces:**
- Consumes: `env` (Task 3), `AppError` (Task 2), `createLogger` (Task 3).
- Produces:
  - `interface BaseResponseRs<T> { data: T; success?: boolean; message?: string }`
  - `@Injectable({providedIn:'root'}) class HttpClientService { request<T>(path: string): Promise<T> }` — maps non-2xx → `AppError('http', status)`, transport/timeout → `AppError('network')`; retries transport failures only with backoff from `env`.

- [ ] **Step 1: Write the failing test**

`src/app/core/network/api/http-client.spec.ts`:
```ts
import { TestBed, fakeAsync, tick, flushMicrotasks } from '@angular/core/testing';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpClientService } from '@core/network/api/http-client';
import { AppError } from '@core/domain/errors/app-error';

const URL = 'https://dummyjson.com/posts';

describe('HttpClientService', () => {
  let service: HttpClientService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(HttpClientService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('returns the parsed body on success', async () => {
    const promise = service.request<{ posts: number[] }>('/posts');
    httpMock.expectOne(URL).flush({ posts: [1, 2, 3] });
    await expect(promise).resolves.toEqual({ posts: [1, 2, 3] });
    httpMock.verify();
  });

  it('maps a non-2xx response to AppError(http) and does NOT retry', async () => {
    const promise = service.request('/posts').catch((e) => e as AppError);
    httpMock.expectOne(URL).flush('nope', { status: 404, statusText: 'Not Found' });
    const err = await promise;
    expect(err).toBeInstanceOf(AppError);
    expect(err.kind).toBe('http');
    expect(err.status).toBe(404);
    httpMock.verify(); // proves only one request was made
  });

  it('retries transport failures (status 0) with backoff, then maps to network', fakeAsync(() => {
    let captured: AppError | undefined;
    service.request('/posts').catch((e) => (captured = e as AppError));

    httpMock.expectOne(URL).error(new ProgressEvent('error'), { status: 0 });
    tick(500); // backoff before retry 1
    httpMock.expectOne(URL).error(new ProgressEvent('error'), { status: 0 });
    tick(1500); // backoff before retry 2
    httpMock.expectOne(URL).error(new ProgressEvent('error'), { status: 0 });
    flushMicrotasks();

    expect(captured).toBeInstanceOf(AppError);
    expect(captured!.kind).toBe('network');
    httpMock.verify();
  }));
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test -- http-client.spec`
Expected: FAIL — cannot find module `@core/network/api/http-client`.

- [ ] **Step 3: Implement `BaseResponseRs`**

`src/app/core/network/api/base-response-rs.ts`:
```ts
// BaseResponseRs: the minimal response envelope every response DTO inherits.
// `success`/`message` are optional and synthesized at the repository boundary
// until the backend actually sends them.
export interface BaseResponseRs<T> {
  data: T;
  success?: boolean;
  message?: string;
}
```

- [ ] **Step 4: Implement `HttpClientService`**

`src/app/core/network/api/http-client.ts`:
```ts
// HttpClientService: a tiny typed wrapper over Angular HttpClient. Returns a
// Promise (so the Promise-based UseCase layer stays unchanged) and maps failures
// to AppError. Transport failures (status 0) and timeouts are retried with
// exponential backoff; HTTP errors (4xx/5xx) are never retried.
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, timer } from 'rxjs';
import { retry, timeout } from 'rxjs/operators';
import { env } from '@core/config/env';
import { AppError } from '@core/domain/errors/app-error';
import { createLogger } from '@core/logging/logger';

@Injectable({ providedIn: 'root' })
export class HttpClientService {
  private readonly http = inject(HttpClient);
  private readonly log = createLogger('core', 'apiClient');
  private readonly baseUrl = env.api.baseUrl;

  async request<T>(path: string): Promise<T> {
    const url = `${this.baseUrl}${path}`;
    this.log.debug('→ GET', url);
    try {
      return await firstValueFrom(
        this.http.get<T>(url).pipe(
          timeout(env.api.timeoutMs),
          retry({
            count: env.api.maxRetries,
            delay: (error, attempt) => {
              // Retry transport failures only; rethrow HTTP errors to stop retrying.
              if (!this.isTransient(error)) {
                throw error;
              }
              const ms = env.api.backoffMs[attempt - 1] ?? 0;
              this.log.warn(
                `retrying in ${ms}ms (attempt ${attempt + 1}/${env.api.maxRetries + 1})`,
                url,
              );
              return timer(ms);
            },
          }),
        ),
      );
    } catch (err) {
      throw this.toAppError(err, url);
    }
  }

  // A transport failure: HttpErrorResponse with status 0 (network/CORS/DNS), or a
  // non-HTTP error such as the rxjs TimeoutError.
  private isTransient(err: unknown): boolean {
    if (err instanceof HttpErrorResponse) {
      return err.status === 0;
    }
    return true;
  }

  private toAppError(err: unknown, url: string): AppError {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 0) {
        this.log.warn('✗ network failure', url);
        return new AppError(err.message || 'Network request failed', 'network');
      }
      this.log.warn('←', err.status, url);
      return new AppError(`HTTP ${err.status}`, 'http', err.status);
    }
    // rxjs TimeoutError or any other transport-level failure.
    this.log.warn('✗ request failed (timeout/transport)', url);
    return new AppError('Network request failed', 'network');
  }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `npm test -- http-client.spec`
Expected: PASS — 3 tests. If the `fakeAsync` retry test reports leftover timers, ensure `flushMicrotasks()` runs after the final `error()`.

- [ ] **Step 6: Commit**

```bash
git add src/app/core/network
git commit -m "feat(core): add HttpClientService with retry/timeout and AppError mapping

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: Core datasource — `KeyValueStore` + `StorageKeys`

**Files:**
- Create: `src/app/core/datasource/keyvalue/key-value.store.ts`
- Create: `src/app/core/datasource/keyvalue/storage-keys.ts`
- Test: `src/app/core/datasource/keyvalue/key-value.store.spec.ts`

**Interfaces:**
- Consumes: `AppError` (Task 2), `createLogger` (Task 3).
- Produces:
  - `@Injectable({providedIn:'root'}) class KeyValueStore` with: `setString/getString`, `setInt/getInt(key, fallback?)`, `setDouble/getDouble(key, fallback?)`, `setObject<T>/getObject<T>`, `remove`, `clear` — all returning Promises.
  - `const StorageKeys = { profile: '@offline-storage/profile' } as const`

- [ ] **Step 1: Write the failing test**

`src/app/core/datasource/keyvalue/key-value.store.spec.ts`:
```ts
import { KeyValueStore } from '@core/datasource/keyvalue/key-value.store';
import { AppError } from '@core/domain/errors/app-error';

describe('KeyValueStore (localStorage)', () => {
  let store: KeyValueStore;

  beforeEach(() => {
    localStorage.clear();
    store = new KeyValueStore();
  });

  it('round-trips a string', async () => {
    await store.setString('k', 'v');
    await expect(store.getString('k')).resolves.toBe('v');
  });

  it('returns null for a missing string key', async () => {
    await expect(store.getString('missing')).resolves.toBeNull();
  });

  it('round-trips an object', async () => {
    await store.setObject('o', { a: 1, b: 'x' });
    await expect(store.getObject<{ a: number; b: string }>('o')).resolves.toEqual({ a: 1, b: 'x' });
  });

  it('throws AppError(storage) when a stored object is corrupt', async () => {
    localStorage.setItem('bad', '{not json');
    await expect(store.getObject('bad')).rejects.toMatchObject({ kind: 'storage' });
  });

  it('falls back to the default for a corrupt primitive', async () => {
    localStorage.setItem('n', 'not-a-number');
    await expect(store.getInt('n', 7)).resolves.toBe(7);
  });

  it('removes a key', async () => {
    await store.setString('k', 'v');
    await store.remove('k');
    await expect(store.getString('k')).resolves.toBeNull();
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test -- key-value.store.spec`
Expected: FAIL — cannot find module `@core/datasource/keyvalue/key-value.store`.

- [ ] **Step 3: Implement `StorageKeys`**

`src/app/core/datasource/keyvalue/storage-keys.ts`:
```ts
// StorageKeys: the single registry of localStorage keys used across the app.
// New features add their keys here.
export const StorageKeys = {
  profile: '@offline-storage/profile',
} as const;
```

- [ ] **Step 4: Implement `KeyValueStore`**

`src/app/core/datasource/keyvalue/key-value.store.ts`:
```ts
// KeyValueStore: a typed wrapper over localStorage that owns serialization and
// error mapping for the whole app. Repositories use it instead of touching
// localStorage directly, so the storage try/catch lives here exactly once.
//
// Semantics (mirrors the mobile base):
//  - A failed op is logged and re-thrown as AppError('storage').
//  - A missing key returns null (string/object) or the caller's fallback (int/double).
//  - Corruption is asymmetric ON PURPOSE: a malformed object throws AppError('storage')
//    (structured data is worth surfacing), while a malformed primitive falls back to
//    the default (primitives stay lenient).
// The synchronous localStorage calls are wrapped in Promises to keep the async
// contract identical to the mobile AsyncStorage-backed store.
import { Injectable } from '@angular/core';
import { AppError } from '@core/domain/errors/app-error';
import { createLogger } from '@core/logging/logger';

const log = createLogger('datasource', 'KeyValueStore');

@Injectable({ providedIn: 'root' })
export class KeyValueStore {
  // Run a storage op, mapping any failure to a 'storage' AppError.
  private async guard<T>(op: string, fn: () => T): Promise<T> {
    try {
      return fn();
    } catch (e) {
      log.warn(`${op} failed`, e);
      throw new AppError(`Failed to ${op} data`, 'storage');
    }
  }

  // region strings
  setString(key: string, value: string): Promise<void> {
    log.debug('setString →', key);
    return this.guard('save', () => {
      localStorage.setItem(key, value);
    });
  }

  getString(key: string): Promise<string | null> {
    log.debug('getString ←', key);
    return this.guard('load', () => localStorage.getItem(key));
  }
  // endregion

  // region numbers (serialized as strings)
  setInt(key: string, value: number): Promise<void> {
    return this.setString(key, String(value));
  }

  async getInt(key: string, fallback = 0): Promise<number> {
    const raw = await this.getString(key);
    if (raw === null) {
      return fallback;
    }
    const n = Number(raw);
    return Number.isInteger(n) ? n : fallback;
  }

  setDouble(key: string, value: number): Promise<void> {
    return this.setString(key, String(value));
  }

  async getDouble(key: string, fallback = 0): Promise<number> {
    const raw = await this.getString(key);
    if (raw === null) {
      return fallback;
    }
    const n = Number(raw);
    return Number.isFinite(n) ? n : fallback;
  }
  // endregion

  // region objects
  setObject<T>(key: string, value: T): Promise<void> {
    return this.setString(key, JSON.stringify(value));
  }

  async getObject<T>(key: string): Promise<T | null> {
    const raw = await this.getString(key);
    if (raw === null) {
      return null;
    }
    try {
      return JSON.parse(raw) as T;
    } catch (e) {
      // Intentional asymmetry vs getInt/getDouble: corrupted structured data is a
      // real error worth surfacing, not silently defaulting.
      log.warn('getObject parse failed', key, e);
      throw new AppError('Failed to load data', 'storage');
    }
  }
  // endregion

  // region management
  remove(key: string): Promise<void> {
    log.debug('remove ✕', key);
    return this.guard('remove', () => {
      localStorage.removeItem(key);
    });
  }

  clear(): Promise<void> {
    log.debug('clear ✕ all');
    return this.guard('clear', () => {
      localStorage.clear();
    });
  }
  // endregion
}

// Shared non-DI instance (parity with the mobile singleton). Prefer DI in app code.
export const keyValueStore = new KeyValueStore();
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `npm test -- key-value.store.spec`
Expected: PASS — 6 tests.

- [ ] **Step 6: Commit**

```bash
git add src/app/core/datasource
git commit -m "feat(core): add localStorage-backed KeyValueStore and StorageKeys

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: Core crypto — `CryptoService` + seams + key store + composition

**Files:**
- Create: `src/app/core/crypto/types.ts`
- Create: `src/app/core/crypto/crypto.service.ts`
- Create: `src/app/core/crypto/web-crypto-key-store.ts`
- Create: `src/app/core/crypto/index.ts`
- Test: `src/app/core/crypto/crypto.service.spec.ts`

**Interfaces:**
- Consumes: `AppError` (Task 2), `createLogger` (Task 3).
- Produces:
  - `interface KeyProvider { getOrCreateKey(): Promise<CryptoKey> }`
  - `interface WebCryptoLike { getRandomValues<T extends ArrayBufferView>(array: T): T; subtle: { encrypt(...): Promise<ArrayBuffer>; decrypt(...): Promise<ArrayBuffer> } }`
  - `class CryptoService { constructor(keys: KeyProvider, crypto: WebCryptoLike); encrypt(plain: string): Promise<string>; decrypt(blob: string): Promise<string> }`
  - `const webCryptoKeyStore: KeyProvider` (production, IndexedDB)
  - `const cryptoService: CryptoService` (production composition)

- [ ] **Step 1: Write the failing test**

`src/app/core/crypto/crypto.service.spec.ts`:
```ts
import { webcrypto } from 'node:crypto';
import { CryptoService } from '@core/crypto/crypto.service';
import { KeyProvider, WebCryptoLike } from '@core/crypto/types';

const cryptoLike = webcrypto as unknown as WebCryptoLike;

class FakeKeyProvider implements KeyProvider {
  private key?: CryptoKey;
  async getOrCreateKey(): Promise<CryptoKey> {
    if (!this.key) {
      this.key = (await webcrypto.subtle.generateKey(
        { name: 'AES-GCM', length: 256 },
        true,
        ['encrypt', 'decrypt'],
      )) as CryptoKey;
    }
    return this.key;
  }
}

describe('CryptoService (Web Crypto)', () => {
  const svc = new CryptoService(new FakeKeyProvider(), cryptoLike);

  it('round-trips plaintext through encrypt/decrypt', async () => {
    const blob = await svc.encrypt('hello world');
    expect(typeof blob).toBe('string');
    await expect(svc.decrypt(blob)).resolves.toBe('hello world');
  });

  it('throws AppError(crypto) when the blob is tampered', async () => {
    const blob = await svc.encrypt('secret');
    const bytes = Buffer.from(blob, 'base64');
    bytes[bytes.length - 1] ^= 0xff; // flip a tag byte
    const tampered = bytes.toString('base64');
    await expect(svc.decrypt(tampered)).rejects.toMatchObject({ kind: 'crypto' });
  });

  it('throws AppError(crypto) when the blob is too short', async () => {
    await expect(svc.decrypt('AAAA')).rejects.toMatchObject({ kind: 'crypto' });
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test -- crypto.service.spec`
Expected: FAIL — cannot find module `@core/crypto/crypto.service`.

- [ ] **Step 3: Implement the crypto seams**

`src/app/core/crypto/types.ts`:
```ts
// Seam interfaces that let CryptoService run without the real browser globals in
// tests. Production wires the global `crypto` (window.crypto) + an IndexedDB-backed
// key provider; tests inject Node's webcrypto + an in-memory key provider.

// The subset of the Web Crypto API the service depends on. Both window.crypto and
// Node's webcrypto satisfy this structurally.
export interface WebCryptoLike {
  getRandomValues<T extends ArrayBufferView>(array: T): T;
  subtle: {
    encrypt(
      algorithm: { name: 'AES-GCM'; iv: BufferSource },
      key: CryptoKey,
      data: BufferSource,
    ): Promise<ArrayBuffer>;
    decrypt(
      algorithm: { name: 'AES-GCM'; iv: BufferSource },
      key: CryptoKey,
      data: BufferSource,
    ): Promise<ArrayBuffer>;
  };
}

// Provides the AES-256-GCM key as a CryptoKey. Get-or-create, cached after first load.
export interface KeyProvider {
  getOrCreateKey(): Promise<CryptoKey>;
}
```

- [ ] **Step 4: Implement `CryptoService`**

`src/app/core/crypto/crypto.service.ts`:
```ts
// CryptoService: AES-256-GCM encrypt/decrypt of arbitrary strings — the web
// equivalent of the mobile CryptoService. The CryptoKey comes from an injected
// KeyProvider (IndexedDB in production); the AES work goes through an injected
// WebCryptoLike. Both seams keep this class testable on Node's webcrypto.
//
// Output format: Base64( iv(12 bytes) ‖ ciphertext+tag ). Unlike the mobile
// `iv ‖ ciphertext ‖ tag` split, Web Crypto APPENDS the 16-byte GCM auth tag to
// the ciphertext, so we keep only the 12-byte IV prefix and treat the rest as one.
import { AppError } from '@core/domain/errors/app-error';
import { createLogger } from '@core/logging/logger';
import { KeyProvider, WebCryptoLike } from './types';

const log = createLogger('core', 'CryptoService');

const IV_BYTES = 12; // GCM-recommended nonce length

export class CryptoService {
  constructor(
    private readonly keys: KeyProvider,
    private readonly crypto: WebCryptoLike,
  ) {}

  async encrypt(plain: string): Promise<string> {
    try {
      const key = await this.keys.getOrCreateKey();
      const iv = this.crypto.getRandomValues(new Uint8Array(IV_BYTES));
      const cipherBuf = await this.crypto.subtle.encrypt(
        { name: 'AES-GCM', iv },
        key,
        new TextEncoder().encode(plain),
      );
      const ciphertext = new Uint8Array(cipherBuf);
      const out = new Uint8Array(iv.length + ciphertext.length);
      out.set(iv, 0);
      out.set(ciphertext, iv.length);
      return toBase64(out);
    } catch (e) {
      log.warn('encrypt failed', e instanceof Error ? e.message : String(e));
      throw new AppError('Encryption failed', 'crypto');
    }
  }

  async decrypt(blob: string): Promise<string> {
    try {
      const raw = fromBase64(blob);
      if (raw.length <= IV_BYTES) {
        throw new Error('blob too short');
      }
      const iv = raw.subarray(0, IV_BYTES);
      const data = raw.subarray(IV_BYTES);
      const key = await this.keys.getOrCreateKey();
      const plainBuf = await this.crypto.subtle.decrypt(
        { name: 'AES-GCM', iv },
        key,
        data,
      );
      return new TextDecoder().decode(plainBuf);
    } catch (e) {
      log.warn('decrypt failed', e instanceof Error ? e.message : String(e));
      throw new AppError('Decryption failed', 'crypto');
    }
  }
}

// Base64 helpers over binary (btoa/atob exist in browsers and jsdom).
function toBase64(bytes: Uint8Array): string {
  let bin = '';
  for (const b of bytes) {
    bin += String.fromCharCode(b);
  }
  return btoa(bin);
}

function fromBase64(b64: string): Uint8Array {
  const bin = atob(b64);
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) {
    out[i] = bin.charCodeAt(i);
  }
  return out;
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `npm test -- crypto.service.spec`
Expected: PASS — 3 tests.

- [ ] **Step 6: Implement the production key store (IndexedDB) and composition**

`src/app/core/crypto/web-crypto-key-store.ts`:
```ts
// webCryptoKeyStore: the production KeyProvider. Generates a NON-EXTRACTABLE
// AES-256-GCM CryptoKey and persists it in IndexedDB (structured clone supports
// CryptoKey). The raw key bytes never leave the browser — stronger than the mobile
// Keychain approach, which had to hand back raw bytes.
import { createLogger } from '@core/logging/logger';
import { KeyProvider } from './types';

const log = createLogger('core', 'webCryptoKeyStore');

const DB_NAME = 'base-frontend-crypto';
const STORE = 'keys';
const KEY_ID = 'aes-256-gcm-master';

let cached: CryptoKey | null = null;

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, 1);
    req.onupgradeneeded = () => req.result.createObjectStore(STORE);
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

function idbGet(db: IDBDatabase, key: string): Promise<CryptoKey | undefined> {
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE, 'readonly');
    const req = tx.objectStore(STORE).get(key);
    req.onsuccess = () => resolve(req.result as CryptoKey | undefined);
    req.onerror = () => reject(req.error);
  });
}

function idbPut(db: IDBDatabase, key: string, value: CryptoKey): Promise<void> {
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE, 'readwrite');
    tx.objectStore(STORE).put(value, key);
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
}

export const webCryptoKeyStore: KeyProvider = {
  async getOrCreateKey(): Promise<CryptoKey> {
    if (cached) {
      return cached;
    }
    const db = await openDb();
    const existing = await idbGet(db, KEY_ID);
    if (existing) {
      cached = existing;
      return cached;
    }
    const key = await crypto.subtle.generateKey(
      { name: 'AES-GCM', length: 256 },
      false, // non-extractable
      ['encrypt', 'decrypt'],
    );
    await idbPut(db, KEY_ID, key);
    log.debug('generated new master key');
    cached = key;
    return cached;
  },
};
```

`src/app/core/crypto/index.ts`:
```ts
// Production composition of CryptoService: the global Web Crypto API + the
// IndexedDB-backed key provider. Importing this pulls in browser globals, so unit
// tests import CryptoService directly and inject fakes instead.
import { CryptoService } from './crypto.service';
import { webCryptoKeyStore } from './web-crypto-key-store';

export { CryptoService } from './crypto.service';
export type { KeyProvider, WebCryptoLike } from './types';

// The global `crypto` (window.crypto) satisfies WebCryptoLike structurally.
export const cryptoService = new CryptoService(webCryptoKeyStore, crypto);
```

- [ ] **Step 7: Re-run the crypto tests and the full suite**

Run: `npm test`
Expected: all tests PASS (Tasks 2–7).

- [ ] **Step 8: Commit**

```bash
git add src/app/core/crypto
git commit -m "feat(core): add CryptoService (Web Crypto AES-GCM) with IndexedDB key store

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 8: Core UI — theme tokens + global styles

**Files:**
- Create: `src/app/core/ui/theme/theme.ts`
- Create: `src/app/core/ui/theme/theme.scss`
- Modify: `src/styles.scss`

**Interfaces:**
- Produces: `const theme` (TS tokens) and CSS variables (`--color-primary`, `--space-md`, `--radius-md`, …) available globally.

- [ ] **Step 1: Implement the TS tokens**

`src/app/core/ui/theme/theme.ts`:
```ts
// theme: design tokens for use from TypeScript. Mirrors the CSS variables in
// theme.scss so component logic and stylesheets share one source of truth.
export const theme = {
  colors: {
    primary: '#0a84ff',
    text: '#111',
    muted: '#555',
    error: '#c00',
    border: '#ccc',
    background: '#fff',
    onPrimary: '#fff',
  },
  spacing: { xs: 4, sm: 8, md: 16, lg: 24, xl: 32 },
  radius: { sm: 6, md: 10 },
} as const;
```

- [ ] **Step 2: Implement the CSS variables**

`src/app/core/ui/theme/theme.scss`:
```scss
// theme.scss: design tokens as CSS custom properties (the source of truth for styles).
:root {
  --color-primary: #0a84ff;
  --color-text: #111;
  --color-muted: #555;
  --color-error: #c00;
  --color-border: #ccc;
  --color-background: #fff;
  --color-on-primary: #fff;

  --space-xs: 4px;
  --space-sm: 8px;
  --space-md: 16px;
  --space-lg: 24px;
  --space-xl: 32px;

  --radius-sm: 6px;
  --radius-md: 10px;
}
```

- [ ] **Step 3: Wire global styles**

Replace `src/styles.scss` with:
```scss
@use './app/core/ui/theme/theme';

html,
body {
  margin: 0;
  padding: 0;
  font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif;
  color: var(--color-text);
  background: var(--color-background);
}

* {
  box-sizing: border-box;
}
```

- [ ] **Step 4: Verify the build picks up styles**

Run: `npm run build`
Expected: build succeeds with no SCSS errors.

- [ ] **Step 5: Commit**

```bash
git add src/app/core/ui/theme src/styles.scss
git commit -m "feat(core): add theme tokens (TS) and CSS variables + global styles

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 9: Core UI — shared components + NotificationService

**Files:**
- Create: `src/app/core/ui/components/nav-button.component.ts`
- Create: `src/app/core/ui/components/text-field.component.ts`
- Create: `src/app/core/ui/notification.service.ts`
- Create: `src/app/core/ui/components/notification-host.component.ts`
- Test: `src/app/core/ui/components/nav-button.component.spec.ts`
- Test: `src/app/core/ui/notification.service.spec.ts`

**Interfaces:**
- Produces:
  - `NavButtonComponent` (selector `app-nav-button`, inputs `label: string`, `disabled = false`, output `press: void`).
  - `TextFieldComponent` (selector `app-text-field`, input `label`, `placeholder`, model `value: string`).
  - `@Injectable({providedIn:'root'}) NotificationService { current: Signal<Notification|null>; success(msg); error(msg); clear() }` where `Notification = { kind:'success'|'error'; message:string }`.
  - `NotificationHostComponent` (selector `app-notification-host`).

- [ ] **Step 1: Write the failing tests**

`src/app/core/ui/components/nav-button.component.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { NavButtonComponent } from '@core/ui/components/nav-button.component';

@Component({
  standalone: true,
  imports: [NavButtonComponent],
  template: `<app-nav-button [label]="'Go'" [disabled]="disabled" (press)="onPress()" />`,
})
class HostComponent {
  disabled = false;
  pressed = 0;
  onPress() { this.pressed++; }
}

describe('NavButtonComponent', () => {
  it('renders the label and emits press on click', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const btn: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    expect(btn.textContent).toContain('Go');
    btn.click();
    expect(fixture.componentInstance.pressed).toBe(1);
  });

  it('does not emit when disabled', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.disabled = true;
    fixture.detectChanges();
    const btn: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    btn.click();
    expect(fixture.componentInstance.pressed).toBe(0);
  });
});
```

`src/app/core/ui/notification.service.spec.ts`:
```ts
import { NotificationService } from '@core/ui/notification.service';

describe('NotificationService', () => {
  it('sets and clears the current notification', () => {
    const svc = new NotificationService();
    expect(svc.current()).toBeNull();
    svc.error('nope');
    expect(svc.current()).toEqual({ kind: 'error', message: 'nope' });
    svc.clear();
    expect(svc.current()).toBeNull();
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `npm test -- nav-button notification`
Expected: FAIL — modules not found.

- [ ] **Step 3: Implement `NavButtonComponent`**

`src/app/core/ui/components/nav-button.component.ts`:
```ts
// NavButton: a reusable themed button (label + press output). No navigation logic.
import { Component, input, output } from '@angular/core';

@Component({
  selector: 'app-nav-button',
  standalone: true,
  template: `
    <button class="nav-button" [disabled]="disabled()" (click)="press.emit()">
      {{ label() }}
    </button>
  `,
  styles: [
    `
      .nav-button {
        background: var(--color-primary);
        color: var(--color-on-primary);
        border: none;
        padding: var(--space-md) var(--space-lg);
        border-radius: var(--radius-md);
        font-size: 16px;
        font-weight: 600;
        cursor: pointer;
      }
      .nav-button:disabled {
        opacity: 0.4;
        cursor: default;
      }
      .nav-button:not(:disabled):active {
        opacity: 0.7;
      }
    `,
  ],
})
export class NavButtonComponent {
  readonly label = input.required<string>();
  readonly disabled = input(false);
  readonly press = output<void>();
}
```

- [ ] **Step 4: Implement `TextFieldComponent`**

`src/app/core/ui/components/text-field.component.ts`:
```ts
// TextField: a labeled input with a two-way `value` model signal.
import { Component, input, model } from '@angular/core';

@Component({
  selector: 'app-text-field',
  standalone: true,
  template: `
    <label class="text-field">
      @if (label()) { <span class="label">{{ label() }}</span> }
      <input
        class="input"
        [value]="value()"
        [placeholder]="placeholder()"
        (input)="onInput($event)"
      />
    </label>
  `,
  styles: [
    `
      .text-field { display: flex; flex-direction: column; gap: var(--space-xs); }
      .label { color: var(--color-muted); font-size: 14px; }
      .input {
        padding: var(--space-sm) var(--space-md);
        border: 1px solid var(--color-border);
        border-radius: var(--radius-sm);
        font-size: 16px;
      }
    `,
  ],
})
export class TextFieldComponent {
  readonly label = input('');
  readonly placeholder = input('');
  readonly value = model('');

  onInput(event: Event): void {
    this.value.set((event.target as HTMLInputElement).value);
  }
}
```

- [ ] **Step 5: Implement `NotificationService` and host**

`src/app/core/ui/notification.service.ts`:
```ts
// NotificationService: app-wide toast/snackbar state (the web equivalent of the
// mobile Alert.alert). Pages call success()/error(); the host renders it.
import { Injectable, signal } from '@angular/core';

export type Notification = { kind: 'success' | 'error'; message: string };

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly _current = signal<Notification | null>(null);
  readonly current = this._current.asReadonly();

  success(message: string): void {
    this._current.set({ kind: 'success', message });
  }

  error(message: string): void {
    this._current.set({ kind: 'error', message });
  }

  clear(): void {
    this._current.set(null);
  }
}
```

`src/app/core/ui/components/notification-host.component.ts`:
```ts
// NotificationHost: renders the current NotificationService message as a dismissible
// banner. Placed once in the app shell.
import { Component, inject } from '@angular/core';
import { NotificationService } from '@core/ui/notification.service';

@Component({
  selector: 'app-notification-host',
  standalone: true,
  template: `
    @if (notifications.current(); as n) {
      <div class="toast" [class.error]="n.kind === 'error'" (click)="notifications.clear()">
        {{ n.message }}
      </div>
    }
  `,
  styles: [
    `
      .toast {
        position: fixed;
        bottom: var(--space-lg);
        left: 50%;
        transform: translateX(-50%);
        background: var(--color-primary);
        color: var(--color-on-primary);
        padding: var(--space-sm) var(--space-lg);
        border-radius: var(--radius-md);
        cursor: pointer;
      }
      .toast.error { background: var(--color-error); }
    `,
  ],
})
export class NotificationHostComponent {
  readonly notifications = inject(NotificationService);
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `npm test -- nav-button notification`
Expected: PASS — 3 tests.

- [ ] **Step 7: Commit**

```bash
git add src/app/core/ui
git commit -m "feat(core): add NavButton, TextField, NotificationService + host

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 10: Composition root + Home feature

**Files:**
- Create: `src/app/features/home/presentation/pages/home.page.ts`
- Create: `src/app/features/home/index.ts`
- Modify: `src/app/app.component.ts` (root shell)
- Modify: `src/app/app.config.ts` (providers)
- Modify: `src/app/app.routes.ts` (routes)
- Test: `src/app/features/home/presentation/pages/home.page.spec.ts`

**Interfaces:**
- Consumes: `NotificationHostComponent` (Task 9), `NavButtonComponent` (Task 9).
- Produces: `HomePage` (selector `app-home-page`), the configured `appConfig` (router + http + animations), and `routes` with `''` → Home. Later feature tasks append their routes here.

- [ ] **Step 1: Write the failing test**

`src/app/features/home/presentation/pages/home.page.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HomePage } from '@features/home';

describe('HomePage', () => {
  it('renders the title and navigation links', () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([])],
    });
    const fixture = TestBed.createComponent(HomePage);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Home');
    expect(text).toContain('API');
    expect(text).toContain('Offline Storage');
    expect(text).toContain('Design');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test -- home.page.spec`
Expected: FAIL — cannot find module `@features/home`.

- [ ] **Step 3: Implement `HomePage`**

`src/app/features/home/presentation/pages/home.page.ts`:
```ts
// HomePage: the landing page. Links into each feature via routerLink.
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-home-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="page">
      <h1>Home</h1>
      <nav class="links">
        <a routerLink="/design">Design</a>
        <a routerLink="/api">API</a>
        <a routerLink="/offline-storage">Offline Storage</a>
        <a routerLink="/profile-form">Profile Form</a>
      </nav>
    </section>
  `,
  styles: [
    `
      .page { padding: var(--space-lg); display: flex; flex-direction: column; gap: var(--space-md); }
      .links { display: flex; flex-direction: column; gap: var(--space-sm); }
      a { color: var(--color-primary); font-size: 18px; text-decoration: none; }
    `,
  ],
})
export class HomePage {}
```

`src/app/features/home/index.ts`:
```ts
export { HomePage } from './presentation/pages/home.page';
```

- [ ] **Step 4: Implement the app shell**

`src/app/app.component.ts`:
```ts
// AppComponent: the root shell. Providers live in app.config.ts; screens come from
// the router. The notification host renders app-wide toasts.
import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { NotificationHostComponent } from '@core/ui/components/notification-host.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, NotificationHostComponent],
  template: `
    <router-outlet />
    <app-notification-host />
  `,
})
export class AppComponent {}
```

- [ ] **Step 5: Implement app config (providers)**

`src/app/app.config.ts`:
```ts
// appConfig: the composition root (the web equivalent of RootProviders).
import { ApplicationConfig } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [provideRouter(routes), provideHttpClient(), provideAnimations()],
};
```

- [ ] **Step 6: Implement the routes (Home only for now)**

`src/app/app.routes.ts`:
```ts
// routes: the app-wide route table (the web equivalent of RootNavigator). Each
// feature task appends its routes here, wiring repository ports in route providers.
import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('@features/home').then((m) => m.HomePage),
  },
];
```

- [ ] **Step 7: Ensure `main.ts` bootstraps `AppComponent` with `appConfig`**

Confirm `src/app/main.ts` or `src/main.ts` matches (the CLI generates this; adjust the import paths if needed):
```ts
import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';

bootstrapApplication(AppComponent, appConfig).catch((err) => console.error(err));
```

- [ ] **Step 8: Run the test + build**

Run: `npm test -- home.page.spec` then `npm run build`
Expected: test PASS; build succeeds.

- [ ] **Step 9: Manual smoke (optional but recommended)**

Run: `npm start` and open the served URL.
Expected: the Home page renders with four links. Stop the server when done.

- [ ] **Step 10: Commit**

```bash
git add src/app/app.component.ts src/app/app.config.ts src/app/app.routes.ts src/app/features/home src/main.ts
git commit -m "feat: wire composition root (shell, config, routes) + Home page

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 11: Feature — `api/fetch-posts` (full vertical slice)

**Files:**
- Create: `src/app/features/api/fetch-posts/data/dto/post-dto-rs.ts`
- Create: `src/app/features/api/fetch-posts/domain/model/post.ts`
- Create: `src/app/features/api/fetch-posts/domain/repositories/post.repository.port.ts`
- Create: `src/app/features/api/fetch-posts/data/repositories/post.repository.ts`
- Create: `src/app/features/api/fetch-posts/domain/usecases/get-posts.use-case.ts`
- Create: `src/app/features/api/fetch-posts/presentation/viewmodels/fetch-posts.viewmodel.ts`
- Create: `src/app/features/api/fetch-posts/presentation/pages/api.page.ts`
- Create: `src/app/features/api/fetch-posts/index.ts`
- Modify: `src/app/app.routes.ts` (add `/api` route + providers)
- Test: `.../domain/usecases/get-posts.use-case.spec.ts`, `.../presentation/viewmodels/fetch-posts.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `BaseResponseRs`, `HttpClientService`, `AppError`, `UseCase`, `createLogger`, `NavButtonComponent`, `NotificationService`.
- Produces (barrel `@features/api/fetch-posts`): `ApiPage`, `FetchPostsViewModel`, `GetPostsUseCase`, `PostRepositoryImpl`, `POST_REPOSITORY`, types `Post`, `PostDtoRs`, `PostsDtoRs`.

- [ ] **Step 1: Implement DTO + model + port (no behavior to TDD yet)**

`src/app/features/api/fetch-posts/data/dto/post-dto-rs.ts`:
```ts
// Response DTOs are suffixed `…DtoRs`, request DTOs `…DtoRq`.
import { BaseResponseRs } from '@core/network/api/base-response-rs';

// PostDtoRs: the raw shape of a single post in the API response (transport layer).
export type PostDtoRs = {
  id: number;
  title: string;
  body: string;
  userId: number;
};

// PostsDtoRs: the posts response, inheriting the base response envelope.
export interface PostsDtoRs extends BaseResponseRs<PostDtoRs[]> {}

// isPostDtoRsValid: guards the raw transport shape before mapping to a domain model.
export function isPostDtoRsValid(dto: PostDtoRs): boolean {
  return (
    typeof dto.id === 'number' &&
    typeof dto.userId === 'number' &&
    typeof dto.title === 'string' &&
    dto.title.length > 0 &&
    typeof dto.body === 'string' &&
    dto.body.length > 0
  );
}
```

`src/app/features/api/fetch-posts/domain/model/post.ts`:
```ts
import { PostDtoRs } from '../../data/dto/post-dto-rs';

// Post: the domain model (clean of any transport/DTO concerns).
export type Post = {
  id: number;
  title: string;
  body: string;
  userId: number;
};

// map a transport DTO to a domain model
export const toPost = (dto: PostDtoRs): Post => ({
  id: dto.id,
  title: dto.title,
  body: dto.body,
  userId: dto.userId,
});
```

`src/app/features/api/fetch-posts/domain/repositories/post.repository.port.ts`:
```ts
import { InjectionToken } from '@angular/core';
import { PostsDtoRs } from '../../data/dto/post-dto-rs';

// IPostRepository: fetches the posts response envelope. Validation and mapping to
// the domain model happen in the use case.
export interface IPostRepository {
  getPosts(): Promise<PostsDtoRs>;
}

// DI token for the port (interfaces don't exist at runtime).
export const POST_REPOSITORY = new InjectionToken<IPostRepository>('POST_REPOSITORY');
```

- [ ] **Step 2: Write the failing use-case test**

`src/app/features/api/fetch-posts/domain/usecases/get-posts.use-case.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { GetPostsUseCase } from './get-posts.use-case';
import { IPostRepository, POST_REPOSITORY } from '../repositories/post.repository.port';
import { PostsDtoRs } from '../../data/dto/post-dto-rs';

function repoReturning(res: PostsDtoRs): IPostRepository {
  return { getPosts: async () => res };
}

describe('GetPostsUseCase', () => {
  function make(repo: IPostRepository): GetPostsUseCase {
    TestBed.configureTestingModule({
      providers: [GetPostsUseCase, { provide: POST_REPOSITORY, useValue: repo }],
    });
    return TestBed.inject(GetPostsUseCase);
  }

  it('validates and maps DTOs to domain models', async () => {
    const uc = make(
      repoReturning({ data: [{ id: 1, title: 't', body: 'b', userId: 9 }], success: true }),
    );
    const r = await uc.run();
    expect(r.ok).toBe(true);
    if (r.ok) {
      expect(r.data).toEqual([{ id: 1, title: 't', body: 'b', userId: 9 }]);
    }
  });

  it('fails with kind=validation on a bad payload', async () => {
    const uc = make(
      repoReturning({ data: [{ id: 1, title: '', body: 'b', userId: 9 }], success: true }),
    );
    const r = await uc.run();
    expect(r.ok).toBe(false);
    if (!r.ok) {
      expect(r.error.kind).toBe('validation');
    }
  });
});
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `npm test -- get-posts.use-case.spec`
Expected: FAIL — cannot find module `./get-posts.use-case`.

- [ ] **Step 4: Implement the use case**

`src/app/features/api/fetch-posts/domain/usecases/get-posts.use-case.ts`:
```ts
// GetPostsUseCase: fetches the posts response, validates the DTOs, maps to domain
// models. Error handling is inherited from UseCase.run().
import { Injectable, inject } from '@angular/core';
import { AppError } from '@core/domain/errors/app-error';
import { UseCase } from '@core/domain/usecase/use-case';
import { isPostDtoRsValid } from '../../data/dto/post-dto-rs';
import { Post, toPost } from '../model/post';
import { POST_REPOSITORY } from '../repositories/post.repository.port';

@Injectable()
export class GetPostsUseCase extends UseCase<void, Post[]> {
  private readonly repo = inject(POST_REPOSITORY);

  constructor() {
    super('GetPosts');
  }

  protected async execute(): Promise<Post[]> {
    const res = await this.repo.getPosts();
    if (!res.data.every(isPostDtoRsValid)) {
      throw new AppError('Invalid data received', 'validation');
    }
    const posts = res.data.map(toPost);
    this.log.debug('mapped', posts.length, 'posts');
    return posts;
  }
}
```

- [ ] **Step 5: Run the use-case test to verify it passes**

Run: `npm test -- get-posts.use-case.spec`
Expected: PASS — 2 tests.

- [ ] **Step 6: Implement the repository**

`src/app/features/api/fetch-posts/data/repositories/post.repository.ts`:
```ts
// PostRepositoryImpl: fetches posts via the shared HttpClientService. dummyjson
// returns a flat { posts, ... }; we synthesize the base response envelope here so
// the rest of the app already speaks { data, success, message }.
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { createLogger } from '@core/logging/logger';
import { IPostRepository } from '../../domain/repositories/post.repository.port';
import { PostDtoRs, PostsDtoRs } from '../dto/post-dto-rs';

@Injectable()
export class PostRepositoryImpl implements IPostRepository {
  private readonly http = inject(HttpClientService);
  private readonly log = createLogger('repository', 'PostRepository');

  async getPosts(): Promise<PostsDtoRs> {
    this.log.debug('getPosts() → GET /posts');
    const raw = await this.http.request<{ posts: PostDtoRs[] }>('/posts');
    this.log.debug('received', raw.posts.length, 'posts');
    return { data: raw.posts, success: true };
  }
}
```

- [ ] **Step 7: Write the failing ViewModel test**

`src/app/features/api/fetch-posts/presentation/viewmodels/fetch-posts.viewmodel.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { FetchPostsViewModel } from './fetch-posts.viewmodel';
import { GetPostsUseCase } from '../../domain/usecases/get-posts.use-case';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';

describe('FetchPostsViewModel', () => {
  function make(run: GetPostsUseCase['run']): FetchPostsViewModel {
    TestBed.configureTestingModule({
      providers: [
        FetchPostsViewModel,
        { provide: GetPostsUseCase, useValue: { run } },
      ],
    });
    return TestBed.inject(FetchPostsViewModel);
  }

  it('populates the posts signal on success', async () => {
    const vm = make(async () => ok([{ id: 1, title: 't', body: 'b', userId: 9 }]));
    await vm.run();
    expect(vm.loading()).toBe(false);
    expect(vm.posts().length).toBe(1);
    expect(vm.error()).toBeNull();
  });

  it('populates the error signal on failure', async () => {
    const vm = make(async () => fail(new AppError('HTTP 500', 'http', 500)));
    await vm.run();
    expect(vm.posts().length).toBe(0);
    expect(vm.error()).toBe('HTTP 500');
  });
});
```

- [ ] **Step 8: Run the test to verify it fails**

Run: `npm test -- fetch-posts.viewmodel.spec`
Expected: FAIL — cannot find module `./fetch-posts.viewmodel`.

- [ ] **Step 9: Implement the ViewModel**

`src/app/features/api/fetch-posts/presentation/viewmodels/fetch-posts.viewmodel.ts`:
```ts
// FetchPostsViewModel: the presentation facade. Wires the use case, exposes signals
// the page renders, and translates the Result into state. No try/catch — the base
// UseCase already normalized errors.
import { Injectable, inject, signal } from '@angular/core';
import { createLogger } from '@core/logging/logger';
import { GetPostsUseCase } from '../../domain/usecases/get-posts.use-case';
import { Post } from '../../domain/model/post';

@Injectable()
export class FetchPostsViewModel {
  private readonly useCase = inject(GetPostsUseCase);
  private readonly log = createLogger('viewmodel', 'FetchPostsViewModel');

  readonly loading = signal(false);
  readonly posts = signal<Post[]>([]);
  readonly error = signal<string | null>(null);

  async run(): Promise<void> {
    this.log.debug('run()');
    this.loading.set(true);
    this.error.set(null);
    const result = await this.useCase.run();
    this.loading.set(false);
    if (result.ok) {
      this.posts.set(result.data);
      this.log.debug('ok:', result.data.length, 'posts');
    } else {
      this.error.set(result.error.message);
      this.log.warn('failed:', result.error.message);
    }
  }
}
```

- [ ] **Step 10: Run the ViewModel test to verify it passes**

Run: `npm test -- fetch-posts.viewmodel.spec`
Expected: PASS — 2 tests.

- [ ] **Step 11: Implement the page + barrel**

`src/app/features/api/fetch-posts/presentation/pages/api.page.ts`:
```ts
// ApiPage: a button that fetches posts through the clean-arch layers and reports
// the outcome via the NotificationService.
import { Component, effect, inject } from '@angular/core';
import { NavButtonComponent } from '@core/ui/components/nav-button.component';
import { NotificationService } from '@core/ui/notification.service';
import { createLogger } from '@core/logging/logger';
import { FetchPostsViewModel } from '../viewmodels/fetch-posts.viewmodel';

@Component({
  selector: 'app-api-page',
  standalone: true,
  imports: [NavButtonComponent],
  template: `
    <section class="page">
      <h1>API</h1>
      <app-nav-button label="Fetch Posts" (press)="onFetch()" [disabled]="vm.loading()" />
      @if (vm.loading()) { <p>Loading…</p> }
      @if (vm.posts().length) {
        <p>Loaded {{ vm.posts().length }} posts</p>
        <ul>
          @for (p of vm.posts(); track p.id) { <li>{{ p.title }}</li> }
        </ul>
      }
    </section>
  `,
  styles: [`.page { padding: var(--space-lg); display: flex; flex-direction: column; gap: var(--space-md); }`],
})
export class ApiPage {
  readonly vm = inject(FetchPostsViewModel);
  private readonly notifications = inject(NotificationService);
  private readonly log = createLogger('page', 'ApiPage');

  constructor() {
    // Surface errors as a toast as soon as the viewmodel reports one.
    effect(() => {
      const err = this.vm.error();
      if (err) {
        this.notifications.error(err);
      }
    });
  }

  onFetch(): void {
    this.log.debug('fetch pressed');
    void this.vm.run().then(() => {
      if (!this.vm.error()) {
        this.notifications.success(`Loaded ${this.vm.posts().length} posts`);
      }
    });
  }
}
```

`src/app/features/api/fetch-posts/index.ts`:
```ts
export { ApiPage } from './presentation/pages/api.page';
export { FetchPostsViewModel } from './presentation/viewmodels/fetch-posts.viewmodel';
export { GetPostsUseCase } from './domain/usecases/get-posts.use-case';
export { PostRepositoryImpl } from './data/repositories/post.repository';
export { POST_REPOSITORY } from './domain/repositories/post.repository.port';
export type { Post } from './domain/model/post';
export type { PostDtoRs, PostsDtoRs } from './data/dto/post-dto-rs';
```

- [ ] **Step 12: Wire the route with feature-scoped providers**

Edit `src/app/app.routes.ts` — add the import and the route:
```ts
import { Routes } from '@angular/router';
// Import only the DI symbols needed for route providers. The page components are
// lazy-loaded via loadComponent, so they are NOT imported here.
import {
  POST_REPOSITORY,
  PostRepositoryImpl,
  GetPostsUseCase,
  FetchPostsViewModel,
} from '@features/api/fetch-posts';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('@features/home').then((m) => m.HomePage),
  },
  {
    path: 'api',
    loadComponent: () => import('@features/api/fetch-posts').then((m) => m.ApiPage),
    providers: [
      { provide: POST_REPOSITORY, useClass: PostRepositoryImpl },
      GetPostsUseCase,
      FetchPostsViewModel,
    ],
  },
];
```

- [ ] **Step 13: Run the full suite + build**

Run: `npm test` then `npm run build`
Expected: all tests PASS; build succeeds.

- [ ] **Step 14: Commit**

```bash
git add src/app/features/api src/app/app.routes.ts
git commit -m "feat(api): add fetch-posts vertical slice (DTO, repo, use case, VM, page)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 12: Feature — `offline-storage` (full vertical slice)

**Files:**
- Create: `src/app/features/offline-storage/domain/model/saved-profile.ts`
- Create: `src/app/features/offline-storage/domain/repositories/profile-storage.repository.port.ts`
- Create: `src/app/features/offline-storage/data/repositories/profile-storage.repository.ts`
- Create: `src/app/features/offline-storage/domain/usecases/save-profile.use-case.ts`
- Create: `src/app/features/offline-storage/domain/usecases/load-profile.use-case.ts`
- Create: `src/app/features/offline-storage/presentation/viewmodels/save-profile.viewmodel.ts`
- Create: `src/app/features/offline-storage/presentation/viewmodels/retrieve-profile.viewmodel.ts`
- Create: `src/app/features/offline-storage/presentation/pages/offline-storage.page.ts`
- Create: `src/app/features/offline-storage/presentation/pages/saving-data.page.ts`
- Create: `src/app/features/offline-storage/presentation/pages/retrieved-data.page.ts`
- Create: `src/app/features/offline-storage/index.ts`
- Modify: `src/app/app.routes.ts`
- Test: `.../usecases/save-profile.use-case.spec.ts`, `.../viewmodels/save-profile.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `KeyValueStore`, `StorageKeys`, `UseCase`, `NavButtonComponent`, `TextFieldComponent`, `NotificationService`, `RouterLink`.
- Produces (barrel `@features/offline-storage`): `OfflineStoragePage`, `SavingDataPage`, `RetrievedDataPage`, `SaveProfileViewModel`, `RetrieveProfileViewModel`, `SaveProfileUseCase`, `LoadProfileUseCase`, `ProfileStorageRepositoryImpl`, `PROFILE_STORAGE_REPOSITORY`, type `SavedProfile`.

- [ ] **Step 1: Implement model + port**

`src/app/features/offline-storage/domain/model/saved-profile.ts`:
```ts
// SavedProfile: the single profile record persisted locally.
export type SavedProfile = {
  firstName: string;
  secondName: string;
};
```

`src/app/features/offline-storage/domain/repositories/profile-storage.repository.port.ts`:
```ts
import { InjectionToken } from '@angular/core';
import { SavedProfile } from '../model/saved-profile';

// IProfileStorageRepository: persists and reads back a single profile record.
// Returns null from load() when nothing has been saved yet.
export interface IProfileStorageRepository {
  save(profile: SavedProfile): Promise<void>;
  load(): Promise<SavedProfile | null>;
}

export const PROFILE_STORAGE_REPOSITORY =
  new InjectionToken<IProfileStorageRepository>('PROFILE_STORAGE_REPOSITORY');
```

- [ ] **Step 2: Implement the repository**

`src/app/features/offline-storage/data/repositories/profile-storage.repository.ts`:
```ts
// ProfileStorageRepositoryImpl: persists the profile via the shared KeyValueStore.
// All serialization/error handling live in KeyValueStore, so this is just a typed
// mapping from the domain model to a storage key.
import { Injectable, inject } from '@angular/core';
import { KeyValueStore } from '@core/datasource/keyvalue/key-value.store';
import { StorageKeys } from '@core/datasource/keyvalue/storage-keys';
import { SavedProfile } from '../../domain/model/saved-profile';
import { IProfileStorageRepository } from '../../domain/repositories/profile-storage.repository.port';

@Injectable()
export class ProfileStorageRepositoryImpl implements IProfileStorageRepository {
  private readonly store = inject(KeyValueStore);

  save(profile: SavedProfile): Promise<void> {
    return this.store.setObject(StorageKeys.profile, profile);
  }

  load(): Promise<SavedProfile | null> {
    return this.store.getObject<SavedProfile>(StorageKeys.profile);
  }
}
```

- [ ] **Step 3: Write the failing use-case test**

`src/app/features/offline-storage/domain/usecases/save-profile.use-case.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { SaveProfileUseCase } from './save-profile.use-case';
import { LoadProfileUseCase } from './load-profile.use-case';
import {
  IProfileStorageRepository,
  PROFILE_STORAGE_REPOSITORY,
} from '../repositories/profile-storage.repository.port';
import { SavedProfile } from '../model/saved-profile';

describe('Save/Load ProfileUseCase', () => {
  function fakeRepo(): IProfileStorageRepository {
    let stored: SavedProfile | null = null;
    return {
      save: async (p) => { stored = p; },
      load: async () => stored,
    };
  }

  function make() {
    TestBed.configureTestingModule({
      providers: [
        SaveProfileUseCase,
        LoadProfileUseCase,
        { provide: PROFILE_STORAGE_REPOSITORY, useValue: fakeRepo() },
      ],
    });
    return {
      save: TestBed.inject(SaveProfileUseCase),
      load: TestBed.inject(LoadProfileUseCase),
    };
  }

  it('saves then loads the profile (ok results)', async () => {
    const { save, load } = make();
    const saveRes = await save.run({ firstName: 'Ada', secondName: 'Lovelace' });
    expect(saveRes.ok).toBe(true);
    const loadRes = await load.run();
    expect(loadRes.ok).toBe(true);
    if (loadRes.ok) {
      expect(loadRes.data).toEqual({ firstName: 'Ada', secondName: 'Lovelace' });
    }
  });

  it('load returns ok(null) when nothing was saved', async () => {
    const { load } = make();
    const res = await load.run();
    expect(res.ok).toBe(true);
    if (res.ok) {
      expect(res.data).toBeNull();
    }
  });
});
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `npm test -- save-profile.use-case.spec`
Expected: FAIL — modules not found.

- [ ] **Step 5: Implement both use cases**

`src/app/features/offline-storage/domain/usecases/save-profile.use-case.ts`:
```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SavedProfile } from '../model/saved-profile';
import { PROFILE_STORAGE_REPOSITORY } from '../repositories/profile-storage.repository.port';

@Injectable()
export class SaveProfileUseCase extends UseCase<SavedProfile, void> {
  private readonly repo = inject(PROFILE_STORAGE_REPOSITORY);

  constructor() {
    super('SaveProfile');
  }

  protected async execute(input: SavedProfile): Promise<void> {
    await this.repo.save(input);
  }
}
```

`src/app/features/offline-storage/domain/usecases/load-profile.use-case.ts`:
```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SavedProfile } from '../model/saved-profile';
import { PROFILE_STORAGE_REPOSITORY } from '../repositories/profile-storage.repository.port';

@Injectable()
export class LoadProfileUseCase extends UseCase<void, SavedProfile | null> {
  private readonly repo = inject(PROFILE_STORAGE_REPOSITORY);

  constructor() {
    super('LoadProfile');
  }

  protected async execute(): Promise<SavedProfile | null> {
    return this.repo.load();
  }
}
```

- [ ] **Step 6: Run the use-case test to verify it passes**

Run: `npm test -- save-profile.use-case.spec`
Expected: PASS — 2 tests.

- [ ] **Step 7: Write the failing ViewModel test**

`src/app/features/offline-storage/presentation/viewmodels/save-profile.viewmodel.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { SaveProfileViewModel } from './save-profile.viewmodel';
import { SaveProfileUseCase } from '../../domain/usecases/save-profile.use-case';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';

describe('SaveProfileViewModel', () => {
  function make(run: SaveProfileUseCase['run']): SaveProfileViewModel {
    TestBed.configureTestingModule({
      providers: [
        SaveProfileViewModel,
        { provide: SaveProfileUseCase, useValue: { run } },
      ],
    });
    return TestBed.inject(SaveProfileViewModel);
  }

  it('sets saved=true on success', async () => {
    const vm = make(async () => ok(undefined));
    const okFlag = await vm.save('Ada', 'Lovelace');
    expect(okFlag).toBe(true);
    expect(vm.saved()).toBe(true);
    expect(vm.error()).toBeNull();
  });

  it('sets error on failure', async () => {
    const vm = make(async () => fail(new AppError('disk full', 'storage')));
    const okFlag = await vm.save('Ada', 'Lovelace');
    expect(okFlag).toBe(false);
    expect(vm.error()).toBe('disk full');
  });
});
```

- [ ] **Step 8: Run the test to verify it fails**

Run: `npm test -- save-profile.viewmodel.spec`
Expected: FAIL — module not found.

- [ ] **Step 9: Implement both ViewModels**

`src/app/features/offline-storage/presentation/viewmodels/save-profile.viewmodel.ts`:
```ts
import { Injectable, inject, signal } from '@angular/core';
import { createLogger } from '@core/logging/logger';
import { SaveProfileUseCase } from '../../domain/usecases/save-profile.use-case';

@Injectable()
export class SaveProfileViewModel {
  private readonly useCase = inject(SaveProfileUseCase);
  private readonly log = createLogger('viewmodel', 'SaveProfileViewModel');

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly saved = signal(false);

  async save(firstName: string, secondName: string): Promise<boolean> {
    this.log.debug('save()');
    this.loading.set(true);
    this.error.set(null);
    const result = await this.useCase.run({ firstName, secondName });
    this.loading.set(false);
    if (result.ok) {
      this.saved.set(true);
      return true;
    }
    this.error.set(result.error.message);
    this.log.warn('failed:', result.error.message);
    return false;
  }
}
```

`src/app/features/offline-storage/presentation/viewmodels/retrieve-profile.viewmodel.ts`:
```ts
import { Injectable, inject, signal } from '@angular/core';
import { createLogger } from '@core/logging/logger';
import { LoadProfileUseCase } from '../../domain/usecases/load-profile.use-case';
import { SavedProfile } from '../../domain/model/saved-profile';

@Injectable()
export class RetrieveProfileViewModel {
  private readonly useCase = inject(LoadProfileUseCase);
  private readonly log = createLogger('viewmodel', 'RetrieveProfileViewModel');

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly profile = signal<SavedProfile | null>(null);

  async load(): Promise<void> {
    this.log.debug('load()');
    this.loading.set(true);
    this.error.set(null);
    const result = await this.useCase.run();
    this.loading.set(false);
    if (result.ok) {
      this.profile.set(result.data);
    } else {
      this.error.set(result.error.message);
      this.log.warn('failed:', result.error.message);
    }
  }
}
```

- [ ] **Step 10: Run the ViewModel test to verify it passes**

Run: `npm test -- save-profile.viewmodel.spec`
Expected: PASS — 2 tests.

- [ ] **Step 11: Implement the three pages + barrel**

`src/app/features/offline-storage/presentation/pages/offline-storage.page.ts`:
```ts
// OfflineStoragePage: hub linking to the save and retrieve demos.
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-offline-storage-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="page">
      <h1>Offline Storage</h1>
      <a routerLink="/saving-data">Save data</a>
      <a routerLink="/retrieved-data">Retrieve data</a>
    </section>
  `,
  styles: [`.page { padding: var(--space-lg); display: flex; flex-direction: column; gap: var(--space-sm); } a { color: var(--color-primary); }`],
})
export class OfflineStoragePage {}
```

`src/app/features/offline-storage/presentation/pages/saving-data.page.ts`:
```ts
// SavingDataPage: a form that saves a profile through the clean-arch layers.
import { Component, inject, signal } from '@angular/core';
import { NavButtonComponent } from '@core/ui/components/nav-button.component';
import { TextFieldComponent } from '@core/ui/components/text-field.component';
import { NotificationService } from '@core/ui/notification.service';
import { SaveProfileViewModel } from '../viewmodels/save-profile.viewmodel';

@Component({
  selector: 'app-saving-data-page',
  standalone: true,
  imports: [NavButtonComponent, TextFieldComponent],
  template: `
    <section class="page">
      <h1>Saving Data</h1>
      <app-text-field
        label="First name"
        [value]="firstName()"
        (valueChange)="firstName.set($event)"
      />
      <app-text-field
        label="Second name"
        [value]="secondName()"
        (valueChange)="secondName.set($event)"
      />
      <app-nav-button label="Save" (press)="onSave()" [disabled]="vm.loading()" />
    </section>
  `,
  styles: [`.page { padding: var(--space-lg); display: flex; flex-direction: column; gap: var(--space-md); max-width: 360px; }`],
})
export class SavingDataPage {
  readonly vm = inject(SaveProfileViewModel);
  private readonly notifications = inject(NotificationService);

  readonly firstName = signal('');
  readonly secondName = signal('');

  async onSave(): Promise<void> {
    const okFlag = await this.vm.save(this.firstName(), this.secondName());
    if (okFlag) {
      this.notifications.success('Profile saved');
    } else {
      this.notifications.error(this.vm.error() ?? 'Save failed');
    }
  }
}
```

`src/app/features/offline-storage/presentation/pages/retrieved-data.page.ts`:
```ts
// RetrievedDataPage: loads and displays the saved profile.
import { Component, inject, OnInit } from '@angular/core';
import { RetrieveProfileViewModel } from '../viewmodels/retrieve-profile.viewmodel';

@Component({
  selector: 'app-retrieved-data-page',
  standalone: true,
  template: `
    <section class="page">
      <h1>Retrieved Data</h1>
      @if (vm.loading()) { <p>Loading…</p> }
      @if (vm.profile(); as p) {
        <p>First name: {{ p.firstName }}</p>
        <p>Second name: {{ p.secondName }}</p>
      } @else if (!vm.loading()) {
        <p>No profile saved yet.</p>
      }
    </section>
  `,
  styles: [`.page { padding: var(--space-lg); display: flex; flex-direction: column; gap: var(--space-sm); }`],
})
export class RetrievedDataPage implements OnInit {
  readonly vm = inject(RetrieveProfileViewModel);

  ngOnInit(): void {
    void this.vm.load();
  }
}
```

`src/app/features/offline-storage/index.ts`:
```ts
export { OfflineStoragePage } from './presentation/pages/offline-storage.page';
export { SavingDataPage } from './presentation/pages/saving-data.page';
export { RetrievedDataPage } from './presentation/pages/retrieved-data.page';
export { SaveProfileViewModel } from './presentation/viewmodels/save-profile.viewmodel';
export { RetrieveProfileViewModel } from './presentation/viewmodels/retrieve-profile.viewmodel';
export { SaveProfileUseCase } from './domain/usecases/save-profile.use-case';
export { LoadProfileUseCase } from './domain/usecases/load-profile.use-case';
export { ProfileStorageRepositoryImpl } from './data/repositories/profile-storage.repository';
export { PROFILE_STORAGE_REPOSITORY } from './domain/repositories/profile-storage.repository.port';
export type { SavedProfile } from './domain/model/saved-profile';
```

- [ ] **Step 12: Wire the routes**

Add to `src/app/app.routes.ts` (import the provider symbols, then add three routes inside the `routes` array). The three pages share the repository + use cases, so provide them on each route that needs them. Import only the DI symbols (the pages are lazy-loaded via `loadComponent`):
```ts
import {
  PROFILE_STORAGE_REPOSITORY,
  ProfileStorageRepositoryImpl,
  SaveProfileUseCase,
  LoadProfileUseCase,
  SaveProfileViewModel,
  RetrieveProfileViewModel,
} from '@features/offline-storage';

// ...inside the routes array, after the 'api' route:
{
  path: 'offline-storage',
  loadComponent: () =>
    import('@features/offline-storage').then((m) => m.OfflineStoragePage),
},
{
  path: 'saving-data',
  loadComponent: () =>
    import('@features/offline-storage').then((m) => m.SavingDataPage),
  providers: [
    { provide: PROFILE_STORAGE_REPOSITORY, useClass: ProfileStorageRepositoryImpl },
    SaveProfileUseCase,
    SaveProfileViewModel,
  ],
},
{
  path: 'retrieved-data',
  loadComponent: () =>
    import('@features/offline-storage').then((m) => m.RetrievedDataPage),
  providers: [
    { provide: PROFILE_STORAGE_REPOSITORY, useClass: ProfileStorageRepositoryImpl },
    LoadProfileUseCase,
    RetrieveProfileViewModel,
  ],
},
```

- [ ] **Step 13: Run the full suite + build**

Run: `npm test` then `npm run build`
Expected: all tests PASS; build succeeds.

- [ ] **Step 14: Commit**

```bash
git add src/app/features/offline-storage src/app/app.routes.ts
git commit -m "feat(offline-storage): add save/load profile vertical slice

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 13: Feature — `design/profile-form` (reactive form → summary)

**Files:**
- Create: `src/app/features/design/profile-form/presentation/profile-form.store.ts`
- Create: `src/app/features/design/profile-form/presentation/pages/profile-form.page.ts`
- Create: `src/app/features/design/profile-form/presentation/pages/profile-summary.page.ts`
- Create: `src/app/features/design/profile-form/index.ts`
- Modify: `src/app/app.routes.ts`
- Test: `.../pages/profile-form.page.spec.ts`

**Interfaces:**
- Consumes: `ReactiveFormsModule`, `Router`, `RouterLink`, `NavButtonComponent`.
- Produces (barrel `@features/design/profile-form`): `ProfileFormPage`, `ProfileSummaryPage`, `ProfileFormStore` (signal holding `{ firstName: string; lastName: string } | null`).

- [ ] **Step 1: Implement the feature store**

`src/app/features/design/profile-form/presentation/profile-form.store.ts`:
```ts
// ProfileFormStore: a tiny feature-scoped signal store that carries the submitted
// form data from the form page to the summary page (the web equivalent of passing
// route params on the mobile base). Provided once on the parent route so both pages
// share the same instance.
import { Injectable, signal } from '@angular/core';

export type ProfileFormData = { firstName: string; lastName: string };

@Injectable()
export class ProfileFormStore {
  readonly profile = signal<ProfileFormData | null>(null);

  set(data: ProfileFormData): void {
    this.profile.set(data);
  }
}
```

- [ ] **Step 2: Write the failing form test**

`src/app/features/design/profile-form/presentation/pages/profile-form.page.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ProfileFormPage } from './profile-form.page';
import { ProfileFormStore } from '../profile-form.store';

describe('ProfileFormPage', () => {
  function make(): ProfileFormPage {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), ProfileFormStore],
    });
    const fixture = TestBed.createComponent(ProfileFormPage);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('is invalid when fields are empty and valid when filled', () => {
    const cmp = make();
    expect(cmp.form.valid).toBe(false);
    cmp.form.setValue({ firstName: 'Ada', lastName: 'Lovelace' });
    expect(cmp.form.valid).toBe(true);
  });
});
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `npm test -- profile-form.page.spec`
Expected: FAIL — module not found.

- [ ] **Step 4: Implement the form page**

`src/app/features/design/profile-form/presentation/pages/profile-form.page.ts`:
```ts
// ProfileFormPage: an Angular reactive form with required-field validation. On
// submit it stores the data and navigates to the summary page.
import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NavButtonComponent } from '@core/ui/components/nav-button.component';
import { ProfileFormStore } from '../profile-form.store';

@Component({
  selector: 'app-profile-form-page',
  standalone: true,
  imports: [ReactiveFormsModule, NavButtonComponent],
  template: `
    <section class="page">
      <h1>Profile Form</h1>
      <form [formGroup]="form" (ngSubmit)="onSubmit()">
        <label>First name <input formControlName="firstName" /></label>
        <label>Last name <input formControlName="lastName" /></label>
        <app-nav-button label="Continue" (press)="onSubmit()" [disabled]="form.invalid" />
      </form>
    </section>
  `,
  styles: [`.page { padding: var(--space-lg); } form { display: flex; flex-direction: column; gap: var(--space-md); max-width: 360px; } label { display: flex; flex-direction: column; gap: var(--space-xs); } input { padding: var(--space-sm); border: 1px solid var(--color-border); border-radius: var(--radius-sm); }`],
})
export class ProfileFormPage {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly store = inject(ProfileFormStore);

  readonly form = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
  });

  onSubmit(): void {
    if (this.form.invalid) {
      return;
    }
    this.store.set(this.form.getRawValue());
    void this.router.navigate(['/profile-summary']);
  }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `npm test -- profile-form.page.spec`
Expected: PASS — 1 test.

- [ ] **Step 6: Implement the summary page + barrel**

`src/app/features/design/profile-form/presentation/pages/profile-summary.page.ts`:
```ts
// ProfileSummaryPage: shows the data submitted on the form page; redirects back if
// the store is empty (e.g. on a direct page load / refresh).
import { Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { ProfileFormStore } from '../profile-form.store';

@Component({
  selector: 'app-profile-summary-page',
  standalone: true,
  template: `
    <section class="page">
      <h1>Profile Summary</h1>
      @if (store.profile(); as p) {
        <p>First name: {{ p.firstName }}</p>
        <p>Last name: {{ p.lastName }}</p>
      }
    </section>
  `,
  styles: [`.page { padding: var(--space-lg); display: flex; flex-direction: column; gap: var(--space-sm); }`],
})
export class ProfileSummaryPage implements OnInit {
  readonly store = inject(ProfileFormStore);
  private readonly router = inject(Router);

  ngOnInit(): void {
    if (!this.store.profile()) {
      void this.router.navigate(['/profile-form']);
    }
  }
}
```

`src/app/features/design/profile-form/index.ts`:
```ts
export { ProfileFormPage } from './presentation/pages/profile-form.page';
export { ProfileSummaryPage } from './presentation/pages/profile-summary.page';
export { ProfileFormStore } from './presentation/profile-form.store';
```

- [ ] **Step 7: Wire the routes (shared store via a parent route)**

Add to `src/app/app.routes.ts`. Because both pages share `ProfileFormStore`, provide it on a parent route whose children are the two pages:
```ts
import { ProfileFormStore } from '@features/design/profile-form';

// ...inside the routes array:
{
  path: '',
  providers: [ProfileFormStore],
  children: [
    {
      path: 'profile-form',
      loadComponent: () =>
        import('@features/design/profile-form').then((m) => m.ProfileFormPage),
    },
    {
      path: 'profile-summary',
      loadComponent: () =>
        import('@features/design/profile-form').then((m) => m.ProfileSummaryPage),
    },
  ],
},
```

- [ ] **Step 8: Run the full suite + build**

Run: `npm test` then `npm run build`
Expected: all tests PASS; build succeeds.

- [ ] **Step 9: Commit**

```bash
git add src/app/features/design/profile-form src/app/app.routes.ts
git commit -m "feat(design): add profile-form reactive form + summary

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 14: Feature — `design/design-hub` (sidebar nav + components + animations)

**Files:**
- Create: `src/app/features/design/design-hub/presentation/components/sidebar-nav.component.ts`
- Create: `src/app/features/design/design-hub/presentation/pages/design-hub.page.ts`
- Create: `src/app/features/design/design-hub/presentation/pages/animation-demo.page.ts`
- Create: `src/app/features/design/design-hub/index.ts`
- Modify: `src/app/app.routes.ts`
- Test: `.../components/sidebar-nav.component.spec.ts`

**Interfaces:**
- Consumes: `RouterLink`, `NavButtonComponent`, `TextFieldComponent`, `@angular/animations`.
- Produces (barrel `@features/design/design-hub`): `DesignHubPage`, `AnimationDemoPage`, `SidebarNavComponent` (selector `app-sidebar-nav`, signal `open`, method `toggle()`).

- [ ] **Step 1: Write the failing sidebar test**

`src/app/features/design/design-hub/presentation/components/sidebar-nav.component.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SidebarNavComponent } from './sidebar-nav.component';

describe('SidebarNavComponent', () => {
  it('toggles the open state', () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(SidebarNavComponent);
    const cmp = fixture.componentInstance;
    fixture.detectChanges();
    expect(cmp.open()).toBe(false);
    cmp.toggle();
    expect(cmp.open()).toBe(true);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test -- sidebar-nav.component.spec`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement the sidebar nav**

`src/app/features/design/design-hub/presentation/components/sidebar-nav.component.ts`:
```ts
// SidebarNavComponent: a responsive drawer (the web equivalent of the mobile
// slide-menu + bottom-nav demos). A button toggles an off-canvas panel of links.
import { Component, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-sidebar-nav',
  standalone: true,
  imports: [RouterLink],
  template: `
    <button class="toggle" (click)="toggle()">☰ Menu</button>
    <aside class="drawer" [class.open]="open()">
      <a routerLink="/" (click)="close()">Home</a>
      <a routerLink="/api" (click)="close()">API</a>
      <a routerLink="/offline-storage" (click)="close()">Offline Storage</a>
      <a routerLink="/profile-form" (click)="close()">Profile Form</a>
      <a routerLink="/design/animations" (click)="close()">Animations</a>
    </aside>
  `,
  styles: [
    `
      .toggle { background: none; border: 1px solid var(--color-border); border-radius: var(--radius-sm); padding: var(--space-sm) var(--space-md); cursor: pointer; }
      .drawer {
        position: fixed; top: 0; left: 0; height: 100%; width: 240px;
        background: var(--color-background); border-right: 1px solid var(--color-border);
        padding: var(--space-lg); display: flex; flex-direction: column; gap: var(--space-md);
        transform: translateX(-100%); transition: transform 200ms ease;
      }
      .drawer.open { transform: translateX(0); }
      a { color: var(--color-primary); text-decoration: none; }
    `,
  ],
})
export class SidebarNavComponent {
  readonly open = signal(false);

  toggle(): void {
    this.open.update((v) => !v);
  }

  close(): void {
    this.open.set(false);
  }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm test -- sidebar-nav.component.spec`
Expected: PASS — 1 test.

- [ ] **Step 5: Implement the design hub page**

`src/app/features/design/design-hub/presentation/pages/design-hub.page.ts`:
```ts
// DesignHubPage: a showcase of the themed shared components plus the sidebar drawer
// and a link to the animation demo.
import { Component, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NavButtonComponent } from '@core/ui/components/nav-button.component';
import { TextFieldComponent } from '@core/ui/components/text-field.component';
import { SidebarNavComponent } from '../components/sidebar-nav.component';

@Component({
  selector: 'app-design-hub-page',
  standalone: true,
  imports: [RouterLink, NavButtonComponent, TextFieldComponent, SidebarNavComponent],
  template: `
    <app-sidebar-nav />
    <section class="page">
      <h1>Design</h1>
      <h2>Buttons</h2>
      <app-nav-button label="Primary button" (press)="clicks.set(clicks() + 1)" />
      <p>Clicked {{ clicks() }} times</p>
      <h2>Inputs</h2>
      <app-text-field
        label="Sample input"
        placeholder="Type here"
        [value]="sample()"
        (valueChange)="sample.set($event)"
      />
      <p>Value: {{ sample() }}</p>
      <h2>More</h2>
      <a routerLink="/design/animations">Animation demo →</a>
    </section>
  `,
  styles: [`.page { padding: var(--space-lg); display: flex; flex-direction: column; gap: var(--space-md); max-width: 480px; } a { color: var(--color-primary); }`],
})
export class DesignHubPage {
  readonly clicks = signal(0);
  readonly sample = signal('');
}
```

- [ ] **Step 6: Implement the animation demo page**

`src/app/features/design/design-hub/presentation/pages/animation-demo.page.ts`:
```ts
// AnimationDemoPage: a minimal Angular animations demo — a box that fades/slides in
// and out as it is toggled. Requires provideAnimations() (set in app.config.ts).
import { Component, signal } from '@angular/core';
import { animate, style, transition, trigger } from '@angular/animations';
import { NavButtonComponent } from '@core/ui/components/nav-button.component';

@Component({
  selector: 'app-animation-demo-page',
  standalone: true,
  imports: [NavButtonComponent],
  animations: [
    trigger('fadeSlide', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(-12px)' }),
        animate('250ms ease-out', style({ opacity: 1, transform: 'translateY(0)' })),
      ]),
      transition(':leave', [
        animate('200ms ease-in', style({ opacity: 0, transform: 'translateY(-12px)' })),
      ]),
    ]),
  ],
  template: `
    <section class="page">
      <h1>Animation Demo</h1>
      <app-nav-button label="Toggle box" (press)="visible.set(!visible())" />
      @if (visible()) {
        <div class="box" @fadeSlide>Hello 👋</div>
      }
    </section>
  `,
  styles: [`.page { padding: var(--space-lg); display: flex; flex-direction: column; gap: var(--space-md); } .box { background: var(--color-primary); color: var(--color-on-primary); padding: var(--space-lg); border-radius: var(--radius-md); width: 160px; text-align: center; }`],
})
export class AnimationDemoPage {
  readonly visible = signal(true);
}
```

`src/app/features/design/design-hub/index.ts`:
```ts
export { DesignHubPage } from './presentation/pages/design-hub.page';
export { AnimationDemoPage } from './presentation/pages/animation-demo.page';
export { SidebarNavComponent } from './presentation/components/sidebar-nav.component';
```

- [ ] **Step 7: Wire the routes**

Add to `src/app/app.routes.ts`:
```ts
// ...inside the routes array:
{
  path: 'design',
  loadComponent: () =>
    import('@features/design/design-hub').then((m) => m.DesignHubPage),
},
{
  path: 'design/animations',
  loadComponent: () =>
    import('@features/design/design-hub').then((m) => m.AnimationDemoPage),
},
```

- [ ] **Step 8: Run the full suite + build**

Run: `npm test` then `npm run build`
Expected: all tests PASS; build succeeds.

- [ ] **Step 9: Manual smoke (optional)**

Run: `npm start`, open the app, visit `/design`, toggle the sidebar, click into Animations and toggle the box. Stop the server when done.

- [ ] **Step 10: Commit**

```bash
git add src/app/features/design/design-hub src/app/app.routes.ts
git commit -m "feat(design): add design hub (sidebar nav, components, animations)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 15: Docs — architecture guide + README

**Files:**
- Create: `docs/architecture/README.md`
- Modify: `README.md` (replace the CLI-generated default)

**Interfaces:** none (documentation only).

- [ ] **Step 1: Write the architecture guide**

Create `docs/architecture/README.md` with these sections (write real prose, not placeholders):
  1. **Overview** — Clean Architecture + MVVM, feature-first; one-way dependency presentation → domain → data → core.
  2. **Layer diagram** — reproduce the data-flow diagram from the design spec (`Page → ViewModel → UseCase.run() → Repository → http/storage/crypto`, with `Result`/`AppError` flowing back).
  3. **Folder structure** — copy the tree from the design spec's "Folder structure" section.
  4. **Mobile ↔ Angular mapping table** — copy the composition-root mapping table from the design spec.
  5. **Naming conventions** — `…DtoRs`/`…DtoRq`, `I…Repository` + `…_REPOSITORY` token, `…UseCase`, `…RepositoryImpl`, `…ViewModel`, kebab-case filenames.
  6. **How to add a new feature** — a numbered checklist: create the `data/domain/presentation` folders; add DTO + validator; add model + mapper; add port interface + token; add repository impl; add use case (extends `UseCase`); add viewmodel (signals); add page; add `index.ts` barrel; wire the route with feature-scoped `providers`; add a use-case test + a viewmodel test.
  7. **Testing** — `npm test` (Jest); what each layer's tests cover.
  8. **Links** — link to `docs/superpowers/specs/2026-06-26-angular-base-design.md`.

- [ ] **Step 2: Replace the project README**

Replace `README.md`:
```markdown
# Base Frontend

An Angular base architecture (Clean Architecture + MVVM, feature-first) — the web
counterpart of the React Native mobile base. Standalone Angular + Signals, with a
Promise + `Result<T>` domain layer.

## Getting started

```sh
npm install
npm start      # ng serve — dev server
npm test       # Jest
npm run build  # production build
```

## Architecture

See [docs/architecture/README.md](docs/architecture/README.md) for the layer model,
folder conventions, the mobile↔Angular mapping, and a "how to add a feature" guide.
The original design spec is in
[docs/superpowers/specs/2026-06-26-angular-base-design.md](docs/superpowers/specs/2026-06-26-angular-base-design.md).

## Layout

- `src/app/core/` — cross-cutting infra: `Result`, `AppError`, `UseCase`, logger,
  env, HTTP client, KeyValueStore, CryptoService, theme, shared UI.
- `src/app/features/<group>/<feature>/{data,domain,presentation}` — vertical slices.
```

- [ ] **Step 3: Verify the docs render and links resolve**

Run:
```powershell
@("docs/architecture/README.md","README.md","docs/superpowers/specs/2026-06-26-angular-base-design.md") | ForEach-Object { "{0}`t{1}" -f $_, (Test-Path $_) }
```
Expected: all `True`.

- [ ] **Step 4: Commit**

```bash
git add README.md docs/architecture
git commit -m "docs: add architecture guide and project README

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Final verification (run after Task 15)

- [ ] `npm test` — all suites pass.
- [ ] `npm run build` — production build succeeds.
- [ ] `npm start` — Home renders; each feature is reachable and works end-to-end:
  - `/api` → "Fetch Posts" loads posts and toasts success.
  - `/saving-data` → save a profile; `/retrieved-data` → shows it back.
  - `/profile-form` → validates, continues to `/profile-summary`.
  - `/design` → sidebar toggles; `/design/animations` → box animates.
- [ ] `git log --oneline` — one commit per task, design spec + plan present.

## Done When

- All task checkboxes (Tasks 1–15) are checked.
- The Angular base mirrors the mobile base's structure, taxonomy, and concept names, with the two approved adaptations (DI-token ports, kebab-case filenames).
- `core/` provides `Result`, `AppError`, `UseCase`, `Logger`, `env`, `HttpClientService`, `KeyValueStore`, `CryptoService`, theme, and shared UI — each preserving the mobile contract.
- All four worked-example feature groups work and pass their tests.
- The architecture guide and README are present and committed.
