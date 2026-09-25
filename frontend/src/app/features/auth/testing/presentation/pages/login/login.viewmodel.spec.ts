import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { LoginViewModel } from '@features/auth/presentation/pages/login/login.viewmodel';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { LoadRolesUseCase } from '@features/auth/domain/usecases/roles/load-roles.use-case';
import { LoadSwimmerCountUseCase } from '@features/swimmers/domain/usecases/load-swimmer-count.use-case';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';
import { AuthSession } from '@features/auth/domain/model/shared/auth';
import { RoleOption } from '@features/auth/domain/model/roles/role-option';

const session: AuthSession = {
  tokens: { accessToken: 'a', refreshToken: 'r' },
  principal: { role: 'head_coach', userId: 'USR-COACH' },
  mustChangePassword: false,
};
const forcedSession: AuthSession = {
  tokens: { accessToken: 'a', refreshToken: 'r' },
  principal: { role: 'captain', userId: 'USR-CAPTAIN' },
  mustChangePassword: true,
};

const headCoach: RoleOption = { code: 'head_coach', nameEn: 'Head Coach', nameAr: 'المدرب العام' };
const captain: RoleOption = { code: 'captain', nameEn: 'Captain', nameAr: 'الكابتن' };

describe('LoginViewModel', () => {
  const auth = {
    signIn: jest.fn(),
    role: jest.fn().mockReturnValue('head_coach'),
  } as unknown as AuthSessionStore;
  const router = { navigate: jest.fn() } as unknown as Router;
  const loadRoles = { run: jest.fn().mockResolvedValue(ok([])) };
  const loadSwimmerCount = { run: jest.fn().mockResolvedValue(ok(24)) };
  let vm: LoginViewModel;
  beforeEach(() => {
    jest.clearAllMocks();
    (loadRoles.run as jest.Mock).mockResolvedValue(ok([]));
    (loadSwimmerCount.run as jest.Mock).mockResolvedValue(ok(24));
    TestBed.configureTestingModule({
      providers: [
        LoginViewModel,
        { provide: AuthSessionStore, useValue: auth },
        { provide: Router, useValue: router },
        { provide: LoadRolesUseCase, useValue: loadRoles },
        { provide: LoadSwimmerCountUseCase, useValue: loadSwimmerCount },
      ],
    });
    vm = TestBed.inject(LoginViewModel);
  });
  it('navigates to /home on success', async () => {
    (auth.signIn as jest.Mock).mockResolvedValue(ok(session));
    vm.email.set('coach@example.com');
    vm.password.set('coach123');
    await vm.submit();
    expect(router.navigate).toHaveBeenCalledWith(['/home']);
    expect(vm.error()).toBeNull();
  });
  it('redirects to /change-password when mustChangePassword is set', async () => {
    (auth.signIn as jest.Mock).mockResolvedValue(ok(forcedSession));
    vm.email.set('captain@example.com');
    vm.password.set('captain123');
    await vm.submit();
    expect(router.navigate).toHaveBeenCalledWith(['/change-password']);
  });
  it('routes a first-login swimmer to /onboarding', async () => {
    const swimmerForced: AuthSession = { tokens: { accessToken: 'a', refreshToken: 'r' }, principal: { role: 'swimmer', userId: 'USR-SWIM' }, mustChangePassword: true };
    (auth.signIn as jest.Mock).mockResolvedValue(ok(swimmerForced));
    (auth.role as jest.Mock).mockReturnValue('swimmer');
    await vm.submit();
    expect(router.navigate).toHaveBeenCalledWith(['/onboarding']);
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
  it('surfaces the role-mismatch message from the server', async () => {
    (auth.signIn as jest.Mock).mockResolvedValue(
      fail(
        new AppError(
          'This account is not registered under the selected role',
          'auth',
          401,
          'ROLE_MISMATCH',
          'This account is not registered under the selected role',
        ),
      ),
    );
    await vm.submit();
    expect(vm.error()).toBe('This account is not registered under the selected role');
    expect(router.navigate).not.toHaveBeenCalled();
  });
  it('bumps shakeKey on failed sign-in', async () => {
    (auth.signIn as jest.Mock).mockResolvedValue(fail(new AppError('bad', 'auth', 401)));
    const before = vm.shakeKey();
    await vm.submit();
    expect(vm.shakeKey()).toBe(before + 1);
  });
  it('sends a captain to /home', async () => {
    (auth.signIn as jest.Mock).mockResolvedValue(ok({ ...session, mustChangePassword: false }));
    (auth.role as jest.Mock).mockReturnValue('captain');
    await vm.submit();
    expect(router.navigate).toHaveBeenCalledWith(['/home']);
  });
  it('sends a head_coach to /home', async () => {
    (auth.signIn as jest.Mock).mockResolvedValue(ok({ ...session, mustChangePassword: false }));
    (auth.role as jest.Mock).mockReturnValue('head_coach');
    await vm.submit();
    expect(router.navigate).toHaveBeenCalledWith(['/home']);
  });
  it('navigates a swimmer to /my-profile after login', async () => {
    const swimmerSession: AuthSession = { tokens: { accessToken: 'a', refreshToken: 'r' }, principal: { role: 'swimmer', userId: 'USR-SWIM' }, mustChangePassword: false };
    (auth.signIn as jest.Mock).mockResolvedValue(ok(swimmerSession));
    (auth.role as jest.Mock).mockReturnValue('swimmer');
    await vm.submit();
    expect(router.navigate).toHaveBeenCalledWith(['/my-profile']);
  });
  it('loads roles into the selector and keeps head_coach selected by default', async () => {
    (loadRoles.run as jest.Mock).mockResolvedValue(ok([headCoach, captain]));
    await vm.loadRoles();
    expect(vm.roles()).toEqual([headCoach, captain]);
    expect(vm.selectedRole()).toBe('head_coach');
  });
  it('falls back to the first role when the default is not offered', async () => {
    (loadRoles.run as jest.Mock).mockResolvedValue(ok([captain]));
    await vm.loadRoles();
    expect(vm.selectedRole()).toBe('captain');
  });
  it('sends the selected role to signIn', async () => {
    (auth.signIn as jest.Mock).mockResolvedValue(ok(session));
    vm.setRole('captain');
    await vm.submit();
    expect(auth.signIn).toHaveBeenCalledWith('headcoach@kheprx.local', 'Passw0rd!', 'captain');
  });
  it('loads the swimmer count into the signal', async () => {
    (loadSwimmerCount.run as jest.Mock).mockResolvedValue(ok(24));
    await vm.loadSwimmerCount();
    expect(vm.swimmerCount()).toBe(24);
  });
});
