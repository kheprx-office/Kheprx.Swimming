# Rename to Kheprx.BaseBackend + Public README — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Uniformly rename the token `BaseBackend` → `Kheprx.BaseBackend` across the whole repo (code, projects, folders, solution, EF migrations, the module-scan glob, and all docs), rewrite the README as a public readme, and add an MIT LICENSE — ending on a green `dotnet build` + `dotnet test`.

**Architecture:** A scripted, mechanical token rename. Replace `BaseBackend`→`Kheprx.BaseBackend` in tracked code/doc file *contents*, `git mv` the project folders / `.csproj` / `.sln` to the new names, delete `bin`/`obj`, then build + test. Docs and README handled separately.

**Tech Stack:** Git Bash (`sed`, `git mv`, `git grep`), .NET 10 (`dotnet build`/`dotnet test`).

## Global Constraints

- **The rename is a pure prefix-prepend:** the literal token `BaseBackend` becomes `Kheprx.BaseBackend` — in file contents and in file/folder names. Run each sweep **once** (running twice would produce `Kheprx.Kheprx.BaseBackend`).
- **Do NOT alter** `BaseApiController`, `BaseEntity`, `BaseApiResponse`, `BaseDirectory` (none contain the token `BaseBackend`), the `Base Backend` working folder, or the historical `docs/superpowers/` specs/plans.
- **Must end green:** `dotnet build` and `dotnet test` both succeed. The `ArchitectureTests`/`ModuleConventionTests` validate module discovery + boundaries — they fail loudly if the scan glob or a boundary was missed.
- **Zero residual standalone `BaseBackend`** outside `docs/superpowers/` and `.git/` — every occurrence must be `Kheprx.BaseBackend`.
- Work from the repo root: `cd "/c/Users/envnt/Desktop/Base Backend"`. Use a long timeout (up to 600000 ms) for `dotnet` steps.

---

### Task 1: Rename the code, solution, and folders — build + test green

**Files:** all tracked `.cs`/`.csproj`/`.props`/`.sln` (contents); the 9 project folders + their `.csproj` + `BaseBackend.sln` (renames).

- [ ] **Step 1: Replace the token in code/solution file contents**

```bash
cd "/c/Users/envnt/Desktop/Base Backend"
git ls-files | grep -E '\.(cs|csproj|props|sln)$' | while IFS= read -r f; do
  sed -i 's/BaseBackend/Kheprx.BaseBackend/g' "$f"
done
echo "content swept"
```

- [ ] **Step 2: `git mv` the solution, project folders, and `.csproj` files**

```bash
cd "/c/Users/envnt/Desktop/Base Backend"
git mv BaseBackend.sln Kheprx.BaseBackend.sln

git mv BaseBackend.Api Kheprx.BaseBackend.Api
git mv "Kheprx.BaseBackend.Api/BaseBackend.Api.csproj" "Kheprx.BaseBackend.Api/Kheprx.BaseBackend.Api.csproj"

git mv src/SharedKernel/BaseBackend.SharedKernel src/SharedKernel/Kheprx.BaseBackend.SharedKernel
git mv "src/SharedKernel/Kheprx.BaseBackend.SharedKernel/BaseBackend.SharedKernel.csproj" "src/SharedKernel/Kheprx.BaseBackend.SharedKernel/Kheprx.BaseBackend.SharedKernel.csproj"

for p in Domain Application Infrastructure Contracts; do
  git mv "src/Modules/Catalog/BaseBackend.Catalog.$p" "src/Modules/Catalog/Kheprx.BaseBackend.Catalog.$p"
  git mv "src/Modules/Catalog/Kheprx.BaseBackend.Catalog.$p/BaseBackend.Catalog.$p.csproj" "src/Modules/Catalog/Kheprx.BaseBackend.Catalog.$p/Kheprx.BaseBackend.Catalog.$p.csproj"
done

for t in Catalog.UnitTests Api.UnitTests ArchitectureTests; do
  git mv "tests/BaseBackend.$t" "tests/Kheprx.BaseBackend.$t"
  git mv "tests/Kheprx.BaseBackend.$t/BaseBackend.$t.csproj" "tests/Kheprx.BaseBackend.$t/Kheprx.BaseBackend.$t.csproj"
done
echo "renames done"; git status --short | head -50
```

- [ ] **Step 3: Delete all `bin`/`obj` (remove stale `BaseBackend.*.dll` and caches)**

```bash
cd "/c/Users/envnt/Desktop/Base Backend"
find . -type d \( -name bin -o -name obj \) -not -path './.git/*' -exec rm -rf {} + 2>/dev/null || true
echo "bin/obj cleaned"
```

- [ ] **Step 4: Build (long timeout)**

```bash
cd "/c/Users/envnt/Desktop/Base Backend" && dotnet build Kheprx.BaseBackend.sln
```
Expected: `Build succeeded.` with 0 errors. If it fails, the error names the file/reference that still says `BaseBackend` or a path that didn't get renamed — fix that specific spot and rebuild.

- [ ] **Step 5: Test (long timeout)**

```bash
cd "/c/Users/envnt/Desktop/Base Backend" && dotnet test Kheprx.BaseBackend.sln
```
Expected: all test projects `Passed!` with `Failed: 0`. The `ArchitectureTests`/`ModuleConventionTests` passing proves module discovery still works under the `Kheprx.BaseBackend.*` assembly names (the scan glob was updated).

- [ ] **Step 6: Code residual check**

```bash
cd "/c/Users/envnt/Desktop/Base Backend"
git grep -n 'BaseBackend' -- '*.cs' '*.csproj' '*.props' '*.sln' | grep -v 'Kheprx\.BaseBackend' && echo "RESIDUAL FOUND ^^" || echo "CLEAN: code fully prefixed"
```
Expected: `CLEAN: code fully prefixed` (no un-prefixed `BaseBackend` remains).

- [ ] **Step 7: Commit**

```bash
cd "/c/Users/envnt/Desktop/Base Backend"
git add -A
git commit -m "refactor: rename BaseBackend -> Kheprx.BaseBackend across code, projects, and solution"
```

---

### Task 2: Rename the token in the docs

**Files:** all `docs/*.html` (16 files). Leaves `docs/superpowers/` untouched.

- [ ] **Step 1: Replace the token in the docs HTML**

```bash
cd "/c/Users/envnt/Desktop/Base Backend"
git ls-files 'docs/*.html' | while IFS= read -r f; do
  sed -i 's/BaseBackend/Kheprx.BaseBackend/g' "$f"
done
echo "docs swept"
```

- [ ] **Step 2: Docs residual check**

```bash
cd "/c/Users/envnt/Desktop/Base Backend"
git grep -n 'BaseBackend' -- 'docs/*.html' | grep -v 'Kheprx\.BaseBackend' && echo "RESIDUAL FOUND ^^" || echo "CLEAN: docs fully prefixed"
```
Expected: `CLEAN: docs fully prefixed`.

- [ ] **Step 3: Eyeball**

Run: `Start-Process "docs/index.html"` (PowerShell) or open `docs/index.html`.
Expected: the nav brand reads `Kheprx.BaseBackend`; titles, the reference graph, the per-layer pages' file paths (`src/Modules/Catalog/Kheprx.BaseBackend.Catalog.*`) and the `dotnet run --project Kheprx.BaseBackend.Api` command all read consistently; the module-scan mention reads `Kheprx.BaseBackend.*.dll`.

- [ ] **Step 4: Commit**

```bash
cd "/c/Users/envnt/Desktop/Base Backend"
git add docs/*.html
git commit -m "docs: rebrand the offline docs to Kheprx.BaseBackend"
```

---

### Task 3: Rewrite the README and add an MIT LICENSE

**Files:** Modify `README.md`; Create `LICENSE`.

- [ ] **Step 1: Replace `README.md` with this exact content**

````markdown
# Kheprx.BaseBackend

A reusable **.NET 10 modular-monolith** backend base — Clean-Architecture layering, auto-discovered modules, and batteries-included cross-cutting concerns. Use it as the starting point for a new service.

## Features

- **Modular monolith** — each feature is a self-contained module (`Domain` / `Application` / `Infrastructure` / `Contracts`). Modules are **auto-discovered** at startup via the `IModule` convention — no `Program.cs` edits to add one.
- **Clean-Architecture boundaries, enforced by tests** — dependencies point inward to the Domain core; `ArchitectureTests` fail the build if a layer reaches where it shouldn't.
- **Validation in depth** — FluentValidation on request DTOs + domain invariants in entities.
- **EF Core + PostgreSQL** (Npgsql), with per-module `DbContext` and migrations.
- **Mapperly** source-generated entity↔DTO mapping (no runtime reflection).
- **Consistent API envelope** — every endpoint returns `ApiResponse<T>` (`Success`/`Failure`).
- **Global exception handling** — domain errors become clean `400`s; everything else a safe `500`.
- **Serilog** structured logging and **Swagger** UI (Development).
- **Offline architecture docs** — a full set of standalone HTML guides under `docs/`.

## Tech stack

.NET 10 · ASP.NET Core · EF Core + Npgsql (PostgreSQL) · FluentValidation · Riok.Mapperly · Serilog · Swashbuckle (Swagger).

## Getting started

**Prerequisites:** .NET 10 SDK, PostgreSQL.

1. Set the connection string `ConnectionStrings:Postgres` in `Kheprx.BaseBackend.Api/appsettings.json`.
2. Apply migrations (see below) or let the app create the schema as configured.
3. Run the API:
   ```bash
   dotnet run --project Kheprx.BaseBackend.Api
   ```
4. Open Swagger (Development only): `https://localhost:<port>/swagger`.

## Documentation

Open **`docs/index.html`** in a browser — a guided, fully offline set of guides: Architecture Overview, Solution Structure, Project Libraries, Module Anatomy (with per-layer deep-dives: Domain, Application, Infrastructure, Contracts, SharedKernel, the IModule convention, Host/Api), Request Flow, Runtime vs Project References, and the Validation guides.

## Project structure

```
Kheprx.BaseBackend.Api/              # Host: controllers, pipeline, module wiring (composition root)
src/
  SharedKernel/
    Kheprx.BaseBackend.SharedKernel/ # ApiResponse<T>, BaseEntity, DomainException, IModule, ModuleRegistration
  Modules/
    Catalog/                         # Example module (four projects)
      Kheprx.BaseBackend.Catalog.Domain/
      Kheprx.BaseBackend.Catalog.Application/
      Kheprx.BaseBackend.Catalog.Infrastructure/
      Kheprx.BaseBackend.Catalog.Contracts/
tests/                               # Unit + Architecture/boundary tests
docs/                                # Offline HTML documentation
```

## Add a module

1. Create four projects under `src/Modules/<Name>/`: `.Domain`, `.Application`, `.Infrastructure`, `.Contracts`.
2. In `.Infrastructure`, add `public sealed class <Name>Module : IModule` whose `Register(...)` wires the module's `DbContext` and services.
3. Reference the new `.Infrastructure` project from `Kheprx.BaseBackend.Api`. No `Program.cs` edits — modules are auto-discovered.

## Migrations

```bash
dotnet ef migrations add <Name> \
  --project src/Modules/<Name>/Kheprx.BaseBackend.<Name>.Infrastructure \
  --startup-project Kheprx.BaseBackend.Api \
  --context <Name>DbContext --output-dir Migrations

dotnet ef database update \
  --project src/Modules/<Name>/Kheprx.BaseBackend.<Name>.Infrastructure \
  --startup-project Kheprx.BaseBackend.Api --context <Name>DbContext
```

## Testing

```bash
dotnet test Kheprx.BaseBackend.sln
```
Runs the unit tests and the architecture/boundary tests (which enforce the layering and module conventions).

## Security notes

The template ships a permissive `AllowAll` CORS policy (any origin, header, and method). **Tighten this before deploying to production** by restricting allowed origins in `Kheprx.BaseBackend.Api/Extensions/ServiceCollectionExtensions.cs`.

## License

[MIT](LICENSE) © 2026 Kheprx (CodeLab Systems).
````

- [ ] **Step 2: Create `LICENSE` (MIT) with this exact content**

```text
MIT License

Copyright (c) 2026 Kheprx (CodeLab Systems)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

- [ ] **Step 3: Verify**

```bash
cd "/c/Users/envnt/Desktop/Base Backend"
echo "title: $(head -1 README.md)"
echo "no stale BaseBackend in README: $(grep -c 'BaseBackend' README.md | xargs) total; un-prefixed:"; grep 'BaseBackend' README.md | grep -v 'Kheprx\.BaseBackend' || echo "  none"
echo "LICENSE present: $(test -f LICENSE && echo yes || echo no); MIT: $(grep -c 'MIT License' LICENSE)"
```
Expected: title `# Kheprx.BaseBackend`; no un-prefixed `BaseBackend`; `LICENSE present: yes`, `MIT: 1`.

- [ ] **Step 4: Commit**

```bash
cd "/c/Users/envnt/Desktop/Base Backend"
git add README.md LICENSE
git commit -m "docs: rewrite README as a public readme and add MIT LICENSE"
```

---

## Final verification (after all tasks)

- [ ] **Repo-wide residual check** (every tracked file except the historical specs/plans):
```bash
cd "/c/Users/envnt/Desktop/Base Backend"
git grep -n 'BaseBackend' -- . ':!docs/superpowers/' | grep -v 'Kheprx\.BaseBackend' && echo "RESIDUAL ^^ — token-replace those files too, then rebuild" || echo "CLEAN: repo fully rebranded to Kheprx.BaseBackend"
```
Expected: `CLEAN: repo fully rebranded to Kheprx.BaseBackend`. If anything is listed (e.g., a CI `.yml` or `.editorconfig`), run `sed -i 's/BaseBackend/Kheprx.BaseBackend/g'` on those specific files, re-run, and amend the relevant commit.
- [ ] `dotnet build Kheprx.BaseBackend.sln` and `dotnet test Kheprx.BaseBackend.sln` are both green.
- [ ] `git log --stat` shows the project folders/`.csproj`/`.sln` as **renames** (history preserved), and `LICENSE` added.
- [ ] `docs/index.html` opens with the `Kheprx.BaseBackend` brand and consistent paths/links; `README.md` renders as a complete public readme.
