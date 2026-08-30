import { Provider } from '@angular/core';
import { AUTH_REPOSITORY } from '@features/auth/domain/repositories/auth.repository';
import { AuthRepositoryImpl } from '@features/auth/data/repositories/auth.repository.impl';

// Live wiring: bind the auth repository port to the fetch-only impl
// (targets the Identity backend at /api/auth/*).
export const AUTH_PROVIDERS: Provider[] = [
  { provide: AUTH_REPOSITORY, useClass: AuthRepositoryImpl },
];
