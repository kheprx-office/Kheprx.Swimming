# CurrentUserId Readability — Design

**Date:** 2026-07-06
**Status:** Approved
**Branch:** feat/identity-auth

Follow-on to `2026-07-06-controller-readability-design.md`. That run covered
the ten guard-clause return sites and explicitly excluded helpers; this spec
covers the one helper it left behind.

This version supersedes the earlier draft of this spec (committed in
`e132198`), which kept a ternary inside the block body. It was redone to
match the guard-clause style the three controllers now use throughout —
after that restructuring, the ternary would have been the last conditional
of its kind in `AuthController`.

## Problem

`AuthController.CurrentUserId()` packs three steps — claim lookup, parse,
and fallback — into a single expression-bodied ternary over 100 characters
long:

```csharp
private Guid CurrentUserId()
    => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : Guid.Empty;
```

## Goal

Rewrite it as a block body in the controllers' guard-clause style: a named
intermediate for the claim lookup, a plain `if`/`return` for the parse, and
the fallback last on its own line. Behavior is identical: same claim, same
parse, same `Guid.Empty` fallback when the `sub` claim is missing or
malformed.

## The change

One method in `AuthController.cs`; the three call sites (`Logout`, `Me`,
`ChangePassword`) are untouched:

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

## Placement decision

The helper **stays private in `AuthController`** — strict YAGNI: it is the
only controller that reads the current user's id today. It moves to
`BaseApiController` (as a `protected` member next to `AcceptLanguage`) the
day a second controller needs it, not before.

Convention this records for future helpers:

- Helpers that read **HTTP context** (claims, headers, cookies,
  `Request`/`Response`) belong in the **controller layer** — private in the
  one controller that uses them, promoted to `BaseApiController` once
  shared.
- Helpers that contain **business logic** belong in the **service layer**.
  Services never touch `HttpContext`.

## Non-goals

- No behavior change — the `Guid.Empty` fallback stays as is.
- No `BaseApiController` member, no `ClaimsPrincipal` extension class.
- No call-site or signature changes.

## Verification

- `dotnet build` — compiles with no new warnings.
- `dotnet test` — full suite passes with zero test edits. The happy path
  (valid `sub` claim → parsed Guid reaches the service) is covered directly:
  `Logout_reads_sub_claim_and_returns_200` asserts the service receives the
  parsed id, and the `Me`/`ChangePassword` tests also run through it.
- Coverage caveat: no test exercises the `Guid.Empty` fallback branch
  (missing or malformed claim); that branch is verified by review, same as
  the uncovered sites noted in the parent spec.
