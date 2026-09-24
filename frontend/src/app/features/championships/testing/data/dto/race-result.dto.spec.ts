import { isResultsDtoValid } from '@features/championships/data/dto/race-result.dto';
import { toRaceResultList } from '@features/championships/data/dto/race-result.mapper';

describe('race-result dto', () => {
  const valid = { results: [{ raceSessionId: 'r1', swimmerId: 's1', timeMs: 24560, points: 0, isPersonalBest: true }] };

  it('accepts a well-formed payload', () => {
    expect(isResultsDtoValid(valid)).toBe(true);
    expect(isResultsDtoValid({ results: [] })).toBe(true);
  });

  it('rejects malformed payloads', () => {
    expect(isResultsDtoValid(null)).toBe(false);
    expect(isResultsDtoValid({})).toBe(false);
    expect(isResultsDtoValid({ results: [{ raceSessionId: 'r1' }] })).toBe(false);
    expect(isResultsDtoValid({ results: [{ raceSessionId: 'r1', swimmerId: 's1', timeMs: 'x', points: 0, isPersonalBest: true }] })).toBe(false);
  });

  it('maps DTO rows to domain entries', () => {
    const rows = toRaceResultList(valid);
    expect(rows).toEqual([{ raceSessionId: 'r1', swimmerId: 's1', timeMs: 24560, points: 0, isPersonalBest: true }]);
  });
});
