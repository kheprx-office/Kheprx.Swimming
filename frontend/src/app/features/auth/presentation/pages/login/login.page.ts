import { Component, inject } from '@angular/core';
import {
  LucideDynamicIcon,
  LucideZap,
  LucideLogIn,
  LucideMail,
  LucideLock,
  LucideAlertCircle,
  LucideLoader2,
} from '@lucide/angular';
import { DecorBackgroundComponent } from '@core/ui/components/decor-background.component';
import { LoginViewModel } from './login.viewmodel';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [LucideDynamicIcon, DecorBackgroundComponent],
  templateUrl: './login.page.html',
})
export class LoginPage {
  protected readonly vm = inject(LoginViewModel);
  readonly ZapIcon = LucideZap;
  readonly LogInIcon = LucideLogIn;
  readonly MailIcon = LucideMail;
  readonly LockIcon = LucideLock;
  readonly AlertCircleIcon = LucideAlertCircle;
  readonly Loader2Icon = LucideLoader2;

  onKeyDown(e: KeyboardEvent): void {
    if (e.key === 'Enter') void this.vm.submit();
  }
}
