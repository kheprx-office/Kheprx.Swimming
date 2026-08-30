import { TestBed } from '@angular/core/testing';
import { LoadCurrentUserUseCase } from '@features/auth/domain/usecases/shared/load-current-user.use-case';
import { AUTH_REPOSITORY, IAuthRepository } from '@features/auth/domain/repositories/auth.repository';
import { CurrentUserDtoRs } from '@features/auth/data/dto/shared/current-user.dto';

const userDto: CurrentUserDtoRs = { userId: 'USR-1', email: 'a@b.c', fullName: 'A', role: 'admin', phone: '0100', gender: 'female', age: 25 };

function repo(overrides: Partial<IAuthRepository> = {}): IAuthRepository {
  return {
    login: async () => { throw new Error('unused'); },
    refresh: async () => { throw new Error('unused'); },
    logout: async () => undefined,
    me: async () => ({ data: userDto }),
    changePassword: async () => { throw new Error('unused'); },
    ...overrides,
  } as IAuthRepository;
}

function configure(r: IAuthRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: AUTH_REPOSITORY, useValue: r }] });
}

describe('LoadCurrentUserUseCase', () => {
  it('validates + maps the current-user DTO to a CurrentUser', async () => {
    configure(repo());
    const res = await TestBed.inject(LoadCurrentUserUseCase).run();
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data).toEqual({ userId: 'USR-1', email: 'a@b.c', fullName: 'A', role: 'admin', phone: '0100', gender: 'female', age: 25 });
  });

  it('fails with a validation error on a malformed DTO', async () => {
    configure(repo({ me: async () => ({ data: { ...userDto, role: 'superadmin' } }) }));
    const res = await TestBed.inject(LoadCurrentUserUseCase).run();
    expect(res.ok).toBe(false);
    if (!res.ok) expect(res.error.kind).toBe('validation');
  });
});
