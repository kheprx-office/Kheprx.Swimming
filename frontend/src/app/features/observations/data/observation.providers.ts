import { Provider } from '@angular/core';
import { OBSERVATION_REPOSITORY } from '@features/observations/domain/repositories/observation.repository';
import { ObservationRepositoryImpl } from '@features/observations/data/repositories/observation.repository.impl';

// Live wiring: bind the observations repository port to the HTTP impl (/api/observations).
export const OBSERVATION_PROVIDERS: Provider[] = [
  { provide: OBSERVATION_REPOSITORY, useClass: ObservationRepositoryImpl },
];
