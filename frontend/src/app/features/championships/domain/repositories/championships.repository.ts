import { InjectionToken } from '@angular/core';
import { CompetitionEventListDtoRs, CompetitionEventItemDtoRs, CreateChampionshipRq } from '@features/championships/data/dto/competition-event.dto';

export interface IChampionshipsRepository {
  getChampionships(): Promise<CompetitionEventListDtoRs>;
  createChampionship(rq: CreateChampionshipRq): Promise<CompetitionEventItemDtoRs>;
}

export const CHAMPIONSHIPS_REPOSITORY = new InjectionToken<IChampionshipsRepository>('CHAMPIONSHIPS_REPOSITORY');
