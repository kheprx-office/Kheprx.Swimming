import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface ScheduleRaceDtoRs {
  id: string;
  strokeId: string;
  distanceId: string;
  scheduledTime: string | null; // 'HH:mm:ss' | 'HH:mm' | null
  swimmerIds: string[];
}
export interface ScheduleDayDtoRs {
  id: string;
  labelEn: string;
  labelAr: string | null;
  dayDate: string;              // 'YYYY-MM-DD'
  races: ScheduleRaceDtoRs[];
}
export interface ScheduleDtoData {
  days: ScheduleDayDtoRs[];
}
export interface ScheduleDtoRs extends BaseResponseRs<ScheduleDtoData> {}

/** PUT body — the whole desired schedule (server replaces atomically). */
export interface SetScheduleRq {
  days: {
    labelEn: string;
    labelAr: string | null;
    dayDate: string;
    races: {
      strokeId: string;
      distanceId: string;
      scheduledTime: string | null;
      swimmerIds: string[];
    }[];
  }[];
}

function isStr(v: unknown): v is string { return typeof v === 'string'; }

export function isScheduleDtoValid(data: unknown): data is ScheduleDtoData {
  const d = data as ScheduleDtoData;
  if (!d || typeof d !== 'object' || !Array.isArray(d.days)) return false;
  return d.days.every(
    (day) =>
      day != null && typeof day === 'object' &&
      isStr(day.id) && isStr(day.labelEn) && isStr(day.dayDate) &&
      Array.isArray(day.races) &&
      day.races.every(
        (r) =>
          r != null && typeof r === 'object' &&
          isStr(r.id) && isStr(r.strokeId) && isStr(r.distanceId) &&
          Array.isArray(r.swimmerIds) && r.swimmerIds.every(isStr),
      ),
  );
}
