import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { LoginViewModel } from '@features/auth/presentation/pages/login/login.viewmodel';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';
import { AuthSession } from '@features/auth/domain/model/shared/auth';

const session: AuthSession = {
  tokens: { accessToken: 'a', refreshToken: 'r' },
  principal: { role: 'admin', userId: 'USR-ADMIN' },
  mustChangePassword: false,
};
const forcedSession: AuthSession = {
  tokens: { accessToken: 'a', refreshToken: 'r' },
  principal: { role: 'moqawel', userId: 'USR-MOQAWEL' },
  mustChangePassword: true,
};

describe('LoginViewModel', () => {
  const auth = {
    signIn: jest.fn(),
    role: jest.fn().mockReturnValue('admin'),
  } as unknown as AuthSessionStore;
  const router = { navigate: jest.fn() } as unknown as Router;
  let vm: LoginViewModel;
  beforeEach(() => {
    jest.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        LoginViewModel,
        { provide: AuthSessionStore, useValue: auth },
        { provide: Router, useValue: router },
      ],
    });
    vm = TestBed.inject(LoginViewModel);
  });
  it('navigates to /home on success', async () => {
    (auth.signIn as jest.Mock).mockResolvedValue(ok(session));
    vm.email.set('admin@example.com');
    vm.password.set('admin123');
    await vm.submit();
    expect(router.navigate).toHaveBeenCalledWith(['/home']);
    expect(vm.error()).toBeNull();
  });
  it('redirects to /change-password when mustChangePassword is set', async () => {
    (auth.signIn as jest.Mock).mockResolvedValue(ok(forcedSession));
    vm.email.set('moqawel@example.com');
    vm.password.set('moqawel123');
    await vm.submit();
    expect(router.navigate).toHaveBeenCalledWith(['/change-password']);
  });
  it('surfaces the error message on failure', async () => {
    (auth.signIn as jest.Mock).mockResolvedValue(
      fail(
        new AppError(
          'Invalid email or password',
          'auth',
          401,
          undefined,
          'Invalid email or password',
        ),
      ),
    );
    await vm.submit();
    expect(vm.error()).toBe('Invalid email or password');
    expect(router.navigate).not.toHaveBeenCalled();
  });
  it('bumps shakeKey on failed sign-in', async () => {
    (auth.signIn as jest.Mock).mockResolvedValue(fail(new AppError('bad', 'auth', 401)));
    const before = vm.shakeKey();
    await vm.submit();
    expect(vm.shakeKey()).toBe(before + 1);
  });
  it('sends a worker to /home', async () => {
    (auth.signIn as jest.Mock).mockResolvedValue(ok({ ...session, mustChangePassword: false }));
    (auth.role as jest.Mock).mockReturnValue('worker');
    await vm.submit();
    expect(router.navigate).toHaveBeenCalledWith(['/home']);
  });
  it('sends a moqawel to /home', async () => {
    (auth.signIn as jest.Mock).mockResolvedValue(ok({ ...session, mustChangePassword: false }));
    (auth.role as jest.Mock).mockReturnValue('moqawel');
    await vm.submit();
    expect(router.navigate).toHaveBeenCalledWith(['/home']);
  });
  it('sends a manager to /home', async () => {
    (auth.signIn as jest.Mock).mockResolvedValue(ok({ ...session, mustChangePassword: false }));
    (auth.role as jest.Mock).mockReturnValue('manager');
    await vm.submit();
    expect(router.navigate).toHaveBeenCalledWith(['/home']);
  });
});
