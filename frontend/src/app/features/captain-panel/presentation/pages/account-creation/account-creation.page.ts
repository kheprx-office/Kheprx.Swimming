import { Component, computed, inject, signal } from '@angular/core';
import { TranslatePipe } from '@core/i18n';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { RegisterSwimmerFormComponent } from './register-swimmer-form.component';
import { RegisterCoachFormComponent } from './register-coach-form.component';

@Component({
  selector: 'app-account-creation-page',
  standalone: true,
  imports: [TranslatePipe, RegisterSwimmerFormComponent, RegisterCoachFormComponent],
  templateUrl: './account-creation.page.html',
})
export class AccountCreationPage {
  private readonly auth = inject(AuthSessionStore);

  protected readonly tab = signal<'swimmer' | 'captain'>('swimmer');

  // Only a Head Coach may register captains/head coaches (POST /api/coaches is head-coach-only);
  // captains create swimmers only, so the Register Captain tab is hidden from them.
  protected readonly canRegisterCaptain = computed(() => this.auth.role() === 'head_coach');
}
