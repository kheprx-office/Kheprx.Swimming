import { parseTimeToMs, formatMsToTime } from '@features/championships/domain/model/race-time';

describe('race-time', () => {
  it('parses mm:ss.SS to milliseconds', () => {
    expect(parseTimeToMs('0:24.56')).toBe(24560);
    expect(parseTimeToMs('1:05.30')).toBe(65300);
    expect(parseTimeToMs('2:08.45')).toBe(128450);
  });

  it('parses ss.SS without a minutes part', () => {
    expect(parseTimeToMs('24.56')).toBe(24560);
    expect(parseTimeToMs('9.1')).toBe(9100); // one decimal → tenths
  });

  it('parses h:mm:ss.SS for races over an hour', () => {
    expect(parseTimeToMs('2:00:00.00')).toBe(7_200_000);       // 2 hours
    expect(parseTimeToMs('1:02:00.00')).toBe(3_720_000);       // 1 hour 2 min
    expect(parseTimeToMs('2:05:33.40')).toBe(7_533_400);       // 2:05:33.40
  });

  it('rejects blank and malformed input', () => {
    expect(parseTimeToMs('')).toBeNull();
    expect(parseTimeToMs('   ')).toBeNull();
    expect(parseTimeToMs('abc')).toBeNull();
    expect(parseTimeToMs('1:99')).toBeNull();   // seconds out of range
    expect(parseTimeToMs('0')).toBeNull();      // zero is not a valid finish time
    expect(parseTimeToMs('1:60:00')).toBeNull(); // minutes out of range when hours present
    expect(parseTimeToMs('2:05:99')).toBeNull(); // seconds out of range
    expect(parseTimeToMs('1:2:3:4')).toBeNull(); // too many parts
  });

  it('formats milliseconds back to m:ss.SS', () => {
    expect(formatMsToTime(24560)).toBe('0:24.56');
    expect(formatMsToTime(65300)).toBe('1:05.30');
  });

  it('formats times of an hour or more as h:mm:ss.SS', () => {
    expect(formatMsToTime(7_200_000)).toBe('2:00:00.00');
    expect(formatMsToTime(3_720_000)).toBe('1:02:00.00');
    expect(formatMsToTime(7_533_400)).toBe('2:05:33.40');
  });

  it('round-trips a parsed value', () => {
    expect(formatMsToTime(parseTimeToMs('1:05.30')!)).toBe('1:05.30');
    expect(formatMsToTime(parseTimeToMs('2:05:33.40')!)).toBe('2:05:33.40');
  });

  it('formats invalid input as empty string', () => {
    expect(formatMsToTime(-1)).toBe('');
    expect(formatMsToTime(NaN)).toBe('');
  });
});
