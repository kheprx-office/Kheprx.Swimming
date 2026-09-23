import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface AttendanceRecordDtoRs {
  id: string;
  swimmerId: string;
  sessionDate: string;
  statusId: string;
  coachNoteEn: string | null;
  coachNoteAr: string | null;
  recordedBy: string;
  recordedByNameEn: string;
  recordedByNameAr: string | null;
}
export interface AttendanceRecordListDtoRs extends BaseResponseRs<AttendanceRecordDtoRs[]> {}

export function isAttendanceRecordDtoRsValid(x: unknown): x is AttendanceRecordDtoRs {
  const d = x as AttendanceRecordDtoRs;
  if (!d || typeof d !== 'object') return false;
  return typeof d.id === 'string'
    && typeof d.swimmerId === 'string'
    && typeof d.sessionDate === 'string'
    && typeof d.statusId === 'string';
}

export function isAttendanceRecordListValid(data: unknown): data is AttendanceRecordDtoRs[] {
  return Array.isArray(data) && data.every(isAttendanceRecordDtoRsValid);
}
