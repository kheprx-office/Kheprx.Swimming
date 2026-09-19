# Swimmer Profile — Identity & Vitals Tab

**Date:** 2026-09-19
**Status:** Approved design (spec)
**Scope:** A new **swimmer profile page**, reached by a coach from the roster (`swimmers/:id`), with **only its first tab — Identity & Vitals — built**. Full-stack: a composite **read** endpoint, plus **functional edit** for both sections (identity → update `app_user`; vitals → record a new dated `medical_exam`), the new **`athlete.medical_exam`** table, and the new **`reference.fitness_assessment`** lookup. The **National ID** field is removed from the UI. Tabs 2–9 are deferred.

## 1. Summary

Clicking a swimmer in the roster (screenshot 1) opens their profile page (screenshot 2): an
identity strip, the full 9-tab strip for visual fidelity, and the **Identity & Vitals** tab —
the only enabled tab this pass. The tab shows two sections:

- **Identity** — Full Name, Date of Birth, Phone (from `app_user`). **National ID is removed
  from the UI** (and is not part of the swimmer schema, so there is nothing to strip
  server-side).
- **Vitals** — Blood Type, Height, Weight, Hemoglobin, Last Exam Date, and Internal
  Medicine / Heart / Spine results, sourced from the swimmer's **latest** `athlete.medical_exam`
  row (resolved through `reference.blood_type` + `reference.fitness_assessment`).

A Head Coach or Captain can **edit** either section. Editing **identity** updates the
`app_user` (name EN/AR, dob, phone). Editing **vitals** **records a new dated `medical_exam`**
(a POST, preserving history); the Vitals section then shows the new latest values.

Two backend tables are built from scratch — `athlete.medical_exam` and the
`reference.fitness_assessment` lookup — neither exists yet (only the schema diagram references
them). The read composes identity (`app_user` + `swimmer_profile` + `club` + `gender`) with the
latest exam; the write paths mirror `SwimmerService.CreateAsync` and the reference-lookup
pattern (`blood_type` in the Identity module).

**Design references:**
- UI prototype (visual reference only): `C:\Users\envnt\Desktop\4dba8937-ce3f-44bd-b093-515f10a5ea2b\` (`src/pages/SwimmerProfile.tsx`, `identity` tab)
- Approved screenshots: `C:\Users\envnt\Desktop\mcp\1.png` (roster entry point), `2.png` (Identity & Vitals tab, National ID struck out)
- DB tables: `docs/references/swimming-database-diagram.html` → `athlete.medical_exam`, `reference.fitness_assessment`, `identity.app_user`, `identity.swimmer_profile`

## 2. Decisions (locked)

1. **Real backend data.** A new composite read endpoint feeds the page; no static/mock data.
2. **Entry point = coach from the roster.** New route `swimmers/:id`; each roster row links to
   it. Any authenticated user may *view*; the *edit* buttons and write endpoints are gated to
   Head Coach + Captain.
3. **Functional edit, both sections.** Identity edit and Vitals edit both persist to the backend.
4. **Vitals edit = record a NEW dated exam (POST).** `medical_exam` is an append-only dated
   history; editing vitals inserts a new row and the section always shows the newest. Past exams
   are not edited or deleted this pass, and exam history is not displayed (latest only).
5. **Identity edit fields = Name (EN + AR), Date of Birth, Phone.** Reuses the existing
   `AppUser.UpdateProfile(...)`, preserving the user's email + gender unchanged. **National ID**
   is not in the swimmer schema and is not shown or sent.
6. **Both new tables live in the Identity module** (single `IdentityDbContext`, so the read is a
   one-context join and the FKs are real), but in **their diagram Postgres schemas**:
   `athlete.medical_exam` and `reference.fitness_assessment` — matching how
   `swimmer_specialization` (athlete schema) and `blood_type` (reference schema) are already
   configured.
7. **`fitness_assessment` seed set = Fit / Unfit / Under Review** (adjustable at implementation).
8. **Frontend = a dedicated `swimmer-profile` feature slice** (not nested under `swimmers`), so
   the page can grow to compose many domains as later tabs are added.
9. **Tab strip = all 9 tabs rendered, only Identity & Vitals enabled.** The rest are disabled
   placeholders (no routing) until each is green-lit.

## 3. Data model

### 3.1 `reference.fitness_assessment` (Identity module, `reference` schema)
Mirror `BloodType` exactly.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | ctor `Guid.NewGuid()` |
| `code` | varchar(20) | unique |
| `name_en` | varchar(100) | required |
| `name_ar` | varchar(100) | nullable |

Seeded rows (via `IdentitySeeder.EnsureFitnessAssessments`, idempotent like `EnsureBloodTypes`):

| code | name_en | name_ar |
|---|---|---|
| fit | Fit | لائق |
| unfit | Unfit | غير لائق |
| under_review | Under Review | قيد المراجعة |

### 3.2 `athlete.medical_exam` (Identity module, `athlete` schema)
New entity with **real FKs** (same `IdentityDbContext`).

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | ctor `Guid.NewGuid()` |
| `swimmer_id` | uuid | FK → `swimmer_profile.id` (Cascade) |
| `exam_date` | date | required (`DateOnly`) |
| `internal_med_id` | uuid | FK → `fitness_assessment.id` (Restrict) |
| `heart_assess_id` | uuid | FK → `fitness_assessment.id` (Restrict) |
| `spine_assess_id` | uuid | FK → `fitness_assessment.id` (Restrict) |
| `blood_type_id` | uuid? | nullable FK → `blood_type.id` (Restrict) |
| `hemoglobin` | numeric(4,1) | required |
| `height_cm` | numeric(5,1) | required |
| `weight_kg` | numeric(5,1) | required |
| `created_at` | timestamptz | ctor `DateTime.UtcNow` (tiebreaker for "latest") |

`MedicalExam` entity: `sealed`; private EF ctor; public
`MedicalExam(Guid swimmerId, DateOnly examDate, Guid internalMedId, Guid heartAssessId, Guid spineAssessId, Guid? bloodTypeId, decimal hemoglobin, decimal heightCm, decimal weightKg)`
setting `Id = Guid.NewGuid()`, `CreatedAt = DateTime.UtcNow`.

**Migration:** one `AddMedicalExamAndFitnessAssessment` migration (Identity.Infrastructure)
creating both tables in their schemas. `fitness_assessment` rows are seeded through the seeder
(not the migration), matching the `blood_type` precedent.

**Doc updates:** in `docs/references/swimming-database-diagram.html`, add
`"reference.fitness_assessment"` and `"athlete.medical_exam"` to the `IMPLEMENTED` set (green
dots). No schema-shape changes — the columns already match the diagram.

## 4. Backend — `fitness_assessment` reference lookup (Identity module)

Paths under `backend/src/Modules/Identity/`. Mirror the `BloodType` slice.

- **Domain:** `Entities/FitnessAssessment.cs` (Id, Code, NameEn, NameAr?; ctor trims + generates
  Id) — copy of `BloodType`. `Repositories/IFitnessAssessmentRepository.cs`:
  `Task<IReadOnlyList<FitnessAssessment>> GetAllAsync(CancellationToken)` and
  `Task<bool> ExistsAsync(Guid id, CancellationToken)`.
- **Infrastructure:** `Configurations/FitnessAssessmentConfiguration.cs`
  (`ToTable("fitness_assessment","reference")`, `Code` unique, lengths as §3.1);
  `Repositories/FitnessAssessmentRepository.cs`; `DbSet<FitnessAssessment>` on
  `IdentityDbContext`; `IdentitySeeder.EnsureFitnessAssessments` seeding the 3 rows; DI
  registration in `IdentityModuleExtensions`.
- **Application:** `ReferenceService.GetFitnessAssessmentsAsync` → `CodedLookupDto` list (reuse
  the existing `CodedLookupDto`); add to `IReferenceService`; add
  `ReferenceMessages.Success.FitnessAssessmentsListed`.
- **Api:** add `GET /api/reference/fitness-assessments` to the existing `ReferenceController`
  (`[Authorize]`), returning `ApiResponse<IReadOnlyList<CodedLookupDto>>`.

## 5. Backend — profile read (`GET /api/swimmers/{id}`)

### 5.1 Read models (Identity.Domain/ReadModels)
- `SwimmerProfileRow(Guid Id, string Uid, string NameEn, string? NameAr, DateOnly? Dob, string? GenderCode, string? Phone, string? TrainingClubNameEn, string? TrainingClubNameAr)`
- `MedicalExamRow(DateOnly ExamDate, decimal Hemoglobin, decimal HeightCm, decimal WeightKg, Guid? BloodTypeId, string? BloodTypeCode, string? BloodTypeNameEn, string? BloodTypeNameAr, Guid InternalMedId, string InternalMedCode, string InternalMedNameEn, string? InternalMedNameAr, Guid HeartAssessId, string HeartAssessCode, string HeartAssessNameEn, string? HeartAssessNameAr, Guid SpineAssessId, string SpineAssessCode, string SpineAssessNameEn, string? SpineAssessNameAr)`

### 5.2 Repository (`ISwimmerProfileRepository` + `SwimmerProfileRepository`)
- `Task<SwimmerProfileRow?> GetProfileByIdAsync(Guid id, CancellationToken)` — the roster join
  (`ListAsync`) narrowed to one id, plus `u.Phone` (left joins for club + gender), returns
  `null` when the id is unknown.
- `Task<MedicalExamRow?> GetLatestExamAsync(Guid swimmerId, CancellationToken)` — join
  `medical_exam` to `blood_type` (left) and `fitness_assessment` ×3 (inner),
  `ORDER BY exam_date DESC, created_at DESC`, `FirstOrDefault`.
- `Task<SwimmerProfile?> GetByIdTrackedAsync(Guid id, CancellationToken)` — tracked profile, used
  by the identity-update path to resolve `UserId`.
- `Task AddExamAsync(MedicalExam exam, CancellationToken)` — `_db.Set<MedicalExam>().AddAsync`.
  (Reuses the existing `SaveChangesAsync`.)

### 5.3 DTOs (`SwimmerDtos.cs` additions)
- `SwimmerProfileDto(SwimmerIdentityDto Identity, SwimmerVitalsDto? Vitals)`
- `SwimmerIdentityDto(Guid Id, string Uid, string NameEn, string? NameAr, DateOnly? Dob, int? Age, string GenderCode, string? Phone, string? TrainingClubNameEn, string? TrainingClubNameAr)`
- `SwimmerVitalsDto(DateOnly ExamDate, CodedLookupDto? BloodType, decimal Hemoglobin, decimal HeightCm, decimal WeightKg, CodedLookupDto InternalMed, CodedLookupDto HeartAssess, CodedLookupDto SpineAssess)`
  (reuse `CodedLookupDto(Guid Id, string Code, string NameEn, string? NameAr)` for the resolved
  refs.)

### 5.4 Service (`SwimmerService.GetProfileAsync`)
- `Task<SwimmerProfileDto?> GetProfileAsync(Guid id, CancellationToken)` — `GetProfileByIdAsync`;
  if `null` return `null` (→ controller 404). Map identity (reuse the existing `ComputeAge`),
  then `GetLatestExamAsync`; map to `SwimmerVitalsDto` or leave `Vitals = null`.

### 5.5 Controller
`SwimmersController` gains `GET("{id:guid}")` (`[Authorize]`) → `GetProfileAsync`; `404` with
`ApiResponse` failure when null; `200` with `ApiResponse<SwimmerProfileDto>` otherwise.
Message: `SwimmerMessages.Success.ProfileRetrieved` / `Errors.ProfileNotFound`.

## 6. Backend — identity update (`PUT /api/swimmers/{id}/identity`)

- **DTO:** `UpdateSwimmerIdentityRequest(string NameEn, string? NameAr, DateOnly Dob, string? Phone)`.
- **Validator:** `UpdateSwimmerIdentityRequestValidator` — `NameEn` not empty (max 200); `Dob`
  not in the future; `Phone` optional (max length).
- **Service:** `Task<bool> UpdateIdentityAsync(Guid id, UpdateSwimmerIdentityRequest req, CancellationToken)`
  — `GetByIdTrackedAsync(id)`; if null return `false` (→ 404). `_users.GetByIdAsync(profile.UserId)`;
  `user.UpdateProfile(req.NameEn, req.NameAr, user.Email, user.GenderId, req.Dob, req.Phone)`
  (email + gender preserved); `_users.SaveChangesAsync`; return `true`.
- **Controller:** `PUT("{id:guid}/identity")` (`[Authorize(Roles="head_coach,captain")]`) → `200`
  (`ApiResponse` success, `SwimmerMessages.Success.IdentityUpdated`) or `404`.

## 7. Backend — vitals write (`POST /api/swimmers/{id}/medical-exams`)

- **DTO:** `CreateMedicalExamRequest(DateOnly ExamDate, Guid? BloodTypeId, decimal Hemoglobin, decimal HeightCm, decimal WeightKg, Guid InternalMedId, Guid HeartAssessId, Guid SpineAssessId)`.
- **Validator:** `CreateMedicalExamRequestValidator` — `InternalMedId`/`HeartAssessId`/`SpineAssessId`
  not empty; `Hemoglobin`/`HeightCm`/`WeightKg` > 0 and within sane numeric bounds; `ExamDate`
  not in the future.
- **Service:** `Task<SwimmerVitalsDto?> CreateExamAsync(Guid id, CreateMedicalExamRequest req, CancellationToken)`
  — verify the swimmer exists (`GetByIdTrackedAsync`; null → `null` → 404); an
  `EnsureReferencesExist`-style guard (the three `fitness_assessment` ids exist via
  `IFitnessAssessmentRepository.ExistsAsync`; `BloodTypeId`, if provided, exists via
  `IBloodTypeRepository.ExistsAsync` — add it); construct `MedicalExam`; `AddExamAsync`;
  `SaveChangesAsync`; return the mapped `SwimmerVitalsDto` (the new latest).
- **Repos:** add `ExistsAsync(Guid, CancellationToken)` to `IBloodTypeRepository` +
  `BloodTypeRepository`.
- **Controller:** `POST("{id:guid}/medical-exams")` (`[Authorize(Roles="head_coach,captain")]`) →
  `201` with `ApiResponse<SwimmerVitalsDto>` (`SwimmerMessages.Success.ExamRecorded`); `404` when
  the swimmer is unknown; unknown reference ids surface as a `400` (validation/`InvalidUserException`
  path, consistent with `CreateAsync`).

## 8. Frontend (Angular)

### 8.1 New feature slice `features/swimmer-profile/`
Mirror the layering of existing slices (`swimmers`, `health-readings`).

- **domain/model/swimmer-profile.ts:**
  - `SwimmerIdentity { id; uid; nameEn; nameAr; dob; age; genderCode; phone; trainingClubNameEn; trainingClubNameAr }`
  - `SwimmerVitals { examDate; bloodType: LookupItem | null; hemoglobin; heightCm; weightKg; internalMed: LookupItem; heartAssess: LookupItem; spineAssess: LookupItem }`
  - `SwimmerProfile { identity: SwimmerIdentity; vitals: SwimmerVitals | null }`
- **domain/repositories/swimmer-profile.repository.ts** — interface + DI token:
  `getProfile(id)`, `updateIdentity(id, body)`, `createExam(id, body)`.
- **domain/usecases/** — `get-swimmer-profile`, `update-swimmer-identity`, `create-medical-exam`
  (each a thin `run(...)` returning the `Result` envelope used across the app).
- **data/dto/** — `swimmer-profile.dto.ts` (`…DtoRs`), `update-identity.dto.ts` (`…DtoRq`),
  `create-medical-exam.dto.ts` (`…DtoRq` / `…DtoRs`), each with a validation guard like the
  existing DTOs.
- **data/repositories/swimmer-profile.repository.impl.ts** — `GET /api/swimmers/{id}`,
  `PUT /api/swimmers/{id}/identity`, `POST /api/swimmers/{id}/medical-exams` via `HttpClientService`.
- **data/swimmer-profile.providers.ts**; **index.ts** exports (page + viewmodel + providers).

### 8.2 `features/reference/` additions
Add `getFitnessAssessments()` to `IReferenceRepository` + impl
(`GET /api/reference/fitness-assessments`) and a `LoadFitnessAssessmentsUseCase`, reusing
`CodedLookupListDtoRs` / `isCodedLookupListValid` / `LookupItem` (exactly like
`load-blood-types.use-case.ts`). The blood-type dropdown reuses the existing
`LoadBloodTypesUseCase`.

### 8.3 Page `presentation/pages/swimmer-profile/`
- **swimmer-profile.page.ts / .html** — identity strip + tab strip + Identity & Vitals tab.
  - *Identity strip:* initials avatar, `nameEn`/`nameAr` by language, `uid · gender · age`,
    training-club badge. H1 is the swimmer's name; breadcrumb `Swimmers / <name>`.
  - *Tab strip:* the 9 tabs from the design; only `identityVitals` is enabled (selected), the
    rest are disabled placeholders (`aria-disabled`, no navigation).
  - *Identity section:* Full Name, Date of Birth (formatted), Phone. **No National ID.** An Edit
    button (shown only to Head Coach / Captain) toggles an inline form — `TextFieldComponent`
    for name EN, name AR, phone; a date input for DOB — with Save / Cancel.
  - *Vitals section:* when `vitals` is null, an empty state (“No exam on record yet”); otherwise
    Blood Type, Height, Weight, Hemoglobin, Last Exam Date, Internal Medicine, Heart, Spine. An
    Edit button (role-gated) toggles a **record-new-exam** form — `SelectFieldComponent` for
    Blood Type (optional) + the three fitness assessments (required), number inputs for
    hemoglobin/height/weight, a date input for exam date (defaults to today) — with Save (POST)
    / Cancel. On success the section re-reads and shows the new latest values.
- **swimmer-profile.viewmodel.ts** — signal-based, like `health-monitoring.viewmodel`:
  - state: `profile`, `loading`, `error`, `notFound`; lookup signals `bloodTypes`,
    `fitnessAssessments`; edit signals `editingIdentity`, `editingVitals`, the draft fields, and
    `submitting`.
  - `canEdit` computed from the current user's role (Head Coach / Captain) via the existing
    session/role source used by `roleGuard`.
  - `load(id)` fetches the profile (404 → `notFound`); lookups load on first entry into an edit
    form. `canSaveIdentity` / `canSaveVitals` gate the Save buttons; `saveIdentity()` /
    `saveVitals()` call the use-cases, toast success/failure via `NotificationService` + i18n,
    close the form, and re-read on success.

### 8.4 Routing + roster wiring
- **app.routes.ts:** add
  `{ path: 'swimmers/:id', canActivate: [firstLoginGuard], loadComponent: () => import('@features/swimmer-profile').then(m => m.SwimmerProfilePage), providers: [SwimmerProfileViewModel] }`
  (import `SwimmerProfileViewModel` from `@features/swimmer-profile`). Place it **after** the
  `swimmers` route. Use-cases + repository are `providedIn: 'root'`.
- **swimmers.page.html / .ts:** wrap each roster `<li>` in a `routerLink` to
  `/swimmers/{{ s.id }}` (add `RouterLink` to the component imports), with hover/cursor
  affordance; keep the existing row layout.

### 8.5 i18n
Add a `swimmerProfile.*` block to `en.json` + `ar.json`: page/breadcrumb, the 9 tab labels,
section titles (Identity, Vitals), field labels (Full Name, Date of Birth, Phone, Blood Type,
Height, Weight, Hemoglobin, Last Examination Date, Internal Medicine Result, Heart Assessment,
Spine Assessment), edit-form labels/placeholders, empty-state text, buttons (Edit, Save, Cancel,
Record Exam), and toasts (identity updated, exam recorded, load failed, save failed). Blood-type
and fitness-assessment option labels come from the reference data (`nameEn`/`nameAr`).

## 9. Data flow

**Read:** open `swimmers/:id` → viewmodel `load(id)` → `GetSwimmerProfileUseCase`
(`GET /api/swimmers/{id}`) → render identity + latest vitals (or the empty vitals state).

**Edit identity:** Edit → inline form pre-filled → Save → `UpdateSwimmerIdentityUseCase`
(`PUT …/identity` `{ nameEn, nameAr, dob, phone }`) → server `UpdateProfile` + save → `200` →
toast + re-read.

**Edit vitals:** Edit → form (lookups loaded) → Save → `CreateMedicalExamUseCase`
(`POST …/medical-exams` `{ examDate, bloodTypeId?, hemoglobin, heightCm, weightKg, internalMedId, heartAssessId, spineAssessId }`)
→ server validates refs + inserts a new exam → `201 + SwimmerVitalsDto` → toast + re-read (new
latest shows).

## 10. Authorization

- `GET /api/swimmers/{id}` and `GET /api/reference/fitness-assessments` → `[Authorize]` (any
  authenticated user).
- `PUT …/identity` and `POST …/medical-exams` → `[Authorize(Roles="head_coach,captain")]`.
- Frontend hides both Edit buttons for other roles; the API enforces regardless.

## 11. Error handling / edge cases

- Unknown swimmer id → API `404`; page shows a not-found state.
- No `medical_exam` on record → Vitals empty state; the Edit-Vitals form still records the first
  exam.
- Nullable blood type handled (optional dropdown; `null` round-trips).
- Validation failures → `400` via the existing validation filter + `ApiResponse` failure shape;
  the form stays intact and shows an error toast; Save is disabled while a request is in flight.
- Page `loading` / `error` signals mirror the roster page.

## 12. Testing

**Backend**
- `Identity.UnitTests`: `MedicalExam` entity ctor (stamps `Id` + `CreatedAt`);
  `FitnessAssessment` entity; `FitnessAssessmentRepository.GetAllAsync` + `ExistsAsync`
  (InMemory); `SwimmerProfileRepository.GetProfileByIdAsync` and `GetLatestExamAsync` (InMemory —
  including the null-exam case and latest-selection ordering by `exam_date`/`created_at`);
  `SwimmerService.GetProfileAsync` mapping (with and without vitals), `UpdateIdentityAsync`
  (updates the user, preserves email + gender, not-found → false), `CreateExamAsync` (persists +
  returns the new vitals; unknown `fitness_assessment`/`blood_type` id rejected);
  `ReferenceService.GetFitnessAssessmentsAsync` mapping.
- `Api.UnitTests`: `SwimmersController` `GET {id}` 200/404, `PUT {id}/identity` 200/404, `POST
  {id}/medical-exams` 201/404 (extend `SwimmersControllerTests`); `ReferenceController`
  fitness-assessments 200 (extend `ReferenceControllerTests`). These actions do not use
  `CurrentUserId()`, so no extra HttpContext plumbing is needed.
- `ArchitectureTests` stay green.

**Frontend**
- `get-swimmer-profile.use-case.spec.ts`, `update-swimmer-identity.use-case.spec.ts`,
  `create-medical-exam.use-case.spec.ts`, `swimmer-profile.repository.impl.spec.ts`.
- `load-fitness-assessments.use-case.spec.ts`.
- `swimmer-profile.viewmodel.spec.ts` — load populates identity + vitals and the null-vitals
  empty state; `canEdit` role gating; `saveIdentity` success (toast + re-read) and failure (error
  toast); `saveVitals` success (new latest shown) and failure.

## 13. Out of scope (deferred)

- Tabs 2–9 (Guardian, Physiological, InBody, Records, Health Monitoring, Attendance,
  Championships, Feedback).
- Editing or deleting past exams; displaying exam history (latest exam only).
- Any National ID field, anywhere in this page.
- Managing the `fitness_assessment` catalog from the UI (seed-only, like the other lookups).
