# Module Layer Detail Pages — Design

**Date:** 2026-06-27
**Status:** Approved design, ready for implementation plan

## Goal

The Module Anatomy page summarizes each layer in a card, but the user wants to drill in.
Add **seven standalone detail pages** — one per layer / building block — each a thorough,
beginner-friendly, file-by-file walkthrough of that project using the **real repo code**.
Link them from Module Anatomy and thread them into a guided read-through sequence.

## The seven pages (reading order) and the real files each walks through

All code is transcribed verbatim (HTML-escaped) from these actual source files.

| # | Page (new file) | Project | Files walked through |
|---|-----------------|---------|----------------------|
| 1 | `docs/MODULE_DOMAIN.html` | BaseBackend.Catalog.Domain | `Entities/Product.cs`, `Exceptions/InvalidProductException.cs`, `Repositories/IProductRepository.cs` |
| 2 | `docs/MODULE_APPLICATION.html` | BaseBackend.Catalog.Application | `Services/Interfaces/IProductService.cs`, `Services/ProductService.cs`, `DTOs/ProductDtos.cs`, `Validators/ProductRequestValidators.cs`, `Mappings/CatalogMapper.cs` |
| 3 | `docs/MODULE_INFRASTRUCTURE.html` | BaseBackend.Catalog.Infrastructure | `CatalogModule.cs`, `Data/CatalogDbContext.cs`, `Data/CatalogDbContextFactory.cs`, `Configurations/ProductConfiguration.cs`, `Repositories/ProductRepository.cs`, `Services/CatalogModuleApi.cs`, + a summary note on `Migrations/` (do NOT dump generated migration code) |
| 4 | `docs/MODULE_CONTRACTS.html` | BaseBackend.Catalog.Contracts | `ICatalogModule.cs` (+ explain why the `.csproj` is empty / references nothing) |
| 5 | `docs/SHAREDKERNEL.html` | BaseBackend.SharedKernel | `Responses/ApiResponse.cs`, `Responses/BaseApiResponse.cs`, `Domain/BaseEntity.cs`, `Domain/DomainException.cs`, `Modules/IModule.cs`, `Modules/ModuleRegistration.cs` |
| 6 | `docs/IMODULE_CONVENTION.html` | (cross-project) | `Modules/IModule.cs`, `Modules/ModuleRegistration.cs` (the `Discover`/`AddModules` mechanism), `CatalogModule.cs` (a real implementation), `BaseBackend.Api/Program.cs` (`AddModules` call) |
| 7 | `docs/HOST_API.html` | BaseBackend.Api | `Program.cs`, `Controllers/BaseApiController.cs`, `Controllers/ProductsController.cs`, `Filters/ValidationFilter.cs`, `Middlewares/ExceptionHandlingMiddleware.cs`, `Extensions/ServiceCollectionExtensions.cs`, `Extensions/ApplicationBuilderExtensions.cs`, `Validation/ModelStateResponse.cs` |

The implementer reads each named source file and transcribes its real code (escaped) — the
source files are the single source of truth; the pages must not invent or paraphrase code.

## Detail-page template (every page follows this)

A complete standalone offline HTML page reusing the shared docs `<style>` block + the
shared top nav bar (no item active) + the standard footer. Structure top to bottom:

1. **Header** — `<h1>` = the project name with its layer `.tag`; a one-line "what it is."
2. **Dependency line** — a short `.banner` or line: "Depends on … · Referenced by …", using
   the real `.csproj` facts (below). Domain: depends on SharedKernel; referenced by
   Application + Infrastructure. Application: depends on Domain, SharedKernel; referenced by
   Infrastructure. Infrastructure: depends on Domain, Application, Contracts, SharedKernel;
   referenced by Host/Api. Contracts: depends on **nothing** (empty `.csproj`); referenced by
   Infrastructure. SharedKernel: depends on **nothing**; referenced by all module layers
   (and transitively the host). Host/Api: references each module's Infrastructure + SharedKernel.
3. **File-by-file walkthrough** — one `.card` per file: the file path (in the muted mono
   `.file` style), a sentence on what it does, the real code in a `<pre>`, and a plain-English
   **"What's happening here"** paragraph. Beginner-friendly.
4. **"In simple words"** recap (`.simple` callout, as on the runtime-vs-references page) and any
   layer-specific conventions/rules.
5. **Footer area** — a **"← Back to Module Anatomy"** link, inline cross-links to related pages
   (e.g. Application → Domain; IModule → Infrastructure & Host), and a **prev/next pager**
   through the seven pages, plus the standard `footer.page` link row.

## Navigation & wiring

- **Prev/next pager** threads the seven in the table's order. Page 1 (Domain) Previous →
  `MODULE_ANATOMY.html`; page 7 (Host/Api) Next → `MODULE_ANATOMY.html`; the rest point to
  their neighbors. (Same pager component/style as the main doc pages.)
- **`docs/MODULE_ANATOMY.html` edits (additive only):**
  - A **"Module internals — deep dives"** index near the top (after the intro/legend): a short
    list/grid linking all seven pages.
  - A **"Read more →"** link appended to the relevant existing sections: the four project cards
    in "The four projects" (→ their pages), the "SharedKernel building blocks" section (→
    `SHAREDKERNEL.html`), and "The IModule convention" section (→ `IMODULE_CONVENTION.html`).
    (Host/Api has no existing section; it is reached via the internals index and the pager.)
- The seven pages are **not** added to the top nav bar's item list.

## Constraints

- **Fully offline:** no JavaScript, no external assets, no CDN/network links.
- **Reuse the shared docs visual language** (`:root` palette + `.card`/`.tag`/`pre`/`code.inline`/
  `.file`/`.banner`/`.simple`/pager classes + top nav + footer), each page carrying its own copy
  of the style block as every doc page does. New-only styling uses literal hex + unique class names.
- **All C# is the real repo code, verbatim (HTML-escaped); explanations must not contradict it.**
  Dependency facts match the actual `.csproj` graph.
- `Migrations/` is **summarized**, never dumped (generated code).
- Edits to `MODULE_ANATOMY.html` are additive (the index + read-more links); no other content,
  page, or `.csproj` changes.

## Files changed

- **New (7):** `MODULE_DOMAIN.html`, `MODULE_APPLICATION.html`, `MODULE_INFRASTRUCTURE.html`,
  `MODULE_CONTRACTS.html`, `SHAREDKERNEL.html`, `IMODULE_CONVENTION.html`, `HOST_API.html`.
- **Edited (1):** `MODULE_ANATOMY.html` (internals index + read-more links).

## Non-goals (YAGNI)

- Not added to the top nav items; no nav dropdown; no JS.
- No new pages beyond these seven; the "Mapperly mapper" and "Add a new module" sections stay
  inline on Module Anatomy (Mapperly is also covered in the Application page's `CatalogMapper` card).
- No changes to source code or `.csproj` files (docs only).
- Generated migration code is not reproduced.

## Verification

- Each of the seven pages opens standalone in the docs style with the top nav bar + footer; the
  dependency line is correct; every named source file appears as a card with its real code and an
  explanation; the prev/next pager and "← Back to Module Anatomy" link resolve; cross-links work.
- The code in each card matches the corresponding real source file (spot-check signatures).
- `MODULE_ANATOMY.html` shows the internals index (7 links) and a "Read more →" link on each of
  the four project cards, SharedKernel, and IModule; all resolve.
- View source on every new page: no `<script>`, no external URLs; self-contained.
