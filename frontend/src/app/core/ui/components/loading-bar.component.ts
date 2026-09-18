// LoadingBarComponent: a thin indeterminate top bar reflecting LoadingService.isLoading().
// Mounted once at the app root (beside the notification host). A ~120ms show-delay prevents
// fast requests from flashing the bar; the pending timer is cleared the moment loading ends.
import { Component, effect, inject, signal } from '@angular/core';
import { LoadingService } from '@core/network/loading.service';

const SHOW_DELAY_MS = 120;

@Component({
  selector: 'app-loading-bar',
  standalone: true,
  template: `
    @if (show()) {
      <div class="fixed inset-x-0 top-0 z-[70] h-0.5 overflow-hidden bg-primary/20"
           role="progressbar" aria-label="Loading">
        <div class="h-full w-1/3 bg-primary animate-loading-bar"></div>
      </div>
    }
  `,
})
export class LoadingBarComponent {
  private readonly loading = inject(LoadingService);
  private readonly _show = signal(false);
  readonly show = this._show.asReadonly();
  private timer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    effect(() => {
      const active = this.loading.isLoading();
      if (active) {
        if (this.timer === null) {
          this.timer = setTimeout(() => {
            this._show.set(true);
            this.timer = null;
          }, SHOW_DELAY_MS);
        }
      } else {
        if (this.timer !== null) {
          clearTimeout(this.timer);
          this.timer = null;
        }
        this._show.set(false);
      }
    });
  }
}
