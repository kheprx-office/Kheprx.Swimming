import { HealthReadingRowDtoRs } from '@features/health-readings/data/dto/health-reading.dto';
import { HealthReadingListItem } from '@features/health-readings/domain/model/health-reading-list-item';

export function toHealthReadingListItem(d: HealthReadingRowDtoRs): HealthReadingListItem {
  return {
    id: d.id,
    medicalTestId: d.medicalTestId,
    testNameEn: d.testNameEn,
    testNameAr: d.testNameAr,
    unit: d.unit,
    value: d.value,
    lowerBound: d.lowerBound,
    upperBound: d.upperBound,
    readingDate: d.readingDate,
    status: d.status,
  };
}

export function toHealthReadingListItemList(list: HealthReadingRowDtoRs[]): HealthReadingListItem[] {
  return list.map(toHealthReadingListItem);
}
