# Middleware Pipeline Restyle — Design

**Date:** 2026-07-06
**Status:** Approved
**Scope:** Backend only (`Kheprx.BaseBackend.Api`). Zero runtime behavior change.

## Goal

Restyle the middleware pipeline to match the user's IslamicApplication
reference (`IslamicApplicationDotNetCore.Api/Extensions/ApplicationBuilderExtensions.cs`
and its `Program.cs` call site): an app-named pipeline extension under a
`// Configure Middleware Pipeline` banner, numbered banner sections inside
the method, and `MapControllers()` in `Program.cs` — while keeping this
project's own middleware exactly as it is.

User modifications to the reference, decided in brainstorming:

- **Excluded** (user's request): `app.UseApiKeyValidation()`,
  `app.UseHeaderValidation()`.
- **CORS:** keep our `"Frontend"` policy (`CorsExtensions.CorsPolicyName`),
  not the reference's `"AllowAll"`.
- **Exception handling:** keep `ExceptionHandlingMiddleware` (the localized
  `ApiResponse` envelope contract) — the reference's
  `UseDeveloperExceptionPage` / `UseExceptionHandler("/error")` + `UseHsts`
  split was rejected as a breaking contract change.
- **HTTPS:** `UseHttpsRedirection`/`UseHsts` skipped — zero behavior change;
  add deliberately later (with forwarded-headers config) if ever needed.
- **Not requested (YAGNI):** `ValidateEnvironment`, `LogStartupInformation`,
  `UsePathBase`.

The result is a pure structure/comment refactor: same middleware components,
same execution order.

## Changes to existing files (2)

### 1. `Kheprx.BaseBackend.Api/Extensions/ApplicationBuilderExtensions.cs` — full rewrite

`UseSharedPipeline` → `UseBaseBackendMiddleware`; numbered ASCII banner
sections; `app.MapControllers()` removed from the method (moves to
`Program.cs`). Complete new content:

```csharp
using Kheprx.BaseBackend.Api.Middlewares;
using Serilog;

namespace Kheprx.BaseBackend.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseBaseBackendMiddleware(this WebApplication app, IWebHostEnvironment environment)
    {
        // ========================================
        // 1. Exception Handling
        // ========================================
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        // ========================================
        // 2. Swagger (Development only)
        // ========================================
        if (environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // ========================================
        // 3. Request Logging
        // ========================================
        app.UseSerilogRequestLogging();

        // ========================================
        // 4. CORS
        // ========================================
        app.UseCors(CorsExtensions.CorsPolicyName);

        // ========================================
        // 5. Authentication
        // ========================================
        app.UseAuthentication();

        // ========================================
        // 6. Authorization
        // ========================================
        app.UseAuthorization();

        return app;
    }
}
```

Notes:

- Usings, namespace, class name, and method signature shape are unchanged
  except the method name.
- Banner comments are plain ASCII (no emoji/Unicode), matching the repo's
  teaching-comment conventions. Bare numbered banners only — explanatory
  sublines can ride with a later teaching-comments pass if wanted.
- Middleware sequence is byte-for-byte the current `UseSharedPipeline`
  order minus the trailing `MapControllers` call.

### 2. `Kheprx.BaseBackend.Api/Program.cs` — tail only

Replace line 35 (`app.UseSharedPipeline(app.Environment);`) with the banner
and two calls. The file's tail becomes:

```csharp
// ========================================
// Configure Middleware Pipeline
// ========================================
app.UseBaseBackendMiddleware(app.Environment);
app.MapControllers();

app.Run();
```

Everything above (usings, service registration, `builder.Build()`, the
migration scope) is untouched.

## Behavior (unchanged)

Identical middleware components in identical order. Moving
`MapControllers()` from the method's last line to the `Program.cs` line
immediately after the pipeline call preserves the exact registration
sequence — endpoints still map after all middleware. The `"Frontend"` CORS
policy, `ExceptionHandlingMiddleware` envelope, Serilog request logging,
and JWT auth are all untouched.

## Testing / verification

- No test references `UseSharedPipeline` (verified by grep — code hits are
  only `Program.cs` and the extensions file itself).
- `dotnet build backend/Kheprx.BaseBackend.sln` — 0 errors, 0 new warnings.
- `dotnet test backend/Kheprx.BaseBackend.sln` — 147/147 (baseline
  unchanged).

## Follow-up (out of scope, already tracked)

- `HOST_API.html` and `IMODULE_CONVENTION.html` show `UseSharedPipeline` —
  both are already on the deferred 8-doc stale-HTML follow-up list
  (`2026-07-06-explicit-module-registration-design.md`); the rename adds to
  what that pass must reconcile.
- Historical superpowers spec/plan markdown mentioning `UseSharedPipeline`:
  dated records, left as-is.

## Decisions log

- **Full reference parity** chosen over banners-only or rename-with-
  MapControllers-inside: app-named method (`UseBaseBackendMiddleware`),
  `MapControllers` in `Program.cs`, numbered banners.
- **Keep `ExceptionHandlingMiddleware`** — preserves the `ApiResponse`
  error envelope; reference's dev-page/HSTS split rejected.
- **Skip HTTPS redirection + HSTS** — zero-behavior-change mandate; proxy
  considerations deferred.
- **User exclusions honored:** no `UseApiKeyValidation`, no
  `UseHeaderValidation`; CORS stays `"Frontend"`.
