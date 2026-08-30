# Explicit Per-Module Registration — Design

**Date:** 2026-07-06
**Status:** Approved
**Scope:** Backend only (`Kheprx.BaseBackend` solution). Zero runtime behavior change.

## Goal

Replace the reflection-based module discovery
(`builder.Services.AddModules(builder.Configuration)`) with explicit
per-module extension calls, matching the user's IslamicApplication reference
file (`IslamicApplicationDotNetCore.Api/Program.cs`):

```csharp
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddIdentityModule(builder.Configuration);
```

The reflection machinery is deleted outright ("full replacement" — user's
choice over keeping `IModule` classes wrapped by thin extensions). The
`IModule` interface's only consumer was the scanner; once registration is
explicit, the abstraction has no purpose. The Api project already references
both module Infrastructure projects at compile time (for controllers and the
migration scope), so the runtime DLL scan guaranteed nothing the project
references don't already.

## New files (2)

Each module's Infrastructure project gets a static extensions class in the
**same namespace** as the deleted `IModule` class. Registration bodies move
verbatim from `CatalogModule.Register` / `IdentityModule.Register`.

### `src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Infrastructure/CatalogModuleExtensions.cs`

```csharp
using Kheprx.BaseBackend.Catalog.Application.Mappings;
using Kheprx.BaseBackend.Catalog.Application.Services;
using Kheprx.BaseBackend.Catalog.Application.Services.Interfaces;
using Kheprx.BaseBackend.Catalog.Contracts;
using Kheprx.BaseBackend.Catalog.Domain.Repositories;
using Kheprx.BaseBackend.Catalog.Infrastructure.Data;
using Kheprx.BaseBackend.Catalog.Infrastructure.Repositories;
using Kheprx.BaseBackend.Catalog.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kheprx.BaseBackend.Catalog.Infrastructure;

public static class CatalogModuleExtensions
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
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

### `src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/IdentityModuleExtensions.cs`

```csharp
using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Kheprx.BaseBackend.Identity.Application.Services;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Contracts;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Kheprx.BaseBackend.Identity.Infrastructure.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Security;
using Kheprx.BaseBackend.Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kheprx.BaseBackend.Identity.Infrastructure;

public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IEngagementTypeRepository, EngagementTypeRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IEngagementTypeService, EngagementTypeService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IIdentityModule, IdentityModuleApi>();
        return services;
    }
}
```

Usings are the deleted classes' usings minus
`Kheprx.BaseBackend.SharedKernel.Modules`. Plain code — no teaching comments
in this change; a teaching-comments pass may follow separately (matching the
FluentValidation-extraction sequence: refactor commit first, comments after).

## Deleted files (4)

- `src/SharedKernel/Kheprx.BaseBackend.SharedKernel/Modules/IModule.cs`
- `src/SharedKernel/Kheprx.BaseBackend.SharedKernel/Modules/ModuleRegistration.cs`
- `src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Infrastructure/CatalogModule.cs`
- `src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/IdentityModule.cs`

The `SharedKernel/Modules` folder disappears entirely. Verified by grep:
no other code consumes `IModule` or `AddModules`. Remaining grep hits are
HTML docs (see Follow-up), historical superpowers spec/plan markdown files
(dated records — left as-is), and one stale code comment fixed below.

## Changes to existing files

1. **`Kheprx.BaseBackend.Api/Program.cs`** — replace the `AddModules` line
   with explicit calls under the reference file's comment style; swap the
   `SharedKernel.Modules` using for the two Infrastructure usings:

   ```csharp
   using Kheprx.BaseBackend.Api.Extensions;
   using Kheprx.BaseBackend.Catalog.Infrastructure;
   using Kheprx.BaseBackend.Identity.Infrastructure;
   using Microsoft.EntityFrameworkCore;
   using Microsoft.Extensions.DependencyInjection;
   using Serilog;

   var builder = WebApplication.CreateBuilder(args);

   builder.Host.UseSerilog((context, loggerConfiguration) =>
       loggerConfiguration.ReadFrom.Configuration(context.Configuration));

   // ========================================
   // Configure Services
   // ========================================
   // Module registrations
   builder.Services.AddCatalogModule(builder.Configuration);
   builder.Services.AddIdentityModule(builder.Configuration);

   // Cross-cutting concerns
   builder.Services.AddSharedInfrastructure();
   builder.Services.AddFluentValidationConfiguration();
   builder.Services.AddCorsConfiguration(builder.Configuration);
   builder.Services.AddJwtAuth(builder.Configuration);
   ```

   Everything after `var app = builder.Build();` (migration scope, pipeline,
   `app.Run()`) is untouched. Banner comments apply only to the service
   block shown — the rest of Program.cs keeps its current style.

2. **`Kheprx.BaseBackend.Api/Extensions/FluentValidationExtensions.cs`** —
   the XML summary says "Call after AddModules so the AppDomain scan sees
   the module assemblies". Update the sentence to reference the explicit
   module registrations (e.g. "Call after the AddCatalogModule /
   AddIdentityModule registrations…"). Comment-only change; the code is
   untouched.

3. **`tests/Kheprx.BaseBackend.ArchitectureTests/ModuleConventionTests.cs`** —
   re-point both tests at the explicit methods; assertions unchanged:
   - `AddModules_registers_catalog_services` →
     `AddCatalogModule_registers_catalog_services`, calling
     `services.AddCatalogModule(configuration)`.
   - `AddModules_registers_identity_services` →
     `AddIdentityModule_registers_identity_services`, calling
     `services.AddIdentityModule(configuration)`.
   - Usings: remove `Kheprx.BaseBackend.SharedKernel.Modules`; add
     `Kheprx.BaseBackend.Catalog.Infrastructure` and
     `Kheprx.BaseBackend.Identity.Infrastructure`. The test csproj already
     references both Infrastructure projects — no csproj change.

4. **`src/SharedKernel/Kheprx.BaseBackend.SharedKernel/Kheprx.BaseBackend.SharedKernel.csproj`** —
   remove both package references
   (`Microsoft.Extensions.DependencyInjection.Abstractions`,
   `Microsoft.Extensions.Configuration.Abstractions`); the deleted Modules
   folder was their only consumer (verified by grep). The `ItemGroup`
   becomes empty and is removed with them.

## Behavior (unchanged) — the assembly-loading edge

The one real risk in deleting `ModuleRegistration`: it force-loaded every
`Kheprx.BaseBackend.*.dll` from the base directory, and
`AddFluentValidationConfiguration` scans **already-loaded** assemblies for
validators. Removing the scanner narrows the loaded set from
"everything on disk" to "everything transitively referenced by executed code".

This is safe here: all validators live in `Catalog.Application` and
`Identity.Application` (verified by grep for `AbstractValidator`), and both
extension-method bodies reference Application types directly
(`CatalogMapper`, `ProductService`, `AuthService`, …). Invoking
`AddCatalogModule` / `AddIdentityModule` therefore loads both Application
assemblies before `AddFluentValidationConfiguration` runs. The ordering
constraint (modules before FluentValidation) already holds in Program.cs and
is documented in the updated XML summary.

Constraint for future modules: a new module's validators are picked up only
if its `AddXxxModule` method executes before `AddFluentValidationConfiguration`
and its registrations reference the assembly containing the validators —
true for any module following the existing anatomy (services registered from
Application).

## Error handling

None new. The extension methods have the same (non-)failure modes as the
`Register` methods they replace. One failure mode is removed: the scanner's
silent `catch { }` around `Assembly.LoadFrom` can no longer mask a module
that fails to load — a missing module is now a compile error.

## Testing / verification

- `dotnet build backend/Kheprx.BaseBackend.sln` — clean build.
- `dotnet test` on the solution — full suite green (architecture tests
  re-pointed as above; no other test references `AddModules`, verified by
  grep).
- No automated end-to-end runtime check (same accepted gap as prior runs);
  covered by the user's manual E2E pass.

## Follow-up work (out of scope, tracked here)

The 8 HTML teaching docs in `backend/docs` describe the auto-discovery
convention and are stale after this change — user chose a separate docs pass:

- `IMODULE_CONVENTION.html` (entirely about the deleted mechanism)
- `MODULE_ANATOMY.html`, `RUNTIME_VS_REFERENCES.html`,
  `ARCHITECTURE_OVERVIEW.html`, `SHAREDKERNEL.html`, `HOST_API.html`,
  `SOLUTION_STRUCTURE.html` (reference `AddModules`/`IModule` in places)
- `MODULE_INFRASTRUCTURE.html` (walks through `CatalogModule : IModule` at
  lines 88/104/118/258)

A possible optional follow-up alongside the docs pass: teaching comments on
the two new extension classes, in the established AuthService voice.

- Optional test-hardening rider (recommended by final review): add
  `ServiceDescriptor.Lifetime` assertions to `ModuleConventionTests` for at
  least `CatalogMapper` (Singleton) and one Scoped service, so a future
  lifetime slip is caught at build time.

## Decisions log

- **Target confirmed:** the IslamicApplication `Program.cs` is the style
  reference (it already matches the user's pasted snippet verbatim); the
  change applies to the Kheprx.Electric backend.
- **Full replacement over thin wrappers:** user chose deleting
  `IModule`/`ModuleRegistration` and converting module classes to extension
  methods, over keeping `IModule` classes wrapped by extensions or keeping
  both registration paths.
- **Docs deferred:** HTML teaching-doc updates go in a separate pass
  (user's choice), recorded above as follow-up.
- **Plain code first:** teaching comments are not part of this change,
  matching the user's refactor-then-comments commit sequence.
