# Controller Readability Refactor — Design

**Date:** 2026-07-06
**Status:** Approved (amended 2026-07-06: added ProductsController.Delete, a tenth site whose inverted bool ternary the original search missed)
**Branch:** feat/identity-auth

## Problem

Ten controller actions return their result through a dense conditional
expression: a `null` check, a `?`, and two 130+-character lines that each
build a localized message, wrap it in an `ApiResponse` envelope, and pick a
status code — all in one expression. The pattern is correct and fully
tested, but hard to read for anyone learning the codebase.

## Goal

Restructure each of the ten sites into a guard clause with named
intermediate variables, so every line does one visible thing. Behavior is
identical: same status codes, same envelopes, same localized messages, same
error codes.

## Non-goals

- **No try/catch and no exceptions.** Services signal expected failures by
  returning `null`; nothing throws on a wrong password, so a `catch` block
  would be dead code. That design stays.
- No service-layer or DTO changes.
- No new helpers, base-class members, or abstractions.
- No changes to endpoints that already return a single short line
  (e.g. `Logout`, list endpoints).

## The template

Every site follows this exact shape (names included — `message`/`envelope`
in the failure block, `successMessage`/`body` for the success path):

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

Note: the failure block's `message`/`envelope` are block-scoped, but C#
(CS0136) still forbids re-declaring those names later in the same method,
hence the distinct success-path names.

## Scope — the ten sites

| Controller | Action | Failure status | Error code |
|---|---|---|---|
| AuthController | Login | 401 Unauthorized | INVALID_CREDENTIALS |
| AuthController | Refresh | 401 Unauthorized | INVALID_REFRESH_TOKEN |
| AuthController | Me | 401 Unauthorized | NOT_AUTHENTICATED |
| AuthController | ChangePassword | 401 Unauthorized | INVALID_CREDENTIALS |
| UsersController | Create | 409 Conflict | EMAIL_IN_USE |
| UsersController | Update | 404 NotFound | USER_NOT_FOUND |
| UsersController | SetStatus | 404 NotFound | USER_NOT_FOUND |
| ProductsController | GetById | 404 NotFound | PRODUCT_NOT_FOUND |
| ProductsController | Update | 404 NotFound | PRODUCT_NOT_FOUND |
| ProductsController | Delete | 404 NotFound | PRODUCT_NOT_FOUND |

Doc comments, attributes (`[ProducesResponseType]` etc.), and method
signatures are untouched.

## Verification

- `dotnet build` — compiles with no new warnings.
- `dotnet test` — the full suite passes with **zero test edits**. If any
  test needs changing, the refactor altered behavior and the code change
  (not the test) must be fixed.
- Coverage caveat (final review, 2026-07-06): the suite does not cover
  every changed site — ProductsController has no controller-level tests
  (GetById, Update, Delete all unexercised), AuthController.Refresh has
  no controller test, and Me's 401 branch is untested. These sites were
  verified semantically equivalent by line-by-line review in the final
  whole-branch review. Follow-up (out of scope for this zero-test-edit
  run): add ProductsControllerTests plus Refresh and Me-401 controller
  tests.
