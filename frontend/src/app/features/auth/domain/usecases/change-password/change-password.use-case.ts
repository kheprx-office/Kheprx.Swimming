import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { AUTH_REPOSITORY } from '@features/auth/domain/repositories/auth.repository';
import { TokenStore } from '@features/auth/data/token-store';
import { AuthSession, toAuthSession } from '@features/auth/domain/model/shared/auth';
import { ChangePasswordInput } from '@features/auth/domain/model/change-password/change-password';
import { isSessionDtoRsValid } from '@features/auth/data/dto/shared/session.dto';

@Injectable({ providedIn: 'root' })
export class ChangePasswordUseCase extends UseCase<ChangePasswordInput, AuthSession> {
  private readonly repo = inject(AUTH_REPOSITORY);
  private readonly tokens = inject(TokenStore);
  constructor() { super('ChangePassword'); }
  protected async execute({ currentPassword, newPassword }: ChangePasswordInput): Promise<AuthSession> {
    const res = await this.repo.changePassword({ currentPassword, newPassword });
    if (!isSessionDtoRsValid(res.data)) throw new AppError('Invalid session data received', 'validation');
    const session = toAuthSession(res.data);
    await this.tokens.save(session.tokens); // backend rotates tokens on change (AD-007)
    return session;
  }
}
