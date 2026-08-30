# Explicit Per-Module Registration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the reflection-based `AddModules()` auto-discovery with explicit `AddCatalogModule()` / `AddIdentityModule()` extension methods, then delete the `IModule`/`ModuleRegistration` machinery.

**Architecture:** Each module's Infrastructure project gets a static extensions class holding the registrations moved verbatim from its `IModule` class. `Program.cs` calls the two methods explicitly under the IslamicApplication reference file's comment style. The SharedKernel `Modules` folder (interface + reflection scanner) is deleted last, after nothing references it.

**Tech Stack:** .NET 10, ASP.NET Core minimal hosting, EF Core (Npgsql), xUnit architecture tests.

**Spec:** `backend/docs/superpowers/specs/2026-07-06-explicit-module-registration-design.md` (approved).

## Global Constraints

- Zero runtime behavior change: registration bodies move **verbatim** — same lifetimes, same order, same types.
- New extension classes live in the **same namespace** as the deleted module classes (`Kheprx.BaseBackend.Catalog.Infrastructure`, `Kheprx.BaseBackend.Identity.Infrastructure`).
- Plain code only in new files — **no teaching comments** (a separate pass may add them later).
- Do NOT touch the 7 HTML teaching docs in `backend/docs` (deferred to a separate docs pass per spec).
- Do NOT stage `frontend/angular.json` — it has unrelated uncommitted changes. Always `git add` explicit paths, never `git add -A` or `git add .`.
- All commands run from the repo root (`C:\Users\envnt\Desktop\Kheprx.Electric`).
- Ordering constraint that must survive: module registrations run **before** `AddFluentValidationConfiguration()` in `Program.cs` (its AppDomain scan only sees already-loaded assemblies).

---

### Task 1: Catalog module extension method

**Files:**
- Create: `backend/src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Infrastructure/CatalogModuleExtensions.cs`
- Modify: `backend/tests/Kheprx.BaseBackend.ArchitectureTests/ModuleConventionTests.cs`
- Test: `backend/tests/Kheprx.BaseBackend.ArchitectureTests/ModuleConventionTests.cs`

**Interfaces:**
- Consumes: existing types only (`CatalogDbContext`, `CatalogMapper`, `IProductRepository`/`ProductRepository`, `IProductService`/`ProductService`, `ICatalogModule`/`CatalogModuleApi`).
- Produces: `public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)` in namespace `Kheprx.BaseBackend.Catalog.Infrastructure`, static class `CatalogModuleExtensions`. Task 3 calls it from `Program.cs`.

Note: `CatalogModule : IModule` still exists after this task — it is deleted in Task 4. Both paths coexisting is expected mid-plan; `Program.cs` still calls `AddModules()` until Task 3.

- [ ] **Step 1: Re-point the catalog architecture test (failing first)**

In `backend/tests/Kheprx.BaseBackend.ArchitectureTests/ModuleConventionTests.cs`:

Add this using (alphabetical position — after the two `Catalog.*` usings that are already there, i.e. below `using Kheprx.BaseBackend.Catalog.Contracts;`):

```csharp
using Kheprx.BaseBackend.Catalog.Infrastructure;
```

Keep `using Kheprx.BaseBackend.SharedKernel.Modules;` for now — the identity test still calls `AddModules` until Task 2.

Replace the first test method (`AddModules_registers_catalog_services`) with:

```csharp
    [Fact]
    public void AddCatalogModule_registers_catalog_services()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=u;Password=p"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddCatalogModule(configuration);

        Assert.Contains(services, d => d.ServiceType == typeof(IProductService));
        Assert.Contains(services, d => d.ServiceType == typeof(ICatalogModule));
    }
```

The second test (`AddModules_registers_identity_services`) stays untouched in this task.

- [ ] **Step 2: Run the architecture tests to verify the failure**

Run:
```bash
dotnet test backend/tests/Kheprx.BaseBackend.ArchitectureTests/Kheprx.BaseBackend.ArchitectureTests.csproj
```

Expected: **build FAILURE** (the test project does not compile) with:
`error CS1061: 'ServiceCollection' does not contain a definition for 'AddCatalogModule'` (or `'IServiceCollection'` — either form counts).

- [ ] **Step 3: Create the extension class**

Create `backend/src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Infrastructure/CatalogModuleExtensions.cs` with exactly:

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

This is the body of `CatalogModule.Register` (in `CatalogModule.cs`, same directory) moved verbatim; the usings are that file's usings minus `Kheprx.BaseBackend.SharedKernel.Modules`. Do not modify `CatalogModule.cs` itself.

- [ ] **Step 4: Run the architecture tests to verify they pass**

Run:
```bash
dotnet test backend/tests/Kheprx.BaseBackend.ArchitectureTests/Kheprx.BaseBackend.ArchitectureTests.csproj
```

Expected: PASS — `Failed: 0`, both `ModuleConventionTests` green (`AddCatalogModule_registers_catalog_services` and the still-old `AddModules_registers_identity_services`).

- [ ] **Step 5: Commit**

```bash
git add backend/src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Infrastructure/CatalogModuleExtensions.cs backend/tests/Kheprx.BaseBackend.ArchitectureTests/ModuleConventionTests.cs
git commit -m "refactor(catalog): add AddCatalogModule extension method

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: Identity module extension method

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/IdentityModuleExtensions.cs`
- Modify: `backend/tests/Kheprx.BaseBackend.ArchitectureTests/ModuleConventionTests.cs`
- Test: `backend/tests/Kheprx.BaseBackend.ArchitectureTests/ModuleConventionTests.cs`

**Interfaces:**
- Consumes: existing types only (`IdentityDbContext`, `JwtOptions`, the five `I*Repository`/`*Repository` pairs, `IPasswordHasher`/`PasswordHasher`, `IJwtTokenService`/`JwtTokenService`, the four `I*Service`/`*Service` pairs, `IIdentityModule`/`IdentityModuleApi`).
- Produces: `public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)` in namespace `Kheprx.BaseBackend.Identity.Infrastructure`, static class `IdentityModuleExtensions`. Task 3 calls it from `Program.cs`.

After this task no test calls `AddModules`, so the test file drops its `SharedKernel.Modules` using. `IdentityModule : IModule` still exists until Task 4.

- [ ] **Step 1: Re-point the identity architecture test (failing first)**

In `backend/tests/Kheprx.BaseBackend.ArchitectureTests/ModuleConventionTests.cs`:

Usings — remove `using Kheprx.BaseBackend.SharedKernel.Modules;` and add `using Kheprx.BaseBackend.Identity.Infrastructure;`. The complete using block becomes:

```csharp
using Kheprx.BaseBackend.Catalog.Application.Services.Interfaces;
using Kheprx.BaseBackend.Catalog.Contracts;
using Kheprx.BaseBackend.Catalog.Infrastructure;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Contracts;
using Kheprx.BaseBackend.Identity.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
```

Replace the second test method (`AddModules_registers_identity_services`) with:

```csharp
    [Fact]
    public void AddIdentityModule_registers_identity_services()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=u;Password=p"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddIdentityModule(configuration);

        Assert.Contains(services, d => d.ServiceType == typeof(IAuthService));
        Assert.Contains(services, d => d.ServiceType == typeof(IRoleService));
        Assert.Contains(services, d => d.ServiceType == typeof(IUserService));
        Assert.Contains(services, d => d.ServiceType == typeof(IIdentityModule));
    }
```

- [ ] **Step 2: Run the architecture tests to verify the failure**

Run:
```bash
dotnet test backend/tests/Kheprx.BaseBackend.ArchitectureTests/Kheprx.BaseBackend.ArchitectureTests.csproj
```

Expected: **build FAILURE** with:
`error CS1061: 'ServiceCollection' does not contain a definition for 'AddIdentityModule'`.

- [ ] **Step 3: Create the extension class**

Create `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/IdentityModuleExtensions.cs` with exactly:

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

This is the body of `IdentityModule.Register` (in `IdentityModule.cs`, same directory) moved verbatim; the usings are that file's usings minus `Kheprx.BaseBackend.SharedKernel.Modules`. Do not modify `IdentityModule.cs` itself.

- [ ] **Step 4: Run the architecture tests to verify they pass**

Run:
```bash
dotnet test backend/tests/Kheprx.BaseBackend.ArchitectureTests/Kheprx.BaseBackend.ArchitectureTests.csproj
```

Expected: PASS — `Failed: 0`, both renamed tests green.

- [ ] **Step 5: Commit**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/IdentityModuleExtensions.cs backend/tests/Kheprx.BaseBackend.ArchitectureTests/ModuleConventionTests.cs
git commit -m "refactor(identity): add AddIdentityModule extension method

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: Switch Program.cs to explicit registration

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Program.cs` (whole file — 28 lines)
- Modify: `backend/Kheprx.BaseBackend.Api/Extensions/FluentValidationExtensions.cs:9` and `:16-17` (comments only)
- Test: full solution build + test suite (no new test — composition-root wiring; the Task 1/2 architecture tests cover the extension methods)

**Interfaces:**
- Consumes: `AddCatalogModule(IServiceCollection, IConfiguration)` from Task 1; `AddIdentityModule(IServiceCollection, IConfiguration)` from Task 2.
- Produces: nothing new — after this task, nothing in the Api project references `AddModules` or `SharedKernel.Modules`, which unblocks Task 4's deletions.

- [ ] **Step 1: Rewrite Program.cs**

Replace the entire content of `backend/Kheprx.BaseBackend.Api/Program.cs` with:

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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<Kheprx.BaseBackend.Identity.Infrastructure.Data.IdentityDbContext>();
    await db.Database.MigrateAsync();
}

app.UseSharedPipeline(app.Environment);
app.Run();
```

Diff vs. current: the `using Kheprx.BaseBackend.SharedKernel.Modules;` line is replaced by the two Infrastructure usings; the single `AddModules` call becomes the banner comment block + two explicit calls + the `// Cross-cutting concerns` label. Everything from `var app = builder.Build();` down is byte-identical to the current file (the fully-qualified `IdentityDbContext` in the migration scope still compiles and stays as-is).

- [ ] **Step 2: Fix the two stale AddModules comments in FluentValidationExtensions.cs**

In `backend/Kheprx.BaseBackend.Api/Extensions/FluentValidationExtensions.cs` (comments only — no code changes):

Line 9, XML summary — replace:
```csharp
    /// Call after AddModules so the AppDomain scan sees the module assemblies.
```
with:
```csharp
    /// Call after the AddCatalogModule/AddIdentityModule registrations so the AppDomain scan sees the module assemblies.
```

Lines 16–17, teaching comment — replace:
```csharp
        // This is why call order in Program.cs matters - AddModules loads the module
        // DLLs first, so they are all in this list by the time we scan.
```
with:
```csharp
        // This is why call order in Program.cs matters - the AddCatalogModule/
        // AddIdentityModule calls load the module DLLs first (their registration code
        // references types in those assemblies), so they are all in this list by the
        // time we scan.
```

- [ ] **Step 3: Build the solution**

Run:
```bash
dotnet build backend/Kheprx.BaseBackend.sln
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 4: Run the full test suite**

Run:
```bash
dotnet test backend/Kheprx.BaseBackend.sln
```

Expected: PASS — `Failed: 0` across all test projects (baseline count unchanged; 147 at spec time).

- [ ] **Step 5: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Program.cs backend/Kheprx.BaseBackend.Api/Extensions/FluentValidationExtensions.cs
git commit -m "refactor(api): register modules explicitly in Program.cs

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 4: Delete the IModule auto-discovery machinery

**Files:**
- Delete: `backend/src/SharedKernel/Kheprx.BaseBackend.SharedKernel/Modules/IModule.cs`
- Delete: `backend/src/SharedKernel/Kheprx.BaseBackend.SharedKernel/Modules/ModuleRegistration.cs`
- Delete: `backend/src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Infrastructure/CatalogModule.cs`
- Delete: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/IdentityModule.cs`
- Modify: `backend/src/SharedKernel/Kheprx.BaseBackend.SharedKernel/Kheprx.BaseBackend.SharedKernel.csproj` (whole file — 6 lines)
- Test: full solution build + test suite (deletion task — green build/tests prove nothing depended on the deleted code)

**Interfaces:**
- Consumes: requires Task 3 complete (last `AddModules` caller gone).
- Produces: nothing — end state. `SharedKernel/Modules` folder no longer exists.

- [ ] **Step 1: Delete the four files**

```bash
git rm backend/src/SharedKernel/Kheprx.BaseBackend.SharedKernel/Modules/IModule.cs backend/src/SharedKernel/Kheprx.BaseBackend.SharedKernel/Modules/ModuleRegistration.cs backend/src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Infrastructure/CatalogModule.cs backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/IdentityModule.cs
```

(`git rm` deletes and stages in one step. The `Modules` folder disappears with its last file.)

- [ ] **Step 2: Remove SharedKernel's now-unused package references**

The two `Microsoft.Extensions.*.Abstractions` packages existed only for the deleted `Modules` folder (verified by grep in the spec). Replace the entire content of `backend/src/SharedKernel/Kheprx.BaseBackend.SharedKernel/Kheprx.BaseBackend.SharedKernel.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
</Project>
```

(Package versions stay pinned centrally in `backend/Directory.Packages.props` for other projects — do not touch that file.)

- [ ] **Step 3: Verify no code references remain**

Run:
```bash
git grep -n -e "AddModules" -e "IModule" -- backend/src backend/Kheprx.BaseBackend.Api backend/tests
```

Expected: **no matches** (exit code 1). `ICatalogModule`/`IIdentityModule` do not contain the substring `IModule`, so any hit is a real leftover — stop and fix it before proceeding. (Hits under `backend/docs` are expected and out of scope; the paths above exclude them.)

- [ ] **Step 4: Build the solution**

Run:
```bash
dotnet build backend/Kheprx.BaseBackend.sln
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 5: Run the full test suite**

Run:
```bash
dotnet test backend/Kheprx.BaseBackend.sln
```

Expected: PASS — `Failed: 0` (same counts as Task 3).

- [ ] **Step 6: Commit**

```bash
git add backend/src/SharedKernel/Kheprx.BaseBackend.SharedKernel/Kheprx.BaseBackend.SharedKernel.csproj
git commit -m "refactor(shared-kernel): delete IModule auto-discovery machinery

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

(The four deletions were already staged by `git rm` in Step 1.)

---

## Out of Scope (tracked in the spec)

- The 7 HTML teaching docs in `backend/docs` describing auto-discovery — separate docs pass.
- Teaching comments on the two new extension classes — optional, rides with the docs pass.
