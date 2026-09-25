import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_ONBOARDING_REPOSITORY } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { IdentityVitalsSubmission } from '@features/swimmer-onboarding/domain/model/onboarding';

@Injectable({ providedIn: 'root' })
export class CompleteIdentityVitalsUseCase extends UseCase<IdentityVitalsSubmission, void> {
  private readonly repo = inject(SWIMMER_ONBOARDING_REPOSITORY);
  constructor() { super('CompleteIdentityVitals'); }

  protected async execute(input: IdentityVitalsSubmission): Promise<void> {
    await this.repo.completeIdentityVitals({
      nameEn: input.nameEn,
      nameAr: input.nameAr ?? null,
      genderId: input.genderId,
      dob: input.dob,
      trainingClubId: input.trainingClubId,
      examDate: input.examDate,
      bloodTypeId: input.bloodTypeId ?? null,
      hemoglobin: input.hemoglobin,
      heightCm: input.heightCm,
      weightKg: input.weightKg,
      internalMedId: input.internalMedId,
      heartAssessId: input.heartAssessId,
      spineAssessId: input.spineAssessId,
      phone: input.phone ?? null,
    });
  }
}
