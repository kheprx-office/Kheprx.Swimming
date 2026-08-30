import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { HomePage } from '@features/home/presentation/pages/home/home.page';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';

function setup(role: 'admin' | 'manager' | 'worker') {
  const navigate = jest.fn();
  const auth = {
    role: () => role,
    currentUserName: () => 'المدير العام',
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
    const { fixture } = setup('admin');
    expect(fixture.nativeElement.textContent).toContain('المدير العام');
  });

  it('shows the إدارة المستخدمين card for an admin', () => {
    const { fixture } = setup('admin');
    expect(fixture.nativeElement.textContent).toContain('إدارة المستخدمين');
  });

  it('hides the admin-only card for a non-admin', () => {
    const { fixture } = setup('worker');
    expect(fixture.nativeElement.textContent).not.toContain('إدارة المستخدمين');
  });

  it('shows the الإعدادات card for all roles', () => {
    const { fixture } = setup('manager');
    expect(fixture.nativeElement.textContent).toContain('الإعدادات');
  });

  it('navigates only when a card has a route', () => {
    const { fixture, navigate } = setup('admin');
    fixture.componentInstance.go('/user-management');
    expect(navigate).toHaveBeenCalledWith(['/user-management']);
    navigate.mockClear();
    fixture.componentInstance.go(undefined);
    expect(navigate).not.toHaveBeenCalled();
  });
});
