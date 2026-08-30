# Docs Landing Page & Navigation — Design

**Date:** 2026-06-27
**Status:** Approved design, ready for implementation plan

## Goal

The `docs/` folder holds seven standalone, offline HTML pages that share one design
language and a footer cross-link row, but there is no entry point. A reader has no
"home" and no obvious order to read them in.

Give the docs a professional, easy-to-understand front door:

1. A new **landing page** (`docs/index.html`) that relates all seven docs as a single
   guided reading path.
2. A consistent **top navigation bar** on every page so any doc is one click away.
3. A **previous / next pager** on every page so the path can be read straight through.

## Constraints (inherited from the existing docs)

- **Fully offline.** Every page must open directly from the filesystem with no web
  server and no internet. Therefore: **no JavaScript, no external assets, no CDN
  links.** Navigation is plain `<a href="FILE.html">` links and CSS only.
- **Reuse the existing visual language.** Same palette (accent `#2563eb`, background
  `#f6f8fa`, card white `#ffffff`, border `#e1e4e8`, muted `#57606a`) and the same
  per-layer colors already defined as CSS variables in each page:
  Domain `#166534`, Application `#5b21b6`, Infrastructure `#9a3412`,
  Contracts `#0f766e`, Host/Api `#2563eb`, SharedKernel `#475569`.
- Each page is a self-contained file with its own `<style>` block. The nav/pager CSS
  is duplicated into each file — consistent with how the pages already duplicate the
  shared style block.
- **No existing page content is removed or rewritten.** We only add a nav bar near the
  top and a pager near the bottom of each existing page. The current footer link row
  stays as-is.

## The reading path (single source of order)

This order drives the landing page, the nav bar, and the pager. It is fixed:

| # | Title (display) | File | Left-edge layer color |
|---|-----------------|------|-----------------------|
| 1 | Architecture Overview | `ARCHITECTURE_OVERVIEW.html` | Host `#2563eb` |
| 2 | Solution Structure | `SOLUTION_STRUCTURE.html` | SharedKernel `#475569` |
| 3 | Project Libraries | `PROJECT_LIBRARIES.html` | Contracts `#0f766e` |
| 4 | Module Anatomy | `MODULE_ANATOMY.html` | Domain `#166534` |
| 5 | Request Flow | `REQUEST_FLOW.html` | Application `#5b21b6` |
| 6 | Validation | `VALIDATION.html` | Infrastructure `#9a3412` |
| 7 | Validation Guide | `VALIDATION_GUIDE.html` | Infrastructure `#9a3412` |

The left-edge color is decorative (it makes the home page echo the layer palette used
across the docs); it is not a claim that the doc "belongs to" that layer.

## Component 1 — Landing page (`docs/index.html`)

A new file that adopts the same base styles as the other pages (same `:root` variables,
`.wrap`/`header.page` conventions) plus the shared nav/pager additions.

**Structure, top to bottom:**

1. **Top nav bar** (see Component 2), with **Home** marked active.
2. **Hero** — `<h1>` reading "`BaseBackend` — Documentation" (with the monospace blue
   "BaseBackend" treatment used in the other headers) and a one-line subtitle:
   *"A .NET 10 modular monolith, explained end to end. New here? Follow the path below
   in order — each step builds on the last."* Plus a primary **"Start here →
   Architecture Overview"** button linking to `ARCHITECTURE_OVERVIEW.html`.
3. **Four stages**, each with an uppercase label preceded by a small numbered pill, and
   one or more doc cards under it:
   - **Stage 1 — Foundations:** card 1 (Architecture Overview).
   - **Stage 2 — Structure & Building Blocks:** cards 2 (Solution Structure),
     3 (Project Libraries), 4 (Module Anatomy).
   - **Stage 3 — How a Request Runs:** card 5 (Request Flow).
   - **Stage 4 — Validation in Depth:** cards 6 (Validation), 7 (Validation Guide).
4. **Footer** — the same footer style and link row the other pages already use.

**Each doc card** is a full-card `<a>` link containing: a circular step number badge
(blue), the doc title (bold), and its one-line description. On hover it lifts with a
soft shadow. Left border uses the layer color from the table above.

**Card descriptions (final copy):**

1. Architecture Overview — *The big picture: a modular monolith and how its pieces fit together. Read this first.*
2. Solution Structure — *Every project and folder in the solution, and what lives where.*
3. Project Libraries — *The NuGet libraries the project relies on, and why each one is used.*
4. Module Anatomy — *Inside one module: its four layers — Domain, Application, Infrastructure, Contracts.*
5. Request Flow — *How an HTTP request travels end to end, from controller to database and back.*
6. Validation — *The five layers of validation a request passes through at runtime, in order.*
7. Validation Guide — *Validation from first principles, plus a full catalog of every rule in the codebase.*

## Component 2 — Top navigation bar (all 8 pages)

A slim, sticky bar inserted as the first element inside `<body>`, before `.wrap`.

- **Background** accent blue `#2563eb`, white text, `position: sticky; top: 0` so it
  stays visible while scrolling long pages. Full-bleed: cancel the body's horizontal
  padding (e.g. negative horizontal margin) and center an inner row to the same
  `980px` max-width as `.wrap`.
- **Items, in path order:** a bold monospace **`BaseBackend`** brand (links to
  `index.html`), then **Home · Architecture · Structure · Libraries · Module ·
  Request Flow · Validation · Guide**.
- **Link → file map:** Home→`index.html`, Architecture→`ARCHITECTURE_OVERVIEW.html`,
  Structure→`SOLUTION_STRUCTURE.html`, Libraries→`PROJECT_LIBRARIES.html`,
  Module→`MODULE_ANATOMY.html`, Request Flow→`REQUEST_FLOW.html`,
  Validation→`VALIDATION.html`, Guide→`VALIDATION_GUIDE.html`.
- The **current page's** link gets an `active` style (subtle translucent white pill,
  bolder). This is set per-file by adding the `active` class to the matching link.
- The row **wraps** onto a second line on narrow viewports (`flex-wrap: wrap`); no
  dropdown, no JS.

## Component 3 — Previous / Next pager (the 7 content pages)

A pager block added at the bottom of each page, just **above** the existing footer.

- Two link slots: **"← Previous: <title>"** on the left, **"Next: <title> →"** on the
  right, styled as bordered cards matching the page (white card, `#e1e4e8` border,
  blue title text).
- Sequence follows the reading-path order. The path loops gently at the ends:
  - `index.html`: not a path step — its pager is omitted (the hero's "Start here"
    button already leads in). *(Implementation note: only the seven content pages get
    a pager; the landing page does not.)*
  - Page 1 (Architecture Overview): Previous → **Home** (`index.html`); Next → Solution Structure.
  - Pages 2–6: Previous/Next are the adjacent docs in the table.
  - Page 7 (Validation Guide): Previous → Validation; Next → **Home** (`index.html`).

## Files changed

- **New:** `docs/index.html`.
- **Edited (7 files):** `ARCHITECTURE_OVERVIEW.html`, `SOLUTION_STRUCTURE.html`,
  `PROJECT_LIBRARIES.html`, `MODULE_ANATOMY.html`, `REQUEST_FLOW.html`,
  `VALIDATION.html`, `VALIDATION_GUIDE.html`. Each gets: (a) nav + pager CSS appended
  to its `<style>` block, (b) the nav bar inserted as the first child of `<body>` with
  the correct link marked `active`, (c) a pager inserted just above `footer.page`.

## Non-goals (YAGNI)

- No search, no collapsible/expanding menus, no dark mode, no JavaScript of any kind.
- No build step, bundler, or templating — pages stay hand-editable standalone HTML.
- No rewrite of existing page bodies or the existing footer link row.
- No new docs or content beyond the landing page and its descriptions.

## Verification

- Open `docs/index.html` directly from disk (file://). Every card and nav link
  resolves to the correct page; the "Start here" button opens Architecture Overview.
- On each of the seven pages: the nav bar shows with the correct link highlighted, the
  brand returns to the index, and the pager's Previous/Next point to the right
  neighbors.
- The nav bar wraps cleanly on a narrow window and stays pinned while scrolling.
- No network requests are made (verifiable with DevTools offline / no external URLs in
  source).
