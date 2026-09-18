import { TestBed } from '@angular/core/testing';
import { PreferencesSection } from '@features/auth/presentation/pages/account/sections/preferences-section/preferences-section.component';
import { LanguageStore } from '@core/i18n';

describe('PreferencesSection — disabled input', () => {
  it('disables the language and theme buttons when [disabled] is true', () => {
    TestBed.configureTestingModule({ imports: [PreferencesSection] });
    TestBed.inject(LanguageStore).set('en');
    const fixture = TestBed.createComponent(PreferencesSection);
    fixture.componentRef.setInput('disabled', true);
    fixture.detectChanges();
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
    expect(buttons.length).toBeGreaterThan(0);
    expect(buttons.every((b) => b.disabled)).toBe(true);
  });

  it('leaves the buttons enabled when [disabled] is false', () => {
    TestBed.configureTestingModule({ imports: [PreferencesSection] });
    TestBed.inject(LanguageStore).set('en');
    const fixture = TestBed.createComponent(PreferencesSection);
    fixture.detectChanges();
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
    expect(buttons.every((b) => !b.disabled)).toBe(true);
  });
});
