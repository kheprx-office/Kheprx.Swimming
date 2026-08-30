# Nested Per-API Regions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wrap each of the 32 API methods (16 controller actions + 16 service methods) in its own named nested region inside the existing `#region APIs` blocks, so the collapsed IDE view reads like a table of contents.

**Architecture:** Pure insertion of `#region <title>` / `#endregion` / blank lines — zero existing characters change. Every edit below is a Replace/With pair whose Replace block is a unique anchor in its file; most pairs close one method's region and open the next in a single edit. The 147-test suite is the safety net.

**Tech Stack:** ASP.NET Core (net10), xUnit. Solution: `backend/Kheprx.BaseBackend.sln`.

**Spec:** `backend/docs/superpowers/specs/2026-07-06-nested-api-regions-design.md`

## Global Constraints

- Pure insertion: `git diff HEAD --numstat` must show 0 deletions for every file; only `#region ...`, `#endregion`, and blank lines are added.
- Zero behavior change; zero test edits. If a test fails, the code change is wrong — fix or revert the code, never the test.
- Region titles verbatim from the spec (repeated exactly in the steps below). Separator is an em dash with spaces (` — `) — copy it exactly.
- Formatting: nested `#region`/`#endregion` at member indentation (4 spaces); one blank line after each nested `#region`, one before its `#endregion`, one between neighbors, one between the last nested `#endregion` and the outer `#endregion` closing APIs.
- Leading comments stay attached to their method INSIDE its nested region (AuthService: the CancellationToken teaching comment stays with `LoginAsync`; the revoke-all design comment stays with `LogoutAsync`).
- Helpers regions stay flat. Do NOT touch: XML docs, attributes, routes, signatures, method bodies, `BaseApiController`, `ValidationFilter`, Infrastructure classes.
- All commands run from the repo root `C:\Users\envnt\Desktop\Kheprx.Electric`.
- Build: `dotnet build backend/Kheprx.BaseBackend.sln` — no new warnings. Test: `dotnet test backend/Kheprx.BaseBackend.sln` — 147 passed, 0 failed.

---

### Task 1: Controllers — 16 nested endpoint regions

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/AuthController.cs` (6 pairs)
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/RolesController.cs` (2 pairs)
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/EngagementTypesController.cs` (2 pairs)
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/UsersController.cs` (5 pairs)
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/ProductsController.cs` (6 pairs)
- Test (run only, never edit): `backend/tests/Kheprx.BaseBackend.Api.UnitTests/`

**Interfaces:**
- Consumes: nothing from other tasks (tasks are independent).
- Produces: nothing other tasks rely on.

Expected insertions (all files 0 deletions): AuthController 20, RolesController 4, EngagementTypesController 4, UsersController 16, ProductsController 20.

- [ ] **Step 1: Verify a green baseline before any change**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: 147 passed, 0 failed. If red, STOP and report instead of proceeding.

- [ ] **Step 2: AuthController — open Login region**

Replace:
```csharp
    #region APIs

    /// <summary>Authenticates a user with email and password and issues a token pair.</summary>
```
With:
```csharp
    #region APIs

    #region Login — POST api/auth/login — sign in with email and password

    /// <summary>Authenticates a user with email and password and issues a token pair.</summary>
```

- [ ] **Step 3: AuthController — close Login, open Refresh**

Replace:
```csharp
        var successMessage = AuthMessages.Success.SignedIn(AppLanguage.Current);
        var body = ApiResponse<SessionDto>.Success(successMessage, session);
        return Ok(body);
    }

    /// <summary>Exchanges a refresh token for a new access/refresh token pair.</summary>
```
With:
```csharp
        var successMessage = AuthMessages.Success.SignedIn(AppLanguage.Current);
        var body = ApiResponse<SessionDto>.Success(successMessage, session);
        return Ok(body);
    }

    #endregion

    #region Refresh — POST api/auth/refresh — exchange refresh token for a new pair

    /// <summary>Exchanges a refresh token for a new access/refresh token pair.</summary>
```

- [ ] **Step 4: AuthController — close Refresh, open Logout**

Replace:
```csharp
        var successMessage = AuthMessages.Success.TokenRefreshed(AppLanguage.Current);
        var body = ApiResponse<SessionDto>.Success(successMessage, session);
        return Ok(body);
    }

    /// <summary>Signs the current user out and revokes their refresh token.</summary>
```
With:
```csharp
        var successMessage = AuthMessages.Success.TokenRefreshed(AppLanguage.Current);
        var body = ApiResponse<SessionDto>.Success(successMessage, session);
        return Ok(body);
    }

    #endregion

    #region Logout — POST api/auth/logout — sign out, revoke refresh tokens

    /// <summary>Signs the current user out and revokes their refresh token.</summary>
```

- [ ] **Step 5: AuthController — close Logout, open Me**

Replace:
```csharp
        var successMessage = AuthMessages.Success.SignedOut(AppLanguage.Current);
        var body = ApiResponse<object>.Success(successMessage, null);
        return Ok(body);
    }

    /// <summary>Returns the profile of the currently authenticated user.</summary>
```
With:
```csharp
        var successMessage = AuthMessages.Success.SignedOut(AppLanguage.Current);
        var body = ApiResponse<object>.Success(successMessage, null);
        return Ok(body);
    }

    #endregion

    #region Me — GET api/auth/me — current user profile

    /// <summary>Returns the profile of the currently authenticated user.</summary>
```

- [ ] **Step 6: AuthController — close Me, open ChangePassword**

Replace:
```csharp
        var successMessage = AuthMessages.Success.CurrentUser(AppLanguage.Current);
        var body = ApiResponse<CurrentUserDto>.Success(successMessage, user);
        return Ok(body);
    }

    /// <summary>Changes the current user's password and issues a fresh session.</summary>
```
With:
```csharp
        var successMessage = AuthMessages.Success.CurrentUser(AppLanguage.Current);
        var body = ApiResponse<CurrentUserDto>.Success(successMessage, user);
        return Ok(body);
    }

    #endregion

    #region ChangePassword — POST api/auth/change-password — change password, issue fresh session

    /// <summary>Changes the current user's password and issues a fresh session.</summary>
```

- [ ] **Step 7: AuthController — close ChangePassword before the APIs #endregion**

Replace:
```csharp
        var successMessage = AuthMessages.Success.PasswordChanged(AppLanguage.Current);
        var body = ApiResponse<SessionDto>.Success(successMessage, session);
        return Ok(body);
    }

    #endregion

    #region Helpers
```
With:
```csharp
        var successMessage = AuthMessages.Success.PasswordChanged(AppLanguage.Current);
        var body = ApiResponse<SessionDto>.Success(successMessage, session);
        return Ok(body);
    }

    #endregion

    #endregion

    #region Helpers
```

- [ ] **Step 8: RolesController — open Get region**

Replace:
```csharp
    #region APIs

    /// <summary>Lists all selectable roles, ordered for display.</summary>
```
With:
```csharp
    #region APIs

    #region Get — GET api/roles — list all selectable roles

    /// <summary>Lists all selectable roles, ordered for display.</summary>
```

- [ ] **Step 9: RolesController — close Get before the APIs #endregion**

Replace:
```csharp
        var successMessage = RoleMessages.Success.RolesListed(AppLanguage.Current);
        var body = ApiResponse<IReadOnlyList<RoleDto>>.Success(successMessage, roles);
        return Ok(body);
    }

    #endregion
}
```
With:
```csharp
        var successMessage = RoleMessages.Success.RolesListed(AppLanguage.Current);
        var body = ApiResponse<IReadOnlyList<RoleDto>>.Success(successMessage, roles);
        return Ok(body);
    }

    #endregion

    #endregion
}
```

- [ ] **Step 10: EngagementTypesController — open Get region**

Replace:
```csharp
    #region APIs

    /// <summary>Lists all engagement types, ordered for display.</summary>
```
With:
```csharp
    #region APIs

    #region Get — GET api/engagement-types — list all engagement types

    /// <summary>Lists all engagement types, ordered for display.</summary>
```

- [ ] **Step 11: EngagementTypesController — close Get before the APIs #endregion**

Replace:
```csharp
        var successMessage = EngagementTypeMessages.Success.EngagementTypesListed(AppLanguage.Current);
        var body = ApiResponse<IReadOnlyList<EngagementTypeDto>>.Success(successMessage, types);
        return Ok(body);
    }

    #endregion
}
```
With:
```csharp
        var successMessage = EngagementTypeMessages.Success.EngagementTypesListed(AppLanguage.Current);
        var body = ApiResponse<IReadOnlyList<EngagementTypeDto>>.Success(successMessage, types);
        return Ok(body);
    }

    #endregion

    #endregion
}
```

- [ ] **Step 12: UsersController — open List region**

Replace:
```csharp
    #region APIs

    /// <summary>Lists users, optionally filtered by a search term (name, email, code, or NID).</summary>
```
With:
```csharp
    #region APIs

    #region List — GET api/users — list users, optional search

    /// <summary>Lists users, optionally filtered by a search term (name, email, code, or NID).</summary>
```

- [ ] **Step 13: UsersController — close List, open Create**

Replace:
```csharp
        var successMessage = UserMessages.Success.UsersListed(AppLanguage.Current);
        var body = ApiResponse<IReadOnlyList<UserDto>>.Success(successMessage, users);
        return Ok(body);
    }

    /// <summary>Creates a user with a role-dependent profile.</summary>
```
With:
```csharp
        var successMessage = UserMessages.Success.UsersListed(AppLanguage.Current);
        var body = ApiResponse<IReadOnlyList<UserDto>>.Success(successMessage, users);
        return Ok(body);
    }

    #endregion

    #region Create — POST api/users — create user with role profile

    /// <summary>Creates a user with a role-dependent profile.</summary>
```

- [ ] **Step 14: UsersController — close Create, open Update**

Replace:
```csharp
        catch (NidInUseException)
        {
            return Conflict(ApiResponse<UserDto>.Failure(UserMessages.Errors.NidInUse(AppLanguage.Current), "NID_IN_USE"));
        }
    }

    /// <summary>Updates a user's details, role, status, and profile.</summary>
```
With:
```csharp
        catch (NidInUseException)
        {
            return Conflict(ApiResponse<UserDto>.Failure(UserMessages.Errors.NidInUse(AppLanguage.Current), "NID_IN_USE"));
        }
    }

    #endregion

    #region Update — PATCH api/users/{id} — update details, status, profile

    /// <summary>Updates a user's details, role, status, and profile.</summary>
```

- [ ] **Step 15: UsersController — close Update, open SetStatus**

Replace:
```csharp
        catch (NidInUseException)
        {
            return Conflict(ApiResponse<UserDto>.Failure(UserMessages.Errors.NidInUse(AppLanguage.Current), "NID_IN_USE"));
        }
    }

    /// <summary>Activates or deactivates a user.</summary>
```
With:
```csharp
        catch (NidInUseException)
        {
            return Conflict(ApiResponse<UserDto>.Failure(UserMessages.Errors.NidInUse(AppLanguage.Current), "NID_IN_USE"));
        }
    }

    #endregion

    #region SetStatus — PATCH api/users/{id}/status — activate or deactivate

    /// <summary>Activates or deactivates a user.</summary>
```

- [ ] **Step 16: UsersController — close SetStatus before the APIs #endregion**

Replace:
```csharp
        var successMessage = UserMessages.Success.StatusUpdated(AppLanguage.Current);
        var body = ApiResponse<UserDto>.Success(successMessage, user);
        return Ok(body);
    }

    #endregion
}
```
With:
```csharp
        var successMessage = UserMessages.Success.StatusUpdated(AppLanguage.Current);
        var body = ApiResponse<UserDto>.Success(successMessage, user);
        return Ok(body);
    }

    #endregion

    #endregion
}
```

- [ ] **Step 17: ProductsController — open GetAll region**

Replace:
```csharp
    #region APIs

    /// <summary>Lists all products.</summary>
```
With:
```csharp
    #region APIs

    #region GetAll — GET api/products — list all products

    /// <summary>Lists all products.</summary>
```

- [ ] **Step 18: ProductsController — close GetAll, open GetById**

Replace:
```csharp
        var successMessage = ProductMessages.Success.ProductsListed(AppLanguage.Current);
        var body = ApiResponse<IReadOnlyList<ProductDto>>.Success(successMessage, products);
        return Ok(body);
    }

    /// <summary>Returns a single product by id.</summary>
```
With:
```csharp
        var successMessage = ProductMessages.Success.ProductsListed(AppLanguage.Current);
        var body = ApiResponse<IReadOnlyList<ProductDto>>.Success(successMessage, products);
        return Ok(body);
    }

    #endregion

    #region GetById — GET api/products/{id} — single product by id

    /// <summary>Returns a single product by id.</summary>
```

- [ ] **Step 19: ProductsController — close GetById, open Create**

Replace:
```csharp
        var successMessage = ProductMessages.Success.ProductRetrieved(AppLanguage.Current);
        var body = ApiResponse<ProductDto>.Success(successMessage, product);
        return Ok(body);
    }

    /// <summary>Creates a product.</summary>
```
With:
```csharp
        var successMessage = ProductMessages.Success.ProductRetrieved(AppLanguage.Current);
        var body = ApiResponse<ProductDto>.Success(successMessage, product);
        return Ok(body);
    }

    #endregion

    #region Create — POST api/products — create product

    /// <summary>Creates a product.</summary>
```

- [ ] **Step 20: ProductsController — close Create, open Update**

Replace:
```csharp
        var successMessage = ProductMessages.Success.ProductCreated(AppLanguage.Current);
        var body = ApiResponse<ProductDto>.Success(successMessage, product);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, body);
    }

    /// <summary>Replaces a product's name, price, and description.</summary>
```
With:
```csharp
        var successMessage = ProductMessages.Success.ProductCreated(AppLanguage.Current);
        var body = ApiResponse<ProductDto>.Success(successMessage, product);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, body);
    }

    #endregion

    #region Update — PUT api/products/{id} — replace name, price, description

    /// <summary>Replaces a product's name, price, and description.</summary>
```

- [ ] **Step 21: ProductsController — close Update, open Delete**

Replace:
```csharp
        var successMessage = ProductMessages.Success.ProductUpdated(AppLanguage.Current);
        var body = ApiResponse<ProductDto>.Success(successMessage, product);
        return Ok(body);
    }

    /// <summary>Deletes a product.</summary>
```
With:
```csharp
        var successMessage = ProductMessages.Success.ProductUpdated(AppLanguage.Current);
        var body = ApiResponse<ProductDto>.Success(successMessage, product);
        return Ok(body);
    }

    #endregion

    #region Delete — DELETE api/products/{id} — delete product

    /// <summary>Deletes a product.</summary>
```

- [ ] **Step 22: ProductsController — close Delete before the APIs #endregion**

Replace:
```csharp
        var successMessage = ProductMessages.Success.ProductDeleted(AppLanguage.Current);
        var body = ApiResponse<object>.Success(successMessage, null);
        return Ok(body);
    }

    #endregion
}
```
With:
```csharp
        var successMessage = ProductMessages.Success.ProductDeleted(AppLanguage.Current);
        var body = ApiResponse<object>.Success(successMessage, null);
        return Ok(body);
    }

    #endregion

    #endregion
}
```

- [ ] **Step 23: Build**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: Build succeeded, no new warnings.

- [ ] **Step 24: Run the full test suite and verify pure insertion**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: 147 passed, 0 failed, zero test-file edits.

Run: `git diff HEAD --numstat`
Expected (added/deleted): AuthController 20/0, RolesController 4/0, EngagementTypesController 4/0, UsersController 16/0, ProductsController 20/0; no other files.

- [ ] **Step 25: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/AuthController.cs backend/Kheprx.BaseBackend.Api/Controllers/RolesController.cs backend/Kheprx.BaseBackend.Api/Controllers/EngagementTypesController.cs backend/Kheprx.BaseBackend.Api/Controllers/UsersController.cs backend/Kheprx.BaseBackend.Api/Controllers/ProductsController.cs
git commit -m "style(api): add nested per-endpoint regions inside controller APIs regions" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: Services — 16 nested method regions

**Files:**
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/AuthService.cs` (6 pairs)
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/UserService.cs` (5 pairs)
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/RoleService.cs` (2 pairs)
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/EngagementTypeService.cs` (2 pairs)
- Modify: `backend/src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Application/Services/ProductService.cs` (6 pairs)
- Test (run only, never edit): full solution suite.

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces: nothing other tasks rely on.

Expected insertions (all files 0 deletions): AuthService 20, UserService 16, RoleService 4, EngagementTypeService 4, ProductService 20. Leading comments (AuthService's CancellationToken teaching comment; the LogoutAsync revoke-all comment) go INSIDE their method's region, directly under the region line.

- [ ] **Step 1: AuthService — open LoginAsync region (teaching comment goes inside)**

Replace:
```csharp
    #region APIs

    // About "CancellationToken ct = default" (used on every method here):
```
With:
```csharp
    #region APIs

    #region LoginAsync — verify credentials, issue session

    // About "CancellationToken ct = default" (used on every method here):
```

- [ ] **Step 2: AuthService — close LoginAsync, open RefreshAsync**

Replace:
```csharp
        return await IssueSessionAsync(user, ct);
    }

    public async Task<SessionDto?> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
```
With:
```csharp
        return await IssueSessionAsync(user, ct);
    }

    #endregion

    #region RefreshAsync — rotate refresh token, reissue pair

    public async Task<SessionDto?> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
```

- [ ] **Step 3: AuthService — close RefreshAsync, open LogoutAsync (revoke-all comment goes inside)**

Replace:
```csharp
        return new SessionDto(access, rawRefresh, roleCode, user.Id, user.MustChangePassword);
    }

    // Logout = revoke-all by design: mark EVERY active refresh token of this user as
```
With:
```csharp
        return new SessionDto(access, rawRefresh, roleCode, user.Id, user.MustChangePassword);
    }

    #endregion

    #region LogoutAsync — revoke all refresh tokens

    // Logout = revoke-all by design: mark EVERY active refresh token of this user as
```

- [ ] **Step 4: AuthService — close LogoutAsync, open ChangePasswordAsync**

Replace:
```csharp
        await _tokens.RevokeAllForUserAsync(userId, ct);
        await _tokens.SaveChangesAsync(ct);
    }

    public async Task<SessionDto?> ChangePasswordAsync(
```
With:
```csharp
        await _tokens.RevokeAllForUserAsync(userId, ct);
        await _tokens.SaveChangesAsync(ct);
    }

    #endregion

    #region ChangePasswordAsync — verify, set new password, reissue session

    public async Task<SessionDto?> ChangePasswordAsync(
```

- [ ] **Step 5: AuthService — close ChangePasswordAsync, open GetCurrentUserAsync**

Replace:
```csharp
        return await IssueSessionAsync(user, ct);          // ...then reissue a pair for the caller
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
```
With:
```csharp
        return await IssueSessionAsync(user, ct);          // ...then reissue a pair for the caller
    }

    #endregion

    #region GetCurrentUserAsync — load current user with role

    public async Task<CurrentUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
```

- [ ] **Step 6: AuthService — close GetCurrentUserAsync before the APIs #endregion**

Replace:
```csharp
        return new CurrentUserDto(user.Id, user.Email ?? string.Empty, user.FullName, role?.Code ?? FallbackRole);
    }

    #endregion
```
With:
```csharp
        return new CurrentUserDto(user.Id, user.Email ?? string.Empty, user.FullName, role?.Code ?? FallbackRole);
    }

    #endregion

    #endregion
```

- [ ] **Step 7: UserService — open ListAsync region**

Replace:
```csharp
    #region APIs

    public async Task<IReadOnlyList<UserDto>> ListAsync(string? search = null, CancellationToken ct = default)
```
With:
```csharp
    #region APIs

    #region ListAsync — list users with profiles, optional search

    public async Task<IReadOnlyList<UserDto>> ListAsync(string? search = null, CancellationToken ct = default)
```

- [ ] **Step 8: UserService — close ListAsync, open CreateAsync**

Replace:
```csharp
            return Map(u, roleCode, profile);
        }).ToList();
    }

    public async Task<UserDto?> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
```
With:
```csharp
            return Map(u, roleCode, profile);
        }).ToList();
    }

    #endregion

    #region CreateAsync — create user + role profile atomically

    public async Task<UserDto?> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
```

- [ ] **Step 9: UserService — close CreateAsync, open UpdateAsync**

Replace:
```csharp
        return Map(user, role.Code, profile);
    }

    public async Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
```
With:
```csharp
        return Map(user, role.Code, profile);
    }

    #endregion

    #region UpdateAsync — update user, profile, status

    public async Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
```

- [ ] **Step 10: UserService — close UpdateAsync, open SetStatusAsync**

Replace:
```csharp
        return Map(user, role.Code, profile);
    }

    public async Task<UserDto?> SetStatusAsync(Guid id, SetUserStatusRequest request, CancellationToken ct = default)
```
With:
```csharp
        return Map(user, role.Code, profile);
    }

    #endregion

    #region SetStatusAsync — activate or deactivate

    public async Task<UserDto?> SetStatusAsync(Guid id, SetUserStatusRequest request, CancellationToken ct = default)
```

- [ ] **Step 11: UserService — close SetStatusAsync before the APIs #endregion**

Replace:
```csharp
        return Map(user, roleCode, await LoadProfileAsync(user.Id, roleCode, ct));
    }

    #endregion
```
With:
```csharp
        return Map(user, roleCode, await LoadProfileAsync(user.Id, roleCode, ct));
    }

    #endregion

    #endregion
```

- [ ] **Step 12: RoleService — open GetAllAsync region**

Replace:
```csharp
    #region APIs

    public async Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken ct = default)
```
With:
```csharp
    #region APIs

    #region GetAllAsync — list all roles for display

    public async Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken ct = default)
```

- [ ] **Step 13: RoleService — close GetAllAsync before the APIs #endregion**

Replace:
```csharp
        return dtos;
    }

    #endregion
}
```
With:
```csharp
        return dtos;
    }

    #endregion

    #endregion
}
```

- [ ] **Step 14: EngagementTypeService — open GetAllAsync region**

Replace:
```csharp
    #region APIs

    public async Task<IReadOnlyList<EngagementTypeDto>> GetAllAsync(CancellationToken ct = default)
```
With:
```csharp
    #region APIs

    #region GetAllAsync — list all engagement types for display

    public async Task<IReadOnlyList<EngagementTypeDto>> GetAllAsync(CancellationToken ct = default)
```

- [ ] **Step 15: EngagementTypeService — close GetAllAsync before the APIs #endregion**

Replace:
```csharp
        return dtos;
    }

    #endregion
}
```
With:
```csharp
        return dtos;
    }

    #endregion

    #endregion
}
```

- [ ] **Step 16: ProductService — open GetAllAsync region**

Replace:
```csharp
    #region APIs

    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct = default)
```
With:
```csharp
    #region APIs

    #region GetAllAsync — list all products

    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct = default)
```

- [ ] **Step 17: ProductService — close GetAllAsync, open GetByIdAsync**

Replace:
```csharp
        => _mapper.ToDtoList(await _repository.GetAllAsync(ct));

    public async Task<ProductDto?> GetByIdAsync(int id, CancellationToken ct = default)
```
With:
```csharp
        => _mapper.ToDtoList(await _repository.GetAllAsync(ct));

    #endregion

    #region GetByIdAsync — single product or null

    public async Task<ProductDto?> GetByIdAsync(int id, CancellationToken ct = default)
```

- [ ] **Step 18: ProductService — close GetByIdAsync, open CreateAsync**

Replace:
```csharp
        return product is null ? null : _mapper.ToDto(product);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
```
With:
```csharp
        return product is null ? null : _mapper.ToDto(product);
    }

    #endregion

    #region CreateAsync — create and save product

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
```

- [ ] **Step 19: ProductService — close CreateAsync, open UpdateAsync**

Replace:
```csharp
        return _mapper.ToDto(product);
    }

    public async Task<ProductDto?> UpdateAsync(int id, UpdateProductRequest request, CancellationToken ct = default)
```
With:
```csharp
        return _mapper.ToDto(product);
    }

    #endregion

    #region UpdateAsync — update product or null

    public async Task<ProductDto?> UpdateAsync(int id, UpdateProductRequest request, CancellationToken ct = default)
```

- [ ] **Step 20: ProductService — close UpdateAsync, open DeleteAsync**

Replace:
```csharp
        return _mapper.ToDto(product);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
```
With:
```csharp
        return _mapper.ToDto(product);
    }

    #endregion

    #region DeleteAsync — delete product, false if missing

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
```

- [ ] **Step 21: ProductService — close DeleteAsync before the APIs #endregion**

Replace:
```csharp
        _repository.Remove(product);
        await _repository.SaveChangesAsync(ct);
        return true;
    }

    #endregion
```
With:
```csharp
        _repository.Remove(product);
        await _repository.SaveChangesAsync(ct);
        return true;
    }

    #endregion

    #endregion
```

- [ ] **Step 22: Build**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: Build succeeded, no new warnings.

- [ ] **Step 23: Run the full test suite and verify pure insertion**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: 147 passed, 0 failed, zero test-file edits.

Run: `git diff HEAD --numstat`
Expected (added/deleted): AuthService 20/0, UserService 16/0, RoleService 4/0, EngagementTypeService 4/0, ProductService 20/0; no other files.

- [ ] **Step 24: Commit**

```bash
git add backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/AuthService.cs backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/UserService.cs backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/RoleService.cs backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/EngagementTypeService.cs backend/src/Modules/Catalog/Kheprx.BaseBackend.Catalog.Application/Services/ProductService.cs
git commit -m "style(backend): add nested per-method regions inside service APIs regions" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```
