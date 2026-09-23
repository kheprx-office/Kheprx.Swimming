import { formatDateRange } from '@features/championships/presentation/pages/championships/format-date-range';

describe('formatDateRange (en)', () => {
  it('collapses a single day', () => {
    expect(formatDateRange('2023-11-15', '2023-11-15', 'en')).toBe('15 Nov 2023');
  });
  it('collapses a same-month range', () => {
    expect(formatDateRange('2023-11-15', '2023-11-16', 'en')).toBe('15–16 Nov 2023');
  });
  it('spans a cross-month range', () => {
    expect(formatDateRange('2023-10-20', '2023-11-05', 'en')).toBe('20 Oct – 5 Nov 2023');
  });
});
