import { Provider } from '@angular/core';
import { HEALTH_READING_REPOSITORY } from '@features/health-readings/domain/repositories/health-reading.repository';
import { HealthReadingRepositoryImpl } from '@features/health-readings/data/repositories/health-reading.repository.impl';

// Live wiring: bind the health-readings repository port to the HTTP impl (/api/health-readings).
export const HEALTH_READING_PROVIDERS: Provider[] = [
  { provide: HEALTH_READING_REPOSITORY, useClass: HealthReadingRepositoryImpl },
];
