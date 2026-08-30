# Kheprx.BaseBackend

A reusable **.NET 10 modular-monolith** backend base — Clean-Architecture layering, explicitly registered modules, and batteries-included cross-cutting concerns. Use it as the starting point for a new service.

## Features

- **Modular monolith** — each feature is a self-contained module (`Domain` / `Application` / `Infrastructure` / `Contracts`). Modules are **explicitly registered** at startup — each module's Infrastructure project exposes an `Add<Name>Module` extension; `Program.cs` calls each one under a `// Module registrations` comment.
- **Clean-Architecture boundaries, enforced by tests** — dependencies point inward to the Domain core; `ArchitectureTests` fail the build if a layer reaches where it shouldn't.
- **Validation in depth** — FluentValidation on request DTOs + domain invariants in entities.
- **EF Core + PostgreSQL** (Npgsql), with per-module `DbContext` and migrations.
- **Mapperly** source-generated entity↔DTO mapping (no runtime reflection).
- **Consistent API envelope** — every endpoint returns `ApiResponse<T>` (`Success`/`Failure`).
- **Global exception handling** — domain errors become clean `400`s; everything else a safe `500`.
- **Serilog** structured logging and **Swagger** UI (Development).
- **Offline architecture docs** — a full set of standalone HTML guides under `docs/`.

## Tech stack

.NET 10 · ASP.NET Core · EF Core + Npgsql (PostgreSQL) · FluentValidation · Riok.Mapperly · Serilog · Swashbuckle (Swagger).

## Getting started

**Prerequisites:** .NET 10 SDK, PostgreSQL.

1. Set the connection string `ConnectionStrings:Postgres` in `Kheprx.BaseBackend.Api/appsettings.json`.
2. Apply migrations (see below) or let the app create the schema as configured.
3. Run the API:
   ```bash
   dotnet run --project Kheprx.BaseBackend.Api
   ```
4. Open Swagger (Development only): `https://localhost:<port>/swagger`.

## Documentation

Open **`docs/index.html`** in a browser — a guided, fully offline set of guides: Architecture Overview, Solution Structure, Project Libraries, Module Anatomy (with per-layer deep-dives: Domain, Application, Infrastructure, Contracts, SharedKernel, Host/Api), Request Flow, Runtime vs Project References, and the Validation guides.

## Project structure

```
Kheprx.BaseBackend.Api/              # Host: controllers, pipeline, module wiring (composition root)
src/
  SharedKernel/
    Kheprx.BaseBackend.SharedKernel/ # ApiResponse<T>, BaseEntity, DomainException
  Modules/
    Catalog/                         # Example module (four projects)
      Kheprx.BaseBackend.Catalog.Domain/
      Kheprx.BaseBackend.Catalog.Application/
      Kheprx.BaseBackend.Catalog.Infrastructure/
      Kheprx.BaseBackend.Catalog.Contracts/
tests/                               # Unit + Architecture/boundary tests
docs/                                # Offline HTML documentation
```

## Add a module

1. Create four projects under `src/Modules/<Name>/`: `.Domain`, `.Application`, `.Infrastructure`, `.Contracts`.
2. In `.Infrastructure/Extensions/`, add a `public static class <Name>ModuleExtensions` with an `Add<Name>Module(this IServiceCollection, IConfiguration)` method that wires the module's `DbContext` and services.
3. Reference the new `.Infrastructure` project from `Kheprx.BaseBackend.Api`.
4. In `Program.cs`, add `builder.Services.Add<Name>Module(builder.Configuration);` under the `// Module registrations` comment.

## Migrations

```bash
dotnet ef migrations add <Name> \
  --project src/Modules/<Name>/Kheprx.BaseBackend.<Name>.Infrastructure \
  --startup-project Kheprx.BaseBackend.Api \
  --context <Name>DbContext --output-dir Migrations

dotnet ef database update \
  --project src/Modules/<Name>/Kheprx.BaseBackend.<Name>.Infrastructure \
  --startup-project Kheprx.BaseBackend.Api --context <Name>DbContext
```

## Testing

```bash
dotnet test Kheprx.BaseBackend.sln
```
Runs the unit tests and the architecture/boundary tests (which enforce the layering and module conventions).

## Security notes

The template ships a permissive `AllowAll` CORS policy (any origin, header, and method). **Tighten this before deploying to production** by restricting allowed origins in `Kheprx.BaseBackend.Api/Extensions/ServiceCollectionExtensions.cs`.

## License

[MIT](LICENSE) © 2026 Kheprx (CodeLab Systems).
