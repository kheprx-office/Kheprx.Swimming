# Register Swimmer — Design

**Date:** 2026-09-02
**Status:** Draft for review
**Scope:** Backend swimmer-registration subsystem (expand `swimmer_profile`, add `swimmer` role + `athlete.swimmer_specialization`, `POST /api/swimmers`, re-seed demo swimmers, migration) and the Angular Account Creation page with a working **Register Swimmer** form.

> **Phase 2 of 3** of the Captain Panel → Account Creation effort. Builds on **Phase 1 — Reference Lookups** (`docs/superpowers/specs/2026-09-02-reference-lookups-design.md`), which delivered `reference.club` / `blood_type` / `stroke` + read endpoints and the Angular `features/reference` loader slice. **Phase 3 — Register Captain / Head Coach** is a later spec.

## Goal

Let a Head Coach or Captain create a swimmer account from the Captain Panel. A swimmer becomes a full `app_user` (role `swimmer`, username, backend-assigned generic password, `is_first_login = true`) plus a `swimmer_profile` (system `uid`, training club, optional championship club, blood type) plus one `swimmer_specialization` per selected stroke — all created atomically by `POST /api/swimmers`. The Angular form is the first real Account Creation UI, wired to the Phase-1 reference lookups by ID.

## Decisions (agreed)

1. **Registration only.** Swimmers get login-ready accounts, but swimmer **login-by-username** and a swimmer login UI are **out of scope** (a later phase) — login is email-only today (`AuthService.LoginAsync` → `GetByEmailAsync`). We set the data up correctly (`username`, generic password, `is_first_login = true`) so that later phase just wires the login half.
2. **Required fields.** Always required: **Name (EN)**, **Username**, **Training Club**, and — per product choice — **Gender**, **Date of Birth**, **Blood Type**, and **≥1 specialization**. Optional: Name (AR), Email, Phone, Championship Club. These are enforced by the API validator **and** the form, even though Gender/DOB/BloodType columns are DB-nullable (an app rule stricter than the schema; the columns stay nullable to match the diagram).
3. **Page shape.** Build the Account Creation page with two tabs — **Register Swimmer** (functional) and **Register Captain** (Phase-3 stub).
4. **Generic password from config**, returned in the create response so the coach can relay it (it is a shared, configured value — not a per-user secret). The frontend never hardcodes it.
5. **`swimmer_specialization` lives in the `athlete` schema but inside the Identity module** — no new module/DbContext, consistent with how Phase 1 kept `reference` tables in the Identity module.
6. **The migration deletes the 24 demo `swimmer_profile` rows** (regenerable seed data) so the required `user_id` FK can be added cleanly; the seeder re-creates them as full records.
7. **Widen the `captain-panel` route guard** to `head_coach` + `captain` (the Head Coach is the primary user of this panel).

## Non-goals

- No swimmer login / login-page changes.
- No Register Captain / Head Coach implementation (Phase-3 stub tab only).
- No other Captain-Panel cards (Swimmer Records / Medical Tests / Health Monitoring).
- No swimmer list / edit / delete (create only).
- No `guardian` / `medical_exam` / `body_measurement` (diagram Athlete tables) — later.

---

## Backend

### Domain — entities

**`SwimmerProfile`** (restructure `Identity.Domain/Entities/SwimmerProfile.cs`). Current minimal shape (`Uid, NameEn, NameAr, CreatedAt`) → new:

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | PK |
| `UserId` | `Guid` | FK → `app_user.Id`, unique, required |
| `Uid` | `string` | system-generated business id `SW-####`, unique |
| `TrainingClubId` | `Guid` | FK → `reference.club`, required |
| `RepresentChampionshipClubId` | `Guid?` | FK → `reference.club`, nullable |
| `BloodTypeId` | `Guid?` | FK → `reference.blood_type`, nullable |
| `CreatedAt` | `DateTime` | UTC, set in ctor |
| `UpdatedAt` | `DateTime` | UTC, set in ctor (= created initially) |

- **Remove** `NameEn`/`NameAr` (names now live on `app_user`).
- Ctor: `SwimmerProfile(Guid userId, string uid, Guid trainingClubId, Guid? representChampionshipClubId = null, Guid? bloodTypeId = null)` — assigns `Id`, sets timestamps.
- The count endpoint (`ISwimmerProfileRepository.CountAsync`) is unaffected (still counts rows).

**`SwimmerSpecialization`** (new, `Identity.Domain/Entities/SwimmerSpecialization.cs`) — swimmer↔stroke junction:

| Property | Type | Notes |
|----------|------|-------|
| `SwimmerProfileId` | `Guid` | FK → `swimmer_profile.Id` |
| `StrokeId` | `Guid` | FK → `reference.stroke.Id` |

- Composite key `(SwimmerProfileId, StrokeId)`. Ctor: `SwimmerSpecialization(Guid swimmerProfileId, Guid strokeId)`.

### Infrastructure — configurations, DbSets

- **`SwimmerProfileConfiguration`** — `ToTable("swimmer_profile", "identity")` (default schema, unchanged); `Uid` `HasMaxLength(32)` required + unique index (existing); `UserId` required + **unique index**; FK `HasOne<AppUser>().WithMany().HasForeignKey(UserId).OnDelete(Cascade)` (deleting the user removes the profile); FK `TrainingClubId` → `Club` (`Restrict`, required); FK `RepresentChampionshipClubId` → `Club` (`Restrict`, nullable); FK `BloodTypeId` → `BloodType` (`Restrict`, nullable); `CreatedAt`/`UpdatedAt` required.
- **`SwimmerSpecializationConfiguration`** — `ToTable("swimmer_specialization", "athlete")`; `HasKey(x => new { x.SwimmerProfileId, x.StrokeId })`; FK `SwimmerProfileId` → `SwimmerProfile` (`Cascade`); FK `StrokeId` → `Stroke` (`Restrict`).
- **`IdentityDbContext`** — add `DbSet<SwimmerSpecialization> SwimmerSpecializations`. (`SwimmerProfiles`, `Strokes`, `Clubs`, `BloodTypes` DbSets already exist.)

### Repositories

- **`IUserRepository`** — add `Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct = default)` (+ impl: `AsNoTracking` lookup by `Username`, used for the uniqueness pre-check).
- **`ISwimmerProfileRepository`** — extend (currently only `CountAsync`):
  - `Task AddAsync(SwimmerProfile profile, CancellationToken ct = default)`
  - `Task AddSpecializationsAsync(IEnumerable<SwimmerSpecialization> specs, CancellationToken ct = default)`
  - `Task<int> GetMaxUidNumberAsync(CancellationToken ct = default)` — parses the numeric suffix of the largest `SW-####` (0 when empty) so the next uid = max + 1.
  - `Task SaveChangesAsync(CancellationToken ct = default)`
- **Reference existence checks** — add `Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)` to `IClubRepository`, `IBloodTypeRepository`, `IStrokeRepository`, `IGenderRepository` (impl: `AnyAsync(x => x.Id == id)`), so the service returns clean 400s instead of raw FK 500s.

### Application — DTOs, options, validator, service, messages

**`SwimmerDtos.cs`** (extend; currently holds `SwimmerCountDto`):
```
CreateSwimmerRequest(
  string NameEn, string Username, Guid TrainingClubId, Guid GenderId,
  DateOnly Dob, Guid BloodTypeId, IReadOnlyList<Guid> StrokeIds,
  string? NameAr, string? Email, string? Phone, Guid? RepresentChampionshipClubId)

CreatedSwimmerDto(Guid Id, string Uid, string Username, string NameEn, string TemporaryPassword)
```

**`SwimmerRegistrationOptions`** — `{ string GenericPassword }`, bound from appsettings section `SwimmerRegistration` (seed value `"Oasis2026!"`; must satisfy the ≥8 password rule). Registered via `services.Configure<SwimmerRegistrationOptions>(config.GetSection("SwimmerRegistration"))` in `IdentityModuleExtensions`. Add the section to `appsettings.json` / `appsettings.Development.json`.

**`CreateSwimmerRequestValidator`** (FluentValidation, localized messages via `AppLanguage.Current`, mirroring `UserRequestValidators`):
- `NameEn` NotEmpty, MaxLength 200.
- `Username` NotEmpty, MaxLength 100, allowed-characters rule (letters/digits/`.`/`_`/`-`, no spaces).
- `TrainingClubId`, `GenderId`, `BloodTypeId` NotEmpty (`!= Guid.Empty`).
- `Dob` NotEmpty and `< today` (valid past date).
- `StrokeIds` NotEmpty (≥1) and no duplicates.
- `NameAr` MaxLength 200 (optional).
- `Email` EmailAddress + MaxLength 256 `When` present.
- `Phone` matches the existing Egyptian pattern `^01[0125]\d{8}$` `When` present.
- `RepresentChampionshipClubId` optional.

**`ISwimmerService` / `SwimmerService`** — add:
```
Task<CreatedSwimmerDto?> CreateAsync(CreateSwimmerRequest request, CancellationToken ct = default)
```
Returns `null` on a uniqueness conflict (controller → 409). Throws `InvalidUserException`/a validation exception for a missing role or an unknown reference id (controller → 400). Flow:
1. Resolve `swimmer` role via `IRoleRepository.GetByCodeAsync("swimmer")`.
2. `GetByUsernameAsync(username)` → if taken, return null. If `Email` present, normalize + `GetByEmailAsync` → if taken, return null.
3. Existence checks: `GenderId`, `TrainingClubId`, `BloodTypeId`, each `StrokeIds`, and `RepresentChampionshipClubId` if present → clean validation error if any missing.
4. `uid = $"SW-{(await GetMaxUidNumberAsync()) + 1:D4}"`.
5. `passwordHash = _hasher.Hash(_options.GenericPassword)`.
6. `new AppUser(username, nameEn, swimmerRole.Id, nameAr, email, passwordHash, genderId, dob, phone, isFirstLogin: true)`.
7. `new SwimmerProfile(user.Id, uid, trainingClubId, representChampionshipClubId, bloodTypeId)`.
8. `StrokeIds.Select(sid => new SwimmerSpecialization(profile.Id, sid))`.
9. `AddAsync` user + profile + specializations, then **one `SaveChangesAsync`** (atomic; all share the scoped `IdentityDbContext`). A unique-index race throws `DbUpdateException` → catch → return null (409).
10. Return `CreatedSwimmerDto(profile.Id, uid, username, nameEn, _options.GenericPassword)`.

**`SwimmerMessages`** — add localized (EN + AR) `SwimmerCreated`, and conflict/validation messages (`UsernameTaken`, `EmailTaken`, `UnknownReference`).

### API — controller

Extend `SwimmersController`:
```
POST /api/swimmers        [Authorize(Roles = "head_coach,captain")]
  body: CreateSwimmerRequest
  201 → ApiResponse<CreatedSwimmerDto>   (SwimmerMessages.Success.SwimmerCreated)
  409 → ApiResponse<CreatedSwimmerDto>.Failure(...) when username/email taken
  400 → validation failures (FluentValidation pipeline) / unknown reference id
```
Follows the `UsersController.Create` pattern (`CreatedAtAction`/`Ok` with the envelope). `GET /api/swimmers/count` is unchanged.

### Seeding

Rework `IdentitySeeder.SeedAsync` order and `EnsureSwimmers`:
- Seed order becomes: **roles (incl. new `swimmer`)** + genders → **strokes / blood types / clubs** (Phase 1) → coaches → **swimmers** (they now need club/blood-type/gender ids, so references must exist first).
- `EnsureSwimmers` (idempotent — guard on `SwimmerProfiles.AnyAsync`): for `i` in 1..24 create an `AppUser` (`username = $"swimmer{i:D2}"`, `nameEn = $"Swimmer {i:D2}"`, `nameAr = $"سبّاح {i:D2}"`, `roleId = swimmer`, `passwordHash = hash(GenericPassword)`, `genderId` alternating male/female, a plausible `dob`, `isFirstLogin: true`) + a `SwimmerProfile` (`uid = SW-{i:D4}`, a seeded `trainingClubId` (e.g. the first seeded club), a `bloodTypeId`) + one `SwimmerSpecialization` (a seeded stroke). Requires the generic password available to the seeder (reuse `IdentitySeeder.DevPassword` or the configured value).

### Migration (with safety pre-check)

`RegisterSwimmerSchema` for `IdentityDbContext`:
1. **Safety pre-check** (`dotnet ef migrations has-pending-model-changes`, same project/startup as Phase 1). Confirm the pending changes are only: swimmer_profile column changes + `athlete.swimmer_specialization` + the `swimmer` role is seed-not-schema. If unrelated changes appear, **stop and consult**.
2. The generated `Up` should: `migrationBuilder.Sql("DELETE FROM identity.swimmer_profile;")` first (clears regenerable demo rows so the NOT-NULL `user_id` can be added); drop `name_en`/`name_ar`; add `user_id` (uuid, NOT NULL) + unique index + FK, `training_club_id` (NOT NULL) + FK, `represent_championship_club_id` (nullable) + FK, `blood_type_id` (nullable) + FK, `updated_at`; create schema `athlete` + table `swimmer_specialization` with the composite PK and two FKs. Hand-add the leading `DELETE` if EF doesn't emit it (EF won't generate data deletes).
3. Verify `Up` touches only swimmer_profile + swimmer_specialization (+ the delete). Auto-applies at startup, then the seeder runs.

> Dev API server must be **stopped** before `dotnet ef` / build / test (DLL lock).

### DI

Register in `IdentityModuleExtensions.AddIdentityModule`: `services.Configure<SwimmerRegistrationOptions>(...)`. Repositories/services already registered (`ISwimmerProfileRepository`, `ISwimmerService`, `IUserRepository`, reference repos) — only new methods are added to existing registrations; no new DI lines beyond the options.

---

## Frontend

### Routing + guard

- Extend `roleGuard` to accept **multiple** roles (`roleGuard('head_coach', 'captain')`); existing single-role calls keep working.
- Widen the `captain-panel` route guard to `roleGuard('head_coach', 'captain')`.
- Add a child route `captain-panel/account-creation` (same guard), lazy-loading `AccountCreationPage`, with the viewmodel(s) in route `providers`.

### Shared form controls (`core/ui/components/`)

New, matching `TextFieldComponent`'s signal + CSS-var + Tailwind style, rendering lookup labels by current language (`LanguageStore.lang()` → `nameAr ?? nameEn` when `ar`):
- **`SelectFieldComponent`** — `label`, `options: LookupItem[]`, `placeholder`, `value = model('')`; used for Gender, Blood Type, Training/Championship Club.
- **`DateFieldComponent`** — `label`, `value = model('')` (`<input type="date">`).
- **`ChipGroupComponent`** — `label`, `options: LookupItem[]`, `selected = model<string[]>([])`; toggle chips (multi-select) for specializations.

### Feature — captain-panel Account Creation

- **`CaptainPanelPage`** — add an "Account Creation" card linking to `account-creation` (keep it minimal; the other mockup cards are out of scope).
- **`AccountCreationPage`** (`presentation/pages/account-creation/`) — header + two tab buttons (**Register Swimmer** / **Register Captain**) via a `tab = signal<'swimmer'|'captain'>('swimmer')`; renders `RegisterSwimmerFormComponent` for `swimmer`, and a "coming soon" stub for `captain`.
- **`RegisterSwimmerFormComponent`** + **`register-swimmer.viewmodel.ts`** (`@Injectable()`, route-provided; signals, no Reactive Forms):
  - Inject `LoadClubsUseCase`, `LoadGendersUseCase`, `LoadBloodTypesUseCase`, `LoadStrokesUseCase` (Phase 1), `CreateSwimmerUseCase`, `NotificationService`, `TranslateService`. Load the four lookups in the constructor via `Promise.all`, store in signals (`clubs`, `genders`, `bloodTypes`, `strokes`).
  - Field signals: `nameEn, nameAr, username, email, phone, genderId, dob, bloodTypeId, trainingClubId, championshipClubId, strokeIds`.
  - `canSubmit = computed(...)` enforcing the required set (NameEn, Username, TrainingClub, Gender, DOB, BloodType, ≥1 stroke); inline error signals for surfaced messages.
  - `created = signal<CreatedSwimmer | null>(null)`; on success, render a credentials panel showing the returned **username + temporary password** and a "create another" reset; `NotificationService.success`. On failure surface `username/email taken` (409) inline + `NotificationService.error`.

### Data layer (extend `features/swimmers`)

- **`data/dto/create-swimmer.dto.ts`** — `CreateSwimmerDtoRq { nameEn; username; trainingClubId; genderId; dob; bloodTypeId; strokeIds; nameAr?; email?; phone?; representChampionshipClubId? }`; `CreatedSwimmerDtoRs { id; uid; username; nameEn; temporaryPassword }`; `CreatedSwimmerItemDtoRs extends BaseResponseRs<CreatedSwimmerDtoRs>`; `isCreatedSwimmerDtoRsValid`.
- **`domain/model/swimmer.ts`** — `CreatedSwimmer { id; uid; username; nameEn; temporaryPassword }`.
- **`domain/repositories/swimmer.repository.ts`** — add `create(rq): Promise<CreatedSwimmerItemDtoRs>` to `ISwimmerRepository`.
- **`data/repositories/swimmer.repository.impl.ts`** — `create(rq) => this.http.post<CreatedSwimmerItemDtoRs>('/api/swimmers', { body: rq })`.
- **`domain/usecases/create-swimmer.use-case.ts`** — `UseCase<CreateSwimmerDtoRq, CreatedSwimmer>`, validates `res.data` (`AppError(...,'validation')`), maps to `CreatedSwimmer`.

### i18n

Add an `accountCreation` namespace to `en.json` + `ar.json`: page/tab titles, every field label + placeholder, the generic-password info block, the create button, success message, the returned-credentials panel labels, and validation messages. Lookup option labels come from the API (`nameEn`/`nameAr`), not i18n.

## Data flow

```
Account Creation page (Register Swimmer tab)
  constructor → Load{Clubs,Genders,BloodTypes,Strokes}UseCase (GET /api/reference/*) → option signals
  submit → CreateSwimmerUseCase.run(dto)
    → SwimmerRepository.create → POST /api/swimmers  [Authorize head_coach,captain]
      → SwimmersController.Create → SwimmerService.CreateAsync
        → resolve swimmer role; uniqueness (username/email); reference existence;
          uid = maxUid+1; hash generic pw;
          AppUser + SwimmerProfile + SwimmerSpecialization[] → one SaveChangesAsync (atomic)
      → 201 ApiResponse<CreatedSwimmerDto>{ id, uid, username, nameEn, temporaryPassword }
  → success: show credentials panel + notification; or 409 → username/email taken inline
```

## Error handling

- **Uniqueness:** username or email taken → service returns null → controller 409; the form surfaces a field-level "already taken" message (mapped from the 409).
- **Unknown reference id / validation:** 400 from the FluentValidation pipeline or the service's existence check; surfaced via the notification/inline errors.
- **Atomicity:** a single `SaveChangesAsync` means a mid-way failure rolls back — no orphan `app_user` without its profile. The unique indexes on `username`/`email`/`uid`/`user_id` backstop races.
- **Frontend transport/validation:** `CreateSwimmerUseCase` returns `Result`; the viewmodel maps `AppError` kinds to user messages (reusing the existing `toUserMessage` pattern).

## Testing

**Backend (xUnit + Moq; in-memory `IdentityDbContext` where entities are persisted):**
- `SwimmerProfile` / `SwimmerSpecialization` entity tests (ctor assigns ids/timestamps; composite key).
- `SwimmerProfileRepository`: `GetMaxUidNumberAsync` (empty → 0; `SW-0007` → 7), `AddAsync` + specializations persist.
- `SwimmerService.CreateAsync`: happy path creates user+profile+specializations and returns the dto with the generic password (mocked repos/options/hasher); username-taken → null; email-taken → null; unknown reference id → error; uid = max+1.
- `CreateSwimmerRequestValidator`: each required field, `StrokeIds` empty/duplicate, dob-in-future, phone/email format.
- `SwimmersController.Create`: 201 on success, 409 on null, 400 on invalid.
- `IdentitySeeder`: after seeding, 24 swimmer profiles each linked to an `app_user` with role `swimmer`; idempotent; the `swimmer` role exists; reference lookups still 5/8/70.

**Frontend (Jest + TestBed):**
- `CreateSwimmerUseCase` (valid maps to `CreatedSwimmer`; malformed → `validation`).
- `swimmer.repository.impl.create` posts `/api/swimmers` with the body.
- `RegisterSwimmerViewModel`: loads the four lookups into signals; `canSubmit` gates on the required set; `submit` success sets `created` + notifies; 409 surfaces "taken".
- `SelectFieldComponent` / `DateFieldComponent` / `ChipGroupComponent`: render options/labels by language; model updates on interaction; chip toggle adds/removes ids.
- `AccountCreationPage`: tab switching renders the swimmer form vs the captain stub.

## Files

**Backend — new:** `SwimmerSpecialization.cs`; `SwimmerSpecializationConfiguration.cs`; `SwimmerRegistrationOptions.cs`; `CreateSwimmerRequestValidator` (in the validators file); `RegisterSwimmerSchema` migration (+ designer + snapshot). **Backend — edit:** `SwimmerProfile.cs`, `SwimmerProfileConfiguration.cs`, `IdentityDbContext.cs`, `IUserRepository.cs`/`UserRepository.cs`, `ISwimmerProfileRepository.cs`/`SwimmerProfileRepository.cs`, the four reference repo interfaces/impls (add `ExistsAsync`), `SwimmerDtos.cs`, `ISwimmerService.cs`/`SwimmerService.cs`, `SwimmerMessages.cs`, `SwimmersController.cs`, `IdentitySeeder.cs`, `IdentityModuleExtensions.cs`, `appsettings*.json`, plus matching test files.
**Frontend — new:** `select-field.component.ts`, `date-field.component.ts`, `chip-group.component.ts`; `account-creation.page.*`, `register-swimmer-form.component.*`, `register-swimmer.viewmodel.ts`; `create-swimmer.dto.ts`, `swimmer.ts` (model), `create-swimmer.use-case.ts`; `en`/`ar` `accountCreation` keys; matching `testing/` specs. **Frontend — edit:** `app.routes.ts`, `role.guard.ts` (multi-role), `swimmer.repository.ts`/`.impl.ts`, `captain-panel.page.*`, `captain-panel/index.ts`, `en.json`/`ar.json`.

## Risks

- **Migration data-delete** — dropping the 24 demo `swimmer_profile` rows is intentional (regenerable) but destructive; guarded by the pre-check and only ever touches demo data. If the dev DB ever held real swimmers this would need a backfill instead.
- **Role-claim authz** — `[Authorize(Roles = "head_coach,captain")]` assumes the JWT carries the role code in the role claim (as the existing `[Authorize(Roles="admin")]` on `UsersController` implies). Verify during implementation; if the claim name differs, adjust the auth policy.
- **Swimmers can't log in yet** — by design (Phase-2 scope). `is_first_login = true` + generic password are set so the future username-login phase works without a data migration.
- **Migration sweep / dev-server lock** — same guards as Phase 1 (pre-check; stop the API before ef/build/test).
- **uid generation race** — `max+1` under concurrency could collide; the `Uid` unique index catches it and the create returns 409 (coach retries). Acceptable for coach-paced single creates.
