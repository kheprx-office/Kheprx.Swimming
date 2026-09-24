// swimmer-onboarding.repository.impl.ts — self-service onboarding read + write (JWT-scoped `me` endpoints).
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { ISwimmerOnboardingRepository } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { OnboardingPrefillItemDtoRs } from '@features/swimmer-onboarding/data/dto/onboarding-prefill.dto';
import { CompleteIdentityVitalsDtoRq, CompleteIdentityVitalsItemDtoRs } from '@features/swimmer-onboarding/data/dto/complete-identity-vitals.dto';

@Injectable({ providedIn: 'root' })
export class SwimmerOnboardingRepositoryImpl implements ISwimmerOnboardingRepository {
  private readonly http = inject(HttpClientService);

  getPrefill(): Promise<OnboardingPrefillItemDtoRs> {
    return this.http.get<OnboardingPrefillItemDtoRs>('/api/swimmers/me/onboarding/identity-vitals');
  }
  completeIdentityVitals(rq: CompleteIdentityVitalsDtoRq): Promise<CompleteIdentityVitalsItemDtoRs> {
    return this.http.post<CompleteIdentityVitalsItemDtoRs>('/api/swimmers/me/onboarding/identity-vitals', { body: rq });
  }
}
