// health-reading.repository.impl.ts — health-readings repository. Returns the API DTO envelope.
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { IHealthReadingRepository } from '@features/health-readings/domain/repositories/health-reading.repository';
import {
  HealthReadingItemDtoRs,
  CreateHealthReadingDtoRq,
} from '@features/health-readings/data/dto/health-reading.dto';

@Injectable({ providedIn: 'root' })
export class HealthReadingRepositoryImpl implements IHealthReadingRepository {
  private readonly http = inject(HttpClientService);

  create(rq: CreateHealthReadingDtoRq): Promise<HealthReadingItemDtoRs> {
    return this.http.post<HealthReadingItemDtoRs>('/api/health-readings', { body: rq });
  }
}
