# Class Structure Regions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give all 5 Application services and all 5 API controllers one consistent class layout — `Fields` / `Constructor` / `APIs` / `Helpers` regions, block-body constructors, and named-local mapping bodies in the two lookup services — with zero behavior change.

**Architecture:** Pure structural refactor. Regions are compile-time-invisible; the 7 constructor rewrites and 2 mapping rewrites are the only statement changes, and the mapping rewrites are directly covered by `RoleServiceTests` and `EngagementTypeServiceTests`. The existing 147-test suite is the safety net.

**Tech Stack:** ASP.NET Core (net10), xUnit. Solution: `backend/Kheprx.BaseBackend.sln`.

**Spec:** `backend/docs/superpowers/specs/2026-07-06-class-structure-regions-design.md`

## Global Constraints

- Zero behavior change: same status codes, envelopes, messages, DTO contents.
- Zero test edits. If any test fails, the code change is wrong — fix or revert the code, never the test.
- Region names, verbatim and in this order: `#region Fields`, `#region Constructor`, `#region APIs`, `#region Helpers` (Helpers only where it already exists). No empty regions.
- Region formatting: `#region`/`#endregion` at member indentation (4 spaces), one blank line after `#region X`, one blank line before `#endregion`, one blank line between an `#endregion` and the next `#region`.
- Everything moves into regions byte-identical (pure insertion) EXCEPT the 7 constructor rewrites and 2 mapping rewrites shown verbatim below.
- Do NOT touch: XML doc comments, attributes, routes, method signatures, `BaseApiController`, `ValidationFilter`, Infrastructure classes, `ProductService.GetAllAsync`'s expression body, or any method body not shown in a Replace/With pair.
- All commands run from the repo root `C:\Users\envnt\Desktop\Kheprx.Electric`.
- Build: `dotnet build backend/Kheprx.BaseBackend.sln` — must succeed with no new warnings.
- Test: `dotnet test backend/Kheprx.BaseBackend.sln` — every test passes, 0 failed.

---

### Task 1: Lookup services — regions, block ctors, named-local mapping

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/RoleService.cs` (whole file)
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/EngagementTypeService.cs` (whole file)
- Test (run only, never edit): `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/RoleServiceTests.cs`, `.../EngagementTypeServiceTests.cs`

**Interfaces:**
- Consumes: nothing from other tasks (each task is independent).
- Produces: nothing other tasks rely on.

These are the only two files in the plan with statement-level changes beyond the constructor. Both files are 19 lines and are replaced wholesale with the exact content below (only the class body changes; usings/namespace are repeated for completeness).

- [ ] **Step 1: Verify a green baseline before any change**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: all tests pass, 0 failed (147 total). If the baseline is red, STOP — report the failure instead of proceeding.

- [ ] **Step 2: Rewrite RoleService.cs — exact final content**

```csharp
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Domain.Repositories;

namespace Kheprx.BaseBackend.Identity.Application.Services;

internal sealed class RoleService : IRoleService
{
    #region Fields

    private readonly IRoleRepository _roles;

    #endregion

    #region Constructor

    public RoleService(IRoleRepository roles)
    {
        _roles = roles;
    }

    #endregion

    #region APIs

    public async Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken ct = default)
    {
        var roles = await _roles.GetAllAsync(ct);

        var dtos = roles
            .Select(r => new RoleDto(r.Id, r.Code, r.LabelAr, r.LabelEn, r.SortOrder))
            .ToList();

        return dtos;
    }

    #endregion
}
```

- [ ] **Step 3: Rewrite EngagementTypeService.cs — exact final content**

```csharp
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Domain.Repositories;

namespace Kheprx.BaseBackend.Identity.Application.Services;

internal sealed class EngagementTypeService : IEngagementTypeService
{
    #region Fields

    private readonly IEngagementTypeRepository _types;

    #endregion

    #region Constructor

    public EngagementTypeService(IEngagementTypeRepository types)
    {
        _types = types;
    }

    #endregion

    #region APIs

    public async Task<IReadOnlyList<EngagementTypeDto>> GetAllAsync(CancellationToken ct = default)
    {
        var types = await _types.GetAllAsync(ct);

        var dtos = types
            .Select(t => new EngagementTypeDto(t.Id, t.Code, t.LabelAr, t.LabelEn, t.SortOrder))
            .ToList();

        return dtos;
    }

    #endregion
}
```

- [ ] **Step 4: Build**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: Build succeeded, no new warnings.

- [ ] **Step 5: Run the service tests, then the full suite**

Run: `dotnet test backend/tests/Kheprx.BaseBackend.Identity.UnitTests --filter "FullyQualifiedName~RoleServiceTests|FullyQualifiedName~EngagementTypeServiceTests"`
Expected: all filtered tests pass, 0 failed.

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: 147 passed, 0 failed, with zero test-file edits (`git status` must show only the two service files modified).

- [ ] **Step 6: Commit**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/RoleService.cs backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/EngagementTypeService.cs
git commit -m "refactor(identity): restructure lookup services - regions, block ctors, named-local mapping" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: Core services — pure region insertion

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/UserService.cs` (4 insertion points)
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/AuthService.cs` (4 insertion points)
- Modify: `backend/src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Application/Services/ProductService.cs` (2 insertion points)
- Test (run only, never edit): full solution suite.

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces: nothing other tasks rely on.

Every edit below is a pure insertion of region lines — zero existing characters change. These three constructors are already block-bodied: keep them byte-identical. Apply each Replace/With pair exactly; the surrounding lines shown are unique anchors in their files.

- [ ] **Step 1: UserService.cs — open Fields region**

Replace:
```csharp
internal sealed class UserService : IUserService
{
    private static readonly IReadOnlyDictionary<string, string> CodePrefixes =
```
With:
```csharp
internal sealed class UserService : IUserService
{
    #region Fields

    private static readonly IReadOnlyDictionary<string, string> CodePrefixes =
```

- [ ] **Step 2: UserService.cs — close Fields, open Constructor**

Replace:
```csharp
    private readonly IPasswordHasher _hasher;

    public UserService(
```
With:
```csharp
    private readonly IPasswordHasher _hasher;

    #endregion

    #region Constructor

    public UserService(
```

- [ ] **Step 3: UserService.cs — close Constructor, open APIs**

Replace:
```csharp
        _hasher = hasher;
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(string? search = null, CancellationToken ct = default)
```
With:
```csharp
        _hasher = hasher;
    }

    #endregion

    #region APIs

    public async Task<IReadOnlyList<UserDto>> ListAsync(string? search = null, CancellationToken ct = default)
```

- [ ] **Step 4: UserService.cs — close APIs before the existing Helpers region**

Replace:
```csharp
        var roleCode = role?.Code ?? string.Empty;
        return Map(user, roleCode, await LoadProfileAsync(user.Id, roleCode, ct));
    }

    #region Helpers
```
With:
```csharp
        var roleCode = role?.Code ?? string.Empty;
        return Map(user, roleCode, await LoadProfileAsync(user.Id, roleCode, ct));
    }

    #endregion

    #region Helpers
```

- [ ] **Step 5: AuthService.cs — open Fields region (const + comment stay inside)**

Replace:
```csharp
internal sealed class AuthService : IAuthService
{
    // Defensive fallback only; RoleId is a required FK to an existing role, so this never triggers in practice.
    private const string FallbackRole = "worker";
```
With:
```csharp
internal sealed class AuthService : IAuthService
{
    #region Fields

    // Defensive fallback only; RoleId is a required FK to an existing role, so this never triggers in practice.
    private const string FallbackRole = "worker";
```

- [ ] **Step 6: AuthService.cs — close Fields, open Constructor**

Replace:
```csharp
    private readonly JwtOptions _options;

    public AuthService(
```
With:
```csharp
    private readonly JwtOptions _options;

    #endregion

    #region Constructor

    public AuthService(
```

- [ ] **Step 7: AuthService.cs — close Constructor, open APIs (teaching comment stays with LoginAsync)**

Replace:
```csharp
        _options = options.Value;
    }

    // About "CancellationToken ct = default" (used on every method here):
```
With:
```csharp
        _options = options.Value;
    }

    #endregion

    #region APIs

    // About "CancellationToken ct = default" (used on every method here):
```

- [ ] **Step 8: AuthService.cs — close APIs before the existing Helpers region**

Replace:
```csharp
        return new CurrentUserDto(user.Id, user.Email ?? string.Empty, user.FullName, role?.Code ?? FallbackRole);
    }

    #region Helpers
```
With:
```csharp
        return new CurrentUserDto(user.Id, user.Email ?? string.Empty, user.FullName, role?.Code ?? FallbackRole);
    }

    #endregion

    #region Helpers
```

- [ ] **Step 9: ProductService.cs — open all three regions (GetAllAsync expression body stays as-is)**

Replace:
```csharp
internal sealed class ProductService : IProductService
{
    private readonly IProductRepository _repository;
    private readonly CatalogMapper _mapper;

    public ProductService(IProductRepository repository, CatalogMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct = default)
```
With:
```csharp
internal sealed class ProductService : IProductService
{
    #region Fields

    private readonly IProductRepository _repository;
    private readonly CatalogMapper _mapper;

    #endregion

    #region Constructor

    public ProductService(IProductRepository repository, CatalogMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    #endregion

    #region APIs

    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct = default)
```

- [ ] **Step 10: ProductService.cs — close APIs at end of class (no Helpers region here)**

Replace:
```csharp
        _repository.Remove(product);
        await _repository.SaveChangesAsync(ct);
        return true;
    }
}
```
With:
```csharp
        _repository.Remove(product);
        await _repository.SaveChangesAsync(ct);
        return true;
    }

    #endregion
}
```

- [ ] **Step 11: Build**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: Build succeeded, no new warnings.

- [ ] **Step 12: Run the full test suite**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: 147 passed, 0 failed, with zero test-file edits (`git status` must show only the three service files modified). Also verify pure insertion: `git diff HEAD --numstat` must show 0 in the deleted column for all three files (expected added counts: UserService 12, AuthService 12, ProductService 12).

- [ ] **Step 13: Commit**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/UserService.cs backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/AuthService.cs backend/src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Application/Services/ProductService.cs
git commit -m "style(backend): add Fields/Constructor/APIs regions to core services" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: Controllers — regions and block-body constructors

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/AuthController.cs` (2 edit points)
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/RolesController.cs` (whole class body)
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/EngagementTypesController.cs` (whole class body)
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/UsersController.cs` (2 edit points)
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/ProductsController.cs` (2 edit points)
- Test (run only, never edit): `backend/tests/Kheprx.BaseBackend.Api.UnitTests/`

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces: nothing other tasks rely on.

Each controller gets `Fields` / `Constructor` / `APIs` regions and a block-body constructor. `BaseApiController` is untouched. All XML docs, attributes, and action bodies stay byte-identical — the head-of-class Replace/With pairs below rewrite only the field + constructor lines and insert region markers; the tail pairs insert `#endregion` only.

- [ ] **Step 1: AuthController.cs — head of class (Fields/Constructor regions + block ctor + open APIs)**

Replace:
```csharp
public sealed class AuthController : BaseApiController
{
    private readonly IAuthService _service;

    public AuthController(IAuthService service) => _service = service;
```
With:
```csharp
public sealed class AuthController : BaseApiController
{
    #region Fields

    private readonly IAuthService _service;

    #endregion

    #region Constructor

    public AuthController(IAuthService service)
    {
        _service = service;
    }

    #endregion

    #region APIs
```

- [ ] **Step 2: AuthController.cs — close APIs before the existing Helpers region**

Replace:
```csharp
        var successMessage = AuthMessages.Success.PasswordChanged(AppLanguage.Current);
        var body = ApiResponse<SessionDto>.Success(successMessage, session);
        return Ok(body);
    }

    #region Helpers
```
With:
```csharp
        var successMessage = AuthMessages.Success.PasswordChanged(AppLanguage.Current);
        var body = ApiResponse<SessionDto>.Success(successMessage, session);
        return Ok(body);
    }

    #endregion

    #region Helpers
```

- [ ] **Step 3: RolesController.cs — exact final content**

```csharp
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

public sealed class RolesController : BaseApiController
{
    #region Fields

    private readonly IRoleService _service;

    #endregion

    #region Constructor

    public RolesController(IRoleService service)
    {
        _service = service;
    }

    #endregion

    #region APIs

    /// <summary>Lists all selectable roles, ordered for display.</summary>
    /// <response code="200">All roles.</response>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RoleDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RoleDto>>>> Get(CancellationToken ct)
    {
        var roles = await _service.GetAllAsync(ct);

        var successMessage = RoleMessages.Success.RolesListed(AppLanguage.Current);
        var body = ApiResponse<IReadOnlyList<RoleDto>>.Success(successMessage, roles);
        return Ok(body);
    }

    #endregion
}
```

- [ ] **Step 4: EngagementTypesController.cs — exact final content**

```csharp
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Resources;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Kheprx.BaseBackend.SharedKernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kheprx.BaseBackend.Api.Controllers;

[Route("api/engagement-types")]
public sealed class EngagementTypesController : BaseApiController
{
    #region Fields

    private readonly IEngagementTypeService _service;

    #endregion

    #region Constructor

    public EngagementTypesController(IEngagementTypeService service)
    {
        _service = service;
    }

    #endregion

    #region APIs

    /// <summary>Lists all engagement types, ordered for display.</summary>
    /// <response code="200">All engagement types.</response>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EngagementTypeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EngagementTypeDto>>>> Get(CancellationToken ct)
    {
        var types = await _service.GetAllAsync(ct);

        var successMessage = EngagementTypeMessages.Success.EngagementTypesListed(AppLanguage.Current);
        var body = ApiResponse<IReadOnlyList<EngagementTypeDto>>.Success(successMessage, types);
        return Ok(body);
    }

    #endregion
}
```

- [ ] **Step 5: UsersController.cs — head of class**

Replace:
```csharp
[Authorize(Roles = "admin")]
public sealed class UsersController : BaseApiController
{
    private readonly IUserService _service;

    public UsersController(IUserService service) => _service = service;
```
With:
```csharp
[Authorize(Roles = "admin")]
public sealed class UsersController : BaseApiController
{
    #region Fields

    private readonly IUserService _service;

    #endregion

    #region Constructor

    public UsersController(IUserService service)
    {
        _service = service;
    }

    #endregion

    #region APIs
```

- [ ] **Step 6: UsersController.cs — close APIs at end of class**

Replace:
```csharp
        var successMessage = UserMessages.Success.StatusUpdated(AppLanguage.Current);
        var body = ApiResponse<UserDto>.Success(successMessage, user);
        return Ok(body);
    }
}
```
With:
```csharp
        var successMessage = UserMessages.Success.StatusUpdated(AppLanguage.Current);
        var body = ApiResponse<UserDto>.Success(successMessage, user);
        return Ok(body);
    }

    #endregion
}
```

- [ ] **Step 7: ProductsController.cs — head of class**

Replace:
```csharp
public sealed class ProductsController : BaseApiController
{
    private readonly IProductService _service;

    public ProductsController(IProductService service) => _service = service;
```
With:
```csharp
public sealed class ProductsController : BaseApiController
{
    #region Fields

    private readonly IProductService _service;

    #endregion

    #region Constructor

    public ProductsController(IProductService service)
    {
        _service = service;
    }

    #endregion

    #region APIs
```

- [ ] **Step 8: ProductsController.cs — close APIs at end of class**

Replace:
```csharp
        var successMessage = ProductMessages.Success.ProductDeleted(AppLanguage.Current);
        var body = ApiResponse<object>.Success(successMessage, null);
        return Ok(body);
    }
}
```
With:
```csharp
        var successMessage = ProductMessages.Success.ProductDeleted(AppLanguage.Current);
        var body = ApiResponse<object>.Success(successMessage, null);
        return Ok(body);
    }

    #endregion
}
```

- [ ] **Step 9: Build**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: Build succeeded, no new warnings.

- [ ] **Step 10: Run the full test suite**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: 147 passed, 0 failed, with zero test-file edits (`git status` must show only the five controller files modified).

- [ ] **Step 11: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/AuthController.cs backend/Kheprx.BaseBackend.Api/Controllers/RolesController.cs backend/Kheprx.BaseBackend.Api/Controllers/EngagementTypesController.cs backend/Kheprx.BaseBackend.Api/Controllers/UsersController.cs backend/Kheprx.BaseBackend.Api/Controllers/ProductsController.cs
git commit -m "style(api): add class regions and block-body constructors to controllers" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```
