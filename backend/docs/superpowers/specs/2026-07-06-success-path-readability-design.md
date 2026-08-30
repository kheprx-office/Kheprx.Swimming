# Success-Path Readability Refactor — Design

**Date:** 2026-07-06
**Status:** Approved
**Branch:** feat/identity-auth

## Problem

Six controller actions have no failure branch, so the earlier guard-clause
refactor (`2026-07-06-controller-readability-design.md`) left them out. Each
still returns through one dense expression that builds a localized message,
wraps it in an `ApiResponse` envelope, and picks a status code in a single
nested call — two of them as expression-bodied members with an inline
`await`. The pattern is correct and tested, but hard to read next to the
restructured sites.

This spec supersedes the earlier spec's non-goal "No changes to endpoints
that already return a single short line (e.g. `Logout`, list endpoints)" —
that exclusion was about the guard-clause round, and these sites now get the
same treatment so one style covers every controller action.

## Goal

Rewrite each of the six sites into the committed named-locals shape, so
every line does one visible thing. Behavior is identical: same status
codes, same envelopes, same localized messages, same `Location` header on
Create.

## Non-goals

- No guard clauses — these sites have no failure branch, so none is added.
- No service-layer or DTO changes.
- No new helpers, base-class members, or abstractions (the shared-helper
  approach was considered and rejected: it hides envelope construction and
  diverges from the committed style).
- No changes to the ten sites already restructured in the previous round.

## The template

Every site follows this exact shape — fetch the data into a named local,
blank line, then `successMessage`/`body`, each local on **one line**
(matching the committed `AuthController.Login` style):

```csharp
var types = await _service.GetAllAsync(ct);

var successMessage = EngagementTypeMessages.Success.EngagementTypesListed(AppLanguage.Current);
var body = ApiResponse<IReadOnlyList<EngagementTypeDto>>.Success(successMessage, types);
return Ok(body);
```

The two expression-bodied actions (`UsersController.List`,
`ProductsController.GetAll`) convert to block bodies so the locals have
somewhere to live. Data locals are named for the resource, plural for
lists: the existing `types` and `roles` stay, `List` gets `users`,
`GetAll` gets `products`, and `Create` keeps `product`.

Special cases:

- **AuthController.Logout** — there is no data: the `LogoutAsync` call stays
  first, and `body` is built with `null` data
  (`ApiResponse<object>.Success(successMessage, null)`). The
  `// AD-009: no body` comment on the signature line stays untouched.
- **ProductsController.Create** — the final line is
  `return CreatedAtAction(nameof(GetById), new { id = product.Id }, body);`
  instead of `Ok(body)`, preserving the `Location` header.

## Scope — the six sites

| Controller | Action | Current shape |
|---|---|---|
| AuthController | Logout | one-line nested return |
| EngagementTypesController | Get | wrapped nested return |
| RolesController | Get | one-line nested return |
| UsersController | List | expression-bodied, inline `await` |
| ProductsController | GetAll | expression-bodied, inline `await` |
| ProductsController | Create | nested `Success(...)` inside `CreatedAtAction` |

Doc comments, attributes (`[ProducesResponseType]` etc.), routes, and method
signatures are untouched.

## Verification

- `dotnet build` — compiles with no new warnings.
- `dotnet test` — the full suite passes with **zero test edits**. If any
  test needs changing, the refactor altered behavior and the code change
  (not the test) must be fixed.
- Coverage caveat (carried over from the previous round): ProductsController
  has no controller-level tests, so its two sites (GetAll, Create) rely on
  build success plus line-by-line review for equivalence. The follow-up to
  add ProductsControllerTests remains out of scope here.
