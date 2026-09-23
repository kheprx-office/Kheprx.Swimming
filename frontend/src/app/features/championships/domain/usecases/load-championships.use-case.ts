import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { isCompetitionEventListValid } from '@features/championships/data/dto/competition-event.dto';
import { toChampionshipList } from '@features/championships/data/dto/competition-event.mapper';
import { Championship } from '@features/championships/domain/model/championship';

@Injectable({ providedIn: 'root' })
export class LoadChampionshipsUseCase extends UseCase<void, Championship[]> {
  private readonly repo = inject(CHAMPIONSHIPS_REPOSITORY);
  constructor() { super('LoadChampionships'); }
  protected async execute(): Promise<Championship[]> {
    const res = await this.repo.getChampionships();
    if (!isCompetitionEventListValid(res.data)) throw new AppError('Invalid championships received', 'validation');
    return toChampionshipList(res.data);
  }
}
