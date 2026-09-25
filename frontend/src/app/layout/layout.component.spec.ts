import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { LayoutComponent } from './layout.component';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { LanguageStore } from '@core/i18n';

function setup(role: 'head_coach' | 'captain' | 'swimmer', mustChangePassword = false) {
  localStorage.clear();
  const signOut = jest.fn().mockResolvedValue(undefined);
  const auth = {
    role: () => role,
    currentUserName: () => 'Sara Ali',
    mustChangePassword: () => mustChangePassword,
    signOut,
  } as unknown as AuthSessionStore;
  TestBed.configureTestingModule({
    imports: [LayoutComponent],
    providers: [provideRouter([]), { provide: AuthSessionStore, useValue: auth }],
  });
  const fixture = TestBed.createComponent(LayoutComponent);
  TestBed.inject(LanguageStore).set('en');
  fixture.detectChanges();
  return { fixture, signOut };
}

describe('LayoutComponent (head_coach)', () => {
  it('renders the avatar initials from the user name', () => {
    const { fixture } = setup('head_coach');
    const avatar = fixture.nativeElement.querySelector('[data-testid="user-avatar"]');
    expect(avatar?.textContent?.trim()).toBe('SA');
  });

  it('signs out via the store', async () => {
    const { fixture, signOut } = setup('head_coach');
    await fixture.componentInstance.signOut();
    expect(signOut).toHaveBeenCalled();
  });

  it('renders the Settings nav item as an enabled button (has a route)', () => {
    const { fixture } = setup('head_coach');
    const navButtons = Array.from(fixture.nativeElement.querySelectorAll('nav button'));
    const settingsBtn = navButtons.find((b) =>
      (b as HTMLElement).textContent?.trim() === 'Settings',
    ) as HTMLButtonElement | undefined;
    expect(settingsBtn).toBeDefined();
    expect(settingsBtn?.disabled).toBe(false);
  });

  it('renders every nav item (e.g. Dashboard) as an enabled, clickable button', () => {
    const { fixture } = setup('head_coach');
    const navButtons = Array.from(fixture.nativeElement.querySelectorAll('nav button'));
    const dashboardBtn = navButtons.find((b) =>
      (b as HTMLElement).textContent?.trim() === 'Dashboard',
    ) as HTMLButtonElement | undefined;
    expect(dashboardBtn).toBeDefined();
    expect(dashboardBtn?.disabled).toBe(false);
    // No nav item should be disabled anymore (unbuilt features route to blank pages).
    expect(navButtons.every((b) => !(b as HTMLButtonElement).disabled)).toBe(true);
  });

  it('locks (disables) every sidebar nav item while a forced password change is pending', () => {
    const { fixture } = setup('head_coach', true);
    const navButtons = Array.from(fixture.nativeElement.querySelectorAll('nav button'));
    expect(navButtons.length).toBeGreaterThan(0);
    expect(navButtons.every((b) => (b as HTMLButtonElement).disabled)).toBe(true);
  });

  it('shows Captain Panel for head_coach', () => {
    const { fixture } = setup('head_coach');
    const labels = Array.from(fixture.nativeElement.querySelectorAll('nav button')).map(
      (b) => (b as HTMLElement).textContent?.trim(),
    );
    expect(labels).toContain('Captain Panel');
  });

  it('toggleLanguage() flips lang() between en and ar', () => {
    const { fixture } = setup('head_coach');
    const component = fixture.componentInstance;
    const langStore = TestBed.inject(LanguageStore);

    // Ensure we start from English
    langStore.set('en');
    fixture.detectChanges();
    expect(component.lang()).toBe('en');

    component.toggleLanguage();
    fixture.detectChanges();
    expect(component.lang()).toBe('ar');

    component.toggleLanguage();
    fixture.detectChanges();
    expect(component.lang()).toBe('en');
  });

  it('does not have a hardcoded dir="rtl" on the root element', () => {
    const { fixture } = setup('head_coach');
    const root = fixture.nativeElement.querySelector('div') as HTMLElement;
    expect(root?.getAttribute('dir')).toBeNull();
  });
});

describe('LayoutComponent (captain)', () => {
  it('shows Captain Panel for captain', () => {
    const { fixture } = setup('captain');
    const labels = Array.from(fixture.nativeElement.querySelectorAll('nav button')).map(
      (b) => (b as HTMLElement).textContent?.trim(),
    );
    expect(labels).toContain('Captain Panel');
  });
});

describe('LayoutComponent (swimmer)', () => {
  it('shows only My Profile and Settings', () => {
    const { fixture } = setup('swimmer');
    const labels = Array.from(fixture.nativeElement.querySelectorAll('nav button'))
      .map((b) => (b as HTMLElement).textContent?.trim());
    expect(labels).toEqual(['My Profile', 'Settings']);
  });
});
