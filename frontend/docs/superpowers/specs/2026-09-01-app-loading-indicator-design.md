# App-Wide Loading Indicator & Busy States — Design

**Date:** 2026-09-01
**Status:** Approved design (pending spec review)
**Scope:** Frontend (Angular 20 app under `frontend/`)

## 1. Problem

API-bound interactions give no consistent feedback:

- On the Settings/Account page, while the profile loads (`GET /api/auth/me`), the
  detail grid (phone/age/gender/National ID) simply disappears until the response
  arrives — no skeleton, no spinner — and the Language/Theme/Change-password
  controls stay clickable the whole time.
- There is no app-wide cue that a request is in flight. Only the change-password
  button shows its own inline spinner.

**Goal:** a professional, app-wide loading system so every API call surfaces a
consistent indicator, data-loading content shows skeletons, and buttons are
disabled until the relevant response returns.

## 2. Approach (chosen: "Layered loading")

Three coordinated pieces, each at the right altitude:

1. **Global backbone** — an HTTP interceptor tracks in-flight requests through a
   signal-based `LoadingService`.
2. **Global indicator** — a thin top progress bar, mounted once at the app root,
   visible whenever any (non-silent) request is in flight.
3. **Content + action states** — a reusable skeleton primitive for data-loading
   content (profile card first), and a documented convention for disabling
   action buttons while their request is in flight.

Rejected alternatives: a full-screen blocking overlay (janky; freezes UI on every
background request) and a purely page-level solution (not app-wide; every new page
re-implements it).

## 3. Existing patterns this builds on

- **Functional HTTP interceptors** registered in `app.config.ts`
  (`provideHttpClient(withInterceptors([authInterceptor]))`).
- **Signal-based global service + host component**: `NotificationService`
  (root-provided signal state) rendered once by `NotificationHostComponent` inside
  `app.ts`. The loading system mirrors this exactly.
- **All requests funnel through** `HttpClientService` → Angular `HttpClient` →
  the interceptor chain, so a single interceptor observes every call (JSON and blob).
- **Existing `.skeleton` shimmer utility** and **`Loader2` spinner** already used by
  the change-password button.

## 4. Components

### 4.1 `LoadingService` — `core/network/loading.service.ts`

Root-provided, signal-based in-flight counter. A counter (not a boolean) so
concurrent requests compose correctly.

```ts
@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly _count = signal(0);
  readonly activeCount = this._count.asReadonly();
  readonly isLoading = computed(() => this._count() > 0);
  begin(): void { this._count.update((n) => n + 1); }
  end(): void { this._count.update((n) => Math.max(0, n - 1)); }
}
```

`Math.max(0, …)` guards against an accidental double-`end()` underflowing the counter.

### 4.2 `loadingInterceptor` — `core/network/loading.interceptor.ts`

```ts
const SILENT = ['/api/auth/refresh', '/api/auth/logout'];
const isSilent = (url: string) => SILENT.some((p) => url.includes(p));

export const loadingInterceptor: HttpInterceptorFn = (req, next) => {
  if (isSilent(req.url)) return next(req);
  const loading = inject(LoadingService);
  loading.begin();
  return next(req).pipe(finalize(() => loading.end()));
};
```

- `finalize` runs on next/error/unsubscribe (cancel), so the counter always
  decrements — no leaks.
- **Silent opt-out:** token refresh and logout are excluded so transparent
  background activity does not flash the indicator (same spirit as
  `authInterceptor`'s `AUTH_NO_REFRESH` list).
- **Retries:** `HttpClientService` applies `retry` *outside* `this.http.request`,
  so each attempt is a fresh interceptor invocation with its own begin/finalize —
  balanced by construction.
- **Registration:** `withInterceptors([authInterceptor, loadingInterceptor])` in
  `app.config.ts`. Order is not significant for counting.

### 4.3 Global top progress bar — `core/ui/components/loading-bar.component.ts`

A 2px indeterminate bar fixed at the top of the viewport, mounted once in `app.ts`
beside `<app-notification-host />` so it covers the login screen and startup too.

```html
@if (show()) {
  <div class="fixed inset-x-0 top-0 z-[70] h-0.5 overflow-hidden bg-primary/20"
       role="progressbar" aria-label="Loading">
    <div class="h-full w-1/3 bg-primary animate-loading-bar"></div>
  </div>
}
```

**Anti-flicker:** `show()` is driven by `LoadingService.isLoading()` but gated by a
~120ms show-delay — a request that completes faster than the delay never shows the
bar. Implemented with an `effect()` + `setTimeout` inside the component:

- When `isLoading()` becomes true, arm a 120ms timer; if still loading when it
  fires, set `show=true`.
- When `isLoading()` becomes false, clear any pending timer and set `show=false`.

A new keyframe in `styles.scss`:

```css
@keyframes loading-bar {
  0%   { transform: translateX(-100%); }
  100% { transform: translateX(400%); }
}
.animate-loading-bar { animation: loading-bar 1.1s ease-in-out infinite; }
```

Respects the existing `prefers-reduced-motion` block (animation duration collapses),
so the bar degrades to a static primary sliver.

### 4.4 Skeleton primitive — `core/ui/components/skeleton.component.ts`

A minimal reusable wrapper over the existing `.skeleton` shimmer. Sizing comes from
the consumer's Tailwind classes.

```ts
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

Usage: `<app-skeleton width="6rem" height="0.875rem" />` or via utility classes.

## 5. Settings page wiring (concrete payoff)

`AccountViewModel.loading` already exists and flips around the profile fetch. Thread
it into the sections:

- **Profile card** (`profile-section.component.html`): when `!loaded`, render
  `<app-skeleton>` rows in place of the detail grid values; the avatar keeps its
  cached initial. Name/email keep cached session values (already non-empty), so no
  skeleton needed there.
- **Preferences** (`preferences-section.component`): add a `@Input() disabled` and
  bind it to the Language/Theme buttons' `[disabled]` so they are inert (and greyed
  via `disabled:opacity-*`) until the profile response arrives.
- **Change-password form** (`change-password-form.component`): add a
  `@Input() disabled` combined with its own `loading()` for the inputs/button, so it
  is disabled during the page load *and* during its own submit.
- `account.page.html` passes `[disabled]="vm.loading()"` into Preferences and the
  Security form.

## 6. Button-busy convention (documented)

A button that triggers an API request MUST:

- bind `[disabled]` to the governing loading signal, and
- render the `Loader2` spinner (`<svg [lucideIcon]="Loader2Icon" class="animate-spin" />`)
  while loading.

The change-password button is the reference implementation. Only the account page
needs changes now; the convention guides future pages.

## 7. Error handling & edge cases

- **Counter underflow:** `end()` floors at 0.
- **Request cancellation:** `finalize` covers unsubscribe, so navigating away mid-request still balances the counter.
- **Concurrent requests:** counter composes; the bar hides only when the last request settles.
- **Fast requests:** 120ms show-delay prevents flashing.
- **Silent requests:** refresh/logout excluded from the indicator.
- **Reduced motion:** animations collapse via the existing media query.

## 8. File changes

**New**
- `core/network/loading.service.ts`
- `core/network/loading.interceptor.ts`
- `core/ui/components/loading-bar.component.ts`
- `core/ui/components/skeleton.component.ts`
- Specs: `loading.service.spec.ts`, `loading.interceptor.spec.ts`,
  `loading-bar.component.spec.ts`, `skeleton.component.spec.ts`

**Edited**
- `app.config.ts` — register `loadingInterceptor`.
- `app.ts` — mount `<app-loading-bar />`.
- `styles.scss` — `@keyframes loading-bar` + `.animate-loading-bar`.
- `pages/account/account.page.html` — pass `[disabled]="vm.loading()"`.
- `sections/profile-section/*` — skeleton rows while `!loaded`.
- `sections/preferences-section/*` — `disabled` input on buttons.
- `components/change-password-form/*` — `disabled` input.
- Related specs updated for the new inputs.

## 9. Testing strategy

- **`LoadingService`** — `begin`/`end` counting; `isLoading` true when count > 0;
  `end()` floors at 0.
- **`loadingInterceptor`** — with `HttpTestingController`: `isLoading()` is true
  after a request is issued and false after it flushes; a `/api/auth/refresh` request
  never toggles `isLoading()`. (Matches the existing `auth.interceptor.spec.ts` shape.)
- **`LoadingBarComponent`** — renders the bar when `show()`/loading is true; hidden otherwise.
- **`SkeletonComponent`** — renders a `.skeleton` element with the given size.
- **Account page** — profile shows skeletons while `vm.loading()` is true and real
  values after; Preferences and change-password controls are `disabled` while loading.

## 10. Out of scope

- Converting `HttpClientService` to thread an `HttpContext` opt-out token (URL-based
  silent list is sufficient now).
- Global blocking overlay / disabling *every* button on *any* background request.
- Skeletons for pages other than the account profile (the primitive is reusable; new
  pages adopt it as they gain real data loads).
