import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { AUTH_REPOSITORY } from '@features/auth/domain/repositories/auth.repository';
import { CurrentUser, toCurrentUser } from '@features/auth/domain/model/shared/auth';
import { isCurrentUserDtoRsValid } from '@features/auth/data/dto/shared/current-user.dto';

@Injectable({ providedIn: 'root' })
export class LoadCurrentUserUseCase extends UseCase<void, CurrentUser> {
  private readonly repo = inject(AUTH_REPOSITORY);
  constructor() { super('LoadCurrentUser'); }
  protected async execute(): Promise<CurrentUser> {
    const res = await this.repo.me();
    if (!isCurrentUserDtoRsValid(res.data)) throw new AppError('Invalid user data received', 'validation');
    return toCurrentUser(res.data);
  }
}
