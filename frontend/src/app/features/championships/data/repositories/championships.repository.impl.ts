import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { IChampionshipsRepository } from '@features/championships/domain/repositories/championships.repository';
import { CompetitionEventListDtoRs, CompetitionEventItemDtoRs, CreateChampionshipRq } from '@features/championships/data/dto/competition-event.dto';
import { EnrollmentIdsDtoRs, EnrollmentSaveDtoRs, SetEnrollmentsRq } from '@features/championships/data/dto/enrollment.dto';
import { ScheduleDtoRs, SetScheduleRq } from '@features/championships/data/dto/schedule.dto';
import { ResultsDtoRs, SetRaceResultsRq } from '@features/championships/data/dto/race-result.dto';
import { SwimmerChampionshipHistoryDtoRs } from '@features/championships/data/dto/swimmer-championship-history.dto';

@Injectable({ providedIn: 'root' })
export class ChampionshipsRepositoryImpl implements IChampionshipsRepository {
  private readonly http = inject(HttpClientService);

  getChampionships(): Promise<CompetitionEventListDtoRs> {
    return this.http.get<CompetitionEventListDtoRs>('/api/championships');
  }

  createChampionship(rq: CreateChampionshipRq): Promise<CompetitionEventItemDtoRs> {
    return this.http.post<CompetitionEventItemDtoRs>('/api/championships', { body: rq });
  }

  getChampionship(id: string): Promise<CompetitionEventItemDtoRs> {
    return this.http.get<CompetitionEventItemDtoRs>(`/api/championships/${id}`);
  }

  getEnrollments(eventId: string): Promise<EnrollmentIdsDtoRs> {
    return this.http.get<EnrollmentIdsDtoRs>(`/api/championships/${eventId}/enrollments`);
  }

  setEnrollments(eventId: string, rq: SetEnrollmentsRq): Promise<EnrollmentSaveDtoRs> {
    return this.http.put<EnrollmentSaveDtoRs>(`/api/championships/${eventId}/enrollments`, { body: rq });
  }

  getSchedule(eventId: string): Promise<ScheduleDtoRs> {
    return this.http.get<ScheduleDtoRs>(`/api/championships/${eventId}/schedule`);
  }

  setSchedule(eventId: string, rq: SetScheduleRq): Promise<ScheduleDtoRs> {
    return this.http.put<ScheduleDtoRs>(`/api/championships/${eventId}/schedule`, { body: rq });
  }

  getResults(eventId: string): Promise<ResultsDtoRs> {
    return this.http.get<ResultsDtoRs>(`/api/championships/${eventId}/results`);
  }

  setRaceResults(eventId: string, raceSessionId: string, rq: SetRaceResultsRq): Promise<ResultsDtoRs> {
    return this.http.put<ResultsDtoRs>(`/api/championships/${eventId}/races/${raceSessionId}/results`, { body: rq });
  }

  getSwimmerChampionshipHistory(swimmerId: string): Promise<SwimmerChampionshipHistoryDtoRs> {
    return this.http.get<SwimmerChampionshipHistoryDtoRs>(`/api/championships/swimmer/${swimmerId}/history`);
  }
}
