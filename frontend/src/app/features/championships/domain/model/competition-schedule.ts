// Editor + wire-data model for the Competition Days schedule.

/** Key-less shape used on the wire (load result / save request). */
export interface ScheduleRaceData {
  strokeId: string;
  distanceId: string;
  scheduledTime: string | null; // 'HH:mm' | null
  swimmerIds: string[];
}
export interface ScheduleDayData {
  labelEn: string;
  labelAr: string | null;
  dayDate: string;              // 'YYYY-MM-DD'
  races: ScheduleRaceData[];
}

/** Editor shape carried in the ViewModel — adds a stable client `key` for @for tracking. */
export interface ScheduleRace extends ScheduleRaceData {
  key: string;
}
export interface ScheduleDay extends Omit<ScheduleDayData, 'races'> {
  key: string;
  races: ScheduleRace[];
}

/** Strip client keys + sort swimmerIds so dirty-comparison is order-stable for selections. */
export function toScheduleData(days: ScheduleDay[]): ScheduleDayData[] {
  return days.map((d) => ({
    labelEn: d.labelEn,
    labelAr: d.labelAr,
    dayDate: d.dayDate,
    races: d.races.map((r) => ({
      strokeId: r.strokeId,
      distanceId: r.distanceId,
      scheduledTime: r.scheduledTime,
      swimmerIds: [...r.swimmerIds].sort(),
    })),
  }));
}

/** Canonical string for dirty tracking. */
export function serializeSchedule(days: ScheduleDay[]): string {
  return JSON.stringify(toScheduleData(days));
}

/**
 * Client-derived status pill. A race is 'awaitingResults' once its scheduled start has passed,
 * otherwise 'scheduled'. No results are persisted here — that is a later tab.
 * NOTE: Comparison uses the browser's local/venue wall-clock time by design (matching the prototype) — NOT UTC; local time is the intended semantics for a venue-scheduled race.
 */
export function raceStatus(dayDate: string, time: string | null): 'scheduled' | 'awaitingResults' {
  if (!dayDate) return 'scheduled';
  const stamp = new Date(`${dayDate}T${time || '00:00'}`);
  if (Number.isNaN(stamp.getTime())) return 'scheduled';
  return stamp.getTime() <= Date.now() ? 'awaitingResults' : 'scheduled';
}
