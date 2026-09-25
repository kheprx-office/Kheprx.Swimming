import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_ONBOARDING_REPOSITORY } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { PhysiologicalSubmission } from '@features/swimmer-onboarding/domain/model/onboarding';

@Injectable({ providedIn: 'root' })
export class CompletePhysiologicalUseCase extends UseCase<PhysiologicalSubmission, void> {
  private readonly repo = inject(SWIMMER_ONBOARDING_REPOSITORY);
  constructor() { super('CompletePhysiological'); }

  protected async execute(input: PhysiologicalSubmission): Promise<void> {
    await this.repo.completePhysiological({
      rightArmCm: input.rightArmCm,
      leftArmCm: input.leftArmCm,
      rightLegCm: input.rightLegCm,
      leftLegCm: input.leftLegCm,
      torsoCm: input.torsoCm,
      bustDiameterCm: input.bustDiameterCm,
      waistDiameterCm: input.waistDiameterCm,
    });
  }
}
