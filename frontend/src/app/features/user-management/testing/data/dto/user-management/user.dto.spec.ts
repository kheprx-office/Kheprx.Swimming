import { UserDtoRs, isUserDtoRsValid } from '@features/user-management/data/dto/user-management/user.dto';

const valid: UserDtoRs = {
  id: 'USR-1', code: 'W-1', fullName: 'A', email: 'a@b.c', phone: '0100000001',
  gender: 'male', age: 24, nid: '29801014501234', role: 'worker', status: 'active',
  mustChangePassword: false,
  profile: { monthlySalary: null, dailyWage: 350 },
};

describe('isUserDtoRsValid', () => {
  it('accepts a well-formed DTO', () => {
    expect(isUserDtoRsValid(valid)).toBe(true);
  });

  it('accepts nullable fields as null (login-less admin shape)', () => {
    expect(isUserDtoRsValid({
      ...valid, code: null, email: null, phone: null, gender: null, age: null, role: 'admin', profile: null,
    })).toBe(true);
  });

  it('rejects a status outside {active,disabled}', () => {
    expect(isUserDtoRsValid({ ...valid, status: 'bogus' })).toBe(false);
  });

  it('rejects a role outside the known role set', () => {
    expect(isUserDtoRsValid({ ...valid, role: 'superadmin' })).toBe(false);
  });

  it('rejects a role that collides with an inherited object property', () => {
    expect(isUserDtoRsValid({ ...valid, role: 'toString' })).toBe(false);
  });

  it('rejects an empty id or empty nid', () => {
    expect(isUserDtoRsValid({ ...valid, id: '' })).toBe(false);
    expect(isUserDtoRsValid({ ...valid, nid: '' })).toBe(false);
  });

  it('rejects an unknown gender', () => {
    expect(isUserDtoRsValid({ ...valid, gender: 'other' })).toBe(false);
  });

  it('rejects a malformed profile', () => {
    expect(isUserDtoRsValid({
      ...valid,
      profile: { ...valid.profile!, dailyWage: '350' as unknown as number },
    })).toBe(false);
  });

  it('rejects a non-boolean mustChangePassword', () => {
    expect(isUserDtoRsValid({ ...valid, mustChangePassword: 'yes' as unknown as boolean })).toBe(false);
  });
});
