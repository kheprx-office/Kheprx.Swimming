import { Provider } from '@angular/core';
import { USERS_REPOSITORY } from '@features/user-management/domain/repositories/users.repository';
import { UsersRepositoryImpl } from '@features/user-management/data/repositories/users.repository.impl';

// Live wiring: bind the users repository port to the fetch-only impl
// (Identity backend at /api/users).
export const USERS_PROVIDERS: Provider[] = [
  { provide: USERS_REPOSITORY, useClass: UsersRepositoryImpl },
];
