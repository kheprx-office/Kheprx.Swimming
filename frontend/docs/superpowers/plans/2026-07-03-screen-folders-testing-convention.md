# Screen-folder + centralized `testing/` convention — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganize each frontend feature so every page lives in a `presentation/pages/<screen>/` folder (page + template + viewmodel together) and every `.spec.ts` lives in a per-feature `testing/` tree mirroring the source layout.

**Architecture:** Pure structural refactor — file moves (`git mv`), import-path rewrites, and two inline-template extractions. No runtime, routing, or test behavior changes. The safety net is the existing Jest suite plus `ng build`; each task must leave both green.

**Tech Stack:** Angular 20 (standalone components, signals), TypeScript 5.9 (strict), Jest 30 via `jest-preset-angular`, Tailwind 3.4. Path aliases `@core/*` → `src/app/core/*`, `@features/*` → `src/app/features/*` (defined in `tsconfig.json`, mirrored in `jest.config.js`).

**Spec:** `docs/superpowers/specs/2026-07-03-screen-folders-testing-convention-design.md`

## Global Constraints

- **Working directory:** all commands run from `frontend/`. Git paths use forward slashes (valid on Windows).
- **COMMIT GATE:** the user has **not** authorized commits. `git mv` stages moves but does not commit — that is allowed. At each "Commit" step, **do not run `git commit`** until the user explicitly authorizes; when they do, run the prepared commits in task order (Task 1 → 4).
- **Keep-green rule:** every task ends with `npm test` (full Jest suite) **and** `npm run build` (Angular build) passing, with the **same test count** as the preflight baseline. No test assertions may be edited — only import paths.
- **Alias-import rule for moved specs:** any spec that imports its subject-under-test by a **relative** path (`./x` or `../x`) must switch to the `@features/…` alias pointing at the subject's **new** location. Specs that already import only via `@core/…`/`@features/…` move **unchanged**.
- **No tsconfig changes:** `tsconfig.app.json` excludes `src/**/*.spec.ts`; `tsconfig.spec.json` includes `src/**/*.spec.ts`. Both already cover the new `testing/` folders. Do not modify them.
- **Router/guard/store untouched:** `src/app/app.routes.ts` imports via feature barrels; `auth.guard.ts` and `auth-session.store.ts` stay at the `presentation/` root. None of these files change.

---

### Task 1: Restructure the `auth` feature

The richest feature: 3 screens (`login`, `change-password`, `account`), 2 viewmodels, 14 spec files, 2 inline templates to extract.

**Files:**
- Move (source, no edit): `data/dto/auth.dto.ts`, `data/repositories/auth.repository.impl.ts`, etc. — **source files do not move**; only specs and presentation files move (below).
- Move (specs → `testing/`, no edit): 12 alias-only specs (listed in Step 2).
- Move + edit (specs): `login.viewmodel.spec.ts`, `change-password.viewmodel.spec.ts`.
- Move (presentation): `login.page.ts`, `login.page.html`, `login.viewmodel.ts`, `change-password.page.ts`, `change-password.viewmodel.ts`, `account.page.ts`.
- Create: `presentation/pages/change-password/change-password.page.html`, `presentation/pages/account/account.page.html`.
- Modify: `login.page.ts` (import), `change-password.page.ts` (import + templateUrl), `account.page.ts` (templateUrl), `index.ts` (5 export paths).

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces: barrel `@features/auth` public surface is **unchanged** — still exports `LoginPage`, `AccountPage`, `ChangePasswordPage`, `LoginViewModel`, `ChangePasswordViewModel`. `app.routes.ts` and `auth.guard.ts` continue to resolve without edits.

- [ ] **Step 1: Preflight — confirm green baseline**

Run: `npm test`
Expected: PASS. **Record the total test count** (e.g. "Tests: N passed").
Run: `npm run build`
Expected: build succeeds, no errors.

- [ ] **Step 2: Move the 12 alias-only specs into `testing/` (no content edits)**

These specs already import only via `@core/…`/`@features/…`, so they move verbatim:

```bash
git mv src/app/features/auth/data/dto/auth.dto.spec.ts                         src/app/features/auth/testing/data/dto/auth.dto.spec.ts
git mv src/app/features/auth/data/repositories/auth.repository.impl.spec.ts    src/app/features/auth/testing/data/repositories/auth.repository.impl.spec.ts
git mv src/app/features/auth/data/auth.interceptor.spec.ts                     src/app/features/auth/testing/data/auth.interceptor.spec.ts
git mv src/app/features/auth/data/token-store.spec.ts                          src/app/features/auth/testing/data/token-store.spec.ts
git mv src/app/features/auth/domain/model/auth.spec.ts                         src/app/features/auth/testing/domain/model/auth.spec.ts
git mv src/app/features/auth/domain/usecases/change-password.use-case.spec.ts  src/app/features/auth/testing/domain/usecases/change-password.use-case.spec.ts
git mv src/app/features/auth/domain/usecases/load-current-user.use-case.spec.ts src/app/features/auth/testing/domain/usecases/load-current-user.use-case.spec.ts
git mv src/app/features/auth/domain/usecases/login.use-case.spec.ts            src/app/features/auth/testing/domain/usecases/login.use-case.spec.ts
git mv src/app/features/auth/domain/usecases/logout.use-case.spec.ts           src/app/features/auth/testing/domain/usecases/logout.use-case.spec.ts
git mv src/app/features/auth/domain/usecases/refresh-token.use-case.spec.ts    src/app/features/auth/testing/domain/usecases/refresh-token.use-case.spec.ts
git mv src/app/features/auth/presentation/auth-session.store.spec.ts           src/app/features/auth/testing/presentation/auth-session.store.spec.ts
git mv src/app/features/auth/presentation/auth.guard.spec.ts                   src/app/features/auth/testing/presentation/auth.guard.spec.ts
```

- [ ] **Step 3: Restructure the `login` screen**

Move the three login files into the screen folder and its spec into the mirrored `testing/` path:

```bash
git mv src/app/features/auth/presentation/pages/login.page.ts        src/app/features/auth/presentation/pages/login/login.page.ts
git mv src/app/features/auth/presentation/pages/login.page.html      src/app/features/auth/presentation/pages/login/login.page.html
git mv src/app/features/auth/presentation/viewmodels/login.viewmodel.ts src/app/features/auth/presentation/pages/login/login.viewmodel.ts
git mv src/app/features/auth/presentation/viewmodels/login.viewmodel.spec.ts src/app/features/auth/testing/presentation/pages/login/login.viewmodel.spec.ts
```

Edit `src/app/features/auth/presentation/pages/login/login.page.ts` line 12 — the viewmodel now sits beside the page:

```ts
// before
import { LoginViewModel } from '../viewmodels/login.viewmodel';
// after
import { LoginViewModel } from './login.viewmodel';
```

Edit `src/app/features/auth/testing/presentation/pages/login/login.viewmodel.spec.ts` line 3 — switch to the alias for the new location:

```ts
// before
import { LoginViewModel } from './login.viewmodel';
// after
import { LoginViewModel } from '@features/auth/presentation/pages/login/login.viewmodel';
```

(`login.page.ts` already uses `templateUrl: './login.page.html'` — still valid, since the html moved with it.)

- [ ] **Step 4: Restructure the `change-password` screen (move + extract template)**

```bash
git mv src/app/features/auth/presentation/pages/change-password.page.ts        src/app/features/auth/presentation/pages/change-password/change-password.page.ts
git mv src/app/features/auth/presentation/viewmodels/change-password.viewmodel.ts src/app/features/auth/presentation/pages/change-password/change-password.viewmodel.ts
git mv src/app/features/auth/presentation/viewmodels/change-password.viewmodel.spec.ts src/app/features/auth/testing/presentation/pages/change-password/change-password.viewmodel.spec.ts
```

Create `src/app/features/auth/presentation/pages/change-password/change-password.page.html` with the extracted markup:

```html
<div class="relative flex min-h-full w-full items-center justify-center p-4">
  <app-decor-background />
  <section class="glass-panel relative z-10 w-full max-w-md rounded-3xl p-8 shadow-xl">
    <h1 class="gradient-text mb-6 text-2xl font-extrabold">تغيير كلمة المرور</h1>
    <div class="space-y-4">
      <div class="space-y-1.5">
        <label class="block text-sm font-semibold text-text-secondary">كلمة المرور الحالية</label>
        <input
          [ngModel]="vm.currentPassword()"
          (ngModelChange)="vm.currentPassword.set($event)"
          type="password"
          placeholder="••••••••"
          autocomplete="current-password"
          dir="ltr"
          class="w-full rounded-xl border border-border bg-canvas px-4 py-3 text-ink outline-none transition-all focus:border-accent focus:ring-2 focus:ring-accent/30" />
      </div>
      <div class="space-y-1.5">
        <label class="block text-sm font-semibold text-text-secondary">كلمة المرور الجديدة</label>
        <input
          [ngModel]="vm.newPassword()"
          (ngModelChange)="vm.newPassword.set($event)"
          type="password"
          placeholder="••••••••"
          autocomplete="new-password"
          dir="ltr"
          class="w-full rounded-xl border border-border bg-canvas px-4 py-3 text-ink outline-none transition-all focus:border-accent focus:ring-2 focus:ring-accent/30" />
      </div>
      <div class="space-y-1.5">
        <label class="block text-sm font-semibold text-text-secondary">تأكيد كلمة المرور</label>
        <input
          [ngModel]="vm.confirmPassword()"
          (ngModelChange)="vm.confirmPassword.set($event)"
          type="password"
          placeholder="••••••••"
          autocomplete="new-password"
          dir="ltr"
          class="w-full rounded-xl border border-border bg-canvas px-4 py-3 text-ink outline-none transition-all focus:border-accent focus:ring-2 focus:ring-accent/30" />
      </div>
      @if (vm.error(); as e) {
        <p class="rounded-xl border border-red-400/40 bg-red-500/10 px-4 py-3 text-sm font-medium text-red-500">{{ e }}</p>
      }
      <button
        (click)="vm.submit()"
        [disabled]="vm.loading()"
        class="mt-2 flex w-full items-center justify-center gap-2 rounded-xl bg-accent px-4 py-3 text-base font-bold text-white shadow-md transition-all hover:brightness-110 disabled:opacity-70 active:scale-[0.97]">
        {{ vm.loading() ? 'جارٍ الحفظ...' : 'تغيير كلمة المرور' }}
      </button>
    </div>
  </section>
</div>
```

Replace the entire contents of `src/app/features/auth/presentation/pages/change-password/change-password.page.ts` with (import fixed to `./`, inline `template:` replaced by `templateUrl:`):

```ts
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecorBackgroundComponent } from '@core/ui/components/decor-background.component';
import { ChangePasswordViewModel } from './change-password.viewmodel';

@Component({
  selector: 'app-change-password-page',
  standalone: true,
  imports: [FormsModule, DecorBackgroundComponent],
  templateUrl: './change-password.page.html',
})
export class ChangePasswordPage {
  protected readonly vm = inject(ChangePasswordViewModel);
}
```

Edit `src/app/features/auth/testing/presentation/pages/change-password/change-password.viewmodel.spec.ts` line 3:

```ts
// before
import { ChangePasswordViewModel } from './change-password.viewmodel';
// after
import { ChangePasswordViewModel } from '@features/auth/presentation/pages/change-password/change-password.viewmodel';
```

- [ ] **Step 5: Restructure the `account` screen (move + extract template)**

```bash
git mv src/app/features/auth/presentation/pages/account.page.ts src/app/features/auth/presentation/pages/account/account.page.ts
```

Create `src/app/features/auth/presentation/pages/account/account.page.html` with the extracted markup:

```html
<div class="relative flex min-h-full w-full items-center justify-center p-4">
  <app-decor-background />
  <section class="glass-panel relative z-10 w-full max-w-md rounded-3xl p-8 shadow-xl">
    <h1 class="gradient-text mb-6 text-2xl font-extrabold">الحساب</h1>
    @if (auth.principal(); as p) {
      <div class="mb-6 space-y-3">
        <div class="rounded-xl border border-border bg-canvas px-4 py-3">
          <p class="text-xs font-semibold text-text-secondary mb-0.5">المستخدم</p>
          <p class="text-base font-bold text-ink" dir="ltr">{{ p.userId }}</p>
        </div>
        <div class="rounded-xl border border-border bg-canvas px-4 py-3">
          <p class="text-xs font-semibold text-text-secondary mb-0.5">الصلاحية</p>
          <p class="text-base font-bold text-ink">{{ ROLE_LABELS[p.role] }}</p>
        </div>
      </div>
    }
    <div class="space-y-3">
      <a
        routerLink="/change-password"
        class="flex w-full items-center justify-center rounded-xl border border-border bg-canvas px-4 py-3 text-base font-semibold text-ink transition-all hover:bg-surface active:scale-[0.98]">
        تغيير كلمة المرور
      </a>
      <button
        (click)="signOut()"
        class="flex w-full items-center justify-center rounded-xl bg-danger px-4 py-3 text-base font-bold text-white shadow-md transition-all hover:brightness-110 active:scale-[0.97]">
        تسجيل الخروج
      </button>
    </div>
  </section>
</div>
```

Replace the entire contents of `src/app/features/auth/presentation/pages/account/account.page.ts` with (inline `template:` replaced by `templateUrl:`; no viewmodel — it injects the store directly):

```ts
import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { ROLE_LABELS } from '@core/domain/roles';
import { DecorBackgroundComponent } from '@core/ui/components/decor-background.component';

@Component({
  selector: 'app-account-page',
  standalone: true,
  imports: [RouterLink, DecorBackgroundComponent],
  templateUrl: './account.page.html',
})
export class AccountPage {
  protected readonly auth = inject(AuthSessionStore);
  private readonly router = inject(Router);
  readonly ROLE_LABELS = ROLE_LABELS;

  async signOut(): Promise<void> {
    await this.auth.signOut();
    void this.router.navigate(['/login']);
  }
}
```

- [ ] **Step 6: Remove the now-empty `viewmodels/` directory**

Run: `Remove-Item -Recurse -Force src/app/features/auth/presentation/viewmodels`
Expected: no error (directory is empty after the moves). If PowerShell reports "not found", it was already pruned — continue.

- [ ] **Step 7: Update the `auth` barrel**

Replace the entire contents of `src/app/features/auth/index.ts` with:

```ts
export { LoginPage } from './presentation/pages/login/login.page';
export { AccountPage } from './presentation/pages/account/account.page';
export { ChangePasswordPage } from './presentation/pages/change-password/change-password.page';
export { LoginViewModel } from './presentation/pages/login/login.viewmodel';
export { ChangePasswordViewModel } from './presentation/pages/change-password/change-password.viewmodel';
```

- [ ] **Step 8: Verify green**

Run: `npm test`
Expected: PASS, **same total test count as Step 1**.
Run: `npm run build`
Expected: build succeeds, no errors.
If either fails, the most likely cause is a missed relative import — grep the moved files for `from './` / `from '../` and confirm each resolves.

- [ ] **Step 9: Commit (GATED — do not run until the user authorizes commits)**

```bash
git add -A
git commit -m "refactor(auth): screen folders + centralized testing/"
```

---

### Task 2: Restructure the `user-management` feature

One screen (`user-management`), one viewmodel, 5 spec files, template already external.

**Files:**
- Move (specs → `testing/`, no edit): `user.dto.spec.ts`, `users.repository.impl.spec.ts`, `user.spec.ts` (model), `users.use-cases.spec.ts`.
- Move + edit (spec): `users.viewmodel.spec.ts`.
- Move (presentation): `user-management.page.ts`, `user-management.page.html`, `users.viewmodel.ts`.
- Modify: `user-management.page.ts` (import), `index.ts` (2 export paths).

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces: barrel `@features/user-management` unchanged — still exports `UserManagementPage`, `UsersViewModel`.

- [ ] **Step 1: Move the 4 alias-only specs into `testing/` (no content edits)**

```bash
git mv src/app/features/user-management/data/dto/user.dto.spec.ts                      src/app/features/user-management/testing/data/dto/user.dto.spec.ts
git mv src/app/features/user-management/data/repositories/users.repository.impl.spec.ts src/app/features/user-management/testing/data/repositories/users.repository.impl.spec.ts
git mv src/app/features/user-management/domain/model/user.spec.ts                      src/app/features/user-management/testing/domain/model/user.spec.ts
git mv src/app/features/user-management/domain/usecases/users.use-cases.spec.ts        src/app/features/user-management/testing/domain/usecases/users.use-cases.spec.ts
```

- [ ] **Step 2: Restructure the `user-management` screen**

```bash
git mv src/app/features/user-management/presentation/pages/user-management.page.ts   src/app/features/user-management/presentation/pages/user-management/user-management.page.ts
git mv src/app/features/user-management/presentation/pages/user-management.page.html src/app/features/user-management/presentation/pages/user-management/user-management.page.html
git mv src/app/features/user-management/presentation/viewmodels/users.viewmodel.ts   src/app/features/user-management/presentation/pages/user-management/users.viewmodel.ts
git mv src/app/features/user-management/presentation/viewmodels/users.viewmodel.spec.ts src/app/features/user-management/testing/presentation/pages/user-management/users.viewmodel.spec.ts
```

Edit `src/app/features/user-management/presentation/pages/user-management/user-management.page.ts` line 10:

```ts
// before
import { UsersViewModel } from '../viewmodels/users.viewmodel';
// after
import { UsersViewModel } from './users.viewmodel';
```

Edit `src/app/features/user-management/testing/presentation/pages/user-management/users.viewmodel.spec.ts` line 2:

```ts
// before
import { UsersViewModel } from './users.viewmodel';
// after
import { UsersViewModel } from '@features/user-management/presentation/pages/user-management/users.viewmodel';
```

- [ ] **Step 3: Remove the now-empty `viewmodels/` directory**

Run: `Remove-Item -Recurse -Force src/app/features/user-management/presentation/viewmodels`
Expected: no error. If "not found", continue.

- [ ] **Step 4: Update the `user-management` barrel**

Replace the entire contents of `src/app/features/user-management/index.ts` with:

```ts
export { UserManagementPage } from './presentation/pages/user-management/user-management.page';
export { UsersViewModel } from './presentation/pages/user-management/users.viewmodel';
```

- [ ] **Step 5: Verify green**

Run: `npm test`
Expected: PASS, same total test count as the baseline.
Run: `npm run build`
Expected: build succeeds.

- [ ] **Step 6: Commit (GATED — do not run until the user authorizes commits)**

```bash
git add -A
git commit -m "refactor(users): screen folders + centralized testing/"
```

---

### Task 3: Restructure the `home` feature

One screen (`home`), no viewmodel, template already external, one **page** spec (not a viewmodel).

**Files:**
- Move (presentation): `home.page.ts`, `home.page.html`.
- Move + edit (spec): `home.page.spec.ts`.
- Modify: `index.ts` (1 export path).

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces: barrel `@features/home` unchanged — still exports `HomePage`.

- [ ] **Step 1: Restructure the `home` screen**

```bash
git mv src/app/features/home/presentation/pages/home.page.ts      src/app/features/home/presentation/pages/home/home.page.ts
git mv src/app/features/home/presentation/pages/home.page.html    src/app/features/home/presentation/pages/home/home.page.html
git mv src/app/features/home/presentation/pages/home.page.spec.ts src/app/features/home/testing/presentation/pages/home/home.page.spec.ts
```

Edit `src/app/features/home/testing/presentation/pages/home/home.page.spec.ts` line 3:

```ts
// before
import { HomePage } from './home.page';
// after
import { HomePage } from '@features/home/presentation/pages/home/home.page';
```

(`home.page.ts` uses `templateUrl: './home.page.html'` and imports no viewmodel — no other edits needed.)

- [ ] **Step 2: Update the `home` barrel**

Replace the entire contents of `src/app/features/home/index.ts` with:

```ts
export { HomePage } from './presentation/pages/home/home.page';
```

- [ ] **Step 3: Verify green**

Run: `npm test`
Expected: PASS, same total test count as the baseline.
Run: `npm run build`
Expected: build succeeds.

- [ ] **Step 4: Commit (GATED — do not run until the user authorizes commits)**

```bash
git add -A
git commit -m "refactor(home): screen folders + centralized testing/"
```

---

### Task 4: Update the architecture docs

Refresh the docs that describe the folder convention so they match the new structure.

**Files:**
- Modify: `docs/FEATURE_ANATOMY.html`, `docs/TESTING.html`, `docs/ADD_A_FEATURE.html`.
- Verify (grep): all `docs/*.html`.

**Interfaces:** none (documentation only).

- [ ] **Step 1: Find every stale path reference in the docs**

Run:
```bash
grep -rnE "presentation/(pages/(login|account|change-password|home|user-management)\.page|viewmodels)" frontend/docs --include=*.html
```
Expected: a list of lines in `FEATURE_ANATOMY.html`, `TESTING.html`, `ADD_A_FEATURE.html` (and possibly others) referencing the **old** flat `pages/<x>.page` and `presentation/viewmodels/` layout, plus any co-located `.spec.ts` mentions. This is the worklist for Steps 2–4.

- [ ] **Step 2: Update `FEATURE_ANATOMY.html`**

Replace the feature-structure description/diagram with the new canonical layout. Every feature now reads:

```
features/<feature>/
├── data/                          (dto, repositories, providers, interceptors — source only)
├── domain/                        (model, repositories, usecases — source only)
├── presentation/
│   ├── pages/
│   │   └── <screen>/              <screen>.page.ts · <screen>.page.html · <screen>.viewmodel.ts (if any)
│   └── <non-screen presentation files: stores, guards>
├── testing/                       (mirrors data/ · domain/ · presentation/ — all *.spec.ts live here)
└── index.ts
```

State the two rules explicitly: (A) each screen is a folder under `presentation/pages/` bundling page + template + viewmodel; non-screen presentation files (stores, guards) stay at the `presentation/` root. (B) no `.spec.ts` lives outside `testing/`; `testing/` mirrors the source path down to the screen folder, and specs import their subject via `@features`/`@core` aliases.

- [ ] **Step 3: Update `TESTING.html`**

Document the centralized `testing/` convention: where specs live (`<feature>/testing/<layer>/…`, mirroring source down to `testing/presentation/pages/<screen>/`), that Jest still discovers them via its `**/*.spec.ts` glob (no config change), and that specs import the unit under test through path aliases rather than relative paths.

- [ ] **Step 4: Update `ADD_A_FEATURE.html`**

Update the step-by-step so a new screen is created at `presentation/pages/<screen>/` with `<screen>.page.ts` + `<screen>.page.html` (+ `<screen>.viewmodel.ts` when it has one), and its spec is created at the mirrored `testing/presentation/pages/<screen>/<screen>.viewmodel.spec.ts` (or `<screen>.page.spec.ts`). Update any data/domain spec-creation steps to point at `testing/data/…` and `testing/domain/…`.

- [ ] **Step 5: Fix any remaining stale references and verify the docs are clean**

Apply the same path corrections to any other `docs/*.html` files surfaced in Step 1. Then re-run:
```bash
grep -rnE "presentation/(pages/(login|account|change-password|home|user-management)\.page|viewmodels)" frontend/docs --include=*.html
```
Expected: **no output** (all stale references fixed).

- [ ] **Step 6: Commit (GATED — do not run until the user authorizes commits)**

```bash
git add -A
git commit -m "docs(fe): document screen-folder + centralized testing convention"
```

---

## Self-Review

**Spec coverage:**
- Rule A (screen folders) → Tasks 1–3 (login/change-password/account/user-management/home each foldered).
- Rule B (central `testing/`, mirror down to screen) → Tasks 1–3 spec moves; alias-import rule applied to the 4 relative-import specs.
- Inline-template extraction (account, change-password) → Task 1 Steps 4–5.
- `index.ts` updates → Tasks 1/2/3.
- Router/guard/store untouched → asserted in Global Constraints; verified by `npm run build` each task.
- Architecture docs → Task 4.
- Commit gate → Global Constraints + every "Commit" step marked GATED.
- Verification (`npm test` same count + `npm run build`) → every task's verify step. ✓ All spec sections covered.

**Placeholder scan:** No "TBD"/"handle edge cases"/"similar to Task N". Every code/edit step shows exact before→after or full file content; extracted HTML is reproduced in full. ✓

**Type consistency:** Barrel exports (`LoginPage`, `AccountPage`, `ChangePasswordPage`, `LoginViewModel`, `ChangePasswordViewModel`, `UserManagementPage`, `UsersViewModel`, `HomePage`) are preserved verbatim from the originals — no renames. Import paths in specs point at the exact new file locations created in the same task. ✓
