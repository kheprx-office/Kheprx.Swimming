# Design: Angular Base — HTML Architecture Docs

**Date:** 2026-06-26
**Status:** Approved

## Goal

Give the Angular `Base Frontend` the same set of polished, standalone **HTML architecture
documents** that the sibling base projects already ship — the React Native mobile base
(`C:\Users\envnt\Desktop\BaseProject\docs`) and the .NET backend base
(`C:\Users\envnt\Desktop\Base Backend\docs`). These pages *present* the architecture visually
(layer color-tags, cards, SVG flow diagrams, code blocks, convention tables) so the three bases
read as siblings.

The Angular base currently has only a markdown guide (`docs/architecture/README.md`) plus the
superpowers specs/plans — it is missing the HTML presentation pages. This project fills that gap.

## Scope

Create **5 self-contained HTML pages**, flat in `docs/` (uppercase names, matching the other
bases), each with inline `<style>` reusing the established design system:

1. `ARCHITECTURE_OVERVIEW.html` — the entry point.
2. `CORE_FILES.html` — the `core/` building blocks.
3. `FEATURE_ANATOMY.html` — anatomy of a feature vertical slice.
4. `DATA_FLOW.html` — the runtime request/data flow.
5. `PROJECT_LIBRARIES.html` — dependencies and rationale.

**Reference docs to match (style + role):**
- Mobile: `REACT_NATIVE_INITIAL_STRUCTURE.html`, `CORE_FILES.html`, `API_FLOW.html`, `PROJECT_LIBRARIES.html`
- Backend: `ARCHITECTURE_OVERVIEW.html`, `SOLUTION_STRUCTURE.html`, `REQUEST_FLOW.html`, `MODULE_ANATOMY.html`, `PROJECT_LIBRARIES.html`

**Out of scope (YAGNI):**
- Any code change to `src/` — this is documentation only.
- A documentation generator / build step — the pages are hand-authored static HTML (the existing
  ones are bespoke and hand-crafted).
- A separate index/landing page — `ARCHITECTURE_OVERVIEW.html` is the entry and cross-links the rest
  (mirrors the backend's "read this first, then…" pattern).
- Retiring the markdown guide — `docs/architecture/README.md` stays.

## Production Approach

Hand-author the 5 pages, reusing the exact design system from the existing docs. All content is
derived from the **actual implemented code** and the design spec
(`docs/superpowers/specs/2026-06-26-angular-base-design.md`) — no invented APIs. Each page is fully
self-contained (inline CSS, no shared stylesheet, no external assets), exactly like the mobile and
backend docs, so a page opens correctly from the filesystem with no build.

## Shared Design System

Reuse the established tokens and components verbatim (from the mobile/backend docs):

- CSS variables: `--bg #f6f8fa`, `--card #fff`, `--border #e1e4e8`, `--text #1f2328`,
  `--muted #57606a`, `--accent #2563eb`, `--accent-soft #eaf1ff`, `--code-bg #0f172a`,
  `--code-text #e2e8f0`.
- Components: `.wrap` (max-width 980px), `header.page`, `.banner`, `.legend` + `.tag`,
  `h2.section`, `p.lead`, `.card` (with left-border layer color), `pre` (dark code block) with
  tokenized highlight spans (`.tok-c/.tok-k/.tok-s/.tok-t`), `code.inline`, `pre.tree` (folder
  tree with colored group labels), `ol.steps` (numbered step cards), `table.conv` (convention
  table), `footer.page`.
- Inline SVG for the architecture/flow diagrams (same visual language as the backend's SVG).

### Frontend layer taxonomy (the color legend)

The frontend's layers differ from the backend's, so the legend is frontend-specific:

| Tag | Layer | Color (text / bg) |
| --- | --- | --- |
| `presentation` | pages + viewmodels (signals) | `#5b21b6` / `#ede9fe` |
| `domain` | use cases, models, repository ports | `#166534` / `#dcfce7` |
| `data` | DTOs, repository impls | `#9a3412` / `#ffedd5` |
| `core` | cross-cutting infra | `#2563eb` / `#eaf1ff` |

`CORE_FILES.html` may add finer sub-group colors within the `core` family (e.g. domain / network /
datasource / crypto / logging / config / ui) for its folder tree, following the mobile
`CORE_FILES.html` precedent — but the four layers above are the canonical legend used across the set.

## Page Contents

### 1. `ARCHITECTURE_OVERVIEW.html` (entry point)
- Header + intro banner: "Clean Architecture + MVVM, feature-first — the web counterpart of the RN mobile base."
- Layer legend (the 4 tags).
- "The big picture" **SVG**: `presentation → domain → data → core`, with `Result<T>` / `AppError`
  flowing back; signals at the presentation edge.
- The one-way dependency rule, and the two approved Angular adaptations: repository ports are an
  interface **+ `InjectionToken`**; the domain/use-case layer uses Angular DI (`inject`,
  `InjectionToken`, `@Injectable`) but is free of HTTP/storage/UI concerns.
- **Folder map** (`pre.tree`) of `src/app` (`core/` + `features/<group>/<feature>/{data,domain,presentation}`).
- **Composition-root mapping table** (`table.conv`): `index.js → src/main.ts`,
  `App.tsx → src/app/app.ts (class App)`, `RootProviders → app.config.ts`,
  `RootNavigator → app.routes.ts`.
- "Read next" cross-links to the other four pages.

### 2. `CORE_FILES.html`
- Intro + `core/` folder tree (`pre.tree`).
- A **card per building block** (file path + one-line purpose + a short real snippet), grouped:
  - domain: `result.ts` (`Result`/`ok`/`fail`), `app-error.ts` (`AppError` + the 6 kinds),
    `usecase/use-case.ts` (`UseCase` base).
  - `logging/logger.ts` (layer/level-aware).
  - `config/env.ts`.
  - network: `base-response-rs.ts`, `http-client.ts` (retry/timeout → Promise, `AppError` mapping).
  - datasource: `key-value.store.ts` (localStorage, async, corrupt-object-throws/primitive-falls-back),
    `storage-keys.ts`.
  - crypto: `crypto.service.ts` (Web Crypto AES-GCM, DI seams), `types.ts`,
    `web-crypto-key-store.ts` (non-extractable key in IndexedDB), `index.ts`.
  - ui: `theme` (tokens + CSS vars), `nav-button`, `text-field`, `notification.service` + host.

### 3. `FEATURE_ANATOMY.html`
- Intro: "every feature is a vertical slice with the same shape."
- `features/api/fetch-posts` folder tree.
- **Layer-by-layer cards** with the worked fetch-posts code: `dto + isPostDtoRsValid` →
  `model + toPost` → `port (IPostRepository + POST_REPOSITORY)` → `repository impl` →
  `GetPostsUseCase` → `FetchPostsViewModel` (signals, clear-on-failure) → `ApiPage` →
  `index.ts` barrel → route wiring (`app.routes.ts` providers).
- The **two repository-port patterns**: DTO-returning for remote/untrusted data (validated + mapped
  in the use case — fetch-posts) vs. domain-returning for local/trusted data (offline-storage).
- **Naming-conventions table** (`…DtoRs`/`…DtoRq`, `I…Repository` + `…_REPOSITORY` token,
  `…UseCase`, `…RepositoryImpl`, `…ViewModel`, kebab-case files / mobile-aligned class names).
- A numbered **"how to add a feature"** list (`ol.steps`).

### 4. `DATA_FLOW.html`
- Intro + **flow SVG**: `Page → ViewModel (signals) → UseCase.run() → Repository (port) →
  http-client / KeyValueStore / CryptoService`, with `Result<T>` / `AppError` flowing back.
- Numbered **`ol.steps`** tracing fetch-posts end to end: button → `vm.run()` sets `loading` →
  `useCase.run()` → `execute()` fetch + validate + map → `httpClient.request()` → envelope synthesis
  → `Result` → signals → success/error toast.
- An **error-handling lane**: where each `AppError` kind originates (http-client `network`/`http`,
  KeyValueStore `storage`, CryptoService `crypto`, use case `validation`, base `unknown`) and how
  `UseCase.run()` normalizes any throw into a `Result`.
- Short code snippets at each hop.

### 5. `PROJECT_LIBRARIES.html`
- Intro + legend (runtime vs. dev).
- **Dependency cards** (what it's for / why chosen):
  - runtime: `@angular/core`, `@angular/common` (+ `/http`), `@angular/router`, `@angular/forms`,
    `@angular/platform-browser` (+ animations), `@angular/animations`, `rxjs` (used inside
    `http-client`), `zone.js`, `tslib`.
  - dev: `jest`, `jest-preset-angular`, `@types/jest`, `jest-environment-jsdom`, `typescript`,
    `@angular/build` / `@angular/cli`.
- Note the deliberate omissions: no global state library (NgRx) — local signals suffice; no extra
  HTTP client — Angular `HttpClient` wrapped.
- Versions should reflect the actual `package.json` at authoring time.

## Cross-Linking & Integration

- `ARCHITECTURE_OVERVIEW.html` links to the other four (and they link back to it), via a small
  nav/cross-link block, mirroring the backend set.
- Update `docs/architecture/README.md` and the project `README.md` to link the HTML doc set.

## Accuracy Constraints

- Every file path, class/type name, method signature, and code snippet must match the real code in
  `src/app`. No invented or aspirational APIs.
- Library versions in `PROJECT_LIBRARIES.html` must match `package.json`.
- Self-contained: each page must open directly from the filesystem (no external CSS/JS/fonts/CDN).

## Verification

- All 5 files exist flat in `docs/` and are valid, self-contained HTML (open in a browser with no
  console/network errors and no missing styles).
- Spot-check accuracy: file paths and snippets in each page match the corresponding files in `src/app`.
- `PROJECT_LIBRARIES.html` versions match `package.json`.
- Cross-links resolve (relative hrefs between the 5 pages); `README.md` and
  `docs/architecture/README.md` link the set.
- Visual parity: the pages use the same design-system components/tokens as the mobile/backend docs.

## Success Criteria

- The Angular base ships the 5 HTML architecture pages, matching the role and visual language of the
  mobile and backend bases.
- The content is accurate to the implemented code and dependencies.
- The pages are self-contained and cross-linked, with the markdown guide and README pointing at them.
