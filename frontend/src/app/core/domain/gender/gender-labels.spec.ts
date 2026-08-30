import { GENDER_LABELS } from '@core/domain/gender';

describe('GENDER_LABELS', () => {
  it('maps gender codes to Arabic labels', () => {
    expect(GENDER_LABELS.male).toBe('ذكر');
    expect(GENDER_LABELS.female).toBe('أنثى');
  });
});
