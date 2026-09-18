// AccountViewModel: route-scoped loader for the /account settings page. Fetches the
// signed-in user's profile via LoadCurrentUserUseCase (GET /api/auth/me) and exposes it
// read-only. On failure it surfaces a toast.
import { Injectable, inject, signal } from '@angular/core';
import { LoadCurrentUserUseCase } from '@features/auth/domain/usecases/shared/load-current-user.use-case';
import { NotificationService } from '@core/ui/notification.service';
import { toUserMessage } from '@core/domain/errors/user-message';
import { CurrentUser } from '@features/auth/domain/model/shared/auth';

@Injectable()
export class AccountViewModel {
  private readonly loadCurrentUser = inject(LoadCurrentUserUseCase);
  private readonly notify = inject(NotificationService);

  readonly loading = signal(false);
  readonly user = signal<CurrentUser | null>(null);

  async init(): Promise<void> {
    this.loading.set(true);
    const r = await this.loadCurrentUser.run();
    this.loading.set(false);
    if (r.ok) this.user.set(r.data);
    else this.notify.error(toUserMessage(r.error));
  }
}
