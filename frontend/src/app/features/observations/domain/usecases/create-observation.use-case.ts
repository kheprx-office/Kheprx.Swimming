import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { OBSERVATION_REPOSITORY } from '@features/observations/domain/repositories/observation.repository';
import { CreateObservationDtoRq, isObservationDtoRsValid } from '@features/observations/data/dto/observation.dto';
import { Observation } from '@features/observations/domain/model/observation';

@Injectable({ providedIn: 'root' })
export class CreateObservationUseCase extends UseCase<CreateObservationDtoRq, Observation> {
  private readonly repo = inject(OBSERVATION_REPOSITORY);
  constructor() { super('CreateObservation'); }

  protected async execute(input: CreateObservationDtoRq): Promise<Observation> {
    const res = await this.repo.create(input);
    if (!isObservationDtoRsValid(res.data)) throw new AppError('Invalid created observation received', 'validation');
    const d = res.data;
    return {
      id: d.id, swimmerId: d.swimmerId, categoryId: d.categoryId,
      fieldLabel: d.fieldLabel, value: d.value, observedDate: d.observedDate, recordedBy: d.recordedBy,
    };
  }
}
