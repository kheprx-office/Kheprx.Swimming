import { AttendanceRecordDtoRs } from '@features/swimmer-profile/data/dto/attendance-record.dto';
import { AttendanceRecord } from '@features/swimmer-profile/domain/model/attendance-record';

export function toAttendanceRecord(d: AttendanceRecordDtoRs): AttendanceRecord {
  return {
    id: d.id,
    sessionDate: d.sessionDate,
    statusId: d.statusId,
    coachNoteEn: d.coachNoteEn,
    coachNoteAr: d.coachNoteAr,
    recordedByNameEn: d.recordedByNameEn,
    recordedByNameAr: d.recordedByNameAr,
  };
}

export function toAttendanceRecordList(list: AttendanceRecordDtoRs[]): AttendanceRecord[] {
  return list.map(toAttendanceRecord);
}
