// App: the root shell. Providers live in app.config.ts; screens come from
// the router. The notification host renders app-wide toasts.
import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { NotificationHostComponent } from '@core/ui/components/notification-host.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, NotificationHostComponent],
  template: `
    <router-outlet />
    <app-notification-host />
  `,
})
export class App {}
