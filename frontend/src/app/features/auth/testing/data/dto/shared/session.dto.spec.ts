import { SessionDtoRs, isSessionDtoRsValid } from '@features/auth/data/dto/shared/session.dto';

const validSession: SessionDtoRs = {
  accessToken: 'a', refreshToken: 'r', role: 'head_coach', userId: 'USR-1', mustChangePassword: false,
};

describe('isSessionDtoRsValid', () => {
  it('accepts a well-formed session DTO', () => {
    expect(isSessionDtoRsValid(validSession)).toBe(true);
  });
  it('rejects an empty accessToken', () => {
    expect(isSessionDtoRsValid({ ...validSession, accessToken: '' })).toBe(false);
  });
  it('rejects an empty refreshToken', () => {
    expect(isSessionDtoRsValid({ ...validSession, refreshToken: '' })).toBe(false);
  });
  it('rejects an unknown role', () => {
    expect(isSessionDtoRsValid({ ...validSession, role: 'superadmin' })).toBe(false);
  });
  it('rejects a role that collides with an inherited object property', () => {
    expect(isSessionDtoRsValid({ ...validSession, role: 'toString' })).toBe(false);
  });
  it('rejects a non-boolean mustChangePassword', () => {
    expect(isSessionDtoRsValid({ ...validSession, mustChangePassword: 'no' as unknown as boolean })).toBe(false);
  });
});
