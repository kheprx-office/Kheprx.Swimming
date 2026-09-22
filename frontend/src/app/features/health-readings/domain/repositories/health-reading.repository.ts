import { InjectionToken } from '@angular/core';
import {
  HealthReadingItemDtoRs,
  CreateHealthReadingDtoRq,
  HealthReadingListDtoRs,
  HealthReadingRowItemDtoRs,
  DeleteHealthReadingItemDtoRs,
  UpdateHealthReadingDtoRq,
} from '@features/health-readings/data/dto/health-reading.dto';

export interface IHealthReadingRepository {
  create(rq: CreateHealthReadingDtoRq): Promise<HealthReadingItemDtoRs>;
  list(swimmerId: string): Promise<HealthReadingListDtoRs>;
  update(id: string, rq: UpdateHealthReadingDtoRq): Promise<HealthReadingRowItemDtoRs>;
  remove(id: string): Promise<DeleteHealthReadingItemDtoRs>;
}

export const HEALTH_READING_REPOSITORY = new InjectionToken<IHealthReadingRepository>('HEALTH_READING_REPOSITORY');
