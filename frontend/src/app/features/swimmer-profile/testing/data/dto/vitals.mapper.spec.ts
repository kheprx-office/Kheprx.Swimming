import { toVitals } from '@features/swimmer-profile/data/dto/vitals.mapper';
import { isVitalsListValid } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';

const REF = { id: 'f1', code: 'fit', nameEn: 'Fit', nameAr: 'لائق' };
const DTO = { id: 'e1', examDate: '2026-09-19', bloodType: null, hemoglobin: 15, heightCm: 183, weightKg: 75, internalMed: REF, heartAssess: REF, spineAssess: REF };

describe('toVitals', () => {
  it('maps a vitals DTO to the domain model incl. id', () => {
    const v = toVitals(DTO);
    expect(v.id).toBe('e1');
    expect(v.bloodType).toBeNull();
    expect(v.internalMed.nameEn).toBe('Fit');
  });
});

describe('isVitalsListValid', () => {
  it('accepts an array of well-formed vitals', () => {
    expect(isVitalsListValid([DTO])).toBe(true);
  });
  it('rejects non-arrays and malformed items', () => {
    expect(isVitalsListValid(null)).toBe(false);
    expect(isVitalsListValid([{ id: 'e1' }])).toBe(false);
  });
});
