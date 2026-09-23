import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface SwimmerSessionRowDtoRs {
  swimmerId: string;
  uid: string;
  nameEn: string;
  nameAr: string | null;
  clubNameEn: string | null;
  clubNameAr: string | null;
  genderCode: string;
  statusId: string | null;
  coachNote: string | null;
  monthRatePct: number | null;
  hasRecord: boolean;
}

export interface AttendanceSessionDtoRs {
  date: string;
  rows: SwimmerSessionRowDtoRs[];
}

export interface AttendanceSessionItemDtoRs extends BaseResponseRs<AttendanceSessionDtoRs> {}

export interface SaveSessionEntryDtoRq {
  swimmerId: string;
  statusId: string;
  coachNote: string | null;
}
export interface SaveSessionDtoRq {
  date: string;
  entries: SaveSessionEntryDtoRq[];
}

export function isAttendanceSessionDtoRsValid(x: unknown): x is AttendanceSessionDtoRs {
  const d = x as AttendanceSessionDtoRs;
  return !!d && typeof d === 'object'
    && typeof d.date === 'string'
    && Array.isArray(d.rows)
    && d.rows.every((r) =>
      !!r && typeof r.swimmerId === 'string' && typeof r.nameEn === 'string'
      && typeof r.hasRecord === 'boolean');
}
