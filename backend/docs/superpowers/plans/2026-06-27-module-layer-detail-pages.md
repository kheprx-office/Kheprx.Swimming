# Module Layer Detail Pages — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create seven file-by-file deep-dive pages (Domain, Application, Infrastructure, Contracts, SharedKernel, the IModule convention, Host/Api) from the real repo code, and wire them into Module Anatomy with a deep-dives index, read-more links, and a prev/next guided sequence.

**Architecture:** Seven new self-contained offline HTML pages sharing one style/nav/pager shell (Task 1 establishes the canonical shell; Tasks 2–7 copy it verbatim and fill their own content), plus additive edits to `MODULE_ANATOMY.html`. No JavaScript.

**Tech Stack:** Hand-written HTML5 + CSS. Each per-file code block is transcribed (HTML-escaped) from the named real source file. Verification by `Select-String`/`grep` + opening pages in a browser.

## Global Constraints

- **Fully offline:** no JavaScript, no external assets, no CDN/network links.
- **All C# code is the real repo code, transcribed verbatim and HTML-escaped** from the named source file. Escape `<`→`&lt;`, `>`→`&gt;`, `&`→`&amp;` (so `Task<Product?>` → `Task&lt;Product?&gt;`, `price <= 0` → `price &lt;= 0`). Do NOT invent or paraphrase code. Explanations must not contradict the code.
- **Reuse the shared docs visual language**; each page carries its own copy of the canonical `<style>` block + top nav bar + footer. New styling uses literal hex + unique class names.
- `Migrations/` is **summarized**, never dumped.
- Edits to `MODULE_ANATOMY.html` are additive only.
- Dependency facts (verified against the `.csproj` graph): Domain → SharedKernel (referenced by Application, Infrastructure); Application → Domain, SharedKernel (referenced by Infrastructure); Infrastructure → Domain, Application, Contracts, SharedKernel (referenced by Host/Api); Contracts → **nothing** (referenced by Infrastructure); SharedKernel → **nothing** (referenced by all module layers); Host/Api → each module's Infrastructure + SharedKernel.

## Page reading order & pager wiring

`MODULE_DOMAIN` (1) ⟷ `MODULE_APPLICATION` (2) ⟷ `MODULE_INFRASTRUCTURE` (3) ⟷ `MODULE_CONTRACTS` (4) ⟷ `SHAREDKERNEL` (5) ⟷ `IMODULE_CONVENTION` (6) ⟷ `HOST_API` (7). Page 1 Previous → `MODULE_ANATOMY.html`; page 7 Next → `MODULE_ANATOMY.html`.

---

### Task 1: Create the canonical page `docs/MODULE_DOMAIN.html`

This page establishes the shared shell (the `<style>` block, the `<nav class="topnav">`, the back-link, and the `.pager`) that Tasks 2–7 copy verbatim.

**Files:**
- Create: `docs/MODULE_DOMAIN.html`

**Interfaces:**
- Produces: the canonical `<style>` block + nav + pager structure that Tasks 2–7 copy; the link target Task 8 points at.

- [ ] **Step 1: Create `docs/MODULE_DOMAIN.html` with this exact content**

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Domain — BaseBackend.Catalog.Domain</title>
  <style>
  :root {
    --bg: #f6f8fa; --card: #ffffff; --border: #e1e4e8; --text: #1f2328; --muted: #57606a;
    --accent: #2563eb; --accent-soft: #eaf1ff; --code-bg: #0f172a; --code-text: #e2e8f0;
    --dom: #166534; --dom-bg: #dcfce7; --app: #5b21b6; --app-bg: #ede9fe;
    --inf: #9a3412; --inf-bg: #ffedd5; --con: #0f766e; --con-bg: #ccfbf1;
    --host: #2563eb; --host-bg: #eaf1ff; --shared: #475569; --shared-bg: #e2e8f0;
  }
  * { box-sizing: border-box; }
  body { margin: 0; padding: 0 16px 72px; background: var(--bg); color: var(--text);
    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; line-height: 1.6; }
  .wrap { max-width: 980px; margin: 0 auto; }
  .topnav { position: sticky; top: 0; z-index: 50; background: #2563eb; margin: 0 -16px; }
  .topnav .ti { max-width: 980px; margin: 0 auto; padding: 9px 16px; display: flex; flex-wrap: wrap; align-items: center; gap: 2px 4px; }
  .topnav a { text-decoration: none; }
  .topnav .brand { color: #fff; font-weight: 700; font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; margin-right: 12px; font-size: 0.95rem; }
  .topnav .nl { color: #fff; font-size: 0.85rem; padding: 4px 9px; border-radius: 6px; opacity: .9; }
  .topnav .nl:hover { background: rgba(255,255,255,.16); opacity: 1; }
  header.page { padding: 30px 0 18px; border-bottom: 2px solid var(--border); margin-bottom: 8px; }
  header.page h1 { margin: 0 0 6px; font-size: 1.85rem; display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
  header.page h1 .mono { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; }
  header.page p { margin: 0; color: var(--muted); font-size: 0.98rem; }
  .backlink { margin: 14px 0 0; font-size: 0.9rem; }
  .backlink a { color: var(--accent); text-decoration: none; }
  .deps { background: var(--accent-soft); border: 1px solid #c7dbff; border-radius: 12px; padding: 12px 16px; margin: 16px 0; font-size: 0.9rem; }
  .deps b { color: var(--text); }
  h2.section { font-size: 1.18rem; margin: 34px 0 12px; padding-bottom: 6px; border-bottom: 1px solid var(--border); }
  p { font-size: 0.95rem; }
  .tag { display: inline-block; padding: 2px 9px; border-radius: 999px; font-weight: 600; font-size: 0.74rem; letter-spacing: .2px; }
  .tag.dom { color: var(--dom); background: var(--dom-bg); } .tag.app { color: var(--app); background: var(--app-bg); }
  .tag.inf { color: var(--inf); background: var(--inf-bg); } .tag.con { color: var(--con); background: var(--con-bg); }
  .tag.host { color: var(--host); background: var(--host-bg); } .tag.shared { color: var(--shared); background: var(--shared-bg); }
  .card { background: var(--card); border: 1px solid var(--border); border-radius: 12px; padding: 16px 18px; margin: 14px 0; }
  .card.dom { border-left: 5px solid var(--dom); } .card.app { border-left: 5px solid var(--app); }
  .card.inf { border-left: 5px solid var(--inf); } .card.con { border-left: 5px solid var(--con); }
  .card.host { border-left: 5px solid var(--host); } .card.shared { border-left: 5px solid var(--shared); }
  .card .ch { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; margin-bottom: 4px; }
  .card .ch h3 { margin: 0; font-size: 1.02rem; }
  .card .file { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: 0.76rem; color: var(--muted); }
  .card p { margin: 8px 0; font-size: 0.93rem; }
  pre { background: var(--code-bg); color: var(--code-text); border-radius: 10px; padding: 14px 16px; overflow-x: auto; font-size: 0.82rem; line-height: 1.5; margin: 10px 0; font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; }
  code.inline { background: #eef1f4; color: #0f172a; border-radius: 5px; padding: 1px 6px; font-size: 0.86em; font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; }
  .simple { background: #ecfdf5; border: 1px solid #a7f3d0; border-left: 4px solid #166534; border-radius: 10px; padding: 11px 15px; margin: 14px 0; font-size: 0.92rem; }
  .simple b { color: #166534; }
  .pager { display: flex; justify-content: space-between; gap: 12px; margin: 40px 0 0; }
  .pager a { flex: 1 1 0; min-width: 0; border: 1px solid var(--border); background: var(--card); border-radius: 10px; padding: 11px 15px; text-decoration: none; }
  .pager a:hover { border-color: var(--accent); }
  .pager a.next { text-align: right; }
  .pager .dir { display: block; font-size: 0.72rem; color: var(--muted); text-transform: uppercase; letter-spacing: .4px; }
  .pager .ttl { display: block; font-size: 0.96rem; font-weight: 700; color: var(--accent); margin-top: 3px; }
  footer.page { margin-top: 40px; padding-top: 18px; border-top: 1px solid var(--border); color: var(--muted); font-size: 0.86rem; }
  footer.page a { color: var(--accent); }
  </style>
</head>
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
    <header class="page">
      <h1><span class="tag dom">Domain</span> <span class="mono">BaseBackend.Catalog.Domain</span></h1>
      <p>The pure business core of the Catalog module — entities, their invariants, the domain exception, and the repository port. No framework, no database.</p>
    </header>
    <p class="backlink"><a href="MODULE_ANATOMY.html">← Back to Module Anatomy</a></p>
    <div class="deps"><b>Depends on:</b> SharedKernel only. &nbsp;·&nbsp; <b>Referenced by:</b> Application, Infrastructure. &nbsp;·&nbsp; ★ This is the core — it points at nothing outward (see the <a href="ARCHITECTURE_OVERVIEW.html">reference graph</a>).</div>

    <h2 class="section">Files in this project</h2>

    <div class="card dom">
      <div class="ch"><span class="tag dom">Domain</span><h3>Product — the aggregate root</h3></div>
      <div class="file">src/Modules/Catalog/BaseBackend.Catalog.Domain/Entities/Product.cs</div>
      <p>The <code class="inline">Product</code> entity. All properties have <b>private setters</b>; the only way to change state is through guard methods that enforce the business rules.</p>
<pre>using BaseBackend.Catalog.Domain.Exceptions;
using BaseBackend.SharedKernel.Domain;

namespace BaseBackend.Catalog.Domain.Entities;

public sealed class Product : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }

    private Product() { } // EF Core

    public Product(string name, decimal price, string? description = null)
    {
        Rename(name);
        ChangePrice(price);
        UpdateDescription(description);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidProductException("Product name is required.");
        Name = name.Trim();
        Touch();
    }

    public void ChangePrice(decimal price)
    {
        if (price &lt;= 0)
            throw new InvalidProductException("Product price must be greater than zero.");
        Price = price;
        Touch();
    }

    public void UpdateDescription(string? description)
    {
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Touch();
    }
}</pre>
      <p><b>What's happening here:</b> the public constructor routes through <code class="inline">Rename</code>/<code class="inline">ChangePrice</code>/<code class="inline">UpdateDescription</code>, so an invalid <code class="inline">Product</code> can never be built — bad input throws <code class="inline">InvalidProductException</code>. The private parameterless constructor exists only so EF Core can materialise rows. <code class="inline">Product</code> extends <code class="inline">BaseEntity</code> (from SharedKernel), which supplies <code class="inline">Id</code>/<code class="inline">CreatedAt</code>/<code class="inline">UpdatedAt</code> and the <code class="inline">Touch()</code> helper that stamps <code class="inline">UpdatedAt</code>.</p>
    </div>

    <div class="card dom">
      <div class="ch"><span class="tag dom">Domain</span><h3>InvalidProductException</h3></div>
      <div class="file">src/Modules/Catalog/BaseBackend.Catalog.Domain/Exceptions/InvalidProductException.cs</div>
      <p>The domain error thrown when a <code class="inline">Product</code> invariant is violated.</p>
<pre>using BaseBackend.SharedKernel.Domain;

namespace BaseBackend.Catalog.Domain.Exceptions;

public sealed class InvalidProductException : DomainException
{
    public InvalidProductException(string message) : base(message) { }
}</pre>
      <p><b>What's happening here:</b> it extends <code class="inline">DomainException</code> (SharedKernel). The host's <code class="inline">ExceptionHandlingMiddleware</code> catches any <code class="inline">DomainException</code> and turns it into a clean <code class="inline">400</code> response — so domain rule violations become friendly API errors without the domain knowing anything about HTTP.</p>
    </div>

    <div class="card dom">
      <div class="ch"><span class="tag dom">Domain</span><h3>IProductRepository — the port</h3></div>
      <div class="file">src/Modules/Catalog/BaseBackend.Catalog.Domain/Repositories/IProductRepository.cs</div>
      <p>The repository <b>interface</b> — declared here in Domain, implemented in Infrastructure. This inversion is what keeps Domain ignorant of EF Core and the database.</p>
<pre>using BaseBackend.Catalog.Domain.Entities;

namespace BaseBackend.Catalog.Domain.Repositories;

public interface IProductRepository
{
    Task&lt;IReadOnlyList&lt;Product&gt;&gt; GetAllAsync(CancellationToken ct = default);
    Task&lt;Product?&gt; GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(Product product, CancellationToken ct = default);
    void Remove(Product product);
    Task&lt;bool&gt; ExistsAsync(int id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}</pre>
      <p><b>What's happening here:</b> Domain owns the <em>contract</em> ("something must store Products"), and Infrastructure provides the <em>how</em> (<code class="inline">ProductRepository</code>). Application depends on this interface, never on Infrastructure — see <a href="RUNTIME_VS_REFERENCES.html">Two Flows: Runtime vs References</a>.</p>
    </div>

    <div class="simple"><b>In simple words:</b> Domain is the rulebook. It defines what a <code class="inline">Product</code> is, what makes it valid, and what a repository must be able to do — but it never touches a database or the web.</div>

    <div class="pager">
      <a href="MODULE_ANATOMY.html"><span class="dir">← Previous</span><span class="ttl">Module Anatomy</span></a>
      <a class="next" href="MODULE_APPLICATION.html"><span class="dir">Next →</span><span class="ttl">Application</span></a>
    </div>
    <footer class="page">BaseBackend docs — <a href="index.html">Home</a> · <a href="ARCHITECTURE_OVERVIEW.html">Architecture Overview</a> · <a href="SOLUTION_STRUCTURE.html">Solution Structure</a> · <a href="PROJECT_LIBRARIES.html">Project Libraries</a> · <a href="MODULE_ANATOMY.html">Module Anatomy</a> · <a href="REQUEST_FLOW.html">Request Flow</a> · <a href="VALIDATION.html">Validation</a> · <a href="VALIDATION_GUIDE.html">Validation Guide</a></footer>
  </div>
</body>
</html>
```

- [ ] **Step 2: Verify**

Run (PowerShell, from repo root):

```powershell
$c = Get-Content docs/MODULE_DOMAIN.html -Raw
"file cards (expect 3): " + ([regex]'class="card dom"').Matches($c).Count
"pre blocks (expect 3): " + ([regex]'<pre>').Matches($c).Count
"pager (expect 1):      " + ([regex]'class="pager"').Matches($c).Count
"backlink (expect 1):   " + ([regex]'Back to Module Anatomy').Matches($c).Count
"script (expect 0):     " + ([regex]'<script').Matches($c).Count
"external http (0):     " + ([regex]'https?://(?!www\.w3\.org)').Matches($c).Count
```

Expected: cards 3, pre 3, pager 1, backlink 1, script 0, external 0. Then `Start-Process "docs/MODULE_DOMAIN.html"` and confirm it renders in the docs style with the three file cards, the dependency banner, and a Next → Application pager.

- [ ] **Step 3: Commit**

```bash
git add docs/MODULE_DOMAIN.html
git commit -m "docs: add Domain layer detail page (canonical template)"
```

---

### Tasks 2–7: the remaining six detail pages

**Shared procedure for each of Tasks 2–7** (the per-task table below gives the specifics):

1. Create the file by copying `docs/MODULE_DOMAIN.html` as a starting point: keep the entire `<head>` (the full `<style>` block) and the `<nav class="topnav">` block **verbatim**. Change only the `<title>`.
2. Replace the `<header class="page">` (the `<h1>` tag+name and the one-line summary), the `.backlink` stays, and set the `.deps` line from the table.
3. Build one `<div class="card LAYER">` per file in the task's file list — each with: the layer `.tag` + an `<h3>` heading, a `<div class="file">` with the **exact source path**, a one-sentence purpose, a `<pre>` containing the **real code read from that source file** (HTML-escaped per the Global Constraints), and a `<p><b>What's happening here:</b> …</p>` explanation.
4. Add a closing `.simple` "In simple words" recap and any inline cross-links named in the table.
5. Set the `.pager` Previous/Next from the table; keep the standard `footer.page`.
6. Verify: PowerShell count of `class="card …"` cards == number of files in the list, `<pre>` count matches, `class="pager"` == 1, `Back to Module Anatomy` == 1, `<script>` == 0, no external URLs. Spot-check two code blocks against their source files. Then `Start-Process` the page.
7. Commit ONLY that page with the message in the table.

> **Card layer class:** use the page's own layer color for its cards (`card app` on the Application page, `card inf` on Infrastructure, `card con` on Contracts, `card shared` on SharedKernel, `card host` on Host/Api). The IModule page mixes layers — tag each card with the layer the file lives in.

#### Task 2 — `docs/MODULE_APPLICATION.html`
- Title `Application — BaseBackend.Catalog.Application`; `<h1>`: `<span class="tag app">Application</span> BaseBackend.Catalog.Application`; summary: "Use-case orchestration — services, DTOs, validators, and the Mapperly mapper. Depends only on Domain."
- `.deps`: **Depends on:** Domain, SharedKernel. **Referenced by:** Infrastructure.
- Cards (read each source file, transcribe): `Services/Interfaces/IProductService.cs`, `Services/ProductService.cs`, `DTOs/ProductDtos.cs`, `Validators/ProductRequestValidators.cs`, `Mappings/CatalogMapper.cs`. (All under `src/Modules/Catalog/BaseBackend.Catalog.Application/`.)
- Cross-links: `IProductService`/repository → <a href="MODULE_DOMAIN.html">Domain</a>; the implementation that runs at runtime → <a href="REQUEST_FLOW.html">Request Flow</a>.
- Pager: Previous → `MODULE_DOMAIN.html` (Domain); Next → `MODULE_INFRASTRUCTURE.html` (Infrastructure).
- Commit: `docs: add Application layer detail page`

#### Task 3 — `docs/MODULE_INFRASTRUCTURE.html`
- Title `Infrastructure — BaseBackend.Catalog.Infrastructure`; `<h1>`: `<span class="tag inf">Infrastructure</span> BaseBackend.Catalog.Infrastructure`; summary: "The outer shell — EF Core, repositories, entity config, DI registration. Implements the ports the inner layers declare."
- `.deps`: **Depends on:** Domain, Application, Contracts, SharedKernel. **Referenced by:** Host / Api.
- Cards (read+transcribe, all under `src/Modules/Catalog/BaseBackend.Catalog.Infrastructure/`): `CatalogModule.cs`, `Data/CatalogDbContext.cs`, `Data/CatalogDbContextFactory.cs`, `Configurations/ProductConfiguration.cs`, `Repositories/ProductRepository.cs`, `Services/CatalogModuleApi.cs`. Then a **final non-code card** "Migrations/" that *summarizes* the folder (EF Core generated migrations — `InitialCatalog`, the model snapshot — created by `dotnet ef migrations add`; do not reproduce the generated code).
- Cross-links: registration → <a href="IMODULE_CONVENTION.html">The IModule convention</a>; the port it implements → <a href="MODULE_DOMAIN.html">Domain</a>.
- Pager: Previous → `MODULE_APPLICATION.html` (Application); Next → `MODULE_CONTRACTS.html` (Contracts).
- Commit: `docs: add Infrastructure layer detail page`

#### Task 4 — `docs/MODULE_CONTRACTS.html`
- Title `Contracts — BaseBackend.Catalog.Contracts`; `<h1>`: `<span class="tag con">Contracts</span> BaseBackend.Catalog.Contracts`; summary: "The deliberately thin public boundary — the only project other modules may reference."
- `.deps`: **Depends on:** nothing — the `.csproj` is empty. **Referenced by:** Infrastructure (which implements it).
- Cards: `ICatalogModule.cs` (read+transcribe; under `src/Modules/Catalog/BaseBackend.Catalog.Contracts/`). Plus a short non-code card/paragraph explaining the empty `.csproj` (references nothing — keeps the boundary minimal; see the <a href="ARCHITECTURE_OVERVIEW.html">reference graph</a>).
- Cross-links: implemented by <a href="MODULE_INFRASTRUCTURE.html">Infrastructure</a> (`CatalogModuleApi`).
- Pager: Previous → `MODULE_INFRASTRUCTURE.html` (Infrastructure); Next → `SHAREDKERNEL.html` (SharedKernel).
- Commit: `docs: add Contracts layer detail page`

#### Task 5 — `docs/SHAREDKERNEL.html`
- Title `SharedKernel — BaseBackend.SharedKernel`; `<h1>`: `<span class="tag shared">SharedKernel</span> BaseBackend.SharedKernel`; summary: "Cross-cutting building blocks shared by every module — never module-specific."
- `.deps`: **Depends on:** nothing. **Referenced by:** every module layer (and transitively the host).
- Cards (read+transcribe, all under `src/SharedKernel/BaseBackend.SharedKernel/`): `Responses/ApiResponse.cs`, `Responses/BaseApiResponse.cs`, `Domain/BaseEntity.cs`, `Domain/DomainException.cs`, `Modules/IModule.cs`, `Modules/ModuleRegistration.cs`. Tag every card `shared`.
- Cross-links: `IModule`/`ModuleRegistration` → <a href="IMODULE_CONVENTION.html">The IModule convention</a>; `BaseEntity` → <a href="MODULE_DOMAIN.html">Domain</a>.
- Pager: Previous → `MODULE_CONTRACTS.html` (Contracts); Next → `IMODULE_CONVENTION.html` (The IModule convention).
- Commit: `docs: add SharedKernel building blocks detail page`

#### Task 6 — `docs/IMODULE_CONVENTION.html`
- Title `The IModule convention — BaseBackend`; `<h1>`: `<span class="tag inf">Convention</span> The IModule convention`; summary: "How every module self-registers: one interface, one discovery scan, one call in Program.cs."
- `.deps`: a sentence (not the standard deps line): "Lives in SharedKernel · implemented by each module's Infrastructure · invoked by the Host."
- Cards (read+transcribe; tag each card with the layer the file lives in — `shared` for the SharedKernel files, `inf` for CatalogModule, `host` for Program.cs): `src/SharedKernel/BaseBackend.SharedKernel/Modules/IModule.cs` (shared), `src/SharedKernel/BaseBackend.SharedKernel/Modules/ModuleRegistration.cs` (shared — the `Discover`/`AddModules` scan), `src/Modules/Catalog/BaseBackend.Catalog.Infrastructure/CatalogModule.cs` (inf — a real implementation), `BaseBackend.Api/Program.cs` (host — the `AddModules(...)` call).
- Cross-links: <a href="MODULE_INFRASTRUCTURE.html">Infrastructure</a>, <a href="SHAREDKERNEL.html">SharedKernel</a>, <a href="HOST_API.html">Host / Api</a>.
- Pager: Previous → `SHAREDKERNEL.html` (SharedKernel); Next → `HOST_API.html` (Host / Api).
- Commit: `docs: add IModule convention detail page`

#### Task 7 — `docs/HOST_API.html`
- Title `Host / Api — BaseBackend.Api`; `<h1>`: `<span class="tag host">Host / Api</span> BaseBackend.Api`; summary: "The composition root — controllers, the request pipeline, validation, error handling, and module wiring."
- `.deps`: **References:** each module's Infrastructure, SharedKernel. ★ the composition root (see <a href="RUNTIME_VS_REFERENCES.html">Two Flows</a>).
- Cards (read+transcribe; tag each `host`, all under `BaseBackend.Api/`): `Program.cs`, `Controllers/BaseApiController.cs`, `Controllers/ProductsController.cs`, `Filters/ValidationFilter.cs`, `Middlewares/ExceptionHandlingMiddleware.cs`, `Extensions/ServiceCollectionExtensions.cs`, `Extensions/ApplicationBuilderExtensions.cs`, `Validation/ModelStateResponse.cs`.
- Cross-links: <a href="IMODULE_CONVENTION.html">The IModule convention</a>, <a href="REQUEST_FLOW.html">Request Flow</a>, <a href="VALIDATION.html">Validation</a>.
- Pager: Previous → `IMODULE_CONVENTION.html` (The IModule convention); Next → `MODULE_ANATOMY.html` (Module Anatomy).
- Commit: `docs: add Host/Api detail page`

---

### Task 8: Wire the detail pages into `MODULE_ANATOMY.html`

**Files:**
- Modify: `docs/MODULE_ANATOMY.html`

- [ ] **Step 1: Add the "Module internals" deep-dives index**

Read `docs/MODULE_ANATOMY.html` to locate the end of the layer legend block (the `<div class="legend">…</div>` near the top, around line 110, just before `<h2 class="section">Module folder structure</h2>`). Immediately AFTER that legend's closing `</div>` and before the `<h2 class="section">Module folder structure</h2>`, insert:

```html
    <div class="banner"><strong>Module internals — deep dives:</strong>
      <a href="MODULE_DOMAIN.html">Domain</a> ·
      <a href="MODULE_APPLICATION.html">Application</a> ·
      <a href="MODULE_INFRASTRUCTURE.html">Infrastructure</a> ·
      <a href="MODULE_CONTRACTS.html">Contracts</a> ·
      <a href="SHAREDKERNEL.html">SharedKernel</a> ·
      <a href="IMODULE_CONVENTION.html">The IModule convention</a> ·
      <a href="HOST_API.html">Host / Api</a>
    </div>
```

(If `MODULE_ANATOMY.html` does not already define `.banner` in its head `<style>`, add the two rules `.banner { background:#eaf1ff; border:1px solid #c7dbff; border-radius:12px; padding:14px 18px; margin:18px 0; font-size:0.95rem; }` and `.banner strong { color:#2563eb; }` before the head `</style>` — check first to avoid duplicating.)

- [ ] **Step 2: Add "Read more →" links to the relevant sections**

In the "The four projects" section, append a read-more link inside each of the four project cards (after the card's existing content, before its closing `</div>`):
- Domain card → `<p class="more"><a href="MODULE_DOMAIN.html">Read more — Domain in detail →</a></p>`
- Application card → `<p class="more"><a href="MODULE_APPLICATION.html">Read more — Application in detail →</a></p>`
- Infrastructure card → `<p class="more"><a href="MODULE_INFRASTRUCTURE.html">Read more — Infrastructure in detail →</a></p>`
- Contracts card → `<p class="more"><a href="MODULE_CONTRACTS.html">Read more — Contracts in detail →</a></p>`

In the "SharedKernel building blocks" section card, append `<p class="more"><a href="SHAREDKERNEL.html">Read more — SharedKernel in detail →</a></p>`.
In the "The IModule convention" section card, append `<p class="more"><a href="IMODULE_CONVENTION.html">Read more — the IModule convention in detail →</a></p>`.

Add one CSS rule before the head `</style>`: `.more { margin: 10px 0 0; font-size: 0.86rem; } .more a { color: #2563eb; text-decoration: none; font-weight: 600; }`

Read the file first to find each card's exact closing tag; insert additively (change no existing text).

- [ ] **Step 3: Verify**

Run (PowerShell, from repo root):

```powershell
$c = Get-Content docs/MODULE_ANATOMY.html -Raw
"internals index (expect 1): " + ([regex]'Module internals — deep dives').Matches($c).Count
"read-more links (expect 6): " + ([regex]'class="more"').Matches($c).Count
'MODULE_DOMAIN.html','MODULE_APPLICATION.html','MODULE_INFRASTRUCTURE.html','MODULE_CONTRACTS.html','SHAREDKERNEL.html','IMODULE_CONVENTION.html','HOST_API.html' | ForEach-Object { if (Test-Path "docs/$_") { "page OK: $_" } else { "page MISSING: $_" } }
```

Expected: internals index 1, read-more 6, seven `page OK` lines. Open `MODULE_ANATOMY.html`, click the index links and the read-more links — all resolve.

- [ ] **Step 4: Commit**

```bash
git add docs/MODULE_ANATOMY.html
git commit -m "docs: link the layer detail pages from Module Anatomy (index + read-more)"
```

---

## Final verification (after all tasks)

- [ ] All seven pages open standalone, styled, with a working nav bar, dependency line, file-by-file cards (each with real code), a "← Back to Module Anatomy" link, and a prev/next pager; the seven pager-chain reads through Domain → … → Host/Api and back to Module Anatomy.
- [ ] Spot-check several code cards against their source files — code matches verbatim (escaped).
- [ ] `MODULE_ANATOMY.html` shows the internals index (7 links) + read-more links (6); all resolve.
- [ ] No `<script>`, no external URLs in any new page; `Migrations/` is summarized, not dumped.
