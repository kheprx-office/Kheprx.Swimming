# FluentValidation Extension Extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract the FluentValidation validator registration (AppDomain module scan + `AddValidatorsFromAssemblies`) from `ServiceCollectionExtensions.AddSharedInfrastructure` into a dedicated `FluentValidationExtensions.cs`, wired from `Program.cs`, with zero behavior change.

**Architecture:** One new per-concern extension class following the existing `AuthenticationExtensions`/`CorsExtensions` pattern: static class, file-scoped namespace, single `Add*` method returning `IServiceCollection`. The `moduleAssemblies` scan moves with the registration line (the scan's only consumer). Two existing files are edited.

**Tech Stack:** ASP.NET Core (.NET, `Microsoft.NET.Sdk.Web` with implicit usings), FluentValidation (`AddValidatorsFromAssemblies`), xUnit suite via `dotnet test`.

**Spec:** `backend/docs/superpowers/specs/2026-07-06-fluentvalidation-extension-design.md`

## Global Constraints

- Zero behavior change: same assembly filter (`Kheprx.BaseBackend.` prefix, `StringComparison.Ordinal`), same `AddValidatorsFromAssemblies(moduleAssemblies, includeInternalTypes: true)` call (the flag widens the scan to internal validators; today's validators are all public).
- `ValidationFilter` in `AddControllers` and the `InvalidModelStateResponseFactory` envelope are untouched — they are validation *enforcement*, not registration.
- `AddFluentValidationConfiguration()` is called from `Program.cs` and MUST stay after `AddModules(...)` — `AppDomain.GetAssemblies()` only returns already-loaded assemblies, and `AddModules` is what loads the module assemblies.
- Do NOT adopt from the reference project: `AddFluentValidationAutoValidation()`, `AddFluentValidationClientsideAdapters()` (deprecated package, would change error responses), or explicit per-module `typeof(...).Assembly` lists.
- No test changes: no existing test references `AddSharedInfrastructure`, `AddValidatorsFromAssemblies`, or `moduleAssemblies`.
- All `dotnet` commands run from the repo root against `backend/Kheprx.BaseBackend.sln`.

---

### Task 1: Extract validator registration into FluentValidationExtensions.cs

**Files:**
- Create: `backend/Kheprx.BaseBackend.Api/Extensions/FluentValidationExtensions.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Extensions/ServiceCollectionExtensions.cs:1,6,17-19,64`
- Modify: `backend/Kheprx.BaseBackend.Api/Program.cs:13-14`
- Test: none (structural refactor; existing suite guards against regressions)

**Interfaces:**
- Consumes: nothing from other tasks (single-task plan). Relies on `AddModules` (already in `Program.cs:12`) having loaded module assemblies before it runs.
- Produces: `public static IServiceCollection AddFluentValidationConfiguration(this IServiceCollection services)` on `public static class FluentValidationExtensions` — consumed by `Program.cs`.

- [ ] **Step 1: Create the new extension file**

Create `backend/Kheprx.BaseBackend.Api/Extensions/FluentValidationExtensions.cs` with exactly:

```csharp
using FluentValidation;

namespace Kheprx.BaseBackend.Api.Extensions;

public static class FluentValidationExtensions
{
    /// <summary>
    /// Registers FluentValidation validators from every loaded Kheprx.BaseBackend module assembly.
    /// Call after AddModules so the AppDomain scan sees the module assemblies.
    /// </summary>
    public static IServiceCollection AddFluentValidationConfiguration(this IServiceCollection services)
    {
        var moduleAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Kheprx.BaseBackend.", StringComparison.Ordinal) == true)
            .ToArray();

        services.AddValidatorsFromAssemblies(moduleAssemblies, includeInternalTypes: true);
        return services;
    }
}
```

`using FluentValidation;` is required (it provides the `AddValidatorsFromAssemblies` extension). No other usings — `IServiceCollection`, `AppDomain`, LINQ, and `StringComparison` are covered by the Web SDK's implicit usings.

- [ ] **Step 2: Build to verify the new file compiles alongside the old code**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: `Build succeeded.` with `0 Warning(s)`, `0 Error(s)`. (The new method exists but is not yet called, so validators are still registered exactly once, by the old code.)

- [ ] **Step 3: Remove the moved code and dead usings from ServiceCollectionExtensions.cs**

In `backend/Kheprx.BaseBackend.Api/Extensions/ServiceCollectionExtensions.cs`, make exactly four deletions:

a. Delete `using System.Reflection;` (line 1) and `using FluentValidation;` (line 6) — both are unused after this step. The using block becomes:

```csharp
using System.Text.Json;
using Kheprx.BaseBackend.Api.Filters;
using Kheprx.BaseBackend.Api.Validation;
using Kheprx.BaseBackend.Api.Swagger;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
```

b. Delete the `moduleAssemblies` scan (lines 17–19) and the blank line after it:

```csharp
        var moduleAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Kheprx.BaseBackend.", StringComparison.Ordinal) == true)
            .ToArray();

```

so `services.AddControllers(...)` becomes the first statement of the method.

c. Delete the registration line (line 64) and one adjacent blank line:

```csharp
        services.AddValidatorsFromAssemblies(moduleAssemblies, includeInternalTypes: true);
```

The end of the method must read exactly (Swagger block close, one blank line, return):

```csharp
        });

        return services;
    }
```

Nothing else in the file changes — `AddControllers` (with `ValidationFilter`), JSON options, `ConfigureApiBehaviorOptions`, and the Swagger block stay byte-identical.

- [ ] **Step 4: Wire the new extension in Program.cs**

In `backend/Kheprx.BaseBackend.Api/Program.cs`, change lines 12–15 from:

```csharp
builder.Services.AddModules(builder.Configuration);
builder.Services.AddSharedInfrastructure();
builder.Services.AddCorsConfiguration(builder.Configuration);
builder.Services.AddJwtAuth(builder.Configuration);
```

to:

```csharp
builder.Services.AddModules(builder.Configuration);
builder.Services.AddSharedInfrastructure();
builder.Services.AddFluentValidationConfiguration();
builder.Services.AddCorsConfiguration(builder.Configuration);
builder.Services.AddJwtAuth(builder.Configuration);
```

The new call MUST stay after `AddModules` (see Global Constraints). This placement — immediately after `AddSharedInfrastructure()` — preserves today's relative order; nothing between the old scan point and the new one loads assemblies, so the scanned set is identical.

- [ ] **Step 5: Build to verify the refactor compiles**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: `Build succeeded.` with `0 Warning(s)`, `0 Error(s)`. If it reports `The name 'moduleAssemblies' does not exist`, Step 3's deletions were incomplete; if it reports a missing `AddValidatorsFromAssemblies`, the `using FluentValidation;` in the NEW file is missing.

- [ ] **Step 6: Run the existing test suite**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: all four test projects pass, 147 total, `Failed: 0`. No test references the moved code, so any failure means a regression outside the intended change — stop and investigate.

- [ ] **Step 7: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Extensions/FluentValidationExtensions.cs backend/Kheprx.BaseBackend.Api/Extensions/ServiceCollectionExtensions.cs backend/Kheprx.BaseBackend.Api/Program.cs
git commit -m "refactor(api): extract FluentValidation registration into FluentValidationExtensions"
```
