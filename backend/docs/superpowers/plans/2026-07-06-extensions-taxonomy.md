# Api Extensions Per-Concern Taxonomy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the six files in `Kheprx.BaseBackend.Api/Extensions/` into per-concern subfolders (`Security/`, `Validation/`, `Mvc/`, `Data/`, `Pipeline/`) with matching namespaces, updating the two consumer files — zero behavior change.

**Architecture:** Pure relocation refactor: `git mv` each file, change its `namespace` line, then fix the two consumers (`Program.cs` 1→5 usings; `ApplicationBuilderExtensions.cs` gains the `Security` using for `CorsExtensions.CorsPolicyName`). The compiler is the RED/GREEN gate — note the RED failures are `CS1061`/`CS0103` on member references, NOT a using error, because the child namespaces keep the parent `…Api.Extensions` namespace alive.

**Tech Stack:** .NET 10, xUnit. No new dependencies.

**Spec:** `backend/docs/superpowers/specs/2026-07-06-extensions-taxonomy-design.md` (approved).

## Global Constraints

- Zero runtime behavior change: same methods, same registrations, same pipeline order, same call sites. Locations, namespaces, and using lines only.
- New namespaces exactly: `Kheprx.BaseBackend.Api.Extensions.Security` (Authentication + Cors), `…Extensions.Validation` (FluentValidation), `…Extensions.Mvc` (ServiceCollection), `…Extensions.Data` (Migration), `…Extensions.Pipeline` (ApplicationBuilder).
- The `namespace` line is the **only** content change in five of the six moved files; `ApplicationBuilderExtensions.cs` additionally gains exactly one using.
- No csproj edits. Do NOT touch the 8 stale HTML teaching docs in `backend/docs`.
- Do NOT stage `frontend/angular.json`. Always `git add` explicit paths, never `git add -A` or `git add .`.
- Do NOT write build/test output to log files in the repo — capture output directly from command results.
- All commands run from the repo root (`C:\Users\envnt\Desktop\Kheprx.Electric`).
- Build/test gate: `dotnet build` 0 errors / 0 new warnings; `dotnet test` 147/147.

---

### Task 1: Move all six extension files into per-concern subfolders

**Files:**
- Move: `backend/Kheprx.BaseBackend.Api/Extensions/AuthenticationExtensions.cs` → `backend/Kheprx.BaseBackend.Api/Extensions/Security/AuthenticationExtensions.cs`
- Move: `backend/Kheprx.BaseBackend.Api/Extensions/CorsExtensions.cs` → `backend/Kheprx.BaseBackend.Api/Extensions/Security/CorsExtensions.cs`
- Move: `backend/Kheprx.BaseBackend.Api/Extensions/FluentValidationExtensions.cs` → `backend/Kheprx.BaseBackend.Api/Extensions/Validation/FluentValidationExtensions.cs`
- Move: `backend/Kheprx.BaseBackend.Api/Extensions/ServiceCollectionExtensions.cs` → `backend/Kheprx.BaseBackend.Api/Extensions/Mvc/ServiceCollectionExtensions.cs`
- Move: `backend/Kheprx.BaseBackend.Api/Extensions/MigrationExtensions.cs` → `backend/Kheprx.BaseBackend.Api/Extensions/Data/MigrationExtensions.cs`
- Move: `backend/Kheprx.BaseBackend.Api/Extensions/ApplicationBuilderExtensions.cs` → `backend/Kheprx.BaseBackend.Api/Extensions/Pipeline/ApplicationBuilderExtensions.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Program.cs:1` (one using becomes five)
- Test: full solution build + existing suite (no new test — relocation only; compiler is the RED/GREEN gate, 147-test suite pins behavior)

**Interfaces:**
- Consumes: `CorsExtensions.CorsPolicyName` (moves to `…Extensions.Security`, consumed by `ApplicationBuilderExtensions` in `…Extensions.Pipeline`).
- Produces: the same six public extension classes under the five new namespaces listed in Global Constraints. No other task follows.

- [ ] **Step 1: Move all six files with git mv**

```bash
git mv backend/Kheprx.BaseBackend.Api/Extensions/AuthenticationExtensions.cs backend/Kheprx.BaseBackend.Api/Extensions/Security/AuthenticationExtensions.cs
git mv backend/Kheprx.BaseBackend.Api/Extensions/CorsExtensions.cs backend/Kheprx.BaseBackend.Api/Extensions/Security/CorsExtensions.cs
git mv backend/Kheprx.BaseBackend.Api/Extensions/FluentValidationExtensions.cs backend/Kheprx.BaseBackend.Api/Extensions/Validation/FluentValidationExtensions.cs
git mv backend/Kheprx.BaseBackend.Api/Extensions/ServiceCollectionExtensions.cs backend/Kheprx.BaseBackend.Api/Extensions/Mvc/ServiceCollectionExtensions.cs
git mv backend/Kheprx.BaseBackend.Api/Extensions/MigrationExtensions.cs backend/Kheprx.BaseBackend.Api/Extensions/Data/MigrationExtensions.cs
git mv backend/Kheprx.BaseBackend.Api/Extensions/ApplicationBuilderExtensions.cs backend/Kheprx.BaseBackend.Api/Extensions/Pipeline/ApplicationBuilderExtensions.cs
```

(`git mv` creates the five folders and stages the renames.)

- [ ] **Step 2: Update the namespace line in each moved file**

All six files currently declare the same line:

```csharp
namespace Kheprx.BaseBackend.Api.Extensions;
```

Replace it per file:

- `Extensions/Security/AuthenticationExtensions.cs` and `Extensions/Security/CorsExtensions.cs`:
  ```csharp
  namespace Kheprx.BaseBackend.Api.Extensions.Security;
  ```
- `Extensions/Validation/FluentValidationExtensions.cs`:
  ```csharp
  namespace Kheprx.BaseBackend.Api.Extensions.Validation;
  ```
- `Extensions/Mvc/ServiceCollectionExtensions.cs`:
  ```csharp
  namespace Kheprx.BaseBackend.Api.Extensions.Mvc;
  ```
- `Extensions/Data/MigrationExtensions.cs`:
  ```csharp
  namespace Kheprx.BaseBackend.Api.Extensions.Data;
  ```
- `Extensions/Pipeline/ApplicationBuilderExtensions.cs`:
  ```csharp
  namespace Kheprx.BaseBackend.Api.Extensions.Pipeline;
  ```

No other line changes in this step.

- [ ] **Step 3: Verify the build fails (consumers not yet updated)**

Run:
```bash
dotnet build backend/Kheprx.BaseBackend.sln
```

Expected: **build FAILURE**. Representative errors (any subset counts as the RED state):
- `error CS0103: The name 'CorsExtensions' does not exist in the current context` in `Extensions/Pipeline/ApplicationBuilderExtensions.cs` (sibling namespace lookup doesn't reach `…Extensions.Security`).
- `error CS1061: 'IServiceCollection' does not contain a definition for 'AddSharedInfrastructure'` (and/or `AddFluentValidationConfiguration`, `AddCorsConfiguration`, `AddJwtAuth`) in `Program.cs`.
- `error CS1061: 'WebApplication' does not contain a definition for 'ApplyIdentityMigrationsAsync'` (and/or `UseBaseBackendMiddleware`) in `Program.cs`.

Note: `using Kheprx.BaseBackend.Api.Extensions;` itself does NOT error — the child namespaces keep the parent alive — which is why the failures appear at the member references, not the using.

- [ ] **Step 4: Update the using block in ApplicationBuilderExtensions.cs**

In `backend/Kheprx.BaseBackend.Api/Extensions/Pipeline/ApplicationBuilderExtensions.cs`, replace the using block:

```csharp
using Kheprx.BaseBackend.Api.Middlewares;
using Serilog;
```

with:

```csharp
using Kheprx.BaseBackend.Api.Extensions.Security;
using Kheprx.BaseBackend.Api.Middlewares;
using Serilog;
```

(`Extensions.Security` sorts before `Middlewares`.) Nothing else in the file changes in this step.

- [ ] **Step 5: Update the using block in Program.cs**

In `backend/Kheprx.BaseBackend.Api/Program.cs`, replace line 1:

```csharp
using Kheprx.BaseBackend.Api.Extensions;
```

with:

```csharp
using Kheprx.BaseBackend.Api.Extensions.Data;
using Kheprx.BaseBackend.Api.Extensions.Mvc;
using Kheprx.BaseBackend.Api.Extensions.Pipeline;
using Kheprx.BaseBackend.Api.Extensions.Security;
using Kheprx.BaseBackend.Api.Extensions.Validation;
```

The complete using block becomes:

```csharp
using Kheprx.BaseBackend.Api.Extensions.Data;
using Kheprx.BaseBackend.Api.Extensions.Mvc;
using Kheprx.BaseBackend.Api.Extensions.Pipeline;
using Kheprx.BaseBackend.Api.Extensions.Security;
using Kheprx.BaseBackend.Api.Extensions.Validation;
using Kheprx.BaseBackend.Catalog.Infrastructure.Extensions;
using Kheprx.BaseBackend.Identity.Infrastructure.Extensions;
using Serilog;
```

Nothing else in the file changes.

- [ ] **Step 6: Build the solution (GREEN)**

Run:
```bash
dotnet build backend/Kheprx.BaseBackend.sln
```

Expected: `Build succeeded` with 0 errors, 0 new warnings.

- [ ] **Step 7: Run the full test suite**

Run:
```bash
dotnet test backend/Kheprx.BaseBackend.sln
```

Expected: PASS — `Failed: 0` across all four projects (Identity 86 + Catalog 16 + Api 33 + Architecture 12 = 147).

- [ ] **Step 8: Grep guard — the old flat using is gone from code paths**

Run:
```bash
git grep -n "using Kheprx.BaseBackend.Api.Extensions;" -- backend/src backend/Kheprx.BaseBackend.Api backend/tests
```

Expected: **no matches** (exit code 1). The exact string (with trailing semicolon) does not match the new sub-namespace usings. Scope excludes `backend/docs` — historical planning markdown quotes the old using by design. A hit inside the scoped paths means a consumer was missed — stop and fix before committing.

- [ ] **Step 9: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Extensions/Security/AuthenticationExtensions.cs backend/Kheprx.BaseBackend.Api/Extensions/Security/CorsExtensions.cs backend/Kheprx.BaseBackend.Api/Extensions/Validation/FluentValidationExtensions.cs backend/Kheprx.BaseBackend.Api/Extensions/Mvc/ServiceCollectionExtensions.cs backend/Kheprx.BaseBackend.Api/Extensions/Data/MigrationExtensions.cs backend/Kheprx.BaseBackend.Api/Extensions/Pipeline/ApplicationBuilderExtensions.cs backend/Kheprx.BaseBackend.Api/Program.cs
git commit -m "refactor(api): categorize Extensions into per-concern subfolders

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

(The renames were staged by `git mv`; the `git add` picks up the post-move namespace/using edits at the new paths plus Program.cs.)

---

## Out of Scope (tracked in the spec)

- Stale HTML teaching docs showing the flat Extensions layout — deferred 8-doc pass.
- Teaching comments — later pass if wanted.
- Module Infrastructure `Extensions/` folders — already done in a prior run, untouched here.
