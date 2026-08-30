# Angular Base — HTML Architecture Docs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add 5 standalone, hand-authored HTML architecture-presentation pages to the Angular `Base Frontend`'s `docs/`, matching the design system and role of the sibling mobile and backend bases' HTML docs.

**Architecture:** Each page is a self-contained `.html` file with an inline `<style>` block (no external CSS/JS/fonts) reusing one shared design system. `ARCHITECTURE_OVERVIEW.html` is the entry point and cross-links the other four. All textual/code content is derived from the real implemented code in `src/app` and the actual `package.json` — nothing invented.

**Tech Stack:** Plain HTML + inline CSS + inline SVG. No build step, no framework, no dependencies. Documentation only — zero changes under `src/`.

## Global Constraints

- **Documentation only:** never modify anything under `src/`. Only create files in `docs/` and update `README.md` + `docs/architecture/README.md`.
- **Self-contained:** each HTML page must open directly from the filesystem with no external resource loads — NO `<link rel="stylesheet">`, NO `<script src=...>`, NO `@import`, NO `url(http...)`, no web fonts/CDNs. (Plain-text URLs and `<a href>` links in page *content*, e.g. a library homepage, are fine — they just must not be loaded as resources.)
- **Shared design system (verbatim across all 5):** reuse the canonical `<style>` block defined in Task 1 in every page, unchanged, so the pages are visually identical siblings of the mobile/backend docs.
- **Frontend layer taxonomy (the only canonical legend):** `presentation` `#5b21b6`/`#ede9fe`; `domain` `#166534`/`#dcfce7`; `data` `#9a3412`/`#ffedd5`; `core` `#2563eb`/`#eaf1ff`.
- **Accuracy:** every file path, class/type name, method signature, and code snippet MUST match the real file in `src/app`. Read the named source files; reproduce real content. No aspirational/invented APIs. Library versions in `PROJECT_LIBRARIES.html` MUST match `package.json` exactly.
- **HTML escaping (critical):** inside `<pre>`/`<code>`, escape `<`→`&lt;`, `>`→`&gt;`, `&`→`&amp;`. This matters constantly here (`Result<T>`, `Promise<T>`, `signal<Post[]>`, `<router-outlet />`, `@if`). A raw `<` will break rendering.
- **Token highlighting is optional polish:** correct escaping is mandatory; hand-applied `.tok-*` syntax spans are encouraged on hero snippets but plain escaped `<pre>` is acceptable. Do not block on missing highlighting.
- **File names (flat in `docs/`, uppercase):** `ARCHITECTURE_OVERVIEW.html`, `CORE_FILES.html`, `FEATURE_ANATOMY.html`, `DATA_FLOW.html`, `PROJECT_LIBRARIES.html`.
- **Cross-link nav:** every page includes the same nav block linking all 5 pages (relative hrefs, e.g. `href="ARCHITECTURE_OVERVIEW.html"`); the current page's link is marked active.
- **Commits:** one commit per task. End every commit message with:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`
- **Style reference (read for visual parity, do not copy backend layer names):** `C:\Users\envnt\Desktop\Base Backend\docs\ARCHITECTURE_OVERVIEW.html` and `C:\Users\envnt\Desktop\BaseProject\docs\CORE_FILES.html`.

---

## File Structure

```
docs/
  ARCHITECTURE_OVERVIEW.html   # Task 1 — entry point; defines the canonical <style> + nav
  CORE_FILES.html              # Task 2 — core/ building blocks
  FEATURE_ANATOMY.html         # Task 3 — a feature vertical slice (fetch-posts)
  DATA_FLOW.html               # Task 4 — runtime flow + error handling
  PROJECT_LIBRARIES.html       # Task 5 — dependencies + rationale
  architecture/README.md       # Task 6 — modify: link the HTML set
README.md                      # Task 6 — modify: link the HTML set
```

Source files the pages document (read these for accurate content):

```
src/app/app.ts, app.config.ts, app.routes.ts, main.ts
src/app/core/domain/{result/result.ts, errors/app-error.ts, usecase/use-case.ts}
src/app/core/{logging/logger.ts, config/env.ts}
src/app/core/network/api/{base-response-rs.ts, http-client.ts}
src/app/core/datasource/keyvalue/{key-value.store.ts, storage-keys.ts}
src/app/core/crypto/{crypto.service.ts, types.ts, web-crypto-key-store.ts, index.ts}
src/app/core/ui/{notification.service.ts, components/*.ts, theme/theme.ts}
src/app/features/api/fetch-posts/**            # the worked-example slice
src/app/features/offline-storage/**            # the second port-pattern (domain-returning)
package.json                                   # PROJECT_LIBRARIES versions
```

---

## Task 1: `ARCHITECTURE_OVERVIEW.html` (entry point + canonical style)

**Files:**
- Create: `docs/ARCHITECTURE_OVERVIEW.html`

**Interfaces:**
- Produces: the canonical `<style>` block and the `.nav` cross-link block reused verbatim by Tasks 2–5. Establishes the 4-layer legend.

- [ ] **Step 1: Create the file with the canonical `<head>` + `<style>`**

Create `docs/ARCHITECTURE_OVERVIEW.html` starting with this exact head/style (this block is the shared design system; Tasks 2–5 copy it verbatim):

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Architecture Overview — Base Frontend</title>
  <style>
    :root {
      --bg:#f6f8fa; --card:#fff; --border:#e1e4e8; --text:#1f2328; --muted:#57606a;
      --accent:#2563eb; --accent-soft:#eaf1ff; --code-bg:#0f172a; --code-text:#e2e8f0;
      --pres:#5b21b6; --pres-bg:#ede9fe;   /* presentation */
      --dom:#166534;  --dom-bg:#dcfce7;    /* domain */
      --data:#9a3412; --data-bg:#ffedd5;   /* data */
      --core:#2563eb; --core-bg:#eaf1ff;   /* core */
    }
    * { box-sizing:border-box; }
    body { margin:0; padding:0 16px 72px; background:var(--bg); color:var(--text);
      font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif; line-height:1.6; }
    .wrap { max-width:980px; margin:0 auto; }
    header.page { padding:40px 0 22px; border-bottom:2px solid var(--border); margin-bottom:8px; }
    header.page h1 { margin:0 0 6px; font-size:1.95rem; }
    header.page h1 .mono { font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace; color:var(--accent); }
    header.page p { margin:0; color:var(--muted); font-size:0.98rem; }
    .nav { display:flex; flex-wrap:wrap; gap:8px; margin:18px 0 6px; }
    .nav a { font-size:0.82rem; text-decoration:none; color:var(--accent); border:1px solid var(--border);
      background:var(--card); border-radius:999px; padding:4px 12px; }
    .nav a.active { background:var(--accent); color:#fff; border-color:var(--accent); }
    .banner { background:var(--accent-soft); border:1px solid #c7dbff; border-radius:12px; padding:14px 18px; margin:22px 0 6px; font-size:0.95rem; }
    .banner strong { color:var(--accent); }
    .legend { display:flex; flex-wrap:wrap; gap:10px; margin:18px 0 6px; font-size:0.8rem; align-items:center; }
    .legend .lab { color:var(--muted); font-weight:600; }
    .tag { display:inline-block; padding:2px 9px; border-radius:999px; font-weight:600; font-size:0.74rem; letter-spacing:.2px; }
    .tag.pres { color:var(--pres); background:var(--pres-bg); }
    .tag.dom  { color:var(--dom);  background:var(--dom-bg); }
    .tag.data { color:var(--data); background:var(--data-bg); }
    .tag.core { color:var(--core); background:var(--core-bg); }
    h2.section { font-size:1.22rem; margin:42px 0 14px; padding-bottom:6px; border-bottom:1px solid var(--border); }
    p.lead { color:var(--muted); margin:0 0 18px; }
    .flow { display:flex; flex-direction:column; gap:0; margin:8px 0 6px; }
    .node { border:1px solid var(--border); border-left-width:5px; background:var(--card); border-radius:10px; padding:12px 16px; }
    .node .nh { font-weight:700; font-size:0.98rem; }
    .node .nf { font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace; font-size:0.78rem; color:var(--muted); }
    .node.pres { border-left-color:var(--pres); } .node.dom { border-left-color:var(--dom); }
    .node.data { border-left-color:var(--data); } .node.core { border-left-color:var(--core); }
    .arrow { align-self:center; color:var(--muted); font-size:0.78rem; padding:6px 0; text-align:center; }
    .arrow .down { font-size:1.1rem; line-height:1; }
    .arrow .what { display:inline-block; background:#fff; border:1px solid var(--border); border-radius:999px; padding:1px 10px; font-size:0.72rem; margin-left:6px; }
    .card { background:var(--card); border:1px solid var(--border); border-radius:12px; padding:16px 18px; margin:14px 0; }
    .card.pres { border-left:5px solid var(--pres); } .card.dom { border-left:5px solid var(--dom); }
    .card.data { border-left:5px solid var(--data); } .card.core { border-left:5px solid var(--core); }
    .card .ch { display:flex; align-items:center; gap:10px; flex-wrap:wrap; margin-bottom:4px; }
    .card .ch h3 { margin:0; font-size:1.02rem; }
    .card .file { font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace; font-size:0.76rem; color:var(--muted); }
    .card p { margin:8px 0; font-size:0.93rem; }
    pre { background:var(--code-bg); color:var(--code-text); border-radius:10px; padding:14px 16px; overflow-x:auto; font-size:0.82rem; line-height:1.5; margin:10px 0 2px; font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace; }
    code.inline { background:#eef1f4; color:#0f172a; border-radius:5px; padding:1px 6px; font-size:0.86em; font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace; }
    .tok-c { color:#94a3b8; } .tok-k { color:#93c5fd; } .tok-s { color:#86efac; } .tok-t { color:#fca5a5; }
    ol.steps { counter-reset:step; list-style:none; padding-left:0; }
    ol.steps > li { position:relative; padding:12px 14px 12px 52px; margin:10px 0; background:var(--card); border:1px solid var(--border); border-radius:10px; }
    ol.steps > li::before { counter-increment:step; content:counter(step); position:absolute; left:14px; top:12px; width:26px; height:26px; border-radius:50%; background:var(--accent); color:#fff; font-weight:700; display:flex; align-items:center; justify-content:center; font-size:0.85rem; }
    ol.steps > li b { display:block; margin-bottom:2px; }
    table.conv { width:100%; border-collapse:collapse; font-size:0.88rem; margin:6px 0; }
    table.conv th, table.conv td { border:1px solid var(--border); padding:8px 10px; text-align:left; vertical-align:top; }
    table.conv th { background:#f0f3f6; }
    pre.tree { background:var(--card); border:1px solid var(--border); border-radius:12px; padding:16px 18px; font-size:0.86rem; line-height:1.7; overflow-x:auto; font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace; color:var(--text); }
    pre.tree .g { font-weight:700; } pre.tree .g.pres { color:var(--pres); } pre.tree .g.dom { color:var(--dom); }
    pre.tree .g.data { color:var(--data); } pre.tree .g.core { color:var(--core); } pre.tree .note { color:var(--muted); }
    footer.page { margin-top:48px; padding-top:18px; border-top:1px solid var(--border); color:var(--muted); font-size:0.86rem; }
  </style>
</head>
<body>
  <div class="wrap">
```

- [ ] **Step 2: Add the header, nav, banner, and legend**

Append:

```html
    <header class="page">
      <h1><span class="mono">Base Frontend</span> — Architecture Overview</h1>
      <p>An Angular v20 base: Clean Architecture + MVVM, feature-first. Read this first, then Core Files, Feature Anatomy, Data Flow, and Project Libraries.</p>
    </header>

    <nav class="nav">
      <a class="active" href="ARCHITECTURE_OVERVIEW.html">Architecture Overview</a>
      <a href="CORE_FILES.html">Core Files</a>
      <a href="FEATURE_ANATOMY.html">Feature Anatomy</a>
      <a href="DATA_FLOW.html">Data Flow</a>
      <a href="PROJECT_LIBRARIES.html">Project Libraries</a>
    </nav>

    <div class="banner"><strong>The web counterpart of the React Native mobile base.</strong>
      Same Clean Architecture + MVVM taxonomy, translated to standalone Angular (Signals at the edge,
      Promise + <code class="inline">Result&lt;T&gt;</code> through the domain).</div>

    <div class="legend"><span class="lab">Layers:</span>
      <span class="tag pres">presentation</span><span class="tag dom">domain</span>
      <span class="tag data">data</span><span class="tag core">core</span>
    </div>
```

- [ ] **Step 3: Add "The big picture" SVG**

Append (this SVG is self-contained; keep it verbatim):

```html
    <h2 class="section">The big picture</h2>
    <p class="lead">One-way dependency: presentation → domain → data → core. The domain layer is framework-light (Angular DI only); transport/storage/UI concerns live at the edges.</p>

    <svg viewBox="0 0 760 300" width="100%" role="img" aria-label="Base Frontend architecture" xmlns="http://www.w3.org/2000/svg">
      <style>
        .b{rx:10;ry:10;stroke:#e1e4e8;stroke-width:1.5;}
        .t{font:600 13px -apple-system,Segoe UI,Roboto,sans-serif;fill:#1f2328;}
        .s{font:11px ui-monospace,Consolas,monospace;fill:#57606a;}
        .ln{stroke:#94a3b8;stroke-width:1.5;marker-end:url(#a);}
      </style>
      <defs><marker id="a" markerWidth="9" markerHeight="9" refX="7" refY="3" orient="auto">
        <path d="M0,0 L7,3 L0,6 Z" fill="#94a3b8"/></marker></defs>
      <rect class="b" x="40"  y="120" width="160" height="120" fill="#ede9fe"/>
      <text class="t" x="58" y="142">Presentation</text>
      <text class="s" x="58" y="160">Page · ViewModel</text>
      <text class="s" x="58" y="176">(signals)</text>
      <rect class="b" x="230" y="120" width="160" height="120" fill="#dcfce7"/>
      <text class="t" x="248" y="142">Domain</text>
      <text class="s" x="248" y="160">UseCase · Model</text>
      <text class="s" x="248" y="176">Repository port</text>
      <rect class="b" x="420" y="120" width="160" height="120" fill="#ffedd5"/>
      <text class="t" x="438" y="142">Data</text>
      <text class="s" x="438" y="160">DTO · RepositoryImpl</text>
      <rect class="b" x="610" y="120" width="120" height="120" fill="#eaf1ff"/>
      <text class="t" x="628" y="142">Core</text>
      <text class="s" x="628" y="160">http · storage</text>
      <text class="s" x="628" y="176">crypto · log</text>
      <line class="ln" x1="200" y1="180" x2="228" y2="180"/>
      <line class="ln" x1="390" y1="180" x2="418" y2="180"/>
      <line class="ln" x1="580" y1="180" x2="608" y2="180"/>
      <text class="s" x="300" y="270">Result&lt;T&gt; / AppError flow back up ←</text>
    </svg>
```

- [ ] **Step 4: Add the dependency rule + Angular adaptations + folder map + mapping table**

Append (the folder tree and table reflect the real layout — keep accurate):

```html
    <h2 class="section">Two Angular adaptations</h2>
    <div class="card core">
      <p><b>Ports are DI tokens.</b> TypeScript interfaces vanish at runtime, so each
      <code class="inline">I…Repository</code> is paired with an <code class="inline">InjectionToken</code>,
      bound in the feature's route-level <code class="inline">providers</code>.</p>
      <p><b>Domain uses Angular DI, not HTTP/storage/UI.</b> Use cases (<code class="inline">@Injectable</code>/<code class="inline">inject</code>),
      ports (<code class="inline">InjectionToken</code>), and viewmodels import <code class="inline">@angular/core</code> —
      but never <code class="inline">HttpClient</code>, <code class="inline">localStorage</code>, or DOM APIs.</p>
    </div>

    <h2 class="section">Folder map</h2>
    <pre class="tree"><span class="g">src/app/</span>
  app.ts                 <span class="note"># root shell (class App): &lt;router-outlet&gt; + notification host</span>
  app.config.ts          <span class="note"># providers: router, http, animations</span>
  app.routes.ts          <span class="note"># route table + feature-scoped DI providers</span>
  <span class="g core">core/</span>                 <span class="note"># cross-cutting infra (see Core Files)</span>
    domain/ logging/ config/ network/ datasource/ crypto/ ui/
  <span class="g">features/</span>
    home/
    api/fetch-posts/      <span class="note"># see Feature Anatomy</span>
    offline-storage/
    design/profile-form/
    design/design-hub/
      <span class="g pres">presentation/</span>  <span class="g dom">domain/</span>  <span class="g data">data/</span>  <span class="note"># every feature is a vertical slice</span></pre>

    <h2 class="section">Composition root: mobile ↔ Angular</h2>
    <table class="conv">
      <tr><th>React Native (mobile)</th><th>Angular (this base)</th></tr>
      <tr><td><code>index.js</code></td><td><code>src/main.ts</code> (<code>bootstrapApplication</code>)</td></tr>
      <tr><td><code>App.tsx</code></td><td><code>src/app/app.ts</code> (class <code>App</code>, <code>&lt;router-outlet&gt;</code>)</td></tr>
      <tr><td><code>providers/RootProviders.tsx</code></td><td><code>app.config.ts</code></td></tr>
      <tr><td><code>RootNavigator.tsx</code></td><td><code>app.routes.ts</code></td></tr>
    </table>
```

- [ ] **Step 5: Close the document with a footer**

Append:

```html
    <footer class="page">
      Base Frontend — Angular v20 base architecture. See the
      <a href="superpowers/specs/2026-06-26-angular-base-design.md">design spec</a>
      and the other pages above.
    </footer>
  </div>
</body>
</html>
```
(The href is relative to this file at `docs/ARCHITECTURE_OVERVIEW.html`, so `superpowers/specs/2026-06-26-angular-base-design.md` resolves to `docs/superpowers/specs/...`. Tasks 2–5 use the same `footer.page` + closing tags, adjusting only the footer's lead-in text.)

- [ ] **Step 6: Verify self-contained + valid + accurate**

Run (expect NO output — no external resource loads):
```bash
cd "C:/Users/envnt/Desktop/Base Frontend"
grep -nE '<link |<script|@import|url\(http|https?://(fonts|cdn|unpkg|cdnjs|ajax)' docs/ARCHITECTURE_OVERVIEW.html || echo "OK: self-contained"
```
Run (expect each to print a match — structure present):
```bash
grep -c "</html>" docs/ARCHITECTURE_OVERVIEW.html
grep -c "class=\"nav\"" docs/ARCHITECTURE_OVERVIEW.html
grep -c "tag pres" docs/ARCHITECTURE_OVERVIEW.html
```
Read the file back and confirm: the head/style is the canonical block, the 4-layer legend renders, the SVG is present, the folder tree and mapping table are accurate to the real `src/app` layout. Open it in a browser if possible and confirm no console errors and styled output.

- [ ] **Step 7: Commit**

```bash
cd "C:/Users/envnt/Desktop/Base Frontend"
git add docs/ARCHITECTURE_OVERVIEW.html
git commit -m "docs: add ARCHITECTURE_OVERVIEW.html (entry point + shared design system)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: `CORE_FILES.html`

**Files:**
- Create: `docs/CORE_FILES.html`

**Interfaces:**
- Consumes: the canonical `<style>` + `.nav` block from `docs/ARCHITECTURE_OVERVIEW.html` (copy verbatim).

- [ ] **Step 1: Read the source files this page documents**

Read each (reproduce real signatures/snippets, escaped):
```
src/app/core/domain/result/result.ts
src/app/core/domain/errors/app-error.ts
src/app/core/domain/usecase/use-case.ts
src/app/core/logging/logger.ts
src/app/core/config/env.ts
src/app/core/network/api/base-response-rs.ts
src/app/core/network/api/http-client.ts
src/app/core/datasource/keyvalue/key-value.store.ts
src/app/core/datasource/keyvalue/storage-keys.ts
src/app/core/crypto/crypto.service.ts
src/app/core/crypto/types.ts
src/app/core/crypto/web-crypto-key-store.ts
src/app/core/crypto/index.ts
src/app/core/ui/notification.service.ts
src/app/core/ui/components/nav-button.component.ts
src/app/core/ui/components/text-field.component.ts
src/app/core/ui/components/notification-host.component.ts
src/app/core/ui/theme/theme.ts
```

- [ ] **Step 2: Create the file: copy the canonical head/style/body-open + nav (active = Core Files)**

Start `docs/CORE_FILES.html` with the EXACT `<!DOCTYPE …>` … `<div class="wrap">` block from `ARCHITECTURE_OVERVIEW.html` (Task 1 Step 1), changing only the `<title>` to `Core Files — Base Frontend`. Then add the header + nav with `CORE_FILES.html` marked `active`:

```html
    <header class="page">
      <h1><span class="mono">core/</span> — shared building blocks</h1>
      <p>Cross-cutting infrastructure every feature reuses. All paths under <code class="inline">src/app/core/</code>.</p>
    </header>
    <nav class="nav">
      <a href="ARCHITECTURE_OVERVIEW.html">Architecture Overview</a>
      <a class="active" href="CORE_FILES.html">Core Files</a>
      <a href="FEATURE_ANATOMY.html">Feature Anatomy</a>
      <a href="DATA_FLOW.html">Data Flow</a>
      <a href="PROJECT_LIBRARIES.html">Project Libraries</a>
    </nav>
    <div class="legend"><span class="lab">Layers:</span>
      <span class="tag pres">presentation</span><span class="tag dom">domain</span>
      <span class="tag data">data</span><span class="tag core">core</span>
    </div>
```

- [ ] **Step 3: Add the `core/` folder tree**

Append a `pre.tree` reflecting the real tree (verify against the file list):

```html
    <h2 class="section">Layout</h2>
    <pre class="tree"><span class="g core">core/</span>
  domain/   <span class="note"># result.ts · errors/app-error.ts · usecase/use-case.ts</span>
  logging/  <span class="note"># logger.ts</span>
  config/   <span class="note"># env.ts</span>
  network/  <span class="note"># api/base-response-rs.ts · api/http-client.ts</span>
  datasource/ <span class="note"># keyvalue/key-value.store.ts · keyvalue/storage-keys.ts</span>
  crypto/   <span class="note"># crypto.service.ts · types.ts · web-crypto-key-store.ts · index.ts</span>
  ui/       <span class="note"># theme/ · components/ · notification.service.ts</span></pre>
```

- [ ] **Step 4: Add one `.card core` per building block**

For each unit, add a card: a heading, the file path in `.file`, a one/two-sentence purpose, and a short ESCAPED real snippet in `<pre>`. Cover all of: `result.ts` (`Result<T>`/`ok`/`fail`), `app-error.ts` (`AppError` + the 6-kind union `'network'|'http'|'validation'|'storage'|'crypto'|'unknown'`), `use-case.ts` (the `execute()`/`run()` split), `logger.ts`, `env.ts`, `base-response-rs.ts`, `http-client.ts` (retry/timeout → Promise, AppError mapping), `key-value.store.ts` (localStorage, corrupt-object-throws/primitive-falls-back), `storage-keys.ts`, `crypto.service.ts` (AES-GCM, DI seams), `types.ts` (`WebCryptoLike`/`KeyProvider`), `web-crypto-key-store.ts` (non-extractable key in IndexedDB), `index.ts` (composition), `notification.service.ts`, `nav-button.component.ts`, `text-field.component.ts`, `notification-host.component.ts`, `theme.ts`. Card template:

```html
    <h2 class="section">Domain primitives</h2>
    <div class="card core">
      <div class="ch"><h3>Result&lt;T&gt;</h3><span class="file">core/domain/result/result.ts</span><span class="tag core">core</span></div>
      <p>The uniform success/failure value every use case returns. Callers switch on <code class="inline">ok</code> instead of writing try/catch.</p>
      <pre>export type Result&lt;T&gt; =
  | { ok: true; data: T }
  | { ok: false; error: AppError };</pre>
    </div>
```
Group the cards under `h2.section` headers (Domain primitives / Logging & config / Network / Storage / Crypto / UI). Reproduce snippets from the files you read in Step 1 — do not paraphrase signatures.

- [ ] **Step 5: Footer + verify + commit**

Append the same `footer.page` + closing tags as Task 1 Step 5 (adjust footer text to "core/ building blocks").

Verify:
```bash
cd "C:/Users/envnt/Desktop/Base Frontend"
grep -nE '<link |<script|@import|url\(http|https?://(fonts|cdn|unpkg|cdnjs|ajax)' docs/CORE_FILES.html || echo "OK: self-contained"
for s in "AppError" "UseCase" "KeyValueStore" "CryptoService" "http-client.ts" "NotificationService" "WebCryptoLike"; do grep -q "$s" docs/CORE_FILES.html && echo "present: $s" || echo "MISSING: $s"; done
grep -c "</html>" docs/CORE_FILES.html
```
Expect "OK: self-contained", every symbol "present", and `</html>` count 1. Read back to confirm escaping (no raw `<T>` outside `&lt;`). Commit:
```bash
git add docs/CORE_FILES.html
git commit -m "docs: add CORE_FILES.html (core/ building blocks)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: `FEATURE_ANATOMY.html`

**Files:**
- Create: `docs/FEATURE_ANATOMY.html`

**Interfaces:**
- Consumes: the canonical `<style>` + `.nav` block (copy verbatim).

- [ ] **Step 1: Read the worked-example slice + the contrast slice**

Read all of `src/app/features/api/fetch-posts/**` (dto, model, port, repository, use case, viewmodel, page, index) and the `app.routes.ts` `api` route block. Also read `src/app/features/offline-storage/domain/repositories/profile-storage.repository.port.ts` and `.../domain/model/saved-profile.ts` for the second port pattern.

- [ ] **Step 2: Create file: canonical head/style + nav (active = Feature Anatomy)**

Start with the canonical block (title `Feature Anatomy — Base Frontend`), header + nav (active = `FEATURE_ANATOMY.html`) + legend, exactly as in Task 2 Steps 2 with the active link moved.

```html
    <header class="page">
      <h1>Anatomy of a feature</h1>
      <p>Every feature is a vertical slice with the same shape. Worked example: <code class="inline">features/api/fetch-posts</code>.</p>
    </header>
```

- [ ] **Step 3: Add the slice folder tree (colored by layer)**

```html
    <h2 class="section">The slice</h2>
    <pre class="tree"><span class="g">features/api/fetch-posts/</span>
  <span class="g data">data/</span>
    dto/post-dto-rs.ts                 <span class="note"># PostDtoRs + isPostDtoRsValid</span>
    repositories/post.repository.ts    <span class="note"># PostRepositoryImpl</span>
  <span class="g dom">domain/</span>
    model/post.ts                      <span class="note"># Post + toPost</span>
    repositories/post.repository.port.ts <span class="note"># IPostRepository + POST_REPOSITORY</span>
    usecases/get-posts.use-case.ts     <span class="note"># GetPostsUseCase</span>
  <span class="g pres">presentation/</span>
    viewmodels/fetch-posts.viewmodel.ts <span class="note"># signals</span>
    pages/api.page.ts                   <span class="note"># ApiPage</span>
  index.ts                              <span class="note"># public barrel</span></pre>
```

- [ ] **Step 4: Layer-by-layer cards (real fetch-posts code)**

Add a card per layer (tagged `data`/`dom`/`pres`), each with an escaped real snippet from the files read in Step 1, in this order: DTO + validator (`data`), model + mapper (`dom`), port + token (`dom`), repository impl (`data`), use case (`dom`), viewmodel (`pres`, show the signals + the clear-on-failure `else` branch), page (`pres`, show NavButton + NotificationService `effect`). Then the route wiring:

```html
    <h2 class="section">Wiring (route-level DI)</h2>
    <div class="card core">
      <div class="ch"><h3>Route providers</h3><span class="file">app.routes.ts</span></div>
      <pre>{
  path: 'api',
  loadComponent: () =&gt; import('@features/api/fetch-posts').then((m) =&gt; m.ApiPage),
  providers: [
    { provide: POST_REPOSITORY, useClass: PostRepositoryImpl },
    GetPostsUseCase,
    FetchPostsViewModel,
  ],
}</pre>
    </div>
```

- [ ] **Step 5: Two repository-port patterns + naming table + how-to**

```html
    <h2 class="section">Two repository-port patterns</h2>
    <div class="card dom">
      <p><b>Remote / untrusted data</b> (e.g. <code class="inline">fetch-posts</code>): the port returns the
      transport <b>DTO</b> (<code class="inline">Promise&lt;PostsDtoRs&gt;</code>); the <b>use case</b> validates
      (<code class="inline">isPostDtoRsValid</code>) and maps to the domain model (<code class="inline">toPost</code>).</p>
      <p><b>Local / trusted data</b> (e.g. <code class="inline">offline-storage</code>): the port returns the
      <b>domain model</b> directly (<code class="inline">Promise&lt;SavedProfile | null&gt;</code>); the repository
      handles (de)serialization via <code class="inline">KeyValueStore</code>. No DTO/validation layer needed.</p>
    </div>

    <h2 class="section">Naming conventions</h2>
    <table class="conv">
      <tr><th>Kind</th><th>Convention</th></tr>
      <tr><td>Response / request DTO</td><td><code>…DtoRs</code> / <code>…DtoRq</code></td></tr>
      <tr><td>Repository port</td><td><code>I…Repository</code> interface + <code>…_REPOSITORY</code> InjectionToken</td></tr>
      <tr><td>Use case</td><td><code>…UseCase</code> (extends <code>UseCase&lt;I,O&gt;</code>)</td></tr>
      <tr><td>Repository impl</td><td><code>…RepositoryImpl</code></td></tr>
      <tr><td>ViewModel</td><td><code>…ViewModel</code> (<code>@Injectable</code> facade, signals)</td></tr>
      <tr><td>Filenames</td><td>kebab-case; class names aligned with the mobile base</td></tr>
    </table>

    <h2 class="section">How to add a feature</h2>
    <ol class="steps">
      <li><b>Create the folders</b> <code class="inline">features/&lt;group&gt;/&lt;feature&gt;/{data,domain,presentation}</code>.</li>
      <li><b>DTO + validator</b> in <code class="inline">data/dto</code> (remote data only).</li>
      <li><b>Model + mapper</b> in <code class="inline">domain/model</code>.</li>
      <li><b>Port</b>: interface + <code class="inline">InjectionToken</code> in <code class="inline">domain/repositories</code>.</li>
      <li><b>Repository impl</b> in <code class="inline">data/repositories</code>.</li>
      <li><b>Use case</b> extending <code class="inline">UseCase</code> in <code class="inline">domain/usecases</code>.</li>
      <li><b>ViewModel</b> (signals; clear success-data on failure) in <code class="inline">presentation/viewmodels</code>.</li>
      <li><b>Page</b> (standalone) in <code class="inline">presentation/pages</code>.</li>
      <li><b>Barrel</b> <code class="inline">index.ts</code>; <b>wire the route</b> with feature-scoped <code class="inline">providers</code>.</li>
      <li><b>Tests</b>: a use-case spec + a viewmodel spec.</li>
    </ol>
```

- [ ] **Step 6: Footer + verify + commit**

Footer + closing tags (as Task 1 Step 5). Verify:
```bash
cd "C:/Users/envnt/Desktop/Base Frontend"
grep -nE '<link |<script|@import|url\(http|https?://(fonts|cdn|unpkg|cdnjs|ajax)' docs/FEATURE_ANATOMY.html || echo "OK: self-contained"
for s in "PostDtoRs" "isPostDtoRsValid" "GetPostsUseCase" "POST_REPOSITORY" "FetchPostsViewModel" "SavedProfile"; do grep -q "$s" docs/FEATURE_ANATOMY.html && echo "present: $s" || echo "MISSING: $s"; done
grep -c "</html>" docs/FEATURE_ANATOMY.html
```
Expect self-contained OK, all symbols present, `</html>` = 1. Commit:
```bash
git add docs/FEATURE_ANATOMY.html
git commit -m "docs: add FEATURE_ANATOMY.html (fetch-posts vertical slice)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: `DATA_FLOW.html`

**Files:**
- Create: `docs/DATA_FLOW.html`

**Interfaces:**
- Consumes: the canonical `<style>` + `.nav` block (copy verbatim).

- [ ] **Step 1: Re-read the flow participants**

Read `fetch-posts.viewmodel.ts`, `get-posts.use-case.ts`, `post.repository.ts`, `core/network/api/http-client.ts`, `core/domain/usecase/use-case.ts` to get the exact call shapes for the trace.

- [ ] **Step 2: Create file: canonical head/style + nav (active = Data Flow)**

Canonical block (title `Data Flow — Base Frontend`), header + nav (active = `DATA_FLOW.html`) + legend.

```html
    <header class="page">
      <h1>Data flow</h1>
      <p>One direction down, <code class="inline">Result&lt;T&gt;</code> back up. Worked trace: fetching posts.</p>
    </header>
```

- [ ] **Step 3: Flow SVG**

```html
    <h2 class="section">The path of a request</h2>
    <svg viewBox="0 0 760 360" width="100%" role="img" aria-label="Data flow" xmlns="http://www.w3.org/2000/svg">
      <style>
        .b{rx:10;ry:10;stroke:#e1e4e8;stroke-width:1.5;}
        .t{font:600 13px -apple-system,Segoe UI,Roboto,sans-serif;fill:#1f2328;}
        .s{font:11px ui-monospace,Consolas,monospace;fill:#57606a;}
        .ln{stroke:#94a3b8;stroke-width:1.5;marker-end:url(#a);}
      </style>
      <defs><marker id="a" markerWidth="9" markerHeight="9" refX="7" refY="3" orient="auto"><path d="M0,0 L7,3 L0,6 Z" fill="#94a3b8"/></marker></defs>
      <rect class="b" x="280" y="14"  width="200" height="42" fill="#ede9fe"/><text class="t" x="298" y="34">ApiPage</text><text class="s" x="298" y="50">(click)</text>
      <rect class="b" x="280" y="80"  width="200" height="42" fill="#ede9fe"/><text class="t" x="298" y="100">FetchPostsViewModel</text><text class="s" x="298" y="116">loading/posts/error signals</text>
      <rect class="b" x="280" y="146" width="200" height="42" fill="#dcfce7"/><text class="t" x="298" y="166">GetPostsUseCase.run()</text><text class="s" x="298" y="182">validate + map</text>
      <rect class="b" x="280" y="212" width="200" height="42" fill="#ffedd5"/><text class="t" x="298" y="232">PostRepositoryImpl</text><text class="s" x="298" y="248">getPosts()</text>
      <rect class="b" x="280" y="278" width="200" height="42" fill="#eaf1ff"/><text class="t" x="298" y="298">HttpClientService</text><text class="s" x="298" y="314">request() → fetch</text>
      <line class="ln" x1="380" y1="56"  x2="380" y2="78"/>
      <line class="ln" x1="380" y1="122" x2="380" y2="144"/>
      <line class="ln" x1="380" y1="188" x2="380" y2="210"/>
      <line class="ln" x1="380" y1="254" x2="380" y2="276"/>
      <text class="s" x="500" y="190">Result&lt;Post[]&gt; / AppError ↑</text>
    </svg>
```

- [ ] **Step 4: Numbered step trace (`ol.steps`)**

Add an `ol.steps` list tracing the 7 hops with one short escaped snippet each: (1) Page click → `vm.run()`; (2) ViewModel sets `loading.set(true)`, awaits `useCase.run()`; (3) `UseCase.run()` try/catch wraps `execute()`; (4) `execute()` calls `repo.getPosts()`, validates with `isPostDtoRsValid`, maps `toPost`; (5) repository `http.request('/posts')`, synthesizes `{ data, success }`; (6) `HttpClientService.request()` GETs, retries transport failures, maps to `AppError`; (7) `Result` bubbles back → ViewModel sets `posts`/`error` signals (clears posts on failure) → page toasts via `NotificationService`.

- [ ] **Step 5: Error-handling lane**

```html
    <h2 class="section">Where errors come from</h2>
    <p class="lead">Each layer throws a typed <code class="inline">AppError</code>; <code class="inline">UseCase.run()</code> catches once and normalizes any non-AppError to <code class="inline">'unknown'</code>, returning a <code class="inline">Result</code>.</p>
    <table class="conv">
      <tr><th>Origin</th><th>AppError kind</th></tr>
      <tr><td>http-client: transport failure / timeout</td><td><code>network</code></td></tr>
      <tr><td>http-client: non-2xx response</td><td><code>http</code> (+ status)</td></tr>
      <tr><td>use case: failed DTO validation</td><td><code>validation</code></td></tr>
      <tr><td>KeyValueStore: storage failure / corrupt object</td><td><code>storage</code></td></tr>
      <tr><td>CryptoService: encrypt/decrypt failure</td><td><code>crypto</code></td></tr>
      <tr><td>any other throw</td><td><code>unknown</code> (normalized by UseCase.run)</td></tr>
    </table>
```

- [ ] **Step 6: Footer + verify + commit**

Footer + closing tags. Verify:
```bash
cd "C:/Users/envnt/Desktop/Base Frontend"
grep -nE '<link |<script|@import|url\(http|https?://(fonts|cdn|unpkg|cdnjs|ajax)' docs/DATA_FLOW.html || echo "OK: self-contained"
for s in "FetchPostsViewModel" "GetPostsUseCase" "HttpClientService" "AppError" "validation"; do grep -q "$s" docs/DATA_FLOW.html && echo "present: $s" || echo "MISSING: $s"; done
grep -c "</html>" docs/DATA_FLOW.html
```
Expect self-contained OK, all present, `</html>` = 1. Commit:
```bash
git add docs/DATA_FLOW.html
git commit -m "docs: add DATA_FLOW.html (request flow + error handling)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: `PROJECT_LIBRARIES.html`

**Files:**
- Create: `docs/PROJECT_LIBRARIES.html`

**Interfaces:**
- Consumes: the canonical `<style>` + `.nav` block (copy verbatim).

- [ ] **Step 1: Read `package.json`**

Read `C:/Users/envnt/Desktop/Base Frontend/package.json`. Use these EXACT versions (verify they still match at authoring time):
- dependencies: `@angular/animations ^20.3.0`, `@angular/common ^20.3.0`, `@angular/compiler ^20.3.0`, `@angular/core ^20.3.0`, `@angular/forms ^20.3.0`, `@angular/platform-browser ^20.3.0`, `@angular/router ^20.3.0`, `rxjs ~7.8.0`, `tslib ^2.3.0`, `zone.js ~0.15.0`.
- devDependencies: `@angular/build ^20.3.26`, `@angular/cli ^20.3.26`, `@angular/compiler-cli ^20.3.0`, `@jest/globals ^30.4.1`, `@types/jest ^30.0.0`, `jest ^30.4.2`, `jest-environment-jsdom ^30.4.1`, `jest-preset-angular ^17.0.0`, `typescript ~5.9.2`.

- [ ] **Step 2: Create file: canonical head/style + nav (active = Project Libraries)**

Canonical block (title `Project Libraries — Base Frontend`), header + nav (active = `PROJECT_LIBRARIES.html`).

```html
    <header class="page">
      <h1>Project libraries</h1>
      <p>What each dependency is for and why it's here. Versions reflect <code class="inline">package.json</code>.</p>
    </header>
    <div class="banner"><strong>Snapshot:</strong> versions below reflect the current
      <code class="inline">package.json</code> (Angular <code class="inline">^20.3.0</code>) and may change as you update.</div>
```

- [ ] **Step 3: Runtime dependency cards**

Add a `.card` per runtime dependency with name + version + "what it's for". Cover all 10 runtime deps. Example:

```html
    <h2 class="section">Runtime dependencies <span class="file">(dependencies)</span></h2>
    <div class="card core">
      <div class="ch"><h3>@angular/core</h3><span class="file">^20.3.0</span></div>
      <p>The framework: components, dependency injection (<code class="inline">inject</code>, <code class="inline">InjectionToken</code>), and signals.</p>
    </div>
    <div class="card core">
      <div class="ch"><h3>@angular/common</h3><span class="file">^20.3.0</span></div>
      <p>Common directives/pipes and <code class="inline">HttpClient</code> (<code class="inline">provideHttpClient</code>) — wrapped by <code class="inline">core/network/api/http-client.ts</code>.</p>
    </div>
    <div class="card core">
      <div class="ch"><h3>rxjs</h3><span class="file">~7.8.0</span></div>
      <p>Used internally by <code class="inline">http-client.ts</code> (<code class="inline">firstValueFrom</code>, <code class="inline">timeout</code>, <code class="inline">retry</code>) and converted to a Promise at the boundary.</p>
    </div>
```
Also cover: `@angular/router` (routing + lazy `loadComponent`), `@angular/forms` (reactive forms — profile-form), `@angular/platform-browser` (`bootstrapApplication`), `@angular/animations` + `provideAnimations()` (design-hub demo), `@angular/compiler`, `zone.js` (change detection), `tslib` (TS runtime helpers).

- [ ] **Step 4: Dev dependency cards + deliberate omissions**

```html
    <h2 class="section">Dev dependencies <span class="file">(devDependencies)</span></h2>
```
Cards for: `@angular/build` + `@angular/cli` (`^20.3.26`, build/serve), `@angular/compiler-cli`, `typescript` (`~5.9.2`), `jest` (`^30.4.2`) + `jest-preset-angular` (`^17.0.0`) + `@types/jest` + `@jest/globals` + `jest-environment-jsdom` (the test stack — `npm test`). Then:

```html
    <h2 class="section">Deliberately NOT included</h2>
    <div class="card core">
      <p><b>No global state library</b> (NgRx, etc.) — local <code class="inline">signal</code>s suffice (YAGNI).</p>
      <p><b>No extra HTTP client</b> — Angular <code class="inline">HttpClient</code>, wrapped for the Promise + <code class="inline">Result</code> contract.</p>
      <p><b>No Karma/Jasmine</b> — testing is Jest-only via <code class="inline">jest-preset-angular</code>.</p>
    </div>
```

- [ ] **Step 5: Footer + verify + commit**

Footer + closing tags. Verify:
```bash
cd "C:/Users/envnt/Desktop/Base Frontend"
grep -nE '<link |<script|@import|url\(http|https?://(fonts|cdn|unpkg|cdnjs|ajax)' docs/PROJECT_LIBRARIES.html || echo "OK: self-contained"
for s in "@angular/core" "rxjs" "jest-preset-angular" "zone.js" "^20.3.0" "~7.8.0"; do grep -q "$s" docs/PROJECT_LIBRARIES.html && echo "present: $s" || echo "MISSING: $s"; done
grep -c "</html>" docs/PROJECT_LIBRARIES.html
```
Expect self-contained OK, all present, `</html>` = 1. Cross-check every version string against `package.json`. Commit:
```bash
git add docs/PROJECT_LIBRARIES.html
git commit -m "docs: add PROJECT_LIBRARIES.html (dependencies + rationale)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: Integration — link the set + whole-set verification

**Files:**
- Modify: `README.md`
- Modify: `docs/architecture/README.md`

**Interfaces:**
- Consumes: all 5 HTML pages from Tasks 1–5.

- [ ] **Step 1: Link the HTML set from `docs/architecture/README.md`**

Read `docs/architecture/README.md`. Add a section near the top (after the intro) listing the HTML pages with relative links (the guide is in `docs/architecture/`, so the pages are one level up):

```markdown
## Visual architecture docs (HTML)

Rich, standalone pages that present this architecture (siblings of the mobile/backend bases' docs):

- [Architecture Overview](../ARCHITECTURE_OVERVIEW.html) — start here
- [Core Files](../CORE_FILES.html)
- [Feature Anatomy](../FEATURE_ANATOMY.html)
- [Data Flow](../DATA_FLOW.html)
- [Project Libraries](../PROJECT_LIBRARIES.html)
```

- [ ] **Step 2: Link the HTML set from the project `README.md`**

Read `README.md`. In its Architecture section, add a line pointing at the HTML docs:

```markdown
Visual architecture docs (open in a browser): [`docs/ARCHITECTURE_OVERVIEW.html`](docs/ARCHITECTURE_OVERVIEW.html)
— start there; it cross-links Core Files, Feature Anatomy, Data Flow, and Project Libraries.
```

- [ ] **Step 2.5: Confirm no `src/` changes**

Run (expect NO output):
```bash
cd "C:/Users/envnt/Desktop/Base Frontend"
git status --short | grep "src/" && echo "ERROR: src changed (should be docs-only)" || echo "OK: no src changes"
```

- [ ] **Step 3: Whole-set verification — all 5 files exist, self-contained, cross-links resolve**

Run:
```bash
cd "C:/Users/envnt/Desktop/Base Frontend"
for f in ARCHITECTURE_OVERVIEW CORE_FILES FEATURE_ANATOMY DATA_FLOW PROJECT_LIBRARIES; do
  test -f "docs/$f.html" && echo "exists: $f.html" || echo "MISSING: $f.html"
done
# self-contained across the set (expect no matches)
grep -lnE '<link |<script|@import|url\(http|https?://(fonts|cdn|unpkg|cdnjs|ajax)' docs/*.html || echo "OK: all self-contained"
# every page links to all 5 (nav present on each)
for f in docs/ARCHITECTURE_OVERVIEW.html docs/CORE_FILES.html docs/FEATURE_ANATOMY.html docs/DATA_FLOW.html docs/PROJECT_LIBRARIES.html; do
  echo "$f links: $(grep -oE '(ARCHITECTURE_OVERVIEW|CORE_FILES|FEATURE_ANATOMY|DATA_FLOW|PROJECT_LIBRARIES)\.html' "$f" | sort -u | wc -l)/5"
done
```
Expect: all 5 exist, "OK: all self-contained", and each page reports `5/5` cross-links.

- [ ] **Step 4: Accuracy spot-check (manual read-back)**

Open each page in a browser (or read it) and confirm: styled output (the design system applies), the layer legend/colors render, SVGs display, and the snippets/paths match the real `src/app` files. Fix any escaping artifacts (raw `<`/`>` showing as broken tags).

- [ ] **Step 5: Commit**

```bash
cd "C:/Users/envnt/Desktop/Base Frontend"
git add README.md docs/architecture/README.md
git commit -m "docs: link the HTML architecture doc set from READMEs

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Final verification (after Task 6)

- [ ] All 5 HTML pages exist flat in `docs/`, each self-contained (no external resource loads).
- [ ] Each page carries the shared design system + the 5-link nav (`5/5`), with the current page active.
- [ ] Content is accurate: paths, class/type names, and snippets match `src/app`; library versions match `package.json`.
- [ ] No changes under `src/` (docs-only).
- [ ] `README.md` and `docs/architecture/README.md` link the set; `docs/architecture/README.md` is retained.

## Done When

- All task checkboxes (Tasks 1–6) are checked.
- The Angular base ships the 5 HTML architecture pages matching the role and visual language of the mobile and backend bases, accurate to the implemented code, self-contained, and cross-linked from the READMEs.
