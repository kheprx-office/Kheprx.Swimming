import { Component, OnInit, inject } from '@angular/core';
import { TranslatePipe, LanguageStore } from '@core/i18n';
import { LookupItem } from '@features/reference/domain/model/reference';
import { OnboardingViewModel } from './onboarding.viewmodel';

interface WizardStep {
  key: string;
  active: boolean;
}

@Component({
  selector: 'app-onboarding-wizard-page',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './onboarding-wizard.page.html',
  styleUrls: ['./onboarding-wizard.page.scss'],
})
export class OnboardingWizardPage implements OnInit {
  protected readonly vm = inject(OnboardingViewModel);
  private readonly language = inject(LanguageStore);

  // Step strip: only Identity & Vitals is functional this pass; 2–5 are upcoming placeholders.
  protected readonly steps: readonly WizardStep[] = [
    { key: 'identity', active: true },
    { key: 'guardian', active: false },
    { key: 'physiological', active: false },
    { key: 'inbody', active: false },
    { key: 'done', active: false },
  ];

  ngOnInit(): void {
    void this.vm.load();
  }

  /** Language-aware option label (Arabic name when the UI language is 'ar', else English). */
  protected labelFor(opt: LookupItem): string {
    return this.language.lang() === 'ar' ? (opt.nameAr ?? opt.nameEn) : opt.nameEn;
  }
}
