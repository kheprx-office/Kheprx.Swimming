import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { isScheduleDtoValid } from '@features/championships/data/dto/schedule.dto';
import { toScheduleDataList, toSetScheduleRq } from '@features/championships/data/dto/schedule.mapper';
import { ScheduleDayData } from '@features/championships/domain/model/competition-schedule';

export interface SaveScheduleInput {
  eventId: string;
  days: ScheduleDayData[];
}

@Injectable({ providedIn: 'root' })
export class SaveScheduleUseCase extends UseCase<SaveScheduleInput, ScheduleDayData[]> {
  private readonly repo = inject(CHAMPIONSHIPS_REPOSITORY);
  constructor() { super('SaveSchedule'); }

  // The PUT replaces the whole schedule and echoes the persisted tree (fresh ids). We re-map it
  // to key-less data so the ViewModel can rebuild its baseline. A 4xx (e.g. 400 validation) throws
  // → run() converts to Result.fail and the caller shows the error toast.
  protected async execute(input: SaveScheduleInput): Promise<ScheduleDayData[]> {
    const res = await this.repo.setSchedule(input.eventId, toSetScheduleRq(input.days));
    if (!isScheduleDtoValid(res.data)) throw new AppError('Invalid schedule received', 'validation');
    return toScheduleDataList(res.data);
  }
}
