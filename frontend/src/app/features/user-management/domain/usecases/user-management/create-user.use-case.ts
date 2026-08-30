import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { USERS_REPOSITORY } from '@features/user-management/domain/repositories/users.repository';
import { User, CreateUserInput, toUser, toCreateUserDtoRq } from '@features/user-management/domain/model/user-management/user';
import { isUserDtoRsValid } from '@features/user-management/data/dto/user-management/user.dto';

@Injectable({ providedIn: 'root' })
export class CreateUserUseCase extends UseCase<CreateUserInput, User> {
  private readonly repo = inject(USERS_REPOSITORY);
  constructor() { super('CreateUser'); }
  protected async execute(input: CreateUserInput): Promise<User> {
    const res = await this.repo.create(toCreateUserDtoRq(input));
    if (!isUserDtoRsValid(res.data)) throw new AppError('Invalid user data received', 'validation');
    return toUser(res.data);
  }
}
