# Swimmer Self-Service Portal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** After onboarding, a swimmer gets a two-item menu (My Profile + Settings) and a read-only, self-scoped version of the captain's tabbed profile; the backend enforces that a swimmer can only read their own record.

**Architecture:** Reuse the existing `SwimmerProfilePage`/`SwimmerProfileViewModel` (read-only already falls out of `canEdit()`); add one backend endpoint (`GET /api/swimmers/me`) so the frontend can resolve the caller's own swimmer id; add a shared `ISwimmerSelfAccessGuard` applied to the ~10 swimmer-scoped read endpoints (403 on a foreign id, coaches unrestricted); role-gate the sidebar and route/landing behavior.

**Tech Stack:** Backend — .NET 10, ASP.NET Core controllers, FluentValidation, xUnit + Moq. Frontend — Angular (standalone components + signals), Jest, clean-architecture use-cases/repositories.

**Spec:** `docs/superpowers/specs/2026-09-25-swimmer-self-service-portal-design.md`

## Global Constraints

- **Roles are exactly** `head_coach | captain | swimmer` (`frontend/src/app/core/domain/roles/user-role.ts`; backend role strings `"head_coach"`, `"captain"`, `"swimmer"`).
- **Read endpoints stay any-authenticated for coaches; only swimmers are restricted** to their own record. Never tighten a GET to a role that breaks coach access.
- **Backend `dotnet test` requires the dev API stopped** (it locks the built DLLs). Stop any running `Kheprx.BaseBackend.Api` process before running backend tests: `Get-Process -Name "Kheprx.BaseBackend.Api" -ErrorAction SilentlyContinue | Stop-Process -Force`.
- **Frontend tests** run from the `frontend/` directory: `npx jest <pattern>`.
- **`ApiResponse<T>` shape:** success via `ApiResponse<T>.Success(msg, data)`, failure via `ApiResponse<T>.Failure(msg, "<code>")`; localized messages come from `*Messages` static classes using `AppLanguage.Current`.
- **Do not commit** unless the user asks — this branch (`feat/championships`) is kept staged/uncommitted by user directive. Each task's "Commit" step stages+commits locally per the normal workflow; if the user has reiterated "no commits", replace the commit step with staging only.
- **Every controller extends `BaseApiController`**, which provides `CurrentUserId()` (returns `Guid`) and `User` (for `User.IsInRole("swimmer")`).

## Review Focus

- **Swimmer with no swimmer profile** hits `/api/swimmers/me` or `/my-profile` → expect a clean 404 / not-found state, not a 500 or blank page. (Pinned: Task 1 `/me` 404 test; Task 6 `loadMe()` failure test.)
- **Route collision `me` vs `{id:guid}`** on `GET /api/swimmers/...` → `"me"` must hit the new handler, not the guid handler. The `:guid` route constraint excludes `"me"`, so ordering is safe. (Pinned: Task 1 `/me` 200 test exercises routing.)
- **A swimmer requests another swimmer's data by id** (URL edit / devtools) → 403 from the API even though the UI hides it. (Pinned: Task 3 foreign-id 403 test; Task 4 representative foreign-id 403 tests.)
- **Home-redirect loop** for swimmers (`/` → `home` → `/my-profile`) → the home guard must send swimmers straight to `/my-profile`, never back to `/`. (Pinned: Task 8 home-guard test.)
- **Coach regression** — coaches must still read any swimmer and see the full menu. (Pinned: Task 2 guard coach→true unit test; Task 7 coach-menu-unchanged test.)

---

## Task 1: Backend — `GET /api/swimmers/me` + `MySwimmerRefDto`

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/SwimmerDtos.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs`

**Interfaces:**
- Consumes: existing `ISwimmerService.GetSwimmerIdByUserAsync(Guid userId, CancellationToken)` → `Task<Guid?>`; `BaseApiController.CurrentUserId()` → `Guid`.
- Produces: `MySwimmerRefDto(Guid SwimmerId)`; endpoint `GET /api/swimmers/me` returning `ApiResponse<MySwimmerRefDto>`.

- [ ] **Step 1: Write the failing tests** — add to `SwimmersControllerTests.cs`, modeled on the existing `GetOnboardingPrefill_returns_200_with_dto` / `_returns_404_when_not_a_swimmer` tests (same `OnboardingController(svc.Object)` helper that sets a swimmer principal):

```csharp
[Fact]
public async Task GetMe_returns_200_with_swimmer_id()
{
    var svc = new Mock<ISwimmerService>();
    var id = Guid.NewGuid();
    svc.Setup(s => s.GetSwimmerIdByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
       .ReturnsAsync(id);

    var result = await OnboardingController(svc.Object).GetMe(CancellationToken.None);

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var body = Assert.IsType<ApiResponse<MySwimmerRefDto>>(ok.Value);
    Assert.True(body.SuccessStatus);
    Assert.Equal(id, body.Data!.SwimmerId);
}

[Fact]
public async Task GetMe_returns_404_when_not_a_swimmer()
{
    var svc = new Mock<ISwimmerService>();
    svc.Setup(s => s.GetSwimmerIdByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
       .ReturnsAsync((Guid?)null);

    var result = await OnboardingController(svc.Object).GetMe(CancellationToken.None);

    Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(result.Result).StatusCode);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests/Kheprx.BaseBackend.Api.UnitTests.csproj --nologo`
Expected: build failure — `MySwimmerRefDto` and `GetMe` do not exist.

- [ ] **Step 3: Add the DTO** — append to `SwimmerDtos.cs`:

```csharp
/// <summary>The caller's own swimmer profile id (GET /api/swimmers/me).</summary>
public sealed record MySwimmerRefDto(Guid SwimmerId);
```

- [ ] **Step 4: Add the endpoint** — in `SwimmersController.cs`, in the swimmer/onboarding region (near the other `[Authorize(Roles = "swimmer")]` endpoints):

```csharp
/// <summary>Returns the calling swimmer's own profile id. Swimmer self-service.</summary>
/// <response code="200">The caller's swimmer id.</response>
/// <response code="404">The caller has no swimmer profile.</response>
[HttpGet("me")]
[Authorize(Roles = "swimmer")]
[ProducesResponseType(typeof(ApiResponse<MySwimmerRefDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse<MySwimmerRefDto>), StatusCodes.Status404NotFound)]
public async Task<ActionResult<ApiResponse<MySwimmerRefDto>>> GetMe(CancellationToken ct)
{
    var id = await _service.GetSwimmerIdByUserAsync(CurrentUserId(), ct);
    if (id is null)
        return StatusCode(StatusCodes.Status404NotFound,
            ApiResponse<MySwimmerRefDto>.Failure(SwimmerMessages.Errors.ProfileNotFound(AppLanguage.Current), "not_found"));
    return Ok(ApiResponse<MySwimmerRefDto>.Success(
        SwimmerMessages.Success.ProfileRetrieved(AppLanguage.Current), new MySwimmerRefDto(id.Value)));
}
```

Note: `[HttpGet("me")]` does not collide with `[HttpGet("{id:guid}")]` — `"me"` fails the `:guid` constraint, so it routes here.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests/Kheprx.BaseBackend.Api.UnitTests.csproj --nologo`
Expected: PASS (all).

- [ ] **Step 6: Commit**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/SwimmerDtos.cs backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs
git commit -m "feat(swimmers): add GET /api/swimmers/me returning the caller's own swimmer id"
```

---

## Task 2: Backend — `ISwimmerSelfAccessGuard` + `Forbidden` message + DI

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Resources/SwimmerMessages.cs`
- Create: `backend/Kheprx.BaseBackend.Api/Security/ISwimmerSelfAccessGuard.cs`
- Create: `backend/Kheprx.BaseBackend.Api/Security/SwimmerSelfAccessGuard.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Program.cs` (DI registration)
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/Security/SwimmerSelfAccessGuardTests.cs`

**Interfaces:**
- Consumes: `ISwimmerService.GetSwimmerIdByUserAsync(Guid, CancellationToken)` → `Task<Guid?>`.
- Produces: `ISwimmerSelfAccessGuard.CanReadAsync(bool callerIsSwimmer, Guid callerUserId, Guid targetSwimmerId, CancellationToken)` → `Task<bool>`; `SwimmerMessages.Errors.Forbidden(string lang)`.

- [ ] **Step 1: Write the failing test** — create `SwimmerSelfAccessGuardTests.cs`:

```csharp
using Kheprx.BaseBackend.Api.Security;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Moq;
using Xunit;

public class SwimmerSelfAccessGuardTests
{
    private static (ISwimmerSelfAccessGuard guard, Mock<ISwimmerService> svc) Build()
    {
        var svc = new Mock<ISwimmerService>();
        return (new SwimmerSelfAccessGuard(svc.Object), svc);
    }

    [Fact]
    public async Task Coach_can_read_any_swimmer()
    {
        var (guard, _) = Build();
        Assert.True(await guard.CanReadAsync(callerIsSwimmer: false, Guid.NewGuid(), Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Swimmer_can_read_own_record()
    {
        var (guard, svc) = Build();
        var userId = Guid.NewGuid();
        var mine = Guid.NewGuid();
        svc.Setup(s => s.GetSwimmerIdByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(mine);
        Assert.True(await guard.CanReadAsync(callerIsSwimmer: true, userId, mine, default));
    }

    [Fact]
    public async Task Swimmer_cannot_read_foreign_record()
    {
        var (guard, svc) = Build();
        var userId = Guid.NewGuid();
        svc.Setup(s => s.GetSwimmerIdByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        Assert.False(await guard.CanReadAsync(callerIsSwimmer: true, userId, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Swimmer_without_profile_is_denied()
    {
        var (guard, svc) = Build();
        var userId = Guid.NewGuid();
        svc.Setup(s => s.GetSwimmerIdByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        Assert.False(await guard.CanReadAsync(callerIsSwimmer: true, userId, Guid.NewGuid(), default));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests/Kheprx.BaseBackend.Api.UnitTests.csproj --nologo`
Expected: build failure — `SwimmerSelfAccessGuard` / `ISwimmerSelfAccessGuard` do not exist.

- [ ] **Step 3: Create the interface** — `ISwimmerSelfAccessGuard.cs`:

```csharp
namespace Kheprx.BaseBackend.Api.Security;

/// <summary>Decides whether the caller may READ a given swimmer's data.
/// Coaches (head_coach/captain) may read anyone; a swimmer may read only their own record.</summary>
public interface ISwimmerSelfAccessGuard
{
    Task<bool> CanReadAsync(bool callerIsSwimmer, Guid callerUserId, Guid targetSwimmerId, CancellationToken ct);
}
```

- [ ] **Step 4: Create the implementation** — `SwimmerSelfAccessGuard.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;

namespace Kheprx.BaseBackend.Api.Security;

public sealed class SwimmerSelfAccessGuard : ISwimmerSelfAccessGuard
{
    private readonly ISwimmerService _swimmers;
    public SwimmerSelfAccessGuard(ISwimmerService swimmers) => _swimmers = swimmers;

    public async Task<bool> CanReadAsync(bool callerIsSwimmer, Guid callerUserId, Guid targetSwimmerId, CancellationToken ct)
    {
        if (!callerIsSwimmer) return true;                       // coaches unrestricted
        var mine = await _swimmers.GetSwimmerIdByUserAsync(callerUserId, ct);
        return mine is { } id && id == targetSwimmerId;         // swimmer: own record only (fail-closed)
    }
}
```

- [ ] **Step 5: Add the `Forbidden` message** — in `SwimmerMessages.Errors` (`SwimmerMessages.cs`), after `ExamNotFound`:

```csharp
        public static string Forbidden(string lang) => lang switch { "ar" => "غير مصرح لك بعرض بيانات سبّاح آخر", _ => "You may only view your own data" };
```

- [ ] **Step 6: Register in DI** — in `Program.cs`, alongside the other service registrations:

```csharp
builder.Services.AddScoped<Kheprx.BaseBackend.Api.Security.ISwimmerSelfAccessGuard, Kheprx.BaseBackend.Api.Security.SwimmerSelfAccessGuard>();
```

- [ ] **Step 7: Run the test to verify it passes**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests/Kheprx.BaseBackend.Api.UnitTests.csproj --nologo`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Security backend/Kheprx.BaseBackend.Api/Program.cs backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Resources/SwimmerMessages.cs backend/tests/Kheprx.BaseBackend.Api.UnitTests/Security/SwimmerSelfAccessGuardTests.cs
git commit -m "feat(security): add ISwimmerSelfAccessGuard (swimmer self-only read) + Forbidden message"
```

---

## Task 3: Backend — apply the guard to `SwimmersController` reads

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs`

**Interfaces:**
- Consumes: `ISwimmerSelfAccessGuard.CanReadAsync(...)` (Task 2); `CurrentUserId()`, `User.IsInRole(...)`.
- Produces: guarded `GetById`, `ListExams`, `GetGuardians`, `GetLatestBodyMeasurement` (403 on foreign swimmer id).

The **guard snippet** (target id is the route `id` here):

```csharp
if (!await _access.CanReadAsync(User.IsInRole("swimmer"), CurrentUserId(), id, ct))
    return StatusCode(StatusCodes.Status403Forbidden,
        ApiResponse<T>.Failure(SwimmerMessages.Errors.Forbidden(AppLanguage.Current), "forbidden"));
```

...where `ApiResponse<T>` matches each handler's existing return generic:
`GetById` → `SwimmerProfileDto`; `ListExams` → `IReadOnlyList<SwimmerVitalsDto>`; `GetGuardians` → `SwimmerGuardiansDto`; `GetLatestBodyMeasurement` → `SwimmerBodyMeasurementDto`.

- [ ] **Step 1: Write the failing tests** — add to `SwimmersControllerTests.cs`. These use a controller built with a mocked `ISwimmerService` **and** a mocked `ISwimmerSelfAccessGuard`; follow the existing controller-construction helper and add the guard mock (extend the helper if needed). Cover `GetById` as the representative:

```csharp
// The guard runs BEFORE the service, so the 403 test needs no service setup.
[Fact]
public async Task GetById_returns_403_when_swimmer_requests_foreign_id()
{
    var svc = new Mock<ISwimmerService>();
    var access = new Mock<ISwimmerSelfAccessGuard>();
    access.Setup(a => a.CanReadAsync(It.IsAny<bool>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(false); // guard denies

    var controller = CreateSwimmers(svc.Object, access.Object); // helper that injects both
    var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

    Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result.Result).StatusCode);
}

[Fact]
public async Task GetById_returns_200_when_guard_allows()
{
    var identity = new SwimmerIdentityDto(Guid.NewGuid(), "SW-1", "Sam", null, null, null, "male", null, null, null);
    var svc = new Mock<ISwimmerService>();
    svc.Setup(s => s.GetProfileAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
       .ReturnsAsync(new SwimmerProfileDto(identity, null));
    var access = new Mock<ISwimmerSelfAccessGuard>();
    access.Setup(a => a.CanReadAsync(It.IsAny<bool>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(true);

    var controller = CreateSwimmers(svc.Object, access.Object);
    var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

    Assert.IsType<OkObjectResult>(result.Result);
}
```

> If the existing tests construct `SwimmersController` directly, add a `CreateSwimmers(ISwimmerService, ISwimmerSelfAccessGuard)` helper (defaulting the guard to a permissive mock — `CanReadAsync(...) => true` — so pre-existing tests are unaffected). The `SwimmerIdentityDto` constructor is `(Guid Id, string Uid, string NameEn, string? NameAr, DateOnly? Dob, int? Age, string GenderCode, string? Phone, string? TrainingClubNameEn, string? TrainingClubNameAr)`.

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests/Kheprx.BaseBackend.Api.UnitTests.csproj --nologo`
Expected: build/compile failure (`_access` / helper not present) or assertion failure (403 not returned).

- [ ] **Step 3: Inject the guard + add the checks** — in `SwimmersController.cs`: add `ISwimmerSelfAccessGuard _access` to the constructor (store the field), then insert the guard snippet as the **first statement** of `GetById`, `ListExams`, `GetGuardians`, and `GetLatestBodyMeasurement` (each with its matching `ApiResponse<T>`). Example for `GetById`:

```csharp
public async Task<ActionResult<ApiResponse<SwimmerProfileDto>>> GetById(Guid id, CancellationToken ct)
{
    if (!await _access.CanReadAsync(User.IsInRole("swimmer"), CurrentUserId(), id, ct))
        return StatusCode(StatusCodes.Status403Forbidden,
            ApiResponse<SwimmerProfileDto>.Failure(SwimmerMessages.Errors.Forbidden(AppLanguage.Current), "forbidden"));

    var dto = await _service.GetProfileAsync(id, ct);
    // ...unchanged...
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests/Kheprx.BaseBackend.Api.UnitTests.csproj --nologo`
Expected: PASS (all).

- [ ] **Step 5: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs
git commit -m "feat(security): enforce swimmer self-only on SwimmersController reads"
```

---

## Task 4: Backend — apply the guard to the remaining swimmer-scoped reads

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/InBodyReadingsController.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/FeedbackEntriesController.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/ObservationsController.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/HealthReadingsController.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/AttendanceRecordsController.cs`
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/ChampionshipsController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/` (the corresponding controller test files)

**Interfaces:**
- Consumes: `ISwimmerSelfAccessGuard.CanReadAsync(...)`.
- Produces: guarded swimmer-scoped list reads across all six controllers.

Each handler gets the **same first-statement guard**, with the target id and `ApiResponse<T>` per this table:

| Controller.Handler | Target id expr | `ApiResponse<T>` |
|---|---|---|
| `InBodyReadingsController.List(Guid id)` | `id` | `IReadOnlyList<InBodyReadingDto>` |
| `FeedbackEntriesController.List(Guid id)` | `id` | `IReadOnlyList<FeedbackEntryDto>` |
| `ObservationsController.List([FromQuery] Guid swimmerId)` | `swimmerId` | `IReadOnlyList<ObservationDto>` |
| `HealthReadingsController.List([FromQuery] Guid swimmerId)` | `swimmerId` | `IReadOnlyList<HealthReadingListItemDto>` |
| `AttendanceRecordsController.List([FromQuery] Guid swimmerId)` | `swimmerId` | `IReadOnlyList<AttendanceRecordDto>` |
| `ChampionshipsController.GetSwimmerHistory(Guid swimmerId)` | `swimmerId` | `IReadOnlyList<ChampionshipSwimmerHistoryDto>` |

Guard snippet (substitute the id expr and `T`):

```csharp
if (!await _access.CanReadAsync(User.IsInRole("swimmer"), CurrentUserId(), <targetId>, ct))
    return StatusCode(StatusCodes.Status403Forbidden,
        ApiResponse<T>.Failure(SwimmerMessages.Errors.Forbidden(AppLanguage.Current), "forbidden"));
```

`SwimmerMessages` is in `Kheprx.BaseBackend.Identity.Application.Resources` — add the `using` where a controller doesn't already have it. Inject `ISwimmerSelfAccessGuard _access` into each controller's constructor.

- [ ] **Step 1: Write the failing tests** — add representative tests to **two** controllers covering both id shapes: `InBodyReadingsController` (route id) and `ObservationsController` (query id). For each: swimmer foreign id → 403; guard-allows → 200. Follow each controller's existing test-construction pattern, adding a mocked `ISwimmerSelfAccessGuard` (default permissive in a shared helper so existing tests are unaffected). Example (InBody):

```csharp
[Fact]
public async Task List_returns_403_when_swimmer_requests_foreign_id()
{
    var svc = new Mock<IInBodyReadingService>();
    var access = new Mock<ISwimmerSelfAccessGuard>();
    access.Setup(a => a.CanReadAsync(It.IsAny<bool>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(false);
    var controller = CreateInBody(svc.Object, access.Object);
    var result = await controller.List(Guid.NewGuid(), CancellationToken.None);
    Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result.Result).StatusCode);
}
```

> **Coverage note (no silent cap):** the four other controllers receive the identical one-line insertion; they are covered by the `SwimmerSelfAccessGuard` unit tests (Task 2) plus these two representative endpoint tests. Add per-controller 403 tests too if the reviewer wants exhaustive coverage.

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests/Kheprx.BaseBackend.Api.UnitTests.csproj --nologo`
Expected: compile/assertion failure.

- [ ] **Step 3: Apply the guard** — inject `ISwimmerSelfAccessGuard` and insert the guard snippet as the first statement of each of the six handlers in the table.

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests/Kheprx.BaseBackend.Api.UnitTests.csproj --nologo`
Expected: PASS (all).

- [ ] **Step 5: Full backend solution check**

Run: `dotnet test backend/Kheprx.BaseBackend.sln --nologo`
Expected: all projects PASS.

- [ ] **Step 6: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers backend/tests/Kheprx.BaseBackend.Api.UnitTests
git commit -m "feat(security): enforce swimmer self-only on inbody/feedback/observations/health/attendance/championship reads"
```

---

## Task 5: Frontend — resolve the caller's own swimmer id

**Files:**
- Create: `frontend/src/app/features/swimmer-profile/data/dto/my-swimmer-ref.dto.ts`
- Modify: `frontend/src/app/features/swimmer-profile/domain/repositories/swimmer-profile.repository.ts`
- Modify: `frontend/src/app/features/swimmer-profile/data/repositories/swimmer-profile.repository.impl.ts`
- Create: `frontend/src/app/features/swimmer-profile/domain/usecases/get-my-swimmer-id.use-case.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/domain/usecases/get-my-swimmer-id.use-case.spec.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/data/repositories/swimmer-profile.repository.impl.spec.ts`

**Interfaces:**
- Produces: `ISwimmerProfileRepository.getMySwimmerId()` → `Promise<MySwimmerRefItemDtoRs>`; `GetMySwimmerIdUseCase.run()` → `Result<string>` (the swimmer id string).

- [ ] **Step 1: Write the failing use-case test** — `get-my-swimmer-id.use-case.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { GetMySwimmerIdUseCase } from '@features/swimmer-profile/domain/usecases/get-my-swimmer-id.use-case';
import { SWIMMER_PROFILE_REPOSITORY, ISwimmerProfileRepository } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

function build(repo: Partial<ISwimmerProfileRepository>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }] });
  return TestBed.inject(GetMySwimmerIdUseCase);
}

describe('GetMySwimmerIdUseCase', () => {
  it('returns the swimmer id from GET /api/swimmers/me', async () => {
    const uc = build({ getMySwimmerId: async () => ({ data: { swimmerId: 'SW-42' } }) } as unknown as ISwimmerProfileRepository);
    const r = await uc.run();
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.data).toBe('SW-42');
  });

  it('fails when the response is malformed', async () => {
    const uc = build({ getMySwimmerId: async () => ({ data: {} }) } as unknown as ISwimmerProfileRepository);
    const r = await uc.run();
    expect(r.ok).toBe(false);
  });
});
```

- [ ] **Step 2: Run to verify failure**

Run (from `frontend/`): `npx jest get-my-swimmer-id`
Expected: FAIL — module/symbol not found.

- [ ] **Step 3: Add the DTO** — `my-swimmer-ref.dto.ts`:

```typescript
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface MySwimmerRefDtoRs { swimmerId: string; }
export interface MySwimmerRefItemDtoRs extends BaseResponseRs<MySwimmerRefDtoRs> {}

export function isMySwimmerRefValid(dto: unknown): dto is MySwimmerRefDtoRs {
  const d = dto as MySwimmerRefDtoRs;
  return !!d && typeof d.swimmerId === 'string' && d.swimmerId.length > 0;
}
```

- [ ] **Step 4: Add the repository method** — in `swimmer-profile.repository.ts` add to the interface:

```typescript
  getMySwimmerId(): Promise<MySwimmerRefItemDtoRs>;
```

(import `MySwimmerRefItemDtoRs` from `@features/swimmer-profile/data/dto/my-swimmer-ref.dto`), and implement in `swimmer-profile.repository.impl.ts`:

```typescript
  getMySwimmerId(): Promise<MySwimmerRefItemDtoRs> {
    return this.http.get<MySwimmerRefItemDtoRs>('/api/swimmers/me');
  }
```

- [ ] **Step 5: Add the use-case** — `get-my-swimmer-id.use-case.ts`:

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { isMySwimmerRefValid } from '@features/swimmer-profile/data/dto/my-swimmer-ref.dto';

@Injectable({ providedIn: 'root' })
export class GetMySwimmerIdUseCase extends UseCase<void, string> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('GetMySwimmerId'); }

  protected async execute(): Promise<string> {
    const res = await this.repo.getMySwimmerId();
    if (!isMySwimmerRefValid(res.data)) throw new AppError('Invalid my-swimmer-id response', 'validation');
    return res.data.swimmerId;
  }
}
```

- [ ] **Step 6: Add the repository-impl test** — append to `swimmer-profile.repository.impl.spec.ts`:

```typescript
  it('getMySwimmerId GETs /api/swimmers/me', async () => {
    (http.get as jest.Mock).mockResolvedValue({ data: { swimmerId: 'SW-1' } });
    await repo.getMySwimmerId();
    expect(http.get).toHaveBeenCalledWith('/api/swimmers/me');
  });
```

- [ ] **Step 7: Run to verify pass**

Run (from `frontend/`): `npx jest get-my-swimmer-id swimmer-profile.repository.impl`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add frontend/src/app/features/swimmer-profile/data/dto/my-swimmer-ref.dto.ts frontend/src/app/features/swimmer-profile/domain/repositories/swimmer-profile.repository.ts frontend/src/app/features/swimmer-profile/data/repositories/swimmer-profile.repository.impl.ts frontend/src/app/features/swimmer-profile/domain/usecases/get-my-swimmer-id.use-case.ts frontend/src/app/features/swimmer-profile/testing
git commit -m "feat(swimmer-profile): resolve caller's own swimmer id via GET /api/swimmers/me"
```

---

## Task 6: Frontend — `loadMe()` on the profile viewmodel + `/my-profile` route

**Files:**
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.ts`
- Modify: `frontend/src/app/features/swimmer-profile/presentation/pages/swimmer-profile/swimmer-profile.page.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Test: `frontend/src/app/features/swimmer-profile/testing/presentation/pages/swimmer-profile/swimmer-profile.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `GetMySwimmerIdUseCase.run()` (Task 5); existing `SwimmerProfileViewModel.load(id: string)`.
- Produces: `SwimmerProfileViewModel.loadMe()` → `Promise<void>`; route `/my-profile`.

- [ ] **Step 1: Write the failing viewmodel tests** — add to `swimmer-profile.viewmodel.spec.ts`. Extend the file's `build()` harness to register `GetMySwimmerIdUseCase` (it is `providedIn: 'root'`, so it MUST be overridden or TestBed will try to construct the real one):

```typescript
// In build()'s TestBed providers, add:
//   { provide: GetMySwimmerIdUseCase, useValue: { run: jest.fn().mockResolvedValue(overrides.myId ?? ok('SW-ME')) } }

it('loadMe() resolves own id then loads that profile', async () => {
  const { vm } = build({ myId: ok('SW-ME') });
  const loadSpy = jest.spyOn(vm, 'load').mockResolvedValue(undefined);
  await vm.loadMe();
  expect(loadSpy).toHaveBeenCalledWith('SW-ME');
});

it('loadMe() sets notFound when own id cannot be resolved', async () => {
  const { vm } = build({ myId: fail(new AppError('nope', 'validation')) });
  await vm.loadMe();
  expect(vm.notFound()).toBe(true);
});

it('canEdit() is false for a swimmer (read-only profile)', () => {
  const { vm } = build({ role: 'swimmer' });
  expect(vm.canEdit()).toBe(false);
});
```

> `build()` already accepts a `role` override (see the existing `canEdit is false for a null role` test). Add `myId` to its overrides type. Import `ok`, `fail`, `AppError` if the spec file doesn't already.

- [ ] **Step 2: Run to verify failure**

Run (from `frontend/`): `npx jest swimmer-profile.viewmodel`
Expected: FAIL — `loadMe` undefined / provider missing.

- [ ] **Step 3: Implement `loadMe()`** — in `swimmer-profile.viewmodel.ts`: inject the use-case and add the method:

```typescript
import { GetMySwimmerIdUseCase } from '@features/swimmer-profile/domain/usecases/get-my-swimmer-id.use-case';
// ...
  private readonly getMyIdUc = inject(GetMySwimmerIdUseCase);
// ...
  async loadMe(): Promise<void> {
    const r = await this.getMyIdUc.run();
    if (!r.ok) { this.notFound.set(true); return; }
    await this.load(r.data);
  }
```

> `notFound` is created with `signal(false)` in this viewmodel; it is settable here (same class). If it is typed `readonly` externally, `.set` is still available inside the class.

- [ ] **Step 4: Wire the page** — in `swimmer-profile.page.ts` `ngOnInit`:

```typescript
ngOnInit(): void {
  const id = this.route.snapshot.paramMap.get('id');
  if (id) void this.vm.load(id);
  else void this.vm.loadMe();
}
```

- [ ] **Step 5: Add the route** — in `app.routes.ts`, inside the shell `children`, add:

```typescript
{
  path: 'my-profile',
  canActivate: [firstLoginGuard, roleGuard('swimmer')],
  loadComponent: () => import('@features/swimmer-profile').then((m) => m.SwimmerProfilePage),
  providers: [SwimmerProfileViewModel],
},
```

(`roleGuard` is already imported in `app.routes.ts`.)

- [ ] **Step 6: Run to verify pass**

Run (from `frontend/`): `npx jest swimmer-profile.viewmodel`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/app/features/swimmer-profile/presentation frontend/src/app/app.routes.ts frontend/src/app/features/swimmer-profile/testing
git commit -m "feat(swimmer-profile): /my-profile route + loadMe() self-scoped read-only view"
```

---

## Task 7: Frontend — role-gated sidebar + i18n

**Files:**
- Modify: `frontend/src/app/layout/layout.component.ts`
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`
- Test: `frontend/src/app/layout/layout.component.spec.ts`

**Interfaces:**
- Consumes: existing `navGroups` role-filtering (`item.roles`).
- Produces: swimmer sees exactly `[My Profile, Settings]`; coaches unchanged.

- [ ] **Step 1: Write the failing tests** — in `layout.component.spec.ts`, widen the `setup` role type to include `'swimmer'` and add:

```typescript
describe('LayoutComponent (swimmer)', () => {
  it('shows only My Profile and Settings', () => {
    const { fixture } = setup('swimmer');
    const labels = Array.from(fixture.nativeElement.querySelectorAll('nav button'))
      .map((b) => (b as HTMLElement).textContent?.trim());
    expect(labels).toEqual(['My Profile', 'Settings']);
  });
});
```

Change `function setup(role: 'head_coach' | 'captain', ...)` → `role: 'head_coach' | 'captain' | 'swimmer'`.

- [ ] **Step 2: Run to verify failure**

Run (from `frontend/`): `npx jest layout.component`
Expected: FAIL — swimmer currently sees coach items; `My Profile` doesn't exist.

- [ ] **Step 3: Update the nav model** — in `layout.component.ts`: import `LucideUser` from `@lucide/angular`; add `readonly UserIcon = LucideUser;` (if the group uses the icon constant directly, reference `LucideUser`). Update `allGroups`:

```typescript
private readonly allGroups: NavGroup[] = [
  {
    header: 'shell.nav.groups.overview',
    items: [
      { label: 'shell.nav.myProfile', icon: LucideUser, route: '/my-profile', roles: ['swimmer'] },
      { label: 'shell.nav.dashboard', icon: LucideLayoutDashboard, route: '/home', roles: ['head_coach', 'captain'] },
    ],
  },
  {
    header: 'shell.nav.groups.coaching',
    items: [
      { label: 'shell.nav.swimmers', icon: LucideUsers, route: '/swimmers', roles: ['head_coach', 'captain'] },
      { label: 'shell.nav.attendance', icon: LucideCalendarCheck, route: '/attendance', roles: ['head_coach', 'captain'] },
      { label: 'shell.nav.championships', icon: LucideTrophy, route: '/championships', roles: ['head_coach', 'captain'] },
    ],
  },
  {
    header: 'shell.nav.groups.administration',
    items: [
      { label: 'shell.nav.captainPanel', icon: LucideShieldAlert, roles: ['head_coach', 'captain'], route: '/captain-panel' },
      { label: 'shell.nav.settings', icon: LucideSettings, route: '/account' },
    ],
  },
];
```

- [ ] **Step 4: Add i18n** — add `"myProfile": "My Profile"` to the `shell.nav` object in `en.json`, and `"myProfile": "ملفي"` in `ar.json`.

- [ ] **Step 5: Run to verify pass**

Run (from `frontend/`): `npx jest layout.component`
Expected: PASS (swimmer sees `[My Profile, Settings]`; existing coach tests still pass — Captain Panel/Dashboard present for coaches).

- [ ] **Step 6: Commit**

```bash
git add frontend/src/app/layout frontend/src/app/core/i18n/en.json frontend/src/app/core/i18n/ar.json
git commit -m "feat(shell): role-gated sidebar — swimmers see My Profile + Settings only"
```

---

## Task 8: Frontend — role-based landing + coach route guards

**Files:**
- Modify: `frontend/src/app/features/auth/presentation/pages/login/login.viewmodel.ts`
- Modify: `frontend/src/app/features/swimmer-onboarding/presentation/pages/onboarding/onboarding.viewmodel.ts`
- Modify: `frontend/src/app/features/auth/presentation/auth.guard.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Test: `frontend/src/app/features/auth/testing/presentation/pages/login/login.viewmodel.spec.ts`
- Test: `frontend/src/app/features/swimmer-onboarding/testing/presentation/pages/onboarding/onboarding.viewmodel.spec.ts`
- Test: `frontend/src/app/features/auth/testing/presentation/auth.guard.spec.ts`

**Interfaces:**
- Produces: `swimmerHomeRedirectGuard` (CanActivateFn); `LANDING_ROUTE_BY_ROLE.swimmer = '/my-profile'`; onboarding completes to `/my-profile`.

- [ ] **Step 1: Write the failing tests**

Login (`login.viewmodel.spec.ts`) — a swimmer with `mustChangePassword=false` lands on `/my-profile`:

```typescript
it('navigates a swimmer to /my-profile after login', async () => {
  // arrange auth mock: signIn ok, role() === 'swimmer', mustChangePassword false
  await vm.submit();
  expect(router.navigate).toHaveBeenCalledWith(['/my-profile']);
});
```

Onboarding (`onboarding.viewmodel.spec.ts`) — change the existing success expectation from `['/home']` to `['/my-profile']` in the `submit()` success test.

Guard (`auth.guard.spec.ts`):

```typescript
it('swimmerHomeRedirectGuard redirects a swimmer to /my-profile', () => {
  // auth mock: role() === 'swimmer'
  const result = TestBed.runInInjectionContext(() => swimmerHomeRedirectGuard(/* route, state */ {} as any, {} as any));
  expect(result).toBe(false);
  expect(router.navigate).toHaveBeenCalledWith(['/my-profile']);
});

it('swimmerHomeRedirectGuard allows a coach through', () => {
  // auth mock: role() === 'head_coach'
  const result = TestBed.runInInjectionContext(() => swimmerHomeRedirectGuard({} as any, {} as any));
  expect(result).toBe(true);
});
```

- [ ] **Step 2: Run to verify failure**

Run (from `frontend/`): `npx jest login.viewmodel onboarding.viewmodel auth.guard`
Expected: FAIL — guard undefined, wrong navigation targets.

- [ ] **Step 3: Update the login landing map** — in `login.viewmodel.ts`:

```typescript
const LANDING_ROUTE_BY_ROLE: Record<UserRole, string> = {
  head_coach: '/home',
  captain: '/home',
  swimmer: '/my-profile',
};
```

- [ ] **Step 4: Update onboarding completion** — in `onboarding.viewmodel.ts` `submit()` success branch, change `this.router.navigate(['/home'])` to `this.router.navigate(['/my-profile'])`.

- [ ] **Step 5: Add the home-redirect guard** — in `auth.guard.ts`:

```typescript
// swimmerHomeRedirectGuard: swimmers have no coach dashboard — send them to their profile.
// Applied to /home so the '' default and roleGuard's '/' fallback never strand a swimmer.
export const swimmerHomeRedirectGuard: CanActivateFn = () => {
  const auth = inject(AuthSessionStore);
  const router = inject(Router);
  if (auth.role() === 'swimmer') { router.navigate(['/my-profile']); return false; }
  return true;
};
```

- [ ] **Step 6: Wire the guards in `app.routes.ts`**
  - Add `swimmerHomeRedirectGuard` to the `home` route's `canActivate` (after `firstLoginGuard`).
  - Add `roleGuard('head_coach', 'captain')` to the `canActivate` arrays of `swimmers`, `swimmers/:id`, `attendance`, `championships`, and `championships/:id`.
  - Import `swimmerHomeRedirectGuard` from `@features/auth/presentation/auth.guard`.

- [ ] **Step 7: Run to verify pass**

Run (from `frontend/`): `npx jest login.viewmodel onboarding.viewmodel auth.guard`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add frontend/src/app/features/auth frontend/src/app/features/swimmer-onboarding frontend/src/app/app.routes.ts
git commit -m "feat(routing): swimmers land on /my-profile; hard-gate coach routes by role"
```

---

## Task 9: Full-suite verification

**Files:** none (verification only).

- [ ] **Step 1: Stop the dev API (release DLL locks)**

Run: `Get-Process -Name "Kheprx.BaseBackend.Api" -ErrorAction SilentlyContinue | Stop-Process -Force`

- [ ] **Step 2: Backend — full solution**

Run: `dotnet test backend/Kheprx.BaseBackend.sln --nologo`
Expected: all projects PASS, 0 failed.

- [ ] **Step 3: Frontend — full suite**

Run (from `frontend/`): `npx jest`
Expected: all suites PASS.

- [ ] **Step 4: Manual smoke (optional but recommended)** — log in as a swimmer: confirm landing on `/my-profile`, sidebar shows only My Profile + Settings, all 9 tabs render read-only (no edit/add/delete buttons), and hitting `/swimmers/<other-id>` (URL) is blocked (redirect) and the API returns 403 for a foreign id.

- [ ] **Step 5: Commit any final fixes** (only if Steps 2–3 required changes).
