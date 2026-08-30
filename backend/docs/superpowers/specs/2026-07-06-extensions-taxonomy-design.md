# Api Extensions Per-Concern Taxonomy — Design

**Date:** 2026-07-06
**Status:** Approved
**Scope:** Backend only (`Kheprx.BaseBackend.Api`). Zero runtime behavior change — file locations, namespaces, and using lines only.

## Goal

Categorize the six files in `Kheprx.BaseBackend.Api/Extensions/` into
per-concern subfolders with matching namespaces (repo rule:
`dotnet_style_namespace_match_folder = true`; precedent: the module
`Extensions/` folders created earlier today). User chose the **full
per-concern taxonomy** over a two-bucket Services/Pipeline split and a
three-bucket variant.

## File moves (6) — `git mv` + one-line namespace edit each

| From (Extensions/) | To (Extensions/) | New namespace |
|---|---|---|
| `AuthenticationExtensions.cs` | `Security/AuthenticationExtensions.cs` | `Kheprx.BaseBackend.Api.Extensions.Security` |
| `CorsExtensions.cs` | `Security/CorsExtensions.cs` | `Kheprx.BaseBackend.Api.Extensions.Security` |
| `FluentValidationExtensions.cs` | `Validation/FluentValidationExtensions.cs` | `Kheprx.BaseBackend.Api.Extensions.Validation` |
| `ServiceCollectionExtensions.cs` | `Mvc/ServiceCollectionExtensions.cs` | `Kheprx.BaseBackend.Api.Extensions.Mvc` |
| `MigrationExtensions.cs` | `Data/MigrationExtensions.cs` | `Kheprx.BaseBackend.Api.Extensions.Data` |
| `ApplicationBuilderExtensions.cs` | `Pipeline/ApplicationBuilderExtensions.cs` | `Kheprx.BaseBackend.Api.Extensions.Pipeline` |

Resulting tree:

```
Extensions/
├─ Data/
│  └─ MigrationExtensions.cs
├─ Mvc/
│  └─ ServiceCollectionExtensions.cs
├─ Pipeline/
│  └─ ApplicationBuilderExtensions.cs
├─ Security/
│  ├─ AuthenticationExtensions.cs
│  └─ CorsExtensions.cs
└─ Validation/
   └─ FluentValidationExtensions.cs
```

The `namespace` line is the **only** content change in five of the six
files; `ApplicationBuilderExtensions.cs` additionally gains one using (see
below). No csproj edits (SDK globbing).

Collision check (verified): the new `…Api.Extensions.Validation` is
distinct from the existing `Kheprx.BaseBackend.Api.Validation`
(`ModelStateResponse`); `…Api.Extensions.Mvc` cannot clash with
`Microsoft.AspNetCore.Mvc` (different namespace roots); `…Api.Extensions.Data`
is distinct from `…Identity.Infrastructure.Data`.

## Consumer updates (2 files)

Verified by grep: the only using of `Kheprx.BaseBackend.Api.Extensions` is
`Program.cs:1`, and the only cross-file reference within the folder is
`ApplicationBuilderExtensions` → `CorsExtensions.CorsPolicyName`.

1. **`Program.cs`** — line 1's single using becomes five; the complete
   using block becomes (alphabetical, module usings unchanged):

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

2. **`Extensions/Pipeline/ApplicationBuilderExtensions.cs`** — gains
   `using Kheprx.BaseBackend.Api.Extensions.Security;` (for
   `CorsExtensions.CorsPolicyName`), inserted alphabetically after
   `using Kheprx.BaseBackend.Api.Middlewares;`. Its using block becomes:

   ```csharp
   using Kheprx.BaseBackend.Api.Extensions.Security;
   using Kheprx.BaseBackend.Api.Middlewares;
   using Serilog;
   ```

   (Note: `Extensions.Security` sorts before `Middlewares` — the block
   above shows the correct alphabetical order.)

## Behavior (unchanged)

Same methods, same registrations, same pipeline order, same call sites.
Locations, namespaces, and using lines only. The editorconfig
namespace-match-folder rule holds for all six files.

## Testing / verification

- No test references any of the six extension classes (verified by grep —
  the flat namespace's only consumer is `Program.cs`).
- `dotnet build backend/Kheprx.BaseBackend.sln` — 0 errors, 0 new warnings.
- `dotnet test backend/Kheprx.BaseBackend.sln` — 147/147.
- Grep guard (code paths only): no remaining
  `using Kheprx.BaseBackend.Api.Extensions;` (exact string, old flat
  namespace) in `backend/src`, `backend/Kheprx.BaseBackend.Api`,
  `backend/tests`. Historical planning docs under `backend/docs` keep the
  old string by design.

## Follow-up (out of scope, already tracked)

- Stale HTML teaching docs (HOST_API.html shows the flat Extensions layout)
  — already on the deferred 8-doc pass; this taxonomy adds to it.
- Teaching comments — later pass if wanted.

## Decisions log

- **Full per-concern taxonomy** (Security/Validation/Mvc/Data/Pipeline)
  chosen over two-bucket Services+Pipeline and three-bucket
  Services+Pipeline+Data — user prefers visible categorization; one-file
  folders accepted.
- **Folder names:** `Security` (JWT + CORS), `Validation` (FluentValidation
  registration), `Mvc` (controllers/JSON/Swagger), `Data` (startup
  migration), `Pipeline` (middleware order) — per the approved preview.
- **Namespaces match folders** — repo editorconfig rule + today's module
  Extensions precedent.
