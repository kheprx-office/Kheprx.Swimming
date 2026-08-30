import { toAuthSession, toAuthTokens, toAuthPrincipal, toCurrentUser, AuthSession, AuthTokens, AuthPrincipal, CurrentUser } from '@features/auth/domain/model/shared/auth';
import { SessionDtoRs } from '@features/auth/data/dto/shared/session.dto';
import { CurrentUserDtoRs } from '@features/auth/data/dto/shared/current-user.dto';

const sessionDto: SessionDtoRs = { accessToken: 'a', refreshToken: 'r', role: 'manager', userId: 'USR-9', mustChangePassword: true };
const userDto: CurrentUserDtoRs = { userId: 'USR-9', email: 'm@x.y', fullName: 'M', role: 'moqawel', phone: '0100', gender: 'male', age: 30 };

describe('auth mappers', () => {
  it('toAuthSession maps a SessionDtoRs to a nested AuthSession', () => {
    const expected: AuthSession = {
      tokens: { accessToken: 'a', refreshToken: 'r' },
      principal: { role: 'manager', userId: 'USR-9' },
      mustChangePassword: true,
    };
    expect(toAuthSession(sessionDto)).toEqual(expected);
  });
  it('toAuthTokens extracts just the token pair', () => {
    const expected: AuthTokens = { accessToken: 'a', refreshToken: 'r' };
    expect(toAuthTokens(sessionDto)).toEqual(expected);
  });
  it('toAuthPrincipal maps a CurrentUserDtoRs to an AuthPrincipal', () => {
    const expected: AuthPrincipal = { role: 'moqawel', userId: 'USR-9' };
    expect(toAuthPrincipal(userDto)).toEqual(expected);
  });
  it('toCurrentUser maps a CurrentUserDtoRs to a CurrentUser', () => {
    const expected: CurrentUser = { userId: 'USR-9', email: 'm@x.y', fullName: 'M', role: 'moqawel', phone: '0100', gender: 'male', age: 30 };
    expect(toCurrentUser(userDto)).toEqual(expected);
  });
});
