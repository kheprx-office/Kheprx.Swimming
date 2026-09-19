import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { isSwimmerGuardiansDtoRsValid } from '@features/swimmer-profile/data/dto/guardians.dto';
import { toSwimmerGuardians } from '@features/swimmer-profile/data/dto/guardians.mapper';
import { SwimmerGuardians } from '@features/swimmer-profile/domain/model/swimmer-guardians';

@Injectable({ providedIn: 'root' })
export class GetSwimmerGuardiansUseCase extends UseCase<string, SwimmerGuardians> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('GetSwimmerGuardians'); }
  protected async execute(id: string): Promise<SwimmerGuardians> {
    const res = await this.repo.getGuardians(id);
    if (!isSwimmerGuardiansDtoRsValid(res.data)) throw new AppError('Invalid guardians received', 'validation');
    return toSwimmerGuardians(res.data);
  }
}
