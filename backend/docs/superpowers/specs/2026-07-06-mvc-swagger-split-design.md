# Mvc/Swagger Extension Split — Design

**Date:** 2026-07-06
**Status:** Approved
**Scope:** Backend only (`Kheprx.BaseBackend.Api`). Zero runtime behavior change.

## Goal

Make `AddSharedInfrastructure` readable by splitting its two bundled
concerns into per-concern extension files, continuing today's Extensions
taxonomy and mirroring the user's IslamicApplication reference (which
registers controllers and Swagger as separate calls:
`AddCustomControllers()` / `AddSwaggerConfiguration()`).

User chose the **two-file split** over private-helper extraction (single
public API kept) and banner-sections-only.

## New files (2)

### `Extensions/Mvc/MvcExtensions.cs`

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

The controllers block moves verbatim. Usings are the four the block needs,
sorted System-first (repo pattern, e.g. `AuthenticationExtensions`) — this
also retires the pre-existing using-order nit flagged by the
extensions-taxonomy final review (`Api.Validation` before `Api.Swagger` in
the old file).

### `Extensions/Swagger/SwaggerExtensions.cs`

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

The explorer + SwaggerGen block moves verbatim with **one necessary edit**:
`typeof(ServiceCollectionExtensions).Assembly` →
`typeof(SwaggerExtensions).Assembly`. Both types live in the same Api
assembly, so the documented-assemblies array is behaviorally identical; the
old type ceases to exist.

Namespace-lookup check (same reasoning validated by the extensions-taxonomy
final review): the file sits in `…Api.Extensions.Swagger` while
`AuthorizeOperationFilter` lives in `…Api.Swagger` — unqualified lookup
finds no `AuthorizeOperationFilter` in any enclosing namespace and falls
through to the using; no type name exists in both namespaces, so no
CS0104. The file contains no `Swagger.X`-style qualified references.

## Deleted file (1)

- `Extensions/Mvc/ServiceCollectionExtensions.cs` — the legacy-generic
  class. Grep-verified: `Program.cs` was the only consumer of
  `AddSharedInfrastructure`; the only other reference to the type name was
  the `typeof` inside its own body (handled above).

## Changes to existing files (1)

**`Program.cs`** — one call becomes two, plus one new using. The using
block becomes:

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
```

and the cross-cutting block becomes:

```csharp
// Cross-cutting concerns
builder.Services.AddMvcConfiguration();
builder.Services.AddSwaggerConfiguration();
builder.Services.AddFluentValidationConfiguration();
builder.Services.AddCorsConfiguration(builder.Configuration);
builder.Services.AddJwtAuth(builder.Configuration);
```

Nothing else in the file changes.

## Behavior (unchanged)

Identical DI descriptor order: controllers → explorer → SwaggerGen run in
the same position relative to FluentValidation/CORS/JWT as inside the old
single method. Registration contents move verbatim except the
same-assembly `typeof` swap.

## Testing / verification

- No test references `AddSharedInfrastructure`, `ServiceCollectionExtensions`,
  or either new method (suite is unit-only; grep-verified consumer set).
- `dotnet build backend/Kheprx.BaseBackend.sln` — 0 errors, 0 new warnings.
- `dotnet test backend/Kheprx.BaseBackend.sln` — 147/147.
- Grep guard (code paths: `backend/src`, `backend/Kheprx.BaseBackend.Api`,
  `backend/tests`): no remaining `AddSharedInfrastructure` or
  `ServiceCollectionExtensions`. Hits under `backend/docs` are historical
  records / deferred HTML docs, by design.

## Follow-up (out of scope, already tracked)

- Stale HTML teaching docs (HOST_API.html shows AddSharedInfrastructure) —
  deferred 8-doc pass; this split adds to it.
- Teaching comments on the two new files — later pass if wanted.

## Decisions log

- **Two-file split** (`MvcExtensions.AddMvcConfiguration` +
  `SwaggerExtensions.AddSwaggerConfiguration`) over private helpers and
  banners-only — user's choice; mirrors the Islamic reference's separate
  registration calls and today's per-concern taxonomy.
- **`AddEndpointsApiExplorer` goes with Swagger** — it exists to feed the
  Swagger generator.
- **`typeof` swap documented** as the single non-verbatim edit
  (same-assembly, behavior-identical).
- **New `Extensions/Swagger/` taxonomy folder** — distinct from the
  existing `Api/Swagger/` folder (which keeps `AuthorizeOperationFilter`);
  namespace-lookup safety verified above.
- **Using-order nit retired** — the new files carry correctly sorted using
  blocks, closing the pre-existing Minor from the taxonomy final review.
