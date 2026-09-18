# Swimming Frontend Foundation — i18n + Theme + Palette + Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the Angular app the infrastructure the swimming mockups need — a working EN/AR language toggle with live LTR↔RTL flip, a light/dark theme toggle, the Oasis teal/cream palette, the swimming role set, and a rebranded minimal shell — so the screens in Plan 3 can be built against it.

**Architecture:** Signal-based `core/` services (`LanguageStore`, `TranslateService`, `ThemeStore`) with no new runtime dependency. Translations live in per-language JSON dictionaries resolved by dot-path; an impure `translate` pipe keeps templates reactive. Theme-aware design tokens are CSS variables swapped under a `.dark` class, so existing semantic Tailwind tokens (`bg-canvas`, `text-ink`, `bg-primary`…) become theme-aware without touching component markup.

**Tech Stack:** Angular 20 (standalone + signals), Tailwind 3.4 (class dark mode), Jest 30 + jest-preset-angular.

**Spec:** `docs/superpowers/specs/2026-08-31-swimming-auth-profile-design.md` (§5, §6.3)

## Global Constraints

- Angular 20, standalone components, signals; path aliases `@core/*`, `@features/*`.
- Roles for this slice: **`head_coach`**, **`captain`** only. `UserRole` must be exactly these two.
- Persisted preference keys: language `kheprx.lang` (`en`|`ar`), theme `kheprx.theme` (`light`|`dark`). Default language `en`, default theme `light`.
- `<html lang>`/`<html dir>` are owned by `LanguageStore` at runtime — no hardcoded `dir`/`lang` in templates.
- Brand string defaults to **"International Swimming Academy"** (single source, in the shell dictionary).
- Tests follow the existing `testing/`-mirror Jest convention.
- Do **not** `git commit` until the user says so (stage only).

---

### Task 1: Reshape the role + gender label sets to swimming

**Files:**
- Modify: `frontend/src/app/core/domain/roles/user-role.ts`
- Modify: `frontend/src/app/core/domain/roles/role-labels.ts`
- Modify: `frontend/src/app/core/domain/gender/*` (label map → i18n keys; locate the `GENDER_LABELS` file)
- Test: `frontend/src/app/core/domain/roles/testing/role-labels.spec.ts` (create/adjust)

**Interfaces:**
- Produces: `type UserRole = 'head_coach' | 'captain'`; `ROLE_LABELS: Record<UserRole, string>` now mapping to **i18n keys** (`'roles.head_coach'`, `'roles.captain'`); `GENDER_LABELS: Record<Gender, string>` mapping to i18n keys (`'gender.male'`, `'gender.female'`).

- [ ] **Step 1: Update the role set**

`user-role.ts`:
```ts
// UserRole is the swimming role set. Head coach has broader access than captain.
// Shared kernel: used by auth, users, and layout.
export type UserRole = 'head_coach' | 'captain';
```

`role-labels.ts` (values are now translation keys resolved by TranslateService):
```ts
import { UserRole } from '@core/domain/roles/user-role';

export const ROLE_LABELS: Record<UserRole, string> = {
  head_coach: 'roles.head_coach',
  captain: 'roles.captain',
};
```

- [ ] **Step 2: Update gender labels to i18n keys**

In the `GENDER_LABELS` file (under `core/domain/gender`), replace the Arabic literals with keys:
```ts
export const GENDER_LABELS: Record<Gender, string> = {
  male: 'gender.male',
  female: 'gender.female',
};
```
(Keep the `Gender` type as `'male' | 'female'`.)

- [ ] **Step 3: Test the label maps**

```ts
import { ROLE_LABELS } from '@core/domain/roles';

describe('ROLE_LABELS', () => {
  it('maps each swimming role to an i18n key', () => {
    expect(ROLE_LABELS.head_coach).toBe('roles.head_coach');
    expect(ROLE_LABELS.captain).toBe('roles.captain');
    expect(Object.keys(ROLE_LABELS).sort()).toEqual(['captain', 'head_coach']);
  });
});
```

- [ ] **Step 4: Run tests** — `cd frontend && npx jest core/domain/roles` → PASS.
- [ ] **Step 5: Stage** (hold commit). Message: `refactor(core): swimming role set + i18n label keys`.

---

### Task 2: `LanguageStore` — language + live direction

**Files:**
- Create: `frontend/src/app/core/i18n/language.store.ts`
- Test: `frontend/src/app/core/i18n/testing/language.store.spec.ts`

**Interfaces:**
- Produces: `type Lang = 'en' | 'ar'`; `LanguageStore` (`providedIn: 'root'`) with `lang: Signal<Lang>`, `dir: Signal<'ltr'|'rtl'>`, `set(lang: Lang): void`, `toggle(): void`. Side effects on change: sets `document.documentElement.lang`/`dir` and persists `kheprx.lang`.

- [ ] **Step 1: Write the failing test**

```ts
import { TestBed } from '@angular/core/testing';
import { LanguageStore } from '@core/i18n/language.store';

describe('LanguageStore', () => {
  beforeEach(() => { localStorage.clear(); TestBed.resetTestingModule(); });

  it('defaults to en / ltr and reflects onto <html>', () => {
    const store = TestBed.inject(LanguageStore);
    TestBed.tick(); // flush effect
    expect(store.lang()).toBe('en');
    expect(document.documentElement.dir).toBe('ltr');
    expect(document.documentElement.lang).toBe('en');
  });

  it('toggle switches to ar / rtl and persists', () => {
    const store = TestBed.inject(LanguageStore);
    store.toggle();
    TestBed.tick();
    expect(store.lang()).toBe('ar');
    expect(store.dir()).toBe('rtl');
    expect(document.documentElement.dir).toBe('rtl');
    expect(localStorage.getItem('kheprx.lang')).toBe('ar');
  });

  it('reads the persisted language on construction', () => {
    localStorage.setItem('kheprx.lang', 'ar');
    expect(TestBed.inject(LanguageStore).lang()).toBe('ar');
  });
});
```

- [ ] **Step 2: Run to verify it fails** — `npx jest core/i18n/language` → FAIL (no module).

- [ ] **Step 3: Implement**

```ts
import { Injectable, computed, effect, signal } from '@angular/core';

export type Lang = 'en' | 'ar';
const KEY = 'kheprx.lang';

@Injectable({ providedIn: 'root' })
export class LanguageStore {
  private readonly _lang = signal<Lang>(this.read());
  readonly lang = this._lang.asReadonly();
  readonly dir = computed<'ltr' | 'rtl'>(() => (this._lang() === 'ar' ? 'rtl' : 'ltr'));

  constructor() {
    effect(() => {
      const lang = this._lang();
      const el = document.documentElement;
      el.lang = lang;
      el.dir = lang === 'ar' ? 'rtl' : 'ltr';
      localStorage.setItem(KEY, lang);
    });
  }

  set(lang: Lang): void { this._lang.set(lang); }
  toggle(): void { this._lang.set(this._lang() === 'en' ? 'ar' : 'en'); }

  private read(): Lang {
    const v = localStorage.getItem(KEY);
    return v === 'ar' || v === 'en' ? v : 'en';
  }
}
```

- [ ] **Step 4: Run tests** — `npx jest core/i18n/language` → PASS.
- [ ] **Step 5: Stage** (hold commit). Message: `feat(core): LanguageStore with live dir/lang`.

---

### Task 3: `TranslateService` + dictionaries + `translate` pipe

**Files:**
- Create: `frontend/src/app/core/i18n/en.json`, `frontend/src/app/core/i18n/ar.json`
- Create: `frontend/src/app/core/i18n/dictionaries.ts`
- Create: `frontend/src/app/core/i18n/translate.service.ts`
- Create: `frontend/src/app/core/i18n/translate.pipe.ts`
- Create: `frontend/src/app/core/i18n/index.ts` (barrel)
- Modify: `frontend/tsconfig.json` (add `"resolveJsonModule": true` under `compilerOptions` if absent)
- Test: `frontend/src/app/core/i18n/testing/translate.service.spec.ts`

**Interfaces:**
- Produces: `TranslateService` (`providedIn:'root'`) with `t(key: string): string` (returns the key itself when missing) that reacts to `LanguageStore.lang()`; `TranslatePipe` (`standalone`, `pure: false`, name `translate`).

- [ ] **Step 1: Seed the dictionaries (shell + common + roles + gender)**

`en.json`:
```json
{
  "common": { "save": "Save", "cancel": "Cancel", "comingSoon": "Coming soon" },
  "roles": { "head_coach": "Head Coach", "captain": "Captain" },
  "gender": { "male": "Male", "female": "Female" },
  "shell": {
    "brand": "International Swimming Academy",
    "signOut": "Sign out",
    "searchPlaceholder": "Search (coming soon)",
    "nav": {
      "dashboard": "Dashboard", "swimmers": "Swimmers", "attendance": "Attendance",
      "championships": "Championships", "captainPanel": "Captain Panel", "settings": "Settings"
    }
  }
}
```

`ar.json`:
```json
{
  "common": { "save": "حفظ", "cancel": "إلغاء", "comingSoon": "قريباً" },
  "roles": { "head_coach": "المدرب العام", "captain": "الكابتن" },
  "gender": { "male": "ذكر", "female": "أنثى" },
  "shell": {
    "brand": "الأكاديمية الدولية للسباحة",
    "signOut": "تسجيل الخروج",
    "searchPlaceholder": "بحث (قريباً)",
    "nav": {
      "dashboard": "الرئيسية", "swimmers": "السباحون", "attendance": "الحضور",
      "championships": "البطولات", "captainPanel": "لوحة الكابتن", "settings": "الإعدادات"
    }
  }
}
```

- [ ] **Step 2: dictionaries.ts + service + pipe + barrel**

`dictionaries.ts`:
```ts
import en from './en.json';
import ar from './ar.json';
import { Lang } from './language.store';

export type Dict = Record<string, unknown>;
export const DICTIONARIES: Record<Lang, Dict> = { en: en as Dict, ar: ar as Dict };
```

`translate.service.ts`:
```ts
import { Injectable, computed, inject } from '@angular/core';
import { LanguageStore } from './language.store';
import { DICTIONARIES, Dict } from './dictionaries';

@Injectable({ providedIn: 'root' })
export class TranslateService {
  private readonly language = inject(LanguageStore);
  private readonly resolver = computed(() => {
    const dict = DICTIONARIES[this.language.lang()];
    return (key: string): string => lookup(dict, key) ?? key;
  });
  t(key: string): string { return this.resolver()(key); }
}

function lookup(dict: Dict, key: string): string | null {
  const val = key.split('.').reduce<unknown>(
    (acc, k) => (acc && typeof acc === 'object' ? (acc as Dict)[k] : undefined), dict);
  return typeof val === 'string' ? val : null;
}
```

`translate.pipe.ts` (impure → recomputes each CD so it tracks language changes):
```ts
import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslateService } from './translate.service';

@Pipe({ name: 'translate', standalone: true, pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly i18n = inject(TranslateService);
  transform(key: string): string { return this.i18n.t(key); }
}
```

`index.ts`:
```ts
export { LanguageStore } from './language.store';
export type { Lang } from './language.store';
export { TranslateService } from './translate.service';
export { TranslatePipe } from './translate.pipe';
```

- [ ] **Step 3: Test**

```ts
import { TestBed } from '@angular/core/testing';
import { LanguageStore } from '@core/i18n/language.store';
import { TranslateService } from '@core/i18n/translate.service';

describe('TranslateService', () => {
  beforeEach(() => { localStorage.clear(); TestBed.resetTestingModule(); });

  it('resolves dot-path keys for the active language', () => {
    const t = TestBed.inject(TranslateService);
    expect(t.t('shell.nav.settings')).toBe('Settings');
  });

  it('switches language reactively', () => {
    const lang = TestBed.inject(LanguageStore);
    const t = TestBed.inject(TranslateService);
    lang.set('ar');
    expect(t.t('shell.nav.settings')).toBe('الإعدادات');
  });

  it('returns the key itself when missing', () => {
    expect(TestBed.inject(TranslateService).t('nope.missing')).toBe('nope.missing');
  });
});
```

- [ ] **Step 4: Run** — `npx jest core/i18n` → PASS. If JSON import errors, confirm `resolveJsonModule` is enabled.
- [ ] **Step 5: Stage** (hold commit). Message: `feat(core): signal-based TranslateService + translate pipe + EN/AR dictionaries`.

---

### Task 4: `ThemeStore` — light/dark

**Files:**
- Create: `frontend/src/app/core/ui/theme/theme.store.ts`
- Test: `frontend/src/app/core/ui/theme/testing/theme.store.spec.ts`

**Interfaces:**
- Produces: `type ThemeMode = 'light' | 'dark'`; `ThemeStore` (`providedIn:'root'`) with `mode: Signal<ThemeMode>`, `set(m)`, `toggle()`. Side effect: toggles `.dark` on `document.documentElement`; persists `kheprx.theme`.

- [ ] **Step 1: Failing test**

```ts
import { TestBed } from '@angular/core/testing';
import { ThemeStore } from '@core/ui/theme/theme.store';

describe('ThemeStore', () => {
  beforeEach(() => { localStorage.clear(); document.documentElement.classList.remove('dark'); TestBed.resetTestingModule(); });

  it('defaults to light', () => {
    const s = TestBed.inject(ThemeStore); TestBed.tick();
    expect(s.mode()).toBe('light');
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });

  it('toggles to dark, applies the class, and persists', () => {
    const s = TestBed.inject(ThemeStore); s.toggle(); TestBed.tick();
    expect(s.mode()).toBe('dark');
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    expect(localStorage.getItem('kheprx.theme')).toBe('dark');
  });
});
```

- [ ] **Step 2: Run to fail** — `npx jest core/ui/theme/testing/theme.store` → FAIL.

- [ ] **Step 3: Implement**

```ts
import { Injectable, effect, signal } from '@angular/core';

export type ThemeMode = 'light' | 'dark';
const KEY = 'kheprx.theme';

@Injectable({ providedIn: 'root' })
export class ThemeStore {
  private readonly _mode = signal<ThemeMode>(this.read());
  readonly mode = this._mode.asReadonly();

  constructor() {
    effect(() => {
      const mode = this._mode();
      document.documentElement.classList.toggle('dark', mode === 'dark');
      localStorage.setItem(KEY, mode);
    });
  }

  set(mode: ThemeMode): void { this._mode.set(mode); }
  toggle(): void { this._mode.set(this._mode() === 'light' ? 'dark' : 'light'); }

  private read(): ThemeMode { return localStorage.getItem(KEY) === 'dark' ? 'dark' : 'light'; }
}
```

- [ ] **Step 4: Run** — PASS.
- [ ] **Step 5: Stage** (hold commit). Message: `feat(core): ThemeStore (light/dark, class-based)`.

---

### Task 5: Teal/cream palette + theme-aware tokens + bootstrap activation

**Files:**
- Modify: `frontend/tailwind.config.js` (class dark mode; map semantic colors to CSS variables)
- Modify: `frontend/src/styles.scss` (define light + `.dark` variable sets)
- Modify: `frontend/src/index.html` (title/brand; keep `lang`/`dir` but they are overwritten at runtime)
- Modify: `frontend/src/app/app.ts` (activate `LanguageStore` + `ThemeStore` at bootstrap)

- [ ] **Step 1: Tailwind — class dark mode + variable-backed tokens**

In `tailwind.config.js`, add `darkMode: 'class',` at the top of the config object and replace the `colors.canvas/surface/ink/primary/accent/border/text` entries so they read CSS variables (keep the existing keys so component markup is unchanged):
```js
canvas: 'rgb(var(--c-canvas) / <alpha-value>)',
surface: 'rgb(var(--c-surface) / <alpha-value>)',
ink: { DEFAULT: 'rgb(var(--c-ink) / <alpha-value>)', hover: 'rgb(var(--c-ink-hover) / <alpha-value>)' },
border: { DEFAULT: 'rgb(var(--c-border) / <alpha-value>)' },
primary: { DEFAULT: 'rgb(var(--c-primary) / <alpha-value>)', hover: 'rgb(var(--c-primary-hover) / <alpha-value>)', light: 'rgb(var(--c-primary-hover) / <alpha-value>)' },
accent: { DEFAULT: 'rgb(var(--c-accent) / <alpha-value>)', light: 'rgb(var(--c-accent-light) / <alpha-value>)' },
text: { primary: 'rgb(var(--c-ink) / <alpha-value>)', secondary: 'rgb(var(--c-text-secondary) / <alpha-value>)', muted: 'rgb(var(--c-text-muted) / <alpha-value>)' },
```
(Leave `success`/`warning`/`danger` as-is.)

- [ ] **Step 2: styles.scss — the two palettes (teal/cream)**

Add at the top of `styles.scss`:
```scss
:root {
  --c-canvas: 245 241 234;      /* #F5F1EA cream */
  --c-surface: 255 255 255;
  --c-ink: 20 48 43;            /* #14302B */
  --c-ink-hover: 13 92 99;
  --c-border: 228 221 207;      /* #E4DDCF */
  --c-primary: 15 118 110;      /* #0F766E teal-700 */
  --c-primary-hover: 13 92 99;  /* #0D5C63 */
  --c-accent: 14 116 144;       /* #0E7490 */
  --c-accent-light: 45 212 191; /* #2DD4BF */
  --c-text-secondary: 71 85 85;
  --c-text-muted: 148 163 163;
}
.dark {
  --c-canvas: 11 31 28;         /* #0B1F1C */
  --c-surface: 18 48 43;        /* #12302B */
  --c-ink: 231 240 238;
  --c-ink-hover: 209 232 228;
  --c-border: 30 58 52;
  --c-primary: 45 212 191;      /* teal-400 */
  --c-primary-hover: 20 184 166;
  --c-accent: 45 212 191;
  --c-accent-light: 94 234 212;
  --c-text-secondary: 156 178 172;
  --c-text-muted: 110 130 124;
}
```

- [ ] **Step 3: index.html brand/title**

Set `<title>International Swimming Academy</title>`. Leave `<html lang="en" dir="ltr">` (the `LanguageStore` effect overrides both at runtime; `en`/`ltr` is the correct pre-hydration default). Keep the Google Fonts link (Cairo/Tajawal for AR, Inter for EN).

- [ ] **Step 4: Activate the stores at bootstrap**

In `app.ts` (the root component), inject both stores so their constructor effects run at startup:
```ts
import { LanguageStore } from '@core/i18n';
import { ThemeStore } from '@core/ui/theme/theme.store';
// ...
export class App {
  constructor() { inject(LanguageStore); inject(ThemeStore); }
}
```
(Import `inject` from `@angular/core` if not already.)

- [ ] **Step 5: Build + smoke test**

Run: `cd frontend && npm run build`
Expected: builds. Manually (`npm start`): page background is cream, primary elements teal; toggling `.dark` on `<html>` in devtools flips to the dark palette.

- [ ] **Step 6: Stage** (hold commit). Message: `feat(ui): teal/cream theme-aware palette + activate language/theme at bootstrap`.

---

### Task 6: Rebrand the minimal shell (nav + i18n + toggles)

**Files:**
- Modify: `frontend/src/app/layout/layout.component.ts`
- Modify: `frontend/src/app/layout/layout.component.html`
- Modify: `frontend/src/app/layout/layout.component.spec.ts`

**Interfaces:**
- Consumes: `TranslatePipe`, `LanguageStore`, `ThemeStore`, `ROLE_LABELS`, `UserRole`.
- Produces: a shell whose nav shows **Settings** (enabled) + Dashboard/Swimmers/Attendance/Championships/Captain Panel (disabled placeholders), all labelled via i18n; a language toggle + theme toggle in the top bar; brand from `shell.brand`.

- [ ] **Step 1: Update the component**

In `layout.component.ts`: import `TranslatePipe` (add to `imports`), `LanguageStore`, `ThemeStore`; inject `language` + `theme`. Replace `allItems` with i18n **keys** and the swimming nav (only Settings has a `route`; others are inert placeholders shown disabled). Add `roles` where relevant (Captain Panel → `['captain']`, others none for now):
```ts
private readonly allItems: NavItem[] = [
  { label: 'shell.nav.dashboard', icon: LucideLayoutDashboard },
  { label: 'shell.nav.swimmers', icon: LucideUsers },
  { label: 'shell.nav.attendance', icon: LucideClipboardCheck },
  { label: 'shell.nav.championships', icon: LucideTrophy },
  { label: 'shell.nav.captainPanel', icon: LucideShieldCheck, roles: ['captain'] },
  { label: 'shell.nav.settings', icon: LucideSettings, route: '/account' },
];
```
Keep the `navItems` role filter. Add `toggleLanguage()` → `this.language.toggle()`, `toggleTheme()` → `this.theme.toggle()`, and expose `lang = this.language.lang`, `mode = this.theme.mode`. `roleLabel`/`userInitial` unchanged except `ROLE_LABELS[r]` now returns an i18n key — pass it through the pipe in the template.

- [ ] **Step 2: Update the template**

In `layout.component.html`: remove the hardcoded `dir="rtl"` from the root `<div>` (direction is inherited from `<html>`). Replace the brand block text with `{{ 'shell.brand' | translate }}` and a teal wave/water glyph. Pipe every `item.label` through `translate`: `{{ item.label | translate }}`; the disabled item `title` → `{{ 'common.comingSoon' | translate }}`. Sign-out label → `{{ 'shell.signOut' | translate }}`; search placeholder → `{{ 'shell.searchPlaceholder' | translate }}`. Add two top-bar buttons before the profile button:
```html
<button (click)="toggleLanguage()" class="text-text-secondary hover:text-primary text-sm font-semibold px-2">
  {{ lang() === 'en' ? 'AR' : 'EN' }}
</button>
<button (click)="toggleTheme()" class="text-text-secondary hover:text-primary px-2" [attr.aria-label]="'theme'">
  <svg [lucideIcon]="mode() === 'dark' ? SunIcon : MoonIcon" [size]="18" />
</button>
```
Add `TranslatePipe` to the component `imports`, and `LucideSun`/`LucideMoon`/`LucideLayoutDashboard`/`LucideClipboardCheck`/`LucideTrophy` icon fields.

- [ ] **Step 3: Update the spec**

Adjust `layout.component.spec.ts`: provide `LanguageStore`/`ThemeStore` (real, `providedIn:'root'` — no mock needed), stub `AuthSessionStore` with `role: () => 'captain'`; assert the Settings nav item renders and Dashboard renders disabled; assert `toggleLanguage()` flips `lang()`.

- [ ] **Step 4: Run** — `npx jest layout` → PASS; then `npm run build` → builds.
- [ ] **Step 5: Stage** (hold commit). Message: `feat(shell): swimming rebrand, i18n nav, language + theme toggles`.

## Self-Review Notes (author)

- **Spec coverage:** §5.1 i18n → Tasks 2–3; §5.2 theme → Tasks 4–5; palette → Task 5; §6.3 shell → Task 6; role reshape (needed by shell + Plan 3) → Task 1.
- **Reactivity choice:** impure `translate` pipe + signal resolver is deliberate — it re-renders on language change without a subscription. `TestBed.tick()` flushes the store effects in tests.
- **Handoff to Plan 3:** login/profile dictionary keys are added in Plan 3 (Tasks 2–3) alongside those screens; this plan seeds only `common`/`roles`/`gender`/`shell`.
