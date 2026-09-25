// swimmer-onboarding.repository.impl.ts — self-service onboarding read + write (JWT-scoped `me` endpoints).
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { ISwimmerOnboardingRepository } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { OnboardingPrefillItemDtoRs } from '@features/swimmer-onboarding/data/dto/onboarding-prefill.dto';
import { CompleteIdentityVitalsDtoRq, CompleteIdentityVitalsItemDtoRs } from '@features/swimmer-onboarding/data/dto/complete-identity-vitals.dto';
import { CompleteGuardianMedicalDtoRq, CompleteGuardianMedicalItemDtoRs } from '@features/swimmer-onboarding/data/dto/complete-guardian-medical.dto';
import { CompletePhysiologicalDtoRq, CompletePhysiologicalItemDtoRs } from '@features/swimmer-onboarding/data/dto/complete-physiological.dto';
import { CompleteInBodyDtoRq, CompleteInBodyItemDtoRs } from '@features/swimmer-onboarding/data/dto/complete-inbody.dto';

@Injectable({ providedIn: 'root' })
export class SwimmerOnboardingRepositoryImpl implements ISwimmerOnboardingRepository {
  private readonly http = inject(HttpClientService);

  getPrefill(): Promise<OnboardingPrefillItemDtoRs> {
    return this.http.get<OnboardingPrefillItemDtoRs>('/api/swimmers/me/onboarding/identity-vitals');
  }
  completeIdentityVitals(rq: CompleteIdentityVitalsDtoRq): Promise<CompleteIdentityVitalsItemDtoRs> {
    return this.http.post<CompleteIdentityVitalsItemDtoRs>('/api/swimmers/me/onboarding/identity-vitals', { body: rq });
  }
  completeGuardianMedical(rq: CompleteGuardianMedicalDtoRq): Promise<CompleteGuardianMedicalItemDtoRs> {
    return this.http.post<CompleteGuardianMedicalItemDtoRs>('/api/swimmers/me/onboarding/guardian-medical', { body: rq });
  }
  completePhysiological(rq: CompletePhysiologicalDtoRq): Promise<CompletePhysiologicalItemDtoRs> {
    return this.http.post<CompletePhysiologicalItemDtoRs>('/api/swimmers/me/onboarding/physiological', { body: rq });
  }
  completeInBody(rq: CompleteInBodyDtoRq): Promise<CompleteInBodyItemDtoRs> {
    return this.http.post<CompleteInBodyItemDtoRs>('/api/swimmers/me/onboarding/inbody', { body: rq });
  }
}
