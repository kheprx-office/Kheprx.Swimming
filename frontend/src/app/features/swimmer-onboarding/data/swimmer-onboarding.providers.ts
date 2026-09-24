import { Provider } from '@angular/core';
import { SWIMMER_ONBOARDING_REPOSITORY } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { SwimmerOnboardingRepositoryImpl } from '@features/swimmer-onboarding/data/repositories/swimmer-onboarding.repository.impl';

export const SWIMMER_ONBOARDING_PROVIDERS: Provider[] = [
  { provide: SWIMMER_ONBOARDING_REPOSITORY, useClass: SwimmerOnboardingRepositoryImpl },
];
