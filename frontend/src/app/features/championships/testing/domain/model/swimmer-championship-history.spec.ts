import { resolveRaceName } from '@features/championships/domain/model/swimmer-championship-history';

const distances = [{ id: 'd1', nameEn: '50m', nameAr: '٥٠م' }];
const strokes = [{ id: 's1', nameEn: 'Freestyle', nameAr: 'حرة' }];

describe('resolveRaceName', () => {
  it('joins distance and stroke in the active language', () => {
    expect(resolveRaceName(distances, strokes, 'd1', 's1', 'en')).toBe('50m Freestyle');
    expect(resolveRaceName(distances, strokes, 'd1', 's1', 'ar')).toBe('٥٠م حرة');
  });

  it('degrades gracefully when a lookup id is missing (no crash)', () => {
    expect(resolveRaceName(distances, strokes, 'd1', 'unknown', 'en')).toBe('50m');
    expect(resolveRaceName([], [], 'd1', 's1', 'en')).toBe('');
  });
});
