import { Component, OnInit, inject } from '@angular/core';
import { TranslatePipe, LanguageStore, type Lang } from '@core/i18n';
import { LookupItem } from '@features/reference/domain/model/reference';
import { OnboardingViewModel } from './onboarding.viewmodel';

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
  protected readonly lang = this.language.lang;

  // Step strip: active/done are derived in the template via vm.currentStep().
  protected readonly steps: readonly string[] = ['identity', 'guardian', 'physiological', 'inbody', 'done'];

  protected readonly medicalRows = [
    { key: 'allergies', yes: this.vm.allergyYes, details: this.vm.allergyDetails },
    { key: 'surgeries', yes: this.vm.surgeryYes, details: this.vm.surgeryDetails },
    { key: 'chronic', yes: this.vm.chronicYes, details: this.vm.chronicDetails },
    { key: 'autoimmune', yes: this.vm.autoimmuneYes, details: this.vm.autoimmuneDetails },
  ];

  ngOnInit(): void {
    void this.vm.load();
  }

  /** Language-aware option label (Arabic name when the UI language is 'ar', else English). */
  protected labelFor(opt: LookupItem): string {
    return this.language.lang() === 'ar' ? (opt.nameAr ?? opt.nameEn) : opt.nameEn;
  }

  /** Switch the UI language; RTL + persistence are handled globally by LanguageStore. */
  setLang(lang: Lang): void {
    this.language.set(lang);
  }
}
