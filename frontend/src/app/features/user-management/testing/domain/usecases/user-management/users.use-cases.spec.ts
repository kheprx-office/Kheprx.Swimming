import { TestBed } from '@angular/core/testing';
import { ListUsersUseCase } from '@features/user-management/domain/usecases/user-management/list-users.use-case';
import { CreateUserUseCase } from '@features/user-management/domain/usecases/user-management/create-user.use-case';
import { UpdateUserUseCase } from '@features/user-management/domain/usecases/user-management/update-user.use-case';
import { SetUserStatusUseCase } from '@features/user-management/domain/usecases/user-management/set-user-status.use-case';
import { USERS_REPOSITORY, IUsersRepository } from '@features/user-management/domain/repositories/users.repository';
import { UserDtoRs } from '@features/user-management/data/dto/user-management/user.dto';
import { User, CreateUserInput } from '@features/user-management/domain/model/user-management/user';
import { AppError } from '@core/domain/errors/app-error';

const dto: UserDtoRs = {
  id: 'USR-1', code: 'W-1', fullName: 'A', email: 'a@b.c', phone: null,
  gender: 'male', age: 24, nid: '29801014501234', role: 'worker', status: 'active',
  mustChangePassword: false,
  profile: { monthlySalary: null, dailyWage: 350 },
};
const user: User = {
  id: 'USR-1', code: 'W-1', fullName: 'A', email: 'a@b.c', phone: null,
  gender: 'male', age: 24, nid: '29801014501234', role: 'worker', status: 'active',
  mustChangePassword: false,
  profile: { monthlySalary: null, dailyWage: 350 },
};
const workerInput: CreateUserInput = {
  fullName: 'A', role: 'worker', nid: '29801014501234',
  email: 'a@b.c', password: 'password8', phone: null, gender: 'male', age: 24,
  monthlySalary: null, dailyWage: 350,
};
function repo(overrides: Partial<IUsersRepository> = {}): IUsersRepository {
  return {
    list: async () => ({ data: [dto] }),
    create: async () => ({ data: dto }),
    update: async () => ({ data: dto }),
    setStatus: async () => ({ data: { ...dto, status: 'disabled' } }),
    ...overrides,
  } as IUsersRepository;
}

function configure(r: IUsersRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      { provide: USERS_REPOSITORY, useValue: r },
    ],
  });
}

describe('users use-cases', () => {
  it('ListUsersUseCase forwards the search term, validates + maps DTOs', async () => {
    const list = jest.fn().mockResolvedValue({ data: [dto] });
    configure(repo({ list }));
    const res = await TestBed.inject(ListUsersUseCase).run('29801');
    expect(list).toHaveBeenCalledWith('29801');
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data).toEqual([user]);
  });

  it('ListUsersUseCase fails with a validation error on a malformed DTO', async () => {
    configure(repo({ list: async () => ({ data: [{ ...dto, status: 'bogus' }] }) }));
    const res = await TestBed.inject(ListUsersUseCase).run(undefined);
    expect(res.ok).toBe(false);
    if (!res.ok) expect(res.error.kind).toBe('validation');
  });

  it('CreateUserUseCase sends the role-gated request DTO and maps the result', async () => {
    const create = jest.fn().mockResolvedValue({ data: dto });
    configure(repo({ create }));
    const res = await TestBed.inject(CreateUserUseCase).run({ ...workerInput, monthlySalary: 15000 });
    expect(create).toHaveBeenCalledWith({
      fullName: 'A', role: 'worker', nid: '29801014501234',
      email: 'a@b.c', password: 'password8', phone: null, gender: 'male', age: 24,
      monthlySalary: null, dailyWage: 350,
    });
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data).toEqual(user);
  });

  it('CreateUserUseCase surfaces a repository failure (with code) as Result.error', async () => {
    configure(repo({ create: async () => { throw new AppError('الرقم القومي مستخدم بالفعل', 'http', 409, 'NID_IN_USE'); } }));
    const res = await TestBed.inject(CreateUserUseCase).run(workerInput);
    expect(res.ok).toBe(false);
    if (!res.ok) { expect(res.error.status).toBe(409); expect(res.error.code).toBe('NID_IN_USE'); }
  });

  it('UpdateUserUseCase forwards id + role echo + status in the request DTO', async () => {
    const update = jest.fn().mockResolvedValue({ data: dto });
    configure(repo({ update }));
    await TestBed.inject(UpdateUserUseCase).run({ id: 'USR-1', input: { ...workerInput, status: 'active' } });
    expect(update).toHaveBeenCalledWith('USR-1', expect.objectContaining({
      role: 'worker', nid: '29801014501234', status: 'active', dailyWage: 350,
    }));
  });

  it('SetUserStatusUseCase forwards id + status DTO and maps the result', async () => {
    const setStatus = jest.fn().mockResolvedValue({ data: { ...dto, status: 'disabled' } });
    configure(repo({ setStatus }));
    const res = await TestBed.inject(SetUserStatusUseCase).run({ id: 'USR-1', status: 'disabled' });
    expect(setStatus).toHaveBeenCalledWith('USR-1', { status: 'disabled' });
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data.status).toBe('disabled');
  });
});
