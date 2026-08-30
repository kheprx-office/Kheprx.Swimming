# Class Structure Regions & Constructor Refactor — Design

**Date:** 2026-07-06
**Status:** Approved
**Branch:** feat/identity-auth

## Problem

The API surface has drifted into three small inconsistencies:

1. Seven classes declare their constructor as a one-line expression body
   (`public RoleService(IRoleRepository roles) => _roles = roles;`) while the
   three larger services use block bodies.
2. The two lookup services (`RoleService`, `EngagementTypeService`) return
   their DTO list through a fluent `Select(...).ToList()` chain returned
   directly, with an inline lambda — dense next to the named-locals style
   the controllers now use.
3. Only private helpers have a region convention (`#region Helpers`, three
   classes); fields, constructors, and public methods float free, so class
   anatomy differs file to file.

## Goal

One consistent class layout across the whole API surface — 5 Application
services + 5 API controllers — with named regions, block-body constructors,
and explicit mapping bodies in the two lookup services. Zero behavior
change.

## Non-goals

- No mapper abstraction for the lookup services (considered and rejected:
  each maps one 5-property record in exactly one method — a `ToDto` helper
  or mapper class is ceremony until a second call site exists).
- No changes to `ProductService.GetAllAsync`'s one-liner
  (`=> _mapper.ToDtoList(...)`) — it delegates to the module's existing
  `CatalogMapper` and was not part of the ask.
- No changes to `BaseApiController` (no fields, no constructor),
  `ValidationFilter`, or any Infrastructure class (repositories, module
  APIs, `JwtTokenService`) — their 10 expression-bodied constructors stay.
- No service-layer logic, DTO, or test changes.

## Region layout

Four region names, matching the existing `#region Helpers` convention
(PascalCase, `#region` at member indentation, one blank line after
`#region X` and one before `#endregion`, one blank line between regions),
always in this order:

```csharp
#region Fields        // all class-level state: consts, static readonly, private readonly fields
#region Constructor
#region APIs          // public interface methods (services) / action methods (controllers),
                      // each with its XML doc comment and attributes
#region Helpers       // existing — stays last (AuthController, AuthService, UserService only)
```

Rules:

- Classes that lack a section don't get that region — no empty regions.
- Everything moves *into* regions byte-identical — pure insertion of
  `#region`/`#endregion` lines — except the constructor and mapping
  rewrites below.
- `AuthService`'s `FallbackRole` const (with its explanatory comment) and
  `UserService`'s `CodePrefixes` dictionary live under **Fields**.
- `AuthService`'s CancellationToken teaching comment stays attached to
  `LoginAsync` inside **APIs**.

## Constructor rewrites — 7 sites

Expression-bodied constructors become block bodies:

```csharp
public RoleService(IRoleRepository roles)
{
    _roles = roles;
}
```

`UserService`, `AuthService`, `ProductService` already use block bodies —
unchanged, just wrapped in the Constructor region.

## Lookup mapping rewrites — 2 sites

`RoleService.GetAllAsync` and `EngagementTypeService.GetAllAsync` keep
LINQ but name the result and return it on its own line:

```csharp
public async Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken ct = default)
{
    var roles = await _roles.GetAllAsync(ct);

    var dtos = roles
        .Select(r => new RoleDto(r.Id, r.Code, r.LabelAr, r.LabelEn, r.SortOrder))
        .ToList();

    return dtos;
}
```

`EngagementTypeService` is identical in shape with `types`, `t`, and
`EngagementTypeDto(t.Id, t.Code, t.LabelAr, t.LabelEn, t.SortOrder)`.

## Scope — the ten classes

| Class | Regions | Ctor → block | Mapping rewrite |
|---|---|---|---|
| RoleService | Fields, Constructor, APIs | yes | yes |
| EngagementTypeService | Fields, Constructor, APIs | yes | yes |
| UserService | Fields, Constructor, APIs, Helpers | already block | — |
| AuthService | Fields, Constructor, APIs, Helpers | already block | — |
| ProductService | Fields, Constructor, APIs | already block | — |
| AuthController | Fields, Constructor, APIs, Helpers | yes | — |
| RolesController | Fields, Constructor, APIs | yes | — |
| EngagementTypesController | Fields, Constructor, APIs | yes | — |
| UsersController | Fields, Constructor, APIs | yes | — |
| ProductsController | Fields, Constructor, APIs | yes | — |

XML doc comments, attributes (`[ProducesResponseType]`, routes, etc.),
method signatures, and all method bodies other than the 9 rewrite sites
above are untouched.

## Verification

- `dotnet build backend/Kheprx.BaseBackend.sln` — no new warnings.
- `dotnet test backend/Kheprx.BaseBackend.sln` — the full 147-test suite
  passes with **zero test edits**. If any test needs changing, the refactor
  altered behavior and the code change (not the test) must be fixed.
- Regions and block-vs-expression bodies are compile-time formatting only;
  the two mapping rewrites are the only sites where statements change, and
  both are directly covered:
  `backend/tests/Kheprx.BaseBackend.Identity.UnitTests/Services/RoleServiceTests.cs`
  and `.../EngagementTypeServiceTests.cs` exercise the rewritten
  `GetAllAsync` bodies themselves (the controller tests mock the service
  interfaces, so they do not).
