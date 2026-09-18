import { Provider } from '@angular/core';
import { SWIMMER_REPOSITORY } from '@features/swimmers/domain/repositories/swimmer.repository';
import { SwimmerRepositoryImpl } from '@features/swimmers/data/repositories/swimmer.repository.impl';

// Live wiring: bind the swimmers repository port to the fetch-only impl (/api/swimmers/*).
export const SWIMMER_PROVIDERS: Provider[] = [
  { provide: SWIMMER_REPOSITORY, useClass: SwimmerRepositoryImpl },
];
