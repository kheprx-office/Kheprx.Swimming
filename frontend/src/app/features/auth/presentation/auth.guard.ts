import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { UserRole } from '@core/domain/roles';

// authGuard: blocks anonymous access, sending unauthenticated users to /login.
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthSessionStore);
  const router = inject(Router);
  if (!auth.isAuthenticated()) { router.navigate(['/login']); return false; }
  return true;
};

// roleGuard: requires one of `roles`. Authenticated users lacking the role are sent
// to the home route ('/') — the base's configurable "forbidden" destination.
export const roleGuard = (...roles: UserRole[]): CanActivateFn => () => {
  const auth = inject(AuthSessionStore);
  const router = inject(Router);
  if (!auth.isAuthenticated()) { router.navigate(['/login']); return false; }
  const role = auth.role();
  if (!role || !roles.includes(role)) { router.navigate(['/']); return false; }
  return true;
};

// firstLoginGuard: an authenticated user still flagged mustChangePassword is pinned to their
// first-login destination until it clears — swimmers to /onboarding (finish the wizard),
// everyone else to /change-password. Applied to shell children EXCEPT those two routes.
export const firstLoginGuard: CanActivateFn = () => {
  const auth = inject(AuthSessionStore);
  const router = inject(Router);
  if (auth.mustChangePassword()) {
    router.navigate([auth.role() === 'swimmer' ? '/onboarding' : '/change-password']);
    return false;
  }
  return true;
};

// swimmerHomeRedirectGuard: swimmers have no coach dashboard — send them to their profile.
// Applied to /home so the '' default and roleGuard's '/' fallback never strand a swimmer.
export const swimmerHomeRedirectGuard: CanActivateFn = () => {
  const auth = inject(AuthSessionStore);
  const router = inject(Router);
  if (auth.role() === 'swimmer') { router.navigate(['/my-profile']); return false; }
  return true;
};

// onboardingGuard: the /onboarding wizard is only for a first-login swimmer. Anonymous → /login;
// a non-swimmer or an already-onboarded swimmer → /home.
export const onboardingGuard: CanActivateFn = () => {
  const auth = inject(AuthSessionStore);
  const router = inject(Router);
  if (!auth.isAuthenticated()) { router.navigate(['/login']); return false; }
  if (auth.role() !== 'swimmer' || !auth.mustChangePassword()) { router.navigate(['/home']); return false; }
  return true;
};
