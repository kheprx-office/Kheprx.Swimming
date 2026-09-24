import { Injectable, inject, signal } from '@angular/core';
import { LoadDashboardSummaryUseCase } from '@features/dashboard/domain/usecases/load-dashboard-summary.use-case';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { DashboardSummary } from '@features/dashboard/domain/model/dashboard-summary';

/**
 * Dashboard state: loads the summary once and exposes it as signals. Region-specific
 * presentation math lives in the child components (hero, weekly chart, stroke split).
 */
@Injectable()
export class DashboardViewModel {
  private readonly loadSummary = inject(LoadDashboardSummaryUseCase);
  private readonly auth = inject(AuthSessionStore);

  readonly loading = signal(false);
  readonly error = signal(false);
  readonly summary = signal<DashboardSummary | null>(null);

  readonly currentUserName = this.auth.currentUserName;

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(false);
    const res = await this.loadSummary.run();
    if (res.ok) {
      this.summary.set(res.data);
    } else {
      this.error.set(true);
      this.summary.set(null);
    }
    this.loading.set(false);
  }
}
