# Kheprx Swimming — Starter Scaffold Design

**Date:** 2026-08-30
**Status:** Approved (design), pending spec review
**Target directory:** `C:\Users\envnt\Desktop\Kheprx.Swmming`
**Model repo:** `C:\Users\envnt\Desktop\Kheprx.Electric`

## 1. Goal

Stand up a clean **starter scaffold** for a new Kheprx application ("Swimming") that
reproduces the `Kheprx.Electric` monorepo architecture and folder structure, but
contains **no business logic** (no swimming logic, no Electric/construction logic).

The result is a buildable skeleton — infrastructure + authentication only — from which
each future feature can be started independently. The retained `auth` feature (frontend)
and `Identity` module (backend) double as the copy-me template for new features.

## 2. Non-goals

- No swimming domain logic, entities, pages, or endpoints.
- No Electric/construction business modules or features.
- No new framework choices — the frontend stays **Angular 20** (the Magic-Patterns
  React/Vite export is a *design reference* only, not part of this scaffold).
- No rename of projects/namespaces — base names `Kheprx.BaseBackend` /
  `kheprx.basefrontend` are kept, exactly as Electric did.

## 3. Decisions (locked)

| Axis | Decision |
|------|----------|
| Source | Copy `Kheprx.Electric` source tree, then strip business logic |
| Frontend stack | Angular 20 base frontend; Magic-Patterns export = design reference only |
| Scope | Infrastructure + Auth/Identity skeleton; empty `features/` and `Modules/` |
| Naming | Keep `Kheprx.BaseBackend` / `kheprx.basefrontend` names & namespaces |
| Reference module | **Strip** (its lookups are Egypt/construction-specific) |
| Contracts client | **Regenerate** from the trimmed API (auth/identity endpoints only) |
| Git | Fresh `git init` in the target — no Electric history |

## 4. Method

Copy from `Kheprx.Electric` **source-only**, excluding build/IDE/vendor artifacts:
`bin`, `obj`, `node_modules`, `dist`, `.angular/cache`, `.vs`, `.idea`, `.git`,
`publish-electric`, `backend/References`, `frontend/References`.

Then apply the keep/strip/edit rules below and verify the result builds.

## 5. Backend (.NET modular monolith, Clean Architecture per module)

### Keep (skeleton)
- `backend/Kheprx.BaseBackend.Api` — the host (trimmed, see edits).
- `backend/src/SharedKernel/Kheprx.BaseBackend.SharedKernel`.
- `backend/src/Modules/Identity/` — `Domain`, `Application`, `Contracts`, `Infrastructure`.
  - Verified self-contained: Identity.Infrastructure references only Identity.{Domain,Application,Contracts} + SharedKernel.
- Tests: `Kheprx.BaseBackend.Api.UnitTests`, `Kheprx.BaseBackend.ArchitectureTests`,
  `Kheprx.BaseBackend.Identity.UnitTests`.
- `backend/docs`, `backend/.claude`, `backend/.superpowers` (structure only — see §7).

### Strip (business modules + their tests + their controllers)
Modules: `Projects`, `Attendance`, `Suppliers`, `Inventory`, `Finance`, `Tasks`,
`Catalog`, **`Reference`**. Plus `backend/publish-*`, `backend/References`, `.idea`, `bin`, `obj`.

### Edit seams (so the host still compiles)
These are the *only* couplings between the host and business modules (verified):

1. **`Kheprx.BaseBackend.Api/Program.cs`** — remove business `using`s, `Add*Module(...)`
   registrations, `Apply*MigrationsAsync(...)` calls, and the Dashboard aggregation block.
   Keep: `AddIdentityModule`, pipeline / security / swagger / serilog / validation setup.
2. **`Kheprx.BaseBackend.Api/Kheprx.BaseBackend.Api.csproj`** — remove business
   `ProjectReference`s; keep `Identity.Infrastructure` reference and all `PackageReference`s.
3. **`Kheprx.BaseBackend.Api/Extensions/Data/MigrationExtensions.cs`** — remove business
   `Apply*MigrationsAsync` methods and their `using`s; keep Identity migration application.
4. **`Kheprx.BaseBackend.Api/Controllers/`** — keep only `AuthController`,
   `BaseApiController`, `RolesController`, `UsersController`. Delete the business
   controllers (Attendance, Dashboard, Distributions, EngagementTypes, Finance,
   Governorates, InventoryProducts, InventoryStockMovements, Managers, Moqaweleen,
   ProductStatuses, ProjectAssignmentOptions, ProjectFinance, ProjectPaymentTypes,
   ProjectStolenItems, Projects, SupplierPayments, Suppliers, SupplyRecords, Tasks,
   Units, Workers).
5. **`backend/Kheprx.BaseBackend.sln`** — remove the stripped project and test entries
   (and their solution folders) so the solution loads clean.

## 6. Frontend (Angular 20)

### Keep (skeleton)
- `frontend/src/app/core/` — config, network, datasource, logging, ui, crypto,
  geolocation, text, util, domain. (Verified: `core/` does not import `features/`.)
- `frontend/src/app/layout/`, `frontend/src/app/testing/`.
- Features: `auth`, `user-management`, `home` (landing shell).
- All root config: `angular.json`, `package.json`, `tsconfig*.json`, `tailwind.config.js`,
  `postcss.config.js`, `jest.config.js`, `setup-jest.ts`, `public/`, `README.md`.

### Strip (business features)
`attendance`, `dashboard`, `distributions`, `finance`, `inventory`, `labour`,
`moqaweleen`, `projects`, `suppliers`, `tasks`, `worker-mobile`. Plus `node_modules`,
`dist`, `.angular/cache`, `.idea`, `frontend/References`.

### Edit seams
1. **`frontend/src/app/app.routes.ts`** — the single file importing every feature.
   Rewrite to keep only: `login`, `home`, `account`, `change-password`,
   `user-management`. Remove all business imports/routes and the entire `worker`
   route tree.
2. **Navigation menu** — locate the sidebar/nav config (appears data-driven; layout
   templates don't hardcode business routes) and prune entries pointing to removed routes.

## 7. Wrapper folders

- **`contracts/`** — keep the OpenAPI→client generation pipeline/config. Regenerate
  `contracts/generated/frontend-api-client` from the trimmed API so it exposes only
  auth/identity endpoints.
- **`infra/`** (nginx, deployment) — keep (generic `.gitkeep` placeholders).
- **`scripts/feature-doc`** — keep (feature-scaffolding helper).
- **`tools/`** — keep the folder and generic infra probe `AivenProbe`; strip
  business/data tooling (`ProvisionLabour`, `sql` seed dumps, `ProvisionAccounts`).
  Each item confirmed during implementation before deletion.
- **`docs/`** — keep the skeleton (`backend/`, `frontend/`, `decisions/`, `superpowers/`);
  strip Electric-specific decision records, specs, and `docs/references/electric-website-angular`.
- **`.superpowers/`** and **`.claude/`** — keep structure/config; strip Electric-specific
  SDD specs (`.superpowers/sdd/*`, `.superpowers/brainstorm/*`) and `.claude/worktrees` content.
- **`.gitignore`, `.gitattributes`** — keep. Drop root `publish-electric/`.

## 8. Deliverables

1. The stripped, buildable scaffold in `Kheprx.Swmming`.
2. **`docs/STARTER.md`** — "how to start each feature separately":
   - **Backend:** copy the `Identity` module (Domain/Application/Infrastructure/Contracts)
     as a template → register in `Program.cs` (`Add<Name>Module`, `Apply<Name>MigrationsAsync`),
     add `ProjectReference` in `Api.csproj`, add a controller, add projects to the `.sln`.
   - **Frontend:** copy the `auth` feature folder (presentation/domain/application/data)
     as a template → add a route in `app.routes.ts`, add a nav entry.
3. Fresh git repository initialized in the target.

## 9. Final target layout (top level)

```
Kheprx.Swmming/
├─ backend/          # .NET modular monolith: Api host + SharedKernel + Identity module + tests
├─ frontend/         # Angular 20: core/ + layout/ + features/{auth,user-management,home}
├─ contracts/        # OpenAPI → generated frontend API client (auth/identity only)
├─ infra/            # nginx, deployment placeholders
├─ scripts/          # feature-doc helper
├─ tools/            # AivenProbe (generic infra)
├─ docs/             # backend/ frontend/ decisions/ superpowers/ + STARTER.md
├─ .claude/  .superpowers/
├─ .gitignore  .gitattributes
```

## 10. Verification (evidence before "done")

- Backend: `dotnet build backend/Kheprx.BaseBackend.sln` → build succeeds, 0 errors.
- Frontend: `cd frontend && npm install && npm run build` → build succeeds.
- Solution loads with no dangling project references; `Program.cs` compiles with only
  the Identity module wired.

## 11. Assumptions

- The nav/menu is data-driven; if a hardcoded menu is found during implementation, it is
  pruned as part of §6 seam 2.
- `RolesController` / `UsersController` are backed by the Identity module and remain valid
  after business modules are removed.
- Regenerating the contracts client requires the trimmed API to build first; if generation
  tooling is unavailable in this environment, the generated client is emptied/stubbed and
  the step is documented in `STARTER.md` instead.
