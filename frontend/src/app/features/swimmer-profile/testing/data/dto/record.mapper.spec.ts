import { toRecordEntry, toRecordEntryList } from '@features/swimmer-profile/data/dto/record.mapper';
import { RecordDtoRs } from '@features/swimmer-profile/data/dto/record.dto';

const DTO: RecordDtoRs = {
  id: 'o1', swimmerId: 's1', categoryId: 'c1', fieldLabel: 'Penicillin', value: 'Severe',
  observedDate: '2026-09-20T10:00:00Z', recordedBy: 'u1',
};

describe('record.mapper', () => {
  it('maps a record (drops recordedBy)', () => {
    const m = toRecordEntry(DTO);
    expect(m.id).toBe('o1');
    expect(m.categoryId).toBe('c1');
    expect(m.fieldLabel).toBe('Penicillin');
    expect(m.observedDate).toBe('2026-09-20T10:00:00Z');
    expect((m as unknown as Record<string, unknown>).recordedBy).toBeUndefined();
  });

  it('maps a list', () => {
    expect(toRecordEntryList([DTO])).toHaveLength(1);
  });
});
