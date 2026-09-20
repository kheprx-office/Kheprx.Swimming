import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { isRecordListValid } from '@features/swimmer-profile/data/dto/record.dto';
import { toRecordEntryList } from '@features/swimmer-profile/data/dto/record.mapper';
import { RecordEntry } from '@features/swimmer-profile/domain/model/record-entry';

@Injectable({ providedIn: 'root' })
export class ListRecordsUseCase extends UseCase<string, RecordEntry[]> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('ListRecords'); }
  protected async execute(id: string): Promise<RecordEntry[]> {
    const res = await this.repo.listRecords(id);
    if (!isRecordListValid(res.data)) throw new AppError('Invalid records received', 'validation');
    return toRecordEntryList(res.data);
  }
}
