import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { USERS_REPOSITORY } from '@features/user-management/domain/repositories/users.repository';
import { User, UserStatus, toUser } from '@features/user-management/domain/model/user-management/user';
import { SetUserStatusDtoRq } from '@features/user-management/data/dto/user-management/set-user-status.dto';
import { isUserDtoRsValid } from '@features/user-management/data/dto/user-management/user.dto';

export interface SetUserStatusArgs { id: string; status: UserStatus; }

@Injectable({ providedIn: 'root' })
export class SetUserStatusUseCase extends UseCase<SetUserStatusArgs, User> {
  private readonly repo = inject(USERS_REPOSITORY);
  constructor() { super('SetUserStatus'); }
  protected async execute({ id, status }: SetUserStatusArgs): Promise<User> {
    const rq: SetUserStatusDtoRq = { status };
    const res = await this.repo.setStatus(id, rq);
    if (!isUserDtoRsValid(res.data)) throw new AppError('Invalid user data received', 'validation');
    return toUser(res.data);
  }
}
