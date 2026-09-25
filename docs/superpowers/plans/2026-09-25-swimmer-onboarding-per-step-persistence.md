# Swimmer Onboarding — Per-Step Persistence (Idempotent) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (chosen) to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Save each onboarding wizard step to the DB when its "Next" is clicked (server is the validation gate), and make every step endpoint idempotent so Back→edit→Next and resume re-runs never create duplicate rows.

**Architecture:** Reverse the deferred-latched end-submit. The frontend viewmodel becomes per-step "save-then-advance": each step POSTs its own endpoint; a 400 keeps the swimmer on the step and shows the *specific* server message; success advances. Each of the four backend write paths becomes "update the latest onboarding row if it exists, else insert" (guardians already upsert; observations become a delete-by-swimmer-then-insert replace). Completion (`IsFirstLogin` clear) stays at the final InBody step — the single unchanged commit point.

**Tech Stack:** Backend — .NET / C#, EF Core (Npgsql), xUnit + Moq (mock-based unit tests; repository implementations are NOT unit-tested), FluentValidation. Clean architecture (Domain / Application / Infrastructure per module: Identity, Health). Frontend — Angular standalone + signals, Jest, clean-architecture feature layout (domain / data / presentation), Result<T> use-cases.

**Spec:** `docs/superpowers/specs/2026-09-25-swimmer-onboarding-per-step-persistence-design.md`

## Global Constraints

- **Branch `feat/championships`; STAGE-ONLY — never commit.** The whole session is staged-not-committed; the user integrates themselves. Every task's final step is `git add <files>` with **no `git commit`**.
- **No DB migration.** Uses existing tables only (`athlete.body_measurement`, `athlete.medical_exam`, `athlete.guardian`, `health.observation`, `health.inbody_reading`).
- **Server is the validation gate.** No client-side rule duplication; the existing FluentValidation validators (`CompleteIdentityVitalsRequestValidator`, `CompleteGuardianMedicalRequestValidator`, `CompletePhysiologicalRequestValidator`, `CompleteInBodyRequestValidator`) are unchanged and remain the gate. `canSubmitStepN` on the frontend only enables the button (required-field presence).
- **Idempotency mechanism = "update the latest row if it exists, else insert."** Safe because onboarding is first-login → the swimmer has at most one of each onboarding row. Guardians are already upsert (no change). Observations use **replace** = delete the swimmer's observations, then insert the current "Yes" set.
- **`IsFirstLogin` clears ONLY at the final InBody step** (unchanged commit point, `CompleteOnboardingAsync`). Abandoning mid-wizard leaves first-login true → next login resumes at Step 1.
- **Only Step 1 prefills.** Steps 2–4 are re-entered on resume and idempotently overwritten (no prefill).
- **Test style:** backend unit tests use `Mock<T>` on interfaces + `.Verify(...)` (see `SwimmerServiceTests`, `ObservationServiceTests`, `SwimmersControllerTests`); a single `SaveChangesAsync` per service operation. Frontend tests are Jest with `TestBed` and `ok(...)`/`fail(...)` from `@core/domain/result/result`.
- **`dotnet test` requires the running API to be stopped** (DLL lock) — ensure no `dotnet run` / watch is holding the build output before running backend tests.

## Review Focus

- **Pre-existing observations are deleted by the delete-by-swimmer replace (potential data loss).** If the captain added any `health.observation` rows for the swimmer *before* first login (e.g. via the Captain Panel / Records tab), the Step-2 replace removes them along with onboarding rows. The approved spec chose delete-by-swimmer on the first-login assumption "the swimmer has at most one of each onboarding row." Task 3 pins this behavior with a test; **flagged for user confirmation at the review gate** (scoping the delete to the four onboarding categories is the alternative).
- **Toggling a medical answer Yes→No on resume must remove the stale "Yes" observation.** Because the frontend only sends the current "Yes" set, Step 2 must call the replace **even when the medical list is empty** (empty replace = delete all, insert none). Task 3 controller test pins that `ReplaceForSwimmerAsync` is invoked with an empty item list.
- **A server 400 must surface the *specific* message to the swimmer, not a generic toast.** `describeError` returns `err.code` (the backend's joined field messages) on `status === 400 && code`, else the generic `onboarding.saveFailed`. Task 5 viewmodel test pins both branches.
- **Guardian-medical spans two DbContexts (Identity guardians + Health observations) with no distributed transaction.** If the observation replace fails after guardians are upserted, guardians persist but observations don't; the swimmer stays on Step 2 and retries. Idempotency makes the retry safe (guardians re-upsert, observations re-replace). Task 3 pins that a second identical call does not duplicate.
- **Step Back must NOT save; only Next saves.** `backToStepN` remains pure navigation (and clears the inline error); re-entering a step and clicking Next re-POSTs, relying on server idempotency. Task 5 viewmodel test pins that re-clicking a step's save re-invokes the use case (no latch) and that Back does not call any use case.

---

### Task 1: Physiological (Step 3) — update-latest-body-measurement-or-insert

Make `POST me/onboarding/physiological` idempotent: update the swimmer's latest `body_measurement` row if one exists, else insert. Body measurement currently only inserts, so this task adds a domain `Update` method and a tracked "get latest" repository method.

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/BodyMeasurement.cs` (add `Update`)
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/ISwimmerProfileRepository.cs` (add `GetLatestBodyMeasurementTrackedAsync`)
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/SwimmerProfileRepository.cs` (implement it)
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs` (`CompleteOnboardingPhysiologicalAsync`)
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/BodyMeasurementTests.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs`

**Interfaces:**
- Consumes: existing `ISwimmerProfileRepository.GetByUserIdTrackedAsync`, `AddBodyMeasurementAsync`, `SaveChangesAsync`.
- Produces:
  - `BodyMeasurement.Update(decimal rightArmCm, decimal leftArmCm, decimal rightLegCm, decimal leftLegCm, decimal torsoCm, decimal bustDiameterCm, decimal waistDiameterCm)` — mutates the 7 values and refreshes `MeasuredAt` to today.
  - `Task<BodyMeasurement?> ISwimmerProfileRepository.GetLatestBodyMeasurementTrackedAsync(Guid swimmerId, CancellationToken ct = default)` — tracked, newest first.

- [ ] **Step 1: Write the failing domain test** — append to `BodyMeasurementTests.cs`:

```csharp
    [Fact]
    public void Update_overwrites_values_and_refreshes_measured_at()
    {
        var m = new BodyMeasurement(Guid.NewGuid(), 1m, 2m, 3m, 4m, 5m, 6m, 7m);

        m.Update(78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m);

        Assert.Equal(78.5m, m.RightArmCm);
        Assert.Equal(78.2m, m.LeftArmCm);
        Assert.Equal(96.2m, m.RightLegCm);
        Assert.Equal(96.0m, m.LeftLegCm);
        Assert.Equal(52.8m, m.TorsoCm);
        Assert.Equal(94.0m, m.BustDiameterCm);
        Assert.Equal(76.5m, m.WaistDiameterCm);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), m.MeasuredAt);
    }
```

- [ ] **Step 2: Run it to confirm it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~BodyMeasurementTests`
Expected: FAIL — `BodyMeasurement` does not contain a definition for `Update`.

- [ ] **Step 3: Add the `Update` method** to `BodyMeasurement.cs` (after the public constructor):

```csharp
    public void Update(decimal rightArmCm, decimal leftArmCm, decimal rightLegCm,
        decimal leftLegCm, decimal torsoCm, decimal bustDiameterCm, decimal waistDiameterCm)
    {
        MeasuredAt = DateOnly.FromDateTime(DateTime.UtcNow);
        RightArmCm = rightArmCm;
        LeftArmCm = leftArmCm;
        RightLegCm = rightLegCm;
        LeftLegCm = leftLegCm;
        TorsoCm = torsoCm;
        BustDiameterCm = bustDiameterCm;
        WaistDiameterCm = waistDiameterCm;
    }
```

- [ ] **Step 4: Run the domain test to confirm it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~BodyMeasurementTests`
Expected: PASS.

- [ ] **Step 5: Add the tracked repository method (interface + impl)**

In `ISwimmerProfileRepository.cs`, add next to `GetLatestBodyMeasurementAsync`:

```csharp
    Task<BodyMeasurement?> GetLatestBodyMeasurementTrackedAsync(Guid swimmerId, CancellationToken ct = default);
```

In `SwimmerProfileRepository.cs`, add next to `GetLatestBodyMeasurementAsync` (tracked — no `AsNoTracking`, same ordering):

```csharp
    public Task<BodyMeasurement?> GetLatestBodyMeasurementTrackedAsync(Guid swimmerId, CancellationToken ct = default)
        => _db.BodyMeasurements
              .Where(m => m.SwimmerId == swimmerId)
              .OrderByDescending(m => m.MeasuredAt).ThenByDescending(m => m.Id)
              .FirstOrDefaultAsync(ct);
```

- [ ] **Step 6: Write the failing service idempotency test** — in `SwimmerServiceTests.cs`, under the "Onboarding Step 3 (physiological)" region, add:

```csharp
    [Fact]
    public async Task CompleteOnboardingPhysiological_updates_latest_measurement_when_one_exists()
    {
        var (svc, swimmers, _) = Build();
        var userId = Guid.NewGuid();
        var profile = new SwimmerProfile(userId, "SW-0001", Guid.NewGuid());
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        var existing = new BodyMeasurement(profile.Id, 1m, 2m, 3m, 4m, 5m, 6m, 7m);
        swimmers.Setup(r => r.GetLatestBodyMeasurementTrackedAsync(profile.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var ok = await svc.CompleteOnboardingPhysiologicalAsync(userId, new CompletePhysiologicalRequest(32.5m, 31.0m, 95m, 94m, 60m, 90m, 75m));

        Assert.True(ok);
        Assert.Equal(32.5m, existing.RightArmCm);            // updated in place
        Assert.Equal(75m, existing.WaistDiameterCm);
        swimmers.Verify(r => r.AddBodyMeasurementAsync(It.IsAny<BodyMeasurement>(), It.IsAny<CancellationToken>()), Times.Never);
        swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
```

Also update the existing `CompleteOnboardingPhysiological_stores_all_seven_limbs_and_keeps_first_login` test to make the insert branch explicit (add the following setup line after the `GetByUserIdTrackedAsync` setup):

```csharp
        swimmers.Setup(r => r.GetLatestBodyMeasurementTrackedAsync(profile.Id, It.IsAny<CancellationToken>())).ReturnsAsync((BodyMeasurement?)null);
```

- [ ] **Step 7: Run to confirm the new test fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~CompleteOnboardingPhysiological`
Expected: FAIL — the service still always inserts, so the "updates in place / Add Never" assertions fail.

- [ ] **Step 8: Rewrite `CompleteOnboardingPhysiologicalAsync`** in `SwimmerService.cs` to update-or-insert:

```csharp
    public async Task<bool> CompleteOnboardingPhysiologicalAsync(
        Guid userId, CompletePhysiologicalRequest req, CancellationToken ct = default)
    {
        var profile = await _swimmers.GetByUserIdTrackedAsync(userId, ct);
        if (profile is null) return false;

        // Idempotent per-step save: update the swimmer's latest measurement if one exists
        // (Back→edit→Next / resume re-run), else insert a new dated row.
        var existing = await _swimmers.GetLatestBodyMeasurementTrackedAsync(profile.Id, ct);
        if (existing is null)
        {
            var measurement = new BodyMeasurement(profile.Id,
                req.RightArmCm, req.LeftArmCm, req.RightLegCm, req.LeftLegCm, req.TorsoCm, req.BustDiameterCm, req.WaistDiameterCm);
            await _swimmers.AddBodyMeasurementAsync(measurement, ct);
        }
        else
        {
            existing.Update(req.RightArmCm, req.LeftArmCm, req.RightLegCm, req.LeftLegCm, req.TorsoCm, req.BustDiameterCm, req.WaistDiameterCm);
        }

        // Onboarding is NOT completed here — Step 4 (InBody) is the finish line.
        await _swimmers.SaveChangesAsync(ct);
        return true;
    }
```

- [ ] **Step 9: Run the physiological tests to confirm they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~CompleteOnboardingPhysiological`
Expected: PASS (both the insert-branch and update-branch tests).

- [ ] **Step 10: Run the full Identity unit-test project**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests`
Expected: PASS (all green).

- [ ] **Step 11: Stage (no commit)**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Entities/BodyMeasurement.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/ISwimmerProfileRepository.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/SwimmerProfileRepository.cs \
        backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Entities/BodyMeasurementTests.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs
```

---

### Task 2: Identity-Vitals (Step 1) — update-latest-exam-or-insert + stale-comment fix

Make `POST me/onboarding/identity-vitals` idempotent: keep the identity `UpdateProfile`, then update the swimmer's latest medical exam if one exists, else insert. Reuses the existing `GetLatestExamAsync` / `GetExamTrackedAsync` / `MedicalExam.Update` — no new repository methods.

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs` (`CompleteIdentityVitalsAsync`)
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs` (fix the stale `<summary>` on the identity-vitals endpoint)
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs`

**Interfaces:**
- Consumes: existing `ISwimmerProfileRepository.GetLatestExamAsync(swimmerId)` → `MedicalExamRow?` (has `.Id`, newest first), `GetExamTrackedAsync(examId)` → `MedicalExam?`, `AddExamAsync`, `SaveChangesAsync`; `MedicalExam.Update(examDate, internalMedId, heartAssessId, spineAssessId, bloodTypeId, hemoglobin, heightCm, weightKg)`.
- Produces: no new signatures (behavior change only).

- [ ] **Step 1: Write the failing service idempotency test** — in `SwimmerServiceTests.cs`, under the "CompleteIdentityVitals tests" region, add:

```csharp
    [Fact]
    public async Task CompleteIdentityVitals_updates_latest_exam_when_one_exists()
    {
        var (svc, swimmers, users) = Build();
        var userId = Guid.NewGuid();
        var profile = new SwimmerProfile(userId, "SW-0001", Guid.NewGuid());
        swimmers.Setup(r => r.GetByUserIdTrackedAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new AppUser("s", "S", Guid.NewGuid(), email: "s@x.io", isFirstLogin: true));

        var examId = Guid.NewGuid();
        var im = Guid.NewGuid();
        swimmers.Setup(r => r.GetLatestExamAsync(profile.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MedicalExamRow(examId, new DateOnly(2020, 1, 1), 10m, 100m, 40m,
                    null, null, null, null, im, "fit", "Fit", "لائق", im, "fit", "Fit", "لائق", im, "fit", "Fit", "لائق"));
        var trackedExam = new MedicalExam(profile.Id, new DateOnly(2020, 1, 1), im, im, im, null, 10m, 100m, 40m);
        swimmers.Setup(r => r.GetExamTrackedAsync(examId, It.IsAny<CancellationToken>())).ReturnsAsync(trackedExam);

        var result = await svc.CompleteIdentityVitalsAsync(userId, CompleteReq());

        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2026, 1, 1), trackedExam.ExamDate);   // updated in place (CompleteReq ExamDate)
        Assert.Equal(14.5m, trackedExam.Hemoglobin);
        swimmers.Verify(r => r.AddExamAsync(It.IsAny<MedicalExam>(), It.IsAny<CancellationToken>()), Times.Never);
        swimmers.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
```

(The existing `CompleteIdentityVitals_updates_identity_inserts_exam_and_keeps_first_login` test already exercises the insert branch: `GetLatestExamAsync` is unset, so Moq returns `null`. Leave it as-is; it still verifies `AddExamAsync ... Times.Once`.)

- [ ] **Step 2: Run to confirm it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~CompleteIdentityVitals`
Expected: FAIL — the service still always inserts, so `AddExamAsync ... Never` and the "updated in place" assertions fail.

- [ ] **Step 3: Rewrite the exam persistence in `CompleteIdentityVitalsAsync`** — replace the current block (the `AddExamAsync(new MedicalExam(...))` call and its `// (removed) ...` comment) with:

```csharp
        // Vitals (idempotent per-step save): update the swimmer's latest exam if one exists
        // (Back→edit→Next / resume re-run), else insert a new dated exam.
        var latestExam = await _swimmers.GetLatestExamAsync(profile.Id, ct);
        if (latestExam is null)
        {
            var exam = new MedicalExam(profile.Id, request.ExamDate, request.InternalMedId, request.HeartAssessId,
                request.SpineAssessId, request.BloodTypeId, request.Hemoglobin, request.HeightCm, request.WeightKg);
            await _swimmers.AddExamAsync(exam, ct);
        }
        else
        {
            var tracked = await _swimmers.GetExamTrackedAsync(latestExam.Id, ct);
            tracked!.Update(request.ExamDate, request.InternalMedId, request.HeartAssessId, request.SpineAssessId,
                request.BloodTypeId, request.Hemoglobin, request.HeightCm, request.WeightKg);
        }

        // Onboarding is NOT completed here — Step 4 (InBody) clears first-login.
        await _swimmers.SaveChangesAsync(ct);
        return new OnboardingStepResultDto(false);
```

Keep the identity `user.UpdateProfile(...)` and `profile.SetTrainingClub(...)` lines above unchanged.

- [ ] **Step 4: Run the identity-vitals tests to confirm they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter FullyQualifiedName~CompleteIdentityVitals`
Expected: PASS (insert branch + update branch + null-blood-type + reference-unknown).

- [ ] **Step 5: Fix the stale XML-doc comments** on the identity-vitals AND physiological endpoints in `SwimmersController.cs` (spec §5 — only the InBody step completes onboarding).

Replace the `<summary>`/`<response>` block for `CompleteOnboardingIdentityVitals`:

```csharp
    /// <summary>Saves the swimmer's own first-login Step 1 (identity + medical exam), idempotently, and advances to Step 2. Does NOT clear first-login. Swimmer only.</summary>
    /// <response code="200">Saved; first-login stays true until the InBody step completes onboarding.</response>
    /// <response code="404">The caller is not a swimmer / has no profile.</response>
```

Replace the `<summary>`/`<response>` block for `CompleteOnboardingPhysiological` (currently says "clearing first-login"):

```csharp
    /// <summary>Saves the swimmer's first-login Step 3 (physiological measurements), idempotently, and advances to Step 4. Does NOT clear first-login. Swimmer only.</summary>
    /// <response code="200">Saved; first-login stays true until the InBody step completes onboarding.</response>
    /// <response code="404">The caller is not a swimmer / has no profile.</response>
```

- [ ] **Step 6: Run the full Identity unit-test project**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests`
Expected: PASS.

- [ ] **Step 7: Stage (no commit)**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs \
        backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs \
        backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs
```

---

### Task 3: Guardian-Medical (Step 2) — replace observations (idempotent)

Guardians already upsert. Make the medical observations idempotent by **replacing** the swimmer's observations (delete-by-swimmer, then insert the current "Yes" set) instead of insert-only. Add a repository tracked-list + range-remove, an `IObservationService.ReplaceForSwimmerAsync`, and switch the controller orchestration to call it (always — even when the medical list is empty).

**Files:**
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Repositories/IObservationRepository.cs` (add `ListBySwimmerTrackedAsync`, `RemoveRange`)
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Repositories/ObservationRepository.cs` (implement them)
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/Interfaces/IObservationService.cs` (add `ReplaceForSwimmerAsync`)
- Modify: `backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/ObservationService.cs` (implement it)
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs` (`CompleteOnboardingGuardianMedical`)
- Test: `backend/tests/Kheprx.BaseBackend.Health.UnitTests/Services/ObservationServiceTests.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs`

**Interfaces:**
- Consumes: existing `IObservationRepository.AddAsync`, `SaveChangesAsync`; existing `CreateObservationRequest(Guid SwimmerId, Guid CategoryId, string FieldLabel, string Value)`; existing `Observation(Guid swimmerId, Guid categoryId, string fieldLabel, string value, Guid recordedBy)`.
- Produces:
  - `Task<IReadOnlyList<Observation>> IObservationRepository.ListBySwimmerTrackedAsync(Guid swimmerId, CancellationToken ct = default)` (tracked).
  - `void IObservationRepository.RemoveRange(IEnumerable<Observation> observations)`.
  - `Task IObservationService.ReplaceForSwimmerAsync(Guid swimmerId, IReadOnlyList<CreateObservationRequest> items, Guid recordedBy, CancellationToken ct = default)`.

- [ ] **Step 1: Write the failing service test** — append to `ObservationServiceTests.cs`:

```csharp
    [Fact]
    public async Task ReplaceForSwimmer_removes_existing_then_inserts_new_set()
    {
        var repo = new Mock<IObservationRepository>();
        var sw = Guid.NewGuid();
        var recordedBy = Guid.NewGuid();
        var stale = new List<Observation> { new(sw, Guid.NewGuid(), "Old", "Value", Guid.NewGuid()) };
        repo.Setup(r => r.ListBySwimmerTrackedAsync(sw, It.IsAny<CancellationToken>())).ReturnsAsync(stale);
        var added = new List<Observation>();
        repo.Setup(r => r.AddAsync(It.IsAny<Observation>(), It.IsAny<CancellationToken>()))
            .Callback<Observation, CancellationToken>((o, _) => added.Add(o)).Returns(Task.CompletedTask);
        var svc = new ObservationService(repo.Object);

        var cat = Guid.NewGuid();
        var items = new List<CreateObservationRequest> { new(sw, cat, "Allergies", "Peanuts") };
        await svc.ReplaceForSwimmerAsync(sw, items, recordedBy);

        repo.Verify(r => r.RemoveRange(stale), Times.Once);
        Assert.Single(added);
        Assert.Equal("Peanuts", added[0].Value);
        Assert.Equal(recordedBy, added[0].RecordedBy);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReplaceForSwimmer_with_empty_set_deletes_all_and_inserts_none()
    {
        var repo = new Mock<IObservationRepository>();
        var sw = Guid.NewGuid();
        var stale = new List<Observation> { new(sw, Guid.NewGuid(), "Old", "Value", Guid.NewGuid()) };
        repo.Setup(r => r.ListBySwimmerTrackedAsync(sw, It.IsAny<CancellationToken>())).ReturnsAsync(stale);
        var svc = new ObservationService(repo.Object);

        await svc.ReplaceForSwimmerAsync(sw, System.Array.Empty<CreateObservationRequest>(), Guid.NewGuid());

        repo.Verify(r => r.RemoveRange(stale), Times.Once);
        repo.Verify(r => r.AddAsync(It.IsAny<Observation>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
```

- [ ] **Step 2: Run to confirm it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~ReplaceForSwimmer`
Expected: FAIL — `IObservationRepository` has no `ListBySwimmerTrackedAsync`/`RemoveRange`, `ObservationService` has no `ReplaceForSwimmerAsync` (compile error).

- [ ] **Step 3: Add the repository methods (interface + impl)**

In `IObservationRepository.cs`, add:

```csharp
    Task<IReadOnlyList<Observation>> ListBySwimmerTrackedAsync(Guid swimmerId, CancellationToken ct = default);
    void RemoveRange(IEnumerable<Observation> observations);
```

In `ObservationRepository.cs`, add (tracked — no `AsNoTracking`):

```csharp
    public async Task<IReadOnlyList<Observation>> ListBySwimmerTrackedAsync(Guid swimmerId, CancellationToken ct = default)
        => await _db.Observations.Where(o => o.SwimmerId == swimmerId).ToListAsync(ct);

    public void RemoveRange(IEnumerable<Observation> observations) => _db.Observations.RemoveRange(observations);
```

- [ ] **Step 4: Add `ReplaceForSwimmerAsync` (interface + impl)**

In `IObservationService.cs`, add:

```csharp
    Task ReplaceForSwimmerAsync(Guid swimmerId, IReadOnlyList<CreateObservationRequest> items, Guid recordedBy, CancellationToken ct = default);
```

In `ObservationService.cs`, add:

```csharp
    public async Task ReplaceForSwimmerAsync(Guid swimmerId, IReadOnlyList<CreateObservationRequest> items, Guid recordedBy, CancellationToken ct = default)
    {
        // Idempotent per-step onboarding save: remove the swimmer's observations, then insert the current set.
        var existing = await _observations.ListBySwimmerTrackedAsync(swimmerId, ct);
        _observations.RemoveRange(existing);
        foreach (var item in items)
            await _observations.AddAsync(new Observation(item.SwimmerId, item.CategoryId, item.FieldLabel, item.Value, recordedBy), ct);
        await _observations.SaveChangesAsync(ct);
    }
```

- [ ] **Step 5: Run the Health service tests to confirm they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests --filter FullyQualifiedName~ObservationService`
Expected: PASS.

- [ ] **Step 6: Update the controller tests to expect the replace** — in `SwimmersControllerTests.cs`, rewrite the three guardian-medical tests:

Replace `CompleteGuardianMedical_upserts_creates_observations_and_does_not_complete` body's assertions (keep the setup that returns `swimmerId` from `UpsertOnboardingGuardiansAsync`):

```csharp
        Assert.IsType<OkObjectResult>(result.Result);
        obs.Verify(o => o.ReplaceForSwimmerAsync(
            swimmerId,
            It.Is<IReadOnlyList<CreateObservationRequest>>(list =>
                list.Count == 1 && list[0].SwimmerId == swimmerId && list[0].CategoryId == catId && list[0].Value == "Peanuts"),
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        svc.Verify(s => s.CompleteOnboardingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
```

Replace `CompleteGuardianMedical_with_empty_medical_does_not_complete` assertions — an empty medical list must still call replace (delete-all), and must not complete:

```csharp
        Assert.IsType<OkObjectResult>(result.Result);
        obs.Verify(o => o.ReplaceForSwimmerAsync(It.IsAny<Guid>(),
            It.Is<IReadOnlyList<CreateObservationRequest>>(list => list.Count == 0),
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        svc.Verify(s => s.CompleteOnboardingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
```

Replace `CompleteGuardianMedical_returns_404_when_not_a_swimmer` observation assertion:

```csharp
        obs.Verify(o => o.ReplaceForSwimmerAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<CreateObservationRequest>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
```

- [ ] **Step 7: Run to confirm the controller tests fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter FullyQualifiedName~CompleteGuardianMedical`
Expected: FAIL — controller still calls `CreateAsync` per item, not `ReplaceForSwimmerAsync`.

- [ ] **Step 8: Rewrite the controller orchestration** — in `SwimmersController.cs`, replace the medical `foreach ... CreateAsync` block in `CompleteOnboardingGuardianMedical` with:

```csharp
        // 2) Medical history (Health) — replace the swimmer's observations with the current "Yes" set (idempotent).
        var items = request.Medical
            .Select(m => new CreateObservationRequest(swimmerId.Value, m.CategoryId, m.FieldLabel, m.Value))
            .ToList();
        await _observations.ReplaceForSwimmerAsync(swimmerId.Value, items, userId, ct);
```

Also correct the stale trailing comment in that method (it currently reads "Step 3 (Physiological) is the finish line") to:

```csharp
        // Onboarding is NOT completed here — Step 4 (InBody) is the finish line.
```

- [ ] **Step 9: Run the guardian-medical controller tests to confirm they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter FullyQualifiedName~CompleteGuardianMedical`
Expected: PASS.

- [ ] **Step 10: Run the full Health + Api unit-test projects**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests && dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests`
Expected: PASS.

- [ ] **Step 11: Stage (no commit)**

```bash
git add backend/src/Modules/Health/Kheprx.BaseBackend.Health.Domain/Repositories/IObservationRepository.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Infrastructure/Repositories/ObservationRepository.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/Interfaces/IObservationService.cs \
        backend/src/Modules/Health/Kheprx.BaseBackend.Health.Application/Services/ObservationService.cs \
        backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs \
        backend/tests/Kheprx.BaseBackend.Health.UnitTests/Services/ObservationServiceTests.cs \
        backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs
```

---

### Task 4: InBody (Step 4) — update-latest-reading-or-insert

Make `POST me/onboarding/inbody` idempotent: update the swimmer's latest `inbody_reading` if one exists, else insert; then `CompleteOnboardingAsync` (unchanged — the single commit point). Reuses the existing `IInBodyReadingService.ListAsync` (newest first) / `UpdateAsync` / `CreateAsync` — no new methods.

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs` (`CompleteOnboardingInBody`)
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs`

**Interfaces:**
- Consumes: existing `IInBodyReadingService.ListAsync(swimmerId)` → `IReadOnlyList<InBodyReadingDto>` (newest first; `InBodyReadingDto` first field is `Id`), `UpdateAsync(swimmerId, readingId, CreateInBodyReadingRequest)`, `CreateAsync(swimmerId, CreateInBodyReadingRequest, recordedBy)`; existing `ISwimmerService.CompleteOnboardingAsync`.
- Produces: no new signatures (behavior change only).

- [ ] **Step 1: Update the existing insert-branch test + write the failing update-branch test** — in `SwimmersControllerTests.cs`:

In `CompleteInBody_saves_reading_then_completes`, add an explicit empty-list setup on the inbody mock (before the call) so the insert branch is deterministic:

```csharp
        inbody.Setup(i => i.ListAsync(swimmerId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<InBodyReadingDto>());
```

Add the new update-branch test after it:

```csharp
    [Fact]
    public async Task CompleteInBody_updates_latest_reading_when_one_exists()
    {
        var svc = new Mock<ISwimmerService>();
        var swimmerId = Guid.NewGuid();
        svc.Setup(s => s.GetSwimmerIdByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(swimmerId);
        svc.Setup(s => s.CompleteOnboardingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var inbody = new Mock<IInBodyReadingService>();
        var readingId = Guid.NewGuid();
        inbody.Setup(i => i.ListAsync(swimmerId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new List<InBodyReadingDto> { new(readingId, new DateOnly(2026, 1, 1), 170m, 60m, 10m, 35m, 50m, 3m, 1m, Guid.NewGuid()) });

        var req = new CompleteInBodyRequest(175m, 68m, 15m, 40m, 55m, 3.2m, 1.05m);
        var result = await Create(svc.Object, null, inbody.Object).CompleteOnboardingInBody(req, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        inbody.Verify(i => i.UpdateAsync(swimmerId, readingId,
            It.Is<CreateInBodyReadingRequest>(r => r.HeightCm == 175m && r.WeightKg == 68m && r.WaterPct == 55m),
            It.IsAny<CancellationToken>()), Times.Once);
        inbody.Verify(i => i.CreateAsync(It.IsAny<Guid>(), It.IsAny<CreateInBodyReadingRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        svc.Verify(s => s.CompleteOnboardingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }
```

- [ ] **Step 2: Run to confirm the new test fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter FullyQualifiedName~CompleteInBody`
Expected: FAIL — controller always calls `CreateAsync`, so `UpdateAsync ... Once` / `CreateAsync ... Never` fail.

- [ ] **Step 3: Rewrite the InBody persistence** — in `SwimmersController.cs` `CompleteOnboardingInBody`, replace step "2)" (`await _inbody.CreateAsync(...)`) with:

```csharp
        // 2) Save the InBody reading (Health) — update the latest reading if one exists (idempotent
        //    Back→edit→Next / resume re-run), else insert. Date server-set to today, recorded by the swimmer.
        var reading = new CreateInBodyReadingRequest(DateOnly.FromDateTime(DateTime.UtcNow),
            request.HeightCm, request.WeightKg, request.FatPct, request.MusclePct, request.WaterPct, request.BoneDensity, request.BodyDensity);
        var existing = await _inbody.ListAsync(swimmerId.Value, ct);
        if (existing.Count > 0)
            await _inbody.UpdateAsync(swimmerId.Value, existing[0].Id, reading, ct);
        else
            await _inbody.CreateAsync(swimmerId.Value, reading, userId, ct);
```

Keep step "3)" (`await _service.CompleteOnboardingAsync(userId, ct);`) unchanged.

- [ ] **Step 4: Run the InBody controller tests to confirm they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter FullyQualifiedName~CompleteInBody`
Expected: PASS (insert branch, update branch, 404 branch).

- [ ] **Step 5: Run the full Api unit-test project**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests`
Expected: PASS.

- [ ] **Step 6: Stage (no commit)**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs \
        backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs
```

---

### Task 5: Frontend — per-step save-then-advance viewmodel + wizard wiring

Rewrite the onboarding viewmodel so each step's "Next" POSTs that step and advances on success, staying on the step and surfacing the specific server message on failure. Drop the deferred chain and the three latches. Wire the wizard HTML buttons to the per-step handlers, disable them while submitting, and show an inline per-step error. Rewrite the viewmodel spec accordingly.

**Files:**
- Modify: `frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding.viewmodel.ts`
- Modify: `frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding-wizard.page.html`
- Test: `frontend/src/app/features/swimmer-onboarding/testing/presentation/pages/onboarding/onboarding.viewmodel.spec.ts`

**Interfaces:**
- Consumes: existing use cases (unchanged) `CompleteIdentityVitalsUseCase`/`CompleteGuardianMedicalUseCase`/`CompletePhysiologicalUseCase`/`CompleteInBodyUseCase` (each `.run(input)` → `Result<void>`); `AppError` (`{ status?, code? }`); `Result<T>` (`ok`/`fail`).
- Produces (public viewmodel API used by the template):
  - `readonly stepError: Signal<string | null>`
  - `saveStep1(): Promise<void>` — POST identity-vitals; advance to 2 on ok.
  - `saveStep2(): Promise<void>` — resolve medical, guard unresolved category, POST guardian-medical; advance to 3 on ok.
  - `saveStep3(): Promise<void>` — POST physiological; advance to 4 on ok.
  - `submit(): Promise<void>` — POST inbody; on ok `markOnboardingComplete()` + navigate `/my-profile`.
  - `backToStep1/2/3(): void` — pure navigation; clears `stepError`.
  - **Removed:** `goToStep2/3/4`, `identitySaved`/`guardianSaved`/`physiologicalSaved`.

- [ ] **Step 1: Rewrite the viewmodel spec (failing) to the per-step contract** — replace the whole file `onboarding.viewmodel.spec.ts` with:

```ts
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { OnboardingViewModel } from '@features/swimmer-onboarding/presentation/pages/onboarding/onboarding.viewmodel';
import { GetOnboardingPrefillUseCase } from '@features/swimmer-onboarding/domain/usecases/get-onboarding-prefill.use-case';
import { CompleteIdentityVitalsUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-identity-vitals.use-case';
import { CompleteGuardianMedicalUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-guardian-medical.use-case';
import { CompletePhysiologicalUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-physiological.use-case';
import { CompleteInBodyUseCase } from '@features/swimmer-onboarding/domain/usecases/complete-inbody.use-case';
import { LoadClubsUseCase, LoadGendersUseCase, LoadBloodTypesUseCase } from '@features/reference';
import { LoadFitnessAssessmentsUseCase } from '@features/reference/domain/usecases/load-fitness-assessments.use-case';
import { LoadObservationCategoriesUseCase } from '@features/reference/domain/usecases/load-observation-categories.use-case';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';

const PREFILL = { uid: 'SW-1', nameEn: 'Sam', nameAr: 'سام', genderId: 'g1', dob: '2010-01-01', trainingClubId: 'c1', phone: '01099999999' };
const LOOKUP = [{ id: 'x', nameEn: 'X', nameAr: null }];
const CATS = [
  { id: 'cat-allergy', code: 'allergy', nameEn: 'Allergy', nameAr: null },
  { id: 'cat-surgery', code: 'surgery', nameEn: 'Surgery', nameAr: null },
  { id: 'cat-chronic', code: 'chronic', nameEn: 'Chronic', nameAr: null },
  { id: 'cat-autoimmune', code: 'autoimmune', nameEn: 'Autoimmune', nameAr: null },
];

function build(overrides: { identity?: unknown; guardianMedical?: unknown; physiological?: unknown; inbody?: unknown; cats?: unknown } = {}) {
  const getPrefill = { run: jest.fn().mockResolvedValue(ok(PREFILL)) };
  const identity = { run: jest.fn().mockResolvedValue(overrides.identity ?? ok(undefined)) };
  const guardianMedical = { run: jest.fn().mockResolvedValue(overrides.guardianMedical ?? ok(undefined)) };
  const physiological = { run: jest.fn().mockResolvedValue(overrides.physiological ?? ok(undefined)) };
  const inbody = { run: jest.fn().mockResolvedValue(overrides.inbody ?? ok(undefined)) };
  const lookups = { run: jest.fn().mockResolvedValue(ok(LOOKUP)) };
  const cats = { run: jest.fn().mockResolvedValue(overrides.cats ?? ok(CATS)) };
  const auth = { markOnboardingComplete: jest.fn() } as unknown as AuthSessionStore;
  const router = { navigate: jest.fn() } as unknown as Router;
  const notify = { success: jest.fn(), error: jest.fn() } as unknown as NotificationService;
  const i18n = { t: (k: string) => k } as unknown as TranslateService;
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      OnboardingViewModel,
      { provide: GetOnboardingPrefillUseCase, useValue: getPrefill },
      { provide: CompleteIdentityVitalsUseCase, useValue: identity },
      { provide: CompleteGuardianMedicalUseCase, useValue: guardianMedical },
      { provide: CompletePhysiologicalUseCase, useValue: physiological },
      { provide: CompleteInBodyUseCase, useValue: inbody },
      { provide: LoadClubsUseCase, useValue: lookups },
      { provide: LoadGendersUseCase, useValue: lookups },
      { provide: LoadBloodTypesUseCase, useValue: lookups },
      { provide: LoadFitnessAssessmentsUseCase, useValue: lookups },
      { provide: LoadObservationCategoriesUseCase, useValue: cats },
      { provide: AuthSessionStore, useValue: auth },
      { provide: Router, useValue: router },
      { provide: NotificationService, useValue: notify },
      { provide: TranslateService, useValue: i18n },
    ],
  });
  return { vm: TestBed.inject(OnboardingViewModel), identity, guardianMedical, physiological, inbody, auth, router, notify };
}

function fillStep1(vm: OnboardingViewModel) {
  vm.examDate.set('2026-01-01'); vm.internalMedId.set('f1'); vm.heartAssessId.set('f1'); vm.spineAssessId.set('f1');
  vm.hemoglobin.set('14.5'); vm.heightCm.set('175'); vm.weightKg.set('68');
}
function fillStep2(vm: OnboardingViewModel) {
  vm.fatherName.set('Ahmed'); vm.fatherNationalId.set('12345678901234'); vm.fatherPhone.set('010');
  vm.motherName.set('Sara'); vm.motherNationalId.set('43210987654321'); vm.motherPhone.set('011');
}
function fillStep3(vm: OnboardingViewModel) {
  vm.rightArmCm.set('32.5'); vm.leftArmCm.set('31'); vm.rightLegCm.set('95'); vm.leftLegCm.set('94');
  vm.torsoCm.set('60'); vm.bustDiameterCm.set('90'); vm.waistDiameterCm.set('75');
}
function fillStep4(vm: OnboardingViewModel) {
  vm.ibHeightCm.set('175'); vm.ibWeightKg.set('68'); vm.ibFatPct.set('15'); vm.ibMusclePct.set('40');
  vm.ibWaterPct.set('55'); vm.ibBoneDensity.set('3.2'); vm.ibBodyDensity.set('1.05');
}

describe('OnboardingViewModel', () => {
  it('load() seeds identity drafts, lookups, and observation categories', async () => {
    const { vm } = build();
    await vm.load();
    expect(vm.nameEn()).toBe('Sam');
    expect(vm.nameAr()).toBe('سام');
    expect(vm.phone()).toBe('01099999999');
    expect(vm.genderId()).toBe('g1');
    expect(vm.trainingClubId()).toBe('c1');
    expect(vm.observationCategories().length).toBe(4);
  });

  it('canSubmitStep4 requires all seven non-empty and in-bounds (0 allowed for %, blank rejected)', async () => {
    const { vm } = build();
    await vm.load();
    expect(vm.canSubmitStep4()).toBe(false);
    fillStep4(vm);
    expect(vm.canSubmitStep4()).toBe(true);
    vm.ibWaterPct.set('');
    expect(vm.canSubmitStep4()).toBe(false);
    vm.ibWaterPct.set('0');
    expect(vm.canSubmitStep4()).toBe(true);
    vm.ibFatPct.set('101');
    expect(vm.canSubmitStep4()).toBe(false);
  });

  it('saveStep1() posts identity-vitals and advances to step 2 on success', async () => {
    const { vm, identity } = build();
    await vm.load();
    fillStep1(vm);
    await vm.saveStep1();
    expect(identity.run).toHaveBeenCalledTimes(1);
    expect(identity.run).toHaveBeenCalledWith(expect.objectContaining({ nameEn: 'Sam', nameAr: 'سام', phone: '01099999999' }));
    expect(vm.currentStep()).toBe(2);
    expect(vm.stepError()).toBeNull();
  });

  it('saveStep1() stays on step 1 and surfaces the specific 400 message on failure', async () => {
    const { vm, notify } = build({ identity: fail(new AppError('Date of birth must be in the past', 'http', 400, 'Date of birth must be in the past')) });
    await vm.load();
    fillStep1(vm);
    await vm.saveStep1();
    expect(vm.currentStep()).toBe(1);
    expect(vm.stepError()).toBe('Date of birth must be in the past');
    expect(notify.error).toHaveBeenCalledWith('Date of birth must be in the past');
  });

  it('saveStep1() shows the generic message on a non-400 failure', async () => {
    const { vm, notify } = build({ identity: fail(new AppError('nope', 'network')) });
    await vm.load();
    fillStep1(vm);
    await vm.saveStep1();
    expect(vm.currentStep()).toBe(1);
    expect(vm.stepError()).toBe('onboarding.saveFailed');
    expect(notify.error).toHaveBeenCalledWith('onboarding.saveFailed');
  });

  it('re-clicking saveStep1 re-POSTs (no latch)', async () => {
    const { vm, identity } = build();
    await vm.load();
    fillStep1(vm);
    await vm.saveStep1();
    vm.backToStep1();
    await vm.saveStep1();
    expect(identity.run).toHaveBeenCalledTimes(2);
  });

  it('saveStep2() posts guardian-medical and advances to step 3 on success', async () => {
    const { vm, guardianMedical } = build();
    await vm.load();
    fillStep1(vm); await vm.saveStep1();
    fillStep2(vm); await vm.saveStep2();
    expect(guardianMedical.run).toHaveBeenCalledTimes(1);
    expect(vm.currentStep()).toBe(3);
  });

  it('saveStep2() blocks (posts nothing) when a medical "Yes" has an unresolved category id', async () => {
    const { vm, guardianMedical, notify } = build({ cats: ok([]) });
    await vm.load();
    fillStep1(vm); await vm.saveStep1();
    fillStep2(vm);
    vm.allergyYes.set(true); vm.allergyDetails.set('Peanuts');
    await vm.saveStep2();
    expect(guardianMedical.run).not.toHaveBeenCalled();
    expect(vm.currentStep()).toBe(2);
    expect(notify.error).toHaveBeenCalled();
  });

  it('saveStep3() posts physiological and advances to step 4 on success', async () => {
    const { vm, physiological } = build();
    await vm.load();
    fillStep1(vm); await vm.saveStep1();
    fillStep2(vm); await vm.saveStep2();
    fillStep3(vm); await vm.saveStep3();
    expect(physiological.run).toHaveBeenCalledWith({ rightArmCm: 32.5, leftArmCm: 31, rightLegCm: 95, leftLegCm: 94, torsoCm: 60, bustDiameterCm: 90, waistDiameterCm: 75 });
    expect(vm.currentStep()).toBe(4);
  });

  it('saveStep3() stays on step 3 and surfaces the error on failure', async () => {
    const { vm, notify } = build({ physiological: fail(new AppError('nope', 'network')) });
    await vm.load();
    fillStep1(vm); await vm.saveStep1();
    fillStep2(vm); await vm.saveStep2();
    fillStep3(vm); await vm.saveStep3();
    expect(vm.currentStep()).toBe(3);
    expect(notify.error).toHaveBeenCalled();
  });

  it('submit() posts inbody, completes, and navigates on success', async () => {
    const { vm, inbody, auth, router } = build();
    await vm.load();
    fillStep1(vm); await vm.saveStep1();
    fillStep2(vm); await vm.saveStep2();
    fillStep3(vm); await vm.saveStep3();
    fillStep4(vm);
    await vm.submit();
    expect(inbody.run).toHaveBeenCalledWith({ heightCm: 175, weightKg: 68, fatPct: 15, musclePct: 40, waterPct: 55, boneDensity: 3.2, bodyDensity: 1.05 });
    expect(auth.markOnboardingComplete).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/my-profile']);
  });

  it('submit() stays on step 4 and does not complete when inbody fails', async () => {
    const { vm, auth, router, notify } = build({ inbody: fail(new AppError('bad hemoglobin', 'http', 400, 'bad hemoglobin')) });
    await vm.load();
    fillStep1(vm); await vm.saveStep1();
    fillStep2(vm); await vm.saveStep2();
    fillStep3(vm); await vm.saveStep3();
    fillStep4(vm);
    await vm.submit();
    expect(vm.currentStep()).toBe(4);
    expect(auth.markOnboardingComplete).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
    expect(notify.error).toHaveBeenCalledWith('bad hemoglobin');
  });

  it('backToStep1() navigates without posting and clears the step error', async () => {
    const { vm, identity } = build({ identity: fail(new AppError('x', 'network')) });
    await vm.load();
    fillStep1(vm); await vm.saveStep1();       // fails → stepError set, stays on 1
    expect(vm.stepError()).not.toBeNull();
    vm.currentStep.set(2);
    vm.backToStep1();
    expect(vm.currentStep()).toBe(1);
    expect(vm.stepError()).toBeNull();
    expect(identity.run).toHaveBeenCalledTimes(1); // Back did not re-post
  });
});
```

- [ ] **Step 2: Run the spec to confirm it fails**

Run: `cd frontend && npx jest --testPathPattern onboarding.viewmodel.spec`
Expected: FAIL — `saveStep1`/`saveStep2`/`saveStep3`/`stepError` don't exist; `submit()` still runs the deferred chain.

- [ ] **Step 3: Rewrite the viewmodel** — in `onboarding.viewmodel.ts`:

Add the `AppError` import at the top:

```ts
import { AppError } from '@core/domain/errors/app-error';
```

Remove the three latch signals (`identitySaved`, `guardianSaved`, `physiologicalSaved`) and add a step-error signal next to `submitting`:

```ts
  readonly submitting = signal(false);
  readonly stepError = signal<string | null>(null);
```

Replace the six navigation methods (`goToStep2`/`backToStep1`/`goToStep3`/`backToStep2`/`goToStep4`/`backToStep3`) with pure Back handlers:

```ts
  backToStep1(): void { this.stepError.set(null); this.currentStep.set(1); }
  backToStep2(): void { this.stepError.set(null); this.currentStep.set(2); }
  backToStep3(): void { this.stepError.set(null); this.currentStep.set(3); }

  private describeError(err: AppError): string {
    // The backend packs the specific field message(s) into the error body (AppError.code) —
    // surface it verbatim on a 400 instead of a generic toast (server is the validation gate).
    if (err.status === 400 && err.code) return err.code;
    return this.i18n.t('onboarding.saveFailed');
  }

  private failStep(err: AppError): void {
    const message = this.describeError(err);
    this.stepError.set(message);
    this.notify.error(message);
  }

  async saveStep1(): Promise<void> {
    if (!this.canSubmitStep1() || this.submitting()) return;
    this.submitting.set(true);
    this.stepError.set(null);
    const r = await this.completeUc.run({
      nameEn: this.nameEn().trim(), nameAr: this.nameAr().trim() || null,
      genderId: this.genderId(), dob: this.dob(), trainingClubId: this.trainingClubId(),
      examDate: this.examDate(), bloodTypeId: this.bloodTypeId() || null,
      hemoglobin: Number(this.hemoglobin()), heightCm: Number(this.heightCm()), weightKg: Number(this.weightKg()),
      internalMedId: this.internalMedId(), heartAssessId: this.heartAssessId(), spineAssessId: this.spineAssessId(),
      phone: this.phone().trim() || null,
    });
    this.submitting.set(false);
    if (r.ok) this.currentStep.set(2); else this.failStep(r.error);
  }

  async saveStep2(): Promise<void> {
    if (!this.canSubmitStep2() || this.submitting()) return;

    // Guard against unresolved category ids — check before persisting anything.
    const medicalItems = this.buildMedical();
    if (medicalItems.some((item) => item.categoryId === '')) {
      const message = this.i18n.t('onboarding.saveFailed');
      this.stepError.set(message);
      this.notify.error(message);
      return;
    }

    this.submitting.set(true);
    this.stepError.set(null);
    const r = await this.completeGuardianMedicalUc.run({
      father: { name: this.fatherName().trim(), nationalId: this.fatherNationalId().trim(), phone: this.fatherPhone().trim() },
      mother: { name: this.motherName().trim(), nationalId: this.motherNationalId().trim(), phone: this.motherPhone().trim() },
      medical: medicalItems,
    });
    this.submitting.set(false);
    if (r.ok) this.currentStep.set(3); else this.failStep(r.error);
  }

  async saveStep3(): Promise<void> {
    if (!this.canSubmitStep3() || this.submitting()) return;
    this.submitting.set(true);
    this.stepError.set(null);
    const r = await this.completePhysiologicalUc.run({
      rightArmCm: Number(this.rightArmCm()), leftArmCm: Number(this.leftArmCm()),
      rightLegCm: Number(this.rightLegCm()), leftLegCm: Number(this.leftLegCm()),
      torsoCm: Number(this.torsoCm()), bustDiameterCm: Number(this.bustDiameterCm()), waistDiameterCm: Number(this.waistDiameterCm()),
    });
    this.submitting.set(false);
    if (r.ok) this.currentStep.set(4); else this.failStep(r.error);
  }
```

Replace the whole `submit()` method with the InBody-only version:

```ts
  async submit(): Promise<void> {
    if (!this.canSubmitStep4() || this.submitting()) return;
    this.submitting.set(true);
    this.stepError.set(null);

    // Saves the reading AND completes onboarding (server-side, at the InBody step).
    const r = await this.completeInBodyUc.run({
      heightCm: Number(this.ibHeightCm()), weightKg: Number(this.ibWeightKg()),
      fatPct: Number(this.ibFatPct()), musclePct: Number(this.ibMusclePct()), waterPct: Number(this.ibWaterPct()),
      boneDensity: Number(this.ibBoneDensity()), bodyDensity: Number(this.ibBodyDensity()),
    });
    this.submitting.set(false);

    if (r.ok) {
      this.auth.markOnboardingComplete();
      this.notify.success(this.i18n.t('onboarding.completed'));
      void this.router.navigate(['/my-profile']);
    } else {
      this.failStep(r.error);
    }
  }
```

Keep `buildMedical()`, `load()`, and the `canSubmitStepN` computeds unchanged.

- [ ] **Step 4: Wire the wizard HTML** — in `onboarding-wizard.page.html`:

Add an inline step-error paragraph right after the existing load-error block (after line ~27, the `@if (vm.error()) { ... }` block):

```html
      @if (vm.stepError()) {
        <p class="oa-error">{{ vm.stepError() }}</p>
      }
```

Change the Step 1 Next button (currently `(click)="vm.goToStep2()"`):

```html
          <button type="button" class="oa-next" [disabled]="!vm.canSubmitStep1() || vm.submitting()" (click)="vm.saveStep1()">
```

Change the Step 2 Next button (currently `(click)="vm.goToStep3()"`):

```html
          <button type="button" class="oa-next" [disabled]="!vm.canSubmitStep2() || vm.submitting()" (click)="vm.saveStep2()">
```

Change the Step 3 Next button (currently `(click)="vm.goToStep4()"`):

```html
          <button type="button" class="oa-next" [disabled]="!vm.canSubmitStep3() || vm.submitting()" (click)="vm.saveStep3()">
```

Leave the Step 4 button as-is (`[disabled]="!vm.canSubmitStep4() || vm.submitting()" (click)="vm.submit()"`) and the three `backToStepN` buttons unchanged.

- [ ] **Step 5: Run the viewmodel spec to confirm it passes**

Run: `cd frontend && npx jest --testPathPattern onboarding.viewmodel.spec`
Expected: PASS.

- [ ] **Step 6: Run the full frontend suite (guards against collateral breakage)**

Run: `cd frontend && npx jest`
Expected: PASS (all green).

- [ ] **Step 7: Stage (no commit)**

```bash
git add frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding.viewmodel.ts \
        frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding-wizard.page.html \
        frontend/src/app/features/swimmer-onboarding/testing/presentation/pages/onboarding/onboarding.viewmodel.spec.ts
```

---

## Final Verification

- [ ] Backend — run all four affected projects together:
  `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests && dotnet test backend/tests/Kheprx.BaseBackend.Health.UnitTests && dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests`
  Expected: all PASS.
- [ ] Frontend — `cd frontend && npx jest` → all PASS.
- [ ] `git status` shows only the files listed across Tasks 1–5 as staged; **nothing committed** (stage-only; the user integrates).
