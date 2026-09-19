import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { isVitalsListValid } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';
import { toVitals } from '@features/swimmer-profile/data/dto/vitals.mapper';
import { SwimmerVitals } from '@features/swimmer-profile/domain/model/swimmer-profile';

@Injectable({ providedIn: 'root' })
export class ListMedicalExamsUseCase extends UseCase<string, SwimmerVitals[]> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('ListMedicalExams'); }
  protected async execute(id: string): Promise<SwimmerVitals[]> {
    const res = await this.repo.listExams(id);
    if (!isVitalsListValid(res.data)) throw new AppError('Invalid exam list received', 'validation');
    return res.data.map(toVitals);
  }
}
