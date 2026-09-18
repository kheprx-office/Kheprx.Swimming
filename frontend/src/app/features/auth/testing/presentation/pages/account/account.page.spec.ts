import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { AccountPage } from '@features/auth/presentation/pages/account/account.page';
import { AccountViewModel } from '@features/auth/presentation/pages/account/account.viewmodel';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { LanguageStore } from '@core/i18n';
import { ThemeStore } from '@core/ui/theme/theme.store';
import { CurrentUser } from '@features/auth/domain/model/shared/auth';

const user: CurrentUser = {
  userId: 'USR-1',
  email: 'ahmed@example.com',
  nameEn: 'Ahmed Samir',
  nameAr: 'أحمد سمير',
  role: 'head_coach',
  phone: null,
  gender: null,
  age: null,
  nationalId: '29901010100000',
};

const populatedUser: CurrentUser = {
  userId: 'USR-2',
  email: 'x@y.z',
  nameEn: 'Samir',
  nameAr: null,
  role: 'captain',
  phone: '01000000000',
  gender: 'male',
  age: 30,
  nationalId: null,
};

function setup(loading: boolean, current: CurrentUser | null) {
  const vm = {
    loading: signal(loading),
    user: signal(current),
    init: jest.fn(),
  } as unknown as AccountViewModel;

  const auth = {
    principal: signal({ role: 'head_coach', userId: 'USR-1' }),
    displayName: signal('Ahmed Samir'),
    changePassword: jest.fn(),
  } as unknown as AuthSessionStore;

  const language = {
    lang: signal('en'),
    dir: signal('ltr'),
    toggle: jest.fn(),
  } as unknown as LanguageStore;

  const theme = {
    mode: signal('light'),
    set: jest.fn(),
  } as unknown as ThemeStore;

  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [AccountPage],
    providers: [
      provideRouter([]),
      { provide: AccountViewModel, useValue: vm },
      { provide: AuthSessionStore, useValue: auth },
      { provide: LanguageStore, useValue: language },
      { provide: ThemeStore, useValue: theme },
    ],
  });
  const fixture = TestBed.createComponent(AccountPage);
  fixture.detectChanges();
  return { fixture, vm, auth, language, theme };
}

describe('AccountPage', () => {
  it('calls vm.init on creation', () => {
    const { vm } = setup(false, null);
    expect(vm.init).toHaveBeenCalled();
  });

  it('renders the loaded name and email', () => {
    const { fixture } = setup(false, user);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Ahmed Samir');
    expect(text).toContain('ahmed@example.com');
  });

  it('renders the role badge translated (Head Coach for head_coach)', () => {
    const { fixture } = setup(false, user);
    const text = fixture.nativeElement.textContent as string;
    // The translate pipe resolves 'roles.head_coach' -> 'Head Coach'
    expect(text).toContain('Head Coach');
  });

  it('renders nationalId from the loaded profile', () => {
    const { fixture } = setup(false, user);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('29901010100000');
  });

  it('shows the notSet placeholder (Not set) when fields are null', () => {
    const { fixture } = setup(false, { ...user, nationalId: null });
    const text = fixture.nativeElement.textContent as string;
    // The translate pipe resolves 'profile.fields.notSet' -> 'Not set'
    expect(text).toContain('Not set');
  });

  it('shows phone, translated gender, and age when present in populatedUser', () => {
    const { fixture } = setup(false, populatedUser);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('01000000000');
    // GENDER_LABELS['male'] = 'gender.male', translate pipe resolves -> 'Male'
    expect(text).toContain('Male');
    expect(text).toContain('30');
  });

  it('has no avatar file upload control', () => {
    const { fixture } = setup(false, user);
    expect(fixture.nativeElement.querySelector('input[type="file"]')).toBeNull();
  });

  it('renders four sections: profile, preferences, security, about', () => {
    const { fixture } = setup(false, user);
    const text = fixture.nativeElement.textContent as string;
    // Translated section headings from the EN dictionary
    expect(text).toContain('Preferences');
    expect(text).toContain('Security');
    expect(text).toContain('About');
  });

  it('renders three password inputs in the security section', () => {
    const { fixture } = setup(false, user);
    const inputs = fixture.nativeElement.querySelectorAll('input[type="password"]');
    expect(inputs.length).toBe(3);
  });

  it('renders language toggle button with current language label', () => {
    const { fixture } = setup(false, user);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('EN');
  });

  it('renders theme toggle buttons (Light / Dark translated labels)', () => {
    const { fixture } = setup(false, user);
    const text = fixture.nativeElement.textContent as string;
    // Translated 'profile.preferences.light' -> 'Light', 'profile.preferences.dark' -> 'Dark'
    expect(text).toContain('Light');
    expect(text).toContain('Dark');
  });

  it('shows skeletons and disables preference buttons while loading', () => {
    const { fixture } = setup(true, null);
    expect(fixture.nativeElement.querySelector('app-skeleton')).toBeTruthy();
    const prefButtons = Array.from(
      fixture.nativeElement.querySelectorAll('app-preferences-section button'),
    ) as HTMLButtonElement[];
    expect(prefButtons.length).toBeGreaterThan(0);
    expect(prefButtons.every((b) => b.disabled)).toBe(true);
  });

  it('enables controls and hides skeletons once loaded', () => {
    const { fixture } = setup(false, user);
    expect(fixture.nativeElement.querySelector('app-skeleton')).toBeNull();
    const prefButtons = Array.from(
      fixture.nativeElement.querySelectorAll('app-preferences-section button'),
    ) as HTMLButtonElement[];
    expect(prefButtons.every((b) => !b.disabled)).toBe(true);
  });
});
