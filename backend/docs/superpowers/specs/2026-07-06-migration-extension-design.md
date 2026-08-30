# Migration Extension Extraction — Design

**Date:** 2026-07-06
**Status:** Approved
**Scope:** Backend only (`Kheprx.BaseBackend.Api`). Zero runtime behavior change.

## Goal

Extract the startup database-migration block from `Program.cs` into a
dedicated per-concern extension file, continuing the established Api
`Extensions/` pattern (`CorsExtensions`, `FluentValidationExtensions`,
`ApplicationBuilderExtensions`). User chose a **new file in
`Api/Extensions`** over adding to `ApplicationBuilderExtensions.cs` (mixes
migration with middleware) or module-owned placement in
`Identity.Infrastructure/Extensions` (Api keeps its per-concern precedent).

The extraction is Identity-only, matching current behavior — no
generalization to other DbContexts (YAGNI; Catalog has no startup migration
today and this change adds none).

## New file (1)

`backend/Kheprx.BaseBackend.Api/Extensions/MigrationExtensions.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Api.Extensions;

public static class MigrationExtensions
{
    public static async Task ApplyIdentityMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync();
    }
}
```

Two deliberate shape changes from the original block, both
behavior-identical:

1. `using (var scope = …) { … }` becomes a `using` declaration — the scope
   still disposes before control returns to `Program.cs` (method end), and
   nothing executed between the old block's close and the next Program.cs
   statement.
2. The fully-qualified
   `Kheprx.BaseBackend.Identity.Infrastructure.Data.IdentityDbContext`
   becomes a using + short name.

Everything else is verbatim. `CreateScope`, `GetRequiredService`, `Task`,
and `WebApplication` are covered by the Web SDK's implicit usings — only
the two usings shown are required. Plain code, no teaching comments
(consistent with the refactor-first convention).

## Changes to existing files (1)

**`backend/Kheprx.BaseBackend.Api/Program.cs`** — the six-line migration
block is replaced by one awaited call (no banner), and two now-dead usings
are removed:

- `using Microsoft.EntityFrameworkCore;` — only consumer was
  `MigrateAsync`, which moved.
- `using Microsoft.Extensions.DependencyInjection;` — only consumer was the
  scope block; it is also in the SDK's implicit usings.

Complete new file content:

```csharp
using Kheprx.BaseBackend.Api.Extensions;
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
builder.Services.AddSharedInfrastructure();
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

## Behavior (unchanged)

Same scope creation, same service resolution, same awaited
`Database.MigrateAsync()` on the async main path before any middleware
setup, same disposal semantics. Identity-only migration stays
Identity-only.

## Testing / verification

- No test references the migration block or `Program.cs` startup flow
  (suite is unit-only — pre-existing, noted in the middleware-restyle
  final review).
- `dotnet build backend/Kheprx.BaseBackend.sln` — 0 errors, 0 new warnings
  (also proves the removed `Microsoft.EntityFrameworkCore` using was
  genuinely dead: any remaining consumer would fail to compile; the
  `Microsoft.Extensions.DependencyInjection` using is in the Web SDK's
  implicit usings, so its removal is safe regardless of the build —
  precision noted by the final review 2026-07-06).
- `dotnet test backend/Kheprx.BaseBackend.sln` — 147/147.

## Follow-up (out of scope, already tracked)

- Stale HTML teaching docs showing the old `Program.cs` — already on the
  deferred 8-doc follow-up list; this extraction adds to what that pass
  reconciles.
- Optional teaching comments on the new file — later pass if wanted.

## Decisions log

- **New per-concern file** in `Api/Extensions` over pipeline-file co-location
  and module-owned placement.
- **Name:** `ApplyIdentityMigrationsAsync` — says exactly what it does
  (Identity-only), await-shaped like the body it wraps.
- **No banner** over the call in `Program.cs` — per the approved preview;
  the call is self-describing.
- **Verbatim-but-for-shape:** `using` declaration + short type name are the
  only differences, both proven behavior-identical above.
