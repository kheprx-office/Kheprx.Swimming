# Migration Extension Extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract the startup Identity migration block from `Program.cs` into a new `MigrationExtensions.cs` with `ApplyIdentityMigrationsAsync`, removing two dead usings — zero behavior change.

**Architecture:** Consumer-first extraction: rewrite `Program.cs` to call the not-yet-existing extension (RED — compiler proves the call site), then create the extension file (GREEN). The body moves verbatim except two documented behavior-identical shape changes: `using` block → `using` declaration, fully-qualified `IdentityDbContext` → using + short name.

**Tech Stack:** .NET 10, EF Core (Npgsql), xUnit. No new dependencies.

**Spec:** `backend/docs/superpowers/specs/2026-07-06-migration-extension-design.md` (approved).

## Global Constraints

- Zero runtime behavior change: same scope creation, same service resolution, same awaited `Database.MigrateAsync()` before any middleware setup, same disposal semantics. Identity-only stays Identity-only — no Catalog migration is added.
- New file is plain code — no teaching comments, no XML docs.
- Usings in the new file: exactly `Kheprx.BaseBackend.Identity.Infrastructure.Data` and `Microsoft.EntityFrameworkCore` (everything else comes from the Web SDK's implicit usings).
- Do NOT touch the 8 stale HTML teaching docs in `backend/docs`.
- Do NOT stage `frontend/angular.json`. Always `git add` explicit paths, never `git add -A` or `git add .`.
- All commands run from the repo root (`C:\Users\envnt\Desktop\Kheprx.Electric`).
- Build/test gate: `dotnet build` 0 errors / 0 new warnings; `dotnet test` 147/147.

---

### Task 1: Extract the migration block into MigrationExtensions.cs

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Program.cs` (whole file — 41 lines become 34; execution note 2026-07-06: this line originally said "36 become 32", counted from a pre-middleware-restyle snapshot)
- Create: `backend/Kheprx.BaseBackend.Api/Extensions/MigrationExtensions.cs`
- Test: full solution build + existing suite (no new test — extraction refactor; the compiler is the RED/GREEN gate, and the green build additionally proves the two removed usings were dead)

**Interfaces:**
- Consumes: `IdentityDbContext` (namespace `Kheprx.BaseBackend.Identity.Infrastructure.Data`), `UseBaseBackendMiddleware` / module registration extensions (unchanged).
- Produces: `public static async Task ApplyIdentityMigrationsAsync(this WebApplication app)` in `Kheprx.BaseBackend.Api.Extensions.MigrationExtensions`. No other task follows.

- [ ] **Step 1: Rewrite Program.cs (consumer first)**

Replace the entire content of `backend/Kheprx.BaseBackend.Api/Program.cs` with:

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

Diff vs. current: the six-line `using (var scope = …) { … }` migration block becomes `await app.ApplyIdentityMigrationsAsync();`, and two usings are removed (`Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.DependencyInjection` — both only served the removed block; DI is also in the SDK's implicit usings). Everything else is unchanged.

- [ ] **Step 2: Verify the build fails (extension does not exist yet)**

Run:
```bash
dotnet build backend/Kheprx.BaseBackend.sln
```

Expected: **build FAILURE** with `error CS1061: 'WebApplication' does not contain a definition for 'ApplyIdentityMigrationsAsync'` in `Program.cs`. This is the RED state — it proves the call site is wired before the implementation exists.

- [ ] **Step 3: Create MigrationExtensions.cs**

Create `backend/Kheprx.BaseBackend.Api/Extensions/MigrationExtensions.cs` with exactly:

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

This is the old Program.cs block with exactly two shape changes (both behavior-identical, per the spec): `using` statement → `using` declaration, and fully-qualified `IdentityDbContext` → using + short name.

- [ ] **Step 4: Build the solution (GREEN)**

Run:
```bash
dotnet build backend/Kheprx.BaseBackend.sln
```

Expected: `Build succeeded` with 0 errors, 0 new warnings. (A clean build also proves the two removed Program.cs usings were genuinely dead.)

- [ ] **Step 5: Run the full test suite**

Run:
```bash
dotnet test backend/Kheprx.BaseBackend.sln
```

Expected: PASS — `Failed: 0` across all four projects (Identity 86 + Catalog 16 + Api 33 + Architecture 12 = 147).

- [ ] **Step 6: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Program.cs backend/Kheprx.BaseBackend.Api/Extensions/MigrationExtensions.cs
git commit -m "refactor(api): extract identity migration into MigrationExtensions

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Out of Scope (tracked in the spec)

- Stale HTML teaching docs showing the old `Program.cs` — deferred 8-doc pass.
- Teaching comments on the new file — later pass if wanted.
- Catalog migrations — none exist at startup today; none added.
