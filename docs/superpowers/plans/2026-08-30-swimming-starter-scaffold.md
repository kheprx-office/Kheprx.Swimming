# Swimming Starter Scaffold Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a clean, buildable Kheprx application skeleton in `C:\Users\envnt\Desktop\Kheprx.Swmming` by copying the `Kheprx.Electric` architecture and stripping all business logic, leaving infrastructure + authentication only.

**Architecture:** A monorepo mirroring `Kheprx.Electric`: a .NET modular-monolith backend (Api host + SharedKernel + Identity module), an Angular 20 frontend (core + layout + auth/user-management/home features), plus wrapper folders (contracts, infra, scripts, tools, docs). The work is subtractive — copy the whole tree source-only, delete business modules/features, and trim the handful of wiring seams so it still compiles.

**Tech Stack:** .NET (modular monolith, EF Core, Clean Architecture per module), Angular 20 + Tailwind + Jest, OpenAPI-generated TypeScript API client, Windows + PowerShell/robocopy, git.

**Spec:** `docs/superpowers/specs/2026-08-30-swimming-starter-scaffold-design.md`

## Global Constraints

- **Target directory:** `C:\Users\envnt\Desktop\Kheprx.Swmming` (the working dir; already contains `docs/superpowers/specs/` and `docs/superpowers/plans/` — these must be preserved by every copy step).
- **Source repo:** `C:\Users\envnt\Desktop\Kheprx.Electric` (read-only; never modify it).
- **Names unchanged:** keep `Kheprx.BaseBackend` (backend namespaces/csproj/sln) and `kheprx.basefrontend` (frontend package). No rename.
- **Frontend stack is Angular 20.** The Magic-Patterns React/Vite export is a design reference only — it is NOT copied into this scaffold.
- **No business logic** of any kind (swimming, Electric/construction): no business modules, features, controllers, entities, pages, or seed data.
- **Base modules kept:** backend `Identity` + `SharedKernel` + `Api` host; frontend `core`, `layout`, `auth`, `user-management`, `home`.
- **Reference module: strip.** **Contracts client: regenerate** from the trimmed API (stub + document if generation tooling is unavailable).
- **Artifacts never copied:** `bin`, `obj`, `node_modules`, `dist`, `.angular`, `.vs`, `.idea`, `.git`, `publish-electric`, `backend/References`, `frontend/References`.
- **Git:** fresh local repo in the target; default branch `main`; remote `origin` = `https://github.com/kheprx-office/Kheprx.Swimming.git`; commit per task; **do not push until the user explicitly approves the first push.**
- **Definition of done for a build step:** command exits 0 with no compile errors; existing test suites that remain must pass.

---

### Task 1: Seed the scaffold (copy source-only + git init)

**Files:**
- Create: the full `Kheprx.Swmming` tree (copied from Electric, artifacts excluded)
- Preserve: `Kheprx.Swmming/docs/superpowers/specs/*` and `docs/superpowers/plans/*` (already present)

**Interfaces:**
- Produces: the on-disk tree that every later task edits — `backend/`, `frontend/`, `contracts/`, `infra/`, `scripts/`, `tools/`, `docs/`, `.claude/`, `.superpowers/`, `.gitignore`, `.gitattributes`.

- [ ] **Step 1: Preflight — verify toolchain is available**

Run (PowerShell):
```powershell
dotnet --version; node --version; npm --version; git --version; (Get-Command robocopy).Source
```
Expected: each prints a version / path with no error. If `dotnet` or `node` is missing, stop and report — later build gates cannot pass without them.

- [ ] **Step 2: Copy Electric into the target, source-only**

Run (PowerShell). robocopy exit codes 0–7 mean success; ≥8 is failure:
```powershell
$src = "C:\Users\envnt\Desktop\Kheprx.Electric"
$dst = "C:\Users\envnt\Desktop\Kheprx.Swmming"
robocopy $src $dst /E `
  /XD bin obj node_modules dist .angular .vs .idea `
      "$src\.git" "$src\publish-electric" "$src\backend\publish-electric" `
      "$src\backend\References" "$src\frontend\References" `
  /XF "*.user"
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with code $LASTEXITCODE" } else { "robocopy OK ($LASTEXITCODE)" }
```

- [ ] **Step 3: Verify the copy — structure present, artifacts absent, spec/plan preserved**

Run (PowerShell):
```powershell
$d = "C:\Users\envnt\Desktop\Kheprx.Swmming"
"backend","frontend","contracts","infra","scripts","tools","docs" | % { "{0,-10} {1}" -f $_, (Test-Path "$d\$_") }
"Api host   " + (Test-Path "$d\backend\Kheprx.BaseBackend.Api\Kheprx.BaseBackend.Api.csproj")
"Identity   " + (Test-Path "$d\backend\src\Modules\Identity")
"spec kept  " + (Test-Path "$d\docs\superpowers\specs\2026-08-30-swimming-starter-scaffold-design.md")
"no node_mod " + (-not (Test-Path "$d\frontend\node_modules"))
"no bin/obj  " + (-not (Test-Path "$d\backend\Kheprx.BaseBackend.Api\bin"))
"no .git copy " + (-not (Test-Path "$d\.git"))
```
Expected: every line prints `True` (except artifacts show `True` for "no ..." meaning absent).

- [ ] **Step 4: Initialize a fresh git repo, wire origin, make the baseline commit**

Run:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
git init -b main
git remote add origin https://github.com/kheprx-office/Kheprx.Swimming.git
git add -A
git commit -m "chore: seed scaffold from Kheprx.Electric (source-only, pre-strip)"
```
Expected: repo initialized on branch `main`; `origin` set to the Kheprx.Swimming remote; one commit created. (The copied `.gitignore` keeps artifacts out.) **Do not push** — the first push happens only after the user approves it (see Task 6).

---

### Task 2: Strip backend business modules and trim host wiring (backend builds green)

**Files:**
- Delete: `backend/src/Modules/{Projects,Attendance,Suppliers,Inventory,Finance,Tasks,Catalog,Reference}`
- Delete: `backend/tests/Kheprx.BaseBackend.{Projects,Attendance,Suppliers,Inventory,Finance,Tasks,Catalog,Reference}.UnitTests`
- Delete: business controllers under `backend/Kheprx.BaseBackend.Api/Controllers/`
- Modify: `backend/Kheprx.BaseBackend.Api/Program.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Kheprx.BaseBackend.Api.csproj`
- Modify: `backend/Kheprx.BaseBackend.Api/Extensions/Data/MigrationExtensions.cs`
- Modify: `backend/Kheprx.BaseBackend.sln`
- Modify (if they reference removed modules): `backend/tests/Kheprx.BaseBackend.ArchitectureTests/*`, `backend/tests/Kheprx.BaseBackend.Api.UnitTests/*`

**Interfaces:**
- Consumes: the tree from Task 1.
- Produces: a backend where only `Identity` + `SharedKernel` + `Api` remain and `dotnet build` is green — Task 3 regenerates the API client from this build's Swagger.

- [ ] **Step 1: Delete business module and test directories**

Run:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming/backend"
for m in Projects Attendance Suppliers Inventory Finance Tasks Catalog Reference; do
  rm -rf "src/Modules/$m" "tests/Kheprx.BaseBackend.$m.UnitTests"
done
```
Expected: only `src/Modules/Identity` remains under `src/Modules`.

- [ ] **Step 2: Delete business controllers, keep the four base controllers**

Keep exactly: `AuthController.cs`, `BaseApiController.cs`, `RolesController.cs`, `UsersController.cs`.
Run:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming/backend/Kheprx.BaseBackend.Api/Controllers"
for c in Attendance Dashboard Distributions EngagementTypes Finance Governorates \
         InventoryProducts InventoryStockMovements Managers Moqaweleen ProductStatuses \
         ProjectAssignmentOptions ProjectFinance ProjectPaymentTypes ProjectStolenItems \
         Projects SupplierPayments Suppliers SupplyRecords Tasks Units Workers; do
  rm -f "${c}Controller.cs"
done
ls
```
Expected: `ls` shows only `AuthController.cs  BaseApiController.cs  RolesController.cs  UsersController.cs`.

- [ ] **Step 3: Remove business `ProjectReference`s from the Api csproj**

In `backend/Kheprx.BaseBackend.Api/Kheprx.BaseBackend.Api.csproj`, delete the `<ProjectReference>` lines for `Projects`, `Reference`, `Attendance`, `Suppliers`, `Inventory`, `Finance`, `Tasks` (and `Catalog` if present). Keep the `Identity.Infrastructure` reference and all `<PackageReference>`s. The remaining ProjectReference block should read exactly:
```xml
    <ProjectReference Include="..\src\Modules\Identity\Kheprx.BaseBackend.Identity.Infrastructure\Kheprx.BaseBackend.Identity.Infrastructure.csproj" />
```

- [ ] **Step 4: Trim `Program.cs`**

In `backend/Kheprx.BaseBackend.Api/Program.cs`, remove:
- `using` lines for the business module Infrastructure/Extensions namespaces (Attendance, Projects, Reference, Suppliers, Inventory, Finance, Tasks, Catalog).
- The `builder.Services.Add<Module>Module(builder.Configuration);` calls for every business module — keep only `AddIdentityModule`.
- Every `await app.Apply<Module>MigrationsAsync();` call for business modules — keep only the Identity migration call.
- The "Dashboard aggregation" block (the read-only composition that owns no data).

Leave all non-module infrastructure (auth/JWT, Serilog, Swagger, CORS, validation, pipeline) untouched.

- [ ] **Step 5: Trim `MigrationExtensions.cs`**

In `backend/Kheprx.BaseBackend.Api/Extensions/Data/MigrationExtensions.cs`, delete the `Apply<Module>MigrationsAsync` methods and their `using ...Infrastructure.Data;` lines for every business module. Keep only the Identity migration method (and any that operate on the Identity `DbContext`).

- [ ] **Step 6: Remove stripped projects from the solution**

Run (removes each stripped project from the .sln by path):
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming/backend"
for p in $(dotnet sln Kheprx.BaseBackend.sln list | grep -Ei 'Projects|Attendance|Suppliers|Inventory|Finance|Tasks|Catalog|Reference'); do
  dotnet sln Kheprx.BaseBackend.sln remove "$p"
done
dotnet sln Kheprx.BaseBackend.sln list
```
Expected: the list shows only `SharedKernel`, the four `Identity.*` projects, `Api`, and the kept test projects (`Api.UnitTests`, `ArchitectureTests`, `Identity.UnitTests`). If any empty solution folders remain, they are harmless.

- [ ] **Step 7: Build the backend; trim kept tests that reference removed modules**

Run:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming/backend"
dotnet build Kheprx.BaseBackend.sln
```
Expected: `Build succeeded. 0 Error(s)`.
If `ArchitectureTests` or `Api.UnitTests` fail to compile because they reference removed modules/controllers:
- Open the failing test project's `.csproj`; remove `<ProjectReference>`s to deleted business modules.
- In the test sources, delete test cases/fixtures that name a removed module or controller (e.g. an architecture rule enumerating `Finance`/`Projects`, or a controller test for `SuppliersController`).
- Re-run `dotnet build` until green.

- [ ] **Step 8: Run the kept test suites**

Run:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming/backend"
dotnet test Kheprx.BaseBackend.sln
```
Expected: all remaining tests pass (Identity, base Api, architecture rules over the trimmed graph).

- [ ] **Step 9: Smoke-check the host boots**

Run (starts, waits, stops):
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming/backend/Kheprx.BaseBackend.Api"
dotnet run --no-build 2>&1 | head -40
```
Expected: the app starts and logs "Now listening on ..." with no missing-service / DI exceptions. Stop it (Ctrl-C) after confirming startup. (A DB connection warning is acceptable; a DI/registration exception is not — fix the offending wiring.)

- [ ] **Step 10: Commit**

```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
git add -A
git commit -m "refactor(backend): strip business modules to Identity-only skeleton"
```

---

### Task 3: Regenerate the contracts API client from the trimmed backend

**Files:**
- Modify/Create: `contracts/generated/frontend-api-client/*` (regenerated output)
- Read: `contracts/` (generation config/script), `frontend/package.json` (generation npm script, if any)

**Interfaces:**
- Consumes: the trimmed API from Task 2 (its Swagger/OpenAPI document).
- Produces: a TypeScript client exposing only auth/identity operations, consumed by the frontend `auth`/`user-management` features in Task 4.

- [ ] **Step 1: Locate the generation mechanism**

Run:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
ls contracts; cat contracts/README* 2>/dev/null
grep -riE "openapi|nswag|swagger|generate|orval|openapi-generator" contracts frontend/package.json | head -30
```
Determine how the client is produced (an npm script like `generate:api`, an NSwag config, an `openapi-generator` invocation, or a committed spec + generator).

- [ ] **Step 2: Regenerate against the trimmed API**

If a generation script exists, run it per its documented flow (typically: start the API so `/swagger/v1/swagger.json` is served, then run the generator). Example shape (adapt to what Step 1 found):
```bash
# terminal A
cd "C:/Users/envnt/Desktop/Kheprx.Swmming/backend/Kheprx.BaseBackend.Api" && dotnet run
# terminal B
cd "C:/Users/envnt/Desktop/Kheprx.Swmming/frontend" && npm run generate:api   # or the discovered command
```
Expected: files under `contracts/generated/frontend-api-client/` regenerate; a `git diff` shows business operations/models removed, auth/identity retained.

- [ ] **Step 3: Fallback if generation tooling is unavailable**

If no generator can run in this environment: manually remove business operation/model files from `contracts/generated/frontend-api-client/` that reference removed endpoints, keeping auth/identity ones, and add a note to `docs/STARTER.md` (created in Task 6) describing the exact regeneration command for later. Do not leave broken imports — the kept frontend must still compile in Task 4.

- [ ] **Step 4: Commit**

```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
git add -A
git commit -m "chore(contracts): regenerate API client for auth/identity-only surface"
```

---

### Task 4: Strip frontend business features and rewrite routing (frontend builds green)

**Files:**
- Delete: `frontend/src/app/features/{attendance,dashboard,distributions,finance,inventory,labour,moqaweleen,projects,suppliers,tasks,worker-mobile}`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: the navigation/menu config (located in Step 3)
- Keep: `frontend/src/app/features/{auth,user-management,home}`, `core/`, `layout/`, `testing/`

**Interfaces:**
- Consumes: the regenerated client from Task 3; the retained `auth` feature is the copy-me template referenced by `docs/STARTER.md`.
- Produces: an Angular app that builds with only auth/identity/home routes.

- [ ] **Step 1: Delete business feature directories**

Run:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming/frontend/src/app/features"
for f in attendance dashboard distributions finance inventory labour moqaweleen projects suppliers tasks worker-mobile; do
  rm -rf "$f"
done
ls
```
Expected: `ls` shows only `auth  home  user-management`.

- [ ] **Step 2: Rewrite `app.routes.ts` to the skeleton route set**

Replace `frontend/src/app/app.routes.ts` so it imports only from `@features/auth`, `@features/user-management`, `@features/home`, the `authGuard`/`roleGuard`, and `LayoutComponent`. Keep exactly these routes and drop everything else (all business routes and the entire `worker` route tree):
```typescript
import { Routes } from '@angular/router';
import { authGuard, roleGuard } from '@features/auth/presentation/auth.guard';
import { LayoutComponent } from './layout/layout.component';
import { LoginViewModel, ChangePasswordViewModel, AccountViewModel } from '@features/auth';
import { UsersViewModel } from '@features/user-management';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('@features/auth').then((m) => m.LoginPage),
    providers: [LoginViewModel],
  },
  {
    path: '',
    component: LayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: 'home', loadComponent: () => import('@features/home').then((m) => m.HomePage) },
      {
        path: 'account',
        loadComponent: () => import('@features/auth').then((m) => m.AccountPage),
        providers: [AccountViewModel],
      },
      {
        path: 'change-password',
        loadComponent: () => import('@features/auth').then((m) => m.ChangePasswordPage),
        providers: [ChangePasswordViewModel],
      },
      {
        path: 'user-management',
        canActivate: [roleGuard('admin')],
        loadComponent: () => import('@features/user-management').then((m) => m.UserManagementPage),
        providers: [UsersViewModel],
      },
      { path: '', pathMatch: 'full', redirectTo: 'home' },
    ],
  },
  { path: '**', redirectTo: '' },
];
```
Note: keep the exported symbol names (`LoginPage`, `AccountPage`, `ChangePasswordPage`, `UserManagementPage`, `HomePage`, `*ViewModel`) exactly as the retained features export them — if the real exports differ, match the feature's `index.ts`, do not invent names.

- [ ] **Step 3: Prune the navigation menu**

Find where nav items are declared (data-driven; not hardcoded in layout templates):
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming/frontend"
grep -rniE "moqaweleen|labour|attendance|suppliers|inventory|projects|tasks|distributions|finance|dashboard|worker" src/app/layout src/app/core | grep -iE "route|path|nav|menu|link|label" | head -40
```
Remove menu/nav entries pointing to any removed route. Keep entries for `home`, `account`, `change-password`, `user-management` (and any auth/logout controls).

- [ ] **Step 4: Install dependencies and build**

Run:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming/frontend"
npm install
npm run build
```
Expected: build succeeds with no unresolved `@features/*` imports. If the compiler reports a dangling import (a kept file importing a removed feature), remove that import/usage and rebuild until green.

- [ ] **Step 5: Run frontend tests (co-located specs)**

Run:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming/frontend"
npm test -- --watch=false 2>&1 | tail -30
```
Expected: specs pass. Business specs were removed with their folders; if a kept spec references a removed feature, delete that reference and re-run.

- [ ] **Step 6: Commit**

```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
git add -A
git commit -m "refactor(frontend): strip business features to auth/home skeleton"
```

---

### Task 5: Clean wrapper folders (docs, tools, infra, scripts, .superpowers, .claude)

**Files:**
- Modify/Delete under: `docs/`, `tools/`, `.superpowers/`, `.claude/`
- Keep: `infra/`, `scripts/feature-doc`, `docs/` skeleton structure, `.gitignore`, `.gitattributes`

**Interfaces:**
- Consumes: the tree; independent of build output.
- Produces: a wrapper free of Electric-specific residue, structure intact for future features.

- [ ] **Step 1: Strip Electric-specific SDD/brainstorm artifacts (keep folder structure)**

Run:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
rm -rf .superpowers/sdd/* .superpowers/brainstorm/* .claude/worktrees/*
rm -rf backend/.superpowers/sdd/* backend/.superpowers/brainstorm/* 2>/dev/null
rm -rf frontend/.superpowers/sdd/* 2>/dev/null
```
Expected: `.superpowers/` directories still exist but hold no Electric feature specs.

- [ ] **Step 2: Strip Electric-specific docs, keep the skeleton**

Remove Electric-specific decision records, specs, and references while keeping the `docs/{backend,frontend,decisions,superpowers}` structure and our own spec/plan:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming/docs"
rm -rf references/electric-website-angular
# remove Electric-specific spec/plan/decision files, but KEEP our 2026-08-30-swimming-* files
find superpowers/specs superpowers/plans -type f ! -name '2026-08-30-swimming-*' -delete 2>/dev/null
ls -R . | head -40
```
Review remaining decision records under `docs/decisions`, `docs/backend/decisions`, `docs/frontend/decisions`; delete any that describe Electric/construction business decisions, keep generic/architectural ones.

- [ ] **Step 3: Trim `tools/` to generic infrastructure**

Keep `AivenProbe` (generic DB connectivity probe) and the `tools/` folder. Remove business/data tooling:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming/tools"
ls
rm -rf ProvisionLabour ProvisionAccounts sql
ls
```
Before deleting each, open it briefly to confirm it is business/seed-specific (ProvisionLabour = labour seeding, `sql` = seed dumps). If `ProvisionAccounts` turns out to be a generic "create first admin" utility, keep it and note it in `STARTER.md`.

- [ ] **Step 4: Confirm infra and scripts are generic (keep as-is)**

Run:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
find infra scripts -type f | head; echo "---"; ls scripts/feature-doc
```
Expected: `infra/` holds nginx/deployment placeholders; `scripts/feature-doc` present. No changes needed unless a file embeds Electric-specific values — if so, neutralize them.

- [ ] **Step 5: Commit**

```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
git add -A
git commit -m "chore: remove Electric-specific wrapper artifacts, keep skeleton"
```

---

### Task 6: Author STARTER.md and run final end-to-end verification

**Files:**
- Create: `docs/STARTER.md`
- Verify: full backend + frontend build from a clean state

**Interfaces:**
- Consumes: the finished skeleton.
- Produces: the "how to start each feature separately" guide + a verified-green scaffold.

- [ ] **Step 1: Write `docs/STARTER.md`**

Create `docs/STARTER.md` documenting how to add a new feature, using the retained `Identity` module and `auth` feature as templates:
```markdown
# Starter Guide — Adding a Feature

This scaffold ships infrastructure + authentication only. Each feature is added
independently. Use the retained `Identity` module (backend) and `auth` feature
(frontend) as copy-me templates.

## Backend — new module `<Name>`
1. Copy `backend/src/Modules/Identity` to `backend/src/Modules/<Name>`, renaming
   projects/namespaces `Identity` → `<Name>` (Domain / Application / Infrastructure / Contracts).
2. Add the four projects to `backend/Kheprx.BaseBackend.sln` (`dotnet sln add`).
3. In `Kheprx.BaseBackend.Api/Kheprx.BaseBackend.Api.csproj`, add a `<ProjectReference>`
   to `<Name>.Infrastructure`.
4. In `Program.cs`, call `builder.Services.Add<Name>Module(builder.Configuration);`
   and `await app.Apply<Name>MigrationsAsync();`.
5. In `Extensions/Data/MigrationExtensions.cs`, add `Apply<Name>MigrationsAsync`.
6. Add a `<Name>Controller` under `Kheprx.BaseBackend.Api/Controllers/`.
7. `dotnet build` and `dotnet test`.

## Frontend — new feature `<name>`
1. Copy `frontend/src/app/features/auth` to `frontend/src/app/features/<name>`
   (presentation / domain / application / data), updating its `index.ts` exports.
2. Add a route in `frontend/src/app/app.routes.ts`.
3. Add a nav entry in the menu config (see `layout/`).
4. `npm run build`.

## Contracts
Regenerate the API client after backend endpoint changes: `<command from Task 3>`.

## Verify
- Backend: `dotnet build backend/Kheprx.BaseBackend.sln` && `dotnet test`
- Frontend: `cd frontend && npm run build`
```
Replace `<command from Task 3>` with the actual generation command discovered in Task 3.

- [ ] **Step 2: Clean full-build verification (backend)**

Run:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming/backend"
dotnet build Kheprx.BaseBackend.sln -c Release
```
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 3: Clean full-build verification (frontend)**

Run:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming/frontend"
npm run build
```
Expected: build succeeds.

- [ ] **Step 4: Final structural sanity check**

Run:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
echo "modules:"; ls backend/src/Modules
echo "features:"; ls frontend/src/app/features
echo "controllers:"; ls backend/Kheprx.BaseBackend.Api/Controllers
```
Expected: modules = `Identity`; features = `auth home user-management`; controllers = the four base controllers only.

- [ ] **Step 5: Commit**

```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
git add -A
git commit -m "docs: add STARTER guide; verify skeleton builds green"
```

- [ ] **Step 6: Publish to the remote (only after explicit user approval)**

Do NOT run this until the user says to push. Then:
```bash
cd "C:/Users/envnt/Desktop/Kheprx.Swmming"
git push -u origin main
```
Expected: the scaffold is published to `https://github.com/kheprx-office/Kheprx.Swimming.git`. If the remote already has commits, stop and confirm the reconciliation strategy with the user before force-anything.

---

## Self-Review

**Spec coverage** (spec → task):
- §4 copy method / artifact exclusion → Task 1.
- §5 backend keep/strip/edit seams (Program.cs, csproj, MigrationExtensions, controllers, sln) → Task 2.
- §6 frontend keep/strip + app.routes rewrite + nav prune → Task 4.
- §7 wrapper (contracts) → Task 3; (docs/tools/infra/scripts/.superpowers/.claude) → Task 5.
- §8 deliverables: STARTER.md → Task 6 Step 1; buildable scaffold → Tasks 2/4/6; git init → Task 1.
- §10 verification (dotnet build, npm build, boot) → Tasks 2/4/6.
- Decisions: strip Reference → Task 2 Step 1; regenerate contracts → Task 3; keep base names → enforced in Global Constraints and every rename-free step.
All spec sections map to a task. No gaps.

**Placeholder scan:** No "TBD/TODO/handle edge cases". The two conditional steps (Task 2 Step 7 test-trim, Task 3 Step 3 fallback) carry explicit criteria and commands, not vague instructions. `<Name>`/`<name>`/`<command from Task 3>` in STARTER.md are intentional template tokens for the guide's reader, and Task 3's command is resolved before it is written in.

**Type/name consistency:** Backend seam names are consistent (`Add<Module>Module`, `Apply<Module>MigrationsAsync`, `Kheprx.BaseBackend.*`). Frontend route symbols (`LoginPage`, `AccountPage`, `ChangePasswordPage`, `UserManagementPage`, `HomePage`, `LoginViewModel`, `AccountViewModel`, `ChangePasswordViewModel`, `UsersViewModel`) match the retained features' exports, with an explicit instruction to match each feature's `index.ts` if reality differs. The four kept controllers are named identically wherever referenced.
