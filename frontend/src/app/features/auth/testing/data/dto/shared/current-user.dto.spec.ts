import { CurrentUserDtoRs, isCurrentUserDtoRsValid } from '@features/auth/data/dto/shared/current-user.dto';

const validUser: CurrentUserDtoRs = {
  userId: 'USR-1', email: 'a@b.c', nameEn: 'Ahmed', nameAr: 'أحمد', role: 'captain',
  phone: '01000000000', gender: 'male', age: 30, nationalId: '12345678901234',
};

describe('isCurrentUserDtoRsValid', () => {
  it('accepts a well-formed current-user DTO', () => {
    expect(isCurrentUserDtoRsValid(validUser)).toBe(true);
  });
  it('accepts a DTO with null optional fields', () => {
    expect(isCurrentUserDtoRsValid({ ...validUser, nameAr: null, nationalId: null, phone: null, gender: null, age: null })).toBe(true);
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
  it('rejects a missing nameEn', () => {
    const dto = { ...validUser } as unknown as Record<string, unknown>;
    delete dto['nameEn'];
    expect(isCurrentUserDtoRsValid(dto as unknown as CurrentUserDtoRs)).toBe(false);
  });
  it('rejects a non-string nameEn', () => {
    expect(isCurrentUserDtoRsValid({ ...validUser, nameEn: 42 as unknown as string })).toBe(false);
  });
  it('rejects null phone/gender/age', () => {
    expect(isCurrentUserDtoRsValid({ ...validUser, phone: null, gender: null, age: null })).toBe(true);
  });
  it('rejects an invalid gender', () => {
    expect(isCurrentUserDtoRsValid({ ...validUser, gender: 'x' as unknown as string })).toBe(false);
  });
  it('rejects a non-number age', () => {
    expect(isCurrentUserDtoRsValid({ ...validUser, age: '30' as unknown as number })).toBe(false);
  });
});
