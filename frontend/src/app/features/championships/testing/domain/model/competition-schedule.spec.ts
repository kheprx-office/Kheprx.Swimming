import {
  ScheduleDay,
  toScheduleData,
  serializeSchedule,
  raceStatus,
} from '@features/championships/domain/model/competition-schedule';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeDay(overrides: Partial<ScheduleDay> = {}): ScheduleDay {
  return {
    key: 'day-key-1',
    labelEn: 'Day 1',
    labelAr: null,
    dayDate: '2023-11-15',
    races: [
      {
        key: 'race-key-1',
        strokeId: 'st1',
        distanceId: 'ds1',
        scheduledTime: '09:00',
        swimmerIds: ['s1', 's2'],
      },
    ],
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// toScheduleData — key stripping + swimmerIds sort
// ---------------------------------------------------------------------------

describe('toScheduleData', () => {
  it('strips the day-level key from the result', () => {
    const result = toScheduleData([makeDay()]);
    expect((result[0] as any).key).toBeUndefined();
  });

  it('strips the race-level key from the result', () => {
    const result = toScheduleData([makeDay()]);
    expect((result[0].races[0] as any).key).toBeUndefined();
  });

  it('preserves day fields that are not key', () => {
    const result = toScheduleData([makeDay()]);
    expect(result[0].labelEn).toBe('Day 1');
    expect(result[0].dayDate).toBe('2023-11-15');
    expect(result[0].labelAr).toBeNull();
  });

  it('sorts swimmerIds so selection order does not affect dirty comparison', () => {
    const day = makeDay({
      races: [
        {
          key: 'race-key-1',
          strokeId: 'st1',
          distanceId: 'ds1',
          scheduledTime: '09:00',
          swimmerIds: ['s3', 's1', 's2'],
        },
      ],
    });

    const result = toScheduleData([day]);
    expect(result[0].races[0].swimmerIds).toEqual(['s1', 's2', 's3']);
  });

  it('does not mutate the original swimmerIds array', () => {
    const swimmerIds = ['s3', 's1', 's2'];
    const day = makeDay({
      races: [{ key: 'rk', strokeId: 'st1', distanceId: 'ds1', scheduledTime: null, swimmerIds }],
    });
    toScheduleData([day]);
    expect(swimmerIds).toEqual(['s3', 's1', 's2']);
  });
});

// ---------------------------------------------------------------------------
// serializeSchedule — keys + order-independence for dirty tracking
// ---------------------------------------------------------------------------

describe('serializeSchedule', () => {
  it('returns identical strings for two ScheduleDay[] that differ only in key values', () => {
    const a = makeDay({ key: 'key-AAA', races: [{ key: 'rk-AAA', strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00', swimmerIds: ['s1'] }] });
    const b = makeDay({ key: 'key-ZZZ', races: [{ key: 'rk-ZZZ', strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00', swimmerIds: ['s1'] }] });

    expect(serializeSchedule([a])).toBe(serializeSchedule([b]));
  });

  it('returns identical strings for two ScheduleDay[] that differ only in swimmerIds order', () => {
    const a = makeDay({ races: [{ key: 'rk', strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00', swimmerIds: ['s3', 's1', 's2'] }] });
    const b = makeDay({ races: [{ key: 'rk', strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00', swimmerIds: ['s1', 's2', 's3'] }] });

    expect(serializeSchedule([a])).toBe(serializeSchedule([b]));
  });

  it('returns identical strings when both key values and swimmerIds order differ simultaneously', () => {
    const a = makeDay({ key: 'k-A', races: [{ key: 'rk-A', strokeId: 'st1', distanceId: 'ds1', scheduledTime: '10:00', swimmerIds: ['z', 'a', 'm'] }] });
    const b = makeDay({ key: 'k-B', races: [{ key: 'rk-B', strokeId: 'st1', distanceId: 'ds1', scheduledTime: '10:00', swimmerIds: ['a', 'm', 'z'] }] });

    expect(serializeSchedule([a])).toBe(serializeSchedule([b]));
  });

  it('returns different strings when actual data differs', () => {
    const a = makeDay({ dayDate: '2023-11-15' });
    const b = makeDay({ dayDate: '2023-11-16' });

    expect(serializeSchedule([a])).not.toBe(serializeSchedule([b]));
  });
});

// ---------------------------------------------------------------------------
// raceStatus — local wall-clock semantics (matching venue timezone intent)
// ---------------------------------------------------------------------------

describe('raceStatus', () => {
  it('returns "scheduled" when dayDate is empty', () => {
    // Local-time semantics: no date means no scheduled moment — treat as not yet past.
    expect(raceStatus('', '09:00')).toBe('scheduled');
  });

  it('returns "awaitingResults" for a clearly-past date', () => {
    // Uses the browser\'s local/venue wall-clock by design (matching the prototype) —
    // NOT UTC; local time is the intended semantics for a venue-scheduled race.
    expect(raceStatus('2000-01-01', '09:00')).toBe('awaitingResults');
  });

  it('returns "scheduled" for a clearly-future date', () => {
    expect(raceStatus('2999-01-01', '09:00')).toBe('scheduled');
  });

  it('returns "scheduled" when time is null and date is in the future', () => {
    expect(raceStatus('2999-01-01', null)).toBe('scheduled');
  });

  it('returns "awaitingResults" when time is null and date is in the past', () => {
    expect(raceStatus('2000-01-01', null)).toBe('awaitingResults');
  });
});
