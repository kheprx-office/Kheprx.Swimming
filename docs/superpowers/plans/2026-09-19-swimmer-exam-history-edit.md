# Swimmer Profile — Exam History, Edit & Delete Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the Identity & Vitals tab so a coach can pick any of a swimmer's medical exams from a date dropdown, edit any exam (all fields, incl. date), and delete an exam.

**Architecture:** Backend adds the exam `id` to the vitals read model + DTO, a `MedicalExam.Update(...)` mutator, and list/update/delete repo+service methods behind three new `SwimmersController` endpoints (reusing `CreateMedicalExamRequest` + its validator for the PUT). Frontend adds list/update/delete use-cases and reworks the Vitals section into a list-driven dropdown with Record/Edit/Delete. Extends the already-built (uncommitted) swimmer-profile feature.

**Tech Stack:** .NET (EF Core + Postgres, FluentValidation, xUnit + Moq), Angular 20 (standalone, signals, Jest), Tailwind.

**Spec:** `docs/superpowers/specs/2026-09-19-swimmer-exam-history-edit-design.md`

## Global Constraints

- **Approach A:** dedicated `GET {id}/medical-exams` list endpoint; the profile read (`GET /api/swimmers/{id}`) is unchanged.
- **Edit = all fields incl. exam date.** Reuse `CreateMedicalExamRequest` + `CreateMedicalExamRequestValidator` for the PUT body.
- **Ownership guard:** PUT/DELETE require the target exam's `SwimmerId == {id}` route value, else 404.
- **Auth:** list = `[Authorize]`; update + delete = `[Authorize(Roles="head_coach,captain")]`. UI Record/Edit/Delete gated on `vm.canEdit()`.
- **Expose the exam id** end-to-end: `Guid Id` first field of `MedicalExamRow` and `SwimmerVitalsDto`; `id: string` first field of the frontend `SwimmerVitals`.
- **List 404 = unknown swimmer** → reuse `SwimmerMessages.Errors.ProfileNotFound`. `ExamNotFound` is for PUT/DELETE missing/not-owned exam.
- **Inline delete confirm** (no new modal component, no browser `confirm()`).
- **Bilingual:** every new i18n key in BOTH `en.json` and `ar.json`.
- **TDD.** Backend tests: `dotnet test <project>`. Frontend tests: `npx jest <path>` (from `frontend/`). Compile: backend `dotnet build`; frontend `npm run build`.
- **Stop any running backend before `dotnet test`/`dotnet build`** (running backend locks DLLs). **Never redirect test/build output into files inside the repo.**
- **Commits:** the user asked to defer committing — the executing agent must confirm before the first `git commit`. Never push. Branch: `feat/swimmer-profile-identity-vitals`.

---

# Phase 1 — Backend

## Task 1: `MedicalExam.Update(...)` mutator

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/MedicalExam.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/MedicalExamTests.cs`

**Interfaces:**
- Produces: `MedicalExam.Update(DateOnly examDate, Guid internalMedId, Guid heartAssessId, Guid spineAssessId, Guid? bloodTypeId, decimal hemoglobin, decimal heightCm, decimal weightKg)` — mutates the eight fields, leaves `Id`/`SwimmerId`/`CreatedAt`.

- [ ] **Step 1: Write the failing test** (append to `MedicalExamTests`)

```csharp
    [Fact]
    public void Update_changes_mutable_fields_and_keeps_id_swimmer_createdAt()
    {
        var exam = new MedicalExam(Guid.NewGuid(), new DateOnly(2024, 1, 1),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 13.0m, 178m, 71m);
        var id = exam.Id; var swimmerId = exam.SwimmerId; var createdAt = exam.CreatedAt;
        var im = Guid.NewGuid(); var ha = Guid.NewGuid(); var sa = Guid.NewGuid(); var bt = Guid.NewGuid();

        exam.Update(new DateOnly(2026, 9, 19), im, ha, sa, bt, 15.0m, 183m, 75m);

        Assert.Equal(new DateOnly(2026, 9, 19), exam.ExamDate);
        Assert.Equal(im, exam.InternalMedId);
        Assert.Equal(ha, exam.HeartAssessId);
        Assert.Equal(sa, exam.SpineAssessId);
        Assert.Equal(bt, exam.BloodTypeId);
        Assert.Equal(15.0m, exam.Hemoglobin);
        Assert.Equal(183m, exam.HeightCm);
        Assert.Equal(75m, exam.WeightKg);
        Assert.Equal(id, exam.Id);
        Assert.Equal(swimmerId, exam.SwimmerId);
        Assert.Equal(createdAt, exam.CreatedAt);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter MedicalExamTests`
Expected: FAIL — `Update` does not exist.

- [ ] **Step 3: Write minimal implementation** (add the method to `MedicalExam`, after the constructor)

```csharp
    public void Update(DateOnly examDate, Guid internalMedId, Guid heartAssessId, Guid spineAssessId,
        Guid? bloodTypeId, decimal hemoglobin, decimal heightCm, decimal weightKg)
    {
        ExamDate = examDate;
        InternalMedId = internalMedId;
        HeartAssessId = heartAssessId;
        SpineAssessId = spineAssessId;
        BloodTypeId = bloodTypeId;
        Hemoglobin = hemoglobin;
        HeightCm = heightCm;
        WeightKg = weightKg;
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter MedicalExamTests`
Expected: PASS.

- [ ] **Step 5: Commit** (after user authorizes)

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/MedicalExam.cs backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/MedicalExamTests.cs
git commit -m "feat(identity): MedicalExam.Update mutator"
```

---

## Task 2: Thread the exam `id` through the read model + DTO

**Files:**
- Modify: `.../Identity.Domain/ReadModels/MedicalExamRow.cs`
- Modify: `.../Identity.Infrastructure/Repositories/SwimmerProfileRepository.cs` (the `Project()` expression)
- Modify: `.../Identity.Application/DTOs/SwimmerDtos.cs` (`SwimmerVitalsDto`)
- Modify: `.../Identity.Application/Services/SwimmerService.cs` (`MapVitals`)
- Modify (ripple): `backend/tests/…/Services/SwimmerServiceTests.cs`, `backend/tests/…/Api.UnitTests/SwimmersControllerTests.cs`

**Interfaces:**
- Produces:
  - `MedicalExamRow(Guid Id, DateOnly ExamDate, decimal Hemoglobin, decimal HeightCm, decimal WeightKg, Guid? BloodTypeId, string? BloodTypeCode, string? BloodTypeNameEn, string? BloodTypeNameAr, Guid InternalMedId, string InternalMedCode, string InternalMedNameEn, string? InternalMedNameAr, Guid HeartAssessId, string HeartAssessCode, string HeartAssessNameEn, string? HeartAssessNameAr, Guid SpineAssessId, string SpineAssessCode, string SpineAssessNameEn, string? SpineAssessNameAr)` — `Id` prepended.
  - `SwimmerVitalsDto(Guid Id, DateOnly ExamDate, CodedLookupDto? BloodType, decimal Hemoglobin, decimal HeightCm, decimal WeightKg, CodedLookupDto InternalMed, CodedLookupDto HeartAssess, CodedLookupDto SpineAssess)` — `Id` prepended.

- [ ] **Step 1: Update the rippled tests first (they should FAIL to compile until the impl changes)**

In `SwimmerServiceTests.cs`, `GetProfile_maps_latest_vitals_when_present`: add an `examId` and thread it, and assert it maps. Change the setup + add an assertion:

```csharp
        var im = Guid.NewGuid();
        var examId = Guid.NewGuid();
        swimmers.Setup(r => r.GetProfileByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SwimmerProfileRow(id, "SW-0001", "Alpha", null, null, null, null, null, null));
        swimmers.Setup(r => r.GetLatestExamAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MedicalExamRow(examId, new DateOnly(2024, 10, 8), 14.8m, 182m, 74m,
                    null, null, null, null,
                    im, "fit", "Fit", "لائق",
                    im, "fit", "Fit", "لائق",
                    im, "fit", "Fit", "لائق"));
```
…and after `var dto = await svc.GetProfileAsync(id);` add:
```csharp
        Assert.Equal(examId, dto!.Vitals!.Id);
```

In the same file, `CreateExam_persists_and_returns_vitals`, prepend an id to the `MedicalExamRow`:
```csharp
        swimmers.Setup(r => r.GetExamRowByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MedicalExamRow(Guid.NewGuid(), new DateOnly(2026, 9, 19), 15m, 183m, 75m,
                    null, null, null, null, im, "fit", "Fit", "لائق", im, "fit", "Fit", "لائق", im, "fit", "Fit", "لائق"));
```
(`im` already exists in that test.)

In `SwimmersControllerTests.cs`, `CreateExam_returns_201_with_vitals`, prepend an id to the `SwimmerVitalsDto`:
```csharp
        svc.Setup(s => s.CreateExamAsync(It.IsAny<Guid>(), It.IsAny<CreateMedicalExamRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new SwimmerVitalsDto(Guid.NewGuid(), new DateOnly(2026, 9, 19), null, 15m, 183m, 75m, im, im, im));
```

- [ ] **Step 2: Run to verify a compile failure** (the DTO/read model don't yet have `Id`)

Run: `dotnet build backend`
Expected: FAIL — too many args for `MedicalExamRow`/`SwimmerVitalsDto`.

- [ ] **Step 3: Add `Id` to the read model, projection, DTO, and MapVitals**

`MedicalExamRow.cs` — add `Guid Id,` as the FIRST positional parameter (before `DateOnly ExamDate`).

`SwimmerProfileRepository.cs` `Project()` — add `x.ExamId,` as the first argument:
```csharp
    private static System.Linq.Expressions.Expression<Func<ExamJoin, MedicalExamRow>> Project() =>
        x => new MedicalExamRow(
            x.ExamId,
            x.ExamDate, x.Hemoglobin, x.HeightCm, x.WeightKg,
            x.BloodTypeId,
            x.BloodTypeCode,
            x.BloodTypeNameEn,
            x.BloodTypeNameAr,
            x.InternalMedId, x.InternalMedCode, x.InternalMedNameEn, x.InternalMedNameAr,
            x.HeartAssessId, x.HeartAssessCode, x.HeartAssessNameEn, x.HeartAssessNameAr,
            x.SpineAssessId, x.SpineAssessCode, x.SpineAssessNameEn, x.SpineAssessNameAr);
```

`SwimmerDtos.cs` — add `Guid Id,` as the FIRST field of `SwimmerVitalsDto`:
```csharp
public sealed record SwimmerVitalsDto(
    Guid Id, DateOnly ExamDate, CodedLookupDto? BloodType, decimal Hemoglobin, decimal HeightCm, decimal WeightKg,
    CodedLookupDto InternalMed, CodedLookupDto HeartAssess, CodedLookupDto SpineAssess);
```

`SwimmerService.cs` `MapVitals` — pass `e.Id` first:
```csharp
        return new SwimmerVitalsDto(
            e.Id, e.ExamDate, blood, e.Hemoglobin, e.HeightCm, e.WeightKg,
            new CodedLookupDto(e.InternalMedId, e.InternalMedCode, e.InternalMedNameEn, e.InternalMedNameAr),
            new CodedLookupDto(e.HeartAssessId, e.HeartAssessCode, e.HeartAssessNameEn, e.HeartAssessNameAr),
            new CodedLookupDto(e.SpineAssessId, e.SpineAssessCode, e.SpineAssessNameEn, e.SpineAssessNameAr));
```

- [ ] **Step 4: Run the affected suites + build**

Run:
```
dotnet build backend
dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter SwimmerServiceTests
dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter SwimmersControllerTests
```
Expected: build succeeds; both suites PASS (incl. the new `examId` assertion).

- [ ] **Step 5: Commit** (after user authorizes)

```bash
git add backend/
git commit -m "feat(identity): expose medical_exam id on MedicalExamRow + SwimmerVitalsDto"
```

---

## Task 3: Repository — list / get-tracked / remove exams

**Files:**
- Modify: `.../Identity.Domain/Repositories/ISwimmerProfileRepository.cs`
- Modify: `.../Identity.Infrastructure/Repositories/SwimmerProfileRepository.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/SwimmerProfileRepositoryTests.cs`

**Interfaces:**
- Consumes: `ExamRows()` / `Project()` (Task 2), `MedicalExam` (Task 1).
- Produces:
  - `Task<IReadOnlyList<MedicalExamRow>> ListExamsAsync(Guid swimmerId, CancellationToken ct = default)`
  - `Task<MedicalExam?> GetExamTrackedAsync(Guid examId, CancellationToken ct = default)`
  - `void RemoveExam(MedicalExam exam)`

- [ ] **Step 1: Write the failing tests** (append to `SwimmerProfileRepositoryTests`)

```csharp
    [Fact]
    public async Task ListExams_returns_all_newest_first_with_ids()
    {
        await using var db = NewDb();
        var swimmerId = Guid.NewGuid();
        var fit = new FitnessAssessment("fit", "Fit", "لائق");
        db.FitnessAssessments.Add(fit);
        var older = new MedicalExam(swimmerId, new DateOnly(2024, 1, 1), fit.Id, fit.Id, fit.Id, null, 13m, 178m, 71m);
        var newer = new MedicalExam(swimmerId, new DateOnly(2024, 10, 8), fit.Id, fit.Id, fit.Id, null, 14.8m, 182m, 74m);
        db.MedicalExams.AddRange(older, newer);
        await db.SaveChangesAsync();

        var rows = await new SwimmerProfileRepository(db).ListExamsAsync(swimmerId);

        Assert.Equal(2, rows.Count);
        Assert.Equal(newer.Id, rows[0].Id);       // newest first
        Assert.Equal(older.Id, rows[1].Id);
        Assert.Equal("Fit", rows[0].InternalMedNameEn);
    }

    [Fact]
    public async Task GetExamTracked_returns_tracked_or_null()
    {
        await using var db = NewDb();
        var fit = new FitnessAssessment("fit", "Fit", "لائق");
        db.FitnessAssessments.Add(fit);
        var exam = new MedicalExam(Guid.NewGuid(), new DateOnly(2024, 1, 1), fit.Id, fit.Id, fit.Id, null, 13m, 178m, 71m);
        db.MedicalExams.Add(exam);
        await db.SaveChangesAsync();
        var repo = new SwimmerProfileRepository(db);

        Assert.NotNull(await repo.GetExamTrackedAsync(exam.Id));
        Assert.Null(await repo.GetExamTrackedAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RemoveExam_deletes_on_save()
    {
        await using var db = NewDb();
        var fit = new FitnessAssessment("fit", "Fit", "لائق");
        db.FitnessAssessments.Add(fit);
        var exam = new MedicalExam(Guid.NewGuid(), new DateOnly(2024, 1, 1), fit.Id, fit.Id, fit.Id, null, 13m, 178m, 71m);
        db.MedicalExams.Add(exam);
        await db.SaveChangesAsync();
        var repo = new SwimmerProfileRepository(db);

        var tracked = await repo.GetExamTrackedAsync(exam.Id);
        repo.RemoveExam(tracked!);
        await repo.SaveChangesAsync();

        Assert.Equal(0, await db.MedicalExams.CountAsync());
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter SwimmerProfileRepositoryTests`
Expected: FAIL — methods don't exist.

- [ ] **Step 3: Implement**

`ISwimmerProfileRepository.cs` — add:
```csharp
    Task<IReadOnlyList<MedicalExamRow>> ListExamsAsync(Guid swimmerId, CancellationToken ct = default);
    Task<MedicalExam?> GetExamTrackedAsync(Guid examId, CancellationToken ct = default);
    void RemoveExam(MedicalExam exam);
```

`SwimmerProfileRepository.cs` — add (next to the other exam methods):
```csharp
    public async Task<IReadOnlyList<MedicalExamRow>> ListExamsAsync(Guid swimmerId, CancellationToken ct = default)
        => await ExamRows()
            .Where(x => x.SwimmerId == swimmerId)
            .OrderByDescending(x => x.ExamDate).ThenByDescending(x => x.CreatedAt)
            .Select(Project())
            .ToListAsync(ct);

    public Task<MedicalExam?> GetExamTrackedAsync(Guid examId, CancellationToken ct = default)
        => _db.MedicalExams.FirstOrDefaultAsync(e => e.Id == examId, ct);

    public void RemoveExam(MedicalExam exam) => _db.MedicalExams.Remove(exam);
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter SwimmerProfileRepositoryTests`
Expected: PASS.

- [ ] **Step 5: Commit** (after user authorizes)

```bash
git add backend/
git commit -m "feat(identity): repo list/get-tracked/remove medical exams"
```

---

## Task 4: Service — list / update / delete exams

**Files:**
- Modify: `.../Identity.Application/Services/Interfaces/ISwimmerService.cs`
- Modify: `.../Identity.Application/Services/SwimmerService.cs`
- Test: `backend/tests/…/Services/SwimmerServiceTests.cs`

**Interfaces:**
- Consumes: `ListExamsAsync`/`GetExamTrackedAsync`/`RemoveExam` (Task 3), `MapVitals`, `EnsureExamReferencesExist`, `MedicalExam.Update` (Task 1), `GetExamRowByIdAsync`.
- Produces:
  - `Task<IReadOnlyList<SwimmerVitalsDto>?> ListExamsAsync(Guid swimmerId, CancellationToken ct = default)`
  - `Task<SwimmerVitalsDto?> UpdateExamAsync(Guid swimmerId, Guid examId, CreateMedicalExamRequest request, CancellationToken ct = default)`
  - `Task<bool> DeleteExamAsync(Guid swimmerId, Guid examId, CancellationToken ct = default)`

- [ ] **Step 1: Write the failing tests** (append to `SwimmerServiceTests`; `Build()`, `ExamReq()`, and the `swimmers`/`Build(refsExist:…)` helpers already exist)

```csharp
    [Fact]
    public async Task ListExams_returns_null_when_swimmer_missing()
    {
        var (svc, swimmers, _) = Build();
        swimmers.Setup(r => r.GetByIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwimmerProfile?)null);
        Assert.Null(await svc.ListExamsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ListExams_maps_rows_to_dtos()
    {
        var (svc, swimmers, _) = Build();
        var id = Guid.NewGuid(); var examId = Guid.NewGuid(); var im = Guid.NewGuid();
        swimmers.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid()));
        swimmers.Setup(r => r.ListExamsAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new MedicalExamRow(examId, new DateOnly(2024, 10, 8), 14.8m, 182m, 74m,
                    null, null, null, null, im, "fit", "Fit", "لائق", im, "fit", "Fit", "لائق", im, "fit", "Fit", "لائق") });

        var list = await svc.ListExamsAsync(id);

        Assert.NotNull(list);
        Assert.Single(list!);
        Assert.Equal(examId, list![0].Id);
    }

    [Fact]
    public async Task UpdateExam_returns_null_when_exam_missing_or_not_owned()
    {
        var (svc, swimmers, _) = Build();
        var swimmerId = Guid.NewGuid();
        // not found
        swimmers.Setup(r => r.GetExamTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MedicalExam?)null);
        Assert.Null(await svc.UpdateExamAsync(swimmerId, Guid.NewGuid(), ExamReq()));
        // owned by a different swimmer
        var other = new MedicalExam(Guid.NewGuid(), new DateOnly(2024, 1, 1), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 13m, 178m, 71m);
        swimmers.Setup(r => r.GetExamTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(other);
        Assert.Null(await svc.UpdateExamAsync(swimmerId, other.Id, ExamReq()));
    }

    [Fact]
    public async Task UpdateExam_updates_saves_and_returns_vitals()
    {
        var (svc, swimmers, _) = Build();
        var swimmerId = Guid.NewGuid(); var im = Guid.NewGuid();
        var exam = new MedicalExam(swimmerId, new DateOnly(2024, 1, 1), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 13m, 178m, 71m);
        swimmers.Setup(r => r.GetExamTrackedAsync(exam.Id, It.IsAny<CancellationToken>())).ReturnsAsync(exam);
        swimmers.Setup(r => r.GetExamRowByIdAsync(exam.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MedicalExamRow(exam.Id, new DateOnly(2026, 9, 19), 15m, 183m, 75m,
                    null, null, null, null, im, "fit", "Fit", "لائق", im, "fit", "Fit", "لائق", im, "fit", "Fit", "لائق"));

        var vitals = await svc.UpdateExamAsync(swimmerId, exam.Id, ExamReq());

        Assert.NotNull(vitals);
        Assert.Equal(exam.Id, vitals!.Id);
        Assert.Equal(new DateOnly(2026, 9, 19), exam.ExamDate);   // entity was mutated
        swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteExam_removes_and_returns_true()
    {
        var (svc, swimmers, _) = Build();
        var swimmerId = Guid.NewGuid();
        var exam = new MedicalExam(swimmerId, new DateOnly(2024, 1, 1), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 13m, 178m, 71m);
        swimmers.Setup(r => r.GetExamTrackedAsync(exam.Id, It.IsAny<CancellationToken>())).ReturnsAsync(exam);

        Assert.True(await svc.DeleteExamAsync(swimmerId, exam.Id));
        swimmers.Verify(r => r.RemoveExam(exam), Times.Once);
        swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteExam_returns_false_when_missing_or_not_owned()
    {
        var (svc, swimmers, _) = Build();
        swimmers.Setup(r => r.GetExamTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MedicalExam?)null);
        Assert.False(await svc.DeleteExamAsync(Guid.NewGuid(), Guid.NewGuid()));
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter SwimmerServiceTests`
Expected: FAIL — methods don't exist.

- [ ] **Step 3: Implement**

`ISwimmerService.cs` — add:
```csharp
    Task<IReadOnlyList<SwimmerVitalsDto>?> ListExamsAsync(Guid swimmerId, CancellationToken ct = default);
    Task<SwimmerVitalsDto?> UpdateExamAsync(Guid swimmerId, Guid examId, CreateMedicalExamRequest request, CancellationToken ct = default);
    Task<bool> DeleteExamAsync(Guid swimmerId, Guid examId, CancellationToken ct = default);
```

`SwimmerService.cs` — add (near `CreateExamAsync`). `MapVitals` is non-null for a real row, so use the null-forgiving `!`:
```csharp
    public async Task<IReadOnlyList<SwimmerVitalsDto>?> ListExamsAsync(Guid swimmerId, CancellationToken ct = default)
    {
        var profile = await _swimmers.GetByIdTrackedAsync(swimmerId, ct);
        if (profile is null) return null;
        var rows = await _swimmers.ListExamsAsync(swimmerId, ct);
        return rows.Select(r => MapVitals(r)!).ToList();
    }

    public async Task<SwimmerVitalsDto?> UpdateExamAsync(Guid swimmerId, Guid examId, CreateMedicalExamRequest request, CancellationToken ct = default)
    {
        var exam = await _swimmers.GetExamTrackedAsync(examId, ct);
        if (exam is null || exam.SwimmerId != swimmerId) return null;

        await EnsureExamReferencesExist(request, ct);

        exam.Update(request.ExamDate, request.InternalMedId, request.HeartAssessId, request.SpineAssessId,
            request.BloodTypeId, request.Hemoglobin, request.HeightCm, request.WeightKg);
        await _swimmers.SaveChangesAsync(ct);

        return MapVitals(await _swimmers.GetExamRowByIdAsync(examId, ct));
    }

    public async Task<bool> DeleteExamAsync(Guid swimmerId, Guid examId, CancellationToken ct = default)
    {
        var exam = await _swimmers.GetExamTrackedAsync(examId, ct);
        if (exam is null || exam.SwimmerId != swimmerId) return false;

        _swimmers.RemoveExam(exam);
        await _swimmers.SaveChangesAsync(ct);
        return true;
    }
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter SwimmerServiceTests`
Expected: PASS.

- [ ] **Step 5: Commit** (after user authorizes)

```bash
git add backend/
git commit -m "feat(identity): service list/update/delete medical exams"
```

---

## Task 5: Controller endpoints + messages

**Files:**
- Modify: `.../Identity.Application/Resources/SwimmerMessages.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs`
- Test: `backend/tests/…/Api.UnitTests/SwimmersControllerTests.cs`

**Interfaces:**
- Consumes: service `ListExamsAsync`/`UpdateExamAsync`/`DeleteExamAsync` (Task 4).
- Produces: `GET/PUT/DELETE {id}/medical-exams[/{examId}]` controller actions.

- [ ] **Step 1: Write the failing tests** (append to `SwimmersControllerTests`; `im` = `new CodedLookupDto(Guid.NewGuid(), "fit", "Fit", "لائق")` and `req = new CreateMedicalExamRequest(new DateOnly(2026,9,19), null, 15m, 183m, 75m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())` per the existing CreateExam tests)

```csharp
    [Fact]
    public async Task ListExams_returns_200_with_exams()
    {
        var svc = new Mock<ISwimmerService>();
        var im = new CodedLookupDto(Guid.NewGuid(), "fit", "Fit", "لائق");
        svc.Setup(s => s.ListExamsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<SwimmerVitalsDto> { new(Guid.NewGuid(), new DateOnly(2026, 9, 19), null, 15m, 183m, 75m, im, im, im) });

        var result = await new SwimmersController(svc.Object).ListExams(Guid.NewGuid(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<IReadOnlyList<SwimmerVitalsDto>>>(ok.Value);
        Assert.Single(body.Data!);
    }

    [Fact]
    public async Task ListExams_returns_404_when_swimmer_missing()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.ListExamsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync((IReadOnlyList<SwimmerVitalsDto>?)null);
        var result = await new SwimmersController(svc.Object).ListExams(Guid.NewGuid(), CancellationToken.None);
        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task UpdateExam_returns_200_with_vitals()
    {
        var svc = new Mock<ISwimmerService>();
        var im = new CodedLookupDto(Guid.NewGuid(), "fit", "Fit", "لائق");
        svc.Setup(s => s.UpdateExamAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreateMedicalExamRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new SwimmerVitalsDto(Guid.NewGuid(), new DateOnly(2026, 9, 19), null, 15m, 183m, 75m, im, im, im));
        var req = new CreateMedicalExamRequest(new DateOnly(2026, 9, 19), null, 15m, 183m, 75m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var result = await new SwimmersController(svc.Object).UpdateExam(Guid.NewGuid(), Guid.NewGuid(), req, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateExam_returns_404_when_not_found()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.UpdateExamAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreateMedicalExamRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync((SwimmerVitalsDto?)null);
        var req = new CreateMedicalExamRequest(new DateOnly(2026, 9, 19), null, 15m, 183m, 75m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var result = await new SwimmersController(svc.Object).UpdateExam(Guid.NewGuid(), Guid.NewGuid(), req, CancellationToken.None);
        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }

    [Fact]
    public async Task DeleteExam_returns_200_when_deleted()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.DeleteExamAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var result = await new SwimmersController(svc.Object).DeleteExam(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task DeleteExam_returns_404_when_missing()
    {
        var svc = new Mock<ISwimmerService>();
        svc.Setup(s => s.DeleteExamAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var result = await new SwimmersController(svc.Object).DeleteExam(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        var nf = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, nf.StatusCode);
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter SwimmersControllerTests`
Expected: FAIL — actions don't exist.

- [ ] **Step 3: Implement**

`SwimmerMessages.cs` — add to `Success`:
```csharp
        public static string ExamsListed(string lang) => lang switch { "ar" => "سجل الفحوصات", _ => "Exam history" };
        public static string ExamUpdated(string lang) => lang switch { "ar" => "تم تحديث الفحص", _ => "Exam updated" };
        public static string ExamDeleted(string lang) => lang switch { "ar" => "تم حذف الفحص", _ => "Exam deleted" };
```
…and to `Errors`:
```csharp
        public static string ExamNotFound(string lang) => lang switch { "ar" => "الفحص غير موجود", _ => "Exam not found" };
```

`SwimmersController.cs` — add a region (after the `CreateExam` region), reusing `[FromBody]` inference for the PUT:
```csharp
    #region Exams — GET list / PUT update / DELETE

    /// <summary>Lists all medical exams for a swimmer, newest first.</summary>
    /// <response code="200">The exam history.</response>
    /// <response code="404">No swimmer with that id.</response>
    [HttpGet("{id:guid}/medical-exams")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SwimmerVitalsDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SwimmerVitalsDto>>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SwimmerVitalsDto>>>> ListExams(Guid id, CancellationToken ct)
    {
        var exams = await _service.ListExamsAsync(id, ct);
        if (exams is null)
        {
            var nf = ApiResponse<IReadOnlyList<SwimmerVitalsDto>>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<IReadOnlyList<SwimmerVitalsDto>>.Success(SwimmerMessages.Success.ExamsListed(AppLanguage.Current), exams));
    }

    /// <summary>Updates a specific medical exam (all fields). Head Coach or Captain only.</summary>
    /// <response code="200">Exam updated; returns the new vitals.</response>
    /// <response code="404">No such exam for this swimmer.</response>
    [HttpPut("{id:guid}/medical-exams/{examId:guid}")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<SwimmerVitalsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SwimmerVitalsDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SwimmerVitalsDto>>> UpdateExam(Guid id, Guid examId, CreateMedicalExamRequest request, CancellationToken ct)
    {
        var vitals = await _service.UpdateExamAsync(id, examId, request, ct);
        if (vitals is null)
        {
            var nf = ApiResponse<SwimmerVitalsDto>.Failure(SwimmerMessages.Errors.ExamNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<SwimmerVitalsDto>.Success(SwimmerMessages.Success.ExamUpdated(AppLanguage.Current), vitals));
    }

    /// <summary>Deletes a specific medical exam. Head Coach or Captain only.</summary>
    /// <response code="200">Exam deleted.</response>
    /// <response code="404">No such exam for this swimmer.</response>
    [HttpDelete("{id:guid}/medical-exams/{examId:guid}")]
    [Authorize(Roles = "head_coach,captain")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteExam(Guid id, Guid examId, CancellationToken ct)
    {
        var deleted = await _service.DeleteExamAsync(id, examId, ct);
        if (!deleted)
        {
            var nf = ApiResponse<object>.Failure(SwimmerMessages.Errors.ExamNotFound(AppLanguage.Current), "not_found");
            return StatusCode(StatusCodes.Status404NotFound, nf);
        }
        return Ok(ApiResponse<object>.Success(SwimmerMessages.Success.ExamDeleted(AppLanguage.Current), null));
    }

    #endregion
```

- [ ] **Step 4: Run the affected suites + build**

Run:
```
dotnet build backend
dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter SwimmersControllerTests
dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests
```
Expected: build succeeds; both suites PASS.

- [ ] **Step 5: Commit** (after user authorizes)

```bash
git add backend/
git commit -m "feat(swimmers): GET/PUT/DELETE medical-exams endpoints"
```

---

# Phase 2 — Frontend

Run from `frontend/`: tests `npx jest <path>`, compile `npm run build`.

## Task 6: Expose `id` on the vitals model/DTO + shared vitals mapper

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/domain/model/swimmer-profile.ts`
- Modify: `frontend/src/app/features/swimmer-profile/data/dto/swimmer-profile.dto.ts`
- Create: `frontend/src/app/features/swimmer-profile/data/dto/vitals.mapper.ts`
- Modify: `frontend/src/app/features/swimmer-profile/domain/usecases/get-swimmer-profile.use-case.ts`
- Modify: `frontend/src/app/features/swimmer-profile/domain/usecases/create-medical-exam.use-case.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/data/dto/vitals.mapper.spec.ts`
- Modify (ripple): the two use-case specs' `VITALS` fixtures.

**Interfaces:**
- Produces:
  - `SwimmerVitals { id: string; examDate; bloodType; hemoglobin; heightCm; weightKg; internalMed; heartAssess; spineAssess }`
  - `SwimmerVitalsDtoRs { id: string; … }`; `MedicalExamListDtoRs extends BaseResponseRs<SwimmerVitalsDtoRs[]>`; `isVitalsListValid(data): data is SwimmerVitalsDtoRs[]`; `DeleteExamItemDtoRs extends BaseResponseRs<unknown>`.
  - `toRef(r: CodedRefDtoRs): LookupItem`, `toVitals(d: SwimmerVitalsDtoRs): SwimmerVitals` (in `vitals.mapper.ts`).

- [ ] **Step 1: Write the failing test** (`vitals.mapper.spec.ts`)

```typescript
import { toVitals } from '@features/swimmer-profile/data/dto/vitals.mapper';
import { isVitalsListValid } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';

const REF = { id: 'f1', code: 'fit', nameEn: 'Fit', nameAr: 'لائق' };
const DTO = { id: 'e1', examDate: '2026-09-19', bloodType: null, hemoglobin: 15, heightCm: 183, weightKg: 75, internalMed: REF, heartAssess: REF, spineAssess: REF };

describe('toVitals', () => {
  it('maps a vitals DTO to the domain model incl. id', () => {
    const v = toVitals(DTO);
    expect(v.id).toBe('e1');
    expect(v.bloodType).toBeNull();
    expect(v.internalMed.nameEn).toBe('Fit');
  });
});

describe('isVitalsListValid', () => {
  it('accepts an array of well-formed vitals', () => {
    expect(isVitalsListValid([DTO])).toBe(true);
  });
  it('rejects non-arrays and malformed items', () => {
    expect(isVitalsListValid(null)).toBe(false);
    expect(isVitalsListValid([{ id: 'e1' }])).toBe(false);
  });
});
```

- [ ] **Step 2: Run to verify failure**

Run: `npx jest vitals.mapper`
Expected: FAIL — modules/exports don't exist.

- [ ] **Step 3: Implement**

`domain/model/swimmer-profile.ts` — add `id: string;` as the first field of `SwimmerVitals`:
```typescript
export interface SwimmerVitals {
  id: string;
  examDate: string;
  bloodType: LookupItem | null;
  hemoglobin: number;
  heightCm: number;
  weightKg: number;
  internalMed: LookupItem;
  heartAssess: LookupItem;
  spineAssess: LookupItem;
}
```

`data/dto/swimmer-profile.dto.ts` — add `id: string;` to `SwimmerVitalsDtoRs`, and append the new list DTO + guard + delete DTO:
```typescript
export interface SwimmerVitalsDtoRs {
  id: string; examDate: string; bloodType: CodedRefDtoRs | null;
  hemoglobin: number; heightCm: number; weightKg: number;
  internalMed: CodedRefDtoRs; heartAssess: CodedRefDtoRs; spineAssess: CodedRefDtoRs;
}
```
```typescript
export interface MedicalExamListDtoRs extends BaseResponseRs<SwimmerVitalsDtoRs[]> {}
export interface DeleteExamItemDtoRs extends BaseResponseRs<unknown> {}

export function isVitalsListValid(data: unknown): data is SwimmerVitalsDtoRs[] {
  return Array.isArray(data) && data.every(
    (x) => x != null && typeof x === 'object'
      && typeof (x as SwimmerVitalsDtoRs).id === 'string'
      && typeof (x as SwimmerVitalsDtoRs).hemoglobin === 'number');
}
```

`data/dto/vitals.mapper.ts` (new):
```typescript
// vitals.mapper.ts — DTO → domain mapping shared by the profile/list/create/update use-cases.
import { CodedRefDtoRs, SwimmerVitalsDtoRs } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';
import { SwimmerVitals } from '@features/swimmer-profile/domain/model/swimmer-profile';
import { LookupItem } from '@features/reference/domain/model/reference';

export function toRef(r: CodedRefDtoRs): LookupItem {
  return { id: r.id, code: r.code, nameEn: r.nameEn, nameAr: r.nameAr };
}

export function toVitals(d: SwimmerVitalsDtoRs): SwimmerVitals {
  return {
    id: d.id,
    examDate: d.examDate,
    bloodType: d.bloodType ? toRef(d.bloodType) : null,
    hemoglobin: d.hemoglobin, heightCm: d.heightCm, weightKg: d.weightKg,
    internalMed: toRef(d.internalMed), heartAssess: toRef(d.heartAssess), spineAssess: toRef(d.spineAssess),
  };
}
```

`domain/usecases/get-swimmer-profile.use-case.ts` — remove the local `toRef` and the inline vitals mapping; import `toVitals` and use it. Replace the whole `toProfile` + `toRef` region with:
```typescript
import { SwimmerProfileDtoRs, isSwimmerProfileDtoRsValid } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';
import { toVitals } from '@features/swimmer-profile/data/dto/vitals.mapper';
// … (class unchanged: execute() still calls toProfile(res.data))

function toProfile(d: SwimmerProfileDtoRs): SwimmerProfile {
  return {
    identity: {
      id: d.identity.id, uid: d.identity.uid, nameEn: d.identity.nameEn, nameAr: d.identity.nameAr ?? null,
      dob: d.identity.dob ?? null, age: typeof d.identity.age === 'number' ? d.identity.age : null,
      genderCode: d.identity.genderCode, phone: d.identity.phone ?? null,
      trainingClubNameEn: d.identity.trainingClubNameEn ?? null, trainingClubNameAr: d.identity.trainingClubNameAr ?? null,
    },
    vitals: d.vitals ? toVitals(d.vitals) : null,
  };
}
```
(Drop the now-unused `CodedRefDtoRs`/`LookupItem` imports if they become unused.)

`domain/usecases/create-medical-exam.use-case.ts` — replace the inline `toRef` + mapping with the shared `toVitals`:
```typescript
import { toVitals } from '@features/swimmer-profile/data/dto/vitals.mapper';
// … in execute():
    const d = res.data;
    if (!d || typeof d.hemoglobin !== 'number') throw new AppError('Invalid created exam received', 'validation');
    return toVitals(d);
```
(Drop the now-unused `CodedRefDtoRs`/`LookupItem` imports.)

**Ripple — fixtures:** in both `get-swimmer-profile.use-case.spec.ts` and `create-medical-exam.use-case.spec.ts`, add `id: 'e1',` to the `VITALS` constant so it satisfies `SwimmerVitalsDtoRs`. (Assertions are unaffected.)

- [ ] **Step 4: Run to verify pass**

Run: `npx jest swimmer-profile`
Expected: PASS (mapper spec + the two refactored use-case specs + the dto spec).

- [ ] **Step 5: Commit** (after user authorizes)

```bash
git add frontend/src/app/features/swimmer-profile/
git commit -m "feat(swimmer-profile): vitals id + shared toVitals mapper"
```

---

## Task 7: Repository methods + list/update/delete use-cases

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/domain/repositories/swimmer-profile.repository.ts`
- Modify: `frontend/src/app/features/swimmer-profile/data/repositories/swimmer-profile.repository.impl.ts`
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/list-medical-exams.use-case.ts`
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/update-medical-exam.use-case.ts`
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/delete-medical-exam.use-case.ts`
- Test: repo impl spec (extend) + three use-case specs.

**Interfaces:**
- Consumes: `MedicalExamListDtoRs`, `DeleteExamItemDtoRs`, `CreatedVitalsItemDtoRs`, `CreateMedicalExamDtoRq`, `toVitals` (Task 6).
- Produces:
  - repo: `listExams(id): Promise<MedicalExamListDtoRs>`, `updateExam(id, examId, rq): Promise<CreatedVitalsItemDtoRs>`, `deleteExam(id, examId): Promise<DeleteExamItemDtoRs>`
  - `ListMedicalExamsUseCase extends UseCase<string, SwimmerVitals[]>`
  - `UpdateMedicalExamUseCase extends UseCase<{id; examId; rq}, SwimmerVitals>` (input `UpdateMedicalExamInput`)
  - `DeleteMedicalExamUseCase extends UseCase<{id; examId}, void>` (input `DeleteMedicalExamInput`)

- [ ] **Step 1: Write the failing tests**

Append to `swimmer-profile.repository.impl.spec.ts` (the `http` mock already has get/put/post — add `delete`):
```typescript
  it('listExams GETs the exams endpoint', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: [] });
    await repo.listExams('s1');
    expect(http.get).toHaveBeenCalledWith('/api/swimmers/s1/medical-exams');
  });

  it('updateExam PUTs the exam endpoint with the body', async () => {
    (http.put as jest.Mock).mockResolvedValue({ data: {} });
    const rq = { examDate: '2026-09-19', hemoglobin: 15, heightCm: 183, weightKg: 75, internalMedId: 'f1', heartAssessId: 'f1', spineAssessId: 'f1' };
    await repo.updateExam('s1', 'e1', rq as never);
    expect(http.put).toHaveBeenCalledWith('/api/swimmers/s1/medical-exams/e1', { body: rq });
  });

  it('deleteExam DELETEs the exam endpoint', async () => {
    ((http as unknown as { delete: jest.Mock }).delete) = jest.fn().mockResolvedValue({ data: null });
    await repo.deleteExam('s1', 'e1');
    expect((http as unknown as { delete: jest.Mock }).delete).toHaveBeenCalledWith('/api/swimmers/s1/medical-exams/e1');
  });
```
Add `delete: jest.fn()` to the `http` mock object at the top of that spec.

`list-medical-exams.use-case.spec.ts`:
```typescript
import { TestBed } from '@angular/core/testing';
import { ListMedicalExamsUseCase } from '@features/swimmer-profile/domain/usecases/list-medical-exams.use-case';
import { SWIMMER_PROFILE_REPOSITORY, ISwimmerProfileRepository } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

const REF = { id: 'f1', code: 'fit', nameEn: 'Fit', nameAr: 'لائق' };
const DTO = { id: 'e1', examDate: '2026-09-19', bloodType: null, hemoglobin: 15, heightCm: 183, weightKg: 75, internalMed: REF, heartAssess: REF, spineAssess: REF };

function build(repo: Partial<ISwimmerProfileRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }] });
  return TestBed.inject(ListMedicalExamsUseCase);
}

describe('ListMedicalExamsUseCase', () => {
  it('maps the exam list', async () => {
    const uc = build({ listExams: async () => ({ data: [DTO] }) } as unknown as ISwimmerProfileRepository);
    const r = await uc.run('s1');
    expect(r.ok).toBe(true);
    if (r.ok) { expect(r.data).toHaveLength(1); expect(r.data[0].id).toBe('e1'); }
  });
  it('fails validation on a malformed payload', async () => {
    const uc = build({ listExams: async () => ({ data: [{ id: 'e1' }] }) } as unknown as ISwimmerProfileRepository);
    const r = await uc.run('s1');
    expect(r.ok).toBe(false);
  });
});
```

`update-medical-exam.use-case.spec.ts`:
```typescript
import { TestBed } from '@angular/core/testing';
import { UpdateMedicalExamUseCase } from '@features/swimmer-profile/domain/usecases/update-medical-exam.use-case';
import { SWIMMER_PROFILE_REPOSITORY, ISwimmerProfileRepository } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

const REF = { id: 'f1', code: 'fit', nameEn: 'Fit', nameAr: 'لائق' };
const DTO = { id: 'e1', examDate: '2026-09-19', bloodType: null, hemoglobin: 15, heightCm: 183, weightKg: 75, internalMed: REF, heartAssess: REF, spineAssess: REF };

function build(repo: Partial<ISwimmerProfileRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }] });
  return TestBed.inject(UpdateMedicalExamUseCase);
}

describe('UpdateMedicalExamUseCase', () => {
  it('maps the updated vitals', async () => {
    const uc = build({ updateExam: async () => ({ data: DTO }) } as unknown as ISwimmerProfileRepository);
    const rq = { examDate: '2026-09-19', hemoglobin: 15, heightCm: 183, weightKg: 75, internalMedId: 'f1', heartAssessId: 'f1', spineAssessId: 'f1' };
    const r = await uc.run({ id: 's1', examId: 'e1', rq });
    expect(r.ok).toBe(true);
    if (r.ok) { expect(r.data.id).toBe('e1'); expect(r.data.hemoglobin).toBe(15); }
  });
});
```

`delete-medical-exam.use-case.spec.ts`:
```typescript
import { TestBed } from '@angular/core/testing';
import { DeleteMedicalExamUseCase } from '@features/swimmer-profile/domain/usecases/delete-medical-exam.use-case';
import { SWIMMER_PROFILE_REPOSITORY, ISwimmerProfileRepository } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

function build(repo: Partial<ISwimmerProfileRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }] });
  return TestBed.inject(DeleteMedicalExamUseCase);
}

describe('DeleteMedicalExamUseCase', () => {
  it('calls the repository with id + examId', async () => {
    const deleteExam = jest.fn().mockResolvedValue({ data: null });
    const uc = build({ deleteExam } as unknown as ISwimmerProfileRepository);
    const r = await uc.run({ id: 's1', examId: 'e1' });
    expect(r.ok).toBe(true);
    expect(deleteExam).toHaveBeenCalledWith('s1', 'e1');
  });
  it('fails when the repository throws', async () => {
    const uc = build({ deleteExam: jest.fn().mockRejectedValue(new Error('boom')) } as unknown as ISwimmerProfileRepository);
    const r = await uc.run({ id: 's1', examId: 'e1' });
    expect(r.ok).toBe(false);
  });
});
```

- [ ] **Step 2: Run to verify failure**

Run: `npx jest swimmer-profile`
Expected: FAIL — repo methods / use-cases don't exist.

- [ ] **Step 3: Implement**

`swimmer-profile.repository.ts` — add to the interface (and imports):
```typescript
import { SwimmerProfileItemDtoRs, MedicalExamListDtoRs, DeleteExamItemDtoRs } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';
// … existing imports for UpdateIdentity/CreateMedicalExam DTOs …

export interface ISwimmerProfileRepository {
  getProfile(id: string): Promise<SwimmerProfileItemDtoRs>;
  updateIdentity(id: string, rq: UpdateIdentityDtoRq): Promise<UpdateIdentityItemDtoRs>;
  createExam(id: string, rq: CreateMedicalExamDtoRq): Promise<CreatedVitalsItemDtoRs>;
  listExams(id: string): Promise<MedicalExamListDtoRs>;
  updateExam(id: string, examId: string, rq: CreateMedicalExamDtoRq): Promise<CreatedVitalsItemDtoRs>;
  deleteExam(id: string, examId: string): Promise<DeleteExamItemDtoRs>;
}
```

`swimmer-profile.repository.impl.ts` — add the three methods + imports:
```typescript
  listExams(id: string): Promise<MedicalExamListDtoRs> {
    return this.http.get<MedicalExamListDtoRs>(`/api/swimmers/${id}/medical-exams`);
  }
  updateExam(id: string, examId: string, rq: CreateMedicalExamDtoRq): Promise<CreatedVitalsItemDtoRs> {
    return this.http.put<CreatedVitalsItemDtoRs>(`/api/swimmers/${id}/medical-exams/${examId}`, { body: rq });
  }
  deleteExam(id: string, examId: string): Promise<DeleteExamItemDtoRs> {
    return this.http.delete<DeleteExamItemDtoRs>(`/api/swimmers/${id}/medical-exams/${examId}`);
  }
```

`list-medical-exams.use-case.ts`:
```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { isVitalsListValid } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';
import { toVitals } from '@features/swimmer-profile/data/dto/vitals.mapper';
import { SwimmerVitals } from '@features/swimmer-profile/domain/model/swimmer-profile';

@Injectable({ providedIn: 'root' })
export class ListMedicalExamsUseCase extends UseCase<string, SwimmerVitals[]> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('ListMedicalExams'); }
  protected async execute(id: string): Promise<SwimmerVitals[]> {
    const res = await this.repo.listExams(id);
    if (!isVitalsListValid(res.data)) throw new AppError('Invalid exam list received', 'validation');
    return res.data.map(toVitals);
  }
}
```

`update-medical-exam.use-case.ts`:
```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { CreateMedicalExamDtoRq } from '@features/swimmer-profile/data/dto/create-medical-exam.dto';
import { toVitals } from '@features/swimmer-profile/data/dto/vitals.mapper';
import { SwimmerVitals } from '@features/swimmer-profile/domain/model/swimmer-profile';

export interface UpdateMedicalExamInput { id: string; examId: string; rq: CreateMedicalExamDtoRq; }

@Injectable({ providedIn: 'root' })
export class UpdateMedicalExamUseCase extends UseCase<UpdateMedicalExamInput, SwimmerVitals> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('UpdateMedicalExam'); }
  protected async execute(input: UpdateMedicalExamInput): Promise<SwimmerVitals> {
    const res = await this.repo.updateExam(input.id, input.examId, input.rq);
    const d = res.data;
    if (!d || typeof d.hemoglobin !== 'number') throw new AppError('Invalid updated exam received', 'validation');
    return toVitals(d);
  }
}
```

`delete-medical-exam.use-case.ts`:
```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

export interface DeleteMedicalExamInput { id: string; examId: string; }

@Injectable({ providedIn: 'root' })
export class DeleteMedicalExamUseCase extends UseCase<DeleteMedicalExamInput, void> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('DeleteMedicalExam'); }
  protected async execute(input: DeleteMedicalExamInput): Promise<void> {
    await this.repo.deleteExam(input.id, input.examId);
  }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `npx jest swimmer-profile`
Expected: PASS.

- [ ] **Step 5: Commit** (after user authorizes)

```bash
git add frontend/src/app/features/swimmer-profile/
git commit -m "feat(swimmer-profile): list/update/delete exam repo methods + use-cases"
```

---

## Task 8: Viewmodel — exam list, selection, edit/delete flows

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.ts`
- Modify: `frontend/src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `ListMedicalExamsUseCase`, `UpdateMedicalExamUseCase`, `DeleteMedicalExamUseCase`, `CreateMedicalExamUseCase` (Task 7), `SwimmerVitals` (Task 6).
- Produces (viewmodel surface used by the page in Task 9): `exams()`, `selectedExamId` (writable), `selectedExam()`, `loadingExams()`, `editingExamId()`, `confirmingDelete()`, `deleting()`, `startNewExam()`, `startEditExam()`, `saveVitals()`, `askDeleteExam()`, `cancelDelete()`, `confirmDeleteExam()`. `startEditVitals()` is **removed** (replaced by `startNewExam`/`startEditExam`).

- [ ] **Step 1: Update the spec first**

In `swimmer-profile.viewmodel.spec.ts`:

Add imports:
```typescript
import { ListMedicalExamsUseCase } from '@features/swimmer-profile/domain/usecases/list-medical-exams.use-case';
import { UpdateMedicalExamUseCase } from '@features/swimmer-profile/domain/usecases/update-medical-exam.use-case';
import { DeleteMedicalExamUseCase } from '@features/swimmer-profile/domain/usecases/delete-medical-exam.use-case';
```

Add an `id` to `VITALS` and a second exam fixture:
```typescript
const VITALS = { id: 'e1', examDate: '2026-09-19', bloodType: null, hemoglobin: 14.8, heightCm: 182, weightKg: 74, internalMed: REF, heartAssess: REF, spineAssess: REF };
const VITALS2 = { id: 'e2', examDate: '2024-01-01', bloodType: null, hemoglobin: 13, heightCm: 178, weightKg: 71, internalMed: REF, heartAssess: REF, spineAssess: REF };
```

Extend `build()`'s `over` type with `list?`, `updateExam?`, `deleteExam?`, add the three mocks, register them, and return them:
```typescript
  const listExamsUc = { run: jest.fn().mockResolvedValue(over.list ?? { ok: true, data: [VITALS, VITALS2] }) };
  const updateExamUc = { run: jest.fn().mockResolvedValue(over.updateExam ?? { ok: true, data: VITALS }) };
  const deleteExamUc = { run: jest.fn().mockResolvedValue(over.deleteExam ?? { ok: true, data: undefined }) };
  // …in providers:
    { provide: ListMedicalExamsUseCase, useValue: listExamsUc },
    { provide: UpdateMedicalExamUseCase, useValue: updateExamUc },
    { provide: DeleteMedicalExamUseCase, useValue: deleteExamUc },
  // …in the return:
  return { vm: TestBed.inject(SwimmerProfileViewModel), getUc, updateUc, createUc, listExamsUc, updateExamUc, deleteExamUc, notify };
```
(Type the `over` param as `{ profile?: unknown; update?: unknown; create?: unknown; list?: unknown; updateExam?: unknown; deleteExam?: unknown; role?: 'head_coach' | 'captain' | null }`.)

In the existing test **`saveVitals records a new exam…`**, replace `await vm.startEditVitals();` with `await vm.startNewExam();`.

Add these tests:
```typescript
  it('loadExams populates the list and selects the latest', async () => {
    const { vm } = build();
    await vm.load('s1');
    expect(vm.exams()).toHaveLength(2);
    expect(vm.selectedExamId()).toBe('e1');
    expect(vm.selectedExam()?.id).toBe('e1');
  });

  it('changing selectedExamId updates selectedExam', async () => {
    const { vm } = build();
    await vm.load('s1');
    vm.selectedExamId.set('e2');
    expect(vm.selectedExam()?.id).toBe('e2');
  });

  it('startEditExam + saveVitals updates the selected exam', async () => {
    const { vm, updateExamUc, notify } = build();
    await vm.load('s1');
    await vm.startEditExam();
    expect(vm.editingExamId()).toBe('e1');
    await vm.saveVitals();
    expect(updateExamUc.run).toHaveBeenCalled();
    expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.examUpdated');
    expect(vm.editingVitals()).toBe(false);
  });

  it('delete flow confirms, deletes, toasts and reloads', async () => {
    const { vm, deleteExamUc, listExamsUc, notify } = build();
    await vm.load('s1');
    vm.askDeleteExam();
    expect(vm.confirmingDelete()).toBe(true);
    await vm.confirmDeleteExam();
    expect(deleteExamUc.run).toHaveBeenCalledWith({ id: 's1', examId: 'e1' });
    expect(notify.success).toHaveBeenCalledWith('swimmerProfile.toasts.examDeleted');
    expect(vm.confirmingDelete()).toBe(false);
    expect(listExamsUc.run).toHaveBeenCalledTimes(2); // initial load + after delete
  });
```

- [ ] **Step 2: Run to verify failure**

Run: `npx jest swimmer-profile.viewmodel`
Expected: FAIL — new use-cases/methods don't exist; `startEditVitals` removed.

- [ ] **Step 3: Implement the viewmodel changes**

Add imports + injections:
```typescript
import { ListMedicalExamsUseCase } from '@features/swimmer-profile/domain/usecases/list-medical-exams.use-case';
import { UpdateMedicalExamUseCase } from '@features/swimmer-profile/domain/usecases/update-medical-exam.use-case';
import { DeleteMedicalExamUseCase } from '@features/swimmer-profile/domain/usecases/delete-medical-exam.use-case';
// … in the class:
  private readonly listExamsUc = inject(ListMedicalExamsUseCase);
  private readonly updateExamUc = inject(UpdateMedicalExamUseCase);
  private readonly deleteExamUc = inject(DeleteMedicalExamUseCase);
```

Add state (near the other vitals signals):
```typescript
  readonly exams = signal<SwimmerVitals[]>([]);
  readonly selectedExamId = signal('');
  readonly loadingExams = signal(false);
  readonly selectedExam = computed(() =>
    this.exams().find((e) => e.id === this.selectedExamId()) ?? this.exams()[0] ?? null);
  readonly editingExamId = signal<string | null>(null);
  readonly confirmingDelete = signal(false);
  readonly deleting = signal(false);
```
Add the import for `SwimmerVitals` (already imported alongside `SwimmerProfile` — extend the import: `import { SwimmerProfile, SwimmerVitals } from '@features/swimmer-profile/domain/model/swimmer-profile';`).

In `load()`, after `this.profile.set(r.data);` add `await this.loadExams();`.

Add `loadExams()`:
```typescript
  private async loadExams(): Promise<void> {
    this.loadingExams.set(true);
    const r = await this.listExamsUc.run(this.swimmerId);
    this.loadingExams.set(false);
    if (r.ok) {
      this.exams.set(r.data);
      this.selectedExamId.set(r.data[0]?.id ?? '');
    } else {
      this.exams.set([]);
      this.selectedExamId.set('');
    }
  }
```

Replace `startEditVitals()` and `cancelEditVitals()` with:
```typescript
  async startNewExam(): Promise<void> {
    await this.ensureLookups();
    this.editingExamId.set(null);
    this.vExamDate.set(new Date().toISOString().slice(0, 10));
    this.vBloodTypeId.set('');
    this.vHemoglobin.set(''); this.vHeightCm.set(''); this.vWeightKg.set('');
    this.vInternalMedId.set(''); this.vHeartAssessId.set(''); this.vSpineAssessId.set('');
    this.editingVitals.set(true);
  }

  async startEditExam(): Promise<void> {
    const v = this.selectedExam();
    if (!v) return;
    await this.ensureLookups();
    this.editingExamId.set(v.id);
    this.vExamDate.set(v.examDate);
    this.vBloodTypeId.set(v.bloodType?.id ?? '');
    this.vHemoglobin.set(String(v.hemoglobin)); this.vHeightCm.set(String(v.heightCm)); this.vWeightKg.set(String(v.weightKg));
    this.vInternalMedId.set(v.internalMed.id); this.vHeartAssessId.set(v.heartAssess.id); this.vSpineAssessId.set(v.spineAssess.id);
    this.editingVitals.set(true);
  }

  cancelEditVitals(): void { this.editingVitals.set(false); this.editingExamId.set(null); }
```

Replace the body of `saveVitals()` to branch on `editingExamId`:
```typescript
  async saveVitals(): Promise<void> {
    if (!this.canSaveVitals() || this.savingVitals()) return;
    this.savingVitals.set(true);
    const rq = {
      examDate: this.vExamDate(),
      bloodTypeId: this.vBloodTypeId() || null,
      hemoglobin: Number(this.vHemoglobin()), heightCm: Number(this.vHeightCm()), weightKg: Number(this.vWeightKg()),
      internalMedId: this.vInternalMedId(), heartAssessId: this.vHeartAssessId(), spineAssessId: this.vSpineAssessId(),
    };
    const examId = this.editingExamId();
    const r = examId
      ? await this.updateExamUc.run({ id: this.swimmerId, examId, rq })
      : await this.createExamUc.run({ id: this.swimmerId, rq });
    this.savingVitals.set(false);
    if (r.ok) {
      this.notify.success(this.i18n.t(examId ? 'swimmerProfile.toasts.examUpdated' : 'swimmerProfile.toasts.examRecorded'));
      this.editingVitals.set(false);
      this.editingExamId.set(null);
      await this.loadExams();
      this.selectedExamId.set(r.data.id);
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.saveFailed'));
    }
  }
```

Add the delete flow:
```typescript
  askDeleteExam(): void { this.confirmingDelete.set(true); }
  cancelDelete(): void { this.confirmingDelete.set(false); }

  async confirmDeleteExam(): Promise<void> {
    const v = this.selectedExam();
    if (!v || this.deleting()) return;
    this.deleting.set(true);
    const r = await this.deleteExamUc.run({ id: this.swimmerId, examId: v.id });
    this.deleting.set(false);
    if (r.ok) {
      this.notify.success(this.i18n.t('swimmerProfile.toasts.examDeleted'));
      this.confirmingDelete.set(false);
      await this.loadExams();
    } else {
      this.notify.error(this.i18n.t('swimmerProfile.toasts.deleteFailed'));
    }
  }
```

> Note: `startEditVitals()` is removed. The page still references it until Task 9 rewires the template, so `npm run build` will be red between this task and Task 9 — that is expected; this task's gate is the viewmodel jest spec (Jest/Babel does not type-check the template). Task 9 restores a green build.

- [ ] **Step 4: Run to verify pass**

Run: `npx jest swimmer-profile.viewmodel`
Expected: PASS (existing + new tests).

- [ ] **Step 5: Commit** (after user authorizes)

```bash
git add frontend/src/app/features/swimmer-profile/
git commit -m "feat(swimmer-profile): viewmodel exam list, selection, edit/delete"
```

---

## Task 9: Vitals section template + i18n

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.html` (the Vitals `<section>`)
- Modify: `frontend/src/app/core/i18n/en.json` (swimmerProfile block)
- Modify: `frontend/src/app/core/i18n/ar.json` (swimmerProfile block)

**Interfaces:**
- Consumes: the viewmodel surface from Task 8.

> No unit test (page logic lives in the viewmodel). Gate: a clean `npm run build`.

- [ ] **Step 1: Replace the Vitals `<section>`**

Replace the entire block from `<!-- Vitals section -->` through its closing `</section>` (the last section before the final `}`/`</div>`) with:

```html
    <!-- Vitals section -->
    <section class="rounded-2xl border border-border bg-surface p-6 shadow-sm">
      <div class="mb-4 flex flex-wrap items-center justify-between gap-3 border-b border-border pb-2">
        <h2 class="font-heading text-lg text-ink">{{ 'swimmerProfile.sections.vitals' | translate }}</h2>
        <div class="flex flex-wrap items-center gap-3">
          @if (vm.exams().length > 0 && !vm.editingVitals()) {
            <label class="flex items-center gap-2 text-sm text-text-secondary">
              <span>{{ 'swimmerProfile.selectExam' | translate }}</span>
              <select class="h-9 rounded-md border border-input bg-card px-2 text-sm text-ink"
                      [value]="vm.selectedExamId()" (change)="vm.selectedExamId.set($any($event.target).value)">
                @for (e of vm.exams(); track e.id; let i = $index) {
                  <option [value]="e.id">{{ e.examDate }}{{ i === 0 ? ' (' + ('swimmerProfile.latest' | translate) + ')' : '' }}</option>
                }
              </select>
            </label>
          }
          @if (vm.canEdit() && !vm.editingVitals()) {
            @if (vm.confirmingDelete()) {
              <span class="text-sm text-danger">{{ 'swimmerProfile.confirmDelete' | translate }}</span>
              <button type="button" class="text-sm font-medium text-danger disabled:opacity-50" [disabled]="vm.deleting()" (click)="vm.confirmDeleteExam()">{{ 'swimmerProfile.confirmYes' | translate }}</button>
              <button type="button" class="text-sm text-text-secondary" (click)="vm.cancelDelete()">{{ 'swimmerProfile.confirmNo' | translate }}</button>
            } @else {
              <button type="button" class="text-sm font-medium text-primary" (click)="vm.startNewExam()">{{ 'swimmerProfile.recordExam' | translate }}</button>
              @if (vm.selectedExam()) {
                <button type="button" class="text-sm font-medium text-primary" (click)="vm.startEditExam()">{{ 'swimmerProfile.editExam' | translate }}</button>
                <button type="button" class="text-sm font-medium text-danger" (click)="vm.askDeleteExam()">{{ 'swimmerProfile.delete' | translate }}</button>
              }
            }
          }
        </div>
      </div>

      @if (vm.editingVitals()) {
        <form class="grid grid-cols-1 gap-5 md:grid-cols-3" (submit)="$event.preventDefault(); vm.saveVitals()">
          <p class="md:col-span-3 text-sm font-semibold text-ink">{{ (vm.editingExamId() ? 'swimmerProfile.editExam' : 'swimmerProfile.recordExam') | translate }}</p>
          <label class="flex flex-col gap-1">
            <span class="text-sm font-bold text-ink">{{ 'swimmerProfile.fields.examDate' | translate }}</span>
            <input type="date" class="h-10 rounded-md border border-input bg-card px-3 text-sm text-ink" [value]="vm.vExamDate()" (change)="vm.vExamDate.set($any($event.target).value)" />
          </label>
          <app-select-field [label]="'swimmerProfile.fields.bloodType' | translate" [placeholder]="'swimmerProfile.placeholders.optional' | translate" [options]="vm.bloodTypes()" [value]="vm.vBloodTypeId()" (valueChange)="vm.vBloodTypeId.set($event)"></app-select-field>
          <app-text-field [label]="'swimmerProfile.fields.hemoglobin' | translate" [value]="vm.vHemoglobin()" (valueChange)="vm.vHemoglobin.set($event)"></app-text-field>
          <app-text-field [label]="'swimmerProfile.fields.height' | translate" [value]="vm.vHeightCm()" (valueChange)="vm.vHeightCm.set($event)"></app-text-field>
          <app-text-field [label]="'swimmerProfile.fields.weight' | translate" [value]="vm.vWeightKg()" (valueChange)="vm.vWeightKg.set($event)"></app-text-field>
          <app-select-field [label]="'swimmerProfile.fields.internalMed' | translate" [placeholder]="'swimmerProfile.placeholders.assessment' | translate" [options]="vm.fitnessAssessments()" [value]="vm.vInternalMedId()" (valueChange)="vm.vInternalMedId.set($event)"></app-select-field>
          <app-select-field [label]="'swimmerProfile.fields.heart' | translate" [placeholder]="'swimmerProfile.placeholders.assessment' | translate" [options]="vm.fitnessAssessments()" [value]="vm.vHeartAssessId()" (valueChange)="vm.vHeartAssessId.set($event)"></app-select-field>
          <app-select-field [label]="'swimmerProfile.fields.spine' | translate" [placeholder]="'swimmerProfile.placeholders.assessment' | translate" [options]="vm.fitnessAssessments()" [value]="vm.vSpineAssessId()" (valueChange)="vm.vSpineAssessId.set($event)"></app-select-field>
          <div class="flex items-end gap-2 md:col-span-3">
            <button type="submit" class="rounded-md bg-primary px-5 py-2.5 text-sm font-semibold text-white disabled:opacity-50" [disabled]="!vm.canSaveVitals() || vm.savingVitals()">{{ 'common.save' | translate }}</button>
            <button type="button" class="rounded-md px-5 py-2.5 text-sm text-text-secondary" (click)="vm.cancelEditVitals()">{{ 'common.cancel' | translate }}</button>
          </div>
        </form>
      } @else if (vm.selectedExam(); as v) {
        <dl class="grid grid-cols-1 gap-y-5 gap-x-10 md:grid-cols-3">
          <div><dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.fields.bloodType' | translate }}</dt><dd class="text-ink">{{ refLabel(v.bloodType) }}</dd></div>
          <div><dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.fields.height' | translate }}</dt><dd class="text-ink">{{ v.heightCm }} cm</dd></div>
          <div><dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.fields.weight' | translate }}</dt><dd class="text-ink">{{ v.weightKg }} kg</dd></div>
          <div><dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.fields.hemoglobin' | translate }}</dt><dd class="text-ink">{{ v.hemoglobin }} g/dL</dd></div>
          <div><dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.fields.examDate' | translate }}</dt><dd class="text-ink">{{ v.examDate }}</dd></div>
          <div><dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.fields.internalMed' | translate }}</dt><dd class="text-ink">{{ refLabel(v.internalMed) }}</dd></div>
          <div><dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.fields.heart' | translate }}</dt><dd class="text-ink">{{ refLabel(v.heartAssess) }}</dd></div>
          <div><dt class="text-sm font-bold text-text-secondary">{{ 'swimmerProfile.fields.spine' | translate }}</dt><dd class="text-ink">{{ refLabel(v.spineAssess) }}</dd></div>
        </dl>
      } @else {
        <p class="text-sm text-text-secondary">{{ 'swimmerProfile.states.noExam' | translate }}</p>
      }
    </section>
```

- [ ] **Step 2: Add the i18n keys**

In `en.json`, inside the `swimmerProfile` object, add the top-level keys (next to `recordExam`) and the `toasts` entries:
```json
    "selectExam": "Examination date",
    "latest": "latest",
    "editExam": "Edit exam",
    "delete": "Delete",
    "confirmDelete": "Delete this exam?",
    "confirmYes": "Yes",
    "confirmNo": "No",
```
```json
    "toasts": {
      "identityUpdated": "Identity updated",
      "examRecorded": "Exam recorded",
      "examUpdated": "Exam updated",
      "examDeleted": "Exam deleted",
      "saveFailed": "Couldn't save. Please try again.",
      "deleteFailed": "Couldn't delete. Please try again."
    }
```

In `ar.json`, inside `swimmerProfile`, add:
```json
    "selectExam": "تاريخ الفحص",
    "latest": "الأحدث",
    "editExam": "تعديل الفحص",
    "delete": "حذف",
    "confirmDelete": "حذف هذا الفحص؟",
    "confirmYes": "نعم",
    "confirmNo": "لا",
```
```json
    "toasts": {
      "identityUpdated": "تم تحديث بيانات الهوية",
      "examRecorded": "تم تسجيل الفحص",
      "examUpdated": "تم تحديث الفحص",
      "examDeleted": "تم حذف الفحص",
      "saveFailed": "تعذّر الحفظ. حاول مرة أخرى.",
      "deleteFailed": "تعذّر الحذف. حاول مرة أخرى."
    }
```
(Preserve valid JSON — merge into the existing `toasts` object rather than duplicating it; keep the existing `identityUpdated`/`examRecorded`/`saveFailed` values.)

- [ ] **Step 3: Verify the build + full frontend suite**

Run (from `frontend/`):
```
npm run build
npx jest
```
Expected: build succeeds (template type-checks; `startEditVitals` no longer referenced); all jest suites pass.

- [ ] **Step 4: Commit** (after user authorizes)

```bash
git add frontend/src/app/features/swimmer-profile/ frontend/src/app/core/i18n/en.json frontend/src/app/core/i18n/ar.json
git commit -m "feat(swimmer-profile): exam history dropdown + edit/delete UI + i18n"
```

---

## Task 10: Full verification

- [ ] **Step 1: Run the full suites** (stop any running backend first)

Backend:
```
dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests
dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests
dotnet test backend/tests/Kheprx.BaseBackend.ArchitectureTests
```
Frontend (from `frontend/`):
```
npx jest
npm run build
```
Expected: all green; build succeeds.

- [ ] **Step 2: Manual smoke (optional, requires DB)**

With the app running and logged in as Head Coach, open a swimmer with ≥2 exams: the Vitals dropdown lists all exam dates (latest marked); picking one shows its values; **Edit** corrects a field (incl. date) and the row re-selects after reorder; **Record exam** adds a new one; **Delete** (inline confirm) removes one and selection falls to the new latest. A swimmer with no exams shows the empty state; Record still works.

- [ ] **Step 3: Commit** (after user authorizes) — nothing new to commit if Tasks 1–9 were committed; otherwise commit any residual.

---

## Self-Review (completed during planning)

**1. Spec coverage** — every spec section maps to a task:
- §3.1 entity `Update` → Task 1; read model/DTO `Id` + `MapVitals` → Task 2; repo list/get-tracked/remove → Task 3; service list/update/delete → Task 4; controller endpoints + messages → Task 5.
- §4.1 model/DTO + shared mapper → Task 6; §4.2 repo + use-cases → Task 7; §4.3 viewmodel → Task 8; §4.4 page + §4.5 i18n → Task 9.
- §6 auth → Task 5 attributes + Task 8 `canEdit` gating. §7 edge cases → ownership 404 (Tasks 4/5), reorder-by-id (Task 8 `selectedExam`/`saveVitals`), delete-selection fallback (Task 8 `loadExams`), empty state (Task 9). §8 testing → each task's tests + Task 10.
- §2 decisions: Approach A (Task 5 GET list; profile read untouched), edit incl. date + reuse `CreateMedicalExamRequest` (Tasks 4/5), ownership guard (Task 4), expose id (Tasks 2/6), inline confirm (Tasks 8/9), list-driven section (Tasks 8/9).

**2. Placeholder scan** — no TBD/TODO; every step carries real code or an exact command.

**3. Type consistency** — verified across tasks: `MedicalExamRow`/`SwimmerVitalsDto` gain `Id` as the first field (Task 2) and every construction site (Tasks 2/4/5 tests) matches; service signatures (`ListExamsAsync?`, `UpdateExamAsync`, `DeleteExamAsync`) consistent between interface, impl, controller, and tests; FE `SwimmerVitals.id` + `toVitals` used by list/update/create/profile; repo methods (`listExams`/`updateExam`/`deleteExam`) and use-case inputs (`{id, examId, rq}` / `{id, examId}`) match producer and consumer; viewmodel surface used by the page (Task 9) matches Task 8's produced members; `startEditVitals` removed in Task 8 and no longer referenced after Task 9.

---

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-09-19-swimmer-exam-history-edit.md`.** Two execution options:

**1. Subagent-Driven (recommended)** — a fresh subagent per task, review between tasks, fast iteration.

**2. Inline Execution** — execute tasks in this session using executing-plans, batch execution with checkpoints.

**Note:** per your standing instruction I have **not** committed the plan or any prior work. When we execute, I'll confirm before the first `git commit`. One sequencing caveat baked into the plan: `npm run build` is intentionally red between Task 8 (viewmodel drops `startEditVitals`) and Task 9 (template rewired) — Task 8's gate is its jest spec, Task 9 restores the green build.

**Which approach?**
