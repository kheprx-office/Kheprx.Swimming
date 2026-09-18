# Swimmer Count — Design

**Date:** 2026-09-01
**Status:** Approved for planning
**Scope:** Backend swimmers table + count endpoint + seed data, and wiring the login page's swimmer stat to the live count.

## Goal

The login page shows a hardcoded "452 Swimmers tracked" (`frontend/.../login/login.page.html`). Replace it with a real, database-backed count: create a `swimmer_profile` table, seed demo swimmers, expose an anonymous count endpoint, and display the live number on the login page.

## Decisions (agreed)

1. **Minimal table.** Build `identity.swimmer_profile` with only the diagram columns that need no other new tables. No FKs to `app_user`, `reference.club`, or `reference.blood_type` yet. The full diagram-faithful entity (club/blood-type FKs, app_user linkage, athlete/health schemas) is deferred to when the swimmers subsystem is built for real.
2. **Seed 24 demo swimmers** (idempotent). The endpoint returns whatever rows exist; 24 is the initial real number.
3. **Wire the frontend.** Replace the hardcoded `452` with the live count.

## Non-goals

- No `reference.club` / `reference.blood_type` tables.
- No swimmer login / no swimmer `app_user` rows / no swimmer role.
- No athlete/health/measurement tables from the diagram.
- No swimmer CRUD — only a count. (List/create/update come later.)

## Placement

Swimmers live in the **Identity module**. The diagram places `swimmer_profile` in the `identity` schema, and `IdentityDbContext` already defaults to `identity` (`HasDefaultSchema("identity")`). This reuses the existing DbContext, migration history, DI wiring, and `BaseApiController` patterns — no new module or DbContext.

## Components

### Domain — `SwimmerProfile`
`Identity.Domain/Entities/SwimmerProfile.cs`, mirroring `CaptainProfile`'s style (private EF ctor + public ctor, private setters):

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | PK, assigned in ctor |
| `Uid` | `string` | Unique business id, e.g. `SW-0001` |
| `NameEn` | `string` | Required |
| `NameAr` | `string?` | Optional |
| `CreatedAt` | `DateTime` | UTC, set in ctor |

Public ctor: `SwimmerProfile(string uid, string nameEn, string? nameAr = null)`.

### Infrastructure
- `Configurations/SwimmerProfileConfiguration.cs` — `ToTable("swimmer_profile")`, `HasKey(Id)`, `Uid` `HasMaxLength(32)` required + unique index, `NameEn` `HasMaxLength(200)` required, `NameAr` `HasMaxLength(200)`, `CreatedAt` required. Mirrors `CaptainProfileConfiguration` (minus the `AppUser` FK). Picked up automatically by `ApplyConfigurationsFromAssembly`.
- `IdentityDbContext` — add `public DbSet<SwimmerProfile> SwimmerProfiles => Set<SwimmerProfile>();`.
- `Domain/Repositories/ISwimmerProfileRepository.cs` — `Task<int> CountAsync(CancellationToken ct = default);`.
- `Repositories/SwimmerProfileRepository.cs` — `_db.SwimmerProfiles.AsNoTracking().CountAsync(ct)` (mirrors `RoleRepository`).
- Register both repository and service in `IdentityModuleExtensions.AddIdentityModule`.

### Application
- `DTOs` — `SwimmerCountDto(int Count)` (new record; may live in a `SwimmerDtos.cs`).
- `Services/Interfaces/ISwimmerService.cs` — `Task<SwimmerCountDto> GetCountAsync(CancellationToken ct = default);`.
- `Services/SwimmerService.cs` — calls `ISwimmerProfileRepository.CountAsync`, wraps in `SwimmerCountDto`.
- `Resources/SwimmerMessages.cs` — localized (EN + AR) success message for the count endpoint, following `RoleMessages`.

### API
- `Controllers/SwimmersController.cs : BaseApiController` — `[HttpGet("count")]`, `[AllowAnonymous]`, returns `ApiResponse<SwimmerCountDto>`. Route: `GET /api/swimmers/count`. Anonymous because the login page reads it before authentication (consistent with the `/api/roles` decision).

### Seeding
Extend `IdentitySeeder.SeedAsync`: if `!await db.SwimmerProfiles.AnyAsync(ct)`, insert 24 `SwimmerProfile` rows with `Uid` `SW-0001`…`SW-0024` and simple generated names. Idempotent — re-running the seeder never duplicates.

### Migration (with safety pre-check)
1. **Pre-check:** confirm the model has no pending changes *other than* `swimmer_profile` (e.g. `dotnet ef migrations has-pending-model-changes`, or inspect the generated migration before saving). If unrelated in-progress schema edits would be swept in, **stop and consult** rather than committing a mixed migration.
2. Generate `AddSwimmerProfile` for `IdentityDbContext` (via `IdentityDbContextFactory`).
3. Verify the migration's `Up` creates only `identity.swimmer_profile` + its unique index.
4. It auto-applies at startup via `MigrationExtensions.ApplyIdentityMigrationsAsync` (`MigrateAsync`), then the seeder runs.

> Requires the running dev server stopped (build/output lock) and the EF design tools.

### Frontend
New minimal `features/swimmers` slice, mirroring the roles slice built earlier:
- `data/dto/swimmer-count.dto.ts` — `SwimmerCountDtoRs { count: number }` + `isSwimmerCountDtoRsValid`.
- `domain/repositories/swimmer.repository.ts` — port `getCount()` + `SWIMMER_REPOSITORY` token.
- `data/repositories/swimmer.repository.impl.ts` — `GET /api/swimmers/count` via `HttpClientService`.
- `domain/usecases/load-swimmer-count.use-case.ts` — validates + maps to a number.
- DI provider wiring alongside the existing repository providers.

Login page wiring:
- `login.viewmodel.ts` — inject `LoadSwimmerCountUseCase`; add `readonly swimmerCount = signal<number | null>(null)`; load on construction (best-effort; failure leaves it null).
- `login.page.html` — replace the hardcoded `452` with `{{ vm.swimmerCount() ?? 452 }}` (452 as graceful fallback until the value loads / on error).

## Data flow

```
Login page load
  → LoadSwimmerCountUseCase.run()
    → SwimmerRepository.getCount()  → GET /api/swimmers/count  [AllowAnonymous]
      → SwimmersController.Count → SwimmerService.GetCountAsync → SwimmerProfileRepository.CountAsync → SELECT count(*) FROM identity.swimmer_profile
  → swimmerCount signal set → hero stat renders the live number
```

## Error handling

- Count endpoint: no failure modes beyond transport; on the frontend a failed/slow load leaves `swimmerCount` null and the UI shows the `452` fallback (no error surfaced on the hero — it is decorative).
- Endpoint is anonymous, so it is not affected by the auth interceptor's 401→refresh path.

## Testing

- **Backend unit:** `SwimmerService.GetCountAsync` returns the repository count (mocked repo); `SwimmersController.Count` returns 200 with the count in the envelope. (Follows `RoleServiceTests` / `AuthControllerTests` style.)
- **Frontend:** `LoadSwimmerCountUseCase` spec (valid maps to number; malformed DTO → `validation` fail); `swimmer.repository.impl` spec (`GET /api/swimmers/count`); `login.viewmodel` spec (count loads into the signal).

## Files

**Backend — new:** `SwimmerProfile.cs`, `SwimmerProfileConfiguration.cs`, `ISwimmerProfileRepository.cs`, `SwimmerProfileRepository.cs`, `SwimmerDtos.cs`, `ISwimmerService.cs`, `SwimmerService.cs`, `SwimmerMessages.cs`, `SwimmersController.cs`, `AddSwimmerProfile` migration (+ designer + snapshot update).
**Backend — edit:** `IdentityDbContext.cs`, `IdentityModuleExtensions.cs`, `IdentitySeeder.cs`.
**Frontend — new:** `swimmer-count.dto.ts`, `swimmer.repository.ts`, `swimmer.repository.impl.ts`, `load-swimmer-count.use-case.ts`, DI provider, specs.
**Frontend — edit:** `login.viewmodel.ts`, `login.page.html`, providers/barrel as needed.

## Risks

- **Migration sweep** — the working tree has an in-progress schema refactor; the pre-check above guards against a mixed migration.
- **Dev server lock** — building/migrating requires the running API stopped (same as prior tasks).
- **Divergence from the diagram** — the minimal table intentionally omits diagram columns/FKs; documented as a known, deferred gap.
