import { TestBed } from '@angular/core/testing';
import { LogoutUseCase } from '@features/auth/domain/usecases/account/logout.use-case';
import { AUTH_REPOSITORY, IAuthRepository } from '@features/auth/domain/repositories/auth.repository';
import { TokenStore } from '@features/auth/data/token-store';
import { KeyValueStore } from '@core/datasource/keyvalue/key-value.store';

function makeRepo(overrides: Partial<IAuthRepository> = {}): IAuthRepository {
  return {
    login: jest.fn(),
    refresh: jest.fn(),
    logout: jest.fn().mockResolvedValue(undefined),
    me: jest.fn(),
    changePassword: jest.fn(),
    ...overrides,
  } as unknown as IAuthRepository;
}

describe('LogoutUseCase', () => {
  let tokens: TokenStore;
  const build = (repo: IAuthRepository) => {
    TestBed.resetTestingModule();
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [LogoutUseCase, TokenStore, KeyValueStore, { provide: AUTH_REPOSITORY, useValue: repo }],
    });
    tokens = TestBed.inject(TokenStore);
    return TestBed.inject(LogoutUseCase);
  };

  it('calls the server revoke then clears local tokens', async () => {
    const repo = makeRepo();
    const uc = build(repo);
    await tokens.save({ accessToken: 'a', refreshToken: 'r' });
    const r = await uc.run();
    expect(r.ok).toBe(true);
    expect(repo.logout).toHaveBeenCalled();
    expect(await tokens.getAccess()).toBeNull();
  });

  it('still clears local tokens when the server revoke fails', async () => {
    const repo = makeRepo({ logout: jest.fn().mockRejectedValue(new Error('network')) });
    const uc = build(repo);
    await tokens.save({ accessToken: 'a', refreshToken: 'r' });
    const r = await uc.run();
    expect(r.ok).toBe(true);
    expect(await tokens.getAccess()).toBeNull();
  });
});
