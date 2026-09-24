import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { isScheduleDtoValid } from '@features/championships/data/dto/schedule.dto';
import { toScheduleDataList } from '@features/championships/data/dto/schedule.mapper';
import { ScheduleDayData } from '@features/championships/domain/model/competition-schedule';

@Injectable({ providedIn: 'root' })
export class LoadScheduleUseCase extends UseCase<string, ScheduleDayData[]> {
  private readonly repo = inject(CHAMPIONSHIPS_REPOSITORY);
  constructor() { super('LoadSchedule'); }

  protected async execute(eventId: string): Promise<ScheduleDayData[]> {
    const res = await this.repo.getSchedule(eventId);
    if (!isScheduleDtoValid(res.data)) throw new AppError('Invalid schedule received', 'validation');
    return toScheduleDataList(res.data);
  }
}
