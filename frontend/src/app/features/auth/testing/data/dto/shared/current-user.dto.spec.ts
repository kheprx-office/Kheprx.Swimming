import { CurrentUserDtoRs, isCurrentUserDtoRsValid } from '@features/auth/data/dto/shared/current-user.dto';

const validUser: CurrentUserDtoRs = {
  userId: 'USR-1', email: 'a@b.c', fullName: 'A', role: 'worker',
  phone: '01000000000', gender: 'male', age: 30,
};

describe('isCurrentUserDtoRsValid', () => {
  it('accepts a well-formed current-user DTO', () => {
    expect(isCurrentUserDtoRsValid(validUser)).toBe(true);
  });
  it('rejects an empty userId', () => {
    expect(isCurrentUserDtoRsValid({ ...validUser, userId: '' })).toBe(false);
  });
  it('rejects an unknown role', () => {
    expect(isCurrentUserDtoRsValid({ ...validUser, role: 'superadmin' })).toBe(false);
  });
  it('rejects a role that collides with an inherited object property', () => {
    expect(isCurrentUserDtoRsValid({ ...validUser, role: 'toString' })).toBe(false);
  });
  it('accepts null phone/gender/age', () => {
    expect(isCurrentUserDtoRsValid({ ...validUser, phone: null, gender: null, age: null })).toBe(true);
  });
  it('rejects an invalid gender', () => {
    expect(isCurrentUserDtoRsValid({ ...validUser, gender: 'x' as unknown as string })).toBe(false);
  });
  it('rejects a non-number age', () => {
    expect(isCurrentUserDtoRsValid({ ...validUser, age: '30' as unknown as number })).toBe(false);
  });
});
