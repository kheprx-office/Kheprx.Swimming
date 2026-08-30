# Rename to Kheprx.BaseBackend + Public README — Design

**Date:** 2026-06-28
**Status:** Approved design, ready for implementation plan

## Goal

Rebrand the whole project from `BaseBackend` to **`Kheprx.BaseBackend`** — namespaces,
assemblies, projects, folders, the solution, EF-migration namespaces, the module-scan
glob, the tests, and all 16 docs pages — and rewrite the README as a polished public
readme (with an MIT LICENSE), ready to push. The build and test suite must be green
afterward.

## The core rule (this is the whole rename)

**Replace the token `BaseBackend` with `Kheprx.BaseBackend` everywhere it appears** — in
file *contents* (`.cs`, `.csproj`, `.sln`, and `docs/*.html`) **and** in file/folder names.
(`README.md` is a full rewrite, not a token-replace; the historical `docs/superpowers/`
specs and plans are left untouched.)
Because the change is a pure prefix-prepend (`Kheprx.` in front of `BaseBackend`), this
one rule is complete and uniform; it correctly updates:

- C# `namespace` declarations and `using` directives;
- assembly/project names and `<ProjectReference>` paths;
- the `.sln` project entries and paths;
- the EF-migration namespaces in the generated migration files;
- **the module-discovery glob** `Directory.GetFiles(AppContext.BaseDirectory, "BaseBackend.*.dll")`
  → `"Kheprx.BaseBackend.*.dll"` in `ModuleRegistration.cs` (a functional hotspot — the
  renamed assemblies are `Kheprx.BaseBackend.*.dll`, so the glob still matches);
- the docs (code references, file paths, and the brand text);
- the README.

**Do not touch** identifiers that merely start with "Base": `BaseApiController`,
`BaseEntity`, `BaseApiResponse`, `BaseDirectory` — none contain the token `BaseBackend`,
so a literal `BaseBackend` replace leaves them alone.

**Do not rename the local working folder** `Base Backend` (note the space) — it does not
contain the token `BaseBackend`, and renaming it would only churn absolute paths. The
public repository name is chosen when pushing, independent of this folder.

## Renames (file/folder)

Project folders + their `.csproj` files (and the solution) get `Kheprx.` prepended:

| From | To |
|------|----|
| `BaseBackend.sln` | `Kheprx.BaseBackend.sln` |
| `BaseBackend.Api/` (+ `BaseBackend.Api.csproj`) | `Kheprx.BaseBackend.Api/` (+ `.csproj`) |
| `src/SharedKernel/BaseBackend.SharedKernel/` (+ csproj) | `…/Kheprx.BaseBackend.SharedKernel/` |
| `src/Modules/Catalog/BaseBackend.Catalog.Domain/` (+ csproj) | `…/Kheprx.BaseBackend.Catalog.Domain/` |
| `src/Modules/Catalog/BaseBackend.Catalog.Application/` (+ csproj) | `…/Kheprx.BaseBackend.Catalog.Application/` |
| `src/Modules/Catalog/BaseBackend.Catalog.Infrastructure/` (+ csproj) | `…/Kheprx.BaseBackend.Catalog.Infrastructure/` |
| `src/Modules/Catalog/BaseBackend.Catalog.Contracts/` (+ csproj) | `…/Kheprx.BaseBackend.Catalog.Contracts/` |
| `tests/BaseBackend.Catalog.UnitTests/` (+ csproj) | `tests/Kheprx.BaseBackend.Catalog.UnitTests/` |
| `tests/BaseBackend.Api.UnitTests/` (+ csproj) | `tests/Kheprx.BaseBackend.Api.UnitTests/` |
| `tests/BaseBackend.ArchitectureTests/` (+ csproj) | `tests/Kheprx.BaseBackend.ArchitectureTests/` |

The `Catalog/`, `Migrations/`, `src/`, `tests/` directory names contain no `BaseBackend`
token and stay. Migration files (`…_InitialCatalog.cs`, `.Designer.cs`,
`CatalogDbContextModelSnapshot.cs`) keep their names; only their namespace contents
change. Use `git mv` so history is preserved.

## Implementation order (for the plan)

1. Replace the token in `.cs`, `.csproj`, `.sln`, and `docs/*.html` contents (NOT the
   historical `docs/superpowers/*.md`; `README.md` is rewritten in a later step) so
   internal references point at the new names/paths.
2. `git mv` the folders, `.csproj` files, and the `.sln` to their new names so the paths
   those updated references now point to exist.
3. Delete every `bin/` and `obj/` directory (remove stale `BaseBackend.*.dll` and caches).
4. `dotnet build` then `dotnet test` — both must pass.

## Docs

Blanket: every `BaseBackend` in the 16 HTML pages becomes `Kheprx.BaseBackend` — brand
(nav logo, page titles, footer) and code references/paths alike. This keeps it a single
mechanical rule. After the rename, the docs' file-path references
(`src/Modules/Catalog/Kheprx.BaseBackend.Catalog.*`, `dotnet run --project Kheprx.BaseBackend.Api`)
match the renamed folders, and the docs' `"Kheprx.BaseBackend.*.dll"` scan-glob mention
matches the code.

## README (rewrite) + LICENSE

Replace `README.md` with a public-facing readme for **Kheprx.BaseBackend**, with these
sections (real content, grounded in the repo):

1. **Title + one-line description** — "A reusable .NET 10 modular-monolith backend base."
2. **Features** — modular monolith with auto-discovered modules (`IModule`), Clean-Architecture
   layering (Domain/Application/Infrastructure/Contracts) with enforced boundaries
   (architecture tests), FluentValidation, Mapperly mapping, EF Core + PostgreSQL,
   Serilog, Swagger, the `ApiResponse<T>` envelope, global exception handling.
3. **Tech stack** — .NET 10, EF Core / Npgsql, PostgreSQL, FluentValidation, Riok.Mapperly,
   Serilog, Swashbuckle (versions not hard-coded in prose; point at `Directory.Packages.props`/`docs`).
4. **Getting started** — prerequisites (.NET 10 SDK, PostgreSQL), set `ConnectionStrings:Postgres`
   in `Kheprx.BaseBackend.Api/appsettings.json`, `dotnet run --project Kheprx.BaseBackend.Api`,
   Swagger at `/swagger` (Development).
5. **Documentation** — link to the offline docs: open `docs/index.html` (Architecture, Module
   Anatomy + the per-layer deep dives, Request Flow, Validation, Runtime-vs-References).
6. **Project structure** — the `src/` (SharedKernel, Modules) + `tests/` + host layout.
7. **Add a module** — the four-project recipe + reference the new `.Infrastructure` from
   `Kheprx.BaseBackend.Api`; modules auto-discovered (no `Program.cs` edits).
8. **Migrations** — the `dotnet ef migrations add` / `database update` commands with the
   renamed project paths.
9. **Testing** — `dotnet test` (unit + architecture/boundary tests).
10. **Security notes** — the permissive `AllowAll` CORS; tighten before production.
11. **License** — MIT; add a top-level `LICENSE` file (standard MIT text, copyright holder
    "Kheprx" / CodeLab Systems, year 2026) and reference it.

## Constraints

- The rename must end with **`dotnet build` and `dotnet test` both green** — the
  `ArchitectureTests` / `ModuleConventionTests` are the safety net for the module-scan glob
  and the layer boundaries.
- **Zero residual standalone `BaseBackend`** — every occurrence must become
  `Kheprx.BaseBackend` (verify: no `BaseBackend` that is not immediately preceded by `Kheprx.`).
- Do not rename the `Base Backend` working folder; do not alter `BaseApiController` /
  `BaseEntity` / `BaseApiResponse` / `BaseDirectory`.
- Docs-only/spec/plan files under `docs/superpowers/` describe past work — they may keep
  historical `BaseBackend` references; do NOT rewrite the prior specs/plans (only the
  living docs in `docs/*.html` and `README.md` are rebranded). The residual check excludes
  `docs/superpowers/` and `.git/`.

## Files changed

- **Renamed:** 9 project folders, 9 `.csproj`, `BaseBackend.sln` → `Kheprx.BaseBackend.sln`.
- **Content-edited:** all `.cs` (incl. EF migrations + `ModuleRegistration`), all `.csproj`,
  the `.sln`, all 16 `docs/*.html`, `README.md`.
- **New:** top-level `LICENSE` (MIT).
- **Deleted:** all `bin/` and `obj/` directories (regenerated by build).

## Non-goals (YAGNI)

- No code/behavior changes beyond the rename (no refactors, no new features).
- No rename of the `Catalog` module or any type names — only the `BaseBackend` prefix.
- No rewrite of historical `docs/superpowers/` specs and plans.
- No remote/push actions in this effort (the user pushes when ready).

## Verification

- `git status` after `git mv` shows renames (history preserved), not delete+add.
- Residual: searching the repo (excluding `.git/` and `docs/superpowers/`) for `BaseBackend`
  not preceded by `Kheprx.` returns **0**.
- `dotnet build` succeeds; `dotnet test` is all-green (unit + architecture/boundary tests pass —
  proving module discovery still works under the new assembly names).
- Open `docs/index.html`: nav/brand reads `Kheprx.BaseBackend`, all path/code references and
  links are consistent.
- `README.md` renders as a complete public readme; `LICENSE` (MIT) present at the repo root.
