import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { HEALTH_READING_REPOSITORY } from '@features/health-readings/domain/repositories/health-reading.repository';
import { CreateHealthReadingDtoRq, isHealthReadingDtoRsValid } from '@features/health-readings/data/dto/health-reading.dto';
import { HealthReading } from '@features/health-readings/domain/model/health-reading';

@Injectable({ providedIn: 'root' })
export class CreateHealthReadingUseCase extends UseCase<CreateHealthReadingDtoRq, HealthReading> {
  private readonly repo = inject(HEALTH_READING_REPOSITORY);
  constructor() { super('CreateHealthReading'); }

  protected async execute(input: CreateHealthReadingDtoRq): Promise<HealthReading> {
    const res = await this.repo.create(input);
    if (!isHealthReadingDtoRsValid(res.data)) throw new AppError('Invalid created health reading received', 'validation');
    const d = res.data;
    return {
      id: d.id, swimmerId: d.swimmerId, medicalTestId: d.medicalTestId,
      value: d.value, readingDate: d.readingDate, recordedBy: d.recordedBy, status: d.status,
    };
  }
}
