import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { UpdateRecordDtoRq, isRecordDtoRsValid } from '@features/swimmer-profile/data/dto/record.dto';
import { toRecordEntry } from '@features/swimmer-profile/data/dto/record.mapper';
import { RecordEntry } from '@features/swimmer-profile/domain/model/record-entry';

export interface UpdateRecordInput { recordId: string; rq: UpdateRecordDtoRq; }

@Injectable({ providedIn: 'root' })
export class UpdateRecordUseCase extends UseCase<UpdateRecordInput, RecordEntry> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('UpdateRecord'); }
  protected async execute(input: UpdateRecordInput): Promise<RecordEntry> {
    const res = await this.repo.updateRecord(input.recordId, input.rq);
    if (!isRecordDtoRsValid(res.data)) throw new AppError('Invalid record received', 'validation');
    return toRecordEntry(res.data);
  }
}
