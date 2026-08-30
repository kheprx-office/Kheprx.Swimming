# FluentValidation Teaching Comments — Design

**Date:** 2026-07-06
**Status:** Approved
**Scope:** One file, comments only. Zero behavior change.

## Goal

Make `AddFluentValidationConfiguration` understandable to a reader who does
not know AppDomain scanning or FluentValidation, by adding teaching-style
comments before each statement — the same plain-English "what AND why" voice
already used throughout `AuthService` (e.g. its `CancellationToken`
explainer). User-requested; scoped to this one method (the other extension
classes may get the same treatment later if the result reads well).

## The change

In `backend/Kheprx.BaseBackend.Api/Extensions/FluentValidationExtensions.cs`,
insert three comment blocks inside the method body. Every existing line —
including the XML `<summary>` — stays byte-identical. The resulting method
reads exactly:

```csharp
    public static IServiceCollection AddFluentValidationConfiguration(this IServiceCollection services)
    {
        // Ask the runtime for every assembly (DLL) that is ALREADY loaded into this
        // process, then keep only ours: the ones whose name starts with
        // "Kheprx.BaseBackend." (Identity.Application, Catalog.Application, ...).
        // This is why call order in Program.cs matters - AddModules loads the module
        // DLLs first, so they are all in this list by the time we scan.
        // Filter details: GetName().Name is the simple assembly name; "?." + "== true"
        // treats a null name as "not ours" instead of crashing; Ordinal is a plain
        // character-by-character match (no culture rules).
        var moduleAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Kheprx.BaseBackend.", StringComparison.Ordinal) == true)
            .ToArray();

        // Hand those assemblies to FluentValidation: it scans each one for classes that
        // inherit AbstractValidator<T> (LoginRequestValidator, ...) and registers each
        // in DI as IValidator<T>, so ValidationFilter can resolve the right validator
        // for each incoming request DTO. includeInternalTypes: true widens the scan to
        // internal classes too - our validators are public today, but module
        // implementation types are conventionally internal here (AuthService is one),
        // so the flag keeps the scan safe if validators follow that convention.
        services.AddValidatorsFromAssemblies(moduleAssemblies, includeInternalTypes: true);

        // Return the same collection - the standard fluent shape for extension methods.
        return services;
    }
```

## Constraints

- Pure insertion: the diff contains only added comment lines (and their
  surrounding blank-line spacing shown above); `git diff --numstat` shows
  0 deletions for the file.
- The XML `<summary>` is untouched. The first body comment deliberately
  restates its call-order constraint at the point of confusion
  (`GetAssemblies()`) — the same accepted duplication trade-off as the
  regions run's title-vs-summary overlap.
- Comment voice matches `AuthService`: full sentences, plain English,
  ASCII hyphens/quotes as shown.
- No other file changes; zero test edits.

## Verification

Comments cannot change behavior; the standard cheap gate still applies:
`dotnet build backend/Kheprx.BaseBackend.sln` (0 warnings) and
`dotnet test backend/Kheprx.BaseBackend.sln` (147/147).

## Decisions log

- **Scope — just this method:** user chose the single pasted method over a
  consistency sweep of all Api extension classes.
- **Style — teaching:** user chose AuthService-style multi-line why-comments
  over concise one-liners, from side-by-side previews.
- **Post-review correction (2026-07-06):** the original `includeInternalTypes`
  rationale was factually wrong — all 8 validators are `public sealed class`,
  not internal (final-review Critical). Block 2 reworded to the truth (the
  flag widens the scan; kept for safety) and block 3's Program.cs chaining
  reference dropped. Corrected insertion is 16 comment lines + 1 blank.
