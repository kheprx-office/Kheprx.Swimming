import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { isAttendanceRecordListValid } from '@features/swimmer-profile/data/dto/attendance-record.dto';
import { toAttendanceRecordList } from '@features/swimmer-profile/data/dto/attendance-record.mapper';
import { AttendanceRecord } from '@features/swimmer-profile/domain/model/attendance-record';

@Injectable({ providedIn: 'root' })
export class ListAttendanceRecordsUseCase extends UseCase<string, AttendanceRecord[]> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('ListAttendanceRecords'); }
  protected async execute(id: string): Promise<AttendanceRecord[]> {
    const res = await this.repo.getAttendanceRecords(id);
    if (!isAttendanceRecordListValid(res.data)) throw new AppError('Invalid attendance records received', 'validation');
    return toAttendanceRecordList(res.data);
  }
}
