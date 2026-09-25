import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_ONBOARDING_REPOSITORY } from '@features/swimmer-onboarding/domain/repositories/swimmer-onboarding.repository';
import { InBodySubmission } from '@features/swimmer-onboarding/domain/model/onboarding';

@Injectable({ providedIn: 'root' })
export class CompleteInBodyUseCase extends UseCase<InBodySubmission, void> {
  private readonly repo = inject(SWIMMER_ONBOARDING_REPOSITORY);
  constructor() { super('CompleteInBody'); }

  protected async execute(input: InBodySubmission): Promise<void> {
    await this.repo.completeInBody({
      heightCm: input.heightCm,
      weightKg: input.weightKg,
      fatPct: input.fatPct,
      musclePct: input.musclePct,
      waterPct: input.waterPct,
      boneDensity: input.boneDensity,
      bodyDensity: input.bodyDensity,
    });
  }
}
