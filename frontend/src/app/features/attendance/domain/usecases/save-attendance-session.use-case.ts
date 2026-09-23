import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { ATTENDANCE_ENTRY_REPOSITORY } from '@features/attendance/domain/repositories/attendance-entry.repository';
import { isAttendanceSessionDtoRsValid, SaveSessionDtoRq } from '@features/attendance/data/dto/attendance-session.dto';
import { toAttendanceSession } from '@features/attendance/data/dto/attendance-session.mapper';
import { AttendanceSession } from '@features/attendance/domain/model/attendance-session';

@Injectable({ providedIn: 'root' })
export class SaveAttendanceSessionUseCase extends UseCase<SaveSessionDtoRq, AttendanceSession> {
  private readonly repo = inject(ATTENDANCE_ENTRY_REPOSITORY);
  constructor() { super('SaveAttendanceSession'); }
  protected async execute(rq: SaveSessionDtoRq): Promise<AttendanceSession> {
    const res = await this.repo.saveSession(rq);
    if (!isAttendanceSessionDtoRsValid(res.data)) throw new AppError('Invalid attendance session received', 'validation');
    return toAttendanceSession(res.data);
  }
}
