# Nested Per-API Regions — Design

**Date:** 2026-07-06
**Status:** Approved
**Branch:** feat/identity-auth

## Problem

The `#region APIs` blocks added by the class-structure round
(`2026-07-06-class-structure-regions-design.md`) group all public methods
into one collapsible block, but a collapsed class still shows only "APIs" —
finding a specific endpoint means expanding and scrolling. The user wants
the collapsed view to read like a table of contents: one named, collapsible
region per API method.

## Goal

Inside every `#region APIs` in the 10 classes from the class-structure
round, wrap each method in its own nested region whose title carries the
method name, the HTTP verb + route (controllers), and a short purpose
phrase. 32 nested regions total (16 controller actions + 16 service
methods). Zero behavior change.

## Trade-off (accepted)

Region titles duplicate part of each method's XML `<summary>` and do not
rename automatically with the method. Accepted: the user's priority is
IDE navigation; titles are kept short, and purpose phrases are condensed
from the existing summaries so no new claims are invented.

## Non-goals

- Helpers regions stay flat — no nested region per helper method.
- No changes to XML doc comments, attributes, routes, signatures, or any
  method body. Pure insertion of `#region`/`#endregion` lines only.
- `BaseApiController`, `ValidationFilter`, and Infrastructure classes
  untouched.
- No test changes.

## Formatting rules

- Nested `#region` markers sit at member indentation (4 spaces), the same
  level as the method they wrap and as the enclosing `#region APIs`.
- Title separator is an em dash surrounded by spaces (` — `) — the same
  character these files already use in XML docs.
- One blank line after each nested `#region`, one before its `#endregion`,
  one between a nested `#endregion` and the next nested `#region`, and one
  between the last nested `#endregion` and the outer `#endregion` that
  closes APIs.
- Each nested region encloses the method's XML doc comment, attributes,
  and body as one unit.

## The template

```csharp
    #region APIs

    #region Login — POST api/auth/login — sign in with email and password

    /// <summary>Authenticates a user with email and password and issues a token pair.</summary>
    ...attributes...
    public async Task<ActionResult<ApiResponse<SessionDto>>> Login(LoginRequest request, CancellationToken ct)
    {
        ...
    }

    #endregion

    #region Refresh — POST api/auth/refresh — exchange refresh token for a new pair

    ...

    #endregion

    #endregion
```

## Region titles — verbatim

### Controllers (16)

| Class | Title |
|---|---|
| AuthController | `Login — POST api/auth/login — sign in with email and password` |
| AuthController | `Refresh — POST api/auth/refresh — exchange refresh token for a new pair` |
| AuthController | `Logout — POST api/auth/logout — sign out, revoke refresh tokens` |
| AuthController | `Me — GET api/auth/me — current user profile` |
| AuthController | `ChangePassword — POST api/auth/change-password — change password, issue fresh session` |
| RolesController | `Get — GET api/roles — list all selectable roles` |
| EngagementTypesController | `Get — GET api/engagement-types — list all engagement types` |
| UsersController | `List — GET api/users — list users, optional search` |
| UsersController | `Create — POST api/users — create user with role profile` |
| UsersController | `Update — PATCH api/users/{id} — update details, status, profile` |
| UsersController | `SetStatus — PATCH api/users/{id}/status — activate or deactivate` |
| ProductsController | `GetAll — GET api/products — list all products` |
| ProductsController | `GetById — GET api/products/{id} — single product by id` |
| ProductsController | `Create — POST api/products — create product` |
| ProductsController | `Update — PUT api/products/{id} — replace name, price, description` |
| ProductsController | `Delete — DELETE api/products/{id} — delete product` |

### Services (16)

| Class | Title |
|---|---|
| AuthService | `LoginAsync — verify credentials, issue session` |
| AuthService | `RefreshAsync — rotate refresh token, reissue pair` |
| AuthService | `LogoutAsync — revoke all refresh tokens` |
| AuthService | `ChangePasswordAsync — verify, set new password, reissue session` |
| AuthService | `GetCurrentUserAsync — load current user with role` |
| UserService | `ListAsync — list users with profiles, optional search` |
| UserService | `CreateAsync — create user + role profile atomically` |
| UserService | `UpdateAsync — update user, profile, status` |
| UserService | `SetStatusAsync — activate or deactivate` |
| RoleService | `GetAllAsync — list all roles for display` |
| EngagementTypeService | `GetAllAsync — list all engagement types for display` |
| ProductService | `GetAllAsync — list all products` |
| ProductService | `GetByIdAsync — single product or null` |
| ProductService | `CreateAsync — create and save product` |
| ProductService | `UpdateAsync — update product or null` |
| ProductService | `DeleteAsync — delete product, false if missing` |

Special note: `AuthService`'s multi-line CancellationToken teaching comment
sits above `LoginAsync` today; it goes INSIDE the `LoginAsync` nested
region (directly under the region line), staying attached to the method it
explains.

## Verification

- `dotnet build backend/Kheprx.BaseBackend.sln` — no new warnings.
- `dotnet test backend/Kheprx.BaseBackend.sln` — 147/147 pass with zero
  test edits.
- Every file's diff must be pure insertion (`git diff --numstat` shows 0
  deletions): only `#region ...`, `#endregion`, and blank lines are added.
