# Swimmers Roster Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `GET /api/swimmers` list endpoint and replace the placeholder Angular Swimmers page with a working, searchable, gender-filterable roster.

**Architecture:** Backend mirrors the existing `GET /api/users?search=` slice — controller action → service → repository projection-join returning a flattened read-model row → DTO. Frontend mirrors the feature's existing count/create wiring — DTO → domain model → repository port/impl → use-case → standalone page component using Angular signals.

**Tech Stack:** .NET (ASP.NET Core, EF Core/PostgreSQL, xUnit + Moq, EF InMemory for repo tests) · Angular standalone components + signals, Jest, TailwindCSS.

**Spec:** `docs/superpowers/specs/2026-09-13-swimmers-roster-design.md`

## Global Constraints

- **DO NOT `git commit` or `git push` until the user explicitly authorizes it** (standing instruction, 2026-09-13). Each task's final step stages changes with `git add`; the actual commit is **deferred**. Do not run `git commit`.
- **Stop any running backend dev server before `dotnet test`** — a running API locks the build-output DLLs and the test build will fail.
- Backend gender codes are seeded as `"male"` / `"female"` (see `IdentitySeeder.cs`) — these match the frontend `Gender` type verbatim; no code translation is needed.
- `EF.Functions.ILike` is PostgreSQL-only and does **not** translate on the EF InMemory provider. Repository tests must call `ListAsync(null)` (no search term); search-term passthrough is verified at the service level with a mocked repository (same split the codebase already uses for users).
- Both `en.json` and `ar.json` must stay structurally parallel — every key added to one is added to the other.
- The `/swimmers` route and the `SWIMMER_PROVIDERS` repository binding (in `app.config.ts`) already exist and are global. No routing or provider wiring changes are needed.

---

## File Structure

**Backend (new):**
- `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/ReadModels/SwimmerListRow.cs` — flattened roster row (projection target).

**Backend (modified):**
- `.../Identity.Domain/Repositories/ISwimmerProfileRepository.cs` — add `ListAsync`.
- `.../Identity.Infrastructure/Repositories/SwimmerProfileRepository.cs` — implement `ListAsync`.
- `.../Identity.Application/DTOs/SwimmerDtos.cs` — add `SwimmerListItemDto`.
- `.../Identity.Application/Services/Interfaces/ISwimmerService.cs` — add `ListAsync`.
- `.../Identity.Application/Services/SwimmerService.cs` — implement `ListAsync` + `ComputeAge`.
- `.../Identity.Application/Resources/SwimmerMessages.cs` — add `SwimmersListed`.
- `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs` — add `List` action.

**Backend (tests):**
- `.../Identity.UnitTests/Repositories/SwimmerProfileRepositoryTests.cs`
- `.../Identity.UnitTests/Services/SwimmerServiceTests.cs`
- `.../Api.UnitTests/SwimmersControllerTests.cs`

**Frontend (new):**
- `.../features/swimmers/data/dto/swimmer-list.dto.ts`
- `.../features/swimmers/domain/usecases/list-swimmers.use-case.ts`
- `.../features/swimmers/testing/domain/usecases/list-swimmers.use-case.spec.ts`
- `.../features/swimmers/testing/presentation/pages/swimmers/swimmers.page.spec.ts`

**Frontend (modified):**
- `.../features/swimmers/domain/model/swimmer.ts` — add `SwimmerListItem`.
- `.../features/swimmers/domain/repositories/swimmer.repository.ts` — add `list`.
- `.../features/swimmers/data/repositories/swimmer.repository.impl.ts` — implement `list`.
- `.../features/swimmers/testing/data/repositories/swimmer.repository.impl.spec.ts` — add `list` tests.
- `.../features/swimmers/presentation/pages/swimmers/swimmers.page.ts` — rewrite.
- `.../features/swimmers/presentation/pages/swimmers/swimmers.page.html` — rewrite.
- `.../core/i18n/en.json` and `.../core/i18n/ar.json` — add `swimmers` block.

---

## Task 1: Backend — repository `ListAsync` + read model

**Files:**
- Create: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/ReadModels/SwimmerListRow.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/ISwimmerProfileRepository.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/SwimmerProfileRepository.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/SwimmerProfileRepositoryTests.cs`

**Interfaces:**
- Produces: `SwimmerListRow(Guid Id, string Uid, string NameEn, string? NameAr, string? ClubNameEn, string? ClubNameAr, string? GenderCode, DateOnly? Dob)` and `ISwimmerProfileRepository.ListAsync(string? search = null, CancellationToken ct = default) : Task<IReadOnlyList<SwimmerListRow>>`.

- [ ] **Step 1: Create the read-model record**

Create `SwimmerListRow.cs`:

```csharp
namespace Kheprx.BaseBackend.Identity.Domain.ReadModels;

/// <summary>Flattened swimmer row for the roster list — a join of profile + user + club + gender.</summary>
public sealed record SwimmerListRow(
    Guid Id,
    string Uid,
    string NameEn,
    string? NameAr,
    string? ClubNameEn,
    string? ClubNameAr,
    string? GenderCode,
    DateOnly? Dob);
```

- [ ] **Step 2: Add `ListAsync` to the repository interface**

In `ISwimmerProfileRepository.cs`, add the `using` and the method:

```csharp
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.ReadModels;

namespace Kheprx.BaseBackend.Identity.Domain.Repositories;

public interface ISwimmerProfileRepository
{
    Task<int> CountAsync(CancellationToken ct = default);
    Task<int> GetMaxUidNumberAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SwimmerListRow>> ListAsync(string? search = null, CancellationToken ct = default);
    Task AddAsync(SwimmerProfile profile, CancellationToken ct = default);
    Task AddSpecializationsAsync(IEnumerable<SwimmerSpecialization> specializations, CancellationToken ct = default);
    Task<bool> SaveChangesAsync(CancellationToken ct = default);  // true = saved; false = unique-index conflict
}
```

- [ ] **Step 3: Write the failing repository test**

In `SwimmerProfileRepositoryTests.cs`, add the `using` for read models at the top (`using Kheprx.BaseBackend.Identity.Domain.ReadModels;`) and this test method inside the class:

```csharp
[Fact]
public async Task ListAsync_joins_user_club_gender_and_orders_by_name()
{
    await using var db = NewDb();

    var gender = new Gender("male", "Male", "ذكر");
    var club = new Club("Oasis Main", "الواحة");
    db.Genders.Add(gender);
    db.Clubs.Add(club);

    var userB = new AppUser("b.user", "Bravo", Guid.NewGuid(), genderId: gender.Id, dob: new DateOnly(2010, 1, 1));
    var userA = new AppUser("a.user", "Alpha", Guid.NewGuid(), genderId: gender.Id, dob: new DateOnly(2012, 6, 1));
    db.Users.AddRange(userA, userB);

    db.SwimmerProfiles.Add(new SwimmerProfile(userB.Id, "SW-0002", club.Id));
    db.SwimmerProfiles.Add(new SwimmerProfile(userA.Id, "SW-0001", club.Id));
    await db.SaveChangesAsync();

    var repo = new SwimmerProfileRepository(db);
    var rows = await repo.ListAsync(null);

    Assert.Equal(2, rows.Count);
    Assert.Equal("Alpha", rows[0].NameEn);            // ordered by NameEn
    Assert.Equal("Bravo", rows[1].NameEn);
    Assert.Equal("SW-0001", rows[0].Uid);
    Assert.Equal("Oasis Main", rows[0].ClubNameEn);
    Assert.Equal("male", rows[0].GenderCode);
    Assert.Equal(new DateOnly(2012, 6, 1), rows[0].Dob);
}
```

- [ ] **Step 4: Run the test to verify it fails**

Ensure no backend dev server is running, then run from the repo root:

```
dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~SwimmerProfileRepositoryTests.ListAsync_joins_user_club_gender_and_orders_by_name"
```

Expected: FAIL — `ISwimmerProfileRepository` does not contain a definition for `ListAsync` (compile error).

- [ ] **Step 5: Implement `ListAsync` in the repository**

In `SwimmerProfileRepository.cs`, add `using Kheprx.BaseBackend.Identity.Domain.ReadModels;` at the top, then add this method to the class:

```csharp
public async Task<IReadOnlyList<SwimmerListRow>> ListAsync(string? search = null, CancellationToken ct = default)
{
    var query =
        from p in _db.SwimmerProfiles.AsNoTracking()
        join u in _db.Users.AsNoTracking() on p.UserId equals u.Id
        join c in _db.Clubs.AsNoTracking() on p.TrainingClubId equals c.Id into clubs
        from c in clubs.DefaultIfEmpty()
        join g in _db.Genders.AsNoTracking() on u.GenderId equals g.Id into genders
        from g in genders.DefaultIfEmpty()
        select new { p, u, c, g };

    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim()
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
        var pattern = "%" + term + "%";
        query = query.Where(x =>
            EF.Functions.ILike(x.u.NameEn, pattern) ||
            (x.u.NameAr != null && EF.Functions.ILike(x.u.NameAr, pattern)) ||
            EF.Functions.ILike(x.p.Uid, pattern));
    }

    return await query
        .OrderBy(x => x.u.NameEn)
        .Select(x => new SwimmerListRow(
            x.p.Id,
            x.p.Uid,
            x.u.NameEn,
            x.u.NameAr,
            x.c == null ? null : x.c.NameEn,
            x.c == null ? null : x.c.NameAr,
            x.g == null ? null : x.g.Code,
            x.u.Dob))
        .ToListAsync(ct);
}
```

- [ ] **Step 6: Run the test to verify it passes**

```
dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~SwimmerProfileRepositoryTests.ListAsync_joins_user_club_gender_and_orders_by_name"
```

Expected: PASS.

- [ ] **Step 7: Stage changes (commit deferred — see Global Constraints)**

```
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/ReadModels/SwimmerListRow.cs backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Domain/Repositories/ISwimmerProfileRepository.cs backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Infrastructure/Repositories/SwimmerProfileRepository.cs backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Repositories/SwimmerProfileRepositoryTests.cs
```

Do **not** commit. Intended message when authorized: `feat(swimmers): add ListAsync repository read for roster`.

---

## Task 2: Backend — service `ListAsync` + DTO + message

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/SwimmerDtos.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/ISwimmerService.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs`
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Resources/SwimmerMessages.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs`

**Interfaces:**
- Consumes: `ISwimmerProfileRepository.ListAsync` and `SwimmerListRow` (Task 1).
- Produces: `SwimmerListItemDto(Guid Id, string Uid, string NameEn, string? NameAr, string? ClubNameEn, string? ClubNameAr, string GenderCode, int? Age)` and `ISwimmerService.ListAsync(string? search = null, CancellationToken ct = default) : Task<IReadOnlyList<SwimmerListItemDto>>`.

- [ ] **Step 1: Add the DTO**

In `SwimmerDtos.cs`, append:

```csharp
/// <summary>One swimmer row for the roster list (GET /api/swimmers).</summary>
public sealed record SwimmerListItemDto(
    Guid Id,
    string Uid,
    string NameEn,
    string? NameAr,
    string? ClubNameEn,
    string? ClubNameAr,
    string GenderCode,
    int? Age);
```

- [ ] **Step 2: Add `ListAsync` to the service interface**

In `ISwimmerService.cs`:

```csharp
using Kheprx.BaseBackend.Identity.Application.DTOs;

namespace Kheprx.BaseBackend.Identity.Application.Services.Interfaces;

public interface ISwimmerService
{
    Task<SwimmerCountDto> GetCountAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SwimmerListItemDto>> ListAsync(string? search = null, CancellationToken ct = default);
    Task<CreatedSwimmerDto?> CreateAsync(CreateSwimmerRequest request, CancellationToken ct = default);
}
```

- [ ] **Step 3: Add the success message**

In `SwimmerMessages.cs`, inside `public static class Success`, add:

```csharp
public static string SwimmersListed(string lang) => lang switch
{
    "ar" => "قائمة السبّاحين",
    _ => "Swimmers list"
};
```

- [ ] **Step 4: Write the failing service test**

In `SwimmerServiceTests.cs`, add `using Kheprx.BaseBackend.Identity.Domain.ReadModels;` at the top, then add:

```csharp
[Fact]
public async Task List_maps_rows_computes_age_and_passes_search()
{
    var (svc, swimmers, _) = Build();
    var dob = new DateOnly(2010, 3, 15);
    var rows = new[]
    {
        new SwimmerListRow(Guid.NewGuid(), "SW-0001", "Alpha", "ألفا", "Oasis Main", "الواحة", "male", dob),
        new SwimmerListRow(Guid.NewGuid(), "SW-0002", "Bravo", null, null, null, null, null),
    };
    swimmers.Setup(r => r.ListAsync("al", It.IsAny<CancellationToken>())).ReturnsAsync(rows);

    var result = await svc.ListAsync("al");

    swimmers.Verify(r => r.ListAsync("al", It.IsAny<CancellationToken>()), Times.Once);
    Assert.Equal(2, result.Count);
    Assert.Equal("SW-0001", result[0].Uid);
    Assert.Equal("Oasis Main", result[0].ClubNameEn);
    Assert.Equal("male", result[0].GenderCode);

    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var expectedAge = today.Year - dob.Year;
    if (dob > today.AddYears(-expectedAge)) expectedAge--;
    Assert.Equal(expectedAge, result[0].Age);

    Assert.Null(result[1].Age);                        // null Dob → null age
    Assert.Equal(string.Empty, result[1].GenderCode);  // null gender code → ""
    Assert.Null(result[1].ClubNameEn);
}
```

- [ ] **Step 5: Run the test to verify it fails**

```
dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~SwimmerServiceTests.List_maps_rows_computes_age_and_passes_search"
```

Expected: FAIL — `SwimmerService` does not contain a definition for `ListAsync` (compile error).

- [ ] **Step 6: Implement `ListAsync` + `ComputeAge` in the service**

In `SwimmerService.cs`, add `using Kheprx.BaseBackend.Identity.Domain.ReadModels;` at the top, then add these members to the class:

```csharp
public async Task<IReadOnlyList<SwimmerListItemDto>> ListAsync(string? search = null, CancellationToken ct = default)
{
    var rows = await _swimmers.ListAsync(search, ct);
    return rows.Select(r => new SwimmerListItemDto(
        r.Id, r.Uid, r.NameEn, r.NameAr, r.ClubNameEn, r.ClubNameAr,
        r.GenderCode ?? string.Empty, ComputeAge(r.Dob))).ToList();
}

private static int? ComputeAge(DateOnly? dob)
{
    if (dob is null) return null;
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var age = today.Year - dob.Value.Year;
    if (dob.Value > today.AddYears(-age)) age--;
    return age;
}
```

- [ ] **Step 7: Run the test to verify it passes**

```
dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~SwimmerServiceTests.List_maps_rows_computes_age_and_passes_search"
```

Expected: PASS.

- [ ] **Step 8: Stage changes (commit deferred)**

```
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/DTOs/SwimmerDtos.cs backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/Interfaces/ISwimmerService.cs backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/SwimmerService.cs backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Resources/SwimmerMessages.cs backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/SwimmerServiceTests.cs
```

Intended message: `feat(swimmers): map roster rows to DTO with computed age`.

---

## Task 3: Backend — controller `GET /api/swimmers`

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs`
- Test: `backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs`

**Interfaces:**
- Consumes: `ISwimmerService.ListAsync` and `SwimmerListItemDto` (Task 2).
- Produces: `SwimmersController.List(string? search, CancellationToken ct)` returning `ActionResult<ApiResponse<IReadOnlyList<SwimmerListItemDto>>>`.

- [ ] **Step 1: Write the failing controller test**

In `SwimmersControllerTests.cs`, add:

```csharp
[Fact]
public async Task List_returns_200_with_swimmers()
{
    var svc = new Mock<ISwimmerService>();
    var rows = new List<SwimmerListItemDto>
    {
        new(Guid.NewGuid(), "SW-0001", "Alpha", null, "Oasis Main", null, "male", 15),
    };
    svc.Setup(s => s.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
       .ReturnsAsync(rows);

    var result = await new SwimmersController(svc.Object).List("al", CancellationToken.None);

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var body = Assert.IsType<ApiResponse<IReadOnlyList<SwimmerListItemDto>>>(ok.Value);
    Assert.True(body.SuccessStatus);
    Assert.Single(body.Data!);
    Assert.Equal("SW-0001", body.Data![0].Uid);
}
```

- [ ] **Step 2: Run the test to verify it fails**

```
dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter "FullyQualifiedName~SwimmersControllerTests.List_returns_200_with_swimmers"
```

Expected: FAIL — `SwimmersController` does not contain a definition for `List` (compile error).

- [ ] **Step 3: Add the `List` action**

In `SwimmersController.cs`, add this region immediately after the `Count` region (before `Create`). `Microsoft.AspNetCore.Authorization` is already imported.

```csharp
#region List — GET api/swimmers — roster list, optional search

// مستخدم في:
// 1. صفحة السبّاحين (/swimmers) — القائمة + البحث
/// <summary>Lists registered swimmers, optionally filtered by a search term (name or UID).</summary>
/// <remarks>Any authenticated user may view the roster.</remarks>
/// <response code="200">The matching swimmers.</response>
[HttpGet]
[Authorize]
[ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SwimmerListItemDto>>), StatusCodes.Status200OK)]
public async Task<ActionResult<ApiResponse<IReadOnlyList<SwimmerListItemDto>>>> List(
    [FromQuery] string? search, CancellationToken ct)
{
    var swimmers = await _service.ListAsync(search, ct);
    var successMessage = SwimmerMessages.Success.SwimmersListed(AppLanguage.Current);
    var body = ApiResponse<IReadOnlyList<SwimmerListItemDto>>.Success(successMessage, swimmers);
    return Ok(body);
}

#endregion
```

- [ ] **Step 4: Run the test to verify it passes**

```
dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests --filter "FullyQualifiedName~SwimmersControllerTests.List_returns_200_with_swimmers"
```

Expected: PASS.

- [ ] **Step 5: Run the full backend test suite for both projects**

```
dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests
dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests
```

Expected: PASS (no regressions).

- [ ] **Step 6: Stage changes (commit deferred)**

```
git add backend/Kheprx.BaseBackend.Api/Controllers/SwimmersController.cs backend/tests/Kheprx.BaseBackend.Api.UnitTests/SwimmersControllerTests.cs
```

Intended message: `feat(swimmers): add GET /api/swimmers roster endpoint`.

---

## Task 4: Frontend — DTO, domain model, repository port + impl

**Files:**
- Create: `frontend/src/app/features/swimmers/data/dto/swimmer-list.dto.ts`
- Modify: `frontend/src/app/features/swimmers/domain/model/swimmer.ts`
- Modify: `frontend/src/app/features/swimmers/domain/repositories/swimmer.repository.ts`
- Modify: `frontend/src/app/features/swimmers/data/repositories/swimmer.repository.impl.ts`
- Test: `frontend/src/app/features/swimmers/testing/data/repositories/swimmer.repository.impl.spec.ts`

**Interfaces:**
- Produces: `SwimmerListItemDtoRs`, `SwimmerListItemsDtoRs`, `isSwimmerListItemDtoRsValid`, `SwimmerListItem` model, and `ISwimmerRepository.list(search?: string): Promise<SwimmerListItemsDtoRs>`.

All frontend commands run from the `frontend/` directory.

- [ ] **Step 1: Create the DTO**

Create `swimmer-list.dto.ts`:

```typescript
// swimmer-list.dto.ts — swimmer roster response DTO (API_FLOW convention).
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface SwimmerListItemDtoRs {
  id: string;
  uid: string;
  nameEn: string;
  nameAr?: string | null;
  clubNameEn?: string | null;
  clubNameAr?: string | null;
  genderCode: string;
  age?: number | null;
}

export interface SwimmerListItemsDtoRs extends BaseResponseRs<SwimmerListItemDtoRs[]> {}

export function isSwimmerListItemDtoRsValid(dto: unknown): dto is SwimmerListItemDtoRs {
  const d = dto as SwimmerListItemDtoRs;
  return !!d && typeof d.id === 'string' && typeof d.uid === 'string'
    && typeof d.nameEn === 'string' && typeof d.genderCode === 'string';
}
```

- [ ] **Step 2: Add the domain model**

In `domain/model/swimmer.ts`, append:

```typescript
export interface SwimmerListItem {
  id: string;
  uid: string;
  nameEn: string;
  nameAr: string | null;
  clubNameEn: string | null;
  clubNameAr: string | null;
  gender: 'male' | 'female' | null;
  age: number | null;
}
```

- [ ] **Step 3: Add `list` to the repository port**

In `domain/repositories/swimmer.repository.ts`, add the import and the method:

```typescript
import { InjectionToken } from '@angular/core';
import { SwimmerCountItemDtoRs } from '@features/swimmers/data/dto/swimmer-count.dto';
import { CreateSwimmerDtoRq, CreatedSwimmerItemDtoRs } from '@features/swimmers/data/dto/create-swimmer.dto';
import { SwimmerListItemsDtoRs } from '@features/swimmers/data/dto/swimmer-list.dto';

export interface ISwimmerRepository {
  getCount(): Promise<SwimmerCountItemDtoRs>;
  list(search?: string): Promise<SwimmerListItemsDtoRs>;
  create(rq: CreateSwimmerDtoRq): Promise<CreatedSwimmerItemDtoRs>;
}

export const SWIMMER_REPOSITORY = new InjectionToken<ISwimmerRepository>('SWIMMER_REPOSITORY');
```

- [ ] **Step 4: Write the failing repository-impl test**

In `swimmer.repository.impl.spec.ts`, add these two `it` blocks inside the existing `describe`:

```typescript
it('list GETs /api/swimmers with the search param', async () => {
  (http.get as jest.Mock).mockResolvedValue({ data: [] });
  await repo.list('ali');
  expect(http.get).toHaveBeenCalledWith('/api/swimmers', { params: { search: 'ali' } });
});

it('list GETs /api/swimmers with no options when search is empty', async () => {
  (http.get as jest.Mock).mockResolvedValue({ data: [] });
  await repo.list('');
  expect(http.get).toHaveBeenCalledWith('/api/swimmers', undefined);
});
```

- [ ] **Step 5: Run the test to verify it fails**

```
npx jest swimmer.repository.impl
```

Expected: FAIL — `repo.list is not a function` (method not implemented yet).

- [ ] **Step 6: Implement `list` in the repository impl**

In `swimmer.repository.impl.ts`, add the import and the method:

```typescript
import { SwimmerListItemsDtoRs } from '@features/swimmers/data/dto/swimmer-list.dto';
```

Add to the class body:

```typescript
list(search?: string): Promise<SwimmerListItemsDtoRs> {
  const term = search?.trim();
  return this.http.get<SwimmerListItemsDtoRs>('/api/swimmers', term ? { params: { search: term } } : undefined);
}
```

- [ ] **Step 7: Run the test to verify it passes**

```
npx jest swimmer.repository.impl
```

Expected: PASS (all blocks, including the pre-existing count/create ones).

- [ ] **Step 8: Stage changes (commit deferred)**

```
git add frontend/src/app/features/swimmers/data/dto/swimmer-list.dto.ts frontend/src/app/features/swimmers/domain/model/swimmer.ts frontend/src/app/features/swimmers/domain/repositories/swimmer.repository.ts frontend/src/app/features/swimmers/data/repositories/swimmer.repository.impl.ts frontend/src/app/features/swimmers/testing/data/repositories/swimmer.repository.impl.spec.ts
```

Intended message: `feat(swimmers): add roster DTO, model, and repository list()`.

---

## Task 5: Frontend — `ListSwimmersUseCase`

**Files:**
- Create: `frontend/src/app/features/swimmers/domain/usecases/list-swimmers.use-case.ts`
- Test: `frontend/src/app/features/swimmers/testing/domain/usecases/list-swimmers.use-case.spec.ts`

**Interfaces:**
- Consumes: `ISwimmerRepository.list`, `SwimmerListItemDtoRs`, `isSwimmerListItemDtoRsValid` (Task 4), `SwimmerListItem` (Task 4).
- Produces: `ListSwimmersUseCase` with `run(search?: string): Promise<Result<SwimmerListItem[]>>`.

- [ ] **Step 1: Write the failing use-case spec**

Create `list-swimmers.use-case.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { SWIMMER_REPOSITORY, ISwimmerRepository } from '@features/swimmers/domain/repositories/swimmer.repository';

function makeRepo(overrides: Partial<ISwimmerRepository> = {}): ISwimmerRepository {
  return {
    list: async () => ({ data: [
      { id: '1', uid: 'SW-0001', nameEn: 'Alpha', nameAr: 'ألفا', clubNameEn: 'Oasis Main', clubNameAr: null, genderCode: 'male', age: 15 },
    ] }),
    ...overrides,
  } as unknown as ISwimmerRepository;
}

function build(repo: ISwimmerRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: SWIMMER_REPOSITORY, useValue: repo }] });
  return TestBed.inject(ListSwimmersUseCase);
}

describe('ListSwimmersUseCase', () => {
  it('maps DTOs to domain models', async () => {
    const uc = build(makeRepo());
    const r = await uc.run(undefined);
    expect(r.ok).toBe(true);
    if (r.ok) {
      expect(r.data).toHaveLength(1);
      expect(r.data[0].uid).toBe('SW-0001');
      expect(r.data[0].gender).toBe('male');
      expect(r.data[0].age).toBe(15);
    }
  });

  it('drops malformed items and coerces unknown gender to null', async () => {
    const uc = build(makeRepo({ list: async () => ({ data: [
      { id: '2', uid: 'SW-0002', nameEn: 'Bravo', genderCode: '', age: null } as never,
      { uid: 'broken' } as never,
    ] }) }));
    const r = await uc.run(undefined);
    expect(r.ok).toBe(true);
    if (r.ok) {
      expect(r.data).toHaveLength(1);
      expect(r.data[0].gender).toBeNull();
      expect(r.data[0].age).toBeNull();
    }
  });
});
```

- [ ] **Step 2: Run the spec to verify it fails**

```
npx jest list-swimmers.use-case
```

Expected: FAIL — cannot find module `list-swimmers.use-case` (file not created yet).

- [ ] **Step 3: Implement the use-case**

Create `list-swimmers.use-case.ts`:

```typescript
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_REPOSITORY } from '@features/swimmers/domain/repositories/swimmer.repository';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';
import { SwimmerListItemDtoRs, isSwimmerListItemDtoRsValid } from '@features/swimmers/data/dto/swimmer-list.dto';

// ListSwimmersUseCase: fetches the roster (GET /api/swimmers), optionally filtered by a
// server-side search term. Drops any row that fails validation, so a single malformed
// item never blanks the whole list.
@Injectable({ providedIn: 'root' })
export class ListSwimmersUseCase extends UseCase<string | undefined, SwimmerListItem[]> {
  private readonly repo = inject(SWIMMER_REPOSITORY);
  constructor() { super('ListSwimmers'); }

  protected async execute(search?: string): Promise<SwimmerListItem[]> {
    const res = await this.repo.list(search);
    const items = Array.isArray(res.data) ? res.data : [];
    return items.filter(isSwimmerListItemDtoRsValid).map(toModel);
  }
}

function toModel(d: SwimmerListItemDtoRs): SwimmerListItem {
  return {
    id: d.id,
    uid: d.uid,
    nameEn: d.nameEn,
    nameAr: d.nameAr ?? null,
    clubNameEn: d.clubNameEn ?? null,
    clubNameAr: d.clubNameAr ?? null,
    gender: d.genderCode === 'male' || d.genderCode === 'female' ? d.genderCode : null,
    age: typeof d.age === 'number' ? d.age : null,
  };
}
```

- [ ] **Step 4: Run the spec to verify it passes**

```
npx jest list-swimmers.use-case
```

Expected: PASS.

- [ ] **Step 5: Stage changes (commit deferred)**

```
git add frontend/src/app/features/swimmers/domain/usecases/list-swimmers.use-case.ts frontend/src/app/features/swimmers/testing/domain/usecases/list-swimmers.use-case.spec.ts
```

Intended message: `feat(swimmers): add ListSwimmersUseCase`.

---

## Task 6: Frontend — roster page + i18n

**Files:**
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`
- Modify: `frontend/src/app/features/swimmers/presentation/pages/swimmers/swimmers.page.ts`
- Modify: `frontend/src/app/features/swimmers/presentation/pages/swimmers/swimmers.page.html`
- Test: `frontend/src/app/features/swimmers/testing/presentation/pages/swimmers/swimmers.page.spec.ts`

**Interfaces:**
- Consumes: `ListSwimmersUseCase` (Task 5), `SwimmerListItem` (Task 4), `LanguageStore`, `Gender`, `GENDER_LABELS`.

- [ ] **Step 1: Add the `swimmers` i18n block (English)**

In `en.json`, add this top-level key (e.g. after the `gender` block; remember to keep valid JSON commas):

```json
"swimmers": {
  "title": "Swimmers",
  "subtitle": "Manage and track all registered swimmers",
  "searchPlaceholder": "Search by name or ID…",
  "filterGender": "Gender",
  "all": "All",
  "count": "swimmers",
  "loading": "Loading swimmers…",
  "empty": { "title": "No swimmers found", "desc": "Try adjusting your search or filter." },
  "error": "Couldn't load swimmers. Please try again."
},
```

- [ ] **Step 2: Add the `swimmers` i18n block (Arabic)**

In `ar.json`, add the parallel key at the matching position:

```json
"swimmers": {
  "title": "السبّاحون",
  "subtitle": "إدارة ومتابعة جميع السبّاحين المسجلين",
  "searchPlaceholder": "ابحث بالاسم أو الرقم…",
  "filterGender": "النوع",
  "all": "الكل",
  "count": "سبّاح",
  "loading": "جارٍ تحميل السبّاحين…",
  "empty": { "title": "لا يوجد سبّاحون", "desc": "جرّب تعديل البحث أو الفلتر." },
  "error": "تعذّر تحميل السبّاحين. حاول مرة أخرى."
},
```

- [ ] **Step 3: Write the failing page spec**

Create `swimmers.page.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { SwimmersPage } from '@features/swimmers/presentation/pages/swimmers/swimmers.page';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';
import { ok } from '@core/domain/result/result';

const sample: SwimmerListItem[] = [
  { id: '1', uid: 'SW-0001', nameEn: 'Alpha', nameAr: null, clubNameEn: 'Oasis Main', clubNameAr: null, gender: 'male', age: 15 },
  { id: '2', uid: 'SW-0002', nameEn: 'Bravo', nameAr: null, clubNameEn: 'Oasis North', clubNameAr: null, gender: 'female', age: 16 },
];

function setup(items: SwimmerListItem[]) {
  const run = jest.fn().mockResolvedValue(ok(items));
  TestBed.configureTestingModule({
    imports: [SwimmersPage],
    providers: [{ provide: ListSwimmersUseCase, useValue: { run } }],
  });
  const fixture = TestBed.createComponent(SwimmersPage);
  fixture.detectChanges(); // ngOnInit -> load()
  return { fixture, run };
}

describe('SwimmersPage', () => {
  it('renders swimmers after load', async () => {
    const { fixture } = setup(sample);
    await fixture.whenStable();
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Alpha');
    expect(text).toContain('SW-0001');
    expect(text).toContain('Bravo');
  });

  it('filters by gender client-side', async () => {
    const { fixture } = setup(sample);
    await fixture.whenStable();
    fixture.detectChanges();
    fixture.componentInstance.setGenderFilter('female');
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Bravo');
    expect(text).not.toContain('Alpha');
  });
});
```

- [ ] **Step 4: Run the spec to verify it fails**

```
npx jest swimmers.page
```

Expected: FAIL — `setGenderFilter` / signals not present (the placeholder component has none of these members).

- [ ] **Step 5: Rewrite the page component**

Replace the entire contents of `swimmers.page.ts` with:

```typescript
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@core/i18n';
import { LanguageStore } from '@core/i18n/language.store';
import { Gender } from '@core/domain/gender/gender';
import { GENDER_LABELS } from '@core/domain/gender/gender-labels';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';

type GenderFilter = 'all' | Gender;

// Swimmers roster: server-side search (debounced re-fetch) + client-side gender filter.
// Attendance is intentionally absent — the backend has no attendance data (see spec).
@Component({
  selector: 'app-swimmers-page',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './swimmers.page.html',
})
export class SwimmersPage implements OnInit {
  private readonly listSwimmers = inject(ListSwimmersUseCase);
  private readonly language = inject(LanguageStore);

  readonly loading = signal(true);
  readonly error = signal(false);
  readonly search = signal('');
  readonly genderFilter = signal<GenderFilter>('all');
  private readonly swimmers = signal<SwimmerListItem[]>([]);

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  readonly visible = computed<SwimmerListItem[]>(() => {
    const g = this.genderFilter();
    const list = this.swimmers();
    return g === 'all' ? list : list.filter((s) => s.gender === g);
  });

  ngOnInit(): void { void this.load(); }

  onSearchInput(value: string): void {
    this.search.set(value);
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => void this.load(), 300);
  }

  setGenderFilter(value: string): void {
    this.genderFilter.set(value === 'male' || value === 'female' ? value : 'all');
  }

  displayName(s: SwimmerListItem): string {
    return this.language.lang() === 'ar' ? (s.nameAr ?? s.nameEn) : s.nameEn;
  }

  clubName(s: SwimmerListItem): string {
    const ar = this.language.lang() === 'ar';
    return (ar ? s.clubNameAr ?? s.clubNameEn : s.clubNameEn) ?? '';
  }

  genderLabelKey(s: SwimmerListItem): string {
    return s.gender ? GENDER_LABELS[s.gender] : '';
  }

  initials(s: SwimmerListItem): string {
    const parts = s.nameEn.trim().split(/\s+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(false);
    const term = this.search().trim();
    const result = await this.listSwimmers.run(term ? term : undefined);
    if (result.ok) {
      this.swimmers.set(result.data);
    } else {
      this.error.set(true);
      this.swimmers.set([]);
    }
    this.loading.set(false);
  }
}
```

- [ ] **Step 6: Rewrite the page template**

Replace the entire contents of `swimmers.page.html` with:

```html
<div class="mx-auto max-w-5xl px-4 py-8 sm:px-6">
  <header class="mb-6">
    <h1 class="font-heading text-3xl text-ink">{{ 'swimmers.title' | translate }}</h1>
    <p class="mt-1 text-text-secondary">{{ 'swimmers.subtitle' | translate }}</p>
  </header>

  <div class="rounded-2xl border border-border bg-surface shadow-sm">
    <!-- Controls -->
    <div class="flex flex-col gap-3 border-b border-border px-4 py-3 sm:flex-row sm:items-center">
      <input
        type="search"
        [ngModel]="search()"
        (ngModelChange)="onSearchInput($event)"
        [attr.placeholder]="'swimmers.searchPlaceholder' | translate"
        [attr.aria-label]="'swimmers.searchPlaceholder' | translate"
        class="h-9 w-full rounded-lg border border-border bg-surface px-3 text-sm text-ink sm:max-w-xs" />

      <select
        [ngModel]="genderFilter()"
        (ngModelChange)="setGenderFilter($event)"
        [attr.aria-label]="'swimmers.filterGender' | translate"
        class="h-9 rounded-lg border border-border bg-surface px-3 text-sm text-ink">
        <option value="all">{{ 'swimmers.all' | translate }}</option>
        <option value="male">{{ 'gender.male' | translate }}</option>
        <option value="female">{{ 'gender.female' | translate }}</option>
      </select>

      <span class="text-xs text-text-secondary sm:ms-auto">
        {{ visible().length }} {{ 'swimmers.count' | translate }}
      </span>
    </div>

    <!-- States -->
    @if (loading()) {
      <div class="px-5 py-10 text-center text-sm text-text-secondary">{{ 'swimmers.loading' | translate }}</div>
    } @else if (error()) {
      <div class="px-5 py-10 text-center text-sm text-danger">{{ 'swimmers.error' | translate }}</div>
    } @else if (visible().length === 0) {
      <div class="px-5 py-12 text-center">
        <p class="text-sm font-medium text-ink">{{ 'swimmers.empty.title' | translate }}</p>
        <p class="mt-1 text-xs text-text-secondary">{{ 'swimmers.empty.desc' | translate }}</p>
      </div>
    } @else {
      <ul class="divide-y divide-border">
        @for (s of visible(); track s.id) {
          <li class="flex items-center gap-4 px-5 py-4">
            <div class="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-primary/10 text-sm font-semibold text-primary">
              {{ initials(s) }}
            </div>
            <div class="min-w-0 flex-1">
              <p class="truncate text-sm font-medium text-ink">{{ displayName(s) }}</p>
              <p class="mt-0.5 truncate text-xs text-text-secondary">{{ s.uid }}</p>
            </div>
            <div class="hidden w-32 shrink-0 truncate text-sm text-text-secondary md:block">{{ clubName(s) }}</div>
            <div class="hidden w-24 shrink-0 text-xs text-text-secondary sm:block">
              @if (s.gender) { <span>{{ genderLabelKey(s) | translate }}</span> }
              @if (s.gender && s.age !== null) { <span> · </span> }
              @if (s.age !== null) { <span>{{ s.age }}</span> }
            </div>
          </li>
        }
      </ul>
    }
  </div>
</div>
```

- [ ] **Step 7: Run the spec to verify it passes**

```
npx jest swimmers.page
```

Expected: PASS.

- [ ] **Step 8: Run the full frontend suite + a production build**

```
npx jest
npm run build
```

Expected: all Jest suites PASS; the Angular build succeeds (no template/type errors).

- [ ] **Step 9: Stage changes (commit deferred)**

```
git add frontend/src/app/core/i18n/en.json frontend/src/app/core/i18n/ar.json frontend/src/app/features/swimmers/presentation/pages/swimmers/swimmers.page.ts frontend/src/app/features/swimmers/presentation/pages/swimmers/swimmers.page.html frontend/src/app/features/swimmers/testing/presentation/pages/swimmers/swimmers.page.spec.ts
```

Intended message: `feat(swimmers): build roster page with search and gender filter`.

---

## Final verification

- [ ] **Backend:** `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests` and `dotnet test backend/tests/Kheprx.BaseBackend.Api.UnitTests` both green (dev server stopped first).
- [ ] **Frontend:** `npx jest` all green; `npm run build` succeeds.
- [ ] **Manual smoke (optional):** run the API + `npm start`, sign in, open `/swimmers`, confirm the roster loads, search filters server-side, and the gender dropdown filters client-side.
- [ ] **Commit:** only after the user authorizes — then commit the staged tasks (one commit per task, using the intended messages above) and report.
