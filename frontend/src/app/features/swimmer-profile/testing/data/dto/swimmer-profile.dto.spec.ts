import { isSwimmerProfileDtoRsValid } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';

describe('isSwimmerProfileDtoRsValid', () => {
  it('accepts a well-formed identity payload (vitals may be null)', () => {
    const dto = { identity: { id: 's1', uid: 'SW-1', nameEn: 'Ahmed', nameAr: null, dob: '2010-01-01', age: 16, genderCode: 'male', phone: null, trainingClubNameEn: 'Oasis', trainingClubNameAr: null }, vitals: null };
    expect(isSwimmerProfileDtoRsValid(dto)).toBe(true);
  });

  it('rejects a payload missing identity fields', () => {
    expect(isSwimmerProfileDtoRsValid({ identity: { id: 's1' }, vitals: null })).toBe(false);
    expect(isSwimmerProfileDtoRsValid(null)).toBe(false);
  });
});
