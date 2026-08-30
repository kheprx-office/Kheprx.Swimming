# Middleware Pipeline Restyle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename `UseSharedPipeline` to `UseBaseBackendMiddleware` with numbered banner sections, and move `MapControllers()` to `Program.cs` under a `// Configure Middleware Pipeline` banner — zero behavior change.

**Architecture:** Pure structure/comment refactor. The extensions file is rewritten with the exact content from the spec (same middleware, same order, minus the trailing `MapControllers`); `Program.cs` replaces its one pipeline line with the banner plus two calls. The compiler is the RED/GREEN gate: after the rename, the old call site fails to build until updated.

**Tech Stack:** .NET 10, ASP.NET Core, Serilog, xUnit. No new dependencies.

**Spec:** `backend/docs/superpowers/specs/2026-07-06-middleware-pipeline-restyle-design.md` (approved).

## Global Constraints

- Zero runtime behavior change: identical middleware components in identical order (`ExceptionHandlingMiddleware` → dev Swagger → Serilog request logging → CORS `"Frontend"` → AuthN → AuthZ → endpoints). `MapControllers()` moves to the `Program.cs` line immediately after the pipeline call — same registration sequence.
- Do NOT adopt from the reference: `UseApiKeyValidation`, `UseHeaderValidation`, `"AllowAll"` CORS, `UseDeveloperExceptionPage`/`UseExceptionHandler`/`UseHsts`, `UseHttpsRedirection`, `ValidateEnvironment`, `LogStartupInformation`, `UsePathBase` (all rejected in the spec's decisions log).
- Banner comments are plain ASCII, exact text as shown in the steps below.
- Do NOT touch the 8 stale HTML teaching docs in `backend/docs`.
- Do NOT stage `frontend/angular.json`. Always `git add` explicit paths, never `git add -A` or `git add .`.
- All commands run from the repo root (`C:\Users\envnt\Desktop\Kheprx.Electric`).
- Build/test gate: `dotnet build` 0 errors / 0 new warnings; `dotnet test` 147/147.

---

### Task 1: Restyle the pipeline extension and its Program.cs call site

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Extensions/ApplicationBuilderExtensions.cs` (whole file — 25 lines)
- Modify: `backend/Kheprx.BaseBackend.Api/Program.cs:35` (one line becomes six)
- Test: full solution build + existing suite (no new test — rename/comment refactor; the compiler is the RED/GREEN gate and the 147-test suite pins behavior)

**Interfaces:**
- Consumes: existing middleware — `ExceptionHandlingMiddleware` (namespace `Kheprx.BaseBackend.Api.Middlewares`), `CorsExtensions.CorsPolicyName` (same `Kheprx.BaseBackend.Api.Extensions` namespace).
- Produces: `public static WebApplication UseBaseBackendMiddleware(this WebApplication app, IWebHostEnvironment environment)` in `Kheprx.BaseBackend.Api.Extensions.ApplicationBuilderExtensions`. `UseSharedPipeline` ceases to exist. No other task follows.

- [ ] **Step 1: Rewrite ApplicationBuilderExtensions.cs**

Replace the entire content of `backend/Kheprx.BaseBackend.Api/Extensions/ApplicationBuilderExtensions.cs` with:

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

Relative to the current file this renames the method, inserts the six numbered banners, and deletes the `app.MapControllers();` line — the middleware calls themselves are byte-identical and in the same order.

- [ ] **Step 2: Verify the build fails (Program.cs still calls the old name)**

Run:
```bash
dotnet build backend/Kheprx.BaseBackend.sln
```

Expected: **build FAILURE** with `error CS1061: 'WebApplication' does not contain a definition for 'UseSharedPipeline'` at `Program.cs` line 35. This is the RED state — it proves the old entry point is gone and the call site must move to the new name.

- [ ] **Step 3: Update the Program.cs tail**

In `backend/Kheprx.BaseBackend.Api/Program.cs`, replace the single line:

```csharp
app.UseSharedPipeline(app.Environment);
```

with:

```csharp
// ========================================
// Configure Middleware Pipeline
// ========================================
app.UseBaseBackendMiddleware(app.Environment);
app.MapControllers();
```

The file's tail (from `var app = builder.Build();` down) becomes exactly:

```csharp
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<Kheprx.BaseBackend.Identity.Infrastructure.Data.IdentityDbContext>();
    await db.Database.MigrateAsync();
}

// ========================================
// Configure Middleware Pipeline
// ========================================
app.UseBaseBackendMiddleware(app.Environment);
app.MapControllers();

app.Run();
```

Nothing above `var app = builder.Build();` changes.

- [ ] **Step 4: Build the solution (GREEN)**

Run:
```bash
dotnet build backend/Kheprx.BaseBackend.sln
```

Expected: `Build succeeded` with 0 errors, 0 new warnings.

- [ ] **Step 5: Run the full test suite**

Run:
```bash
dotnet test backend/Kheprx.BaseBackend.sln
```

Expected: PASS — `Failed: 0` across all four projects (Identity 86 + Catalog 16 + Api 33 + Architecture 12 = 147).

- [ ] **Step 6: Grep guard — no code reference to the old name remains**

Run:
```bash
git grep -n "UseSharedPipeline" -- backend/src backend/Kheprx.BaseBackend.Api backend/tests
```

Expected: **no matches** (exit code 1). Scope deliberately excludes `backend/docs` — the stale HTML docs and historical planning markdown mention the old name by design (deferred docs pass / dated records). A hit inside the scoped paths means a code consumer was missed — stop and fix it before committing.

- [ ] **Step 7: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Extensions/ApplicationBuilderExtensions.cs backend/Kheprx.BaseBackend.Api/Program.cs
git commit -m "refactor(api): restyle middleware pipeline as UseBaseBackendMiddleware with banner sections

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Out of Scope (tracked in the spec)

- `HOST_API.html` / `IMODULE_CONVENTION.html` mentions of `UseSharedPipeline` — already on the deferred 8-doc follow-up list; this rename adds to what that pass reconciles.
- Historical superpowers spec/plan markdown — dated records, left as-is.
- Optional explanatory sublines under the banners — a later teaching-comments pass if wanted.
