import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_REPOSITORY } from '@features/swimmers/domain/repositories/swimmer.repository';
import { isSwimmerCountDtoRsValid } from '@features/swimmers/data/dto/swimmer-count.dto';

// LoadSwimmerCountUseCase: fetches the total tracked-swimmer count (GET /api/swimmers/count)
// for the login hero stat. Anonymous endpoint, safe to call before sign-in.
@Injectable({ providedIn: 'root' })
export class LoadSwimmerCountUseCase extends UseCase<void, number> {
  private readonly repo = inject(SWIMMER_REPOSITORY);
  constructor() { super('LoadSwimmerCount'); }
  protected async execute(): Promise<number> {
    const res = await this.repo.getCount();
    if (!isSwimmerCountDtoRsValid(res.data)) throw new AppError('Invalid swimmer count received', 'validation');
    return res.data.count;
  }
}
