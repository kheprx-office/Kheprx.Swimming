# App-Wide Loading Indicator & Busy States — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every API call consistent, professional feedback — a global top progress bar, skeleton placeholders for data-loading content, and buttons disabled until their response returns.

**Architecture:** A root `LoadingService` holds a signal-based in-flight counter. A functional `loadingInterceptor` increments/decrements it around every non-silent HTTP request. A `LoadingBarComponent` mounted once in the app root reflects that state as a thin top bar (with an anti-flicker delay). A reusable `SkeletonComponent` wraps the existing `.skeleton` shimmer; the Settings page uses it for the profile card and disables its controls while the profile loads.

**Tech Stack:** Angular 20 (standalone components, signals, functional HTTP interceptors), Tailwind 3.4.17, `@lucide/angular`, Jest (`jest-preset-angular`).

**Spec:** `frontend/docs/superpowers/specs/2026-09-01-app-loading-indicator-design.md`

## Global Constraints

- All components are `standalone: true` (no NgModules). Match existing style.
- Run a single spec: `npx jest <path-to-spec>` from `frontend/`. Full suite: `npx jest`.
- Type/AOT check: `npx ng build --configuration development` from `frontend/` — must be clean (strict templates are on).
- **Commit policy:** the user has a standing "do not commit without explicit approval" preference. Where a step says *Commit*, stage the files and pause for the user's go-ahead rather than committing unprompted.
- Silent-request opt-out list (exact): `['/api/auth/refresh', '/api/auth/logout']`.
- Progress-bar constants (exact): height `h-0.5` (2px), `z-[70]`, show-delay `120` ms.
- Skeletons reuse the existing global `.skeleton` class in `src/styles.scss` — do not invent a new shimmer.
- New core files live under `src/app/core/network` and `src/app/core/ui/components`, with specs co-located (mirrors `core/network/api/http-client.spec.ts`). New auth-feature section specs go in the `src/app/features/auth/testing/...` mirror tree (matches existing convention).

---

### Task 1: LoadingService

**Files:**
- Create: `src/app/core/network/loading.service.ts`
- Test: `src/app/core/network/loading.service.spec.ts`

**Interfaces:**
- Consumes: nothing.
- Produces: `class LoadingService` (root-provided) with `readonly isLoading: Signal<boolean>`, `readonly activeCount: Signal<number>`, `begin(): void`, `end(): void`.

- [ ] **Step 1: Write the failing test**

`src/app/core/network/loading.service.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { LoadingService } from './loading.service';

describe('LoadingService', () => {
  let svc: LoadingService;
  beforeEach(() => { svc = TestBed.inject(LoadingService); });

  it('is not loading initially', () => {
    expect(svc.isLoading()).toBe(false);
    expect(svc.activeCount()).toBe(0);
  });

  it('is loading after begin() and clears after the matching end()', () => {
    svc.begin();
    expect(svc.isLoading()).toBe(true);
    svc.end();
    expect(svc.isLoading()).toBe(false);
  });

  it('stays loading until all concurrent requests end', () => {
    svc.begin();
    svc.begin();
    expect(svc.activeCount()).toBe(2);
    svc.end();
    expect(svc.isLoading()).toBe(true);
    svc.end();
    expect(svc.isLoading()).toBe(false);
  });

  it('never underflows below zero', () => {
    svc.end();
    expect(svc.activeCount()).toBe(0);
    expect(svc.isLoading()).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx jest src/app/core/network/loading.service.spec.ts`
Expected: FAIL — cannot find module `./loading.service`.

- [ ] **Step 3: Write minimal implementation**

`src/app/core/network/loading.service.ts`:
```ts
// LoadingService: app-wide in-flight request counter, exposed as signals. Driven by
// loadingInterceptor; read by the global LoadingBar and any busy-aware UI. A counter
// (not a boolean) so concurrent requests compose — the bar hides only when the last settles.
import { Injectable, computed, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly _count = signal(0);
  readonly activeCount = this._count.asReadonly();
  readonly isLoading = computed(() => this._count() > 0);

  begin(): void {
    this._count.update((n) => n + 1);
  }

  end(): void {
    this._count.update((n) => Math.max(0, n - 1));
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npx jest src/app/core/network/loading.service.spec.ts`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit** (per commit policy, stage + confirm)

```bash
git add src/app/core/network/loading.service.ts src/app/core/network/loading.service.spec.ts
git commit -m "feat(loading): add signal-based LoadingService in-flight counter"
```

---

### Task 2: loadingInterceptor + registration

**Files:**
- Create: `src/app/core/network/loading.interceptor.ts`
- Test: `src/app/core/network/loading.interceptor.spec.ts`
- Modify: `src/app/app.config.ts` (interceptor list)

**Interfaces:**
- Consumes: `LoadingService` (`begin`/`end`) from Task 1.
- Produces: `const loadingInterceptor: HttpInterceptorFn`.

- [ ] **Step 1: Write the failing test**

`src/app/core/network/loading.interceptor.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors, HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { loadingInterceptor } from './loading.interceptor';
import { LoadingService } from './loading.service';

describe('loadingInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let loading: LoadingService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([loadingInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    loading = TestBed.inject(LoadingService);
  });

  afterEach(() => httpMock.verify());

  it('flags loading while a request is in flight and clears it on completion', () => {
    expect(loading.isLoading()).toBe(false);
    http.get('/api/anything').subscribe();
    expect(loading.isLoading()).toBe(true);
    httpMock.expectOne('/api/anything').flush({});
    expect(loading.isLoading()).toBe(false);
  });

  it('clears loading even when the request errors', () => {
    http.get('/api/boom').subscribe({ error: () => undefined });
    expect(loading.isLoading()).toBe(true);
    httpMock.expectOne('/api/boom').flush('nope', { status: 500, statusText: 'Server Error' });
    expect(loading.isLoading()).toBe(false);
  });

  it('does not flag loading for silent auth requests', () => {
    http.post('/api/auth/refresh', {}).subscribe();
    expect(loading.isLoading()).toBe(false);
    httpMock.expectOne('/api/auth/refresh').flush({});
    expect(loading.isLoading()).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx jest src/app/core/network/loading.interceptor.spec.ts`
Expected: FAIL — cannot find module `./loading.interceptor`.

- [ ] **Step 3: Write minimal implementation**

`src/app/core/network/loading.interceptor.ts`:
```ts
// loadingInterceptor: increments the LoadingService counter around every request and
// decrements it via finalize (covers success, error, and cancel — no leaks). Silent auth
// calls (transparent token refresh, logout) opt out so background traffic never blips the UI.
import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { finalize } from 'rxjs';
import { LoadingService } from './loading.service';

const SILENT = ['/api/auth/refresh', '/api/auth/logout'];
const isSilent = (url: string) => SILENT.some((path) => url.includes(path));

export const loadingInterceptor: HttpInterceptorFn = (req, next) => {
  if (isSilent(req.url)) return next(req);
  const loading = inject(LoadingService);
  loading.begin();
  return next(req).pipe(finalize(() => loading.end()));
};
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npx jest src/app/core/network/loading.interceptor.spec.ts`
Expected: PASS (3 tests).

- [ ] **Step 5: Register the interceptor**

In `src/app/app.config.ts`, add the import and append to the interceptor array:
```ts
import { loadingInterceptor } from '@core/network/loading.interceptor';
```
Change:
```ts
    provideHttpClient(withInterceptors([authInterceptor])),
```
to:
```ts
    provideHttpClient(withInterceptors([authInterceptor, loadingInterceptor])),
```

- [ ] **Step 6: Verify the build**

Run: `npx ng build --configuration development`
Expected: clean build.

- [ ] **Step 7: Commit** (stage + confirm)

```bash
git add src/app/core/network/loading.interceptor.ts src/app/core/network/loading.interceptor.spec.ts src/app/app.config.ts
git commit -m "feat(loading): track in-flight requests via loadingInterceptor"
```

---

### Task 3: Global top progress bar

**Files:**
- Create: `src/app/core/ui/components/loading-bar.component.ts`
- Test: `src/app/core/ui/components/loading-bar.component.spec.ts`
- Modify: `src/styles.scss` (keyframe)
- Modify: `src/app/app.ts` (mount)

**Interfaces:**
- Consumes: `LoadingService.isLoading` from Task 1.
- Produces: `class LoadingBarComponent` with selector `app-loading-bar`.

- [ ] **Step 1: Add the keyframe to `src/styles.scss`**

Add near the other keyframes (e.g. after the `slideInRight`/`slideInLeft` block):
```css
@keyframes loadingBar {
  0%   { transform: translateX(-100%); }
  100% { transform: translateX(400%); }
}
.animate-loading-bar { animation: loadingBar 1.1s ease-in-out infinite; }
```
(The existing `prefers-reduced-motion` block already collapses animation durations, so the bar degrades gracefully.)

- [ ] **Step 2: Write the failing test**

`src/app/core/ui/components/loading-bar.component.spec.ts`:
```ts
import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { LoadingBarComponent } from './loading-bar.component';
import { LoadingService } from '@core/network/loading.service';

function bar(fixture: { nativeElement: HTMLElement }): HTMLElement | null {
  return fixture.nativeElement.querySelector('[role="progressbar"]');
}

describe('LoadingBarComponent', () => {
  it('shows the bar after the show-delay while loading, and hides it when loading ends', fakeAsync(() => {
    const loading = TestBed.inject(LoadingService);
    const fixture = TestBed.createComponent(LoadingBarComponent);
    fixture.detectChanges();
    expect(bar(fixture)).toBeNull();

    loading.begin();
    fixture.detectChanges();
    tick(120);
    fixture.detectChanges();
    expect(bar(fixture)).toBeTruthy();

    loading.end();
    fixture.detectChanges();
    expect(bar(fixture)).toBeNull();
  }));

  it('does not flash for requests faster than the show-delay', fakeAsync(() => {
    const loading = TestBed.inject(LoadingService);
    const fixture = TestBed.createComponent(LoadingBarComponent);
    fixture.detectChanges();

    loading.begin();
    fixture.detectChanges();
    tick(50);       // completes before the 120ms delay
    loading.end();
    fixture.detectChanges();
    tick(120);
    fixture.detectChanges();
    expect(bar(fixture)).toBeNull();
  }));
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `npx jest src/app/core/ui/components/loading-bar.component.spec.ts`
Expected: FAIL — cannot find module `./loading-bar.component`.

- [ ] **Step 4: Write minimal implementation**

`src/app/core/ui/components/loading-bar.component.ts`:
```ts
// LoadingBarComponent: a thin indeterminate top bar reflecting LoadingService.isLoading().
// Mounted once at the app root (beside the notification host). A ~120ms show-delay prevents
// fast requests from flashing the bar; the pending timer is cleared the moment loading ends.
import { Component, effect, inject, signal } from '@angular/core';
import { LoadingService } from '@core/network/loading.service';

const SHOW_DELAY_MS = 120;

@Component({
  selector: 'app-loading-bar',
  standalone: true,
  template: `
    @if (show()) {
      <div class="fixed inset-x-0 top-0 z-[70] h-0.5 overflow-hidden bg-primary/20"
           role="progressbar" aria-label="Loading">
        <div class="h-full w-1/3 bg-primary animate-loading-bar"></div>
      </div>
    }
  `,
})
export class LoadingBarComponent {
  private readonly loading = inject(LoadingService);
  private readonly _show = signal(false);
  readonly show = this._show.asReadonly();
  private timer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    effect(() => {
      const active = this.loading.isLoading();
      if (active) {
        if (this.timer === null) {
          this.timer = setTimeout(() => {
            this._show.set(true);
            this.timer = null;
          }, SHOW_DELAY_MS);
        }
      } else {
        if (this.timer !== null) {
          clearTimeout(this.timer);
          this.timer = null;
        }
        this._show.set(false);
      }
    });
  }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `npx jest src/app/core/ui/components/loading-bar.component.spec.ts`
Expected: PASS (2 tests). If effects don't flush under `fakeAsync`, add `fixture.detectChanges()` right after each `loading.begin()/end()` (already included).

- [ ] **Step 6: Mount in the app root**

In `src/app/app.ts`, add the import, add to `imports`, and render it after the notification host:
```ts
import { LoadingBarComponent } from '@core/ui/components/loading-bar.component';
```
```ts
  imports: [RouterOutlet, NotificationHostComponent, LoadingBarComponent],
  template: `
    <app-loading-bar />
    <router-outlet />
    <app-notification-host />
  `,
```

- [ ] **Step 7: Verify the build**

Run: `npx ng build --configuration development`
Expected: clean build.

- [ ] **Step 8: Commit** (stage + confirm)

```bash
git add src/app/core/ui/components/loading-bar.component.ts src/app/core/ui/components/loading-bar.component.spec.ts src/styles.scss src/app/app.ts
git commit -m "feat(loading): add global top progress bar with anti-flicker delay"
```

---

### Task 4: Reusable Skeleton component

**Files:**
- Create: `src/app/core/ui/components/skeleton.component.ts`
- Test: `src/app/core/ui/components/skeleton.component.spec.ts`

**Interfaces:**
- Consumes: the global `.skeleton` CSS class (already in `src/styles.scss`).
- Produces: `class SkeletonComponent` with selector `app-skeleton`, `@Input() width: string`, `@Input() height: string`.

- [ ] **Step 1: Write the failing test**

`src/app/core/ui/components/skeleton.component.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { SkeletonComponent } from './skeleton.component';

describe('SkeletonComponent', () => {
  it('renders a .skeleton element sized by its inputs', () => {
    const fixture = TestBed.createComponent(SkeletonComponent);
    fixture.componentRef.setInput('width', '5rem');
    fixture.componentRef.setInput('height', '2rem');
    fixture.detectChanges();
    const el = fixture.nativeElement.querySelector('.skeleton') as HTMLElement;
    expect(el).toBeTruthy();
    expect(el.style.width).toBe('5rem');
    expect(el.style.height).toBe('2rem');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx jest src/app/core/ui/components/skeleton.component.spec.ts`
Expected: FAIL — cannot find module `./skeleton.component`.

- [ ] **Step 3: Write minimal implementation**

`src/app/core/ui/components/skeleton.component.ts`:
```ts
// SkeletonComponent: a single shimmer placeholder block. Sizing comes from the consumer,
// via width/height inputs or Tailwind utility classes on the host. Wraps the global
// `.skeleton` shimmer so every loading placeholder looks identical.
import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-skeleton',
  standalone: true,
  template: `<span class="skeleton block" [style.width]="width" [style.height]="height"></span>`,
})
export class SkeletonComponent {
  @Input() width = '100%';
  @Input() height = '1rem';
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npx jest src/app/core/ui/components/skeleton.component.spec.ts`
Expected: PASS (1 test).

- [ ] **Step 5: Commit** (stage + confirm)

```bash
git add src/app/core/ui/components/skeleton.component.ts src/app/core/ui/components/skeleton.component.spec.ts
git commit -m "feat(ui): add reusable Skeleton placeholder component"
```

---

### Task 5: Disable the change-password form during page load

**Files:**
- Modify: `src/app/features/auth/presentation/components/change-password-form/change-password-form.component.ts` (add input)
- Modify: `src/app/features/auth/presentation/components/change-password-form/change-password-form.component.html` (bind disabled)
- Modify: `src/app/features/auth/presentation/pages/account/sections/security-section/security-section.component.ts` (add input)
- Modify: `src/app/features/auth/presentation/pages/account/sections/security-section/security-section.component.html` (forward disabled)
- Test: extend `src/app/features/auth/testing/presentation/components/change-password-form/change-password-form.component.spec.ts`

**Interfaces:**
- Consumes: nothing new.
- Produces: `ChangePasswordForm` gains `@Input() disabled: boolean`; `SecuritySection` gains `@Input() disabled: boolean` forwarded to the form.

- [ ] **Step 1: Write the failing test**

Append to `change-password-form.component.spec.ts` (mirror the file's existing TestBed setup — it provides `AuthSessionStore`; reuse that `setup`/providers). Minimal standalone version:
```ts
import { TestBed } from '@angular/core/testing';
import { ChangePasswordForm } from '@features/auth/presentation/components/change-password-form/change-password-form.component';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';

describe('ChangePasswordForm — disabled input', () => {
  it('disables the submit button when [disabled] is true (even when not loading)', () => {
    TestBed.configureTestingModule({
      imports: [ChangePasswordForm],
      providers: [{ provide: AuthSessionStore, useValue: { changePassword: jest.fn() } as unknown as AuthSessionStore }],
    });
    const fixture = TestBed.createComponent(ChangePasswordForm);
    fixture.componentRef.setInput('disabled', true);
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(btn.disabled).toBe(true);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx jest src/app/features/auth/testing/presentation/components/change-password-form/change-password-form.component.spec.ts`
Expected: FAIL — button not disabled (the `disabled` input does not exist / is not bound yet).

- [ ] **Step 3: Add the input to the component**

In `change-password-form.component.ts`, add alongside the existing `@Input() variant`:
```ts
  @Input() disabled = false;
```

- [ ] **Step 4: Bind it in the template**

In `change-password-form.component.html`, change the button's disabled binding from:
```html
  <button (click)="submit()" [disabled]="loading()"
```
to:
```html
  <button (click)="submit()" [disabled]="loading() || disabled"
```
Also add `[disabled]="loading() || disabled"` to each of the three `<input type="password" …>` elements so the fields are inert while the page loads.

- [ ] **Step 5: Forward from the security section**

In `security-section.component.ts`, add:
```ts
  @Input() disabled = false;
```
And add the import for `Input` if not already present (`import { Component, Input, signal } from '@angular/core';`).

In `security-section.component.html`, change:
```html
  <app-change-password-form variant="compact" (succeeded)="onSaved()" />
```
to:
```html
  <app-change-password-form variant="compact" [disabled]="disabled" (succeeded)="onSaved()" />
```

- [ ] **Step 6: Run test to verify it passes**

Run: `npx jest src/app/features/auth/testing/presentation/components/change-password-form/change-password-form.component.spec.ts`
Expected: PASS (existing tests + the new one).

- [ ] **Step 7: Commit** (stage + confirm)

```bash
git add src/app/features/auth/presentation/components/change-password-form/ src/app/features/auth/presentation/pages/account/sections/security-section/ src/app/features/auth/testing/presentation/components/change-password-form/change-password-form.component.spec.ts
git commit -m "feat(account): allow disabling the change-password form during page load"
```

---

### Task 6: Disable the preferences controls during page load

**Files:**
- Modify: `src/app/features/auth/presentation/pages/account/sections/preferences-section/preferences-section.component.ts` (add input)
- Modify: `src/app/features/auth/presentation/pages/account/sections/preferences-section/preferences-section.component.html` (bind disabled)
- Test: create `src/app/features/auth/testing/presentation/pages/account/sections/preferences-section/preferences-section.component.spec.ts`

**Interfaces:**
- Consumes: nothing new.
- Produces: `PreferencesSection` gains `@Input() disabled: boolean`.

- [ ] **Step 1: Write the failing test**

`.../preferences-section/preferences-section.component.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { PreferencesSection } from '@features/auth/presentation/pages/account/sections/preferences-section/preferences-section.component';
import { LanguageStore } from '@core/i18n';

describe('PreferencesSection — disabled input', () => {
  it('disables the language and theme buttons when [disabled] is true', () => {
    TestBed.configureTestingModule({ imports: [PreferencesSection] });
    TestBed.inject(LanguageStore).set('en');
    const fixture = TestBed.createComponent(PreferencesSection);
    fixture.componentRef.setInput('disabled', true);
    fixture.detectChanges();
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
    expect(buttons.length).toBeGreaterThan(0);
    expect(buttons.every((b) => b.disabled)).toBe(true);
  });

  it('leaves the buttons enabled when [disabled] is false', () => {
    TestBed.configureTestingModule({ imports: [PreferencesSection] });
    TestBed.inject(LanguageStore).set('en');
    const fixture = TestBed.createComponent(PreferencesSection);
    fixture.detectChanges();
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
    expect(buttons.every((b) => !b.disabled)).toBe(true);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx jest src/app/features/auth/testing/presentation/pages/account/sections/preferences-section/preferences-section.component.spec.ts`
Expected: FAIL — buttons are not disabled (input/binding missing).

- [ ] **Step 3: Add the input**

In `preferences-section.component.ts`, add `Input` to the import and declare the field:
```ts
import { Component, Input, inject } from '@angular/core';
```
```ts
  @Input() disabled = false;
```

- [ ] **Step 4: Bind it in the template**

In `preferences-section.component.html`, add `[disabled]="disabled"` to all four buttons (the two language buttons and the two theme buttons), and add `disabled:opacity-50 disabled:cursor-not-allowed` to each button's class so the disabled state reads clearly. Example for the EN button:
```html
      <button type="button" (click)="toggleLanguage()" [attr.aria-pressed]="lang() === 'en'"
        [disabled]="disabled"
        class="h-7 rounded-[4px] px-2.5 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
        [class.bg-surface]="lang() === 'en'" [class.text-ink]="lang() === 'en'" [class.shadow-sm]="lang() === 'en'"
        [class.text-text-secondary]="lang() !== 'en'">EN</button>
```
Apply the same `[disabled]="disabled"` + disabled utility classes to the `عربي`, `Light`, and `Dark` buttons.

- [ ] **Step 5: Run test to verify it passes**

Run: `npx jest src/app/features/auth/testing/presentation/pages/account/sections/preferences-section/preferences-section.component.spec.ts`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit** (stage + confirm)

```bash
git add src/app/features/auth/presentation/pages/account/sections/preferences-section/ src/app/features/auth/testing/presentation/pages/account/sections/preferences-section/
git commit -m "feat(account): disable preference toggles during page load"
```

---

### Task 7: Skeletons in the profile card

**Files:**
- Modify: `src/app/features/auth/presentation/pages/account/sections/profile-section/profile-section.component.ts` (import SkeletonComponent)
- Modify: `src/app/features/auth/presentation/pages/account/sections/profile-section/profile-section.component.html` (skeleton branch)
- Test: create `src/app/features/auth/testing/presentation/pages/account/sections/profile-section/profile-section.component.spec.ts`

**Interfaces:**
- Consumes: `SkeletonComponent` (`app-skeleton`) from Task 4; existing `@Input() loaded: boolean`.
- Produces: no new public API.

- [ ] **Step 1: Write the failing test**

`.../profile-section/profile-section.component.spec.ts`:
```ts
import { TestBed } from '@angular/core/testing';
import { ProfileSection } from '@features/auth/presentation/pages/account/sections/profile-section/profile-section.component';
import { LanguageStore } from '@core/i18n';

describe('ProfileSection — loading state', () => {
  function make(loaded: boolean) {
    TestBed.configureTestingModule({ imports: [ProfileSection] });
    TestBed.inject(LanguageStore).set('en');
    const fixture = TestBed.createComponent(ProfileSection);
    fixture.componentRef.setInput('loaded', loaded);
    fixture.detectChanges();
    return fixture;
  }

  it('shows skeleton placeholders and hides the value grid while not loaded', () => {
    const fixture = make(false);
    expect(fixture.nativeElement.querySelector('app-skeleton')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('dl')).toBeNull();
  });

  it('shows the real value grid once loaded', () => {
    const fixture = make(true);
    expect(fixture.nativeElement.querySelector('dl')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('app-skeleton')).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx jest src/app/features/auth/testing/presentation/pages/account/sections/profile-section/profile-section.component.spec.ts`
Expected: FAIL — no `app-skeleton` rendered while not loaded.

- [ ] **Step 3: Import SkeletonComponent**

In `profile-section.component.ts`, add the import and register it:
```ts
import { SkeletonComponent } from '@core/ui/components/skeleton.component';
```
```ts
  imports: [LucideDynamicIcon, TranslatePipe, SkeletonComponent],
```

- [ ] **Step 4: Add the skeleton branch to the template**

In `profile-section.component.html`, replace the existing `@if (loaded) { … }` block so an `@else` renders a skeleton grid mirroring the real one:
```html
  @if (loaded) {
    <dl class="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <div>
        <dt class="text-xs font-semibold text-text-secondary">{{ 'profile.fields.phone' | translate }}</dt>
        <dd class="mt-1 text-sm text-ink" dir="ltr">{{ phone || ('profile.fields.notSet' | translate) }}</dd>
      </div>
      <div>
        <dt class="text-xs font-semibold text-text-secondary">{{ 'profile.fields.age' | translate }}</dt>
        <dd class="mt-1 text-sm text-ink">{{ age ?? ('profile.fields.notSet' | translate) }}</dd>
      </div>
      <div>
        <dt class="text-xs font-semibold text-text-secondary">{{ 'profile.fields.gender' | translate }}</dt>
        <dd class="mt-1 text-sm text-ink">{{ genderKey ? (genderKey | translate) : ('profile.fields.notSet' | translate) }}</dd>
      </div>
      <div>
        <dt class="text-xs font-semibold text-text-secondary">{{ 'profile.fields.nationalId' | translate }}</dt>
        <dd class="mt-1 text-sm text-ink" dir="ltr">{{ nationalId || ('profile.fields.notSet' | translate) }}</dd>
      </div>
    </dl>
  } @else {
    <div class="grid grid-cols-1 gap-4 sm:grid-cols-2" aria-hidden="true">
      @for (row of [1, 2, 3, 4]; track row) {
        <div>
          <app-skeleton width="4rem" height="0.75rem" />
          <div class="mt-2"><app-skeleton width="8rem" height="0.875rem" /></div>
        </div>
      }
    </div>
  }
```

- [ ] **Step 5: Run test to verify it passes**

Run: `npx jest src/app/features/auth/testing/presentation/pages/account/sections/profile-section/profile-section.component.spec.ts`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit** (stage + confirm)

```bash
git add src/app/features/auth/presentation/pages/account/sections/profile-section/ src/app/features/auth/testing/presentation/pages/account/sections/profile-section/
git commit -m "feat(account): show profile skeletons while the profile loads"
```

---

### Task 8: Wire the account page (integration)

**Files:**
- Modify: `src/app/features/auth/presentation/pages/account/account.page.html` (pass `[disabled]="vm.loading()"`)
- Test: extend `src/app/features/auth/testing/presentation/pages/account/account.page.spec.ts`

**Interfaces:**
- Consumes: `PreferencesSection.disabled` (Task 6), `SecuritySection.disabled` (Task 5), `ProfileSection.loaded` (existing), `AccountViewModel.loading` (existing signal).
- Produces: the end-to-end behavior — skeletons + disabled controls while `vm.loading()`.

- [ ] **Step 1: Write the failing test**

Add to `account.page.spec.ts` (reuse the file's existing providers/mocks; below is a self-contained version — merge, don't duplicate, provider setup):
```ts
import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { AccountPage } from '@features/auth/presentation/pages/account/account.page';
import { AccountViewModel } from '@features/auth/presentation/pages/account/account.viewmodel';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { LanguageStore } from '@core/i18n';

describe('AccountPage — loading wiring', () => {
  function mountWith(loading: boolean) {
    const vm = {
      loading: signal(loading),
      user: signal(null),
      init: () => undefined,
    } as unknown as AccountViewModel;
    const auth = {
      displayName: () => 'Head Coach',
      principal: () => ({ role: 'head_coach', userId: 'u' }),
      changePassword: jest.fn(),
    } as unknown as AuthSessionStore;
    TestBed.configureTestingModule({
      imports: [AccountPage],
      providers: [
        { provide: AccountViewModel, useValue: vm },
        { provide: AuthSessionStore, useValue: auth },
      ],
    });
    TestBed.inject(LanguageStore).set('en');
    const fixture = TestBed.createComponent(AccountPage);
    fixture.detectChanges();
    return fixture;
  }

  it('shows skeletons and disables preference buttons while loading', () => {
    const fixture = mountWith(true);
    expect(fixture.nativeElement.querySelector('app-skeleton')).toBeTruthy();
    const prefButtons = Array.from(
      fixture.nativeElement.querySelectorAll('app-preferences-section button'),
    ) as HTMLButtonElement[];
    expect(prefButtons.length).toBeGreaterThan(0);
    expect(prefButtons.every((b) => b.disabled)).toBe(true);
  });

  it('enables controls and hides skeletons once loaded', () => {
    const fixture = mountWith(false);
    expect(fixture.nativeElement.querySelector('app-skeleton')).toBeNull();
    const prefButtons = Array.from(
      fixture.nativeElement.querySelectorAll('app-preferences-section button'),
    ) as HTMLButtonElement[];
    expect(prefButtons.every((b) => !b.disabled)).toBe(true);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx jest src/app/features/auth/testing/presentation/pages/account/account.page.spec.ts`
Expected: FAIL — preference buttons are not disabled (the page does not yet pass `[disabled]`).

- [ ] **Step 3: Pass the loading state into the sections**

In `account.page.html`, change:
```html
    <app-preferences-section />
    <app-security-section />
```
to:
```html
    <app-preferences-section [disabled]="vm.loading()" />
    <app-security-section [disabled]="vm.loading()" />
```
(The profile section already receives `[loaded]="!vm.loading()"`.)

- [ ] **Step 4: Run test to verify it passes**

Run: `npx jest src/app/features/auth/testing/presentation/pages/account/account.page.spec.ts`
Expected: PASS.

- [ ] **Step 5: Run the full suite and build**

Run: `npx jest`
Expected: all suites pass.
Run: `npx ng build --configuration development`
Expected: clean build.

- [ ] **Step 6: Commit** (stage + confirm)

```bash
git add src/app/features/auth/presentation/pages/account/account.page.html src/app/features/auth/testing/presentation/pages/account/account.page.spec.ts
git commit -m "feat(account): wire skeletons + disabled controls to the profile load"
```

---

## Self-Review

**Spec coverage:**
- §4.1 LoadingService → Task 1 ✓
- §4.2 loadingInterceptor + registration + silent opt-out → Task 2 ✓
- §4.3 global top progress bar + keyframe + app.ts mount + show-delay → Task 3 ✓
- §4.4 Skeleton primitive → Task 4 ✓
- §5 Settings wiring: profile skeletons → Task 7; preferences disabled → Task 6; change-password disabled → Task 5; account.page passes `vm.loading()` → Task 8 ✓
- §6 button-busy convention → change-password already conformant; documented in the spec, exercised by Task 5 ✓
- §7 edge cases: underflow floor → Task 1 test; finalize on error → Task 2 test; fast-request flicker → Task 3 test; silent opt-out → Task 2 test ✓
- §9 testing strategy → each task's tests ✓

**Placeholder scan:** No TBD/TODO; every code and test step contains concrete content.

**Type consistency:** `LoadingService.begin/end/isLoading/activeCount`, `loadingInterceptor`, `LoadingBarComponent`/`app-loading-bar`, `SkeletonComponent`/`app-skeleton` with `width`/`height`, and the `@Input() disabled` on `ChangePasswordForm`/`SecuritySection`/`PreferencesSection` are used consistently across tasks and match the spec.

No gaps found.
