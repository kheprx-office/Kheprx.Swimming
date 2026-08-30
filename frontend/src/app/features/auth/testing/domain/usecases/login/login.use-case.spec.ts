import { TestBed } from '@angular/core/testing';
import { LoginUseCase } from '@features/auth/domain/usecases/login/login.use-case';
import { AUTH_REPOSITORY, IAuthRepository } from '@features/auth/domain/repositories/auth.repository';
import { SessionDtoRs } from '@features/auth/data/dto/shared/session.dto';
import { TokenStore } from '@features/auth/data/token-store';
import { KeyValueStore } from '@core/datasource/keyvalue/key-value.store';
import { AppError } from '@core/domain/errors/app-error';

const sessionDto: SessionDtoRs = { accessToken: 'a', refreshToken: 'r', role: 'admin', userId: 'USR-ADMIN', mustChangePassword: false };

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
    providers: [LoginUseCase, TokenStore, KeyValueStore, { provide: AUTH_REPOSITORY, useValue: repo }],
  });
  return TestBed.inject(LoginUseCase);
}

describe('LoginUseCase', () => {
  it('validates + maps the session and persists tokens', async () => {
    const uc = build(makeRepo());
    const r = await uc.run({ email: 'admin@example.com', password: 'admin123' });
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.data.principal.role).toBe('admin');
    expect(await TestBed.inject(TokenStore).getAccess()).toBe('a');
  });

  it('returns fail(auth) on bad credentials', async () => {
    const uc = build(makeRepo({ login: async () => { throw new AppError('bad', 'auth', 401); } }));
    const r = await uc.run({ email: 'x@y.z', password: 'nope' });
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('auth');
  });

  it('returns fail(validation) on a malformed session DTO', async () => {
    const uc = build(makeRepo({ login: async () => ({ data: { ...sessionDto, role: 'superadmin' } }) }));
    const r = await uc.run({ email: 'admin@example.com', password: 'admin123' });
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
