import { InjectionToken } from '@angular/core';
import { AttendanceSessionItemDtoRs, SaveSessionDtoRq } from '@features/attendance/data/dto/attendance-session.dto';

export interface IAttendanceEntryRepository {
  getSession(date: string): Promise<AttendanceSessionItemDtoRs>;
  saveSession(rq: SaveSessionDtoRq): Promise<AttendanceSessionItemDtoRs>;
}

export const ATTENDANCE_ENTRY_REPOSITORY = new InjectionToken<IAttendanceEntryRepository>('ATTENDANCE_ENTRY_REPOSITORY');
