import { InjectionToken } from '@angular/core';
import { CompetitionEventListDtoRs, CompetitionEventItemDtoRs, CreateChampionshipRq } from '@features/championships/data/dto/competition-event.dto';
import { EnrollmentIdsDtoRs, EnrollmentSaveDtoRs, SetEnrollmentsRq } from '@features/championships/data/dto/enrollment.dto';
import { ScheduleDtoRs, SetScheduleRq } from '@features/championships/data/dto/schedule.dto';
import { ResultsDtoRs, SetRaceResultsRq } from '@features/championships/data/dto/race-result.dto';
import { SwimmerChampionshipHistoryDtoRs } from '@features/championships/data/dto/swimmer-championship-history.dto';

export interface IChampionshipsRepository {
  getChampionships(): Promise<CompetitionEventListDtoRs>;
  createChampionship(rq: CreateChampionshipRq): Promise<CompetitionEventItemDtoRs>;
  getChampionship(id: string): Promise<CompetitionEventItemDtoRs>;
  getEnrollments(eventId: string): Promise<EnrollmentIdsDtoRs>;
  setEnrollments(eventId: string, rq: SetEnrollmentsRq): Promise<EnrollmentSaveDtoRs>;
  getSchedule(eventId: string): Promise<ScheduleDtoRs>;
  setSchedule(eventId: string, rq: SetScheduleRq): Promise<ScheduleDtoRs>;
  getResults(eventId: string): Promise<ResultsDtoRs>;
  setRaceResults(eventId: string, raceSessionId: string, rq: SetRaceResultsRq): Promise<ResultsDtoRs>;
  getSwimmerChampionshipHistory(swimmerId: string): Promise<SwimmerChampionshipHistoryDtoRs>;
}

export const CHAMPIONSHIPS_REPOSITORY = new InjectionToken<IChampionshipsRepository>('CHAMPIONSHIPS_REPOSITORY');
