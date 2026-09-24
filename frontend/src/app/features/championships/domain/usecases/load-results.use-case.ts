import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { isResultsDtoValid } from '@features/championships/data/dto/race-result.dto';
import { toRaceResultList } from '@features/championships/data/dto/race-result.mapper';
import { RaceResultEntry } from '@features/championships/domain/model/race-result';

@Injectable({ providedIn: 'root' })
export class LoadResultsUseCase extends UseCase<string, RaceResultEntry[]> {
  private readonly repo = inject(CHAMPIONSHIPS_REPOSITORY);
  constructor() { super('LoadResults'); }

  protected async execute(eventId: string): Promise<RaceResultEntry[]> {
    const res = await this.repo.getResults(eventId);
    if (!isResultsDtoValid(res.data)) throw new AppError('Invalid results received', 'validation');
    return toRaceResultList(res.data);
  }
}
