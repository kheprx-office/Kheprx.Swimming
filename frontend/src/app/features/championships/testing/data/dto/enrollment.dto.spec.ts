import { isEnrollmentIdsValid } from '@features/championships/data/dto/enrollment.dto';

describe('enrollment.dto', () => {
  it('accepts an array of strings', () => {
    expect(isEnrollmentIdsValid(['a', 'b'])).toBe(true);
    expect(isEnrollmentIdsValid([])).toBe(true);
  });

  it('rejects non-arrays and non-string members', () => {
    expect(isEnrollmentIdsValid(null)).toBe(false);
    expect(isEnrollmentIdsValid([1, 2])).toBe(false);
    expect(isEnrollmentIdsValid('a')).toBe(false);
  });
});
