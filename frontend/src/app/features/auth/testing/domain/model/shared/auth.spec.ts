import { toAuthSession, toAuthTokens, toAuthPrincipal, toCurrentUser, AuthSession, AuthTokens, AuthPrincipal, CurrentUser } from '@features/auth/domain/model/shared/auth';
import { SessionDtoRs } from '@features/auth/data/dto/shared/session.dto';
import { CurrentUserDtoRs } from '@features/auth/data/dto/shared/current-user.dto';

const sessionDto: SessionDtoRs = { accessToken: 'a', refreshToken: 'r', role: 'head_coach', userId: 'USR-9', mustChangePassword: true };
const userDto: CurrentUserDtoRs = { userId: 'USR-9', email: 'm@x.y', nameEn: 'M', nameAr: 'م', role: 'captain', phone: '0100', gender: 'male', age: 30, nationalId: '99999999901234' };

describe('auth mappers', () => {
  it('toAuthSession maps a SessionDtoRs to a nested AuthSession', () => {
    const expected: AuthSession = {
      tokens: { accessToken: 'a', refreshToken: 'r' },
      principal: { role: 'head_coach', userId: 'USR-9' },
      mustChangePassword: true,
    };
    expect(toAuthSession(sessionDto)).toEqual(expected);
  });
  it('toAuthTokens extracts just the token pair', () => {
    const expected: AuthTokens = { accessToken: 'a', refreshToken: 'r' };
    expect(toAuthTokens(sessionDto)).toEqual(expected);
  });
  it('toAuthPrincipal maps a CurrentUserDtoRs to an AuthPrincipal', () => {
    const expected: AuthPrincipal = { role: 'captain', userId: 'USR-9' };
    expect(toAuthPrincipal(userDto)).toEqual(expected);
  });
  it('toCurrentUser maps nameEn, nameAr, and nationalId from the DTO', () => {
    const expected: CurrentUser = { userId: 'USR-9', email: 'm@x.y', nameEn: 'M', nameAr: 'م', role: 'captain', phone: '0100', gender: 'male', age: 30, nationalId: '99999999901234' };
    expect(toCurrentUser(userDto)).toEqual(expected);
  });
  it('toCurrentUser handles null nameAr and nationalId', () => {
    const dto: CurrentUserDtoRs = { ...userDto, nameAr: null, nationalId: null };
    const result = toCurrentUser(dto);
    expect(result.nameAr).toBeNull();
    expect(result.nationalId).toBeNull();
  });
});
