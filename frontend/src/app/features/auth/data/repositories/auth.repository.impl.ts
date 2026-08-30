// auth.repository.impl.ts — fetch-only auth repository. Calls HttpClientService
// directly and returns the API DTO envelope (no unwrap, no map). The Bearer token is
// attached by authInterceptor; validation + DTO→model mapping happen in the use cases.
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { BaseResponseRs } from '@core/network/api/base-response-rs';
import { IAuthRepository } from '@features/auth/domain/repositories/auth.repository';
import { SessionItemDtoRs } from '@features/auth/data/dto/shared/session.dto';
import { CurrentUserItemDtoRs } from '@features/auth/data/dto/shared/current-user.dto';
import { LoginDtoRq } from '@features/auth/data/dto/login/login.dto';
import { RefreshDtoRq } from '@features/auth/data/dto/shared/refresh.dto';
import { ChangePasswordDtoRq } from '@features/auth/data/dto/change-password/change-password.dto';

@Injectable({ providedIn: 'root' })
export class AuthRepositoryImpl implements IAuthRepository {
  private readonly http = inject(HttpClientService);

  login(rq: LoginDtoRq): Promise<SessionItemDtoRs> {
    return this.http.post<SessionItemDtoRs>('/api/auth/login', { body: rq });
  }

  refresh(rq: RefreshDtoRq): Promise<SessionItemDtoRs> {
    return this.http.post<SessionItemDtoRs>('/api/auth/refresh', { body: rq });
  }

  async logout(): Promise<void> {
    await this.http.post<BaseResponseRs<unknown>>('/api/auth/logout', {}); // no body (AD-009)
  }

  me(): Promise<CurrentUserItemDtoRs> {
    return this.http.get<CurrentUserItemDtoRs>('/api/auth/me');
  }

  changePassword(rq: ChangePasswordDtoRq): Promise<SessionItemDtoRs> {
    return this.http.post<SessionItemDtoRs>('/api/auth/change-password', { body: rq });
  }
}
