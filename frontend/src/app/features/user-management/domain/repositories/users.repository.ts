// users.repository.ts — the user-management repository port (domain contract).
// Returns the raw DTO envelopes; mapping to the domain model happens in the use cases.
import { InjectionToken } from '@angular/core';
import { UsersDtoRs, UserItemDtoRs } from '@features/user-management/data/dto/user-management/user.dto';
import { CreateUserDtoRq } from '@features/user-management/data/dto/user-management/create-user.dto';
import { UpdateUserDtoRq } from '@features/user-management/data/dto/user-management/update-user.dto';
import { SetUserStatusDtoRq } from '@features/user-management/data/dto/user-management/set-user-status.dto';

export interface IUsersRepository {
  list(search?: string): Promise<UsersDtoRs>;
  create(rq: CreateUserDtoRq): Promise<UserItemDtoRs>;
  update(id: string, rq: UpdateUserDtoRq): Promise<UserItemDtoRs>;
  setStatus(id: string, rq: SetUserStatusDtoRq): Promise<UserItemDtoRs>;
}

export const USERS_REPOSITORY = new InjectionToken<IUsersRepository>('USERS_REPOSITORY');
