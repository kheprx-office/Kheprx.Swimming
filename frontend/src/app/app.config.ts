// appConfig: the composition root.
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { routes } from './app.routes';
import { authInterceptor } from '@features/auth/data/auth.interceptor';
import { AUTH_PROVIDERS } from '@features/auth/data/auth.providers';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { USERS_PROVIDERS } from '@features/user-management/data/users.providers';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAnimations(),
    ...AUTH_PROVIDERS,
    ...USERS_PROVIDERS,
    // Restore the persisted session (GET /api/auth/me) BEFORE the router activates, so
    // guards don't see a not-yet-rehydrated store and bounce a logged-in user to /login
    // on page reload.
    provideAppInitializer(() => inject(AuthSessionStore).whenReady()),
  ],
};
