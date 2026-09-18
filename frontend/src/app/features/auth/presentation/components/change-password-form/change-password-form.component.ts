import { Component, EventEmitter, Input, Output, computed, inject, signal } from '@angular/core';
import { LucideDynamicIcon, LucideLoader2 } from '@lucide/angular';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { TranslatePipe, TranslateService } from '@core/i18n';
import { toUserMessage } from '@core/domain/errors/user-message';

@Component({
  selector: 'app-change-password-form',
  standalone: true,
  imports: [LucideDynamicIcon, TranslatePipe],
  templateUrl: './change-password-form.component.html',
})
export class ChangePasswordForm {
  private readonly auth = inject(AuthSessionStore);
  private readonly i18n = inject(TranslateService);

  @Input() variant: 'full' | 'compact' = 'compact';
  @Input() disabled = false;
  @Output() succeeded = new EventEmitter<void>();

  readonly Loader2Icon = LucideLoader2;

  readonly currentPassword = signal('');
  readonly newPassword = signal('');
  readonly confirmPassword = signal('');
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  // Idle label: forced page uses the changePassword copy; Settings uses the compact "Save".
  readonly submitLabel = computed(() => (this.variant === 'full' ? 'changePassword.submit' : 'profile.security.save'));

  async submit(): Promise<void> {
    this.error.set(null);
    if (this.newPassword() !== this.confirmPassword()) {
      this.error.set(this.i18n.t('changePassword.mismatch'));
      return;
    }
    this.loading.set(true);
    const r = await this.auth.changePassword(this.currentPassword(), this.newPassword());
    this.loading.set(false);
    if (r.ok) {
      this.currentPassword.set('');
      this.newPassword.set('');
      this.confirmPassword.set('');
      this.succeeded.emit();
    } else {
      this.error.set(toUserMessage(r.error));
    }
  }
}
