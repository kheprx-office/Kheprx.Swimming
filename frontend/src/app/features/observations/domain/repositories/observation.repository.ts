import { InjectionToken } from '@angular/core';
import { ObservationItemDtoRs, CreateObservationDtoRq } from '@features/observations/data/dto/observation.dto';

export interface IObservationRepository {
  create(rq: CreateObservationDtoRq): Promise<ObservationItemDtoRs>;
}

export const OBSERVATION_REPOSITORY = new InjectionToken<IObservationRepository>('OBSERVATION_REPOSITORY');
