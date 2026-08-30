# Architecture Reference Graph — Design

**Date:** 2026-06-27
**Status:** Approved design, ready for implementation plan

## Goal

Add a diagram to `docs/ARCHITECTURE_OVERVIEW.html`, above the existing "big picture"
layered stack, that shows **who references whom** — the actual `<ProjectReference>`
edges between the solution's projects, as a tiered arrow graph. While adding it, fix a
small inaccuracy the new diagram exposes in the existing stack.

## Ground truth (from the `.csproj` files)

Verified directly from the project files. "X → Y" means X has a `<ProjectReference>` to Y:

| Project | References (actual) |
|---------|---------------------|
| `BaseBackend.Api` (host) | Infrastructure **only** |
| `Catalog.Infrastructure` | Domain, Application, Contracts, SharedKernel |
| `Catalog.Application` | Domain, SharedKernel |
| `Catalog.Domain` | SharedKernel |
| `Catalog.Contracts` | **nothing** — its `.csproj` is empty |
| `BaseBackend.SharedKernel` | **nothing** — it is the base |

Two facts worth teaching: nothing references the host (`Api`), and `Contracts`
references nothing (so it does **not** reference SharedKernel, despite what the current
stack says).

## Component 1 — New "Who references whom" section (the reference graph)

Inserted **above** the `<h2 class="section">The big picture</h2>` heading, as its own
`<h2 class="section">Who references whom</h2>` section with a one-line lead, then the
graph.

The graph is a **single, carefully-authored inline SVG** (an arrow graph is what SVG is
for), wrapped in a `overflow-x: auto` scroll container with a `min-width` so narrow
screens scroll rather than clip. It reuses the layer palette and is laid out in tiers:

- **Top:** `Api` (host colors).
- **Tier 2:** `Infrastructure`.
- **Tier 3 (a row):** `Application`, `Domain`, `Contracts`.
- **Base (full-width bar):** `SharedKernel`.

Arrows (`marker-end` arrowheads, muted `#94a3b8`) draw every actual reference edge from
the ground-truth table: Api→Infrastructure; Infrastructure→{Application, Domain,
Contracts, SharedKernel}; Application→{Domain, SharedKernel}; Domain→SharedKernel.
`Contracts` and `SharedKernel` have no outgoing arrows.

**Authoring rules** (so it does not repeat the old SVG's bugs): boxes are sized
generously for their **short, stable labels** (the layer names `Api`,
`Infrastructure`, `Application`, `Domain`, `Contracts`, `SharedKernel`) so text never
clips; boxes never overlap; the long Infrastructure→SharedKernel edge routes down the
gap between `Domain` and `Contracts` to avoid crossing boxes.

A caption under the graph notes the two tells: `Contracts` has no outgoing arrow
(references nothing — empty `.csproj`), and nothing points at `Api` (the host is the
top).

**Lead text:** "Every arrow is a real `<ProjectReference>` in the `.csproj` — 'X → Y'
means X references Y. The host points to one module's Infrastructure; Infrastructure
pulls in everything it needs; the inner layers point only inward."

## Component 2 — Reconcile the existing stack (3 lines)

So the two diagrams agree, change three lines in the existing "big picture" stack
(currently at lines 156, 159, 160):

1. **Contracts dependency note** (line 156): `SharedKernel only` → `nothing yet`.
2. **Connector before the SharedKernel bar** (line 159):
   `↓ every project builds on ↓` → `↓ referenced by Domain · Application · Infrastructure ↓`.
3. **SharedKernel bar** (line 160): `— shared by all: BaseEntity · IModule · ApiResponse<T>`
   → `— shared by Domain, Application & Infrastructure: BaseEntity · IModule · ApiResponse<T>`.

(The later standalone "SharedKernel" section's prose at line ~207, "shared by all
modules," is about module-level availability and stays unchanged.)

## Constraints

- **Fully offline:** no JavaScript, no external assets, no CDN/network links.
- **Reuse the layer palette (exact hex):** Domain `#166534`/`#dcfce7`, Application
  `#5b21b6`/`#ede9fe`, Infrastructure `#9a3412`/`#ffedd5`, Contracts `#0f766e`/`#ccfbf1`,
  Host/Api `#2563eb`/`#eaf1ff`, SharedKernel `#475569`/`#e2e8f0`. Arrow gray `#94a3b8`.
- **Scoped CSS:** any new CSS for the graph wrapper/caption is scoped under a `.refgraph`
  wrapper class with literal hex, so it cannot collide with existing page classes. The
  SVG itself carries its own scoped `<style>` for box/line/text classes.
- **Only `docs/ARCHITECTURE_OVERVIEW.html` changes.** Touch no other page; do not alter
  the nav, pager, footer, or any other section beyond the new graph and the three
  reconciliation lines.

## Files changed

- `docs/ARCHITECTURE_OVERVIEW.html` — insert the new "Who references whom" section above
  "The big picture"; change the three stack lines (156, 159, 160).

## Non-goals (YAGNI)

- No JavaScript, no diagram libraries, no build step.
- No reference matrix or chip-list variant (the tiered arrow graph was chosen).
- No change to the layered-stack structure beyond the three reconciliation lines.
- No edits to other pages or to the actual `.csproj` files (this is docs only).

## Verification

- Open `docs/ARCHITECTURE_OVERVIEW.html`: above "The big picture" there is a "Who
  references whom" graph with `Api` at the top, `Infrastructure` below it,
  `Application`/`Domain`/`Contracts` in a row, and a `SharedKernel` base bar; arrows
  match the ground-truth table; no clipped text, no overlapping boxes. Narrowing the
  window scrolls the graph horizontally rather than clipping it.
- The stack below now reads "nothing yet" for Contracts and the SharedKernel
  bar/connector no longer claim "all/every project."
- The graph and the stack agree: Contracts references nothing; SharedKernel is
  referenced by Domain, Application, Infrastructure (not Contracts, not Api directly).
- View source: the new graph is the only `<svg>` on the page; no `<script>`, no external
  URLs (besides the SVG xmlns).
