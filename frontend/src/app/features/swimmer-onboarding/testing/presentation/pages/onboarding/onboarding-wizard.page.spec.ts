import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { OnboardingWizardPage } from '@features/swimmer-onboarding/presentation/pages/onboarding/onboarding-wizard.page';
import { OnboardingViewModel } from '@features/swimmer-onboarding/presentation/pages/onboarding/onboarding.viewmodel';
import { LanguageStore } from '@core/i18n';

// Minimal OnboardingViewModel stub — only the members the component's field initializers
// touch: `medicalRows` reads the four Yes/details signal pairs, ngOnInit calls load().
function stubVm() {
  return {
    load: jest.fn(),
    allergyYes: signal(false), allergyDetails: signal(''),
    surgeryYes: signal(false), surgeryDetails: signal(''),
    chronicYes: signal(false), chronicDetails: signal(''),
    autoimmuneYes: signal(false), autoimmuneDetails: signal(''),
  } as unknown as OnboardingViewModel;
}

describe('OnboardingWizardPage — language switcher', () => {
  function make() {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [{ provide: OnboardingViewModel, useValue: stubVm() }],
    });
    const page = TestBed.runInInjectionContext(() => new OnboardingWizardPage());
    const store = TestBed.inject(LanguageStore);
    store.set('en'); // deterministic start — the store is a root singleton
    return { page, store };
  }

  it('setLang switches the shared language store (and RTL) between en and ar', () => {
    const { page, store } = make();
    expect(store.lang()).toBe('en');

    page.setLang('ar');
    expect(store.lang()).toBe('ar');
    expect(store.dir()).toBe('rtl');

    page.setLang('en');
    expect(store.lang()).toBe('en');
    expect(store.dir()).toBe('ltr');
  });
});
