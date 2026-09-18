import { InjectionToken } from '@angular/core';
import { CreateCoachDtoRq, CreatedCoachItemDtoRs } from '@features/coaches/data/dto/create-coach.dto';

export interface ICoachRepository {
  create(rq: CreateCoachDtoRq): Promise<CreatedCoachItemDtoRs>;
}

export const COACH_REPOSITORY = new InjectionToken<ICoachRepository>('COACH_REPOSITORY');
