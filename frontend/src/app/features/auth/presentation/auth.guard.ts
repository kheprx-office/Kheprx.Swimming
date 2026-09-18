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

// firstLoginGuard: an authenticated user still flagged mustChangePassword is pinned to
// /change-password until they change it. Applied to shell children EXCEPT /change-password.
export const firstLoginGuard: CanActivateFn = () => {
  const auth = inject(AuthSessionStore);
  const router = inject(Router);
  if (auth.mustChangePassword()) { router.navigate(['/change-password']); return false; }
  return true;
};
