import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { isScheduleDtoValid } from '@features/championships/data/dto/schedule.dto';
import { toRaceScheduleList } from '@features/championships/data/dto/schedule.mapper';
import { RaceScheduleDay } from '@features/championships/domain/model/race-result';

@Injectable({ providedIn: 'root' })
export class LoadRaceScheduleUseCase extends UseCase<string, RaceScheduleDay[]> {
  private readonly repo = inject(CHAMPIONSHIPS_REPOSITORY);
  constructor() { super('LoadRaceSchedule'); }

  protected async execute(eventId: string): Promise<RaceScheduleDay[]> {
    const res = await this.repo.getSchedule(eventId);
    if (!isScheduleDtoValid(res.data)) throw new AppError('Invalid schedule received', 'validation');
    return toRaceScheduleList(res.data);
  }
}
