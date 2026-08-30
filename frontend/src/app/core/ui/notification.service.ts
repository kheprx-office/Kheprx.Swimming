// NotificationService: app-wide toast/snackbar state (the web equivalent of the
// mobile Alert.alert). Pages call success()/error(); the host renders it. Each
// notification auto-dismisses after AUTO_DISMISS_MS; a new one resets the timer.
import { Injectable, signal } from '@angular/core';

export type Notification = { kind: 'success' | 'error'; message: string };

const AUTO_DISMISS_MS = 10_000;

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly _current = signal<Notification | null>(null);
  readonly current = this._current.asReadonly();

  private timer: ReturnType<typeof setTimeout> | null = null;

  success(message: string): void {
    this.show({ kind: 'success', message });
  }

  error(message: string): void {
    this.show({ kind: 'error', message });
  }

  clear(): void {
    if (this.timer) {
      clearTimeout(this.timer);
      this.timer = null;
    }
    this._current.set(null);
  }

  private show(n: Notification): void {
    if (this.timer) clearTimeout(this.timer);
    this._current.set(n);
    this.timer = setTimeout(() => this.clear(), AUTO_DISMISS_MS);
  }
}
