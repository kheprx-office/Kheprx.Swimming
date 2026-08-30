import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AUTH_REPOSITORY } from '@features/auth/domain/repositories/auth.repository';
import { TokenStore } from '@features/auth/data/token-store';

@Injectable({ providedIn: 'root' })
export class LogoutUseCase extends UseCase<void, void> {
  private readonly repo = inject(AUTH_REPOSITORY);
  private readonly store = inject(TokenStore);
  constructor() { super('Logout'); }
  protected async execute(): Promise<void> {
    // AD-009: revoke the server session (best-effort). The local clear must always
    // succeed, so a failed revoke is swallowed.
    try { await this.repo.logout(); } catch { /* best-effort */ }
    await this.store.clear();
  }
}
