import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { ICoachRepository } from '@features/coaches/domain/repositories/coach.repository';
import { CreateCoachDtoRq, CreatedCoachItemDtoRs } from '@features/coaches/data/dto/create-coach.dto';

@Injectable({ providedIn: 'root' })
export class CoachRepositoryImpl implements ICoachRepository {
  private readonly http = inject(HttpClientService);
  create(rq: CreateCoachDtoRq): Promise<CreatedCoachItemDtoRs> {
    return this.http.post<CreatedCoachItemDtoRs>('/api/coaches', { body: rq });
  }
}
