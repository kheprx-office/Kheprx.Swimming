# Architecture & Sequence Diagram Redesign — Design

**Date:** 2026-06-27
**Status:** Approved design, ready for implementation plan

## Goal

Two of the docs pages render hand-positioned SVG diagrams that are buggy and hard
to maintain. Replace them with robust, professional, teachable HTML/CSS diagrams
that fix the bugs and make the architecture easier to learn.

## Problems in the current diagrams

**`docs/ARCHITECTURE_OVERVIEW.html` — "The big picture" SVG (the broken one):**
- The `SharedKernel` box (x 610–740) overlaps the `Catalog.Infrastructure` box
  (x 480–650) — a visible ~40px overlap.
- The grey monospace sub-labels are wider than their 170-unit boxes, so
  `Entities · Exceptions · Por…` and `Services · DTOs · Validator…` are clipped
  where the next box begins.
- Boxes are 120 units tall with text crammed at the top (wasted space), and the two
  arrows don't show which layer depends on which — the main teaching point is missing.

**`docs/REQUEST_FLOW.html` — "Sequence diagram" SVG:**
- More carefully built, but the same fragile hand-positioned format: fixed pixel
  coordinates, SVG text that can't wrap, and `fill:var(--…-bg)` dependencies. It
  squashes (rather than scrolls) on narrow screens and is hard to edit safely.

## Constraints (inherited from the docs)

- **Fully offline:** no JavaScript, no external assets, no CDN/network links. HTML + CSS only.
- **Reuse the existing visual language:** the layer palette and `.tag` pill colors —
  Domain `#166534`/`#dcfce7`, Application `#5b21b6`/`#ede9fe`,
  Infrastructure `#9a3412`/`#ffedd5`, Contracts `#0f766e`/`#ccfbf1`,
  Host/Api `#2563eb`/`#eaf1ff`, SharedKernel `#475569`/`#e2e8f0`. Returns use green
  `#16a34a`; calls/accent use `#2563eb`; borders `#e1e4e8`; muted text `#57606a`.
- **Self-contained pages:** new diagram CSS goes into each page's own head `<style>`
  block, using **literal hex values** (not `var(--…)`), so a diagram never depends on
  CSS variables resolving.
- **New CSS classes must not collide** with classes already defined on the page. Use a
  distinct prefix for each diagram's classes (e.g. `archdiag-*` on the architecture
  page, `seqdiag-*` on the request-flow page).
- **Only the two `<svg>` blocks change.** Keep each section's `<h2>` heading and its
  intro `<p class="lead">` (the lead text may be lightly reworded to match the new
  visual). No other page content is touched. No other pages are touched.

## Component 1 — Architecture "big picture" (layered HTML stack)

Replaces the `<svg>` at `ARCHITECTURE_OVERVIEW.html` (currently lines ~117–150).

A vertical stack. Each row carries an explicit "depends on" note; the teaching point
is that every dependency points toward the core (Domain) and nothing points outward:

1. **Host bar** (host colors, full width): `Host / Api` tag + **BaseBackend.Api** —
   "discovers & registers every module at startup, owns the HTTP pipeline."
2. **Connector line:** `↓ AddModules() · the host references each module's Infrastructure only`.
3. **Module container** (dashed border) labelled **"One module — e.g. Catalog — four
   projects"**, holding four layer rows. Each row = a left-accent card (border-left in
   the layer color) with: the layer `.tag` pill, the project name, a monospace contents
   line, and a right-aligned muted **"depends on …"** note:

   | Layer (top→bottom) | Project | Contents | Depends on |
   |--------------------|---------|----------|------------|
   | Infrastructure | `Catalog.Infrastructure` | DbContext · Repositories · DI · CatalogModule | **Domain · Application · Contracts** |
   | Application | `Catalog.Application` | Services · DTOs · Validators · Mappers | **Domain** |
   | Domain ★ core | `Catalog.Domain` | Entities · Exceptions · Ports | **nothing** but SharedKernel |
   | Contracts | `Catalog.Contracts` | the only project other modules may reference | SharedKernel only |

4. **Connector line:** `↓ every project builds on ↓`.
5. **SharedKernel bar** (shared colors, full width, centered): `SharedKernel` tag +
   **BaseBackend.SharedKernel** — "shared by all: BaseEntity · IModule · ApiResponse&lt;T&gt;".

These dependency facts are exactly those documented elsewhere on the page (the
per-layer "Depends on" lines) and enforced in
`tests/BaseBackend.ArchitectureTests/BoundaryTests.cs` — the diagram must not
contradict them.

The intro `<p class="lead">` becomes: "The host discovers modules at startup and wires
them in. Each module is four projects; the 'depends on' note on each row is the rule —
dependencies point inward to the Domain core, and modules expose only their Contracts."

## Component 2 — Request Flow sequence diagram (robust HTML)

Replaces the `<svg>` at `REQUEST_FLOW.html` (currently lines ~176–239). Keeps the
sequence-diagram form (participants across the top, time flowing down) but in HTML/CSS.

- **Five participant columns**, each a layer badge pill above a participant box:
  Controller (Host/Api), Service (Application), Repository (Infrastructure),
  DbContext (Infrastructure), PostgreSQL (External — uses the shared/slate color).
- **Dashed vertical lifelines** under each participant.
- **Eight messages**, each a horizontal connector spanning two adjacent columns with a
  centered label above and an arrowhead glyph at the destination end:
  - Calls — solid blue `#2563eb`, arrow pointing right/down the stack:
    1. Controller → Service `GetAllAsync(ct)`
    2. Service → Repository `GetAllAsync(ct)`
    3. Repository → DbContext `ToListAsync(ct)`
    4. DbContext → PostgreSQL `SQL SELECT`
  - Returns — dashed green `#16a34a`, arrow pointing left/back up:
    4. PostgreSQL → DbContext `rows`
    3. DbContext → Repository `List<Product>`
    2. Repository → Service `IReadOnlyList<Product>`
    1. Service → Controller `IReadOnlyList<ProductDto>`
- The whole diagram sits in a **horizontal-scroll wrapper** (`overflow-x: auto`) with a
  sensible `min-width`, so narrow screens scroll instead of clipping or squashing.
- A small **legend** under it: solid blue = call, dashed green = return.

The intro `<p class="lead">` stays substantially as-is (it already describes solid
calls / dashed returns and the per-participant layer pills).

## Implementation approach

- For each page: append the diagram's CSS to the head `<style>` block (before the
  `</style>` that precedes `</head>`), and replace the `<svg>…</svg>` element with the
  new HTML markup. The inline `<style>` inside each old SVG is removed with it.
- Layout uses flexbox/grid for the boxes; the sequence diagram positions lifelines and
  message connectors with CSS (percentage offsets inside a `position: relative` body).
  No JavaScript.
- Accessibility improves: the diagrams become real selectable text rather than an SVG
  image.

## Files changed

- `docs/ARCHITECTURE_OVERVIEW.html` — swap the architecture SVG for the layered stack;
  add `archdiag-*` CSS.
- `docs/REQUEST_FLOW.html` — swap the sequence SVG for the HTML sequence diagram;
  add `seqdiag-*` CSS.

## Non-goals (YAGNI)

- No JavaScript, no diagram libraries (Mermaid etc. need JS/CDN — excluded by the
  offline rule), no build step.
- No changes to any other page, to the page headers/nav/pager/footer, or to the
  non-SVG content on these two pages (the existing vertical `.node` request-flow list
  and all code cards stay).
- No new diagrams beyond replacing the two existing ones.

## Verification

- Open `docs/ARCHITECTURE_OVERVIEW.html`: the big-picture area shows the host bar, the
  four layer rows with correct "depends on" notes, and the SharedKernel bar — no
  overlap, no clipped text. Narrowing the window reflows the rows without clipping.
- Open `docs/REQUEST_FLOW.html`: the sequence diagram shows five badged participants,
  lifelines, four blue calls and four green returns with readable labels; narrowing the
  window produces a horizontal scrollbar rather than clipped/overlapping text.
- View source / DevTools: no `<svg>` remains in the two replaced sections, no `<script>`,
  no external URLs.
- The dependency notes match the page's per-layer "Depends on" lines and `BoundaryTests.cs`.
