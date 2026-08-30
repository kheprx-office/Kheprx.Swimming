# CurrentUserId Readability Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rewrite `AuthController.CurrentUserId()` from a one-line expression-bodied ternary into an if/return guard-style block body, with behavior identical.

**Architecture:** Pure readability refactor of one private helper in the API controller layer. The claim lookup gets a named intermediate (`subject`), the parse becomes a plain `if`/`return`, and the `Guid.Empty` fallback sits last on its own line — matching the guard-clause style the three controllers were restructured into (commits `bc4bebd`, `b80d4bc`, `fe37093`). No call site, signature, or behavior changes.

**Tech Stack:** ASP.NET Core (C#), xUnit test suite in `backend/tests/Kheprx.BaseBackend.Api.UnitTests`.

**Spec:** `backend/docs/superpowers/specs/2026-07-06-currentuserid-readability-design.md`

## Global Constraints

- Behavior must be identical: same `sub` claim, same `Guid.TryParse`, same `Guid.Empty` fallback when the claim is missing or malformed.
- Zero test-file edits. **No new tests either:** the spec explicitly leaves the `Guid.Empty` fallback branch uncovered (verified by review) — do not add coverage for it.
- Do NOT touch the three call sites (`Logout`, `Me`, `ChangePassword`), the method's signature, or anything else in `AuthController.cs`.
- Do NOT move the helper to `BaseApiController` or an extension class — it stays `private` in `AuthController` per the spec's placement decision.
- All commands run from the repo root `C:\Users\envnt\Desktop\Kheprx.Electric`.
- Build: `dotnet build backend/Kheprx.BaseBackend.sln` — must succeed with no new warnings.
- Test: `dotnet test backend/Kheprx.BaseBackend.sln` — every test passes, 0 failed.
- The working tree already contains an unrelated pre-existing modification to `frontend/angular.json`. Leave it alone: never stage it, and expect to see it in `git status` output throughout.

---

### Task 1: Rewrite CurrentUserId as a guard-style block body

**Files:**
- Modify: `backend/Kheprx.BaseBackend.Api/Controllers/AuthController.cs:127-128`
- Test: none (existing suite must stay green with zero edits)

**Interfaces:**
- Consumes: `User.FindFirst(JwtRegisteredClaimNames.Sub)` from `ClaimsPrincipal`; the `JwtRegisteredClaimNames` using directive already exists in the file.
- Produces: `private Guid CurrentUserId()` — signature unchanged; the three call sites at `AuthController.cs:75`, `:88`, and `:113` keep compiling and behaving identically.

- [ ] **Step 1: Verify a green baseline before any change**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: all tests pass, 0 failed. If the baseline is red, STOP — report the failure instead of proceeding.

- [ ] **Step 2: Rewrite the method**

In `backend/Kheprx.BaseBackend.Api/Controllers/AuthController.cs`, replace exactly this (currently lines 127–128, the last member of the class):

```csharp
    private Guid CurrentUserId()
        => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : Guid.Empty;
```

with:

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
```

Nothing else in the file changes.

- [ ] **Step 3: Build**

Run: `dotnet build backend/Kheprx.BaseBackend.sln`
Expected: Build succeeded, no new warnings.

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test backend/Kheprx.BaseBackend.sln`
Expected: all tests pass, 0 failed, with zero test-file edits. `git status` must show only `backend/Kheprx.BaseBackend.Api/Controllers/AuthController.cs` modified (plus the pre-existing, unrelated `frontend/angular.json` — do not stage it). The happy path is covered directly: `Logout_reads_sub_claim_and_returns_200` in `backend/tests/Kheprx.BaseBackend.Api.UnitTests/AuthControllerTests.cs` asserts the service receives the parsed id, and the `Me`/`ChangePassword` tests also run through the helper.

- [ ] **Step 5: Commit**

```bash
git add backend/Kheprx.BaseBackend.Api/Controllers/AuthController.cs
git commit -m "refactor(api): rewrite CurrentUserId as guard-style block body

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```
