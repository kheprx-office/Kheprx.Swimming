import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { ChangePasswordViewModel } from '@features/auth/presentation/pages/change-password/change-password.viewmodel';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';
import { AuthSession } from '@features/auth/domain/model/shared/auth';

const session: AuthSession = { tokens: { accessToken: 'a', refreshToken: 'r' }, principal: { role: 'moqawel', userId: 'USR-MOQAWEL' }, mustChangePassword: false };

describe('ChangePasswordViewModel', () => {
  const auth = { changePassword: jest.fn() } as unknown as AuthSessionStore;
  const router = { navigate: jest.fn() } as unknown as Router;
  let vm: ChangePasswordViewModel;
  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [ChangePasswordViewModel, { provide: AuthSessionStore, useValue: auth }, { provide: Router, useValue: router }],
    });
    vm = TestBed.inject(ChangePasswordViewModel);
  });

  it('blocks submit and shows an error when confirmation does not match', async () => {
    vm.currentPassword.set('old'); vm.newPassword.set('new12345'); vm.confirmPassword.set('different');
    await vm.submit();
    expect(auth.changePassword).not.toHaveBeenCalled();
    expect(vm.error()).toBe('كلمة المرور الجديدة وتأكيدها غير متطابقين');
  });

  it('navigates to /account on success', async () => {
    (auth.changePassword as jest.Mock).mockResolvedValue(ok(session));
    vm.currentPassword.set('old'); vm.newPassword.set('new12345'); vm.confirmPassword.set('new12345');
    await vm.submit();
    expect(auth.changePassword).toHaveBeenCalledWith('old', 'new12345');
    expect(router.navigate).toHaveBeenCalledWith(['/account']);
    expect(vm.error()).toBeNull();
  });

  it('surfaces the error message on failure', async () => {
    (auth.changePassword as jest.Mock).mockResolvedValue(fail(new AppError('Current password is incorrect', 'auth', 400, undefined, 'Current password is incorrect')));
    vm.currentPassword.set('wrong'); vm.newPassword.set('new12345'); vm.confirmPassword.set('new12345');
    await vm.submit();
    expect(vm.error()).toBe('Current password is incorrect');
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
