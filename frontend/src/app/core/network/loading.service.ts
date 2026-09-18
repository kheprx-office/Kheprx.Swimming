// LoadingService: app-wide in-flight request counter, exposed as signals. Driven by
// loadingInterceptor; read by the global LoadingBar and any busy-aware UI. A counter
// (not a boolean) so concurrent requests compose — the bar hides only when the last settles.
import { Injectable, computed, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly _count = signal(0);
  readonly activeCount = this._count.asReadonly();
  readonly isLoading = computed(() => this._count() > 0);

  begin(): void {
    this._count.update((n) => n + 1);
  }

  end(): void {
    this._count.update((n) => Math.max(0, n - 1));
  }
}
