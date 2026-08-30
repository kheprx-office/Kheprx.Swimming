import { UserDtoRs } from '@features/user-management/data/dto/user-management/user.dto';
import {
  CreateUserInput, UpdateUserInput, toUser, toCreateUserDtoRq, toUpdateUserDtoRq, GENDER_LABELS, formatCompensation,
} from '@features/user-management/domain/model/user-management/user';

const dto: UserDtoRs = {
  id: 'USR-1', code: 'W-1', fullName: 'A', email: 'a@b.c', phone: null,
  gender: 'male', age: 24, nid: '29801014501234', role: 'worker', status: 'active',
  mustChangePassword: false,
  profile: { monthlySalary: null, dailyWage: 350 },
};

const workerInput: CreateUserInput = {
  fullName: 'A', role: 'worker', nid: '29801014501234',
  email: 'a@b.c', password: 'password8', phone: null, gender: 'male', age: 24,
  monthlySalary: 15000, dailyWage: 350,
};

describe('toUser', () => {
  it('maps every field including the profile', () => {
    const u = toUser(dto);
    expect(u).toEqual({
      id: 'USR-1', code: 'W-1', fullName: 'A', email: 'a@b.c', phone: null,
      gender: 'male', age: 24, nid: '29801014501234', role: 'worker', status: 'active',
      mustChangePassword: false,
      profile: { monthlySalary: null, dailyWage: 350 },
    });
    expect(u.profile).not.toBe(dto.profile); // cloned, not aliased
  });

  it('keeps a null profile null', () => {
    expect(toUser({ ...dto, role: 'admin', profile: null }).profile).toBeNull();
  });
});

describe('toCreateUserDtoRq role gating', () => {
  it('worker: keeps dailyWage, nulls monthlySalary', () => {
    expect(toCreateUserDtoRq(workerInput)).toEqual({
      fullName: 'A', role: 'worker', nid: '29801014501234',
      email: 'a@b.c', password: 'password8', phone: null, gender: 'male', age: 24,
      monthlySalary: null, dailyWage: 350,
    });
  });

  it('manager: keeps monthlySalary, nulls wage fields', () => {
    const rq = toCreateUserDtoRq({ ...workerInput, role: 'manager' });
    expect(rq.monthlySalary).toBe(15000);
    expect(rq.dailyWage).toBeNull();
  });

  it('moqawel: keeps wage fields, nulls monthlySalary', () => {
    const rq = toCreateUserDtoRq({ ...workerInput, role: 'moqawel' });
    expect(rq.dailyWage).toBe(350);
    expect(rq.monthlySalary).toBeNull();
  });

  it('admin: nulls all profile fields', () => {
    const rq = toCreateUserDtoRq({ ...workerInput, role: 'admin' });
    expect(rq.monthlySalary).toBeNull();
    expect(rq.dailyWage).toBeNull();
  });
});

describe('toUpdateUserDtoRq', () => {
  it('carries role echo + status and applies the same gating', () => {
    const input: UpdateUserInput = { ...workerInput, status: 'disabled' };
    const rq = toUpdateUserDtoRq(input);
    expect(rq.role).toBe('worker');
    expect(rq.status).toBe('disabled');
    expect(rq.monthlySalary).toBeNull();
    expect(rq.dailyWage).toBe(350);
  });
});

describe('GENDER_LABELS', () => {
  it('maps gender codes to Arabic labels', () => {
    expect(GENDER_LABELS.male).toBe('ذكر');
    expect(GENDER_LABELS.female).toBe('أنثى');
  });
});

describe('formatCompensation', () => {
  it('manager → monthly salary with label', () => {
    const u = toUser({ ...dto, role: 'manager', profile: { monthlySalary: 15000, dailyWage: null } });
    expect(formatCompensation(u)).toBe('15000 (راتب شهري)');
  });
  it('worker → daily wage with label', () => {
    expect(formatCompensation(toUser(dto))).toBe('350 (يومية)');
  });
  it('moqawel → daily wage with label', () => {
    expect(formatCompensation(toUser({ ...dto, role: 'moqawel' }))).toBe('350 (يومية)');
  });
  it('admin / null profile → em dash', () => {
    expect(formatCompensation(toUser({ ...dto, role: 'admin', profile: null }))).toBe('—');
  });
  it('manager with a null salary → em dash', () => {
    const u = toUser({ ...dto, role: 'manager', profile: { monthlySalary: null, dailyWage: null } });
    expect(formatCompensation(u)).toBe('—');
  });
});
