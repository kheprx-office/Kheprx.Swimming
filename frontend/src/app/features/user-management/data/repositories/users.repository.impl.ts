// users.repository.impl.ts — fetch-only users repository. Calls HttpClientService
// directly and returns the API DTO envelope (no unwrap, no map).
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { IUsersRepository } from '@features/user-management/domain/repositories/users.repository';
import { UsersDtoRs, UserItemDtoRs } from '@features/user-management/data/dto/user-management/user.dto';
import { CreateUserDtoRq } from '@features/user-management/data/dto/user-management/create-user.dto';
import { UpdateUserDtoRq } from '@features/user-management/data/dto/user-management/update-user.dto';
import { SetUserStatusDtoRq } from '@features/user-management/data/dto/user-management/set-user-status.dto';

@Injectable({ providedIn: 'root' })
export class UsersRepositoryImpl implements IUsersRepository {
  private readonly http = inject(HttpClientService);

  list(search?: string): Promise<UsersDtoRs> {
    const s = search?.trim();
    return this.http.get<UsersDtoRs>('/api/users', s ? { params: { search: s } } : undefined);
  }

  create(rq: CreateUserDtoRq): Promise<UserItemDtoRs> {
    return this.http.post<UserItemDtoRs>('/api/users', { body: rq });
  }

  update(id: string, rq: UpdateUserDtoRq): Promise<UserItemDtoRs> {
    return this.http.patch<UserItemDtoRs>(`/api/users/${id}`, { body: rq });
  }

  setStatus(id: string, rq: SetUserStatusDtoRq): Promise<UserItemDtoRs> {
    return this.http.patch<UserItemDtoRs>(`/api/users/${id}/status`, { body: rq });
  }
}
