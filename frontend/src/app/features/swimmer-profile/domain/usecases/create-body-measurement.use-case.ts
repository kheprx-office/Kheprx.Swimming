import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { CreateBodyMeasurementDtoRq } from '@features/swimmer-profile/data/dto/body-measurement.dto';

export interface CreateBodyMeasurementInput { id: string; rq: CreateBodyMeasurementDtoRq; }

@Injectable({ providedIn: 'root' })
export class CreateBodyMeasurementUseCase extends UseCase<CreateBodyMeasurementInput, void> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('CreateBodyMeasurement'); }
  protected async execute(input: CreateBodyMeasurementInput): Promise<void> {
    await this.repo.createBodyMeasurement(input.id, input.rq);
  }
}
