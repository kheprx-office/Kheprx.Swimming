import { TestBed } from '@angular/core/testing';
import { runInInjectionContext, Injector } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { authGuard, roleGuard, firstLoginGuard } from '@features/auth/presentation/auth.guard';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';

function setup(auth: Partial<AuthSessionStore>) {
  const router = { navigate: jest.fn() } as unknown as Router;
  TestBed.configureTestingModule({
    providers: [
      { provide: AuthSessionStore, useValue: auth },
      { provide: Router, useValue: router },
    ],
  });
  return { injector: TestBed.inject(Injector), router };
}
const run = (injector: Injector, guard: CanActivateFn) =>
  runInInjectionContext(injector, () => guard({} as never, {} as never));

describe('authGuard', () => {
  it('allows authenticated users', () => {
    const { injector } = setup({ isAuthenticated: () => true } as Partial<AuthSessionStore>);
    expect(run(injector, authGuard)).toBe(true);
  });
  it('redirects anonymous users to /login', () => {
    const { injector, router } = setup({ isAuthenticated: () => false } as Partial<AuthSessionStore>);
    expect(run(injector, authGuard)).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });
});

describe('roleGuard', () => {
  it('allows a matching role', () => {
    const { injector } = setup({ isAuthenticated: () => true, role: () => 'head_coach' } as Partial<AuthSessionStore>);
    expect(run(injector, roleGuard('head_coach'))).toBe(true);
  });
  it('redirects a non-matching role to /', () => {
    const { injector, router } = setup({ isAuthenticated: () => true, role: () => 'captain' } as Partial<AuthSessionStore>);
    expect(run(injector, roleGuard('head_coach'))).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/']);
  });
});

describe('firstLoginGuard', () => {
  it('redirects to /change-password when mustChangePassword is true', () => {
    const { injector, router } = setup({ mustChangePassword: () => true } as Partial<AuthSessionStore>);
    expect(run(injector, firstLoginGuard)).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/change-password']);
  });
  it('allows navigation when mustChangePassword is false', () => {
    const { injector, router } = setup({ mustChangePassword: () => false } as Partial<AuthSessionStore>);
    expect(run(injector, firstLoginGuard)).toBe(true);
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
