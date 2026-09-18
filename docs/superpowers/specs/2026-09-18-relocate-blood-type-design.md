# Relocate blood_type from swimmer_profile to medical_exam

**Date:** 2026-09-18
**Status:** Approved design (spec)
**Scope:** Remove the implemented `blood_type_id` from `identity.swimmer_profile` (backend + Register Swimmer form), and relocate it to `athlete.medical_exam` in the schema docs (docs-only — `medical_exam` is unimplemented).

## 1. Summary

Blood type is captured during the medical exam, so it should live on `medical_exam`, not
on `swimmer_profile`. Unlike the earlier enum→lookup doc refactors, `swimmer_profile.blood_type_id`
is **fully implemented and in use** (entity, EF config, migration, `CreateSwimmerRequest`,
`SwimmerService` validation, seeded demo swimmers, and a **required Blood Type dropdown** in the
Register Swimmer form). So this is a breaking refactor of a committed feature, not a doc edit.

The `blood_type` **reference lookup itself stays** (table, seed, `GET /api/reference/blood-types`,
`LoadBloodTypesUseCase`) — it is exactly what the future `medical_exam` will consume. Only the
`swimmer_profile` coupling is removed.

**Accepted consequence:** after this change the app captures blood type **nowhere** until
`medical_exam` is implemented (Register Swimmer drops the field; `medical_exam` doesn't exist yet).
This gap is inherent to the move and was explicitly accepted.

**Design references:**
- DB tables: `docs/references/swimming-database-diagram.html` → `identity.swimmer_profile`, `athlete.medical_exam`
- Related committed feature: Account Creation / Register Swimmer ([[account-creation-feature]])

## 2. Decisions (locked)

1. **Full backend refactor** (not docs-only) — remove `blood_type_id` from the implemented
   `swimmer_profile` everywhere it is used, with a DROP-column migration.
2. **Keep the blood_type reference lookup + endpoint + `LoadBloodTypesUseCase`.** They become
   unused by any live consumer after this change, but are retained for the future `medical_exam`
   consumer (removing then re-adding is churn). A deliberate YAGNI exception — call it out in code
   review so it is not flagged as dead code. The `reference.blood_type` table and its seed are
   untouched.
3. **DROP the column** (no data migration). `swimmer_profile.blood_type_id` is nullable and holds
   only re-seeded demo data — nothing to preserve.
4. **medical_exam side is docs-only.** `medical_exam` is unimplemented; adding `blood_type_id`
   there is a diagram + schema-spec change, no code.
5. **Register Swimmer stops collecting blood type.** The required dropdown is removed; the
   `POST /api/swimmers` contract drops `bloodTypeId`.

## 3. Backend changes (Identity module)

Paths under `backend/src/Modules/Identity/`.

- **Domain:** `Entities/SwimmerProfile.cs` — remove the `BloodTypeId` property and the
  `bloodTypeId` constructor parameter/assignment.
- **Infrastructure:**
  - `Configurations/SwimmerProfileConfiguration.cs` — remove the `BloodTypeId` property/FK mapping.
  - `Data/IdentityDbContext.cs` — no change expected (its `DbSet<BloodType> BloodTypes` is the
    lookup, which stays). Verify no `SwimmerProfile.BloodTypeId` reference remains.
  - `Data/IdentitySeeder.cs` — `EnsureSwimmers` seeds swimmers without a blood type; the
    `EnsureBloodTypes` lookup seed is untouched.
  - **New migration** `DropSwimmerBloodType` — drops `blood_type_id` from `identity.swimmer_profile`
    (generated via EF tools; verify `Up()` drops the column + its FK/index and `Down()` re-adds it).
- **Application:**
  - `DTOs/SwimmerDtos.cs` — remove `Guid BloodTypeId` from `CreateSwimmerRequest`.
  - `Validators/UserRequestValidators.cs` — remove the `BloodTypeId` NotEmpty rule from the
    create-swimmer validator.
  - `Services/SwimmerService.cs` — remove the blood-type existence check and stop passing
    `bloodTypeId` to the `SwimmerProfile` ctor. Drop the `IBloodTypeRepository` dependency **if it
    is used only for this**; keep it if the service uses it elsewhere (verify at implementation).
  - `Resources/SwimmerMessages.cs` — remove `BloodTypeRequired`.

**Untouched (reference lookup — keep):** `BloodType` entity/config/repository,
`IReferenceService.GetBloodTypesAsync` / `ReferenceService` / `ReferenceMessages.BloodTypesListed`,
`ReferenceController` `blood-types` endpoint, `IdentityModuleExtensions` registration of
`IBloodTypeRepository`.

## 4. Frontend changes (Register Swimmer)

Paths under `frontend/src/app/`.

- `features/captain-panel/presentation/pages/account-creation/register-swimmer.viewmodel.ts` —
  remove the `LoadBloodTypesUseCase` injection, the `bloodTypes` and `bloodTypeId` signals, the
  `bloodTypeId` clause in `canSubmit`, its load in `loadLookups`, its field in the `submit`
  payload, and its entry in `reset`.
- `features/captain-panel/presentation/pages/account-creation/register-swimmer-form.component.html`
  — remove the Blood Type `<app-select-field>`.
- `features/swimmers/data/dto/create-swimmer.dto.ts` — remove `bloodTypeId` from the request DTO.
- `core/i18n/en.json` + `ar.json` — remove `accountCreation.fields.bloodType`.

**Untouched (keep):** `features/reference` `LoadBloodTypesUseCase`, `getBloodTypes` repo method,
and the reference DTO — retained for the future `medical_exam` consumer (Decision 2).

## 5. Docs relocation → medical_exam

- **`docs/references/swimming-database-diagram.html`:**
  - Remove `{c:"blood_type_id",...}` from `swimmer_profile`.
  - Add `{c:"blood_type_id",t:"uuid",k:"FK",fk:"reference.blood_type.id",n:true}` to `medical_exam`.
  - Re-point the relationship: `reference.blood_type → identity.swimmer_profile` becomes
    `reference.blood_type → athlete.medical_exam` (label e.g. "typed in").
  - `IMPLEMENTED` set unchanged — `swimmer_profile` stays implemented (still a table, minus a
    column); `medical_exam` stays planned.
- **`docs/superpowers/specs/2026-08-30-swimming-database-design.md`:**
  - §5 `swimmer_profile` — remove the `blood_type_id` row.
  - §6 `medical_exam` — add a `blood_type_id | uuid | FK → reference.blood_type (nullable)` row.
  - §11 relationships — change `blood_type 1—* swimmer_profile` → `blood_type 1—* medical_exam`.

## 6. Data flow (Register Swimmer, after change)

`POST /api/swimmers` body no longer includes `bloodTypeId`. The form loads clubs, genders, and
strokes (no blood types); `canSubmit` no longer gates on blood type; the created `SwimmerProfile`
has no blood-type column. Everything else in the registration flow is unchanged.

## 7. Testing

**Backend (`Identity.UnitTests`)**
- `Entities/SwimmerProfileTests.cs` — drop blood-type assertions / ctor args.
- `Services/SwimmerServiceTests.cs` — remove blood-type validation cases; `CreateSwimmerRequest`
  fixtures drop `BloodTypeId`.
- `Validators/CreateSwimmerRequestValidatorTests.cs` — remove the blood-type-required case; update
  the valid fixture.
- `Data/IdentitySeederTests.cs` — update any assertion about seeded swimmer blood type.
- Reference lookup tests (`ReferenceServiceTests`, `ReferenceRepositoryTests`, `ReferenceEntityTests`)
  are **unchanged** (the lookup stays).
- `Api.UnitTests` `SwimmersControllerTests` — update the `CreateSwimmerRequest` fixture (drops the
  `BloodTypeId` arg).

**Frontend**
- `register-swimmer.viewmodel.spec.ts` — remove blood-type expectations; `canSubmit` no longer
  needs it.
- `features/swimmers/testing/.../create-swimmer.use-case.spec.ts` and
  `.../swimmer.repository.impl.spec.ts` — drop `bloodTypeId` from request fixtures.
- Whole Identity + Api backend suites, `ArchitectureTests`, and the frontend `npm run build` +
  affected jest suites must stay green.

## 8. Out of scope

- Implementing `medical_exam` (still deferred; the relocation is docs-only for that side).
- Removing the `blood_type` reference lookup / endpoint / `LoadBloodTypesUseCase` (kept per Decision 2).
- Any data-preservation/backfill of existing blood-type values (demo data, re-seeded).
