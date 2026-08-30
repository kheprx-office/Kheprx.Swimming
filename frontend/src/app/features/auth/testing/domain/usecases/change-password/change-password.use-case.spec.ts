import { TestBed } from '@angular/core/testing';
import { ChangePasswordUseCase } from '@features/auth/domain/usecases/change-password/change-password.use-case';
import { AUTH_REPOSITORY, IAuthRepository } from '@features/auth/domain/repositories/auth.repository';
import { SessionDtoRs } from '@features/auth/data/dto/shared/session.dto';
import { TokenStore } from '@features/auth/data/token-store';
import { KeyValueStore } from '@core/datasource/keyvalue/key-value.store';
import { AppError } from '@core/domain/errors/app-error';

const rotated: SessionDtoRs = { accessToken: 'new-a', refreshToken: 'new-r', role: 'moqawel', userId: 'USR-MOQAWEL', mustChangePassword: false };

function makeRepo(overrides: Partial<IAuthRepository> = {}): IAuthRepository {
  return {
    login: jest.fn(), refresh: jest.fn(), logout: jest.fn(), me: jest.fn(),
    changePassword: async () => ({ data: rotated }),
    ...overrides,
  } as unknown as IAuthRepository;
}

describe('ChangePasswordUseCase', () => {
  let tokens: TokenStore;
  const build = (repo: IAuthRepository) => {
    TestBed.resetTestingModule();
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [ChangePasswordUseCase, TokenStore, KeyValueStore, { provide: AUTH_REPOSITORY, useValue: repo }],
    });
    tokens = TestBed.inject(TokenStore);
    return TestBed.inject(ChangePasswordUseCase);
  };

  it('validates + maps, saves the rotated tokens, returns the fresh session', async () => {
    const uc = build(makeRepo());
    const r = await uc.run({ currentPassword: 'old', newPassword: 'new12345' });
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.data.mustChangePassword).toBe(false);
    expect(await tokens.getAccess()).toBe('new-a');
  });

  it('returns fail(auth) when the current password is wrong', async () => {
    const uc = build(makeRepo({ changePassword: async () => { throw new AppError('Current password is incorrect', 'auth', 400); } }));
    const r = await uc.run({ currentPassword: 'wrong', newPassword: 'new12345' });
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('auth');
  });

  it('returns fail(validation) on a malformed session DTO', async () => {
    const uc = build(makeRepo({ changePassword: async () => ({ data: { ...rotated, role: 'superadmin' } }) }));
    const r = await uc.run({ currentPassword: 'old', newPassword: 'new12345' });
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
