# Swimmer Profile — Exam History, Edit & Delete

**Date:** 2026-09-19
**Status:** Approved design (spec)
**Scope:** Extend the **Identity & Vitals** tab's Vitals section so a coach can (1) pick any of the swimmer's medical exams from a **date dropdown** and view it, (2) **edit** a specific exam (all fields, including the exam date) to correct wrong data, and (3) **delete** an exam entered by mistake. Adds three backend endpoints (list / update / delete exams) and reworks the Vitals UI from "latest only + record new" to "history dropdown + view/edit/delete any + record new".

## 1. Summary

The Vitals section today shows only the **latest** `athlete.medical_exam` and supports adding a new exam (`POST …/medical-exams`). This feature — the deferred scope of the parent design (`2026-09-19-swimmer-profile-identity-vitals-design.md`, §13) — adds:

- **List all exams:** `GET /api/swimmers/{id}/medical-exams` returns every exam for the swimmer (each with its id + resolved vitals), newest first. The Vitals section is driven by this list; a date `<select>` picks which exam is displayed (default = latest).
- **Edit a specific exam:** `PUT /api/swimmers/{id}/medical-exams/{examId}` overwrites **all** fields of that exam (date, blood type, hemoglobin, height, weight, the three assessments). Reuses the existing `CreateMedicalExamRequest` payload + validator.
- **Delete a specific exam:** `DELETE /api/swimmers/{id}/medical-exams/{examId}`.

The enabling change is exposing the exam **id** end-to-end: it is added to `MedicalExamRow` and `SwimmerVitalsDto` so one exam type flows through the profile-latest read, the list, and the create/update responses.

**Design references:**
- Parent feature (built, uncommitted): `docs/superpowers/specs/2026-09-19-swimmer-profile-identity-vitals-design.md` and its plan `docs/superpowers/plans/2026-09-19-swimmer-profile-identity-vitals.md`.
- Approved screenshot: the running Vitals section with the "Record exam" action (this feature adds the date dropdown + Edit + Delete beside it).
- DB table: `docs/references/swimming-database-diagram.html` → `athlete.medical_exam` (already implemented).

## 2. Decisions (locked)

1. **Approach A — dedicated exam-list endpoint.** `GET …/medical-exams`; the profile read (`GET /api/swimmers/{id}`) is unchanged and still returns identity + latest vitals. The Vitals section is driven by the list.
2. **Edit = all fields, including exam date.** Changing a date can change which exam is "latest"; the UI reselects the edited exam by id after reload.
3. **Delete allowed** for a fully-mistaken exam, behind an **inline** confirm (no new modal component, no browser `confirm()`).
4. **Reuse `CreateMedicalExamRequest` + `CreateMedicalExamRequestValidator`** as the PUT (update) body — same fields, same rules.
5. **Expose the exam id** by adding `Guid Id` (first field) to `MedicalExamRow` and `SwimmerVitalsDto`, and `id: string` to the frontend `SwimmerVitals`. One exam type everywhere.
6. **Ownership guard:** `PUT`/`DELETE` require the target exam's `SwimmerId` to equal the route `{id}`; otherwise 404. Prevents editing another swimmer's exam via a mismatched URL.
7. **Auth:** list = `[Authorize]`; update + delete = `[Authorize(Roles="head_coach,captain")]`. UI Record/Edit/Delete gated on `canEdit()`.
8. **List-driven Vitals section;** `profile.vitals` (still returned) is only a first-paint fallback until the list loads.

## 3. Backend (Identity module)

Paths under `backend/src/Modules/Identity/` and `backend/Kheprx.BaseBackend.Api/`.

### 3.1 Domain
- **`MedicalExam` entity** — add a mutator:
  ```csharp
  public void Update(DateOnly examDate, Guid internalMedId, Guid heartAssessId, Guid spineAssessId,
      Guid? bloodTypeId, decimal hemoglobin, decimal heightCm, decimal weightKg)
  {
      ExamDate = examDate; InternalMedId = internalMedId; HeartAssessId = heartAssessId;
      SpineAssessId = spineAssessId; BloodTypeId = bloodTypeId;
      Hemoglobin = hemoglobin; HeightCm = heightCm; WeightKg = weightKg;
  }
  ```
  `Id`, `SwimmerId`, `CreatedAt` are not touched.
- **`MedicalExamRow` read model** — add `Guid Id` as the **first** positional field. The `ExamRows()` join helper already carries `ExamId`; `Project()` maps `x.ExamId` into the new `Id`.
- **`ISwimmerProfileRepository`** — add:
  - `Task<IReadOnlyList<MedicalExamRow>> ListExamsAsync(Guid swimmerId, CancellationToken ct = default)`
  - `Task<MedicalExam?> GetExamTrackedAsync(Guid examId, CancellationToken ct = default)`
  - `void RemoveExam(MedicalExam exam)`

### 3.2 Infrastructure (`SwimmerProfileRepository`)
- `ListExamsAsync` → `ExamRows().Where(x => x.SwimmerId == swimmerId).OrderByDescending(x => x.ExamDate).ThenByDescending(x => x.CreatedAt).Select(Project()).ToListAsync(ct)`.
- `GetExamTrackedAsync` → `_db.MedicalExams.FirstOrDefaultAsync(e => e.Id == examId, ct)` (tracked — no `AsNoTracking`).
- `RemoveExam` → `_db.MedicalExams.Remove(exam)`.

### 3.3 Application
- **`SwimmerDtos.cs`** — `SwimmerVitalsDto` gains `Guid Id` as the first field:
  ```csharp
  public sealed record SwimmerVitalsDto(
      Guid Id, DateOnly ExamDate, CodedLookupDto? BloodType, decimal Hemoglobin, decimal HeightCm, decimal WeightKg,
      CodedLookupDto InternalMed, CodedLookupDto HeartAssess, CodedLookupDto SpineAssess);
  ```
  `MapVitals` sets `Id = e.Id`. (`CreateMedicalExamRequest` is unchanged and reused for update.)
- **`ISwimmerService` / `SwimmerService`** — add:
  - `Task<IReadOnlyList<SwimmerVitalsDto>?> ListExamsAsync(Guid swimmerId, CancellationToken ct = default)` — `GetByIdTrackedAsync(swimmerId)` null → return `null` (→404); else `ListExamsAsync` rows → `MapVitals` each (never null since a row always has the fields).
  - `Task<SwimmerVitalsDto?> UpdateExamAsync(Guid swimmerId, Guid examId, CreateMedicalExamRequest request, CancellationToken ct = default)` — `GetExamTrackedAsync(examId)`; if `null` **or** `exam.SwimmerId != swimmerId` → return `null` (→404); `EnsureExamReferencesExist(request, ct)` (existing); `exam.Update(...)`; `SaveChangesAsync`; return `MapVitals(await GetExamRowByIdAsync(examId, ct))`.
  - `Task<bool> DeleteExamAsync(Guid swimmerId, Guid examId, CancellationToken ct = default)` — `GetExamTrackedAsync(examId)`; if `null` or wrong swimmer → `false`; `RemoveExam(exam)`; `SaveChangesAsync`; `true`.
- **`SwimmerMessages`** — add `Success.{ExamsListed, ExamUpdated, ExamDeleted}` and `Errors.ExamNotFound` (bilingual, matching the existing style). The list endpoint's unknown-swimmer 404 reuses the existing `Errors.ProfileNotFound`; `ExamNotFound` is for the update/delete when the exam is missing or not owned.

### 3.4 Api (`SwimmersController`)
- `GET("{id:guid}/medical-exams")` `[Authorize]` → `ListExamsAsync`; 404 (`Errors.ProfileNotFound` — unknown swimmer) when null; else 200 `ApiResponse<IReadOnlyList<SwimmerVitalsDto>>` (`ExamsListed`).
- `PUT("{id:guid}/medical-exams/{examId:guid}")` `[Authorize(Roles="head_coach,captain")]` → `UpdateExamAsync`; 404 when null; else 200 `ApiResponse<SwimmerVitalsDto>` (`ExamUpdated`). Body validated by the existing `CreateMedicalExamRequestValidator`.
- `DELETE("{id:guid}/medical-exams/{examId:guid}")` `[Authorize(Roles="head_coach,captain")]` → `DeleteExamAsync`; 404 when false; else 200 `ApiResponse<object>` (`ExamDeleted`, data `null`).

### 3.5 Test ripple (existing tests to update)
Adding `Id` to `SwimmerVitalsDto` + `MedicalExamRow` changes their constructors. Update the positional constructions in:
- `SwimmerServiceTests` — `GetProfile_maps_latest_vitals_when_present` (`MedicalExamRow` gains a leading `Guid.NewGuid()`), `CreateExam_persists_and_returns_vitals` (`MedicalExamRow`), and any `SwimmerVitalsDto` built directly.
- `SwimmersControllerTests` — `CreateExam_returns_201_with_vitals` (`SwimmerVitalsDto` gains a leading `Guid.NewGuid()`).
- `SwimmerProfileRepositoryTests` — assertions that read `MedicalExamRow` fields still hold; add an assertion that `Id` is populated.

## 4. Frontend (`swimmer-profile` slice)

Paths under `frontend/src/app/features/swimmer-profile/`.

### 4.1 Model + DTOs
- `domain/model/swimmer-profile.ts` — `SwimmerVitals` gains `id: string` (first field).
- `data/dto/swimmer-profile.dto.ts` — `SwimmerVitalsDtoRs` gains `id: string`; add `MedicalExamListDtoRs extends BaseResponseRs<SwimmerVitalsDtoRs[]>` and a guard `isVitalsListValid` (array of items each with a string `id`).
- Reuse `CreateMedicalExamDtoRq` for the update body and `CreatedVitalsItemDtoRs` for the update response; add `DeleteExamItemDtoRs extends BaseResponseRs<unknown>`.

### 4.2 Repository + use-cases
- `domain/repositories/swimmer-profile.repository.ts` + impl — add:
  - `listExams(id): Promise<MedicalExamListDtoRs>` → `GET /api/swimmers/${id}/medical-exams`
  - `updateExam(id, examId, rq): Promise<CreatedVitalsItemDtoRs>` → `PUT /api/swimmers/${id}/medical-exams/${examId}` `{ body: rq }`
  - `deleteExam(id, examId): Promise<DeleteExamItemDtoRs>` → `DELETE /api/swimmers/${id}/medical-exams/${examId}`
- `domain/usecases/`:
  - `ListMedicalExamsUseCase extends UseCase<string, SwimmerVitals[]>` — validates + maps each row to `SwimmerVitals` (reuse the `toRef`/vitals mapping already used by `GetSwimmerProfileUseCase` / `CreateMedicalExamUseCase`; extract a shared `toVitals(dto)` helper in the model or a `data/dto` mapper to avoid triplication).
  - `UpdateMedicalExamUseCase extends UseCase<{id; examId; rq}, SwimmerVitals>` — maps the updated vitals.
  - `DeleteMedicalExamUseCase extends UseCase<{id; examId}, void>` — pass-through.

### 4.3 Viewmodel (`swimmer-profile.viewmodel.ts`)
Add:
- `exams = signal<SwimmerVitals[]>([])`, `selectedExamId = signal('')`, `loadingExams = signal(false)`.
- `selectedExam = computed(() => this.exams().find(e => e.id === this.selectedExamId()) ?? this.exams()[0] ?? null)`.
- `editingExamId = signal<string | null>(null)`; `confirmingDelete = signal(false)`; `deleting = signal(false)`.
- `load(id)` — after the profile loads, call `loadExams()`.
- `loadExams()` — `ListMedicalExamsUseCase.run(swimmerId)` → set `exams`; set `selectedExamId` to `exams()[0]?.id ?? ''`.
- `startNewExam()` — `editingExamId=null`; clear the `v*` fields (date defaults to today); `ensureLookups()`; `editingVitals=true`.
- `startEditExam()` — `const v = selectedExam(); if(!v) return`; `editingExamId=v.id`; prefill the `v*` fields from `v`; `ensureLookups()`; `editingVitals=true`.
- `saveVitals()` — build the request from the `v*` fields; if `editingExamId()` → `UpdateMedicalExamUseCase.run({id, examId: editingExamId(), rq})` (toast `examUpdated`); else → `CreateMedicalExamUseCase.run` (toast `examRecorded`). On success: close form, `loadExams()`, and set `selectedExamId` to the saved/created exam's id (from the use-case result). On failure: toast `saveFailed`.
- `askDeleteExam()` → `confirmingDelete=true`; `cancelDelete()` → `false`; `confirmDeleteExam()` — `DeleteMedicalExamUseCase.run({id, examId: selectedExam()!.id})` (toast `examDeleted`) → `loadExams()` (selection falls to the new latest), `confirmingDelete=false`. On failure: toast `deleteFailed`.
- `canSaveVitals` unchanged.

`startEditVitals()` (the current single method) is replaced by `startNewExam()` / `startEditExam()`.

### 4.4 Page (`swimmer-profile.page.html`) — Vitals section
- **Header:** title `Vitals` + an exam-date `<select>` (bound to `selectedExamId`, options newest-first, the first labeled `(latest)`), shown only when `exams().length > 0`. Role-gated actions: **Record exam** (`startNewExam`), and when `selectedExam()` exists — **Edit** (`startEditExam`) and **Delete** (`askDeleteExam`).
- **Delete inline confirm:** when `confirmingDelete()`, replace the actions with `confirmDelete` text + **Yes** (`confirmDeleteExam`, disabled while `deleting()`) / **No** (`cancelDelete`).
- **Read view:** renders `selectedExam()` (was `p.vitals`); empty state (`states.noExam`) when `exams().length === 0`.
- **Edit form:** the existing shared form; heading reflects add vs. edit (`editExam` when `editingExamId()`); `Save`/`Cancel` as today, submit calls `saveVitals()`.

### 4.5 i18n (en + ar)
Add under `swimmerProfile`: `selectExam`, `latest`, `editExam`, `delete`, `confirmDelete`, `confirmYes`, `confirmNo`, and toasts `examUpdated`, `examDeleted`, `deleteFailed`.

## 5. Data flow

- **View history:** open the tab → `load()` (profile) → `loadExams()` (`GET …/medical-exams`) → dropdown lists all exams, latest selected → read view shows `selectedExam()`. Changing the dropdown re-renders the selected exam (no fetch).
- **Edit:** Edit → form prefilled from `selectedExam` → Save → `PUT …/{examId}` → 200 vitals → toast + `loadExams()` + reselect the edited exam by id.
- **Record:** Record exam → blank form (date = today) → Save → `POST …/medical-exams` → toast + `loadExams()` + select the new exam.
- **Delete:** Delete → inline confirm → Yes → `DELETE …/{examId}` → toast + `loadExams()` → selection falls to the new latest (or empty state).

## 6. Authorization

- `GET …/medical-exams` → `[Authorize]`.
- `PUT`/`DELETE …/medical-exams/{examId}` → `[Authorize(Roles="head_coach,captain")]`.
- UI: dropdown visible to all authenticated users; Record/Edit/Delete gated on `canEdit()`; the API enforces regardless.

## 7. Error handling / edge cases

- Unknown swimmer on list → 404 (page keeps the existing error/empty handling).
- `PUT`/`DELETE` with an `examId` not owned by `{id}` → 404 (ownership guard).
- No exams → empty state, no dropdown, Record still works.
- Editing a date so the exam is no longer newest → list reorders on reload; selection stays on the edited exam by id.
- Deleting the selected/last exam → selection falls to the new latest, or the empty state when none remain.
- Validation failures on update → 400 via the existing validation filter; the form stays intact with an error toast; Save/Delete disabled while in flight.

## 8. Testing

**Backend**
- `Identity.UnitTests`: `MedicalExam.Update` (mutates the eight fields, leaves Id/SwimmerId/CreatedAt); `SwimmerProfileRepository` `ListExamsAsync` (ordering by date then created_at, ids + resolved refs), `GetExamTrackedAsync`, `RemoveExam` (InMemory); `SwimmerService` `ListExamsAsync` (unknown swimmer → null; maps list), `UpdateExamAsync` (updates + returns vitals; **404 when the exam belongs to another swimmer**; unknown reference rejected), `DeleteExamAsync` (removes; 404 when missing/not owned). Plus the mechanical `Id`-argument updates to existing `SwimmerVitalsDto`/`MedicalExamRow` constructions.
- `Api.UnitTests`: `SwimmersController` `GET medical-exams` 200/404, `PUT …/{examId}` 200/404, `DELETE …/{examId}` 200/404; update the existing `CreateExam` test for the new `Id` on the returned vitals.
- `ArchitectureTests` stay green.

**Frontend**
- `list-medical-exams.use-case.spec`, `update-medical-exam.use-case.spec`, `delete-medical-exam.use-case.spec`; repository-impl spec (three new paths/bodies).
- `swimmer-profile.viewmodel.spec`: `loadExams` populates + defaults to latest; changing `selectedExamId` updates `selectedExam`; edit path (`editingExamId` → `updateExam`) success + failure; record path still works; delete inline-confirm flow (ask → confirm → `deleteExam` → reload) success + failure; `canEdit` gating.

## 9. Out of scope (deferred)

- A reusable confirm-dialog component (inline confirm used instead).
- An exam change-audit trail / history-of-edits.
- Bulk edit/delete.
- The other profile tabs (Guardian, Physiological, InBody, Records, Health Monitoring, Attendance, Championships, Feedback).
