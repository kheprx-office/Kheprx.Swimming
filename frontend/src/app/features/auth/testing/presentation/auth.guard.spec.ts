import { TestBed } from '@angular/core/testing';
import { runInInjectionContext, Injector } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { authGuard, roleGuard } from '@features/auth/presentation/auth.guard';
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
    const { injector } = setup({ isAuthenticated: () => true, role: () => 'admin' } as Partial<AuthSessionStore>);
    expect(run(injector, roleGuard('admin'))).toBe(true);
  });
  it('redirects a non-matching role to /', () => {
    const { injector, router } = setup({ isAuthenticated: () => true, role: () => 'worker' } as Partial<AuthSessionStore>);
    expect(run(injector, roleGuard('admin'))).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/']);
  });
});
