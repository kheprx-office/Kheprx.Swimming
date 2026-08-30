import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { USERS_REPOSITORY } from '@features/user-management/domain/repositories/users.repository';
import { User, UpdateUserInput, toUser, toUpdateUserDtoRq } from '@features/user-management/domain/model/user-management/user';
import { isUserDtoRsValid } from '@features/user-management/data/dto/user-management/user.dto';

export interface UpdateUserArgs { id: string; input: UpdateUserInput; }

@Injectable({ providedIn: 'root' })
export class UpdateUserUseCase extends UseCase<UpdateUserArgs, User> {
  private readonly repo = inject(USERS_REPOSITORY);
  constructor() { super('UpdateUser'); }
  protected async execute({ id, input }: UpdateUserArgs): Promise<User> {
    const res = await this.repo.update(id, toUpdateUserDtoRq(input));
    if (!isUserDtoRsValid(res.data)) throw new AppError('Invalid user data received', 'validation');
    return toUser(res.data);
  }
}
