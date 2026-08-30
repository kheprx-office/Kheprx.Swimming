# Base Frontend Documentation Site Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the frontend's 5 disconnected HTML docs into a cohesive, backend-style documentation site — an `index.html` hub with a numbered learning path, a unified sticky top-nav on every page, and 5 new deep-dive pages — matching the depth and goal of the .NET backend docs.

**Architecture:** Hand-authored, fully self-contained static HTML pages in `docs/` (inline `<style>`, no build step, no external assets), reusing the design system already established in the existing pages plus the sticky top-nav/hub chrome ported from the backend's `index.html`. All code snippets are copied verbatim (or faithfully reduced) from the real source in `src/app`.

**Tech Stack:** Static HTML5 + inline CSS + inline SVG. No JS, no framework, no doc generator. Verification via shell (`grep`) + a manual browser open.

**Spec:** [`docs/superpowers/specs/2026-06-28-frontend-docs-site-design.md`](../specs/2026-06-28-frontend-docs-site-design.md)

## Global Constraints

- **Self-contained:** every page must open from the filesystem with no network. No `<link rel="stylesheet">`, no `<script src=…>`, no CDN/font/asset URLs. Inline `<style>` only. (Inline `<svg>` is fine.)
- **Design system reuse:** copy the full `<style>` block from `docs/ARCHITECTURE_OVERVIEW.html` as the canonical page stylesheet for every new content page, then add the `.topnav` rules (below). Do **not** invent new tokens.
- **Layer color palette (unchanged):** `--pres #5b21b6`, `--dom #166534`, `--data #9a3412`, `--core #2563eb` (backgrounds: `#ede9fe`, `#dcfce7`, `#ffedd5`, `#eaf1ff`). Accent `--accent #2563eb`.
- **Accuracy:** every file path, identifier, signature, and snippet must match the real code in `src/app`. No invented or aspirational APIs. When in doubt, read the source file named in the task.
- **Canonical top-nav link list (identical order on every page, Home first):** Home → `index.html`, Architecture → `ARCHITECTURE_OVERVIEW.html`, Libraries → `PROJECT_LIBRARIES.html`, Core → `CORE_FILES.html`, Feature → `FEATURE_ANATOMY.html`, Data Flow → `DATA_FLOW.html`, Composition → `COMPOSITION_ROOT.html`, Errors → `ERROR_HANDLING.html`, Validation → `VALIDATION.html`, Add a Feature → `ADD_A_FEATURE.html`, Testing → `TESTING.html`.
- **Canonical `.topnav` CSS** (add to every page's `<style>`):

```css
.topnav { position:sticky; top:0; z-index:50; background:var(--accent); margin:0 -16px; }
.topnav .ti { max-width:980px; margin:0 auto; padding:9px 16px; display:flex; flex-wrap:wrap; align-items:center; gap:2px 4px; }
.topnav a { text-decoration:none; }
.topnav .brand { color:#fff; font-weight:700; font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace; margin-right:12px; font-size:0.95rem; }
.topnav .nl { color:#fff; font-size:0.85rem; padding:4px 9px; border-radius:6px; opacity:.9; }
.topnav .nl:hover { background:rgba(255,255,255,.16); opacity:1; }
.topnav .nl.active { background:rgba(255,255,255,.24); opacity:1; font-weight:600; }
```

- **Canonical `.topnav` HTML** (placed as the **first child of `<body>`**, before `<div class="wrap">`; on each page give exactly one `.nl` the `active` class — the one matching that page):

```html
<nav class="topnav">
  <div class="ti">
    <a class="brand" href="index.html">Base Frontend</a>
    <a class="nl" href="index.html">Home</a>
    <a class="nl" href="ARCHITECTURE_OVERVIEW.html">Architecture</a>
    <a class="nl" href="PROJECT_LIBRARIES.html">Libraries</a>
    <a class="nl" href="CORE_FILES.html">Core</a>
    <a class="nl" href="FEATURE_ANATOMY.html">Feature</a>
    <a class="nl" href="DATA_FLOW.html">Data Flow</a>
    <a class="nl" href="COMPOSITION_ROOT.html">Composition</a>
    <a class="nl" href="ERROR_HANDLING.html">Errors</a>
    <a class="nl" href="VALIDATION.html">Validation</a>
    <a class="nl" href="ADD_A_FEATURE.html">Add a Feature</a>
    <a class="nl" href="TESTING.html">Testing</a>
  </div>
</nav>
```

- **Commit style:** one commit per task, `docs:` prefix, end with the Co-Authored-By trailer the repo uses.
- **Reusable verification snippet** (run from repo root; used in every task's verify step):

```bash
F="docs/<PAGE>.html"
test -f "$F" && echo "EXISTS"
grep -c 'class="topnav"' "$F"                         # expect: 1
grep -oE 'class="nl[^"]*active' "$F" | head           # expect: exactly one match
grep -nE '<link rel="stylesheet"|<script ' "$F" || echo "SELF-CONTAINED (no external css/js)"
# cross-link integrity: every internal href target must exist in docs/
for h in $(grep -oE 'href="[A-Z_]+\.html"' "$F" | sed -E 's/href="(.*)"/\1/' | sort -u); do
  test -f "docs/$h" && echo "link ok: $h" || echo "BROKEN LINK: $h"
done
```

---

### Task 1: Build the hub (`index.html`)

The new entry point. Establishes the site chrome (sticky top-nav) and the numbered learning path over all 10 pages. It is fully self-contained and uses the frontend layer palette.

**Files:**
- Create: `docs/index.html`

**Interfaces:**
- Produces: the canonical `.topnav` block (Home active) and the four learning-path stages. Tasks 2–7 reuse the identical `.topnav` HTML/CSS.

- [ ] **Step 1: Create `docs/index.html` with this exact content**

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Documentation — Base Frontend</title>
  <style>
  :root {
    --bg:#f6f8fa; --card:#fff; --border:#e1e4e8; --text:#1f2328; --muted:#57606a;
    --accent:#2563eb; --accent-soft:#eaf1ff;
    --pres:#5b21b6; --dom:#166534; --data:#9a3412; --core:#2563eb;
  }
  * { box-sizing:border-box; }
  body { margin:0; padding:0 16px 72px; background:var(--bg); color:var(--text);
    font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif; line-height:1.6; }
  .wrap { max-width:980px; margin:0 auto; }
  .topnav { position:sticky; top:0; z-index:50; background:var(--accent); margin:0 -16px; }
  .topnav .ti { max-width:980px; margin:0 auto; padding:9px 16px; display:flex; flex-wrap:wrap; align-items:center; gap:2px 4px; }
  .topnav a { text-decoration:none; }
  .topnav .brand { color:#fff; font-weight:700; font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace; margin-right:12px; font-size:0.95rem; }
  .topnav .nl { color:#fff; font-size:0.85rem; padding:4px 9px; border-radius:6px; opacity:.9; }
  .topnav .nl:hover { background:rgba(255,255,255,.16); opacity:1; }
  .topnav .nl.active { background:rgba(255,255,255,.24); opacity:1; font-weight:600; }
  header.hero { padding:36px 0 8px; }
  header.hero h1 { margin:0 0 6px; font-size:1.9rem; }
  header.hero h1 .mono { font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace; color:var(--accent); }
  header.hero p { margin:0; color:var(--muted); font-size:0.98rem; max-width:70ch; }
  .start { display:inline-block; margin-top:16px; background:var(--accent); color:#fff; font-weight:600; font-size:0.92rem; padding:10px 18px; border-radius:8px; text-decoration:none; }
  .start:hover { background:#1d4ed8; }
  .stage { margin:30px 0 0; }
  .stage .lab { display:flex; align-items:center; gap:9px; font-size:0.74rem; letter-spacing:.5px; text-transform:uppercase; color:var(--muted); font-weight:700; margin-bottom:11px; }
  .stage .lab .pill { background:var(--accent-soft); color:var(--accent); width:22px; height:22px; border-radius:50%; display:inline-flex; align-items:center; justify-content:center; font-size:0.74rem; }
  .grid { display:flex; flex-direction:column; gap:9px; }
  a.doc { display:flex; gap:13px; align-items:flex-start; background:var(--card); border:1px solid var(--border); border-left-width:5px; border-radius:10px; padding:13px 15px; text-decoration:none; color:inherit; }
  a.doc:hover { box-shadow:0 3px 12px rgba(0,0,0,.08); transform:translateY(-1px); }
  a.doc .n { flex-shrink:0; width:25px; height:25px; border-radius:50%; background:var(--accent); color:#fff; font-weight:700; font-size:0.82rem; display:flex; align-items:center; justify-content:center; }
  a.doc .tt { font-size:1.0rem; font-weight:700; color:var(--text); }
  a.doc .dd { font-size:0.86rem; color:var(--muted); margin-top:2px; }
  a.doc.pres { border-left-color:var(--pres); } a.doc.dom { border-left-color:var(--dom); }
  a.doc.data { border-left-color:var(--data); } a.doc.core { border-left-color:var(--core); }
  footer.page { margin-top:48px; padding-top:18px; border-top:1px solid var(--border); color:var(--muted); font-size:0.86rem; }
  footer.page a { color:var(--accent); }
  </style>
</head>
<body>
  <nav class="topnav">
    <div class="ti">
      <a class="brand" href="index.html">Base Frontend</a>
      <a class="nl active" href="index.html">Home</a>
      <a class="nl" href="ARCHITECTURE_OVERVIEW.html">Architecture</a>
      <a class="nl" href="PROJECT_LIBRARIES.html">Libraries</a>
      <a class="nl" href="CORE_FILES.html">Core</a>
      <a class="nl" href="FEATURE_ANATOMY.html">Feature</a>
      <a class="nl" href="DATA_FLOW.html">Data Flow</a>
      <a class="nl" href="COMPOSITION_ROOT.html">Composition</a>
      <a class="nl" href="ERROR_HANDLING.html">Errors</a>
      <a class="nl" href="VALIDATION.html">Validation</a>
      <a class="nl" href="ADD_A_FEATURE.html">Add a Feature</a>
      <a class="nl" href="TESTING.html">Testing</a>
    </div>
  </nav>
  <div class="wrap">
    <header class="hero">
      <h1><span class="mono">Base Frontend</span> — Documentation</h1>
      <p>An Angular v20 base — Clean Architecture + MVVM, feature-first — explained end to end. New here? Follow the path below in order; each step builds on the last.</p>
      <a class="start" href="ARCHITECTURE_OVERVIEW.html">Start here → Architecture Overview</a>
    </header>

    <div class="stage">
      <div class="lab"><span class="pill">1</span> Foundations</div>
      <div class="grid">
        <a class="doc core" href="ARCHITECTURE_OVERVIEW.html"><span class="n">1</span><span><span class="tt">Architecture Overview</span><span class="dd">The big picture: Clean Architecture + MVVM and the one-way layer dependency. Read this first.</span></span></a>
      </div>
    </div>

    <div class="stage">
      <div class="lab"><span class="pill">2</span> Structure &amp; Building Blocks</div>
      <div class="grid">
        <a class="doc core" href="PROJECT_LIBRARIES.html"><span class="n">2</span><span><span class="tt">Project Libraries</span><span class="dd">The dependencies the base relies on, and why each one is used.</span></span></a>
        <a class="doc core" href="CORE_FILES.html"><span class="n">3</span><span><span class="tt">Core Files</span><span class="dd">The shared kernel: Result, AppError, UseCase, HTTP client, storage, crypto, logging, UI.</span></span></a>
        <a class="doc dom" href="FEATURE_ANATOMY.html"><span class="n">4</span><span><span class="tt">Feature Anatomy</span><span class="dd">Inside one feature: the data / domain / presentation vertical slice.</span></span></a>
      </div>
    </div>

    <div class="stage">
      <div class="lab"><span class="pill">3</span> How a View Renders</div>
      <div class="grid">
        <a class="doc pres" href="DATA_FLOW.html"><span class="n">5</span><span><span class="tt">Data Flow</span><span class="dd">How a button press travels Page → ViewModel → UseCase → Repository and back.</span></span></a>
        <a class="doc core" href="COMPOSITION_ROOT.html"><span class="n">6</span><span><span class="tt">Composition Root &amp; DI</span><span class="dd">How the app boots and how every dependency is wired: tokens and route-scoped providers.</span></span></a>
      </div>
    </div>

    <div class="stage">
      <div class="lab"><span class="pill">4</span> In Depth</div>
      <div class="grid">
        <a class="doc dom" href="ERROR_HANDLING.html"><span class="n">7</span><span><span class="tt">Error Handling &amp; Result</span><span class="dd">The Result&lt;T&gt; contract, AppError kinds, and the UseCase base's single try/catch.</span></span></a>
        <a class="doc data" href="VALIDATION.html"><span class="n">8</span><span><span class="tt">Validation &amp; Forms</span><span class="dd">DTO validation at the boundary and Angular reactive-forms validation.</span></span></a>
        <a class="doc pres" href="ADD_A_FEATURE.html"><span class="n">9</span><span><span class="tt">Add a Feature</span><span class="dd">A step-by-step cookbook for a new vertical slice, following fetch-posts.</span></span></a>
        <a class="doc core" href="TESTING.html"><span class="n">10</span><span><span class="tt">Testing</span><span class="dd">What each layer's spec covers, and the TestBed patterns for use cases and viewmodels.</span></span></a>
      </div>
    </div>

    <footer class="page">Base Frontend docs — <a href="index.html">Home</a> · <a href="ARCHITECTURE_OVERVIEW.html">Architecture</a> · <a href="PROJECT_LIBRARIES.html">Libraries</a> · <a href="CORE_FILES.html">Core Files</a> · <a href="FEATURE_ANATOMY.html">Feature Anatomy</a> · <a href="DATA_FLOW.html">Data Flow</a> · <a href="COMPOSITION_ROOT.html">Composition Root</a> · <a href="ERROR_HANDLING.html">Error Handling</a> · <a href="VALIDATION.html">Validation</a> · <a href="ADD_A_FEATURE.html">Add a Feature</a> · <a href="TESTING.html">Testing</a></footer>
  </div>
</body>
</html>
```

- [ ] **Step 2: Verify structure & self-containment**

Run:
```bash
test -f docs/index.html && echo EXISTS
grep -c 'class="topnav"' docs/index.html            # expect: 1
grep -c 'class="doc' docs/index.html                # expect: 10 (one per page)
grep -nE '<link rel="stylesheet"|<script ' docs/index.html || echo SELF-CONTAINED
grep -oE 'class="nl active"' docs/index.html         # expect: 1 (Home)
```
Expected: `EXISTS`, `1`, `10`, `SELF-CONTAINED`, one `class="nl active"`.

(The 6 links to not-yet-created pages — `COMPOSITION_ROOT`, `ERROR_HANDLING`, `VALIDATION`, `ADD_A_FEATURE`, `TESTING` — will resolve as Tasks 3–7 land. That's expected at this point.)

- [ ] **Step 3: Open in a browser and eyeball**

Open `docs/index.html`. Confirm: sticky blue nav, hero with "Start here" button, four numbered stages, 10 cards with colored left borders. No missing styles.

- [ ] **Step 4: Commit**

```bash
git add docs/index.html
git commit -m "docs: add documentation hub (index.html) with learning path

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: Unify navigation on the 5 existing pages

Replace the in-`.wrap` pill-nav (`<nav class="nav">`) on each existing page with the sticky top-nav, so all pages share one chrome and link to the hub + new pages.

**Files (modify):**
- `docs/ARCHITECTURE_OVERVIEW.html` (active link: Architecture)
- `docs/PROJECT_LIBRARIES.html` (active link: Libraries)
- `docs/CORE_FILES.html` (active link: Core)
- `docs/FEATURE_ANATOMY.html` (active link: Feature)
- `docs/DATA_FLOW.html` (active link: Data Flow)

**Interfaces:**
- Consumes: the canonical `.topnav` CSS + HTML from Global Constraints.

For **each** of the 5 files, do Steps 1–3 (the edits are identical except which `.nl` is `active`):

- [ ] **Step 1: Add the `.topnav` CSS**

Read the file. In its `<style>` block, immediately after the `.wrap { … }` rule, paste the canonical `.topnav` CSS (Global Constraints). The pages already define `--accent` and `body { padding:0 16px 72px }`, so the `margin:0 -16px` full-bleed trick works as-is.

- [ ] **Step 2: Insert the sticky top-nav and remove the old pill-nav**

Insert the canonical `.topnav` HTML block (Global Constraints) as the **first child of `<body>`**, immediately before `<div class="wrap">`. Set `active` on the `.nl` matching this page (e.g. for `ARCHITECTURE_OVERVIEW.html`, `<a class="nl active" href="ARCHITECTURE_OVERVIEW.html">Architecture</a>`).

Then delete the old in-`.wrap` nav block. For `ARCHITECTURE_OVERVIEW.html` it is exactly:
```html
    <nav class="nav">
      <a class="active" href="ARCHITECTURE_OVERVIEW.html">Architecture Overview</a>
      <a href="CORE_FILES.html">Core Files</a>
      <a href="FEATURE_ANATOMY.html">Feature Anatomy</a>
      <a href="DATA_FLOW.html">Data Flow</a>
      <a href="PROJECT_LIBRARIES.html">Project Libraries</a>
    </nav>
```
The other four pages have the same block with a different `active` link; locate each page's `<nav class="nav"> … </nav>` and remove it. (Leaving the now-unused `.nav` CSS rules in `<style>` is harmless, but you may delete them for tidiness.)

- [ ] **Step 3: Verify this page**

Run (substitute the filename):
```bash
F="docs/ARCHITECTURE_OVERVIEW.html"
grep -c 'class="topnav"' "$F"                        # expect: 1
grep -c 'class="nav"' "$F"                           # expect: 0 (old pill-nav gone)
grep -oE 'class="nl active"' "$F"                     # expect: 1
grep -nE '<link rel="stylesheet"|<script ' "$F" || echo SELF-CONTAINED
```
Expected: `1`, `0`, one active link, `SELF-CONTAINED`.

- [ ] **Step 4: After all 5 pages are done, open each in a browser**

Confirm the sticky nav appears at the top, the correct link is highlighted, and the page body is unchanged below it.

- [ ] **Step 5: Commit**

```bash
git add docs/ARCHITECTURE_OVERVIEW.html docs/PROJECT_LIBRARIES.html docs/CORE_FILES.html docs/FEATURE_ANATOMY.html docs/DATA_FLOW.html
git commit -m "docs: unify navigation with sticky top-nav across existing pages

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: `COMPOSITION_ROOT.html` — Composition Root & DI

How the app boots and how every dependency is wired. Mirrors the backend's HOST_API/composition-root page.

**Files:**
- Create: `docs/COMPOSITION_ROOT.html`

**Source of truth (read before authoring):** `src/main.ts`, `src/app/app.ts`, `src/app/app.config.ts`, `src/app/app.routes.ts`, `src/app/features/api/fetch-posts/domain/repositories/post.repository.port.ts`.

- [ ] **Step 1: Scaffold the page**

Copy the full `<style>` block from `docs/ARCHITECTURE_OVERVIEW.html`, add the canonical `.topnav` CSS, set `<title>Composition Root & DI — Base Frontend</title>`, add the canonical `.topnav` HTML (active = Composition), then a `header.page` (`<h1><span class="mono">Base Frontend</span> — Composition Root &amp; DI</h1>` + one-line lead) and a `footer.page`. Body sits inside `<div class="wrap">` after the nav.

- [ ] **Step 2: Section — "How the app boots" (`ol.steps`)**

Four numbered steps, each with a file path and the real snippet:

1. `src/main.ts` — bootstrap:
```ts
bootstrapApplication(App, appConfig)
  .catch((err) => console.error(err));
```
2. `src/app/app.config.ts` — the composition root (providers):
```ts
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(),
    provideAnimations(),
  ],
};
```
3. `src/app/app.routes.ts` — the route table (lazy `loadComponent`, route-scoped providers). Show the `api` route entry (snippet in Step 4).
4. `src/app/app.ts` — the root shell:
```ts
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, NotificationHostComponent],
  template: `
    <router-outlet />
    <app-notification-host />
  `,
})
export class App {}
```

- [ ] **Step 3: Section — "Ports are DI tokens" (`.card core`)**

Explain: TypeScript interfaces are erased at runtime, so a port can't be injected by its interface. Each `I…Repository` is paired with an `InjectionToken`. Real snippet from `post.repository.port.ts`:
```ts
export interface IPostRepository {
  getPosts(): Promise<PostsDtoRs>;
}

// DI token for the port (interfaces don't exist at runtime).
export const POST_REPOSITORY = new InjectionToken<IPostRepository>('POST_REPOSITORY');
```

- [ ] **Step 4: Section — "Route-scoped providers" (`.card core` + code)**

Show the real `api` route and explain that binding the impl at route level keeps the feature self-contained and makes swapping a fake in tests trivial (just provide a different value for the token):
```ts
{
  path: 'api',
  loadComponent: () => import('@features/api/fetch-posts').then((m) => m.ApiPage),
  providers: [
    { provide: POST_REPOSITORY, useClass: PostRepositoryImpl },
    GetPostsUseCase,
    FetchPostsViewModel,
  ],
}
```

- [ ] **Step 5: Section — mobile ↔ Angular mapping (`table.conv`)**

| React Native (mobile) | Angular (this base) |
| --- | --- |
| `index.js` | `src/main.ts` (`bootstrapApplication`) |
| `App.tsx` | `src/app/app.ts` (class `App`, `<router-outlet>`) |
| `providers/RootProviders.tsx` | `src/app/app.config.ts` (`appConfig`) |
| `RootNavigator.tsx` | `src/app/app.routes.ts` |

Add a "Read next" line linking `ERROR_HANDLING.html` and `DATA_FLOW.html`.

- [ ] **Step 6: Verify**

Run the reusable verification snippet with `<PAGE>=COMPOSITION_ROOT`. Then spot-check accuracy:
```bash
grep -q 'POST_REPOSITORY' docs/COMPOSITION_ROOT.html && grep -rq 'POST_REPOSITORY' src/app && echo "snippet ok"
grep -q 'provideZoneChangeDetection' docs/COMPOSITION_ROOT.html && grep -q 'provideZoneChangeDetection' src/app/app.config.ts && echo "config ok"
```
Expected: `EXISTS`, topnav `1`, one active link, `SELF-CONTAINED`, all links ok, `snippet ok`, `config ok`. Open in a browser to confirm styling.

- [ ] **Step 7: Commit**

```bash
git add docs/COMPOSITION_ROOT.html
git commit -m "docs: add Composition Root & DI page

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: `ERROR_HANDLING.html` — Error Handling & Result

The single error contract end to end. Mirrors the error spine of the backend's VALIDATION page.

**Files:**
- Create: `docs/ERROR_HANDLING.html`

**Source of truth:** `src/app/core/domain/result/result.ts`, `src/app/core/domain/errors/app-error.ts`, `src/app/core/domain/usecase/use-case.ts`, `src/app/core/network/api/http-client.ts`, `src/app/features/api/fetch-posts/presentation/viewmodels/fetch-posts.viewmodel.ts`.

- [ ] **Step 1: Scaffold the page**

Same scaffold as Task 3, Step 1, but `<title>Error Handling & Result — Base Frontend</title>`, active nav = Errors, `<h1>… — Error Handling &amp; Result</h1>`.

- [ ] **Step 2: Section — `Result<T>` (`.card dom`)**

```ts
export type Result<T> =
  | { ok: true; data: T }
  | { ok: false; error: AppError };

export const ok = <T>(data: T): Result<T> => ({ ok: true, data });
export const fail = (error: AppError): Result<never> => ({ ok: false, error });
```
Explain: callers switch on `.ok` (which narrows the type) instead of writing their own try/catch.

- [ ] **Step 3: Section — `AppError` and its kinds (`.card data` + `table.conv`)**

```ts
export type AppErrorKind =
  | 'network' | 'http' | 'validation' | 'storage' | 'crypto' | 'unknown';

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
Then a "where each kind originates" table (verified against the source):

| Kind | Origin |
| --- | --- |
| `network` | `http-client.ts` — transport failure (status 0) or timeout |
| `http` | `http-client.ts` — non-2xx response (`HTTP <status>`, carries `status`) |
| `validation` | a use case — bad DTO payload (e.g. `GetPostsUseCase`) |
| `storage` | `key-value.store.ts` — corrupt stored object |
| `crypto` | `crypto.service.ts` — decrypt/tamper failure |
| `unknown` | `UseCase.run()` — any non-`AppError` throw, normalized |

- [ ] **Step 4: Section — the `UseCase` base owns the single try/catch (`.card core`)**

```ts
export abstract class UseCase<I, O> {
  protected readonly log: Logger;
  constructor(name: string) { this.log = createLogger('usecase', name); }

  // happy path only — throw an AppError to signal failure
  protected abstract execute(input: I): Promise<O>;

  async run(input: I): Promise<Result<O>> {
    try {
      return ok(await this.execute(input));
    } catch (e) {
      const error =
        e instanceof AppError ? e : new AppError('Unexpected error', 'unknown');
      return fail(error);
    }
  }
}
```
Explain the template-method split: subclasses implement only `execute()` (happy path); `run()` normalizes every throw into a `Result` once, so no use case repeats the try/catch.

- [ ] **Step 5: Section — "The rule: no try/catch above `run()`" (`.card pres`)**

Show the viewmodel consuming the `Result` and resetting stale data on failure:
```ts
const result = await this.useCase.run();
this.loading.set(false);
if (result.ok) {
  this.posts.set(result.data);
} else {
  this.posts.set([]);            // clear stale data on failure
  this.error.set(result.error.message);
}
```
Add a "Read next" line to `VALIDATION.html` and `DATA_FLOW.html`.

- [ ] **Step 6: Verify**

Reusable snippet with `<PAGE>=ERROR_HANDLING`, then:
```bash
grep -q 'ok: false; error: AppError' docs/ERROR_HANDLING.html && echo "result ok"
for k in network http validation storage crypto unknown; do grep -q "'$k'" docs/ERROR_HANDLING.html && echo "kind $k ok"; done
```
Expected: all checks pass; open in a browser.

- [ ] **Step 7: Commit**

```bash
git add docs/ERROR_HANDLING.html
git commit -m "docs: add Error Handling & Result page

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: `VALIDATION.html` — Validation & Forms

The two validation sites: DTO validation at the use-case boundary (untrusted/remote data) and Angular reactive-forms validation (user input). Mirrors the backend VALIDATION + VALIDATION_GUIDE pages.

**Files:**
- Create: `docs/VALIDATION.html`

**Source of truth:** `src/app/features/api/fetch-posts/data/dto/post-dto-rs.ts`, `src/app/features/api/fetch-posts/domain/usecases/get-posts.use-case.ts`, `src/app/features/design/profile-form/presentation/pages/profile-form.page.ts`, `src/app/features/design/profile-form/presentation/profile-form.store.ts`.

- [ ] **Step 1: Scaffold the page**

Same scaffold; `<title>Validation & Forms — Base Frontend</title>`, active nav = Validation, `<h1>… — Validation &amp; Forms</h1>`. Add a `.legend` with two tags: `data` (boundary/DTO) and `presentation` (forms).

- [ ] **Step 2: Section — Boundary (DTO) validation for untrusted data (`.card data`)**

The type guard:
```ts
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
And its use at the use-case boundary, throwing a `validation` `AppError`:
```ts
protected async execute(): Promise<Post[]> {
  const res = await this.repo.getPosts();
  if (!res.data.every(isPostDtoRsValid)) {
    throw new AppError('Invalid data received', 'validation');
  }
  return res.data.map(toPost);
}
```
Explain: HTTP data is untrusted, so it is validated at the boundary before mapping to a domain model. Tie back to the "remote/untrusted" repository-port pattern.

- [ ] **Step 3: Section — Reactive-forms validation for user input (`.card pres`)**

```ts
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
```
Note the submit button is gated on validity in the template: `[disabled]="form.invalid"`. Explain this is trusted/local input — no DTO layer needed; the validated value flows to `ProfileFormStore` and on to the summary page.

- [ ] **Step 4: Section — "Where to validate what" (`table.conv`)**

| Source of data | Where it's validated | Mechanism |
| --- | --- | --- |
| Remote API (untrusted) | use-case boundary | `is…DtoRsValid` guard → `AppError('…','validation')` |
| User input (forms) | the component | Angular `Validators` on a `FormGroup`, submit gated on `form.invalid` |
| Local store (trusted, app-written) | not re-validated | round-tripped via `KeyValueStore` |

Add a "Read next" line to `ERROR_HANDLING.html` and `FEATURE_ANATOMY.html`.

- [ ] **Step 5: Verify**

Reusable snippet with `<PAGE>=VALIDATION`, then:
```bash
grep -q 'isPostDtoRsValid' docs/VALIDATION.html && grep -rq 'isPostDtoRsValid' src/app && echo "dto ok"
grep -q 'Validators.required' docs/VALIDATION.html && grep -rq 'Validators.required' src/app && echo "forms ok"
```
Expected: all pass; open in a browser.

- [ ] **Step 6: Commit**

```bash
git add docs/VALIDATION.html
git commit -m "docs: add Validation & Forms page

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 6: `ADD_A_FEATURE.html` — Add a Feature (cookbook)

The step-by-step recipe for a new vertical slice, following `api/fetch-posts`. Ports `docs/architecture/README.md` §6 into a polished `ol.steps` page.

**Files:**
- Create: `docs/ADD_A_FEATURE.html`

**Source of truth:** `docs/architecture/README.md` §6 (the 11-step recipe) and the real `api/fetch-posts` slice files: `data/dto/post-dto-rs.ts`, `domain/model/post.ts`, `domain/repositories/post.repository.port.ts`, `data/repositories/post.repository.ts`, `domain/usecases/get-posts.use-case.ts`, `presentation/viewmodels/fetch-posts.viewmodel.ts`, `presentation/pages/api.page.ts`, `index.ts`, `src/app/app.routes.ts`.

- [ ] **Step 1: Scaffold the page**

Same scaffold; `<title>Add a Feature — Base Frontend</title>`, active nav = Add a Feature, `<h1>… — Add a Feature</h1>`, intro lead: "Add a vertical slice by following the `api/fetch-posts` example. Substitute your entity name wherever `Post`/`post` appears."

- [ ] **Step 2: Section — folder scaffold (`pre.tree`)**

```
src/app/features/<group>/<feature>/
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

- [ ] **Step 3: Section — the 11-step recipe (`ol.steps`)**

One numbered step per item; each step names the file and shows the snippet. Use the **real** fetch-posts code as the worked example (not generic placeholders):

1. **DTO + validator** — `data/dto/post-dto-rs.ts`: the `PostDtoRs` type, `PostsDtoRs extends BaseResponseRs<PostDtoRs[]>`, and `isPostDtoRsValid` (full function from Task 5, Step 2).
2. **Domain model + mapper** — `domain/model/post.ts`:
```ts
export type Post = { id: number; title: string; body: string; userId: number; };
export const toPost = (dto: PostDtoRs): Post => ({
  id: dto.id, title: dto.title, body: dto.body, userId: dto.userId,
});
```
3. **Port + DI token** — `domain/repositories/post.repository.port.ts` (the `IPostRepository` + `POST_REPOSITORY` snippet from Task 3, Step 3).
4. **Repository impl** — `data/repositories/post.repository.ts`:
```ts
@Injectable()
export class PostRepositoryImpl implements IPostRepository {
  private readonly http = inject(HttpClientService);
  async getPosts(): Promise<PostsDtoRs> {
    const raw = await this.http.request<{ posts: PostDtoRs[] }>('/posts');
    return { data: raw.posts, success: true };
  }
}
```
5. **Use case** — `domain/usecases/get-posts.use-case.ts` (the full `GetPostsUseCase` from Task 4 context: extends `UseCase<void, Post[]>`, `inject(POST_REPOSITORY)`, validate-then-map in `execute()`).
6. **ViewModel** — `presentation/viewmodels/fetch-posts.viewmodel.ts` (signals `loading`/`posts`/`error`, `run()` switching on `result.ok`, clear-on-failure).
7. **Page** — `presentation/pages/api.page.ts` (inject the vm, render from signals, `effect()` → toast on error).
8. **Barrel** — `index.ts`:
```ts
export { ApiPage } from './presentation/pages/api.page';
export { FetchPostsViewModel } from './presentation/viewmodels/fetch-posts.viewmodel';
export { GetPostsUseCase } from './domain/usecases/get-posts.use-case';
export { PostRepositoryImpl } from './data/repositories/post.repository';
export { POST_REPOSITORY } from './domain/repositories/post.repository.port';
export type { Post } from './domain/model/post';
export type { PostDtoRs, PostsDtoRs } from './data/dto/post-dto-rs';
```
9. **Route wiring** — `src/app/app.routes.ts` (the `api` route entry from Task 3, Step 4).
10. **Tests** — point to Task 7 (Testing page) for the use-case and viewmodel test recipes.
11. **Add to nav/home** — register the route path and link it from the home page.

- [ ] **Step 4: Section — "Two repository-port patterns" (`.card data` + `.card dom`)**

- Remote/untrusted (fetch-posts): port returns the **DTO** envelope; the use case validates + maps.
- Local/trusted (offline-storage): port returns the **domain model** directly; the repo serialises via `KeyValueStore`, no DTO layer.

Add a callout (`.banner`) on the **clear-stale-data-on-failure** rule. Add a "Read next" line to `TESTING.html`.

- [ ] **Step 5: Verify**

Reusable snippet with `<PAGE>=ADD_A_FEATURE`, then:
```bash
grep -c 'steps' docs/ADD_A_FEATURE.html              # expect: >=1 (ol.steps present)
grep -q 'PostRepositoryImpl' docs/ADD_A_FEATURE.html && grep -rq 'PostRepositoryImpl' src/app && echo "impl ok"
grep -q 'toPost' docs/ADD_A_FEATURE.html && grep -rq 'toPost' src/app && echo "mapper ok"
```
Expected: all pass; open in a browser and confirm the numbered steps render.

- [ ] **Step 6: Commit**

```bash
git add docs/ADD_A_FEATURE.html
git commit -m "docs: add Add-a-Feature cookbook page

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 7: `TESTING.html` — Testing

How testing is set up, what each layer's spec covers, and the TestBed recipes. Ports `docs/architecture/README.md` §7.

**Files:**
- Create: `docs/TESTING.html`

**Source of truth:** `jest.config.js`, `setup-jest.ts`, `docs/architecture/README.md` §7, `src/app/features/api/fetch-posts/domain/usecases/get-posts.use-case.spec.ts`, `src/app/features/api/fetch-posts/presentation/viewmodels/fetch-posts.viewmodel.spec.ts`.

- [ ] **Step 1: Scaffold the page**

Same scaffold; `<title>Testing — Base Frontend</title>`, active nav = Testing, `<h1>… — Testing</h1>`.

- [ ] **Step 2: Section — Setup (`.card core`)**

Explain `jest-preset-angular`, the config, and `npm test`. Real snippets:
```js
// jest.config.js
const { createCjsPreset } = require('jest-preset-angular/presets');
module.exports = {
  ...createCjsPreset(),
  setupFilesAfterEnv: ['<rootDir>/setup-jest.ts'],
  moduleNameMapper: {
    '^@core/(.*)$': '<rootDir>/src/app/core/$1',
    '^@features/(.*)$': '<rootDir>/src/app/features/$1',
  },
};
```
```ts
// setup-jest.ts
import { setupZoneTestEnv } from 'jest-preset-angular/setup-env/zone';
setupZoneTestEnv();
```
Run: `npm test`. Note path aliases mirror `tsconfig.json`.

- [ ] **Step 3: Section — Coverage table (`table.conv`)**

Port the table from `docs/architecture/README.md` §7 (one row per spec: `result.spec.ts`, `use-case.spec.ts`, `logger.spec.ts`, `http-client.spec.ts`, `key-value.store.spec.ts`, `crypto.service.spec.ts`, `get-posts.use-case.spec.ts`, `fetch-posts.viewmodel.spec.ts`, `save-profile.use-case.spec.ts`, `save-profile.viewmodel.spec.ts`, `retrieve-profile.viewmodel.spec.ts`, `profile-form.page.spec.ts`, `nav-button.component.spec.ts`, `notification.service.spec.ts`, `sidebar-nav.component.spec.ts`, `home.page.spec.ts`) with its "what the tests verify" description.

- [ ] **Step 4: Section — Use-case test recipe (`.card dom`)**

Bind a fake repo to the port token and assert on the `Result` (real snippet):
```ts
function repoReturning(res: PostsDtoRs): IPostRepository {
  return { getPosts: async () => res };
}

TestBed.configureTestingModule({
  providers: [GetPostsUseCase, { provide: POST_REPOSITORY, useValue: repo }],
});
const uc = TestBed.inject(GetPostsUseCase);
const r = await uc.run();
expect(r.ok).toBe(true);
```
Note the second case: a bad payload yields `r.ok === false` with `r.error.kind === 'validation'`.

- [ ] **Step 5: Section — ViewModel test recipe (`.card pres`)**

Stub the use case's `run` and assert on signal transitions (real snippet):
```ts
TestBed.configureTestingModule({
  providers: [
    FetchPostsViewModel,
    { provide: GetPostsUseCase, useValue: { run } },
  ],
});
const vm = TestBed.inject(FetchPostsViewModel);
await vm.run();
expect(vm.loading()).toBe(false);
expect(vm.posts().length).toBe(1);
```
Note the clear-stale-data case: after a successful fetch, a failing re-fetch resets `posts` to length 0 and sets `error`. Close with a note that component tests are light smoke tests (matches the mobile base). Add a "Read next" line back to `index.html`.

- [ ] **Step 6: Verify**

Reusable snippet with `<PAGE>=TESTING`, then:
```bash
grep -q 'createCjsPreset' docs/TESTING.html && grep -q 'createCjsPreset' jest.config.js && echo "config ok"
grep -q 'POST_REPOSITORY' docs/TESTING.html && echo "usecase recipe ok"
grep -q "kind === 'validation'\|'validation'" docs/TESTING.html && echo "validation case ok"
```
Expected: all pass; open in a browser.

- [ ] **Step 7: Commit**

```bash
git add docs/TESTING.html
git commit -m "docs: add Testing page

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 8: Wire the docs into the READMEs + whole-site verification

Point the entry READMEs at the new hub, extend the markdown guide's HTML list, and verify the whole site cross-links cleanly.

**Files (modify):**
- `README.md`
- `docs/architecture/README.md`

- [ ] **Step 1: Update `README.md` "Architecture" section**

Replace lines 18–19 (the current "Visual architecture docs" sentence pointing at `ARCHITECTURE_OVERVIEW.html`) with:
```markdown
Visual architecture docs (open in a browser): [`docs/index.html`](docs/index.html)
— the documentation hub. It opens a numbered learning path: Architecture Overview → Core
Files → Feature Anatomy → Data Flow → Composition Root & DI → Error Handling → Validation
→ Add a Feature → Testing.
```

- [ ] **Step 2: Update `docs/architecture/README.md` HTML list**

Replace the bullet list under "## Visual architecture docs (HTML)" with the full set, hub first:
```markdown
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
```

- [ ] **Step 3: Whole-site verification**

Run from repo root:
```bash
# 1. all 11 files exist
for f in index ARCHITECTURE_OVERVIEW PROJECT_LIBRARIES CORE_FILES FEATURE_ANATOMY DATA_FLOW COMPOSITION_ROOT ERROR_HANDLING VALIDATION ADD_A_FEATURE TESTING; do
  test -f "docs/$f.html" && echo "ok: $f" || echo "MISSING: $f"
done
# 2. every internal href across all pages resolves
for f in docs/*.html; do
  for h in $(grep -oE 'href="[A-Za-z_]+\.html"' "$f" | sed -E 's/href="(.*)"/\1/' | sort -u); do
    test -f "docs/$h" || echo "BROKEN: $f -> $h"
  done
done
echo "link check done"
# 3. no external css/js anywhere
grep -lnE '<link rel="stylesheet"|<script ' docs/*.html || echo "ALL SELF-CONTAINED"
# 4. every page carries the sticky nav
for f in docs/*.html; do c=$(grep -c 'class="topnav"' "$f"); echo "$f topnav=$c"; done
```
Expected: 11 `ok:` lines, no `BROKEN:` lines, `link check done`, `ALL SELF-CONTAINED`, every page `topnav=1`.

- [ ] **Step 4: Manual browser pass**

Open `docs/index.html`, click through all 10 cards and the top-nav links on a couple of pages. Confirm navigation works, the active link highlights correctly, and no page has missing styles or a broken layout.

- [ ] **Step 5: Commit**

```bash
git add README.md docs/architecture/README.md
git commit -m "docs: link the documentation hub from the READMEs

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Self-Review (completed by plan author)

**1. Spec coverage** — every spec section maps to a task:
- Hub (`index.html`) → Task 1. ✓
- Unify nav / sticky top-nav on existing 5 → Task 2. ✓
- 5 new pages (Composition Root, Error Handling, Validation, Add a Feature, Testing) → Tasks 3–7. ✓
- Cross-linking + README integration → Task 8 (+ per-page "Read next"). ✓
- Accuracy constraints → "Source of truth" + snippet spot-checks in every page task. ✓
- Self-contained / design-system reuse → Global Constraints + per-task verify. ✓
- Learning-path stages & color coding → Task 1 hub content. ✓

**2. Placeholder scan** — no TBD/TODO; every code step shows real, source-derived code. The only intentional `<…>` are the generic-feature placeholders in the Add-a-Feature scaffold (`<group>/<feature>`), which are correct as written (they're template literals the reader substitutes).

**3. Type/name consistency** — identifiers verified against source and used consistently across tasks: `Result<T>`, `ok`/`fail`, `AppError`/`AppErrorKind`, `UseCase<I,O>`/`run`/`execute`, `IPostRepository`/`POST_REPOSITORY`, `PostRepositoryImpl`, `GetPostsUseCase`, `FetchPostsViewModel`, `isPostDtoRsValid`, `toPost`, `PostsDtoRs`/`BaseResponseRs`, `ProfileFormStore`/`Validators.required`. Top-nav link list and filenames identical in Global Constraints, the hub (Task 1), and the cross-link check (Task 8).
