# Mvc/Swagger Extension Split Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split `AddSharedInfrastructure` into `MvcExtensions.AddMvcConfiguration` and `SwaggerExtensions.AddSwaggerConfiguration`, delete the legacy `ServiceCollectionExtensions.cs`, and have `Program.cs` call the two new methods — zero behavior change.

**Architecture:** Consumer-first: rewrite `Program.cs` to call the two not-yet-existing methods (RED), create both new files (GREEN — the old file coexists harmlessly for one step), then `git rm` the old file and build again (proves nothing referenced the legacy class). Bodies move verbatim except one documented same-assembly `typeof` swap.

**Tech Stack:** .NET 10, Swashbuckle, xUnit. No new dependencies.

**Spec:** `backend/docs/superpowers/specs/2026-07-06-mvc-swagger-split-design.md` (approved).

## Global Constraints

- Zero runtime behavior change: identical DI descriptor order (controllers → explorer → SwaggerGen, same position relative to FluentValidation/CORS/JWT). Bodies move verbatim except `typeof(ServiceCollectionExtensions).Assembly` → `typeof(SwaggerExtensions).Assembly` (same Api assembly — behaviorally identical).
- New files are plain code — no teaching comments, no XML docs. Usings exactly as shown in the steps (System-first sorted).
- No csproj edits. Do NOT touch the 8 stale HTML teaching docs in `backend/docs`.
- Do NOT stage `frontend/angular.json`. Always `git add` explicit paths, never `git add -A` or `git add .`.
- Do NOT write build/test output to log files in the repo — capture output directly from command results.
- All commands run from the repo root (`C:\Users\envnt\Desktop\Kheprx.Electric`).
- Build/test gate: `dotnet build` 0 errors / 0 new warnings; `dotnet test` 147/147.

---

### Task 1: Split AddSharedInfrastructure into Mvc and Swagger extensions

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Program.cs` (using block + cross-cutting block)
- Create: `backend/Kheprx.BaseBackend.Api/Extensions/Mvc/MvcExtensions.cs`
- Create: `backend/Kheprx.BaseBackend.Api/Extensions/Swagger/SwaggerExtensions.cs`
- Delete: `backend/Kheprx.BaseBackend.Api/Extensions/Mvc/ServiceCollectionExtensions.cs`
- Test: full solution build + existing suite (no new test — extraction refactor; compiler is the RED/GREEN gate, 147-test suite pins behavior)

**Interfaces:**
- Consumes: `ValidationFilter` (`Kheprx.BaseBackend.Api.Filters`), `ModelStateResponse` (`Kheprx.BaseBackend.Api.Validation`), `AuthorizeOperationFilter` (`Kheprx.BaseBackend.Api.Swagger`), `SessionDto`/`ProductDto` (module Application DTOs).
- Produces: `public static IServiceCollection AddMvcConfiguration(this IServiceCollection services)` in `Kheprx.BaseBackend.Api.Extensions.Mvc.MvcExtensions`, and `public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)` in `Kheprx.BaseBackend.Api.Extensions.Swagger.SwaggerExtensions`. `AddSharedInfrastructure` and `ServiceCollectionExtensions` cease to exist. No other task follows.

- [ ] **Step 1: Rewrite Program.cs (consumer first)**

Replace the entire content of `backend/Kheprx.BaseBackend.Api/Program.cs` with:

```csharp
using Kheprx.BaseBackend.Api.Extensions.Data;
using Kheprx.BaseBackend.Api.Extensions.Mvc;
using Kheprx.BaseBackend.Api.Extensions.Pipeline;
using Kheprx.BaseBackend.Api.Extensions.Security;
using Kheprx.BaseBackend.Api.Extensions.Swagger;
using Kheprx.BaseBackend.Api.Extensions.Validation;
using Kheprx.BaseBackend.Catalog.Infrastructure.Extensions;
using Kheprx.BaseBackend.Identity.Infrastructure.Extensions;
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
builder.Services.AddMvcConfiguration();
builder.Services.AddSwaggerConfiguration();
builder.Services.AddFluentValidationConfiguration();
builder.Services.AddCorsConfiguration(builder.Configuration);
builder.Services.AddJwtAuth(builder.Configuration);

var app = builder.Build();

await app.ApplyIdentityMigrationsAsync();

// ========================================
// Configure Middleware Pipeline
// ========================================
app.UseBaseBackendMiddleware(app.Environment);
app.MapControllers();

app.Run();
```

Diff vs. current: one new using (`…Extensions.Swagger`, alphabetically between `Security` and `Validation`) and `builder.Services.AddSharedInfrastructure();` becomes the two calls shown. Everything else is unchanged.

- [ ] **Step 2: Verify the build fails (new methods do not exist yet)**

Run:
```bash
dotnet build backend/Kheprx.BaseBackend.sln
```

Expected: **build FAILURE** with `error CS1061: 'IServiceCollection' does not contain a definition for 'AddMvcConfiguration'` and the same for `AddSwaggerConfiguration` in `Program.cs`. (The `…Extensions.Swagger` using may also raise CS0234 until the namespace exists — either error set counts as RED.)

- [ ] **Step 3: Create MvcExtensions.cs**

Create `backend/Kheprx.BaseBackend.Api/Extensions/Mvc/MvcExtensions.cs` with exactly:

```csharp
using System.Text.Json;
using Kheprx.BaseBackend.Api.Filters;
using Kheprx.BaseBackend.Api.Validation;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Extensions.Mvc;

public static class MvcExtensions
{
    public static IServiceCollection AddMvcConfiguration(this IServiceCollection services)
    {
        services.AddControllers(options => options.Filters.Add<ValidationFilter>())
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
            .ConfigureApiBehaviorOptions(options =>
                options.InvalidModelStateResponseFactory = context =>
                    new BadRequestObjectResult(ModelStateResponse.From(context.ModelState)));

        return services;
    }
}
```

The controllers block is verbatim from `ServiceCollectionExtensions.AddSharedInfrastructure` (which still exists in the sibling file — do not modify it in this step).

- [ ] **Step 4: Create SwaggerExtensions.cs**

Create `backend/Kheprx.BaseBackend.Api/Extensions/Swagger/SwaggerExtensions.cs` with exactly:

```csharp
using Kheprx.BaseBackend.Api.Swagger;
using Microsoft.OpenApi.Models;

namespace Kheprx.BaseBackend.Api.Extensions.Swagger;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Kheprx Electric API",
                Version = "v1",
                Description = "Backend API for the Kheprx Electric platform. Every endpoint wraps its payload in the "
                    + "ApiResponse envelope: { successStatus, message, error, data }. Failure responses carry a "
                    + "machine-readable code in 'error' (e.g. EMAIL_IN_USE)."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Paste the accessToken returned by POST /api/auth/login."
            });

            options.OperationFilter<AuthorizeOperationFilter>();

            var documentedAssemblies = new[]
            {
                typeof(SwaggerExtensions).Assembly,
                typeof(Kheprx.BaseBackend.Identity.Application.DTOs.SessionDto).Assembly,
                typeof(Kheprx.BaseBackend.Catalog.Application.DTOs.ProductDto).Assembly,
            };
            foreach (var assembly in documentedAssemblies)
            {
                var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");
                if (File.Exists(xmlPath))
                    options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }
}
```

Verbatim from the old method except the sanctioned edit: `typeof(ServiceCollectionExtensions).Assembly` → `typeof(SwaggerExtensions).Assembly` (same Api assembly).

- [ ] **Step 5: Build the solution (GREEN — old file still coexists)**

Run:
```bash
dotnet build backend/Kheprx.BaseBackend.sln
```

Expected: `Build succeeded` with 0 errors, 0 new warnings. The unused `ServiceCollectionExtensions` still compiles alongside — defining methods registers nothing.

- [ ] **Step 6: Delete the legacy file**

```bash
git rm backend/Kheprx.BaseBackend.Api/Extensions/Mvc/ServiceCollectionExtensions.cs
```

- [ ] **Step 7: Build again (proves nothing referenced the deleted class)**

Run:
```bash
dotnet build backend/Kheprx.BaseBackend.sln
```

Expected: `Build succeeded` with 0 errors, 0 new warnings.

- [ ] **Step 8: Run the full test suite**

Run:
```bash
dotnet test backend/Kheprx.BaseBackend.sln
```

Expected: PASS — `Failed: 0` across all four projects (Identity 86 + Catalog 16 + Api 33 + Architecture 12 = 147).

- [ ] **Step 9: Grep guard — legacy names gone from code paths**

Run:
```bash
git grep -n -e "AddSharedInfrastructure" -e "ServiceCollectionExtensions" -- backend/src backend/Kheprx.BaseBackend.Api backend/tests
```

Expected: **no matches** (exit code 1). Scope excludes `backend/docs` — historical planning markdown and the deferred HTML docs mention the old names by design. A hit inside the scoped paths means a consumer was missed — stop and fix before committing.

- [ ] **Step 10: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Program.cs backend/Kheprx.BaseBackend.Api/Extensions/Mvc/MvcExtensions.cs backend/Kheprx.BaseBackend.Api/Extensions/Swagger/SwaggerExtensions.cs
git commit -m "refactor(api): split AddSharedInfrastructure into Mvc and Swagger extensions

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

(The deletion was staged by `git rm` in Step 6.)

---

## Out of Scope (tracked in the spec)

- Stale HTML teaching docs (HOST_API.html shows AddSharedInfrastructure) — deferred 8-doc pass.
- Teaching comments on the two new files — later pass if wanted.
