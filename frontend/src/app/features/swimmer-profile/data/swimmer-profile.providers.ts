import { Provider } from '@angular/core';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { SwimmerProfileRepositoryImpl } from '@features/swimmer-profile/data/repositories/swimmer-profile.repository.impl';

export const SWIMMER_PROFILE_PROVIDERS: Provider[] = [
  { provide: SWIMMER_PROFILE_REPOSITORY, useClass: SwimmerProfileRepositoryImpl },
];
