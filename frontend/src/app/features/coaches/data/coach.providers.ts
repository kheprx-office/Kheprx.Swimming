import { Provider } from '@angular/core';
import { COACH_REPOSITORY } from '@features/coaches/domain/repositories/coach.repository';
import { CoachRepositoryImpl } from '@features/coaches/data/repositories/coach.repository.impl';

export const COACH_PROVIDERS: Provider[] = [
  { provide: COACH_REPOSITORY, useClass: CoachRepositoryImpl },
];
