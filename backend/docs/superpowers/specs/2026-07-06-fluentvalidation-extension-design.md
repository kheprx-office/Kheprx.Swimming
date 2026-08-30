# FluentValidation Extension Extraction — Design

**Date:** 2026-07-06
**Status:** Approved
**Scope:** Backend only (`Kheprx.BaseBackend.Api`). Zero behavior change.

## Goal

Extract the FluentValidation validator registration currently inlined in
`ServiceCollectionExtensions.AddSharedInfrastructure` into a dedicated
`FluentValidationExtensions.cs` — the FluentValidation twin of the CORS
extraction (`2026-07-06-cors-extension-design.md`), following the same
per-concern extension pattern (`AuthenticationExtensions`, `CorsExtensions`)
and modeled on the user's reference file from the IslamicApplication project.

Structure changes only. Explicitly **not** adopted from the reference file:

- `AddFluentValidationAutoValidation()` / `AddFluentValidationClientsideAdapters()`
  — these come from the deprecated `FluentValidation.AspNetCore` package and
  would double-up validation alongside the project's custom `ValidationFilter`,
  changing error responses.
- The explicit per-module `AddValidatorsFromAssembly(typeof(...).Assembly)`
  list — the project's AppDomain scan auto-discovers module assemblies and is
  kept verbatim.

The `moduleAssemblies` scan moves with the registration line: the scan exists
only to feed it (single consumer, verified by grep).

## New file

`backend/Kheprx.BaseBackend.Api/Extensions/FluentValidationExtensions.cs`

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

Notes:

- Method name `AddFluentValidationConfiguration` matches the reference file
  (consistent with the `AddCorsConfiguration` precedent).
- Scan and registration are verbatim copies of the current code
  (same `Kheprx.BaseBackend.` ordinal prefix filter, same
  `includeInternalTypes: true` — kept for safety; it widens the scan to
  internal validators, though today's validators are all public).
- The XML summary documents the ordering constraint the code cannot show:
  the method must run after `AddModules`, because `AppDomain.GetAssemblies()`
  only returns already-loaded assemblies. Today this ordering is implicit in
  `AddSharedInfrastructure`'s position; the comment makes it explicit.
- `using FluentValidation;` is required for `AddValidatorsFromAssemblies`;
  everything else is covered by the Web SDK's implicit usings.

## Changes to existing files

1. **`ServiceCollectionExtensions.cs`** — delete:
   - the `moduleAssemblies` scan (lines 17–19),
   - the `services.AddValidatorsFromAssemblies(...)` line (line 64) and the
     blank line before it,
   - `using System.Reflection;` (line 1) and `using FluentValidation;`
     (line 6), both unused after the move.

   The method then contains only controllers + JSON + Swagger registration.
   `ValidationFilter` stays wired in `AddControllers` — it is MVC pipeline
   configuration, not validator registration, and is untouched.

2. **`Program.cs`** — add
   `builder.Services.AddFluentValidationConfiguration();` immediately after
   `AddSharedInfrastructure()` and before `AddCorsConfiguration(...)`:

   ```csharp
   builder.Services.AddModules(builder.Configuration);
   builder.Services.AddSharedInfrastructure();
   builder.Services.AddFluentValidationConfiguration();
   builder.Services.AddCorsConfiguration(builder.Configuration);
   builder.Services.AddJwtAuth(builder.Configuration);
   ```

   This keeps the scan after `AddModules` (required) and in the same relative
   order as today. Nothing between the old and new scan points loads
   assemblies, so the scanned set is identical.

## Behavior (unchanged)

- Same assembly filter, same `includeInternalTypes: true`.
- `ValidationFilter` enforcement and the `InvalidModelStateResponseFactory`
  envelope are untouched.
- DI registration order shifts by a few calls with no observable effect
  (validator registrations are independent of controllers/Swagger).

## Error handling

None new. The method has no failure modes beyond what exists today.

## Testing / verification

- No existing test references `AddSharedInfrastructure`,
  `AddValidatorsFromAssemblies`, or `moduleAssemblies` (verified by grep);
  validator unit tests construct validators directly via
  `FluentValidation.TestHelper`.
- Verify with `dotnet build backend/Kheprx.BaseBackend.sln` and the existing
  suite (expected 147/147).
- Accepted gap (same as the CORS run): no automated end-to-end check that
  validators resolve at runtime; the user's manual E2E pass covers it.

## Decisions log

- **Structure only:** user chose to match the reference file's structure while
  keeping the AppDomain auto-discovery scan and the custom `ValidationFilter`
  mechanism; auto-validation and the explicit assembly list were rejected.
- **Wired from `Program.cs`:** carried forward from the CORS-run decision
  (composition-root calls, matching `AddJwtAuth`/`AddCorsConfiguration`) —
  not re-asked.
