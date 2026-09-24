import { isSwimmerChampionshipHistoryValid } from '@features/championships/data/dto/swimmer-championship-history.dto';
import { toSwimmerChampionshipHistory } from '@features/championships/data/dto/swimmer-championship-history.mapper';

const valid = [{
  eventId: 'e1', nameEn: 'National', nameAr: null,
  startDate: '2023-11-15', endDate: '2023-11-16', locationEn: 'Cairo', locationAr: null,
  races: [{ dayLabelEn: 'Day 1', dayLabelAr: null, distanceId: 'd1', strokeId: 's1', timeMs: 52340, isPersonalBest: true }],
}];

describe('swimmer-championship-history dto', () => {
  it('accepts a well-formed payload', () => {
    expect(isSwimmerChampionshipHistoryValid(valid)).toBe(true);
    expect(isSwimmerChampionshipHistoryValid([])).toBe(true); // empty history is valid
  });

  it('rejects a non-array, a missing races array, and a non-numeric time', () => {
    expect(isSwimmerChampionshipHistoryValid(null)).toBe(false);
    expect(isSwimmerChampionshipHistoryValid([{ ...valid[0], races: undefined }])).toBe(false);
    expect(isSwimmerChampionshipHistoryValid([{ ...valid[0], races: [{ ...valid[0].races[0], timeMs: '52' }] }])).toBe(false);
  });

  it('maps a payload to the domain model preserving fields', () => {
    const model = toSwimmerChampionshipHistory(valid as never);
    expect(model[0].eventId).toBe('e1');
    expect(model[0].races[0].timeMs).toBe(52340);
    expect(model[0].races[0].isPersonalBest).toBe(true);
  });
});
