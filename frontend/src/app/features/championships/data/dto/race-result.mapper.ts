import { ResultsData } from '@features/championships/data/dto/race-result.dto';
import { RaceResultEntry } from '@features/championships/domain/model/race-result';

export function toRaceResultList(dto: ResultsData): RaceResultEntry[] {
  return dto.results.map((r) => ({
    raceSessionId: r.raceSessionId,
    swimmerId: r.swimmerId,
    timeMs: r.timeMs,
    points: r.points,
    isPersonalBest: r.isPersonalBest,
  }));
}
