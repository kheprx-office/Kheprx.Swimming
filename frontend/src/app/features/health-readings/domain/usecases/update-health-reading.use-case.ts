import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { HEALTH_READING_REPOSITORY } from '@features/health-readings/domain/repositories/health-reading.repository';
import { UpdateHealthReadingDtoRq, isHealthReadingRowDtoRsValid } from '@features/health-readings/data/dto/health-reading.dto';
import { toHealthReadingListItem } from '@features/health-readings/data/dto/health-reading-row.mapper';
import { HealthReadingListItem } from '@features/health-readings/domain/model/health-reading-list-item';

export interface UpdateHealthReadingInput { id: string; rq: UpdateHealthReadingDtoRq; }

@Injectable({ providedIn: 'root' })
export class UpdateHealthReadingUseCase extends UseCase<UpdateHealthReadingInput, HealthReadingListItem> {
  private readonly repo = inject(HEALTH_READING_REPOSITORY);
  constructor() { super('UpdateHealthReading'); }
  protected async execute(input: UpdateHealthReadingInput): Promise<HealthReadingListItem> {
    const res = await this.repo.update(input.id, input.rq);
    if (!isHealthReadingRowDtoRsValid(res.data)) throw new AppError('Invalid health reading received', 'validation');
    return toHealthReadingListItem(res.data);
  }
}
