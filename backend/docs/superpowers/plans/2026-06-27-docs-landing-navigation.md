# Docs Landing Page & Cross-Page Navigation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the seven standalone HTML docs in `docs/` a professional front door — a guided landing page plus a sticky top nav bar and prev/next pager on every page.

**Architecture:** Pure static HTML/CSS, no build step. A new `docs/index.html` presents the docs as a numbered 1→7 reading path in four stages. The same sticky top nav bar is duplicated into all eight pages (current page highlighted); the seven content pages also get a prev/next pager above their existing footer. Each page stays a self-contained file with its own `<style>` block — consistent with how these docs already duplicate their shared style block.

**Tech Stack:** Hand-written HTML5 + CSS. No JavaScript, no frameworks, no external assets. Edits applied with a text-edit tool; verification by `grep`/`Select-String` and by opening files in a browser.

## Global Constraints

Copied verbatim from the spec — every task must honor these:

- **Fully offline:** no JavaScript, no external assets, no CDN/network links. Navigation is plain `<a href="FILE.html">` + CSS only.
- **Reuse the existing palette (exact hex):** accent `#2563eb`, background `#f6f8fa`, card `#ffffff`, border `#e1e4e8`, muted text `#57606a`, body text `#1f2328`.
- **Layer colors (exact hex):** Domain `#166534`, Application `#5b21b6`, Infrastructure `#9a3412`, Contracts `#0f766e`, Host/Api `#2563eb`, SharedKernel `#475569`.
- **Do not remove or rewrite existing page content.** Only add nav near the top and a pager near the bottom. The existing `footer.page` link row stays as-is.
- Each page is self-contained: nav/pager CSS is added to each file's own `<style>` block (literal hex values, so it does not depend on any page's CSS variables).

## The reading path (drives nav order, card order, pager order)

| # | Title | File | Card left-edge color class |
|---|-------|------|----------------------------|
| 1 | Architecture Overview | `ARCHITECTURE_OVERVIEW.html` | `host` |
| 2 | Solution Structure | `SOLUTION_STRUCTURE.html` | `shared` |
| 3 | Project Libraries | `PROJECT_LIBRARIES.html` | `con` |
| 4 | Module Anatomy | `MODULE_ANATOMY.html` | `dom` |
| 5 | Request Flow | `REQUEST_FLOW.html` | `app` |
| 6 | Validation | `VALIDATION.html` | `inf` |
| 7 | Validation Guide | `VALIDATION_GUIDE.html` | `inf` |

## File structure

- **Create:** `docs/index.html` — the landing page (hero + 4 stages + 7 doc cards + footer), including its own copy of the top-nav markup and CSS.
- **Modify (7 files):** each content page gets (a) nav + pager CSS appended to its `<style>`, (b) the nav bar inserted as the first child of `<body>`, (c) a pager inserted just above `footer.page`.

---

### Task 1: Create the landing page `docs/index.html`

**Files:**
- Create: `docs/index.html`

**Interfaces:**
- Consumes: nothing (first task).
- Produces: `docs/index.html` containing the canonical top-nav markup (with `Home` marked `active`) and the nav CSS that Tasks 2–3 replicate into the content pages. The link→file map other tasks rely on: Home→`index.html`, Architecture→`ARCHITECTURE_OVERVIEW.html`, Structure→`SOLUTION_STRUCTURE.html`, Libraries→`PROJECT_LIBRARIES.html`, Module→`MODULE_ANATOMY.html`, Request Flow→`REQUEST_FLOW.html`, Validation→`VALIDATION.html`, Guide→`VALIDATION_GUIDE.html`.

- [ ] **Step 1: Create `docs/index.html` with this exact content**

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Documentation — BaseBackend</title>
  <style>
  :root {
    --bg: #f6f8fa; --card: #ffffff; --border: #e1e4e8; --text: #1f2328; --muted: #57606a;
    --accent: #2563eb; --accent-soft: #eaf1ff;
    --dom: #166534; --app: #5b21b6; --inf: #9a3412; --con: #0f766e; --host: #2563eb; --shared: #475569;
  }
  * { box-sizing: border-box; }
  body { margin: 0; padding: 0 16px 72px; background: var(--bg); color: var(--text);
    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; line-height: 1.6; }
  .wrap { max-width: 980px; margin: 0 auto; }
  /* ===== shared top nav (duplicated onto every doc page) ===== */
  .topnav { position: sticky; top: 0; z-index: 50; background: #2563eb; margin: 0 -16px; }
  .topnav .ti { max-width: 980px; margin: 0 auto; padding: 9px 16px; display: flex; flex-wrap: wrap; align-items: center; gap: 2px 4px; }
  .topnav a { text-decoration: none; }
  .topnav .brand { color: #fff; font-weight: 700; font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; margin-right: 12px; font-size: 0.95rem; }
  .topnav .nl { color: #fff; font-size: 0.85rem; padding: 4px 9px; border-radius: 6px; opacity: .9; }
  .topnav .nl:hover { background: rgba(255,255,255,.16); opacity: 1; }
  .topnav .nl.active { background: rgba(255,255,255,.24); opacity: 1; font-weight: 600; }
  /* ===== hero ===== */
  header.hero { padding: 36px 0 8px; }
  header.hero h1 { margin: 0 0 6px; font-size: 1.9rem; }
  header.hero h1 .mono { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; color: var(--accent); }
  header.hero p { margin: 0; color: var(--muted); font-size: 0.98rem; max-width: 70ch; }
  .start { display: inline-block; margin-top: 16px; background: var(--accent); color: #fff; font-weight: 600; font-size: 0.92rem; padding: 10px 18px; border-radius: 8px; text-decoration: none; }
  .start:hover { background: #1d4ed8; }
  /* ===== stages & doc cards ===== */
  .stage { margin: 30px 0 0; }
  .stage .lab { display: flex; align-items: center; gap: 9px; font-size: 0.74rem; letter-spacing: .5px; text-transform: uppercase; color: var(--muted); font-weight: 700; margin-bottom: 11px; }
  .stage .lab .pill { background: var(--accent-soft); color: var(--accent); width: 22px; height: 22px; border-radius: 50%; display: inline-flex; align-items: center; justify-content: center; font-size: 0.74rem; }
  .grid { display: flex; flex-direction: column; gap: 9px; }
  a.doc { display: flex; gap: 13px; align-items: flex-start; background: var(--card); border: 1px solid var(--border); border-left-width: 5px; border-radius: 10px; padding: 13px 15px; text-decoration: none; color: inherit; }
  a.doc:hover { box-shadow: 0 3px 12px rgba(0,0,0,.08); transform: translateY(-1px); }
  a.doc .n { flex-shrink: 0; width: 25px; height: 25px; border-radius: 50%; background: var(--accent); color: #fff; font-weight: 700; font-size: 0.82rem; display: flex; align-items: center; justify-content: center; }
  a.doc .tt { font-size: 1.0rem; font-weight: 700; color: var(--text); }
  a.doc .dd { font-size: 0.86rem; color: var(--muted); margin-top: 2px; }
  a.doc.host { border-left-color: var(--host); } a.doc.shared { border-left-color: var(--shared); }
  a.doc.dom { border-left-color: var(--dom); } a.doc.app { border-left-color: var(--app); }
  a.doc.inf { border-left-color: var(--inf); } a.doc.con { border-left-color: var(--con); }
  footer.page { margin-top: 48px; padding-top: 18px; border-top: 1px solid var(--border); color: var(--muted); font-size: 0.86rem; }
  footer.page a { color: var(--accent); }
  </style>
</head>
<body>
  <nav class="topnav">
    <div class="ti">
      <a class="brand" href="index.html">BaseBackend</a>
      <a class="nl active" href="index.html">Home</a>
      <a class="nl" href="ARCHITECTURE_OVERVIEW.html">Architecture</a>
      <a class="nl" href="SOLUTION_STRUCTURE.html">Structure</a>
      <a class="nl" href="PROJECT_LIBRARIES.html">Libraries</a>
      <a class="nl" href="MODULE_ANATOMY.html">Module</a>
      <a class="nl" href="REQUEST_FLOW.html">Request Flow</a>
      <a class="nl" href="VALIDATION.html">Validation</a>
      <a class="nl" href="VALIDATION_GUIDE.html">Guide</a>
    </div>
  </nav>
  <div class="wrap">
    <header class="hero">
      <h1><span class="mono">BaseBackend</span> — Documentation</h1>
      <p>A .NET 10 modular monolith, explained end to end. New here? Follow the path below in order — each step builds on the last.</p>
      <a class="start" href="ARCHITECTURE_OVERVIEW.html">Start here → Architecture Overview</a>
    </header>

    <div class="stage">
      <div class="lab"><span class="pill">1</span> Foundations</div>
      <div class="grid">
        <a class="doc host" href="ARCHITECTURE_OVERVIEW.html"><span class="n">1</span><span><span class="tt">Architecture Overview</span><span class="dd">The big picture: a modular monolith and how its pieces fit together. Read this first.</span></span></a>
      </div>
    </div>

    <div class="stage">
      <div class="lab"><span class="pill">2</span> Structure &amp; Building Blocks</div>
      <div class="grid">
        <a class="doc shared" href="SOLUTION_STRUCTURE.html"><span class="n">2</span><span><span class="tt">Solution Structure</span><span class="dd">Every project and folder in the solution, and what lives where.</span></span></a>
        <a class="doc con" href="PROJECT_LIBRARIES.html"><span class="n">3</span><span><span class="tt">Project Libraries</span><span class="dd">The NuGet libraries the project relies on, and why each one is used.</span></span></a>
        <a class="doc dom" href="MODULE_ANATOMY.html"><span class="n">4</span><span><span class="tt">Module Anatomy</span><span class="dd">Inside one module: its four layers — Domain, Application, Infrastructure, Contracts.</span></span></a>
      </div>
    </div>

    <div class="stage">
      <div class="lab"><span class="pill">3</span> How a Request Runs</div>
      <div class="grid">
        <a class="doc app" href="REQUEST_FLOW.html"><span class="n">5</span><span><span class="tt">Request Flow</span><span class="dd">How an HTTP request travels end to end, from controller to database and back.</span></span></a>
      </div>
    </div>

    <div class="stage">
      <div class="lab"><span class="pill">4</span> Validation in Depth</div>
      <div class="grid">
        <a class="doc inf" href="VALIDATION.html"><span class="n">6</span><span><span class="tt">Validation</span><span class="dd">The five layers of validation a request passes through at runtime, in order.</span></span></a>
        <a class="doc inf" href="VALIDATION_GUIDE.html"><span class="n">7</span><span><span class="tt">Validation Guide</span><span class="dd">Validation from first principles, plus a full catalog of every rule in the codebase.</span></span></a>
      </div>
    </div>

    <footer class="page">BaseBackend docs — <a href="index.html">Home</a> · <a href="ARCHITECTURE_OVERVIEW.html">Architecture Overview</a> · <a href="SOLUTION_STRUCTURE.html">Solution Structure</a> · <a href="PROJECT_LIBRARIES.html">Project Libraries</a> · <a href="MODULE_ANATOMY.html">Module Anatomy</a> · <a href="REQUEST_FLOW.html">Request Flow</a> · <a href="VALIDATION.html">Validation</a> · <a href="VALIDATION_GUIDE.html">Validation Guide</a></footer>
  </div>
</body>
</html>
```

- [ ] **Step 2: Verify every link target exists**

Run (PowerShell, from repo root):

```powershell
'index.html','ARCHITECTURE_OVERVIEW.html','SOLUTION_STRUCTURE.html','PROJECT_LIBRARIES.html','MODULE_ANATOMY.html','REQUEST_FLOW.html','VALIDATION.html','VALIDATION_GUIDE.html' |
  ForEach-Object { if (Test-Path "docs/$_") { "OK  $_" } else { "MISSING $_" } }
```

Expected: eight `OK` lines, zero `MISSING`.

- [ ] **Step 3: Open in a browser and eyeball it**

Run: `Start-Process "docs/index.html"`
Expected: blue sticky nav at top with `Home` highlighted; hero with "Start here" button; four stage labels (1–4) with seven numbered cards in order; each card's left edge shows its layer color. Click each of the 7 cards and the "Start here" button — each opens the matching doc.

- [ ] **Step 4: Commit**

```bash
git add docs/index.html
git commit -m "docs: add landing page (index.html) for the docs guide"
```

---

### Task 2: Add the sticky top nav bar to all 7 content pages

Applies the same change to each of the seven files in the reading-path table. The nav markup is identical except for which link carries `active`.

**Files:**
- Modify: `docs/ARCHITECTURE_OVERVIEW.html`, `docs/SOLUTION_STRUCTURE.html`, `docs/PROJECT_LIBRARIES.html`, `docs/MODULE_ANATOMY.html`, `docs/REQUEST_FLOW.html`, `docs/VALIDATION.html`, `docs/VALIDATION_GUIDE.html`

**Interfaces:**
- Consumes: the link→file map and `.topnav` CSS established in Task 1.
- Produces: a `<nav class="topnav">` as the first child of `<body>` on every content page, with the current page's link marked `active`.

**Active-link map** (which `<a class="nl">` becomes `<a class="nl active">` in each file):

| File | Active link label |
|------|-------------------|
| ARCHITECTURE_OVERVIEW.html | Architecture |
| SOLUTION_STRUCTURE.html | Structure |
| PROJECT_LIBRARIES.html | Libraries |
| MODULE_ANATOMY.html | Module |
| REQUEST_FLOW.html | Request Flow |
| VALIDATION.html | Validation |
| VALIDATION_GUIDE.html | Guide |

- [ ] **Step 1: Add the nav CSS to each file's `<style>` block**

In **each** of the 7 files, FIND this exact text (the head `<style>` always closes on the line directly above `</head>`):

```html
  </style>
</head>
```

REPLACE with (inserts the nav CSS just before the closing tag):

```html
  /* ===== docs cross-page navigation (added to every page) ===== */
  .topnav { position: sticky; top: 0; z-index: 50; background: #2563eb; margin: 0 -16px; }
  .topnav .ti { max-width: 980px; margin: 0 auto; padding: 9px 16px; display: flex; flex-wrap: wrap; align-items: center; gap: 2px 4px; }
  .topnav a { text-decoration: none; }
  .topnav .brand { color: #fff; font-weight: 700; font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; margin-right: 12px; font-size: 0.95rem; }
  .topnav .nl { color: #fff; font-size: 0.85rem; padding: 4px 9px; border-radius: 6px; opacity: .9; }
  .topnav .nl:hover { background: rgba(255,255,255,.16); opacity: 1; }
  .topnav .nl.active { background: rgba(255,255,255,.24); opacity: 1; font-weight: 600; }
  </style>
</head>
```

- [ ] **Step 2: Insert the nav bar as the first child of `<body>`**

In **each** of the 7 files, FIND this exact text:

```html
<body>
  <div class="wrap">
```

REPLACE with the block below, then add ` active` to the one link named in the Active-link map for that file (so e.g. in `VALIDATION.html`, `<a class="nl" href="VALIDATION.html">Validation</a>` becomes `<a class="nl active" href="VALIDATION.html">Validation</a>`):

```html
<body>
  <nav class="topnav">
    <div class="ti">
      <a class="brand" href="index.html">BaseBackend</a>
      <a class="nl" href="index.html">Home</a>
      <a class="nl" href="ARCHITECTURE_OVERVIEW.html">Architecture</a>
      <a class="nl" href="SOLUTION_STRUCTURE.html">Structure</a>
      <a class="nl" href="PROJECT_LIBRARIES.html">Libraries</a>
      <a class="nl" href="MODULE_ANATOMY.html">Module</a>
      <a class="nl" href="REQUEST_FLOW.html">Request Flow</a>
      <a class="nl" href="VALIDATION.html">Validation</a>
      <a class="nl" href="VALIDATION_GUIDE.html">Guide</a>
    </div>
  </nav>
  <div class="wrap">
```

- [ ] **Step 3: Verify the nav landed once per file, with exactly one active link**

Run (PowerShell, from repo root):

```powershell
'ARCHITECTURE_OVERVIEW','SOLUTION_STRUCTURE','PROJECT_LIBRARIES','MODULE_ANATOMY','REQUEST_FLOW','VALIDATION','VALIDATION_GUIDE' |
  ForEach-Object {
    $c = Get-Content "docs/$_.html" -Raw
    $nav = ([regex]'class="topnav"').Matches($c).Count
    $act = ([regex]'class="nl active"').Matches($c).Count
    "{0,-22} topnav={1} active={2}" -f $_, $nav, $act
  }
```

Expected: every line shows `topnav=1 active=1`.

- [ ] **Step 4: Browser smoke-test**

Run: `Start-Process "docs/VALIDATION.html"`
Expected: blue sticky nav at top; `Validation` highlighted; clicking `Home` opens the landing page; clicking other links opens the right docs; the bar stays pinned while scrolling. Resize the window narrow — the links wrap to a second line without overflowing.

- [ ] **Step 5: Commit**

```bash
git add docs/ARCHITECTURE_OVERVIEW.html docs/SOLUTION_STRUCTURE.html docs/PROJECT_LIBRARIES.html docs/MODULE_ANATOMY.html docs/REQUEST_FLOW.html docs/VALIDATION.html docs/VALIDATION_GUIDE.html
git commit -m "docs: add sticky top nav bar to all doc pages"
```

---

### Task 3: Add the prev/next pager to all 7 content pages

**Files:**
- Modify: the same seven files as Task 2.

**Interfaces:**
- Consumes: the `.pager` CSS added in this task; the reading-path order from the table.
- Produces: a `<div class="pager">` directly above `footer.page` on every content page.

**Pager values per file** (exact label → href for the two links):

| File | Previous (label → href) | Next (label → href) |
|------|--------------------------|----------------------|
| ARCHITECTURE_OVERVIEW.html | Home → `index.html` | Solution Structure → `SOLUTION_STRUCTURE.html` |
| SOLUTION_STRUCTURE.html | Architecture Overview → `ARCHITECTURE_OVERVIEW.html` | Project Libraries → `PROJECT_LIBRARIES.html` |
| PROJECT_LIBRARIES.html | Solution Structure → `SOLUTION_STRUCTURE.html` | Module Anatomy → `MODULE_ANATOMY.html` |
| MODULE_ANATOMY.html | Project Libraries → `PROJECT_LIBRARIES.html` | Request Flow → `REQUEST_FLOW.html` |
| REQUEST_FLOW.html | Module Anatomy → `MODULE_ANATOMY.html` | Validation → `VALIDATION.html` |
| VALIDATION.html | Request Flow → `REQUEST_FLOW.html` | Validation Guide → `VALIDATION_GUIDE.html` |
| VALIDATION_GUIDE.html | Validation → `VALIDATION.html` | Home → `index.html` |

- [ ] **Step 1: Add the pager CSS to each file's `<style>` block**

In **each** of the 7 files, FIND this exact text:

```html
  </style>
</head>
```

REPLACE with:

```html
  .pager { display: flex; justify-content: space-between; gap: 12px; margin: 44px 0 0; }
  .pager a { flex: 1 1 0; min-width: 0; border: 1px solid #e1e4e8; background: #ffffff; border-radius: 10px; padding: 11px 15px; text-decoration: none; }
  .pager a:hover { border-color: #2563eb; }
  .pager a.next { text-align: right; }
  .pager .dir { display: block; font-size: 0.72rem; color: #57606a; text-transform: uppercase; letter-spacing: .4px; }
  .pager .ttl { display: block; font-size: 0.96rem; font-weight: 700; color: #2563eb; margin-top: 3px; }
  </style>
</head>
```

> Note: after Task 2, each file already has nav CSS before `</style>`; this inserts the pager CSS just after it (still before `</style>`). The `  </style>\n</head>` anchor is unchanged by Task 2.

- [ ] **Step 2: Insert the pager above the footer**

In **each** of the 7 files, FIND this exact text:

```html
    <footer class="page">
```

REPLACE with the pager block below followed by the footer tag, substituting the four values from the Pager-values table for that file:

```html
    <div class="pager">
      <a href="PREV_HREF"><span class="dir">← Previous</span><span class="ttl">PREV_LABEL</span></a>
      <a class="next" href="NEXT_HREF"><span class="dir">Next →</span><span class="ttl">NEXT_LABEL</span></a>
    </div>
    <footer class="page">
```

Worked example for `REQUEST_FLOW.html` (PREV = Module Anatomy, NEXT = Validation):

```html
    <div class="pager">
      <a href="MODULE_ANATOMY.html"><span class="dir">← Previous</span><span class="ttl">Module Anatomy</span></a>
      <a class="next" href="VALIDATION.html"><span class="dir">Next →</span><span class="ttl">Validation</span></a>
    </div>
    <footer class="page">
```

- [ ] **Step 3: Verify each pager exists once with the correct neighbors**

Run (PowerShell, from repo root):

```powershell
$expect = @{
  'ARCHITECTURE_OVERVIEW' = @('index.html','SOLUTION_STRUCTURE.html')
  'SOLUTION_STRUCTURE'    = @('ARCHITECTURE_OVERVIEW.html','PROJECT_LIBRARIES.html')
  'PROJECT_LIBRARIES'     = @('SOLUTION_STRUCTURE.html','MODULE_ANATOMY.html')
  'MODULE_ANATOMY'        = @('PROJECT_LIBRARIES.html','REQUEST_FLOW.html')
  'REQUEST_FLOW'          = @('MODULE_ANATOMY.html','VALIDATION.html')
  'VALIDATION'            = @('REQUEST_FLOW.html','VALIDATION_GUIDE.html')
  'VALIDATION_GUIDE'      = @('VALIDATION.html','index.html')
}
foreach ($k in $expect.Keys) {
  $c = Get-Content "docs/$k.html" -Raw
  $pager = ([regex]'class="pager"').Matches($c).Count
  $prevOk = $c -match [regex]::Escape('href="' + $expect[$k][0] + '"><span class="dir">← Previous')
  $nextOk = $c -match [regex]::Escape('class="next" href="' + $expect[$k][1] + '"')
  "{0,-22} pager={1} prevOk={2} nextOk={3}" -f $k,$pager,$prevOk,$nextOk
}
```

Expected: every line shows `pager=1 prevOk=True nextOk=True`.

- [ ] **Step 4: Browser walk-through of the whole path**

Run: `Start-Process "docs/ARCHITECTURE_OVERVIEW.html"`
Expected: a pager sits just above the footer. From Architecture, `Previous` → Home and `Next` → Solution Structure. Click `Next` repeatedly to walk 1→7; on Validation Guide, `Next` returns to Home. The pager renders as two bordered cards (Previous left-aligned, Next right-aligned).

- [ ] **Step 5: Commit**

```bash
git add docs/ARCHITECTURE_OVERVIEW.html docs/SOLUTION_STRUCTURE.html docs/PROJECT_LIBRARIES.html docs/MODULE_ANATOMY.html docs/REQUEST_FLOW.html docs/VALIDATION.html docs/VALIDATION_GUIDE.html
git commit -m "docs: add prev/next pager to all doc pages"
```

---

## Final verification (after all tasks)

- [ ] From `docs/index.html`, click every card and the "Start here" button; each opens the right page.
- [ ] On each of the seven pages, the nav highlights the correct link and the pager points to the correct neighbors.
- [ ] Spot-check that no existing content was altered: `git diff` on the seven pages shows only added lines (nav CSS, pager CSS, the `<nav>` block, the `<div class="pager">` block, and the single ` active` class addition) — no deletions of existing markup.
