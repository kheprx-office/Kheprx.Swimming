import { Component, computed, inject } from '@angular/core';
import { TranslatePipe } from '@core/i18n';
import { AccountViewModel } from './account.viewmodel';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { LanguageStore } from '@core/i18n';
import { GENDER_LABELS } from '@core/domain/gender';
import { ProfileSection } from './sections/profile-section/profile-section.component';
import { PreferencesSection } from './sections/preferences-section/preferences-section.component';
import { SecuritySection } from './sections/security-section/security-section.component';
import { AboutSection } from './sections/about-section/about-section.component';

@Component({
  selector: 'app-account-page',
  standalone: true,
  imports: [TranslatePipe, ProfileSection, PreferencesSection, SecuritySection, AboutSection],
  templateUrl: './account.page.html',
})
export class AccountPage {
  protected readonly vm = inject(AccountViewModel);
  private readonly auth = inject(AuthSessionStore);
  private readonly language = inject(LanguageStore);

  protected readonly lang = this.language.lang;

  protected readonly displayName = computed(() => {
    const u = this.vm.user();
    if (!u) return this.auth.displayName();
    return this.lang() === 'ar' ? (u.nameAr ?? u.nameEn) : u.nameEn;
  });
  protected readonly email = computed(() => this.vm.user()?.email ?? '');
  protected readonly role = computed(() => this.vm.user()?.role ?? this.auth.principal()?.role ?? null);
  protected readonly phone = computed(() => this.vm.user()?.phone ?? null);
  protected readonly age = computed(() => this.vm.user()?.age ?? null);
  protected readonly nationalId = computed(() => this.vm.user()?.nationalId ?? null);
  protected readonly genderKey = computed(() => {
    const g = this.vm.user()?.gender;
    return g ? GENDER_LABELS[g] : null;
  });
  protected readonly initial = computed(() => this.displayName().trim().slice(0, 1) || '?');

  constructor() { void this.vm.init(); }
}
