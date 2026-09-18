import { Component, Input, signal } from '@angular/core';
import { ChangePasswordForm } from '@features/auth/presentation/components/change-password-form/change-password-form.component';
import { TranslatePipe } from '@core/i18n';
import { LucideDynamicIcon, LucideShield } from '@lucide/angular';

@Component({
  selector: 'app-security-section',
  standalone: true,
  imports: [ChangePasswordForm, TranslatePipe, LucideDynamicIcon],
  templateUrl: './security-section.component.html',
})
export class SecuritySection {
  readonly ShieldIcon = LucideShield;
  readonly saved = signal(false);
  @Input() disabled = false;
  onSaved(): void { this.saved.set(true); }
}
