// Read + view models for the Finished races and Results tabs.

export interface RaceResultEntry {
  raceSessionId: string;
  swimmerId: string;
  timeMs: number;
  points: number;
  isPersonalBest: boolean;
}

// Id-carrying schedule read model. (The Days editor model in competition-schedule.ts intentionally
// drops server ids; Finished/Results need them to match results and target the results PUT.)
export interface RaceScheduleRace {
  id: string;
  strokeId: string;
  distanceId: string;
  scheduledTime: string | null; // 'HH:mm' | null
  swimmerIds: string[];
}
export interface RaceScheduleDay {
  id: string;
  labelEn: string;
  labelAr: string | null;
  dayDate: string;              // 'YYYY-MM-DD'
  races: RaceScheduleRace[];
}

// Card view-models the template renders.
export interface FinishedRaceCard {
  raceSessionId: string;
  raceName: string;
  dayLabel: string;
  scheduledTime: string | null;
  swimmers: { id: string; name: string }[];
}
export interface ResultsRaceEntry {
  swimmerName: string;
  timeMs: number;
  rank: number;
  isPersonalBest: boolean;
}
export interface ResultsRaceCard {
  raceSessionId: string;
  raceName: string;
  dayLabel: string;
  entries: ResultsRaceEntry[];
}
