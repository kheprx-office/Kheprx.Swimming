import { TestBed } from '@angular/core/testing';
import { LoadRolesUseCase } from '@features/auth/domain/usecases/roles/load-roles.use-case';
import { AUTH_REPOSITORY, IAuthRepository } from '@features/auth/domain/repositories/auth.repository';
import { RoleDtoRs } from '@features/auth/data/dto/roles/role.dto';

const roles: RoleDtoRs[] = [
  { id: 'r1', code: 'head_coach', nameEn: 'Head Coach', nameAr: 'المدرب العام' },
  { id: 'r2', code: 'captain', nameEn: 'Captain', nameAr: 'الكابتن' },
];

function makeRepo(overrides: Partial<IAuthRepository> = {}): IAuthRepository {
  return {
    login: jest.fn(), refresh: jest.fn(), logout: jest.fn(), me: jest.fn(), changePassword: jest.fn(),
    getRoles: async () => ({ data: roles }),
    ...overrides,
  } as unknown as IAuthRepository;
}

function build(repo: IAuthRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: AUTH_REPOSITORY, useValue: repo }] });
  return TestBed.inject(LoadRolesUseCase);
}

describe('LoadRolesUseCase', () => {
  it('validates + maps the roles to role options', async () => {
    const uc = build(makeRepo());
    const r = await uc.run();
    expect(r.ok).toBe(true);
    if (r.ok) {
      expect(r.data).toEqual([
        { code: 'head_coach', nameEn: 'Head Coach', nameAr: 'المدرب العام' },
        { code: 'captain', nameEn: 'Captain', nameAr: 'الكابتن' },
      ]);
    }
  });

  it('returns an empty list when the server sends none', async () => {
    const uc = build(makeRepo({ getRoles: async () => ({ data: [] }) }));
    const r = await uc.run();
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.data).toEqual([]);
  });

  it('returns fail(validation) on a malformed role row', async () => {
    const uc = build(makeRepo({ getRoles: async () => ({ data: [{ id: 'r1', code: '', nameEn: 'X', nameAr: null }] }) }));
    const r = await uc.run();
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.error.kind).toBe('validation');
  });
});
