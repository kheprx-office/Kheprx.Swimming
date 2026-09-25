import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_ONBOARDING_REPOSITORY } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { GuardianMedicalSubmission } from '@features/swimmer-onboarding/domain/model/onboarding';

@Injectable({ providedIn: 'root' })
export class CompleteGuardianMedicalUseCase extends UseCase<GuardianMedicalSubmission, void> {
  private readonly repo = inject(SWIMMER_ONBOARDING_REPOSITORY);
  constructor() { super('CompleteGuardianMedical'); }

  protected async execute(input: GuardianMedicalSubmission): Promise<void> {
    await this.repo.completeGuardianMedical({
      father: { name: input.father.name, nationalId: input.father.nationalId, phone: input.father.phone },
      mother: { name: input.mother.name, nationalId: input.mother.nationalId, phone: input.mother.phone },
      medical: input.medical.map((m) => ({ categoryId: m.categoryId, fieldLabel: m.fieldLabel, value: m.value })),
    });
  }
}
