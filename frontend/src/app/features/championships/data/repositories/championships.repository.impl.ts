import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { IChampionshipsRepository } from '@features/championships/domain/repositories/championships.repository';
import { CompetitionEventListDtoRs, CompetitionEventItemDtoRs, CreateChampionshipRq } from '@features/championships/data/dto/competition-event.dto';

@Injectable({ providedIn: 'root' })
export class ChampionshipsRepositoryImpl implements IChampionshipsRepository {
  private readonly http = inject(HttpClientService);

  getChampionships(): Promise<CompetitionEventListDtoRs> {
    return this.http.get<CompetitionEventListDtoRs>('/api/championships');
  }

  createChampionship(rq: CreateChampionshipRq): Promise<CompetitionEventItemDtoRs> {
    return this.http.post<CompetitionEventItemDtoRs>('/api/championships', { body: rq });
  }
}
