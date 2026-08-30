# Design: Base Frontend — Documentation Site (Depth + Hub)

**Date:** 2026-06-28
**Status:** Approved

## Goal

Bring the Angular `Base Frontend` HTML docs up to the **same style and the same goal** as the
.NET backend base docs (`C:\Users\envnt\Desktop\Base Backend\docs`): a cohesive, guided
**documentation site** that teaches the team by describing each thing in detail.

The backend ships a `index.html` hub with a numbered learning path, a sticky top-nav on every
page, and ~16 detailed pages (including layer-by-layer breakdowns, request flow, validation, host
API, shared kernel). The frontend currently ships 5 strong but **disconnected** pages — no hub, no
unified nav, and fewer topics. This project closes that gap so the frontend docs read as a sibling
of the backend set in both depth and navigation.

This builds directly on the prior spec
[`2026-06-26-angular-architecture-html-docs-design.md`](2026-06-26-angular-architecture-html-docs-design.md),
which created the existing 5 pages. That spec deliberately said *"no separate index/landing page."*
**This spec reverses that decision**: the set has grown to 10 pages, so a hub with a learning path
(matching the backend) is now the right entry point.

## Scope

**A. Add the hub.** Create `docs/index.html` — the backend-style landing page: hero with a
"Start here" call to action, plus numbered **learning-path stages** grouping all 10 pages.

**B. Unify navigation & style.** Replace the existing pill-nav on the 5 current pages with the
backend's **sticky top-nav** (brand `Base Frontend` + links to the main pages + Home), so every
page — old and new — belongs to one site. Keep the frontend's established layer color palette.

**C. Add 5 new deep-dive pages**, flat in `docs/` (uppercase names, matching the set):

6. `COMPOSITION_ROOT.html` — how the app boots and wires itself (DI).
7. `ERROR_HANDLING.html` — `Result<T>`, `AppError`, and the `UseCase` contract.
8. `VALIDATION.html` — DTO validation at the boundary + reactive-forms validation.
9. `ADD_A_FEATURE.html` — the step-by-step cookbook for a new vertical slice.
10. `TESTING.html` — what each layer's spec covers and the TestBed patterns.

The existing 5 pages keep their names: `ARCHITECTURE_OVERVIEW.html`, `PROJECT_LIBRARIES.html`,
`CORE_FILES.html`, `FEATURE_ANATOMY.html`, `DATA_FLOW.html`.

### Final page set & learning path (the hub)

| Stage | # | Page | File | Status |
| --- | --- | --- | --- | --- |
| 1 · Foundations | 1 | Architecture Overview | `ARCHITECTURE_OVERVIEW.html` | exists (restyle nav) |
| 2 · Structure & Building Blocks | 2 | Project Libraries | `PROJECT_LIBRARIES.html` | exists (restyle nav) |
| | 3 | Core Files *(Shared Kernel)* | `CORE_FILES.html` | exists (restyle nav) |
| | 4 | Feature Anatomy | `FEATURE_ANATOMY.html` | exists (restyle nav) |
| 3 · How a View Renders | 5 | Data Flow | `DATA_FLOW.html` | exists (restyle nav) |
| | 6 | Composition Root & DI | `COMPOSITION_ROOT.html` | **new** |
| 4 · In Depth | 7 | Error Handling & Result | `ERROR_HANDLING.html` | **new** |
| | 8 | Validation & Forms | `VALIDATION.html` | **new** |
| | 9 | Add a Feature | `ADD_A_FEATURE.html` | **new** |
| | 10 | Testing | `TESTING.html` | **new** |

**Out of scope (YAGNI):**
- Any code change to `src/` — documentation only.
- A documentation generator / build step — pages are hand-authored static HTML.
- A JS framework, client-side search, or external assets/CDN — pages stay self-contained.
- Splitting `FEATURE_ANATOMY.html` into per-layer pages — the new DI / Error / Validation pages
  already supply that depth.
- Retiring the markdown guide — `docs/architecture/README.md` stays (and is updated to link the set).

## Production Approach

Hand-author the hub and the 5 new pages, and edit the nav block on the 5 existing pages. Reuse the
**exact design system** already established in the existing docs (the prior spec's Shared Design
System) and the backend's hub/top-nav components. All content derives from the **actual implemented
code** in `src/app` and the design spec
([`2026-06-26-angular-base-design.md`](2026-06-26-angular-base-design.md)) — no invented APIs. Every
page remains fully self-contained (inline CSS, no shared stylesheet, no external assets), so any
page opens directly from the filesystem with no build.

## Shared Design System

Two parts: the page-level system (already in use, kept verbatim) and the new site-level chrome
(ported from the backend).

### Page-level (unchanged, reused on every page)
- CSS variables: `--bg #f6f8fa`, `--card #fff`, `--border #e1e4e8`, `--text #1f2328`,
  `--muted #57606a`, `--accent #2563eb`, `--accent-soft #eaf1ff`, `--code-bg #0f172a`,
  `--code-text #e2e8f0`.
- Components: `.wrap` (max-width 980px), `header.page`, `.banner`, `.legend` + `.tag`,
  `h2.section`, `p.lead`, `.card` (left-border layer color), `pre` (dark code block with
  `.tok-c/.tok-k/.tok-s/.tok-t` highlight spans), `code.inline`, `pre.tree`, `ol.steps`,
  `table.conv`, `footer.page`. Inline SVG for diagrams.

### Site-level chrome (new — ported from the backend `index.html`)
- **`.topnav`** — sticky, `--accent` (`#2563eb`) background, max-width 980px inner row,
  `flex-wrap` so links wrap gracefully. Contains the `.brand` (`Base Frontend`, monospace) and
  `.nl` links; the current page's link gets `.nl.active`. This **replaces** the existing
  `.nav` pill row on the 5 current pages.
- **Hub-only components** (`index.html`): `header.hero` (h1 + intro + `.start` button),
  `.stage` (a learning-path group with a numbered `.pill` label), and `a.doc` cards
  (numbered circle `.n`, title `.tt`, description `.dd`, left-border colored by layer).
- The top-nav lists Home + all 10 pages (Architecture, Libraries, Core, Feature, Data Flow,
  Composition, Errors, Validation, Add a Feature, Testing), using short labels and `flex-wrap`
  so the row wraps gracefully on narrow widths (same mechanism the backend top-nav uses). The
  hub and the page footers remain the canonical place for the full ordered learning path.

### Frontend layer taxonomy (the color legend — unchanged)

| Tag | Layer | Color (text / bg) |
| --- | --- | --- |
| `presentation` | pages + viewmodels (signals) | `#5b21b6` / `#ede9fe` |
| `domain` | use cases, models, repository ports | `#166534` / `#dcfce7` |
| `data` | DTOs, repository impls | `#9a3412` / `#ffedd5` |
| `core` | cross-cutting infra | `#2563eb` / `#eaf1ff` |

The hub's stage cards use these four colors on their left border (the same way the backend hub
color-codes by layer).

## Page Contents

### 0. `index.html` (the hub — NEW)
- Sticky top-nav (active = Home).
- `header.hero`: `Base Frontend — Documentation`, one-line intro ("An Angular v20 base —
  Clean Architecture + MVVM, feature-first — explained end to end. New here? Follow the path
  below in order."), and a `.start` button → `ARCHITECTURE_OVERVIEW.html`.
- Four `.stage` groups with `a.doc` cards, exactly per the learning-path table above, each card
  carrying its number, title, and one-line description, color-coded by the layer it concerns.
- `footer.page` listing the full ordered path.

### 6. `COMPOSITION_ROOT.html` (NEW — mirrors backend `HOST_API` / composition root)
- Intro banner: "How the app boots and how every dependency gets wired."
- **Boot sequence** (`ol.steps` or SVG): `src/main.ts` (`bootstrapApplication(App, appConfig)`) →
  `app.config.ts` (`appConfig` — `provideRouter`, `provideHttpClient`, animations) →
  `app.routes.ts` (typed `Routes`, lazy `loadComponent`) → `app.ts` (root shell, `<router-outlet>`
  + notification host).
- **The `InjectionToken` port pattern**: why a TS interface alone can't be injected (type erasure),
  so each `I…Repository` is paired with a `…_REPOSITORY` token; card with the real
  `POST_REPOSITORY` example.
- **Route-scoped providers**: a real `app.routes.ts` entry showing
  `{ provide: POST_REPOSITORY, useClass: PostRepositoryImpl }`, the use case, and the viewmodel
  bound at route level; explain why this keeps features self-contained and makes swapping a fake in
  tests trivial.
- **Composition-root mapping table** (mobile ↔ Angular), consistent with the Architecture Overview.

### 7. `ERROR_HANDLING.html` (NEW — mirrors backend `VALIDATION` error spine)
- Intro: "One error contract end to end."
- **`Result<T>` card**: the discriminated union, `ok()` / `fail()`, and `.ok` type-narrowing, with
  the real `result.ts` snippet.
- **`AppError` card**: constructor + the kinds `network`, `http`, `validation`, `storage`,
  `crypto`, `unknown`, and where each originates (a `table.conv`: kind → source layer/file).
- **`UseCase` base card**: the abstract `UseCase<I,O>`, the single `try/catch` in `run()`, the
  `execute()` template method, and `AppError`-vs-unknown normalization (`use-case.ts` snippet).
- **The rule**: no `try/catch` above `UseCase.run()`; the viewmodel switches on `result.ok` and
  resets stale data on failure.

### 8. `VALIDATION.html` (NEW — mirrors backend `VALIDATION` + `VALIDATION_GUIDE`)
- Intro + legend distinguishing the **two validation sites**.
- **Boundary (DTO) validation** for untrusted/remote data: `isPostDtoRsValid` in the use case,
  throwing `AppError('…','validation')`; tie back to the "remote/untrusted" repository-port pattern.
- **Reactive-forms validation** for user input: the `profile-form` feature — `FormGroup`/
  `Validators`, error display, submit gating; the trusted/local pattern needs no DTO layer.
- A short **"where to validate what"** table (untrusted boundary vs. user input vs. trusted local).

### 9. `ADD_A_FEATURE.html` (NEW — the cookbook, mirrors backend guide style)
- Intro: "Add a vertical slice by following `api/fetch-posts`."
- The 11-step `ol.steps` recipe from `docs/architecture/README.md` §6, each step with the real
  template snippet: folder scaffold → DTO + validator → model + mapper → port + token →
  repository impl → use case → viewmodel → page → barrel → route wiring → tests.
- Callout on the **clear-stale-data-on-failure** rule.

### 10. `TESTING.html` (NEW — mirrors backend guide depth)
- Intro: how testing is set up (`jest-preset-angular`, `jest.config.js`, `setup-jest.ts`, path
  aliases) and `npm test`.
- **Coverage table** (`table.conv`) — what each layer's `.spec` verifies (ported from
  `docs/architecture/README.md` §7).
- **TestBed recipes**: the use-case test (bind a fake repo to the port token, assert on `Result`)
  and the viewmodel test (stub the use case's `run`, assert on signal transitions), with real
  snippets and pointers to `get-posts.use-case.spec.ts` / `fetch-posts.viewmodel.spec.ts`.
- Note the pragmatic depth of component smoke tests (matches the mobile base).

## Cross-Linking & Integration

- The sticky top-nav (with Home → `index.html`) appears on **all 11 pages**.
- The hub orders the full learning path; each page footer lists the path too.
- Existing pages' internal "read next" cross-links are kept and extended to the new pages where
  relevant (e.g. Feature Anatomy → Add a Feature; Data Flow → Error Handling).
- Update `docs/architecture/README.md` (the "Visual architecture docs (HTML)" list) and the project
  `README.md` to point at `index.html` as the entry and list the new pages.

## Accuracy Constraints

- Every file path, class/type name, method signature, and code snippet must match the real code in
  `src/app`. No invented or aspirational APIs.
- Snippets in the new pages are taken from (or faithfully reduced from) the actual source:
  `main.ts`, `app/app.ts`, `app/app.config.ts`, `app/app.routes.ts`, `core/domain/**`,
  `core/network/api/**`, the `api/fetch-posts` and `design/profile-form` slices, and the `.spec`
  files referenced in the Testing page.
- Self-contained: each page opens directly from the filesystem (no external CSS/JS/fonts/CDN).
- Visual parity with the existing pages and the backend hub (same tokens/components).

## Verification

- All 11 files exist flat in `docs/` (`index.html` + 10 pages) and are valid, self-contained HTML
  (open in a browser with no console/network errors and no missing styles).
- The sticky top-nav is present and consistent on every page; the active link matches the page.
- The hub lists all 10 pages in the four learning-path stages, in order, color-coded.
- Spot-check accuracy: file paths and snippets in each new page match the corresponding files in
  `src/app`; the Testing coverage table matches the actual `.spec` files.
- Cross-links resolve (relative hrefs among all 11 pages); `README.md` and
  `docs/architecture/README.md` link the set with `index.html` as the entry.

## Success Criteria

- The Angular base ships a cohesive documentation **site** — a hub + 10 detailed pages with unified
  navigation — matching the role, depth, and visual language of the backend base docs.
- The content is accurate to the implemented code, dependencies, and tests.
- A new team member can open `index.html` and follow the numbered path from "big picture" to
  "add a feature and test it" without leaving the site.
