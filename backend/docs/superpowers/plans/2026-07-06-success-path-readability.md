# Success-Path Readability Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rewrite six success-only controller return sites into the committed `successMessage`/`body` named-locals style, with zero behavior change.

**Architecture:** Pure formatting refactor inside existing controller action bodies. Services, DTOs, tests, doc comments, attributes, and method signatures are untouched. The existing unit-test suite is the safety net: it must pass with zero test edits. Four of the six sites have direct controller tests; the two ProductsController sites rely on build success plus line-by-line review.

**Tech Stack:** ASP.NET Core (net9), xUnit. Solution: `backend/Kheprx.BaseBackend.sln`.

**Spec:** `backend/docs/superpowers/specs/2026-07-06-success-path-readability-design.md`

## Global Constraints

- Zero behavior change: same status codes, same `ApiResponse` envelopes, same localized messages, same `Location` header on Create.
- Zero test edits. If any test fails after a change, the code change is wrong — fix or revert the code, never the test.
- Variable naming, verbatim: `successMessage` then `body`; data locals named for the resource (`types`, `roles`, `users`, `products`, `product`). Each local on ONE line — do not wrap.
- No guard clauses — these sites have no failure branch, so none is added.
- Do NOT touch: XML doc comments, `[ProducesResponseType]`/route attributes, method signatures, the `// AD-009: no body` comment, `try`/`catch` blocks, or the ten sites already restructured in the previous round.
- All commands run from the repo root `C:\Users\envnt\Desktop\Kheprx.Electric`.
- Build: `dotnet build backend/Kheprx.BaseBackend.sln` — must succeed with no new warnings.
- Test: `dotnet test backend/Kheprx.BaseBackend.sln` — every test passes, 0 failed.

---

### Task 1: Restructure AuthController.Logout

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/AuthController.cs:73-77` (Logout)
- Test (run only, never edit): `backend/tests/Kheprx.BaseBackend.Api.UnitTests/AuthControllerTests.cs` (covers Logout: `Logout_reads_sub_claim_and_returns_200`)

**Interfaces:**
- Consumes: nothing from other tasks (behavior-preserving; each task is independent).
- Produces: nothing other tasks rely on.

The signature line keeps its trailing `// AD-009: no body` comment byte-identical. Only the two statements inside the body change. Every other method in this file is out of scope — do not touch it.

- [ ] **Step 1: Verify a green baseline before any change**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: all tests pass, 0 failed. If the baseline is red, STOP — report the failure instead of proceeding.

- [ ] **Step 2: Restructure Logout**

Replace:
```csharp
    public async Task<ActionResult<ApiResponse<object>>> Logout(CancellationToken ct) // AD-009: no body
    {
        await _service.LogoutAsync(CurrentUserId(), ct);
        return Ok(ApiResponse<object>.Success(AuthMessages.Success.SignedOut(AppLanguage.Current), null));
    }
```
With:
```csharp
    public async Task<ActionResult<ApiResponse<object>>> Logout(CancellationToken ct) // AD-009: no body
    {
        await _service.LogoutAsync(CurrentUserId(), ct);

        var successMessage = AuthMessages.Success.SignedOut(AppLanguage.Current);
        var body = ApiResponse<object>.Success(successMessage, null);
        return Ok(body);
    }
```

- [ ] **Step 3: Build**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: Build succeeded, no new warnings.

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: all tests pass, 0 failed, with zero test-file edits (`git status` must show only `AuthController.cs` modified).

- [ ] **Step 5: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/AuthController.cs
git commit -m "refactor(api): restructure Logout success path into named locals" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: Restructure the lookup controllers (EngagementTypes.Get, Roles.Get)

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/EngagementTypesController.cs:23-28` (Get)
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/RolesController.cs:22-26` (Get)
- Test (run only, never edit): `backend/tests/Kheprx.BaseBackend.Api.UnitTests/EngagementTypesControllerTests.cs`, `backend/tests/Kheprx.BaseBackend.Api.UnitTests/RolesControllerTests.cs`

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces: nothing other tasks rely on.

Two identical one-endpoint changes, one commit. The existing `types`/`roles` data locals stay; only the return statement expands.

- [ ] **Step 1: Restructure EngagementTypesController.Get**

Replace:
```csharp
        var types = await _service.GetAllAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<EngagementTypeDto>>.Success(
            EngagementTypeMessages.Success.EngagementTypesListed(AppLanguage.Current), types));
```
With:
```csharp
        var types = await _service.GetAllAsync(ct);

        var successMessage = EngagementTypeMessages.Success.EngagementTypesListed(AppLanguage.Current);
        var body = ApiResponse<IReadOnlyList<EngagementTypeDto>>.Success(successMessage, types);
        return Ok(body);
```

- [ ] **Step 2: Restructure RolesController.Get**

Replace:
```csharp
        var roles = await _service.GetAllAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<RoleDto>>.Success(RoleMessages.Success.RolesListed(AppLanguage.Current), roles));
```
With:
```csharp
        var roles = await _service.GetAllAsync(ct);

        var successMessage = RoleMessages.Success.RolesListed(AppLanguage.Current);
        var body = ApiResponse<IReadOnlyList<RoleDto>>.Success(successMessage, roles);
        return Ok(body);
```

- [ ] **Step 3: Build**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: Build succeeded, no new warnings.

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: all tests pass, 0 failed, with zero test-file edits (`git status` must show only `EngagementTypesController.cs` and `RolesController.cs` modified).

- [ ] **Step 5: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/EngagementTypesController.cs backend/Kheprx.BaseBackend.Api/Controllers/RolesController.cs
git commit -m "refactor(api): restructure lookup list returns into named locals" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: Restructure UsersController.List (expression body → block body)

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/UsersController.cs:23-26` (List)
- Test (run only, never edit): `backend/tests/Kheprx.BaseBackend.Api.UnitTests/UsersControllerTests.cs` (covers List twice: `List_returns_200_with_users`, `List_passes_search_to_service`)

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces: nothing other tasks rely on.

The expression-bodied member becomes a block body with a `users` data local. The two-line signature (with `[FromQuery]` on its own line) stays byte-identical. `Create`, `Update`, and `SetStatus` are out of scope — do not touch them.

- [ ] **Step 1: Restructure List**

Replace:
```csharp
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserDto>>>> List(
        [FromQuery] string? search, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<UserDto>>.Success(
            UserMessages.Success.UsersListed(AppLanguage.Current), await _service.ListAsync(search, ct)));
```
With:
```csharp
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserDto>>>> List(
        [FromQuery] string? search, CancellationToken ct)
    {
        var users = await _service.ListAsync(search, ct);

        var successMessage = UserMessages.Success.UsersListed(AppLanguage.Current);
        var body = ApiResponse<IReadOnlyList<UserDto>>.Success(successMessage, users);
        return Ok(body);
    }
```

- [ ] **Step 2: Build**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: Build succeeded, no new warnings.

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: all tests pass, 0 failed, with zero test-file edits (`git status` must show only `UsersController.cs` modified).

- [ ] **Step 4: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/UsersController.cs
git commit -m "refactor(api): restructure UsersController.List into named locals" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 4: Restructure ProductsController (GetAll, Create)

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/ProductsController.cs:21-23` (GetAll), `:53-59` (Create)
- Test (run only, never edit): `backend/tests/Kheprx.BaseBackend.Api.UnitTests/`, `backend/tests/Kheprx.BaseBackend.Catalog.UnitTests/`

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces: nothing other tasks rely on.

Coverage note: ProductsController has no controller-level tests, so these two sites are verified by build success plus a careful character-level comparison of the replace/with blocks. GetAll converts from expression body to block body with a `products` data local. Create keeps `CreatedAtAction` (NOT `Ok`) so the `Location` header is preserved. `GetById`, `Update`, and `Delete` are out of scope — do not touch them.

- [ ] **Step 1: Restructure GetAll (expression body → block body)**

Replace:
```csharp
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProductDto>>>> GetAll(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ProductDto>>.Success(
            ProductMessages.Success.ProductsListed(AppLanguage.Current), await _service.GetAllAsync(ct)));
```
With:
```csharp
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProductDto>>>> GetAll(CancellationToken ct)
    {
        var products = await _service.GetAllAsync(ct);

        var successMessage = ProductMessages.Success.ProductsListed(AppLanguage.Current);
        var body = ApiResponse<IReadOnlyList<ProductDto>>.Success(successMessage, products);
        return Ok(body);
    }
```

- [ ] **Step 2: Restructure Create (keep CreatedAtAction)**

Replace:
```csharp
        var product = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = product.Id },
            ApiResponse<ProductDto>.Success(ProductMessages.Success.ProductCreated(AppLanguage.Current), product));
```
With:
```csharp
        var product = await _service.CreateAsync(request, ct);

        var successMessage = ProductMessages.Success.ProductCreated(AppLanguage.Current);
        var body = ApiResponse<ProductDto>.Success(successMessage, product);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, body);
```

- [ ] **Step 3: Build**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: Build succeeded, no new warnings.

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: all tests pass, 0 failed, with zero test-file edits (`git status` must show only `ProductsController.cs` modified).

- [ ] **Step 5: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/ProductsController.cs
git commit -m "refactor(api): restructure ProductsController success paths into named locals" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```
