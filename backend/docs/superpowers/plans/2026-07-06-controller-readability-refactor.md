# Controller Readability Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure ten dense ternary return sites across three API controllers into guard clauses with named intermediate variables, with zero behavior change.

**Architecture:** Pure formatting refactor inside existing controller action bodies. Services, DTOs, tests, doc comments, attributes, and method signatures are untouched. The existing unit-test suite is the safety net: it must pass with zero test edits.

**Tech Stack:** ASP.NET Core (net9), xUnit. Solution: `backend/Kheprx.BaseBackend.sln`.

**Spec:** `backend/docs/superpowers/specs/2026-07-06-controller-readability-design.md`

## Global Constraints

- Zero behavior change: same status codes, same `ApiResponse` envelopes, same localized messages, same error code strings.
- Zero test edits. If any test fails after a change, the code change is wrong — fix or revert the code, never the test.
- Variable naming, verbatim: failure block uses `message` then `envelope`; success path uses `successMessage` then `body`.
- Do NOT touch: XML doc comments, `[ProducesResponseType]`/route attributes, method signatures, `catch` blocks, or single-expression endpoints (`AuthController.Logout`, `UsersController.List`, `ProductsController.GetAll`, `ProductsController.Create`).
- All commands run from the repo root `C:\Users\envnt\Desktop\Kheprx.Electric`.
- Build: `dotnet build backend/Kheprx.BaseBackend.sln` — must succeed with no new warnings.
- Test: `dotnet test backend/Kheprx.BaseBackend.sln` — every test passes, 0 failed.

---

### Task 1: Amend spec — add ProductsController.Delete as the tenth site

The approved spec lists nine sites. During planning a tenth was found: `ProductsController.Delete` uses the same dense ternary but inverted (success branch first, keyed on a `bool`), which is why the original search missed it. It falls squarely under the approved "one style everywhere" goal, so it joins the scope.

**Files:**
- Modify: `backend/docs/superpowers/specs/2026-07-06-controller-readability-design.md`

**Interfaces:**
- Consumes: nothing.
- Produces: an amended spec that Tasks 2–4 reference; Task 4 implements the new tenth site.

- [ ] **Step 1: Verify a green baseline before any change**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: all tests pass, 0 failed. If the baseline is red, STOP — report the failure instead of proceeding.

- [ ] **Step 2: Update the spec's site count and scope table**

In `backend/docs/superpowers/specs/2026-07-06-controller-readability-design.md`, make these exact edits:

Replace:
```markdown
Nine controller actions return their result through a dense conditional
```
With:
```markdown
Ten controller actions return their result through a dense conditional
```

Replace:
```markdown
Restructure each of the nine sites into a guard clause with named
```
With:
```markdown
Restructure each of the ten sites into a guard clause with named
```

Replace:
```markdown
## Scope — the nine sites
```
With:
```markdown
## Scope — the ten sites
```

Replace:
```markdown
| ProductsController | Update | 404 NotFound | PRODUCT_NOT_FOUND |
```
With:
```markdown
| ProductsController | Update | 404 NotFound | PRODUCT_NOT_FOUND |
| ProductsController | Delete | 404 NotFound | PRODUCT_NOT_FOUND |
```

Replace:
```markdown
**Status:** Approved
```
With:
```markdown
**Status:** Approved (amended 2026-07-06: added ProductsController.Delete, a tenth site whose inverted bool ternary the original search missed)
```

- [ ] **Step 3: Commit**

```bash
git add backend/docs/superpowers/specs/2026-07-06-controller-readability-design.md
git commit -m "docs(specs): add ProductsController.Delete as tenth readability-refactor site" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: Restructure AuthController (4 sites)

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/AuthController.cs:27-33` (Login), `:44-50` (Refresh), `:70-76` (Me), `:87-93` (ChangePassword)
- Test (run only, never edit): `backend/tests/Kheprx.BaseBackend.Api.UnitTests/AuthControllerTests.cs`

**Interfaces:**
- Consumes: nothing from other tasks (behavior-preserving; each task is independent).
- Produces: nothing other tasks rely on.

Only the statements shown change. Doc comments, attributes, and signatures around them stay byte-identical. `Logout` is out of scope — do not touch it.

- [ ] **Step 1: Restructure Login**

Replace:
```csharp
        var session = await _service.LoginAsync(request, ct);
        return session is null
            ? Unauthorized(ApiResponse<SessionDto>.Failure(AuthMessages.Errors.InvalidCredentials(AppLanguage.Current), "INVALID_CREDENTIALS"))
            : Ok(ApiResponse<SessionDto>.Success(AuthMessages.Success.SignedIn(AppLanguage.Current), session));
```
With:
```csharp
        var session = await _service.LoginAsync(request, ct);

        if (session is null)
        {
            var message = AuthMessages.Errors.InvalidCredentials(AppLanguage.Current);
            var envelope = ApiResponse<SessionDto>.Failure(message, "INVALID_CREDENTIALS");
            return Unauthorized(envelope);
        }

        var successMessage = AuthMessages.Success.SignedIn(AppLanguage.Current);
        var body = ApiResponse<SessionDto>.Success(successMessage, session);
        return Ok(body);
```

- [ ] **Step 2: Restructure Refresh**

Replace:
```csharp
        var session = await _service.RefreshAsync(request, ct);
        return session is null
            ? Unauthorized(ApiResponse<SessionDto>.Failure(AuthMessages.Errors.InvalidRefreshToken(AppLanguage.Current), "INVALID_REFRESH_TOKEN"))
            : Ok(ApiResponse<SessionDto>.Success(AuthMessages.Success.TokenRefreshed(AppLanguage.Current), session));
```
With:
```csharp
        var session = await _service.RefreshAsync(request, ct);

        if (session is null)
        {
            var message = AuthMessages.Errors.InvalidRefreshToken(AppLanguage.Current);
            var envelope = ApiResponse<SessionDto>.Failure(message, "INVALID_REFRESH_TOKEN");
            return Unauthorized(envelope);
        }

        var successMessage = AuthMessages.Success.TokenRefreshed(AppLanguage.Current);
        var body = ApiResponse<SessionDto>.Success(successMessage, session);
        return Ok(body);
```

- [ ] **Step 3: Restructure Me**

Replace:
```csharp
        var user = await _service.GetCurrentUserAsync(CurrentUserId(), ct);
        return user is null
            ? Unauthorized(ApiResponse<CurrentUserDto>.Failure(AuthMessages.Errors.NotAuthenticated(AppLanguage.Current), "NOT_AUTHENTICATED"))
            : Ok(ApiResponse<CurrentUserDto>.Success(AuthMessages.Success.CurrentUser(AppLanguage.Current), user));
```
With:
```csharp
        var user = await _service.GetCurrentUserAsync(CurrentUserId(), ct);

        if (user is null)
        {
            var message = AuthMessages.Errors.NotAuthenticated(AppLanguage.Current);
            var envelope = ApiResponse<CurrentUserDto>.Failure(message, "NOT_AUTHENTICATED");
            return Unauthorized(envelope);
        }

        var successMessage = AuthMessages.Success.CurrentUser(AppLanguage.Current);
        var body = ApiResponse<CurrentUserDto>.Success(successMessage, user);
        return Ok(body);
```

- [ ] **Step 4: Restructure ChangePassword**

Replace:
```csharp
        var session = await _service.ChangePasswordAsync(CurrentUserId(), request, ct);
        return session is null
            ? Unauthorized(ApiResponse<SessionDto>.Failure(AuthMessages.Errors.CurrentPasswordIncorrect(AppLanguage.Current), "INVALID_CREDENTIALS"))
            : Ok(ApiResponse<SessionDto>.Success(AuthMessages.Success.PasswordChanged(AppLanguage.Current), session));
```
With:
```csharp
        var session = await _service.ChangePasswordAsync(CurrentUserId(), request, ct);

        if (session is null)
        {
            var message = AuthMessages.Errors.CurrentPasswordIncorrect(AppLanguage.Current);
            var envelope = ApiResponse<SessionDto>.Failure(message, "INVALID_CREDENTIALS");
            return Unauthorized(envelope);
        }

        var successMessage = AuthMessages.Success.PasswordChanged(AppLanguage.Current);
        var body = ApiResponse<SessionDto>.Success(successMessage, session);
        return Ok(body);
```

- [ ] **Step 5: Build**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: Build succeeded, no new warnings.

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: all tests pass, 0 failed, with zero test-file edits (`git status` must show only `AuthController.cs` modified).

- [ ] **Step 7: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/AuthController.cs
git commit -m "refactor(api): restructure AuthController returns into guard clauses" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: Restructure UsersController (3 sites)

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/UsersController.cs:36-49` (Create), `:61-78` (Update), `:88-94` (SetStatus)
- Test (run only, never edit): `backend/tests/Kheprx.BaseBackend.Api.UnitTests/`

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces: nothing other tasks rely on.

Create and Update sit inside existing `try` blocks — the `try`/`catch` structure and all `catch` bodies stay byte-identical; only the ternary statement inside the `try` changes. `List` is out of scope — do not touch it.

- [ ] **Step 1: Restructure Create (inside its try block)**

Replace:
```csharp
            var user = await _service.CreateAsync(request, ct);
            return user is null
                ? Conflict(ApiResponse<UserDto>.Failure(UserMessages.Errors.EmailInUse(AppLanguage.Current), "EMAIL_IN_USE"))
                : StatusCode(StatusCodes.Status201Created, ApiResponse<UserDto>.Success(UserMessages.Success.UserCreated(AppLanguage.Current), user));
```
With:
```csharp
            var user = await _service.CreateAsync(request, ct);

            if (user is null)
            {
                var message = UserMessages.Errors.EmailInUse(AppLanguage.Current);
                var envelope = ApiResponse<UserDto>.Failure(message, "EMAIL_IN_USE");
                return Conflict(envelope);
            }

            var successMessage = UserMessages.Success.UserCreated(AppLanguage.Current);
            var body = ApiResponse<UserDto>.Success(successMessage, user);
            return StatusCode(StatusCodes.Status201Created, body);
```

- [ ] **Step 2: Restructure Update (inside its try block)**

Replace:
```csharp
            var user = await _service.UpdateAsync(id, request, ct);
            return user is null
                ? NotFound(ApiResponse<UserDto>.Failure(UserMessages.Errors.UserNotFound(AppLanguage.Current), "USER_NOT_FOUND"))
                : Ok(ApiResponse<UserDto>.Success(UserMessages.Success.UserUpdated(AppLanguage.Current), user));
```
With:
```csharp
            var user = await _service.UpdateAsync(id, request, ct);

            if (user is null)
            {
                var message = UserMessages.Errors.UserNotFound(AppLanguage.Current);
                var envelope = ApiResponse<UserDto>.Failure(message, "USER_NOT_FOUND");
                return NotFound(envelope);
            }

            var successMessage = UserMessages.Success.UserUpdated(AppLanguage.Current);
            var body = ApiResponse<UserDto>.Success(successMessage, user);
            return Ok(body);
```

- [ ] **Step 3: Restructure SetStatus**

Replace:
```csharp
        var user = await _service.SetStatusAsync(id, request, ct);
        return user is null
            ? NotFound(ApiResponse<UserDto>.Failure(UserMessages.Errors.UserNotFound(AppLanguage.Current), "USER_NOT_FOUND"))
            : Ok(ApiResponse<UserDto>.Success(UserMessages.Success.StatusUpdated(AppLanguage.Current), user));
```
With:
```csharp
        var user = await _service.SetStatusAsync(id, request, ct);

        if (user is null)
        {
            var message = UserMessages.Errors.UserNotFound(AppLanguage.Current);
            var envelope = ApiResponse<UserDto>.Failure(message, "USER_NOT_FOUND");
            return NotFound(envelope);
        }

        var successMessage = UserMessages.Success.StatusUpdated(AppLanguage.Current);
        var body = ApiResponse<UserDto>.Success(successMessage, user);
        return Ok(body);
```

- [ ] **Step 4: Build**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: Build succeeded, no new warnings.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: all tests pass, 0 failed, with zero test-file edits (`git status` must show only `UsersController.cs` modified).

- [ ] **Step 6: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/UsersController.cs
git commit -m "refactor(api): restructure UsersController returns into guard clauses" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 4: Restructure ProductsController (3 sites)

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/ProductsController.cs:31-37` (GetById), `:61-68` (Update), `:76-82` (Delete)
- Test (run only, never edit): `backend/tests/Kheprx.BaseBackend.Api.UnitTests/`, `backend/tests/Kheprx.BaseBackend.Catalog.UnitTests/`

**Interfaces:**
- Consumes: the spec amendment from Task 1 (Delete is in scope).
- Produces: nothing other tasks rely on.

Delete's ternary is inverted (success branch first, keyed on a `bool`), so its guard tests `!deleted`. `GetAll` and `Create` are out of scope — do not touch them.

- [ ] **Step 1: Restructure GetById**

Replace:
```csharp
        var product = await _service.GetByIdAsync(id, ct);
        return product is null
            ? NotFound(ApiResponse<ProductDto>.Failure(ProductMessages.Errors.ProductNotFound(AppLanguage.Current), "PRODUCT_NOT_FOUND"))
            : Ok(ApiResponse<ProductDto>.Success(ProductMessages.Success.ProductRetrieved(AppLanguage.Current), product));
```
With:
```csharp
        var product = await _service.GetByIdAsync(id, ct);

        if (product is null)
        {
            var message = ProductMessages.Errors.ProductNotFound(AppLanguage.Current);
            var envelope = ApiResponse<ProductDto>.Failure(message, "PRODUCT_NOT_FOUND");
            return NotFound(envelope);
        }

        var successMessage = ProductMessages.Success.ProductRetrieved(AppLanguage.Current);
        var body = ApiResponse<ProductDto>.Success(successMessage, product);
        return Ok(body);
```

- [ ] **Step 2: Restructure Update**

Replace:
```csharp
        var product = await _service.UpdateAsync(id, request, ct);
        return product is null
            ? NotFound(ApiResponse<ProductDto>.Failure(ProductMessages.Errors.ProductNotFound(AppLanguage.Current), "PRODUCT_NOT_FOUND"))
            : Ok(ApiResponse<ProductDto>.Success(ProductMessages.Success.ProductUpdated(AppLanguage.Current), product));
```
With:
```csharp
        var product = await _service.UpdateAsync(id, request, ct);

        if (product is null)
        {
            var message = ProductMessages.Errors.ProductNotFound(AppLanguage.Current);
            var envelope = ApiResponse<ProductDto>.Failure(message, "PRODUCT_NOT_FOUND");
            return NotFound(envelope);
        }

        var successMessage = ProductMessages.Success.ProductUpdated(AppLanguage.Current);
        var body = ApiResponse<ProductDto>.Success(successMessage, product);
        return Ok(body);
```

- [ ] **Step 3: Restructure Delete (inverted bool guard)**

Replace:
```csharp
        var deleted = await _service.DeleteAsync(id, ct);
        return deleted
            ? Ok(ApiResponse<object>.Success(ProductMessages.Success.ProductDeleted(AppLanguage.Current), null))
            : NotFound(ApiResponse<object>.Failure(ProductMessages.Errors.ProductNotFound(AppLanguage.Current), "PRODUCT_NOT_FOUND"));
```
With:
```csharp
        var deleted = await _service.DeleteAsync(id, ct);

        if (!deleted)
        {
            var message = ProductMessages.Errors.ProductNotFound(AppLanguage.Current);
            var envelope = ApiResponse<object>.Failure(message, "PRODUCT_NOT_FOUND");
            return NotFound(envelope);
        }

        var successMessage = ProductMessages.Success.ProductDeleted(AppLanguage.Current);
        var body = ApiResponse<object>.Success(successMessage, null);
        return Ok(body);
```

- [ ] **Step 4: Build**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: Build succeeded, no new warnings.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: all tests pass, 0 failed, with zero test-file edits (`git status` must show only `ProductsController.cs` modified).

- [ ] **Step 6: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/ProductsController.cs
git commit -m "refactor(api): restructure ProductsController returns into guard clauses" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```
