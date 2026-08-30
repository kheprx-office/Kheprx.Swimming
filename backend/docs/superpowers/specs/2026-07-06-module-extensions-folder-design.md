# Module Extensions Folder Move — Design

**Date:** 2026-07-06
**Status:** Approved
**Scope:** Backend only. Zero behavior change — file location + namespace only.

## Goal

Move both module extension classes (created in
`2026-07-06-explicit-module-registration-design.md`) from their
Infrastructure project roots into `Extensions/` folders, with namespaces
updated to match — the same folder-and-namespace pattern the Api project
already uses (`Kheprx.BaseBackend.Api/Extensions/` →
`Kheprx.BaseBackend.Api.Extensions`), and consistent with the repo's
`.editorconfig` (`dotnet_style_namespace_match_folder = true`, line 13).

User decisions: applies to **both** modules (structural symmetry), and the
namespace **changes** with the folder. The Microsoft-DI-namespace
alternative (`namespace Microsoft.Extensions.DependencyInjection`, callers
need no using) was rejected as less discoverable; keeping the old namespace
under the new folder was rejected for violating the editorconfig rule.
Note: the IslamicApplication reference project keeps these classes at
project root — this move deliberately goes beyond the reference to match
this repo's own folder conventions.

## File moves (2) — `git mv` + one-line namespace edit each

1. `src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Infrastructure/CatalogModuleExtensions.cs`
   → `src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Infrastructure/Extensions/CatalogModuleExtensions.cs`

   ```csharp
   namespace Kheprx.BaseBackend.Catalog.Infrastructure.Extensions;
   ```

2. `src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/IdentityModuleExtensions.cs`
   → `src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs`

   ```csharp
   namespace Kheprx.BaseBackend.Identity.Infrastructure.Extensions;
   ```

The namespace declaration is the **only** content change in each file. All
existing usings inside the files remain valid and untouched (they reference
sibling namespaces explicitly). No csproj edits — SDK-style projects glob
all `.cs` files.

## Consumer updates (2 files, 2 using lines each)

1. **`Kheprx.BaseBackend.Api/Program.cs`** — the two Infrastructure usings
   gain the `.Extensions` suffix; alphabetical positions are unchanged:

   ```csharp
   using Kheprx.BaseBackend.Api.Extensions;
   using Kheprx.BaseBackend.Catalog.Infrastructure.Extensions;
   using Kheprx.BaseBackend.Identity.Infrastructure.Extensions;
   using Microsoft.EntityFrameworkCore;
   using Microsoft.Extensions.DependencyInjection;
   using Serilog;
   ```

   The fully-qualified
   `Kheprx.BaseBackend.Identity.Infrastructure.Data.IdentityDbContext` in
   the migration scope is unaffected. Nothing else in the file changes.

2. **`tests/Kheprx.BaseBackend.ArchitectureTests/ModuleConventionTests.cs`** —
   same two suffix swaps; alphabetical positions unchanged:

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

## README touch-up (1 line)

`backend/README.md` line 57, add-a-module step 2 — replace:

> 2. In `.Infrastructure`, add a `public static class <Name>ModuleExtensions` with an `Add<Name>Module(this IServiceCollection, IConfiguration)` method that wires the module's `DbContext` and services.

with:

> 2. In `.Infrastructure/Extensions/`, add a `public static class <Name>ModuleExtensions` with an `Add<Name>Module(this IServiceCollection, IConfiguration)` method that wires the module's `DbContext` and services.

## Behavior (unchanged)

Namespace and location only. Same registrations, same lifetimes, same
Program.cs execution order — the assembly-loading guarantee for the
FluentValidation scan (extension bodies reference Application types) is
unaffected by the namespace rename.

## Testing / verification

- `dotnet build backend/Kheprx.BaseBackend.sln` — 0 errors, 0 new warnings.
- `dotnet test backend/Kheprx.BaseBackend.sln` — 147/147 (baseline
  unchanged).
- `git grep` guard after the change: no remaining references to the old
  namespaces `Kheprx.BaseBackend.Catalog.Infrastructure`/`...Identity.Infrastructure`
  **as the extension-class namespace** — concretely, the two consumer files
  reference only the `.Extensions` variants. (The base namespaces still
  exist legitimately for Data/Repositories/Services types.)

## Non-goals

- The Api project's own `Extensions/` folder: untouched.
- Prior run's spec/plan documents: dated records, left as-is.
- The 8 stale HTML teaching docs: still deferred to the separate docs pass.

## Decisions log

- **Both modules**, not Identity only — user's choice for structural symmetry.
- **`Extensions/` + matching namespace** over (a) folder with unchanged
  namespace and (b) Microsoft-DI namespace — matches Api-project precedent
  and the editorconfig namespace-match-folder rule. The rule is IDE-level
  only (`enforce_code_style_in_build` unset), so this is convention, not a
  build requirement.
- **README updated in-scope** (1 line) so the add-a-module instructions
  stay accurate — continuing the precedent set by the previous run's
  final-review fix.
