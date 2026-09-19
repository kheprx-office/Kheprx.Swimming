import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TranslatePipe } from '@core/i18n';
import { LanguageStore } from '@core/i18n/language.store';
import { SelectFieldComponent } from '@core/ui/components/select-field.component';
import { TextFieldComponent } from '@core/ui/components/text-field.component';
import { SwimmerProfileViewModel } from './swimmer-profile.viewmodel';

interface ProfileTab { key: string; labelKey: string; }

@Component({
  selector: 'app-swimmer-profile-page',
  standalone: true,
  imports: [TranslatePipe, SelectFieldComponent, TextFieldComponent],
  templateUrl: './swimmer-profile.page.html',
})
export class SwimmerProfilePage implements OnInit {
  protected readonly vm = inject(SwimmerProfileViewModel);
  private readonly route = inject(ActivatedRoute);
  private readonly language = inject(LanguageStore);

  // Full tab strip for visual fidelity; only 'identityVitals', 'guardian', 'physiological', and 'inbody' are enabled this pass.
  protected readonly enabledTabs = new Set(['identityVitals', 'guardian', 'physiological', 'inbody']);
  isEnabled(key: string): boolean { return this.enabledTabs.has(key); }

  protected readonly tabs: ProfileTab[] = [
    { key: 'identityVitals', labelKey: 'swimmerProfile.tabs.identityVitals' },
    { key: 'guardian', labelKey: 'swimmerProfile.tabs.guardian' },
    { key: 'physiological', labelKey: 'swimmerProfile.tabs.physiological' },
    { key: 'inbody', labelKey: 'swimmerProfile.tabs.inbody' },
    { key: 'records', labelKey: 'swimmerProfile.tabs.records' },
    { key: 'healthMonitoring', labelKey: 'swimmerProfile.tabs.healthMonitoring' },
    { key: 'attendance', labelKey: 'swimmerProfile.tabs.attendance' },
    { key: 'championships', labelKey: 'swimmerProfile.tabs.championships' },
    { key: 'feedback', labelKey: 'swimmerProfile.tabs.feedback' },
  ];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    void this.vm.load(id);
  }

  displayName(): string {
    const i = this.vm.profile()?.identity;
    if (!i) return '';
    return this.language.lang() === 'ar' ? (i.nameAr ?? i.nameEn) : i.nameEn;
  }

  refLabel(ref: { nameEn: string; nameAr: string | null } | null | undefined): string {
    if (!ref) return '—';
    return this.language.lang() === 'ar' ? (ref.nameAr ?? ref.nameEn) : ref.nameEn;
  }

  initials(): string {
    const en = this.vm.profile()?.identity.nameEn ?? '';
    const parts = en.trim().split(/\s+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase();
  }
}
