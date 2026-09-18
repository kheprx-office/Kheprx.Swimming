import { Component, inject } from '@angular/core';
import { LucideDynamicIcon, LucideWaves, LucideMail, LucideLock, LucideLoader2, LucideAlertCircle, LucideShieldAlert } from '@lucide/angular';
import { LoginViewModel } from './login.viewmodel';
import { LanguageStore, TranslatePipe, type Lang } from '@core/i18n';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [LucideDynamicIcon, TranslatePipe],
  templateUrl: './login.page.html',
})
export class LoginPage {
  protected readonly vm = inject(LoginViewModel);
  private readonly language = inject(LanguageStore);
  protected readonly lang = this.language.lang;

  protected readonly WavesIcon = LucideWaves;
  protected readonly MailIcon = LucideMail;
  protected readonly LockIcon = LucideLock;
  protected readonly Loader2Icon = LucideLoader2;
  protected readonly AlertCircleIcon = LucideAlertCircle;
  protected readonly ShieldAlertIcon = LucideShieldAlert;

  setLang(lang: Lang): void { this.language.set(lang); }
  onKeyDown(e: KeyboardEvent): void { if (e.key === 'Enter') void this.vm.submit(); }
}
