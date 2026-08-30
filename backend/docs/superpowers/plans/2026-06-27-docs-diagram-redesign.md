# Architecture & Sequence Diagram Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the two fragile hand-positioned SVG diagrams in the docs with robust, teachable HTML/CSS diagrams — a layered "depends on" stack on the Architecture page and an HTML sequence diagram on the Request Flow page.

**Architecture:** Pure static HTML/CSS, no build step. Each task edits one existing page: it appends scoped diagram CSS to the page's head `<style>` block (literal hex values) and replaces the single `<svg>…</svg>` element with semantic HTML. No JavaScript.

**Tech Stack:** Hand-written HTML5 + CSS (flexbox/grid + absolutely-positioned connectors). Edits applied with a text-edit tool; verification by `grep`/`Select-String` and opening files in a browser.

## Global Constraints

Copied from the spec — every task must honor these:

- **Fully offline:** no JavaScript, no external assets, no CDN/network links. HTML + CSS only.
- **Exact-hex palette:** accent/call `#2563eb`, return-green `#16a34a`, border `#e1e4e8`, card `#ffffff`, muted `#57606a`, body text `#1f2328`. Layer pills: Domain `#166534`/`#dcfce7`, Application `#5b21b6`/`#ede9fe`, Infrastructure `#9a3412`/`#ffedd5`, Contracts `#0f766e`/`#ccfbf1`, Host/Api `#2563eb`/`#eaf1ff`, SharedKernel `#475569`/`#e2e8f0`.
- **Reuse the page's existing `.tag` pills** (already defined on both pages) for layer labels; do not redefine `.tag`.
- **No class collisions:** all new diagram CSS is scoped under a wrapper class (`.archdiag` on the architecture page; `.seqdiag`, `.seqdiag-scroll`, `.seqdiag-legend` on the request-flow page) using descendant selectors, so inner class names cannot collide with existing page styles. Use literal hex (never `var(--…)`).
- **Only the `<svg>` blocks change** (plus the architecture page's big-picture `<p class="lead">`). Keep each section's `<h2>` heading. Touch no other content, no other pages, and not the nav/pager/footer.
- Each page has **exactly one** `<svg>…</svg>` element — confirmed. Replacing "the only SVG in the file" is unambiguous.

---

### Task 1: Architecture page — layered "depends on" stack

**Files:**
- Modify: `docs/ARCHITECTURE_OVERVIEW.html` (insert CSS before the head `</style>` — the one immediately above `</head>`; big-picture lead at line 115; the only `<svg>` spans lines 117–150)

**Interfaces:**
- Consumes: the page's existing `.tag`/`.tag.dom`/`.tag.app`/`.tag.inf`/`.tag.con`/`.tag.host`/`.tag.shared` pill styles.
- Produces: a `.archdiag` block replacing the architecture SVG. No later task depends on this.

- [ ] **Step 1: Add the `.archdiag` CSS to the head `<style>` block**

FIND this exact text (the head `<style>` closes on the line directly above `</head>`):

```html
  </style>
</head>
```

REPLACE with (inserts the diagram CSS just before the closing tags):

```html
  /* ===== architecture "big picture" — layered depends-on stack ===== */
  .archdiag { margin: 10px 0 6px; }
  .archdiag .bar { border: 1px solid #e1e4e8; border-radius: 10px; padding: 11px 14px; font-size: 0.9rem; }
  .archdiag .bar.host { background: #eaf1ff; border-color: #bcd4ff; }
  .archdiag .bar.shared { background: #e2e8f0; border-color: #cbd5e1; text-align: center; }
  .archdiag .conn { text-align: center; color: #94a3b8; font-size: 0.78rem; margin: 7px 0; }
  .archdiag .mod { border: 1px dashed #cbd5e1; border-radius: 12px; padding: 12px; }
  .archdiag .modlab { font-size: 0.7rem; letter-spacing: .5px; text-transform: uppercase; color: #57606a; font-weight: 700; margin-bottom: 9px; }
  .archdiag .row { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; background: #fff; border: 1px solid #e1e4e8; border-left-width: 5px; border-radius: 9px; padding: 9px 12px; margin-bottom: 7px; }
  .archdiag .row:last-child { margin-bottom: 0; }
  .archdiag .row.inf { border-left-color: #9a3412; }
  .archdiag .row.app { border-left-color: #5b21b6; }
  .archdiag .row.dom { border-left-color: #166534; }
  .archdiag .row.con { border-left-color: #0f766e; }
  .archdiag .meat { flex: 1 1 280px; min-width: 0; }
  .archdiag .nm { font-weight: 700; font-size: 0.92rem; }
  .archdiag .core { color: #166534; font-weight: 700; font-size: 0.78rem; }
  .archdiag .ct { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: 0.74rem; color: #57606a; margin-top: 2px; }
  .archdiag .dep { font-size: 0.74rem; color: #57606a; text-align: right; }
  .archdiag .dep b { color: #1f2328; font-weight: 600; }
  </style>
</head>
```

- [ ] **Step 2: Update the big-picture lead paragraph**

FIND this exact line:

```html
    <p class="lead">The host discovers modules at startup and wires them in. Each module owns its data and exposes a thin contract.</p>
```

REPLACE with:

```html
    <p class="lead">The host discovers modules at startup and wires them in. Each module is four projects; the "depends on" note on each row is the rule — dependencies point inward to the Domain core, and modules expose only their Contracts.</p>
```

- [ ] **Step 3: Replace the architecture SVG with the layered stack**

In `docs/ARCHITECTURE_OVERVIEW.html`, delete the entire `<svg … aria-label="BaseBackend architecture" …> … </svg>` element — it is the **only** `<svg>` in the file, opening at line 117 `    <svg viewBox="0 0 760 300" …>` and closing at line 150 `    </svg>` (inclusive of its inner `<style>`, `<defs>`, `<rect>`, `<text>`, and `<line>` children) — and put this HTML in its place:

```html
    <div class="archdiag">
      <div class="bar host"><span class="tag host">Host / Api</span> &nbsp;<b>BaseBackend.Api</b> — discovers &amp; registers every module at startup, owns the HTTP pipeline.</div>
      <div class="conn">&#8595; &nbsp;AddModules() &nbsp;·&nbsp; the host references each module's Infrastructure only</div>
      <div class="mod">
        <div class="modlab">One module — e.g. Catalog — four projects</div>
        <div class="row inf">
          <div class="meat"><span class="tag inf">Infrastructure</span> &nbsp;<span class="nm">Catalog.Infrastructure</span><div class="ct">DbContext · Repositories · DI · CatalogModule</div></div>
          <div class="dep">depends on <b>Domain · Application · Contracts</b></div>
        </div>
        <div class="row app">
          <div class="meat"><span class="tag app">Application</span> &nbsp;<span class="nm">Catalog.Application</span><div class="ct">Services · DTOs · Validators · Mappers</div></div>
          <div class="dep">depends on <b>Domain</b></div>
        </div>
        <div class="row dom">
          <div class="meat"><span class="tag dom">Domain</span> &nbsp;<span class="nm">Catalog.Domain</span> &nbsp;<span class="core">&#9733; core</span><div class="ct">Entities · Exceptions · Ports</div></div>
          <div class="dep">depends on <b>nothing</b> but SharedKernel</div>
        </div>
        <div class="row con">
          <div class="meat"><span class="tag con">Contracts</span> &nbsp;<span class="nm">Catalog.Contracts</span><div class="ct">the only project other modules may reference</div></div>
          <div class="dep">SharedKernel only</div>
        </div>
      </div>
      <div class="conn">&#8595; every project builds on &#8595;</div>
      <div class="bar shared"><span class="tag shared">SharedKernel</span> &nbsp;<b>BaseBackend.SharedKernel</b> — shared by all: BaseEntity · IModule · ApiResponse&lt;T&gt;</div>
    </div>
```

- [ ] **Step 4: Verify the replacement**

Run (PowerShell, from repo root):

```powershell
$c = Get-Content docs/ARCHITECTURE_OVERVIEW.html -Raw
"svg tags (expect 0):      " + ([regex]'<svg').Matches($c).Count
"archdiag block (expect 1):" + ([regex]'class="archdiag"').Matches($c).Count
"layer rows (expect 4):    " + ([regex]'class="row (inf|app|dom|con)"').Matches($c).Count
"script tags (expect 0):   " + ([regex]'<script').Matches($c).Count
"http(s) links (expect 0): " + ([regex]'https?://(?!www\.w3\.org)').Matches($c).Count
```

Expected: `svg tags 0`, `archdiag block 1`, `layer rows 4`, `script tags 0`, `http(s) links 0`.

Then run: `Start-Process "docs/ARCHITECTURE_OVERVIEW.html"`
Expected: under "The big picture", a blue Host bar, a dashed module box with four left-accent rows (Infrastructure, Application, Domain ★ core, Contracts) each showing a "depends on …" note on the right, and a grey SharedKernel bar — no overlap, no clipped text. Narrowing the window reflows the rows.

- [ ] **Step 5: Commit**

```bash
git add docs/ARCHITECTURE_OVERVIEW.html
git commit -m "docs: replace architecture SVG with a layered depends-on stack"
```

---

### Task 2: Request Flow page — HTML sequence diagram

**Files:**
- Modify: `docs/REQUEST_FLOW.html` (head `<style>` closes on the line above `</head>`; the only `<svg>` spans lines 176–239; its intro lead at line 174 is left unchanged)

**Interfaces:**
- Consumes: nothing from Task 1 (independent page).
- Produces: a `.seqdiag-scroll` / `.seqdiag` block replacing the sequence SVG.

- [ ] **Step 1: Add the `.seqdiag` CSS to the head `<style>` block**

FIND this exact text:

```html
  </style>
</head>
```

REPLACE with:

```html
  /* ===== request-flow sequence diagram (HTML) ===== */
  .seqdiag-scroll { overflow-x: auto; margin: 10px 0 6px; }
  .seqdiag { min-width: 620px; }
  .seqdiag .head { display: grid; grid-template-columns: repeat(5, 1fr); gap: 8px; text-align: center; margin-bottom: 4px; }
  .seqdiag .badge { font-size: 0.64rem; font-weight: 700; padding: 1px 7px; border-radius: 999px; display: inline-block; }
  .seqdiag .badge.host { color: #2563eb; background: #eaf1ff; }
  .seqdiag .badge.app { color: #5b21b6; background: #ede9fe; }
  .seqdiag .badge.inf { color: #9a3412; background: #ffedd5; }
  .seqdiag .badge.ext { color: #475569; background: #e2e8f0; }
  .seqdiag .box { border: 1px solid #e1e4e8; background: #fff; border-radius: 7px; padding: 6px 2px; font-weight: 700; font-size: 0.82rem; margin-top: 3px; }
  .seqdiag .body { position: relative; height: 236px; margin-top: 2px; }
  .seqdiag .life { position: absolute; top: 0; bottom: 0; border-left: 1px dashed #cbd5e1; }
  .seqdiag .msg { position: absolute; height: 0; }
  .seqdiag .msg .line { border-top: 2px solid #2563eb; }
  .seqdiag .msg.ret .line { border-top: 1.7px dashed #16a34a; }
  .seqdiag .msg .lab { position: absolute; left: 0; right: 0; top: -14px; text-align: center; font-size: 0.68rem; color: #334155; white-space: nowrap; }
  .seqdiag .msg .hd { position: absolute; top: -7px; font-size: 0.72rem; line-height: 1; }
  .seqdiag .msg.call .hd { right: -3px; color: #2563eb; }
  .seqdiag .msg.ret .hd { left: -3px; color: #16a34a; }
  .seqdiag-legend { font-size: 0.74rem; color: #57606a; margin-top: 10px; }
  .seqdiag-legend .c { color: #2563eb; font-weight: 700; }
  .seqdiag-legend .g { color: #16a34a; font-weight: 700; }
  </style>
</head>
```

- [ ] **Step 2: Replace the sequence SVG with the HTML sequence diagram**

In `docs/REQUEST_FLOW.html`, delete the entire `<svg … aria-label="GET /api/products sequence …" …> … </svg>` element — the **only** `<svg>` in the file, opening at line 176 and closing at line 239 (inclusive of its inner `<style>`, `<defs>`, and all `<rect>`/`<text>`/`<line>` children) — and put this HTML in its place:

```html
    <div class="seqdiag-scroll">
      <div class="seqdiag">
        <div class="head">
          <div><span class="badge host">Host / Api</span><div class="box">Controller</div></div>
          <div><span class="badge app">Application</span><div class="box">Service</div></div>
          <div><span class="badge inf">Infrastructure</span><div class="box">Repository</div></div>
          <div><span class="badge inf">Infrastructure</span><div class="box">DbContext</div></div>
          <div><span class="badge ext">External</span><div class="box">PostgreSQL</div></div>
        </div>
        <div class="body">
          <div class="life" style="left:10%"></div>
          <div class="life" style="left:30%"></div>
          <div class="life" style="left:50%"></div>
          <div class="life" style="left:70%"></div>
          <div class="life" style="left:90%"></div>
          <div class="msg call" style="left:10%;width:20%;top:16px"><div class="line"></div><div class="lab">1. GetAllAsync(ct)</div><div class="hd">&#9654;</div></div>
          <div class="msg call" style="left:30%;width:20%;top:44px"><div class="line"></div><div class="lab">2. GetAllAsync(ct)</div><div class="hd">&#9654;</div></div>
          <div class="msg call" style="left:50%;width:20%;top:72px"><div class="line"></div><div class="lab">3. ToListAsync(ct)</div><div class="hd">&#9654;</div></div>
          <div class="msg call" style="left:70%;width:20%;top:100px"><div class="line"></div><div class="lab">4. SQL SELECT</div><div class="hd">&#9654;</div></div>
          <div class="msg ret" style="left:70%;width:20%;top:132px"><div class="line"></div><div class="lab">rows</div><div class="hd">&#9664;</div></div>
          <div class="msg ret" style="left:50%;width:20%;top:164px"><div class="line"></div><div class="lab">List&lt;Product&gt;</div><div class="hd">&#9664;</div></div>
          <div class="msg ret" style="left:30%;width:20%;top:192px"><div class="line"></div><div class="lab">IReadOnlyList&lt;Product&gt;</div><div class="hd">&#9664;</div></div>
          <div class="msg ret" style="left:10%;width:20%;top:220px"><div class="line"></div><div class="lab">IReadOnlyList&lt;ProductDto&gt;</div><div class="hd">&#9664;</div></div>
        </div>
      </div>
    </div>
    <div class="seqdiag-legend"><span class="c">Solid blue</span> = a call (down the stack) &nbsp;·&nbsp; <span class="g">dashed green</span> = a return (back up)</div>
```

- [ ] **Step 3: Verify the replacement**

Run (PowerShell, from repo root):

```powershell
$c = Get-Content docs/REQUEST_FLOW.html -Raw
"svg tags (expect 0):       " + ([regex]'<svg').Matches($c).Count
"seqdiag block (expect 1):  " + ([regex]'class="seqdiag"').Matches($c).Count
"call msgs (expect 4):      " + ([regex]'class="msg call"').Matches($c).Count
"return msgs (expect 4):    " + ([regex]'class="msg ret"').Matches($c).Count
"lifelines (expect 5):      " + ([regex]'class="life"').Matches($c).Count
"script tags (expect 0):    " + ([regex]'<script').Matches($c).Count
"http(s) links (expect 0):  " + ([regex]'https?://(?!www\.w3\.org)').Matches($c).Count
```

Expected: `svg tags 0`, `seqdiag block 1`, `call msgs 4`, `return msgs 4`, `lifelines 5`, `script tags 0`, `http(s) links 0`.

Then run: `Start-Process "docs/REQUEST_FLOW.html"`
Expected: under "Sequence diagram", five badged participant columns (Controller, Service, Repository, DbContext, PostgreSQL), dashed lifelines, four blue solid call arrows pointing right and four green dashed return arrows pointing left, each with a readable label, plus the legend. Narrowing the window produces a horizontal scrollbar rather than clipped text.

- [ ] **Step 4: Commit**

```bash
git add docs/REQUEST_FLOW.html
git commit -m "docs: replace request-flow sequence SVG with a robust HTML diagram"
```

---

## Final verification (after both tasks)

- [ ] `git diff --stat ARCHITECTURE_OVERVIEW.html REQUEST_FLOW.html` shows changes confined to those two files; the diff removes each `<svg>` block and the architecture lead, and adds the new CSS + HTML — no other content deleted.
- [ ] Open both pages: diagrams render correctly, no overlap, no clipped text; the rest of each page (headers, nav, pager, code cards, the existing vertical request-flow `.node` list, footer) is unchanged.
- [ ] Neither file contains `<svg`, `<script>`, or external URLs (other than the XML namespace, which is now gone with the SVGs).
