import { GENDER_LABELS } from '@core/domain/gender';

describe('GENDER_LABELS', () => {
  it('maps gender codes to i18n keys', () => {
    expect(GENDER_LABELS.male).toBe('gender.male');
    expect(GENDER_LABELS.female).toBe('gender.female');
  });
});
