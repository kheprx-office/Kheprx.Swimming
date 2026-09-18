// reference.providers.ts — bind the reference repository port to the fetch-only impl.
import { Provider } from '@angular/core';
import { REFERENCE_REPOSITORY } from '@features/reference/domain/repositories/reference.repository';
import { ReferenceRepositoryImpl } from '@features/reference/data/repositories/reference.repository.impl';

export const REFERENCE_PROVIDERS: Provider[] = [
  { provide: REFERENCE_REPOSITORY, useClass: ReferenceRepositoryImpl },
];
