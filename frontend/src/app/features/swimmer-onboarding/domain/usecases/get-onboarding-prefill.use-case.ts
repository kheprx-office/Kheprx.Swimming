import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_ONBOARDING_REPOSITORY } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { isOnboardingPrefillValid } from '@features/swimmer-onboarding/data/dto/onboarding-prefill.dto';
import { OnboardingPrefill } from '@features/swimmer-onboarding/domain/model/onboarding';

@Injectable({ providedIn: 'root' })
export class GetOnboardingPrefillUseCase extends UseCase<void, OnboardingPrefill> {
  private readonly repo = inject(SWIMMER_ONBOARDING_REPOSITORY);
  constructor() { super('GetOnboardingPrefill'); }

  protected async execute(): Promise<OnboardingPrefill> {
    const res = await this.repo.getPrefill();
    if (!isOnboardingPrefillValid(res.data)) throw new AppError('Invalid onboarding prefill received', 'validation');
    const d = res.data;
    return { uid: d.uid, nameEn: d.nameEn, nameAr: d.nameAr ?? null, genderId: d.genderId ?? null, dob: d.dob ?? null, trainingClubId: d.trainingClubId ?? null, phone: d.phone ?? null };
  }
}
