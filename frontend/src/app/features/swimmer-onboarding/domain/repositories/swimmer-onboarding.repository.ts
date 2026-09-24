import { InjectionToken } from '@angular/core';
import { OnboardingPrefillItemDtoRs } from '@features/swimmer-onboarding/data/dto/onboarding-prefill.dto';
import { CompleteIdentityVitalsDtoRq, CompleteIdentityVitalsItemDtoRs } from '@features/swimmer-onboarding/data/dto/complete-identity-vitals.dto';

export interface ISwimmerOnboardingRepository {
  getPrefill(): Promise<OnboardingPrefillItemDtoRs>;
  completeIdentityVitals(rq: CompleteIdentityVitalsDtoRq): Promise<CompleteIdentityVitalsItemDtoRs>;
}

export const SWIMMER_ONBOARDING_REPOSITORY = new InjectionToken<ISwimmerOnboardingRepository>('SWIMMER_ONBOARDING_REPOSITORY');
