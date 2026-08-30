# Architecture Reference Graph Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "who references whom" tiered arrow graph (inline SVG) above the big-picture stack on the Architecture page, drawn from the real `.csproj` references, and reconcile three now-inaccurate lines in the existing stack.

**Architecture:** A single static HTML page edit. Append scoped CSS to the head `<style>`, insert a new `<h2>` section with the SVG above "The big picture", and change three lines in the stack so it agrees with the new graph. No JavaScript.

**Tech Stack:** Hand-written HTML5 + CSS + one inline SVG. Verification by `Select-String`/`grep` and opening the page in a browser.

## Global Constraints

- **Fully offline:** no JavaScript, no external assets, no CDN/network links.
- **Exact-hex layer palette:** Domain `#166534`/`#dcfce7`, Application `#5b21b6`/`#ede9fe`, Infrastructure `#9a3412`/`#ffedd5`, Contracts `#0f766e`/`#ccfbf1`, Host/Api `#2563eb`/`#eaf1ff`, SharedKernel `#475569`/`#e2e8f0`. Arrow gray `#94a3b8`, muted text `#57606a`, body text `#1f2328`.
- **Scoped, non-colliding names:** wrapper CSS under `.refgraph`; the SVG's own classes are `.rg-b`/`.rg-t`/`.rg-l` and its marker id is `rgar` (all unique to this graph). Reuse the page's existing `p.lead` and `code.inline` classes.
- **Only `docs/ARCHITECTURE_OVERVIEW.html` changes.** Do not touch the nav, pager, footer, or any other section beyond the new graph and the three reconciliation lines. Do not edit any `.csproj` (docs only).
- The page currently contains **zero** `<svg>` (the old one was replaced earlier); this adds exactly one.

## Reference edges (ground truth — the graph must match exactly)

Api → Infrastructure · Infrastructure → {Application, Domain, Contracts, SharedKernel} · Application → {Domain, SharedKernel} · Domain → SharedKernel · Contracts → nothing · SharedKernel → nothing.

---

### Task 1: Add the reference graph and reconcile the stack

**Files:**
- Modify: `docs/ARCHITECTURE_OVERVIEW.html` (head `<style>` closes on the line above `</head>`; "The big picture" `<h2>` at line 134; reconciliation lines at 156, 159, 160)

**Interfaces:**
- Consumes: the page's existing `p.lead` and `code.inline` styles, and the layer palette.
- Produces: a new "Who references whom" section above "The big picture". Nothing else depends on it.

- [ ] **Step 1: Add the `.refgraph` CSS to the head `<style>` block**

FIND this exact text (the head `<style>` closes on the line directly above `</head>`):

```html
  </style>
</head>
```

REPLACE with:

```html
  /* ===== project reference graph ===== */
  .refgraph { overflow-x: auto; margin: 10px 0 6px; }
  .refgraph svg { min-width: 540px; display: block; }
  .refgraph .cap { font-size: 0.82rem; color: #57606a; margin: 8px 0 0; text-align: center; }
  .refgraph .cap b { color: #1f2328; }
  </style>
</head>
```

- [ ] **Step 2: Insert the new "Who references whom" section above "The big picture"**

FIND this exact line:

```html
    <h2 class="section">The big picture</h2>
```

REPLACE with (the new section, then the original heading kept at the end):

```html
    <h2 class="section">Who references whom</h2>
    <p class="lead">Every arrow is a real <code class="inline">&lt;ProjectReference&gt;</code> in the <code class="inline">.csproj</code> — &ldquo;X &#8594; Y&rdquo; means X references Y. The host points to one module's Infrastructure; Infrastructure pulls in everything it needs; the inner layers point only inward.</p>
    <div class="refgraph">
      <svg viewBox="0 0 720 350" width="100%" role="img" aria-label="Project reference graph: Api references Infrastructure; Infrastructure references Application, Domain, Contracts and SharedKernel; Application references Domain and SharedKernel; Domain references SharedKernel; Contracts and SharedKernel reference nothing." xmlns="http://www.w3.org/2000/svg">
        <defs>
          <marker id="rgar" markerWidth="9" markerHeight="9" refX="7" refY="3" orient="auto"><path d="M0,0 L7,3 L0,6 Z" fill="#94a3b8"/></marker>
        </defs>
        <style>
          .rg-b{stroke-width:1.5;rx:9;ry:9;}
          .rg-t{font:700 13px -apple-system,Segoe UI,Roboto,sans-serif;fill:#1f2328;}
          .rg-l{stroke:#94a3b8;stroke-width:1.6;fill:none;marker-end:url(#rgar);}
        </style>
        <!-- edges (drawn under the boxes) -->
        <line class="rg-l" x1="360" y1="56" x2="360" y2="83"/>
        <line class="rg-l" x1="320" y1="126" x2="205" y2="163"/>
        <line class="rg-l" x1="360" y1="126" x2="360" y2="163"/>
        <line class="rg-l" x1="402" y1="126" x2="540" y2="163"/>
        <path class="rg-l" d="M430,124 C 455,140 458,150 458,166 L458,272"/>
        <line class="rg-l" x1="252" y1="187" x2="307" y2="187"/>
        <line class="rg-l" x1="160" y1="209" x2="160" y2="272"/>
        <line class="rg-l" x1="360" y1="209" x2="360" y2="272"/>
        <!-- boxes -->
        <rect class="rg-b" x="300" y="16"  width="120" height="40" fill="#eaf1ff" stroke="#2563eb"/>
        <text class="rg-t" x="360" y="41" text-anchor="middle">Api</text>
        <rect class="rg-b" x="282" y="84"  width="156" height="42" fill="#ffedd5" stroke="#9a3412"/>
        <text class="rg-t" x="360" y="110" text-anchor="middle">Infrastructure</text>
        <rect class="rg-b" x="120" y="165" width="132" height="42" fill="#ede9fe" stroke="#5b21b6"/>
        <text class="rg-t" x="186" y="191" text-anchor="middle">Application</text>
        <rect class="rg-b" x="308" y="165" width="104" height="42" fill="#dcfce7" stroke="#166534"/>
        <text class="rg-t" x="360" y="191" text-anchor="middle">Domain</text>
        <rect class="rg-b" x="500" y="165" width="120" height="42" fill="#ccfbf1" stroke="#0f766e"/>
        <text class="rg-t" x="560" y="191" text-anchor="middle">Contracts</text>
        <rect class="rg-b" x="110" y="272" width="500" height="40" fill="#e2e8f0" stroke="#475569"/>
        <text class="rg-t" x="360" y="297" text-anchor="middle">SharedKernel</text>
      </svg>
      <p class="cap"><b>Contracts</b> has no outgoing arrow — it references nothing (its <code class="inline">.csproj</code> is empty). Nothing points at <b>Api</b> — the host sits at the top.</p>
    </div>

    <h2 class="section">The big picture</h2>
```

- [ ] **Step 3: Reconcile the Contracts dependency note (line 156)**

FIND this exact line:

```html
          <div class="dep">SharedKernel only</div>
```

REPLACE with:

```html
          <div class="dep">nothing yet</div>
```

- [ ] **Step 4: Reconcile the connector line (line 159)**

FIND this exact line:

```html
      <div class="conn">&#8595; every project builds on &#8595;</div>
```

REPLACE with:

```html
      <div class="conn">&#8595; referenced by Domain · Application · Infrastructure &#8595;</div>
```

- [ ] **Step 5: Reconcile the SharedKernel bar (line 160)**

FIND this exact substring (within the SharedKernel bar line):

```html
— shared by all: BaseEntity · IModule · ApiResponse&lt;T&gt;
```

REPLACE with:

```html
— shared by Domain, Application &amp; Infrastructure: BaseEntity · IModule · ApiResponse&lt;T&gt;
```

- [ ] **Step 6: Verify**

Run (PowerShell, from repo root):

```powershell
$c = Get-Content docs/ARCHITECTURE_OVERVIEW.html -Raw
"svg (expect 1):                 " + ([regex]'<svg').Matches($c).Count
"refgraph wrapper (expect 1):    " + ([regex]'class="refgraph"').Matches($c).Count
"graph edges rg-l (expect 8):    " + ([regex]'class="rg-l"').Matches($c).Count
"heading 'Who references whom':  " + ([regex]'Who references whom').Matches($c).Count
"stale 'SharedKernel only' (0):  " + ([regex]'SharedKernel only').Matches($c).Count
"'nothing yet' (expect 1):       " + ([regex]'nothing yet').Matches($c).Count
"stale 'shared by all: BaseEntity' (0): " + ([regex]'shared by all: BaseEntity').Matches($c).Count
"script tags (expect 0):         " + ([regex]'<script').Matches($c).Count
"external http links (expect 0): " + ([regex]'https?://(?!www\.w3\.org)').Matches($c).Count
```

Expected: `svg 1`, `refgraph 1`, `rg-l 8`, `Who references whom 1`, `SharedKernel only 0`, `nothing yet 1`, `shared by all: BaseEntity 0`, `script 0`, `external http 0`.

Then run: `Start-Process "docs/ARCHITECTURE_OVERVIEW.html"`
Expected: above "The big picture", a "Who references whom" graph — `Api` on top, `Infrastructure` below, `Application`/`Domain`/`Contracts` in a row, a `SharedKernel` base bar, arrows matching the ground-truth edges, no clipped text, no overlapping boxes. The stack below now shows "nothing yet" for Contracts and the SharedKernel bar/connector no longer say "all/every project". Narrowing the window scrolls the graph horizontally.

- [ ] **Step 7: Commit**

```bash
git add docs/ARCHITECTURE_OVERVIEW.html
git commit -m "docs: add project reference graph and reconcile the stack"
```

---

## Final verification (after the task)

- [ ] `git diff --stat` shows only `docs/ARCHITECTURE_OVERVIEW.html` changed.
- [ ] The new graph's arrows exactly match: Api→Infrastructure; Infrastructure→{Application, Domain, Contracts, SharedKernel}; Application→{Domain, SharedKernel}; Domain→SharedKernel; Contracts and SharedKernel have no outgoing arrows.
- [ ] The reference graph and the layered stack agree (Contracts references nothing; SharedKernel referenced by Domain/Application/Infrastructure, not Contracts or Api directly).
- [ ] No `<script>`, no external URLs (besides the SVG xmlns); nav/pager/footer and all other sections unchanged.
