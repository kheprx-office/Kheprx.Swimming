# Swimming Screens — Login + Settings/Profile + Routing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the restyled login screen and the role-aware Settings/Profile screen (Profile + Preferences + inline Security + About) on top of the Plan 1 API and Plan 2 foundation, wire the forced first-login flow, and sync the frontend auth contract to the enriched profile shape.

**Architecture:** Angular clean-architecture auth feature (data DTO → domain model/use-case → presentation viewmodel/page). The login and account pages are restyled and the account page becomes a single Settings page. Language/theme come from the Plan 2 stores; profile display name follows the active language (`name_en`/`name_ar`).

**Tech Stack:** Angular 20 (standalone + signals), Tailwind 3.4 (logical `ps/pe/start/end` utilities for RTL), Jest 30, `@lucide/angular`.

**Spec:** `docs/superpowers/specs/2026-08-31-swimming-auth-profile-design.md` (§6, §7)

**Depends on:** Plan 1 (backend `/api/auth/*` + enriched `CurrentUserDto`) and Plan 2 (i18n, theme, palette, roles, shell).

## Global Constraints

- `UserRole = 'head_coach' | 'captain'` (from Plan 2). No `admin` role exists.
- Login is email + password only — no demo role picker, no demo-credentials box.
- Forced first-login: `mustChangePassword` (from the session) routes to `/change-password` and blocks the shell until changed.
- All user-facing strings via the `translate` pipe / `TranslateService`; new keys go under `login.*` and `profile.*` in `en.json`/`ar.json`.
- Use logical Tailwind utilities (`ps-*`, `pe-*`, `start-*`, `end-*`, `ms-*`, `me-*`) so RTL mirrors automatically; do not hardcode `left`/`right` on flow-relative spacing.
- Tests follow the existing `testing/`-mirror Jest convention.
- Do **not** `git commit` until the user says so (stage only).

---

### Task 1: Enrich the auth profile contract (DTO + model + store)

**Files:**
- Modify: `frontend/src/app/features/auth/data/dto/shared/current-user.dto.ts`
- Modify: `frontend/src/app/features/auth/domain/model/shared/auth.ts` (model + `toCurrentUser`)
- Modify: `frontend/src/app/features/auth/presentation/auth-session.store.ts` (display name by language)
- Modify: `frontend/src/app/features/auth/presentation/pages/login/login.viewmodel.ts` (swimming landing map)
- Test: `.../testing/data/dto/shared/current-user.dto.spec.ts`, `.../testing/domain/model/shared/auth.spec.ts`, `.../testing/presentation/auth-session.store.spec.ts`

**Interfaces:**
- Produces:
  - `CurrentUserDtoRs { userId; email; nameEn; nameAr: string | null; role; phone: string | null; gender: string | null; age: number | null; nationalId: string | null }`.
  - `CurrentUser { userId; email; nameEn; nameAr: string | null; role: UserRole; phone; gender: Gender | null; age; nationalId: string | null }`.
  - `AuthSessionStore.displayName: Signal<string>` (language-aware), `currentUserName` unchanged in name but backed by `displayName`.

- [ ] **Step 1: Update the DTO + validation**

In `current-user.dto.ts`, replace `fullName` with `nameEn` + `nameAr` and add `nationalId`:
```ts
export interface CurrentUserDtoRs {
  userId: string;
  email: string;
  nameEn: string;
  nameAr: string | null;
  role: string;
  phone: string | null;
  gender: string | null;
  age: number | null;
  nationalId: string | null;
}
```
In `isCurrentUserDtoRsValid`, replace the `fullName` check with `typeof dto.nameEn === 'string'`, add `isNullableString(dto.nameAr)` and `isNullableString(dto.nationalId)`. The role check (`hasOwnProperty(ROLE_LABELS, dto.role)`) now validates `head_coach`/`captain` automatically.

- [ ] **Step 2: Update the domain model + mapper**

In `auth.ts`, change `CurrentUser` and `toCurrentUser`:
```ts
export interface CurrentUser {
  userId: string; email: string; nameEn: string; nameAr: string | null;
  role: UserRole; phone: string | null; gender: Gender | null; age: number | null;
  nationalId: string | null;
}

export function toCurrentUser(dto: CurrentUserDtoRs): CurrentUser {
  return {
    userId: dto.userId, email: dto.email, nameEn: dto.nameEn, nameAr: dto.nameAr,
    role: dto.role as UserRole, phone: dto.phone, gender: dto.gender as Gender | null,
    age: dto.age, nationalId: dto.nationalId,
  };
}
```

- [ ] **Step 3: Update `AuthSessionStore` display name (language-aware)**

Inject `LanguageStore`; replace `_fullName` with name parts:
```ts
private readonly language = inject(LanguageStore);
private readonly _nameEn = signal<string | null>(null);
private readonly _nameAr = signal<string | null>(null);
readonly displayName = computed<string>(() => {
  const en = this._nameEn(); const ar = this._nameAr();
  return this.language.lang() === 'ar' ? (ar ?? en ?? '') : (en ?? '');
});
readonly currentUserName = computed<string>(() => this.displayName() || this.principal()?.userId || '');
```
In `rehydrate()` and `refreshProfileName()`, set `this._nameEn.set(r.data.nameEn); this._nameAr.set(r.data.nameAr);` (instead of `_fullName`). In `signOut()`, reset both to `null`.

- [ ] **Step 4: Update the login landing map**

In `login.viewmodel.ts`, replace the Electric record:
```ts
const LANDING_ROUTE_BY_ROLE: Record<UserRole, string> = {
  head_coach: '/home',
  captain: '/home',
};
```
(The `mustChangePassword → /change-password` branch already exists — leave it.)

- [ ] **Step 5: Update tests**

- `current-user.dto.spec.ts`: build a valid DTO with `nameEn/nameAr/nationalId`, role `'captain'`; assert valid; assert invalid when `role` unknown or `nameEn` missing.
- `auth.spec.ts`: assert `toCurrentUser` maps `nameEn/nameAr/nationalId`.
- `auth-session.store.spec.ts`: after a mocked `loadCurrentUser` returning `{nameEn:'Dave', nameAr:'ديف', role:'captain', ...}`, assert `displayName()` is `'Dave'` in en and `'ديف'` after `LanguageStore.set('ar')`.

- [ ] **Step 6: Run + build** — `cd frontend && npx jest features/auth` → PASS; `npm run build` → builds.
- [ ] **Step 7: Stage** (hold commit). Message: `feat(auth): enrich profile contract (name_en/name_ar/national_id) + language-aware display name`.

---

### Task 2: Restyle the login screen

**Files:**
- Modify: `frontend/src/app/features/auth/presentation/pages/login/login.page.html`
- Modify: `frontend/src/app/features/auth/presentation/pages/login/login.page.ts`
- Modify: `frontend/src/app/core/i18n/en.json`, `ar.json` (add `login.*`)
- Modify: `.../testing/presentation/pages/login/login.viewmodel.spec.ts` (only if landing assertions reference old roles)

- [ ] **Step 1: Add `login.*` dictionary keys**

`en.json` → add:
```json
"login": {
  "heroTitle": "Every swimmer, every session, every result — in one place.",
  "heroSubtitle": "Attendance, measurements, medical records and championships, under one roof.",
  "welcome": "Welcome back",
  "subtitle": "Sign in to your academy portal.",
  "email": "Email address",
  "password": "Password",
  "signIn": "Sign in",
  "signingIn": "Signing in…"
}
```
`ar.json` → add:
```json
"login": {
  "heroTitle": "كل سبّاح، كل حصة، كل نتيجة — في مكان واحد.",
  "heroSubtitle": "الحضور والقياسات والسجلات الطبية والبطولات، تحت سقف واحد.",
  "welcome": "مرحباً بعودتك",
  "subtitle": "سجّل الدخول إلى بوابة الأكاديمية.",
  "email": "البريد الإلكتروني",
  "password": "كلمة المرور",
  "signIn": "تسجيل الدخول",
  "signingIn": "جارٍ التحقق…"
}
```

- [ ] **Step 2: Rewrite `login.page.ts`**

```ts
import { Component, inject } from '@angular/core';
import { LucideDynamicIcon, LucideWaves, LucideMail, LucideLock, LucideLogIn, LucideLoader2, LucideAlertCircle } from '@lucide/angular';
import { LoginViewModel } from './login.viewmodel';
import { LanguageStore, TranslatePipe } from '@core/i18n';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [LucideDynamicIcon, TranslatePipe],
  templateUrl: './login.page.html',
})
export class LoginPage {
  protected readonly vm = inject(LoginViewModel);
  private readonly language = inject(LanguageStore);
  protected readonly lang = this.language.lang;

  protected readonly WavesIcon = LucideWaves;
  protected readonly MailIcon = LucideMail;
  protected readonly LockIcon = LucideLock;
  protected readonly LogInIcon = LucideLogIn;
  protected readonly Loader2Icon = LucideLoader2;
  protected readonly AlertCircleIcon = LucideAlertCircle;

  toggleLanguage(): void { this.language.toggle(); }
  onKeyDown(e: KeyboardEvent): void { if (e.key === 'Enter') void this.vm.submit(); }
}
```

- [ ] **Step 3: Rewrite `login.page.html`** (two-panel Oasis layout, i18n, logical RTL utilities)

```html
<main class="relative flex min-h-screen w-full bg-canvas text-ink">
  <section class="relative hidden w-1/2 flex-col justify-between bg-primary p-12 text-white lg:flex">
    <div class="flex items-center gap-3">
      <div class="flex h-11 w-11 items-center justify-center rounded-2xl bg-white/15">
        <svg [lucideIcon]="WavesIcon" [size]="24" />
      </div>
      <span class="text-lg font-semibold">{{ 'shell.brand' | translate }}</span>
    </div>
    <div>
      <h1 class="max-w-md text-4xl font-extrabold leading-tight">{{ 'login.heroTitle' | translate }}</h1>
      <p class="mt-4 max-w-md text-white/70">{{ 'login.heroSubtitle' | translate }}</p>
    </div>
    <p class="text-sm text-white/50">© 2026 {{ 'shell.brand' | translate }}</p>
  </section>

  <section class="relative flex w-full flex-col items-center justify-center p-6 lg:w-1/2">
    <button (click)="toggleLanguage()" class="absolute end-6 top-6 text-sm font-semibold text-text-secondary hover:text-primary">
      {{ lang() === 'en' ? 'العربية' : 'English' }}
    </button>

    <div class="w-full max-w-sm" [class.animate-shake]="vm.shakeKey() % 2 === 1">
      <h2 class="text-2xl font-bold">{{ 'login.welcome' | translate }}</h2>
      <p class="mt-1 text-sm text-text-secondary">{{ 'login.subtitle' | translate }}</p>

      <div class="mt-8 space-y-4">
        <div class="space-y-1.5">
          <label for="email" class="block text-sm font-semibold">{{ 'login.email' | translate }}</label>
          <div class="relative">
            <svg [lucideIcon]="MailIcon" [size]="18" class="pointer-events-none absolute inset-y-0 my-auto start-3 text-text-muted" />
            <input id="email" type="email" dir="ltr"
              [value]="vm.email()" (input)="vm.email.set($any($event.target).value)" (keydown)="onKeyDown($event)"
              class="w-full rounded-xl border border-border bg-surface py-3 ps-10 pe-3 outline-none focus:border-primary focus:ring-2 focus:ring-primary/30"
              placeholder="you@example.com" />
          </div>
        </div>

        <div class="space-y-1.5">
          <label for="password" class="block text-sm font-semibold">{{ 'login.password' | translate }}</label>
          <div class="relative">
            <svg [lucideIcon]="LockIcon" [size]="18" class="pointer-events-none absolute inset-y-0 my-auto start-3 text-text-muted" />
            <input id="password" type="password" dir="ltr"
              [value]="vm.password()" (input)="vm.password.set($any($event.target).value)" (keydown)="onKeyDown($event)"
              class="w-full rounded-xl border border-border bg-surface py-3 ps-10 pe-3 outline-none focus:border-primary focus:ring-2 focus:ring-primary/30"
              placeholder="••••••••" />
          </div>
        </div>

        @if (vm.error()) {
          <div role="alert" class="flex items-center gap-2 rounded-xl border border-danger/40 bg-danger-bg px-4 py-3 text-sm font-medium text-danger">
            <svg [lucideIcon]="AlertCircleIcon" [size]="16" class="shrink-0" />
            {{ vm.error() }}
          </div>
        }
      </div>

      <button (click)="vm.submit()" [disabled]="vm.loading()"
        class="mt-6 flex w-full items-center justify-center gap-2 rounded-xl bg-primary px-4 py-3 font-bold text-white transition hover:bg-primary-hover disabled:opacity-70 active:scale-[0.98]">
        @if (vm.loading()) { <svg [lucideIcon]="Loader2Icon" [size]="20" class="animate-spin" /> }
        @else { <svg [lucideIcon]="LogInIcon" [size]="20" /> }
        {{ vm.loading() ? ('login.signingIn' | translate) : ('login.signIn' | translate) }}
      </button>
    </div>
  </section>
</main>
```

- [ ] **Step 4: Build + verify** — `npm run build`; `npx jest features/auth/testing/presentation/pages/login` → PASS. Manually: login renders two-panel English, toggles to Arabic RTL, no demo box.
- [ ] **Step 5: Stage** (hold commit). Message: `feat(login): Oasis two-panel restyle, bilingual, teal palette`.

---

### Task 3: Build the Settings/Profile screen

**Files:**
- Modify: `frontend/src/app/features/auth/presentation/pages/account/account.viewmodel.ts`
- Modify: `frontend/src/app/features/auth/presentation/pages/account/account.page.ts`
- Modify: `frontend/src/app/features/auth/presentation/pages/account/account.page.html`
- Modify: `frontend/src/app/core/i18n/en.json`, `ar.json` (add `profile.*`)
- Modify: `.../testing/presentation/pages/account/account.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `LoadCurrentUserUseCase`, `AuthSessionStore.changePassword`, `LanguageStore`, `ThemeStore`, `TranslateService`, `ROLE_LABELS`, `GENDER_LABELS`, `CurrentUser`.
- Produces: an `AccountViewModel` with profile signal + inline change-password form; a Settings page with four sections.

- [ ] **Step 1: Add `profile.*` dictionary keys**

`en.json` → add:
```json
"profile": {
  "title": "Settings",
  "subtitle": "Manage your account and preferences",
  "sections": { "profile": "Profile", "preferences": "Preferences", "security": "Security", "about": "About" },
  "fields": { "name": "Full name", "email": "Email", "phone": "Phone", "gender": "Gender", "age": "Age", "role": "Role", "nationalId": "National ID", "notSet": "Not set", "readonly": "Read-only" },
  "preferences": { "language": "Language", "theme": "Theme", "light": "Light", "dark": "Dark" },
  "security": { "current": "Current password", "new": "New password", "confirm": "Confirm password", "save": "Update password", "mismatch": "Passwords do not match", "success": "Password updated" },
  "about": { "version": "Version 1.0.0", "description": "A comprehensive management system for swimming academies with full bilingual EN/AR support." }
}
```
`ar.json` → add the Arabic mirror (same keys; e.g. `"title": "الإعدادات"`, `"sections": { "profile": "الملف الشخصي", "preferences": "التفضيلات", "security": "الأمان", "about": "حول" }`, `"security.mismatch": "كلمتا المرور غير متطابقتين"`, `"security.success": "تم تحديث كلمة المرور"`, etc.).

- [ ] **Step 2: Extend `account.viewmodel.ts`**

```ts
import { Injectable, inject, signal } from '@angular/core';
import { LoadCurrentUserUseCase } from '@features/auth/domain/usecases/shared/load-current-user.use-case';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { NotificationService } from '@core/ui/notification.service';
import { TranslateService } from '@core/i18n';
import { toUserMessage } from '@core/domain/errors/user-message';
import { CurrentUser } from '@features/auth/domain/model/shared/auth';

@Injectable()
export class AccountViewModel {
  private readonly loadCurrentUser = inject(LoadCurrentUserUseCase);
  private readonly auth = inject(AuthSessionStore);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslateService);

  readonly loading = signal(false);
  readonly user = signal<CurrentUser | null>(null);

  readonly currentPassword = signal('');
  readonly newPassword = signal('');
  readonly confirmPassword = signal('');
  readonly pwLoading = signal(false);
  readonly pwError = signal<string | null>(null);
  readonly pwSuccess = signal(false);

  async init(): Promise<void> {
    this.loading.set(true);
    const r = await this.loadCurrentUser.run();
    this.loading.set(false);
    if (r.ok) this.user.set(r.data);
    else this.notify.error(toUserMessage(r.error));
  }

  async changePassword(): Promise<void> {
    this.pwError.set(null); this.pwSuccess.set(false);
    if (this.newPassword() !== this.confirmPassword()) {
      this.pwError.set(this.i18n.t('profile.security.mismatch')); return;
    }
    this.pwLoading.set(true);
    const r = await this.auth.changePassword(this.currentPassword(), this.newPassword());
    this.pwLoading.set(false);
    if (r.ok) {
      this.pwSuccess.set(true);
      this.currentPassword.set(''); this.newPassword.set(''); this.confirmPassword.set('');
    } else {
      this.pwError.set(toUserMessage(r.error));
    }
  }
}
```

- [ ] **Step 3: Rewrite `account.page.ts`**

```ts
import { Component, computed, inject } from '@angular/core';
import { LucideDynamicIcon, LucideUser, LucideMail, LucidePhone, LucideUsers, LucideCalendar, LucideShield, LucideIdCard, LucideSun, LucideMoon, LucideLanguages } from '@lucide/angular';
import { AccountViewModel } from './account.viewmodel';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { LanguageStore, TranslatePipe } from '@core/i18n';
import { ThemeStore } from '@core/ui/theme/theme.store';
import { ROLE_LABELS } from '@core/domain/roles';
import { GENDER_LABELS } from '@core/domain/gender';

@Component({
  selector: 'app-account-page',
  standalone: true,
  imports: [LucideDynamicIcon, TranslatePipe],
  templateUrl: './account.page.html',
})
export class AccountPage {
  protected readonly vm = inject(AccountViewModel);
  private readonly auth = inject(AuthSessionStore);
  private readonly language = inject(LanguageStore);
  private readonly theme = inject(ThemeStore);

  protected readonly ROLE_LABELS = ROLE_LABELS;
  protected readonly GENDER_LABELS = GENDER_LABELS;
  protected readonly lang = this.language.lang;
  protected readonly mode = this.theme.mode;

  protected readonly UserIcon = LucideUser; protected readonly MailIcon = LucideMail;
  protected readonly PhoneIcon = LucidePhone; protected readonly UsersIcon = LucideUsers;
  protected readonly CalendarIcon = LucideCalendar; protected readonly ShieldIcon = LucideShield;
  protected readonly IdIcon = LucideIdCard; protected readonly SunIcon = LucideSun;
  protected readonly MoonIcon = LucideMoon; protected readonly LangIcon = LucideLanguages;

  protected readonly displayName = computed(() => {
    const u = this.vm.user(); if (!u) return this.auth.displayName();
    return this.lang() === 'ar' ? (u.nameAr ?? u.nameEn) : u.nameEn;
  });
  protected readonly email = computed(() => this.vm.user()?.email ?? '');
  protected readonly role = computed(() => this.vm.user()?.role ?? this.auth.principal()?.role ?? null);
  protected readonly phone = computed(() => this.vm.user()?.phone ?? null);
  protected readonly age = computed(() => this.vm.user()?.age ?? null);
  protected readonly nationalId = computed(() => this.vm.user()?.nationalId ?? null);
  protected readonly genderKey = computed(() => { const g = this.vm.user()?.gender; return g ? GENDER_LABELS[g] : null; });
  protected readonly initial = computed(() => this.displayName().trim().slice(0, 1) || '?');

  toggleLanguage(): void { this.language.toggle(); }
  setTheme(mode: 'light' | 'dark'): void { this.theme.set(mode); }

  constructor() { void this.vm.init(); }
}
```

- [ ] **Step 4: Rewrite `account.page.html`** (four sections; read-only profile + preferences + inline security + about)

```html
<div class="mx-auto max-w-2xl">
  <header class="mb-8">
    <h1 class="text-3xl font-extrabold text-ink">{{ 'profile.title' | translate }}</h1>
    <p class="mt-1 text-text-secondary">{{ 'profile.subtitle' | translate }}</p>
  </header>

  <!-- Profile -->
  <section class="mb-6 rounded-2xl border border-border bg-surface p-6 shadow-sm">
    <div class="mb-6 flex items-center gap-4">
      <div class="flex h-16 w-16 items-center justify-center rounded-full bg-primary text-2xl font-bold text-white">{{ initial() }}</div>
      <div>
        <p class="text-lg font-bold text-ink">{{ displayName() || ('profile.fields.notSet' | translate) }}</p>
        <p class="text-sm text-text-secondary" dir="ltr">{{ email() }}</p>
        @if (role(); as r) {
          <span class="mt-1 inline-flex items-center gap-1 rounded-full bg-primary/10 px-3 py-1 text-xs font-semibold text-primary">
            <svg [lucideIcon]="ShieldIcon" [size]="12" /> {{ ROLE_LABELS[r] | translate }}
          </span>
        }
      </div>
    </div>
    @if (!vm.loading()) {
      <dl class="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div><dt class="text-xs font-semibold text-text-secondary">{{ 'profile.fields.phone' | translate }}</dt>
          <dd class="mt-1 text-sm text-ink" dir="ltr">{{ phone() || ('profile.fields.notSet' | translate) }}</dd></div>
        <div><dt class="text-xs font-semibold text-text-secondary">{{ 'profile.fields.age' | translate }}</dt>
          <dd class="mt-1 text-sm text-ink">{{ age() ?? ('profile.fields.notSet' | translate) }}</dd></div>
        <div><dt class="text-xs font-semibold text-text-secondary">{{ 'profile.fields.gender' | translate }}</dt>
          <dd class="mt-1 text-sm text-ink">{{ genderKey() ? (genderKey()! | translate) : ('profile.fields.notSet' | translate) }}</dd></div>
        <div><dt class="text-xs font-semibold text-text-secondary">{{ 'profile.fields.nationalId' | translate }}</dt>
          <dd class="mt-1 text-sm text-ink" dir="ltr">{{ nationalId() || ('profile.fields.notSet' | translate) }}</dd></div>
      </dl>
    }
  </section>

  <!-- Preferences -->
  <section class="mb-6 rounded-2xl border border-border bg-surface p-6 shadow-sm">
    <h2 class="mb-4 text-lg font-bold text-ink">{{ 'profile.sections.preferences' | translate }}</h2>
    <div class="flex items-center justify-between border-b border-border/60 py-3">
      <span class="flex items-center gap-2 text-sm font-medium text-ink"><svg [lucideIcon]="LangIcon" [size]="16" /> {{ 'profile.preferences.language' | translate }}</span>
      <button (click)="toggleLanguage()" class="rounded-lg border border-border px-3 py-1.5 text-sm font-semibold text-primary hover:bg-canvas">
        {{ lang() === 'en' ? 'English' : 'العربية' }}
      </button>
    </div>
    <div class="flex items-center justify-between py-3">
      <span class="flex items-center gap-2 text-sm font-medium text-ink"><svg [lucideIcon]="mode() === 'dark' ? MoonIcon : SunIcon" [size]="16" /> {{ 'profile.preferences.theme' | translate }}</span>
      <div class="flex gap-1 rounded-lg border border-border p-0.5">
        <button (click)="setTheme('light')" [class]="'rounded-md px-3 py-1 text-sm ' + (mode() === 'light' ? 'bg-primary text-white' : 'text-text-secondary')">{{ 'profile.preferences.light' | translate }}</button>
        <button (click)="setTheme('dark')" [class]="'rounded-md px-3 py-1 text-sm ' + (mode() === 'dark' ? 'bg-primary text-white' : 'text-text-secondary')">{{ 'profile.preferences.dark' | translate }}</button>
      </div>
    </div>
  </section>

  <!-- Security -->
  <section class="mb-6 rounded-2xl border border-border bg-surface p-6 shadow-sm">
    <h2 class="mb-4 text-lg font-bold text-ink">{{ 'profile.sections.security' | translate }}</h2>
    <div class="space-y-3">
      <input type="password" dir="ltr" [value]="vm.currentPassword()" (input)="vm.currentPassword.set($any($event.target).value)"
        [placeholder]="'profile.security.current' | translate" class="w-full rounded-lg border border-border bg-canvas px-3 py-2.5 text-sm outline-none focus:border-primary" />
      <input type="password" dir="ltr" [value]="vm.newPassword()" (input)="vm.newPassword.set($any($event.target).value)"
        [placeholder]="'profile.security.new' | translate" class="w-full rounded-lg border border-border bg-canvas px-3 py-2.5 text-sm outline-none focus:border-primary" />
      <input type="password" dir="ltr" [value]="vm.confirmPassword()" (input)="vm.confirmPassword.set($any($event.target).value)"
        [placeholder]="'profile.security.confirm' | translate" class="w-full rounded-lg border border-border bg-canvas px-3 py-2.5 text-sm outline-none focus:border-primary" />
      @if (vm.pwError()) { <p class="text-sm font-medium text-danger">{{ vm.pwError() }}</p> }
      @if (vm.pwSuccess()) { <p class="text-sm font-medium text-success">{{ 'profile.security.success' | translate }}</p> }
      <button (click)="vm.changePassword()" [disabled]="vm.pwLoading()"
        class="rounded-xl bg-primary px-4 py-2.5 text-sm font-bold text-white hover:bg-primary-hover disabled:opacity-70">
        {{ 'profile.security.save' | translate }}
      </button>
    </div>
  </section>

  <!-- About -->
  <section class="rounded-2xl border border-border bg-surface p-6 shadow-sm">
    <h2 class="mb-2 text-lg font-bold text-ink">{{ 'profile.sections.about' | translate }}</h2>
    <p class="text-sm font-semibold text-ink">{{ 'shell.brand' | translate }}</p>
    <p class="text-xs text-text-secondary">{{ 'profile.about.version' | translate }}</p>
    <p class="mt-2 text-sm text-text-secondary">{{ 'profile.about.description' | translate }}</p>
  </section>
</div>
```

- [ ] **Step 5: Update the viewmodel spec**

In `account.viewmodel.spec.ts`: mock `LoadCurrentUserUseCase` to return a `CurrentUser` with `nameEn/nationalId`; assert `init()` sets `user()`. Add a test: `newPassword`≠`confirmPassword` → `changePassword()` sets `pwError` (the resolved mismatch string) without calling `AuthSessionStore.changePassword` (mock it and assert not called). Add a success test: matching passwords + `auth.changePassword` returns `{ok:true}` → `pwSuccess()` true and fields cleared.

- [ ] **Step 6: Run + build** — `npx jest features/auth/testing/presentation/pages/account` → PASS; `npm run build` → builds. Manually: Settings shows the four sections; language + theme toggles work live; changing password shows success.
- [ ] **Step 7: Stage** (hold commit). Message: `feat(profile): Settings screen (profile, preferences, inline security, about), bilingual`.

---

### Task 4: Routing + guards (first-login + swimming roles)

**Files:**
- Modify: `frontend/src/app/features/auth/presentation/auth.guard.ts` (add `firstLoginGuard`)
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `.../testing/presentation/auth.guard.spec.ts`

- [ ] **Step 1: Add the first-login guard**

Append to `auth.guard.ts`:
```ts
// firstLoginGuard: an authenticated user still flagged mustChangePassword is pinned to
// /change-password until they change it. Applied to shell children EXCEPT /change-password.
export const firstLoginGuard: CanActivateFn = () => {
  const auth = inject(AuthSessionStore);
  const router = inject(Router);
  if (auth.mustChangePassword()) { router.navigate(['/change-password']); return false; }
  return true;
};
```

- [ ] **Step 2: Update `app.routes.ts`**

Apply `firstLoginGuard` to `home`, `account`, and `user-management` (not `change-password`), and change the user-management role guard from the removed `'admin'` to `'head_coach'`:
```ts
{ path: 'home', canActivate: [firstLoginGuard], loadComponent: () => import('@features/home').then((m) => m.HomePage) },
{ path: 'account', canActivate: [firstLoginGuard], loadComponent: () => import('@features/auth').then((m) => m.AccountPage), providers: [AccountViewModel] },
{ path: 'change-password', loadComponent: () => import('@features/auth').then((m) => m.ChangePasswordPage), providers: [ChangePasswordViewModel] },
{ path: 'user-management', canActivate: [firstLoginGuard, roleGuard('head_coach')], loadComponent: () => import('@features/user-management').then((m) => m.UserManagementPage), providers: [UsersViewModel] },
```
(Import `firstLoginGuard` alongside `authGuard`/`roleGuard`.)

- [ ] **Step 3: Test the guard**

In `auth.guard.spec.ts`, add: with `AuthSessionStore.mustChangePassword` returning `true`, `firstLoginGuard` returns `false` and navigates to `/change-password`; with `false`, returns `true`.

- [ ] **Step 4: Run + build** — `npx jest features/auth/testing/presentation/auth.guard` → PASS; `npm run build` → builds. Manually: logging in as the seeded head-coach (`is_first_login`) lands on `/change-password` and navigating to `/account` bounces back until the password is changed.
- [ ] **Step 5: Stage** (hold commit). Message: `feat(auth): forced first-login guard + swimming role routing`.

---

### Task 5: Sync the generated contract + full end-to-end verification

**Files:**
- Modify: `contracts/generated/frontend-api-client/*` (regenerate or hand-sync)
- Verify: whole app builds + tests green against the running API.

- [ ] **Step 1: Regenerate the client (or hand-sync)**

Inspect `contracts/` for the generation entry point (a `package.json` script, `openapi`/`nswag`/`kiota` config, or README). If runnable: start the API (`dotnet run --project backend/Kheprx.BaseBackend.Api`) and run the documented generation command so the client picks up the enriched `CurrentUserDto` (`nameEn`/`nameAr`/`nationalId`) and swimming roles. If the generator is not runnable in this environment, the hand-edited DTOs in Task 1 are the source of truth — note the manual step in `docs/STARTER.md` (per the scaffold's fallback convention).

- [ ] **Step 2: Full frontend build + test**

Run: `cd frontend && npm run build && npx jest`
Expected: build succeeds; all suites green (auth, layout, core/i18n, core/ui/theme, user-management).

- [ ] **Step 3: Manual end-to-end (API from Plan 1 running with Swimming_Production)**

1. `npm start`; open the app → login is English LTR, teal/cream, no demo box.
2. Log in as `captain@kheprx.local / Passw0rd!` → lands on `/home`; shell shows Settings enabled, other nav disabled.
3. Open Settings → profile shows name/email/role/phone/gender/age/national_id; toggle language → whole shell + Settings flip to Arabic RTL live; toggle theme → dark palette; both persist on reload.
4. Change the password inline → success message.
5. Log out; log in as `headcoach@kheprx.local / Passw0rd!` (`is_first_login`) → forced to `/change-password`; `/account` bounces back until changed; after change → reaches Settings, role badge shows Head Coach.

- [ ] **Step 4: Stage** (hold commit). Message: `chore(contracts): sync frontend auth client to swimming profile shape`.

## Self-Review Notes (author)

- **Spec coverage:** §6.1 login → Task 2; §6.2 Settings → Task 3; §6.3 shell → Plan 2 Task 6; §6.4 routing/first-login → Task 4; §7 contracts → Task 5; profile contract (name_en/name_ar/national_id) → Task 1.
- **RTL:** logical utilities (`ps/pe/start/end`) + `<html dir>` from `LanguageStore` give automatic mirroring; only the always-LTR inputs (email/password/phone/national_id) pin `dir="ltr"`.
- **user-management:** its route stays (guarded `head_coach`) and compiles against the trimmed backend from Plan 1; its UI is intentionally not restyled in this slice.
- **Cross-plan ordering:** run Plan 1 → Plan 2 → Plan 3. Task 5's manual e2e requires the Plan 1 API pointed at `Swimming_Production` and the seeded users.
