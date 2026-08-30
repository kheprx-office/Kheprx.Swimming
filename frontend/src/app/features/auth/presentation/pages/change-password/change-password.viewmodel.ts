// ChangePasswordViewModel: presentation facade for the change-password form. Guards
// the confirm-match locally, then drives AuthSessionStore.changePassword().
import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { createLogger } from '@core/logging/logger';
import { toUserMessage } from '@core/domain/errors/user-message';

@Injectable()
export class ChangePasswordViewModel {
  private readonly auth = inject(AuthSessionStore);
  private readonly router = inject(Router);
  private readonly log = createLogger('viewmodel', 'ChangePasswordViewModel');

  readonly currentPassword = signal('');
  readonly newPassword = signal('');
  readonly confirmPassword = signal('');
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  async submit(): Promise<void> {
    this.log.debug('submit()');
    this.error.set(null);
    if (this.newPassword() !== this.confirmPassword()) {
      this.error.set('كلمة المرور الجديدة وتأكيدها غير متطابقين');
      return;
    }
    this.loading.set(true);
    const r = await this.auth.changePassword(this.currentPassword(), this.newPassword());
    this.loading.set(false);
    if (r.ok) {
      void this.router.navigate(['/account']);
    } else {
      this.error.set(toUserMessage(r.error));
    }
  }
}
