# Design: Validation Reference + Module Folder Detail Docs (+ unified-envelope fix)

**Date:** 2026-06-26
**Status:** Approved

## Goal

Deepen the BaseBackend architecture docs in two areas the user asked for: (1) a **folder-by-folder
reference** for each module layer (Domain / Application / Infrastructure / Contracts), and (2) a new
**validation reference** page documenting the architecture's validation layers. As part of the
validation work, make a small code change so **all** validation failures return the same
`ApiResponse<T>` envelope (today the `[ApiController]` automatic ModelState 400 returns a
ProblemDetails body, which is inconsistent with the rest of the stack).

This builds on the existing five-page doc set (`ARCHITECTURE_OVERVIEW`, `SOLUTION_STRUCTURE`,
`PROJECT_LIBRARIES`, `REQUEST_FLOW`, `MODULE_ANATOMY`). It does **not** restructure existing code
beyond the targeted validation-consistency fix.

## Key Decisions

- **Doc layout:** expand the existing `MODULE_ANATOMY.html` with a folder-by-folder section (no
  second overlapping page), and add **one new `VALIDATION.html`** page. Doc set goes 5 → 6 pages.
- **ModelState consistency:** **fix it.** Add an `InvalidModelStateResponseFactory` so binding/ModelState
  failures also return `ApiResponse.Failure("Validation failed", …)`, then document the unified
  behavior. (Chosen over docs-only.)
- **Docs reuse the existing design system** verbatim (inline `<style>`, layer colors, pill tags,
  cards, dark code blocks, inline SVG only) and remain fully offline / self-contained.
- **Mapping is Mapperly, BaseEntity is non-generic** — keep consistent with the corrected docs; never
  write "AutoMapper" and never reference a generic `BaseEntity<TId>`.

### Out of scope (YAGNI)

- No integration/WebApplicationFactory tests (the base intentionally has none); the fix is made
  unit-testable instead.
- No changes to the FluentValidation rules, the domain guards, or the EF constraints themselves.
- No new business module; no auth.

## The five validation layers (the model the docs describe)

| # | Layer | Where it lives | Owns | Failure response |
|---|-------|----------------|------|------------------|
| 1 | Route constraints | `[HttpGet("{id:int}")]` in controllers (Api) | route value shape/type | 404 (no route match) |
| 2 | Model binding / `[ApiController]` | host (via the new `InvalidModelStateResponseFactory`) | missing/malformed/required fields, type conversion | 400 `ApiResponse.Failure("Validation failed", …)` |
| 3 | FluentValidation | `*Validator` in Application, run by `ValidationFilter` (Api) | request-DTO business rules (required, length, ranges) | 400 `ApiResponse.Failure("Validation failed", …)` |
| 4 | Domain invariants | entity methods in Domain → `ExceptionHandlingMiddleware` | invariants that must always hold regardless of entry point | 400 `ApiResponse.Failure(…, "DOMAIN_RULE_VIOLATION")` |
| 5 | Persistence constraints | EF fluent config in Infrastructure | DB-level integrity (max length, required, column type) as defense-in-depth | 500 (DbUpdateException backstop) |

Layers 2–4 all return the same `ApiResponse<T>` envelope after the fix. Layer 1 is a routing concern
(404) and layer 5 is a backstop that mirrors the FluentValidation rules.

## Part 1 — Code fix: unified validation envelope

**File:** `BaseBackend.Api/Extensions/ServiceCollectionExtensions.cs` — chain
`.ConfigureApiBehaviorOptions(...)` onto `AddControllers(...)`:

```csharp
services.AddControllers(o => o.Filters.Add<ValidationFilter>())
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
    .ConfigureApiBehaviorOptions(o =>
        o.InvalidModelStateResponseFactory = ctx =>
            new BadRequestObjectResult(ModelStateResponse.From(ctx.ModelState)));
```

**New file:** `BaseBackend.Api/Validation/ModelStateResponse.cs` — a pure, unit-testable helper:

```csharp
public static class ModelStateResponse
{
    public static ApiResponse<object> From(ModelStateDictionary modelState)
    {
        var errors = string.Join("; ", modelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .Where(m => !string.IsNullOrWhiteSpace(m)));
        return ApiResponse<object>.Failure("Validation failed", errors);
    }
}
```

This reuses the exact `"Validation failed"` message and `ApiResponse<object>.Failure` shape the
`ValidationFilter` already produces, so a missing field (layer 2) and an invalid field (layer 3) are
indistinguishable in shape.

**Test:** new project `tests/BaseBackend.Api.UnitTests` (the host's own test home; versionless refs
under central package management, `IsPackable=false`, references `BaseBackend.Api`). One focused
unit test: construct a `ModelStateDictionary`, `AddModelError("name", "The name field is required.")`,
call `ModelStateResponse.From(...)`, and assert the result has `SuccessStatus == false`,
`Message == "Validation failed"`, and a non-empty `Error` containing the message. Then re-verify the
whole solution builds warning-clean and all tests pass.

## Part 2 — New page: `VALIDATION.html`

Standalone offline HTML in the shared design system. Sections:

1. **Header + banner + legend** — "Validation happens in five layers, from the HTTP edge to the
   database; each owns a different kind of rule." Layer-tag legend (Host / Application / Domain /
   Infrastructure).
2. **Validation-flow diagram** (`.flow` / `.node` / `.arrow`) — a request body traveling top-to-bottom
   through the five layers in pipeline order, each node colored by where it runs and labelled with
   what it checks and the failure response. Communicates the "fail fast, cheapest first" ordering.
3. **Five layer cards** (colored by layer) — each with the real file path, the kind of rule owned, a
   **real code snippet** (route constraint from `ProductsController`; the `ModelStateResponse.From` +
   factory; `CreateProductRequestValidator` + `ValidationFilter`; `Product.ChangePrice` throwing
   `InvalidProductException`; `ProductConfiguration` constraints), the failure response, and when it
   runs in the pipeline.
4. **"Which layer owns this rule?" decision table** — request shape/format/required → FluentValidation;
   an invariant that must always hold regardless of entry point → Domain; uniqueness/referential
   integrity → repository/DB; type/route shape → route constraint.
5. **Unified response shape** — the actual JSON a validation failure returns after the fix
   (`successStatus:false`, `message:"Validation failed"`, `error:"…"`), plus a small table mapping each
   layer → HTTP status + `error` value.
6. **"Add a validation rule" steps** (`ol.steps`) — decide the layer via the table → add/extend the
   FluentValidation validator (shape rules) OR a domain guard throwing a `DomainException`
   (invariants) OR an EF constraint (persistence) → `dotnet test`.
7. **Footer** cross-linking all six pages.

## Part 3 — Expand `MODULE_ANATOMY.html`

Add a **"Folder-by-folder reference"** section after the existing "The four projects" overview (which
stays unchanged). For each of the four layers, a layer-colored card containing a table with columns
**Folder · What lives here · Naming convention · Example**, sourced from the real Catalog code:

- **Domain** — `Entities/` (aggregate roots extending `BaseEntity`, one file per aggregate →
  `Product.cs`); `Exceptions/` (domain exceptions extending `DomainException` →
  `InvalidProductException.cs`).
- **Application** — `DTOs/` (request/response `record`s → `ProductDtos.cs`); `Validators/` (one
  `AbstractValidator<TRequest>` per request → `ProductRequestValidators.cs`); `Mappings/` (Mapperly
  `[Mapper]` partials → `CatalogMapper.cs`); `Services/Interfaces/` (service contracts →
  `IProductService.cs`).
- **Infrastructure** — `Data/` (`<Module>DbContext` + design-time factory); `Configurations/` (one
  `IEntityTypeConfiguration<T>` per entity → `ProductConfiguration.cs`); `Repositories/`
  (`I<Entity>Repository` + impl); `Services/` (`<Entity>Service` + the `<Module>ModuleApi` contracts
  facade); `Migrations/` (EF auto-generated — don't hand-edit); and the root `<Module>Module.cs`
  (the `IModule` entry point).
- **Contracts** — root only, the public `I<Module>Module.cs` interface; no subfolders (kept thin).

Each table makes the naming conventions explicit so laying out a new module is mechanical.

## Part 4 — Cross-page wiring

- Update the **footer on all six pages** so each cross-links the new `VALIDATION.html` (the existing
  five footers currently list five pages; they must list six).
- Add a one-line link to `VALIDATION.html` from `REQUEST_FLOW.html`'s conventions row about
  validation, so readers can jump to the deep dive.

## Constraints (carry over from the base)

- `net10.0` set once in `Directory.Build.props`; no child `.csproj` redeclares it.
- Central package management; no `Version` on `<PackageReference>`; the new test project references
  test packages versionlessly.
- `TreatWarningsAsErrors=true` + NuGet audit on: warning-clean, audit-clean, no suppressions.
- Docs fully offline/self-contained: no external `<link>`/`<script src>`/CDN/web-font; only inline
  `xmlns="http://www.w3.org/2000/svg"` permitted. No "AutoMapper"; no generic `BaseEntity<TId>`.
- Mapping is Mapperly; `ApiResponse<T>` envelope everywhere.

## Success Criteria

- A request body with a missing required field returns the **same** `ApiResponse` envelope
  (`message:"Validation failed"`) as a request that fails FluentValidation — verified by the
  `ModelStateResponse.From` unit test and a clean build.
- `dotnet build` warning-clean and `dotnet test` green, including the new `BaseBackend.Api.UnitTests`.
- `docs/VALIDATION.html` renders standalone (offline) in the BaseProject visual style, with the
  five-layer flow diagram, five layer cards with real code, the decision table, the unified response
  shape, and the "add a rule" steps.
- `docs/MODULE_ANATOMY.html` gains the folder-by-folder reference with explicit naming conventions
  for every layer's folders.
- All six doc pages cross-link each other (footers list all six).
