// env: the single source of environment + tuning config. A plain typed object.
// isDevMode() is false in optimized production builds, true otherwise (incl. Jest).
import { isDevMode } from '@angular/core';

export type AppEnv = 'development' | 'production';

const isDev = isDevMode();

export const env = {
  name: (isDev ? 'development' : 'production') as AppEnv,
  isDev,
  api: {
    // Per-environment base URL. Replace with real URLs when available.
    baseUrl: isDev
      ? 'http://localhost:5080' // dev: local backend. https://localhost:7080 also serves it (needs a trusted dev cert).
      : '/api', // prod: same-origin sub-path — update to match your deployment
    timeoutMs: 10_000,
    maxRetries: 2, // total attempts = 1 + maxRetries
    backoffMs: [500, 1500], // delay before retry 1, retry 2
  },
} as const;
