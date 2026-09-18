import { isClubListValid, isCodedLookupListValid } from '@features/reference/data/dto/reference.dto';

describe('reference.dto validators', () => {
  it('accepts a valid club list', () => {
    expect(isClubListValid([{ id: 'c1', nameEn: 'Al Ahly', nameAr: 'الأهلي' }])).toBe(true);
  });

  it('rejects a club missing id', () => {
    expect(isClubListValid([{ nameEn: 'X', nameAr: null }])).toBe(false);
  });

  it('accepts a valid coded-lookup list', () => {
    expect(isCodedLookupListValid([{ id: 's1', code: 'medley', nameEn: 'IM', nameAr: null }])).toBe(true);
  });

  it('rejects a coded lookup missing code', () => {
    expect(isCodedLookupListValid([{ id: 's1', nameEn: 'IM', nameAr: null }])).toBe(false);
  });

  it('rejects a non-array', () => {
    expect(isClubListValid('nope')).toBe(false);
  });
});
