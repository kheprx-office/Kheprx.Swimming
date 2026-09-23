import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { IAttendanceEntryRepository } from '@features/attendance/domain/repositories/attendance-entry.repository';
import { AttendanceSessionItemDtoRs, SaveSessionDtoRq } from '@features/attendance/data/dto/attendance-session.dto';

@Injectable({ providedIn: 'root' })
export class AttendanceEntryRepositoryImpl implements IAttendanceEntryRepository {
  private readonly http = inject(HttpClientService);

  getSession(date: string): Promise<AttendanceSessionItemDtoRs> {
    return this.http.get<AttendanceSessionItemDtoRs>('/api/attendance-records/session', { params: { date } });
  }

  saveSession(rq: SaveSessionDtoRq): Promise<AttendanceSessionItemDtoRs> {
    return this.http.put<AttendanceSessionItemDtoRs>('/api/attendance-records/session', { body: rq });
  }
}
