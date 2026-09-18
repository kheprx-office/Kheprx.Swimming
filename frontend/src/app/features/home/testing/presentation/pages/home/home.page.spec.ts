import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { HomePage } from '@features/home/presentation/pages/home/home.page';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';

function setup(role: 'head_coach' | 'captain') {
  const navigate = jest.fn();
  const auth = {
    role: () => role,
    currentUserName: () => 'المدرب العام',
  } as unknown as AuthSessionStore;
  TestBed.configureTestingModule({
    imports: [HomePage],
    providers: [{ provide: AuthSessionStore, useValue: auth }, { provide: Router, useValue: { navigate } }],
  });
  const fixture = TestBed.createComponent(HomePage);
  fixture.detectChanges();
  return { fixture, navigate };
}

describe('HomePage', () => {
  it('greets the user by name, not their id', () => {
    const { fixture } = setup('head_coach');
    expect(fixture.nativeElement.textContent).toContain('المدرب العام');
  });

  it('shows the الإعدادات card for all roles', () => {
    const { fixture } = setup('captain');
    expect(fixture.nativeElement.textContent).toContain('الإعدادات');
  });

  it('does not show an إدارة المستخدمين card (feature parked)', () => {
    const { fixture } = setup('head_coach');
    expect(fixture.nativeElement.textContent).not.toContain('إدارة المستخدمين');
  });

  it('navigates only when a card has a route', () => {
    const { fixture, navigate } = setup('head_coach');
    fixture.componentInstance.go('/account');
    expect(navigate).toHaveBeenCalledWith(['/account']);
    navigate.mockClear();
    fixture.componentInstance.go(undefined);
    expect(navigate).not.toHaveBeenCalled();
  });
});
