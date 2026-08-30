import { businessDaysBetween } from '@core/util/business-days';

describe('businessDaysBetween', () => {
  it('counts Sun–Thu inclusive', () => {
    expect(businessDaysBetween('2026-07-19', '2026-07-23')).toBe(5); // Sun..Thu
  });
  it('excludes Fri+Sat', () => {
    expect(businessDaysBetween('2026-07-19', '2026-07-25')).toBe(5); // full week
    expect(businessDaysBetween('2026-07-24', '2026-07-25')).toBe(0); // Fri..Sat
  });
  it('is inclusive of a single billable day', () => {
    expect(businessDaysBetween('2026-07-19', '2026-07-19')).toBe(1);
  });
  it('returns 0 for empty or reversed ranges', () => {
    expect(businessDaysBetween('', '2026-07-23')).toBe(0);
    expect(businessDaysBetween('2026-07-23', '2026-07-19')).toBe(0);
  });
});
