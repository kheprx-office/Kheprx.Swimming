import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ChangePasswordForm } from '@features/auth/presentation/components/change-password-form/change-password-form.component';
import { LanguageStore, TranslatePipe } from '@core/i18n';

@Component({
  selector: 'app-change-password-page',
  standalone: true,
  imports: [ChangePasswordForm, TranslatePipe],
  templateUrl: './change-password.page.html',
})
export class ChangePasswordPage {
  private readonly router = inject(Router);
  private readonly language = inject(LanguageStore);
  protected readonly lang = this.language.lang;

  toggleLanguage(): void { this.language.toggle(); }
  onDone(): void { void this.router.navigate(['/account']); }
}
