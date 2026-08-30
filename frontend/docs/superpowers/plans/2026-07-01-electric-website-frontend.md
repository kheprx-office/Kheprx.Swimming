# Electric Website Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the demo scaffold UI with the النور كهرباء website — an RTL Arabic app shell, restyled Login/Change-password/Account, a Home hub, and إدارة المستخدمين — styled to match the `electric-website-angular` reference, in our Clean Architecture + MVVM structure.

**Architecture:** Port the reference's Tailwind design system (palette, fonts, utility CSS, decorative background) into our project; build a routed RTL `LayoutComponent` shell wrapping authenticated pages; keep the Phase-1 auth logic; add a swap-ready **Mock users seam** (`USERS_DATA_SOURCE` port → `MockUsersDataSource` default / `HttpUsersDataSource` built-not-wired) feeding the إدارة المستخدمين feature.

**Tech Stack:** Angular v20 (standalone, signals), TypeScript 5.9 strict, Tailwind 3.4, `@lucide/angular`, Jest 30 + jest-preset-angular.

## Global Constraints

- **Reference:** `docs/references/electric-website-angular` — port templates/styles from it with the exact adaptations each task names. It is in-repo; read the cited file.
- **RTL Arabic:** `index.html` → `lang="ar" dir="rtl"`. Brand: **النور كهرباء** / "نظام إدارة مقاولات الكهرباء".
- **Roles:** `UserRole = 'admin' | 'manager' | 'moqawel' | 'worker'`. `admin` is the top role (reference's `owner` equivalent). Arabic labels: `admin:'مدير النظام', manager:'مدير المشروع', moqawel:'مقاول', worker:'عامل'`.
- **Users data:** Mock seam, swap-ready. No backend `/api/users`; no backend changes. Seed mock users mirror the backend accounts: `{role}@kheprx.local`, `fullName='{role} (seed)'`, `status:'active'`, `mustChangePassword:true`, ids `USR-ADMIN/MANAGER/MOQAWEL/WORKER`.
- **Auth transport:** stays Mock-default, Http-swap-ready. Do not change the default `AUTH_DATA_SOURCE` binding.
- **Design system:** replace the token-based Tailwind palette with the reference palette directly in `tailwind.config.js`. No surviving `core` component uses the old token color classes (verified).
- **`AppErrorKind`** union = `network|http|validation|storage|crypto|auth|unknown` (no `conflict`; use `validation` + status 409 for duplicate email).
- **Routes after this plan:** `/login` (no shell); shell (authGuard) children `/home`, `/user-management` (roleGuard('admin')), `/change-password`, `/account`, `''`→`home`; `**`→`''`.
- **TDD** (RED→GREEN); commit each task. Commit trailer last line: `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.
- **Run from** `frontend/`. Focused test `npx jest <path>`; full suite `npx jest`; build `npm run build`.

---

## File Map

**Create:** `core/ui/components/decor-background.component.ts`; `core/ui/layout/layout.component.ts` + `.html`; `core/auth/role-labels.ts`; `core/users/{users.types.ts, users-data-source.ts, mock-users.data-source.ts, http-users.data-source.ts, users.repository.interface.ts, users.repository.ts, users.repository.port.ts, users.providers.ts, usecases/{list-users,create-user,update-user,set-user-status}.use-case.ts}`; `features/home/{index.ts, presentation/pages/home.page.ts + home.page.html}`; `features/user-management/{index.ts, presentation/pages/user-management.page.ts + .html, presentation/viewmodels/users.viewmodel.ts}`.

**Modify:** `index.html`, `tailwind.config.js`, `src/styles.scss`, `src/app/app.routes.ts`, `src/app/app.config.ts`, `core/ui/components/notification-host.component.ts`, `features/auth/presentation/pages/{login,change-password,account}.page.ts` (+ new `login.page.html`), `features/auth/presentation/viewmodels/login.viewmodel.ts` (+ spec), `features/auth/index.ts`.

**Remove:** `features/home/` (demo), `features/api/`, `features/offline-storage/`, `features/design/`, `core/datasource/api/`, `features/auth/presentation/pages/admin.page.ts`.

---

# Phase A — Foundation, shell, screens

## Task 1: Pre-flight working-tree reconciliation (controller git ops, no code)

Reconcile the uncommitted run-session edits so the branch starts clean: keep the real `angular.json` serve-target fix, keep `env.ts` dev baseUrl at the backend for swap-readiness, and revert the temporary auth→Http wiring (auth stays Mock-default).

**Files:** `frontend/src/app/core/auth/auth.providers.ts` (revert), `frontend/angular.json` (keep), `frontend/src/app/core/config/env.ts` (keep).

- [ ] **Step 1: Inspect the uncommitted changes**

Run: `cd frontend && git status --porcelain && git diff --stat`
Expected: modifications to `src/app/core/auth/auth.providers.ts`, `src/app/core/config/env.ts`, `angular.json`.

- [ ] **Step 2: Revert only the auth Http test-wiring (back to MockAuthDataSource default)**

Run: `git checkout -- src/app/core/auth/auth.providers.ts`
Then confirm it binds `MockAuthDataSource`: `grep -n "useClass" src/app/core/auth/auth.providers.ts` → Expected: `{ provide: AUTH_DATA_SOURCE, useClass: MockAuthDataSource }`.

- [ ] **Step 3: Verify + commit the kept fixes**

Run: `npx jest src/app/core/auth/auth.providers.ts 2>/dev/null; npm run build`
Expected: build clean (`ng serve` config now valid; auth Mock-default).

```bash
git add angular.json src/app/core/config/env.ts
git commit -m "fix(frontend): correct ng-serve buildTarget project name + point dev baseUrl at Identity backend

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
Expected: working tree clean, `git status --porcelain` empty.

---

## Task 2: Design-system foundation (palette, fonts, RTL, utility CSS, decor background)

Port the reference's Tailwind theme + global CSS + decorative background, and switch the app to RTL Arabic. No route/logic changes — existing specs stay green.

**Files:**
- Modify: `frontend/src/index.html`, `frontend/tailwind.config.js`, `frontend/src/styles.scss`
- Create: `frontend/src/app/core/ui/components/decor-background.component.ts`
- Test: `frontend/src/app/core/ui/components/decor-background.component.spec.ts`

**Interfaces:**
- Produces: `DecorBackgroundComponent` (selector `app-decor-background`, `variant = input<'page'|'auth'>('page')`, `className = input<string>('')`); Tailwind palette keys `ink/canvas/surface/border/primary/accent/gold/text.{primary,secondary,muted}/success/warning/danger`; global CSS utility classes (`glass-panel`, `gradient-text`, `bg-grid-dots`, `animate-*`, `card-hover`, `modal-*`, `skeleton`, `animate-shake`).

- [ ] **Step 1: Write the failing decor-background spec**

Create `frontend/src/app/core/ui/components/decor-background.component.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { DecorBackgroundComponent } from '@core/ui/components/decor-background.component';

describe('DecorBackgroundComponent', () => {
  it('defaults to the page variant', () => {
    const fixture = TestBed.createComponent(DecorBackgroundComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.variant()).toBe('page');
    expect(fixture.nativeElement.querySelector('.bg-grid-dots')).toBeTruthy();
  });

  it('renders the auth variant when set', () => {
    const fixture = TestBed.createComponent(DecorBackgroundComponent);
    fixture.componentRef.setInput('variant', 'auth');
    fixture.detectChanges();
    expect(fixture.componentInstance.variant()).toBe('auth');
    // auth variant uses stronger blobs (bg-primary/30)
    expect(fixture.nativeElement.innerHTML).toContain('bg-primary/30');
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npx jest src/app/core/ui/components/decor-background.component.spec.ts`
Expected: FAIL — `DecorBackgroundComponent` does not exist.

- [ ] **Step 3: Create the decor-background component**

Create `frontend/src/app/core/ui/components/decor-background.component.ts` by copying the reference component at `docs/references/electric-website-angular/src/app/shared/decor-background/decor-background.ts` **verbatim** (its template, both `auth`/`page` variants, inputs). Only change: nothing — it has no external deps beyond `@angular/core` + `CommonModule`. Keep `selector: 'app-decor-background'`.

- [ ] **Step 4: Set the app to RTL Arabic**

In `frontend/src/index.html`: change `<html lang="en">` → `<html lang="ar" dir="rtl">` and `<title>Kheprx.BaseFrontend</title>` → `<title>النور كهرباء</title>`.

- [ ] **Step 5: Replace the Tailwind theme with the reference palette**

Replace the `theme.extend` block in `frontend/tailwind.config.js` with the reference's (copy `colors`, `fontFamily`, `borderRadius`, `keyframes`, `animation` from `docs/references/electric-website-angular/tailwind.config.js` lines 5–76). Keep `content: ['./src/**/*.{html,ts}']` and `plugins: []`.

- [ ] **Step 6: Import fonts + port the global utility CSS**

In `frontend/src/styles.scss`: keep the `@use './app/core/ui/theme/theme';` line and the `@tailwind` directives. Add at the very top (before `@use`) the fonts import:
```scss
@import url('https://fonts.googleapis.com/css2?family=Cairo:wght@400;500;600;700;800&family=Tajawal:wght@400;500;700&family=Inter:wght@400;500;600;700&display=swap');
```
Change the `html, body` rule to:
```scss
html, body { margin: 0; padding: 0; background: theme('colors.canvas'); color: theme('colors.text.primary'); font-family: 'Cairo', 'Tajawal', sans-serif; }
* { box-sizing: border-box; }
```
Then append the reference's global utility CSS: copy `docs/references/electric-website-angular/src/styles.css` **lines 11–225** (everything after the `@tailwind utilities;` line — `.tabular-data`, scrollbar, all `@keyframes`/`.animate-*`, `.stagger-*`, `.gradient-text*`, `.glass-panel*`, `.gradient-border`, `.bg-grid-*`, `.card-hover`/`.card-glow`, the global `button, a` transition, `.modal-*`, `.skeleton`, `.animate-shake`, reduced-motion) into the end of `styles.scss`.

- [ ] **Step 7: Run the decor spec + full suite + build**

Run: `npx jest src/app/core/ui/components/decor-background.component.spec.ts` → PASS.
Run: `npx jest` → all existing specs still PASS.
Run: `npm run build` → clean.

- [ ] **Step 8: Commit**

```bash
git add src/index.html tailwind.config.js src/styles.scss src/app/core/ui/components/decor-background.component.ts src/app/core/ui/components/decor-background.component.spec.ts
git commit -m "feat(ui): port reference design system (palette, fonts, RTL, utility CSS, decor background)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: Remove the demo scaffold features

Delete the demo feature slices and their wiring so the app is scoped to auth + (soon) the electric screens. After this, routes are `/login`, `/account`, `/change-password`, and `''`→`/login` (temporary until the shell lands in Task 6).

**Files:**
- Remove: `frontend/src/app/features/home/`, `features/api/`, `features/offline-storage/`, `features/design/`, `frontend/src/app/core/datasource/api/`, `frontend/src/app/features/auth/presentation/pages/admin.page.ts`
- Modify: `frontend/src/app/app.routes.ts`, `frontend/src/app/app.config.ts`, `frontend/src/app/features/auth/index.ts`

- [ ] **Step 1: Delete the demo feature folders + posts data source + admin stub**

Run:
```bash
git rm -r src/app/features/home src/app/features/api src/app/features/offline-storage src/app/features/design src/app/core/datasource/api src/app/features/auth/presentation/pages/admin.page.ts
```

- [ ] **Step 2: Prune `app.config.ts`**

Replace `frontend/src/app/app.config.ts` with (removes the `API_DATA_SOURCE`/`MockApiDataSource` provider + imports):

```ts
// appConfig: the composition root.
import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { routes } from './app.routes';
import { authInterceptor } from '@core/network/interceptors/auth.interceptor';
import { AUTH_PROVIDERS } from '@core/auth/auth.providers';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAnimations(),
    ...AUTH_PROVIDERS,
  ],
};
```

- [ ] **Step 3: Prune the auth barrel (drop AdminPage)**

Replace `frontend/src/app/features/auth/index.ts` with:

```ts
export { LoginPage } from './presentation/pages/login.page';
export { AccountPage } from './presentation/pages/account.page';
export { ChangePasswordPage } from './presentation/pages/change-password.page';
export { LoginViewModel } from './presentation/viewmodels/login.viewmodel';
export { ChangePasswordViewModel } from './presentation/viewmodels/change-password.viewmodel';
```

- [ ] **Step 4: Rewrite `app.routes.ts` to only the surviving routes (temporary)**

Replace `frontend/src/app/app.routes.ts` with:

```ts
// routes: the app-wide route table. The authenticated shell + electric screens
// are wired in a later task; for now only the auth routes exist.
import { Routes } from '@angular/router';
import { authGuard } from '@core/guards/auth.guard';
import { LoginViewModel, ChangePasswordViewModel } from '@features/auth';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('@features/auth').then((m) => m.LoginPage), providers: [LoginViewModel] },
  { path: 'account', canActivate: [authGuard], loadComponent: () => import('@features/auth').then((m) => m.AccountPage) },
  {
    path: 'change-password',
    canActivate: [authGuard],
    loadComponent: () => import('@features/auth').then((m) => m.ChangePasswordPage),
    providers: [ChangePasswordViewModel],
  },
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: '**', redirectTo: '' },
];
```

- [ ] **Step 5: Run the full suite + build**

Run: `npx jest` → all remaining specs PASS (the deleted features' specs no longer run).
Run: `npm run build` → clean.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore(frontend): remove demo scaffold features (home/api/offline-storage/design) + posts data source + admin stub

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: Role labels + RTL layout shell

Add the Arabic role labels and the `LayoutComponent` (reference RTL sidebar + header + footer). The component is built and unit-tested here; it is wired into routing in Task 6.

**Files:**
- Create: `frontend/src/app/core/auth/role-labels.ts`, `frontend/src/app/core/ui/layout/layout.component.ts`, `frontend/src/app/core/ui/layout/layout.component.html`
- Test: `frontend/src/app/core/ui/layout/layout.component.spec.ts`

**Interfaces:**
- Consumes: `AuthSessionStore` (`role()`, `principal()`, `signOut()`), `Router`.
- Produces: `ROLE_LABELS: Record<UserRole,string>`; `LayoutComponent` (selector `app-layout`, renders `<router-outlet/>`, role-filtered nav with active items home/user-management(admin)/account and inert items, sign-out).

- [ ] **Step 1: Create the role labels**

Create `frontend/src/app/core/auth/role-labels.ts`:

```ts
import { UserRole } from '@core/auth/auth.types';

export const ROLE_LABELS: Record<UserRole, string> = {
  admin: 'مدير النظام',
  manager: 'مدير المشروع',
  moqawel: 'مقاول',
  worker: 'عامل',
};
```

- [ ] **Step 2: Write the failing layout spec**

Create `frontend/src/app/core/ui/layout/layout.component.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { LayoutComponent } from '@core/ui/layout/layout.component';
import { AuthSessionStore } from '@core/auth/auth-session.store';

function setup(role: 'admin' | 'worker') {
  const signOut = jest.fn().mockResolvedValue(undefined);
  const auth = {
    role: () => role,
    principal: () => ({ role, userId: 'USR-1' }),
    signOut,
  } as unknown as AuthSessionStore;
  TestBed.configureTestingModule({
    imports: [LayoutComponent],
    providers: [provideRouter([]), { provide: AuthSessionStore, useValue: auth }],
  });
  const fixture = TestBed.createComponent(LayoutComponent);
  fixture.detectChanges();
  return { fixture, signOut };
}

describe('LayoutComponent', () => {
  it('shows إدارة المستخدمين for an admin', () => {
    const { fixture } = setup('admin');
    expect(fixture.nativeElement.textContent).toContain('إدارة المستخدمين');
  });

  it('hides إدارة المستخدمين for a non-admin', () => {
    const { fixture } = setup('worker');
    expect(fixture.nativeElement.textContent).not.toContain('إدارة المستخدمين');
  });

  it('signs out via the store', async () => {
    const { fixture, signOut } = setup('admin');
    await fixture.componentInstance.signOut();
    expect(signOut).toHaveBeenCalled();
  });
});
```

- [ ] **Step 3: Run it to verify it fails**

Run: `npx jest src/app/core/ui/layout/layout.component.spec.ts`
Expected: FAIL — `LayoutComponent` does not exist.

- [ ] **Step 4: Create the layout component class**

Create `frontend/src/app/core/ui/layout/layout.component.ts`:

```ts
import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterOutlet, RouterLink } from '@angular/router';
import {
  LucideAngularModule, Home, ShieldCheck, Settings, LogOut, Menu, X, Search,
  LayoutDashboard, FolderKanban, ClipboardList, Package, Users, Wallet,
} from '@lucide/angular';
import { AuthSessionStore } from '@core/auth/auth-session.store';
import { UserRole } from '@core/auth/auth.types';
import { ROLE_LABELS } from '@core/auth/role-labels';

interface NavItem { label: string; icon: unknown; route?: string; roles?: UserRole[]; }

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, LucideAngularModule],
  templateUrl: './layout.component.html',
})
export class LayoutComponent {
  private readonly auth = inject(AuthSessionStore);
  private readonly router = inject(Router);

  readonly ROLE_LABELS = ROLE_LABELS;
  readonly MenuIcon = Menu;
  readonly XIcon = X;
  readonly SearchIcon = Search;
  readonly LogOutIcon = LogOut;

  readonly isSidebarOpen = signal(true);
  readonly isMobileMenuOpen = signal(false);

  readonly principal = this.auth.principal;
  readonly userInitial = computed(() => (this.principal()?.userId ?? '؟').slice(-1));
  readonly roleLabel = computed(() => {
    const r = this.auth.role();
    return r ? ROLE_LABELS[r] : '';
  });

  // Active items navigate; inert items (route omitted) render disabled ("قريباً").
  private readonly allItems: NavItem[] = [
    { label: 'الرئيسية', icon: Home, route: '/home' },
    { label: 'الملخص', icon: LayoutDashboard },
    { label: 'المشاريع', icon: FolderKanban },
    { label: 'المهام', icon: ClipboardList },
    { label: 'المخازن', icon: Package },
    { label: 'العمالة', icon: Users },
    { label: 'الحسابات', icon: Wallet },
    { label: 'إدارة المستخدمين', icon: ShieldCheck, route: '/user-management', roles: ['admin'] },
    { label: 'الإعدادات', icon: Settings, route: '/account' },
  ];
  readonly navItems = computed<NavItem[]>(() => {
    const role = this.auth.role();
    return this.allItems.filter((i) => !i.roles || (role !== null && i.roles.includes(role)));
  });

  isActive(route?: string): boolean {
    return !!route && this.router.url.startsWith(route);
  }

  async signOut(): Promise<void> {
    await this.auth.signOut();
    void this.router.navigate(['/login']);
  }
}
```

- [ ] **Step 5: Create the layout template**

Create `frontend/src/app/core/ui/layout/layout.component.html` by porting `docs/references/electric-website-angular/src/app/layout/layout.html` with these adaptations:
- Sidebar brand text stays `النور كهرباء`.
- Replace the nav `@for` loop to iterate `navItems()` and, per item, render a `[routerLink]="item.route"` button when `item.route` is set (active styling via `isActive(item.route)`), or a `disabled` button with `title="قريباً"` and muted styling when `item.route` is absent. Use `item.icon` with `<lucide-icon [img]="item.icon" [size]="20" />` and `item.label`.
- Header: keep the mobile toggle and the profile avatar (`{{ userInitial() }}`, `[routerLink]="'/account'"`, `[title]="roleLabel()"`). Replace the search `<input>` with a **disabled** placeholder input (`disabled`, `placeholder="بحث (قريباً)"`) and delete the search-results dropdown block and all search/`ProjectsService`/`LabourService`/`TasksService` logic (not present in this component).
- Footer: sign-out button calls `(click)="signOut()"`; keep the collapse toggle bound to `isSidebarOpen`.
- Keep `<router-outlet />` in the main content area.

- [ ] **Step 6: Run the layout spec**

Run: `npx jest src/app/core/ui/layout/layout.component.spec.ts` → PASS (admin sees إدارة المستخدمين, worker doesn't, signOut delegates).

- [ ] **Step 7: Build**

Run: `npm run build` → clean.

- [ ] **Step 8: Commit**

```bash
git add src/app/core/auth/role-labels.ts src/app/core/ui/layout/
git commit -m "feat(ui): Arabic role labels + RTL layout shell (sidebar + header, inert non-implemented nav)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: Home hub page

The Home hub: gradient greeting + section cards, all inert except إدارة المستخدمين (admin-only, navigates).

**Files:**
- Create: `frontend/src/app/features/home/presentation/pages/home.page.ts`, `frontend/src/app/features/home/presentation/pages/home.page.html`, `frontend/src/app/features/home/index.ts`
- Test: `frontend/src/app/features/home/presentation/pages/home.page.spec.ts`

**Interfaces:**
- Consumes: `AuthSessionStore` (`role()`, `principal()`), `Router`, `DecorBackgroundComponent`.
- Produces: `HomePage` (selector `app-home-page`); only the إدارة المستخدمين card navigates (`go('/user-management')`).

- [ ] **Step 1: Write the failing home spec**

Create `frontend/src/app/features/home/presentation/pages/home.page.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { HomePage } from './home.page';
import { AuthSessionStore } from '@core/auth/auth-session.store';

function setup(role: 'admin' | 'worker') {
  const navigate = jest.fn();
  const auth = { role: () => role, principal: () => ({ role, userId: 'USR-1' }) } as unknown as AuthSessionStore;
  TestBed.configureTestingModule({
    imports: [HomePage],
    providers: [{ provide: AuthSessionStore, useValue: auth }, { provide: Router, useValue: { navigate } }],
  });
  const fixture = TestBed.createComponent(HomePage);
  fixture.detectChanges();
  return { fixture, navigate };
}

describe('HomePage', () => {
  it('shows the إدارة المستخدمين card for an admin', () => {
    const { fixture } = setup('admin');
    expect(fixture.nativeElement.textContent).toContain('ادارة المستخدمين');
  });

  it('hides the admin-only card for a non-admin', () => {
    const { fixture } = setup('worker');
    expect(fixture.nativeElement.textContent).not.toContain('ادارة المستخدمين');
  });

  it('navigates only when a card has a route', () => {
    const { fixture, navigate } = setup('admin');
    fixture.componentInstance.go('/user-management');
    expect(navigate).toHaveBeenCalledWith(['/user-management']);
    navigate.mockClear();
    fixture.componentInstance.go(undefined);
    expect(navigate).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npx jest src/app/features/home/presentation/pages/home.page.spec.ts`
Expected: FAIL — `HomePage` does not exist.

- [ ] **Step 3: Create the home page class**

Create `frontend/src/app/features/home/presentation/pages/home.page.ts`:

```ts
import { Component, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import {
  LucideAngularModule, FolderKanban, Users, ClipboardList, Package, Wallet, Truck, ShieldCheck,
} from '@lucide/angular';
import { AuthSessionStore } from '@core/auth/auth-session.store';
import { DecorBackgroundComponent } from '@core/ui/components/decor-background.component';

interface HomeCard { label: string; desc: string; icon: unknown; color: string; route?: string; adminOnly?: boolean; }

@Component({
  selector: 'app-home-page',
  standalone: true,
  imports: [LucideAngularModule, DecorBackgroundComponent],
  templateUrl: './home.page.html',
})
export class HomePage {
  private readonly auth = inject(AuthSessionStore);
  private readonly router = inject(Router);

  readonly principal = this.auth.principal;

  private readonly allCards: HomeCard[] = [
    { label: 'المشاريع', desc: 'إدارة ومتابعة كافة المشاريع', icon: FolderKanban, color: 'from-primary to-primary-light' },
    { label: 'العمالة', desc: 'العمال والحضور والانصراف', icon: Users, color: 'from-accent to-accent-light' },
    { label: 'المهام', desc: 'إنشاء ومتابعة سير العمل', icon: ClipboardList, color: 'from-primary to-primary-hover' },
    { label: 'المخزون', desc: 'المنتجات وحركة المخزون', icon: Package, color: 'from-success to-emerald-400' },
    { label: 'الحسابات', desc: 'المصروفات والدخل والأرباح', icon: Wallet, color: 'from-accent to-amber-400' },
    { label: 'توريدات الموردين', desc: 'الموردون والتوريدات', icon: Truck, color: 'from-primary to-cyan-400' },
    { label: 'ادارة المستخدمين', desc: 'المستخدمون والصلاحيات', icon: ShieldCheck, color: 'from-danger to-rose-400', route: '/user-management', adminOnly: true },
  ];
  readonly cards = computed<HomeCard[]>(() => {
    const isAdmin = this.auth.role() === 'admin';
    return this.allCards.filter((c) => !c.adminOnly || isAdmin);
  });

  go(route?: string): void {
    if (route) void this.router.navigate([route]);
  }
}
```

- [ ] **Step 4: Create the home template**

Create `frontend/src/app/features/home/presentation/pages/home.page.html` by porting `docs/references/electric-website-angular/src/app/features/home/home.html` with these adaptations:
- Greeting name: use `{{ principal()?.userId }}` (there is no name field on the principal).
- Iterate `cards()`; the card is a `<button>` — when `card.route` is set, `(click)="go(card.route)"` and normal styling; when absent, add `disabled`, `title="قريباً"`, `cursor-not-allowed opacity-60` and no click. Keep the gradient icon tile (`[class]="card.color"`), `card.label`, `card.desc`.

- [ ] **Step 5: Create the barrel**

Create `frontend/src/app/features/home/index.ts`:

```ts
export { HomePage } from './presentation/pages/home.page';
```

- [ ] **Step 6: Run the home spec + build**

Run: `npx jest src/app/features/home/presentation/pages/home.page.spec.ts` → PASS.
Run: `npm run build` → clean.

- [ ] **Step 7: Commit**

```bash
git add src/app/features/home/
git commit -m "feat(home): electric home hub — section cards, inert except إدارة المستخدمين

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: Wire the authenticated shell routing

Move the authenticated pages under the `LayoutComponent` shell and add `/home`. After this the app has a working login → shell → home flow.

**Files:** Modify `frontend/src/app/app.routes.ts`
**Test:** `frontend/src/app/app.routes.spec.ts` (new — verifies the route table shape)

**Interfaces:**
- Consumes: `LayoutComponent` (Task 4), `HomePage` (Task 5), `authGuard`, `roleGuard`.

- [ ] **Step 1: Write a failing route-table spec**

Create `frontend/src/app/app.routes.spec.ts`:

```ts
import { routes } from './app.routes';

describe('routes', () => {
  it('nests home + account + change-password under a guarded shell', () => {
    const shell = routes.find((r) => r.path === '' && !!r.children);
    expect(shell).toBeTruthy();
    expect(shell!.canActivate).toBeTruthy();
    const childPaths = (shell!.children ?? []).map((c) => c.path);
    expect(childPaths).toEqual(expect.arrayContaining(['home', 'account', 'change-password']));
  });

  it('keeps /login outside the shell', () => {
    expect(routes.some((r) => r.path === 'login' && !r.children)).toBe(true);
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npx jest src/app/app.routes.spec.ts`
Expected: FAIL — no shell route with children yet.

- [ ] **Step 3: Rewrite `app.routes.ts` with the shell**

Replace `frontend/src/app/app.routes.ts` with:

```ts
// routes: /login sits outside the shell; the authenticated area is nested under
// LayoutComponent behind authGuard. إدارة المستخدمين arrives in a later task.
import { Routes } from '@angular/router';
import { authGuard } from '@core/guards/auth.guard';
import { LayoutComponent } from '@core/ui/layout/layout.component';
import { LoginViewModel, ChangePasswordViewModel } from '@features/auth';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('@features/auth').then((m) => m.LoginPage), providers: [LoginViewModel] },
  {
    path: '',
    component: LayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: 'home', loadComponent: () => import('@features/home').then((m) => m.HomePage) },
      { path: 'account', loadComponent: () => import('@features/auth').then((m) => m.AccountPage) },
      {
        path: 'change-password',
        loadComponent: () => import('@features/auth').then((m) => m.ChangePasswordPage),
        providers: [ChangePasswordViewModel],
      },
      { path: '', pathMatch: 'full', redirectTo: 'home' },
    ],
  },
  { path: '**', redirectTo: '' },
];
```

- [ ] **Step 4: Run the route spec + full suite + build**

Run: `npx jest src/app/app.routes.spec.ts` → PASS.
Run: `npx jest` → all PASS.
Run: `npm run build` → clean (lazy chunks for LayoutComponent/HomePage appear).

- [ ] **Step 5: Commit**

```bash
git add src/app/app.routes.ts src/app/app.routes.spec.ts
git commit -m "feat(routing): nest home/account/change-password under the RTL shell behind authGuard

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: Restyle login / change-password / account + login redirect to /home

Restyle the three auth pages to the reference look, add the login shake-on-error, and land the post-login redirect on `/home`.

**Files:**
- Modify: `frontend/src/app/features/auth/presentation/pages/login.page.ts` (+ new `login.page.html`), `change-password.page.ts`, `account.page.ts`, `frontend/src/app/features/auth/presentation/viewmodels/login.viewmodel.ts`
- Test: `frontend/src/app/features/auth/presentation/viewmodels/login.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `DecorBackgroundComponent`, `@lucide/angular`, `AuthSessionStore`, `ROLE_LABELS`.
- Produces: `LoginViewModel.shakeKey: Signal<number>` (bumps on failed sign-in); login redirect `mustChangePassword ? '/change-password' : '/home'`.

- [ ] **Step 1: Update the login view-model spec (shake + /home redirect)**

In `frontend/src/app/features/auth/presentation/viewmodels/login.viewmodel.spec.ts`: change the success-case expectation from `['/account']` to `['/home']`; keep the `mustChangePassword` case at `['/change-password']`; and add:

```ts
  it('bumps shakeKey on failed sign-in', async () => {
    (auth.signIn as jest.Mock).mockResolvedValue(fail(new AppError('bad', 'auth', 401)));
    const before = vm.shakeKey();
    await vm.submit();
    expect(vm.shakeKey()).toBe(before + 1);
  });
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npx jest src/app/features/auth/presentation/viewmodels/login.viewmodel.spec.ts`
Expected: FAIL — redirect still `/account`; `shakeKey` undefined.

- [ ] **Step 3: Update the login view-model**

In `frontend/src/app/features/auth/presentation/viewmodels/login.viewmodel.ts`: add `readonly shakeKey = signal(0);`, change the success branch to `void this.router.navigate([r.data.mustChangePassword ? '/change-password' : '/home']);`, and in the failure branch add `this.shakeKey.update((k) => k + 1);` alongside `this.error.set(r.error.message)`.

- [ ] **Step 4: Run the login VM spec**

Run: `npx jest src/app/features/auth/presentation/viewmodels/login.viewmodel.spec.ts` → PASS.

- [ ] **Step 5: Restyle the login page**

Create `frontend/src/app/features/auth/presentation/pages/login.page.html` by porting `docs/references/electric-website-angular/src/app/features/login/login.html` with these adaptations:
- The demo-credentials block lists our real seeded backend accounts: `admin@kheprx.local / ChangeMe123!`, `manager@kheprx.local / ChangeMe123!`, `moqawel@kheprx.local / ChangeMe123!`, `worker@kheprx.local / ChangeMe123!` (use `&#64;` for `@`).
- `vm.error()` is `string | null` — the `@if (vm.error())` guards already handle it.

Replace `frontend/src/app/features/auth/presentation/pages/login.page.ts` with:

```ts
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucideAngularModule, Zap, LogIn, Mail, Lock, AlertCircle, Loader2 } from '@lucide/angular';
import { DecorBackgroundComponent } from '@core/ui/components/decor-background.component';
import { LoginViewModel } from '../viewmodels/login.viewmodel';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [FormsModule, LucideAngularModule, DecorBackgroundComponent],
  templateUrl: './login.page.html',
})
export class LoginPage {
  protected readonly vm = inject(LoginViewModel);
  readonly ZapIcon = Zap;
  readonly LogInIcon = LogIn;
  readonly MailIcon = Mail;
  readonly LockIcon = Lock;
  readonly AlertCircleIcon = AlertCircle;
  readonly Loader2Icon = Loader2;

  onKeyDown(e: KeyboardEvent): void {
    if (e.key === 'Enter') void this.vm.submit();
  }
}
```

- [ ] **Step 6: Restyle change-password + account**

Replace the `template` of `frontend/src/app/features/auth/presentation/pages/change-password.page.ts` so the form sits in a centered reference-style card (imports add `DecorBackgroundComponent`); keep all `vm.*` bindings and logic identical to the current page (currentPassword/newPassword/confirmPassword inputs, error `<p>`, submit button). Wrap:
```html
<div class="relative flex min-h-full w-full items-center justify-center p-4">
  <app-decor-background />
  <section class="glass-panel relative z-10 w-full max-w-md rounded-3xl p-8 shadow-xl">
    <h1 class="gradient-text mb-6 text-2xl font-extrabold">تغيير كلمة المرور</h1>
    <!-- existing three password inputs, error <p>, and submit button, restyled with
         the reference input classes: w-full rounded-xl border border-border bg-canvas px-4 py-3 -->
  </section>
</div>
```
Replace the `template` of `frontend/src/app/features/auth/presentation/pages/account.page.ts` to a reference-style profile card showing the principal via `ROLE_LABELS` (import `ROLE_LABELS` from `@core/auth/role-labels`), a `routerLink="/change-password"` "تغيير كلمة المرور" link, and a "تسجيل الخروج" button calling the existing `signOut()`; keep `RouterLink` in imports and the existing `AuthSessionStore` + `Router` wiring.

- [ ] **Step 7: Run auth specs + build**

Run: `npx jest src/app/features/auth` → PASS.
Run: `npm run build` → clean.

- [ ] **Step 8: Commit**

```bash
git add src/app/features/auth/
git commit -m "feat(auth-ui): restyle login/change-password/account to the reference look; shake on error; redirect to /home

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

# Phase B — إدارة المستخدمين

## Task 8: Users seam (types, port, mock, http, repository, providers)

Add the swap-ready users transport mirroring the auth seam. Mock is the default.

**Files:**
- Create: `frontend/src/app/core/users/users.types.ts`, `users-data-source.ts`, `mock-users.data-source.ts`, `http-users.data-source.ts`, `users.repository.interface.ts`, `users.repository.ts`, `users.repository.port.ts`, `users.providers.ts`
- Modify: `frontend/src/app/app.config.ts` (register `USERS_PROVIDERS` globally — like `AUTH_PROVIDERS`)
- Test: `frontend/src/app/core/users/mock-users.data-source.spec.ts`, `users.repository.spec.ts`

**Interfaces:**
- Produces:
  - `ManagedUser { id; fullName; email; role: UserRole; status: 'active'|'disabled'; mustChangePassword: boolean }`
  - `CreateUserInput { fullName; email; role: UserRole; password: string }`; `UpdateUserInput { fullName; email; status; password? }`
  - `UsersDataSource` / `IUsersRepository`: `list(): Promise<ManagedUser[]>`, `create(CreateUserInput): Promise<ManagedUser>`, `update(id, UpdateUserInput): Promise<ManagedUser>`, `setStatus(id, UserStatus): Promise<ManagedUser>`
  - Tokens `USERS_DATA_SOURCE`, `USERS_REPOSITORY`; `USERS_PROVIDERS`.

- [ ] **Step 1: Write the failing mock spec**

Create `frontend/src/app/core/users/mock-users.data-source.spec.ts`:

```ts
import { MockUsersDataSource } from '@core/users/mock-users.data-source';

describe('MockUsersDataSource', () => {
  let ds: MockUsersDataSource;
  beforeEach(() => { ds = new MockUsersDataSource(); });

  it('lists the 4 seeded accounts', async () => {
    const users = await ds.list();
    expect(users).toHaveLength(4);
    expect(users.map((u) => u.email)).toEqual(
      expect.arrayContaining(['admin@kheprx.local', 'manager@kheprx.local', 'moqawel@kheprx.local', 'worker@kheprx.local']),
    );
  });

  it('creates a user with a generated id and appends it', async () => {
    const u = await ds.create({ fullName: 'New Person', email: 'new@kheprx.local', role: 'worker', password: 'x' });
    expect(u.id).toMatch(/^USR-/);
    expect(u.status).toBe('active');
    expect(u.mustChangePassword).toBe(true);
    expect(await ds.list()).toHaveLength(5);
  });

  it('rejects a duplicate email with validation/409', async () => {
    await expect(
      ds.create({ fullName: 'Dup', email: 'admin@kheprx.local', role: 'manager', password: 'x' }),
    ).rejects.toMatchObject({ kind: 'validation', status: 409 });
  });

  it('updates fields and toggles status', async () => {
    const updated = await ds.update('USR-WORKER', { fullName: 'Renamed', email: 'worker@kheprx.local', status: 'disabled' });
    expect(updated.fullName).toBe('Renamed');
    expect(updated.status).toBe('disabled');
    const back = await ds.setStatus('USR-WORKER', 'active');
    expect(back.status).toBe('active');
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npx jest src/app/core/users/mock-users.data-source.spec.ts`
Expected: FAIL — module not found.

- [ ] **Step 3: Create the types + port**

Create `frontend/src/app/core/users/users.types.ts`:

```ts
import { UserRole } from '@core/auth/auth.types';

export type UserStatus = 'active' | 'disabled';
export interface ManagedUser { id: string; fullName: string; email: string; role: UserRole; status: UserStatus; mustChangePassword: boolean; }
export interface CreateUserInput { fullName: string; email: string; role: UserRole; password: string; }
export interface UpdateUserInput { fullName: string; email: string; status: UserStatus; password?: string; }
```

Create `frontend/src/app/core/users/users-data-source.ts`:

```ts
import { InjectionToken } from '@angular/core';
import { ManagedUser, CreateUserInput, UpdateUserInput, UserStatus } from '@core/users/users.types';

export interface UsersDataSource {
  list(): Promise<ManagedUser[]>;
  create(input: CreateUserInput): Promise<ManagedUser>;
  update(id: string, input: UpdateUserInput): Promise<ManagedUser>;
  setStatus(id: string, status: UserStatus): Promise<ManagedUser>;
}

export const USERS_DATA_SOURCE = new InjectionToken<UsersDataSource>('USERS_DATA_SOURCE');
```

- [ ] **Step 4: Create the mock + http data sources**

Create `frontend/src/app/core/users/mock-users.data-source.ts`:

```ts
import { Injectable } from '@angular/core';
import { AppError } from '@core/domain/errors/app-error';
import { UsersDataSource } from '@core/users/users-data-source';
import { ManagedUser, CreateUserInput, UpdateUserInput, UserStatus } from '@core/users/users.types';

const SEED: ManagedUser[] = [
  { id: 'USR-ADMIN', fullName: 'admin (seed)', email: 'admin@kheprx.local', role: 'admin', status: 'active', mustChangePassword: true },
  { id: 'USR-MANAGER', fullName: 'manager (seed)', email: 'manager@kheprx.local', role: 'manager', status: 'active', mustChangePassword: true },
  { id: 'USR-MOQAWEL', fullName: 'moqawel (seed)', email: 'moqawel@kheprx.local', role: 'moqawel', status: 'active', mustChangePassword: true },
  { id: 'USR-WORKER', fullName: 'worker (seed)', email: 'worker@kheprx.local', role: 'worker', status: 'active', mustChangePassword: true },
];

@Injectable({ providedIn: 'root' })
export class MockUsersDataSource implements UsersDataSource {
  private users: ManagedUser[] = SEED.map((u) => ({ ...u }));
  private seq = 0;

  async list(): Promise<ManagedUser[]> {
    await Promise.resolve();
    return this.users.map((u) => ({ ...u }));
  }

  async create(input: CreateUserInput): Promise<ManagedUser> {
    await Promise.resolve();
    const email = input.email.trim().toLowerCase();
    if (this.users.some((u) => u.email.toLowerCase() === email)) {
      throw new AppError('البريد مستخدم بالفعل', 'validation', 409);
    }
    const user: ManagedUser = {
      id: `USR-${++this.seq}`, fullName: input.fullName.trim(), email: input.email.trim(),
      role: input.role, status: 'active', mustChangePassword: true,
    };
    this.users.push(user);
    return { ...user };
  }

  async update(id: string, input: UpdateUserInput): Promise<ManagedUser> {
    await Promise.resolve();
    const u = this.users.find((x) => x.id === id);
    if (!u) throw new AppError('المستخدم غير موجود', 'validation', 404);
    u.fullName = input.fullName.trim();
    u.email = input.email.trim();
    u.status = input.status;
    return { ...u };
  }

  async setStatus(id: string, status: UserStatus): Promise<ManagedUser> {
    await Promise.resolve();
    const u = this.users.find((x) => x.id === id);
    if (!u) throw new AppError('المستخدم غير موجود', 'validation', 404);
    u.status = status;
    return { ...u };
  }
}
```

Create `frontend/src/app/core/users/http-users.data-source.ts` (built-not-wired):

```ts
// HttpUsersDataSource: real users transport (targets /api/users). Built but NOT bound
// by default — swap USERS_DATA_SOURCE to this once the backend exposes /api/users.
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { BaseResponseRs } from '@core/network/api/base-response-rs';
import { UsersDataSource } from '@core/users/users-data-source';
import { ManagedUser, CreateUserInput, UpdateUserInput, UserStatus } from '@core/users/users.types';

@Injectable({ providedIn: 'root' })
export class HttpUsersDataSource implements UsersDataSource {
  private readonly http = inject(HttpClientService);
  async list(): Promise<ManagedUser[]> {
    const res = await this.http.get<BaseResponseRs<ManagedUser[]>>('/api/users');
    return res.data;
  }
  async create(input: CreateUserInput): Promise<ManagedUser> {
    const res = await this.http.post<BaseResponseRs<ManagedUser>>('/api/users', { body: input });
    return res.data;
  }
  async update(id: string, input: UpdateUserInput): Promise<ManagedUser> {
    const res = await this.http.patch<BaseResponseRs<ManagedUser>>(`/api/users/${id}`, { body: input });
    return res.data;
  }
  async setStatus(id: string, status: UserStatus): Promise<ManagedUser> {
    const res = await this.http.patch<BaseResponseRs<ManagedUser>>(`/api/users/${id}/status`, { body: { status } });
    return res.data;
  }
}
```

- [ ] **Step 5: Create the repository + port + providers, with a delegation spec**

Create `frontend/src/app/core/users/users.repository.interface.ts`:

```ts
import { ManagedUser, CreateUserInput, UpdateUserInput, UserStatus } from '@core/users/users.types';

export interface IUsersRepository {
  list(): Promise<ManagedUser[]>;
  create(input: CreateUserInput): Promise<ManagedUser>;
  update(id: string, input: UpdateUserInput): Promise<ManagedUser>;
  setStatus(id: string, status: UserStatus): Promise<ManagedUser>;
}
```

Create `frontend/src/app/core/users/users.repository.ts`:

```ts
import { Injectable, inject } from '@angular/core';
import { IUsersRepository } from '@core/users/users.repository.interface';
import { USERS_DATA_SOURCE } from '@core/users/users-data-source';
import { ManagedUser, CreateUserInput, UpdateUserInput, UserStatus } from '@core/users/users.types';

@Injectable({ providedIn: 'root' })
export class UsersRepository implements IUsersRepository {
  private readonly api = inject(USERS_DATA_SOURCE);
  list(): Promise<ManagedUser[]> { return this.api.list(); }
  create(input: CreateUserInput): Promise<ManagedUser> { return this.api.create(input); }
  update(id: string, input: UpdateUserInput): Promise<ManagedUser> { return this.api.update(id, input); }
  setStatus(id: string, status: UserStatus): Promise<ManagedUser> { return this.api.setStatus(id, status); }
}
```

Create `frontend/src/app/core/users/users.repository.port.ts`:

```ts
import { InjectionToken, inject } from '@angular/core';
import { IUsersRepository } from '@core/users/users.repository.interface';
import { UsersRepository } from '@core/users/users.repository';

export type { IUsersRepository };

export const USERS_REPOSITORY = new InjectionToken<IUsersRepository>('USERS_REPOSITORY', {
  providedIn: 'root',
  factory: () => inject(UsersRepository),
});
```

Create `frontend/src/app/core/users/users.providers.ts`:

```ts
import { Provider } from '@angular/core';
import { USERS_DATA_SOURCE } from '@core/users/users-data-source';
import { USERS_REPOSITORY } from '@core/users/users.repository.port';
import { MockUsersDataSource } from '@core/users/mock-users.data-source';
import { UsersRepository } from '@core/users/users.repository';

// Default wiring: seed-backed mock users. To go live, swap MockUsersDataSource →
// HttpUsersDataSource once the backend exposes /api/users.
export const USERS_PROVIDERS: Provider[] = [
  { provide: USERS_DATA_SOURCE, useClass: MockUsersDataSource },
  { provide: USERS_REPOSITORY, useClass: UsersRepository },
];
```

Create `frontend/src/app/core/users/users.repository.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { UsersRepository } from '@core/users/users.repository';
import { USERS_DATA_SOURCE, UsersDataSource } from '@core/users/users-data-source';
import { ManagedUser } from '@core/users/users.types';

const sample: ManagedUser = { id: 'USR-1', fullName: 'A', email: 'a@b.c', role: 'worker', status: 'active', mustChangePassword: false };

describe('UsersRepository', () => {
  it('delegates list to the data source', async () => {
    const api = { list: async () => [sample], create: jest.fn(), update: jest.fn(), setStatus: jest.fn() } as unknown as UsersDataSource;
    TestBed.configureTestingModule({ providers: [UsersRepository, { provide: USERS_DATA_SOURCE, useValue: api }] });
    await expect(TestBed.inject(UsersRepository).list()).resolves.toEqual([sample]);
  });
});
```

- [ ] **Step 5b: Register `USERS_PROVIDERS` globally (so the root `UsersRepository` can resolve `USERS_DATA_SOURCE`)**

In `frontend/src/app/app.config.ts` add the import and spread `USERS_PROVIDERS` alongside `AUTH_PROVIDERS`:
```ts
import { USERS_PROVIDERS } from '@core/users/users.providers';
// ...in the providers array, after ...AUTH_PROVIDERS:
    ...USERS_PROVIDERS,
```
This mirrors the auth seam (both `USERS_DATA_SOURCE` and `USERS_REPOSITORY` live at the root injector, so the root-provided use-cases resolve correctly; the route only scopes the view-model).

- [ ] **Step 6: Run the users-core specs + build**

Run: `npx jest src/app/core/users` → PASS.
Run: `npm run build` → clean.

- [ ] **Step 7: Commit**

```bash
git add src/app/core/users/ src/app/app.config.ts
git commit -m "feat(users): swap-ready users seam (port + MockUsersDataSource default + Http built-not-wired + repository)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 9: Users use-cases

Four use-cases over the users repository, each returning `Result<...>` via the `UseCase` base.

**Files:**
- Create: `frontend/src/app/core/users/usecases/{list-users,create-user,update-user,set-user-status}.use-case.ts`
- Test: `frontend/src/app/core/users/usecases/users.use-cases.spec.ts`

**Interfaces:**
- Consumes: `USERS_REPOSITORY` (Task 8).
- Produces: `ListUsersUseCase.run()`; `CreateUserUseCase.run(CreateUserInput)`; `UpdateUserUseCase.run({id,input:UpdateUserInput})`; `SetUserStatusUseCase.run({id,status})` — all `Promise<Result<...>>`.

- [ ] **Step 1: Write the failing use-cases spec**

Create `frontend/src/app/core/users/usecases/users.use-cases.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { ListUsersUseCase } from '@core/users/usecases/list-users.use-case';
import { CreateUserUseCase } from '@core/users/usecases/create-user.use-case';
import { UpdateUserUseCase } from '@core/users/usecases/update-user.use-case';
import { SetUserStatusUseCase } from '@core/users/usecases/set-user-status.use-case';
import { USERS_REPOSITORY, IUsersRepository } from '@core/users/users.repository.port';
import { ManagedUser } from '@core/users/users.types';
import { AppError } from '@core/domain/errors/app-error';

const u: ManagedUser = { id: 'USR-1', fullName: 'A', email: 'a@b.c', role: 'worker', status: 'active', mustChangePassword: false };

function repo(overrides: Partial<IUsersRepository> = {}): IUsersRepository {
  return { list: async () => [u], create: async () => u, update: async () => u, setStatus: async () => ({ ...u, status: 'disabled' }), ...overrides } as IUsersRepository;
}
function configure(r: IUsersRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: USERS_REPOSITORY, useValue: r }] });
}

describe('users use-cases', () => {
  it('ListUsersUseCase returns the list', async () => {
    configure(repo());
    const res = await TestBed.inject(ListUsersUseCase).run();
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data).toEqual([u]);
  });

  it('CreateUserUseCase returns ok on success', async () => {
    configure(repo());
    const res = await TestBed.inject(CreateUserUseCase).run({ fullName: 'A', email: 'a@b.c', role: 'worker', password: 'x' });
    expect(res.ok).toBe(true);
  });

  it('CreateUserUseCase returns fail(validation) on duplicate email', async () => {
    configure(repo({ create: async () => { throw new AppError('البريد مستخدم بالفعل', 'validation', 409); } }));
    const res = await TestBed.inject(CreateUserUseCase).run({ fullName: 'A', email: 'a@b.c', role: 'worker', password: 'x' });
    expect(res.ok).toBe(false);
    if (!res.ok) expect(res.error.status).toBe(409);
  });

  it('UpdateUserUseCase forwards id + input', async () => {
    const update = jest.fn().mockResolvedValue(u);
    configure(repo({ update }));
    await TestBed.inject(UpdateUserUseCase).run({ id: 'USR-1', input: { fullName: 'B', email: 'a@b.c', status: 'active' } });
    expect(update).toHaveBeenCalledWith('USR-1', { fullName: 'B', email: 'a@b.c', status: 'active' });
  });

  it('SetUserStatusUseCase forwards id + status', async () => {
    configure(repo());
    const res = await TestBed.inject(SetUserStatusUseCase).run({ id: 'USR-1', status: 'disabled' });
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data.status).toBe('disabled');
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npx jest src/app/core/users/usecases/users.use-cases.spec.ts`
Expected: FAIL — use-cases don't exist.

- [ ] **Step 3: Create the four use-cases**

Create `frontend/src/app/core/users/usecases/list-users.use-case.ts`:

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { USERS_REPOSITORY } from '@core/users/users.repository.port';
import { ManagedUser } from '@core/users/users.types';

@Injectable({ providedIn: 'root' })
export class ListUsersUseCase extends UseCase<void, ManagedUser[]> {
  private readonly repo = inject(USERS_REPOSITORY);
  constructor() { super('ListUsers'); }
  protected execute(): Promise<ManagedUser[]> { return this.repo.list(); }
}
```

Create `frontend/src/app/core/users/usecases/create-user.use-case.ts`:

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { USERS_REPOSITORY } from '@core/users/users.repository.port';
import { ManagedUser, CreateUserInput } from '@core/users/users.types';

@Injectable({ providedIn: 'root' })
export class CreateUserUseCase extends UseCase<CreateUserInput, ManagedUser> {
  private readonly repo = inject(USERS_REPOSITORY);
  constructor() { super('CreateUser'); }
  protected execute(input: CreateUserInput): Promise<ManagedUser> { return this.repo.create(input); }
}
```

Create `frontend/src/app/core/users/usecases/update-user.use-case.ts`:

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { USERS_REPOSITORY } from '@core/users/users.repository.port';
import { ManagedUser, UpdateUserInput } from '@core/users/users.types';

export interface UpdateUserArgs { id: string; input: UpdateUserInput; }

@Injectable({ providedIn: 'root' })
export class UpdateUserUseCase extends UseCase<UpdateUserArgs, ManagedUser> {
  private readonly repo = inject(USERS_REPOSITORY);
  constructor() { super('UpdateUser'); }
  protected execute({ id, input }: UpdateUserArgs): Promise<ManagedUser> { return this.repo.update(id, input); }
}
```

Create `frontend/src/app/core/users/usecases/set-user-status.use-case.ts`:

```ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { USERS_REPOSITORY } from '@core/users/users.repository.port';
import { ManagedUser, UserStatus } from '@core/users/users.types';

export interface SetUserStatusArgs { id: string; status: UserStatus; }

@Injectable({ providedIn: 'root' })
export class SetUserStatusUseCase extends UseCase<SetUserStatusArgs, ManagedUser> {
  private readonly repo = inject(USERS_REPOSITORY);
  constructor() { super('SetUserStatus'); }
  protected execute({ id, status }: SetUserStatusArgs): Promise<ManagedUser> { return this.repo.setStatus(id, status); }
}
```

- [ ] **Step 4: Run the spec + build**

Run: `npx jest src/app/core/users/usecases/users.use-cases.spec.ts` → PASS.
Run: `npm run build` → clean.

- [ ] **Step 5: Commit**

```bash
git add src/app/core/users/usecases/
git commit -m "feat(users): list/create/update/set-status use-cases

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 10: UsersViewModel

The presentation facade for إدارة المستخدمين: list + filters + add/edit modal, with toasts via `NotificationService`.

**Files:**
- Create: `frontend/src/app/features/user-management/presentation/viewmodels/users.viewmodel.ts`
- Test: `frontend/src/app/features/user-management/presentation/viewmodels/users.viewmodel.spec.ts`

**Interfaces:**
- Consumes: `ListUsersUseCase`, `CreateUserUseCase`, `UpdateUserUseCase`, `SetUserStatusUseCase`, `NotificationService` (`success`/`error`), `ManagedUser`/`UserRole`/`UserStatus`.
- Produces: `UsersViewModel` signals `users`, `loading`, `search`, `filterRole`, `filterStatus`, `filtered` (computed), modal signals (`modalOpen`, `editingId`, `formFullName`, `formEmail`, `formRole`, `formPassword`, `formStatus`); methods `load()`, `openAdd()`, `openEdit(u)`, `closeModal()`, `save()`, `toggleStatus(u)`.

- [ ] **Step 1: Write the failing view-model spec**

Create `frontend/src/app/features/user-management/presentation/viewmodels/users.viewmodel.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { UsersViewModel } from './users.viewmodel';
import { ListUsersUseCase } from '@core/users/usecases/list-users.use-case';
import { CreateUserUseCase } from '@core/users/usecases/create-user.use-case';
import { UpdateUserUseCase } from '@core/users/usecases/update-user.use-case';
import { SetUserStatusUseCase } from '@core/users/usecases/set-user-status.use-case';
import { NotificationService } from '@core/ui/notification.service';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';
import { ManagedUser } from '@core/users/users.types';

const admin: ManagedUser = { id: 'USR-ADMIN', fullName: 'admin (seed)', email: 'admin@kheprx.local', role: 'admin', status: 'active', mustChangePassword: true };
const worker: ManagedUser = { id: 'USR-WORKER', fullName: 'worker (seed)', email: 'worker@kheprx.local', role: 'worker', status: 'disabled', mustChangePassword: true };

describe('UsersViewModel', () => {
  const list = { run: jest.fn() };
  const create = { run: jest.fn() };
  const update = { run: jest.fn() };
  const setStatus = { run: jest.fn() };
  const notify = { success: jest.fn(), error: jest.fn() };
  let vm: UsersViewModel;

  beforeEach(() => {
    jest.clearAllMocks();
    list.run.mockResolvedValue(ok([admin, worker]));
    TestBed.configureTestingModule({
      providers: [
        UsersViewModel,
        { provide: ListUsersUseCase, useValue: list },
        { provide: CreateUserUseCase, useValue: create },
        { provide: UpdateUserUseCase, useValue: update },
        { provide: SetUserStatusUseCase, useValue: setStatus },
        { provide: NotificationService, useValue: notify },
      ],
    });
    vm = TestBed.inject(UsersViewModel);
  });

  it('load() populates users', async () => {
    await vm.load();
    expect(vm.users()).toHaveLength(2);
  });

  it('filters by search, role, and status', async () => {
    await vm.load();
    vm.search.set('worker'); expect(vm.filtered().map((u) => u.id)).toEqual(['USR-WORKER']);
    vm.search.set(''); vm.filterRole.set('admin'); expect(vm.filtered().map((u) => u.id)).toEqual(['USR-ADMIN']);
    vm.filterRole.set(''); vm.filterStatus.set('disabled'); expect(vm.filtered().map((u) => u.id)).toEqual(['USR-WORKER']);
  });

  it('save() in add mode validates name, then creates + reloads + toasts', async () => {
    await vm.load();
    vm.openAdd();
    vm.formFullName.set(''); await vm.save();
    expect(create.run).not.toHaveBeenCalled();
    expect(notify.error).toHaveBeenCalled();

    create.run.mockResolvedValue(ok(admin));
    vm.formFullName.set('New'); vm.formEmail.set('new@kheprx.local'); vm.formRole.set('manager'); vm.formPassword.set('pw');
    await vm.save();
    expect(create.run).toHaveBeenCalledWith({ fullName: 'New', email: 'new@kheprx.local', role: 'manager', password: 'pw' });
    expect(notify.success).toHaveBeenCalled();
    expect(vm.modalOpen()).toBe(false);
    expect(list.run).toHaveBeenCalledTimes(2); // initial + reload
  });

  it('save() surfaces a create failure as an error toast and keeps the modal open', async () => {
    await vm.load();
    vm.openAdd();
    vm.formFullName.set('Dup'); vm.formEmail.set('admin@kheprx.local'); vm.formPassword.set('x');
    create.run.mockResolvedValue(fail(new AppError('البريد مستخدم بالفعل', 'validation', 409)));
    await vm.save();
    expect(notify.error).toHaveBeenCalledWith('البريد مستخدم بالفعل');
    expect(vm.modalOpen()).toBe(true);
  });

  it('openEdit() then save() calls update with the id', async () => {
    await vm.load();
    vm.openEdit(worker);
    expect(vm.editingId()).toBe('USR-WORKER');
    update.run.mockResolvedValue(ok(worker));
    vm.formFullName.set('Renamed');
    await vm.save();
    expect(update.run).toHaveBeenCalledWith({ id: 'USR-WORKER', input: { fullName: 'Renamed', email: 'worker@kheprx.local', status: 'disabled' } });
  });

  it('toggleStatus() flips active/disabled and reloads', async () => {
    await vm.load();
    setStatus.run.mockResolvedValue(ok({ ...admin, status: 'disabled' }));
    await vm.toggleStatus(admin);
    expect(setStatus.run).toHaveBeenCalledWith({ id: 'USR-ADMIN', status: 'disabled' });
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npx jest src/app/features/user-management/presentation/viewmodels/users.viewmodel.spec.ts`
Expected: FAIL — `UsersViewModel` does not exist.

- [ ] **Step 3: Create the view-model**

Create `frontend/src/app/features/user-management/presentation/viewmodels/users.viewmodel.ts`:

```ts
import { Injectable, computed, inject, signal } from '@angular/core';
import { ListUsersUseCase } from '@core/users/usecases/list-users.use-case';
import { CreateUserUseCase } from '@core/users/usecases/create-user.use-case';
import { UpdateUserUseCase } from '@core/users/usecases/update-user.use-case';
import { SetUserStatusUseCase } from '@core/users/usecases/set-user-status.use-case';
import { NotificationService } from '@core/ui/notification.service';
import { ManagedUser, UserStatus } from '@core/users/users.types';
import { UserRole } from '@core/auth/auth.types';

@Injectable()
export class UsersViewModel {
  private readonly listUsers = inject(ListUsersUseCase);
  private readonly createUser = inject(CreateUserUseCase);
  private readonly updateUser = inject(UpdateUserUseCase);
  private readonly setUserStatus = inject(SetUserStatusUseCase);
  private readonly notify = inject(NotificationService);

  readonly users = signal<ManagedUser[]>([]);
  readonly loading = signal(false);
  readonly search = signal('');
  readonly filterRole = signal<'' | UserRole>('');
  readonly filterStatus = signal<'' | UserStatus>('');

  readonly modalOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly formFullName = signal('');
  readonly formEmail = signal('');
  readonly formRole = signal<UserRole>('manager');
  readonly formPassword = signal('');
  readonly formStatus = signal<UserStatus>('active');

  readonly filtered = computed(() => {
    const q = this.search().trim();
    const fr = this.filterRole();
    const fs = this.filterStatus();
    return this.users().filter((u) => {
      if (fr && u.role !== fr) return false;
      if (fs && u.status !== fs) return false;
      if (q && !u.fullName.includes(q) && !u.email.includes(q)) return false;
      return true;
    });
  });

  async load(): Promise<void> {
    this.loading.set(true);
    const r = await this.listUsers.run();
    this.loading.set(false);
    if (r.ok) this.users.set(r.data);
    else this.notify.error(r.error.message);
  }

  openAdd(): void {
    this.editingId.set(null);
    this.formFullName.set(''); this.formEmail.set(''); this.formRole.set('manager');
    this.formPassword.set(''); this.formStatus.set('active');
    this.modalOpen.set(true);
  }

  openEdit(u: ManagedUser): void {
    this.editingId.set(u.id);
    this.formFullName.set(u.fullName); this.formEmail.set(u.email); this.formRole.set(u.role);
    this.formPassword.set(''); this.formStatus.set(u.status);
    this.modalOpen.set(true);
  }

  closeModal(): void { this.modalOpen.set(false); }

  async save(): Promise<void> {
    if (!this.formFullName().trim()) { this.notify.error('الاسم مطلوب'); return; }
    const id = this.editingId();
    if (id) {
      const r = await this.updateUser.run({
        id,
        input: { fullName: this.formFullName(), email: this.formEmail(), status: this.formStatus() },
      });
      if (!r.ok) { this.notify.error(r.error.message); return; }
      this.notify.success('تم تحديث المستخدم');
    } else {
      const r = await this.createUser.run({
        fullName: this.formFullName(), email: this.formEmail(), role: this.formRole(), password: this.formPassword(),
      });
      if (!r.ok) { this.notify.error(r.error.message); return; }
      this.notify.success('تمت إضافة المستخدم');
    }
    this.modalOpen.set(false);
    await this.load();
  }

  async toggleStatus(u: ManagedUser): Promise<void> {
    const next: UserStatus = u.status === 'active' ? 'disabled' : 'active';
    const r = await this.setUserStatus.run({ id: u.id, status: next });
    if (!r.ok) { this.notify.error(r.error.message); return; }
    this.notify.success(next === 'active' ? 'تم التفعيل' : 'تم التعطيل');
    await this.load();
  }
}
```

- [ ] **Step 4: Run the VM spec + build**

Run: `npx jest src/app/features/user-management/presentation/viewmodels/users.viewmodel.spec.ts` → PASS.
Run: `npm run build` → clean.

- [ ] **Step 5: Commit**

```bash
git add src/app/features/user-management/presentation/viewmodels/
git commit -m "feat(users): UsersViewModel (list, filters, add/edit modal, status toggle, toasts)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 11: UserManagementPage + route

The إدارة المستخدمين screen (reference layout bound to our fields) and its `/user-management` route under the shell, admin-guarded.

**Files:**
- Create: `frontend/src/app/features/user-management/presentation/pages/user-management.page.ts`, `user-management.page.html`, `frontend/src/app/features/user-management/index.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Test: `frontend/src/app/app.routes.spec.ts` (extend)

**Interfaces:**
- Consumes: `UsersViewModel` (route-scoped), `ROLE_LABELS`, `DecorBackgroundComponent`, `@lucide/angular`, `USERS_PROVIDERS`, use-cases.
- Produces: `UserManagementPage` (selector `app-user-management-page`); route `/user-management` (`roleGuard('admin')`).

- [ ] **Step 1: Create the page class**

Create `frontend/src/app/features/user-management/presentation/pages/user-management.page.ts`:

```ts
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucideAngularModule, Plus, Search, Pencil, Power, X } from '@lucide/angular';
import { UsersViewModel } from '../viewmodels/users.viewmodel';
import { DecorBackgroundComponent } from '@core/ui/components/decor-background.component';
import { ROLE_LABELS } from '@core/auth/role-labels';

@Component({
  selector: 'app-user-management-page',
  standalone: true,
  imports: [FormsModule, LucideAngularModule, DecorBackgroundComponent],
  templateUrl: './user-management.page.html',
})
export class UserManagementPage implements OnInit {
  protected readonly vm = inject(UsersViewModel);
  readonly ROLE_LABELS = ROLE_LABELS;
  readonly PlusIcon = Plus;
  readonly SearchIcon = Search;
  readonly PencilIcon = Pencil;
  readonly PowerIcon = Power;
  readonly XIcon = X;

  ngOnInit(): void { void this.vm.load(); }
}
```

- [ ] **Step 2: Create the page template**

Create `frontend/src/app/features/user-management/presentation/pages/user-management.page.html` by porting `docs/references/electric-website-angular/src/app/features/user-management/user-management.html` with these adaptations:
- **Drop** the `role() !== 'owner'` forbidden wrapper (the route is already `roleGuard('admin')`); render the management view directly.
- Bind to `vm.*`: `vm.search`, `vm.filterRole` (options: `''`,`admin`,`manager`,`moqawel`,`worker` via `ROLE_LABELS`), `vm.filterStatus` (`''`,`active`,`disabled`), header count `vm.filtered().length`, "إضافة مستخدم" → `vm.openAdd()`.
- **Table columns** (replace the ERP columns): الاسم (`u.fullName`), البريد (`u.email`, `dir="ltr"`), الدور (`ROLE_LABELS[u.role]`), الحالة (badge: `u.status === 'active'` → success "نشط" else danger "غير نشط"), الإجراءات → edit button `(click)="vm.openEdit(u)"` (Pencil) + status-toggle button `(click)="vm.toggleStatus(u)"` (Power). Iterate `vm.filtered()`; empty state "لا يوجد مستخدمون مطابقون".
- **Modal** (`@if (vm.modalOpen())`): title `vm.editingId() ? 'تعديل مستخدم' : 'إضافة مستخدم'`; fields — الاسم (`vm.formFullName`), البريد (`vm.formEmail`, `dir="ltr"`), الدور (`vm.formRole` select of the 4 roles, `[disabled]="!!vm.editingId()"`), كلمة المرور (`vm.formPassword`, `type="password"`, shown only when `!vm.editingId()`), الحالة (`vm.formStatus` select active/disabled, shown only when `vm.editingId()`). Footer: حفظ/إضافة → `vm.save()`, إلغاء → `vm.closeModal()`. Remove all ERP-only fields (الرقم القومي/الجنس/العمر/الراتب/اليومية).

- [ ] **Step 3: Create the barrel**

Create `frontend/src/app/features/user-management/index.ts`:

```ts
export { UserManagementPage } from './presentation/pages/user-management.page';
export { UsersViewModel } from './presentation/viewmodels/users.viewmodel';
```

- [ ] **Step 4: Add the guarded route (extend the route spec first)**

In `frontend/src/app/app.routes.spec.ts` add:

```ts
  it('adds a guarded /user-management child', () => {
    const shell = routes.find((r) => r.path === '' && !!r.children)!;
    const um = (shell.children ?? []).find((c) => c.path === 'user-management');
    expect(um).toBeTruthy();
    expect(um!.canActivate).toBeTruthy();
    expect(um!.providers).toBeTruthy();
  });
```
Run: `npx jest src/app/app.routes.spec.ts` → FAIL (no user-management child yet).

Then in `frontend/src/app/app.routes.ts` update the guards import to include `roleGuard` and add the view-model import:
```ts
import { authGuard, roleGuard } from '@core/guards/auth.guard';
import { UsersViewModel } from '@features/user-management';
```
and add this child route to the shell's `children` array (after `change-password`, before the `''` redirect). The users seam + use-cases are registered globally (Task 8, Step 5b) and are `providedIn: 'root'`, so the route only needs to scope the view-model:
```ts
      {
        path: 'user-management',
        canActivate: [roleGuard('admin')],
        loadComponent: () => import('@features/user-management').then((m) => m.UserManagementPage),
        providers: [UsersViewModel],
      },
```

- [ ] **Step 5: Run the route spec + full suite + build**

Run: `npx jest src/app/app.routes.spec.ts` → PASS.
Run: `npx jest` → all PASS.
Run: `npm run build` → clean (user-management lazy chunk appears).

- [ ] **Step 6: Commit**

```bash
git add src/app/features/user-management/ src/app/app.routes.ts src/app/app.routes.spec.ts
git commit -m "feat(users): إدارة المستخدمين page + guarded /user-management route (reference layout, our fields)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 12: Toast restyle + README + final verification

Restyle the notification host to the reference toast look, refresh the README, and run the full gate.

**Files:**
- Modify: `frontend/src/app/core/ui/components/notification-host.component.ts`, `frontend/README.md`

- [ ] **Step 1: Restyle the notification host**

Replace the `template` + `styles` of `frontend/src/app/core/ui/components/notification-host.component.ts` with a reference-style toast (keep the `NotificationService` wiring + `@if (notifications.current(); as n)`):

```ts
  template: `
    @if (notifications.current(); as n) {
      <div
        class="modal-panel fixed bottom-6 left-1/2 z-[60] -translate-x-1/2 rounded-xl px-5 py-3 text-sm font-semibold shadow-xl cursor-pointer"
        [class]="n.kind === 'error' ? 'bg-danger-bg text-danger border border-danger/30' : 'bg-success-bg text-success border border-success/30'"
        (click)="notifications.clear()">
        {{ n.message }}
      </div>
    }
  `,
  styles: [],
```
(Remove the old `styles` block that used `var(--color-*)`.)

- [ ] **Step 2: Update the README**

In `frontend/README.md`: update the intro to describe the النور كهرباء site (RTL Arabic, login → home hub → إدارة المستخدمين); replace the demo "offline mock posts" and old demo-user table with the real seeded accounts (`{role}@kheprx.local` / `ChangeMe123!`, forced change on first login); note إدارة المستخدمين runs on the swap-ready Mock users seam (`USERS_DATA_SOURCE` → `MockUsersDataSource`; swap to `HttpUsersDataSource` when `/api/users` exists). Keep the auth go-live section.

- [ ] **Step 3: Full verification gate**

Run: `npx jest` → entire suite green.
Run: `npm run build` → clean production build.
Manual smoke (optional): `npm start`, sign in (`admin@kheprx.local` / `ChangeMe123!` against the running backend, or a mock account if reverted to mock auth) → forced change → `/home` hub → click إدارة المستخدمين → list/add/edit/toggle a user → sign out.

- [ ] **Step 4: Commit**

```bash
git add src/app/core/ui/components/notification-host.component.ts README.md
git commit -m "feat(ui): reference-style toast + README refresh for the electric site

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Final Verification

- [ ] `npx jest` — entire suite green (new: decor-background, layout, home, routes, mock-users, users-repository, users use-cases, UsersViewModel; updated: login VM).
- [ ] `npm run build` — clean.
- [ ] Default bindings unchanged: `AUTH_DATA_SOURCE → MockAuthDataSource`, `USERS_DATA_SOURCE → MockUsersDataSource`.
- [ ] App is RTL Arabic; demo scaffold features gone; only Home + إدارة المستخدمين (+ auth pages) reachable; other sidebar/home items inert.

## Non-goals (do not implement)

- Other reference screens (dashboard/projects/tasks/inventory/finance/procurement/etc.) — inert.
- Backend `/api/users`; any change to Phase-1 auth logic or the default auth transport.
