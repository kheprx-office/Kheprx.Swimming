import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { isSwimmerBodyMeasurementDtoRsValid } from '@features/swimmer-profile/data/dto/body-measurement.dto';
import { toLatestBodyMeasurement } from '@features/swimmer-profile/data/dto/body-measurement.mapper';
import { BodyMeasurement } from '@features/swimmer-profile/domain/model/body-measurement';

@Injectable({ providedIn: 'root' })
export class GetLatestBodyMeasurementUseCase extends UseCase<string, BodyMeasurement | null> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('GetLatestBodyMeasurement'); }
  protected async execute(id: string): Promise<BodyMeasurement | null> {
    const res = await this.repo.getBodyMeasurement(id);
    if (!isSwimmerBodyMeasurementDtoRsValid(res.data)) throw new AppError('Invalid body measurement received', 'validation');
    return toLatestBodyMeasurement(res.data);
  }
}
