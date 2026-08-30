# Helper-Method Regions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wrap the existing private-helper blocks of `AuthController`, `AuthService`, and `UserService` in `#region Helpers` / `#endregion`, establishing the backend's helper-region convention.

**Architecture:** Pure insertion — in all three classes the helpers already sit contiguously as the last members before the class's closing brace, so each file gains exactly two directive lines (plus spacing) and nothing moves. `#region` is invisible to the compiler; the build and the full test suite prove no behavioral drift.

**Tech Stack:** ASP.NET Core (C#), xUnit suite in `backend/tests`.

**Spec:** `backend/docs/superpowers/specs/2026-07-06-helper-regions-design.md`

## Global Constraints

- No behavior change of any kind; no member reordering — insertion only.
- The region label is exactly `Helpers`; one region per class, always the last block before the class's closing brace.
- Only the three listed files change. Zero test-file edits, no new tests.
- All commands run from the repo root `C:\Users\envnt\Desktop\Kheprx.Electric`.
- Build: `dotnet build backend/Kheprx.BaseBackend.sln` — must succeed with no new warnings.
- Test: `dotnet test backend/Kheprx.BaseBackend.sln` — every test passes (147 total), 0 failed.
- The working tree contains an unrelated pre-existing modification to `frontend/angular.json`. Leave it alone: never stage it; expect it in `git status` throughout.

---

### Task 1: Insert #region Helpers in the three qualifying classes

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/AuthController.cs` (helper block near end of class, ~lines 127–137)
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/AuthService.cs` (helper block ~lines 154–169)
- Modify: `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/UserService.cs` (helper block ~lines 152–254)
- Test: none (existing suite must stay green with zero edits)

**Interfaces:**
- Consumes: nothing from other tasks (this is the only task).
- Produces: no API change — `#region` directives only; every method signature in all three files is byte-identical afterward.

- [ ] **Step 1: Verify a green baseline before any change**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: all tests pass, 0 failed (147 total across 4 projects). If the baseline is red, STOP — report the failure instead of proceeding.

- [ ] **Step 2: Wrap AuthController.CurrentUserId**

In `backend/Kheprx.BaseBackend.Api/Controllers/AuthController.cs`, the class currently ends with:

```csharp
    private Guid CurrentUserId()
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (Guid.TryParse(subject, out var userId))
        {
            return userId;
        }

        return Guid.Empty;
    }
}
```

Replace that with:

```csharp
    #region Helpers

    private Guid CurrentUserId()
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (Guid.TryParse(subject, out var userId))
        {
            return userId;
        }

        return Guid.Empty;
    }

    #endregion
}
```

The method body is byte-identical; only the two directive lines and their blank-line spacing are new.

- [ ] **Step 3: Wrap AuthService's two helpers**

In `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/AuthService.cs`, insert the opening directive between `GetCurrentUserAsync`'s closing brace and `IssueSessionAsync`. Currently:

```csharp
    }

    private async Task<SessionDto> IssueSessionAsync(User user, CancellationToken ct)
```

becomes:

```csharp
    }

    #region Helpers

    private async Task<SessionDto> IssueSessionAsync(User user, CancellationToken ct)
```

Then close the region at the end of the class. Currently:

```csharp
    private async Task<(string roleCode, string accessToken)> IssueAccessAsync(User user, CancellationToken ct)
    {
        var role = await _roles.GetByIdAsync(user.RoleId, ct);
        var roleCode = role?.Code ?? FallbackRole;
        return (roleCode, _jwt.CreateAccessToken(user, roleCode));
    }
}
```

becomes:

```csharp
    private async Task<(string roleCode, string accessToken)> IssueAccessAsync(User user, CancellationToken ct)
    {
        var role = await _roles.GetByIdAsync(user.RoleId, ct);
        var roleCode = role?.Code ?? FallbackRole;
        return (roleCode, _jwt.CreateAccessToken(user, roleCode));
    }

    #endregion
}
```

- [ ] **Step 4: Wrap UserService's eleven helpers**

In `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/UserService.cs`, insert the opening directive between `SetStatusAsync`'s closing brace and `LoadProfileAsync`. Currently:

```csharp
        return Map(user, roleCode, await LoadProfileAsync(user.Id, roleCode, ct));
    }

    private async Task<UserProfileDto?> LoadProfileAsync(Guid userId, string roleCode, CancellationToken ct)
```

becomes:

```csharp
        return Map(user, roleCode, await LoadProfileAsync(user.Id, roleCode, ct));
    }

    #region Helpers

    private async Task<UserProfileDto?> LoadProfileAsync(Guid userId, string roleCode, CancellationToken ct)
```

Then close the region at the end of the class. Currently:

```csharp
    private static UserDto Map(User u, string roleCode, UserProfileDto? profile) => new(
        u.Id, u.Code, u.FullName, u.Email, u.Phone, u.Gender, u.Age, u.Nid,
        roleCode, u.IsActive ? "active" : "disabled", u.MustChangePassword, profile);
}
```

becomes:

```csharp
    private static UserDto Map(User u, string roleCode, UserProfileDto? profile) => new(
        u.Id, u.Code, u.FullName, u.Email, u.Phone, u.Gender, u.Age, u.Nid,
        roleCode, u.IsActive ? "active" : "disabled", u.MustChangePassword, profile);

    #endregion
}
```

The eleven helper bodies between the two insertion points (`LoadProfileAsync`, `ResolveEngagementTypeAsync`, `NextCodeAsync`, `AddProfileAsync`, `UpsertProfileAsync`, both `Label` overloads, `ManagerProfile`, `MoqawelProfile`, `WorkerProfile`, `Map`) are untouched.

- [ ] **Step 5: Build**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: Build succeeded, 0 Warning(s), 0 Error(s). A malformed or unbalanced directive fails here with CS1038 (`#endregion directive expected`).

- [ ] **Step 6: Run the full test suite and check the working tree**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: all tests pass, 0 failed (147 total), with zero test-file edits.

Run: `git status --short`
Expected output — exactly these three modified files plus the pre-existing, unrelated `frontend/angular.json` (do not stage it):

```
 M backend/Kheprx.BaseBackend.Api/Controllers/AuthController.cs
 M backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/AuthService.cs
 M backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/UserService.cs
 M frontend/angular.json
```

- [ ] **Step 7: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/AuthController.cs backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/AuthService.cs backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/UserService.cs
git commit -m "style(backend): group private helpers under #region Helpers

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```
