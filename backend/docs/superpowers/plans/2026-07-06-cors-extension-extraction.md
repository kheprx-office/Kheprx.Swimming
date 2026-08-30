# CORS Extension Extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract the CORS registration inlined in `ServiceCollectionExtensions.AddSharedInfrastructure` into a dedicated `CorsExtensions.cs`, wired from `Program.cs`, with zero behavior change.

**Architecture:** One new per-concern extension class (`CorsExtensions`) following the existing `AuthenticationExtensions` pattern: static class, file-scoped namespace, single `Add*` method returning `IServiceCollection`. The `CorsPolicyName` const moves into the new class; three existing files are edited to remove the old registration and wire the new one.

**Tech Stack:** ASP.NET Core (.NET, `Microsoft.NET.Sdk.Web` with implicit usings — the new file needs no `using` directives), xUnit test suite via `dotnet test`.

**Spec:** `backend/docs/superpowers/specs/2026-07-06-cors-extension-design.md`

## Global Constraints

- Zero behavior change: policy name stays exactly `"Frontend"`, config key stays exactly `Cors:Origins`, allow-all fallback only when the section is empty/missing, no `AllowCredentials` (auth is Bearer-token).
- Middleware order in `UseSharedPipeline` must not change (`UseCors` stays between `UseSerilogRequestLogging` and `UseAuthentication`).
- New extension method is called from `Program.cs` (composition root), NOT from inside `AddSharedInfrastructure`.
- No test changes: no existing test references CORS or `CorsPolicyName`.
- All `dotnet` commands run from the repo root against `backend/Kheprx.BaseBackend.sln`.

---

### Task 1: Extract CORS registration into CorsExtensions.cs

**Files:**
- Create: `backend/Kheprx.BaseBackend.Api/Extensions/CorsExtensions.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Extensions/ServiceCollectionExtensions.cs:15,69-76`
- Modify: `backend/Kheprx.BaseBackend.Api/Extensions/ApplicationBuilderExtensions.cs:19`
- Modify: `backend/Kheprx.BaseBackend.Api/Program.cs:13-14`
- Test: none (structural refactor; existing suite guards against regressions)

**Interfaces:**
- Consumes: `Cors:Origins` string-array section from `appsettings.json` (already present: `["http://localhost:4200"]`).
- Produces: `public const string CorsExtensions.CorsPolicyName = "Frontend"` and `public static IServiceCollection AddCorsConfiguration(this IServiceCollection services, IConfiguration configuration)` — consumed by `ApplicationBuilderExtensions.UseSharedPipeline` and `Program.cs` respectively.

- [ ] **Step 1: Create the new extension file**

Create `backend/Kheprx.BaseBackend.Api/Extensions/CorsExtensions.cs` with exactly:

```csharp
namespace Kheprx.BaseBackend.Api.Extensions;

public static class CorsExtensions
{
    public const string CorsPolicyName = "Frontend";

    /// <summary>
    /// Configures the named CORS policy from the Cors:Origins configuration section.
    /// Falls back to allowing any origin when unconfigured (dev only).
    /// </summary>
    public static IServiceCollection AddCorsConfiguration(
        this IServiceCollection services, IConfiguration configuration)
    {
        var corsOrigins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
        {
            if (corsOrigins.Length > 0)
                policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod(); // no AllowCredentials — Bearer tokens
            else
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod(); // dev fallback when unconfigured
        }));
        return services;
    }
}
```

No `using` directives are needed — the Web SDK's implicit usings cover `IServiceCollection`, `IConfiguration`, and `Array`. (Sibling file `AuthenticationExtensions.cs` relies on the same.)

- [ ] **Step 2: Build to verify the new file compiles alongside the old code**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: `Build succeeded.` with `0 Error(s)`. (Both classes temporarily define a `CorsPolicyName` const — that's legal since they're separate classes.)

- [ ] **Step 3: Remove the old const and CORS block from ServiceCollectionExtensions.cs**

In `backend/Kheprx.BaseBackend.Api/Extensions/ServiceCollectionExtensions.cs`, delete the const (line 15):

```csharp
    public const string CorsPolicyName = "Frontend";
```

(leaving `public static class ServiceCollectionExtensions {` immediately followed by the `AddSharedInfrastructure` method), and delete the CORS registration block (lines 69–76):

```csharp
        var corsOrigins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
        {
            if (corsOrigins.Length > 0)
                policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod(); // no AllowCredentials — Bearer tokens
            else
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod(); // dev fallback when unconfigured
        }));
```

so the method ends with `services.AddValidatorsFromAssemblies(...)` directly followed by `return services;`. Nothing else in the file changes.

- [ ] **Step 4: Point ApplicationBuilderExtensions at the new const**

In `backend/Kheprx.BaseBackend.Api/Extensions/ApplicationBuilderExtensions.cs` line 19, change:

```csharp
        app.UseCors(ServiceCollectionExtensions.CorsPolicyName);
```

to:

```csharp
        app.UseCors(CorsExtensions.CorsPolicyName);
```

Do not reorder any middleware.

- [ ] **Step 5: Wire the new extension in Program.cs**

In `backend/Kheprx.BaseBackend.Api/Program.cs`, change lines 12–14 from:

```csharp
builder.Services.AddModules(builder.Configuration);
builder.Services.AddSharedInfrastructure(builder.Configuration);
builder.Services.AddJwtAuth(builder.Configuration);
```

to:

```csharp
builder.Services.AddModules(builder.Configuration);
builder.Services.AddSharedInfrastructure(builder.Configuration);
builder.Services.AddCorsConfiguration(builder.Configuration);
builder.Services.AddJwtAuth(builder.Configuration);
```

- [ ] **Step 6: Build to verify the refactor compiles**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: `Build succeeded.` with `0 Error(s)`. If it reports `ServiceCollectionExtensions` does not contain a definition for `CorsPolicyName`, Step 4 was missed.

- [ ] **Step 7: Run the existing test suite**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: all four test projects pass, `Failed: 0`. No test references CORS, so any failure means a regression outside the intended change — stop and investigate.

- [ ] **Step 8 (optional smoke check): Verify the CORS header end-to-end**

Note: this boots the API against the production Aiven database (startup runs migrations). Skip if that's undesirable right now.

Run the API (`dotnet run --project backend/Kheprx.BaseBackend.Api`), then from another shell:

```powershell
curl.exe -s -i -X OPTIONS http://localhost:5080/api/auth/login -H "Origin: http://localhost:4200" -H "Access-Control-Request-Method: POST" | Select-String "Access-Control"
```

Expected: response includes `Access-Control-Allow-Origin: http://localhost:4200`. Stop the API afterwards.

- [ ] **Step 9: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Extensions/CorsExtensions.cs backend/Kheprx.BaseBackend.Api/Extensions/ServiceCollectionExtensions.cs backend/Kheprx.BaseBackend.Api/Extensions/ApplicationBuilderExtensions.cs backend/Kheprx.BaseBackend.Api/Program.cs
git commit -m "refactor(api): extract CORS registration into CorsExtensions"
```
