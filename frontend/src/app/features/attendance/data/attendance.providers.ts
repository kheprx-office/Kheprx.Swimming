import { Provider } from '@angular/core';
import { ATTENDANCE_ENTRY_REPOSITORY } from '@features/attendance/domain/repositories/attendance-entry.repository';
import { AttendanceEntryRepositoryImpl } from '@features/attendance/data/repositories/attendance-entry.repository.impl';

export const ATTENDANCE_PROVIDERS: Provider[] = [
  { provide: ATTENDANCE_ENTRY_REPOSITORY, useClass: AttendanceEntryRepositoryImpl },
];
