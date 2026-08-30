import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  LucideDynamicIcon,
  LucideUser,
  LucideMail,
  LucidePhone,
  LucideUsers,
  LucideCalendar,
  LucideShield,
} from '@lucide/angular';
import { AccountViewModel } from './account.viewmodel';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { DecorBackgroundComponent } from '@core/ui/components/decor-background.component';
import { ROLE_LABELS } from '@core/domain/roles';
import { GENDER_LABELS } from '@core/domain/gender';

@Component({
  selector: 'app-account-page',
  standalone: true,
  imports: [RouterLink, LucideDynamicIcon, DecorBackgroundComponent],
  templateUrl: './account.page.html',
})
export class AccountPage {
  protected readonly vm = inject(AccountViewModel);
  private readonly auth = inject(AuthSessionStore);
  protected readonly ROLE_LABELS = ROLE_LABELS;

  protected readonly UserIcon = LucideUser;
  protected readonly MailIcon = LucideMail;
  protected readonly PhoneIcon = LucidePhone;
  protected readonly UsersIcon = LucideUsers;
  protected readonly CalendarIcon = LucideCalendar;
  protected readonly ShieldIcon = LucideShield;

  // Name/email come from the loaded profile; role falls back to the session principal
  // so it still renders if the /api/auth/me fetch fails.
  protected readonly fullName = computed(() => this.vm.user()?.fullName ?? '');
  protected readonly email = computed(() => this.vm.user()?.email ?? '');
  protected readonly role = computed(() => this.vm.user()?.role ?? this.auth.principal()?.role ?? null);
  protected readonly phone = computed(() => this.vm.user()?.phone ?? null);
  protected readonly age = computed(() => this.vm.user()?.age ?? null);
  protected readonly genderLabel = computed(() => {
    const g = this.vm.user()?.gender;
    return g ? GENDER_LABELS[g] : null;
  });
  protected readonly initial = computed(() => this.fullName().trim().slice(0, 1) || 'م');

  constructor() { void this.vm.init(); }
}
