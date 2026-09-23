import { InjectionToken } from '@angular/core';
import { CompetitionEventListDtoRs, CompetitionEventItemDtoRs, CreateChampionshipRq } from '@features/championships/data/dto/competition-event.dto';
import { EnrollmentIdsDtoRs, EnrollmentSaveDtoRs, SetEnrollmentsRq } from '@features/championships/data/dto/enrollment.dto';

export interface IChampionshipsRepository {
  getChampionships(): Promise<CompetitionEventListDtoRs>;
  createChampionship(rq: CreateChampionshipRq): Promise<CompetitionEventItemDtoRs>;
  getChampionship(id: string): Promise<CompetitionEventItemDtoRs>;
  getEnrollments(eventId: string): Promise<EnrollmentIdsDtoRs>;
  setEnrollments(eventId: string, rq: SetEnrollmentsRq): Promise<EnrollmentSaveDtoRs>;
}

export const CHAMPIONSHIPS_REPOSITORY = new InjectionToken<IChampionshipsRepository>('CHAMPIONSHIPS_REPOSITORY');
