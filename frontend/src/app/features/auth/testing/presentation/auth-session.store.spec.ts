import { TestBed } from '@angular/core/testing';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { LoginUseCase } from '@features/auth/domain/usecases/login/login.use-case';
import { LogoutUseCase } from '@features/auth/domain/usecases/account/logout.use-case';
import { ChangePasswordUseCase } from '@features/auth/domain/usecases/change-password/change-password.use-case';
import { LoadCurrentUserUseCase } from '@features/auth/domain/usecases/shared/load-current-user.use-case';
import { TokenStore } from '@features/auth/data/token-store';
import { KeyValueStore } from '@core/datasource/keyvalue/key-value.store';
import { LanguageStore } from '@core/i18n';
import { ok, fail } from '@core/domain/result/result';
import { AppError } from '@core/domain/errors/app-error';
import { AuthSession } from '@features/auth/domain/model/shared/auth';
import { StorageKeys } from '@core/datasource/keyvalue/storage-keys';

const session: AuthSession = {
  tokens: { accessToken: 'na', refreshToken: 'nr' },
  principal: { role: 'head_coach', userId: 'USR-COACH' },
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
        AuthSessionStore, TokenStore, KeyValueStore, LanguageStore,
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
    const r = await store.signIn('coach@example.com', 'coach123', 'head_coach');
    expect(r.ok).toBe(true);
    expect(store.isAuthenticated()).toBe(true);
    expect(store.role()).toBe('head_coach');
    expect(store.principal()?.userId).toBe('USR-COACH');
  });

  it('signOut clears the session', async () => {
    login.run.mockResolvedValue(ok(session));
    const store = make();
    await store.signIn('coach@example.com', 'coach123', 'head_coach');
    await store.signOut();
    expect(store.isAuthenticated()).toBe(false);
  });

  it('signIn failure leaves the store unauthenticated', async () => {
    login.run.mockResolvedValue(fail(new AppError('bad credentials', 'auth', 401)));
    const store = make();
    const r = await store.signIn('x@example.com', 'wrong', 'head_coach');
    expect(r.ok).toBe(false);
    expect(store.isAuthenticated()).toBe(false);
    expect(store.role()).toBeNull();
  });

  it('rehydrates the session via LoadCurrentUserUseCase when tokens are present', async () => {
    localStorage.setItem(StorageKeys.accessToken, 'jwt-access');
    localStorage.setItem(StorageKeys.refreshToken, 'jwt-refresh');
    loadCurrentUser.run.mockResolvedValue(ok({ userId: 'USR-COACH', email: 'a@b.c', nameEn: 'Coach', nameAr: null, role: 'head_coach', phone: null, gender: null, age: null, nationalId: null }));
    const store = make();
    await Promise.resolve();
    await Promise.resolve();
    expect(loadCurrentUser.run).toHaveBeenCalled();
    expect(store.isAuthenticated()).toBe(true);
    expect(store.role()).toBe('head_coach');
    expect(store.principal()?.userId).toBe('USR-COACH');
    expect(store.principal()).toEqual({ role: 'head_coach', userId: 'USR-COACH' });
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
    loadCurrentUser.run.mockResolvedValue(ok({ userId: 'USR-COACH', email: 'a@b.c', nameEn: 'Coach', nameAr: null, role: 'head_coach', phone: null, gender: null, age: null, nationalId: null }));
    const store = make();
    await store.whenReady();
    expect(store.isAuthenticated()).toBe(true);
    expect(store.role()).toBe('head_coach');
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

  it('signIn loads the display name from the profile (en)', async () => {
    login.run.mockResolvedValue(ok(session));
    loadCurrentUser.run.mockResolvedValue(ok({ userId: 'USR-COACH', email: 'a@b.c', nameEn: 'Dave', nameAr: 'ديف', role: 'head_coach', phone: null, gender: null, age: null, nationalId: null }));
    const store = make();
    await store.signIn('coach@example.com', 'coach123', 'head_coach');
    // Default language is 'en'
    expect(store.displayName()).toBe('Dave');
    expect(store.currentUserName()).toBe('Dave');
  });

  it('displayName returns the Arabic name when language is set to ar', async () => {
    login.run.mockResolvedValue(ok(session));
    loadCurrentUser.run.mockResolvedValue(ok({ userId: 'USR-COACH', email: 'a@b.c', nameEn: 'Dave', nameAr: 'ديف', role: 'head_coach', phone: null, gender: null, age: null, nationalId: null }));
    const store = make();
    await store.signIn('coach@example.com', 'coach123', 'head_coach');
    const langStore = TestBed.inject(LanguageStore);
    langStore.set('ar');
    expect(store.displayName()).toBe('ديف');
    expect(store.currentUserName()).toBe('ديف');
  });

  it('displayName falls back to nameEn when nameAr is null and lang is ar', async () => {
    login.run.mockResolvedValue(ok(session));
    loadCurrentUser.run.mockResolvedValue(ok({ userId: 'USR-COACH', email: 'a@b.c', nameEn: 'Dave', nameAr: null, role: 'head_coach', phone: null, gender: null, age: null, nationalId: null }));
    const store = make();
    await store.signIn('coach@example.com', 'coach123', 'head_coach');
    const langStore = TestBed.inject(LanguageStore);
    langStore.set('ar');
    expect(store.displayName()).toBe('Dave');
  });

  it('keeps signIn successful and falls back to the userId when the profile fetch fails', async () => {
    login.run.mockResolvedValue(ok(session));
    loadCurrentUser.run.mockResolvedValue(fail(new AppError('nope', 'network')));
    const store = make();
    const r = await store.signIn('coach@example.com', 'coach123', 'head_coach');
    expect(r.ok).toBe(true);
    expect(store.isAuthenticated()).toBe(true);
    expect(store.displayName()).toBe('');
    expect(store.currentUserName()).toBe('USR-COACH');
  });

  it('rehydrate populates the display name from the profile', async () => {
    localStorage.setItem(StorageKeys.accessToken, 'jwt-access');
    localStorage.setItem(StorageKeys.refreshToken, 'jwt-refresh');
    loadCurrentUser.run.mockResolvedValue(ok({ userId: 'USR-COACH', email: 'a@b.c', nameEn: 'Coach User', nameAr: 'كوتش', role: 'head_coach', phone: null, gender: null, age: null, nationalId: null }));
    const store = make();
    await store.whenReady();
    expect(store.currentUserName()).toBe('Coach User');
  });

  it('markOnboardingComplete flips mustChangePassword to false', async () => {
    login.run.mockResolvedValue(ok({ ...session, principal: { role: 'swimmer', userId: 'USR-SWIM' }, mustChangePassword: true }));
    const store = make();
    await store.signIn('swimmer@example.com', 'Oasis2026!', 'swimmer');
    expect(store.mustChangePassword()).toBe(true);
    store.markOnboardingComplete();
    expect(store.mustChangePassword()).toBe(false);
    expect(store.isAuthenticated()).toBe(true);
  });

  it('signOut clears the display name', async () => {
    login.run.mockResolvedValue(ok(session));
    loadCurrentUser.run.mockResolvedValue(ok({ userId: 'USR-COACH', email: 'a@b.c', nameEn: 'Coach User', nameAr: null, role: 'head_coach', phone: null, gender: null, age: null, nationalId: null }));
    const store = make();
    await store.signIn('coach@example.com', 'coach123', 'head_coach');
    expect(store.currentUserName()).toBe('Coach User');
    await store.signOut();
    expect(store.displayName()).toBe('');
    expect(store.currentUserName()).toBe('');
  });
});
