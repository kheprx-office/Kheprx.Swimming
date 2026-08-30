# Base App Frontend — Design Spec

**Date:** 2026-06-28
**Status:** Approved (brainstorm) — ready for implementation planning
**Project:** `C:\Users\envnt\Desktop\Base App Frontend`

## 1. Goal

Create a reusable, domain-neutral, **batteries-included** Angular base called **Base App Frontend**.
It is built by **enhancing a copy of `Base Frontend`** (the minimal canonical base) with hardened
HTTP, a swappable data-source seam, and generic auth infrastructure ported and genericized from the
`Electric_webiste_angular` project.

This cycle delivers **only the template**: it builds clean, the Jest suite is green, the demo runs
**offline** (no backend required), and it ships a full HTML doc site. Bootstrapping a real app
(e.g. "swimming") is an explicit **future, separate** spec → plan → implement cycle and is **out of
scope here**.

## 2. Build approach (locked)

- **Enhance a copy**, do not de-domain Electric. `Base Frontend` is already clean and domain-neutral.
- Source copied: `Base Frontend` → `Base App Frontend`, excluding `node_modules`, `.git`, `.angular`,
  `dist`. Package renamed to `base-app-frontend`. Fresh `git init`.
- `Base Frontend` stays **untouched** as the minimal canonical base.
- All additions are **purely additive** and land in `src/app/core/` following BF's existing layout.
- Port sources from `Electric_webiste_angular` are **genericized** — no electrician domain
  (no owner/manager/moqawel/worker roles, no Arabic seed names/emails, no `DataStore` /
  `domain.models.ts` / `seed-data.ts`).

## 3. Locked decisions (from brainstorm)

| # | Decision | Choice |
|---|----------|--------|
| A | Styling | **Add Tailwind + lucide**, coexisting with BF's SCSS theme tokens (one source of truth) |
| B | Auth depth | **Richer demo** — roles + guards + an auth-gated example, runnable offline |
| C | Example slices | **Keep all** BF slices; **wire `fetch-posts` through the new seam** as the reference consumer |
| D | Scope | **Template only**; swimming/any real app is a later cycle |
| E | Docs | **Full HTML doc-site** like BF's, extended with new pages |

## 4. Architecture additions

Everything new is cross-cutting and lives under `src/app/core/`. New sub-dirs:
`core/auth/`, `core/guards/`, `core/datasource/api/`, `core/network/interceptors/`.
Path aliases (`@core/*`, `@features/*`) unchanged. Angular 20 standalone APIs, signals, Jest.

### 4.1 Foundation — errors, HTTP, seam

**AppError** (`core/domain/errors/app-error.ts`)
- Extend BF's current 6 kinds (`network | http | validation | storage | crypto | unknown`) to add
  **`'auth'`** → **7 kinds**, and add an optional **`code?: string`** field (backend error code).

**HTTP client** (`core/network/api/http-client.ts`)
- **Replace** BF's GET-only `request<T>(path)` with the all-verb client ported from Electric:
  `get/post/put/patch/delete<T>(path, opts?)` where `opts: HttpRequestOptions` carries
  `body`, `headers`, `params`.
- Preserve BF's behavior: Promise-returning, retry/exponential-backoff on transport failures only
  (never on 4xx/5xx), timeout from `env.api.timeoutMs`, layer-aware logging.

**http-error** (`core/network/api/http-error.ts`)
- Port Electric's parser: status `0` → `network`, `401` → `auth`, else → `http`.
- Parse optional error body `{ message?, code? }`; fall back to `HTTP {status}` for the message;
  carry `status` + `code` onto the `AppError`.

**ApiDataSource seam** (`core/datasource/api/`)
- `ApiDataSource` interface (mirrors the verb surface) + `API_DATA_SOURCE` injection token.
- `HttpApiDataSource` — real impl delegating to `HttpClientService` (baseUrl + error mapping).
- `MockApiDataSource` — seed-backed route registry; unmatched paths reject to surface wiring gaps.
- **Default binding: `MockApiDataSource`** in the composition root, so the base runs with **zero
  backend**. Going live = swap to `HttpApiDataSource` and set `env.api.baseUrl`.

**Reference wiring**
- Convert `fetch-posts`' `PostRepositoryImpl` to inject `API_DATA_SOURCE` instead of
  `HttpClientService` directly. Seed demo posts in `MockApiDataSource`. `fetch-posts` becomes the
  canonical seam consumer and runs offline.

### 4.2 Auth (richer demo: roles + guards)

**Genericization rules**
- Roles collapse to **`UserRole = 'admin' | 'user'`**. Two demo users (one admin, one user). No
  Arabic names/emails, no electrician roles/terms.
- **Data-source symmetry** with the generic seam: an `AuthDataSource` interface + token, with
  **`MockAuthDataSource`** (seed-backed, the two demo users; **default**) and **`HttpAuthDataSource`**
  (real, swappable). Going live = swap the binding.

**Ported / genericized pieces** (`core/auth/`, `core/guards/`, `core/network/interceptors/`)
- `auth.types` — `AuthTokens`, `AuthPrincipal { role: UserRole; userId }`, `AuthSession`.
- `token-store` — over `KeyValueStore`; keys `@auth/access-token`, `@auth/refresh-token` added to
  `core/datasource/keyvalue/storage-keys.ts`. `save/getAccess/getRefresh/clear`.
- `auth.interceptor` — attaches `Authorization: Bearer {access}`; on `401`, refreshes **once** then
  retries the original request; on refresh failure, clears tokens + redirects to `/login`.
  Registered via `provideHttpClient(withInterceptors([authInterceptor]))`.
- `auth.repository` + `auth.repository.interface` (`login`, `refresh`) + `auth.repository.port`
  (`AUTH_REPOSITORY` token) + `auth.providers` (`AUTH_PROVIDERS`).
- Use cases: `login` (calls repo, saves tokens, populates session), `refresh` (saves new tokens),
  `logout` (clears token store + session).
- **`AuthSessionStore`** — new signal-based state holder, genericized from Electric's `AuthService`
  with all name-mapping stripped: `session`, `isAuthenticated`, `principal`. Login populates it;
  logout clears it; guards read it.
- **Guards** (`core/guards/`): `authGuard` (→ `/login` if unauthenticated) and
  `roleGuard(...roles)` (→ a configurable forbidden route). Electric's `worker`→`/m` hardcoding
  is removed.

**Presentation (auth-gated demo)** — minimal pages under a new feature slice:
- `login` page (form → login use case).
- auth-gated **`account`** page (shows the principal; protected by `authGuard`).
- admin-only page (protected by `roleGuard('admin')`) to demonstrate role protection end-to-end.

**Explicitly NOT ported:** Electric's `DataStore`, `domain.models.ts`, `seed-data.ts` (heavily
domain-coupled).

### 4.3 Styling — Tailwind + lucide alongside SCSS tokens

**Coexistence model: one source of truth, no divergence.**
- Keep BF's `core/ui/theme/theme.scss` CSS custom properties as the canonical design tokens, and
  keep the existing SCSS UI kit (`nav-button`, `text-field`, `notification-host`) as-is.
- Add Tailwind: `tailwind.config.js`, `postcss.config.js` (with `autoprefixer`),
  `@tailwind base/components/utilities` in `src/styles.scss`.
- **Map Tailwind's theme** colors/spacing to the existing CSS variables (e.g.
  `primary: 'var(--color-primary)'`) so utilities and the UI kit share one palette.
- Add `lucide-angular` and **actually wire it** (Electric installed it but never used it): render
  one icon in `design-hub` so it is demonstrably working.
- **YAGNI trim:** port the Tailwind *mechanism* only — **not** Electric's bespoke glass-morphism /
  decorative animation utilities (`.glass-panel`, blob/shimmer/etc.). Those are app-flavored, not
  base material.

**New dependencies:** `tailwindcss`, `postcss`, `autoprefixer` (dev), `lucide-angular`.

## 5. Data flow (unchanged contract)

The layered dependency rule from BF is preserved:

```
Page → ViewModel → UseCase → Repository(port) → Repository(impl) → ApiDataSource(seam) → HTTP
```

Auth threads through the same shape:

```
Login page → Login VM → Login use case → AuthRepository → AuthDataSource(Mock|Http)
                                       ↘ TokenStore + AuthSessionStore
authGuard / roleGuard → read AuthSessionStore
auth.interceptor → TokenStore (Bearer + 401 refresh-retry)
```

## 6. Error handling

- Single `Result<T>` + `AppError` contract (BF's), now with the `auth` kind and `code` field.
- `http-error` centralizes status→kind mapping; the interceptor handles `401` transparently
  (refresh-once) so use cases see `auth` only when refresh itself fails.
- Use cases catch and map to `Result<T>`; view models surface errors to the notification host.

## 7. Testing (Jest, all green)

Every ported piece ships Jest specs:
- extended `http-client` (all verbs + error mapping), `http-error`.
- `HttpApiDataSource`, `MockApiDataSource`.
- `token-store`, `auth.interceptor` (401-refresh-retry-once), `login` / `refresh` / `logout`
  use cases, `authGuard` / `roleGuard`, `AuthSessionStore`, login viewmodel.
- BF's existing ~40 specs stay green; the `fetch-posts` repository + its spec are updated for the
  seam and the new client signature.
- Stack: `jest` + `jest-preset-angular` (BF's). No Karma.

## 8. Documentation (full HTML site like BF's)

Copy BF's 10-page HTML hub, then:
- **Update** affected pages: `ERROR_HANDLING` (+`auth` kind, `code`), `PROJECT_LIBRARIES`
  (+Tailwind/lucide), `CORE_FILES`, `ADD_A_FEATURE` (mentions the seam).
- **Add** new pages: **HTTP Client** (all verbs), **Data-Source Seam** (Mock↔Http swap),
  **Auth** (interceptor / token-store / guards / use-cases / session store), **Styling**
  (tokens + Tailwind + lucide).
- Update the hub `index.html`.
- **README**: setup, run-offline, and the two go-live swaps (data source → `Http`, auth source →
  `Http`, set `env.api.baseUrl`).

## 9. Scope boundary & success criteria

**In scope (this cycle):**
- The Base App Frontend template: copy + genericized port of foundation/auth/styling above.
- `ng build` clean, `jest` green, demo **runnable offline**, full HTML docs + README.

**Out of scope (future cycles):**
- Any real domain app (swimming or otherwise) bootstrapped from this base.
- Electric's domain layer (`DataStore`, domain models, seed data).
- Tailwind decorative/glass-morphism utility library.

**Done when:** clean install builds, the Jest suite passes, the app runs and demonstrates
fetch-posts (through the seam, offline), the auth flow (login → auth-gated account → admin-only via
roleGuard → logout) offline, and the doc site + README are complete.

## 10. References

- Copy source: `C:\Users\envnt\Desktop\Base Frontend`
- Port source: `C:\Users\envnt\Desktop\Electric_webiste_angular`
- Related: Electric clean-arch conversion (origin of the foundation enhancements); Angular base
  mirrors the RN mobile base.
