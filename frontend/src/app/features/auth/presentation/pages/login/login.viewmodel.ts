// LoginViewModel: the presentation facade for the login form. Drives AuthSessionStore
// and translates the Result into form state + navigation.
import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { createLogger } from '@core/logging/logger';
import { toUserMessage } from '@core/domain/errors/user-message';
import { UserRole } from '@core/domain/roles';

// Where each role lands after signing in. All roles land on /home (the starter scaffold
// landing shell); business-specific landing pages are added when business modules are built.
const LANDING_ROUTE_BY_ROLE: Record<UserRole, string> = {
  admin: '/home',
  manager: '/home',
  moqawel: '/home',
  worker: '/home',
};

@Injectable()
export class LoginViewModel {
  private readonly auth = inject(AuthSessionStore);
  private readonly router = inject(Router);
  private readonly log = createLogger('viewmodel', 'LoginViewModel');

  readonly email = signal('');
  readonly password = signal('');
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly shakeKey = signal(0);

  async submit(): Promise<void> {
    this.log.debug('submit()');
    this.loading.set(true);
    this.error.set(null);
    const r = await this.auth.signIn(this.email(), this.password());
    this.loading.set(false);
    if (r.ok) {
      if (r.data.mustChangePassword) {
        void this.router.navigate(['/change-password']);
      } else {
        const role = this.auth.role();
        void this.router.navigate([role ? LANDING_ROUTE_BY_ROLE[role] : '/home']);
      }
    } else {
      this.error.set(toUserMessage(r.error));
      this.shakeKey.update((k) => k + 1);
    }
  }
}
