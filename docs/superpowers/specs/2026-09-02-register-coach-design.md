# Register Captain / Head Coach — Design

**Date:** 2026-09-02
**Status:** Draft for review
**Scope:** A create endpoint (`POST /api/coaches`) that registers a Captain or Head Coach (app_user + the matching profile), and the Angular Register Captain form that fills the currently-stubbed tab on the Account Creation page.

> **Phase 3 of 3** of the Captain Panel → Account Creation effort. Builds on Phase 1 (reference lookups) and Phase 2 (Register Swimmer + the Account Creation page + shared form controls). This phase needs **no schema change** — `identity.captain_profile` and `identity.head_coach_profile` already exist.

## Goal

Let a Head Coach register a Captain or Head Coach from the Captain Panel. One form with a "Captain Type" switch chooses the role; the backend creates an `app_user` (the chosen role, username, backend-assigned generic password, `is_first_login = true`, required email) plus a `captain_profile` **or** `head_coach_profile` (`national_id`) atomically via `POST /api/coaches`. Unlike swimmers, coaches log in by **email** (already supported), so their generic-password + forced-first-login flow works immediately.

## Existing building blocks (no migration)

- `CaptainProfile` / `HeadCoachProfile` entities: `{ Id, UserId, NationalId, CreatedAt }`, ctor `(Guid userId, string nationalId)`.
- Configs enforce `national_id` `varchar(14)` **unique** and `user_id` **unique** FK → `app_user` (cascade).
- `ICoachProfileRepository` already exposes `AddCaptainAsync`, `AddHeadCoachAsync`, `SaveChangesAsync`, `GetNationalIdAsync` (the Add/Save methods are currently unused — safe to extend).
- Roles `captain` and `head_coach` are seeded; a demo captain + head coach exist.

## Decisions (agreed)

1. **One endpoint** `POST /api/coaches` with a `role` discriminator (`captain` | `head_coach`) — matches the single form + switch; `[Authorize(Roles = "head_coach")]` (only a Head Coach creates coaches; Captains create swimmers only, per Phase 1).
2. **Required fields (API validator AND form):** Name (EN), Username, **Email** (the login handle), National ID (exactly 14 digits), Captain Type, **Gender, Date of Birth, Phone**. Only **Name (AR)** is optional. (Gender/DOB columns stay DB-nullable — app rule stricter than schema.)
3. **Generalize the generic-password option:** rename Phase 2's `SwimmerRegistrationOptions` (section `SwimmerRegistration`) → shared **`AccountCreationOptions`** (section `AccountCreation`), consumed by both `SwimmerService` and `CoachService` — one concept for the whole feature.
4. **Repository conflict translation:** `ICoachProfileRepository.SaveChangesAsync` returns `Task<bool>` (false = Postgres `23505` unique-violation), matching the Phase-2 swimmer repo, so a uniqueness race → 409 and the Application layer stays EF-free.
5. **Permissions block is display-only** — informational chips from the mockup, not persisted (consistent with Assigned Clubs being dropped).
6. **`features/coaches`** frontend slice for the create data layer, mirroring `features/swimmers`.

## Non-goals

- No schema/migration (profiles already exist).
- No Assigned Clubs; no permissions persistence.
- No swimmer changes; no login/login-page changes (coach email login already works).
- No coach list / edit / delete (create only).

---

## Backend

### Application — DTOs, options, validator, service, messages

**`CoachDtos.cs`** (new):
```
CreateCoachRequest(
  string Role,            // "captain" | "head_coach"
  string NameEn, string Username, string Email, string NationalId,
  Guid GenderId, DateOnly Dob, string Phone,
  string? NameAr)

CreatedCoachDto(Guid Id, string Username, string NameEn, string Role, string TemporaryPassword)
```

**`AccountCreationOptions`** — rename of `SwimmerRegistrationOptions`: `{ string GenericPassword }`, `SectionName = "AccountCreation"`. Update `appsettings.json` / `appsettings.Development.json` (rename the `SwimmerRegistration` section to `AccountCreation`, same value `"Oasis2026!"`), the `services.Configure<...>` registration, and `SwimmerService`'s injection (`IOptions<AccountCreationOptions>`). No behavior change for swimmers.

**`CreateCoachRequestValidator`** (FluentValidation, localized via `AppLanguage.Current`, mirroring `CreateSwimmerRequestValidator`):
- `Role` NotEmpty and `Must(r => r is "captain" or "head_coach")`.
- `NameEn` NotEmpty, MaxLength 200.
- `Username` NotEmpty, MaxLength 100, charset `^[A-Za-z0-9._-]+$`.
- `Email` NotEmpty, EmailAddress, MaxLength 256.
- `NationalId` NotEmpty, `Matches(@"^\d{14}$")` (exactly 14 digits).
- `GenderId` `NotEqual(Guid.Empty)`; `Dob` past date; `Phone` NotEmpty + Egyptian pattern (`UserValidationRules.PhonePattern`).
- `NameAr` MaxLength 200 (optional).

**`ICoachService` / `CoachService`** (`internal sealed`):
```
Task<CreatedCoachDto?> CreateAsync(CreateCoachRequest request, CancellationToken ct = default)
```
Injects `IUserRepository`, `IRoleRepository`, `ICoachProfileRepository`, `IGenderRepository`, `IPasswordHasher`, `IOptions<AccountCreationOptions>`. Flow:
1. Resolve role via `GetByCodeAsync(request.Role)` → throw `InvalidUserException` if unknown (validator already restricts to the two codes).
2. `GetByUsernameAsync(username)` taken → return null; email present-and-taken (`GetByEmailAsync`) → return null; `NationalIdExistsAsync(nationalId)` → return null.
3. Gender existence check (`_genders.ExistsAsync`) for a clean 400.
4. `passwordHash = _hasher.Hash(_options.GenericPassword)`.
5. `new AppUser(username, nameEn, role.Id, nameAr, email, passwordHash, genderId, dob, phone, isFirstLogin: true)`; `_users.AddAsync(user)`.
6. `role.Code == "captain"` → `_coaches.AddCaptainAsync(new CaptainProfile(user.Id, nationalId))`; else `_coaches.AddHeadCoachAsync(new HeadCoachProfile(user.Id, nationalId))`.
7. `if (!await _coaches.SaveChangesAsync(ct)) return null;` (single atomic save; 23505 race → null → 409).
8. Return `CreatedCoachDto(user.Id, user.Username, user.NameEn, role.Code, _options.GenericPassword)`.

**`CoachMessages`** — localized (EN + AR) `Success.CoachCreated` and `Errors.{UsernameTaken, EmailTaken, NationalIdTaken, UnknownGender}`.

### Infrastructure — repository

`ICoachProfileRepository` / `CoachProfileRepository`:
- Add `Task<bool> NationalIdExistsAsync(string nationalId, CancellationToken ct = default)` → `_db.CaptainProfiles.AnyAsync(p => p.NationalId == nationalId) || _db.HeadCoachProfiles.AnyAsync(...)` (two `AnyAsync`, OR).
- Change `SaveChangesAsync` from `Task` to `Task<bool>`: `try { await _db.SaveChangesAsync(ct); return true; } catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" }) { return false; }` (mirrors `SwimmerProfileRepository`; add `using Npgsql;`). No current caller depends on the old `Task` return (AuthService only calls `GetNationalIdAsync`).

### API — controller

New `CoachesController : BaseApiController` (mirrors `SwimmersController.Create`):
```
POST /api/coaches   [Authorize(Roles = "head_coach")]
  body: CreateCoachRequest
  201 → ApiResponse<CreatedCoachDto>  (CoachMessages.Success.CoachCreated)
  409 → ApiResponse<CreatedCoachDto>.Failure(...) when username/email/national_id taken
  400 → validation failures / unknown gender
```
Use `StatusCode(201, body)` and `StatusCode(409, body)` (plain `ObjectResult`, consistent with the swimmer controller + exact-type test assertions).

### DI

In `IdentityModuleExtensions.AddIdentityModule`: register `services.AddScoped<ICoachService, CoachService>();` and update the options registration to `AccountCreationOptions`. `ICoachProfileRepository` is already registered.

---

## Frontend

### Feature — coaches data slice (`features/coaches`, mirrors `features/swimmers`)

- **`data/dto/create-coach.dto.ts`** — `CreateCoachDtoRq { role; nameEn; username; email; nationalId; genderId; dob; phone; nameAr? }`; `CreatedCoachDtoRs { id; username; nameEn; role; temporaryPassword }`; `CreatedCoachItemDtoRs extends BaseResponseRs<CreatedCoachDtoRs>`; `isCreatedCoachDtoRsValid`.
- **`domain/model/coach.ts`** — `CreatedCoach { id; username; nameEn; role; temporaryPassword }`.
- **`domain/repositories/coach.repository.ts`** — `ICoachRepository { create(rq): Promise<CreatedCoachItemDtoRs> }` + `COACH_REPOSITORY` token.
- **`data/repositories/coach.repository.impl.ts`** — `create(rq) => http.post<CreatedCoachItemDtoRs>('/api/coaches', { body: rq })`.
- **`domain/usecases/create-coach.use-case.ts`** — `UseCase<CreateCoachDtoRq, CreatedCoach>`, validate + map.
- **`data/coach.providers.ts`** + `index.ts` — `COACH_PROVIDERS` bound and spread into `app.config.ts` alongside the others.

### Register Captain form (fills the stubbed tab)

- **`RegisterCoachFormComponent`** + **`register-coach.viewmodel.ts`** under `captain-panel/presentation/pages/account-creation/`, signals-based, mirroring the swimmer form/viewmodel. Injects `LoadGendersUseCase` (Phase 1), `CreateCoachUseCase`, `NotificationService`, `TranslateService`.
  - Field signals: `role` (default `'captain'`), `nameEn, nameAr, username, email, phone, nationalId, genderId, dob`.
  - Captain Type is a `SelectFieldComponent`-style choice of the two roles — but its options are the role **codes** with labels from `roles.*` i18n (not a lookup API); implement as a small inline select or a lightweight two-option control (reuse `SelectFieldComponent` by passing `[{id:'head_coach',nameEn:'Head Coach',nameAr:'المدرب العام'},{id:'captain',...}]`, or a plain `<select>` bound to the `role` signal — keep it simple).
  - `canSubmit` enforces the required set (NameEn, Username, Email, NationalId, Gender, DOB, Phone, Role). `nationalId` client check: 14 digits.
  - A static, display-only **Permissions** info block (mockup text), rendered from i18n.
  - `submit()` → `CreateCoachUseCase.run(dto)`; success → `created` signal (credentials panel: username + temp password) + `notify.success`; 409 → surface national-id/username/email taken + `notify.error`; `reset()`.
- **`AccountCreationPage`** — replace the captain-tab stub (`@else { <p>coming soon</p> }`) with `<app-register-coach-form>`; add it to the page's `imports`.
- **`app.routes.ts`** — add `RegisterCoachViewModel` to the `captain-panel/account-creation` route `providers` (alongside `RegisterSwimmerViewModel`); export it from `@features/captain-panel`.

### i18n

Extend the `accountCreation` namespace (EN + AR) with the captain fields: `nationalId`, `captainType`, the `permissions` block (title + the chip labels: Attendance, Swimmer Data, Test Readings, Health, Records), and a `captainSubmit` / reuse `submit`. Reuse `roles.head_coach` / `roles.captain` for the type labels.

## Data flow

```
Account Creation page (Register Captain tab)
  constructor → LoadGendersUseCase (GET /api/reference/genders) → gender options
  submit → CreateCoachUseCase.run(dto)
    → CoachRepository.create → POST /api/coaches  [Authorize head_coach]
      → CoachesController.Create → CoachService.CreateAsync
        → resolve role; uniqueness (username/email/national_id); gender existence;
          hash generic pw; AppUser + (CaptainProfile | HeadCoachProfile) → one SaveChangesAsync (bool)
      → 201 { id, username, nameEn, role, temporaryPassword }
  → success: credentials panel + notification; or 409 → taken (national id / username / email)
```

## Error handling

- Username / email / national_id taken → service returns null → 409; the form surfaces a field-level "already taken" message.
- Unknown gender / validation → 400 (FluentValidation pipeline / existence check).
- Atomicity: single `SaveChangesAsync` — a mid-way failure rolls back (no orphan `app_user`); unique indexes on username/email/national_id/user_id backstop races via the 23505 translation.

## Testing

**Backend (xUnit + Moq):**
- `CreateCoachRequestValidator`: role restricted to the two codes; each required field; national-id must be 14 digits; dob past; email/phone format.
- `CoachService.CreateAsync`: happy path (captain → CaptainProfile added; head_coach → HeadCoachProfile added; returns dto + generic password); username/email/national_id taken → null; unknown gender → error; save-conflict (`SaveChangesAsync → false`) → null.
- `CoachProfileRepository`: `NationalIdExistsAsync` true/false across both tables; `SaveChangesAsync` returns true on a normal in-memory save.
- `CoachesController.Create`: 201 on success, 409 on null.
- `SwimmerServiceTests` still green after the options rename.

**Frontend (Jest + TestBed):**
- `CreateCoachUseCase` (valid maps; malformed → validation).
- `coach.repository.impl.create` posts `/api/coaches`.
- `RegisterCoachViewModel`: loads genders; `canSubmit` gates on the required set incl. role + 14-digit national id; submit success sets `created` + notifies; 409 surfaces taken.
- `AccountCreationPage`: captain tab now renders the coach form (keep the spec minimal — assert the tab switches; don't force child DI unless stubbed).

## Files

**Backend — new:** `CoachDtos.cs`; `CreateCoachRequestValidator` (in the validators file); `ICoachService.cs`/`CoachService.cs`; `CoachMessages.cs`; `CoachesController.cs`; plus test files. **Backend — edit:** `SwimmerRegistrationOptions.cs` → `AccountCreationOptions.cs` (rename), `SwimmerService.cs` (options type), `appsettings*.json` (section rename), `ICoachProfileRepository.cs`/`CoachProfileRepository.cs` (NationalIdExistsAsync + `SaveChangesAsync`→bool), `IdentityModuleExtensions.cs` (register `ICoachService`, options), `SwimmerServiceTests.cs` (options rename).
**Frontend — new:** `features/coaches/*` (dto, model, repository port/impl, use-case, providers, index + specs); `register-coach-form.component.*`, `register-coach.viewmodel.ts` (+ specs). **Frontend — edit:** `account-creation.page.*` (captain tab → form), `app.routes.ts` (viewmodel provider), `captain-panel/index.ts` (export `RegisterCoachViewModel`), `app.config.ts` (`...COACH_PROVIDERS`), `en.json`/`ar.json` (captain i18n).

## Risks

- **Options rename touches completed Phase 2** — `SwimmerRegistrationOptions` → `AccountCreationOptions` is a mechanical, test-covered rename; the swimmer suite must stay green. If preferred, a separate `CoachRegistrationOptions` avoids the Phase-2 touch at the cost of a duplicate concept.
- **Role-claim authz** — `[Authorize(Roles="head_coach")]` relies on the role claim (verified working in Phase 2). Captains correctly cannot reach this endpoint (they create swimmers only).
- **`national_id` uniqueness across two tables** — the pre-check queries both; the two unique indexes (one per table) plus the 23505 translation backstop a race. A captain and a head coach cannot share a national id because the pre-check spans both, though the DB indexes are per-table (acceptable; the pre-check covers the cross-table case at create time).
- **No commit / dev-server lock** — same constraints as prior phases.
