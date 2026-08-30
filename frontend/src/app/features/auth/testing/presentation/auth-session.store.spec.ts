import { TestBed } from '@angular/core/testing';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { LoginUseCase } from '@features/auth/domain/usecases/login/login.use-case';
import { LogoutUseCase } from '@features/auth/domain/usecases/account/logout.use-case';
import { ChangePasswordUseCase } from '@features/auth/domain/usecases/change-password/change-password.use-case';
import { LoadCurrentUserUseCase } from '@features/auth/domain/usecases/shared/load-current-user.use-case';
import { TokenStore } from '@features/auth/data/token-store';
import { KeyValueStore } from '@core/datasource/keyvalue/key-value.store';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';
import { AuthSession } from '@features/auth/domain/model/shared/auth';
import { StorageKeys } from '@core/datasource/keyvalue/storage-keys';

const session: AuthSession = {
  tokens: { accessToken: 'na', refreshToken: 'nr' },
  principal: { role: 'admin', userId: 'USR-ADMIN' },
  mustChangePassword: false,
};

describe('AuthSessionStore', () => {
  let login: { run: jest.Mock };
  let logout: { run: jest.Mock };
  let changePassword: { run: jest.Mock };
  let loadCurrentUser: { run: jest.Mock };

  function make(): AuthSessionStore {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        AuthSessionStore, TokenStore, KeyValueStore,
        { provide: LoginUseCase, useValue: login },
        { provide: LogoutUseCase, useValue: logout },
        { provide: ChangePasswordUseCase, useValue: changePassword },
        { provide: LoadCurrentUserUseCase, useValue: loadCurrentUser },
      ],
    });
    return TestBed.inject(AuthSessionStore);
  }

  beforeEach(() => {
    localStorage.clear();
    login = { run: jest.fn() };
    logout = { run: jest.fn().mockResolvedValue(ok(undefined)) };
    changePassword = { run: jest.fn() };
    loadCurrentUser = { run: jest.fn() };
  });

  it('starts unauthenticated', () => {
    const store = make();
    expect(store.isAuthenticated()).toBe(false);
    expect(store.role()).toBeNull();
  });

  it('signIn populates the session', async () => {
    login.run.mockResolvedValue(ok(session));
    const store = make();
    const r = await store.signIn('admin@example.com', 'admin123');
    expect(r.ok).toBe(true);
    expect(store.isAuthenticated()).toBe(true);
    expect(store.role()).toBe('admin');
    expect(store.principal()?.userId).toBe('USR-ADMIN');
  });

  it('signOut clears the session', async () => {
    login.run.mockResolvedValue(ok(session));
    const store = make();
    await store.signIn('admin@example.com', 'admin123');
    await store.signOut();
    expect(store.isAuthenticated()).toBe(false);
  });

  it('signIn failure leaves the store unauthenticated', async () => {
    login.run.mockResolvedValue(fail(new AppError('bad credentials', 'auth', 401)));
    const store = make();
    const r = await store.signIn('x@example.com', 'wrong');
    expect(r.ok).toBe(false);
    expect(store.isAuthenticated()).toBe(false);
    expect(store.role()).toBeNull();
  });

  it('rehydrates the session via LoadCurrentUserUseCase when tokens are present', async () => {
    localStorage.setItem(StorageKeys.accessToken, 'jwt-access');
    localStorage.setItem(StorageKeys.refreshToken, 'jwt-refresh');
    loadCurrentUser.run.mockResolvedValue(ok({ userId: 'USR-ADMIN', email: 'a@b.c', fullName: 'Admin', role: 'admin' }));
    const store = make();
    await Promise.resolve();
    await Promise.resolve();
    expect(loadCurrentUser.run).toHaveBeenCalled();
    expect(store.isAuthenticated()).toBe(true);
    expect(store.role()).toBe('admin');
    expect(store.principal()?.userId).toBe('USR-ADMIN');
    expect(store.principal()).toEqual({ role: 'admin', userId: 'USR-ADMIN' });
  });

  it('stays unauthenticated when LoadCurrentUser fails', async () => {
    localStorage.setItem(StorageKeys.accessToken, 'jwt-access');
    localStorage.setItem(StorageKeys.refreshToken, 'jwt-refresh');
    loadCurrentUser.run.mockResolvedValue(fail(new AppError('invalid', 'validation')));
    const store = make();
    await Promise.resolve();
    await Promise.resolve();
    expect(store.isAuthenticated()).toBe(false);
  });

  // Regression: on reload the guard must not run before rehydration finishes.
  // whenReady() lets an APP_INITIALIZER await the startup GET /api/auth/me so the
  // session is populated before any route guard reads isAuthenticated().
  it('whenReady() resolves after rehydration so a ready session is observable', async () => {
    localStorage.setItem(StorageKeys.accessToken, 'jwt-access');
    localStorage.setItem(StorageKeys.refreshToken, 'jwt-refresh');
    loadCurrentUser.run.mockResolvedValue(ok({ userId: 'USR-ADMIN', email: 'a@b.c', fullName: 'Admin', role: 'admin' }));
    const store = make();
    await store.whenReady();
    expect(store.isAuthenticated()).toBe(true);
    expect(store.role()).toBe('admin');
  });

  it('whenReady() resolves without blocking bootstrap when no tokens are stored', async () => {
    const store = make();
    await store.whenReady();
    expect(loadCurrentUser.run).not.toHaveBeenCalled();
    expect(store.isAuthenticated()).toBe(false);
  });

  it('changePassword updates the session with the rotated result', async () => {
    changePassword.run.mockResolvedValue(ok(session));
    const store = make();
    const r = await store.changePassword('old', 'new12345');
    expect(r.ok).toBe(true);
    expect(store.isAuthenticated()).toBe(true);
    expect(store.mustChangePassword()).toBe(false);
    expect(store.session()?.tokens.accessToken).toBe('na');
  });

  it('signIn loads the display name from the profile', async () => {
    login.run.mockResolvedValue(ok(session));
    loadCurrentUser.run.mockResolvedValue(ok({ userId: 'USR-ADMIN', email: 'a@b.c', fullName: 'Admin User', role: 'admin' }));
    const store = make();
    await store.signIn('admin@example.com', 'admin123');
    expect(store.fullName()).toBe('Admin User');
    expect(store.currentUserName()).toBe('Admin User');
  });

  it('keeps signIn successful and falls back to the userId when the profile fetch fails', async () => {
    login.run.mockResolvedValue(ok(session));
    loadCurrentUser.run.mockResolvedValue(fail(new AppError('nope', 'network')));
    const store = make();
    const r = await store.signIn('admin@example.com', 'admin123');
    expect(r.ok).toBe(true);
    expect(store.isAuthenticated()).toBe(true);
    expect(store.fullName()).toBeNull();
    expect(store.currentUserName()).toBe('USR-ADMIN');
  });

  it('rehydrate populates the display name from the profile', async () => {
    localStorage.setItem(StorageKeys.accessToken, 'jwt-access');
    localStorage.setItem(StorageKeys.refreshToken, 'jwt-refresh');
    loadCurrentUser.run.mockResolvedValue(ok({ userId: 'USR-ADMIN', email: 'a@b.c', fullName: 'Admin User', role: 'admin' }));
    const store = make();
    await store.whenReady();
    expect(store.currentUserName()).toBe('Admin User');
  });

  it('signOut clears the display name', async () => {
    login.run.mockResolvedValue(ok(session));
    loadCurrentUser.run.mockResolvedValue(ok({ userId: 'USR-ADMIN', email: 'a@b.c', fullName: 'Admin User', role: 'admin' }));
    const store = make();
    await store.signIn('admin@example.com', 'admin123');
    expect(store.currentUserName()).toBe('Admin User');
    await store.signOut();
    expect(store.fullName()).toBeNull();
  });
});
