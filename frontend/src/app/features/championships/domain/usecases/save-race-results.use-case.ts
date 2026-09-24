import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { isResultsDtoValid, SetRaceResultsRq } from '@features/championships/data/dto/race-result.dto';
import { toRaceResultList } from '@features/championships/data/dto/race-result.mapper';
import { RaceResultEntry } from '@features/championships/domain/model/race-result';

export interface SaveRaceResultsInput {
  eventId: string;
  raceSessionId: string;
  entries: { swimmerId: string; timeMs: number }[];
}

@Injectable({ providedIn: 'root' })
export class SaveRaceResultsUseCase extends UseCase<SaveRaceResultsInput, RaceResultEntry[]> {
  private readonly repo = inject(CHAMPIONSHIPS_REPOSITORY);
  constructor() { super('SaveRaceResults'); }

  protected async execute(input: SaveRaceResultsInput): Promise<RaceResultEntry[]> {
    const rq: SetRaceResultsRq = { entries: input.entries };
    const res = await this.repo.setRaceResults(input.eventId, input.raceSessionId, rq);
    if (!isResultsDtoValid(res.data)) throw new AppError('Invalid results received', 'validation');
    return toRaceResultList(res.data);
  }
}
