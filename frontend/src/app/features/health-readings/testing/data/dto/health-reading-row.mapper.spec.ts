import { toHealthReadingListItem, toHealthReadingListItemList } from '@features/health-readings/data/dto/health-reading-row.mapper';
import { HealthReadingRowDtoRs } from '@features/health-readings/data/dto/health-reading.dto';

const ROW: HealthReadingRowDtoRs = {
  id: 'r1', medicalTestId: 't1', testNameEn: 'Glucose', testNameAr: 'الجلوكوز', unit: 'mg/dL',
  value: 90, lowerBound: 70, upperBound: 110, readingDate: '2024-10-04T00:00:00Z', status: 'normal',
};

describe('health-reading-row.mapper', () => {
  it('maps a row DTO to the model', () => {
    const m = toHealthReadingListItem(ROW);
    expect(m).toEqual({
      id: 'r1', medicalTestId: 't1', testNameEn: 'Glucose', testNameAr: 'الجلوكوز', unit: 'mg/dL',
      value: 90, lowerBound: 70, upperBound: 110, readingDate: '2024-10-04T00:00:00Z', status: 'normal',
    });
  });

  it('maps a list', () => {
    expect(toHealthReadingListItemList([ROW, ROW])).toHaveLength(2);
  });
});
