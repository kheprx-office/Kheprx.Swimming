# Settings & Shell Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle the shared Angular shell (teal sidebar + refined header) and the Settings (`/account`) page to match the reference screenshots, decompose Settings into section components, and extract one reusable `ChangePasswordForm` shared by `/change-password` (forced first-login) and Settings→Security.

**Architecture:** Frontend-only Angular (standalone components + signals, Tailwind with CSS-variable design tokens). Add a `sidebar` teal token; reuse the existing cream `canvas`, teal `primary`, Fraunces `font-heading`, and `.grain` overlay. Extract the duplicated password logic (today in both `ChangePasswordViewModel` and `AccountViewModel`) into `ChangePasswordForm`. Decompose `account.page` into `profile/preferences/security/about` section components.

**Tech Stack:** Angular (standalone, signals, control-flow `@if/@for`), TailwindCSS, Jest, `@lucide/angular` icons.

**Spec:** `frontend/docs/superpowers/specs/2026-09-01-settings-shell-redesign-design.md`

## Global Constraints

- **Real data, our brand:** wire the sidebar user-card + Profile to live session data (`AuthSessionStore` / `AccountViewModel`); keep the app's existing brand text. Do NOT hardcode "Oasis / Coach Dave".
- **Active club** = static placeholder (no backend). **Search** = styled but inert (no handler/binding, no "coming soon" text). **No new routes**; `/change-password` route + guard preserved.
- **Reference to match (Read these while doing visual tasks):** `C:\Users\envnt\Desktop\MCP\ui 1.png`, `ui 2.png`, `ui 3.png`. React reference (structure only, do not copy): `C:\Users\envnt\Desktop\4dba8937-ce3f-44bd-b093-515f10a5ea2b\src`.
- **Design tokens/classes to reuse:** cards `rounded-2xl border border-border bg-surface p-6 shadow-warm`; card headings `font-heading text-lg text-ink`; page heading `font-heading text-3xl text-ink`; teal accents/buttons `bg-primary text-white hover:bg-primary-hover`; role pill `inline-flex items-center gap-1 rounded-full border border-primary/40 px-3 py-1 text-xs font-semibold text-primary`; segmented `EN | عربي` control — reuse the markup from `login.page.html` (the `inline-flex rounded-[10px] border border-border …` block).
- **Shared password i18n key:** confirm-mismatch uses `changePassword.mismatch`; inline success uses `profile.security.success`.
- **Dir-aware:** shell is a flex row; sidebar is left in LTR / right in RTL. Use logical spacing; don't hardcode physical left/right that breaks Arabic.
- **Dirty working tree:** the repo has unrelated in-progress changes. Every commit step uses explicit `git add <paths>` — never `-A`. Confirm commit strategy with the user before the first commit (they may prefer no commits).
- **Commands (run from `frontend/`):** tests `npx jest <path> --silent`; full suite `npx jest --silent`; type-check/AOT `npx ng build --configuration development`. A pre-existing "worker process failed to exit gracefully" jest warning is normal.

---

### Task 1: Theme tokens — teal sidebar

**Files:**
- Modify: `frontend/src/styles.scss` (the `:root` and `.dark` token blocks near the top, ~lines 3–23)
- Modify: `frontend/tailwind.config.js` (the `theme.extend.colors` map, ~lines 7–52)

**Interfaces:**
- Produces: Tailwind classes `bg-sidebar`, `text-sidebar-ink` (and alpha variants) backed by `--c-sidebar` / `--c-sidebar-ink`, defined for both light and dark.

- [ ] **Step 1: Add the CSS variables**

In `src/styles.scss`, inside `:root { … }` add:
```scss
  --c-sidebar: 11 74 88;         /* #0B4A58 deep ocean teal */
  --c-sidebar-ink: 236 244 244;  /* #ECF4F4 light text on teal */
```
and inside `.dark { … }` add:
```scss
  --c-sidebar: 10 58 68;         /* #0A3A44 darker teal for dark mode */
  --c-sidebar-ink: 236 244 244;  /* #ECF4F4 */
```

- [ ] **Step 2: Map the Tailwind color**

In `tailwind.config.js`, inside `theme.extend.colors`, add:
```js
        sidebar: {
          DEFAULT: 'rgb(var(--c-sidebar) / <alpha-value>)',
          ink: 'rgb(var(--c-sidebar-ink) / <alpha-value>)',
        },
```

- [ ] **Step 3: Verify it type-checks/compiles**

Run: `npx ng build --configuration development`
Expected: build succeeds (no test asserts color; this token is consumed in Task 7).

- [ ] **Step 4: Commit** (if committing)
```bash
git add frontend/src/styles.scss frontend/tailwind.config.js
git commit -m "feat(theme): add teal sidebar design token (light + dark)"
```

---

### Task 2: i18n keys

**Files:**
- Modify: `frontend/src/app/core/i18n/en.json`
- Modify: `frontend/src/app/core/i18n/ar.json`

**Interfaces:**
- Produces translation keys: `shell.brandSubtitle`, `shell.activeClub.label`, `shell.activeClub.value`, `shell.nav.groups.overview|coaching|administration`; updated `shell.searchPlaceholder`.

- [ ] **Step 1: Add/adjust keys in `en.json`**

Inside the `"shell"` object, add these keys (and change `searchPlaceholder`):
```json
    "brandSubtitle": "International Swimming",
    "activeClub": { "label": "Active club", "value": "Oasis Aquatics" },
    "searchPlaceholder": "Search swimmers, events…",
    "nav": {
      "groups": { "overview": "Overview", "coaching": "Coaching", "administration": "Administration" },
      "dashboard": "Dashboard", "swimmers": "Swimmers", "attendance": "Attendance",
      "championships": "Championships", "captainPanel": "Captain Panel", "settings": "Settings"
    }
```
(Merge the `groups` key into the existing `nav` object — keep the existing nav item keys.)

- [ ] **Step 2: Add/adjust the same keys in `ar.json`**
```json
    "brandSubtitle": "السباحة الدولية",
    "activeClub": { "label": "النادي النشط", "value": "أوٰسِس أكواتيكس" },
    "searchPlaceholder": "ابحث عن سبّاحين، فعاليات…",
    "nav": {
      "groups": { "overview": "نظرة عامة", "coaching": "التدريب", "administration": "الإدارة" },
      "dashboard": "الرئيسية", "swimmers": "السباحون", "attendance": "الحضور",
      "championships": "البطولات", "captainPanel": "لوحة الكابتن", "settings": "الإعدادات"
    }
```

- [ ] **Step 3: Verify JSON is valid + app builds**

Run: `npx ng build --configuration development`
Expected: build succeeds. (Invalid JSON fails the build.)

- [ ] **Step 4: Commit** (if committing)
```bash
git add frontend/src/app/core/i18n/en.json frontend/src/app/core/i18n/ar.json
git commit -m "feat(i18n): add shell subtitle, active-club, nav groups, search placeholder"
```

---

### Task 3: Reusable `ChangePasswordForm` (TDD)

**Files:**
- Create: `frontend/src/app/features/auth/presentation/components/change-password-form/change-password-form.component.ts`
- Create: `frontend/src/app/features/auth/presentation/components/change-password-form/change-password-form.component.html`
- Test: `frontend/src/app/features/auth/testing/presentation/components/change-password-form/change-password-form.component.spec.ts`

**Interfaces:**
- Consumes: `AuthSessionStore.changePassword(current, new): Promise<Result<AuthSession>>`, `TranslateService.t`, `toUserMessage`.
- Produces: `ChangePasswordForm` (selector `app-change-password-form`) with `@Input() variant: 'full' | 'compact'` (default `'compact'`), `@Output() succeeded: EventEmitter<void>`, and signals `currentPassword`, `newPassword`, `confirmPassword`, `loading`, `error`.

- [ ] **Step 1: Write the failing spec**

`.../testing/presentation/components/change-password-form/change-password-form.component.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { ChangePasswordForm } from '@features/auth/presentation/components/change-password-form/change-password-form.component';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { TranslateService } from '@core/i18n';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';
import { AuthSession } from '@features/auth/domain/model/shared/auth';

const session: AuthSession = { tokens: { accessToken: 'a', refreshToken: 'r' }, principal: { role: 'captain', userId: 'U' }, mustChangePassword: false };
const MISMATCH_KEY = 'changePassword.mismatch';
const MISMATCH_MSG = 'New password and confirmation do not match';

describe('ChangePasswordForm', () => {
  const auth = { changePassword: jest.fn() } as unknown as AuthSessionStore;
  const i18n = { t: jest.fn((k: string) => (k === MISMATCH_KEY ? MISMATCH_MSG : k)) } as unknown as TranslateService;
  let cmp: ChangePasswordForm;
  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [ChangePasswordForm, { provide: AuthSessionStore, useValue: auth }, { provide: TranslateService, useValue: i18n }],
    });
    cmp = TestBed.inject(ChangePasswordForm);
  });

  it('blocks submit and shows the mismatch error when confirmation differs', async () => {
    cmp.currentPassword.set('old'); cmp.newPassword.set('new12345'); cmp.confirmPassword.set('different');
    await cmp.submit();
    expect(auth.changePassword).not.toHaveBeenCalled();
    expect(cmp.error()).toBe(MISMATCH_MSG);
  });

  it('calls changePassword, clears fields, and emits succeeded on success', async () => {
    (auth.changePassword as jest.Mock).mockResolvedValue(ok(session));
    const emit = jest.spyOn(cmp.succeeded, 'emit');
    cmp.currentPassword.set('old'); cmp.newPassword.set('new12345'); cmp.confirmPassword.set('new12345');
    await cmp.submit();
    expect(auth.changePassword).toHaveBeenCalledWith('old', 'new12345');
    expect(cmp.error()).toBeNull();
    expect(cmp.currentPassword()).toBe('');
    expect(cmp.newPassword()).toBe('');
    expect(cmp.confirmPassword()).toBe('');
    expect(emit).toHaveBeenCalled();
  });

  it('surfaces the server error and does NOT emit on failure', async () => {
    (auth.changePassword as jest.Mock).mockResolvedValue(fail(new AppError('bad', 'auth', 401, undefined, 'Current password is incorrect')));
    const emit = jest.spyOn(cmp.succeeded, 'emit');
    cmp.currentPassword.set('wrong'); cmp.newPassword.set('new12345'); cmp.confirmPassword.set('new12345');
    await cmp.submit();
    expect(cmp.error()).toBe('Current password is incorrect');
    expect(emit).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run the spec to verify it fails**

Run: `npx jest src/app/features/auth/testing/presentation/components/change-password-form --silent`
Expected: FAIL — component does not exist.

- [ ] **Step 3: Create the component**

`change-password-form.component.ts`:
```ts
import { Component, EventEmitter, Input, Output, computed, inject, signal } from '@angular/core';
import { LucideDynamicIcon, LucideLoader2 } from '@lucide/angular';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { TranslatePipe, TranslateService } from '@core/i18n';
import { toUserMessage } from '@core/domain/errors/user-message';

@Component({
  selector: 'app-change-password-form',
  standalone: true,
  imports: [LucideDynamicIcon, TranslatePipe],
  templateUrl: './change-password-form.component.html',
})
export class ChangePasswordForm {
  private readonly auth = inject(AuthSessionStore);
  private readonly i18n = inject(TranslateService);

  @Input() variant: 'full' | 'compact' = 'compact';
  @Output() succeeded = new EventEmitter<void>();

  readonly Loader2Icon = LucideLoader2;

  readonly currentPassword = signal('');
  readonly newPassword = signal('');
  readonly confirmPassword = signal('');
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  // Idle label: forced page uses the changePassword copy; Settings uses the compact "Save".
  readonly submitLabel = computed(() => (this.variant === 'full' ? 'changePassword.submit' : 'profile.security.save'));

  async submit(): Promise<void> {
    this.error.set(null);
    if (this.newPassword() !== this.confirmPassword()) {
      this.error.set(this.i18n.t('changePassword.mismatch'));
      return;
    }
    this.loading.set(true);
    const r = await this.auth.changePassword(this.currentPassword(), this.newPassword());
    this.loading.set(false);
    if (r.ok) {
      this.currentPassword.set('');
      this.newPassword.set('');
      this.confirmPassword.set('');
      this.succeeded.emit();
    } else {
      this.error.set(toUserMessage(r.error));
    }
  }
}
```

- [ ] **Step 4: Create the template**

`change-password-form.component.html` (three `password` inputs using the signal `[value]`/`(input)` pattern; error block; button styled by `variant` — full-width primary vs small teal Save). Match the reference Security card (`ui 2.png`/`ui 3.png`) for the compact variant and the current `/change-password` card for full:
```html
<div class="space-y-4">
  <div class="space-y-1.5">
    <label class="block text-sm font-semibold text-ink">{{ 'changePassword.current' | translate }}</label>
    <input type="password" dir="ltr" autocomplete="current-password"
      [value]="currentPassword()" (input)="currentPassword.set($any($event.target).value)"
      placeholder="••••••••"
      [class]="'rounded-xl border border-border bg-canvas px-4 py-3 text-ink outline-none transition-all focus:border-primary focus:ring-2 focus:ring-primary/30 ' + (variant === 'full' ? 'w-full' : 'w-full sm:w-[55%]')" />
  </div>
  <div class="space-y-1.5">
    <label class="block text-sm font-semibold text-ink">{{ 'changePassword.new' | translate }}</label>
    <input type="password" dir="ltr" autocomplete="new-password"
      [value]="newPassword()" (input)="newPassword.set($any($event.target).value)"
      placeholder="••••••••"
      [class]="'rounded-xl border border-border bg-canvas px-4 py-3 text-ink outline-none transition-all focus:border-primary focus:ring-2 focus:ring-primary/30 ' + (variant === 'full' ? 'w-full' : 'w-full sm:w-[55%]')" />
  </div>
  <div class="space-y-1.5">
    <label class="block text-sm font-semibold text-ink">{{ 'changePassword.confirm' | translate }}</label>
    <input type="password" dir="ltr" autocomplete="new-password"
      [value]="confirmPassword()" (input)="confirmPassword.set($any($event.target).value)"
      placeholder="••••••••"
      [class]="'rounded-xl border border-border bg-canvas px-4 py-3 text-ink outline-none transition-all focus:border-primary focus:ring-2 focus:ring-primary/30 ' + (variant === 'full' ? 'w-full' : 'w-full sm:w-[55%]')" />
  </div>

  @if (error(); as e) {
    <div role="alert" class="rounded-xl border border-danger/40 bg-danger-bg px-4 py-3 text-sm font-medium text-danger">{{ e }}</div>
  }

  <button (click)="submit()" [disabled]="loading()"
    [class]="'flex items-center justify-center gap-2 rounded-xl bg-primary font-bold text-white transition hover:bg-primary-hover disabled:opacity-70 ' + (variant === 'full' ? 'w-full px-4 py-3 text-base' : 'px-5 py-2.5 text-sm')">
    @if (loading()) { <svg [lucideIcon]="Loader2Icon" [size]="18" class="animate-spin" /> }
    {{ submitLabel() | translate }}
  </button>
</div>
```

- [ ] **Step 5: Run the spec to verify it passes**

Run: `npx jest src/app/features/auth/testing/presentation/components/change-password-form --silent`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit** (if committing)
```bash
git add frontend/src/app/features/auth/presentation/components/change-password-form/ frontend/src/app/features/auth/testing/presentation/components/change-password-form/
git commit -m "feat(auth): add reusable ChangePasswordForm component"
```

---

### Task 4: Adopt the form in `/change-password`; retire `ChangePasswordViewModel`

**Files:**
- Modify: `frontend/src/app/features/auth/presentation/pages/change-password/change-password.page.ts`
- Modify: `frontend/src/app/features/auth/presentation/pages/change-password/change-password.page.html`
- Delete: `frontend/src/app/features/auth/presentation/pages/change-password/change-password.viewmodel.ts`
- Delete: `frontend/src/app/features/auth/testing/presentation/pages/change-password/change-password.viewmodel.spec.ts`
- Modify: `frontend/src/app/app.routes.ts` (remove the `ChangePasswordViewModel` route provider)
- Modify: `frontend/src/app/features/auth/index.ts` (remove the `ChangePasswordViewModel` export)

**Interfaces:**
- Consumes: `ChangePasswordForm` (Task 3), `Router`, `LanguageStore`.

- [ ] **Step 1: Rewrite the page component**

`change-password.page.ts`:
```ts
import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ChangePasswordForm } from '@features/auth/presentation/components/change-password-form/change-password-form.component';
import { LanguageStore, TranslatePipe } from '@core/i18n';

@Component({
  selector: 'app-change-password-page',
  standalone: true,
  imports: [ChangePasswordForm, TranslatePipe],
  templateUrl: './change-password.page.html',
})
export class ChangePasswordPage {
  private readonly router = inject(Router);
  private readonly language = inject(LanguageStore);
  protected readonly lang = this.language.lang;

  toggleLanguage(): void { this.language.toggle(); }
  onDone(): void { void this.router.navigate(['/account']); }
}
```

- [ ] **Step 2: Rewrite the page template to host the form (full variant)**

`change-password.page.html` — keep the outer standalone card + language toggle + title/subtitle; replace the inline fields/button with the form:
```html
<main class="relative flex min-h-screen w-full items-center justify-center bg-canvas p-6 text-ink">
  <button (click)="toggleLanguage()" class="absolute end-6 top-6 text-sm font-semibold text-text-secondary hover:text-primary">
    {{ lang() === 'en' ? 'العربية' : 'English' }}
  </button>
  <section class="w-full max-w-md rounded-2xl border border-border bg-surface p-8 shadow-lg">
    <h1 class="font-heading text-2xl text-ink">{{ 'changePassword.title' | translate }}</h1>
    <p class="mt-1 text-sm text-text-secondary">{{ 'changePassword.subtitle' | translate }}</p>
    <div class="mt-8">
      <app-change-password-form variant="full" (succeeded)="onDone()" />
    </div>
  </section>
</main>
```

- [ ] **Step 3: Delete the retired viewmodel + its spec, and drop references**

- Delete `change-password.viewmodel.ts` and `change-password.viewmodel.spec.ts`.
- In `app.routes.ts`: the `change-password` route currently has `providers: [ChangePasswordViewModel]` — remove that `providers` line and the `ChangePasswordViewModel` import.
- In `features/auth/index.ts`: remove the `export { ChangePasswordViewModel } …` line.

- [ ] **Step 4: Verify build + full suite**

Run: `npx ng build --configuration development` then `npx jest --silent`
Expected: build succeeds; suite passes (the deleted VM spec is gone; the new form spec covers the logic). If any import of `ChangePasswordViewModel` remains, the build points to it — remove it.

- [ ] **Step 5: Visual check**

Read `C:\Users\envnt\Desktop\MCP\our code 1.png` for the current `/change-password`; confirm the rebuilt page still shows the three inputs + a full-width primary button and the card is unchanged in layout.

- [ ] **Step 6: Commit** (if committing)
```bash
git add frontend/src/app/features/auth/presentation/pages/change-password/ frontend/src/app/app.routes.ts frontend/src/app/features/auth/index.ts
git rm frontend/src/app/features/auth/testing/presentation/pages/change-password/change-password.viewmodel.spec.ts
git commit -m "refactor(auth): /change-password uses ChangePasswordForm; remove ChangePasswordViewModel"
```

---

### Task 5: Settings section components

**Files:**
- Create: `.../account/sections/profile-section/profile-section.component.ts` + `.html`
- Create: `.../account/sections/preferences-section/preferences-section.component.ts` + `.html`
- Create: `.../account/sections/security-section/security-section.component.ts` + `.html`
- Create: `.../account/sections/about-section/about-section.component.ts` + `.html`
  (base path: `frontend/src/app/features/auth/presentation/pages/account/`)

**Interfaces:**
- `ProfileSection` `@Input()`s: `nameEn/nameAr` (or a resolved `displayName`), `email`, `role: UserRole | null`, `phone`, `age`, `gender`, `nationalId`, `initial`, `loaded: boolean` — mirroring today's `account.page.ts` computed values. Renders avatar + name/email + Role pill + the extra fields grid.
- `PreferencesSection`: no inputs; injects `LanguageStore` + `ThemeStore` (language row + theme row), same handlers as today (`toggle`, `set('light'|'dark')`).
- `SecuritySection`: hosts `<app-change-password-form variant="compact" (succeeded)="onSaved()">`; local `saved = signal(false)` shown as the `profile.security.success` line.
- `AboutSection`: static content from `shell.brand` + `profile.about.version` + `profile.about.description`.

These are presentational; verification is build + visual compare (no new unit tests — the composed page is tested in Task 6, the form logic in Task 3).

- [ ] **Step 1: Create `SecuritySection` (it carries the only logic — the success line)**

`security-section.component.ts`:
```ts
import { Component, signal } from '@angular/core';
import { ChangePasswordForm } from '@features/auth/presentation/components/change-password-form/change-password-form.component';
import { TranslatePipe } from '@core/i18n';
import { LucideDynamicIcon, LucideShield } from '@lucide/angular';

@Component({
  selector: 'app-security-section',
  standalone: true,
  imports: [ChangePasswordForm, TranslatePipe, LucideDynamicIcon],
  templateUrl: './security-section.component.html',
})
export class SecuritySection {
  readonly ShieldIcon = LucideShield;
  readonly saved = signal(false);
  onSaved(): void { this.saved.set(true); }
}
```
`security-section.component.html`:
```html
<section class="rounded-2xl border border-border bg-surface p-6 shadow-warm">
  <h2 class="mb-4 flex items-center gap-2 font-heading text-lg text-ink">
    <svg [lucideIcon]="ShieldIcon" [size]="18" /> {{ 'profile.sections.security' | translate }}
  </h2>
  <app-change-password-form variant="compact" (succeeded)="onSaved()" />
  @if (saved()) {
    <p class="mt-3 text-sm font-medium text-success">{{ 'profile.security.success' | translate }}</p>
  }
</section>
```

- [ ] **Step 2: Create `ProfileSection`, `PreferencesSection`, `AboutSection`**

Port the current `account.page.html` markup for each region into its own component, restyled to the reference (`ui 1.png`):
- **ProfileSection** — take the Profile block (`account.page.html:8–45`): avatar (`bg-primary` circle, `initial`), name (`displayName`), email (`dir="ltr"`), Role pill (use the role-pill recipe from Global Constraints), then the extra fields grid (phone/age/gender/national ID) shown when `loaded`. Card heading `font-heading text-lg`. Inputs replace today's component computeds.
- **PreferencesSection** — take the Preferences block (`account.page.html:48–73`): Language row (label + `Choose display language` subtext + segmented `EN | عربي`) and Theme row (label + `Light or dark mode` subtext + theme toggle). Inject `LanguageStore`/`ThemeStore` directly.
- **AboutSection** — take the About block (`account.page.html:116–121`): heading + `shell.brand` + `profile.about.version` + `profile.about.description`.

Each is a card: `rounded-2xl border border-border bg-surface p-6 shadow-warm` with `font-heading text-lg` heading. Compare spacing/typography to `ui 1.png`/`ui 3.png`.

- [ ] **Step 3: Verify build**

Run: `npx ng build --configuration development`
Expected: build succeeds (components compile; they're wired into the page in Task 6).

- [ ] **Step 4: Commit** (if committing)
```bash
git add frontend/src/app/features/auth/presentation/pages/account/sections/
git commit -m "feat(settings): add profile/preferences/security/about section components"
```

---

### Task 6: Compose `AccountPage` from sections; slim `AccountViewModel`; fix specs

**Files:**
- Modify: `frontend/src/app/features/auth/presentation/pages/account/account.page.ts` + `.html`
- Modify: `frontend/src/app/features/auth/presentation/pages/account/account.viewmodel.ts` (remove password members)
- Modify: `frontend/src/app/features/auth/testing/presentation/pages/account/account.viewmodel.spec.ts` (remove password tests)
- Modify: `frontend/src/app/features/auth/testing/presentation/pages/account/account.page.spec.ts` (adapt to decomposed structure)

**Interfaces:**
- Consumes: the four section components (Task 5).

- [ ] **Step 1: Remove password members from `AccountViewModel`**

In `account.viewmodel.ts`, delete the password signals and `changePassword()` method (lines ~22–27 and ~37–55), plus the now-unused `AuthSessionStore`, `TranslateService`, `toUserMessage` imports/injections. Keep `loading`, `user`, and `init()`. Result:
```ts
import { Injectable, inject, signal } from '@angular/core';
import { LoadCurrentUserUseCase } from '@features/auth/domain/usecases/shared/load-current-user.use-case';
import { NotificationService } from '@core/ui/notification.service';
import { toUserMessage } from '@core/domain/errors/user-message';
import { CurrentUser } from '@features/auth/domain/model/shared/auth';

@Injectable()
export class AccountViewModel {
  private readonly loadCurrentUser = inject(LoadCurrentUserUseCase);
  private readonly notify = inject(NotificationService);

  readonly loading = signal(false);
  readonly user = signal<CurrentUser | null>(null);

  async init(): Promise<void> {
    this.loading.set(true);
    const r = await this.loadCurrentUser.run();
    this.loading.set(false);
    if (r.ok) this.user.set(r.data);
    else this.notify.error(toUserMessage(r.error));
  }
}
```

- [ ] **Step 2: Update `account.viewmodel.spec.ts`**

Remove the three `changePassword()` tests (the `--- changePassword() ---` block, lines ~73–117) and drop the now-unused `auth`/`i18n` mocks + `AuthSessionStore`/`TranslateService`/`AppError`-only-for-pw imports. Keep the two `init()` tests. Run:
`npx jest src/app/features/auth/testing/presentation/pages/account/account.viewmodel.spec --silent` → expect PASS (2 tests).

- [ ] **Step 3: Rewrite `account.page.ts` + `.html` to compose sections**

`account.page.html`:
```html
<div class="mx-auto max-w-3xl">
  <header class="mb-8">
    <h1 class="font-heading text-3xl text-ink">{{ 'profile.title' | translate }}</h1>
    <p class="mt-1 text-text-secondary">{{ 'profile.subtitle' | translate }}</p>
  </header>
  <div class="space-y-6">
    <app-profile-section
      [displayName]="displayName()" [email]="email()" [role]="role()"
      [phone]="phone()" [age]="age()" [genderKey]="genderKey()"
      [nationalId]="nationalId()" [initial]="initial()" [loaded]="!vm.loading()" />
    <app-preferences-section />
    <app-security-section />
    <app-about-section />
  </div>
</div>
```
`account.page.ts` — keep the existing computeds (`displayName/email/role/phone/age/genderKey/nationalId/initial`) and `constructor(){ void this.vm.init(); }`; change `imports` to the four section components (drop the now-unneeded icon imports that moved into sections). Keep `AccountViewModel` injection.

- [ ] **Step 4: Adapt `account.page.spec.ts` to the decomposed page**

The composed page still renders all text through its children, so most assertions hold. Changes:
- The mock `vm` no longer needs password signals — reduce it to `{ loading: signal(false), user: signal(current), init: jest.fn() }`. Provide `AuthSessionStore`, `LanguageStore`, `ThemeStore` as today (children inject them).
- **Remove** the two tests that drove `vm.pwError`/`vm.pwSuccess` (`shows pwError message…`, `shows success message…`) — that behavior now lives in the `ChangePasswordForm` spec (Task 3) and `SecuritySection`.
- Keep: init called, name/email/role/nationalId/notSet/phone/gender/age render, four sections present, **three password inputs** (now rendered by `ChangePasswordForm` inside `SecuritySection`), language toggle label, theme labels. Ensure `AuthSessionStore` mock also exposes `changePassword: jest.fn()` (the form injects it) — it already does.

Run: `npx jest src/app/features/auth/testing/presentation/pages/account/account.page.spec --silent` → expect PASS.

- [ ] **Step 5: Full suite + build**

Run: `npx jest --silent` then `npx ng build --configuration development`
Expected: all pass; build clean.

- [ ] **Step 6: Visual check** — Read `ui 1.png`/`ui 2.png`/`ui 3.png`; confirm the four cards, serif headings, Role pill, half-width Security inputs + small teal Save, centered column.

- [ ] **Step 7: Commit** (if committing)
```bash
git add frontend/src/app/features/auth/presentation/pages/account/ frontend/src/app/features/auth/testing/presentation/pages/account/
git commit -m "refactor(settings): compose AccountPage from section components; slim AccountViewModel"
```

---

### Task 7: Sidebar redesign

**Files:**
- Modify: `frontend/src/app/layout/layout.component.ts` (nav grouping, active-club, user-card data)
- Modify: `frontend/src/app/layout/layout.component.html` (sidebar markup)

**Interfaces:**
- Consumes: `AuthSessionStore` (`currentUserName`, `role`, `signOut`), `ROLE_LABELS`, translations from Task 2, `sidebar` token from Task 1.

- [ ] **Step 1: Restructure nav into groups in `layout.component.ts`**

Replace the flat `allItems` with grouped structure and expose an `activeClub` constant. Add group typing:
```ts
interface NavGroup { header: string; items: NavItem[]; }

private readonly allGroups: NavGroup[] = [
  { header: 'shell.nav.groups.overview', items: [
    { label: 'shell.nav.dashboard', icon: LucideLayoutDashboard },
  ]},
  { header: 'shell.nav.groups.coaching', items: [
    { label: 'shell.nav.swimmers', icon: LucideUsers },
    { label: 'shell.nav.attendance', icon: LucideClipboardCheck },
    { label: 'shell.nav.championships', icon: LucideTrophy },
  ]},
  { header: 'shell.nav.groups.administration', items: [
    { label: 'shell.nav.captainPanel', icon: LucideShieldCheck, roles: ['captain'] },
    { label: 'shell.nav.settings', icon: LucideSettings, route: '/account' },
  ]},
];

readonly navGroups = computed<NavGroup[]>(() => {
  const role = this.auth.role();
  return this.allGroups
    .map((g) => ({ ...g, items: g.items.filter((i) => !i.roles || (role !== null && i.roles.includes(role))) }))
    .filter((g) => g.items.length > 0);
});
```
Keep `userInitial`, `roleLabel`, `isActive`, `signOut`, `toggleLanguage`, `toggleTheme`. Add `readonly currentUserName = this.auth.currentUserName;`.

- [ ] **Step 2: Rebuild the sidebar markup in `layout.component.html`**

Change the `<aside>` background to `bg-sidebar text-sidebar-ink` (from `bg-ink text-surface`). Update the four regions (compare to `ui 1.png`):
- **Brand:** waves icon + `shell.brand` + a subtitle line `shell.brandSubtitle` (smaller, muted).
- **Active-club card:** below the brand, an inset rounded card `rounded-xl bg-white/10 px-3 py-2` with a tiny label `{{ 'shell.activeClub.label' | translate }}` and value `{{ 'shell.activeClub.value' | translate }}`.
- **Grouped nav:** `@for (group of navGroups(); …)` → a small uppercase group header `{{ group.header | translate }}` then `@for (item of group.items; …)` the existing active/route/disabled button logic. Selected (active) item → `bg-white text-primary` pill (white background, teal text) with `shadow-sm`; inactive → `text-sidebar-ink/70 hover:bg-white/10`.
- **Bottom user card:** replace the standalone Sign-out row with a card: initials avatar (`bg-white/15`), `currentUserName()` + `roleLabel() | translate`, and a logout icon button (`LogOutIcon`, `signOut()`) aligned to the end. Keep the desktop collapse toggle button below or beside it.

Keep all existing `isSidebarOpen`/`isMobileMenuOpen` collapse/drawer behavior and `dir`-aware classes.

- [ ] **Step 3: Verify build + suite**

Run: `npx ng build --configuration development` then `npx jest --silent`
Expected: build + tests pass (layout has no spec; ensure nothing referencing the old `navItems` remains — the template must use `navGroups`).

- [ ] **Step 4: Visual check** — Read `ui 1.png`; confirm teal sidebar, brand+subtitle, active-club card, three nav groups with headers, Settings selected as a white/teal pill, bottom user card with logout icon.

- [ ] **Step 5: Commit** (if committing)
```bash
git add frontend/src/app/layout/layout.component.ts frontend/src/app/layout/layout.component.html
git commit -m "feat(shell): teal grouped sidebar with active-club and user card"
```

---

### Task 8: Header redesign

**Files:**
- Modify: `frontend/src/app/layout/layout.component.ts` (breadcrumb helper)
- Modify: `frontend/src/app/layout/layout.component.html` (header markup)

**Interfaces:**
- Consumes: `navGroups`/active route (Task 7), `LanguageStore`, `ThemeStore`.

- [ ] **Step 1: Add a breadcrumb helper in `layout.component.ts`**

Compute the active item's group header + label from `navGroups()` and the current URL:
```ts
readonly breadcrumb = computed(() => {
  const url = this.router.url;
  for (const g of this.navGroups()) {
    const item = g.items.find((i) => i.route && url.startsWith(i.route));
    if (item) return { group: g.header, label: item.label };
  }
  return { group: 'shell.nav.groups.administration', label: 'shell.nav.settings' };
});
```
(Note: `router.url` is not reactive; acceptable here since the shell rebuilds per navigation. If needed, this can later subscribe to router events — out of scope.)

- [ ] **Step 2: Rebuild the header markup**

In `layout.component.html` header (compare `ui 1.png`):
- **Left/breadcrumb:** `{{ 'shell.activeClub.value' | translate }} › {{ breadcrumb().group | translate }}` on one line, `{{ breadcrumb().label | translate }}` bolder below (or inline per the reference).
- **Search:** keep the styled input but make it **inert** — remove `disabled`, remove any binding/handler, set placeholder to `{{ 'shell.searchPlaceholder' | translate }}`. It is decorative (no results).
- **Right controls:** replace the single `AR/EN` text button with the **segmented `EN | عربي`** control (reuse the `login.page.html` switcher block, calling `toggleLanguage()` / `LanguageStore`); keep the theme toggle button; **remove** the circular avatar profile button entirely.

- [ ] **Step 3: Verify build + suite**

Run: `npx ng build --configuration development` then `npx jest --silent`
Expected: pass. Confirm no leftover references to the removed avatar (`userInitial` may still be used by the sidebar user card — keep it; only the header avatar markup is removed).

- [ ] **Step 4: Visual check** — Read `ui 1.png`; confirm breadcrumb, styled search with the new placeholder, segmented `EN | عربي`, theme toggle, and no header avatar.

- [ ] **Step 5: Commit** (if committing)
```bash
git add frontend/src/app/layout/layout.component.ts frontend/src/app/layout/layout.component.html
git commit -m "feat(shell): breadcrumb header with segmented language + inert search"
```

---

### Task 9: Final verification

**Files:** none (verification only)

- [ ] **Step 1: Full suite + AOT build**

Run: `npx jest --silent` then `npx ng build --configuration development`
Expected: all tests pass; build clean.

- [ ] **Step 2: Visual pass against every screenshot**

Run the app (`npx ng serve`; the API can be running or not — Settings needs a session, so log in). Compare each screen to `ui 1.png` (sidebar + header + Profile + Preferences), `ui 2.png` (Preferences + Security), `ui 3.png` (Security + About). Check: teal sidebar with groups + active-club + user card; breadcrumb header; cream dotted background; serif card headings; Role pill; half-width Security inputs + small teal Save; About card. Note any spacing/typography drift and fix in the owning task's files.

- [ ] **Step 3: Regression sweep** — Visit Dashboard (and any other route) to confirm the shared shell changes didn't break other pages; confirm `/change-password` still forces on first login and navigates to `/account` on success (forced-first-login behavior preserved).

---

## Self-Review

**Spec coverage:**
- Theme tokens (teal sidebar, reuse cream/primary/serif/grain) → Task 1. ✔
- Sidebar (teal, brand+subtitle, active-club, grouped nav, user card, logout) → Task 7. ✔
- Header (breadcrumb, inert search, segmented EN|عربي, theme, avatar removed) → Task 8. ✔
- Settings decomposition (profile/preferences/security/about) → Tasks 5–6. ✔
- Keep extra Profile fields → Task 5 (ProfileSection) + Task 6. ✔
- Reusable ChangePasswordForm (`variant`, `succeeded`), used by both routes; forced-login preserved → Tasks 3, 4, 5. ✔
- Remove duplicated password logic (AccountViewModel + ChangePasswordViewModel) → Tasks 4, 6. ✔
- i18n keys → Task 2. ✔
- Testing (form spec; updated account viewmodel/page specs; deleted VM spec) → Tasks 3, 4, 6. ✔

**Placeholder scan:** Logic pieces (form, viewmodel, specs, tokens, i18n) have full literal code. Visual template tasks give concrete structure + Tailwind-class recipes + the exact screenshot to match — the appropriate spec for pixel-fidelity work where the acceptance test is the screenshot, not a unit test. No "TBD"/"similar to Task N".

**Type consistency:** `ChangePasswordForm` (`variant: 'full'|'compact'`, `succeeded` output, `currentPassword/newPassword/confirmPassword/loading/error` signals, `submit()`) is used identically in Tasks 4 (page, `variant="full"`) and 5 (`SecuritySection`, `variant="compact"`). `AccountViewModel` reduced to `loading/user/init()` in Task 6 and its spec (Task 6 Step 2) + page spec (Step 4) match. `navGroups`/`breadcrumb` defined in Task 7/8 and consumed in the same templates. Mismatch key `changePassword.mismatch` consistent across form + spec.

**Known deferrals (spec non-goals):** search is inert (no backend), active-club is a placeholder string, no new routes, router-url breadcrumb is non-reactive-but-acceptable.
