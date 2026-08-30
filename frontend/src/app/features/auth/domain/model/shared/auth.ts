// auth.ts — auth domain models (session vocabulary) + DTO→model mappers.
import { UserRole } from '@core/domain/roles';
import { Gender } from '@core/domain/gender';
import { SessionDtoRs } from '@features/auth/data/dto/shared/session.dto';
import { CurrentUserDtoRs } from '@features/auth/data/dto/shared/current-user.dto';

export interface AuthTokens { accessToken: string; refreshToken: string; }
export interface AuthPrincipal { role: UserRole; userId: string; }
// mustChangePassword is a session-lifecycle fact (AD-007 forced first-login change),
// surfaced by login/refresh/change-password; not part of the identity principal.
export interface AuthSession { tokens: AuthTokens; principal: AuthPrincipal; mustChangePassword: boolean; }

export interface CurrentUser { userId: string; email: string; fullName: string; role: UserRole; phone: string | null; gender: Gender | null; age: number | null; }

export function toAuthSession(dto: SessionDtoRs): AuthSession {
  return {
    tokens: { accessToken: dto.accessToken, refreshToken: dto.refreshToken },
    principal: { role: dto.role as UserRole, userId: dto.userId },
    mustChangePassword: dto.mustChangePassword,
  };
}

export function toAuthTokens(dto: SessionDtoRs): AuthTokens {
  return { accessToken: dto.accessToken, refreshToken: dto.refreshToken };
}

export function toAuthPrincipal(dto: CurrentUserDtoRs): AuthPrincipal {
  return { role: dto.role as UserRole, userId: dto.userId };
}

export function toCurrentUser(dto: CurrentUserDtoRs): CurrentUser {
  return { userId: dto.userId, email: dto.email, fullName: dto.fullName, role: dto.role as UserRole, phone: dto.phone, gender: dto.gender as Gender | null, age: dto.age };
}
