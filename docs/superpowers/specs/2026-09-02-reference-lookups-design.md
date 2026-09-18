# Reference Lookups Foundation — Design

**Date:** 2026-09-02
**Status:** Draft for review
**Scope:** Backend reference lookups (`club`, `blood_type`, `stroke`) + seed data + read endpoints, and an Angular `reference` slice that loads them (plus `genders`) by ID. Foundation for the Account Creation forms.

> **Phase 1 of 3.** This spec covers only the shared lookups both registration forms depend on. **Phase 2 — Register Swimmer** and **Phase 3 — Register Captain / Head Coach** are separate specs that build on this one. See "Context: the bigger effort" below.

## Context: the bigger effort

The Captain Panel → Account Creation feature (Register Swimmer / Register Captain / Register Head Coach) is being built against the production **Angular** app (`frontend/`). The approved visual design lives in a separate React/Vite prototype (`Desktop/4dba8937-…/src/pages/CaptainPanel.tsx`) and is used **only as a visual reference** — the Angular app currently has no such forms (the `captain-panel` feature is a placeholder page).

A gap analysis (2026-09-02) found the real backend cannot persist most registration fields yet: no `club` / `blood_type` / `stroke` lookups, `swimmer_profile` has no `app_user` link or club/blood-type FKs, and no create endpoint writes a role profile. The work was decomposed into three phases, each with its own spec → plan → implementation cycle:

1. **Reference lookups foundation** — *this spec*.
2. **Register Swimmer** — expand `swimmer_profile` (app_user link, club/blood-type FKs), add `swimmer` role + `swimmer_specialization` junction, `POST /api/swimmers`, Angular swimmer form.
3. **Register Captain / Head Coach** — role-aware create endpoints writing `captain_profile` / `head_coach_profile`, Angular captain form.

Cross-phase decisions already agreed (recorded here so later specs inherit them):
- Swimmers **log in** (app_user, `role = swimmer`, generic password, forced first-login change). The 24 demo `swimmer_profile` rows will be **re-seeded** as full app_user-linked records in Phase 2.
- Authorization: **Head Coach** creates swimmers, captains, and head coaches; **Captain** creates swimmers only.
- "Full Name" splits into **Name (English, required)** + **Name (Arabic, optional)**; no auto-copy.
- All lookups are submitted as **IDs**; display labels are UI-only. **IM = `medley`.**
- `swimmer_profile.uid` is **backend-generated** (`SW-####`), never user-entered.
- The generic password comes from **backend config**, and the forced first-login flow is unchanged.
- Role codes keep the backend's existing convention (`head_coach`, `captain`, add `swimmer`) — not the diagram's `headCoach`.
- **"Assigned Clubs"** (captain) is **dropped** — no schema for it, and it is not needed.

## Goal

Create the three missing shared reference tables, seed their fixed data, and expose read endpoints so the Account Creation forms can populate every dropdown/chip by ID:

- `reference.stroke` — 5 strokes (swimmer specialization chips; `IM → medley`).
- `reference.blood_type` — 8 blood types (swimmer blood type).
- `reference.club` — ~70 fixed Egyptian clubs (training club, championship club).

Plus a read endpoint for the already-seeded `reference.gender` (the form needs gender **IDs**, but no genders endpoint exists today). `reference.role` already has `GET /api/roles`.

## Decisions (agreed)

1. **Reuse the existing pattern, no new module.** `Gender` and `Role` already live in the **Identity** module mapped to the `reference` schema (`ToTable("gender", "reference")`). The new lookups follow the same placement — new entities in `Identity.Domain/Entities`, configs in `Identity.Infrastructure/Configurations`, added to `IdentityDbContext`. This reuses the DbContext, migration history, DI wiring, and `BaseApiController` patterns.
2. **Read-only in this phase.** Lookups are seeded and served; no create/update/delete UI or endpoints for reference data.
3. **`distance` is out of scope.** Registration does not use it (only championships/races do). Deferred to the championships work — YAGNI here.
4. **Endpoints require authentication.** The new reference endpoints (`clubs`, `blood-types`, `strokes`, `genders`) are `[Authorize]` (any authenticated user): they are consumed inside the logged-in Captain Panel, not before sign-in. (`GET /api/roles` stays `[AllowAnonymous]` — the login page reads it.)
5. **Labels are API-sourced.** Dropdown/chip text uses each row's `name_en` / `name_ar` selected by the current language — a single source of truth — rather than duplicating names in the Angular i18n dictionaries.

## Non-goals

- No `distance` table.
- No create/update endpoints for reference data (seed-only).
- No form UI (Phases 2 and 3).
- No `swimmer_profile` / `captain_profile` schema changes (Phases 2 and 3).
- No `captain_club` / "Assigned Clubs" relationship (dropped).

## Backend components

### Domain — entities

`Identity.Domain/Entities/`, mirroring `Gender` (public get / private set, private EF ctor + public ctor that trims and assigns `Guid.NewGuid()`):

**`Stroke`** → `reference.stroke`
| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | PK |
| `Code` | `string` | `freestyle \| backstroke \| butterfly \| breaststroke \| medley`, unique |
| `NameEn` | `string` | required |
| `NameAr` | `string?` | optional |

Ctor: `Stroke(string code, string nameEn, string? nameAr = null)`.

**`BloodType`** → `reference.blood_type`
| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | PK |
| `Code` | `string` | `A+ \| A- \| B+ \| B- \| AB+ \| AB- \| O+ \| O-`, unique |
| `NameEn` | `string` | required (= the notation, e.g. `A+`) |
| `NameAr` | `string?` | optional |

Ctor: `BloodType(string code, string nameEn, string? nameAr = null)`.

**`Club`** → `reference.club` (no `code` — matches the diagram)
| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | PK |
| `NameEn` | `string` | required |
| `NameAr` | `string?` | optional |
| `CreatedAt` | `DateTime` | UTC, set in ctor |

Ctor: `Club(string nameEn, string? nameAr = null)`.

### Infrastructure — configurations, DbSets, repositories

- **Configs** (`Identity.Infrastructure/Configurations/`), mirroring `GenderConfiguration`:
  - `StrokeConfiguration` — `ToTable("stroke", "reference")`, `Code` `HasMaxLength(50)` required + unique index, `NameEn` `HasMaxLength(100)` required, `NameAr` `HasMaxLength(100)`.
  - `BloodTypeConfiguration` — `ToTable("blood_type", "reference")`, `Code` `HasMaxLength(10)` required + unique index, `NameEn` `HasMaxLength(100)` required, `NameAr` `HasMaxLength(100)`.
  - `ClubConfiguration` — `ToTable("club", "reference")`, `NameEn` `HasMaxLength(200)` required, `NameAr` `HasMaxLength(200)`, `CreatedAt` required. (No `code`; no unique index on name — the seed list may contain near-duplicate transliterations, and identity is by `Id`.)
  - Picked up automatically by `ApplyConfigurationsFromAssembly`.
- **`IdentityDbContext`** — add `DbSet<Stroke> Strokes`, `DbSet<BloodType> BloodTypes`, `DbSet<Club> Clubs`.
- **Repositories** (`Domain/Repositories` interfaces + `Infrastructure/Repositories` impls), mirroring `RoleRepository.GetAllAsync` (`AsNoTracking().OrderBy(...).ToListAsync`):
  - `IStrokeRepository` / `StrokeRepository` — `GetAllAsync` ordered by `NameEn`.
  - `IBloodTypeRepository` / `BloodTypeRepository` — `GetAllAsync` ordered by `Code`.
  - `IClubRepository` / `ClubRepository` — `GetAllAsync` ordered by `NameEn`.
  - `IGenderRepository` / `GenderRepository` — `GetAllAsync` ordered by `NameEn` (new; genders had no repository before).
- **DI** — register the four repositories and `IReferenceService` in `IdentityModuleExtensions.AddIdentityModule`.

### Application — DTOs, service, messages

- **DTOs** (`Application/DTOs/ReferenceDtos.cs`):
  - `CodedLookupDto(Guid Id, string Code, string NameEn, string? NameAr)` — for strokes, blood types, genders (mirrors the existing `RoleDto` shape).
  - `ClubDto(Guid Id, string NameEn, string? NameAr)` — clubs have no code.
- **`IReferenceService` / `ReferenceService`** (mirroring `RoleService`) — one service aggregating the four reads:
  - `Task<IReadOnlyList<ClubDto>> GetClubsAsync(ct)`
  - `Task<IReadOnlyList<CodedLookupDto>> GetBloodTypesAsync(ct)`
  - `Task<IReadOnlyList<CodedLookupDto>> GetStrokesAsync(ct)`
  - `Task<IReadOnlyList<CodedLookupDto>> GetGendersAsync(ct)`
  - Each maps entities → DTOs via `.Select(...)`.
- **`Resources/ReferenceMessages.cs`** — localized (EN + AR) success messages per list, following `SwimmerMessages` / `RoleMessages` (e.g. `ClubsListed`, `BloodTypesListed`, `StrokesListed`, `GendersListed`).

### API — controller

`Controllers/ReferenceController.cs : BaseApiController`, mirroring `RolesController` (constructor-injected service, `ApiResponse<...>.Success(message, data)` envelope, `[ProducesResponseType]`), all `[Authorize]`:

| Route | Returns |
|-------|---------|
| `GET /api/reference/clubs` | `ApiResponse<IReadOnlyList<ClubDto>>` |
| `GET /api/reference/blood-types` | `ApiResponse<IReadOnlyList<CodedLookupDto>>` |
| `GET /api/reference/strokes` | `ApiResponse<IReadOnlyList<CodedLookupDto>>` |
| `GET /api/reference/genders` | `ApiResponse<IReadOnlyList<CodedLookupDto>>` |

### Seeding

Extend `IdentitySeeder.SeedAsync` with idempotent `EnsureStroke` / `EnsureBloodType` / `EnsureClub` helpers (same `FirstOrDefaultAsync` → add-if-null shape as `EnsureRole` / `EnsureGender`), called before `SaveChangesAsync`:

- **Strokes (5):** `freestyle`/Freestyle/حرة, `backstroke`/Backstroke/ظهر, `butterfly`/Butterfly/فراشة, `breaststroke`/Breaststroke/صدر, `medley`/IM/متنوع فردي. (Arabic names taken from the prototype's chip labels; `medley` display name is "IM".)
- **Blood types (8):** `A+`,`A-`,`B+`,`B-`,`AB+`,`AB-`,`O+`,`O-` (`name_en` = code; `name_ar` = code — universal notation).
- **Clubs (70):** the fixed Egyptian list from `docs/superpowers/specs/2026-08-30-swimming-database-design.md` §10 (EN + AR). Idempotency by name: seed a club only if no club with that `NameEn` exists (so re-running never duplicates). Seed the full list in one `EnsureClubs` pass.

### Migration (with safety pre-check)

1. **Pre-check:** confirm the model has no pending changes *other than* the three new reference tables (`dotnet ef migrations has-pending-model-changes` and/or inspect the generated migration before saving). The working tree has an in-progress identity refactor — if unrelated schema edits would be swept in, **stop and consult** rather than committing a mixed migration.
2. Generate `AddReferenceLookups` for `IdentityDbContext`.
3. Verify the migration's `Up` creates only `reference.stroke`, `reference.blood_type`, `reference.club` (+ the two unique indexes on `stroke.code` / `blood_type.code`).
4. It auto-applies at startup via `MigrationExtensions.ApplyIdentityMigrationsAsync`, then the seeder runs.

> Requires the running dev server **stopped** (build/output DLL lock) and the EF design tools. See the "Risks" note.

## Frontend components

New `features/reference` slice, mirroring the existing `features/swimmers` slice (fetch-only repository → use-case → DTO validator, `HttpClientService`, `UseCase<In,Out>`, `AppError('…','validation')`):

- **`data/dto/reference.dto.ts`**
  - `CodedLookupDtoRs { id: string; code: string; nameEn: string; nameAr: string | null }` + `isCodedLookupListValid(x): boolean`.
  - `ClubDtoRs { id: string; nameEn: string; nameAr: string | null }` + `isClubListValid(x): boolean`.
- **`domain/model/reference.ts`** — `LookupItem { id: string; code?: string; nameEn: string; nameAr: string | null }` (the mapped shape use-cases return).
- **`domain/repositories/reference.repository.ts`** — `IReferenceRepository` with `getClubs()`, `getBloodTypes()`, `getStrokes()`, `getGenders()` (each returns the envelope `{ data: … }`), plus a `REFERENCE_REPOSITORY` injection token.
- **`data/repositories/reference.repository.impl.ts`** — `@Injectable({providedIn:'root'})` calling `http.get<...>('/api/reference/clubs' | '/blood-types' | '/strokes' | '/genders')`.
- **`domain/usecases/`** — `LoadClubsUseCase`, `LoadBloodTypesUseCase`, `LoadStrokesUseCase`, `LoadGendersUseCase`. Each validates `res.data` with the matching validator (throw `AppError('Invalid … received','validation')` on failure) and maps to `LookupItem[]`.
- **`data/reference.providers.ts`** + **`index.ts`** — DI provider binding `REFERENCE_REPOSITORY → ReferenceRepositoryImpl`, wired alongside the existing feature providers.

No component/page changes in this phase — the slice is consumed by the Phase 2/3 forms.

## Data flow

```
Captain Panel form (Phase 2/3)
  → Load{Clubs|BloodTypes|Strokes|Genders}UseCase.run()
    → ReferenceRepository.get…()  → GET /api/reference/…  [Authorize]
      → ReferenceController → ReferenceService.Get…Async → {Club|BloodType|Stroke|Gender}Repository.GetAllAsync
        → SELECT … FROM reference.<table>
  → validated → LookupItem[] → dropdown/chip options (value = id, label = nameEn/nameAr)
```

## Error handling

- List endpoints have no failure modes beyond transport; on the frontend a failed/malformed load throws `AppError(…, 'validation')`, surfaced by the existing use-case/error handling (the consuming form decides how to degrade — e.g. disable the control). Empty tables return an empty list (not an error), but seeding guarantees data.
- Endpoints are authenticated, so they participate in the normal 401→refresh interceptor path.

## Testing

- **Backend unit:**
  - `IdentitySeederTests` — after seeding, exactly 5 strokes / 8 blood types / 70 clubs exist, and re-running the seeder does not duplicate (idempotent). Follows the existing `IdentitySeederTests` style.
  - `ReferenceService` — each `Get…Async` returns the repository rows mapped to DTOs (mocked repositories).
  - `ReferenceController` — each endpoint returns 200 with the list in the `ApiResponse` envelope (follows `AuthControllerTests` / `RolesControllerTests`).
  - Architecture tests continue to pass (new types respect module boundaries — reference entities live in Identity, like `Gender`/`Role`).
- **Frontend:**
  - Each `Load…UseCase` spec — valid DTO maps to `LookupItem[]`; malformed DTO → `validation` failure.
  - `reference.repository.impl` spec — each method issues the correct `GET` path.

## Files

**Backend — new:** `Stroke.cs`, `BloodType.cs`, `Club.cs` (entities); `StrokeConfiguration.cs`, `BloodTypeConfiguration.cs`, `ClubConfiguration.cs`; `IStrokeRepository.cs`/`StrokeRepository.cs`, `IBloodTypeRepository.cs`/`BloodTypeRepository.cs`, `IClubRepository.cs`/`ClubRepository.cs`, `IGenderRepository.cs`/`GenderRepository.cs`; `ReferenceDtos.cs`; `IReferenceService.cs`/`ReferenceService.cs`; `ReferenceMessages.cs`; `ReferenceController.cs`; `AddReferenceLookups` migration (+ designer + snapshot update).
**Backend — edit:** `IdentityDbContext.cs` (DbSets), `IdentityModuleExtensions.cs` (DI), `IdentitySeeder.cs` (seed helpers + calls).
**Frontend — new:** `reference.dto.ts`, `reference.ts` (model), `reference.repository.ts` (port + token), `reference.repository.impl.ts`, `load-clubs.use-case.ts`, `load-blood-types.use-case.ts`, `load-strokes.use-case.ts`, `load-genders.use-case.ts`, `reference.providers.ts`, `index.ts`, and matching `testing/` specs.
**Frontend — edit:** provider registration/barrel as needed.

## Risks

- **Migration sweep** — the working tree has an in-progress identity refactor; the pre-check guards against a mixed migration. If the model shows unrelated pending changes, stop and consult.
- **Dev server lock** — building/migrating/testing requires the running API stopped (documented DLL-lock constraint; also noted in memory).
- **Club seed accuracy** — English transliterations in §10 are best-effort; names can be corrected later without schema impact (identity is by `Id`). No unique constraint on club name, so a corrected duplicate would need a manual cleanup, not a migration.
- **Gender label duplication** — the app currently has a `core/domain/gender` enum + i18n labels used elsewhere (e.g. login). This phase adds an API-sourced gender lookup for the forms; the two coexist. Consolidation (if desired) is out of scope.
