// observation.repository.impl.ts — observations repository. Returns the API DTO envelope.
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { IObservationRepository } from '@features/observations/domain/repositories/observation.repository';
import { ObservationItemDtoRs, CreateObservationDtoRq } from '@features/observations/data/dto/observation.dto';

@Injectable({ providedIn: 'root' })
export class ObservationRepositoryImpl implements IObservationRepository {
  private readonly http = inject(HttpClientService);

  create(rq: CreateObservationDtoRq): Promise<ObservationItemDtoRs> {
    return this.http.post<ObservationItemDtoRs>('/api/observations', { body: rq });
  }
}
