# Design: .NET Backend Base Architecture (Modular Monolith)

**Date:** 2026-06-26
**Status:** Approved

## Goal

Create a reusable **.NET backend base** — a modular-monolith starter template — modeled on the
existing `IslamicApplicationVersionTwo` project, with a first-class set of architecture docs in the
visual style of the `BaseProject` mobile docs. The base keeps the Islamic app's familiar patterns
exactly, while adding the few improvements that make a template better than a copied app.

This is the backend counterpart to the existing React Native mobile base.

## Reference Sources

- **Architecture template:** `C:\Users\envnt\Desktop\DotNetCourseApi\IslamicApplicationVersionTwo`
  (a mature .NET 8 modular monolith).
- **Documentation style:** `C:\Users\envnt\Desktop\BaseProject\docs`
  (standalone offline HTML pages with a shared inline-CSS design system).

## Key Decisions

- **Scope:** Skeleton (all plumbing) + **one** generic sample module. No auth in the base.
- **Patterns:** Mirror the Islamic app **exactly** — Controllers + Service layer + Repository
  pattern (no MediatR/CQRS), FluentValidation, `ApiResponse<T>` envelope, per-module
  `DbContext`, `BaseApiController`, Accept-Language localization. **Mapping uses Mapperly**
  (source-generated) instead of the Islamic app's AutoMapper — AutoMapper went commercial in 2025
  and its last free line carries an unpatched DoS advisory (GHSA-rvv3-g6hj-g44x); Mapperly is free
  (MIT), compile-time, and dependency-light. This is the one deliberate deviation from "mirror exactly."
- **Naming / namespace prefix:** `BaseBackend`. Every project is `BaseBackend.<Module>.<Layer>`
  (e.g. `BaseBackend.Catalog.Domain`); host is `BaseBackend.Api`; shared is `BaseBackend.SharedKernel`.
- **Target framework:** `.NET 10` (current LTS). Patterns identical to the Islamic app; package
  versions bump to 10.x.
- **Database:** PostgreSQL via Npgsql, per-module `DbContext`, one shared connection string.
- **Template-grade improvements (approved):** central package management
  (`Directory.Packages.props`), shared build props (`Directory.Build.props`), an `IModule`
  registration convention, architecture tests enforcing boundaries, plus `global.json`,
  `.editorconfig`, `.gitignore`, and `README.md`.
- **Sample module:** `Catalog` (a simple Product CRUD) demonstrating the full four-project vertical.

### Out of scope (YAGNI)

- Authentication / authorization (no JWT, no API-key middleware, no Identity module).
- Additional business modules beyond the single `Catalog` sample.
- Integration / end-to-end test suites (unit + architecture tests only).
- CI/CD, containerization, deployment configuration.

## Solution Structure

```
BaseBackend/
├── BaseBackend.sln
├── global.json                 # pins the .NET 10 SDK version
├── Directory.Build.props       # shared: net10.0, Nullable, ImplicitUsings, analyzers
├── Directory.Packages.props    # ALL NuGet versions, declared once (central)
├── .editorconfig               # formatting/style rules
├── .gitignore                  # standard VS/.NET ignores (bin, obj, .vs, publish)
├── README.md                   # what the base is + how to run / add a module
│
├── BaseBackend.Api/            # web host (at root, matches the Islamic app)
│   ├── Controllers/BaseApiController.cs
│   ├── Extensions/             # AddSwagger, AddSerilog, AddFluentValidation, Cors, Mvc…
│   ├── Filters/                # ValidationExceptionFilter, request-logging filter
│   ├── Middlewares/            # minimal (no auth)
│   ├── appsettings*.json
│   └── Program.cs
│
├── src/
│   ├── SharedKernel/
│   │   └── BaseBackend.SharedKernel/
│   └── Modules/
│       └── Catalog/
│           ├── BaseBackend.Catalog.Domain/
│           ├── BaseBackend.Catalog.Application/
│           ├── BaseBackend.Catalog.Infrastructure/
│           └── BaseBackend.Catalog.Contracts/
│
├── tests/
│   ├── BaseBackend.ArchitectureTests/
│   └── BaseBackend.Catalog.UnitTests/
│
└── docs/                       # architecture docs (see below)
```

### Template-grade improvements, concretely

- **`Directory.Packages.props`** — every package + version listed once; each `.csproj` references
  packages with no inline version. Replaces the Islamic app's per-`.csproj` version sprawl.
- **`Directory.Build.props`** — sets `net10.0`, `Nullable`, `ImplicitUsings`, and warnings policy
  once for all projects.
- **`IModule` convention** — lives in SharedKernel; each module implements a small registration
  contract; the host discovers and registers all modules in one call (no `Program.cs` edits to add
  a module).
- **`global.json` + `.editorconfig`** — pin the SDK and enforce consistent style across the template.

## SharedKernel (`BaseBackend.SharedKernel`)

Cross-module foundation; no business logic.

- **`Responses/`** — `BaseApiResponse` (success flag, message, error) and `ApiResponse<T>` with
  `Success(...)` / `Failure(...)` factories — the envelope every endpoint returns.
- **`Domain/`** — `BaseEntity` (Id, `CreatedAt`, `UpdatedAt`) and a `DomainException` base type.
- **`Modules/`** — the **`IModule`** convention:
  `IServiceCollection Register(IServiceCollection services, IConfiguration config)`. A
  `ModuleRegistration` extension scans loaded assemblies for `IModule` implementations and registers
  them all.
- **`Errors/` + `Resources/`** — typed error codes and a localization base (Accept-Language →
  message), matching the Islamic app's `en-US` / `ar-EG` switch pattern.

## Sample Module — `Catalog` (Product CRUD)

Demonstrates the full four-project vertical; cloning it equals adding a feature.

| Project | Contents |
|---|---|
| **`Catalog.Domain`** | `Product` entity with behavior (`Rename()`, `ChangePrice()`, `Validate()` throwing `InvalidProductException`), `Entities/`, `Exceptions/` |
| **`Catalog.Application`** | `DTOs/` (CreateProductRequest, UpdateProductRequest, ProductDto), `Validators/` (FluentValidation), `Mappings/` (Mapperly `[Mapper]` partial class), `Services/Interfaces/IProductService.cs`, `Resources/ProductMessages.cs` |
| **`Catalog.Infrastructure`** | `Data/CatalogDbContext.cs`, `Configurations/ProductConfiguration.cs` (EF fluent), `Repositories/` (IProductRepository + impl), `Services/ProductService.cs`, `CatalogModule.cs` (implements `IModule`) |
| **`Catalog.Contracts`** | `ICatalogModule` — the only thing other modules may reference (e.g. `ProductExistsAsync(id)`) |

**Request flow** (identical to the Islamic app):
`ProductsController : BaseApiController` → `IProductService` → `IProductRepository` →
`CatalogDbContext` → PostgreSQL, returning `ApiResponse<ProductDto>`. Validation runs via the global
`ValidationExceptionFilter`; mapping via Mapperly (source-generated).

## Host Wiring (`Program.cs`)

```
builder.Services.AddModules(builder.Configuration);   // scans & registers all IModule modules
builder.Services.AddSharedInfrastructure();           // Swagger, Serilog, FluentValidation, CORS, Mvc, ApiResponse filters
var app = builder.Build();
app.UseSharedPipeline(app.Environment);               // exception handling, logging, swagger, controllers
```

Adding a new module = create the four projects + implement `IModule` → auto-registered. No
`Program.cs` edits.

## Database & Migrations

- **Per-module `DbContext`** (`CatalogDbContext` owns only Catalog tables), PostgreSQL via Npgsql,
  one shared connection string in `appsettings.json`.
- EF fluent configs applied via `ApplyConfigurationsFromAssembly`.
- Migrations live **inside each module's Infrastructure project** (`Migrations/`) and run per-module,
  so modules stay independent. The README documents the `dotnet ef` command with the
  `--project` / `--startup-project` pair.

## Testing

- **`ArchitectureTests`** (NetArchTest + xUnit) — enforces: Domain references no Infrastructure;
  modules reference each other only via `.Contracts`; controllers inherit `BaseApiController`.
- **`Catalog.UnitTests`** — sample tests for `Product` domain behavior, the validator, and
  `ProductService` (repository mocked) — the pattern to copy per module.

## Documentation Set (`docs/`)

Reuses the BaseProject visual design system: standalone offline HTML, inline CSS via `:root`
variables, color-coded layers, pill tags, cards with colored left borders, dark syntax-highlighted
code blocks, and SVG sequence diagrams.

**Layer color legend (consistent across every page):** Domain = green, Application = purple,
Infrastructure = orange, Contracts = teal, Host/Api = blue, SharedKernel = slate.

Five HTML pages:

1. **`ARCHITECTURE_OVERVIEW.html`** — landing page. Explains modular monolith as a concept, the
   host + SharedKernel + modules picture (SVG diagram), the four-layer-per-module model, and the
   dependency rules (who may reference whom).
2. **`SOLUTION_STRUCTURE.html`** — card-per-file/folder walkthrough of the tree with the tag system
   (**You edit** / **Auto-generated** / **Set-once config**), plus a "mental model" section.
3. **`PROJECT_LIBRARIES.html`** — `Directory.Packages.props` / `.csproj` anatomy in a code block,
   then tables: runtime packages (EF Core, Npgsql, FluentValidation, Mapperly, Serilog,
   Swashbuckle) vs test packages (xUnit, NetArchTest, Moq), each with version badge + description.
4. **`REQUEST_FLOW.html`** — worked example **GET /api/products** traveling
   Controller → Service → Repository → DbContext → PostgreSQL and back as `ApiResponse<T>`. Includes
   a vertical flow diagram, an SVG sequence diagram, per-layer cards with real code snippets + file
   paths, a conventions table, and a numbered "Add a new endpoint" checklist.
5. **`MODULE_ANATOMY.html`** — the four-project module shape with a card per project, the `IModule`
   auto-registration convention, the SharedKernel building blocks, and a step-by-step "Add a new
   module" guide.

The HTML docs are hand-authored content describing the real structure; they are written/finalized
during implementation once the actual files exist, so every snippet quotes authentic code (not
pseudocode), matching how the BaseProject docs are built.

## Tech Stack

- **.NET 10** (LTS), C# with Nullable + ImplicitUsings.
- **EF Core 10** + **Npgsql** (PostgreSQL).
- **FluentValidation**, **Mapperly** (Riok.Mapperly, source-generated mapping), **Serilog**, **Swashbuckle** (Swagger).
- **xUnit**, **NetArchTest**, **Moq** (tests).
- Central package management via `Directory.Packages.props`.

## Success Criteria

- `dotnet build` succeeds on the full solution; `dotnet test` runs unit + architecture tests green.
- The `Catalog` module exposes working CRUD endpoints returning the `ApiResponse<T>` envelope, backed
  by PostgreSQL through EF Core.
- Adding a new module requires only: create the four projects + implement `IModule` (no `Program.cs`
  edits), confirmed by the architecture tests.
- All NuGet versions are declared once in `Directory.Packages.props`.
- The five HTML docs render standalone (offline) in the BaseProject visual style and quote real code.
```
