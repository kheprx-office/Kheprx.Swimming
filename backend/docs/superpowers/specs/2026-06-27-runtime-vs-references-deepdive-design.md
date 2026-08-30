# Runtime vs Project References — Deep-Dive Page Design

**Date:** 2026-06-27
**Status:** Approved design, ready for implementation plan

## Goal

Add a standalone, beginner-friendly explainer page that teaches why the **runtime
request flow** (`API → Application → Infrastructure → Database`) and the **compile-time
project-reference flow** (`API → Infrastructure → Application → Domain`) are two
different things — answering the common "why does the host reference Infrastructure, not
Application?" question. The page is reached as a contextual deep-dive (a "Learn more"
link), not as part of the main reading path.

## Audience & teaching style (the core requirement)

Detailed **and** easy to understand. Every concept is taught, not just shown:

- Each code block is followed by a plain-English **"What's happening here"** explanation.
- Hard ideas get a short **analogy**:
  - *Composition root* → the electrician who, at startup, wires every plug (interface)
    to the right socket (implementation) so the appliances just work.
  - *Dependency inversion* → Application orders from a **catalog** (`IProductRepository`);
    Infrastructure is the **warehouse** that ships the real item.
  - *DI resolution* → a step-by-step "the container builds Controller → needs
    `IProductService` → builds `ProductService` → needs `IProductRepository` → builds
    `ProductRepository` → needs `CatalogDbContext` → ..." walkthrough.
- **"In simple words"** one-sentence recaps close each section.
- Two diagrams in the existing docs style + a side-by-side comparison.
- A friendly intro that validates the reader's instinct ("Your thinking is correct —
  here's the subtlety").

## Accuracy: use the real code

All code on the page is copied from the actual repo (not illustrative), and one
refinement over common explanations is made explicit: **`IProductRepository` is a Domain
port** (declared in `BaseBackend.Catalog.Domain.Repositories`, implemented in
Infrastructure) — that is the exact mechanism that lets Application stay ignorant of
Infrastructure. `IProductService` is declared in Application.

Real snippets to use verbatim:

```csharp
// Host / Api — BaseBackend.Api/Controllers/ProductsController.cs
public sealed class ProductsController : BaseApiController
{
    private readonly IProductService _service;
    public ProductsController(IProductService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProductDto>>>> GetAll(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ProductDto>>.Success(
            "Products retrieved", await _service.GetAllAsync(ct)));
}
```

```csharp
// Application — .../Application/Services/ProductService.cs
internal sealed class ProductService : IProductService
{
    private readonly IProductRepository _repository;   // a Domain port — an interface
    private readonly CatalogMapper _mapper;
    public ProductService(IProductRepository repository, CatalogMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct = default)
        => _mapper.ToDtoList(await _repository.GetAllAsync(ct));
}
```

```csharp
// Infrastructure — .../Infrastructure/Repositories/ProductRepository.cs
internal sealed class ProductRepository : IProductRepository
{
    private readonly CatalogDbContext _db;
    public ProductRepository(CatalogDbContext db) => _db = db;

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default)
        => await _db.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync(ct);
}
```

```csharp
// Infrastructure — .../Infrastructure/CatalogModule.cs  (the module's DI registration)
public sealed class CatalogModule : IModule
{
    public IServiceCollection Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CatalogDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));
        services.AddSingleton<CatalogMapper>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICatalogModule, CatalogModuleApi>();
        return services;
    }
}
```

```csharp
// Host — BaseBackend.Api/Program.cs
builder.Services.AddModules(builder.Configuration);   // discovers every IModule and calls Register(...)
```

## Component 1 — The new page (`docs/RUNTIME_VS_REFERENCES.html`)

A complete standalone offline HTML page reusing the shared docs `<style>` block (so it
has the same `:root` palette, `.card`, `.tag`, `.node`/`.arrow`, `.banner`, `pre`,
`code.inline`, `ol.steps`, `table.conv`, and `footer.page` styling) plus the shared
**top nav bar** (current item: none highlighted, since it's not a nav page) and the
**footer**. **No** prev/next pager. **Not** added to the nav bar's item list.

Title: `BaseBackend — Two Flows: Runtime vs Project References`.

Section outline (top to bottom):

1. **Header + intro banner** — "Your thinking is correct — there are *two different
   flows*. This page untangles them." Names the two flows up front.
2. **1 · Runtime request flow** — a vertical `.node`/`.arrow` flow, each node layer-tagged:
   HTTP request → `ProductsController` (Host/Api) → `IProductService` → `ProductService`
   (Application) → `IProductRepository` *(Domain port)* → `ProductRepository`
   (Infrastructure) → `CatalogDbContext` (Infrastructure) → PostgreSQL (External). Then
   the **dependency-inversion detail** using the real `ProductService` and
   `ProductRepository` code + the catalog/warehouse analogy + an "In simple words" recap.
3. **2 · Compile-time / project-reference flow** — the reference chain
   `Api → Infrastructure → Application → Domain` (noting it is transitive, with a link to
   the full **reference graph** on the Architecture page). Explains *why*: Infrastructure
   holds the module registration — show the real `CatalogModule.Register(...)` and
   `Program.cs` `AddModules(...)`.
4. **The key difference** — a side-by-side (two cards / two columns):
   *Runtime:* `API → Application → Infrastructure → Database`  ·
   *References:* `API → Infrastructure → Application → Domain`. One line: "They are not
   the same diagram."
5. **Why the API references Infrastructure** — the host is the **composition root**
   (where DI is configured). Walk the DI resolution as an `ol.steps` list, ending: the
   controller asks for `IProductService`, DI hands it `ProductService`; `ProductService`
   asks for `IProductRepository`, DI hands it `ProductRepository`.
6. **The expert rule** — the controller depends on the Application abstraction
   `IProductService` ✅ (real `ProductsController`), **not** `CatalogDbContext` ❌ or
   `ProductRepository` ❌. A short do/don't.
7. **Simple mental model** — "API calls Application · Application asks for an interface ·
   Infrastructure implements it · DI connects them at startup." Plus cross-links: "See
   the compile-time **reference graph** (Architecture) and the runtime **Request Flow**."

## Component 2 — Links from existing pages

- **`docs/ARCHITECTURE_OVERVIEW.html`** — extend the existing "Compile-time, not runtime"
  banner with a trailing **"Learn more — runtime vs references →"** link to
  `RUNTIME_VS_REFERENCES.html`.
- **`docs/REQUEST_FLOW.html`** — add a one-line note (near the sequence-diagram section)
  linking to `RUNTIME_VS_REFERENCES.html` for "why the project references differ from
  this call order."

## Constraints

- **Fully offline:** no JavaScript, no external assets, no CDN/network links.
- **Reuse the shared docs style** (`:root` palette + existing component classes) and the
  shared top-nav-bar markup/CSS and footer — copied into this standalone file the same
  way every other page carries its own copy. Any genuinely new styling (e.g. the
  side-by-side compare, "In simple words" callout) is scoped under a wrapper class and
  uses literal hex.
- **All code is the real repo code** (verbatim); explanations must not contradict it.
- Edits to the two existing pages are **additive** (one link each) — no other content
  changes; nav/pager/footer/other sections untouched.

## Files changed

- **New:** `docs/RUNTIME_VS_REFERENCES.html`.
- **Edited:** `docs/ARCHITECTURE_OVERVIEW.html` (append a link to the banner),
  `docs/REQUEST_FLOW.html` (add a one-line link note).

## Non-goals (YAGNI)

- Not added to the top nav bar's items, the landing-page cards, or the prev/next reading
  path (it is a contextual deep-dive).
- No JavaScript, no diagram libraries, no build step.
- No change to the actual `.csproj` or source code (docs only).
- No new diagrams beyond the runtime ladder, the reference chain, and the side-by-side
  compare.

## Verification

- Open `docs/RUNTIME_VS_REFERENCES.html`: it renders in the docs style with the top nav
  bar and footer, no pager; all seven sections present; the runtime ladder and reference
  chain render; code blocks match the repo; cross-links resolve.
- From `docs/ARCHITECTURE_OVERVIEW.html`, the "Compile-time, not runtime" banner's
  "Learn more →" opens the new page; from `docs/REQUEST_FLOW.html`, the note link opens it.
- The new page's "Learn more"/footer links back to Architecture and Request Flow resolve.
- View source: no `<script>`, no external URLs; the page is self-contained.
