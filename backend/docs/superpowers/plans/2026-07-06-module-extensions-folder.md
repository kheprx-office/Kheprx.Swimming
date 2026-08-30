# Module Extensions Folder Move Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move `CatalogModuleExtensions.cs` and `IdentityModuleExtensions.cs` from their Infrastructure project roots into `Extensions/` folders with matching namespaces, and update the 4 consumer using lines plus 1 README line.

**Architecture:** Pure relocation refactor — `git mv` each file, change its `namespace` line (the only content edit), suffix-swap the two usings in each of the two consumer files. Matches the Api project's `Extensions/` folder-and-namespace pattern and the repo's `dotnet_style_namespace_match_folder = true` editorconfig rule.

**Tech Stack:** .NET 10, xUnit. No new dependencies.

**Spec:** `backend/docs/superpowers/specs/2026-07-06-module-extensions-folder-design.md` (approved).

## Global Constraints

- Zero behavior change: same registrations, same lifetimes, same Program.cs execution order. The `namespace` declaration is the **only** content change in each moved file — every other line stays byte-identical.
- New namespaces exactly: `Kheprx.BaseBackend.Catalog.Infrastructure.Extensions` and `Kheprx.BaseBackend.Identity.Infrastructure.Extensions`.
- No csproj edits (SDK-style projects glob all `.cs`).
- Do NOT touch the 8 stale HTML teaching docs in `backend/docs` (separate deferred docs pass).
- Do NOT stage `frontend/angular.json` (unrelated uncommitted changes). Always `git add` explicit paths, never `git add -A` or `git add .`.
- All commands run from the repo root (`C:\Users\envnt\Desktop\Kheprx.Electric`).
- Build/test gate: `dotnet build` 0 errors / 0 new warnings; `dotnet test` 147/147.

---

### Task 1: Move both extension classes into Extensions/ folders

**Files:**
- Move: `backend/src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Infrastructure/CatalogModuleExtensions.cs` → `backend/src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Infrastructure/Extensions/CatalogModuleExtensions.cs`
- Move: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/IdentityModuleExtensions.cs` → `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Program.cs:2-3` (two using lines)
- Modify: `backend/tests/Kheprx.BaseBackend.ArchitectureTests/ModuleConventionTests.cs:3,6` (two using lines)
- Modify: `backend/README.md:57` (one line)
- Test: full solution build + existing suite (no new test — relocation only; the compiler is the RED/GREEN gate and the 147-test suite pins behavior)

**Interfaces:**
- Consumes: `AddCatalogModule(this IServiceCollection, IConfiguration)` and `AddIdentityModule(this IServiceCollection, IConfiguration)` — signatures unchanged.
- Produces: the same two methods, now in namespaces `Kheprx.BaseBackend.Catalog.Infrastructure.Extensions` / `Kheprx.BaseBackend.Identity.Infrastructure.Extensions`. No other task follows.

- [ ] **Step 1: Move both files with git mv**

```bash
git mv backend/src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Infrastructure/CatalogModuleExtensions.cs backend/src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Infrastructure/Extensions/CatalogModuleExtensions.cs
git mv backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/IdentityModuleExtensions.cs backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs
```

(`git mv` creates the `Extensions/` directories and stages the renames.)

- [ ] **Step 2: Update the namespace line in each moved file**

In `backend/src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Infrastructure/Extensions/CatalogModuleExtensions.cs`, replace:

```csharp
namespace Kheprx.BaseBackend.Catalog.Infrastructure;
```

with:

```csharp
namespace Kheprx.BaseBackend.Catalog.Infrastructure.Extensions;
```

In `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs`, replace:

```csharp
namespace Kheprx.BaseBackend.Identity.Infrastructure;
```

with:

```csharp
namespace Kheprx.BaseBackend.Identity.Infrastructure.Extensions;
```

No other line in either file changes — the existing usings all reference sibling namespaces explicitly and remain valid.

- [ ] **Step 3: Verify the build fails (consumers still use the old namespaces)**

Run:
```bash
dotnet build backend/Kheprx.BaseBackend.sln
```

Expected: **build FAILURE** with `error CS1061: 'IServiceCollection' does not contain a definition for 'AddCatalogModule'` (or `AddIdentityModule`) in `Program.cs`, and the same in `ModuleConventionTests.cs`. This is the relocation refactor's RED state — it proves the old namespaces no longer expose the methods.

- [ ] **Step 4: Update the two usings in Program.cs**

In `backend/Kheprx.BaseBackend.Api/Program.cs`, replace lines 2-3:

```csharp
using Kheprx.BaseBackend.Catalog.Infrastructure;
using Kheprx.BaseBackend.Identity.Infrastructure;
```

with:

```csharp
using Kheprx.BaseBackend.Catalog.Infrastructure.Extensions;
using Kheprx.BaseBackend.Identity.Infrastructure.Extensions;
```

The complete using block becomes (alphabetical positions unchanged):

```csharp
using Kheprx.BaseBackend.Api.Extensions;
using Kheprx.BaseBackend.Catalog.Infrastructure.Extensions;
using Kheprx.BaseBackend.Identity.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
```

Nothing else in the file changes (the fully-qualified `Kheprx.BaseBackend.Identity.Infrastructure.Data.IdentityDbContext` in the migration scope is unaffected).

- [ ] **Step 5: Update the two usings in ModuleConventionTests.cs**

In `backend/tests/Kheprx.BaseBackend.ArchitectureTests/ModuleConventionTests.cs`, replace:

```csharp
using Kheprx.BaseBackend.Catalog.Infrastructure;
```

with:

```csharp
using Kheprx.BaseBackend.Catalog.Infrastructure.Extensions;
```

and replace:

```csharp
using Kheprx.BaseBackend.Identity.Infrastructure;
```

with:

```csharp
using Kheprx.BaseBackend.Identity.Infrastructure.Extensions;
```

The complete using block becomes (alphabetical positions unchanged):

```csharp
using Kheprx.BaseBackend.Catalog.Application.Services.Interfaces;
using Kheprx.BaseBackend.Catalog.Contracts;
using Kheprx.BaseBackend.Catalog.Infrastructure.Extensions;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Contracts;
using Kheprx.BaseBackend.Identity.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
```

Test bodies are untouched.

- [ ] **Step 6: Update README add-a-module step 2**

In `backend/README.md` line 57, replace:

```markdown
2. In `.Infrastructure`, add a `public static class <Name>ModuleExtensions` with an `Add<Name>Module(this IServiceCollection, IConfiguration)` method that wires the module's `DbContext` and services.
```

with:

```markdown
2. In `.Infrastructure/Extensions/`, add a `public static class <Name>ModuleExtensions` with an `Add<Name>Module(this IServiceCollection, IConfiguration)` method that wires the module's `DbContext` and services.
```

(Only `.Infrastructure` → `.Infrastructure/Extensions/` changes.)

- [ ] **Step 7: Build the solution (GREEN)**

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

- [ ] **Step 9: Grep guard — no consumer still uses the old plain-Infrastructure usings**

Run:
```bash
git grep -n -e "using Kheprx.BaseBackend.Catalog.Infrastructure;" -e "using Kheprx.BaseBackend.Identity.Infrastructure;" -- backend/src backend/Kheprx.BaseBackend.Api backend/tests
```

Expected: **no matches** (exit code 1). Deeper namespaces like `Infrastructure.Data` don't match these exact strings; a hit means a consumer was missed — stop and fix it before committing. (Scope excludes `backend/docs` — historical planning/spec markdown quotes the old using lines in code blocks; those are dated records, not consumers. Execution note 2026-07-06: the implementer ran `-- backend/src backend/tests`, omitting `backend/Kheprx.BaseBackend.Api`; the task reviewer re-ran the guard over all code paths and confirmed zero code hits — the 13 full-scope hits are all under `backend/docs/superpowers/`.)

- [ ] **Step 10: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Program.cs backend/tests/Kheprx.BaseBackend.ArchitectureTests/ModuleConventionTests.cs backend/README.md backend/src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Infrastructure/Extensions/CatalogModuleExtensions.cs backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs
git commit -m "refactor(modules): move module extension classes into Extensions folders

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

(The renames were staged by `git mv` in Step 1; the `git add` picks up the post-move namespace edits at the new paths plus the three consumer/doc edits.)

---

## Out of Scope (tracked in the spec)

- The Api project's own `Extensions/` folder — untouched.
- Prior run's spec/plan documents — dated records, left as-is.
- The 8 stale HTML teaching docs — separate deferred docs pass.
