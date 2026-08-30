# Helper-Method Regions — Design

**Date:** 2026-07-06
**Status:** Approved
**Branch:** feat/identity-auth

## Problem

There is no fast way to spot a class's private helpers; a reader scrolls
past the public surface to discover them. The backend has no `#region`
usage at all today, so this spec establishes the convention from scratch.

## Goal

Every controller, service, and repository with private helper methods
groups them under a single collapsible `#region Helpers` block, so opening
any file answers "what helpers does this class have?" in one glance.

## The convention

- Any concrete controller, service, or repository class with **at least
  one private helper method** has **exactly one** `#region Helpers` block.
- The region is always the **last block in the class**, immediately before
  the closing brace.
- The region contains **all private helper methods and nothing else** —
  DI fields, constructors, and public members never go inside regions.
- Classes with no helpers get **no region** — no empty ceremony. When a
  class gains its first helper, the region is added with it.
- The label is always exactly `Helpers` — no per-class descriptive names.
- Scope: backend concrete classes in the three layers (18 classes today).
  Interfaces, entities, DTOs, validators, and the frontend are out of
  scope.

## The change

Three classes qualify today. In all three the helpers already sit
contiguously at the bottom of the class, so the change is pure insertion
of `#region Helpers` / `#endregion` around the existing block — no member
moves, no reordering:

| File | Wrapped members |
|---|---|
| `backend/Kheprx.BaseBackend.Api/Controllers/AuthController.cs` | `CurrentUserId()` |
| `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/AuthService.cs` | `IssueSessionAsync`, `IssueAccessAsync` |
| `backend/src/Modules/Identity/Kheprx.BaseBackend.Identity.Application/Services/UserService.cs` | `LoadProfileAsync`, `ResolveEngagementTypeAsync`, `NextCodeAsync`, `AddProfileAsync`, `UpsertProfileAsync`, `Label` (both overloads), `ManagerProfile`, `MoqawelProfile`, `WorkerProfile`, `Map` |

Shape (AuthService shown):

```csharp
    // ...public methods above...

    #region Helpers

    private async Task<SessionDto> IssueSessionAsync(User user, CancellationToken ct)
    {
        // ...unchanged body...
    }

    private async Task<(string roleCode, string accessToken)> IssueAccessAsync(User user, CancellationToken ct)
    {
        // ...unchanged body...
    }

    #endregion
}
```

The remaining 15 concrete classes (all repositories, `ProductService`,
`RoleService`, `EngagementTypeService`, `JwtTokenService`,
`BaseApiController`, and the other four controllers) contain only DI
fields and public members — the convention covers them, but they have
nothing to wrap today.

## Non-goals

- No behavior change of any kind.
- No reordering of existing members (helpers are already contiguous and
  last in all three files).
- No full-file region skeletons (Fields / Constructor / Endpoints).
- No regions in interfaces, entities, DTOs, validators, or the frontend.

## Verification

`#region` directives are invisible to the compiler, so:

- `dotnet build backend/Kheprx.BaseBackend.sln` — succeeds with no new
  warnings (catches any malformed directive).
- `dotnet test backend/Kheprx.BaseBackend.sln` — stays 147/147 passed
  with zero test edits.
- `git status` — only the three listed files modified.
