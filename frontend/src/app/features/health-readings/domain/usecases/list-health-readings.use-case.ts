import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { HEALTH_READING_REPOSITORY } from '@features/health-readings/domain/repositories/health-reading.repository';
import { isHealthReadingRowListValid } from '@features/health-readings/data/dto/health-reading.dto';
import { toHealthReadingListItemList } from '@features/health-readings/data/dto/health-reading-row.mapper';
import { HealthReadingListItem } from '@features/health-readings/domain/model/health-reading-list-item';

@Injectable({ providedIn: 'root' })
export class ListHealthReadingsUseCase extends UseCase<string, HealthReadingListItem[]> {
  private readonly repo = inject(HEALTH_READING_REPOSITORY);
  constructor() { super('ListHealthReadings'); }
  protected async execute(swimmerId: string): Promise<HealthReadingListItem[]> {
    const res = await this.repo.list(swimmerId);
    if (!isHealthReadingRowListValid(res.data)) throw new AppError('Invalid health readings received', 'validation');
    return toHealthReadingListItemList(res.data);
  }
}
