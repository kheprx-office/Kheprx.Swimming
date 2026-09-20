import { RecordDtoRs } from '@features/swimmer-profile/data/dto/record.dto';
import { RecordEntry } from '@features/swimmer-profile/domain/model/record-entry';

export function toRecordEntry(d: RecordDtoRs): RecordEntry {
  return {
    id: d.id,
    swimmerId: d.swimmerId,
    categoryId: d.categoryId,
    fieldLabel: d.fieldLabel,
    value: d.value,
    observedDate: d.observedDate,
  };
}

export function toRecordEntryList(list: RecordDtoRs[]): RecordEntry[] {
  return list.map(toRecordEntry);
}
