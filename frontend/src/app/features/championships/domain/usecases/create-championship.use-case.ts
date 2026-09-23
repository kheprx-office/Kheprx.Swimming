import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { CreateChampionshipRq, isCompetitionEventDtoRsValid } from '@features/championships/data/dto/competition-event.dto';
import { toChampionship } from '@features/championships/data/dto/competition-event.mapper';
import { Championship } from '@features/championships/domain/model/championship';

@Injectable({ providedIn: 'root' })
export class CreateChampionshipUseCase extends UseCase<CreateChampionshipRq, Championship> {
  private readonly repo = inject(CHAMPIONSHIPS_REPOSITORY);
  constructor() { super('CreateChampionship'); }
  protected async execute(rq: CreateChampionshipRq): Promise<Championship> {
    const res = await this.repo.createChampionship(rq);
    if (!isCompetitionEventDtoRsValid(res.data)) throw new AppError('Invalid championship received', 'validation');
    return toChampionship(res.data);
  }
}
