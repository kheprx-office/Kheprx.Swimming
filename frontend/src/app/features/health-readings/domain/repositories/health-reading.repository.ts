import { InjectionToken } from '@angular/core';
import {
  HealthReadingItemDtoRs,
  CreateHealthReadingDtoRq,
} from '@features/health-readings/data/dto/health-reading.dto';

export interface IHealthReadingRepository {
  create(rq: CreateHealthReadingDtoRq): Promise<HealthReadingItemDtoRs>;
}

export const HEALTH_READING_REPOSITORY = new InjectionToken<IHealthReadingRepository>('HEALTH_READING_REPOSITORY');
