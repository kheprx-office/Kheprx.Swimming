// swimmer.repository.impl.ts — swimmers repository. Returns the API DTO envelope.
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { ISwimmerRepository } from '@features/swimmers/domain/repositories/swimmer.repository';
import { SwimmerCountItemDtoRs } from '@features/swimmers/data/dto/swimmer-count.dto';
import { CreateSwimmerDtoRq, CreatedSwimmerItemDtoRs } from '@features/swimmers/data/dto/create-swimmer.dto';
import { SwimmerListItemsDtoRs } from '@features/swimmers/data/dto/swimmer-list.dto';

@Injectable({ providedIn: 'root' })
export class SwimmerRepositoryImpl implements ISwimmerRepository {
  private readonly http = inject(HttpClientService);

  getCount(): Promise<SwimmerCountItemDtoRs> {
    return this.http.get<SwimmerCountItemDtoRs>('/api/swimmers/count');
  }

  list(search?: string): Promise<SwimmerListItemsDtoRs> {
    const term = search?.trim();
    return this.http.get<SwimmerListItemsDtoRs>('/api/swimmers', term ? { params: { search: term } } : undefined);
  }

  create(rq: CreateSwimmerDtoRq): Promise<CreatedSwimmerItemDtoRs> {
    return this.http.post<CreatedSwimmerItemDtoRs>('/api/swimmers', { body: rq });
  }
}
