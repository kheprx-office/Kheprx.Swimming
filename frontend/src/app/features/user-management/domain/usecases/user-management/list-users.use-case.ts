import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { USERS_REPOSITORY } from '@features/user-management/domain/repositories/users.repository';
import { User, toUser } from '@features/user-management/domain/model/user-management/user';
import { isUserDtoRsValid } from '@features/user-management/data/dto/user-management/user.dto';

@Injectable({ providedIn: 'root' })
export class ListUsersUseCase extends UseCase<string | undefined, User[]> {
  private readonly repo = inject(USERS_REPOSITORY);
  constructor() { super('ListUsers'); }
  protected async execute(search: string | undefined): Promise<User[]> {
    const res = await this.repo.list(search);
    if (!res.data.every(isUserDtoRsValid)) throw new AppError('Invalid user data received', 'validation');
    return res.data.map(toUser);
  }
}
