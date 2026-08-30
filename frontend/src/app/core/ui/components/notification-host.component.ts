// NotificationHost: renders the current NotificationService message as a dismissible
// banner. Placed once in the app shell.
import { Component, inject } from '@angular/core';
import { NotificationService } from '@core/ui/notification.service';

@Component({
  selector: 'app-notification-host',
  standalone: true,
  template: `
    @if (notifications.current(); as n) {
      <div
        class="modal-panel fixed bottom-6 left-6 z-[60] rounded-xl px-5 py-3 text-sm font-semibold shadow-xl cursor-pointer"
        [class]="n.kind === 'error' ? 'bg-danger-bg text-danger border border-danger/30' : 'bg-success-bg text-success border border-success/30'"
        (click)="notifications.clear()">
        {{ n.message }}
      </div>
    }
  `,
  styles: [],
})
export class NotificationHostComponent {
  readonly notifications = inject(NotificationService);
}
