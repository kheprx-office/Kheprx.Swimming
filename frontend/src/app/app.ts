// App: the root shell. Providers live in app.config.ts; screens come from
// the router. The notification host renders app-wide toasts.
import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { NotificationHostComponent } from '@core/ui/components/notification-host.component';
import { LoadingBarComponent } from '@core/ui/components/loading-bar.component';
import { LanguageStore } from '@core/i18n';
import { ThemeStore } from '@core/ui/theme/theme.store';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, NotificationHostComponent, LoadingBarComponent],
  template: `
    <app-loading-bar />
    <router-outlet />
    <app-notification-host />
  `,
})
export class App {
  constructor() {
    inject(LanguageStore);
    inject(ThemeStore);
  }
}
