import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface SwimmerChampionshipRaceData {
  dayLabelEn: string;
  dayLabelAr: string | null;
  distanceId: string;
  strokeId: string;
  timeMs: number;
  isPersonalBest: boolean;
}
export interface SwimmerChampionshipHistoryData {
  eventId: string;
  nameEn: string;
  nameAr: string | null;
  startDate: string;
  endDate: string;
  locationEn: string;
  locationAr: string | null;
  races: SwimmerChampionshipRaceData[];
}
export interface SwimmerChampionshipHistoryDtoRs extends BaseResponseRs<SwimmerChampionshipHistoryData[]> {}

function isStr(v: unknown): v is string { return typeof v === 'string'; }
function isNum(v: unknown): v is number { return typeof v === 'number' && Number.isFinite(v); }

function isRace(r: unknown): r is SwimmerChampionshipRaceData {
  const x = r as SwimmerChampionshipRaceData;
  return !!x && typeof x === 'object'
    && isStr(x.dayLabelEn) && isStr(x.distanceId) && isStr(x.strokeId)
    && isNum(x.timeMs) && typeof x.isPersonalBest === 'boolean';
}

export function isSwimmerChampionshipHistoryValid(data: unknown): data is SwimmerChampionshipHistoryData[] {
  if (!Array.isArray(data)) return false;
  return data.every((c) => {
    const x = c as SwimmerChampionshipHistoryData;
    return !!x && typeof x === 'object'
      && isStr(x.eventId) && isStr(x.nameEn)
      && isStr(x.startDate) && isStr(x.endDate) && isStr(x.locationEn)
      && Array.isArray(x.races) && x.races.every(isRace);
  });
}
