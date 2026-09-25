import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { isMySwimmerRefValid } from '@features/swimmer-profile/data/dto/my-swimmer-ref.dto';

@Injectable({ providedIn: 'root' })
export class GetMySwimmerIdUseCase extends UseCase<void, string> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('GetMySwimmerId'); }

  protected async execute(): Promise<string> {
    const res = await this.repo.getMySwimmerId();
    if (!isMySwimmerRefValid(res.data)) throw new AppError('Invalid my-swimmer-id response', 'validation');
    return res.data.swimmerId;
  }
}
