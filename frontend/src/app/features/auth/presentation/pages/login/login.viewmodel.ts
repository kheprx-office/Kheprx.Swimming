// LoginViewModel: the presentation facade for the login form. Drives AuthSessionStore
// and translates the Result into form state + navigation. Owns the role selector state:
// the selectable roles come from the database (GET /api/roles), and the picked role is
// sent with the credentials so the backend can reject a role that doesn't match the account.
import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { LoadRolesUseCase } from '@features/auth/domain/usecases/roles/load-roles.use-case';
import { LoadSwimmerCountUseCase } from '@features/swimmers/domain/usecases/load-swimmer-count.use-case';
import { RoleOption } from '@features/auth/domain/model/roles/role-option';
import { createLogger } from '@core/logging/logger';
import { toUserMessage } from '@core/domain/errors/user-message';
import { UserRole } from '@core/domain/roles';

// Where each role lands after signing in. All roles land on /home (the starter scaffold
// landing shell); business-specific landing pages are added when business modules are built.
const LANDING_ROUTE_BY_ROLE: Record<UserRole, string> = {
  head_coach: '/home',
  captain: '/home',
  swimmer: '/home',
};

// Default selection before roles load; matches the dev-prefilled Head Coach credentials.
const DEFAULT_ROLE = 'head_coach';

@Injectable()
export class LoginViewModel {
  private readonly auth = inject(AuthSessionStore);
  private readonly router = inject(Router);
  private readonly loadRolesUseCase = inject(LoadRolesUseCase);
  private readonly loadSwimmerCountUseCase = inject(LoadSwimmerCountUseCase);
  private readonly log = createLogger('viewmodel', 'LoginViewModel');

  // TODO: dev-only, remove before release — prefilled Head Coach dev credentials for quick testing.
  readonly email = signal('headcoach@kheprx.local');
  readonly password = signal('Passw0rd!');
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly shakeKey = signal(0);

  readonly swimmerCount = signal<number | null>(null);

  // Role selector: options loaded from the DB, plus the currently picked role code.
  readonly roles = signal<RoleOption[]>([]);
  readonly selectedRole = signal<string>(DEFAULT_ROLE);

  constructor() {
    void this.loadRoles();
    void this.loadSwimmerCount();
  }

  async loadSwimmerCount(): Promise<void> {
    const r = await this.loadSwimmerCountUseCase.run();
    if (r.ok) this.swimmerCount.set(r.data);
  }

  async loadRoles(): Promise<void> {
    this.log.debug('loadRoles()');
    const r = await this.loadRolesUseCase.run();
    if (r.ok) {
      this.roles.set(r.data);
      // Keep the default selection when it's offered; otherwise fall back to the first role.
      if (r.data.length > 0 && !r.data.some((role) => role.code === this.selectedRole())) {
        this.selectedRole.set(r.data[0].code);
      }
    }
  }

  setRole(code: string): void {
    this.selectedRole.set(code);
  }

  async submit(): Promise<void> {
    this.log.debug('submit()');
    this.loading.set(true);
    this.error.set(null);
    const r = await this.auth.signIn(this.email(), this.password(), this.selectedRole());
    this.loading.set(false);
    if (r.ok) {
      if (r.data.mustChangePassword) {
        void this.router.navigate([this.auth.role() === 'swimmer' ? '/onboarding' : '/change-password']);
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
