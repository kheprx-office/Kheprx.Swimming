import { TestBed } from '@angular/core/testing';
import { RefreshTokenUseCase } from '@features/auth/domain/usecases/shared/refresh-token.use-case';
import { AUTH_REPOSITORY, IAuthRepository } from '@features/auth/domain/repositories/auth.repository';
import { SessionDtoRs } from '@features/auth/data/dto/shared/session.dto';
import { TokenStore } from '@features/auth/data/token-store';
import { KeyValueStore } from '@core/datasource/keyvalue/key-value.store';

const sessionDto: SessionDtoRs = { accessToken: 'a2', refreshToken: 'r2', role: 'admin', userId: 'USR-ADMIN', mustChangePassword: false };

function makeRepo(overrides: Partial<IAuthRepository> = {}): IAuthRepository {
  return {
    login: async () => ({ data: sessionDto }),
    refresh: async () => ({ data: sessionDto }),
    logout: async () => undefined,
    me: async () => ({ data: { userId: 'USR-ADMIN', email: 'a@b.c', fullName: 'A', role: 'admin' } }),
    changePassword: async () => ({ data: sessionDto }),
    ...overrides,
  } as IAuthRepository;
}

function build(repo: IAuthRepository) {
  TestBed.resetTestingModule();
  localStorage.clear();
  TestBed.configureTestingModule({
    providers: [RefreshTokenUseCase, TokenStore, KeyValueStore, { provide: AUTH_REPOSITORY, useValue: repo }],
  });
  return { uc: TestBed.inject(RefreshTokenUseCase), tokens: TestBed.inject(TokenStore) };
}

describe('RefreshTokenUseCase', () => {
  it('fails with auth when no refresh token is stored', async () => {
    const { uc } = build(makeRepo());
    const r = await uc.run();
    expect(r.ok).toBe(false);
    if (!r.ok) { expect(r.error.kind).toBe('auth'); expect(r.error.status).toBe(401); }
  });

  it('validates + maps and persists the new tokens', async () => {
    const { uc, tokens } = build(makeRepo());
    await tokens.save({ accessToken: 'a', refreshToken: 'r' });
    const r = await uc.run();
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.data.accessToken).toBe('a2');
    expect(await tokens.getAccess()).toBe('a2');
  });

  it('returns fail(validation) on a malformed refresh session DTO', async () => {
    const { uc, tokens } = build(makeRepo({ refresh: async () => ({ data: { ...sessionDto, accessToken: '' } }) }));
    await tokens.save({ accessToken: 'a', refreshToken: 'r' });
    const r = await uc.run();
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
