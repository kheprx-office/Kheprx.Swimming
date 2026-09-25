import { InjectionToken } from '@angular/core';
import { OnboardingPrefillItemDtoRs } from '@features/swimmer-onboarding/data/dto/onboarding-prefill.dto';
import { CompleteIdentityVitalsDtoRq, CompleteIdentityVitalsItemDtoRs } from '@features/swimmer-onboarding/data/dto/complete-identity-vitals.dto';
import { CompleteGuardianMedicalDtoRq, CompleteGuardianMedicalItemDtoRs } from '@features/swimmer-onboarding/data/dto/complete-guardian-medical.dto';
import { CompletePhysiologicalDtoRq, CompletePhysiologicalItemDtoRs } from '@features/swimmer-onboarding/data/dto/complete-physiological.dto';
import { CompleteInBodyDtoRq, CompleteInBodyItemDtoRs } from '@features/swimmer-onboarding/data/dto/complete-inbody.dto';

export interface ISwimmerOnboardingRepository {
  getPrefill(): Promise<OnboardingPrefillItemDtoRs>;
  completeIdentityVitals(rq: CompleteIdentityVitalsDtoRq): Promise<CompleteIdentityVitalsItemDtoRs>;
  completeGuardianMedical(rq: CompleteGuardianMedicalDtoRq): Promise<CompleteGuardianMedicalItemDtoRs>;
  completePhysiological(rq: CompletePhysiologicalDtoRq): Promise<CompletePhysiologicalItemDtoRs>;
  completeInBody(rq: CompleteInBodyDtoRq): Promise<CompleteInBodyItemDtoRs>;
}

export const SWIMMER_ONBOARDING_REPOSITORY = new InjectionToken<ISwimmerOnboardingRepository>('SWIMMER_ONBOARDING_REPOSITORY');
