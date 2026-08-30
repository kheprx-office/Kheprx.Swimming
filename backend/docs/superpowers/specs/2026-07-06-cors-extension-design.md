# CORS Extension Extraction — Design

**Date:** 2026-07-06
**Status:** Approved
**Scope:** Backend only (`Kheprx.BaseBackend.Api`). Zero behavior change.

## Goal

Extract the CORS registration currently inlined in
`ServiceCollectionExtensions.AddSharedInfrastructure` into a dedicated
`CorsExtensions.cs`, following the per-concern extension-file shape already
used by `AuthenticationExtensions` (and modeled on the user's reference file
from the IslamicApplication project).

Structure changes only. The existing config-driven policy is kept as-is; the
reference file's hardcoded AllowAll behavior is explicitly **not** adopted
(this API runs against the production Aiven database).

## New file

`backend/Kheprx.BaseBackend.Api/Extensions/CorsExtensions.cs`

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

Notes:

- The `CorsPolicyName` const moves here from `ServiceCollectionExtensions`
  (it belongs with the policy definition).
- The method takes `IConfiguration`, unlike the reference file, because the
  policy is config-driven — same signature pattern as `AddJwtAuth`.
- File-scoped namespace, static class, `Add*` method returning
  `IServiceCollection`, XML doc summary: matches both the reference file and
  `AuthenticationExtensions`.

## Changes to existing files

1. **`ServiceCollectionExtensions.cs`** — delete the `CorsPolicyName` const
   (line 15) and the CORS registration block (lines 69–76). Nothing else moves.
2. **`ApplicationBuilderExtensions.cs`** — line 19 becomes
   `app.UseCors(CorsExtensions.CorsPolicyName);`.
3. **`Program.cs`** — add
   `builder.Services.AddCorsConfiguration(builder.Configuration);` after
   `AddSharedInfrastructure`, before `AddJwtAuth`. Registration order does not
   matter for CORS; this placement is readable grouping only.

## Behavior (unchanged)

- Policy name: `"Frontend"`.
- Origins read from `Cors:Origins` in appsettings
  (currently `["http://localhost:4200"]`).
- Allow-all fallback only when the section is empty or missing.
- No `AllowCredentials` — auth uses Bearer tokens, not cookies.
- Middleware order in `UseSharedPipeline` untouched
  (`UseCors` stays between request logging and `UseAuthentication`).

## Error handling

None new. A missing or empty `Cors:Origins` section falls back to allow-all,
exactly as today.

## Testing / verification

- No existing test references CORS or `CorsPolicyName` (verified by grep over
  `backend/tests`), so no test changes are required.
- Verify with `dotnet build` and the existing test suite.
- Optional smoke check: boot the API and confirm the
  `Access-Control-Allow-Origin` response header on a request sent with
  `Origin: http://localhost:4200`.

## Decisions log

- **Structure only, not AllowAll:** user chose to match the reference file's
  structure while keeping the current restricted, config-driven policy.
- **Wired from `Program.cs`:** user chose the composition-root call (matching
  the `AddJwtAuth` precedent) over delegating from inside
  `AddSharedInfrastructure`.
- **Post-review follow-up (2026-07-06):** dropped the now-unused
  `IConfiguration` parameter from `AddSharedInfrastructure` (its only
  consumer was the extracted CORS block). Final-review Minor finding,
  user-approved.
