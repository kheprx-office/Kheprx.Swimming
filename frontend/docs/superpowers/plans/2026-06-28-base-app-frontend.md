# Base App Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the copied `Base Frontend` into **Base App Frontend** — a batteries-included, domain-neutral Angular base with a hardened all-verb HTTP client, a swappable data-source seam, generic auth (roles + guards + auth-gated demo), Tailwind+lucide styling, and a full HTML doc site — building clean, Jest-green, and runnable offline.

**Architecture:** Clean Architecture + MVVM (unchanged from Base Frontend). New cross-cutting code lives under `src/app/core/` (`core/auth`, `core/guards`, `core/datasource/api`, `core/network/interceptors`). Repositories depend on a transport **seam** (`API_DATA_SOURCE`) bound to a seed-backed `MockApiDataSource` by default; auth uses a parallel `AUTH_DATA_SOURCE` seam (`MockAuthDataSource` default). Going live = swap the two Mock bindings to the Http impls and set `env.api.baseUrl`.

**Tech Stack:** Angular 20.3 (standalone APIs + signals), TypeScript 5.9 (strict), RxJS 7.8, Jest 30 + jest-preset-angular 17, Tailwind 3.4.17 + lucide-angular, SCSS design tokens.

## Global Constraints

- Angular 20 **standalone** components/APIs and **signals** only — no NgModules.
- Tests: **Jest only** (`jest-preset-angular`), spec files named `*.spec.ts`. No Karma.
- Path aliases: `@core/*` → `src/app/core/*`, `@features/*` → `src/app/features/*`.
- All async failures map to **`AppError`**; use cases extend **`UseCase<I,O>`** and return **`Result<T>`** (`{ ok: true, data } | { ok: false, error }`).
- **Domain-neutral:** no electrician domain, no Arabic strings. Roles are exactly **`type UserRole = 'admin' | 'user'`**. Demo users: `admin@example.com`/`admin123` (admin), `user@example.com`/`user123` (user).
- Dependency pins (match the source project): **`tailwindcss@3.4.17`**, `postcss@^8.5.15`, `autoprefixer@^10.5.0`, `lucide-angular@^1.0.0`.
- Tailwind's theme **maps to the existing CSS custom properties** in `src/app/core/ui/theme/theme.scss` — one palette, no divergence.
- **Mock data sources are bound by default** (offline-runnable). Do not bind the Http impls.
- Each task ends **building clean** (`npm run build`) and **Jest-green** (`npx jest`); **commit** after each task. `node_modules/`, `.angular/`, `dist/` are gitignored.

---

### Task 0: Restore dependencies & establish the green baseline

**Files:** none changed (dependency restore only).

- [ ] **Step 1: Install dependencies**

The project was copied without `node_modules`. Restore them:

Run: `npm install`
Expected: completes; `node_modules/` appears (gitignored).

- [ ] **Step 2: Confirm the untouched base is Jest-green**

Run: `npx jest`
Expected: all existing suites PASS (~40 specs across fetch-posts, offline-storage, profile-form, core).

- [ ] **Step 3: Confirm the base builds**

Run: `npm run build`
Expected: build succeeds with no errors.

- [ ] **Step 4: Confirm clean git status**

Run: `git status -s`
Expected: empty (no commit needed — this task only restores ignored `node_modules`).

---

## Phase 1 — Foundation (errors, HTTP, seam)

### Task 1: Extend AppError with the `auth` kind and `code`

**Files:**
- Modify: `src/app/core/domain/errors/app-error.ts`
- Test: `src/app/core/domain/errors/app-error.spec.ts` (create)

**Interfaces:**
- Produces: `AppErrorKind` union now includes `'auth'`; `new AppError(message, kind, status?, code?)` with readonly `kind`, `status?`, `code?`.

- [ ] **Step 1: Write the failing test**

Create `src/app/core/domain/errors/app-error.spec.ts`:

```ts
import { AppError } from '@core/domain/errors/app-error';

describe('AppError', () => {
  it('carries kind, status, and code, and is an Error', () => {
    const e = new AppError('nope', 'auth', 401, 'E_AUTH');
    expect(e).toBeInstanceOf(Error);
    expect(e.name).toBe('AppError');
    expect(e.kind).toBe('auth');
    expect(e.status).toBe(401);
    expect(e.code).toBe('E_AUTH');
  });
});
```

- [ ] **Step 2: Run it and watch it fail**

Run: `npx jest src/app/core/domain/errors/app-error.spec.ts`
Expected: FAIL — `'auth'` is not assignable to `AppErrorKind`, and `code` does not exist.

- [ ] **Step 3: Replace `app-error.ts` with the 7-kind version**

```ts
// AppError: a typed error carrying the failure kind (and HTTP status / backend code when known).
export type AppErrorKind =
  | 'network'
  | 'http'
  | 'validation'
  | 'storage'
  | 'crypto'
  | 'auth'
  | 'unknown';

export class AppError extends Error {
  readonly kind: AppErrorKind;
  readonly status?: number;
  readonly code?: string;

  constructor(message: string, kind: AppErrorKind, status?: number, code?: string) {
    super(message);
    this.name = 'AppError';
    this.kind = kind;
    this.status = status;
    this.code = code;
  }
}
```

- [ ] **Step 4: Run the test (and the full suite) to verify green**

Run: `npx jest src/app/core/domain/errors/app-error.spec.ts`
Expected: PASS.
Run: `npx jest`
Expected: all PASS (no regressions).

- [ ] **Step 5: Commit**

```bash
git add src/app/core/domain/errors/app-error.ts src/app/core/domain/errors/app-error.spec.ts
git commit -m "feat(core): add 'auth' AppError kind and backend code field"
```

---

### Task 2: All-verb HTTP client + http-error mapping

**Files:**
- Create: `src/app/core/network/api/http-error.ts`
- Modify (replace): `src/app/core/network/api/http-client.ts`
- Modify (replace): `src/app/core/network/api/http-client.spec.ts`
- Modify: `src/app/features/api/fetch-posts/data/repositories/post.repository.ts` (interim: `request` → `get`, keeps build green)

**Interfaces:**
- Produces: `HttpClientService.get/post/put/patch/delete<T>(path, opts?)`; `interface HttpRequestOptions { body?; headers?; params? }`; `type HttpMethod`; `toAppError(err): AppError` (0→network, 401→auth, else→http with status+code).
- Consumes: `AppError` (Task 1).

- [ ] **Step 1: Write `http-error.ts`**

```ts
// toAppError: maps an HttpErrorResponse (or transport error) to a typed AppError,
// parsing the backend error body ({ message?, code? }) when present.
import { HttpErrorResponse } from '@angular/common/http';
import { AppError } from '@core/domain/errors/app-error';

export function toAppError(err: unknown): AppError {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) {
      return new AppError(err.message || 'Network request failed', 'network');
    }
    const body = err.error as { message?: string; code?: string } | string | null;
    const message =
      body && typeof body === 'object' && body.message ? body.message : `HTTP ${err.status}`;
    const code = body && typeof body === 'object' ? body.code : undefined;
    const kind = err.status === 401 ? 'auth' : 'http';
    return new AppError(message, kind, err.status, code);
  }
  // rxjs TimeoutError or any other transport failure
  return new AppError('Network request failed', 'network');
}
```

- [ ] **Step 2: Replace `http-client.spec.ts` with the all-verb spec (the failing test)**

```ts
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HttpClientService } from '@core/network/api/http-client';
import { env } from '@core/config/env';

describe('HttpClientService', () => {
  let svc: HttpClientService;
  let httpMock: HttpTestingController;
  const base = env.api.baseUrl;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    svc = TestBed.inject(HttpClientService);
    httpMock = TestBed.inject(HttpTestingController);
  });
  afterEach(() => httpMock.verify());

  it('POSTs a body and resolves the response', async () => {
    const p = svc.post<{ id: number }>('/things', { body: { name: 'x' } });
    const req = httpMock.expectOne(`${base}/things`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'x' });
    req.flush({ id: 1 });
    await expect(p).resolves.toEqual({ id: 1 });
  });

  it('serializes query params on GET', async () => {
    const p = svc.get('/things', { params: { page: 2, q: 'a' } });
    const req = httpMock.expectOne((r) => r.url === `${base}/things`);
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('q')).toBe('a');
    req.flush([]);
    await p;
  });

  it('maps a 401 to AppError(auth)', async () => {
    const p = svc.get('/secure');
    httpMock
      .expectOne(`${base}/secure`)
      .flush({ message: 'nope' }, { status: 401, statusText: 'Unauthorized' });
    await expect(p).rejects.toMatchObject({ kind: 'auth', status: 401, message: 'nope' });
  });

  it('maps a 500 with an error body to AppError(http) using the body message + code', async () => {
    const p = svc.get('/boom');
    httpMock
      .expectOne(`${base}/boom`)
      .flush({ message: 'server exploded', code: 'E_BOOM' }, { status: 500, statusText: 'Error' });
    await expect(p).rejects.toMatchObject({ kind: 'http', status: 500, message: 'server exploded', code: 'E_BOOM' });
  });
});
```

- [ ] **Step 3: Run it and watch it fail**

Run: `npx jest src/app/core/network/api/http-client.spec.ts`
Expected: FAIL — `svc.post`/`svc.get(path, opts)` don't exist on the GET-only client.

- [ ] **Step 4: Replace `http-client.ts` with the all-verb implementation**

```ts
// HttpClientService: a typed wrapper over Angular HttpClient. Returns Promises (so
// the Promise-based UseCase layer stays simple) and maps failures to AppError.
// Transport failures (status 0) and timeouts are retried with exponential backoff;
// HTTP errors (4xx/5xx) are never retried.
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, timer } from 'rxjs';
import { retry, timeout } from 'rxjs/operators';
import { env } from '@core/config/env';
import { createLogger } from '@core/logging/logger';
import { toAppError } from '@core/network/api/http-error';

export type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
export interface HttpRequestOptions {
  body?: unknown;
  headers?: Record<string, string>;
  params?: Record<string, string | number | boolean>;
}

@Injectable({ providedIn: 'root' })
export class HttpClientService {
  private readonly http = inject(HttpClient);
  private readonly log = createLogger('core', 'apiClient');
  private readonly baseUrl = env.api.baseUrl;

  get<T>(path: string, opts?: HttpRequestOptions): Promise<T> { return this.request<T>('GET', path, opts); }
  post<T>(path: string, opts?: HttpRequestOptions): Promise<T> { return this.request<T>('POST', path, opts); }
  put<T>(path: string, opts?: HttpRequestOptions): Promise<T> { return this.request<T>('PUT', path, opts); }
  patch<T>(path: string, opts?: HttpRequestOptions): Promise<T> { return this.request<T>('PATCH', path, opts); }
  delete<T>(path: string, opts?: HttpRequestOptions): Promise<T> { return this.request<T>('DELETE', path, opts); }

  private request<T>(method: HttpMethod, path: string, opts: HttpRequestOptions = {}): Promise<T> {
    const url = `${this.baseUrl}${path}`;
    let params = new HttpParams();
    for (const [k, v] of Object.entries(opts.params ?? {})) params = params.set(k, String(v));
    this.log.debug('→', method, url);

    return new Promise<T>((resolve, reject) => {
      (this.http.request<T>(method, url, {
        body: opts.body,
        headers: opts.headers,
        params,
      }) as Observable<unknown>)
        .pipe(
          timeout(env.api.timeoutMs),
          retry({
            count: env.api.maxRetries,
            delay: (error, attempt) => {
              if (!this.isTransient(error)) throw error;
              const ms = env.api.backoffMs[attempt - 1] ?? 0;
              this.log.warn(`retrying in ${ms}ms (attempt ${attempt + 1})`, url);
              return timer(ms);
            },
          }),
        )
        .subscribe({
          next: (v) => resolve(v as T),
          error: (err) => reject(toAppError(err)),
        });
    });
  }

  private isTransient(err: unknown): boolean {
    if (err instanceof HttpErrorResponse) return err.status === 0;
    return true; // rxjs TimeoutError / non-HTTP transport failure
  }
}
```

- [ ] **Step 5: Keep the build green — update the one `request()` caller**

The only caller of the old GET-only `request()` is `PostRepositoryImpl`. Verify there are no others:

Run: `git grep -n "\.request<" -- src` (or use the Grep tool)
Expected: only `src/app/features/api/fetch-posts/data/repositories/post.repository.ts`.

Edit `src/app/features/api/fetch-posts/data/repositories/post.repository.ts` — change the call from `request` to `get` (still using `HttpClientService` directly; the seam rewire is Task 4):

```ts
const raw = await this.http.get<{ posts: PostDtoRs[] }>('/posts');
```

(Only that line changes; the rest of the file is unchanged.)

- [ ] **Step 6: Run the suite to verify green**

Run: `npx jest`
Expected: all PASS (the new http-client spec + existing fetch-posts specs).
Run: `npm run build`
Expected: succeeds.

- [ ] **Step 7: Commit**

```bash
git add src/app/core/network/api/http-error.ts src/app/core/network/api/http-client.ts src/app/core/network/api/http-client.spec.ts src/app/features/api/fetch-posts/data/repositories/post.repository.ts
git commit -m "feat(core): all-verb HTTP client with status-aware error mapping"
```

---

### Task 3: ApiDataSource seam (interface + token + Http + Mock)

**Files:**
- Create: `src/app/core/datasource/api/api-data-source.ts`
- Create: `src/app/core/datasource/api/mock-api-data-source.ts`
- Create: `src/app/core/datasource/api/mock-api-data-source.spec.ts`
- Modify: `src/app/app.config.ts` (bind `API_DATA_SOURCE` → `MockApiDataSource`)

**Interfaces:**
- Produces: `interface ApiDataSource { get/post/put/patch/delete<T>(path, opts?) }`; `API_DATA_SOURCE` token; `HttpApiDataSource`; `MockApiDataSource` (registers `GET /posts` → `{ data: SEED_POSTS, success: true }`).
- Consumes: `HttpClientService`, `HttpRequestOptions` (Task 2); `BaseResponseRs`; `AppError`.

- [ ] **Step 1: Write `api-data-source.ts` (interface + token + real impl)**

```ts
// ApiDataSource: the swappable transport seam every repository depends on (via the
// API_DATA_SOURCE token). HttpApiDataSource is the real impl over HttpClientService;
// MockApiDataSource (separate file) serves seed data behind the same interface.
import { InjectionToken, Injectable, inject } from '@angular/core';
import { HttpClientService, HttpRequestOptions } from '@core/network/api/http-client';

export interface ApiDataSource {
  get<T>(path: string, opts?: HttpRequestOptions): Promise<T>;
  post<T>(path: string, opts?: HttpRequestOptions): Promise<T>;
  put<T>(path: string, opts?: HttpRequestOptions): Promise<T>;
  patch<T>(path: string, opts?: HttpRequestOptions): Promise<T>;
  delete<T>(path: string, opts?: HttpRequestOptions): Promise<T>;
}

export const API_DATA_SOURCE = new InjectionToken<ApiDataSource>('API_DATA_SOURCE');

@Injectable({ providedIn: 'root' })
export class HttpApiDataSource implements ApiDataSource {
  private readonly http = inject(HttpClientService);
  get<T>(path: string, opts?: HttpRequestOptions) { return this.http.get<T>(path, opts); }
  post<T>(path: string, opts?: HttpRequestOptions) { return this.http.post<T>(path, opts); }
  put<T>(path: string, opts?: HttpRequestOptions) { return this.http.put<T>(path, opts); }
  patch<T>(path: string, opts?: HttpRequestOptions) { return this.http.patch<T>(path, opts); }
  delete<T>(path: string, opts?: HttpRequestOptions) { return this.http.delete<T>(path, opts); }
}
```

- [ ] **Step 2: Write the failing mock spec**

Create `src/app/core/datasource/api/mock-api-data-source.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { MockApiDataSource } from '@core/datasource/api/mock-api-data-source';
import { BaseResponseRs } from '@core/network/api/base-response-rs';

describe('MockApiDataSource', () => {
  let api: MockApiDataSource;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [MockApiDataSource] });
    api = TestBed.inject(MockApiDataSource);
  });

  it('GET /posts returns the seed collection in an envelope', async () => {
    const res = await api.get<BaseResponseRs<unknown[]>>('/posts');
    expect(res.success).toBe(true);
    expect(res.data.length).toBeGreaterThan(0);
  });

  it('rejects an unhandled path with kind=unknown', async () => {
    await expect(api.get('/nope')).rejects.toMatchObject({ kind: 'unknown' });
  });
});
```

- [ ] **Step 3: Run it and watch it fail**

Run: `npx jest src/app/core/datasource/api/mock-api-data-source.spec.ts`
Expected: FAIL — `MockApiDataSource` does not exist.

- [ ] **Step 4: Write `mock-api-data-source.ts` (generic, seed posts — NO DataStore/domain)**

```ts
// MockApiDataSource: serves in-memory seed data behind the ApiDataSource interface,
// so repositories run unchanged until a real backend exists. Handlers are registered
// per route; an unmatched path rejects (surfacing missing wiring early). Swap the
// binding to HttpApiDataSource (and set env.api.baseUrl) to go live.
import { Injectable } from '@angular/core';
import { AppError } from '@core/domain/errors/app-error';
import { createLogger } from '@core/logging/logger';
import { ApiDataSource } from '@core/datasource/api/api-data-source';
import { HttpRequestOptions } from '@core/network/api/http-client';
import { BaseResponseRs } from '@core/network/api/base-response-rs';

type Handler = (opts: HttpRequestOptions, match: RegExpMatchArray) => BaseResponseRs<unknown>;
interface Route { method: string; pattern: RegExp; handler: Handler; }

const SEED_POSTS = [
  { id: 1, title: 'Hello, Base App Frontend', body: 'This post is served by MockApiDataSource — no backend required.', userId: 1 },
  { id: 2, title: 'Clean architecture', body: 'Vertical slices keep each feature isolated and testable.', userId: 1 },
  { id: 3, title: 'A swappable seam', body: 'Bind HttpApiDataSource and set env.api.baseUrl to go live.', userId: 2 },
];

@Injectable({ providedIn: 'root' })
export class MockApiDataSource implements ApiDataSource {
  private readonly log = createLogger('datasource', 'MockApi');
  private readonly routes: Route[] = [];

  constructor() { this.registerPosts(); }

  protected register(method: string, pattern: RegExp, handler: Handler): void {
    this.routes.push({ method, pattern, handler });
  }

  private async dispatch<T>(method: string, path: string, opts: HttpRequestOptions = {}): Promise<T> {
    await Promise.resolve(); // mimic async boundary
    const route = this.routes.find((r) => r.method === method && r.pattern.test(path));
    if (!route) {
      this.log.warn('unhandled', method, path);
      throw new AppError(`No mock handler for ${method} ${path}`, 'unknown');
    }
    return route.handler(opts, path.match(route.pattern)!) as T;
  }

  get<T>(path: string, opts?: HttpRequestOptions) { return this.dispatch<T>('GET', path, opts); }
  post<T>(path: string, opts?: HttpRequestOptions) { return this.dispatch<T>('POST', path, opts); }
  put<T>(path: string, opts?: HttpRequestOptions) { return this.dispatch<T>('PUT', path, opts); }
  patch<T>(path: string, opts?: HttpRequestOptions) { return this.dispatch<T>('PATCH', path, opts); }
  delete<T>(path: string, opts?: HttpRequestOptions) { return this.dispatch<T>('DELETE', path, opts); }

  // ── Posts endpoints (seed-backed reference resource) ────────────────────────
  private registerPosts(): void {
    this.register('GET', /^\/posts$/, () => ({ data: SEED_POSTS, success: true }));
  }
}
```

- [ ] **Step 5: Run the mock spec to verify green**

Run: `npx jest src/app/core/datasource/api/mock-api-data-source.spec.ts`
Expected: PASS.

- [ ] **Step 6: Bind the seam in the composition root**

Edit `src/app/app.config.ts` — add the imports and the provider (leave the rest as-is):

```ts
import { API_DATA_SOURCE } from '@core/datasource/api/api-data-source';
import { MockApiDataSource } from '@core/datasource/api/mock-api-data-source';
```

Add to the `providers` array (after `provideAnimations()`):

```ts
    { provide: API_DATA_SOURCE, useClass: MockApiDataSource },
```

- [ ] **Step 7: Verify build + full suite**

Run: `npm run build`
Expected: succeeds.
Run: `npx jest`
Expected: all PASS.

- [ ] **Step 8: Commit**

```bash
git add src/app/core/datasource/api/ src/app/app.config.ts
git commit -m "feat(core): swappable ApiDataSource seam with seed-backed mock (default)"
```

---

### Task 4: Wire fetch-posts through the seam

**Files:**
- Modify: `src/app/features/api/fetch-posts/data/repositories/post.repository.ts`
- Test: `src/app/features/api/fetch-posts/data/repositories/post.repository.spec.ts` (create)

**Interfaces:**
- Consumes: `API_DATA_SOURCE` / `ApiDataSource` (Task 3); `PostsDtoRs` (= `BaseResponseRs<PostDtoRs[]>`).
- Produces: `PostRepositoryImpl.getPosts(): Promise<PostsDtoRs>` reading the seam.

- [ ] **Step 1: Write the failing repository spec**

Create `src/app/features/api/fetch-posts/data/repositories/post.repository.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { PostRepositoryImpl } from './post.repository';
import { API_DATA_SOURCE, ApiDataSource } from '@core/datasource/api/api-data-source';
import { PostsDtoRs } from '../dto/post-dto-rs';

describe('PostRepositoryImpl', () => {
  it('returns the posts envelope from the data source', async () => {
    const envelope: PostsDtoRs = { data: [{ id: 1, title: 't', body: 'b', userId: 9 }], success: true };
    const api = { get: async () => envelope } as unknown as ApiDataSource;
    TestBed.configureTestingModule({
      providers: [PostRepositoryImpl, { provide: API_DATA_SOURCE, useValue: api }],
    });
    const repo = TestBed.inject(PostRepositoryImpl);
    await expect(repo.getPosts()).resolves.toEqual(envelope);
  });
});
```

- [ ] **Step 2: Run it and watch it fail**

Run: `npx jest src/app/features/api/fetch-posts/data/repositories/post.repository.spec.ts`
Expected: FAIL — `PostRepositoryImpl` still injects `HttpClientService` and returns a synthesized envelope, not the data source's.

- [ ] **Step 3: Rewire `post.repository.ts` to the seam**

```ts
// PostRepositoryImpl: reads the posts envelope from the ApiDataSource seam. With the
// default MockApiDataSource binding this resolves offline; swap to HttpApiDataSource
// (and set env.api.baseUrl) to hit a real backend that speaks { data, success }.
import { Injectable, inject } from '@angular/core';
import { API_DATA_SOURCE } from '@core/datasource/api/api-data-source';
import { createLogger } from '@core/logging/logger';
import { IPostRepository } from '../../domain/repositories/post.repository.port';
import { PostsDtoRs } from '../dto/post-dto-rs';

@Injectable()
export class PostRepositoryImpl implements IPostRepository {
  private readonly api = inject(API_DATA_SOURCE);
  private readonly log = createLogger('repository', 'PostRepository');

  async getPosts(): Promise<PostsDtoRs> {
    this.log.debug('getPosts() → GET /posts');
    return this.api.get<PostsDtoRs>('/posts');
  }
}
```

- [ ] **Step 4: Run the suite to verify green**

Run: `npx jest src/app/features/api/fetch-posts`
Expected: PASS (repository spec + get-posts use-case spec + viewmodel spec — the seed envelope satisfies `isPostDtoRsValid`).
Run: `npx jest`
Expected: all PASS.
Run: `npm run build`
Expected: succeeds.

- [ ] **Step 5: Commit**

```bash
git add src/app/features/api/fetch-posts/data/repositories/post.repository.ts src/app/features/api/fetch-posts/data/repositories/post.repository.spec.ts
git commit -m "refactor(fetch-posts): read posts through the ApiDataSource seam (offline)"
```

---

## Phase 2 — Auth (roles + guards + auth-gated demo)

### Task 5: Storage keys, auth types, token store

**Files:**
- Modify: `src/app/core/datasource/keyvalue/storage-keys.ts`
- Create: `src/app/core/auth/auth.types.ts`
- Create: `src/app/core/auth/token-store.ts`
- Create: `src/app/core/auth/token-store.spec.ts`

**Interfaces:**
- Produces: `type UserRole = 'admin' | 'user'`; `AuthTokens`, `AuthPrincipal`, `AuthSession`; `TokenStore.save/getAccess/getRefresh/clear`; `StorageKeys.accessToken`, `StorageKeys.refreshToken`.
- Consumes: `KeyValueStore` (existing).

- [ ] **Step 1: Add the auth storage keys**

Replace `src/app/core/datasource/keyvalue/storage-keys.ts`:

```ts
// StorageKeys: the single registry of localStorage keys used across the app.
// New features add their keys here.
export const StorageKeys = {
  profile: '@offline-storage/profile',
  accessToken: '@auth/access-token',
  refreshToken: '@auth/refresh-token',
} as const;
```

- [ ] **Step 2: Add `auth.types.ts` (roles defined locally — no domain models)**

```ts
// Auth domain primitives. UserRole is intentionally generic; an app narrows or
// extends it as needed. No coupling to any feature domain.
export type UserRole = 'admin' | 'user';

export interface AuthTokens { accessToken: string; refreshToken: string; }
export interface AuthPrincipal { role: UserRole; userId: string; }
export interface AuthSession { tokens: AuthTokens; principal: AuthPrincipal; }
```

- [ ] **Step 3: Write the failing token-store spec**

Create `src/app/core/auth/token-store.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { TokenStore } from '@core/auth/token-store';
import { KeyValueStore } from '@core/datasource/keyvalue/key-value.store';

describe('TokenStore', () => {
  let store: TokenStore;
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [TokenStore, KeyValueStore] });
    store = TestBed.inject(TokenStore);
  });
  it('saves, reads, and clears tokens', async () => {
    await store.save({ accessToken: 'a', refreshToken: 'r' });
    expect(await store.getAccess()).toBe('a');
    expect(await store.getRefresh()).toBe('r');
    await store.clear();
    expect(await store.getAccess()).toBeNull();
  });
});
```

- [ ] **Step 4: Run it and watch it fail**

Run: `npx jest src/app/core/auth/token-store.spec.ts`
Expected: FAIL — `TokenStore` does not exist.

- [ ] **Step 5: Write `token-store.ts`**

```ts
import { Injectable, inject } from '@angular/core';
import { KeyValueStore } from '@core/datasource/keyvalue/key-value.store';
import { StorageKeys } from '@core/datasource/keyvalue/storage-keys';
import { AuthTokens } from '@core/auth/auth.types';

@Injectable({ providedIn: 'root' })
export class TokenStore {
  private readonly kv = inject(KeyValueStore);
  async save(t: AuthTokens): Promise<void> {
    await this.kv.setString(StorageKeys.accessToken, t.accessToken);
    await this.kv.setString(StorageKeys.refreshToken, t.refreshToken);
  }
  getAccess(): Promise<string | null> { return this.kv.getString(StorageKeys.accessToken); }
  getRefresh(): Promise<string | null> { return this.kv.getString(StorageKeys.refreshToken); }
  async clear(): Promise<void> {
    await this.kv.remove(StorageKeys.accessToken);
    await this.kv.remove(StorageKeys.refreshToken);
  }
}
```

- [ ] **Step 6: Verify green + commit**

Run: `npx jest src/app/core/auth/token-store.spec.ts`
Expected: PASS.

```bash
git add src/app/core/datasource/keyvalue/storage-keys.ts src/app/core/auth/auth.types.ts src/app/core/auth/token-store.ts src/app/core/auth/token-store.spec.ts
git commit -m "feat(auth): auth types, storage keys, and KeyValueStore-backed token store"
```

---

### Task 6: AuthDataSource seam — Mock (default) + Http

**Files:**
- Create: `src/app/core/auth/auth-data-source.ts`
- Create: `src/app/core/auth/mock-auth.data-source.ts`
- Create: `src/app/core/auth/http-auth.data-source.ts`
- Create: `src/app/core/auth/mock-auth.data-source.spec.ts`

**Interfaces:**
- Produces: `interface AuthDataSource { login(email,password): Promise<AuthSession>; refresh(refreshToken): Promise<AuthTokens> }`; `AUTH_DATA_SOURCE` token; `MockAuthDataSource` (2 demo users; tokens `mock-access.{role}.{userId}` / `mock-refresh.{role}.{userId}`); `HttpAuthDataSource` (POST `/auth/login`, `/auth/refresh`).
- Consumes: `AuthSession`, `AuthTokens`, `UserRole` (Task 5); `AppError`; `HttpClientService`.

- [ ] **Step 1: Write `auth-data-source.ts` (interface + token)**

```ts
import { InjectionToken } from '@angular/core';
import { AuthSession, AuthTokens } from '@core/auth/auth.types';

// AuthDataSource: the swappable auth transport. MockAuthDataSource validates against
// seed users (default); HttpAuthDataSource talks to a real backend. Swap the binding
// in AUTH_PROVIDERS to go live.
export interface AuthDataSource {
  login(email: string, password: string): Promise<AuthSession>;
  refresh(refreshToken: string): Promise<AuthTokens>;
}

export const AUTH_DATA_SOURCE = new InjectionToken<AuthDataSource>('AUTH_DATA_SOURCE');
```

- [ ] **Step 2: Write the failing mock-auth spec**

Create `src/app/core/auth/mock-auth.data-source.spec.ts`:

```ts
import { MockAuthDataSource } from '@core/auth/mock-auth.data-source';

describe('MockAuthDataSource', () => {
  const ds = new MockAuthDataSource();

  it('logs in the admin demo user and mints role-stamped tokens', async () => {
    const s = await ds.login('admin@example.com', 'admin123');
    expect(s.principal).toEqual({ role: 'admin', userId: 'USR-ADMIN' });
    expect(s.tokens.accessToken).toBe('mock-access.admin.USR-ADMIN');
  });

  it('logs in the standard demo user', async () => {
    const s = await ds.login('user@example.com', 'user123');
    expect(s.principal.role).toBe('user');
  });

  it('rejects bad credentials with kind=auth, status 401', async () => {
    await expect(ds.login('admin@example.com', 'wrong')).rejects.toMatchObject({ kind: 'auth', status: 401 });
  });

  it('refreshes a mock refresh token', async () => {
    const t = await ds.refresh('mock-refresh.user.USR-1');
    expect(t.accessToken).toBe('mock-access.user.USR-1');
  });
});
```

- [ ] **Step 3: Run it and watch it fail**

Run: `npx jest src/app/core/auth/mock-auth.data-source.spec.ts`
Expected: FAIL — `MockAuthDataSource` does not exist.

- [ ] **Step 4: Write `mock-auth.data-source.ts`**

```ts
// MockAuthDataSource: seed-backed auth for offline/demo use. Validates against two
// generic demo users and mints deterministic mock tokens. Swap for HttpAuthDataSource
// at integration time.
import { Injectable } from '@angular/core';
import { AppError } from '@core/domain/errors/app-error';
import { AuthDataSource } from '@core/auth/auth-data-source';
import { AuthSession, AuthTokens, UserRole } from '@core/auth/auth.types';

interface DemoUser { email: string; password: string; role: UserRole; userId: string; }

const USERS: DemoUser[] = [
  { email: 'admin@example.com', password: 'admin123', role: 'admin', userId: 'USR-ADMIN' },
  { email: 'user@example.com', password: 'user123', role: 'user', userId: 'USR-1' },
];

@Injectable({ providedIn: 'root' })
export class MockAuthDataSource implements AuthDataSource {
  async login(email: string, password: string): Promise<AuthSession> {
    await Promise.resolve();
    const e = email.trim().toLowerCase();
    const user = USERS.find((u) => u.email === e && u.password === password);
    if (!user) throw new AppError('Invalid email or password', 'auth', 401);
    return { tokens: this.mint(user.role, user.userId), principal: { role: user.role, userId: user.userId } };
  }

  async refresh(refreshToken: string): Promise<AuthTokens> {
    await Promise.resolve();
    if (!refreshToken.startsWith('mock-refresh.')) throw new AppError('Invalid refresh token', 'auth', 401);
    const [, role, userId] = refreshToken.split('.');
    return this.mint(role as UserRole, userId);
  }

  private mint(role: UserRole, userId: string): AuthTokens {
    return { accessToken: `mock-access.${role}.${userId}`, refreshToken: `mock-refresh.${role}.${userId}` };
  }
}
```

- [ ] **Step 5: Write `http-auth.data-source.ts` (the real swap target)**

```ts
// HttpAuthDataSource: the real auth transport. Bind it in place of MockAuthDataSource
// (and set env.api.baseUrl) once a backend exposes /auth/login and /auth/refresh.
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { AuthDataSource } from '@core/auth/auth-data-source';
import { AuthSession, AuthTokens } from '@core/auth/auth.types';

@Injectable({ providedIn: 'root' })
export class HttpAuthDataSource implements AuthDataSource {
  private readonly http = inject(HttpClientService);
  login(email: string, password: string): Promise<AuthSession> {
    return this.http.post<AuthSession>('/auth/login', { body: { email, password } });
  }
  refresh(refreshToken: string): Promise<AuthTokens> {
    return this.http.post<AuthTokens>('/auth/refresh', { body: { refreshToken } });
  }
}
```

- [ ] **Step 6: Verify green + commit**

Run: `npx jest src/app/core/auth/mock-auth.data-source.spec.ts`
Expected: PASS.

```bash
git add src/app/core/auth/auth-data-source.ts src/app/core/auth/mock-auth.data-source.ts src/app/core/auth/http-auth.data-source.ts src/app/core/auth/mock-auth.data-source.spec.ts
git commit -m "feat(auth): AuthDataSource seam with seed-backed mock (default) and http impl"
```

---

### Task 7: Auth repository (interface, port, impl) + providers

**Files:**
- Create: `src/app/core/auth/auth.repository.interface.ts`
- Create: `src/app/core/auth/auth.repository.port.ts`
- Create: `src/app/core/auth/auth.repository.ts`
- Create: `src/app/core/auth/auth.providers.ts`
- Create: `src/app/core/auth/auth.repository.spec.ts`

**Interfaces:**
- Produces: `IAuthRepository { login, refresh }`; `AUTH_REPOSITORY` token (providedIn factory → `AuthRepository`); `AuthRepository` (delegates to `AUTH_DATA_SOURCE`); `AUTH_PROVIDERS` (binds `AUTH_DATA_SOURCE`→`MockAuthDataSource`, `AUTH_REPOSITORY`→`AuthRepository`).
- Consumes: `AUTH_DATA_SOURCE` (Task 6); `AuthSession`, `AuthTokens`.

- [ ] **Step 1: Write the interface**

`src/app/core/auth/auth.repository.interface.ts`:

```ts
import { AuthSession, AuthTokens } from '@core/auth/auth.types';

export interface IAuthRepository {
  login(email: string, password: string): Promise<AuthSession>;
  refresh(refreshToken: string): Promise<AuthTokens>;
}
```

- [ ] **Step 2: Write the port (token)**

`src/app/core/auth/auth.repository.port.ts`:

```ts
import { InjectionToken, inject } from '@angular/core';
import { IAuthRepository } from '@core/auth/auth.repository.interface';
import { AuthRepository } from '@core/auth/auth.repository';

// Re-export so callers can keep importing IAuthRepository from this file.
export type { IAuthRepository };

// providedIn factory covers test injectors lacking AUTH_PROVIDERS; in the app,
// AUTH_PROVIDERS overrides it with the same class (identical result).
export const AUTH_REPOSITORY = new InjectionToken<IAuthRepository>('AUTH_REPOSITORY', {
  providedIn: 'root',
  factory: () => inject(AuthRepository),
});
```

- [ ] **Step 3: Write the implementation (injects the data-source token)**

`src/app/core/auth/auth.repository.ts`:

```ts
import { Injectable, inject } from '@angular/core';
import { IAuthRepository } from '@core/auth/auth.repository.interface';
import { AUTH_DATA_SOURCE } from '@core/auth/auth-data-source';
import { AuthSession, AuthTokens } from '@core/auth/auth.types';

@Injectable({ providedIn: 'root' })
export class AuthRepository implements IAuthRepository {
  private readonly api = inject(AUTH_DATA_SOURCE);
  login(email: string, password: string): Promise<AuthSession> { return this.api.login(email, password); }
  refresh(refreshToken: string): Promise<AuthTokens> { return this.api.refresh(refreshToken); }
}
```

- [ ] **Step 4: Write the providers**

`src/app/core/auth/auth.providers.ts`:

```ts
import { Provider } from '@angular/core';
import { AUTH_REPOSITORY } from '@core/auth/auth.repository.port';
import { AUTH_DATA_SOURCE } from '@core/auth/auth-data-source';
import { AuthRepository } from '@core/auth/auth.repository';
import { MockAuthDataSource } from '@core/auth/mock-auth.data-source';

// Default wiring: seed-backed mock auth. To go live, swap MockAuthDataSource →
// HttpAuthDataSource and set env.api.baseUrl.
export const AUTH_PROVIDERS: Provider[] = [
  { provide: AUTH_DATA_SOURCE, useClass: MockAuthDataSource },
  { provide: AUTH_REPOSITORY, useClass: AuthRepository },
];
```

- [ ] **Step 5: Write a delegation test**

`src/app/core/auth/auth.repository.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { AuthRepository } from '@core/auth/auth.repository';
import { AUTH_DATA_SOURCE, AuthDataSource } from '@core/auth/auth-data-source';
import { AuthSession } from '@core/auth/auth.types';

const session: AuthSession = { tokens: { accessToken: 'a', refreshToken: 'r' }, principal: { role: 'admin', userId: 'USR-ADMIN' } };

describe('AuthRepository', () => {
  it('delegates login to the data source', async () => {
    const api = { login: async () => session, refresh: async () => session.tokens } as AuthDataSource;
    TestBed.configureTestingModule({
      providers: [AuthRepository, { provide: AUTH_DATA_SOURCE, useValue: api }],
    });
    const repo = TestBed.inject(AuthRepository);
    await expect(repo.login('admin@example.com', 'admin123')).resolves.toEqual(session);
  });
});
```

- [ ] **Step 6: Run + verify green + commit**

Run: `npx jest src/app/core/auth/auth.repository.spec.ts`
Expected: PASS.
Run: `npm run build`
Expected: succeeds.

```bash
git add src/app/core/auth/auth.repository.interface.ts src/app/core/auth/auth.repository.port.ts src/app/core/auth/auth.repository.ts src/app/core/auth/auth.providers.ts src/app/core/auth/auth.repository.spec.ts
git commit -m "feat(auth): auth repository, port, and DI providers (mock bound by default)"
```

---

### Task 8: Login / refresh / logout use cases

**Files:**
- Create: `src/app/core/auth/usecases/login.use-case.ts`
- Create: `src/app/core/auth/usecases/refresh-token.use-case.ts`
- Create: `src/app/core/auth/usecases/logout.use-case.ts`
- Create: `src/app/core/auth/usecases/login.use-case.spec.ts`

**Interfaces:**
- Produces: `LoginUseCase.run({ email, password }): Promise<Result<AuthSession>>`; `RefreshTokenUseCase.run(): Promise<Result<AuthTokens>>`; `LogoutUseCase.run(): Promise<Result<void>>`.
- Consumes: `AUTH_REPOSITORY` (Task 7), `TokenStore` (Task 5), `UseCase` base.

- [ ] **Step 1: Write the failing login spec**

`src/app/core/auth/usecases/login.use-case.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { LoginUseCase } from '@core/auth/usecases/login.use-case';
import { AUTH_REPOSITORY, IAuthRepository } from '@core/auth/auth.repository.port';
import { TokenStore } from '@core/auth/token-store';
import { KeyValueStore } from '@core/datasource/keyvalue/key-value.store';
import { AppError } from '@core/domain/errors/app-error';

const fakeRepo: IAuthRepository = {
  login: async (email) =>
    email === 'admin@example.com'
      ? { tokens: { accessToken: 'a', refreshToken: 'r' }, principal: { role: 'admin', userId: 'USR-ADMIN' } }
      : Promise.reject(new AppError('bad', 'auth', 401)),
  refresh: async () => ({ accessToken: 'a2', refreshToken: 'r2' }),
};

describe('LoginUseCase', () => {
  let uc: LoginUseCase;
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [LoginUseCase, TokenStore, KeyValueStore, { provide: AUTH_REPOSITORY, useValue: fakeRepo }],
    });
    uc = TestBed.inject(LoginUseCase);
  });
  it('logs in and persists tokens', async () => {
    const r = await uc.run({ email: 'admin@example.com', password: 'admin123' });
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.data.principal.role).toBe('admin');
    expect(await TestBed.inject(TokenStore).getAccess()).toBe('a');
  });
  it('returns fail(auth) on bad credentials', async () => {
    const r = await uc.run({ email: 'x@y.z', password: 'nope' });
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('auth');
  });
});
```

- [ ] **Step 2: Run it and watch it fail**

Run: `npx jest src/app/core/auth/usecases/login.use-case.spec.ts`
Expected: FAIL — `LoginUseCase` does not exist.

- [ ] **Step 3: Write the three use cases**

`src/app/core/auth/usecases/login.use-case.ts`:

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AUTH_REPOSITORY } from '@core/auth/auth.repository.port';
import { TokenStore } from '@core/auth/token-store';
import { AuthSession } from '@core/auth/auth.types';

export interface LoginInput { email: string; password: string; }

@Injectable({ providedIn: 'root' })
export class LoginUseCase extends UseCase<LoginInput, AuthSession> {
  private readonly repo = inject(AUTH_REPOSITORY);
  private readonly tokens = inject(TokenStore);
  constructor() { super('Login'); }
  protected async execute({ email, password }: LoginInput): Promise<AuthSession> {
    const session = await this.repo.login(email, password);
    await this.tokens.save(session.tokens);
    return session;
  }
}
```

`src/app/core/auth/usecases/refresh-token.use-case.ts`:

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AUTH_REPOSITORY } from '@core/auth/auth.repository.port';
import { TokenStore } from '@core/auth/token-store';
import { AppError } from '@core/domain/errors/app-error';
import { AuthTokens } from '@core/auth/auth.types';

@Injectable({ providedIn: 'root' })
export class RefreshTokenUseCase extends UseCase<void, AuthTokens> {
  private readonly repo = inject(AUTH_REPOSITORY);
  private readonly store = inject(TokenStore);
  constructor() { super('RefreshToken'); }
  protected async execute(): Promise<AuthTokens> {
    const refresh = await this.store.getRefresh();
    if (!refresh) throw new AppError('No refresh token', 'auth', 401);
    const tokens = await this.repo.refresh(refresh);
    await this.store.save(tokens);
    return tokens;
  }
}
```

`src/app/core/auth/usecases/logout.use-case.ts`:

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { TokenStore } from '@core/auth/token-store';

@Injectable({ providedIn: 'root' })
export class LogoutUseCase extends UseCase<void, void> {
  private readonly store = inject(TokenStore);
  constructor() { super('Logout'); }
  protected async execute(): Promise<void> { await this.store.clear(); }
}
```

- [ ] **Step 4: Verify green + commit**

Run: `npx jest src/app/core/auth/usecases/login.use-case.spec.ts`
Expected: PASS.

```bash
git add src/app/core/auth/usecases/
git commit -m "feat(auth): login, refresh-token, and logout use cases"
```

---

### Task 9: AuthSessionStore (signal-based session state)

**Files:**
- Create: `src/app/core/auth/auth-session.store.ts`
- Create: `src/app/core/auth/auth-session.store.spec.ts`

**Interfaces:**
- Produces: `AuthSessionStore` with `session()`, `isAuthenticated()`, `principal()`, `role(): UserRole | null`, `signIn(email, password): Promise<Result<AuthSession>>`, `signOut(): Promise<void>`; rehydrates from stored mock tokens on construction.
- Consumes: `LoginUseCase`, `LogoutUseCase` (Task 8), `TokenStore` (Task 5), `Result`.

- [ ] **Step 1: Write the failing spec**

`src/app/core/auth/auth-session.store.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { AuthSessionStore } from '@core/auth/auth-session.store';
import { LoginUseCase } from '@core/auth/usecases/login.use-case';
import { LogoutUseCase } from '@core/auth/usecases/logout.use-case';
import { TokenStore } from '@core/auth/token-store';
import { KeyValueStore } from '@core/datasource/keyvalue/key-value.store';
import { ok } from '@core/domain/result/result';
import { AuthSession } from '@core/auth/auth.types';

const session: AuthSession = {
  tokens: { accessToken: 'mock-access.admin.USR-ADMIN', refreshToken: 'mock-refresh.admin.USR-ADMIN' },
  principal: { role: 'admin', userId: 'USR-ADMIN' },
};

describe('AuthSessionStore', () => {
  let store: AuthSessionStore;
  const login = { run: jest.fn() } as unknown as LoginUseCase;
  beforeEach(() => {
    localStorage.clear();
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [AuthSessionStore, LogoutUseCase, TokenStore, KeyValueStore, { provide: LoginUseCase, useValue: login }],
    });
    store = TestBed.inject(AuthSessionStore);
  });

  it('starts unauthenticated', () => {
    expect(store.isAuthenticated()).toBe(false);
    expect(store.role()).toBeNull();
  });

  it('signIn populates the session', async () => {
    (login.run as jest.Mock).mockResolvedValue(ok(session));
    const r = await store.signIn('admin@example.com', 'admin123');
    expect(r.ok).toBe(true);
    expect(store.isAuthenticated()).toBe(true);
    expect(store.role()).toBe('admin');
    expect(store.principal()?.userId).toBe('USR-ADMIN');
  });

  it('signOut clears the session', async () => {
    (login.run as jest.Mock).mockResolvedValue(ok(session));
    await store.signIn('admin@example.com', 'admin123');
    await store.signOut();
    expect(store.isAuthenticated()).toBe(false);
  });
});
```

- [ ] **Step 2: Run it and watch it fail**

Run: `npx jest src/app/core/auth/auth-session.store.spec.ts`
Expected: FAIL — `AuthSessionStore` does not exist.

- [ ] **Step 3: Write `auth-session.store.ts`**

```ts
// AuthSessionStore: the single source of truth for the current auth session, exposed
// as signals for guards and pages. Wraps the login/logout use cases and rehydrates
// from the stored (mock) access token on construction. A real backend would decode a
// JWT or call a /me endpoint here instead of parsing the mock token shape.
import { Injectable, computed, inject, signal } from '@angular/core';
import { LoginUseCase } from '@core/auth/usecases/login.use-case';
import { LogoutUseCase } from '@core/auth/usecases/logout.use-case';
import { TokenStore } from '@core/auth/token-store';
import { AuthSession, UserRole } from '@core/auth/auth.types';
import { Result } from '@core/domain/result/result';

@Injectable({ providedIn: 'root' })
export class AuthSessionStore {
  private readonly loginUseCase = inject(LoginUseCase);
  private readonly logoutUseCase = inject(LogoutUseCase);
  private readonly tokens = inject(TokenStore);

  private readonly _session = signal<AuthSession | null>(null);
  readonly session = this._session.asReadonly();
  readonly isAuthenticated = computed(() => this._session() !== null);
  readonly principal = computed(() => this._session()?.principal ?? null);
  readonly role = computed<UserRole | null>(() => this._session()?.principal.role ?? null);

  constructor() { void this.rehydrate(); }

  async signIn(email: string, password: string): Promise<Result<AuthSession>> {
    const r = await this.loginUseCase.run({ email, password });
    if (r.ok) this._session.set(r.data);
    return r;
  }

  async signOut(): Promise<void> {
    await this.logoutUseCase.run();
    this._session.set(null);
  }

  private async rehydrate(): Promise<void> {
    const accessToken = await this.tokens.getAccess();
    const refreshToken = await this.tokens.getRefresh();
    if (!accessToken || !refreshToken) return;
    const [, role, userId] = accessToken.split('.');
    if (!role || !userId) return;
    this._session.set({ tokens: { accessToken, refreshToken }, principal: { role: role as UserRole, userId } });
  }
}
```

- [ ] **Step 4: Verify green + commit**

Run: `npx jest src/app/core/auth/auth-session.store.spec.ts`
Expected: PASS.

```bash
git add src/app/core/auth/auth-session.store.ts src/app/core/auth/auth-session.store.spec.ts
git commit -m "feat(auth): signal-based AuthSessionStore with token rehydration"
```

---

### Task 10: Auth interceptor + composition-root wiring

**Files:**
- Create: `src/app/core/network/interceptors/auth.interceptor.ts`
- Create: `src/app/core/network/interceptors/auth.interceptor.spec.ts`
- Modify: `src/app/app.config.ts` (interceptor + `AUTH_PROVIDERS`)

**Interfaces:**
- Produces: `authInterceptor: HttpInterceptorFn` (Bearer header; on 401, one refresh then retry; on refresh failure, clear tokens + navigate `/login`).
- Consumes: `TokenStore`, `RefreshTokenUseCase` (Tasks 5/8), `Router`, `AUTH_PROVIDERS` (Task 7).

- [ ] **Step 1: Write `auth.interceptor.ts`**

```ts
// authInterceptor: attaches the access token and transparently refreshes once on 401.
import { HttpInterceptorFn, HttpRequest, HttpHandlerFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { from, switchMap, catchError, throwError } from 'rxjs';
import { TokenStore } from '@core/auth/token-store';
import { RefreshTokenUseCase } from '@core/auth/usecases/refresh-token.use-case';

const withAuth = (req: HttpRequest<unknown>, token: string | null) =>
  token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

export const authInterceptor: HttpInterceptorFn = (req, next: HttpHandlerFn) => {
  const tokens = inject(TokenStore);
  const refresh = inject(RefreshTokenUseCase);
  const router = inject(Router);

  return from(tokens.getAccess()).pipe(
    switchMap((access) =>
      next(withAuth(req, access)).pipe(
        catchError((err) => {
          if (err?.status !== 401) return throwError(() => err);
          // one refresh attempt, then retry the original request
          return from(refresh.run()).pipe(
            switchMap((result) => {
              if (!result.ok) {
                void tokens.clear();
                void router.navigate(['/login']);
                return throwError(() => err);
              }
              return next(withAuth(req, result.data.accessToken));
            }),
          );
        }),
      ),
    ),
  );
};
```

- [ ] **Step 2: Write the spec**

`src/app/core/network/interceptors/auth.interceptor.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors, HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { authInterceptor } from '@core/network/interceptors/auth.interceptor';
import { TokenStore } from '@core/auth/token-store';
import { RefreshTokenUseCase } from '@core/auth/usecases/refresh-token.use-case';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';

describe('authInterceptor', () => {
  let http: HttpClient;
  let mock: HttpTestingController;
  const tokenStore = { getAccess: jest.fn(), clear: jest.fn() } as unknown as TokenStore;
  const refresh = { run: jest.fn() } as unknown as RefreshTokenUseCase;
  const router = { navigate: jest.fn() } as unknown as Router;

  beforeEach(() => {
    jest.clearAllMocks();
    (tokenStore.getAccess as jest.Mock).mockResolvedValue(null);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: TokenStore, useValue: tokenStore },
        { provide: RefreshTokenUseCase, useValue: refresh },
        { provide: Router, useValue: router },
      ],
    });
    http = TestBed.inject(HttpClient);
    mock = TestBed.inject(HttpTestingController);
  });
  afterEach(() => mock.verify());

  it('attaches the bearer token', async () => {
    (tokenStore.getAccess as jest.Mock).mockResolvedValue('tok');
    http.get('/x').subscribe();
    await Promise.resolve();
    const req = mock.expectOne('/x');
    expect(req.request.headers.get('Authorization')).toBe('Bearer tok');
    req.flush({});
  });

  it('refreshes once on 401 and retries', async () => {
    (tokenStore.getAccess as jest.Mock).mockResolvedValue('old');
    (refresh.run as jest.Mock).mockResolvedValue(ok({ accessToken: 'new', refreshToken: 'r' }));
    const done = new Promise<void>((resolve) => http.get('/y').subscribe(() => resolve()));
    await Promise.resolve();
    mock.expectOne('/y').flush({ message: 'unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    await Promise.resolve(); await Promise.resolve();
    const retry = mock.expectOne('/y');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer new');
    retry.flush({});
    await done;
    expect(refresh.run).toHaveBeenCalledTimes(1);
  });

  it('clears tokens and navigates to /login when refresh fails on 401', async () => {
    (tokenStore.getAccess as jest.Mock).mockResolvedValue('old');
    (refresh.run as jest.Mock).mockResolvedValue(fail(new AppError('expired', 'auth', 401)));
    let errorFired = false;
    const done = new Promise<void>((resolve) =>
      http.get('/z').subscribe({ error: () => { errorFired = true; resolve(); } }),
    );
    await Promise.resolve();
    mock.expectOne('/z').flush({ message: 'unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    await Promise.resolve(); await Promise.resolve();
    await done;
    expect(tokenStore.clear).toHaveBeenCalledTimes(1);
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
    expect(errorFired).toBe(true);
  });
});
```

- [ ] **Step 3: Run it to verify green**

Run: `npx jest src/app/core/network/interceptors/auth.interceptor.spec.ts`
Expected: PASS.

- [ ] **Step 4: Wire the interceptor + auth providers into `app.config.ts`**

Edit `src/app/app.config.ts`. Add imports:

```ts
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { authInterceptor } from '@core/network/interceptors/auth.interceptor';
import { AUTH_PROVIDERS } from '@core/auth/auth.providers';
```

Change `provideHttpClient()` → `provideHttpClient(withInterceptors([authInterceptor]))`, and add `...AUTH_PROVIDERS` to the providers array. The final array reads:

```ts
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAnimations(),
    { provide: API_DATA_SOURCE, useClass: MockApiDataSource },
    ...AUTH_PROVIDERS,
  ],
```

- [ ] **Step 5: Verify build + full suite + commit**

Run: `npm run build`
Expected: succeeds.
Run: `npx jest`
Expected: all PASS.

```bash
git add src/app/core/network/interceptors/ src/app/app.config.ts
git commit -m "feat(auth): bearer interceptor with 401 refresh-retry; wire auth providers"
```

---

### Task 11: Route guards (authGuard, roleGuard)

**Files:**
- Create: `src/app/core/guards/auth.guard.ts`
- Create: `src/app/core/guards/auth.guard.spec.ts`

**Interfaces:**
- Produces: `authGuard: CanActivateFn` (→ `/login` if unauthenticated); `roleGuard(...roles: UserRole[]): CanActivateFn` (authenticated-but-wrong-role → `/`).
- Consumes: `AuthSessionStore` (Task 9), `Router`, `UserRole`.

- [ ] **Step 1: Write the failing spec**

`src/app/core/guards/auth.guard.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { runInInjectionContext, Injector } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { authGuard, roleGuard } from '@core/guards/auth.guard';
import { AuthSessionStore } from '@core/auth/auth-session.store';

function setup(auth: Partial<AuthSessionStore>) {
  const router = { navigate: jest.fn() } as unknown as Router;
  TestBed.configureTestingModule({
    providers: [
      { provide: AuthSessionStore, useValue: auth },
      { provide: Router, useValue: router },
    ],
  });
  return { injector: TestBed.inject(Injector), router };
}
const run = (injector: Injector, guard: CanActivateFn) =>
  runInInjectionContext(injector, () => guard({} as never, {} as never));

describe('authGuard', () => {
  it('allows authenticated users', () => {
    const { injector } = setup({ isAuthenticated: () => true } as Partial<AuthSessionStore>);
    expect(run(injector, authGuard)).toBe(true);
  });
  it('redirects anonymous users to /login', () => {
    const { injector, router } = setup({ isAuthenticated: () => false } as Partial<AuthSessionStore>);
    expect(run(injector, authGuard)).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });
});

describe('roleGuard', () => {
  it('allows a matching role', () => {
    const { injector } = setup({ isAuthenticated: () => true, role: () => 'admin' } as Partial<AuthSessionStore>);
    expect(run(injector, roleGuard('admin'))).toBe(true);
  });
  it('redirects a non-matching role to /', () => {
    const { injector, router } = setup({ isAuthenticated: () => true, role: () => 'user' } as Partial<AuthSessionStore>);
    expect(run(injector, roleGuard('admin'))).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/']);
  });
});
```

- [ ] **Step 2: Run it and watch it fail**

Run: `npx jest src/app/core/guards/auth.guard.spec.ts`
Expected: FAIL — guards do not exist.

- [ ] **Step 3: Write `auth.guard.ts`**

```ts
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthSessionStore } from '@core/auth/auth-session.store';
import { UserRole } from '@core/auth/auth.types';

// authGuard: blocks anonymous access, sending unauthenticated users to /login.
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthSessionStore);
  const router = inject(Router);
  if (!auth.isAuthenticated()) { router.navigate(['/login']); return false; }
  return true;
};

// roleGuard: requires one of `roles`. Authenticated users lacking the role are sent
// to the home route ('/') — the base's configurable "forbidden" destination.
export const roleGuard = (...roles: UserRole[]): CanActivateFn => () => {
  const auth = inject(AuthSessionStore);
  const router = inject(Router);
  if (!auth.isAuthenticated()) { router.navigate(['/login']); return false; }
  const role = auth.role();
  if (!role || !roles.includes(role)) { router.navigate(['/']); return false; }
  return true;
};
```

- [ ] **Step 4: Verify green + commit**

Run: `npx jest src/app/core/guards/auth.guard.spec.ts`
Expected: PASS.

```bash
git add src/app/core/guards/
git commit -m "feat(auth): authGuard and roleGuard route guards"
```

---

### Task 12: Auth feature slice (login, account, admin) + routes

**Files:**
- Create: `src/app/features/auth/presentation/viewmodels/login.viewmodel.ts`
- Create: `src/app/features/auth/presentation/viewmodels/login.viewmodel.spec.ts`
- Create: `src/app/features/auth/presentation/pages/login.page.ts`
- Create: `src/app/features/auth/presentation/pages/account.page.ts`
- Create: `src/app/features/auth/presentation/pages/admin.page.ts`
- Create: `src/app/features/auth/index.ts`
- Modify: `src/app/app.routes.ts`

**Interfaces:**
- Produces: `LoginViewModel` (`email`, `password`, `loading`, `error` signals; `submit()` → navigates `/account` on success); `LoginPage`, `AccountPage`, `AdminPage`; routes `/login`, `/account` (authGuard), `/admin` (roleGuard('admin')).
- Consumes: `AuthSessionStore` (Task 9), guards (Task 11).

- [ ] **Step 1: Write the failing viewmodel spec**

`src/app/features/auth/presentation/viewmodels/login.viewmodel.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { LoginViewModel } from './login.viewmodel';
import { AuthSessionStore } from '@core/auth/auth-session.store';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';
import { AuthSession } from '@core/auth/auth.types';

const session: AuthSession = { tokens: { accessToken: 'a', refreshToken: 'r' }, principal: { role: 'admin', userId: 'USR-ADMIN' } };

describe('LoginViewModel', () => {
  const auth = { signIn: jest.fn() } as unknown as AuthSessionStore;
  const router = { navigate: jest.fn() } as unknown as Router;
  let vm: LoginViewModel;
  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [LoginViewModel, { provide: AuthSessionStore, useValue: auth }, { provide: Router, useValue: router }],
    });
    vm = TestBed.inject(LoginViewModel);
  });
  it('navigates to /account on success', async () => {
    (auth.signIn as jest.Mock).mockResolvedValue(ok(session));
    vm.email.set('admin@example.com'); vm.password.set('admin123');
    await vm.submit();
    expect(router.navigate).toHaveBeenCalledWith(['/account']);
    expect(vm.error()).toBeNull();
  });
  it('surfaces the error message on failure', async () => {
    (auth.signIn as jest.Mock).mockResolvedValue(fail(new AppError('Invalid email or password', 'auth', 401)));
    await vm.submit();
    expect(vm.error()).toBe('Invalid email or password');
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run it and watch it fail**

Run: `npx jest src/app/features/auth/presentation/viewmodels/login.viewmodel.spec.ts`
Expected: FAIL — `LoginViewModel` does not exist.

- [ ] **Step 3: Write `login.viewmodel.ts`**

```ts
// LoginViewModel: the presentation facade for the login form. Drives AuthSessionStore
// and translates the Result into form state + navigation.
import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthSessionStore } from '@core/auth/auth-session.store';
import { createLogger } from '@core/logging/logger';

@Injectable()
export class LoginViewModel {
  private readonly auth = inject(AuthSessionStore);
  private readonly router = inject(Router);
  private readonly log = createLogger('viewmodel', 'LoginViewModel');

  readonly email = signal('');
  readonly password = signal('');
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  async submit(): Promise<void> {
    this.log.debug('submit()');
    this.loading.set(true);
    this.error.set(null);
    const r = await this.auth.signIn(this.email(), this.password());
    this.loading.set(false);
    if (r.ok) {
      void this.router.navigate(['/account']);
    } else {
      this.error.set(r.error.message);
    }
  }
}
```

- [ ] **Step 4: Run the viewmodel spec to verify green**

Run: `npx jest src/app/features/auth/presentation/viewmodels/login.viewmodel.spec.ts`
Expected: PASS.

- [ ] **Step 5: Write the three pages (standalone components)**

`src/app/features/auth/presentation/pages/login.page.ts`:

```ts
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LoginViewModel } from '../viewmodels/login.viewmodel';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section style="max-width: 360px; margin: 48px auto; display: grid; gap: var(--space-md);">
      <h1>Sign in</h1>
      <p style="color: var(--color-muted);">
        Demo users: admin&#64;example.com / admin123 · user&#64;example.com / user123
      </p>
      <input [ngModel]="vm.email()" (ngModelChange)="vm.email.set($event)" placeholder="Email" autocomplete="username" />
      <input [ngModel]="vm.password()" (ngModelChange)="vm.password.set($event)" type="password" placeholder="Password" autocomplete="current-password" />
      @if (vm.error(); as e) { <p style="color: var(--color-error);">{{ e }}</p> }
      <button (click)="vm.submit()" [disabled]="vm.loading()">
        {{ vm.loading() ? 'Signing in…' : 'Sign in' }}
      </button>
    </section>
  `,
})
export class LoginPage {
  protected readonly vm = inject(LoginViewModel);
}
```

`src/app/features/auth/presentation/pages/account.page.ts`:

```ts
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Router } from '@angular/router';
import { AuthSessionStore } from '@core/auth/auth-session.store';

@Component({
  selector: 'app-account-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section style="max-width: 480px; margin: 48px auto; display: grid; gap: var(--space-md);">
      <h1>Account</h1>
      @if (auth.principal(); as p) {
        <p>Signed in as <strong>{{ p.userId }}</strong> with role <strong>{{ p.role }}</strong>.</p>
        @if (p.role === 'admin') { <a routerLink="/admin">Go to the admin area →</a> }
      }
      <button (click)="signOut()">Sign out</button>
    </section>
  `,
})
export class AccountPage {
  protected readonly auth = inject(AuthSessionStore);
  private readonly router = inject(Router);
  async signOut(): Promise<void> {
    await this.auth.signOut();
    void this.router.navigate(['/login']);
  }
}
```

`src/app/features/auth/presentation/pages/admin.page.ts`:

```ts
import { Component } from '@angular/core';

@Component({
  selector: 'app-admin-page',
  standalone: true,
  template: `
    <section style="max-width: 480px; margin: 48px auto;">
      <h1>Admin area</h1>
      <p>Only users with the <code>admin</code> role can reach this page (enforced by <code>roleGuard('admin')</code>).</p>
    </section>
  `,
})
export class AdminPage {}
```

- [ ] **Step 6: Write the barrel `index.ts`**

`src/app/features/auth/index.ts`:

```ts
export { LoginPage } from './presentation/pages/login.page';
export { AccountPage } from './presentation/pages/account.page';
export { AdminPage } from './presentation/pages/admin.page';
export { LoginViewModel } from './presentation/viewmodels/login.viewmodel';
```

- [ ] **Step 7: Wire the routes**

Edit `src/app/app.routes.ts`. Add imports near the top:

```ts
import { authGuard, roleGuard } from '@core/guards/auth.guard';
import { LoginViewModel } from '@features/auth';
```

Insert these route objects into the `routes` array **before** the final `{ path: '**', redirectTo: '' }`:

```ts
  {
    path: 'login',
    loadComponent: () => import('@features/auth').then((m) => m.LoginPage),
    providers: [LoginViewModel],
  },
  {
    path: 'account',
    canActivate: [authGuard],
    loadComponent: () => import('@features/auth').then((m) => m.AccountPage),
  },
  {
    path: 'admin',
    canActivate: [roleGuard('admin')],
    loadComponent: () => import('@features/auth').then((m) => m.AdminPage),
  },
```

- [ ] **Step 8: Verify build + full suite + manual smoke**

Run: `npm run build`
Expected: succeeds.
Run: `npx jest`
Expected: all PASS.
Run: `npm start` then open `http://localhost:4200/account` — expect redirect to `/login`. Sign in as `user@example.com`/`user123`, land on `/account`; visit `/admin` → redirected to `/` (home). Sign out, sign in as `admin@example.com`/`admin123`, visit `/admin` → allowed. Stop the server.

- [ ] **Step 9: Commit**

```bash
git add src/app/features/auth/ src/app/app.routes.ts
git commit -m "feat(auth): login/account/admin pages with guarded routes (offline demo)"
```

---

## Phase 3 — Styling (Tailwind + lucide)

### Task 13: Add Tailwind (mapped to CSS-var tokens) + PostCSS

**Files:**
- Modify: `package.json` (deps via npm)
- Create: `tailwind.config.js`
- Create: `postcss.config.js`
- Modify: `src/styles.scss`

**Interfaces:**
- Produces: working Tailwind utilities whose `primary`/`text`/spacing/radius scales resolve to the existing `--color-*` / `--space-*` / `--radius-*` CSS variables.

- [ ] **Step 1: Install the pinned dependencies**

Run: `npm install -D tailwindcss@3.4.17 postcss@^8.5.15 autoprefixer@^10.5.0`
Run: `npm install lucide-angular@^1.0.0`
Expected: both succeed; `package.json` updated.

- [ ] **Step 2: Create `tailwind.config.js` (theme maps to CSS variables)**

```js
/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      // Map Tailwind's scales to the SCSS design tokens (theme.scss) so utilities
      // and the existing UI kit share exactly one palette.
      colors: {
        primary: 'var(--color-primary)',
        text: 'var(--color-text)',
        muted: 'var(--color-muted)',
        error: 'var(--color-error)',
        border: 'var(--color-border)',
        background: 'var(--color-background)',
        'on-primary': 'var(--color-on-primary)',
      },
      spacing: {
        xs: 'var(--space-xs)',
        sm: 'var(--space-sm)',
        md: 'var(--space-md)',
        lg: 'var(--space-lg)',
        xl: 'var(--space-xl)',
      },
      borderRadius: {
        sm: 'var(--radius-sm)',
        md: 'var(--radius-md)',
      },
    },
  },
  plugins: [],
};
```

- [ ] **Step 3: Create `postcss.config.js`**

```js
module.exports = {
  plugins: {
    tailwindcss: {},
    autoprefixer: {},
  },
};
```

- [ ] **Step 4: Add the Tailwind directives to `src/styles.scss`**

`@use` rules must stay first in an SCSS file; place the Tailwind directives immediately after:

```scss
@use './app/core/ui/theme/theme';

@tailwind base;
@tailwind components;
@tailwind utilities;

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

- [ ] **Step 5: Verify the build compiles Tailwind**

Run: `npm run build`
Expected: succeeds (Angular's application builder auto-detects `tailwind.config.js` + `postcss.config.js`).

- [ ] **Step 6: Verify a mapped utility resolves to the token**

Temporarily add `class="text-primary p-md"` to the `<h1>` in `src/app/features/auth/presentation/pages/admin.page.ts`, run `npm start`, open `/admin` (sign in as admin first), and confirm in DevTools the heading color is `--color-primary` (#0a84ff) and padding is `--space-md` (16px). Then **revert** the temporary class.

- [ ] **Step 7: Run the suite + commit**

Run: `npx jest`
Expected: all PASS (no test impact).

```bash
git add package.json package-lock.json tailwind.config.js postcss.config.js src/styles.scss
git commit -m "feat(styling): add Tailwind (mapped to CSS-var tokens) + PostCSS"
```

---

### Task 14: Wire a lucide icon into the design hub

**Files:**
- Modify: `src/app/features/design/design-hub/presentation/pages/design-hub.page.ts`

**Interfaces:**
- Produces: a rendered lucide icon proving `lucide-angular` is wired.

- [ ] **Step 1: Read the page to integrate cleanly**

Read `src/app/features/design/design-hub/presentation/pages/design-hub.page.ts` to see its `imports` array and template.

- [ ] **Step 2: Add the icon**

Add to the component:

```ts
import { LucideAngularModule, Home } from 'lucide-angular';
```

Add `LucideAngularModule` to the component's `imports` array, add a field `protected readonly HomeIcon = Home;`, and place this in the template heading area:

```html
<lucide-icon [img]="HomeIcon" [size]="20"></lucide-icon>
```

> Note: lucide-angular v1 supports the standalone `[img]` binding without module-level icon registration. If the installed version instead requires registration, add `importProvidersFrom(LucideAngularModule.pick({ Home }))` to `app.config.ts` and use `<lucide-icon name="home">`. Prefer the `[img]` form.

- [ ] **Step 3: Verify build + render**

Run: `npm run build`
Expected: succeeds.
Run: `npm start`, open `/design`, confirm the home icon renders. Stop the server.

- [ ] **Step 4: Run the suite + commit**

Run: `npx jest`
Expected: all PASS.

```bash
git add src/app/features/design/design-hub/presentation/pages/design-hub.page.ts
git commit -m "feat(styling): render a lucide icon in the design hub"
```

---

## Phase 4 — Documentation

### Task 15: Extend the HTML doc site + README

**Files:**
- Modify: `docs/ERROR_HANDLING.html`, `docs/PROJECT_LIBRARIES.html`, `docs/CORE_FILES.html`, `docs/ADD_A_FEATURE.html`, `docs/index.html`
- Create: `docs/HTTP_CLIENT.html`, `docs/DATA_SOURCE_SEAM.html`, `docs/AUTH.html`, `docs/STYLING.html`
- Modify: `README.md`

**Interfaces:** documentation only — no code contracts. New pages mirror the existing pages' HTML structure, inline CSS, and navigation header so the site stays visually consistent.

- [ ] **Step 1: Study the house style**

Read `docs/index.html` and one content page (e.g. `docs/ERROR_HANDLING.html`) to copy the exact page shell (head/styles, nav header, section markup) used by every doc page. New pages must reuse that shell.

- [ ] **Step 2: Update `ERROR_HANDLING.html`**

Document the new `'auth'` `AppErrorKind` and the optional `code` field; explain that `http-error.ts` maps status `0`→`network`, `401`→`auth`, else→`http` (carrying `status` + backend `code`). Reference `src/app/core/network/api/http-error.ts`.

- [ ] **Step 3: Update `PROJECT_LIBRARIES.html`**

Add rows for **Tailwind CSS 3.4.17**, **autoprefixer/postcss**, and **lucide-angular** with one-line "why". Note Tailwind's theme is mapped to the SCSS CSS-variable tokens (one palette).

- [ ] **Step 4: Update `CORE_FILES.html`**

Add entries for the new core building blocks: all-verb `HttpClientService` + `http-error`, the `ApiDataSource` seam (`api-data-source.ts`, `mock-api-data-source.ts`), and the `core/auth/*` set (types, token-store, data sources, repository, use cases, `AuthSessionStore`) and `core/guards/auth.guard.ts`.

- [ ] **Step 5: Update `ADD_A_FEATURE.html`**

In the cookbook, note that a feature's repository depends on `API_DATA_SOURCE` (not `HttpClientService` directly), and add a "register a mock handler" step pointing at `MockApiDataSource.registerPosts()` as the worked example.

- [ ] **Step 6: Create `HTTP_CLIENT.html`**

Document `HttpClientService`: the five verbs + `HttpRequestOptions` (`body`/`headers`/`params`), Promise return, retry/backoff on transport failures only, timeout, and error mapping via `toAppError`. Include the `get`/`post` signatures.

- [ ] **Step 7: Create `DATA_SOURCE_SEAM.html`**

Document the `ApiDataSource` interface + `API_DATA_SOURCE` token, `HttpApiDataSource` vs `MockApiDataSource` (seed-backed, route registry, default binding), and the exact one-line swap to go live (`{ provide: API_DATA_SOURCE, useClass: HttpApiDataSource }` + set `env.api.baseUrl`). Use the `/posts` reference resource.

- [ ] **Step 8: Create `AUTH.html`**

Document the auth stack end-to-end: `auth.types` (roles `'admin' | 'user'`), `TokenStore`, the `AuthDataSource` seam (`MockAuthDataSource` demo users + `HttpAuthDataSource`), repository/port/providers, the login/refresh/logout use cases, `AuthSessionStore` signals, the `authInterceptor` (Bearer + 401-refresh-retry), and `authGuard`/`roleGuard`. Include the demo credentials and the live-swap note.

- [ ] **Step 9: Create `STYLING.html`**

Document the styling model: SCSS CSS-variable tokens in `theme.scss` as the single source of truth, Tailwind mapped to those variables, the existing UI kit, and lucide-angular usage (`[img]` binding).

- [ ] **Step 10: Update `index.html` (the hub)**

Add navigation cards/links to the four new pages, grouped sensibly with the existing entries.

- [ ] **Step 11: Rewrite `README.md`**

Cover: what Base App Frontend is (and how it differs from Base Frontend), `npm install` / `npm start` / `npx jest` / `npm run build`, that it runs **offline** on mock data + auth by default, the demo credentials, and the two go-live swaps (`API_DATA_SOURCE`→`HttpApiDataSource`, `AUTH_DATA_SOURCE`→`HttpAuthDataSource`, set `env.api.baseUrl`). Link to `docs/index.html`.

- [ ] **Step 12: Verify links + commit**

Open `docs/index.html` in a browser; click through to each new page and confirm the four new links resolve and the pages render in the shared style.

```bash
git add docs/ README.md
git commit -m "docs: HTTP client, data-source seam, auth, and styling pages + README"
```

---

## Phase 5 — Verification & wrap-up

### Task 16: Full verification pass

**Files:** none (verification + optional fixups).

- [ ] **Step 1: Clean install from the lockfile**

Run: `npm ci`
Expected: succeeds (proves the lockfile is consistent for a fresh clone).

- [ ] **Step 2: Full Jest suite**

Run: `npx jest`
Expected: ALL suites PASS — existing BF specs plus the new app-error, http-client, mock-api, post.repository, token-store, mock-auth, auth.repository, login, auth-session, auth.interceptor, auth.guard, and login.viewmodel specs.

- [ ] **Step 3: Production build**

Run: `npm run build`
Expected: succeeds within the existing budgets.

- [ ] **Step 4: End-to-end manual smoke (offline)**

Run: `npm start`. Verify, with no backend running:
- `/api` (fetch-posts) renders the three seeded posts.
- `/account` redirects to `/login` when signed out.
- Login as `user@example.com`/`user123` → `/account`; `/admin` redirects to `/`.
- Login as `admin@example.com`/`admin123` → `/admin` is allowed.
- `/design` shows the lucide icon.
Stop the server.

- [ ] **Step 5: Confirm clean tree**

Run: `git status -s`
Expected: empty (everything committed).

- [ ] **Step 6: Finish the branch**

Use the **superpowers:finishing-a-development-branch** skill to choose how to integrate (the work was committed directly on `master` of the new repo, so this is mainly a final review + summary).

---

## Self-Review (completed by plan author)

- **Spec coverage:** §4.1 errors/HTTP/seam → Tasks 1–4; §4.2 auth (types, token-store, data-source symmetry, repository, use cases, session store, interceptor, guards, demo pages) → Tasks 5–12; §4.3 Tailwind+lucide → Tasks 13–14; §7 testing → specs in every task + Task 16; §8 docs → Task 15; §9 success criteria → Task 16. The "not ported" items (DataStore/domain models/seed/glass-morphism) are intentionally absent. ✅
- **Placeholder scan:** no TBD/TODO; every code step shows complete code. The only "read the file first" step (Task 14) is a deliberate integration into an existing component, with the exact additions specified. ✅
- **Type consistency:** `UserRole='admin'|'user'`, `AuthSession`/`AuthTokens`/`AuthPrincipal`, `API_DATA_SOURCE`/`AUTH_DATA_SOURCE`, `signIn`/`signOut`/`role()`/`isAuthenticated()`/`principal()`, `PostsDtoRs` envelope, and `mock-access.{role}.{userId}` token shape are used identically across producing and consuming tasks. ✅
