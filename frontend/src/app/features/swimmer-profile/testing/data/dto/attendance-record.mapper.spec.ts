import { toAttendanceRecord, toAttendanceRecordList } from '@features/swimmer-profile/data/dto/attendance-record.mapper';
import { AttendanceRecordDtoRs } from '@features/swimmer-profile/data/dto/attendance-record.dto';

const DTO: AttendanceRecordDtoRs = {
  id: 'a1', swimmerId: 's1', sessionDate: '2026-09-09', statusId: 'st1',
  coachNoteEn: 'Excused', coachNoteAr: 'بعذر', recordedBy: 'u1',
  recordedByNameEn: 'Coach Layla', recordedByNameAr: 'الكابتن ليلى',
};

describe('attendance-record.mapper', () => {
  it('maps a record (keeps status + notes + recorder names, drops ids we do not render)', () => {
    const m = toAttendanceRecord(DTO);
    expect(m.id).toBe('a1');
    expect(m.sessionDate).toBe('2026-09-09');
    expect(m.statusId).toBe('st1');
    expect(m.coachNoteEn).toBe('Excused');
    expect(m.recordedByNameEn).toBe('Coach Layla');
    expect((m as unknown as Record<string, unknown>).swimmerId).toBeUndefined();
    expect((m as unknown as Record<string, unknown>).recordedBy).toBeUndefined();
  });

  it('maps a list', () => {
    expect(toAttendanceRecordList([DTO])).toHaveLength(1);
  });
});
