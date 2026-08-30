import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { AccountPage } from '@features/auth/presentation/pages/account/account.page';
import { AccountViewModel } from '@features/auth/presentation/pages/account/account.viewmodel';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { CurrentUser } from '@features/auth/domain/model/shared/auth';
import { ROLE_LABELS } from '@core/domain/roles';

const user: CurrentUser = { userId: 'USR-1', email: 'ahmed@alnoor-electric.com', fullName: 'أحمد محمود', role: 'admin', phone: null, gender: null, age: null };
const populatedUser: CurrentUser = { userId: 'USR-2', email: 'x@y.z', fullName: 'سمير', role: 'admin', phone: '01000000000', gender: 'male', age: 30 };

function setup(loading: boolean, current: CurrentUser | null) {
  const vm = { loading: signal(loading), user: signal(current), init: jest.fn() } as unknown as AccountViewModel;
  const auth = { principal: () => ({ role: 'admin', userId: 'USR-1' }) } as unknown as AuthSessionStore;
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [AccountPage],
    providers: [
      provideRouter([]),
      { provide: AccountViewModel, useValue: vm },
      { provide: AuthSessionStore, useValue: auth },
    ],
  });
  const fixture = TestBed.createComponent(AccountPage);
  fixture.detectChanges();
  return { fixture, vm };
}

describe('AccountPage', () => {
  it('renders the loaded name and email', () => {
    const { fixture } = setup(false, user);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('أحمد محمود');
    expect(text).toContain('ahmed@alnoor-electric.com');
  });

  it('renders the role from the session principal when the profile did not load', () => {
    const { fixture } = setup(false, null);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain(ROLE_LABELS['admin']);
  });

  it('shows غير محدد placeholders for phone/gender/age and keeps the change-password link', () => {
    const { fixture } = setup(false, user);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('غير محدد');
    expect(text).toContain('تغيير كلمة المرور');
    expect(fixture.nativeElement.querySelector('a[href="/change-password"]')).toBeTruthy();
  });

  it('has no avatar upload control', () => {
    const { fixture } = setup(false, user);
    expect(fixture.nativeElement.querySelector('input[type="file"]')).toBeNull();
  });

  it('calls vm.init on creation', () => {
    const { vm } = setup(true, null);
    expect(vm.init).toHaveBeenCalled();
  });

  it('shows a loading spinner and hides the fields while loading', () => {
    const { fixture } = setup(true, null);
    expect(fixture.nativeElement.querySelector('.animate-spin')).toBeTruthy();
    expect((fixture.nativeElement.textContent as string)).not.toContain('تغيير كلمة المرور');
  });

  it('renders real phone, gender label, and age when present', () => {
    const { fixture } = setup(false, populatedUser);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('01000000000');
    expect(text).toContain('ذكر');
    expect(text).toContain('30');
    expect(text).not.toContain('غير محدد');
  });
});
