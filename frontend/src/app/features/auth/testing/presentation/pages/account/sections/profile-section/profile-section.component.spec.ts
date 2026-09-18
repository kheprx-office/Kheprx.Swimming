import { TestBed } from '@angular/core/testing';
import { ProfileSection } from '@features/auth/presentation/pages/account/sections/profile-section/profile-section.component';
import { LanguageStore } from '@core/i18n';

describe('ProfileSection — loading state', () => {
  function make(loaded: boolean) {
    TestBed.configureTestingModule({ imports: [ProfileSection] });
    TestBed.inject(LanguageStore).set('en');
    const fixture = TestBed.createComponent(ProfileSection);
    fixture.componentRef.setInput('loaded', loaded);
    fixture.detectChanges();
    return fixture;
  }

  it('shows skeleton placeholders and hides the value grid while not loaded', () => {
    const fixture = make(false);
    expect(fixture.nativeElement.querySelector('app-skeleton')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('dl')).toBeNull();
  });

  it('shows the real value grid once loaded', () => {
    const fixture = make(true);
    expect(fixture.nativeElement.querySelector('dl')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('app-skeleton')).toBeNull();
  });
});
