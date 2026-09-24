import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface RaceResultData {
  raceSessionId: string;
  swimmerId: string;
  timeMs: number;
  points: number;
  isPersonalBest: boolean;
}
export interface ResultsData { results: RaceResultData[]; }
export interface ResultsDtoRs extends BaseResponseRs<ResultsData> {}

/** PUT body — the finish times for one race (server replaces that race's results atomically). */
export interface SetRaceResultsRq {
  entries: { swimmerId: string; timeMs: number }[];
}

function isStr(v: unknown): v is string { return typeof v === 'string'; }
function isNum(v: unknown): v is number { return typeof v === 'number' && Number.isFinite(v); }

export function isResultsDtoValid(data: unknown): data is ResultsData {
  const d = data as ResultsData;
  if (!d || typeof d !== 'object' || !Array.isArray(d.results)) return false;
  return d.results.every(
    (r) =>
      r != null && typeof r === 'object' &&
      isStr(r.raceSessionId) && isStr(r.swimmerId) &&
      isNum(r.timeMs) && isNum(r.points) && typeof r.isPersonalBest === 'boolean',
  );
}
