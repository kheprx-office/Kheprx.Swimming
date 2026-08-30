import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { AUTH_REPOSITORY } from '@features/auth/domain/repositories/auth.repository';
import { TokenStore } from '@features/auth/data/token-store';
import { AuthTokens, toAuthTokens } from '@features/auth/domain/model/shared/auth';
import { isSessionDtoRsValid } from '@features/auth/data/dto/shared/session.dto';

@Injectable({ providedIn: 'root' })
export class RefreshTokenUseCase extends UseCase<void, AuthTokens> {
  private readonly repo = inject(AUTH_REPOSITORY);
  private readonly store = inject(TokenStore);
  constructor() { super('RefreshToken'); }
  protected async execute(): Promise<AuthTokens> {
    const refresh = await this.store.getRefresh();
    if (!refresh) throw new AppError('No refresh token', 'auth', 401);
    const res = await this.repo.refresh({ refreshToken: refresh });
    // Validates the FULL session DTO (role + mustChangePassword), not just the tokens —
    // safe per the backend SessionDto contract on /api/auth/refresh; a contract drift here
    // surfaces as a forced re-login via the interceptor's refresh-failure path.
    if (!isSessionDtoRsValid(res.data)) throw new AppError('Invalid session data received', 'validation');
    const tokens = toAuthTokens(res.data);
    await this.store.save(tokens);
    return tokens;
  }
}
