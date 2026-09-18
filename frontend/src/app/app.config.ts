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
import { languageInterceptor } from '@core/i18n/language.interceptor';
import { loadingInterceptor } from '@core/network/loading.interceptor';
import { AUTH_PROVIDERS } from '@features/auth/data/auth.providers';
import { SWIMMER_PROVIDERS } from '@features/swimmers/data/swimmer.providers';
import { REFERENCE_PROVIDERS } from '@features/reference/data/reference.providers';
import { COACH_PROVIDERS } from '@features/coaches/data/coach.providers';
import { MEDICAL_TEST_PROVIDERS } from '@features/medical-tests/data/medical-test.providers';
import { HEALTH_READING_PROVIDERS } from '@features/health-readings/data/health-reading.providers';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor, languageInterceptor, loadingInterceptor])),
    provideAnimations(),
    ...AUTH_PROVIDERS,
    ...SWIMMER_PROVIDERS,
    ...REFERENCE_PROVIDERS,
    ...COACH_PROVIDERS,
    ...MEDICAL_TEST_PROVIDERS,
    ...HEALTH_READING_PROVIDERS,
    // Restore the persisted session (GET /api/auth/me) BEFORE the router activates, so
    // guards don't see a not-yet-rehydrated store and bounce a logged-in user to /login
    // on page reload.
    provideAppInitializer(() => inject(AuthSessionStore).whenReady()),
  ],
};
