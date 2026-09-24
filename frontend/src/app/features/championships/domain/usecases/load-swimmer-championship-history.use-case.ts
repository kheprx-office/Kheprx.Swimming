import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { isSwimmerChampionshipHistoryValid } from '@features/championships/data/dto/swimmer-championship-history.dto';
import { toSwimmerChampionshipHistory } from '@features/championships/data/dto/swimmer-championship-history.mapper';
import { SwimmerChampionshipHistory } from '@features/championships/domain/model/swimmer-championship-history';

@Injectable({ providedIn: 'root' })
export class LoadSwimmerChampionshipHistoryUseCase extends UseCase<string, SwimmerChampionshipHistory[]> {
  private readonly repo = inject(CHAMPIONSHIPS_REPOSITORY);
  constructor() { super('LoadSwimmerChampionshipHistory'); }

  protected async execute(swimmerId: string): Promise<SwimmerChampionshipHistory[]> {
    const res = await this.repo.getSwimmerChampionshipHistory(swimmerId);
    if (!isSwimmerChampionshipHistoryValid(res.data)) throw new AppError('Invalid championship history received', 'validation');
    return toSwimmerChampionshipHistory(res.data);
  }
}
