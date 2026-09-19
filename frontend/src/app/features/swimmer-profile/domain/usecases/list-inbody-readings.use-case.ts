import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { isInBodyReadingListValid } from '@features/swimmer-profile/data/dto/inbody-reading.dto';
import { toInBodyReadingList } from '@features/swimmer-profile/data/dto/inbody-reading.mapper';
import { InBodyReading } from '@features/swimmer-profile/domain/model/inbody-reading';

@Injectable({ providedIn: 'root' })
export class ListInBodyReadingsUseCase extends UseCase<string, InBodyReading[]> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('ListInBodyReadings'); }
  protected async execute(id: string): Promise<InBodyReading[]> {
    const res = await this.repo.getInBodyReadings(id);
    if (!isInBodyReadingListValid(res.data)) throw new AppError('Invalid InBody readings received', 'validation');
    return toInBodyReadingList(res.data);
  }
}
