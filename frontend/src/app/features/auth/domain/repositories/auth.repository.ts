// auth.repository.ts — the auth repository port (domain contract).
// Returns the raw DTO envelopes; mapping to domain models happens in the use cases.
import { InjectionToken } from '@angular/core';
import { SessionItemDtoRs } from '@features/auth/data/dto/shared/session.dto';
import { CurrentUserItemDtoRs } from '@features/auth/data/dto/shared/current-user.dto';
import { LoginDtoRq } from '@features/auth/data/dto/login/login.dto';
import { RefreshDtoRq } from '@features/auth/data/dto/shared/refresh.dto';
import { ChangePasswordDtoRq } from '@features/auth/data/dto/change-password/change-password.dto';

export interface IAuthRepository {
  login(rq: LoginDtoRq): Promise<SessionItemDtoRs>;
  refresh(rq: RefreshDtoRq): Promise<SessionItemDtoRs>;
  logout(): Promise<void>;
  me(): Promise<CurrentUserItemDtoRs>;
  changePassword(rq: ChangePasswordDtoRq): Promise<SessionItemDtoRs>;
}

export const AUTH_REPOSITORY = new InjectionToken<IAuthRepository>('AUTH_REPOSITORY');
